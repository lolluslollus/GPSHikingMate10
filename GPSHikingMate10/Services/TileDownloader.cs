using LolloGPS.Data;
using LolloGPS.Data.Leeching;
using LolloGPS.Data.Runtime;
using LolloGPS.Data.TileCache;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Utilz;
using Windows.ApplicationModel.Core;
using Windows.Devices.Geolocation;
using Windows.UI.Xaml.Controls.Maps;

namespace LolloGPS.Core
{
    public interface IGeoBoundingBoxProvider
    {
        Task<GeoboundingBox> GetMinMaxLatLonAsync();
        BasicGeoposition GetCentre();
    }
    public sealed class TileDownloader
    {
        #region properties
        public const int MaxTilesToLeech = 10000;
        public const int MaxProgressStepsToReport = 25;

        private volatile bool _isCancelledBySuspend = false;
        public bool IsCancelledBySuspend { get { return _isCancelledBySuspend; } private set { _isCancelledBySuspend = value; } }

        private volatile bool _isCancelledByUser = false;
        public bool IsCancelledByUser { get { return _isCancelledByUser; } private set { _isCancelledByUser = value; } }

        public bool IsCancelled { get { return _isCancelledBySuspend || _isCancelledByUser || !(RuntimeData.GetInstance().IsConnectionAvailable); } }

        private IGeoBoundingBoxProvider _gbbProvider = null;
        #endregion properties

        #region events
        public event EventHandler<double> SaveProgressChanged;
        private void RaiseSaveProgressChanged(double progress)
        {
            Data.Runtime.RuntimeData.SetDownloadProgressValue_UI(progress);
            var listener = SaveProgressChanged;
            if (listener != null)
            {
                listener(this, progress);
            }
        }
        #endregion events

        #region construct and dispose
        internal TileDownloader(IGeoBoundingBoxProvider gbbProvider)
        {
            _gbbProvider = gbbProvider;
        }
        internal void Activate()
        {
            IsCancelledBySuspend = false;
            IsCancelledByUser = false;
        }
        internal void Deactivate()
        {
            IsCancelledBySuspend = true;
        }
        #endregion construct and dispose

        #region save services
        private static SemaphoreSlimSafeRelease _saveSemaphore = new SemaphoreSlimSafeRelease(1, 1);
        internal void CancelDownloadByUser()
        {
            IsCancelledByUser = true;
        }
        public async Task<Tuple<int, int>> StartOrResumeDownloadTilesAsync()
        {
            var output = Tuple.Create<int, int>(0, 0);
            PersistentData persistentData = PersistentData.GetInstance();
            var currentTileSource_mt = persistentData.CurrentTileSource; // read the value as soon as the present method is called
            var maxDesiredZoomForDownloadingTiles_mt = persistentData.MaxDesiredZoomForDownloadingTiles; // read the value as soon as the present method is called
            //var isMapCached_mt = persistentData.IsMapCached; // useless here
            try
            {
                await _saveSemaphore.WaitAsync().ConfigureAwait(false);

                var lastDownloadSession_mt = persistentData.LastDownloadSession; // read the latest available value
                var tileSourcez_mt = persistentData.TileSourcez; // read the latest available value

                IsCancelledByUser = false;

                TileCache tileCache = null;
                // last download completed: start a new one with the current tile source
                if (lastDownloadSession_mt == null)
                {
                    var gbb = await _gbbProvider.GetMinMaxLatLonAsync().ConfigureAwait(false);
                    if (gbb != null)
                    {
                        tileCache = new TileCache(currentTileSource_mt, false);

                        DownloadSession newSession = new DownloadSession(
                            tileCache.GetMinZoom(),
                            Math.Min(tileCache.GetMaxZoom(), maxDesiredZoomForDownloadingTiles_mt),
                            gbb.NorthwestCorner,
                            gbb.SoutheastCorner,
                            tileCache.TileSource.TechName);
                        // never write an invalid DownloadSession into the persistent data
                        if (newSession.IsValid) persistentData.LastDownloadSession = lastDownloadSession_mt = newSession;
                    }
                }
                // last download did not complete: start a new one with the old tile source
                else
                {
                    TileSourceRecord prevSessionTileSource = tileSourcez_mt.FirstOrDefault(a => a.TechName == lastDownloadSession_mt.TileSourceTechName);
                    if (prevSessionTileSource != null) tileCache = new TileCache(prevSessionTileSource, false);
                    // of course, we don't touch the unfinished download session
                }

                if (tileCache != null && lastDownloadSession_mt != null && lastDownloadSession_mt.IsValid)
                {
                    output = await DownloadTiles_RespondingToCancel_Async(tileCache, lastDownloadSession_mt).ConfigureAwait(false);
                }
                // even if something went wrong (maybe the new session is not valid), do not leave the download open!
                CloseDownload(persistentData, true);
            }
            catch (Exception ex)
            {
                CloseDownload(persistentData, false);
                Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
            }
            finally
            {
                SemaphoreSlimSafeRelease.TryRelease(_saveSemaphore);
            }
            return output;
        }
        private async Task<Tuple<int, int>> DownloadTiles_RespondingToCancel_Async(TileCache tileCache, DownloadSession session)
        {
            var output = Tuple.Create<int, int>(0, 0);
            if (RuntimeData.GetInstance().IsConnectionAvailable)
            {
                output = await SaveTiles_RespondingToCancel_Async(tileCache, session).ConfigureAwait(false);
            }
            return output;
        }
        private void CloseDownload(PersistentData persistentData, bool isSuccess)
        {
            // maybe the user cancelled: that means they are happy with this download, or at least we can consider it complete.
            if (IsCancelledByUser)
            {
                persistentData.IsTilesDownloadDesired = false;
                persistentData.LastDownloadSession = null;
            }
            // unless it was interrupted by suspension or connection going missing, the download is no more required because it finished: mark it.
            else if (!IsCancelledBySuspend && RuntimeData.GetInstance().IsConnectionAvailable)
            {
                if (isSuccess) persistentData.IsTilesDownloadDesired = false;
                persistentData.LastDownloadSession = null;
            }
        }
        private async Task<Tuple<int, int>> SaveTiles_RespondingToCancel_Async(TileCache tileCache, DownloadSession session)
        {
            RaiseSaveProgressChanged(0.0);

            int totalCnt = 0;
            int currentOkCnt = 0;
            int currentCnt = 0;

            if (!IsCancelled)
            {
                List<TileCacheRecord> requiredTilesOrderedByZoom = GetTileData_RespondingToCancel(session);
                totalCnt = requiredTilesOrderedByZoom.Count;

                if (!IsCancelled && totalCnt > 0)
                {
                    int howManyProgressStepsIWantToReport = Math.Min(MaxProgressStepsToReport, totalCnt);

                    int[] stepsWhenIWantToRaiseProgress = new int[howManyProgressStepsIWantToReport];
                    if (howManyProgressStepsIWantToReport > 0)
                    {
                        for (int i = 0; i < howManyProgressStepsIWantToReport; i++)
                        {
                            stepsWhenIWantToRaiseProgress[i] = totalCnt * i / howManyProgressStepsIWantToReport;
                        }
                    }
                    //TODO check this parallelisation. Or maybe not, it could annoy ceertain providers.
                    //CancellationTokenSource cts = new CancellationTokenSource();
                    //var token = cts.Token;
                    //try
                    //{
                    //    Parallel.ForEach(requiredTilesOrderedByZoom, new ParallelOptions() { CancellationToken = token }, async (tile) =>
                    //    {
                    //        bool isOk = await _tileCache.SaveTileAsync(tile.X, tile.Y, tile.Z, tile.Zoom).ConfigureAwait(false);
                    //        if (isOk) currentOkCnt++;

                    //        currentCnt++;
                    //        if (stepsWhenIWantToRaiseProgress.Contains(currentCnt)) RaiseSaveProgressChanged((double)currentCnt / (double)totalCnt);

                    //        if (IsCancelled) cts.Cancel(true);
                    //    });
                    //}
                    //catch (OperationCanceledException) { }
                    //catch (ObjectDisposedException) { } 
                    //finally
                    //{
                    //    if (cts != null)
                    //    {
                    //        cts.Dispose();
                    //        cts = null;
                    //    }
                    //}

                    foreach (var tile in requiredTilesOrderedByZoom)
                    {
                        bool isOk = await tileCache.SaveTileAsync(tile.X, tile.Y, tile.Z, tile.Zoom).ConfigureAwait(false);
                        if (isOk) currentOkCnt++;

                        currentCnt++;
                        if (totalCnt > 0)
                        {
                            if (stepsWhenIWantToRaiseProgress.Contains(currentCnt)) RaiseSaveProgressChanged((double)currentCnt / (double)totalCnt);
                        }
                        if (IsCancelled) break;
                    }
                }
            }
            RaiseSaveProgressChanged(1.0);
            return Tuple.Create<int, int>(currentOkCnt, totalCnt);
        }
        #endregion save services

        #region read services
        public async Task<List<Tuple<int, int>>> GetHowManyTiles4DifferentZooms4CurrentConditionsAsync()
        {
            PersistentData persistentData = PersistentData.GetInstance();
            var currentTileSource_mt = persistentData.CurrentTileSource;
            var isMapCached_mt = persistentData.IsMapCached;

            IsCancelledByUser = false;
            List<Tuple<int, int>> output = new List<Tuple<int, int>>();

            try
            {
                TileCache tileCache = new TileCache(currentTileSource_mt, isMapCached_mt);

                var gbb = await _gbbProvider.GetMinMaxLatLonAsync().ConfigureAwait(false);
                if (gbb != null)
                {
                    // now I have a geobounding box that certainly encloses the screen.
                    DownloadSession session = new DownloadSession(
                        tileCache.GetMinZoom(),
                        tileCache.GetMaxZoom(),
                        gbb.NorthwestCorner,
                        gbb.SoutheastCorner,
                        tileCache.TileSource.TechName);
                    if (session.IsValid)
                    {
                        List<TileCacheRecord> tilesOrderedByZoom = GetTileData_RespondingToCancel(session);

                        for (int zoom = session.MinZoom; zoom <= session.MaxZoom; zoom++)
                        {
                            int howManyAtOrBeforeZoom = tilesOrderedByZoom.Count(a => a.Zoom <= zoom);
                            output.Add(Tuple.Create<int, int>(zoom, howManyAtOrBeforeZoom));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
            }
            return output;
        }

        private List<TileCacheRecord> GetTileData_RespondingToCancel(DownloadSession session)
        {
            List<TileCacheRecord> output = new List<TileCacheRecord>();

            if (!IsCancelled)
            {
                if (!IsCancelled && session != null &&
                    (session.NWCorner.Latitude != session.SECorner.Latitude || session.NWCorner.Longitude != session.SECorner.Longitude))
                {
                    int totalCnt = 0;
                    for (int zoom = session.MinZoom; zoom <= session.MaxZoom; zoom++)
                    {
                        TileCacheRecord topLeftTile = new TileCacheRecord(session.TileSourceTechName, Lon2TileX(session.NWCorner.Longitude, zoom), Lat2TileY(session.NWCorner.Latitude, zoom), 0, zoom); // Alaska
                        TileCacheRecord bottomRightTile = new TileCacheRecord(session.TileSourceTechName, Lon2TileX(session.SECorner.Longitude, zoom), Lat2TileY(session.SECorner.Latitude, zoom), 0, zoom); // New Zealand
                        Debug.WriteLine("topLeftTile.X = " + topLeftTile.X + " topLeftTile.Y = " + topLeftTile.Y + " bottomRightTile.X = " + bottomRightTile.X + " bottomRightTile.Y = " + bottomRightTile.Y + " and zoom = " + zoom);
                        for (int x = topLeftTile.X; x <= bottomRightTile.X; x++)
                        {
                            for (int y = topLeftTile.Y; y <= bottomRightTile.Y; y++)
                            {
                                output.Add(new TileCacheRecord(session.TileSourceTechName, x, y, 0, zoom));
                                totalCnt++;
                                if (totalCnt > MaxTilesToLeech) break;
                            }
                            if (totalCnt > MaxTilesToLeech || IsCancelled) break;
                        }
                        if (totalCnt > MaxTilesToLeech || IsCancelled) break;
                    }
                }
            }
            return output;
        }
        #endregion read services

        #region private services
        private static double toRad = Math.PI / 180.0;
        private static double toDeg = 180.0 / Math.PI;
        private static int Lon2TileX(double lonDeg, int z)
        {
            //                   N * (lon + 180) / 360
            return (int)(Math.Floor((lonDeg + 180.0) / 360.0 * Math.Pow(2.0, z)));
        }
        private static int Lat2TileY(double latDeg, int z)
        {
            //                   N *  { 1 - log[ tan ( lat ) + sec ( lat ) ] / Pi } / 2
            //      sec(x) = 1 / cos(x)
            //return (int)(Math.Floor((1.0 - Math.Log(Math.Tan(latDeg * Math.PI / 180.0) + 1.0 / Math.Cos(latDeg * Math.PI / 180.0)) / Math.PI) / 2.0 * Math.Pow(2.0, z)));
            return (int)(Math.Floor((1.0 - Math.Log(Math.Tan(latDeg * toRad) + 1.0 / Math.Cos(latDeg * toRad)) / Math.PI) / 2.0 * Math.Pow(2.0, z)));
        }
        private static int Zoom2TileN(int z)
        {
            return (int)Math.Pow(2.0, Convert.ToDouble(z));
        }
        private static double TileX2Lon(int x, int z)
        {
            return x / Math.Pow(2.0, z) * 360.0 - 180;
        }
        private static double TileY2Lat(int y, int z)
        {
            double n = Math.PI - 2.0 * Math.PI * y / Math.Pow(2.0, z);
            //return 180.0 / Math.PI * Math.Atan(0.5 * (Math.Exp(n) - Math.Exp(-n)));
            return toDeg * Math.Atan(Math.Sinh(n));
        }
        #endregion private services
    }

    /*
     * Reproject the coordinates to the Mercator projection (from EPSG:4326 to EPSG:3857): 
        x = lon
        y = arsinh(tan(lat)) = log[tan(lat) + sec(lat)]
        (lat and lon are in radians)
     * Transform range of x and y to 0 – 1 and shift origin to top left corner: 
        x = [1 + (x / π)] / 2
        y = [1 − (y / π)] / 2

     * Calculate the number of tiles across the map, n, using 2zoom
     * Multiply x and y by n. Round results down to give tilex and tiley.
*/
    // some more notes

    // http://msdn.microsoft.com/en-us/library/windowsphone/develop/windows.ui.xaml.controls.maps.aspx
    // http://pietschsoft.com/post/2009/11/13/Prototype_OpenStreetMap_Silverlight_Control_using_Bing_Maps_SDK_and_DeepEarth

    // openstreetmap http://tile.openstreetmap.org/{2}/{0}/{1}.png that is, zoom, x, y, that is, zoom, long, lat
    // or http://a.tile.openstreetmap.org/{zoomlevel}/{x}/{y}.png
    // with zoom = 2,
    // x and y go between 0 and 3. y = 3 is antarctica, y = 0 is the arctic. No decimals are allowed.z goes between 2 and 19.
    // with zoom = 1, x and y go between 0 and 1
    // with zoom 3, x and y go between 0 and 7
    // basically, the max value is Pow(2,zoom) -1, for both x and y
    // the zero x is at meridian 180°
    // the zero y is at 85.0511 N (they use a Mercator projection) 85.0511 is the result of arctan(sinh(π)). 
    // the min zoom is 0 (zoomed out)
    // the max zoom is 19 (zoomed in)
    // MapControl goes between 1 and 20, instead
    // similar is openaerialmap http://tile.openaerialmap.org/tiles/1.0.0/openaerialmap-900913/{2}/{0}/{1}.jpg
    // otherwise, we can use http://nominatim.openstreetmap.org/search.php?q=8%2C+44%2C+9%2C+49&viewbox=-168.76%2C66.24%2C168.76%2C-66.24&polygon=1 not really, that one looks for names
    // http://wiki.openstreetmap.org/wiki/Nominatim
    //
    // http://pietschsoft.com/post/2009/03/20/Virtual-Earth-Silverlight-Overlay-OpenStreetMap2c-OpenAerialMap-and-Yahoo-Map-Imagery-using-Custom-Tile-Layers!
    //yahoo http://us.maps2.yimg.com/us.png.maps.yimg.com/png?v=3.52&t=m&x={0}&y={1}&z={2}
    //         public override Uri GetUri(int x, int y, int zoomLevel) 
    //{
    //    // The math used here was copied from the DeepEarth Project (http://deepearth.codeplex.com) 
    //    double posY;
    //    double zoom = 18 - zoomLevel;
    //    double num4 = Math.Pow(2.0, zoomLevel) / 2.0;

    //    if (y < num4)
    //    {
    //        posY = (num4 - Convert.ToDouble(y)) - 1.0;
    //    }
    //    else
    //    {
    //        posY = ((Convert.ToDouble(y) + 1) - num4) * -1.0;
    //    }
    // more here: http://wiki.openstreetmap.org/wiki/Slippy_map_tilenames
    // hence:
    //            int long2tilex(double lon, int z) 
    //{ 
    //    return (int)(floor((lon + 180.0) / 360.0 * pow(2.0, z))); 
    //}

    //int lat2tiley(double lat, int z)
    //{ 
    //    return (int)(floor((1.0 - log( tan(lat * M_PI/180.0) + 1.0 / cos(lat * M_PI/180.0)) / M_PI) / 2.0 * pow(2.0, z))); 
    //}

    //double tilex2long(int x, int z) 
    //{
    //    return x / pow(2.0, z) * 360.0 - 180;
    //}

    //double tiley2lat(int y, int z) 
    //{
    //    double n = M_PI - 2.0 * M_PI * y / pow(2.0, z);
    //    return 180.0 / M_PI * atan(0.5 * (exp(n) - exp(-n)));
    //}
    //    return new Uri(String.Format(this.UriFormat, x, posY, zoom));
    //}

    //            public PointF WorldToTilePos(double lon, double lat, int zoom)
    //{
    //    PointF p = new Point();
    //    p.X = (float)((lon + 180.0) / 360.0 * (1 << zoom));
    //    p.Y = (float)((1.0 - Math.Log(Math.Tan(lat * Math.PI / 180.0) + 
    //        1.0 / Math.Cos(lat * Math.PI / 180.0)) / Math.PI) / 2.0 * (1 << zoom));

    //    return p;
    //}

    //public PointF TileToWorldPos(double tile_x, double tile_y, int zoom) 
    //{
    //    PointF p = new Point();
    //    double n = Math.PI - ((2.0 * Math.PI * tile_y) / Math.Pow(2.0, zoom));

    //    p.X = (float)((tile_x / Math.Pow(2.0, zoom) * 360.0) - 180.0);
    //    p.Y = (float)(180.0 / Math.PI * Math.Atan(Math.Sinh(n)));

    //    return p;
    //}
    //HttpMapTileDataSource dataSource = new HttpMapTileDataSource() { UriFormatString = "{UriScheme}://ecn.t0.tiles.virtualearth.net/tiles/r{quadkey}.jpeg?g=129&mkt=en-us&shading=hill&stl=H" };
    //HttpMapTileDataSource dataSource = new HttpMapTileDataSource() { UriFormatString = "http://tile.openstreetmap.org/2/0/1.png" }; //  "{UriScheme}://ecn.t0.tiles.virtualearth.net/tiles/r{quadkey}.jpeg?g=129&mkt=en-us&shading=hill&stl=H" };

}
