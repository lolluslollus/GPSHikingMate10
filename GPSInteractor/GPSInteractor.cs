using LolloGPS.Data;
using LolloGPS.Data.Constants;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Utilz;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.Core;
using Windows.Devices.Geolocation;
using Windows.Foundation;

namespace LolloGPS.GPSInteraction
{
	public sealed class GPSInteractor : OpenableObservableData
	{
		#region properties
		private IGPSDataModel _myPersistentData = null;
		private Geolocator _geolocator = null;
		private CancellationTokenSource _getLocationCts = null;

		private bool _isGpsWorking = false;
		public bool IsGPSWorking { get { return _isGpsWorking; } private set { _isGpsWorking = value; RaisePropertyChanged_UI(); } }

		private volatile IBackgroundTaskRegistration _getlocBkgTask = null;
		#endregion properties


		#region lifecycle
		private static GPSInteractor _instance;
		private static readonly object _instanceLock = new object();
		public static GPSInteractor GetInstance(IGPSDataModel persistentData)
		{
			lock (_instanceLock)
			{
				if (_instance == null)
				{
					_instance = new GPSInteractor(persistentData);
				}
				return _instance;
			}
		}
		private GPSInteractor(IGPSDataModel persistentData)
		{
			_myPersistentData = persistentData;
		}
		protected override async Task OpenMayOverrideAsync()
		{
			await Task.Run(async delegate
			{
				AddHandlers_DataModelPropertyChanged();
				await TryUpdateBackgroundPropsAfterPropsChanged().ConfigureAwait(false);
				UpdateForegroundTrkProps(false);
			}).ConfigureAwait(false);

		}

		protected override Task CloseMayOverrideAsync()
		{
			RemoveHandlers_DataModelPropertyChanged();
			RemoveHandlers_GeoLocator();
			RemoveHandlers_GetLocBackgroundTask();

			CancelPendingTasks(); // after removing the handlers
			return Task.CompletedTask;
		}

		private void CancelPendingTasks()
		{
			_getLocationCts?.Cancel();
			//_getLocationCts.Dispose(); This is done in the exception handler that catches the IsCanceled exception. If you do it here, the exception handler will throw an ObjectDisposed exception
			//_getLocationCts = null;
		}
		#endregion lifecycle


		#region event handling
		private bool _isGeoLocatorHandlersActive = false;
		private bool _isDataModelHandlersActive = false;
		private bool _isGetLocTaskHandlersActive = false;

		private void OnPersistentData_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			// use semaphores to deal with interlinked properties, which contain cross references.
			// semaphores must be async so they don't block the UI thread.
			// these operations can be cancelled by disposing and rector'ing their semaphores. This may cause exceptions, which are silently caught below.
			// In general, beware of this deadlock trap with semaphores: enter a non-async semaphore, call the same method recursively, you will lock the thread forever.
			// This is an awaitable semaphore, so it does not make trouble.

			Task upd = RunFunctionIfOpenAsyncT(async delegate
			{
				if (e.PropertyName == nameof(PersistentData.DesiredAccuracyInMeters)
				|| e.PropertyName == nameof(PersistentData.ReportIntervalInMilliSec))
				//|| e.PropertyName == "MovementThresholdInMetres" || e.PropertyName == "PositAccuracy") // no! either these or the ones I use, they are conflicting
				{
					RemoveHandlers_GeoLocator(); // must reinit the geolocator whenever I change these props
					_geolocator = null;
					UpdateForegroundTrkProps(true);
				}
				else if (e.PropertyName == nameof(PersistentData.IsTracking))
				{
					UpdateForegroundTrkProps(true);
				}
				else if (e.PropertyName == nameof(PersistentData.BackgroundUpdatePeriodInMinutes))
				{
					if (_myPersistentData.IsBackgroundEnabled)
					{
						CloseGetLocBackgroundTask_All(); // must reinit whenever I change these props
						await TryUpdateBackgroundPropsAfterPropsChanged().ConfigureAwait(false); // and reregister with the new value (if registration is possible at this time)
					}
				}
				else if (e.PropertyName == nameof(PersistentData.IsBackgroundEnabled))
				{
					bool isOk = await TryUpdateBackgroundPropsAfterPropsChanged().ConfigureAwait(false);
					// if just switched on and ok, get a location now. The foreground tracking does it, so we do it here as well
					if (isOk && _myPersistentData.IsBackgroundEnabled)
					{
						var loc = await GetGeoLocationAppendingHistoryAsync().ConfigureAwait(false);
					}
				}
			});
		}

		private void AddHandlers_DataModelPropertyChanged()
		{
			if (!_isDataModelHandlersActive)
			{
				if (_myPersistentData != null)
				{
					_myPersistentData.PropertyChanged += OnPersistentData_PropertyChanged;
					_isDataModelHandlersActive = true;
				}
			}
		}
		private void RemoveHandlers_DataModelPropertyChanged()
		{
			if (_myPersistentData != null)
			{
				_myPersistentData.PropertyChanged -= OnPersistentData_PropertyChanged;
				_isDataModelHandlersActive = false;
			}
		}

		private void AddHandlers_GeoLocator()
		{
			if (!_isGeoLocatorHandlersActive)
			{
				_isGeoLocatorHandlersActive = true;
				_geolocator.PositionChanged += OnGeolocator_PositionChangedAsync;
				//_geolocator.StatusChanged += OnGeolocator_StatusChangedAsync;
			}
		}

		private void RemoveHandlers_GeoLocator()
		{
			if (_geolocator != null)
			{
				_geolocator.PositionChanged -= OnGeolocator_PositionChangedAsync;
				//_geolocator.StatusChanged -= OnGeolocator_StatusChangedAsync;
				_isGeoLocatorHandlersActive = false;
			}
		}

		private void AddHandlers_GetLocBackgroundTask()
		{
			if (!_isGetLocTaskHandlersActive)
			{
				if (_getlocBkgTask != null)
				{
					GetLocBackgroundTaskSemaphoreManager.TryWait();
					_getlocBkgTask.Completed += new BackgroundTaskCompletedEventHandler(OnGetLocBackgroundTaskCompleted);
					_isGetLocTaskHandlersActive = true;
				}
			}
		}

		private void RemoveHandlers_GetLocBackgroundTask()
		{
			if (_getlocBkgTask != null)
			{
				_getlocBkgTask.Completed -= OnGetLocBackgroundTaskCompleted;
				_isGetLocTaskHandlersActive = false;
			}
			GetLocBackgroundTaskSemaphoreManager.Release();
		}
		#endregion event handling

		#region foreground tracking
		private void OnGeolocator_PositionChangedAsync(Geolocator sender, PositionChangedEventArgs e)
		{
			Task ooo = RunFunctionIfOpenAsyncT(async delegate
			{
				try
				{
					IsGPSWorking = true;

					if (e != null)
					{
						var newDataRecord = GetNewHistoryRecord(e.Position);
						await _myPersistentData.AddHistoryRecordAsync(newDataRecord, false).ConfigureAwait(false);
					}
				}
				catch (Exception exc)
				{
					SetLastMessage_UI(exc.Message);
					Logger.Add_TPL("OnGeolocator_PositionChangedAsync threw " + exc.ToString(), Logger.ForegroundLogFilename, Logger.Severity.Error, false);
				}
				finally
				{
					IsGPSWorking = false;
				}
			});
		}

		private void UpdateForegroundTrkProps(bool setUserMessage)
		{
			if (_geolocator == null)
			{
				_geolocator = new Geolocator()
				{
					DesiredAccuracyInMeters = _myPersistentData.DesiredAccuracyInMeters,
					ReportInterval = _myPersistentData.ReportIntervalInMilliSec,
					//MovementThreshold = _myPersistentData.MovementThresholdInMetres, // no! either these props or the ones I use, they are conflicting sets
					//DesiredAccuracy = _myPersistentData.PositAccuracy, // no! either these props or the ones I use, they are conflicting sets
				};
				// Only with windows phone: You must set the MovementThreshold for 
				// distance-based tracking or ReportInterval for
				// periodic-based tracking before adding event handlers.
			}

			if (_myPersistentData.IsTracking)
			{
				AddHandlers_GeoLocator();
				if (setUserMessage) SetLastMessage_UI("Tracking on, waiting for update...");
			}
			else
			{
				RemoveHandlers_GeoLocator();
				if (setUserMessage) SetLastMessage_UI("Tracking off");
			}
		}
		#endregion foreground tracking


		#region background task
		private static IBackgroundTaskRegistration GetTaskIfAlreadyRegistered()
		{
			foreach (var cur in BackgroundTaskRegistration.AllTasks)
			{
				if (cur.Value.Name == ConstantData.GET_LOCATION_BACKGROUND_TASK_NAME)
				{
					return cur.Value;
				}
			}
			return null;
		}

		private async Task<Tuple<bool, string>> TryOpenGetLocBackgroundTaskAsync()
		{
			bool isOk = false;
			string msg = string.Empty;

			string errorMsg = string.Empty;
			BackgroundAccessStatus backgroundAccessStatus = BackgroundAccessStatus.Unspecified;

			_getlocBkgTask = GetTaskIfAlreadyRegistered();

			if (_getlocBkgTask == null) // bkg task not registered yet: register it
			{
				try
				{
					//maniman
					CloseGetLocBackgroundTask_All();

					// Get permission for a background task from the user. If the user has already answered once,
					// this does nothing and the user must manually update their preference via PC Settings.
					backgroundAccessStatus = await BackgroundExecutionManager.RequestAccessAsync().AsTask().ConfigureAwait(false);

					// Regardless of the answer, register the background task. If the user later adds this application
					// to the lock screen, the background task will be ready to run.
					// Create a new background task builder
					BackgroundTaskBuilder geolocTaskBuilder = new BackgroundTaskBuilder()
					{
						Name = ConstantData.GET_LOCATION_BACKGROUND_TASK_NAME,
						TaskEntryPoint = ConstantData.GET_LOCATION_BACKGROUND_TASK_ENTRY_POINT
					};

					// Create a new timer triggering at a 15 minute interval 
					// and associate it with the background task builder
					// Less than 15 will throw an exception, this is how this class works
					geolocTaskBuilder.SetTrigger(new TimeTrigger(_myPersistentData.BackgroundUpdatePeriodInMinutes, false));

					// Register the background task
					_getlocBkgTask = geolocTaskBuilder.Register();
				}
				catch (Exception ex)
				{
					errorMsg = ex.ToString();
					backgroundAccessStatus = BackgroundAccessStatus.Denied;
					Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
				}
			}
			else // bkg task registered: see if it is ok
			{
				try
				{
					backgroundAccessStatus = BackgroundExecutionManager.GetAccessStatus();
				}
				catch (Exception ex)
				{
					errorMsg = ex.ToString();
					backgroundAccessStatus = BackgroundAccessStatus.Denied;
					Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
				}
			}

			switch (backgroundAccessStatus)
			{
				case BackgroundAccessStatus.Unspecified:
					RemoveHandlers_GetLocBackgroundTask();
					msg = "Cannot run in background, enable it in the \"Battery Saver\" app";
					break;
				case BackgroundAccessStatus.Denied:
					RemoveHandlers_GetLocBackgroundTask();
					if (string.IsNullOrWhiteSpace(errorMsg)) msg = "Cannot run in background, enable it in Settings - Privacy - Background apps";
					else msg = errorMsg;
					break;
				default:
					AddHandlers_GetLocBackgroundTask();
					msg = "Background task on";
					isOk = true;
					break;
			}

			return Tuple.Create(isOk, msg);
		}

		private void CloseGetLocBackgroundTask_Current()
		{
			if (_getlocBkgTask != null)
			{
				RemoveHandlers_GetLocBackgroundTask();
				_getlocBkgTask.Unregister(true);
				_getlocBkgTask = null;
			}
		}

		private void CloseGetLocBackgroundTask_All()
		{
			CloseGetLocBackgroundTask_Current();

			// LOLLO BEGIN new
			var allBkgTasks = BackgroundTaskRegistration.AllTasks.Values.ToList(); // clone
			foreach (var item in allBkgTasks)
			{
				if (item.Name == ConstantData.GET_LOCATION_BACKGROUND_TASK_NAME)
				{
					_getlocBkgTask = item;
					CloseGetLocBackgroundTask_Current();
				}
			}
			// LOLLO END new

			// LOLLO BEGIN old
			//foreach (var item in BackgroundTaskRegistration.AllTasks)
			//         {
			//             if (item.Value.Name == ConstantData.GET_LOCATION_BACKGROUND_TASK_NAME)
			//             {
			//                 _getlocTask = item.Value;
			//                 UnregisterGetLocBackgroundTask_Current();
			//             }
			//         }
			// LOLLO END old
		}

		private void OnGetLocBackgroundTaskCompleted(IBackgroundTaskRegistration sender, BackgroundTaskCompletedEventArgs e)
		{
			Debug.WriteLine("BackgroundTask completed, event caught");
			Task getLoc = GetGeoLocationAppendingHistoryAsync();
		}

		private async Task<bool> TryUpdateBackgroundPropsAfterPropsChanged()
		{
			if (_myPersistentData.IsBackgroundEnabled)
			{
				Tuple<bool, string> result = await TryOpenGetLocBackgroundTaskAsync().ConfigureAwait(false);
				// in case of failure (eg the user revoked background permissions when the app was suspended or off), reset the variables
				if (!result.Item1)
				{
					CloseGetLocBackgroundTask_All();
					// notify the user
					if (!string.IsNullOrWhiteSpace(result.Item2)) SetLastMessage_UI(result.Item2);
					// BODGE change the value back to false in a deferred cycle, otherwise the control won't update, even if the property will.
					// the trouble seems to lie in the ToggleSwitch style: both mine and MS default don't work.
					// it could also be a problem with the binding engine, which I have seen already: you cannot change a property twice within one cycle.
					// In any case, this change must take place on the UI thread, so no issue really.
					IAsyncAction qqq = CoreApplication.MainView.CoreWindow.Dispatcher.RunIdleAsync((a) => _myPersistentData.IsBackgroundEnabled = false);
				}
				return result.Item1;
			}
			else
			{
				CloseGetLocBackgroundTask_All();
				return true;
			}
		}
		#endregion background task


		#region services
		/// <summary>
		/// Gets the current geolocation and appends the results to History
		/// </summary>
		/// <returns></returns>
		public async Task<PointRecord> GetGeoLocationAppendingHistoryAsync()
		{
			PointRecord result = null;
			await RunFunctionIfOpenAsyncT(async delegate
			{
				IsGPSWorking = true;
				SetLastMessage_UI("getting current location...");

				_getLocationCts = new CancellationTokenSource();
				CancellationToken token = _getLocationCts.Token;
				try
				{
					if (_geolocator != null)
					{
						var pos = await _geolocator.GetGeopositionAsync().AsTask(token).ConfigureAwait(false);
						var newDataRecord = GetNewHistoryRecord(pos);
						token.ThrowIfCancellationRequested();
						if (await _myPersistentData.AddHistoryRecordAsync(newDataRecord, false).ConfigureAwait(false)) result = newDataRecord;
					}
				}
				catch (UnauthorizedAccessException)
				{
					result = null;
					SetLastMessage_UI("Give the app permission to access your location (Settings - Privacy - Location)");
				}
				catch (TaskCanceledException) // inherits from OperationCanceled
				{
					result = null;
					SetLastMessage_UI("location acquisition cancelled");
				}
				catch (Exception ex)
				{
					result = null;
					SetLastMessage_UI("cannot get location");
					await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename).ConfigureAwait(false);
				}
				finally
				{
					_getLocationCts?.Dispose();
					_getLocationCts = null;
					//if (result != null)
					//{
					//	await RunInUiThreadAsync(delegate
					//	{
					//		_myPersistentData?.SelectCurrentHistoryRecord(); // LOLLO TODO why?
					//	}).ConfigureAwait(false);
					//}
					IsGPSWorking = false;
				}
			}).ConfigureAwait(false);
			return result;
		}

		/// <summary>
		/// Only call this from a task, which is not the main one (eg call it from the background task).
		/// Otherwise, you may screw things up.
		/// </summary>
		/// <param name="token"></param>
		/// <returns></returns>
		public static async Task<bool> GetGeoLocationAppendingHistoryStaticAsync(CancellationToken token)
		{
			bool isSaved = false;
			// note that PersistentData.GetInstance() is always an empty instance, because i am in a separate process
			// and I took away the following to save performance and memory (max 40 MB is allowed in background tasks)
			// SuspensionManager.LoadDataAsync(_myData, false).Wait(token); //read the last saved settings and the history from the db, skipping the route.

			//this takes time
			Geolocator geolocator = new Geolocator() { DesiredAccuracyInMeters = PersistentData.DefaultDesiredAccuracyInMetres }; //, ReportInterval = myDataModel.ReportIntervalInMilliSec };

			token.ThrowIfCancellationRequested();

			if (geolocator != null)
			{
				var pos = await geolocator.GetGeopositionAsync().AsTask(token).ConfigureAwait(false);
				Debug.WriteLine("GetLocationBackgroundTask done getting geoposition");

				token.ThrowIfCancellationRequested();

				// save to the db, synchronously, otherwise the background task may be cancelled before the db is updated.
				// this would fail to save the new value and leave around named semaphores, which block everything else.
				if (pos != null)
				{
					isSaved = PersistentData.RunDbOpInOtherTask(delegate
					{
						var newDataRecord = GetNewHistoryRecord(pos);
						return PersistentData.AddHistoryRecordOnlyDb(newDataRecord, true);
					});
				}
			}
			return isSaved;
		}
		//public static bool AppendGeoPositionOnlyDb(PersistentData persistentData, Geoposition pos, bool checkMaxEntries)
		//{
		//	PointRecord newDataRecord = new PointRecord();
		//	InitNewHistoryRecord(pos, newDataRecord);
		//	bool isOk = persistentData.AddHistoryRecordOnlyDb(newDataRecord, checkMaxEntries);
		//	return isOk;
		//}

		private static PointRecord GetNewHistoryRecord(Geoposition pos)
		{
			PointRecord newDataRecord = null;
			if (pos != null)
			{
				newDataRecord = new PointRecord();
				newDataRecord.Latitude = pos.Coordinate.Point.Position.Latitude;
				newDataRecord.Longitude = pos.Coordinate.Point.Position.Longitude;
				newDataRecord.Altitude = pos.Coordinate.Point.Position.Altitude;
				newDataRecord.Accuracy = pos.Coordinate.Accuracy;
				newDataRecord.AltitudeAccuracy = pos.Coordinate.AltitudeAccuracy.GetValueOrDefault();// == null ? default(double) : pos.Coordinate.AltitudeAccuracy;
				newDataRecord.PositionSource = pos.Coordinate.PositionSource.ToString();
				newDataRecord.TimePoint = pos.Coordinate.Timestamp.DateTime;
				newDataRecord.SpeedInMetreSec = pos.Coordinate.Speed ?? default(double);
			}
			return newDataRecord;
		}

		private void SetLastMessage_UI(string message)
		{
			if (_myPersistentData != null) _myPersistentData.LastMessage = message;
		}
		#endregion services
	}
}