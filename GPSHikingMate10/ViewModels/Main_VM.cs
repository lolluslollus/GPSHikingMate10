using GPX;
using Utilz.Controlz;
using LolloGPS.Data;
using LolloGPS.Data.Runtime;
using LolloGPS.Data.TileCache;
using LolloGPS.GPSInteraction;
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
using Windows.UI.Xaml;
using System.Threading;
using System.Collections.ObjectModel;
using Windows.Storage.Pickers;

namespace LolloGPS.Core
{
    public sealed class MainVM : OpenableObservableData, IBackPressedRaiser
    {
        #region IBackPressedRaiser
        public event EventHandler<BackOrHardSoftKeyPressedEventArgs> BackOrHardSoftKeyPressed;
        #endregion IBackPressedRaiser


        #region properties
        private const double MIN_ALTITUDE_M_ABS = .1;
        private const double MAX_ALTITUDE_M_ABS = 10000.0;
        //private static readonly double MIN_ALTITUDE_FT_ABS = MIN_ALTITUDE_M_ABS * ConstantData.M_TO_FOOT;
        //private static readonly double MAX_ALTITUDE_FT_ABS = MAX_ALTITUDE_M_ABS * ConstantData.M_TO_FOOT;

        public PersistentData PersistentData { get { return App.PersistentData; } }
        public RuntimeData RuntimeData { get { return App.RuntimeData; } }

        private readonly IMapAltProfCentrer _lolloMap = null;
        private readonly IMapAltProfCentrer _altitudeProfiles = null;
        private readonly TileCacheClearer _tileCacheClearer = null;
        private readonly GPSInteractor _gpsInteractor = null;
        public GPSInteractor GPSInteractor { get { return _gpsInteractor; } }

        // the following bools should be volatile, instead we choose to only read and write them in the UI thread.
        private bool _isClearCustomCacheEnabled = false;
        public bool IsClearCustomCacheEnabled { get { return _isClearCustomCacheEnabled; } private set { if (_isClearCustomCacheEnabled != value) { _isClearCustomCacheEnabled = value; RaisePropertyChanged_UI(); } } }
        private bool _isClearCacheEnabled = false;
        public bool IsClearCacheEnabled { get { return _isClearCacheEnabled; } private set { if (_isClearCacheEnabled != value) { _isClearCacheEnabled = value; RaisePropertyChanged_UI(); } } }
        private bool _isCacheBtnEnabled = false;
        public bool IsCacheBtnEnabled { get { return _isCacheBtnEnabled; } private set { if (_isCacheBtnEnabled != value) { _isCacheBtnEnabled = value; RaisePropertyChanged_UI(); } } }
        private bool _isLeechingEnabled = false;
        public bool IsLeechingEnabled { get { return _isLeechingEnabled; } private set { if (_isLeechingEnabled != value) { _isLeechingEnabled = value; RaisePropertyChanged_UI(); } } }
        private bool _isTestBtnEnabled = false;
        public bool IsTestCustomTileSourceEnabled { get { return _isTestBtnEnabled; } private set { if (_isTestBtnEnabled != value) { _isTestBtnEnabled = value; RaisePropertyChanged_UI(); } } }
        private bool _isChangeTileSourceEnabled = false;
        public bool IsChangeTileSourceEnabled { get { return _isChangeTileSourceEnabled; } private set { if (_isChangeTileSourceEnabled != value) { _isChangeTileSourceEnabled = value; RaisePropertyChanged_UI(); } } }
        private bool _isChangeMapStyleEnabled = false;
        public bool IsChangeMapStyleEnabled { get { return _isChangeMapStyleEnabled; } private set { if (_isChangeMapStyleEnabled != value) { _isChangeMapStyleEnabled = value; RaisePropertyChanged_UI(); } } }

        private string _testTileSourceErrorMsg = "";
        public string TestTileSourceErrorMsg { get { return _testTileSourceErrorMsg; } private set { _testTileSourceErrorMsg = value; RaisePropertyChanged_UI(); } }

        private volatile bool _isWideEnough = false;
        public bool IsWideEnough { get { return _isWideEnough; } set { if (_isWideEnough != value) { _isWideEnough = value; RaisePropertyChanged_UI(); } } }

        private volatile bool _isDrawing = true; // always written under _isOpenSemaphore
        public bool IsDrawing { get { return _isDrawing; } private set { if (_isDrawing != value) { _isDrawing = value; RaisePropertyChanged_UI(); } } }

        private readonly object _loadSaveLocker = new object();
        
        public PersistentData.Tables WhichSeriesToSaveToFileOnNextResume
        {
            get
            {
                lock (_loadSaveLocker)
                {
                    PersistentData.Tables whichSeries = PersistentData.Tables.Nil;
                    Enum.TryParse(
                        RegistryAccess.GetValue(ConstantData.REG_SAVE_SERIES_WHICH_SERIES),
                        out whichSeries);
                    return whichSeries;
                }
            }
            private set
            {
                lock (_loadSaveLocker)
                {
                    RegistryAccess.TrySetValue(ConstantData.REG_SAVE_SERIES_WHICH_SERIES, value.ToString());
                }
            }
        }
        public DateTime DateTimeForSave
        {
            get
            {
                lock (_loadSaveLocker)
                {
                    DateTime dt = default(DateTime);
                    DateTime.TryParseExact(
                        RegistryAccess.GetValue(ConstantData.REG_SAVE_SERIES_FILE_CREATION_DATE_TIME),
                        ConstantData.GPX_DATE_TIME_FORMAT,
                        CultureInfo.CurrentUICulture,
                        DateTimeStyles.None,
                        out dt);
                    return dt;
                }
            }
            private set
            {
                lock (_loadSaveLocker)
                {
                    RegistryAccess.TrySetValue(ConstantData.REG_SAVE_SERIES_FILE_CREATION_DATE_TIME, value.ToString(ConstantData.GPX_DATE_TIME_FORMAT, CultureInfo.CurrentUICulture));
                }
            }
        }
        private bool _isLoading = false;
        public bool IsLoading { get { lock (_loadSaveLocker) { return _isLoading; } } private set { lock (_loadSaveLocker) { _isLoading = value; } RaisePropertyChanged_UI(); } }
        private bool _isSaving = false;
        public bool IsSaving { get { lock (_loadSaveLocker) { return _isSaving; } } private set { lock (_loadSaveLocker) { _isSaving = value; } RaisePropertyChanged_UI(); } }        

        private string _logText;
        public string LogText { get { return _logText; } set { _logText = value; RaisePropertyChanged_UI(); } }

        //private bool _isPointInfoPanelOpen = false;
        //public bool IsPointInfoPanelOpen { get { return _isPointInfoPanelOpen; } set { _isPointInfoPanelOpen = value; } }
        #endregion properties

        #region lifecycle
        private static readonly object _instanceLocker = new object();
        private readonly IOpenable _owner = null;
        private IOpenable Owner { get { return _owner; } }
        private static MainVM _instance = null;
        private static MainVM Instance { get { lock (_instanceLocker) { return _instance; } } }
        public MainVM(bool isWideEnough, IMapAltProfCentrer lolloMap, IMapAltProfCentrer altitudeProfiles, IOpenable owner)
        {
            _gpsInteractor = GPSInteractor.GetInstance(PersistentData);
            _tileCacheClearer = TileCacheClearer.GetInstance();
            IsWideEnough = isWideEnough;
            _lolloMap = lolloMap;
            _altitudeProfiles = altitudeProfiles;
            lock (_instanceLocker)
            {
                _instance = this;
            }
            _owner = owner;
        }

        protected override async Task OpenMayOverrideAsync(object args = null)
        {
            try
            {
                IsDrawing = true;
                Logger.Add_TPL($"MainVm.OpenMayOverrideAsync started. There are {PersistentData.Checkpoints.Count} checkpoints, {PersistentData.History.Count} history points and {PersistentData.Route0.Count} route points", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
                await Task.Run(async delegate
                {
                    PersistentData.OpenMainDb();
                    // When resuming, settings and data are already in, unless:
                    // 1) The background task may have changed the history while I was suspended.
                    // 2) The device suspended with the file picker. In this case, my picker is smart and will continue on his own, as soon as everyhing is open again.
                    bool isResuming = args != null && (LifecycleEvents)args == LifecycleEvents.Resuming;

                    //if (isResuming && WhichSeriesToLoadFromFileOnNextResume != PersistentData.Tables.Nil)
                    //{
                    //    var file = await Pickers.GetLastPickedOpenFileAsync().ConfigureAwait(false);
                    //    Logger.Add_TPL($"MainVm.OpenMayOverrideAsync is about to read file {file?.Name ?? "NONE"} with {WhichSeriesToLoadFromFileOnNextResume}", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
                    //    Task loadCheckpointsFromFile = WhichSeriesToLoadFromFileOnNextResume != PersistentData.Tables.Checkpoints ? Task.CompletedTask : LoadSeriesFromFileIntoDbAsync(file, PersistentData.Tables.Checkpoints, CancToken);
                    //    Task loadRoute0FromFile = WhichSeriesToLoadFromFileOnNextResume != PersistentData.Tables.Route0 ? Task.CompletedTask : LoadSeriesFromFileIntoDbAsync(file, PersistentData.Tables.Route0, CancToken);
                    //    await Task.WhenAll(loadCheckpointsFromFile, loadRoute0FromFile).ConfigureAwait(false);
                    //}

                    //Logger.Add_TPL($"MainVm.OpenMayOverrideAsync is about to load from db; isResuming = {isResuming} and the series is {WhichSeriesToLoadFromDbOnNextResume}", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
                    //Task loadCheckpointsFromDb = isResuming && WhichSeriesToLoadFromDbOnNextResume != PersistentData.Tables.Checkpoints ? Task.CompletedTask : PersistentData.LoadCheckpointsFromDbAsync(false, true);
                    //Task loadHistoryFromDb = PersistentData.LoadHistoryFromDbAsync(false, true); // always load this, the bkb task may have changed it
                    //Task loadRoute0FromDb = isResuming && WhichSeriesToLoadFromDbOnNextResume != PersistentData.Tables.Route0 ? Task.CompletedTask : PersistentData.LoadRoute0FromDbAsync(false, true);
                    //await Task.WhenAll(loadCheckpointsFromDb, loadHistoryFromDb, loadRoute0FromDb).ConfigureAwait(false);
                    await PersistentData.LoadHistoryFromDbAsync(false, true).ConfigureAwait(false); // always load this, the bkb task may have changed it
                    Logger.Add_TPL($"MainVm.OpenMayOverrideAsync has loaded from db. There are {PersistentData.Checkpoints.Count} checkpoints, {PersistentData.History.Count} history points and {PersistentData.Route0.Count} route points", Logger.AppEventsLogFilename, Logger.Severity.Info, false);

                    // LOLLO TODO avoid reading these variables too often, they access the registry.
                    //if (isResuming && WhichSeriesToSaveToFileOnNextResume != PersistentData.Tables.Nil)
                    //{
                    //    var file = await Pickers.GetLastPickedSaveFileAsync().ConfigureAwait(false);
                    //    Logger.Add_TPL($"MainVm.OpenMayOverrideAsync is about to save file {file?.Name ?? "NONE"} with {WhichSeriesToSaveToFileOnNextResume}", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
                    //    Task saveCheckpointsToFile = WhichSeriesToSaveToFileOnNextResume != PersistentData.Tables.Checkpoints ? Task.CompletedTask : ContinueAfterPickSaveSeriesToFileAsync(PersistentData.Checkpoints, PersistentData.Tables.Checkpoints, DateTimeForSave, file, CancToken);
                    //    Task saveHistoryToFile = WhichSeriesToSaveToFileOnNextResume != PersistentData.Tables.History ? Task.CompletedTask : ContinueAfterPickSaveSeriesToFileAsync(PersistentData.History, PersistentData.Tables.History, DateTimeForSave, file, CancToken);
                    //    Task saveRoute0ToFile = WhichSeriesToSaveToFileOnNextResume != PersistentData.Tables.Route0 ? Task.CompletedTask : ContinueAfterPickSaveSeriesToFileAsync(PersistentData.Route0, PersistentData.Tables.Route0, DateTimeForSave, file, CancToken);
                    //    await Task.WhenAll(saveCheckpointsToFile, saveHistoryToFile, saveRoute0ToFile).ConfigureAwait(false);
                    //}
                });

                await _gpsInteractor.OpenAsync(args);
                await _tileCacheClearer.OpenAsync(args);
                RuntimeData.GetInstance().IsAllowCentreOnCurrent = true;
                AddHandlers_DataChanged();

                UpdateIsClearCacheEnabled();
                UpdateIsClearCustomCacheEnabled();
                UpdateIsCacheBtnEnabled();
                UpdateIsLeechingEnabled();
                UpdateIsChangeTileSourceEnabled();
                UpdateIsTestCustomTileSourceEnabled();
                Task upd = UpdateIsChangeMapStyleEnabledAsync();

                await RunInUiThreadAsync(delegate
                {
                    KeepAlive.UpdateKeepAlive(PersistentData.IsKeepAlive);
                }).ConfigureAwait(false);

                Logger.Add_TPL("MainVM.OpenMayOverrideAsync() ran OK", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
            }
            catch (Exception ex)
            {
                await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename);
            }
            finally
            {
                //WhichSeriesToLoadFromFileOnNextResume = PersistentData.Tables.Nil;
                //WhichSeriesToLoadFromDbOnNextResume = PersistentData.Tables.Nil;
                //WhichSeriesToSaveToFileOnNextResume = PersistentData.Tables.Nil;
                IsDrawing = false;
            }
        }
        protected override async Task CloseMayOverrideAsync(object args = null)
        {
            try
            {
                RemoveHandlers_DataChanged();
                // do not show the drawing overlay, it may take too long
                Task closeDb = Task.Run(delegate { PersistentData.CloseMainDb(); });
                Task closeKeepAlive = RunInUiThreadAsync(KeepAlive.StopKeepAlive);
                Task closeTileCacheClearer = _tileCacheClearer.CloseAsync(args);
                Task closeGps = _gpsInteractor.CloseAsync(args);
                await Task.WhenAll(closeDb, closeKeepAlive, closeTileCacheClearer, closeGps).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename);
            }
        }
        #endregion lifecycle

        #region updaters
        internal void UpdateIsClearCustomCacheEnabled()
        {
            Task ui = RunInUiThreadAsync(delegate
            {
                IsClearCustomCacheEnabled =
                PersistentData.TileSourcez.FirstOrDefault(ts => ts.IsDeletable) != null && // not atomic, not volatile, not critical
                !_tileCacheClearer.IsClearingScheduled
                && !PersistentData.IsTileSourcezBusy;
            });
        }
        internal void UpdateIsClearCacheEnabled()
        {
            Task ui = RunInUiThreadAsync(delegate
            {
                IsClearCacheEnabled = !_tileCacheClearer.IsClearingScheduled
                && !PersistentData.IsTileSourcezBusy;
            });
        }
        internal void UpdateIsCacheBtnEnabled()
        {
            Task ui = RunInUiThreadAsync(delegate
            {
                IsCacheBtnEnabled =
                    PersistentData.CurrentTileSource?.IsDefault == false;
                // && !TileCacheProcessingQueue.GetInstance().IsClearingScheduled;
            });
        }
        internal void UpdateIsLeechingEnabled()
        {
            Task ui = RunInUiThreadAsync(delegate
            {
                IsLeechingEnabled = !PersistentData.IsTilesDownloadDesired
                && PersistentData.CurrentTileSource?.IsDefault == false
                && RuntimeData.IsConnectionAvailable
                && !_tileCacheClearer.IsClearingScheduled
                && !PersistentData.IsTileSourcezBusy;
            });
        }
        internal void UpdateIsChangeTileSourceEnabled()
        {
            Task ui = RunInUiThreadAsync(delegate
            {
                IsChangeTileSourceEnabled = !_tileCacheClearer.IsClearingScheduled
                && !PersistentData.IsTileSourcezBusy;
            });
        }
        internal void UpdateIsTestCustomTileSourceEnabled()
        {
            Task ui = RunInUiThreadAsync(delegate
            {
                IsTestCustomTileSourceEnabled = !_tileCacheClearer.IsClearingScheduled
                && !PersistentData.IsTileSourcezBusy
                && RuntimeData.IsConnectionAvailable;
            });
        }
        internal async Task UpdateIsChangeMapStyleEnabledAsync()
        {
            var ts = await PersistentData.GetCurrentTileSourceCloneAsync().ConfigureAwait(false);
            Task ui = RunInUiThreadAsync(delegate
            {
                IsChangeMapStyleEnabled = !ts.IsDefault;
            });
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
                TileCacheClearer.IsClearingScheduledChanged += OnTileCache_IsClearingScheduledChanged;
                TileCacheClearer.CacheCleared += OnTileCache_CacheCleared;
            }
        }

        private void RemoveHandlers_DataChanged()
        {
            if (PersistentData != null)
            {
                PersistentData.PropertyChanged -= OnPersistentData_PropertyChanged;
                RuntimeData.PropertyChanged -= OnRuntimeData_PropertyChanged;
                TileCacheClearer.IsClearingScheduledChanged -= OnTileCache_IsClearingScheduledChanged;
                TileCacheClearer.CacheCleared -= OnTileCache_CacheCleared;
                _isDataChangedHandlerActive = false;
            }
        }
        private void OnPersistentData_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PersistentData.IsKeepAlive))
            {
                Task ka = RunInUiThreadAsync(delegate
                {
                    KeepAlive.UpdateKeepAlive(PersistentData.IsKeepAlive);
                });
            }
            else if (e.PropertyName == nameof(PersistentData.IsTilesDownloadDesired))
            {
                Task gt = RunInUiThreadAsync(UpdateIsLeechingEnabled);
            }
            else if (e.PropertyName == nameof(PersistentData.CurrentTileSource))
            {
                Task gt = RunInUiThreadAsync(delegate
                {
                    UpdateIsLeechingEnabled();
                    UpdateIsCacheBtnEnabled();
                    Task upd = UpdateIsChangeMapStyleEnabledAsync();
                });
            }
            else if (e.PropertyName == nameof(PersistentData.TileSourcez))
            {
                Task gt = RunInUiThreadAsync(UpdateIsClearCustomCacheEnabled);
            }
            else if (e.PropertyName == nameof(PersistentData.IsTileSourcezBusy))
            {
                Task gt = RunInUiThreadAsync(delegate
                {
                    UpdateIsClearCacheEnabled();
                    UpdateIsClearCustomCacheEnabled();
                    UpdateIsLeechingEnabled();
                    UpdateIsChangeTileSourceEnabled();
                    UpdateIsTestCustomTileSourceEnabled();
                });
            }
        }

        private void OnRuntimeData_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RuntimeData.IsConnectionAvailable))
            {
                Task gt = RunInUiThreadAsync(delegate
                {
                    UpdateIsLeechingEnabled();
                    UpdateIsTestCustomTileSourceEnabled();
                });
            }
        }

        private void OnTileCache_IsClearingScheduledChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            UpdateIsClearCacheEnabled();
            UpdateIsClearCustomCacheEnabled();
            UpdateIsLeechingEnabled();
            UpdateIsChangeTileSourceEnabled();
            UpdateIsTestCustomTileSourceEnabled();
        }
        private void OnTileCache_CacheCleared(object sender, TileCacheClearer.CacheClearedEventArgs args)
        {
            // output messages
            if (args.TileSource.IsAll)
            {
                /*if (args.HowManyRecordsDeleted > 0)
				{
					PersistentData.LastMessage = (args.HowManyRecordsDeleted + " records deleted");
				}
				else */
                if (args.IsCacheCleared)
                {
                    PersistentData.LastMessage = ("Cache empty");
                }
                else
                {
                    PersistentData.LastMessage = ("Cache busy");
                }
            }
            else
            {
                /*if (args.HowManyRecordsDeleted > 0)
				{
					PersistentData.LastMessage = (args.HowManyRecordsDeleted + " " + args.TileSource.DisplayName + " records deleted");
				}
				else */
                if (args.IsCacheCleared)
                {
                    PersistentData.LastMessage = (args.TileSource.DisplayName + " cache is empty");
                }
                else
                {
                    PersistentData.LastMessage = (args.TileSource.DisplayName + " cache is busy");
                }
            }
        }
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
            var gpsInt = _gpsInteractor;
            if (gpsInt != null)
            {
                Task getLoc = Task.Run(gpsInt.GetGeoLocationAppendingHistoryAsync);
            }
        }
        public async Task ScheduleClearCacheAsync(TileSourceRecord tileSource, bool isAlsoRemoveSources)
        {
            bool isScheduled = await Task.Run(() => _tileCacheClearer.TryScheduleClearCacheAsync(tileSource, isAlsoRemoveSources)).ConfigureAwait(false);
            PersistentData.LastMessage = isScheduled ? "cache will be cleared asap" : "cache unchanged";
        }

        public Task StartUserTestingTileSourceAsync()
        {
            return RunFunctionIfOpenAsyncT(async delegate
            {
                Tuple<bool, string> result = await PersistentData.TryInsertTestTileSourceIntoTileSourcezAsync();

                if (result?.Item1 == true)
                {
                    TestTileSourceErrorMsg = string.Empty; // ok
                    PersistentData.IsShowingPivot = false;
                }
                else TestTileSourceErrorMsg = result?.Item2; // error

                SetLastMessage_UI(result?.Item2);
            });
        }
        /// <summary>
        /// Makes sure the numbers make sense:
        /// their absolute value must not be too large or too small.
        /// Numbers larger than the limit are set to 0.0.
        /// </summary>
        /// <param name="dblIn"></param>
        /// <param name="isImperialUnits"></param>
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
                    if (dblIn > MAX_ALTITUDE_M_ABS) return 0.0; // MAX_ALTITUDE_FT_ABS;
                    if (dblIn < -MAX_ALTITUDE_M_ABS) return 0.0; // -MAX_ALTITUDE_FT_ABS;
                    return dblIn * ConstantData.M_TO_FOOT;
                }
                else
                {
                    if (dblIn > MAX_ALTITUDE_M_ABS) return 0.0; // MAX_ALTITUDE_M_ABS;
                    if (dblIn < -MAX_ALTITUDE_M_ABS) return 0.0; // -MAX_ALTITUDE_M_ABS;
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
                    Task launch = Launcher.LaunchUriAsync(ub.Uri/*, new LauncherOptions() { DesiredRemainingView = ViewSizePreference.UseHalf }*/).AsTask();
                }
            }
            catch { }
        }
        public async Task SetTargetToCurrentAsync()
        {
            GPSInteractor gpsInteractor = GPSInteractor.GetInstance(PersistentData);
            if (gpsInteractor == null) return;

            Task vibrate = Task.Run(() => RuntimeData.ShortVibration());

            var currrent = await gpsInteractor.GetGeoLocationAppendingHistoryAsync();
            var persistentData = PersistentData;
            if (currrent != null && persistentData != null)
            {
                Task upd = currrent.UpdateUIEditablePropertiesAsync(persistentData.Target, PersistentData.Tables.History).ContinueWith(delegate
                {
                    PointRecord currentClone = null;
                    PointRecord.Clone(currrent, ref currentClone);
                    Task add = persistentData.TryAddPointToCheckpointsAsync(currentClone);
                });
            }
        }
        public Task SetTilesDownloadPropsAsync(int maxZoom)
        {
            return RunFunctionIfOpenAsyncT(async delegate
            {
                await PersistentData.SetTilesDownloadPropsAsync(true, maxZoom, false).ConfigureAwait(false);
                PersistentData.IsShowingPivot = false;
            });
        }
        public Task SetCurrentTileSourceAsync(TileSourceRecord tileSource)
        {
            return RunFunctionIfOpenAsyncT(() => PersistentData.SetCurrentTileSourceAsync(tileSource));
        }
        #endregion services

        #region IMapAltProfCentrer
        public Task CentreOnRoute0Async()
        {
            PersistentData.IsShowingPivot = false;
            Task alt = _altitudeProfiles?.CentreOnRoute0Async() ?? Task.CompletedTask;
            Task map = _lolloMap?.CentreOnRoute0Async() ?? Task.CompletedTask;
            return Task.WhenAll(alt, map);
        }
        public Task CentreOnHistoryAsync()
        {
            PersistentData.IsShowingPivot = false;
            Task alt = _altitudeProfiles?.CentreOnHistoryAsync() ?? Task.CompletedTask;
            Task map = _lolloMap?.CentreOnHistoryAsync() ?? Task.CompletedTask;
            return Task.WhenAll(alt, map);
        }
        public Task CentreOnCheckpointsAsync()
        {
            PersistentData.IsShowingPivot = false;
            Task alt = _altitudeProfiles?.CentreOnCheckpointsAsync() ?? Task.CompletedTask;
            Task map = _lolloMap?.CentreOnCheckpointsAsync() ?? Task.CompletedTask;
            return Task.WhenAll(alt, map);
        }
        /// <summary>
        /// This method calls a CenterOnSeries() with a delay, 
        /// so that it happens after the controls have certainly read the data.
        /// </summary>
        /// <param name="series"></param>
        private void CentreOnSeriesDelayed(PersistentData.Tables? series)
        {
            if (series == null) return;

            Func<Task> centre = null;
            if (series == PersistentData.Tables.Route0)
            {
                centre = () => Task.Run(async delegate
                {
                    await Task.Delay(1).ConfigureAwait(false);
                    await CentreOnRoute0Async().ConfigureAwait(false);
                });
            }
            else if (series == PersistentData.Tables.Checkpoints)
            {
                centre = () => Task.Run(async delegate
                {
                    await Task.Delay(1).ConfigureAwait(false);
                    await CentreOnCheckpointsAsync().ConfigureAwait(false);
                });
            }
            Task cc = centre?.Invoke();
        }
        public Task CentreOnTargetAsync()
        {
            PersistentData.IsShowingPivot = false;
            // await _altitudeProfiles?.CentreOnTargetAsync() ?? Task.CompletedTask; // useless
            return _lolloMap?.CentreOnTargetAsync() ?? Task.CompletedTask;
        }
        public Task CentreOnCurrentAsync()
        {
            PersistentData.IsShowingPivot = false;
            Task cenA = _altitudeProfiles?.CentreOnCurrentAsync() ?? Task.CompletedTask;
            Task cenM = _lolloMap?.CentreOnCurrentAsync() ?? Task.CompletedTask;
            return Task.WhenAll(cenA, cenM);
        }
        public Task Goto2DAsync()
        {
            PersistentData.IsShowingPivot = false;
            Task alt = _altitudeProfiles?.Goto2DAsync() ?? Task.CompletedTask;
            Task map = _lolloMap?.Goto2DAsync() ?? Task.CompletedTask;
            return Task.WhenAll(alt, map);
        }
        #endregion IMapAltProfCentrer

        #region save and load with picker
        internal async Task PickSaveSeriesToFileAsync(PersistentData.Tables whichSeries, string fileNameSuffix)
        {
            if (CancToken.IsCancellationRequested) return;
            await RunFunctionIfOpenAsyncT(async () =>
            {
                SetLastMessage_UI("saving GPX file...");

                SwitchableObservableCollection<PointRecord> series = PersistentData.GetSeries(whichSeries);
                DateTime fileCreationDateTime = DateTime.Now;

                WhichSeriesToSaveToFileOnNextResume = whichSeries;
                DateTimeForSave = fileCreationDateTime;
                Logger.Add_TPL($"PickSaveSeriesToFileAsync will open a picker for series {whichSeries.ToString()}", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
                // this may trigger a suspend on phones.
                var file = await Pickers.PickSaveFileAsync(new string[] { ConstantData.GPX_EXTENSION }, fileCreationDateTime.ToString(ConstantData.GPX_DATE_TIME_FORMAT_ONLY_LETTERS_AND_NUMBERS, CultureInfo.InvariantCulture) + fileNameSuffix).ConfigureAwait(false);
                if (file != null)
                {
                    try
                    {
                        // LOLLO TODO this hits after onsuspending is done, it's super quick, so it has no effect (on phone)
                        //WhichSeriesToSaveToFileOnNextResume = whichSeries;
                        //DateTimeForSave = fileCreationDateTime;
                        Logger.Add_TPL($"PickSaveSeriesToFileAsync has picked file {file.Name} for series {whichSeries.ToString()}; isOpen = {_isOpen}; the parameters are saved", Logger.AppEventsLogFilename, Logger.Severity.Info, false);

                        if (CancToken.IsCancellationRequested) return;
                        Logger.Add_TPL("PickSaveSeriesToFileAsync about to save series = " + whichSeries.ToString(), Logger.AppEventsLogFilename, Logger.Severity.Info, false);
                        // LOLLO NOTE at this point, OnResuming() has just started, if the app was suspended. This is the case with phones. 
                        // We play along and cancel, OpenMayOverrideAsync() will take care of it.
                        var saveResult = await ContinueAfterPickSaveSeriesToFileAsync(series, whichSeries, fileCreationDateTime, file, CancToken).ConfigureAwait(false);
                        if (saveResult.Item2) return; // cancelled
                        WhichSeriesToSaveToFileOnNextResume = PersistentData.Tables.Nil;
                    }
                    catch (OperationCanceledException) { }
                }
                else
                {
                    WhichSeriesToSaveToFileOnNextResume = PersistentData.Tables.Nil;
                    SetLastMessage_UI("Saving cancelled");
                }
            }).ConfigureAwait(false);
        }
        /// <summary>
        /// Returns a pair of bools: the first says if the operation was ok, the second says if the operation was maybe ok but cancelled
        /// </summary>
        /// <param name="series"></param>
        /// <param name="whichSeries"></param>
        /// <param name="fileCreationDateTime"></param>
        /// <param name="file"></param>
        /// <param name="cancToken"></param>
        /// <returns></returns>
        private async Task<Tuple<bool, bool>> ContinueAfterPickSaveSeriesToFileAsync(IReadOnlyCollection<PointRecord> series, PersistentData.Tables whichSeries, DateTime fileCreationDateTime, StorageFile file, CancellationToken cancToken)
        {
            Logger.Add_TPL("ContinueAfterPickSaveSeriesToFileAsync() started with file == null = " + (file == null).ToString() + " and whichSeries = " + whichSeries + " and isOpen = " + _isOpen, Logger.AppEventsLogFilename, Logger.Severity.Info, false);
            if (file == null || whichSeries == PersistentData.Tables.Nil) return Tuple.Create(false, false);
            if (cancToken.IsCancellationRequested) return Tuple.Create(false, true);

            bool isResultOk = false;
            bool isCancelled = false;
            Tuple<bool, string> readerWriterResult = Tuple.Create(false, "");
            try
            {
                await Task.Run(async delegate
                {
                    if (cancToken.IsCancellationRequested) { isCancelled = true; return; }
                    readerWriterResult = await ReaderWriter.SaveAsync(file, series, fileCreationDateTime, whichSeries, cancToken).ConfigureAwait(false);
                    if (cancToken.IsCancellationRequested) { isCancelled = true; return; }
                    isResultOk = readerWriterResult.Item1;
                    Logger.Add_TPL($"MainVm.ContinueAfterPickSaveSeriesToFileAsync has saved series {whichSeries.ToString()} to a file with result ok = {readerWriterResult.Item1.ToString()} and message = {readerWriterResult.Item2}", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
                }).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { isCancelled = true; }
            catch (Exception) { }
            finally
            {
                // inform the user about the outcome if bad
                if (readerWriterResult?.Item1 != true) SetLastMessage_UI($"could not save {PersistentData.GetTextForSeries(whichSeries)}");
                Logger.Add_TPL($"MainVm.ContinueAfterPickSaveSeriesToFileAsync() ended with result good = {isResultOk}", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
            }
            return Tuple.Create(isResultOk, isCancelled);
        }

        internal async Task PickLoadSeriesFromFileAsync(PersistentData.Tables whichSeries)
        {
            var cancToken = CancToken;
            SetLastMessage_UI("reading GPX file...");

            if (cancToken.IsCancellationRequested) return;

            Logger.Add_TPL($"PickLoadSeriesFromFileAsync will open a picker for series {whichSeries.ToString()}", Logger.AppEventsLogFilename, Logger.Severity.Info, false);

            var openPicker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };
            openPicker.FileTypeFilter.Add(ConstantData.GPX_EXTENSION);

            // this may trigger a suspend on phones. If so, execution will be cancelled immediately.
            // It will then resume and come back here, but with a new instance, since I have IOpenable.
            var file = await openPicker.PickSingleFileAsync();
            Logger.Add_TPL($"PickLoadSeriesFromFileAsync has picked file {file?.Name ?? "NONE"} for series {whichSeries.ToString()}", Logger.AppEventsLogFilename, Logger.Severity.Info, false);

            // wait for the UI to be ready, like the user would do, so we don't get in at unexpected instants.
            // This means, wait for Main to be open.
            int cnt = 0;
            while (Instance?.Owner?.IsOpen != true)
            {
                cnt++; if (cnt > 200) return;
                //Logger.Add_TPL($"PickLoadSeriesFromFileAsync is waiting for MainVM to reopen; Instance is there = {(Instance != null).ToString()}; Instance is open = {Instance?.IsOpen == true}", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
                //Logger.Add_TPL($"PickLoadSeriesFromFileAsync is waiting for Main to reopen; Owner is there = {(Instance.Owner != null).ToString()}; Owner is open = {Instance.Owner?.IsOpen == true}", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
                await Task.Delay(25).ConfigureAwait(false);
            }
            Logger.Add_TPL($"PickLoadSeriesFromFileAsync has got an open instance", Logger.AppEventsLogFilename, Logger.Severity.Info, false);

            var instance = Instance;
            if (instance == null) return;

            await instance.RunFunctionIfOpenAsyncT(() =>
            {
                return instance.LoadFile2Async(file, whichSeries==PersistentData.Tables.Checkpoints, whichSeries==PersistentData.Tables.Route0, instance.CancToken);
            }).ConfigureAwait(false);
        }

        // LOLLO NOTE check https://social.msdn.microsoft.com/Forums/sqlserver/en-US/13002ba6-6e59-47b8-a746-c05525953c5a/uwpfileopenpicker-bugs-in-win-10-mobile-when-not-debugging?forum=wpdevelop
        // and AnalyticsVersionInfo.DeviceFamily
        // for picker details        
        #endregion save and load with picker

        #region open app through file
        public Task LoadFileAsync(FileActivatedEventArgs args)
        {
            if (args == null || args.Files == null || args.Files.Count < 1) return Task.CompletedTask;
            var file = args.Files[0] as StorageFile;

            return RunFunctionIfOpenAsyncT_MT(() =>
            {
                return LoadFile2Async(file, true, true, CancToken);
            });
        }
        #endregion open app through file

        #region load and save utils
        private async Task LoadFile2Async(StorageFile file, bool doCheckpoints, bool doRoutes0, CancellationToken cancToken)
        {
            try
            {
                IsDrawing = true;
                // get file data into DB
                var whichTables = await LoadFileIntoDbAsync(file, doCheckpoints, doRoutes0, cancToken).ConfigureAwait(false);
                if (whichTables == null)
                {
                    Logger.Add_TPL("LoadFileAsync() loaded no files into the db", Logger.AppEventsLogFilename, Logger.Severity.Error, true);
                    return;
                }
                Logger.Add_TPL($"LoadFileAsync() loaded {whichTables.Count} file(s) into the db", Logger.AppEventsLogFilename, Logger.Severity.Info, false);

                // get file data from DB into UI
                if (cancToken.IsCancellationRequested) return;

                foreach (var series in whichTables)
                {
                    await LoadSeriesFromDbIntoUIAsync(series).ConfigureAwait(false);
                }
                Logger.Add_TPL("LoadFileAsync() ended proc OK", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
            }
            catch (OperationCanceledException ex) { }
            finally
            {
                IsDrawing = false;
            }
        }
        private async Task<List<PersistentData.Tables>> LoadFileIntoDbAsync(StorageFile file, bool doCheckpoints, bool doRoutes0, CancellationToken cancToken)
        {
            List<PersistentData.Tables> result = new List<PersistentData.Tables>();
            if (file == null) return result;

            Tuple<bool, string> checkpointsResult = Tuple.Create(false, "");
            Tuple<bool, string> route0Result = Tuple.Create(false, "");

            try
            {
                if (cancToken.IsCancellationRequested) return result;
                SetLastMessage_UI("reading GPX file...");
                // load the file, attempting to read checkpoints and route. GPX files can contain both.
                if (cancToken.IsCancellationRequested) return result;
                if (doCheckpoints) checkpointsResult = await ReaderWriter.LoadSeriesFromFileIntoDbAsync(file, PersistentData.Tables.Checkpoints, cancToken).ConfigureAwait(false);
                if (cancToken.IsCancellationRequested) return result;
                if (doRoutes0) route0Result = await ReaderWriter.LoadSeriesFromFileIntoDbAsync(file, PersistentData.Tables.Route0, cancToken).ConfigureAwait(false);
                if (cancToken.IsCancellationRequested) return result;
            }
            catch (Exception) { }
            finally
            {
                // inform the user about the result
                if ((checkpointsResult == null || !checkpointsResult.Item1) && (route0Result == null || !route0Result.Item1)) SetLastMessage_UI("could not read file");
                else if (checkpointsResult?.Item1 == true && route0Result?.Item1 == true)
                {
                    SetLastMessage_UI(route0Result.Item2 + " and " + checkpointsResult.Item2);
                }
                else if (route0Result?.Item1 == true)
                {
                    SetLastMessage_UI(route0Result.Item2);
                }
                else if (checkpointsResult?.Item1 == true)
                {
                    SetLastMessage_UI(checkpointsResult.Item2);
                }
                // fill output
                if (checkpointsResult?.Item1 == true) result.Add(PersistentData.Tables.Checkpoints);
                if (route0Result?.Item1 == true) result.Add(PersistentData.Tables.Route0);
            }

            return result;
        }
        private async Task<bool> LoadSeriesFromDbIntoUIAsync(PersistentData.Tables whichSeries)
        {
            Logger.Add_TPL("LoadSeriesFromDbAsync() started with whichSeries = " + whichSeries + " and isOpen = " + _isOpen, Logger.AppEventsLogFilename, Logger.Severity.Info, false);
            if (whichSeries == PersistentData.Tables.Nil) return false;

            bool isResultOk = false;
            int cnt = 0;
            try
            {
                switch (whichSeries)
                {
                    case PersistentData.Tables.Checkpoints:
                        cnt = await PersistentData.LoadCheckpointsFromDbAsync(true, true).ConfigureAwait(false);
                        Logger.Add_TPL("LoadSeriesFromDbIntoUIAsync() loaded " + cnt + " checkpoints into UI", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
                        break;
                    case PersistentData.Tables.History:
                        cnt = await PersistentData.LoadHistoryFromDbAsync(true, true).ConfigureAwait(false);
                        Logger.Add_TPL("LoadSeriesFromDbIntoUIAsync() loaded " + cnt + " history points into UI", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
                        break;
                    case PersistentData.Tables.Route0:
                        cnt = await PersistentData.LoadRoute0FromDbAsync(true, true).ConfigureAwait(false);
                        Logger.Add_TPL("LoadSeriesFromDbIntoUIAsync() loaded " + cnt + " route0 points into UI", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
                        break;
                    default:
                        break;
                }
                isResultOk = true;
                CentreOnSeriesDelayed(whichSeries);
            }
            catch (Exception) { }
            finally
            {
                // inform the user about the outcome
                SetLastMessage_UI($"{PersistentData.GetTextForSeries(whichSeries)}: {cnt.ToString()} points loaded");
                if (cnt > 0 && isResultOk) PersistentData.IsShowingPivot = false;
                Logger.Add_TPL("LoadSeriesFromDbIntoUIAsync() ended", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
            }
            return isResultOk;
        }
        #endregion utils
    }

    public interface IMapAltProfCentrer
    {
        Task CentreOnHistoryAsync();
        Task CentreOnCheckpointsAsync();
        Task CentreOnRoute0Async();
        Task CentreOnTargetAsync();
        Task CentreOnCurrentAsync();
        Task Goto2DAsync();
    }
}