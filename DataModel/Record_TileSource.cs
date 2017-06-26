using LolloGPS.Data.Runtime;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Utilz;
using Utilz.Data;
using Windows.Storage;

// LOLLO NOTE https://dzjow.com/2012/04/12/free-online-topographic-maps-for-hiking/
// http://onlinetopomaps.net/
// http://www.oruxmaps.com/foro/viewtopic.php?t=3035

namespace LolloGPS.Data
{
    [DataContract]
    [KnownType(typeof(string[]))]
    //[KnownType(typeof(IReadOnlyList<string>))]
    [KnownType(typeof(ReadOnlyCollection<string>))]
    public class TileSourceRecord : ObservableData, IComparable
    {
        #region constants
        protected const string DefaultTileSourceTechName = "Nokia";
        protected const string AllTileSourceTechName = "All";
        protected const string NoTileSourceTechName = "None";
        protected const string SampleTileSourceTechName = "YourOwnTileSource";

        protected const string DefaultTileSourceDisplayName = "Built in";
        protected const string AllTileSourceDisplayName = "All";
        protected const string NoTileSourceDisplayName = "None";

        protected static readonly string[] DefaultTileSourceUriString = { };
        protected static readonly string DefaultTileSourceProviderUriString = string.Empty;
        protected static readonly string[] DummyTileSourceUriString = { };
        protected static readonly string DummyTileSourceProviderUriString = string.Empty;
        protected static readonly string[] SampleUriString = { $"http://tileserver.something/{ZoomLevelPlaceholder}/{XPlaceholder}/{YPlaceholder}.png" };

        public const int MinMinZoom = 0;
        protected const int DummyTileSourceMinZoom = 0;

        public const int MaxMaxZoom = 20;
        protected const int DummyTileSourceMaxZoom = 0;
        protected const int SampleMaxZoom = 16;

        protected const int DefaultTilePixelSize = 256;
        protected const int DummyTileSourceTilePixelSize = 0;

        public const string ZoomLevelPlaceholder = "{zoomlevel}";
        public const string XPlaceholder = "{x}";
        public const string YPlaceholder = "{y}";
        public const string ZoomLevelPlaceholder_Internal = "{0}";
        public const string XPlaceholder_Internal = "{1}";
        public const string YPlaceholder_Internal = "{2}";

        public const int MinTilePixelSize = 64;
        public const int MaxTilePixelSize = 1024;
        public const int MaxTechNameLength = 25;

        protected static Dictionary<string, string> GetDefaultWebHeaderCollection()
        {
            return new Dictionary<string, string>();
        }
        private static Dictionary<string, string> GetAcceptImageWebHeaderCollection()
        {
            var headers = new Dictionary<string, string>();
            headers[Enum.GetName(typeof(HttpRequestHeader), HttpRequestHeader.Accept)] = TileCache.TileCacheReaderWriter.MimeTypeImageAny;
            return headers;
        }
        private static Dictionary<string, string> GetSchweizmobilWebHeaderCollection()
        {
            var headers = new Dictionary<string, string>();
            headers[Enum.GetName(typeof(HttpRequestHeader), HttpRequestHeader.Referer)] = "https://map.schweizmobil.ch/?lang=en&bgLayer=pk&resolution=256";
            return headers;
        }
        #endregion constants

        #region properties
        [DataMember]
        protected string _techName = DefaultTileSourceTechName;
        public string TechName { get { return _techName; } }

        [DataMember]
        protected string _folderName = DefaultTileSourceTechName;
        public string FolderName { get { return _folderName; } }

        [DataMember]
        protected string _displayName = DefaultTileSourceDisplayName;
        public string DisplayName { get { return _displayName; } }

        [DataMember]
        protected string _copyrightNotice = DefaultTileSourceDisplayName;
        public string CopyrightNotice { get { return _copyrightNotice; } }

        [DataMember]
        protected IReadOnlyList<string> _uriStrings = SampleUriString;
        public IReadOnlyList<string> UriStrings { get { return _uriStrings; } }

        [DataMember]
        protected string _providerUriString = string.Empty;
        public string ProviderUriString { get { return _providerUriString; } }

        [DataMember]
        protected int _minZoom = MinMinZoom;
        public int MinZoom { get { return _minZoom; } }

        [DataMember]
        protected int _maxZoom = MaxMaxZoom;
        public int MaxZoom { get { return _maxZoom; } }

        [DataMember]
        protected int _tilePixelSize = DefaultTilePixelSize;
        public int TilePixelSize { get { return _tilePixelSize; } }

        [DataMember]
        protected bool _isDeletable = false;
        public bool IsDeletable { get { return _isDeletable; } }

        [DataMember]
        protected bool _isFileSource = false;
        public bool IsFileSource { get { return _isFileSource; } }

        [DataMember]
        protected string _tileSourceFolderPath = string.Empty;
        public string TileSourceFolderPath { get { return _tileSourceFolderPath; } }

        [DataMember]
        protected string _tileSourceFileName = string.Empty;
        public string TileSourceFileName { get { return _tileSourceFileName; } }

        // WebHeaderCollection cannot be serialised and deserialised, so we use a dictionary
        [DataMember]
        protected Dictionary<string, string> _requestHeaders = new Dictionary<string, string>();
        public Dictionary<string, string> RequestHeaders { get { return _requestHeaders; } }

        [DataMember]
        protected bool _isOverlay = false;
        public bool IsOverlay { get { return _isOverlay; } }

        [IgnoreDataMember]
        public bool IsDefault { get { return _techName == DefaultTileSourceTechName; } }
        [IgnoreDataMember]
        public bool IsAll { get { return _techName == AllTileSourceTechName; } }
        [IgnoreDataMember]
        public bool IsNone { get { return _techName == NoTileSourceTechName; } }
        [IgnoreDataMember]
        public bool IsSample { get { return _techName == SampleTileSourceTechName; } }

        [IgnoreDataMember]
        public int MaxTechNameLengthProp { get { return MaxTechNameLength; } }
        #endregion properties

        public TileSourceRecord(bool isFileSource, string tileSourceFolderPath, string tileSourceFileName, string techName, string displayName, string folderName, string copyrightNotice,
            string providerUri, int minZoom, int maxZoom, int tilePixelSize,
            bool isDeletable, bool isOverlay,
            Dictionary<string, string> headers, params string[] uriStrings)
        {
            _isFileSource = isFileSource;
            _tileSourceFolderPath = tileSourceFolderPath;
            _tileSourceFileName = tileSourceFileName;
            _techName = techName;
            _folderName = folderName;
            _displayName = displayName;
            _copyrightNotice = copyrightNotice;
            _providerUriString = providerUri;
            _minZoom = minZoom;
            _maxZoom = maxZoom;
            _tilePixelSize = tilePixelSize;
            _isDeletable = isDeletable;
            _isOverlay = isOverlay;
            _requestHeaders = headers;
            _uriStrings = uriStrings;
        }

        #region checks
        public async Task<string> CheckAsync(CancellationToken cancToken)
        {
            string errorMsg = string.Empty;
            errorMsg = CheckTechName(TechName);
            if (!string.IsNullOrEmpty(errorMsg)) return errorMsg;
            errorMsg = CheckDisplayName(DisplayName);
            if (!string.IsNullOrEmpty(errorMsg)) return errorMsg;
            if (IsFileSource == true)
            {
                errorMsg = CheckUri(TileSourceFileName, false, true);
                if (!string.IsNullOrEmpty(errorMsg)) return errorMsg;
                errorMsg = await CheckFileSourceAsync(TileSourceFolderPath, TileSourceFileName, cancToken);
                if (!string.IsNullOrEmpty(errorMsg)) return errorMsg;
            }
            else
            {
                errorMsg = CheckUri(UriStrings, false, false);
                if (!string.IsNullOrEmpty(errorMsg)) return errorMsg;
                errorMsg = CheckUri(ProviderUriString, true, false);
                if (!string.IsNullOrEmpty(errorMsg)) return errorMsg;
            }
            errorMsg = CheckMinMaxZoom(MinZoom, MaxZoom);
            if (!string.IsNullOrEmpty(errorMsg)) return errorMsg;
            errorMsg = CheckTilePixelSize(TilePixelSize);
            if (!string.IsNullOrEmpty(errorMsg)) return errorMsg;

            return string.Empty;
        }
        private static async Task<string> CheckFileSourceAsync(string tileSourceFolderPath, string tileSourceFileName, CancellationToken cancToken)
        {
            if (string.IsNullOrWhiteSpace(tileSourceFolderPath)) return "Assign a folder";
            if (string.IsNullOrWhiteSpace(tileSourceFileName)) return "Assign a file name";
            try
            {
                var folder = await Pickers.GetLastPickedFolderAsync(tileSourceFolderPath).ConfigureAwait(false);
                if (folder == null) return "Folder does not exist";
            }
            catch
            {
                return "Folder cannot be accessed";
            }

            return string.Empty;
        }
        private static string CheckTechName(string techName)
        {
            if (string.IsNullOrWhiteSpace(techName)) return "Name must not be empty";
            if (techName.Length > MaxTechNameLength) return string.Format("Name must be max {0} characters", MaxTechNameLength);
            //int invalidCharIndex = techName.IndexOfAny(Path.GetInvalidPathChars());
            //if (invalidCharIndex >= 0) return string.Format("{0} is an invalid character", techName.Substring(invalidCharIndex, 1));
            //invalidCharIndex = techName.IndexOfAny(Path.GetInvalidFileNameChars());
            //if (invalidCharIndex >= 0) return string.Format("{0} is an invalid character", techName.Substring(invalidCharIndex, 1));
            if (!techName.All(char.IsLetter)) return "Name: only letters are allowed";

            return string.Empty;
        }
        private static string CheckDisplayName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName)) return "Name must not be empty";
            if (displayName.Length > MaxTechNameLength) return string.Format("Name must be max {0} characters", MaxTechNameLength);

            return string.Empty;
        }
        private static string CheckUri(IReadOnlyList<string> uris, bool isEmptyAllowed, bool isFileSource)
        {
            if (uris == null || uris.Count == 0)
            {
                if (isEmptyAllowed) return string.Empty;
                else return "Uri is empty";
            }
            foreach (var item in uris)
            {
                var result = CheckUri(item, isEmptyAllowed, isFileSource);
                if (!string.IsNullOrWhiteSpace(result)) return result;
            }
            return string.Empty;
        }
        private static string CheckUri(string uri, bool isEmptyAllowed, bool isFileSource)
        {
            if (string.IsNullOrWhiteSpace(uri))
            {
                if (isEmptyAllowed) return string.Empty;
                else return "Uri is empty";
            }
            if (uri.Length < 7) return "Invalid uri";
            if (!uri.Contains(ZoomLevelPlaceholder)) return string.Format("Uri must contain {0}", ZoomLevelPlaceholder);
            if (!uri.Contains(XPlaceholder)) return string.Format("Uri must contain {0}", XPlaceholder);
            if (!uri.Contains(YPlaceholder)) return string.Format("Uri must contain {0}", YPlaceholder);
            if (uri.Count(u => u == '{') > 3) return "Uri may only have three placeholders";
            if (uri.Count(u => u == '}') > 3) return "Uri may only have three placeholders";

            string sTestUri = string.Empty;
            try
            {
                string testUriFormat = uri.Replace(ZoomLevelPlaceholder, ZoomLevelPlaceholder_Internal);
                testUriFormat = testUriFormat.Replace(XPlaceholder, XPlaceholder_Internal);
                testUriFormat = testUriFormat.Replace(YPlaceholder, YPlaceholder_Internal);

                sTestUri = string.Format(testUriFormat, 84848, 42424, 20202);
                if (!sTestUri.Contains("84848") || !sTestUri.Contains("42424") || !sTestUri.Contains("20202")) return "The string must include {zoomlevel}, {x} and {y}";

                if (isFileSource) return string.Empty; // not a real uri: skip the next checks

                bool isWellFormed = Uri.IsWellFormedUriString(sTestUri, UriKind.Absolute);
                if (!isWellFormed) return "Uri format is invalid";

                var builder = new UriBuilder(sTestUri);
            }
            catch (Exception exc)
            {
                return "Uri format is invalid";
            }
            if (RuntimeData.GetInstance().IsConnectionAvailable)
            {
                try
                {
                    var request = WebRequest.Create(sTestUri);
                }
                catch (Exception exc)
                {
                    return exc.Message;
                }
            }
            return string.Empty;
        }
        public static string CheckMinMaxZoom(int minZoom, int maxZoom)
        {
            if (minZoom < MinMinZoom) return string.Format("Min zoom must be at least {0}", MinMinZoom);
            if (minZoom > MaxMaxZoom) return string.Format("Min zoom must not exceed {0}", MaxMaxZoom);
            if (maxZoom < MinMinZoom) return string.Format("Max zoom must be at least {0}", MinMinZoom);
            if (maxZoom > MaxMaxZoom) return string.Format("Max zoom must not exceed {0}", MaxMaxZoom);
            if (minZoom > maxZoom) return "Min zoom must not exceed max zoom";
            return string.Empty;
        }
        private static string CheckTilePixelSize(int tilePixelSize)
        {
            if (tilePixelSize < MinTilePixelSize) return string.Format("Tile size must be at least {0}", MinTilePixelSize);
            if (tilePixelSize > MaxTilePixelSize) return string.Format("Tile size must not exceed {0}", MaxTilePixelSize);
            return string.Empty;
        }
        #endregion checks

        public static TileSourceRecord Clone(TileSourceRecord source)
        {
            if (source == null) return null;

            return new TileSourceRecord(source._isFileSource, source._tileSourceFolderPath, source._tileSourceFileName, source._techName, source._displayName, source._folderName, source._copyrightNotice,
                source._providerUriString, source._minZoom, source._maxZoom, source._tilePixelSize,
                source._isDeletable, source._isOverlay,
                // LOLLO TODO check that the clones below really clone
                new Dictionary<string, string>(source._requestHeaders), source._uriStrings.ToArray());
        }
        public bool IsEqualTo(TileSourceRecord comp)
        {
            if (comp == null) return false;

            return comp._techName == _techName
                && comp._folderName == _folderName
                && comp._displayName == _displayName
                && comp._copyrightNotice == _copyrightNotice
                && comp._uriStrings.OrderBy(str => str).SequenceEqual(_uriStrings.OrderBy(str => str))
                && comp._providerUriString == _providerUriString
                && comp._minZoom == _minZoom
                && comp._maxZoom == _maxZoom
                && comp._tilePixelSize == _tilePixelSize
                && comp._isDeletable == _isDeletable
                && comp._isFileSource == _isFileSource
                && comp._tileSourceFolderPath == _tileSourceFolderPath
                && comp._tileSourceFileName == _tileSourceFileName
                && comp._isOverlay == _isOverlay
                && comp._requestHeaders.OrderBy(kvp => kvp.Key).SequenceEqual(_requestHeaders.OrderBy(kvp => kvp.Key));
        }
        public int CompareTo(object obj)
        {
            var ts = obj as TileSourceRecord;
            if (ts == null) return -1;
            if (IsEqualTo(ts)) return 0;
            return 1;
        }

        public static List<TileSourceRecord> GetStockTileSources()
        {
            var output = new List<TileSourceRecord>
            {
                TileSourceRecord.GetDefaultTileSource(),
                new TileSourceRecord(false, "", "", "ForUMaps", "4UMaps", "ForUMaps", "4UMaps.eu",
                    "http://www.4umaps.eu/", 2, 15, 256, false, false, GetAcceptImageWebHeaderCollection(), "http://4umaps.eu/{zoomlevel}/{x}/{y}.png"),
                new TileSourceRecord(false, "", "", "OpenTopoMap", "OpenTopoMap", "OpenTopoMap", "OpenTopoMap and OpenStreetMap",
                    "http://opentopomap.org/", 2, 16, 256, false, false, GetAcceptImageWebHeaderCollection(),
                    "http://a.tile.opentopomap.org/{zoomlevel}/{x}/{y}.png",
                    "http://b.tile.opentopomap.org/{zoomlevel}/{x}/{y}.png",
                    "http://c.tile.opentopomap.org/{zoomlevel}/{x}/{y}.png"),
                new TileSourceRecord(false, "", "", "OpenTopoMapTwo", "OpenTopoMap Two", "OpenTopoMap", "OpenTopoMap and OpenStreetMap",
                    "http://opentopomap.org/", 2, 16, 256, false, false, GetAcceptImageWebHeaderCollection(),
                    "http://opentopomap.org/{zoomlevel}/{x}/{y}.png"),
                new TileSourceRecord(false, "", "", "OpenStreetMap", "OpenStreetMap", "OpenStreetMap", "",
                    "http://www.openstreetmap.org/", 0, 18, 256, false, false, GetAcceptImageWebHeaderCollection(),
                    "http://a.tile.openstreetmap.org/{zoomlevel}/{x}/{y}.png",
                    "http://b.tile.openstreetmap.org/{zoomlevel}/{x}/{y}.png",
                    "http://c.tile.openstreetmap.org/{zoomlevel}/{x}/{y}.png"),
                new TileSourceRecord(false, "", "", "OpenStreetMapBW", "OpenStreetMap BW", "OpenStreetMapBW", "OpenStreetMap",
                    "http://www.openstreetmap.org/", 0, 18, 256, false, false, GetAcceptImageWebHeaderCollection(),
                    "http://a.www.toolserver.org/tiles/bw-mapnik/{zoomlevel}/{x}/{y}.png",
                    "http://b.www.toolserver.org/tiles/bw-mapnik/{zoomlevel}/{x}/{y}.png",
                    "http://c.www.toolserver.org/tiles/bw-mapnik/{zoomlevel}/{x}/{y}.png",
                    "http://d.www.toolserver.org/tiles/bw-mapnik/{zoomlevel}/{x}/{y}.png",
                    "http://e.www.toolserver.org/tiles/bw-mapnik/{zoomlevel}/{x}/{y}.png",
                    "http://f.www.toolserver.org/tiles/bw-mapnik/{zoomlevel}/{x}/{y}.png",
                    "http://g.www.toolserver.org/tiles/bw-mapnik/{zoomlevel}/{x}/{y}.png",
                    "http://h.www.toolserver.org/tiles/bw-mapnik/{zoomlevel}/{x}/{y}.png",
                    "http://i.www.toolserver.org/tiles/bw-mapnik/{zoomlevel}/{x}/{y}.png",
                    "http://j.www.toolserver.org/tiles/bw-mapnik/{zoomlevel}/{x}/{y}.png"),
                new TileSourceRecord(false, "", "", "OpenStreetMapHum", "OpenStreetMap Humanitarian","OpenStreetMapHum", "OpenStreetMap",
                    "http://www.openstreetmap.org/", 0, 18, 256, false, false, GetAcceptImageWebHeaderCollection(),
                    "http://tile-a.openstreetmap.fr/hot/{zoomlevel}/{x}/{y}.png",
                    "http://tile-b.openstreetmap.fr/hot/{zoomlevel}/{x}/{y}.png",
                    "http://tile-c.openstreetmap.fr/hot/{zoomlevel}/{x}/{y}.png"),
                new TileSourceRecord(false, "", "", "OpenBusMap", "OpenBusMap", "OpenBusMap", "",
                    "http://openbusmap.org/", 3, 18, 256, false, false, GetAcceptImageWebHeaderCollection(),
                    "http://tileserver.memomaps.de/tilegen/{zoomlevel}/{x}/{y}.png"),
                new TileSourceRecord(false, "", "", "OpenSeaMap", "OpenSeaMap", "OpenSeaMap", "",
                    "http://openseamap.org/?L=1", 9, 18, 256, false, true, GetAcceptImageWebHeaderCollection(),
                    "http://tiles.openseamap.org/seamark/{zoomlevel}/{x}/{y}.png"),
                new TileSourceRecord(false, "", "", "Lonvia", "Lonvia", "Lonvia", "",
                    "http://www.lonvia.de", 5, 18, 256, false, true, GetAcceptImageWebHeaderCollection(),
                    "http://tile.lonvia.de/hiking/{zoomlevel}/{x}/{y}.png"),
				//new TileSourceRecord(false, "", "", "MapQuestOSM", "MapQuest OSM", // no more provided since mid 2016
				//	"http://otile1.mqcdn.com/tiles/1.0.0/osm/{zoomlevel}/{x}/{y}.png", 
				//  "http://www.mapquest.com/", 0, 18, 256, false, false, GetAcceptImageWebHeaderCollection()),
				new TileSourceRecord(false, "", "", "HikeBike", "Hike & Bike Map","HikeBike", "",
                    "http://hikebikemap.org/", 0, 16, 256, false, false, GetAcceptImageWebHeaderCollection(),
                    "http://a.tiles.wmflabs.org/hikebike/{zoomlevel}/{x}/{y}.png",
                    "http://b.tiles.wmflabs.org/hikebike/{zoomlevel}/{x}/{y}.png",
                    "http://c.tiles.wmflabs.org/hikebike/{zoomlevel}/{x}/{y}.png"),
                new TileSourceRecord(false, "", "", "ThunderforestLandscape", "Thunderforest Landscape","ThunderforestLandscape", "",
                    "http://www.thunderforest.com/", 2, 18, 256, false, false, GetAcceptImageWebHeaderCollection(),
                    "http://a.tile.thunderforest.com/landscape/{zoomlevel}/{x}/{y}.png",
                    "http://b.tile.thunderforest.com/landscape/{zoomlevel}/{x}/{y}.png",
                    "http://c.tile.thunderforest.com/landscape/{zoomlevel}/{x}/{y}.png"),
                new TileSourceRecord(false, "", "", "ThunderforestCycle", "Thunderforest Cycle","ThunderforestCycle", "",
                    "http://www.thunderforest.com/", 2, 18, 256, false, false, GetAcceptImageWebHeaderCollection(),
                    "http://a.tile.thunderforest.com/cycle/{zoomlevel}/{x}/{y}.png",
                    "http://b.tile.thunderforest.com/cycle/{zoomlevel}/{x}/{y}.png",
                    "http://c.tile.thunderforest.com/cycle/{zoomlevel}/{x}/{y}.png"),
                new TileSourceRecord(false, "", "", "ThunderforestOutdoors", "Thunderforest Outdoors","ThunderforestOutdoors", "",
                    "http://www.thunderforest.com/", 2, 18, 256, false, false, GetAcceptImageWebHeaderCollection(),
                    "http://a.tile.thunderforest.com/outdoors/{zoomlevel}/{x}/{y}.png",
                    "http://b.tile.thunderforest.com/outdoors/{zoomlevel}/{x}/{y}.png",
                    "http://c.tile.thunderforest.com/outdoors/{zoomlevel}/{x}/{y}.png"),
                new TileSourceRecord(false, "", "", "ThunderforestTransport", "Thunderforest Transport","ThunderforestTransport", "",
                    "http://www.thunderforest.com/", 2, 18, 256, false, false, GetAcceptImageWebHeaderCollection(),
                    "http://a.tile.thunderforest.com/transport/{zoomlevel}/{x}/{y}.png",
                    "http://b.tile.thunderforest.com/transport/{zoomlevel}/{x}/{y}.png",
                    "http://c.tile.thunderforest.com/transport/{zoomlevel}/{x}/{y}.png"),
                // not very useful
                //new TileSourceRecord(false, "", "", "StamenToner", "Stamen Toner","StamenToner", "",
                //    "http://maps.stamen.com/", 2, 18, 256, false, false, GetAcceptImageWebHeaderCollection(),
                //    "http://tile.stamen.com/toner/{zoomlevel}/{x}/{y}.png"),
                // not very useful
                //new TileSourceRecord(false, "", "", "StamenTerrain", "Stamen Terrain","StamenTerrain", "",
                //    "http://tile.stamen.com/terrain/{zoomlevel}/{x}/{y}.jpg",
                //    "http://maps.stamen.com/", 5, 18, 256, false, false, GetAcceptImageWebHeaderCollection()),
                // this one has funny coordinates
                //new TileSourceRecord(false, "", "", "Eniro", "Eniro (Scandinavia)",
                //    "https://map02.eniro.no/geowebcache/service/tms1.0.0/map/{zoomlevel}/{x}/{y}.png",
                //    "https://kartor.eniro.se/", 3, 17, 256, false, false, GetAcceptImageWebHeaderCollection()),
                // this one has funny coordinates
                //new TileSourceRecord(false, "", "", "NLSIceland", "National Land Survey (Iceland)",
                //    "https://gis.lmi.is/mapcache/wmts/1.0.0/LMI_Kort/default/EPSG3057/{zoomlevel}/{x}/{y}.png",
                //    "http://kortasja.lmi.is/en/", 5, 15, 256, false, false, GetAcceptImageWebHeaderCollection()),
                new TileSourceRecord(false, "", "", "KartFinnNo", "Kart Finn Norway","KartFinnNo", "",
                    "http://kart.finn.no/", 4, 20, 256, false, false, GetAcceptImageWebHeaderCollection(),
                    "http://maptiles1.finncdn.no/tileService/1.0.3/normap/{zoomlevel}/{x}/{y}.png",
                    "http://maptiles2.finncdn.no/tileService/1.0.3/normap/{zoomlevel}/{x}/{y}.png",
                    "http://maptiles3.finncdn.no/tileService/1.0.3/normap/{zoomlevel}/{x}/{y}.png",
                    "http://maptiles4.finncdn.no/tileService/1.0.3/normap/{zoomlevel}/{x}/{y}.png"),
                new TileSourceRecord(false, "", "", "KartFinnNoHd", "Kart Finn Norway HD", "KartFinnNoHd", "",
                    "http://kart.finn.no/", 4, 20, 512, false, false, GetAcceptImageWebHeaderCollection(),
                    "http://maptiles1.finncdn.no/tileService/1.0.3/normaphd/{zoomlevel}/{x}/{y}.png",
                    "http://maptiles2.finncdn.no/tileService/1.0.3/normaphd/{zoomlevel}/{x}/{y}.png",
                    "http://maptiles3.finncdn.no/tileService/1.0.3/normaphd/{zoomlevel}/{x}/{y}.png",
                    "http://maptiles4.finncdn.no/tileService/1.0.3/normaphd/{zoomlevel}/{x}/{y}.png"),
                new TileSourceRecord(false, "", "", "UTTopoLight", "UT Topo Light (Norway)", "UTTopoLight", "",
                    "http://ut.no/", 5, 16, 256, false, false, GetAcceptImageWebHeaderCollection(),
                    "http://a-kartcache.nrk.no/tiles/ut_topo_light/{zoomlevel}/{x}/{y}.jpg",
                    "http://b-kartcache.nrk.no/tiles/ut_topo_light/{zoomlevel}/{x}/{y}.jpg",
                    "http://c-kartcache.nrk.no/tiles/ut_topo_light/{zoomlevel}/{x}/{y}.jpg",
                    "http://d-kartcache.nrk.no/tiles/ut_topo_light/{zoomlevel}/{x}/{y}.jpg"),
                new TileSourceRecord(false, "", "", "UTTopoLightTwo", "UT Topo Light 2 (Norway)", "UTTopoLight", "",
                    "http://ut.no/", 5, 16, 256, false, false, GetAcceptImageWebHeaderCollection(),
                    "https://tilesprod.ut.no/tilestache/ut_topo_light/{zoomlevel}/{x}/{y}.jpg"),

                // this is tricky, it needs a prior request to https://kso.etjanster.lantmateriet.se/ to get two cookies, and the following requests will use those cookies, 
                // and return new cookies (which happen to be the same, at least for a while).
                //new TileSourceRecord(false, "", "", "Lantmateriet", "Lantmateriet (Sweden)", "Lantmateriet", "",
                //    "http://www.lantmateriet.se/", 5, 16, 256, false, false, GetAcceptImageWebHeaderCollection(),
                //    "http://kso.etjanster.lantmateriet.se/karta/topowebb/v1/wmts?SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0&LAYER=topowebb&STYLE=default&TILEMATRIXSET=3006&TILEMATRIX={zoomlevel}&TILEROW={y}&TILECOL={x}&FORMAT=image/png"),
                // "http://kso.etjanster.lantmateriet.se/karta/topowebb/v1/wmts?SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0&LAYER=topowebb&STYLE=default&TILEMATRIXSET=3006&TILEMATRIX=13&TILEROW=14371&TILECOL=13384&FORMAT=image/png"
                // this is tricky, it needs a prior request to get a cookie, and the following request will use those cookies.
                //new TileSourceRecord(false, "", "", "LanskartaSe", "Lanskarta (Sweden)","LanskartaSe", "",
                //    "http://ext-webbgis.lansstyrelsen.se/sverigeslanskarta/proxy/proxy.ashx?http://maps.lantmateriet.se/topowebb/v1/wmts/1.0.0/topowebb/default/3006/{zoomlevel}/{y}/{x}.png",
                //    "http://www.lansstyrelsen.se/", 3, 17, 256, false, false, GetAcceptImageWebHeaderCollection()),
                // referer = "http://ext-webbgis.lansstyrelsen.se/sverigeslanskarta/?visibleLayerNames=L%C3%A4nsstyrelsens%20kontor&zoomLevel=4&x=524106.125&y=6883110.65625"
                // not very reliable
                new TileSourceRecord(false, "", "", "KartatKapsiFiTerrain", "Kartat Kapsi Terrain (FI)","KartatKapsiFiTerrain", "",
                    "http://kartat.kapsi.fi/", 2, 17, 256, false, false, GetAcceptImageWebHeaderCollection(),
                    "http://tiles.kartat.kapsi.fi/peruskartta/{zoomlevel}/{x}/{y}.jpg"),
                new TileSourceRecord(false, "", "", "KartatKapsiFiBackground", "Kartat Kapsi Background (FI)","KartatKapsiFiBackground", "",
                    "http://kartat.kapsi.fi/", 2, 17, 256, false, false, GetAcceptImageWebHeaderCollection(),
                    "http://tiles.kartat.kapsi.fi/taustakartta/{zoomlevel}/{x}/{y}.jpg"),
                // strange projection, https
                //new TileSourceRecord(false, "", "", "Maanmittauslaitos", "Maanmittauslaitos (FI)","Maanmittauslaitos", "",
                //    "https://karttamoottori.maanmittauslaitos.fi/maasto/wmts/1.0.0/maastokartta/default/ETRS-TM35FIN/{zoomlevel}/{y}/{x}.png",
                //    "http://www.maanmittauslaitos.fi/", 2, 15, 256, false, false, GetAcceptImageWebHeaderCollection()),
                new TileSourceRecord(false, "", "", "OrdnanceSurvey", "Ordnance Survey (UK)","OrdnanceSurvey", "",
                    "http://www.ordnancesurvey.co.uk/opendata/viewer/index.html", 7, 17, 256, false, false, GetAcceptImageWebHeaderCollection(),
                    "http://a.os.openstreetmap.org/sv/{zoomlevel}/{x}/{y}.png",
                    "http://b.os.openstreetmap.org/sv/{zoomlevel}/{x}/{y}.png",
                    "http://c.os.openstreetmap.org/sv/{zoomlevel}/{x}/{y}.png"),
                new TileSourceRecord(false, "", "", "UmpPoland", "Ump Poland","UmpPoland", "",
                    "http://ump.waw.pl/", 1, 17, 256, false, false, GetAcceptImageWebHeaderCollection(),
                    "http://1.tiles.ump.waw.pl/ump_tiles/{zoomlevel}/{x}/{y}.png",
                    "http://2.tiles.ump.waw.pl/ump_tiles/{zoomlevel}/{x}/{y}.png",
                    "http://3.tiles.ump.waw.pl/ump_tiles/{zoomlevel}/{x}/{y}.png",
                    "http://4.tiles.ump.waw.pl/ump_tiles/{zoomlevel}/{x}/{y}.png",
                    "http://5.tiles.ump.waw.pl/ump_tiles/{zoomlevel}/{x}/{y}.png",
                    "http://6.tiles.ump.waw.pl/ump_tiles/{zoomlevel}/{x}/{y}.png",
                    "http://7.tiles.ump.waw.pl/ump_tiles/{zoomlevel}/{x}/{y}.png",
                    "http://8.tiles.ump.waw.pl/ump_tiles/{zoomlevel}/{x}/{y}.png",
                    "http://9.tiles.ump.waw.pl/ump_tiles/{zoomlevel}/{x}/{y}.png",
                    "http://10.tiles.ump.waw.pl/ump_tiles/{zoomlevel}/{x}/{y}.png"),
                // not so good anymore
                //new TileSourceRecord(false, "", "", "FreemapSlovakia", "Freemap Slovakia","FreemapSlovakia", "",                
                //    "http://www.freemap.sk/layers/allinone/?/BN/{zoomlevel}/{x}/{y}.png",
                //    "http://www.freemap.sk/", 0, 17, 256, false, false, GetAcceptImageWebHeaderCollection()),
                // a good map of spain is http://sigpac.mapama.gob.es/SDG/raster/MTN25@3857/14.8026.10210.img, for zoom 14 and 15, and you must replace MTN25 with MTN200 for smaller zooms
                new TileSourceRecord(false, "", "", "IgnEs", "Ign (Spain)","IgnEs", "",
                    "http://www.ign.es/", 6, 16, 256, false, false, GetAcceptImageWebHeaderCollection(),
                    "http://www.ign.es/wmts/mapa-raster?layer=MTN&style=default&tilematrixset=GoogleMapsCompatible&Service=WMTS&Request=GetTile&Version=1.0.0&Format=image/jpeg&TileMatrix={zoomlevel}&TileCol={x}&TileRow={y}"),
                // Keeps giving 404, I think they use special coordinates
                //new TileSourceRecord(false, "","",  "Minambiente", "Minambiente (Italy)","Minambiente", "",
                //    "http://www.pcn.minambiente.it/", 4, 18, 512, false, false, GetAcceptImageWebHeaderCollection(),
                //    "http://www.pcn.minambiente.it/arcgis/rest/services/immagini/ortofoto_colore_12/MapServer/tile/{zoomlevel}/{y}/{x}"),
                // http://www.pcn.minambiente.it/arcgis/rest/services/immagini/ortofoto_colore_12/MapServer/tile/13/5657/5621
                // http://www.pcn.minambiente.it/arcgis/rest/services/immagini/ortofoto_colore_12/MapServer/tile/18/37543/38249

            };
            // this is rather useless
            //new TileSourceRecord(false, "", "", "CambLaosThaiViet", "OSM Cambodia Laos Thai Vietnam","CambLaosThaiViet", "",
            //    "http://a.tile.osm-tools.org/osm_then/{zoomlevel}/{x}/{y}.png",
            //    "http://osm-tools.org/", 5, 19, 256, false, false, GetAcceptImageWebHeaderCollection()),
            // this has become very unreliable
            //new TileSourceRecord(false, "", "", "NSWTopo", "LPI NSW Topographic Map (AU)","NSWTopo", "",
            //    "http://maps4.six.nsw.gov.au/arcgis/rest/services/sixmaps/LPI_Imagery_Best/MapServer/tile/{zoomlevel}/{y}/{x}",
            //    "http://www.lpi.nsw.gov.au/", 4, 16, 256, false, false, GetAcceptImageWebHeaderCollection()),

#if NOSTORE
            output.Add(new TileSourceRecord(false, "", "", "MyTopo", "My Topo (N America)", "MyTopo", "",
                "http://www.mytopo.com/", 10, 16, 256, false, false, GetAcceptImageWebHeaderCollection(),
                "http://tileserver.trimbleoutdoors.com/SecureTile/TileHandler.ashx?mapType=Topo&x={x}&y={y}&z={zoomlevel}"));

#endif

#if NOSTORE
            // also try http://server.arcgisonline.com/ArcGIS/rest/services/World_Topo_Map/MapServer/tile/{zoomlevel}/{y}/{x}
            output.Add(new TileSourceRecord(false, "", "", "ArcGIS", "ArcGIS World Topo Map", "ArcGIS", "",
                "", 0, 16, 256, false, false, GetAcceptImageWebHeaderCollection(),
                "http://services.arcgisonline.com/ArcGIS/rest/services/World_Topo_Map/MapServer/tile/{zoomlevel}/{y}/{x}"));
#endif
            // This one has its own coordinates. y=0 is south, y=max is north. Zoom 0 accepts 0<=y<=8 and 0<=x<=7.
            // Zoom 1 accepts y max = 16, x max = 15
            // https://www.mapplus.ch/ts1/1.0.0/osm_schweiz_topo_2016/2/31/33.png
            // https://www.mapplus.ch/ts1/1.0.0/osm_schweiz_topo_2016/3/63/67.png
            // Max zoom is 9
            // The top coordinates are https://www.mapplus.ch/ts1/1.0.0/osm_schweiz_topo_2016/9/4049/4295.png
            // lon and lat for x = y = 0 are 3.49378 E, 42.69590 N (SW)
            // lon and lat for x = y = max are 14.53994 E, 50.45367 N (NE)
            // The first request is like https://www.mapplus.ch/?lang=en&basemap=osm&blop=1
            // and it returns an html body and some headers, among which a cookie like name: {PHPSESSID; value: vfhohrdc9ni59tjbb4e1g1pju1; domain: www.mapplus.ch}
            // The tile requests are like https://www.mapplus.ch/ts1/1.0.0/osm_schweiz_topo_2016/9/1785/2347.png
            // https://www.mapplus.ch/ts1/1.0.0/osm_schweiz_topo_2016/9/2185/1673.png
            // with extra headers like {Referer: https://www.mapplus.ch; Cookie: PHPSESSID=vfhohrdc9ni59tjbb4e1g1pju1}
            //output.Add(new TileSourceRecord(false, "", "", "OSMSchweizTopo", "OSM Schweiz Topo (CH)", "OSMSchweizTopo", "",
            //    "http://www.mapplus.ch", 7, 16, 256, false, false, GetAcceptImageWebHeaderCollection(),
            //    "http://www.mapplus.ch/ts1/1.0.0/osm_schweiz_topo_2016/{zoomlevel}/{x}/{y}.png"));

#if NOSTORE
            // there is also this:
            // https://mapproxy.retorte.ch/service?LAYERS=swisstopo&FORMAT=image/jpeg&SRS=EPSG:4326&transparent=true&SERVICE=WMS&VERSION=1.1.1&REQUEST=GetMap&STYLES=&BBOX=7.9376220703125,46.53619267489863,7.943115234375,46.53997127029103&WIDTH=256&HEIGHT=256
            // you get it from https://tools.retorte.ch/map/?swissgrid=2660300,1185186&zoom=16&map=swisstopo 
            // and it needs a referer like https://tools.retorte.ch/map/?swissgrid=2660300,1185186&zoom=16&map=swisstopo
            // very far south:
            // https://wmts103.geo.admin.ch/1.0.0/ch.swisstopo.landeskarte-farbe-10/default/current/21781/26/2146/2363.png
            // very far north:
            // https://wmts108.geo.admin.ch/1.0.0/ch.swisstopo.landeskarte-farbe-10/default/current/21781/26/423/2066.png
            // very far west:
            // https://wmts108.geo.admin.ch/1.0.0/ch.swisstopo.landeskarte-farbe-10/default/current/21781/26/1877/511.png
            // very far east:
            // https://wmts107.geo.admin.ch/1.0.0/ch.swisstopo.landeskarte-farbe-10/default/current/21781/26/1428/3233.png
            // it goes to 27, but it seems rather useless

            // the x = 0 of the leftmost tile is at ~5°5'E (~ Dijon)
            // the top of the topmost tile is at ~48°18'N (~St Dié de Vosges)
            // the bottom of the bottommost tile is at ~45°25'N (~Corsico)
            // the right of the rightmost tile is at ~11°20'E (~Bolzano)
            // https://wmts102.geo.admin.ch/1.0.0/ch.swisstopo.landeskarte-farbe-10/default/current/21781/16/3/1.png
            // https://wmts105.geo.admin.ch/1.0.0/ch.swisstopo.landeskarte-farbe-10/default/current/21781/19/18/43.png
            // https://wmts108.geo.admin.ch/1.0.0/ch.swisstopo.swisstlm3d-karte-farbe/default/current/21781/21/81/185.png
            // also try http://wmts109.geo.admin.ch/1.0.0/ch.swisstopo.pixelkarte-farbe/default/current/21781/18/12/18.jpeg
            // with Referer = "https://map.schweizmobil.ch/" in the header.
            // Occhio che ha le sue coordinate particolari! https://github.com/ValentinMinder/Swisstopo-WGS84-LV03 è un convertitore.
            //output.Add(new TileSourceRecord(false, "", "", "Schweizmobil", "Schweizmobil (CH)", "Swisstopo", "",
            //    "", 4, 16, 256, false, false, GetSchweizmobilWebHeaderCollection(),
            //    "http://wmts100.geo.admin.ch/1.0.0/ch.swisstopo.pixelkarte-farbe/default/current/21781/{zoomlevel}/{y}/{x}.jpeg",
            //    "http://wmts101.geo.admin.ch/1.0.0/ch.swisstopo.pixelkarte-farbe/default/current/21781/{zoomlevel}/{y}/{x}.jpeg",
            //    "http://wmts102.geo.admin.ch/1.0.0/ch.swisstopo.pixelkarte-farbe/default/current/21781/{zoomlevel}/{y}/{x}.jpeg",
            //    "http://wmts103.geo.admin.ch/1.0.0/ch.swisstopo.pixelkarte-farbe/default/current/21781/{zoomlevel}/{y}/{x}.jpeg",
            //    "http://wmts104.geo.admin.ch/1.0.0/ch.swisstopo.pixelkarte-farbe/default/current/21781/{zoomlevel}/{y}/{x}.jpeg",
            //    "http://wmts105.geo.admin.ch/1.0.0/ch.swisstopo.pixelkarte-farbe/default/current/21781/{zoomlevel}/{y}/{x}.jpeg",
            //    "http://wmts106.geo.admin.ch/1.0.0/ch.swisstopo.pixelkarte-farbe/default/current/21781/{zoomlevel}/{y}/{x}.jpeg",
            //    "http://wmts107.geo.admin.ch/1.0.0/ch.swisstopo.pixelkarte-farbe/default/current/21781/{zoomlevel}/{y}/{x}.jpeg",
            //    "http://wmts108.geo.admin.ch/1.0.0/ch.swisstopo.pixelkarte-farbe/default/current/21781/{zoomlevel}/{y}/{x}.jpeg",
            //    "http://wmts109.geo.admin.ch/1.0.0/ch.swisstopo.pixelkarte-farbe/default/current/21781/{zoomlevel}/{y}/{x}.jpeg"));
#endif
#if NOSTORE
            output.Add(new TileSourceRecord(false, "", "", "Swisstopo", "Swisstopo (CH)", "Swisstopo", "",
                "", 7, 16, 256, false, false, GetDefaultWebHeaderCollection(),
                "http://mpa1.mapplus.ch/swisstopo/{zoomlevel}/{x}/{y}.jpg",
                "http://mpa2.mapplus.ch/swisstopo/{zoomlevel}/{x}/{y}.jpg",
                "http://mpa3.mapplus.ch/swisstopo/{zoomlevel}/{x}/{y}.jpg",
                "http://mpa4.mapplus.ch/swisstopo/{zoomlevel}/{x}/{y}.jpg"));
#endif

#if NOSTORE
            output.Add(new TileSourceRecord(false, "", "", "CalTopo", "CalTopo (USA)", "CalTopo", "",
                "http://caltopo.com", 5, 18, 256, false, false, GetAcceptImageWebHeaderCollection(),
                "http://caltopo.com/resource/imagery/mapbuilder/cs-60-40-c21BB6100-h22-a21-r22-t22d-m21-p21/{zoomlevel}/{x}/{y}.png"));
#endif

#if NOSTORE
            output.Add(new TileSourceRecord(false, "", "", "CalTopo2", "CalTopo 2 (USA)", "CalTopo2", "",
                "", 5, 16, 256, false, false, GetAcceptImageWebHeaderCollection(),
                "http://s3-us-west-1.amazonaws.com/caltopo/topo/{zoomlevel}/{x}/{y}.png?v=1"));
#endif

#if NOSTORE
            // referer https://viewer.nationalmap.gov/viewer/
            output.Add(new TileSourceRecord(false, "", "", "USGSTopo", "USGSTopo (USA)", "USGSTopo", "",
                "", 3, 15, 256, false, false, GetAcceptImageWebHeaderCollection(),
                "https://basemap.nationalmap.gov/arcgis/rest/services/USGSTopo/MapServer/tile/{zoomlevel}/{y}/{x}"));
#endif

            // geoportail fr ? http://depot.ign.fr/geoportail/api/develop/tech-docs-js/developpeur/wmts.html
            // to get a key: http://professionnels.ign.fr/ign/contrats
            // or study https://www.geoportail.gouv.fr/carte
            return output;
        }
        public static TileSourceRecord GetDefaultTileSource()
        {
            return new TileSourceRecord(false, "", "", DefaultTileSourceTechName, DefaultTileSourceDisplayName, "", "", DefaultTileSourceProviderUriString, MinMinZoom, MaxMaxZoom, DefaultTilePixelSize, false, false, GetDefaultWebHeaderCollection(), DefaultTileSourceUriString);
        }
        public static List<TileSourceRecord> GetDefaultTileSourceList()
        {
            var result = new List<TileSourceRecord>();
            result.Add(GetDefaultTileSource());
            return result;
        }
        public static TileSourceRecord GetAllTileSource()
        {
            return new TileSourceRecord(false, "", "", AllTileSourceTechName, AllTileSourceDisplayName, "", "", DummyTileSourceProviderUriString, DummyTileSourceMinZoom, DummyTileSourceMaxZoom, DummyTileSourceTilePixelSize, false, false, GetDefaultWebHeaderCollection(), DummyTileSourceUriString);
        }
        public static TileSourceRecord GetNoTileSource()
        {
            return new TileSourceRecord(false, "", "", NoTileSourceTechName, NoTileSourceDisplayName, "", "", DummyTileSourceProviderUriString, DummyTileSourceMinZoom, DummyTileSourceMaxZoom, DummyTileSourceTilePixelSize, false, false, GetDefaultWebHeaderCollection(), DummyTileSourceUriString);
        }

        public void ApplyHeadersToWebRequest(WebRequest request)
        {
            var rhs = RequestHeaders;
            if (request == null || rhs == null) return;

            foreach (var item in rhs)
            {
                HttpRequestHeader headerKey;
                if (Enum.TryParse(item.Key, out headerKey))
                {
                    request.Headers[headerKey] = item.Value;
                }
            }
        }
    }

    [DataContract]
    [KnownType(typeof(ReadOnlyCollection<string>))]
    public class WritableTileSourceRecord : TileSourceRecord
    {
        public new string TechName { get { return _techName; } set { _techName = value; RaisePropertyChanged_UI(); } }

        public new string FolderName { get { return _folderName; } set { _folderName = value; RaisePropertyChanged_UI(); } }

        public new string DisplayName { get { return _displayName; } set { _displayName = value; RaisePropertyChanged_UI(); } }

        public new string CopyrightNotice { get { return _copyrightNotice; } set { _copyrightNotice = value; RaisePropertyChanged_UI(); } }

        public new IReadOnlyList<string> UriStrings { get { return _uriStrings; } set { _uriStrings = value; RaisePropertyChanged_UI(); } }

        public new string ProviderUriString { get { return _providerUriString; } set { _providerUriString = value; RaisePropertyChanged_UI(); } }

        public new int MinZoom { get { return _minZoom; } set { _minZoom = value; RaisePropertyChanged_UI(); } }

        public new int MaxZoom { get { return _maxZoom; } set { _maxZoom = value; RaisePropertyChanged_UI(); } }

        public new int TilePixelSize { get { return _tilePixelSize; } set { _tilePixelSize = value; RaisePropertyChanged_UI(); } }

        public new bool IsDeletable { get { return _isDeletable; } set { _isDeletable = value; RaisePropertyChanged_UI(); } }

        public new bool IsFileSource { get { return _isFileSource; } set { _isFileSource = value; RaisePropertyChanged_UI(); } }

        public new string TileSourceFolderPath { get { return _tileSourceFolderPath; } set { _tileSourceFolderPath = value; RaisePropertyChanged_UI(); } }

        public new string TileSourceFileName { get { return _tileSourceFileName; } set { _tileSourceFileName = value; RaisePropertyChanged_UI(); } }

        public new Dictionary<string, string> RequestHeaders { get { return _requestHeaders; } set { _requestHeaders = value; RaisePropertyChanged_UI(); } }

        public new bool IsOverlay { get { return _isOverlay; } set { _isOverlay = value; RaisePropertyChanged_UI(); } }

        public WritableTileSourceRecord(bool isFileSource, string tileSourceFolderPath, string tileSourceFileName,
            string techName, string displayName, string folderName, string copyrightNotice,
            string providerUri, int minZoom, int maxZoom, int tilePixelSize,
            bool isDeletable, bool isOverlay,
            Dictionary<string, string> headers, params string[] uriStrings) : base(isFileSource, tileSourceFolderPath, tileSourceFileName,
                techName, displayName, folderName, copyrightNotice,
                providerUri, minZoom, maxZoom, tilePixelSize,
                isDeletable, isOverlay,
                headers, uriStrings)
        { }

        public static WritableTileSourceRecord Clone(WritableTileSourceRecord source)
        {
            if (source == null) return null;

            return new WritableTileSourceRecord(source._isFileSource, source._tileSourceFolderPath, source._tileSourceFileName,
                source._techName, source._displayName, source._folderName, source._copyrightNotice,
                source._providerUriString, source._minZoom, source._maxZoom, source._tilePixelSize,
                source._isDeletable, source._isOverlay,
                // LOLLO TODO check that the clones below really clone
                new Dictionary<string, string>(source._requestHeaders), source._uriStrings.ToArray());
        }
        public new static WritableTileSourceRecord Clone(TileSourceRecord source)
        {
            if (source == null) return null;

            return new WritableTileSourceRecord(source.IsFileSource, source.TileSourceFolderPath, source.TileSourceFileName,
                source.TechName, source.DisplayName, source.FolderName, source.CopyrightNotice,
                source.ProviderUriString, source.MinZoom, source.MaxZoom, source.TilePixelSize,
                source.IsDeletable, source.IsOverlay,
                // LOLLO TODO check that the clones below really clone
                new Dictionary<string, string>(source.RequestHeaders), source.UriStrings.ToArray());
        }

        public static WritableTileSourceRecord GetSampleTileSource()
        {
            return new WritableTileSourceRecord(false, "", "MyTile_{zoomlevel}_{x}_{y}.png", SampleTileSourceTechName, SampleTileSourceTechName, "", "", DefaultTileSourceProviderUriString, MinMinZoom, SampleMaxZoom, DefaultTilePixelSize, false, false, GetDefaultWebHeaderCollection(), SampleUriString);
        }
    }
}