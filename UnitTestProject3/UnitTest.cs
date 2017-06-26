using LolloGPS.Calcs;
using LolloGPS.Data.TileCache;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;

namespace UnitTestProject3
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
            bool dummy = false;

            _tdMock = new TileDownloaderMock(0, 10, 1, 0, 1, 10, 0, 1);
            dummy = _tdMock.OpenAsync().Result;
            var aaa = _tdMock.GetTileData_RespondingToCancelTest();
            Assert.AreEqual(aaa.Count, 2);
            dummy = _tdMock.CloseAsync().Result;

            _tdMock = new TileDownloaderMock(0, 10, 1, 0, 1, 10, 0, 1);
            dummy = _tdMock.OpenAsync().Result;
            var aaa2 = _tdMock.GetTileData_RespondingToCancelTest2();
            Assert.AreEqual(aaa2.Count, 2);
            dummy = _tdMock.CloseAsync().Result;

            _tdMock = new TileDownloaderMock(0, 10, -10, 0, -10, 10, 0, 1);
            dummy = _tdMock.OpenAsync().Result;
            var bbb = _tdMock.GetTileData_RespondingToCancelTest();
            Assert.AreEqual(bbb.Count, 5);
            dummy = _tdMock.CloseAsync().Result;

            _tdMock = new TileDownloaderMock(0, 10, -10, 0, -10, 10, 0, 1);
            dummy = _tdMock.OpenAsync().Result;
            var bbb2 = _tdMock.GetTileData_RespondingToCancelTest2();
            Assert.AreEqual(bbb2.Count, 5);
            dummy = _tdMock.CloseAsync().Result;

            _tdMock = new TileDownloaderMock(0, -0.1, 0.1, 0, -66, 89, 2, 2);
            dummy = _tdMock.OpenAsync().Result;
            var ccc = _tdMock.GetTileData_RespondingToCancelTest();
            Assert.AreEqual(ccc.Count, 1);
            Assert.AreEqual(ccc[0].X, 2);
            Assert.AreEqual(ccc[0].Y, 2);
            dummy = _tdMock.CloseAsync().Result;

            _tdMock = new TileDownloaderMock(0, 0.1, -0.1, 0, -66, 89, 2, 2);
            dummy = _tdMock.OpenAsync().Result;
            var ddd = _tdMock.GetTileData_RespondingToCancelTest();
            Assert.AreEqual(ddd.Count, 4);
            dummy = _tdMock.CloseAsync().Result;

            _tdMock = new TileDownloaderMock(0, 0.1, -0.1, 0, -67, 91, 2, 2);
            dummy = _tdMock.OpenAsync().Result;
            var eee = _tdMock.GetTileData_RespondingToCancelTest();
            Assert.AreEqual(eee.Count, 9);
            dummy = _tdMock.CloseAsync().Result;

            _tdMock = new TileDownloaderMock(0, 91, 0.1, 0, 80, 0.2, 2, 2);
            dummy = _tdMock.OpenAsync().Result;
            try
            {
                var fff = _tdMock.GetTileData_RespondingToCancelTest();
                Assert.AreEqual(0, 1);
            }
            catch
            {
                Assert.AreEqual(1, 1);
            }
            dummy = _tdMock.CloseAsync().Result;

            _tdMock = new TileDownloaderMock(0, 89, 0.1, 0, 80, 0.2, 2, 2);
            dummy = _tdMock.OpenAsync().Result;
            var ggg = _tdMock.GetTileData_RespondingToCancelTest();
            Assert.AreEqual(ggg.Count, 1);
            Assert.AreEqual(ggg[0].X, 2);
            Assert.AreEqual(ggg[0].Y, 0);
            dummy = _tdMock.CloseAsync().Result;

            _tdMock = new TileDownloaderMock(0, 89, 0.1, 0, 91, 0.2, 2, 2);
            dummy = _tdMock.OpenAsync().Result;
            try
            {
                var hhh = _tdMock.GetTileData_RespondingToCancelTest();
                Assert.AreEqual(0, 1);
            }
            catch
            {
                Assert.AreEqual(1, 1);
            }
            dummy = _tdMock.CloseAsync().Result;

            _tdMock = new TileDownloaderMock(0, 80, 0.1, 0, 81, 0.2, 2, 2);
            dummy = _tdMock.OpenAsync().Result;
            try
            {
                var iii = _tdMock.GetTileData_RespondingToCancelTest();
                Assert.AreEqual(0, 1);
            }
            catch
            {
                Assert.AreEqual(1, 1);
            }
            dummy = _tdMock.CloseAsync().Result;

            _tdMock = new TileDownloaderMock(0, 1, 179, 0, -1, -179, 0, 3);
            dummy = _tdMock.OpenAsync().Result;
            var jjj = _tdMock.GetTileData_RespondingToCancelTest();
            Assert.AreEqual(jjj.Count, 13);
            dummy = _tdMock.CloseAsync().Result;

            _tdMock = new TileDownloaderMock(0, 1, 179, 0, -1, -179, 3, 3);
            dummy = _tdMock.OpenAsync().Result;
            var kkk = _tdMock.GetTileData_RespondingToCancelTest();
            Assert.AreEqual(kkk.Count, 4);
            Assert.IsTrue(kkk[0].X == 7 && kkk[0].Y == 3 || kkk[1].X == 7 && kkk[1].Y == 3 || kkk[2].X == 7 && kkk[2].Y == 3 || kkk[3].X == 7 && kkk[3].Y == 3);
            Assert.IsTrue(kkk[0].X == 0 && kkk[0].Y == 4 || kkk[1].X == 0 && kkk[1].Y == 4 || kkk[2].X == 0 && kkk[2].Y == 4 || kkk[3].X == 0 && kkk[3].Y == 4);
            dummy = _tdMock.CloseAsync().Result;

            _tdMock = new TileDownloaderMock(0, 1, -100, 0, -1, -80, 0, 3);
            dummy = _tdMock.OpenAsync().Result;
            var lll = _tdMock.GetTileData_RespondingToCancelTest();
            Assert.AreEqual(lll.Count, 11);
            dummy = _tdMock.CloseAsync().Result;

            _tdMock = new TileDownloaderMock(0, 30, 150, 0, 20, -150, 4, 4);
            dummy = _tdMock.OpenAsync().Result;
            var mmm = _tdMock.GetTileData_RespondingToCancelTest();
            Assert.AreEqual(mmm.Count, 8);
            dummy = _tdMock.CloseAsync().Result;

            _tdMock = new TileDownloaderMock(0, -20, 150, 0, -30, -150, 0, 4);
            dummy = _tdMock.OpenAsync().Result;
            var nnn = _tdMock.GetTileData_RespondingToCancelTest();
            Assert.AreEqual(nnn.Count, 15);
            dummy = _tdMock.CloseAsync().Result;

            _tdMock = new TileDownloaderMock(0, -20, 150, 0, -30, -150, 0, 4);
            dummy = _tdMock.OpenAsync().Result;
            var nnn2 = _tdMock.GetTileData_RespondingToCancelTest2();
            Assert.AreEqual(nnn2.Count, 15);
            dummy = _tdMock.CloseAsync().Result;

            _tdMock = null;
        }
        [TestMethod]
        public void GetHowManyTiles_WithBaseLayer()
        {
            bool dummy = false;

            _tdMock = new TileDownloaderMock(0, 10, 1, 0, 1, 10, 0, 1);
            var tileSources = new List<TileSourceRecord>();
            tileSources.Add(new TileSourceRecord(false, "", "", "a", "a", "a", "", "", 8, 8, 256, false, false, new Dictionary<string, string>()));
            tileSources.Add(new TileSourceRecord(false, "", "", "a", "a", "a", "", "", 4, 10, 256, false, true, new Dictionary<string, string>()));
            tileSources.Add(new TileSourceRecord(false, "", "", "a", "a", "a", "", "", 2, 20, 256, false, true, new Dictionary<string, string>()));
            dummy = _tdMock.OpenAsync().Result;
            var aaa = _tdMock.GetTileData_RespondingToCancelTest3(tileSources);
            Assert.AreEqual(aaa.Count, 0);
            dummy = _tdMock.CloseAsync().Result;

            _tdMock = new TileDownloaderMock(0, 10, 1, 0, 1, 10, 0, 2);
            tileSources = new List<TileSourceRecord>();
            tileSources.Add(new TileSourceRecord(false, "", "", "a", "a", "a", "", "", 8, 8, 256, false, false, new Dictionary<string, string>()));
            tileSources.Add(new TileSourceRecord(false, "", "", "a", "a", "a", "", "", 2, 10, 256, false, true, new Dictionary<string, string>()));
            tileSources.Add(new TileSourceRecord(false, "", "", "a", "a", "a", "", "", 8, 20, 256, false, true, new Dictionary<string, string>()));
            dummy = _tdMock.OpenAsync().Result;
            var bbb = _tdMock.GetTileData_RespondingToCancelTest3(tileSources);
            Assert.AreEqual(bbb.Count, 1);
            dummy = _tdMock.CloseAsync().Result;

            _tdMock = new TileDownloaderMock(80, 80, -179, 0, -80, 179, 0, 2);
            tileSources = new List<TileSourceRecord>();
            tileSources.Add(new TileSourceRecord(false, "", "", "a", "a", "a", "", "", 8, 8, 256, false, false, new Dictionary<string, string>()));
            tileSources.Add(new TileSourceRecord(false, "", "", "a", "a", "a", "", "", 2, 10, 256, false, true, new Dictionary<string, string>()));
            tileSources.Add(new TileSourceRecord(false, "", "", "a", "a", "a", "", "", 8, 20, 256, false, true, new Dictionary<string, string>()));
            dummy = _tdMock.OpenAsync().Result;
            var ccc = _tdMock.GetTileData_RespondingToCancelTest3(tileSources);
            Assert.AreEqual(ccc.Count, 16);
            dummy = _tdMock.CloseAsync().Result;
        }
        [TestMethod]
        public void GetHowManyTiles_NoBaseLayer()
        {
            bool dummy = false;

            _tdMock = new TileDownloaderMock(0, 10, 1, 0, 1, 10, 0, 1);
            var tileSources = new List<TileSourceRecord>();
            tileSources.Add(new TileSourceRecord(false, "", "", "a", "a", "a", "", "", 8, 8, 256, false, true, new Dictionary<string, string>()));
            tileSources.Add(new TileSourceRecord(false, "", "", "a", "a", "a", "", "", 4, 10, 256, false, true, new Dictionary<string, string>()));
            tileSources.Add(new TileSourceRecord(false, "", "", "a", "a", "a", "", "", 2, 20, 256, false, true, new Dictionary<string, string>()));
            dummy = _tdMock.OpenAsync().Result;
            var aaa = _tdMock.GetTileData_RespondingToCancelTest3(tileSources);
            Assert.AreEqual(aaa.Count, 0);
            dummy = _tdMock.CloseAsync().Result;

            _tdMock = new TileDownloaderMock(0, 10, 1, 0, 1, 10, 0, 2);
            tileSources = new List<TileSourceRecord>();
            tileSources.Add(new TileSourceRecord(false, "", "", "a", "a", "a", "", "", 8, 8, 256, false, true, new Dictionary<string, string>()));
            tileSources.Add(new TileSourceRecord(false, "", "", "a", "a", "a", "", "", 2, 10, 256, false, true, new Dictionary<string, string>()));
            tileSources.Add(new TileSourceRecord(false, "", "", "a", "a", "a", "", "", 8, 20, 256, false, true, new Dictionary<string, string>()));
            dummy = _tdMock.OpenAsync().Result;
            var bbb = _tdMock.GetTileData_RespondingToCancelTest3(tileSources);
            Assert.AreEqual(bbb.Count, 1);
            dummy = _tdMock.CloseAsync().Result;

            _tdMock = new TileDownloaderMock(80, 80, -179, 0, -80, 179, 0, 2);
            tileSources = new List<TileSourceRecord>();
            tileSources.Add(new TileSourceRecord(false, "", "", "a", "a", "a", "", "", 8, 8, 256, false, true, new Dictionary<string, string>()));
            tileSources.Add(new TileSourceRecord(false, "", "", "a", "a", "a", "", "", 2, 10, 256, false, true, new Dictionary<string, string>()));
            tileSources.Add(new TileSourceRecord(false, "", "", "a", "a", "a", "", "", 8, 20, 256, false, true, new Dictionary<string, string>()));
            dummy = _tdMock.OpenAsync().Result;
            var ccc = _tdMock.GetTileData_RespondingToCancelTest3(tileSources);
            Assert.AreEqual(ccc.Count, 16);
            dummy = _tdMock.CloseAsync().Result;
        }
        public class TileDownloaderMock : TileDownloader
        {
            private readonly int MinZoom;
            private readonly int MaxZoom;
            public TileDownloaderMock(double nwAlt, double nwLat, double nwLon, double seAlt, double seLat, double seLon, int minZoom, int maxZoom) : base(new GbbProvider(nwAlt, nwLat, nwLon, seAlt, seLat, seLon))
            {
                MinZoom = minZoom;
                MaxZoom = maxZoom;
            }
            public static int Lon2TileX_Test(double lonDeg, int z)
            {
                return PseudoMercator.Lon2TileX(lonDeg, z);
            }
            public static int Lat2TileY_Test(double latDeg, int z)
            {
                return PseudoMercator.Lat2TileY(latDeg, z);
            }
            public static int MaxTilexX4Zoom_Test(int z)
            {
                return PseudoMercator.MaxTilexX4Zoom(z);
            }
            public List<TileCacheRecord> GetTileData_RespondingToCancelTest()
            {
                GeoboundingBox nw_se = _gbbProvider.GetMinMaxLatLonAsync().Result;
                var dummyTileSources = TileSourceRecord.GetStockTileSources();
                //foreach (var ts in dummyTileSources)
                //{
                //    ts.TechName = "lolloTest";
                //}
                var ds = new DownloadSession(nw_se, dummyTileSources, MaxZoom);
                return GetTileData2(ds.NWCorner, ds.SECorner, MaxZoom, MinZoom, CancToken);
            }
            public List<TileCacheRecord> GetTileData_RespondingToCancelTest2()
            {
                GeoboundingBox nw_se = _gbbProvider.GetMinMaxLatLonAsync().Result;
                var dummyTileSources = TileSourceRecord.GetStockTileSources();
                //foreach (var ts in dummyTileSources)
                //{
                //    ts.TechName = "lolloTest";
                //}
                var ds = new DownloadSession(MinZoom, MaxZoom, nw_se.NorthwestCorner, nw_se.SoutheastCorner, dummyTileSources);
                return GetTileData2(ds.NWCorner, ds.SECorner, MaxZoom, MinZoom, CancToken);
            }
            public List<TileCacheRecord> GetTileData_RespondingToCancelTest3(IEnumerable<TileSourceRecord> tileSources)
            {
                GeoboundingBox nw_se = _gbbProvider.GetMinMaxLatLonAsync().Result;
                var ds = new DownloadSession(MinZoom, MaxZoom, nw_se.NorthwestCorner, nw_se.SoutheastCorner, tileSources);
                var result = new List<TileCacheRecord>();
                foreach (var ts in ds.TileSources)
                {
                    result.AddRange(GetTileData2(ds.NWCorner, ds.SECorner, ts.MaxZoom, ts.MinZoom, CancToken));
                }
                return result;
            }
        }

        public class GbbProvider : IGeoBoundingBoxProvider
        {
            private BasicGeoposition nw = default(BasicGeoposition);
            private BasicGeoposition se = default(BasicGeoposition);

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
