using LolloGPS.Data;
using LolloGPS.Data.Runtime;
using LolloGPS.Data.TileCache;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Utilz;
using Windows.UI.Xaml.Controls.Maps;

namespace LolloGPS.Core
{
    public sealed class LolloMap_VM : ObservableData, IMapApController
    {
        // http://josm.openstreetmap.de/wiki/Maps

        public Main_VM MyMainVM { get { return Main_VM.GetInstance(); } }
        public PersistentData MyPersistentData { get { return App.PersistentData; } }
        public RuntimeData MyRuntimeData { get { return App.MyRuntimeData; } }

        private TileDownloader _tileDownloader = null;
        private TileCache _tileCache = null;
        private IGeoBoundingBoxProvider _gbbProvider = null;
        private IMapApController _mapController = null;
        private HttpMapTileDataSource _tileDataSource_http = null;
        private MapTileSource _mapTileSource = null;
        //private CustomMapTileDataSource _tileDataSource_custom = null;
        private IList<MapTileSource> _mapTileSources = null;

        #region construct and dispose
        public LolloMap_VM(IList<MapTileSource> mapTileSources, IGeoBoundingBoxProvider gbbProvider, IMapApController mapController)
        {
            MyMainVM.MyLolloMap_VM = this;
            _gbbProvider = gbbProvider;
            _mapController = mapController;
            _mapTileSources = mapTileSources;
            _tileDownloader = new TileDownloader(gbbProvider);
        }
        internal void Open()
        {
            _tileDownloader.Open();
            Task download = Task.Run(UpdateDownloadTilesAfterConditionsChangedAsync);
            if (!MyPersistentData.CurrentTileSource.IsDefault) OpenAlternativeMap_Http(MyPersistentData.CurrentTileSource, MyPersistentData.IsMapCached);
            AddHandler_DataChanged();
        }
        private void OpenAlternativeMap_Http(TileSourceRecord tileSource, bool isCaching)
        {
            _tileCache = new TileCache(tileSource, isCaching);

            _tileDataSource_http = new HttpMapTileDataSource()
            {
                // UriFormatString = _tileCache.GetWebUriFormat(), not required coz we catch the event OnDataSource_UriRequested
                AllowCaching = false, //true,                
            };

            _mapTileSource = new MapTileSource(
                _tileDataSource_http,
                new MapZoomLevelRange() { Max = _tileCache.GetMaxZoom(), Min = _tileCache.GetMinZoom() })
            {
                // Layer = MapTileLayer.BackgroundReplacement,
                Layer = MapTileLayer.BackgroundOverlay, // show the Nokia map when the alternative source is not available, otherwise it goes all blank (ie black)
                                                        // Layer = (MapTileLayer.BackgroundOverlay | MapTileLayer.RoadOverlay), // still does not hide the roads
                AllowOverstretch = true,
                //ZoomLevelRange = new MapZoomLevelRange() { Max = _tileCache.GetMaxZoom(), Min = _tileCache.GetMinZoom() },
                IsRetryEnabled = true,
                TilePixelSize = _tileCache.GetTilePixelSize(),
                IsFadingEnabled = false, //true,
                ZIndex = 999,
            };
            if (!_mapTileSources.Contains(_mapTileSource))
            {
                _mapTileSources.Add(_mapTileSource);
                _tileDataSource_http.UriRequested += OnDataSource_UriRequested;
            }

            Logger.Add_TPL("OpenAlternativeMap_Http ended", Logger.ForegroundLogFilename, Logger.Severity.Info, false);
            //_myMap.Opacity = .1; // show the Nokia map when the alternative source is not available
            //_myMap.Style = MapStyle.None; //so our map will cover the original completely
        }
        //private void ActivateAlternativeMap_Custom()
        //{
        //    _tileDataSource_custom = new CustomMapTileDataSource();

        //    _mapTileSource = new MapTileSource(_tileDataSource_custom)
        //    {
        //        // Layer = MapTileLayer.BackgroundReplacement,
        //        Layer = MapTileLayer.BackgroundOverlay, // show the Nokia map when the alternative source is not available, otherwise it goes all blank (ie black)
        //        AllowOverstretch = true,
        //        ZoomLevelRange = new MapZoomLevelRange() { Max = _tileCache.GetMaxZoom(), Min = _tileCache.GetMinZoom() },
        //        IsRetryEnabled = true,
        //        TilePixelSize = _tileCache.GetTilePixelSize(),
        //        IsFadingEnabled = true,
        //        ZIndex = 999,
        //    };

        //    if (!_mapTileSources.Contains(_mapTileSource))
        //    {
        //        _mapTileSources.Add(_mapTileSource);
        //        _tileDataSource_custom.BitmapRequested += OnCustomDataSource_BitmapRequested;
        //    }

        //    //_myMap.Opacity = .1; // show the Nokia map when the alternative source is not available
        //    //_myMap.Style = MapStyle.None; //so our map will cover the original completely
        //}
        internal void Close()
        {
            RemoveHandler_DataChanged();
            _tileDownloader.Close();
            CloseAlternativeMap_Http();
        }

        private void CloseAlternativeMap_Http()
        {
            if (_tileDataSource_http != null) _tileDataSource_http.UriRequested -= OnDataSource_UriRequested;
            //if (_tileDataSource_custom != null) _tileDataSource_custom.BitmapRequested -= OnCustomDataSource_BitmapRequested;
            if (_mapTileSources != null && _mapTileSource != null) _mapTileSources.Remove(_mapTileSource);
            Logger.Add_TPL("CloseAlternativeMap_Http ended", Logger.ForegroundLogFilename, Logger.Severity.Info, false);
        }
        #endregion construct and dispose
        /// <summary>
        /// Start or resume downloading tiles if so desired
        /// run this on the threadpool because it can last forever
        /// </summary>
        /// <returns></returns>
        private async Task UpdateDownloadTilesAfterConditionsChangedAsync()
        {
            if (MyPersistentData.IsTilesDownloadDesired && MyRuntimeData.IsConnectionAvailable)
            {
                Tuple<int, int> downloadResult = await _tileDownloader.StartOrResumeDownloadTilesAsync().ConfigureAwait(false);
                MyMainVM.SetLastMessage_UI(downloadResult.Item1 + " of " + downloadResult.Item2 + " tiles downloaded");
            }
        }

        #region event handling
        private bool _isDataChangedHandlerActive = false;
        private void AddHandler_DataChanged()
        {
            if (!_isDataChangedHandlerActive && MyPersistentData != null && MyRuntimeData != null)
            {
                MyPersistentData.PropertyChanged += OnPersistentData_PropertyChanged;
                MyRuntimeData.PropertyChanged += OnRuntimeData_PropertyChanged;
                _isDataChangedHandlerActive = true;
            }
        }
        private void RemoveHandler_DataChanged()
        {
            if (MyPersistentData != null && MyRuntimeData != null)
            {
                MyPersistentData.PropertyChanged -= OnPersistentData_PropertyChanged;
                MyRuntimeData.PropertyChanged -= OnRuntimeData_PropertyChanged;
                _isDataChangedHandlerActive = false;
            }
        }
        //private TileSourceRecord _lastTileSource = TileSourceRecord.GetDefaultTileSource();

        private void OnPersistentData_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PersistentData.IsTilesDownloadDesired))
            {
                Task download = Task.Run(UpdateDownloadTilesAfterConditionsChangedAsync);
            }
            else if (e.PropertyName == nameof(PersistentData.CurrentTileSource) || e.PropertyName == nameof(PersistentData.IsMapCached))
            {
				Task gt = RunInUiThreadAsync(delegate
				{
					CloseAlternativeMap_Http();
					if (!MyPersistentData.CurrentTileSource.IsDefault) OpenAlternativeMap_Http(MyPersistentData.CurrentTileSource, MyPersistentData.IsMapCached);
				});
            }
        }
        private void OnRuntimeData_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RuntimeData.IsConnectionAvailable))
            {
                if (MyRuntimeData.IsConnectionAvailable)
                {
                    Task resume = Task.Run(UpdateDownloadTilesAfterConditionsChangedAsync);
                }
            }
        }
        private async void OnDataSource_UriRequested(HttpMapTileDataSource sender, MapTileUriRequestedEventArgs args)
        {
            var deferral = args.Request.GetDeferral();
            try
            {
                var newUri = await _tileCache.GetTileUri(args.X, args.Y, 0, args.ZoomLevel).ConfigureAwait(false);
                if (newUri != null) args.Request.Uri = newUri;
            }
            catch (Exception ex)
            {
                Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename, Logger.Severity.Info);
            }
            deferral.Complete();
        }
        //private async void OnCustomDataSource_BitmapRequested(CustomMapTileDataSource sender, MapTileBitmapRequestedEventArgs args)
        //{
        //    var deferral = args.Request.GetDeferral();
        //    try
        //    {
        //        var pixelRef = await _tileCache.GetTileStreamRef(args.X, args.Y, 0, args.ZoomLevel).ConfigureAwait(false);
        //        if (pixelRef != null) args.Request.PixelData = pixelRef;
        //    }
        //    catch (Exception exc)
        //    {
        //        Debug.WriteLine("Exception in OnCustomDataSource_BitmapRequested(); " + exc.ToString() + exc.Message);
        //    }
        //    deferral.Complete();
        //}
        #endregion event handling

        public Task<List<Tuple<int, int>>> GetHowManyTiles4DifferentZoomsAsync()
        {
            return _tileDownloader.GetHowManyTiles4DifferentZooms4CurrentConditionsAsync();
        }
        public void CancelDownloadByUser()
        {
            _tileDownloader.CancelDownloadByUser();
        }
        public async Task<bool> AddMapCentreToLandmarks()
        {
            if (_gbbProvider != null && MyPersistentData != null)
            {
                var centre = await _gbbProvider.GetCentreAsync();
				// this stupid control does not know the altitude, it gives crazy high numbers
				centre.Altitude = Main_VM.RoundAndRangeAltitude(centre.Altitude, false);
                return await MyPersistentData.TryAddPointToLandmarksAsync(new PointRecord() { Altitude = centre.Altitude, Latitude = centre.Latitude, Longitude = centre.Longitude, }).ConfigureAwait(false);
            }
            return false;
        }

        public Task CentreOnHistoryAsync()
        {
            return _mapController?.CentreOnHistoryAsync();
        }
        public Task CentreOnLandmarksAsync()
        {
            return _mapController?.CentreOnLandmarksAsync();
        }
        public Task CentreOnRoute0Async()
        {
            return _mapController?.CentreOnRoute0Async();
        }
		public Task CentreOnSeriesAsync(PersistentData.Tables series)
		{
			if (series == PersistentData.Tables.History) return CentreOnHistoryAsync();
			else if (series == PersistentData.Tables.Route0) return CentreOnRoute0Async();
			else if (series == PersistentData.Tables.Landmarks) return CentreOnLandmarksAsync();
			else return Task.CompletedTask;
		}
		public Task CentreOnTargetAsync()
        {
            return _mapController?.CentreOnTargetAsync();
        }
		public Task CentreOnCurrentAsync()
		{
			return _mapController?.CentreOnCurrentAsync();
		}
		public Task Goto2DAsync()
        {
            return _mapController?.Goto2DAsync();
        }
    }
}