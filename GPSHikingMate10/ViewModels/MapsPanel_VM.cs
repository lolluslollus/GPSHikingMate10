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

namespace GPSHikingMate10.ViewModels
{
    public class MapsPanelVM : OpenableObservableData
    {
        public PersistentData PersistentData { get { return App.PersistentData; } }
        public RuntimeData RuntimeData { get { return App.RuntimeData; } }

        private readonly LolloMapVM _lolloMapVM;
        private readonly MainVM _mainVM;
        private readonly TileCacheClearer _tileCacheClearer = null;

        // the following are always updated under _isOpenSemaphore
        private bool _isShowZoomLevelChoices = false;
        public bool IsShowZoomLevelChoices { get { return _isShowZoomLevelChoices; } private set { _isShowZoomLevelChoices = value; RaisePropertyChanged_UI(); } }
        private Collection<TextAndTag> _zoomLevelChoices;
        public Collection<TextAndTag> ZoomLevelChoices { get { return _zoomLevelChoices; } private set { _zoomLevelChoices = value; RaisePropertyChanged_UI(); } }

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
        public MapsPanelVM(LolloMapVM lolloMapVM, MainVM mainVM)
        {
            _lolloMapVM = lolloMapVM;
            _mainVM = mainVM;
            _tileCacheClearer = TileCacheClearer.GetInstance();
        }
        protected override async Task OpenMayOverrideAsync(object args = null)
        {
            await _tileCacheClearer.OpenAsync(args);

            AddHandlers_DataChanged();

            //Task allTileSourcesTask = Task.CompletedTask;
            //Task currentTileSourcesTask = Task.CompletedTask;

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
            }).ConfigureAwait(false);
        }

        protected override async Task CloseMayOverrideAsync(object args = null)
        {
            RemoveHandlers_DataChanged();
            await _tileCacheClearer.CloseAsync(args);
        }
        #endregion lifecycle

        #region updaters
        private void UpdateIsClearCustomCacheEnabled(ICollection<TileSourceRecord> allTileSources)
        {
            IsClearCustomCacheEnabled = allTileSources.Any(ts => ts.IsDeletable)
            && !_tileCacheClearer.IsClearingScheduled
            && !PersistentData.IsTileSourcezBusy;
        }
        private void UpdateIsClearCacheEnabled()
        {
            IsClearCacheEnabled = !_tileCacheClearer.IsClearingScheduled
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
            && !_tileCacheClearer.IsClearingScheduled
            && !PersistentData.IsTileSourcezBusy;
        }
        private void UpdateIsChangeTileSourceEnabled()
        {
            IsChangeTileSourceEnabled = !_tileCacheClearer.IsClearingScheduled
            && !PersistentData.IsTileSourcezBusy;
        }
        private void UpdateIsTestCustomTileSourceEnabled()
        {
            IsTestCustomTileSourceEnabled = !_tileCacheClearer.IsClearingScheduled
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

        private async void OnTileCache_IsClearingScheduledChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
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
        public async Task ScheduleClearCacheAsync(TileSourceRecord tileSource, bool isAlsoRemoveSources)
        {
            bool isScheduled = await Task.Run(() => _tileCacheClearer.TryScheduleClearCacheAsync(tileSource, isAlsoRemoveSources)).ConfigureAwait(false);
            PersistentData.LastMessage = isScheduled ? "cache will be cleared asap" : "cache unchanged";
        }
        public Task ShowDownloadZooms()
        {
            return RunFunctionIfOpenAsyncT(async () =>
            {
                if (!_isLeechingEnabled) _mainVM.SetLastMessage_UI("Download busy"); // this is redundant safety

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

                _mainVM?.SetLastMessage_UI(result?.Item2);
            });
        }
        #endregion services
    }
}
