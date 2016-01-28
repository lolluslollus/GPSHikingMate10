using System;
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
using LolloGPS.Core;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using System.Collections.Generic;
using LolloGPS.Data.TileCache;
using LolloGPS.Data.Leeching;
using Utilz.Data;

namespace UnitTestProject1
{
    // LOLLO check http://www.maptiler.org/google-maps-coordinates-tile-bounds-projection/

    [TestClass]
    public class UnitTest1
    {
        private TileDownloaderMock _tdMock = null;

        [TestMethod]
        public void TileDownloaderTest()
        {
            var aaa = TileDownloaderMock.Lon2TileX_Test(0.0, 0);
            var bbb = TileDownloaderMock.Lat2TileY_Test(0.0, 0);
            Assert.AreEqual(aaa, 0);
            Assert.AreEqual(bbb, 0);

            var ccc = TileDownloaderMock.Lon2TileX_Test(89.0, 2);
            var ddd = TileDownloaderMock.Lat2TileY_Test(-66.0, 2);
            Assert.AreEqual(ccc, 2);
            Assert.AreEqual(ddd, 2);
        }
        [TestMethod]
        public void MaxXTest()
        {
            var aaa = TileDownloaderMock.Lon2TileX_Test(179.999999, 15);
            Assert.AreEqual(aaa, 32767);

            var bbb = TileDownloaderMock.Lon2TileX_Test(179.999999, 20);
            Assert.AreEqual(bbb, 1048575);
        }
        [TestMethod]
        public void GetHowManyTiles()
        {
            _tdMock = new TileDownloaderMock(0, 10, 1, 0, 1, 10, 0, 1);
            var aaa = _tdMock.GetTileData_RespondingToCancelTest();
            Assert.AreEqual(aaa.Count, 2);

            _tdMock = new TileDownloaderMock(0, 10, -10, 0, -10, 10, 0, 1);
            var bbb = _tdMock.GetTileData_RespondingToCancelTest();
            Assert.AreEqual(bbb.Count, 5);

            _tdMock = new TileDownloaderMock(0, -0.1, 0.1, 0, -66, 89, 2, 2);
            var ccc = _tdMock.GetTileData_RespondingToCancelTest();
            Assert.AreEqual(ccc.Count, 1);
            Assert.AreEqual(ccc[0].X, 2);
            Assert.AreEqual(ccc[0].Y, 2);

            _tdMock = new TileDownloaderMock(0, 0.1, -0.1, 0, -66, 89, 2, 2);
            var ddd = _tdMock.GetTileData_RespondingToCancelTest();
            Assert.AreEqual(ddd.Count, 4);

            _tdMock = new TileDownloaderMock(0, 0.1, -0.1, 0, -67, 91, 2, 2);
            var eee = _tdMock.GetTileData_RespondingToCancelTest();
            Assert.AreEqual(eee.Count, 9);

            _tdMock = new TileDownloaderMock(0, 91, 0.1, 0, 80, 0.2, 2, 2);
            try
            {
                var fff = _tdMock.GetTileData_RespondingToCancelTest();
                Assert.AreEqual(0, 1);
            }
            catch
            {
                Assert.AreEqual(1, 1);
            }

            _tdMock = new TileDownloaderMock(0, 89, 0.1, 0, 80, 0.2, 2, 2);
            var ggg = _tdMock.GetTileData_RespondingToCancelTest();
            Assert.AreEqual(ggg.Count, 1);
            Assert.AreEqual(ggg[0].X, 2);
            Assert.AreEqual(ggg[0].Y, 0);

            _tdMock = new TileDownloaderMock(0, 89, 0.1, 0, 91, 0.2, 2, 2);
            try
            {
                var hhh = _tdMock.GetTileData_RespondingToCancelTest();
                Assert.AreEqual(0, 1);
            }
            catch
            {
                Assert.AreEqual(1, 1);
            }

            _tdMock = new TileDownloaderMock(0, 80, 0.1, 0, 81, 0.2, 2, 2);
            try
            {
                var iii = _tdMock.GetTileData_RespondingToCancelTest();
                Assert.AreEqual(0, 1);
            }
            catch
            {
                Assert.AreEqual(1, 1);
            }

            _tdMock = new TileDownloaderMock(0, 1, 179, 0, -1, -179, 0, 3);
            var jjj = _tdMock.GetTileData_RespondingToCancelTest();
            Assert.AreEqual(jjj.Count, 13);

            _tdMock = new TileDownloaderMock(0, 1, 179, 0, -1, -179, 3, 3);
            var kkk = _tdMock.GetTileData_RespondingToCancelTest();
            Assert.AreEqual(kkk.Count, 4);
            Assert.IsTrue(kkk[0].X == 7 && kkk[0].Y == 3 || kkk[1].X == 7 && kkk[1].Y == 3 || kkk[2].X == 7 && kkk[2].Y == 3 || kkk[3].X == 7 && kkk[3].Y == 3);
            Assert.IsTrue(kkk[0].X == 0 && kkk[0].Y == 4 || kkk[1].X == 0 && kkk[1].Y == 4 || kkk[2].X == 0 && kkk[2].Y == 4 || kkk[3].X == 0 && kkk[3].Y == 4);

            _tdMock = new TileDownloaderMock(0, 1, -100, 0, -1, -80, 0, 3);
            var lll = _tdMock.GetTileData_RespondingToCancelTest();
            Assert.AreEqual(lll.Count, 11);

            _tdMock = new TileDownloaderMock(0, 30, 150, 0, 20, -150, 4, 4);
            var mmm = _tdMock.GetTileData_RespondingToCancelTest();
            Assert.AreEqual(mmm.Count, 8);

            _tdMock = new TileDownloaderMock(0, -20, 150, 0, -30, -150, 0, 4);
            var nnn = _tdMock.GetTileData_RespondingToCancelTest();
            Assert.AreEqual(nnn.Count, 15);

            _tdMock = null;
        }

        public class TileDownloaderMock : TileDownloader
		{
            int MinZoom;
            int MaxZoom;
            public TileDownloaderMock(double nwAlt, double nwLat, double nwLon, double seAlt, double seLat, double seLon, int minZoom, int maxZoom) : base(new GbbProvider(nwAlt, nwLat, nwLon, seAlt, seLat, seLon))
            {
                MinZoom = minZoom;
                MaxZoom = maxZoom;
            }
            public static int Lon2TileX_Test(double lonDeg, int z)
            {
                return TileDownloader.Lon2TileX(lonDeg, z);
            }
            public static int Lat2TileY_Test(double latDeg, int z)
            {
                return TileDownloader.Lat2TileY(latDeg, z);
            }
            public static int MaxTilexX4Zoom_Test(int z)
            {
                return TileDownloader.MaxTilexX4Zoom(z);
            }
            public List<TileCacheRecord> GetTileData_RespondingToCancelTest()
            {
                GeoboundingBox nw_se = _gbbProvider.GetMinMaxLatLonAsync().Result;
                var ds = new DownloadSession(MinZoom, MaxZoom, nw_se, "lolloTest");
                return GetTileData_RespondingToCancel(ds);
            }
        }

        public class GbbProvider : IGeoBoundingBoxProvider
        {
            BasicGeoposition nw = default(BasicGeoposition);
            BasicGeoposition se = default(BasicGeoposition);

            public GbbProvider(double nwAlt, double nwLat, double nwLon, double seAlt, double seLat, double seLon)
            {
                nw = new BasicGeoposition() { Altitude = nwAlt, Latitude = nwLat, Longitude = nwLon };
                se = new BasicGeoposition() { Altitude = seAlt, Latitude = seLat, Longitude = seLon };
            }
            public async Task<BasicGeoposition> GetCentreAsync()
            {
				await Task.CompletedTask;
                var result = new BasicGeoposition() { Altitude = 0, Latitude = (nw.Latitude + se.Latitude) / 2.0, Longitude = (se.Longitude + nw.Latitude) / 2.0 };
				return result;
            }

            public async Task<GeoboundingBox> GetMinMaxLatLonAsync()
            {
                var gbb = new GeoboundingBox(nw, se);

                await Task.CompletedTask;
                return gbb;
            }
        }
    }
}
