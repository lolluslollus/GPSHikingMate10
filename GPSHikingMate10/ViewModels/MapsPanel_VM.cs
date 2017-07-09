using LolloGPS.Core;
using LolloGPS.Data;
using LolloGPS.Data.Runtime;
using LolloGPS.Data.TileCache;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Utilz;
using Utilz.Controlz;
using Utilz.Data;

namespace LolloGPS.ViewModels
{
    public class MapsPanelVM : OpenableObservableData
    {
        public PersistentData PersistentData { get { return App.PersistentData; } }
        public RuntimeData RuntimeData { get { return App.RuntimeData; } }

        private readonly LolloMapVM _lolloMapVM;
        private readonly MainVM _mainVM;
        private readonly TileCacheClearerSaver _tileCacheClearerSaver = null;

        // the following are always updated under _isOpenSemaphore
        private bool _isShowZoomLevelChoices = false;
        public bool IsShowZoomLevelChoices { get { return _isShowZoomLevelChoices; } private set { _isShowZoomLevelChoices = value; RaisePropertyChanged_UI(); } }
        private Collection<TextAndTag> _zoomLevelChoices;
        public Collection<TextAndTag> ZoomLevelChoices { get { return _zoomLevelChoices; } private set { _zoomLevelChoices = value; RaisePropertyChanged_UI(); } }

        // the following bools should be volatile, instead we choose to only read and write them in the UI thread.
        private bool _isClearCustomCacheEnabled = false;
        public bool IsClearCustomCacheEnabled { get { return _isClearCustomCacheEnabled; } private set { if (_isClearCustomCacheEnabled != value) { _isClearCustomCacheEnabled = value; RaisePropertyChanged_UI(); } } }
        private bool _isClearOrSaveCacheEnabled = false;
        public bool IsClearOrSaveCacheEnabled { get { return _isClearOrSaveCacheEnabled; } private set { if (_isClearOrSaveCacheEnabled != value) { _isClearOrSaveCacheEnabled = value; RaisePropertyChanged_UI(); } } }
        //private bool _isCacheBtnEnabled = false;
        //public bool IsCacheBtnEnabled { get { return _isCacheBtnEnabled; } private set { if (_isCacheBtnEnabled != value) { _isCacheBtnEnabled = value; RaisePropertyChanged_UI(); } } }
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

        private List<TextAndTag> _selectedBaseTiles = new List<TextAndTag>();
        public List<TextAndTag> SelectedBaseTiles { get { return _selectedBaseTiles; } private set { _selectedBaseTiles = value; RaisePropertyChanged_UI(); } }
        private List<TextAndTag> _selectedOverlayTiles = new List<TextAndTag>();
        public List<TextAndTag> SelectedOverlayTiles { get { return _selectedOverlayTiles; } private set { _selectedOverlayTiles = value; RaisePropertyChanged_UI(); } }
        private readonly SwitchableObservableCollection<TextAndTag> _baseTileSourceChoices = new SwitchableObservableCollection<TextAndTag>();
        public SwitchableObservableCollection<TextAndTag> BaseTileSourceChoices { get { return _baseTileSourceChoices; } }
        private readonly SwitchableObservableCollection<TextAndTag> _overlayTileSourceChoices = new SwitchableObservableCollection<TextAndTag>();
        public SwitchableObservableCollection<TextAndTag> OverlayTileSourceChoices { get { return _overlayTileSourceChoices; } }

        private List<TextAndTag> _modelTileSources = new List<TextAndTag>();
        public List<TextAndTag> ModelTileSources { get { return _modelTileSources; } private set { _modelTileSources = value; RaisePropertyChanged_UI(); } }

        private readonly SwitchableObservableCollection<ObservableKeyAndValue> _requestHeaders = new SwitchableObservableCollection<ObservableKeyAndValue>();
        public SwitchableObservableCollection<ObservableKeyAndValue> RequestHeaders { get { return _requestHeaders; } }

        private readonly SwitchableObservableCollection<ObservableString> _uriStrings = new SwitchableObservableCollection<ObservableString>();
        public SwitchableObservableCollection<ObservableString> UriStrings { get { return _uriStrings; } }

        public string SampleLocalUriString { get { return TileSourceRecord.SampleLocalUriString; } }
        public string SampleRemoteUriString { get { return TileSourceRecord.SampleRemoteUriString; } }

        #region lifecycle
        private static readonly object _instanceLocker = new object();
        private static MapsPanelVM _instance = null;
        private static MapsPanelVM Instance { get { lock (_instanceLocker) { return _instance; } } }
        private readonly IOpenable _owner = null;
        private IOpenable Owner { get { return _owner; } }

        public MapsPanelVM(LolloMapVM lolloMapVM, MainVM mainVM, IOpenable owner)
        {
            _lolloMapVM = lolloMapVM;
            _mainVM = mainVM;
            _tileCacheClearerSaver = TileCacheClearerSaver.GetInstance();
            lock (_instanceLocker)
            {
                _instance = this;
            }
            _owner = owner;
        }
        protected override async Task OpenMayOverrideAsync(object args = null)
        {
            await _tileCacheClearerSaver.OpenAsync(args);

            AddHandlers_DataChanged();

            ICollection<TileSourceRecord> allTileSources = null;
            ICollection<TileSourceRecord> currentTileSources = null;
            await Task.Run(async () =>
            {
                allTileSources = await PersistentData.GetAllTileSourcezCloneAsync(CancToken);
                currentTileSources = await PersistentData.GetCurrentTileSourcezCloneAsync(CancToken);
            });

            await RunInUiThreadAsync(delegate
            {
                UpdateIsClearCacheEnabled();
                UpdateIsClearCustomCacheEnabled(allTileSources);
                //UpdateIsCacheBtnEnabled(currentTileSources);
                UpdateIsLeechingEnabled(currentTileSources);
                UpdateIsChangeTileSourceEnabled();
                UpdateIsTestCustomTileSourceEnabled();
                UpdateIsChangeMapStyleEnabled(currentTileSources);
                UpdateTileSourceChoices(allTileSources);
                UpdateSelectedBaseTile(currentTileSources);
                UpdateSelectedOverlayTiles(currentTileSources);
                UpdateModelTileSources();
                UpdateRequestHeaders();
                UpdateUriStrings();
            }).ConfigureAwait(false);
        }

        protected override async Task CloseMayOverrideAsync(object args = null)
        {
            RemoveHandlers_DataChanged();
            await _tileCacheClearerSaver.CloseAsync(args);
        }
        #endregion lifecycle

        #region updaters
        private void UpdateIsClearCustomCacheEnabled(ICollection<TileSourceRecord> allTileSources)
        {
            IsClearCustomCacheEnabled = !_tileCacheClearerSaver.IsClearingScheduled
            && !PersistentData.IsTileSourcezBusy
            && allTileSources?.Any(ts => ts.IsCustom) == true;
        }
        private void UpdateIsClearCacheEnabled()
        {
            IsClearOrSaveCacheEnabled = !_tileCacheClearerSaver.IsClearingScheduled
            && !PersistentData.IsTileSourcezBusy;
        }
        //private void UpdateIsCacheBtnEnabled(ICollection<TileSourceRecord> currentTileSources)
        //{
        //    IsCacheBtnEnabled = currentTileSources.Any(ts => !ts.IsDefault);
        //}
        private void UpdateIsLeechingEnabled(ICollection<TileSourceRecord> currentTileSources)
        {
            IsLeechingEnabled = !PersistentData.IsTilesDownloadDesired
            && RuntimeData.IsConnectionAvailable
            && !_tileCacheClearerSaver.IsClearingScheduled
            && !PersistentData.IsTileSourcezBusy
            && currentTileSources?.Any(ts => !ts.IsDefault) == true;
        }
        private void UpdateIsChangeTileSourceEnabled()
        {
            IsChangeTileSourceEnabled = !_tileCacheClearerSaver.IsClearingScheduled
            && !PersistentData.IsTileSourcezBusy;
        }
        private void UpdateIsTestCustomTileSourceEnabled()
        {
            IsTestCustomTileSourceEnabled = !_tileCacheClearerSaver.IsClearingScheduled
            && !PersistentData.IsTileSourcezBusy
            && RuntimeData.IsConnectionAvailable;
        }
        private void UpdateIsChangeMapStyleEnabled(ICollection<TileSourceRecord> currentTileSources)
        {
            if (currentTileSources == null) IsChangeMapStyleEnabled = false;
            else
            {
                if (currentTileSources.Count == 1) IsChangeMapStyleEnabled = !currentTileSources.ElementAt(0).IsDefault;
                else IsChangeMapStyleEnabled = true;
            }
        }
        private void UpdateTileSourceChoices(ICollection<TileSourceRecord> allTileSources)
        {
            List<TextAndTag> baseTileSources = new List<TextAndTag>();
            List<TextAndTag> overlayTileSources = new List<TextAndTag>();
            if (allTileSources != null)
            {
                foreach (var ts in allTileSources)
                {
                    if (ts.IsOverlay) overlayTileSources.Add(new TextAndTag(ts.DisplayName, ts));
                    else baseTileSources.Add(new TextAndTag(ts.DisplayName, ts));
                }
            }
            _baseTileSourceChoices.ReplaceAll(baseTileSources);
            _overlayTileSourceChoices.ReplaceAll(overlayTileSources);
        }
        private void UpdateModelTileSources()
        {
            var ts = PersistentData?.ModelTileSource;
            if (ts == null)
            {
                ModelTileSources = new List<TextAndTag>();
            }
            else
            {
                ModelTileSources = ((new TextAndTag[] { new TextAndTag(ts.DisplayName, WritableTileSourceRecord.Clone(ts)) }).ToList());
            }
        }

        private void UpdateRequestHeaders()
        {
            var rhs = PersistentData?.TestTileSource?.RequestHeaders;
            if (rhs == null)
            {
                RequestHeaders.Clear();
                return;
            }

            // return if no changes. It can avoid endless loops in edge cases, and it avoids useless rerenders, which can steal the focus.
            var rhd = new Dictionary<string, string>();
            foreach (var rh in _requestHeaders)
            {
                rhd.Add(rh.Key, rh.Val);
            }
            if (rhs.OrderBy(kvp => kvp.Key).SequenceEqual(rhd.OrderBy(kvp => kvp.Key))) return;

            RequestHeaders.ReplaceAll(rhs.Select(kvp => new ObservableKeyAndValue(kvp.Key, kvp.Value)).ToList());
        }

        private void UpdateUriStrings()
        {
            var uss = PersistentData?.TestTileSource?.UriStrings;
            if (uss == null)
            {
                UriStrings.Clear();
                return;
            }

            // return if no changes. It can avoid endless loops in edge cases, and it avoids useless rerenders, which can steal the focus.
            if (uss.OrderBy(str => str).SequenceEqual(_uriStrings.Select(us => us.Str).OrderBy(str => str))) return;

            UriStrings.ReplaceAll(uss.Select(us => new ObservableString(us)));
        }

        private void UpdateSelectedBaseTile(ICollection<TileSourceRecord> currentTileSources)
        {
            var currentBaseTileSource = currentTileSources?.FirstOrDefault(ts => !ts.IsOverlay);
            if (currentBaseTileSource == null) return;
            var selectedBaseTiles = (new TextAndTag[] { new TextAndTag(currentBaseTileSource.DisplayName, currentBaseTileSource) }).ToList();
            SelectedBaseTiles = selectedBaseTiles;
        }
        private void UpdateSelectedOverlayTiles(ICollection<TileSourceRecord> currentTileSources)
        {
            var selectedOverlayTiles = new List<TextAndTag>();
            if (currentTileSources != null)
            {
                foreach (var item in currentTileSources)
                {
                    if (!item.IsOverlay) continue;
                    selectedOverlayTiles.Add(new TextAndTag(item.DisplayName, item));
                }
            }
            SelectedOverlayTiles = selectedOverlayTiles;
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
                TileCacheClearerSaver.IsClearingScheduledChanged += OnTileCache_IsClearingOrSavingScheduledChanged;
                TileCacheClearerSaver.CacheCleared += OnTileCacheClearerSaver_CacheCleared;
                TileCacheClearerSaver.IsSavingScheduledChanged += OnTileCache_IsClearingOrSavingScheduledChanged;
                TileCacheClearerSaver.CacheSaved += OnTileCacheClearerSaver_CacheSaved;
                ObservableKeyAndValue.SomethingChanged += OnKeyAndValue_SomethingChanged;
                ObservableString.SomethingChanged += OnTypedString_SomethingChanged;
            }
        }

        private void RemoveHandlers_DataChanged()
        {
            if (PersistentData != null)
            {
                PersistentData.PropertyChanged -= OnPersistentData_PropertyChanged;
                RuntimeData.PropertyChanged -= OnRuntimeData_PropertyChanged;
                TileCacheClearerSaver.IsClearingScheduledChanged -= OnTileCache_IsClearingOrSavingScheduledChanged;
                TileCacheClearerSaver.CacheCleared -= OnTileCacheClearerSaver_CacheCleared;
                TileCacheClearerSaver.IsSavingScheduledChanged -= OnTileCache_IsClearingOrSavingScheduledChanged;
                TileCacheClearerSaver.CacheSaved -= OnTileCacheClearerSaver_CacheSaved;
                ObservableKeyAndValue.SomethingChanged -= OnKeyAndValue_SomethingChanged;
                ObservableString.SomethingChanged -= OnTypedString_SomethingChanged;

                _isDataChangedHandlerActive = false;
            }
        }
        private async void OnPersistentData_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PersistentData.IsTilesDownloadDesired))
            {
                var currentTileSources = await Task.Run(() => PersistentData.GetCurrentTileSourcezCloneAsync(CancToken));
                await RunInUiThreadAsync(() =>
                {
                    UpdateIsLeechingEnabled(currentTileSources);
                }).ConfigureAwait(false);
            }
            else if (e.PropertyName == nameof(PersistentData.CurrentTileSources))
            {
                var currentTileSources = await Task.Run(() => PersistentData.GetCurrentTileSourcezCloneAsync(CancToken));
                await RunInUiThreadAsync(delegate
                {
                    UpdateIsLeechingEnabled(currentTileSources);
                    //UpdateIsCacheBtnEnabled(currentTileSources);
                    UpdateIsChangeMapStyleEnabled(currentTileSources);
                    UpdateSelectedBaseTile(currentTileSources);
                    UpdateSelectedOverlayTiles(currentTileSources);
                }).ConfigureAwait(false);
            }
            else if (e.PropertyName == nameof(PersistentData.TileSourcez))
            {
                var allTileSources = await Task.Run(() => PersistentData.GetAllTileSourcezCloneAsync(CancToken));
                await RunInUiThreadAsync(delegate
                {
                    UpdateIsClearCustomCacheEnabled(allTileSources);
                    UpdateTileSourceChoices(allTileSources);
                }).ConfigureAwait(false);
            }
            else if (e.PropertyName == nameof(PersistentData.IsTileSourcezBusy))
            {
                bool isBusy = PersistentData.IsTileSourcezBusy;
                if (isBusy)
                {
                    // if I get the tile sources now, this will only fire after the tile sources semaphore has been released, 
                    // ie too late to be useful. Not dangerous but bad UI. It's also a matter of performance.
                    await RunInUiThreadAsync(delegate
                    {
                        UpdateIsClearCacheEnabled();
                        UpdateIsClearCustomCacheEnabled(null);
                        UpdateIsLeechingEnabled(null);
                        UpdateIsChangeTileSourceEnabled();
                        UpdateIsTestCustomTileSourceEnabled();
                    }).ConfigureAwait(false);
                }
                else
                {
                    var allTileSources = await Task.Run(() => PersistentData.GetAllTileSourcezCloneAsync(CancToken));
                    var currentTileSources = await Task.Run(() => PersistentData.GetCurrentTileSourcezCloneAsync(CancToken));
                    await RunInUiThreadAsync(delegate
                    {
                        UpdateIsClearCacheEnabled();
                        UpdateIsClearCustomCacheEnabled(allTileSources);
                        UpdateIsLeechingEnabled(currentTileSources);
                        UpdateIsChangeTileSourceEnabled();
                        UpdateIsTestCustomTileSourceEnabled();
                    }).ConfigureAwait(false);
                }
            }
            else if (e.PropertyName == nameof(PersistentData.ModelTileSource))
            {
                await RunInUiThreadAsync(delegate
                {
                    UpdateModelTileSources();
                }).ConfigureAwait(false);
            }
            else if (e.PropertyName == nameof(PersistentData.TestTileSource))
            {
                await RunInUiThreadAsync(delegate
                {
                    UpdateRequestHeaders();
                    UpdateUriStrings();
                }).ConfigureAwait(false);
            }
        }

        private async void OnRuntimeData_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RuntimeData.IsConnectionAvailable))
            {
                var currentTileSources = await Task.Run(() => PersistentData.GetCurrentTileSourcezCloneAsync(CancToken));
                await RunInUiThreadAsync(delegate
                {
                    UpdateIsLeechingEnabled(currentTileSources);
                    UpdateIsTestCustomTileSourceEnabled();
                }).ConfigureAwait(false);
            }
        }

        private async void OnKeyAndValue_SomethingChanged(object sender, PropertyChangedEventArgs e)
        {
            var pd = PersistentData;
            var tts = pd?.TestTileSource;
            if (tts == null) return;

            var nd = new Dictionary<string, string>();
            foreach (var rh in RequestHeaders)
            {
                nd.Add(rh.Key, rh.Val);
            }
            // LOLLO TODO this is rather overkill, most other fields of TestTileSource are changed directly from the UI with two-way bindings.
            // Maybe I do it properly one day.
            var result = await pd.SetRequestHeadersOfTestTileSourceAsync(nd, CancToken);
            if (result?.Item1 != true) _mainVM?.SetLastMessage_UI(result?.Item2 ?? "Error setting the response headers");
        }

        private async void OnTypedString_SomethingChanged(object sender, PropertyChangedEventArgs e)
        {
            var pd = PersistentData;
            var tts = pd?.TestTileSource;
            if (tts == null) return;

            var result = await pd.SetUriStringsOfTestTileSourceAsync(_uriStrings.Select(us => us.Str).ToList(), CancToken);
            if (result?.Item1 != true) _mainVM?.SetLastMessage_UI(result?.Item2 ?? "Error setting the uri strings");
        }

        private async void OnTileCache_IsClearingOrSavingScheduledChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            var allTileSources = await Task.Run(() => PersistentData.GetAllTileSourcezCloneAsync(CancToken));
            var currentTileSources = await Task.Run(() => PersistentData.GetCurrentTileSourcezCloneAsync(CancToken));
            await RunInUiThreadAsync(delegate
            {
                UpdateIsClearCacheEnabled();
                UpdateIsClearCustomCacheEnabled(allTileSources);
                UpdateIsLeechingEnabled(currentTileSources);
                UpdateIsChangeTileSourceEnabled();
                UpdateIsTestCustomTileSourceEnabled();
            }).ConfigureAwait(false);
        }
        private void OnTileCacheClearerSaver_CacheCleared(object sender, TileCacheClearerSaver.CacheClearedEventArgs args)
        {
            // output messages
            if (args.TileSource.IsAll)
            {
                if (args.IsCacheCleared)
                {
                    _mainVM?.SetLastMessage_UI("Cache empty");
                }
                else
                {
                    _mainVM?.SetLastMessage_UI("Cache busy");
                }
            }
            else
            {
                if (args.IsCacheCleared)
                {
                    _mainVM?.SetLastMessage_UI(args.TileSource.DisplayName + " cache is empty");
                }
                else
                {
                    _mainVM?.SetLastMessage_UI(args.TileSource.DisplayName + " cache is busy");
                }
            }
        }

        private void OnTileCacheClearerSaver_CacheSaved(object sender, TileCacheClearerSaver.CacheSavedEventArgs args)
        {
            _mainVM?.SetLastMessage_UI($"{args.HowManyRecordsSaved} tiles saved");
        }
        #endregion event handlers

        #region services
        private async Task<MapsPanelVM> GetCurrentOpenInstanceAsync()
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
            Logger.Add_TPL($"MapsPanelVM has got an open instance", Logger.AppEventsLogFilename, Logger.Severity.Info, false);

            return Instance;
        }

        public void CancelSaveCache()
        {
            PersistentData.CancelSaveTileCache();
        }
        public async Task ScheduleSaveCacheAsync(TileSourceRecord tileSource)
        {
            if (!_isOpen || CancToken.IsCancellationRequested) return;

            var dir = await Pickers.PickDirectoryAsync(new string[] { "*" }).ConfigureAwait(false);
            if (dir == null) return;

            //Pickers.SetLastPickedFolder(dir, dir.Path);

            var instance = await GetCurrentOpenInstanceAsync().ConfigureAwait(false);
            if (instance == null) return;

            await instance.RunFunctionIfOpenAsyncT(async () =>
            {
                Logger.Add_TPL($"MapsPanelVM is about to schedule saving the tiles", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
                bool isScheduled = await Task.Run(() => instance._tileCacheClearerSaver.TryScheduleSaveCacheAsync(tileSource, dir)).ConfigureAwait(false);
                instance.PersistentData.LastMessage = isScheduled ? "started saving cache" : "nothing saved";
            }).ConfigureAwait(false);
        }
        public async Task ScheduleClearCacheAsync(TileSourceRecord tileSource, bool isAlsoRemoveSources)
        {
            bool isScheduled = await Task.Run(() => _tileCacheClearerSaver.TryScheduleClearCacheAsync(tileSource, isAlsoRemoveSources)).ConfigureAwait(false);
            _mainVM?.SetLastMessage_UI(isScheduled ? "cache will be cleared asap" : "cache unchanged");
        }
        public Task ShowDownloadZooms()
        {
            if (!_isLeechingEnabled)
            {
                _mainVM?.SetLastMessage_UI("Download busy");
                return Task.CompletedTask;
            }
            return RunFunctionIfOpenAsyncT(async () =>
            {
                // present a choice of zoom levels
                List<Tuple<int, int>> howManyTiles4DifferentZooms = await _lolloMapVM.GetHowManyTiles4DifferentZoomsAsync().ConfigureAwait(true);

                Collection<TextAndTag> tts = new Collection<TextAndTag>();
                foreach (var item in howManyTiles4DifferentZooms)
                {
                    if (item.Item2 <= ConstantData.MAX_TILES_TO_LEECH && item.Item1 > 0 && item.Item2 > 0)
                    {
                        string message = "Zoom  " + item.Item1 + " gets up to " + item.Item2 + " tiles each layer";
                        tts.Add(new TextAndTag(message, item.Item1));
                    }

                }
                if (tts.Any())
                {
                    IsShowZoomLevelChoices = true;
                    ZoomLevelChoices = tts;
                }
                else
                {
                    IsShowZoomLevelChoices = false;
                    _mainVM.SetLastMessage_UI("No downloads possible for this area or layers");
                }
            });
        }
        public Task ChooseDownloadZoomLevelAsync(int maxZoom)
        {
            return RunFunctionIfOpenAsyncT(async () =>
            {
                await PersistentData.SetTilesDownloadPropsAsync(true, maxZoom, false);
                // LolloMapVM will detect the property changes and take over from here
                PersistentData.IsShowingPivot = false;
            });
        }

        public Task AddMapSource(TileSourceRecord ts)
        {
            return RunFunctionIfOpenAsyncT(async () =>
            {
                if (ts == null) return;
                var result = await PersistentData.AddCurrentTileSourceAsync(ts, CancToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(result)) _mainVM?.SetLastMessage_UI(result);
            });
        }
        public Task RemoveMapSource(TileSourceRecord ts)
        {
            return RunFunctionIfOpenAsyncT(() =>
            {
                if (ts == null) return Task.CompletedTask;
                return PersistentData.RemoveCurrentTileSourceAsync(ts, CancToken);
            });
        }

        public Task StartUserTestingTileSourceAsync()
        {
            return RunFunctionIfOpenAsyncT(async delegate
            {
                Tuple<bool, string> result = await PersistentData.TryInsertTestTileSourceIntoTileSourcezAsync(CancToken);

                if (result?.Item1 == true)
                {
                    TestTileSourceErrorMsg = string.Empty; // ok
                    PersistentData.IsShowingPivot = false;
                }
                else TestTileSourceErrorMsg = result?.Item2; // error

                _mainVM?.SetLastMessage_UI(result?.Item2);
            });
        }

        public Task ToggleIsFileSourceAsync()
        {
            return RunFunctionIfOpenAsyncA(() =>
            {
                var tts = PersistentData?.TestTileSource;
                if (tts == null) return;
                tts.IsFileSource = !tts.IsFileSource;
            });
        }

        public Task ToggleIsOverlayAsync()
        {
            return RunFunctionIfOpenAsyncA(() =>
            {
                var tts = PersistentData?.TestTileSource;
                if (tts == null) return;
                tts.IsOverlay = !tts.IsOverlay;
            });
        }
        public async Task SetModelTileSourceAsync(TileSourceRecord ts)
        {
            await RunFunctionIfOpenAsyncT(async () =>
            {
                if (ts == null) return;

                var pd = PersistentData;
                if (pd == null) return;

                var result = await pd.TrySetModelTileSourceAsync(ts, CancToken).ConfigureAwait(false);
                if (result?.Item1 == true) TestTileSourceErrorMsg = string.Empty;
                else TestTileSourceErrorMsg = result?.Item2 ?? "";

                _mainVM?.SetLastMessage_UI(result?.Item2);
            });
        }
        public Task AddEmptyUriStringToTestTileSourceAsync()
        {
            return RunFunctionIfOpenAsyncT(async () =>
            {
                var pd = PersistentData;
                if (pd == null) return;

                var result = await pd.AddEmptyUriStringToTestTileSourceAsync(CancToken).ConfigureAwait(false);
                if (result?.Item1 == true) TestTileSourceErrorMsg = string.Empty;
                else TestTileSourceErrorMsg = result?.Item2 ?? "";

                _mainVM?.SetLastMessage_UI(result?.Item2);
            });
        }
        public Task RemoveUriStringFromTestTileSourceAsync(ObservableString uriString)
        {
            if (uriString == null) return Task.CompletedTask;
            return RunFunctionIfOpenAsyncT(async () =>
            {
                var pd = PersistentData;
                if (pd == null) return;

                var result = await pd.RemoveUriStringFromTestTileSourceAsync(uriString?.Str, CancToken).ConfigureAwait(false);
                if (result?.Item1 == true) TestTileSourceErrorMsg = string.Empty;
                else TestTileSourceErrorMsg = result?.Item2 ?? "";

                _mainVM?.SetLastMessage_UI(result?.Item2);
            });
        }

        public Task AddEmptyRequestHeaderToTestTileSourceAsync()
        {
            return RunFunctionIfOpenAsyncT(async () =>
            {
                var pd = PersistentData;
                if (pd == null) return;

                var result = await pd.AddRequestHeaderToTestTileSourceAsync(CancToken).ConfigureAwait(false);
                if (result?.Item1 == true) TestTileSourceErrorMsg = string.Empty;
                else TestTileSourceErrorMsg = result?.Item2 ?? "";

                _mainVM?.SetLastMessage_UI(result?.Item2);
            });
        }
        public Task RemoveRequestHeaderFromTestTileSourceAsync(ObservableKeyAndValue keyAndValue)
        {
            if (keyAndValue == null) return Task.CompletedTask;
            return RunFunctionIfOpenAsyncT(async () =>
            {
                var pd = PersistentData;
                if (pd == null) return;

                var result = await pd.RemoveRequestHeaderFromTestTileSourceAsync(keyAndValue?.Key, CancToken).ConfigureAwait(false);
                if (result?.Item1 == true) TestTileSourceErrorMsg = string.Empty;
                else TestTileSourceErrorMsg = result?.Item2 ?? "";

                _mainVM?.SetLastMessage_UI(result?.Item2);
            });
        }
        #endregion services
    }

    public class ObservableKeyAndValue : ObservableData
    {
        public static event PropertyChangedEventHandler SomethingChanged;
        protected void RaiseSomethingChanged_UI([CallerMemberName] string propertyName = "")
        {
            if (SomethingChanged == null) return;
            Task raise = RunInUiThreadAsync(delegate
            {
                SomethingChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            });
        }
        private string _key = string.Empty;
        public string Key { get { return _key; } set { _key = value; RaisePropertyChanged_UI(); RaiseSomethingChanged_UI(); } }
        private string _val = string.Empty;
        public string Val { get { return _val; } set { _val = value; RaisePropertyChanged_UI(); RaiseSomethingChanged_UI(); } }
        public ObservableKeyAndValue(string key, string val)
        {
            _key = key;
            _val = val;
        }
    }

    public class ObservableString : ObservableData
    {
        public static event PropertyChangedEventHandler SomethingChanged;
        protected void RaiseSomethingChanged_UI([CallerMemberName] string propertyName = "")
        {
            if (SomethingChanged == null) return;
            Task raise = RunInUiThreadAsync(delegate
            {
                SomethingChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            });
        }
        private string _str = string.Empty;
        public string Str { get { return _str; } set { _str = value; RaisePropertyChanged_UI(); RaiseSomethingChanged_UI(); } }

        public ObservableString(string str)
        {
            _str = str;
        }
    }
}
