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
using Windows.UI.Core;

namespace LolloGPS.GPSInteraction
{
	public sealed class GPSInteractor : ObservableData
	{
		private PersistentData _myPersistentData = null;
		private Geolocator _geolocator = null;
		private static SemaphoreSlimSafeRelease _trackingPropsSemaphore = null;
		private CancellationTokenSource _getLocationCts = null;

		private bool _isGpsWorking = false;
		public bool IsGPSWorking { get { return _isGpsWorking; } private set { _isGpsWorking = value; RaisePropertyChanged_UI(); } }

		private volatile IBackgroundTaskRegistration _getlocBkgTask = null;

		#region construct and dispose
		public GPSInteractor(PersistentData persistentData)
		{
			_myPersistentData = persistentData;
			_geolocator = new Geolocator()
			{
				DesiredAccuracyInMeters = _myPersistentData.DesiredAccuracyInMeters,
				ReportInterval = _myPersistentData.ReportIntervalInMilliSec,
				//MovementThreshold = _myPersistentData.MovementThresholdInMetres,
				//DesiredAccuracy = _myPersistentData.PositAccuracy,
			};

			// Only with windows phone: You must set the MovementThreshold for 
			// distance-based tracking or ReportInterval for
			// periodic-based tracking before adding event handlers.
		}
		public async Task OpenAsync()
		{
			try
			{
				if (!SemaphoreSlimSafeRelease.IsAlive(_trackingPropsSemaphore)) _trackingPropsSemaphore = new SemaphoreSlimSafeRelease(1, 1);
				AddHandlers_DataModelPropertyChanged();

				await Task.Run(async delegate
				{
					try
					{
						await _trackingPropsSemaphore.WaitAsync().ConfigureAwait(false);
						await TryUpdateBackgroundPropsAfterPropsChanged().ConfigureAwait(false);
						UpdateForegroundPropsAfterIsTrackingChanged(false);
					}
					catch (Exception ex)
					{
						if (SemaphoreSlimSafeRelease.IsAlive(_trackingPropsSemaphore))
							Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
					}
					finally
					{
						SemaphoreSlimSafeRelease.TryRelease(_trackingPropsSemaphore);
					}
				}).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename).ConfigureAwait(false);
			}
		}
		public void Close()
		{
			RemoveHandlers_GeoLocator();
			RemoveHandlers_DataModelPropertyChanged();
			RemoveHandlers_GetLocBackgroundTask();
			CancelPendingTasks(); // after removing the handlers
		}
		private void CancelPendingTasks()
		{
			_getLocationCts?.Cancel();
			//_getLocationCts.Dispose(); This is done in the exception handler that catches the IsCanceled exception. If you do it here, the exception handler will throw an ObjectDisposed exception
			//_getLocationCts = null;
			SemaphoreSlimSafeRelease.TryDispose(_trackingPropsSemaphore);
		}
		#endregion construct and dispose

		#region event handling
		private bool _isGeoLocatorHandlersActive = false;
		private bool _isDataModelHandlersActive = false;
		private bool _isGetLocTaskHandlersActive = false;

		private async void OnPersistentData_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			// use semaphores to deal with interlinked properties, which contain cross references.
			// semaphores must be async so they don't block the UI thread.
			// these operations can be cancelled by disposing and rector'ing their semaphores. This may cause exceptions, which are silently caught below.
			// In general, beware of this deadlock trap with semaphores: enter a non-async semaphore, call the same method recursively, you will lock the thread forever.
			// This is an awaitable semaphore, so it does not make trouble.

			try
			{
				await _trackingPropsSemaphore.WaitAsync().ConfigureAwait(false); // both properties affect the same parameter, so I need a semaphore.

				if (e.PropertyName == nameof(PersistentData.DesiredAccuracyInMeters)
				|| e.PropertyName == nameof(PersistentData.ReportIntervalInMilliSec))
				//|| e.PropertyName == "MovementThresholdInMetres" || e.PropertyName == "PositAccuracy") // no! either these or the ones I use, they are conflicting
				{
					if (_geolocator != null)
					{
						RemoveHandlers_GeoLocator();
						_geolocator = new Geolocator()
						{
							DesiredAccuracyInMeters = _myPersistentData.DesiredAccuracyInMeters,
							ReportInterval = _myPersistentData.ReportIntervalInMilliSec,
						};
						if (_myPersistentData.IsTracking) AddHandlers_GeoLocator();
					}
				}
				else if (e.PropertyName == nameof(PersistentData.IsTracking))
				{
					UpdateForegroundPropsAfterIsTrackingChanged(true);
				}
				else if (e.PropertyName == nameof(PersistentData.BackgroundUpdatePeriodInMinutes)
				|| e.PropertyName == nameof(PersistentData.IsBackgroundEnabled))
				{
					// LOLLO the semaphore.Wait used to be here, it now encircles the whole method
					// both properties affect the same parameter, so I need a semaphore. 
					// In fact, all properties are interlinked, so I put all in a semaphore.
					if (e.PropertyName == nameof(PersistentData.BackgroundUpdatePeriodInMinutes))
					{
						if (_myPersistentData.IsBackgroundEnabled)
						{
							UnregisterGetLocBackgroundTask_All(); // need to deregister
							await TryUpdateBackgroundPropsAfterPropsChanged().ConfigureAwait(false); // and reregister with the new value (if registration is possible at this time)
						}
					}
					else if (e.PropertyName == nameof(PersistentData.IsBackgroundEnabled))
					{
						bool isOk = await TryUpdateBackgroundPropsAfterPropsChanged().ConfigureAwait(false);
						// if just switched on and ok, get a location now. The foreground tracking does it, so we do it here as well
						if (isOk && _myPersistentData.IsBackgroundEnabled)
						{
							var loc = await GetGeoLocationAsync().ConfigureAwait(false);
						}
					}
				}
			}
			catch (Exception ex)
			{
				if (SemaphoreSlimSafeRelease.IsAlive(_trackingPropsSemaphore))
					Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(_trackingPropsSemaphore);
			}
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
				_geolocator.PositionChanged += OnGeolocator_PositionChangedAsync;
				//_geolocator.StatusChanged += OnGeolocator_StatusChangedAsync;
				_isGeoLocatorHandlersActive = true;
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
		private void StartTracking(bool setUserMessage)
		{
			//this method runs in a UI thread
			if (setUserMessage) SetLastMessage_UI("Tracking on, waiting for update...");
			AddHandlers_GeoLocator();
		}

		private void StopTracking(bool setUserMessage)
		{
			//this method runs in a UI thread
			if (setUserMessage) SetLastMessage_UI("Tracking off");
			RemoveHandlers_GeoLocator();
		}

		private volatile bool _isRespondingToPositionChanged = false;
		private async void OnGeolocator_PositionChangedAsync(Geolocator sender, PositionChangedEventArgs e)
		{
			if (_isRespondingToPositionChanged) return; // get out if another request is already pending

			try
			{
				_isRespondingToPositionChanged = true;
				IsGPSWorking = true;

				if (e != null)
				{
					Geoposition pos = e.Position;
					await AppendGeoPositionAsync(_myPersistentData, pos, false).ConfigureAwait(false);
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
				_isRespondingToPositionChanged = false;
			}
		}

		private void UpdateForegroundPropsAfterIsTrackingChanged(bool setUserMessage)
		{
			if (_myPersistentData.IsTracking)
			{
				StartTracking(setUserMessage);
			}
			else
			{
				StopTracking(setUserMessage);
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

		private async Task<Tuple<bool, string>> TryActivateGetLocBackgroundTaskAsync()
		{
			// this method can run in any thread

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
					UnregisterGetLocBackgroundTask_All();

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

		private void UnregisterGetLocBackgroundTask_Current()
		{
			if (_getlocBkgTask != null)
			{
				RemoveHandlers_GetLocBackgroundTask();
				_getlocBkgTask.Unregister(true);
				_getlocBkgTask = null;
			}
		}

		private void UnregisterGetLocBackgroundTask_All()
		{
			UnregisterGetLocBackgroundTask_Current();

			// LOLLO BEGIN new
			var allBkgTasks = BackgroundTaskRegistration.AllTasks.Values.ToList(); // clone
			foreach (var item in allBkgTasks)
			{
				if (item.Name == ConstantData.GET_LOCATION_BACKGROUND_TASK_NAME)
				{
					_getlocBkgTask = item;
					UnregisterGetLocBackgroundTask_Current();
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
			Task getLoc = GetGeoLocationAsync();
		}

		private async Task<bool> TryUpdateBackgroundPropsAfterPropsChanged()
		{
			if (_myPersistentData.IsBackgroundEnabled)
			{
				Tuple<bool, string> result = await TryActivateGetLocBackgroundTaskAsync().ConfigureAwait(false);
				// in case of failure (eg the user revoked background permissions when the app was suspended or off), reset the variables
				if (!result.Item1)
				{
					UnregisterGetLocBackgroundTask_All();
				}
				// notify the user of the failure
				if (!result.Item1 && !string.IsNullOrWhiteSpace(result.Item2))
				{
					SetLastMessage_UI(result.Item2);
				}
				// BODGE change the value back to false in a deferred cycle, otherwise the control won't update, even if the property will.
				// the trouble seems to lie in the ToggleSwitch style: both mine and MS default don't work.
				// it could also be a problem with the binding engine, which I have seen already: you cannot change a property twice within one cycle.
				// In any case, this change must take place on the UI thread, so no issue really.
				if (!result.Item1)
				{
					IAsyncAction qqq = CoreApplication.MainView.CoreWindow.Dispatcher.RunIdleAsync((a) => _myPersistentData.IsBackgroundEnabled = false);
				}
				return result.Item1;
			}
			else
			{
				UnregisterGetLocBackgroundTask_All();
				return true;
			}
		}
		#endregion background task

		#region services
		/// <summary>
		/// The bool is true if the background task must be disabled
		/// The PointRecord contains the result
		/// </summary>
		/// <returns></returns>
		public async Task<Tuple<bool, PointRecord>> GetGeoLocationAsync()
		{
			//this method runs in a UI thread
			PointRecord dataRecord = null;
			var result = Tuple.Create<bool, PointRecord>(false, null); // the bool tells if the background task must be disabled

			if (_getLocationCts == null) // get out if another request is already pending
			{
				IsGPSWorking = true;
				SetLastMessage_UI("getting current location...");

				_getLocationCts = new CancellationTokenSource();             // Get cancellation token
				CancellationToken token = _getLocationCts.Token;
				try
				{
					Geoposition pos = null;
					if (_geolocator != null)
					{
						pos = await _geolocator.GetGeopositionAsync().AsTask(token).ConfigureAwait(false);
						dataRecord = await AppendGeoPositionAsync(_myPersistentData, pos, false).ConfigureAwait(false);
						result = Tuple.Create(false, dataRecord);
					}
				}
				catch (UnauthorizedAccessException)
				{
					result = Tuple.Create<bool, PointRecord>(true, null);
					SetLastMessage_UI("Give the app permission to access your location (Settings - Privacy - Location)");
				}
				catch (TaskCanceledException) // inherits from OperationCanceled
				{
					result = Tuple.Create<bool, PointRecord>(false, null);
					SetLastMessage_UI("location acquisition cancelled");
				}
				catch (Exception ex)
				{
					result = Tuple.Create<bool, PointRecord>(false, null);
					SetLastMessage_UI("cannot get location");
					await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename).ConfigureAwait(false);
				}
				finally
				{
					_getLocationCts?.Dispose();
					_getLocationCts = null;
					if (dataRecord != null)
					{
						await RunInUiThreadAsync(delegate
						{
							_myPersistentData?.SelectRecordFromSeries(_myPersistentData.Current, PersistentData.Tables.History);
						}).ConfigureAwait(false);
					}
					IsGPSWorking = false;
				}
			}
			return result;
		}

		private static async Task<PointRecord> AppendGeoPositionAsync(PersistentData persistentData, Geoposition pos, bool checkMaxEntries)
		{
			PointRecord newDataRecord = new PointRecord();
			InitNewHistoryRecord(pos, newDataRecord);

			if (await persistentData.AddHistoryRecordAsync(newDataRecord, checkMaxEntries).ConfigureAwait(false)) return newDataRecord;
			else return null;
		}

		private static void InitNewHistoryRecord(Geoposition pos, PointRecord newDataRecord)
		{
			newDataRecord.Latitude = pos.Coordinate.Point.Position.Latitude;
			newDataRecord.Longitude = pos.Coordinate.Point.Position.Longitude;
			newDataRecord.Altitude = pos.Coordinate.Point.Position.Altitude;
			newDataRecord.Accuracy = pos.Coordinate.Accuracy;
			newDataRecord.AltitudeAccuracy = pos.Coordinate.AltitudeAccuracy.GetValueOrDefault();// == null ? default(Double) : pos.Coordinate.AltitudeAccuracy;
			newDataRecord.PositionSource = pos.Coordinate.PositionSource.ToString();
			newDataRecord.TimePoint = pos.Coordinate.Timestamp.DateTime;
			newDataRecord.SpeedInMetreSec = pos.Coordinate.Speed ?? default(Double);
		}

		public static bool AppendGeoPositionOnlyDb(PersistentData persistentData, Geoposition pos, bool checkMaxEntries)
		{
			PointRecord newDataRecord = new PointRecord();
			InitNewHistoryRecord(pos, newDataRecord);
			bool isOk = persistentData.AddHistoryRecordOnlyDb(newDataRecord, checkMaxEntries);
			return isOk;
		}

		private void SetLastMessage_UI(string message)
		{
			if (_myPersistentData != null) _myPersistentData.LastMessage = message;
		}
		#endregion services
	}
}