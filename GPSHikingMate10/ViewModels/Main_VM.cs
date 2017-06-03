﻿using GPX;
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

        private readonly IMapAltProfCentrer _lolloMap = null;

        private readonly IMapAltProfCentrer _altitudeProfiles = null;

        public PersistentData PersistentData { get { return App.PersistentData; } }
        public RuntimeData RuntimeData { get { return App.RuntimeData; } }

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

        private bool _isWideEnough = false;
        public bool IsWideEnough { get { return _isWideEnough; } set { if (_isWideEnough != value) { _isWideEnough = value; RaisePropertyChanged_UI(); } } }

        private readonly object _loadSaveLocker = new object();
        public bool IsLoading
        {
            get
            {
                lock (_loadSaveLocker)
                {
                    return RegistryAccess.GetValue(ConstantData.REG_LOAD_SERIES_IS_LOADING) == true.ToString();
                }
            }
            private set
            {
                lock (_loadSaveLocker)
                {
                    if (RegistryAccess.TrySetValue(ConstantData.REG_LOAD_SERIES_IS_LOADING, value.ToString()))
                    {
                        RaisePropertyChangedUrgent_UI();
                    }
                }
            }
        }
        private bool TrySetIsLoading(bool newValue)
        {
            lock (_loadSaveLocker)
            {
                if (IsLoading == newValue) return false;
                else
                {
                    IsLoading = newValue;
                    return true;
                }
            }
        }

        public bool IsSaving
        {
            get
            {
                lock (_loadSaveLocker)
                {
                    return RegistryAccess.GetValue(ConstantData.REG_SAVE_SERIES_IS_SAVING) == true.ToString();
                }
            }
            private set
            {
                lock (_loadSaveLocker)
                {
                    if (RegistryAccess.TrySetValue(ConstantData.REG_SAVE_SERIES_IS_SAVING, value.ToString()))
                    {
                        RaisePropertyChangedUrgent_UI();
                    }
                }
            }
        }
        private bool TrySetIsSaving(bool newValue)
        {
            lock (_loadSaveLocker)
            {
                if (IsSaving == newValue) return false;
                else
                {
                    IsSaving = newValue;
                    return true;
                }
            }
        }

        private string _logText;
        public string LogText { get { return _logText; } set { _logText = value; RaisePropertyChanged_UI(); } }

        private readonly object _whichSeriesLocker = new object();
        private PersistentData.Tables _whichSeriesJustLoaded = PersistentData.Tables.Nil;

        public PersistentData.Tables WhichSeriesJustLoaded
        {
            get
            {
                lock (_whichSeriesLocker)
                {
                    return _whichSeriesJustLoaded;
                }
            }
            private set
            {
                lock (_whichSeriesLocker)
                {
                    _whichSeriesJustLoaded = value;
                }
            }
        }

        //private bool _isPointInfoPanelOpen = false;
        //public bool IsPointInfoPanelOpen { get { return _isPointInfoPanelOpen; } set { _isPointInfoPanelOpen = value; } }
        #endregion properties

        #region lifecycle
        public MainVM(bool isWideEnough, IMapAltProfCentrer lolloMap, IMapAltProfCentrer altitudeProfiles)
        {
            _gpsInteractor = GPSInteractor.GetInstance(PersistentData);
            _tileCacheClearer = TileCacheClearer.GetInstance();
            IsWideEnough = isWideEnough;
            _lolloMap = lolloMap;
            _altitudeProfiles = altitudeProfiles;
        }

        protected override async Task OpenMayOverrideAsync(object args = null)
        {
            try
            {
                RuntimeData.SetIsDBDataRead_UI(false);
                await Task.Run(delegate
                {
                    PersistentData.OpenMainDb();
                    // When resuming, settings and data are already in.
                    // However, reread the history coz the background task may have changed it while I was suspended.
                    bool isResuming = args != null && (LifecycleEvents)args == LifecycleEvents.Resuming;
                    Task loadCheckpoints = isResuming ? Task.CompletedTask : PersistentData.LoadCheckpointsFromDbAsync(false, false);
                    Task loadHistory = PersistentData.LoadHistoryFromDbAsync(false, false);
                    Task loadRoute0 = isResuming ? Task.CompletedTask : PersistentData.LoadRoute0FromDbAsync(false, false);
                    return Task.WhenAll(loadCheckpoints, loadHistory, loadRoute0);
                });
                RuntimeData.SetIsDBDataRead_UI(true);

                WhichSeriesJustLoaded = PersistentData.Tables.Nil;

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

                if (IsLoading)
                {
                    var file = await Pickers.GetLastPickedOpenFileAsync().ConfigureAwait(false);
                    if (file != null)
                    {
                        PersistentData.Tables whichSeries = PersistentData.Tables.Nil;
                        if (Enum.TryParse(
                            RegistryAccess.GetValue(ConstantData.REG_LOAD_SERIES_WHICH_SERIES),
                            out whichSeries)
                            && whichSeries != PersistentData.Tables.Nil)
                        {
                            await ContinueAfterPickLoadSeriesFromFileAsync(file, whichSeries).ConfigureAwait(false);
                        }
                    }
                }
                if (IsSaving)
                {
                    var file = await Pickers.GetLastPickedSaveFileAsync().ConfigureAwait(false);
                    if (file != null)
                    {
                        PersistentData.Tables whichSeries = PersistentData.Tables.Nil;
                        if (Enum.TryParse(
                            RegistryAccess.GetValue(ConstantData.REG_SAVE_SERIES_WHICH_SERIES),
                            out whichSeries)
                            && whichSeries != PersistentData.Tables.Nil)
                        {
                            DateTime fileCreationDateTime = default(DateTime);
                            if (DateTime.TryParseExact(
                                RegistryAccess.GetValue(ConstantData.REG_SAVE_SERIES_FILE_CREATION_DATE_TIME),
                                ConstantData.GPX_DATE_TIME_FORMAT,
                                CultureInfo.CurrentUICulture,
                                DateTimeStyles.None,
                                out fileCreationDateTime))
                            {
                                var series = PersistentData.GetSeries(whichSeries);
                                await ContinueAfterPickSaveSeriesToFileAsync(series, whichSeries, fileCreationDateTime, file).ConfigureAwait(false);
                            }
                        }
                    }
                }

                Logger.Add_TPL("MainVM.OpenMayOverrideAsync() ran OK", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
            }
            catch (Exception ex)
            {
                await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename);
            }
            finally
            {
                IsLoading = false;
                IsSaving = false;
                //_whichSeriesJustLoaded = PersistentData.Tables.nil; NO!
            }
        }
        protected override async Task CloseMayOverrideAsync(object args = null)
        {
            try
            {
                RemoveHandlers_DataChanged();
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
            PersistentData.LastMessage = isScheduled ? "cache will be cleared asap" : "cache busy";
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

            Task vibrate = Task.Run(() => App.ShortVibration());

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
            if (!TrySetIsSaving(true) || whichSeries == PersistentData.Tables.Nil) return;
            SetLastMessage_UI("saving GPX file...");

            SwitchableObservableCollection<PointRecord> series = PersistentData.GetSeries(whichSeries);
            DateTime fileCreationDateTime = DateTime.Now;

            var file = await Pickers.PickSaveFileAsync(new string[] { ConstantData.GPX_EXTENSION }, fileCreationDateTime.ToString(ConstantData.GPX_DATE_TIME_FORMAT_ONLY_LETTERS_AND_NUMBERS, CultureInfo.InvariantCulture) + fileNameSuffix).ConfigureAwait(false);
            if (file != null)
            {
                RegistryAccess.TrySetValue(ConstantData.REG_SAVE_SERIES_WHICH_SERIES, whichSeries.ToString());
                RegistryAccess.TrySetValue(ConstantData.REG_SAVE_SERIES_FILE_CREATION_DATE_TIME, fileCreationDateTime.ToString(ConstantData.GPX_DATE_TIME_FORMAT, CultureInfo.CurrentUICulture));

                // LOLLO NOTE at this point, OnResuming() has just started, if the app was suspended. 
                await RunFunctionIfOpenAsyncT(
                    () => ContinueAfterPickSaveSeriesToFileAsync(series, whichSeries, fileCreationDateTime, file)).ConfigureAwait(false);
            }
            else
            {
                SetLastMessage_UI("Saving cancelled");
                IsSaving = false;
            }
        }

        /// <summary>
        /// This method is called in a separate task on low-memory phones.
        /// It always runs under _isOpenSEmaphore.
        /// </summary>
        /// <param name="series"></param>
        /// <param name="whichSeries"></param>
        /// <param name="fileCreationDateTime"></param>
        /// <param name="file"></param>
        /// <returns></returns>
        private async Task ContinueAfterPickSaveSeriesToFileAsync(IReadOnlyCollection<PointRecord> series, PersistentData.Tables whichSeries, DateTime fileCreationDateTime, StorageFile file)
        {
            Logger.Add_TPL("ContinueAfterPickSaveSeriesToFileAsync() started with file == null = " + (file == null).ToString() + " and whichSeries = " + whichSeries + " and isOpen = " + _isOpen, Logger.AppEventsLogFilename, Logger.Severity.Info, false);

            Tuple<bool, string> result = Tuple.Create(false, "");
            try
            {
                if (file != null && whichSeries != PersistentData.Tables.Nil)
                {
                    if (CancToken.IsCancellationRequested) return;
                    await Task.Run(async delegate
                    {
                        if (CancToken.IsCancellationRequested) return;
                        SetLastMessage_UI("saving GPX file...");

                        result = await ReaderWriter.SaveAsync(file, series, fileCreationDateTime, whichSeries, CancToken).ConfigureAwait(false);
                    }).ConfigureAwait(false);
                }
            }
            catch (Exception) { }
            finally
            {
                // inform the user about the result
                if (result != null && result.Item1) SetLastMessage_UI(result.Item2);
                else if (whichSeries != PersistentData.Tables.Nil) SetLastMessage_UI(string.Format("could not save {0}", PersistentData.GetTextForSeries(whichSeries)));
                else SetLastMessage_UI("could not save file");

                IsSaving = false;
            }
        }

        internal async Task PickLoadSeriesFromFileAsync(PersistentData.Tables whichSeries)
        {
            if (!TrySetIsLoading(true)) return;
            SetLastMessage_UI("reading GPX file...");

            var file = await Pickers.PickOpenFileAsync(new string[] { ConstantData.GPX_EXTENSION }).ConfigureAwait(false);
            if (file != null)
            {
                RegistryAccess.TrySetValue(ConstantData.REG_LOAD_SERIES_WHICH_SERIES, whichSeries.ToString());

                Logger.Add_TPL("Pick open file about to open series = " + whichSeries.ToString(), Logger.AppEventsLogFilename, Logger.Severity.Info, false);
                // LOLLO NOTE at this point, OnResuming() has just started, if the app was suspended. 
                bool isDone = await RunFunctionIfOpenAsyncT(() => ContinueAfterPickLoadSeriesFromFileAsync(file, whichSeries)).ConfigureAwait(false);
                Logger.Add_TPL("Pick open file has opened series = " + isDone.ToString(), Logger.AppEventsLogFilename, Logger.Severity.Info, false);
            }
            else
            {
                Logger.Add_TPL("Pick open file cancelled, series = " + whichSeries.ToString(), Logger.AppEventsLogFilename, Logger.Severity.Info, false);
                SetLastMessage_UI("Loading cancelled");
                IsLoading = false;
            }
        }

        // LOLLO NOTE check https://social.msdn.microsoft.com/Forums/sqlserver/en-US/13002ba6-6e59-47b8-a746-c05525953c5a/uwpfileopenpicker-bugs-in-win-10-mobile-when-not-debugging?forum=wpdevelop
        // and AnalyticsVersionInfo.DeviceFamily
        // for picker details

        /// <summary>
        /// This method is called in a separate task on low-memory phones.
        /// It always runs under _isOpenSEmaphore.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="whichSeries"></param>
        /// <returns></returns>
        private async Task ContinueAfterPickLoadSeriesFromFileAsync(StorageFile file, PersistentData.Tables whichSeries)
        {
            Logger.Add_TPL("ContinueAfterPickLoadSeriesFromFileAsync() started with file == null = " + (file == null).ToString() + " and whichSeries = " + whichSeries + " and isOpen = " + _isOpen, Logger.AppEventsLogFilename, Logger.Severity.Info, false);

            Tuple<bool, string> result = Tuple.Create(false, "");

            try
            {
                if (file != null && whichSeries != PersistentData.Tables.Nil)
                {
                    if (CancToken.IsCancellationRequested) return;
                    await Task.Run(async delegate
                    {
                        SetLastMessage_UI("reading GPX file...");

                        if (CancToken.IsCancellationRequested) return;

                        // load the file
                        result = await ReaderWriter.LoadSeriesFromFileIntoDbAsync(file, whichSeries, CancToken).ConfigureAwait(false);
                        if (CancToken.IsCancellationRequested) return;
                        Logger.Add_TPL("ContinueAfterPickLoadSeriesFromFileAsync() loaded series into db", Logger.AppEventsLogFilename, Logger.Severity.Info, false);

                        // update the UI with the file data
                        if (result?.Item1 == true)
                        {
                            switch (whichSeries)
                            {
                                case PersistentData.Tables.Route0:
                                    WhichSeriesJustLoaded = PersistentData.Tables.Route0;
                                    int cntR = await PersistentData.LoadRoute0FromDbAsync(true, true).ConfigureAwait(false);
                                    Logger.Add_TPL("ContinueAfterPickLoadSeriesFromFileAsync() loaded " + cntR + " route0 points into UI", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
                                    CentreOnSeriesDelayed(PersistentData.Tables.Route0);
                                    break;
                                case PersistentData.Tables.Checkpoints:
                                    WhichSeriesJustLoaded = PersistentData.Tables.Checkpoints;
                                    int cntC = await PersistentData.LoadCheckpointsFromDbAsync(true, true).ConfigureAwait(false);
                                    Logger.Add_TPL("ContinueAfterPickLoadSeriesFromFileAsync() loaded " + cntC + " checkpoints into UI", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
                                    CentreOnSeriesDelayed(PersistentData.Tables.Checkpoints);
                                    break;
                                default:
                                    WhichSeriesJustLoaded = PersistentData.Tables.Nil;
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
                else if (whichSeries != PersistentData.Tables.Nil) SetLastMessage_UI(string.Format("could not load {0}", PersistentData.GetTextForSeries(whichSeries)));
                else SetLastMessage_UI("could not load file");

                IsLoading = false;
                Logger.Add_TPL("ContinueAfterPickLoadSeriesFromFileAsync() ended", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
            }
        }
        #endregion save and load with picker


        #region open app through file
        public async Task LoadFileAsync(FileActivatedEventArgs args)
        {
            // get file data into DB
            var whichTables = await LoadFileIntoDbAsync(args).ConfigureAwait(false);
            if (whichTables == null)
            {
                Logger.Add_TPL("OnFileActivated() loaded no files into the db", Logger.AppEventsLogFilename, Logger.Severity.Error, true);
                return;
            }
            Logger.Add_TPL($"OnFileActivated() loaded {whichTables.Count} file(s) into the db", Logger.AppEventsLogFilename, Logger.Severity.Info, false);

            // get file data from DB into UI
            foreach (var series in whichTables)
            {
                switch (series)
                {
                    case PersistentData.Tables.History:
                        await PersistentData.LoadHistoryFromDbAsync(true, true).ConfigureAwait(false);
                        break;
                    case PersistentData.Tables.Route0:
                        await PersistentData.LoadRoute0FromDbAsync(true, true).ConfigureAwait(false);
                        break;
                    case PersistentData.Tables.Checkpoints:
                        await PersistentData.LoadCheckpointsFromDbAsync(true, true).ConfigureAwait(false);
                        break;
                    default:
                        break;
                }
                Logger.Add_TPL("OnFileActivated() loaded series " + series.ToString() + " into PersistentData", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
            }

            // centre view on the file data
            CentreOnSeriesDelayed(whichTables.FirstOrDefault());
            Logger.Add_TPL("OnFileActivated() ended proc OK", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
        }
        /// <summary>
        /// This method is called in a separate task on low-memory phones
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private async Task<List<PersistentData.Tables>> LoadFileIntoDbAsync(FileActivatedEventArgs args)
        {
            List<PersistentData.Tables> result = new List<PersistentData.Tables>();
            await RunFunctionIfOpenAsyncT_MT(async delegate
            {
                if (args?.Files?.Count > 0 && args.Files[0] is StorageFile)
                {
                    Tuple<bool, string> checkpointsResult = Tuple.Create(false, "");
                    Tuple<bool, string> route0Result = Tuple.Create(false, "");

                    try
                    {
                        if (CancToken.IsCancellationRequested) return;
                        SetLastMessage_UI("reading GPX file...");
                        // load the file, attempting to read checkpoints and route. GPX files can contain both.
                        StorageFile file = args.Files[0] as StorageFile;
                        if (CancToken.IsCancellationRequested) return;
                        checkpointsResult = await ReaderWriter.LoadSeriesFromFileIntoDbAsync(file, PersistentData.Tables.Checkpoints, CancToken).ConfigureAwait(false);
                        if (CancToken.IsCancellationRequested) return;
                        route0Result = await ReaderWriter.LoadSeriesFromFileIntoDbAsync(file, PersistentData.Tables.Route0, CancToken).ConfigureAwait(false);
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
                }
            }).ConfigureAwait(false);
            return result;
        }
        #endregion open app through file
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