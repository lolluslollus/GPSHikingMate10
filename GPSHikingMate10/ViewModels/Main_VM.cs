using LolloGPS.Core;
using LolloGPS.Data;
using LolloGPS.Data.Runtime;
using LolloGPS.Data.TileCache;
using LolloGPS.GPSInteraction;
using LolloGPS.GPX;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Utilz;
using Utilz.Data;
using Windows.ApplicationModel.Activation;
using Windows.Phone.UI.Input;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;

namespace LolloGPS.ViewModels
{
    public sealed class MainVM : OpenableObservableData, Utilz.Controlz.IBackPressedRaiser
    {
        #region IBackPressedRaiser
        public event EventHandler<Utilz.Controlz.BackOrHardSoftKeyPressedEventArgs> BackOrHardSoftKeyPressed;
        #endregion IBackPressedRaiser


        #region properties
        private const double MIN_ALTITUDE_M_ABS = .1;
        private const double MAX_ALTITUDE_M_ABS = 10000.0;

        public PersistentData PersistentData { get { return App.PersistentData; } }
        public RuntimeData RuntimeData { get { return App.RuntimeData; } }

        private readonly IMapAltProfCentrer _lolloMap = null;
        private readonly IMapAltProfCentrer _altitudeProfiles = null;
        private readonly GPSInteractor _gpsInteractor = null;
        public GPSInteractor GPSInteractor { get { return _gpsInteractor; } }

        private volatile bool _isDrawing = true; // always written under _isOpenSemaphore
        public bool IsDrawing { get { return _isDrawing; } private set { if (_isDrawing != value) { _isDrawing = value; RaisePropertyChanged_UI(); } } }
        private volatile bool _isLoading = false; // always set under _isOpenSemaphore
        public bool IsLoading { get { return _isLoading; } private set { _isLoading = value; RaisePropertyChanged_UI(); } }
        private volatile bool _isSaving = false; // always set under _isOpenSemaphore
        public bool IsSaving { get { return _isSaving; } private set { _isSaving = value; RaisePropertyChanged_UI(); } }

        private string _logText;
        public string LogText { get { return _logText; } set { _logText = value; RaisePropertyChanged_UI(); } }
        #endregion properties

        #region lifecycle
        private static readonly object _instanceLocker = new object();
        private readonly IOpenable _owner = null;
        private IOpenable Owner { get { return _owner; } }
        private static MainVM _instance = null;
        private static MainVM Instance { get { lock (_instanceLocker) { return _instance; } } }
        public MainVM(IMapAltProfCentrer lolloMap, IMapAltProfCentrer altitudeProfiles, IOpenable owner)
        {
            _gpsInteractor = GPSInteractor.GetInstance(PersistentData);
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

                    Task loadCheckpointsFromDb = isResuming ? Task.CompletedTask : PersistentData.LoadCheckpointsFromDbAsync(false, true);
                    Task loadHistoryFromDb = PersistentData.LoadHistoryFromDbAsync(false, true); // always load this, even if resuming, coz the bkb task may have changed it
                    Task loadRoute0FromDb = isResuming ? Task.CompletedTask : PersistentData.LoadRoute0FromDbAsync(false, true);
                    await Task.WhenAll(loadCheckpointsFromDb, loadHistoryFromDb, loadRoute0FromDb).ConfigureAwait(false);
                });

                await _gpsInteractor.OpenAsync(args);
                RuntimeData.GetInstance().IsAllowCentreOnCurrent = true;
                AddHandlers_DataChanged();
                await RunInUiThreadAsync(delegate
                {
                    KeepAlive.UpdateKeepAlive(PersistentData.IsKeepAlive);
                }).ConfigureAwait(false);

                Logger.Add_TPL($"MainVm.OpenMayOverrideAsync ran OK. There are now {PersistentData.Checkpoints.Count} checkpoints, {PersistentData.History.Count} history points and {PersistentData.Route0.Count} route points", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
            }
            catch (Exception ex)
            {
                await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename);
            }
            finally
            {
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
                Task closeGps = _gpsInteractor.CloseAsync(args);
                await Task.WhenAll(closeDb, closeKeepAlive, closeGps).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename);
            }
        }
        #endregion lifecycle

        #region event handlers
        private bool _isDataChangedHandlerActive = false;
        private void AddHandlers_DataChanged()
        {
            if (!_isDataChangedHandlerActive)
            {
                _isDataChangedHandlerActive = true;
                PersistentData.PropertyChanged += OnPersistentData_PropertyChanged;
            }
        }

        private void RemoveHandlers_DataChanged()
        {
            if (PersistentData != null)
            {
                PersistentData.PropertyChanged -= OnPersistentData_PropertyChanged;
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
        }
        #endregion event handlers

        #region services
        public void GoBackMyButtonSoft()
        {
            var args = new Utilz.Controlz.BackOrHardSoftKeyPressedEventArgs();
            BackOrHardSoftKeyPressed?.Invoke(this, args);
            if (!args.Handled) PersistentData.IsShowingPivot = false;
        }
        public void GoBackTabletSoft(object sender, BackRequestedEventArgs e)
        {
            if (PersistentData.IsBackButtonEnabled && e != null) e.Handled = true;
            var args = new Utilz.Controlz.BackOrHardSoftKeyPressedEventArgs();
            BackOrHardSoftKeyPressed?.Invoke(sender, args);
            if (!args.Handled) PersistentData.IsShowingPivot = false;
        }
        public void GoBackHard(object sender, BackPressedEventArgs e)
        {
            if (PersistentData.IsBackButtonEnabled && e != null) e.Handled = true;
            var args = new Utilz.Controlz.BackOrHardSoftKeyPressedEventArgs();
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
            if (currrent == null || persistentData == null) return;

            Task upd = currrent.UpdateUIEditablePropertiesAsync(persistentData.Target, PersistentData.Tables.History).ContinueWith(delegate
            {
                PointRecord currentClone = null;
                PointRecord.Clone(currrent, ref currentClone);
                Task add = persistentData.TryAddPointToCheckpointsAsync(currentClone);
            });
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

        #region load and save with picker
        private async Task<MainVM> GetCurrentOpenInstanceAsync()
        {
            // wait for the UI to be ready, like the user would do, so we don't get in at unexpected instants.
            // This means, wait for Main to be open.
            int cnt = 0;
            while (Instance?.Owner?.IsOpen != true)
            {
                cnt++; if (cnt > 200) return null;
                //Logger.Add_TPL($"PickLoadSeriesFromFileAsync is waiting for MainVM to reopen; Instance is there = {(Instance != null).ToString()}; Instance is open = {Instance?.IsOpen == true}", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
                //Logger.Add_TPL($"PickLoadSeriesFromFileAsync is waiting for Main to reopen; Owner is there = {(Instance.Owner != null).ToString()}; Owner is open = {Instance.Owner?.IsOpen == true}", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
                await Task.Delay(25).ConfigureAwait(false);
            }
            Logger.Add_TPL($"PickLoadSeriesFromFileAsync has got an open instance", Logger.AppEventsLogFilename, Logger.Severity.Info, false);

            return Instance;
        }
        internal async Task PickLoadSeriesFromFileAsync(PersistentData.Tables whichSeries)
        {
            if (!_isOpen || CancToken.IsCancellationRequested) return;

            SetLastMessage_UI("reading GPX file...");
            Logger.Add_TPL($"PickLoadSeriesFromFileAsync will open a picker for series {whichSeries.ToString()}", Logger.AppEventsLogFilename, Logger.Severity.Info, false);

            // the picker triggers a suspend on phones. If so, execution will be cancelled immediately.
            // It will then resume and come back here, but with a new instance, since I have IOpenable.
            var file = await Pickers.PickOpenFileAsync(new string[] { ConstantData.GPX_EXTENSION }, ConstantData.PICK_OPEN_FILE_TOKEN);
            Logger.Add_TPL($"PickLoadSeriesFromFileAsync has picked file {file?.Name ?? "NONE"} for series {whichSeries.ToString()}", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
            // LOLLO NOTE at this point, OnResuming() has just started, if the app was suspended. This is the case with phones. Again, I am on a different instance now.
            if (file == null) return;

            var instance = await GetCurrentOpenInstanceAsync().ConfigureAwait(false);
            if (instance == null) return;

            await instance.RunFunctionIfOpenAsyncT(() =>
            {
                try
                {
                    instance.IsSaving = true;
                    return instance.LoadFile2Async(file, whichSeries == PersistentData.Tables.Checkpoints, whichSeries == PersistentData.Tables.Route0, instance.CancToken);
                }
                finally
                {
                    instance.IsSaving = false;
                }
            }).ConfigureAwait(false);
        }
        internal async Task PickSaveSeriesToFileAsync(PersistentData.Tables whichSeries, string fileNameSuffix)
        {
            if (!_isOpen || CancToken.IsCancellationRequested) return;

            SetLastMessage_UI("saving GPX file...");
            Logger.Add_TPL($"PickSaveSeriesToFileAsync will open a picker for series {whichSeries.ToString()}", Logger.AppEventsLogFilename, Logger.Severity.Info, false);

            var series = await PersistentData.GetSeriesCloneAsync(whichSeries);
            DateTime fileCreationDateTime = DateTime.Now;
            // the picker triggers a suspend on phones. If so, execution will be cancelled immediately.
            // It will then resume and come back here, but with a new instance, since I have IOpenable.
            var file = await Pickers.PickSaveFileAsync(new string[] { ConstantData.GPX_EXTENSION }, ConstantData.PICK_SAVE_FILE_TOKEN, fileCreationDateTime.ToString(ConstantData.GPX_DATE_TIME_FORMAT_ONLY_LETTERS_AND_NUMBERS, CultureInfo.InvariantCulture) + fileNameSuffix).ConfigureAwait(false);
            Logger.Add_TPL($"PickSaveSeriesToFileAsync has picked file {file?.Name ?? "NONE"} for series {whichSeries.ToString()}", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
            // LOLLO NOTE at this point, OnResuming() has just started, if the app was suspended. This is the case with phones. Again, I am on a different instance now.
            if (file == null) return;

            var instance = await GetCurrentOpenInstanceAsync().ConfigureAwait(false);
            if (instance == null) return;

            await instance.RunFunctionIfOpenAsyncT(async () =>
            {
                try
                {
                    instance.IsLoading = true;
                    var saveResult = await instance.SaveSeriesToFileAsync(series, whichSeries, fileCreationDateTime, file, instance.CancToken).ConfigureAwait(false);
                }
                finally
                {
                    instance.IsLoading = false;
                }
            }).ConfigureAwait(false);
        }

        // LOLLO NOTE check https://social.msdn.microsoft.com/Forums/sqlserver/en-US/13002ba6-6e59-47b8-a746-c05525953c5a/uwpfileopenpicker-bugs-in-win-10-mobile-when-not-debugging?forum=wpdevelop
        // and AnalyticsVersionInfo.DeviceFamily
        // for picker details
        internal async Task PickCustomTileFolderAsync()
        {
            if (!_isOpen || CancToken.IsCancellationRequested) return;

            Logger.Add_TPL($"PickCustomTileFolderAsync will open a picker", Logger.AppEventsLogFilename, Logger.Severity.Info, false);

            // the picker triggers a suspend on phones. If so, execution will be cancelled immediately.
            // It will then resume and come back here, but with a new instance, since I have IOpenable.
            var dir = await Pickers.PickFolderAsync(TileCacheReaderWriter.AllowedExtensions.Select(ext => $".{ext}").ToArray(), ConstantData.PICK_FOLDER_TOKEN);
            Logger.Add_TPL($"PickCustomTileFolderAsync has picked dir {dir?.Name ?? "NONE"}", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
            // LOLLO NOTE at this point, OnResuming() has just started, if the app was suspended. This is the case with phones. Again, I am on a different instance now.
            if (dir == null) return;

            Pickers.SetLastPickedFolder(dir, dir.Path);

            var instance = await GetCurrentOpenInstanceAsync().ConfigureAwait(false);
            if (instance == null) return;

            await instance.RunFunctionIfOpenAsyncA(() =>
            {
                instance.PersistentData.TestTileSource.TileSourceFolderPath = dir.Path;
            }).ConfigureAwait(false);
        }
        #endregion load and save with picker

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
            Logger.Add_TPL($"MainVM.LoadFile2Async() is starting, isOpen = {_isOpen}", Logger.AppEventsLogFilename, Logger.Severity.Error, true);
            try
            {
                IsDrawing = true;
                // get file data into DB
                var whichTables = await LoadFileIntoDbAsync(file, doCheckpoints, doRoutes0, cancToken).ConfigureAwait(false);
                if (whichTables == null)
                {
                    Logger.Add_TPL("MainVM.LoadFile2Async() loaded no files into the db", Logger.AppEventsLogFilename, Logger.Severity.Error, true);
                    return;
                }
                Logger.Add_TPL($"MainVM.LoadFile2Async() loaded {whichTables.Count} file(s) into the db", Logger.AppEventsLogFilename, Logger.Severity.Info, false);

                // get file data from DB into UI
                if (cancToken.IsCancellationRequested) return;

                foreach (var series in whichTables)
                {
                    await LoadSeriesFromDbIntoUIAsync(series).ConfigureAwait(false);
                }
                Logger.Add_TPL("MainVM.LoadFile2Async() ended proc OK", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
            }
            catch (OperationCanceledException) { }
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
            }
            return isResultOk;
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
        private async Task<Tuple<bool, bool>> SaveSeriesToFileAsync(IReadOnlyCollection<PointRecord> series, PersistentData.Tables whichSeries, DateTime fileCreationDateTime, StorageFile file, CancellationToken cancToken)
        {
            Logger.Add_TPL($"MainVm.SaveSeriesToFileAsync() started with file == null = {(file == null).ToString()} and whichSeries = {whichSeries} and isOpen = {_isOpen}", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
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
                    Logger.Add_TPL($"MainVm.SaveSeriesToFileAsync has saved series {whichSeries.ToString()} to a file with result ok = {readerWriterResult.Item1.ToString()} and message = {readerWriterResult.Item2}", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
                }).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { isCancelled = true; }
            catch (Exception) { }
            finally
            {
                // inform the user about the outcome
                if (readerWriterResult?.Item1 == true) SetLastMessage_UI($"{PersistentData.GetTextForSeries(whichSeries)} saved");
                else SetLastMessage_UI($"could not save {PersistentData.GetTextForSeries(whichSeries)}");
                Logger.Add_TPL($"MainVm.SaveSeriesToFileAsync() ended with result good = {isResultOk}", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
            }
            return Tuple.Create(isResultOk, isCancelled);
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