using GPX;
using LolloBaseUserControls;
using LolloGPS.Data;
using LolloGPS.Data.Constants;
using LolloGPS.Data.Runtime;
using LolloGPS.Data.TileCache;
using LolloGPS.GPSInteraction;
using LolloGPS.Suspension;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Utilz;
using Windows.ApplicationModel.Activation;
using Windows.Phone.UI.Input;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;

namespace LolloGPS.Core
{
	public sealed class MainVM : OpenableObservableData, IBackPressedRaiser, IMapApController //, IFileActivatable
	{
		#region IBackPressedRaiser
		public event EventHandler<BackOrHardSoftKeyPressedEventArgs> BackOrHardSoftKeyPressed;
		#endregion IBackPressedRaiser


		#region properties
		private const double MIN_ALTITUDE_M_ABS = .1;
		private const double MAX_ALTITUDE_M_ABS = 10000.0;
		//private static readonly double MIN_ALTITUDE_FT_ABS = MIN_ALTITUDE_M_ABS * ConstantData.M_TO_FOOT;
		private static readonly double MAX_ALTITUDE_FT_ABS = MAX_ALTITUDE_M_ABS * ConstantData.M_TO_FOOT;


		private LolloMap_VM _myLolloMap_VM = null;
		public LolloMap_VM MyLolloMap_VM { get { return _myLolloMap_VM; } set { _myLolloMap_VM = value; RaisePropertyChanged_UI(); } }

		private AltitudeProfiles_VM _myAltitudeProfiles_VM = null;
		public AltitudeProfiles_VM MyAltitudeProfiles_VM { get { return _myAltitudeProfiles_VM; } set { _myAltitudeProfiles_VM = value; RaisePropertyChanged_UI(); } }

		public PersistentData MyPersistentData { get { return App.PersistentData; } }
		public RuntimeData MyRuntimeData { get { return App.MyRuntimeData; } }

		private GPSInteractor _myGPSInteractor = null;
		public GPSInteractor MyGPSInteractor { get { return _myGPSInteractor; } }

		private bool _isClearCustomCacheEnabled = false;
		public bool IsClearCustomCacheEnabled { get { return _isClearCustomCacheEnabled; } set { if (_isClearCustomCacheEnabled != value) { _isClearCustomCacheEnabled = value; RaisePropertyChanged_UI(); } } }
		private bool _isClearCacheEnabled = false;
		public bool IsClearCacheEnabled { get { return _isClearCacheEnabled; } set { if (_isClearCacheEnabled != value) { _isClearCacheEnabled = value; RaisePropertyChanged_UI(); } } }
		private bool _isCacheBtnEnabled = false;
		public bool IsCacheBtnEnabled { get { return _isCacheBtnEnabled; } set { if (_isCacheBtnEnabled != value) { _isCacheBtnEnabled = value; RaisePropertyChanged_UI(); } } }
		private bool _isLeechingEnabled = false;
		public bool IsLeechingEnabled { get { return _isLeechingEnabled; } set { if (_isLeechingEnabled != value) { _isLeechingEnabled = value; RaisePropertyChanged_UI(); } } }

		private string _testTileSourceErrorMsg = "";
		public string TestTileSourceErrorMsg { get { return _testTileSourceErrorMsg; } set { _testTileSourceErrorMsg = value; RaisePropertyChanged_UI(); } }

		private bool _isLastMessageVisible = false;
		public bool IsLastMessageVisible { get { return _isLastMessageVisible; } set { if (_isLastMessageVisible != value) { _isLastMessageVisible = value; RaisePropertyChanged_UI(); } } }

		private bool _isLoading = false;
		/// <summary>
		/// Perhaps not the best, but it beats using the registry, which gets stuck to a value whenever the app crashes
		/// </summary>
		public bool IsLoading { get { return _isLoading; } private set { _isLoading = value; RaisePropertyChangedUrgent_UI(); } }

		private bool _isSaving = false;
		/// <summary>
		/// Perhaps not the best, but it beats using the registry, which gets stuck to a value whenever the app crashes
		/// </summary>
		public bool IsSaving { get { return _isSaving; } private set { _isSaving = value; RaisePropertyChangedUrgent_UI(); } }

		private string _logText;
		public string LogText { get { return _logText; } set { _logText = value; RaisePropertyChanged_UI(); } }

		private bool _readDataFromDb = false;
		private bool _readSettingsFromDb = false;
		#endregion properties

		#region construct and dispose
		public MainVM(bool readDataFromDb, bool readSettingsFromDb)
		{
			_readDataFromDb = readDataFromDb;
			_readSettingsFromDb = readSettingsFromDb;
			_myGPSInteractor = GPSInteractor.GetInstance(MyPersistentData);
		}

		protected override async Task OpenMayOverrideAsync()
		{
			try
			{
				if (_readSettingsFromDb) await SuspensionManager.LoadSettingsAndDbDataAsync(_readDataFromDb, _readSettingsFromDb).ConfigureAwait(false);

				await _myGPSInteractor.OpenAsync();
				UpdateClearCacheButtonIsEnabled();
				UpdateClearCustomCacheButtonIsEnabled();
				UpdateCacheButtonIsEnabled();
				UpdateDownloadButtonIsEnabled();

				KeepAlive.UpdateKeepAlive(MyPersistentData.IsKeepAlive);

				RuntimeData.GetInstance().IsAllowCentreOnCurrent = true;

				AddHandlers_DataChanged();

				Logger.Add_TPL("MainVM.OpenMayOverrideAsync() ran OK", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename);
			}
		}
		protected override async Task CloseMayOverrideAsync()
		{
			try
			{
				RemoveHandlers_DataChanged();
				KeepAlive.StopKeepAlive();
				await _myGPSInteractor.CloseAsync().ConfigureAwait(false);

				//CancelPendingTasks(); // after removing the handlers
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename);
			}
		}
		//private void CancelPendingTasks()
		//{
		//	// Do not dispose the cts's here. Dispose is done in the exception handler that catches the OperationCanceled exception. 
		//	// If you do it here, the exception handler will throw an ObjectDisposed exception
		//	_fileOpenContinuationCts?.Cancel(true);
		//	_fileOpenPickerCts?.Cancel(true);
		//	_fileSavePickerCts?.Cancel(true);
		//}
		#endregion construct and dispose

		#region updaters
		internal void UpdateClearCustomCacheButtonIsEnabled()
		{
			IsClearCustomCacheEnabled = // !(MyPersistentData.IsTilesDownloadDesired && MyRuntimeData.IsConnectionAvailable) &&
				MyPersistentData.TileSourcez.FirstOrDefault(a => a.IsDeletable) != null &&
				TileCacheProcessingQueue.IsFree;
		}
		internal void UpdateClearCacheButtonIsEnabled()
		{
			IsClearCacheEnabled = // !(MyPersistentData.IsTilesDownloadDesired && MyRuntimeData.IsConnectionAvailable) &&
				TileCacheProcessingQueue.IsFree;
		}
		internal void UpdateCacheButtonIsEnabled()
		{
			IsCacheBtnEnabled = // !MyPersistentData.CurrentTileSource.IsTesting && 
				!MyPersistentData.CurrentTileSource.IsDefault
				&& TileCacheProcessingQueue.IsFree;
		}
		internal void UpdateDownloadButtonIsEnabled()
		{
			IsLeechingEnabled = !MyPersistentData.IsTilesDownloadDesired
				&& !MyPersistentData.CurrentTileSource.IsDefault
				&& MyRuntimeData.IsConnectionAvailable
				&& TileCacheProcessingQueue.IsFree;
		}
		#endregion updaters

		#region event handlers
		private bool _isDataChangedHandlerActive = false;
		private void AddHandlers_DataChanged()
		{
			if (!_isDataChangedHandlerActive)
			{
				_isDataChangedHandlerActive = true;
				MyPersistentData.PropertyChanged += OnPersistentData_PropertyChanged;
				MyRuntimeData.PropertyChanged += OnRuntimeData_PropertyChanged;
				TileCacheProcessingQueue.IsFreeChanged += OnTileCache_IsFreeChanged;
				TileCacheProcessingQueue.CacheCleared += OnTileCache_CacheCleared;
			}
		}

		private void RemoveHandlers_DataChanged()
		{
			if (MyPersistentData != null)
			{
				MyPersistentData.PropertyChanged -= OnPersistentData_PropertyChanged;
				MyRuntimeData.PropertyChanged -= OnRuntimeData_PropertyChanged;
				TileCacheProcessingQueue.IsFreeChanged -= OnTileCache_IsFreeChanged;
				TileCacheProcessingQueue.CacheCleared -= OnTileCache_CacheCleared;
				_isDataChangedHandlerActive = false;
			}
		}
		private void OnPersistentData_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(PersistentData.IsShowDegrees))
			{
				Task gt = RunInUiThreadAsync(delegate
				{
					MyPersistentData.Current.Latitude = MyPersistentData.Current.Latitude; // trigger PropertyChanged to make it reraw the fields bound to it
					MyPersistentData.Current.Longitude = MyPersistentData.Current.Longitude; // trigger PropertyChanged to make it reraw the fields bound to it
				});
			}
			else if (e.PropertyName == nameof(PersistentData.IsShowImperialUnits))
			{
				Task gt = RunInUiThreadAsync(delegate
				{
					if (MyPersistentData?.Current != null) MyPersistentData.Current.Altitude = MyPersistentData.Current.Altitude;
				});
			}
			else if (e.PropertyName == nameof(PersistentData.IsKeepAlive))
			{
				Task gt = RunInUiThreadAsync(delegate
				{
					KeepAlive.UpdateKeepAlive(MyPersistentData.IsKeepAlive);
				});
			}
			else if (e.PropertyName == nameof(PersistentData.IsTilesDownloadDesired))
			{
				Task gt = RunInUiThreadAsync(delegate
				{
					UpdateDownloadButtonIsEnabled();
				});
			}
			else if (e.PropertyName == nameof(PersistentData.CurrentTileSource))
			{
				Task gt = RunInUiThreadAsync(delegate
				{
					UpdateDownloadButtonIsEnabled();
					UpdateCacheButtonIsEnabled();
				});
			}
			else if (e.PropertyName == nameof(PersistentData.TileSourcez))
			{
				Task gt = RunInUiThreadAsync(delegate
				{
					UpdateClearCustomCacheButtonIsEnabled();
				});
			}
		}

		private void OnRuntimeData_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(RuntimeData.IsConnectionAvailable))
			{
				Task gt = RunInUiThreadAsync(delegate
				{
					UpdateDownloadButtonIsEnabled();
				});
			}
		}
		private void OnTileCache_IsFreeChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			UpdateClearCacheButtonIsEnabled();
			UpdateClearCustomCacheButtonIsEnabled();
			UpdateCacheButtonIsEnabled();
			UpdateDownloadButtonIsEnabled();
		}
		private async void OnTileCache_CacheCleared(object sender, TileCacheProcessingQueue.CacheClearedEventArgs args)
		{
			// LOLLO TODO check this method
			if (args.IsAlsoRemoveSources && args.IsCacheCleared)
			{
				await MyPersistentData.RemoveTileSourcesAsync(args.TileSource);
			}

			// output messages
			if (!args.TileSource.IsAll)
			{
				if (args.HowManyRecordsDeleted > 0)
				{
					MyPersistentData.LastMessage = (args.HowManyRecordsDeleted + " " + args.TileSource.DisplayName + " records deleted");
				}
				else if (args.IsCacheCleared)
				{
					MyPersistentData.LastMessage = (args.TileSource.DisplayName + " cache is empty");
				}
				else
				{
					MyPersistentData.LastMessage = (args.TileSource.DisplayName + " cache is busy");
				}
			}
			else if (args.TileSource.IsAll)
			{
				if (args.HowManyRecordsDeleted > 0)
				{
					MyPersistentData.LastMessage = (args.HowManyRecordsDeleted + " records deleted");
				}
				else if (args.IsCacheCleared)
				{
					MyPersistentData.LastMessage = ("Cache empty");
				}
				else
				{
					MyPersistentData.LastMessage = ("Cache busy");
				}
			}
		}
		//return howManyRecordsDeleted;	
		#endregion event handlers

		#region services
		public void GoBackMyButtonSoft()
		{
			var args = new BackOrHardSoftKeyPressedEventArgs();
			BackOrHardSoftKeyPressed?.Invoke(this, args);
			if (!args.Handled) MyPersistentData.IsShowingPivot = false;
		}
		public void GoBackTabletSoft(object sender, BackRequestedEventArgs e)
		{
			if (MyPersistentData.IsBackButtonEnabled && e != null) e.Handled = true;
			var args = new BackOrHardSoftKeyPressedEventArgs();
			BackOrHardSoftKeyPressed?.Invoke(sender, args);
			if (!args.Handled) MyPersistentData.IsShowingPivot = false;
		}
		public void GoBackHard(object sender, BackPressedEventArgs e)
		{
			if (MyPersistentData.IsBackButtonEnabled && e != null) e.Handled = true;
			var args = new BackOrHardSoftKeyPressedEventArgs();
			BackOrHardSoftKeyPressed?.Invoke(sender, args);
			if (!args.Handled) MyPersistentData.IsShowingPivot = false;
		}
		public void SetLastMessage_UI(string message)
		{
			PersistentData.GetInstance().LastMessage = message;
		}
		public void GetAFix()
		{
			Task getLoc = _myGPSInteractor.GetGeoLocationAppendingHistoryAsync();
		}
		public void ScheduleClearCacheAsync(TileSourceRecord tileSource, bool isAlsoRemoveSources)
		{
			if (!tileSource.IsNone && !tileSource.IsDefault)
			{
				MyPersistentData.LastMessage = "clearing the cache...";
				TileCache.ScheduleClear(tileSource, isAlsoRemoveSources);
				//MyPersistentData.IsMapCached = false; // stop caching if you want to delete the cache // no!
			}
			// LOLLO TODO see what happens now: is the event handler in MainVM enough?

			//	/*howManyRecordsDeleted = */
			//	await Task.Run(delegate
			//	{
			//		TileCache.ScheduleClear(tileSource);
			//	}).ConfigureAwait(false);

			//	if (isAlsoRemoveSources && howManyRecordsDeleted >= 0)
			//	{
			//		await MyPersistentData.RemoveTileSourcesAsync(tileSource);
			//	}

			//	// output messages
			//	if (!tileSource.IsAll)
			//	{
			//		if (howManyRecordsDeleted > 0)
			//		{
			//			SetLastMessage_UI(howManyRecordsDeleted + " " + tileSource.DisplayName + " records deleted");
			//		}
			//		else if (howManyRecordsDeleted == 0)
			//		{
			//			if (isAlsoRemoveSources) SetLastMessage_UI(tileSource.DisplayName + " is gone");
			//			else SetLastMessage_UI(tileSource.DisplayName + " cache is empty");
			//		}
			//		else
			//		{
			//			SetLastMessage_UI(tileSource.DisplayName + " cache is busy");
			//		}
			//	}
			//	else if (tileSource.IsAll)
			//	{
			//		if (howManyRecordsDeleted > 0)
			//		{
			//			SetLastMessage_UI(howManyRecordsDeleted + " records deleted");
			//		}
			//		else if (howManyRecordsDeleted == 0)
			//		{
			//			SetLastMessage_UI("Cache empty");
			//		}
			//		else
			//		{
			//			SetLastMessage_UI("Cache busy");
			//		}
			//	}
			//}
			//return howManyRecordsDeleted;
		}

		public async Task<List<Tuple<int, int>>> GetHowManyTiles4DifferentZoomsAsync()
		{
			if (_myLolloMap_VM != null)
			{
				var output = await _myLolloMap_VM.GetHowManyTiles4DifferentZoomsAsync();
				return output;
			}
			else return new List<Tuple<int, int>>();
		}
		public void CancelDownloadByUser()
		{
			if (_myLolloMap_VM != null) _myLolloMap_VM.CancelDownloadByUser();
		}

		public async Task StartUserTestingTileSourceAsync() // when i click "test"
		{
			Tuple<bool, string> result = await MyPersistentData.TryInsertTestTileSourceIntoTileSourcezAsync();

			if (result?.Item1 == true)
			{
				TestTileSourceErrorMsg = string.Empty; // ok
				MyPersistentData.IsShowingPivot = false;
			}
			else TestTileSourceErrorMsg = result.Item2;         // error

			SetLastMessage_UI(result.Item2);
		}
		/// <summary>
		/// Makes sure the numbers make sense:
		/// their absolute value must not be too large or too small.
		/// </summary>
		/// <param name="dblIn"></param>
		/// <returns></returns>
		internal static double RoundAndRangeAltitude(double dblIn, bool isImperialUnits)
		{
			if (Math.Abs(dblIn) < MIN_ALTITUDE_M_ABS)
			{
				return 0.0;
			}
			else
			{
				if (isImperialUnits)
				{
					if (dblIn > MAX_ALTITUDE_M_ABS) return MAX_ALTITUDE_FT_ABS;
					if (dblIn < -MAX_ALTITUDE_M_ABS) return -MAX_ALTITUDE_FT_ABS;
					return dblIn * ConstantData.M_TO_FOOT;
				}
				else
				{
					if (dblIn > MAX_ALTITUDE_M_ABS) return MAX_ALTITUDE_M_ABS;
					if (dblIn < -MAX_ALTITUDE_M_ABS) return -MAX_ALTITUDE_M_ABS;
					return dblIn;
				}
			}
		}
		internal void NavigateToUri(string uri)
		{
			try
			{
				if (!string.IsNullOrWhiteSpace(uri))
				{
					var ub = new UriBuilder(uri);
					Task upd = Launcher.LaunchUriAsync(ub.Uri, new LauncherOptions() { DesiredRemainingView = ViewSizePreference.UseHalf }).AsTask();
				}
			}
			catch { }
		}
		public async Task SetTargetToCurrentAsync()
		{
			GPSInteractor gpsInteractor = GPSInteractor.GetInstance(MyPersistentData);
			if (gpsInteractor == null) return;

			Task vibrate = Task.Run(() => App.ShortVibration());
			var currrent = await gpsInteractor.GetGeoLocationAppendingHistoryAsync();
			Task upd = currrent?.UpdateUIEditablePropertiesAsync(MyPersistentData?.Target, PersistentData.Tables.History).ContinueWith(delegate
			{
				PointRecord currentClone = null;
				PointRecord.Clone(currrent, ref currentClone);
				Task add = MyPersistentData?.TryAddPointToLandmarksAsync(currentClone);
			});
		}
		#endregion services


		#region IMapApController
		public async Task CentreOnRoute0Async()
		{
			MyPersistentData.IsShowingPivot = false;
			if (_myAltitudeProfiles_VM != null) await _myAltitudeProfiles_VM.CentreOnRoute0Async();
			if (_myLolloMap_VM != null) await _myLolloMap_VM.CentreOnRoute0Async().ConfigureAwait(false);
		}
		public async Task CentreOnHistoryAsync()
		{
			MyPersistentData.IsShowingPivot = false;
			if (_myAltitudeProfiles_VM != null) await _myAltitudeProfiles_VM.CentreOnHistoryAsync();
			if (_myLolloMap_VM != null) await _myLolloMap_VM.CentreOnHistoryAsync().ConfigureAwait(false);
		}
		public async Task CentreOnLandmarksAsync()
		{
			MyPersistentData.IsShowingPivot = false;
			if (_myAltitudeProfiles_VM != null) await _myAltitudeProfiles_VM.CentreOnLandmarksAsync();
			if (_myLolloMap_VM != null) await _myLolloMap_VM.CentreOnLandmarksAsync().ConfigureAwait(false);
		}
		public Task CentreOnSeriesAsync(PersistentData.Tables series)
		{
			if (series == PersistentData.Tables.History) return CentreOnHistoryAsync();
			else if (series == PersistentData.Tables.Route0) return CentreOnRoute0Async();
			else if (series == PersistentData.Tables.Landmarks) return CentreOnLandmarksAsync();
			else return Task.CompletedTask;
		}
		public async Task CentreOnTargetAsync()
		{
			MyPersistentData.IsShowingPivot = false;
			// await _myAltitudeProfiles_VM?.CentreOnTargetAsync(); // useless
			if (_myLolloMap_VM != null) await _myLolloMap_VM.CentreOnTargetAsync().ConfigureAwait(false);
		}
		public async Task CentreOnCurrentAsync()
		{
			MyPersistentData.IsShowingPivot = false;
			// await _myAltitudeProfiles_VM?.CentreOnCurrentAsync(); // useless
			if (_myLolloMap_VM != null) await _myLolloMap_VM.CentreOnCurrentAsync().ConfigureAwait(false);
		}
		public Task Goto2DAsync()
		{
			MyPersistentData.IsShowingPivot = false;
			Task alt = _myAltitudeProfiles_VM?.Goto2DAsync();
			return _myLolloMap_VM?.Goto2DAsync();
		}
		#endregion IMapApController


		#region save and load with picker
		private SemaphoreSlimSafeRelease _loadSaveSemaphore = new SemaphoreSlimSafeRelease(1, 1);
		//internal CancellationTokenSource _fileSavePickerCts = null;
		//internal CancellationTokenSource _fileOpenPickerCts = null;

		internal async Task PickSaveSeriesToFileAsync(PersistentData.Tables whichSeries, string fileNameSuffix)
		{
			if (IsSaving) return;
			try
			{
				IsSaving = true;

				SwitchableObservableCollection<PointRecord> series = null;
				if (whichSeries == PersistentData.Tables.History) series = MyPersistentData.History;
				else if (whichSeries == PersistentData.Tables.Route0) series = MyPersistentData.Route0;
				else if (whichSeries == PersistentData.Tables.Landmarks) series = MyPersistentData.Landmarks;
				else return;

				SetLastMessage_UI("saving GPX file...");

				DateTime fileCreationDateTime = DateTime.Now;
				var file = await Pickers.PickSaveFileAsync(new string[] { ConstantData.GPX_EXTENSION }, fileCreationDateTime.ToString(ConstantData.GpxDateTimeFormat, CultureInfo.InvariantCulture) + fileNameSuffix).ConfigureAwait(false);

				if (file != null)
				{
					// LOLLO NOTE at this point, OnResuming() has just started, if the app was suspended. To avoid surprises, we enqueue the following under the semaphore.
					await ((App)Application.Current).RunAfterResumingAsync(delegate { return SaveSeriesToFileAsync(series, whichSeries, fileCreationDateTime, file); }).ConfigureAwait(false);
					// LOLLO TODO check this on phone, it does not work anymore
					//await RunFunctionIfOpenAsyncT(delegate { return SaveSeriesToFileAsync(series, whichSeries, fileCreationDateTime, file); }).ConfigureAwait(false);
				}
				else
				{
					SetLastMessage_UI("Saving cancelled");
				}
			}
			finally
			{
				IsSaving = false;
			}
		}
		private async Task SaveSeriesToFileAsync(SwitchableObservableCollection<PointRecord> series, PersistentData.Tables whichSeries, DateTime fileCreationDateTime, StorageFile file)
		{
			Logger.Add_TPL("SaveSeriesToFileAsync() started with file == null = " + (file == null).ToString() + " and whichSeries = " + whichSeries + " and isOpen = " + _isOpen, Logger.AppEventsLogFilename, Logger.Severity.Info, false);

			Tuple<bool, string> result = Tuple.Create(false, "");
			try
			{
				if (file != null && whichSeries != PersistentData.Tables.nil)
				{
					await Task.Run(async delegate
					{
						SetLastMessage_UI("saving GPX file...");
						if (CancToken.IsCancellationRequested)
						{
							Logger.Add_TPL("Canc requested", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
							return;
						}
						result = await ReaderWriter.SaveAsync(file, series, fileCreationDateTime, whichSeries, CancToken).ConfigureAwait(false);
					}).ConfigureAwait(false);
				}
			}
			catch (Exception) { }
			finally
			{
				// inform the user about the result
				if (result != null && result.Item1) SetLastMessage_UI(result.Item2);
				else if (whichSeries != PersistentData.Tables.nil) SetLastMessage_UI(string.Format("could not save {0}", PersistentData.GetTextForSeries(whichSeries)));
				else SetLastMessage_UI(string.Format("could not save file"));
			}
		}

		internal async Task PickLoadSeriesFromFileAsync(PersistentData.Tables whichSeries)
		{
			if (IsLoading) return;
			try
			{
				IsLoading = true;

				SetLastMessage_UI("reading GPX file...");

				var file = await Pickers.PickOpenFileAsync(new string[] { ConstantData.GPX_EXTENSION }).ConfigureAwait(false);
				if (file != null)
				{
					// LOLLO NOTE at this point, OnResuming() has just started, if the app was suspended. To avoid surprises, we enqueue the following under the semaphore.
					await ((App)Application.Current).RunAfterResumingAsync(delegate { return LoadSeriesFromFileAsync(file, whichSeries); }).ConfigureAwait(false);
					// LOLLO TODO check this on phone, it does not work anymore
					//await RunFunctionIfOpenAsyncT(delegate { return LoadSeriesFromFileAsync(file, whichSeries); }).ConfigureAwait(false);
				}
				else
				{
					SetLastMessage_UI("Loading cancelled");
				}
			}
			finally
			{
				IsLoading = false;
			}
		}

		// LOLLO NOTE check https://social.msdn.microsoft.com/Forums/sqlserver/en-US/13002ba6-6e59-47b8-a746-c05525953c5a/uwpfileopenpicker-bugs-in-win-10-mobile-when-not-debugging?forum=wpdevelop
		// and AnalyticsVersionInfo.DeviceFamily
		// for picker details

		private async Task LoadSeriesFromFileAsync(StorageFile file, PersistentData.Tables whichSeries)
		{
			Logger.Add_TPL("LoadSeriesFromFileAsync() started with file == null = " + (file == null).ToString() + " and whichSeries = " + whichSeries + " and isOpen = " + _isOpen, Logger.AppEventsLogFilename, Logger.Severity.Info, false);

			Tuple<bool, string> result = Tuple.Create(false, "");
			try
			{
				if (file != null && whichSeries != PersistentData.Tables.nil)
				{
					await Task.Run(async delegate
					{
						Logger.Add_TPL("LoadSeriesFromFileAsync() started a worker thread", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
						SetLastMessage_UI("reading GPX file...");

						if (CancToken.IsCancellationRequested)
						{
							Logger.Add_TPL("Canc requested", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
							return;
						}
						// load the file
						result = await ReaderWriter.LoadSeriesFromFileIntoDbAsync(file, whichSeries, CancToken).ConfigureAwait(false);
						Logger.Add_TPL("LoadSeriesFromFileAsync() loaded series into db", Logger.AppEventsLogFilename, Logger.Severity.Info, false);

						// update the UI with the file data
						if (result?.Item1 == true)
						{
							switch (whichSeries)
							{
								case PersistentData.Tables.History:
									break;
								case PersistentData.Tables.Route0:
									await MyPersistentData.LoadRoute0FromDbAsync(false).ConfigureAwait(false);
									Logger.Add_TPL("LoadSeriesFromFileAsync() loaded route0 into UI", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
									await CentreOnRoute0Async().ConfigureAwait(false);
									break;
								case PersistentData.Tables.Landmarks:
									await MyPersistentData.LoadLandmarksFromDbAsync(false).ConfigureAwait(false);
									Logger.Add_TPL("LoadSeriesFromFileAsync() loaded landmarks into UI", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
									await CentreOnLandmarksAsync().ConfigureAwait(false);
									break;
								case PersistentData.Tables.nil:
									break;
								default:
									break;
							}
						}
					}).ConfigureAwait(false);
				}
			}
			catch (Exception) { }
			finally
			{
				// inform the user about the outcome
				if (result?.Item1 == true)
				{
					SetLastMessage_UI(result.Item2);
					MyPersistentData.IsShowingPivot = false;
				}
				else if (whichSeries != PersistentData.Tables.nil) SetLastMessage_UI(string.Format("could not load {0}", PersistentData.GetTextForSeries(whichSeries)));
				else SetLastMessage_UI("could not load file");
			}
			Logger.Add_TPL("LoadSeriesFromFileAsync() ended", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
		}
		#endregion save and load with picker


		#region continuations
		public async Task<List<PersistentData.Tables>> LoadFileIntoDbAsync(FileActivatedEventArgs args)
		{
			// LOLLO TODO check this on phone, it does not work anymore
			List<PersistentData.Tables> result = new List<PersistentData.Tables>();
			//await RunFunctionIfOpenAsyncT(async delegate
			//{
			if (args?.Files?.Count > 0 && args.Files[0] is StorageFile)
			{
				Tuple<bool, string> landmarksResult = Tuple.Create(false, "");
				Tuple<bool, string> route0Result = Tuple.Create(false, "");

				try
				{
					SetLastMessage_UI("reading GPX file...");
					// load the file, attempting to read landmarks and route. GPX files can contain both.
					StorageFile file_mt = args.Files[0] as StorageFile;
					if (CancToken.IsCancellationRequested)
					{
						Logger.Add_TPL("Canc requested", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
						return result;
					}
					landmarksResult = await ReaderWriter.LoadSeriesFromFileIntoDbAsync(file_mt, PersistentData.Tables.Landmarks, CancToken).ConfigureAwait(false);
					if (CancToken.IsCancellationRequested)
					{
						Logger.Add_TPL("Canc requested", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
						return result;
					}
					route0Result = await ReaderWriter.LoadSeriesFromFileIntoDbAsync(file_mt, PersistentData.Tables.Route0, CancToken).ConfigureAwait(false);
				}
				catch (Exception) { }
				finally
				{
					// inform the user about the result
					if ((landmarksResult == null || !landmarksResult.Item1) && (route0Result == null || !route0Result.Item1)) SetLastMessage_UI("could not read file");
					else if (landmarksResult?.Item1 == true && route0Result?.Item1 == true)
					{
						SetLastMessage_UI(route0Result.Item2 + " and " + landmarksResult.Item2);
					}
					else if (route0Result?.Item1 == true)
					{
						SetLastMessage_UI(route0Result.Item2);
					}
					else if (landmarksResult?.Item1 == true)
					{
						SetLastMessage_UI(landmarksResult.Item2);
					}
					// fill output
					if (landmarksResult?.Item1 == true) result.Add(PersistentData.Tables.Landmarks);
					if (route0Result?.Item1 == true) result.Add(PersistentData.Tables.Route0);
				}
			}
			//}).ConfigureAwait(false);
			return result;
		}
		#endregion continuations
	}

	public interface IMapApController
	{
		Task CentreOnHistoryAsync();
		Task CentreOnLandmarksAsync();
		Task CentreOnRoute0Async();
		Task CentreOnSeriesAsync(PersistentData.Tables series);
		Task CentreOnTargetAsync();
		Task CentreOnCurrentAsync();
		Task Goto2DAsync();
	}
}