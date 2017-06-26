using LolloGPS.Core;
using LolloGPS.Data;
using LolloGPS.Data.Runtime;
using LolloGPS.Data.TileCache;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Utilz;
using Utilz.Data;
using Windows.UI.Xaml.Controls.Maps;

/*
 * LOLLO NOTE
  Something changed here. HttpMapTileDataSource used to do it for me: when UriRequested fired, 
  I could give it an uri pointing to the web or to a file in my local storage. 
  The latter stopped working, so I had to go the CustomMapTileDataSource route, 
  but the app is less fluid and there is extra code to convert bytes to bitmaps and then to stream references.
 */

namespace LolloGPS.ViewModels
{
    public sealed class LolloMapVM : OpenableObservableData
    {
        // http://josm.openstreetmap.de/wiki/Maps

        public PersistentData PersistentData { get { return App.PersistentData; } }
        public RuntimeData RuntimeData { get { return App.RuntimeData; } }

        private readonly IGeoBoundingBoxProvider _gbbProvider = null;
        private readonly TileDownloader _tileDownloader = null;
        private readonly IList<MapTileSource> _mapTileSources = null;
        private readonly List<TileCacheReaderWriter> _tileCaches = new List<TileCacheReaderWriter>();
        private readonly List<CustomMapTileDataSource> _customMapTileDataSources = new List<CustomMapTileDataSource>();
        //private HttpMapTileDataSource _httpMapTileDataSource = null;
        //private LocalMapTileDataSource _localMapTileDataSource = null;

        private bool _isShowCurrentBaseTileSource = false;
        public bool IsShowCurrentBaseTileSource { get { return _isShowCurrentBaseTileSource; } private set { _isShowCurrentBaseTileSource = value; RaisePropertyChanged_UI(); } }
        private TileSourceRecord _currentBaseTileSource = TileSourceRecord.GetDefaultTileSource();
        public TileSourceRecord CurrentBaseTileSource { get { return _currentBaseTileSource; } private set { _currentBaseTileSource = value; RaisePropertyChanged_UI(); } }

        #region construct and dispose
        public LolloMapVM(IList<MapTileSource> mapTileSources, IGeoBoundingBoxProvider gbbProvider)
        {
            _gbbProvider = gbbProvider;
            _mapTileSources = mapTileSources;
            _tileDownloader = new TileDownloader(gbbProvider);
        }
        protected override async Task OpenMayOverrideAsync(object args = null)
        {
            await _tileDownloader.OpenAsync(args);
            AddHandler_DataChanged();
            Task download = Task.Run(UpdateDownloadTilesAfterConditionsChangedAsync);
            await UpdateCurrentTileSourceAsync();

            _ctsManager.Open(CancToken);
            await OpenAlternativeMap_Custom_Async(CancToken).ConfigureAwait(false);
            //await OpenAlternativeMap_Http_Async().ConfigureAwait(false);
            //await OpenAlternativeMap_Local_Async().ConfigureAwait(false);  // LOLLO NOTE If you decide to use it, remember that the tileCacheReaderWriter must always cache!
        }
        /*
        private async Task OpenAlternativeMap_Local_Async()
        {
            var tileSource = await PersistentData.GetCurrentBaseTileSourceCloneAsync().ConfigureAwait(false);
            if (tileSource == null) return;

            if (tileSource.IsDefault)
            {
                await RunInUiThreadAsync(delegate
                {
                    CloseAlternativeMap_Local();
                    PersistentData.MapStyle = MapStyle.Terrain;
                }).ConfigureAwait(false);
                return;
            };

            var tileCache = _tileCache = new TileCacheReaderWriter(tileSource, PersistentData.IsMapCached, true); // both true and false fail, this thing never accepts links to my localcache.
            await RunInUiThreadAsync(delegate
            {
                CloseAlternativeMap_Local();
                _localMapTileDataSource = new LocalMapTileDataSource()
                {
                    // UriFormatString = tileCache.GetWebUriFormat(), not required coz we catch the event OnDataSource_UriRequested
                };

                var mapTileSource = new MapTileSource(
                    _localMapTileDataSource,
                    // The MapControl won't request the uri if the zoom is outside its bounds.
                    // To force it, I set the widest possible bounds, which is OK coz the map control does not limit the zoom to its tile source bounds anyway.
                    new MapZoomLevelRange() { Max = TileSourceRecord.MaxMaxZoom, Min = TileSourceRecord.MinMinZoom })
                {
                    AllowOverstretch = true,
                    IsRetryEnabled = true,
                    IsFadingEnabled = false,
                    IsTransparencyEnabled = false,
                    Layer = MapTileLayer.BackgroundOverlay, // we may have an overlay
                    TilePixelSize = tileCache.GetTilePixelSize(),
                    ZIndex = 999,
                    //ZoomLevelRange = new MapZoomLevelRange() { Max = tileCache.GetMaxZoom(), Min = tileCache.GetMinZoom() },
                };

                _mapTileSources.Add(mapTileSource);
                _localMapTileDataSource.UriRequested += OnLocalMapTileDataSource_UriRequested;
            }).ConfigureAwait(false);
        }
        private async Task OpenAlternativeMap_Http_Async()
        {
            var tileSource = await PersistentData.GetCurrentBaseTileSourceCloneAsync().ConfigureAwait(false);
            if (tileSource == null) return;

            if (tileSource.IsDefault)
            {
                await RunInUiThreadAsync(delegate
                {
                    CloseAlternativeMap_Http();
                    PersistentData.MapStyle = MapStyle.Terrain;
                }).ConfigureAwait(false);
                return;
            };

            var tileCache = _tileCache = new TileCacheReaderWriter(tileSource, PersistentData.IsMapCached, true); // both true and false fail
            await RunInUiThreadAsync(delegate
            {
                CloseAlternativeMap_Http();
                _httpMapTileDataSource = new HttpMapTileDataSource()
                {
                    // UriFormatString = tileCache.GetWebUriFormat(), not required coz we catch the event OnDataSource_UriRequested
                    AllowCaching = false, //true, // we do our own caching
                };

                var mapTileSource = new MapTileSource(
                    _httpMapTileDataSource,
                    // The MapControl won't request the uri if the zoom is outside its bounds.
                    // To force it, I set the widest possible bounds, which is OK coz the map control does not limit the zoom to its tile source bounds anyway.
                    new MapZoomLevelRange() { Max = TileSourceRecord.MaxMaxZoom, Min = TileSourceRecord.MinMinZoom })
                {
                    AllowOverstretch = true,
                    IsRetryEnabled = true,
                    IsFadingEnabled = false,
                    IsTransparencyEnabled = false,
                    Layer = MapTileLayer.BackgroundOverlay, // we may have an overlay
                    TilePixelSize = tileCache.GetTilePixelSize(),
                    ZIndex = 999,
                    //ZoomLevelRange = new MapZoomLevelRange() { Max = tileCache.GetMaxZoom(), Min = tileCache.GetMinZoom() },
                };

                _mapTileSources.Add(mapTileSource);
                _httpMapTileDataSource.UriRequested += OnHttpDataSource_UriRequested;
            }).ConfigureAwait(false);
        }
        */
        private async Task OpenAlternativeMap_Custom_Async(CancellationToken cancToken)
        {
            var tileSources = await PersistentData.GetCurrentTileSourcezCloneAsync(cancToken).ConfigureAwait(false);
            await RunInUiThreadAsync(delegate
            {
                if (cancToken.IsCancellationRequested) return;
                // default tile source ----------
                if (tileSources.Count == 1 && tileSources.ElementAt(0).IsDefault)
                {
                    CloseAlternativeMap_Custom();
                    PersistentData.MapStyle = MapStyle.Terrain;
                }
                // custom tile sources ----------
                else
                {
                    CloseAlternativeMap_Custom(); // unregister events and clear lists
                    foreach (var ts in tileSources)
                    {
                        if (ts.IsDefault) continue; // do nothing with the default source (ie Nokia)
                        var customMapTileDataSource = new CustomMapTileDataSource();
                        var tileCache = new TileCacheReaderWriter(ts, true, false, customMapTileDataSource);
                        var mapTileSource = new MapTileSource(
                            customMapTileDataSource,
                            // The MapControl won't request the uri if the zoom is outside its bounds.
                            // To force it, I set the widest possible bounds, which is OK coz the map control does not limit the zoom to its tile source bounds anyway.
                            new MapZoomLevelRange() { Max = TileSourceRecord.MaxMaxZoom, Min = TileSourceRecord.MinMinZoom })
                        {
                            AllowOverstretch = true,
                            IsRetryEnabled = true,
                            IsFadingEnabled = false,
                            IsTransparencyEnabled = false,
                            Layer = MapTileLayer.BackgroundOverlay, // we may have an overlay
                            TilePixelSize = tileCache.GetTilePixelSize(),
                            ZIndex = 999,
                            //ZoomLevelRange = new MapZoomLevelRange() { Max = tileCache.GetMaxZoom(), Min = tileCache.GetMinZoom() },
                        };

                        _tileCaches.Add(tileCache);
                        _mapTileSources.Add(mapTileSource);
                        _customMapTileDataSources.Add(customMapTileDataSource);
                        customMapTileDataSource.BitmapRequested += OnCustomDataSource_BitmapRequested;
                    }
                }
            }).ConfigureAwait(false);
        }
        protected override async Task CloseMayOverrideAsync(object args = null)
        {
            RemoveHandler_DataChanged();

            await RunInUiThreadAsync(CloseAlternativeMap_Custom).ConfigureAwait(false);
            //await RunInUiThreadAsync(CloseAlternativeMap_Http).ConfigureAwait(false);
            //await RunInUiThreadAsync(CloseAlternativeMap_Local).ConfigureAwait(false);

            _ctsManager.Close();

            var td = _tileDownloader;
            if (td != null) await td.CloseAsync(args).ConfigureAwait(false);
        }
        /*
        private void CloseAlternativeMap_Local()
        {
            var ds = _localMapTileDataSource;
            if (ds != null) ds.UriRequested -= OnLocalMapTileDataSource_UriRequested;
            _mapTileSources?.Clear();
        }
        private void CloseAlternativeMap_Http()
        {
            var ds = _httpMapTileDataSource;
            if (ds != null) ds.UriRequested -= OnHttpDataSource_UriRequested;
            _mapTileSources?.Clear();
        }
        */
        private void CloseAlternativeMap_Custom()
        {
            var dss = _customMapTileDataSources;
            if (dss != null)
            {
                foreach (var ds in dss)
                {
                    ds.BitmapRequested -= OnCustomDataSource_BitmapRequested;
                }
                dss.Clear();
            }
            _mapTileSources?.Clear();
            _tileCaches?.Clear();
        }
        #endregion construct and dispose

        #region event handling
        private bool _isDataChangedHandlerActive = false;
        private void AddHandler_DataChanged()
        {
            if (!_isDataChangedHandlerActive && PersistentData != null && RuntimeData != null)
            {
                PersistentData.PropertyChanged += OnPersistentData_PropertyChanged;
                RuntimeData.PropertyChanged += OnRuntimeData_PropertyChanged;
                _isDataChangedHandlerActive = true;
            }
        }
        private void RemoveHandler_DataChanged()
        {
            if (PersistentData != null && RuntimeData != null)
            {
                PersistentData.PropertyChanged -= OnPersistentData_PropertyChanged;
                RuntimeData.PropertyChanged -= OnRuntimeData_PropertyChanged;
                _isDataChangedHandlerActive = false;
            }
        }

        private void OnPersistentData_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PersistentData.IsTilesDownloadDesired))
            {
                Task down = RunFunctionIfOpenAsyncT_MT(UpdateDownloadTilesAfterConditionsChangedAsync);
            }
            else if (e.PropertyName == nameof(PersistentData.CurrentTileSources))
            {
                Task reopen = RunFunctionIfOpenAsyncT(async delegate
                {
                    // cancel current download
                    _ctsManager.Reset(CancToken);
                    await OpenAlternativeMap_Custom_Async(CancToken).ConfigureAwait(false);
                    //await OpenAlternativeMap_Http_Async().ConfigureAwait(false);
                    //await OpenAlternativeMap_Local_Async().ConfigureAwait(false);
                    await UpdateCurrentTileSourceAsync().ConfigureAwait(false);
                });
            }
            //else if (e.PropertyName == nameof(PersistentData.IsMapCached))
            //{
            //	Task updIsCaching = RunFunctionIfOpenAsyncA(delegate
            //	{
            //		var tc = _tileCache;
            //		if (tc != null) tc.IsCaching = PersistentData.IsMapCached;
            //	});
            //}
        }
        private void OnRuntimeData_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RuntimeData.IsConnectionAvailable))
            {
                Task resume = RunFunctionIfOpenAsyncT_MT(UpdateDownloadTilesAfterConditionsChangedAsync);
            }
        }
        /*
        private async void OnLocalMapTileDataSource_UriRequested(LocalMapTileDataSource sender, MapTileUriRequestedEventArgs args)
        { // this uri must use a local protocol such as ms-appdata
            var deferral = args.Request.GetDeferral();
            try
            {
                //args.Request.Uri = new Uri("ms-appx:///Assets/pointer_start-72.png");
                var tc = _tileCache;
                if (tc == null) return;
                var newUri = await tc.GetTileUriAsync(args.X, args.Y, 0, args.ZoomLevel, _ctsManager.LinkedCancToken).ConfigureAwait(false);
                if (newUri != null)
                {
                    args.Request.Uri = newUri;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                // Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename, Logger.Severity.Info);
            }
            finally
            {
                deferral.Complete();
            }
        }

        private async void OnHttpDataSource_UriRequested(HttpMapTileDataSource sender, MapTileUriRequestedEventArgs args)
        {
            var deferral = args.Request.GetDeferral();
            try
            {
                //args.Request.Uri = new Uri("ms-appx:///Assets/pointer_start-72.png");
                var tc = _tileCache;
                if (tc == null) return;
                var newUri = await tc.GetTileUriAsync(args.X, args.Y, 0, args.ZoomLevel, _ctsManager.LinkedCancToken).ConfigureAwait(false);
                if (newUri != null)
                {
                    args.Request.Uri = newUri;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                // Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename, Logger.Severity.Info);
            }
            finally
            {
                deferral.Complete();
            }
        }
        */
        private async void OnCustomDataSource_BitmapRequested(CustomMapTileDataSource sender, MapTileBitmapRequestedEventArgs args)
        {
            var deferral = args.Request.GetDeferral();
            try
            {
                var tileCache = _tileCaches.FirstOrDefault(tc => tc.MapTileDataSource == sender);
                if (tileCache == null) return;
                var pixelRef = await Task.Run(() => tileCache.GetTileStreamRefAsync(args.X, args.Y, 0, args.ZoomLevel, _ctsManager.LinkedCancToken), _ctsManager.LinkedCancToken).ConfigureAwait(false);
                if (pixelRef != null) args.Request.PixelData = pixelRef;
            }
            catch (Exception exc)
            {
                Debug.WriteLine("Exception in OnCustomDataSource_BitmapRequested(); " + exc.ToString() + exc.Message);
            }
            finally
            {
                deferral.Complete();
            }
        }
        #endregion event handling

        #region services
        private async Task UpdateCurrentTileSourceAsync()
        {
            var currentBaseTs = await PersistentData.GetCurrentBaseTileSourceCloneAsync(CancToken).ConfigureAwait(false);
            IsShowCurrentBaseTileSource = currentBaseTs?.IsDefault != true;
            CurrentBaseTileSource = currentBaseTs;
        }
        /// <summary>
        /// Start or resume downloading tiles if so desired
        /// run this on the threadpool because it can last forever
        /// </summary>
        /// <returns></returns>
        private async Task UpdateDownloadTilesAfterConditionsChangedAsync()
        {
            if (!PersistentData.IsTilesDownloadDesired || !RuntimeData.IsConnectionAvailable) return;

            List<Tuple<int, int>> downloadResult;
            try
            {
                await RunInUiThreadAsync(delegate
                {
                    if (CancToken.IsCancellationRequested) return;
                    KeepAlive.UpdateKeepAlive(true);
                }).ConfigureAwait(false);
                downloadResult = await _tileDownloader.StartOrResumeDownloadTilesAsync(CancToken).ConfigureAwait(false);
            }
            finally
            {
                await RunInUiThreadAsync(delegate
                {
                    KeepAlive.UpdateKeepAlive(PersistentData.IsKeepAlive);
                }).ConfigureAwait(false);
            }
            var pd = PersistentData;
            if (downloadResult != null && pd != null)
            {
                int n = 0; int m = 0;
                n = downloadResult.Sum(dr => dr.Item1);
                m = downloadResult.Sum(dr => dr.Item2);
                pd.LastMessage = $"{n} of {m} tiles downloaded";
            }
        }
        public async Task<List<Tuple<int, int>>> GetHowManyTiles4DifferentZoomsAsync()
        {
            var result = new List<Tuple<int, int>>();
            var td = _tileDownloader;
            if (td == null) return result;
            await RunFunctionIfOpenAsyncT(async delegate { result = await td.GetHowManyTiles4DifferentZooms4CurrentConditionsAsync(CancToken).ConfigureAwait(false); }).ConfigureAwait(false);
            return result;
        }

        public void CancelDownloadByUser()
        {
            _tileDownloader?.CancelDownloadByUser();
        }

        public async Task<bool> AddMapCentreToCheckpoints()
        {
            var gbbProvider = _gbbProvider;
            var pd = PersistentData;
            if (gbbProvider == null || pd == null) return false;

            var centre = await gbbProvider.GetCentreAsync();
            // this stupid control does not know the altitude, it gives crazy high numbers
            centre.Altitude = MainVM.RoundAndRangeAltitude(centre.Altitude, false);
            return await pd.TryAddPointToCheckpointsAsync(new PointRecord() { Altitude = centre.Altitude, Latitude = centre.Latitude, Longitude = centre.Longitude, Symbol = pd.Target?.Symbol ?? PersistentData.CheckpointSymbols.Circle }).ConfigureAwait(false);
        }
        #endregion services

        #region cancellation
        private readonly CtsManager _ctsManager = new CtsManager();
        internal class CtsManager
        {
            private readonly object _cancDownloadTilesLocker = new object();
            private SafeCancellationTokenSource _downloadTilesCts = null;
            private CancellationTokenSource _linkedCts = null;
            internal CancellationToken LinkedCancToken
            {
                get { lock (_cancDownloadTilesLocker) { return _linkedCts.Token; } }
            }
            internal void Open(CancellationToken cancToken)
            {
                lock (_cancDownloadTilesLocker)
                {
                    _downloadTilesCts?.Dispose();
                    _linkedCts?.Dispose();
                    _downloadTilesCts = new SafeCancellationTokenSource();
                    _linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_downloadTilesCts.Token, cancToken);
                }
            }
            internal void Reset(CancellationToken cancToken)
            {
                lock (_cancDownloadTilesLocker)
                {
                    _downloadTilesCts?.CancelSafe(true);
                    Open(cancToken);
                }
            }
            internal void Close()
            {
                lock (_cancDownloadTilesLocker)
                {
                    _downloadTilesCts?.CancelSafe(true);
                    _downloadTilesCts?.Dispose();
                    _linkedCts?.Dispose();
                    _downloadTilesCts = null;
                    _linkedCts = null;
                }
            }
        }
        #endregion cancellation
    }
}