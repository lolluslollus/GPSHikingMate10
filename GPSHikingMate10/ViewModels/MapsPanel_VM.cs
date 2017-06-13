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
using static LolloGPS.Controlz.LolloListChooser;

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
        private List<TextAndTag> _selectedOverlayTiles = new List<TextAndTag>();
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

            UpdateIsClearCacheEnabled();
            UpdateIsClearCustomCacheEnabled();
            UpdateIsCacheBtnEnabled();
            UpdateIsLeechingEnabled();
            UpdateIsChangeTileSourceEnabled();
            UpdateIsTestCustomTileSourceEnabled();
            Task upd1 = UpdateIsChangeMapStyleEnabledAsync();
            Task upd2 = UpdateTileSourceChoicesAsync();
            Task upd3 = UpdateSelectedBaseTileAsync();
            Task upd4 = UpdateSelectedOverlayTilesAsync();
            await Task.WhenAll(upd1, upd2, upd3, upd4).ConfigureAwait(false);
        }

        protected override async Task CloseMayOverrideAsync(object args = null)
        {
            RemoveHandlers_DataChanged();
            await _tileCacheClearer.CloseAsync(args);
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
            var ts = await Task.Run(PersistentData.GetCurrentTileSourceCloneAsync).ConfigureAwait(false);
            await RunInUiThreadAsync(delegate
            {
                IsChangeMapStyleEnabled = !ts.IsDefault;
            }).ConfigureAwait(false);
        }
        internal async Task UpdateTileSourceChoicesAsync()
        {
            var tss = await Task.Run(() =>
            {
                return PersistentData.GetTileSourcezCloneAsync(true, true);
            }).ConfigureAwait(false);

            List<TextAndTag> baseTileSources = new List<TextAndTag>();
            List<TextAndTag> overlayTileSources = new List<TextAndTag>();

            await RunInUiThreadAsync(delegate
            {
                foreach (var ts in tss)
                {
                    if (ts.IsOverlay) overlayTileSources.Add(new TextAndTag(ts.DisplayName, ts));
                    else baseTileSources.Add(new TextAndTag(ts.DisplayName, ts));
                }
                _baseTileSourceChoices.ReplaceAll(baseTileSources);
                _overlayTileSourceChoices.ReplaceAll(overlayTileSources);
            }).ConfigureAwait(false);
        }
        internal async Task UpdateSelectedBaseTileAsync()
        {
            var selectedBaseTile = await Task.Run(PersistentData.GetCurrentTileSourceCloneAsync).ConfigureAwait(false);
            var selectedBaseTiles2 = (new TextAndTag[] { new TextAndTag(selectedBaseTile.DisplayName, selectedBaseTile) }).ToList();
            _selectedBaseTiles = selectedBaseTiles2;
        }
        internal async Task UpdateSelectedOverlayTilesAsync()
        {
            var selectedOverlayTiles = await Task.Run(PersistentData.GetCurrentOverlayTileSourcezCloneAsync).ConfigureAwait(false);
            var selectedOverlayTiles2 = new List<TextAndTag>();
            foreach (var item in selectedOverlayTiles)
            {
                selectedOverlayTiles2.Add(new TextAndTag(item.DisplayName, item));
            }
            _selectedOverlayTiles = selectedOverlayTiles2;
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
            if (e.PropertyName == nameof(PersistentData.IsTilesDownloadDesired))
            {
                Task gt = RunInUiThreadAsync(UpdateIsLeechingEnabled);
            }
            else if (e.PropertyName == nameof(PersistentData.CurrentTileSource))
            {
                Task gt = RunInUiThreadAsync(delegate
                {
                    UpdateIsLeechingEnabled();
                    UpdateIsCacheBtnEnabled();
                    Task upd1 = UpdateIsChangeMapStyleEnabledAsync();
                    Task upd2 = UpdateSelectedBaseTileAsync();
                });
            }
            else if (e.PropertyName == nameof(PersistentData.CurrentOverlayTileSources))
            {
                Task gt = RunInUiThreadAsync(delegate
                {
                    Task upd1 = UpdateSelectedOverlayTilesAsync();
                });
            }
            else if (e.PropertyName == nameof(PersistentData.TileSourcez))
            {
                Task upd1 = RunInUiThreadAsync(UpdateIsClearCustomCacheEnabled);
                Task upd2 = UpdateTileSourceChoicesAsync();
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
        public void SetBaseTileSourceSelection(SelectionRequestedEventArgs args)
        {
            if (args == null || args.Items == null) return;

            var indexes = new List<int>();
            if (_selectedBaseTiles.Count < 1)
            {
                args.Indexes = indexes;
                return;
            }

            for (var i = 0; i < args.Items.Count; i++)
            {
                if (_selectedBaseTiles[0].Tag.CompareTo(args.Items.ElementAt(i).Tag) == 0)
                {
                    indexes.Add(i);
                    break;
                }
            }

            args.Indexes = indexes;
        }
        public void SetOverlayTileSourcesSelection(SelectionRequestedEventArgs args)
        {
            if (args == null || args.Items == null) return;

            var indexes = new List<int>();
            if (_selectedOverlayTiles.Count < 1)
            {
                args.Indexes = indexes;
                return;
            }

            for (var i = 0; i < args.Items.Count; i++)
            {
                if (_selectedOverlayTiles.Any(sel => sel.Tag.CompareTo(args.Items.ElementAt(i).Tag) == 0))
                {
                    indexes.Add(i);
                }
            }

            args.Indexes = indexes;
        }
        public async Task ScheduleClearCacheAsync(TileSourceRecord tileSource, bool isAlsoRemoveSources)
        {
            bool isScheduled = await Task.Run(() => _tileCacheClearer.TryScheduleClearCacheAsync(tileSource, isAlsoRemoveSources)).ConfigureAwait(false);
            PersistentData.LastMessage = isScheduled ? "cache will be cleared asap" : "cache unchanged";
        }
        public Task DownloadMapAsync()
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
                        string message = "Zoom  " + item.Item1 + " gets up to " + item.Item2 + " tiles";
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
        public Task Download_ChooseZoomLevel(int maxZoom)
        {
            return RunFunctionIfOpenAsyncT(async () =>
            {
                await PersistentData.SetTilesDownloadPropsAsync(true, maxZoom, false);
                PersistentData.IsShowingPivot = false;
            });
        }
        public Task SetMapSource(TileSourceRecord ts)
        {
            return RunFunctionIfOpenAsyncT(() =>
            {
                if (ts == null) return Task.CompletedTask;
                return PersistentData.SetCurrentTileSourceAsync(ts, true);
            });
        }
        public Task UnsetMapSource(TileSourceRecord ts)
        {
            return RunFunctionIfOpenAsyncT(() =>
            {
                if (ts == null) return Task.CompletedTask;
                return PersistentData.UnsetCurrentTileSourceAsync(true);
            });
        }
        public Task AddOverlayMapSources(TileSourceRecord ts)
        {
            return RunFunctionIfOpenAsyncT(() =>
            {
                if (ts == null) return Task.CompletedTask;
                return PersistentData.SetCurrentTileSourceAsync(ts, false);
            });
        }
        public Task RemoveOverlayMapSources(TileSourceRecord ts)
        {
            return RunFunctionIfOpenAsyncT(() =>
            {
                if (ts == null) return Task.CompletedTask;
                return PersistentData.UnsetCurrentTileSourceAsync(ts, false);
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
