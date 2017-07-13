using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Utilz;
using Windows.Devices.Geolocation;

namespace LolloGPS.Calcs
{
    public sealed class TileCoordinates
    {
        public int X { get { return _x; } }
        public int Y { get { return _y; } }
        public int Z { get { return _z; } }
        public int Zoom { get { return _zoom; } }

        private readonly int _x = 0;
        private readonly int _y = 0;
        private readonly int _z = 0;
        private readonly int _zoom = 2;

        public TileCoordinates(int x, int y, int z, int zoom)
        {
            _x = x;
            _y = y;
            _z = z;
            _zoom = zoom;
        }

        public static List<TileCoordinates> GetTileCoordinates4MultipleZoomLevels(BasicGeoposition nwCorner, BasicGeoposition seCorner, int maxZoom, int minZoom, long maxTileCount, CancellationToken cancToken)
        {
            var output = new List<TileCoordinates>();
            if (nwCorner.Latitude == seCorner.Latitude && nwCorner.Longitude == seCorner.Longitude || maxZoom < minZoom) return output;

            CancellationTokenSource cancTokenSourceLinked = null;
            try
            {
                if (cancToken.IsCancellationRequested) return output;

                int totalCnt = 0;
                for (int zoom = minZoom; zoom <= maxZoom; zoom++)
                {
                    var topLeftTile = new TileCoordinates(PseudoMercator.Lon2TileX(nwCorner.Longitude, zoom), PseudoMercator.Lat2TileY(nwCorner.Latitude, zoom), 0, zoom); // Alaska
                    var bottomRightTile = new TileCoordinates(PseudoMercator.Lon2TileX(seCorner.Longitude, zoom), PseudoMercator.Lat2TileY(seCorner.Latitude, zoom), 0, zoom); // New Zealand
                    int maxX4Zoom = PseudoMercator.MaxTilexX4Zoom(zoom);
                    Debug.WriteLine("topLeftTile.X = " + topLeftTile.X + " topLeftTile.Y = " + topLeftTile.Y + " bottomRightTile.X = " + bottomRightTile.X + " bottomRightTile.Y = " + bottomRightTile.Y + " and zoom = " + zoom);

                    bool exit = false;
                    bool hasJumpedDateLine = false;

                    int x = topLeftTile.X;
                    while (!exit)
                    {
                        for (int y = topLeftTile.Y; y <= bottomRightTile.Y; y++)
                        {
                            output.Add(new TileCoordinates(x, y, 0, zoom));
                            totalCnt++;
                            if (totalCnt > maxTileCount || cancToken.IsCancellationRequested)
                            {
                                exit = true;
                                break;
                            }
                        }

                        x++;
                        if (x > bottomRightTile.X)
                        {
                            if (topLeftTile.X > bottomRightTile.X && !hasJumpedDateLine)
                            {
                                if (x > maxX4Zoom)
                                {
                                    x = 0;
                                    hasJumpedDateLine = true;
                                }
                            }
                            else
                            {
                                exit = true;
                            }
                        }
                    }
                    if (totalCnt > maxTileCount || cancToken.IsCancellationRequested) break;
                }
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            {
                Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
            }
            finally
            {
                cancTokenSourceLinked?.Dispose();
            }
            return output;
        }
        /*
        internal static Task<TileCacheRecord> GetTileCacheRecordFromDbAsync(TileSourceRecord tileSource, int x, int y, int z, int zoom)
        {
            try
            {
                return DBManager.GetTileRecordAsync(tileSource, x, y, z, zoom);
            }
            catch (Exception ex)
            {
                Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
            }
            return null;
        }
        */
    }
}
