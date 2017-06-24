using LolloGPS.Controlz;
using LolloGPS.Core;
using LolloGPS.Data;
using LolloGPS.Data.Runtime;
using LolloGPS.Data.TileCache;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utilz;
using Utilz.Data;
using static LolloGPS.Controlz.LolloMultipleListChooser;

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
        //private bool? _isFileSource = false;
        //public bool? IsFileSource { get { return _isFileSource; } private set { if (_isFileSource != value) { _isFileSource = value; RaisePropertyChanged_UI(); } } }

        public class TileSourceChoiceRecord
        {
            public string TechName { get; set; }
            public string DisplayName { get; set; }
            public bool IsSelected { get; set; }
        }

        private List<TextAndTag> _selectedBaseTiles = new List<TextAndTag>();
        public List<TextAndTag> SelectedBaseTiles { get { return _selectedBaseTiles; } private set { _selectedBaseTiles = value; RaisePropertyChanged_UI(); } }
        private List<TextAndTag> _selectedOverlayTiles = new List<TextAndTag>();
        public List<TextAndTag> SelectedOverlayTiles { get { return _selectedOverlayTiles; } private set { _selectedOverlayTiles = value; RaisePropertyChanged_UI(); } }
        private readonly SwitchableObservableCollection<TextAndTag> _baseTileSourceChoices = new SwitchableObservableCollection<TextAndTag>();
        public SwitchableObservableCollection<TextAndTag> BaseTileSourceChoices { get { return _baseTileSourceChoices; } }
        private readonly SwitchableObservableCollection<TextAndTag> _overlayTileSourceChoices = new SwitchableObservableCollection<TextAndTag>();
        public SwitchableObservableCollection<TextAndTag> OverlayTileSourceChoices { get { return _overlayTileSourceChoices; } }

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
                allTileSources = await PersistentData.GetAllTileSourcezCloneAsync();
                currentTileSources = await PersistentData.GetCurrentTileSourcezCloneAsync();
            });

            await RunInUiThreadAsync(delegate
            {
                UpdateIsClearCacheEnabled();
                UpdateIsClearCustomCacheEnabled(allTileSources);
                UpdateIsCacheBtnEnabled(currentTileSources);
                UpdateIsLeechingEnabled(currentTileSources);
                UpdateIsChangeTileSourceEnabled();
                UpdateIsTestCustomTileSourceEnabled();
                UpdateIsChangeMapStyleEnabled(currentTileSources);
                UpdateTileSourceChoices(allTileSources);
                UpdateSelectedBaseTile(currentTileSources);
                UpdateSelectedOverlayTiles(currentTileSources);
                //UpdateIsFileSource();
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
            IsClearCustomCacheEnabled = allTileSources.Any(ts => ts.IsDeletable)
            && !_tileCacheClearerSaver.IsClearingScheduled
            && !PersistentData.IsTileSourcezBusy;
        }
        private void UpdateIsClearCacheEnabled()
        {
            IsClearOrSaveCacheEnabled = !_tileCacheClearerSaver.IsClearingScheduled
            && !PersistentData.IsTileSourcezBusy;
        }
        private void UpdateIsCacheBtnEnabled(ICollection<TileSourceRecord> currentTileSources)
        {
            IsCacheBtnEnabled = currentTileSources.Any(ts => !ts.IsDefault);
        }
        private void UpdateIsLeechingEnabled(ICollection<TileSourceRecord> currentTileSources)
        {
            IsLeechingEnabled = !PersistentData.IsTilesDownloadDesired
            && currentTileSources.Any(ts => !ts.IsDefault)
            && RuntimeData.IsConnectionAvailable
            && !_tileCacheClearerSaver.IsClearingScheduled
            && !PersistentData.IsTileSourcezBusy;
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
            if (currentTileSources.Count == 1) IsChangeMapStyleEnabled = !currentTileSources.ElementAt(0).IsDefault;
            else IsChangeMapStyleEnabled = true;
        }
        private void UpdateTileSourceChoices(ICollection<TileSourceRecord> allTileSources)
        {
            List<TextAndTag> baseTileSources = new List<TextAndTag>();
            List<TextAndTag> overlayTileSources = new List<TextAndTag>();

            foreach (var ts in allTileSources)
            {
                if (ts.IsOverlay) overlayTileSources.Add(new TextAndTag(ts.DisplayName, ts));
                else baseTileSources.Add(new TextAndTag(ts.DisplayName, ts));
            }
            _baseTileSourceChoices.ReplaceAll(baseTileSources);
            _overlayTileSourceChoices.ReplaceAll(overlayTileSources);
        }
        private void UpdateSelectedBaseTile(ICollection<TileSourceRecord> currentTileSources)
        {
            var currentBaseTileSource = currentTileSources.FirstOrDefault(ts => !ts.IsOverlay);
            if (currentBaseTileSource == null) return;
            var selectedBaseTiles = (new TextAndTag[] { new TextAndTag(currentBaseTileSource.DisplayName, currentBaseTileSource) }).ToList();
            SelectedBaseTiles = selectedBaseTiles;
        }
        private void UpdateSelectedOverlayTiles(ICollection<TileSourceRecord> currentTileSources)
        {
            var selectedOverlayTiles = new List<TextAndTag>();
            foreach (var item in currentTileSources)
            {
                if (!item.IsOverlay) continue;
                selectedOverlayTiles.Add(new TextAndTag(item.DisplayName, item));
            }
            SelectedOverlayTiles = selectedOverlayTiles;
        }
        //private void UpdateIsFileSource()
        //{
        //    IsFileSource = PersistentData?.TestTileSource?.IsFileSource;
        //}
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
                TileCacheClearerSaver.CacheCleared += OnTileCache_CacheCleared;
                TileCacheClearerSaver.IsSavingScheduledChanged += OnTileCache_IsClearingOrSavingScheduledChanged;
                TileCacheClearerSaver.CacheSaved += OnTileCacheClearerSaver_CacheSaved;
            }
        }

        private void RemoveHandlers_DataChanged()
        {
            if (PersistentData != null)
            {
                PersistentData.PropertyChanged -= OnPersistentData_PropertyChanged;
                RuntimeData.PropertyChanged -= OnRuntimeData_PropertyChanged;
                TileCacheClearerSaver.IsClearingScheduledChanged -= OnTileCache_IsClearingOrSavingScheduledChanged;
                TileCacheClearerSaver.CacheCleared -= OnTileCache_CacheCleared;
                TileCacheClearerSaver.IsSavingScheduledChanged -= OnTileCache_IsClearingOrSavingScheduledChanged;
                TileCacheClearerSaver.CacheSaved -= OnTileCacheClearerSaver_CacheSaved;
                _isDataChangedHandlerActive = false;
            }
        }
        private async void OnPersistentData_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PersistentData.IsTilesDownloadDesired))
            {
                var currentTileSources = await Task.Run(() => PersistentData.GetCurrentTileSourcezCloneAsync());
                await RunInUiThreadAsync(() =>
                {
                    UpdateIsLeechingEnabled(currentTileSources);
                }).ConfigureAwait(false);
            }
            else if (e.PropertyName == nameof(PersistentData.CurrentTileSources))
            {
                var currentTileSources = await Task.Run(() => PersistentData.GetCurrentTileSourcezCloneAsync());
                await RunInUiThreadAsync(delegate
                {
                    UpdateIsLeechingEnabled(currentTileSources);
                    UpdateIsCacheBtnEnabled(currentTileSources);
                    UpdateIsChangeMapStyleEnabled(currentTileSources);
                    UpdateSelectedBaseTile(currentTileSources);
                    UpdateSelectedOverlayTiles(currentTileSources);
                }).ConfigureAwait(false);
            }
            else if (e.PropertyName == nameof(PersistentData.TileSourcez))
            {
                var allTileSources = await Task.Run(() => PersistentData.GetAllTileSourcezCloneAsync());
                await RunInUiThreadAsync(delegate
                {
                    UpdateIsClearCustomCacheEnabled(allTileSources);
                    UpdateTileSourceChoices(allTileSources);
                }).ConfigureAwait(false);
            }
            else if (e.PropertyName == nameof(PersistentData.IsTileSourcezBusy))
            {
                var allTileSources = await Task.Run(() => PersistentData.GetAllTileSourcezCloneAsync());
                var currentTileSources = await Task.Run(() => PersistentData.GetCurrentTileSourcezCloneAsync());
                await RunInUiThreadAsync(delegate
                {
                    UpdateIsClearCacheEnabled();
                    UpdateIsClearCustomCacheEnabled(allTileSources);
                    UpdateIsLeechingEnabled(currentTileSources);
                    UpdateIsChangeTileSourceEnabled();
                    UpdateIsTestCustomTileSourceEnabled();
                }).ConfigureAwait(false);
            }
            //else if (e.PropertyName == nameof(PersistentData.TestTileSource))
            //{
            //    UpdateIsFileSource();
            //}
        }

        private async void OnRuntimeData_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RuntimeData.IsConnectionAvailable))
            {
                var currentTileSources = await Task.Run(() => PersistentData.GetCurrentTileSourcezCloneAsync());
                await RunInUiThreadAsync(delegate
                {
                    UpdateIsLeechingEnabled(currentTileSources);
                    UpdateIsTestCustomTileSourceEnabled();
                }).ConfigureAwait(false);
            }
        }

        private async void OnTileCache_IsClearingOrSavingScheduledChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            var allTileSources = await Task.Run(() => PersistentData.GetAllTileSourcezCloneAsync());
            var currentTileSources = await Task.Run(() => PersistentData.GetCurrentTileSourcezCloneAsync());
            await RunInUiThreadAsync(delegate
            {
                UpdateIsClearCacheEnabled();
                UpdateIsClearCustomCacheEnabled(allTileSources);
                UpdateIsLeechingEnabled(currentTileSources);
                UpdateIsChangeTileSourceEnabled();
                UpdateIsTestCustomTileSourceEnabled();
            }).ConfigureAwait(false);
        }
        private void OnTileCache_CacheCleared(object sender, TileCacheClearerSaver.CacheClearedEventArgs args)
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

        private void OnTileCacheClearerSaver_CacheSaved(object sender, TileCacheClearerSaver.CacheSavedEventArgs args)
        {
            if (args.IsCacheSaved) PersistentData.LastMessage = $"{args.HowManyRecordsSaved} tiles saved";
            else PersistentData.LastMessage = "Tiles not saved";
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
                bool isScheduled = await Task.Run(() => instance._tileCacheClearerSaver.TryScheduleSaveCacheAsync(tileSource, dir)).ConfigureAwait(false);
                instance.PersistentData.LastMessage = isScheduled ? "cache will be saved asap" : "nothing saved";
            }).ConfigureAwait(false);
        }
        public async Task ScheduleClearCacheAsync(TileSourceRecord tileSource, bool isAlsoRemoveSources)
        {
            bool isScheduled = await Task.Run(() => _tileCacheClearerSaver.TryScheduleClearCacheAsync(tileSource, isAlsoRemoveSources)).ConfigureAwait(false);
            PersistentData.LastMessage = isScheduled ? "cache will be cleared asap" : "cache unchanged";
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
                    _mainVM.SetLastMessage_UI("No downloads possible for this area");
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
                var result = await PersistentData.AddCurrentTileSourceAsync(ts).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(result)) PersistentData.LastMessage = result;
            });
        }
        public Task RemoveMapSource(TileSourceRecord ts)
        {
            return RunFunctionIfOpenAsyncT(() =>
            {
                if (ts == null) return Task.CompletedTask;
                return PersistentData.RemoveCurrentTileSourceAsync(ts);
            });
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

                //UpdateIsFileSource();
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
        #endregion services
    }
}
