using GPX;
using LolloBaseUserControls;
using LolloGPS.Data;
using Utilz.Data.Constants;
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
using Utilz.Data;
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


		private LolloMapVM _lolloMapVM = null;
		public LolloMapVM LolloMapVM { get { return _lolloMapVM; } set { _lolloMapVM = value; RaisePropertyChanged_UI(); } }

		private AltitudeProfilesVM _altitudeProfilesVM = null;
		public AltitudeProfilesVM AltitudeProfilesVM { get { return _altitudeProfilesVM; } set { _altitudeProfilesVM = value; RaisePropertyChanged_UI(); } }

		public PersistentData PersistentData { get { return App.PersistentData; } }
		public RuntimeData RuntimeData { get { return App.RuntimeData; } }

		private GPSInteractor _gpsInteractor = null;
		public GPSInteractor GPSInteractor { get { return _gpsInteractor; } }

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
			_gpsInteractor = GPSInteractor.GetInstance(PersistentData);
		}

		protected override async Task OpenMayOverrideAsync()
		{
			try
			{
				if (_readSettingsFromDb) await SuspensionManager.LoadSettingsAndDbDataAsync(_readDataFromDb, _readSettingsFromDb).ConfigureAwait(false);

				await _gpsInteractor.OpenAsync();
				UpdateClearCacheButtonIsEnabled();
				UpdateClearCustomCacheButtonIsEnabled();
				UpdateCacheButtonIsEnabled();
				UpdateDownloadButtonIsEnabled();

				KeepAlive.UpdateKeepAlive(PersistentData.IsKeepAlive);

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
				await _gpsInteractor.CloseAsync().ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename);
			}
		}
		#endregion construct and dispose

		#region updaters
		internal void UpdateClearCustomCacheButtonIsEnabled()
		{
			IsClearCustomCacheEnabled = // !(PersistentData.IsTilesDownloadDesired && RuntimeData.IsConnectionAvailable) &&
				PersistentData.TileSourcez.FirstOrDefault(a => a.IsDeletable) != null &&
				TileCacheProcessingQueue.IsFree;
		}
		internal void UpdateClearCacheButtonIsEnabled()
		{
			IsClearCacheEnabled = // !(PersistentData.IsTilesDownloadDesired && RuntimeData.IsConnectionAvailable) &&
				TileCacheProcessingQueue.IsFree;
		}
		internal void UpdateCacheButtonIsEnabled()
		{
			IsCacheBtnEnabled = // !PersistentData.CurrentTileSource.IsTesting && 
				!PersistentData.CurrentTileSource.IsDefault
				&& TileCacheProcessingQueue.IsFree;
		}
		internal void UpdateDownloadButtonIsEnabled()
		{
			IsLeechingEnabled = !PersistentData.IsTilesDownloadDesired
				&& !PersistentData.CurrentTileSource.IsDefault
				&& RuntimeData.IsConnectionAvailable
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
				PersistentData.PropertyChanged += OnPersistentData_PropertyChanged;
				RuntimeData.PropertyChanged += OnRuntimeData_PropertyChanged;
				TileCacheProcessingQueue.IsFreeChanged += OnTileCache_IsFreeChanged;
				TileCacheProcessingQueue.CacheCleared += OnTileCache_CacheCleared;
			}
		}

		private void RemoveHandlers_DataChanged()
		{
			if (PersistentData != null)
			{
				PersistentData.PropertyChanged -= OnPersistentData_PropertyChanged;
				RuntimeData.PropertyChanged -= OnRuntimeData_PropertyChanged;
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
					PersistentData.Current.Latitude = PersistentData.Current.Latitude; // trigger PropertyChanged to make it reraw the fields bound to it
					PersistentData.Current.Longitude = PersistentData.Current.Longitude; // trigger PropertyChanged to make it reraw the fields bound to it
				});
			}
			else if (e.PropertyName == nameof(PersistentData.IsShowImperialUnits))
			{
				Task gt = RunInUiThreadAsync(delegate
				{
					if (PersistentData?.Current != null) PersistentData.Current.Altitude = PersistentData.Current.Altitude;
				});
			}
			else if (e.PropertyName == nameof(PersistentData.IsKeepAlive))
			{
				Task gt = RunInUiThreadAsync(delegate
				{
					KeepAlive.UpdateKeepAlive(PersistentData.IsKeepAlive);
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
				await PersistentData.RemoveTileSourcesAsync(args.TileSource);
			}

			// output messages
			if (!args.TileSource.IsAll)
			{
				if (args.HowManyRecordsDeleted > 0)
				{
					PersistentData.LastMessage = (args.HowManyRecordsDeleted + " " + args.TileSource.DisplayName + " records deleted");
				}
				else if (args.IsCacheCleared)
				{
					PersistentData.LastMessage = (args.TileSource.DisplayName + " cache is empty");
				}
				else
				{
					PersistentData.LastMessage = (args.TileSource.DisplayName + " cache is busy");
				}
			}
			else if (args.TileSource.IsAll)
			{
				if (args.HowManyRecordsDeleted > 0)
				{
					PersistentData.LastMessage = (args.HowManyRecordsDeleted + " records deleted");
				}
				else if (args.IsCacheCleared)
				{
					PersistentData.LastMessage = ("Cache empty");
				}
				else
				{
					PersistentData.LastMessage = ("Cache busy");
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
			if (!args.Handled) PersistentData.IsShowingPivot = false;
		}
		public void GoBackTabletSoft(object sender, BackRequestedEventArgs e)
		{
			if (PersistentData.IsBackButtonEnabled && e != null) e.Handled = true;
			var args = new BackOrHardSoftKeyPressedEventArgs();
			BackOrHardSoftKeyPressed?.Invoke(sender, args);
			if (!args.Handled) PersistentData.IsShowingPivot = false;
		}
		public void GoBackHard(object sender, BackPressedEventArgs e)
		{
			if (PersistentData.IsBackButtonEnabled && e != null) e.Handled = true;
			var args = new BackOrHardSoftKeyPressedEventArgs();
			BackOrHardSoftKeyPressed?.Invoke(sender, args);
			if (!args.Handled) PersistentData.IsShowingPivot = false;
		}
		public void SetLastMessage_UI(string message)
		{
			App.PersistentData.LastMessage = message;
		}
		public void GetAFix()
		{
			Task getLoc = _gpsInteractor.GetGeoLocationAppendingHistoryAsync();
		}
		public void ScheduleClearCacheAsync(TileSourceRecord tileSource, bool isAlsoRemoveSources)
		{
			if (!tileSource.IsNone && !tileSource.IsDefault)
			{
				PersistentData.LastMessage = "clearing the cache...";
				TileCache.ScheduleClear(tileSource, isAlsoRemoveSources);
				//PersistentData.IsMapCached = false; // stop caching if you want to delete the cache // no!
			}
			// LOLLO TODO see what happens now: is the event handler in MainVM enough?

			//	/*howManyRecordsDeleted = */
			//	await Task.Run(delegate
			//	{
			//		TileCache.ScheduleClear(tileSource);
			//	}).ConfigureAwait(false);

			//	if (isAlsoRemoveSources && howManyRecordsDeleted >= 0)
			//	{
			//		await PersistentData.RemoveTileSourcesAsync(tileSource);
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
			if (_lolloMapVM != null)
			{
				var output = await _lolloMapVM.GetHowManyTiles4DifferentZoomsAsync();
				return output;
			}
			else return new List<Tuple<int, int>>();
		}
		public void CancelDownloadByUser()
		{
			if (_lolloMapVM != null) _lolloMapVM.CancelDownloadByUser();
		}

		public async Task StartUserTestingTileSourceAsync() // when i click "test"
		{
			Tuple<bool, string> result = await PersistentData.TryInsertTestTileSourceIntoTileSourcezAsync();

			if (result?.Item1 == true)
			{
				TestTileSourceErrorMsg = string.Empty; // ok
				PersistentData.IsShowingPivot = false;
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
			GPSInteractor gpsInteractor = GPSInteractor.GetInstance(PersistentData);
			if (gpsInteractor == null) return;

			Task vibrate = Task.Run(() => App.ShortVibration());
			var currrent = await gpsInteractor.GetGeoLocationAppendingHistoryAsync();
			Task upd = currrent?.UpdateUIEditablePropertiesAsync(PersistentData?.Target, PersistentData.Tables.History).ContinueWith(delegate
			{
				PointRecord currentClone = null;
				PointRecord.Clone(currrent, ref currentClone);
				Task add = PersistentData?.TryAddPointToLandmarksAsync(currentClone);
			});
		}
		#endregion services


		#region IMapApController
		public async Task CentreOnRoute0Async()
		{
			PersistentData.IsShowingPivot = false;
			if (_altitudeProfilesVM != null) await _altitudeProfilesVM.CentreOnRoute0Async();
			if (_lolloMapVM != null) await _lolloMapVM.CentreOnRoute0Async().ConfigureAwait(false);
		}
		public async Task CentreOnHistoryAsync()
		{
			PersistentData.IsShowingPivot = false;
			if (_altitudeProfilesVM != null) await _altitudeProfilesVM.CentreOnHistoryAsync();
			if (_lolloMapVM != null) await _lolloMapVM.CentreOnHistoryAsync().ConfigureAwait(false);
		}
		public async Task CentreOnLandmarksAsync()
		{
			PersistentData.IsShowingPivot = false;
			if (_altitudeProfilesVM != null) await _altitudeProfilesVM.CentreOnLandmarksAsync();
			if (_lolloMapVM != null) await _lolloMapVM.CentreOnLandmarksAsync().ConfigureAwait(false);
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
			PersistentData.IsShowingPivot = false;
			// await _myAltitudeProfiles_VM?.CentreOnTargetAsync(); // useless
			if (_lolloMapVM != null) await _lolloMapVM.CentreOnTargetAsync().ConfigureAwait(false);
		}
		public async Task CentreOnCurrentAsync()
		{
			PersistentData.IsShowingPivot = false;
			// await _myAltitudeProfiles_VM?.CentreOnCurrentAsync(); // useless
			if (_lolloMapVM != null) await _lolloMapVM.CentreOnCurrentAsync().ConfigureAwait(false);
		}
		public Task Goto2DAsync()
		{
			PersistentData.IsShowingPivot = false;
			Task alt = _altitudeProfilesVM?.Goto2DAsync();
			return _lolloMapVM?.Goto2DAsync();
		}
		#endregion IMapApController


		#region save and load with picker
		internal async Task PickSaveSeriesToFileAsync(PersistentData.Tables whichSeries, string fileNameSuffix)
		{
			if (IsSaving) return;
			try
			{
				IsSaving = true;

				SwitchableObservableCollection<PointRecord> series = null;
				if (whichSeries == PersistentData.Tables.History) series = PersistentData.History;
				else if (whichSeries == PersistentData.Tables.Route0) series = PersistentData.Route0;
				else if (whichSeries == PersistentData.Tables.Landmarks) series = PersistentData.Landmarks;
				else return;

				SetLastMessage_UI("saving GPX file...");

				DateTime fileCreationDateTime = DateTime.Now;
				var file = await Pickers.PickSaveFileAsync(new string[] { ConstantData.GPX_EXTENSION }, fileCreationDateTime.ToString(ConstantData.GpxDateTimeFormat, CultureInfo.InvariantCulture) + fileNameSuffix).ConfigureAwait(false);

				if (file != null)
				{
					// LOLLO NOTE at this point, OnResuming() has just started, if the app was suspended. To avoid surprises, we enqueue the following under the semaphore.
					await ((App)Application.Current).RunAfterResumingAsync(delegate { return SaveSeriesToFileAsync(series, whichSeries, fileCreationDateTime, file); }).ConfigureAwait(false);
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

		/// <summary>
		/// This method is called in a separate task on low-memory phones
		/// </summary>
		/// <param name="series"></param>
		/// <param name="whichSeries"></param>
		/// <param name="fileCreationDateTime"></param>
		/// <param name="file"></param>
		/// <returns></returns>
		private async Task SaveSeriesToFileAsync(SwitchableObservableCollection<PointRecord> series, PersistentData.Tables whichSeries, DateTime fileCreationDateTime, StorageFile file)
		{
			Logger.Add_TPL("SaveSeriesToFileAsync() started with file == null = " + (file == null).ToString() + " and whichSeries = " + whichSeries + " and isOpen = " + _isOpen, Logger.AppEventsLogFilename, Logger.Severity.Info, false);

			Tuple<bool, string> result = Tuple.Create(false, "");
			try
			{
				if (file != null && whichSeries != PersistentData.Tables.nil && !Cts.IsCancellationRequestedSafe)
				{
					await Task.Run(async delegate
					{
						SetLastMessage_UI("saving GPX file...");
						if (Cts.IsCancellationRequestedSafe) return;
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

		/// <summary>
		/// This method is called in a separate task on low-memory phones
		/// </summary>
		/// <param name="file"></param>
		/// <param name="whichSeries"></param>
		/// <returns></returns>
		private async Task LoadSeriesFromFileAsync(StorageFile file, PersistentData.Tables whichSeries)
		{
			// Logger.Add_TPL("LoadSeriesFromFileAsync() started with file == null = " + (file == null).ToString() + " and whichSeries = " + whichSeries + " and isOpen = " + _isOpen, Logger.AppEventsLogFilename, Logger.Severity.Info, false);

			Tuple<bool, string> result = Tuple.Create(false, "");
			try
			{
				if (file != null && whichSeries != PersistentData.Tables.nil && !Cts.IsCancellationRequestedSafe)
				{
					await Task.Run(async delegate
					{
						SetLastMessage_UI("reading GPX file...");

						if (Cts.IsCancellationRequestedSafe) return;
						// load the file
						result = await ReaderWriter.LoadSeriesFromFileIntoDbAsync(file, whichSeries, CancToken).ConfigureAwait(false);
						if (Cts.IsCancellationRequestedSafe) return;
						Logger.Add_TPL("LoadSeriesFromFileAsync() loaded series into db", Logger.AppEventsLogFilename, Logger.Severity.Info, false);

						// update the UI with the file data
						if (result?.Item1 == true)
						{
							switch (whichSeries)
							{
								case PersistentData.Tables.History:
									break;
								case PersistentData.Tables.Route0:
									await PersistentData.LoadRoute0FromDbAsync(false).ConfigureAwait(false);
									Logger.Add_TPL("LoadSeriesFromFileAsync() loaded route0 into UI", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
									await CentreOnRoute0Async().ConfigureAwait(false);
									break;
								case PersistentData.Tables.Landmarks:
									await PersistentData.LoadLandmarksFromDbAsync(false).ConfigureAwait(false);
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
					PersistentData.IsShowingPivot = false;
				}
				else if (whichSeries != PersistentData.Tables.nil) SetLastMessage_UI(string.Format("could not load {0}", PersistentData.GetTextForSeries(whichSeries)));
				else SetLastMessage_UI("could not load file");
			}
			Logger.Add_TPL("LoadSeriesFromFileAsync() ended", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
		}
		#endregion save and load with picker


		#region open app through file
		/// <summary>
		/// This method is called in a separate task on low-memory phones
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public async Task<List<PersistentData.Tables>> LoadFileIntoDbAsync(FileActivatedEventArgs args)
		{
			List<PersistentData.Tables> result = new List<PersistentData.Tables>();
			if (args?.Files?.Count > 0 && args.Files[0] is StorageFile && !Cts.IsCancellationRequestedSafe)
			{
				Tuple<bool, string> landmarksResult = Tuple.Create(false, "");
				Tuple<bool, string> route0Result = Tuple.Create(false, "");

				try
				{
					SetLastMessage_UI("reading GPX file...");
					// load the file, attempting to read landmarks and route. GPX files can contain both.
					StorageFile file_mt = args.Files[0] as StorageFile;
					if (Cts.IsCancellationRequestedSafe) return result;
					landmarksResult = await ReaderWriter.LoadSeriesFromFileIntoDbAsync(file_mt, PersistentData.Tables.Landmarks, CancToken).ConfigureAwait(false);
					if (Cts.IsCancellationRequestedSafe) return result;
					route0Result = await ReaderWriter.LoadSeriesFromFileIntoDbAsync(file_mt, PersistentData.Tables.Route0, CancToken).ConfigureAwait(false);
				}
				catch (Exception) { }
				finally
				{
					// inform the user about the result LOLLO TODO check when loading a file with both route and landmarks, eg coasttocoast
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
			return result;
		}
		#endregion open app through file
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