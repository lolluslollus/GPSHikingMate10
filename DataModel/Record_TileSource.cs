using LolloGPS.Data.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using Utilz.Data;

// LOLLO NOTE https://dzjow.com/2012/04/12/free-online-topographic-maps-for-hiking/
// http://onlinetopomaps.net/
// http://www.oruxmaps.com/foro/viewtopic.php?t=3035

namespace LolloGPS.Data
{
    [DataContract]
    [KnownType(typeof(IReadOnlyList<string>))]
    [KnownType(typeof(string[]))]
    public sealed class TileSourceRecord : ObservableData
    {
        private const string DefaultTileSourceTechName = "Nokia";
        private const string AllTileSourceTechName = "All";
        private const string NoTileSourceTechName = "None";
        private const string SampleTileSourceTechName = "YourOwnTileSource";

        private const string DefaultTileSourceDisplayName = "Built in";
        private const string AllTileSourceDisplayName = "All";
        private const string NoTileSourceDisplayName = "None";

        private static readonly string[] DefaultTileSourceUriString = { };
        private static readonly string DefaultTileSourceProviderUriString = string.Empty;
        private static readonly string[] DummyTileSourceUriString = { };
        private static readonly string DummyTileSourceProviderUriString = string.Empty;
        private static readonly string[] SampleUriString = { $"http://tileserver.something/{ZoomLevelPlaceholder}/{XPlaceholder}/{YPlaceholder}.png" };

        public const int MinMinZoom = 0;
        private const int DummyTileSourceMinZoom = 0;

        public const int MaxMaxZoom = 20;
        private const int DummyTileSourceMaxZoom = 0;
        private const int SampleMaxZoom = 16;

        private const int DefaultTilePixelSize = 256;
        private const int DummyTileSourceTilePixelSize = 0;

        public const string ZoomLevelPlaceholder = "{zoomlevel}";
        public const string XPlaceholder = "{x}";
        public const string YPlaceholder = "{y}";
        public const string ZoomLevelPlaceholder_Internal = "{0}";
        public const string XPlaceholder_Internal = "{1}";
        public const string YPlaceholder_Internal = "{2}";

        public const int MinTilePixelSize = 64;
        public const int MaxTilePixelSize = 1024;
        public const int MaxTechNameLength = 25;

        private static Dictionary<string, string> GetDefaultWebHeaderCollection()
        {
            return new Dictionary<string, string>();
        }
        private static Dictionary<string, string> GetAcceptImageWebHeaderCollection()
        {
            var headers = new Dictionary<string, string>();
            headers[Enum.GetName(typeof(HttpRequestHeader), HttpRequestHeader.Accept)] = TileCache.TileCacheReaderWriter.MimeTypeImageAny;
            return headers;
        }

        private string _techName = DefaultTileSourceTechName;
        [DataMember]
        public string TechName { get { return _techName; } set { _techName = value; RaisePropertyChanged(); } }

        private string _folderName = DefaultTileSourceTechName;
        [DataMember]
        public string FolderName { get { return _folderName; } set { _folderName = value; RaisePropertyChanged(); } }

        private string _displayName = DefaultTileSourceDisplayName;
        [DataMember]
        public string DisplayName { get { return _displayName; } set { _displayName = value; RaisePropertyChanged(); } }

        private string _copyrightNotice = DefaultTileSourceDisplayName;
        [DataMember]
        public string CopyrightNotice { get { return _copyrightNotice; } set { _copyrightNotice = value; RaisePropertyChanged(); } }

        private IReadOnlyList<string> _uriStrings = SampleUriString;
        [DataMember]
        public IReadOnlyList<string> UriStrings { get { return _uriStrings; } set { _uriStrings = value; RaisePropertyChanged(); } }

        private string _providerUriString = string.Empty;
        [DataMember]
        public string ProviderUriString { get { return _providerUriString; } set { _providerUriString = value; RaisePropertyChanged(); } }

        private int _minZoom = MinMinZoom;
        [DataMember]
        public int MinZoom { get { return _minZoom; } set { _minZoom = value; RaisePropertyChanged(); } }

        private int _maxZoom = MaxMaxZoom;
        [DataMember]
        public int MaxZoom { get { return _maxZoom; } set { _maxZoom = value; RaisePropertyChanged(); } }

        private int _tilePixelSize = DefaultTilePixelSize;
        [DataMember]
        public int TilePixelSize { get { return _tilePixelSize; } set { _tilePixelSize = value; RaisePropertyChanged(); } }

        private bool _isDeletable = false;
        [DataMember]
        public bool IsDeletable { get { return _isDeletable; } set { _isDeletable = value; RaisePropertyChanged(); } }

        // WebHeaderCollection cannot be serialised and deserialised, so we use a dictionary
        private Dictionary<string, string> _requestHeaders = new Dictionary<string, string>();
        [DataMember]
        public Dictionary<string, string> RequestHeaders { get { return _requestHeaders; } set { _requestHeaders = value; RaisePropertyChanged(); } }

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

        public TileSourceRecord(string techName, string displayName, string folderName, string copyrightNotice, string providerUri, int minZoom, int maxZoom, int tilePixelSize, bool isDeletable, Dictionary<string, string> headers, params string[] uriString)
        {
            TechName = techName;
            FolderName = folderName;
            DisplayName = displayName;
            CopyrightNotice = copyrightNotice;
            ProviderUriString = providerUri;
            MinZoom = minZoom;
            MaxZoom = maxZoom;
            TilePixelSize = tilePixelSize;
            IsDeletable = isDeletable;
            RequestHeaders = headers;
            UriStrings = uriString;
        }

        #region checks
        public string Check()
        {
            string errorMsg = string.Empty;
            errorMsg = CheckTechName(TechName);
            if (!string.IsNullOrEmpty(errorMsg)) return errorMsg;
            errorMsg = CheckDisplayName(DisplayName); // not sure we want to ask for a display name
            if (!string.IsNullOrEmpty(errorMsg)) return errorMsg;
            errorMsg = CheckUri(UriStrings, false);
            if (!string.IsNullOrEmpty(errorMsg)) return errorMsg;
            errorMsg = CheckUri(ProviderUriString, true);
            if (!string.IsNullOrEmpty(errorMsg)) return errorMsg;
            errorMsg = CheckMinMaxZoom(MinZoom, MaxZoom);
            if (!string.IsNullOrEmpty(errorMsg)) return errorMsg;
            errorMsg = CheckTilePixelSize(TilePixelSize);
            if (!string.IsNullOrEmpty(errorMsg)) return errorMsg;

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
        private static string CheckUri(IReadOnlyList<string> uris, bool isEmptyAllowed)
        {
            if (uris == null || uris.Count == 0)
            {
                if (isEmptyAllowed) return string.Empty;
                else return "Uri is empty";
            }
            foreach (var item in uris)
            {
                var result = CheckUri(item, isEmptyAllowed);
                if (!string.IsNullOrWhiteSpace(result)) return result;
            }
            return string.Empty;
        }
        private static string CheckUri(string uri, bool isEmptyAllowed)
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

            // check if valid uri
            string sTestUri = string.Empty;
            try
            {
                string testUriFormat = uri.Replace(ZoomLevelPlaceholder, ZoomLevelPlaceholder_Internal);
                testUriFormat = testUriFormat.Replace(XPlaceholder, XPlaceholder_Internal);
                testUriFormat = testUriFormat.Replace(YPlaceholder, YPlaceholder_Internal);

                sTestUri = string.Format(testUriFormat, 0, 0, 0);

                bool isWellFormed = Uri.IsWellFormedUriString(sTestUri, UriKind.Absolute);
                if (!isWellFormed) return "Uri format is invalid";

                var builder = new UriBuilder(sTestUri);
            }
            catch (Exception)
            {
                return "Uri format is invalid";
            }
            if (RuntimeData.GetInstance().IsConnectionAvailable)
            {
                try
                {
                    var request = WebRequest.CreateHttp(sTestUri);
                    request.AllowReadStreamBuffering = true;
                    request.ContinueTimeout = TileCache.TileCacheReaderWriter.WebRequestTimeoutMsec;
                }
                catch (Exception ex)
                {
                    return ex.Message;
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

        public static void Clone(TileSourceRecord source, ref TileSourceRecord target)
        {
            if (source == null) return;

            if (target == null) target = GetDefaultTileSource();

            target.TechName = source._techName;
            target.FolderName = source._folderName;
            target.DisplayName = source._displayName;
            target.CopyrightNotice = source._copyrightNotice;
            target.UriStrings = source._uriStrings;
            target.ProviderUriString = source._providerUriString;
            target.MinZoom = source._minZoom;
            target.MaxZoom = source._maxZoom;
            target.TilePixelSize = source._tilePixelSize;
            target.IsDeletable = source._isDeletable;
            target.RequestHeaders = new Dictionary<string, string>(source._requestHeaders);
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
                && comp._requestHeaders.OrderBy(kvp => kvp.Key).SequenceEqual(_requestHeaders.OrderBy(kvp => kvp.Key));
        }
        public static List<TileSourceRecord> GetDefaultTileSources()
        {
            var output = new List<TileSourceRecord>
            {
                new TileSourceRecord(DefaultTileSourceTechName, DefaultTileSourceDisplayName, "", "",
                    DefaultTileSourceProviderUriString, MinMinZoom, MaxMaxZoom, DefaultTilePixelSize, false, GetDefaultWebHeaderCollection()
                    , DefaultTileSourceUriString),
                new TileSourceRecord("ForUMaps", "4UMaps", "ForUMaps", "4UMaps.eu",
                    "http://www.4umaps.eu/", 2, 15, 256, false, GetAcceptImageWebHeaderCollection(), "http://4umaps.eu/{zoomlevel}/{x}/{y}.png"),
                new TileSourceRecord("OpenTopoMap", "OpenTopoMap", "OpenTopoMap", "OpenTopoMap and OpenStreetMap",
                    "http://opentopomap.org/", 2, 16, 256, false, GetAcceptImageWebHeaderCollection(),
                    "http://a.tile.opentopomap.org/{zoomlevel}/{x}/{y}.png",
                    "http://b.tile.opentopomap.org/{zoomlevel}/{x}/{y}.png",
                    "http://c.tile.opentopomap.org/{zoomlevel}/{x}/{y}.png"),
                new TileSourceRecord("OpenTopoMapTwo", "OpenTopoMap Two", "OpenTopoMap", "OpenTopoMap and OpenStreetMap",
                    "http://opentopomap.org/", 2, 16, 256, false, GetAcceptImageWebHeaderCollection(),
                    "http://opentopomap.org/{zoomlevel}/{x}/{y}.png"),
                new TileSourceRecord("OpenStreetMap", "OpenStreetMap", "OpenStreetMap", "",
                    "http://www.openstreetmap.org/", 0, 18, 256, false, GetAcceptImageWebHeaderCollection(),
                    "http://a.tile.openstreetmap.org/{zoomlevel}/{x}/{y}.png",
                    "http://b.tile.openstreetmap.org/{zoomlevel}/{x}/{y}.png",
                    "http://c.tile.openstreetmap.org/{zoomlevel}/{x}/{y}.png"),
                new TileSourceRecord("OpenStreetMapBW", "OpenStreetMap BW", "OpenStreetMapBW", "OpenStreetMap",
                    "http://www.openstreetmap.org/", 0, 18, 256, false, GetAcceptImageWebHeaderCollection(),
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
                new TileSourceRecord("OpenStreetMapHum", "OpenStreetMap Humanitarian","OpenStreetMapHum", "OpenStreetMap",
                    "http://www.openstreetmap.org/", 0, 18, 256, false, GetAcceptImageWebHeaderCollection(),
                    "http://tile-a.openstreetmap.fr/hot/{zoomlevel}/{x}/{y}.png",
                    "http://tile-b.openstreetmap.fr/hot/{zoomlevel}/{x}/{y}.png",
                    "http://tile-c.openstreetmap.fr/hot/{zoomlevel}/{x}/{y}.png"),
                new TileSourceRecord("OpenBusMap", "OpenBusMap","OpenBusMap", "",
                    "http://openbusmap.org/", 3, 18, 256, false, GetAcceptImageWebHeaderCollection(),
                    "http://tileserver.memomaps.de/tilegen/{zoomlevel}/{x}/{y}.png"),
                new TileSourceRecord("OpenSeaMap", "OpenSeaMap","OpenSeaMap", "",
                    "http://openseamap.org/?L=1", 9, 18, 256, false, GetAcceptImageWebHeaderCollection(),
                    "http://tiles.openseamap.org/seamark/{zoomlevel}/{x}/{y}.png"),
				//new TileSourceRecord("MapQuestOSM", "MapQuest OSM", // no more provided since mid 2016
				//	"http://otile1.mqcdn.com/tiles/1.0.0/osm/{zoomlevel}/{x}/{y}.png", 
				//  "http://www.mapquest.com/", 0, 18, 256, false, GetAcceptImageWebHeaderCollection()),
				new TileSourceRecord("HikeBike", "Hike & Bike Map","HikeBike", "",
                    "http://hikebikemap.org/", 0, 16, 256, false, GetAcceptImageWebHeaderCollection(),
                    "http://a.tiles.wmflabs.org/hikebike/{zoomlevel}/{x}/{y}.png",
                    "http://b.tiles.wmflabs.org/hikebike/{zoomlevel}/{x}/{y}.png",
                    "http://c.tiles.wmflabs.org/hikebike/{zoomlevel}/{x}/{y}.png"),
                new TileSourceRecord("ThunderforestLandscape", "Thunderforest Landscape","ThunderforestLandscape", "",
                    "http://www.thunderforest.com/", 2, 18, 256, false, GetAcceptImageWebHeaderCollection(),
                    "http://a.tile.thunderforest.com/landscape/{zoomlevel}/{x}/{y}.png",
                    "http://b.tile.thunderforest.com/landscape/{zoomlevel}/{x}/{y}.png",
                    "http://c.tile.thunderforest.com/landscape/{zoomlevel}/{x}/{y}.png"),
                new TileSourceRecord("ThunderforestCycle", "Thunderforest Cycle","ThunderforestCycle", "",
                    "http://www.thunderforest.com/", 2, 18, 256, false, GetAcceptImageWebHeaderCollection(),
                    "http://a.tile.thunderforest.com/cycle/{zoomlevel}/{x}/{y}.png",
                    "http://b.tile.thunderforest.com/cycle/{zoomlevel}/{x}/{y}.png",
                    "http://c.tile.thunderforest.com/cycle/{zoomlevel}/{x}/{y}.png"),
                new TileSourceRecord("ThunderforestOutdoors", "Thunderforest Outdoors","ThunderforestOutdoors", "",
                    "http://www.thunderforest.com/", 2, 18, 256, false, GetAcceptImageWebHeaderCollection(),
                    "http://a.tile.thunderforest.com/outdoors/{zoomlevel}/{x}/{y}.png",
                    "http://b.tile.thunderforest.com/outdoors/{zoomlevel}/{x}/{y}.png",
                    "http://c.tile.thunderforest.com/outdoors/{zoomlevel}/{x}/{y}.png"),
                new TileSourceRecord("ThunderforestTransport", "Thunderforest Transport","ThunderforestTransport", "",
                    "http://www.thunderforest.com/", 2, 18, 256, false, GetAcceptImageWebHeaderCollection(),
                    "http://a.tile.thunderforest.com/transport/{zoomlevel}/{x}/{y}.png",
                    "http://b.tile.thunderforest.com/transport/{zoomlevel}/{x}/{y}.png",
                    "http://c.tile.thunderforest.com/transport/{zoomlevel}/{x}/{y}.png"),
                // not very useful
                //new TileSourceRecord("StamenToner", "Stamen Toner","StamenToner", "",
                //    "http://maps.stamen.com/", 2, 18, 256, false, GetAcceptImageWebHeaderCollection(),
                //    "http://tile.stamen.com/toner/{zoomlevel}/{x}/{y}.png"),
                // not very useful
                //new TileSourceRecord("StamenTerrain", "Stamen Terrain","StamenTerrain", "",
                //    "http://tile.stamen.com/terrain/{zoomlevel}/{x}/{y}.jpg",
                //    "http://maps.stamen.com/", 5, 18, 256, false, GetAcceptImageWebHeaderCollection()),
                // this one has funny coordinates
                //new TileSourceRecord("Eniro", "Eniro (Scandinavia)",
                //    "https://map02.eniro.no/geowebcache/service/tms1.0.0/map/{zoomlevel}/{x}/{y}.png",
                //    "https://kartor.eniro.se/", 3, 17, 256, false, GetAcceptImageWebHeaderCollection()),
                // this one has funny coordinates
                //new TileSourceRecord("NLSIceland", "National Land Survey (Iceland)",
                //    "https://gis.lmi.is/mapcache/wmts/1.0.0/LMI_Kort/default/EPSG3057/{zoomlevel}/{x}/{y}.png",
                //    "http://kortasja.lmi.is/en/", 5, 15, 256, false, GetAcceptImageWebHeaderCollection()),
                new TileSourceRecord("KartFinnNo", "Kart Finn Norway","KartFinnNo", "",
                    "http://kart.finn.no/", 4, 20, 256, false, GetAcceptImageWebHeaderCollection(),
                    "http://maptiles1.finncdn.no/tileService/1.0.3/normap/{zoomlevel}/{x}/{y}.png",
                    "http://maptiles2.finncdn.no/tileService/1.0.3/normap/{zoomlevel}/{x}/{y}.png",
                    "http://maptiles3.finncdn.no/tileService/1.0.3/normap/{zoomlevel}/{x}/{y}.png",
                    "http://maptiles4.finncdn.no/tileService/1.0.3/normap/{zoomlevel}/{x}/{y}.png"),
                new TileSourceRecord("KartFinnNoHd", "Kart Finn Norway HD", "KartFinnNoHd", "",
                    "http://kart.finn.no/", 4, 20, 512, false, GetAcceptImageWebHeaderCollection(),
                    "http://maptiles1.finncdn.no/tileService/1.0.3/normaphd/{zoomlevel}/{x}/{y}.png",
                    "http://maptiles2.finncdn.no/tileService/1.0.3/normaphd/{zoomlevel}/{x}/{y}.png",
                    "http://maptiles3.finncdn.no/tileService/1.0.3/normaphd/{zoomlevel}/{x}/{y}.png",
                    "http://maptiles4.finncdn.no/tileService/1.0.3/normaphd/{zoomlevel}/{x}/{y}.png"),
                new TileSourceRecord("UTTopoLight", "UT Topo Light (Norway)", "UTTopoLight", "",
                    "http://ut.no/", 5, 16, 256, false, GetAcceptImageWebHeaderCollection(),
                    "http://a-kartcache.nrk.no/tiles/ut_topo_light/{zoomlevel}/{x}/{y}.jpg",
                    "http://b-kartcache.nrk.no/tiles/ut_topo_light/{zoomlevel}/{x}/{y}.jpg",
                    "http://c-kartcache.nrk.no/tiles/ut_topo_light/{zoomlevel}/{x}/{y}.jpg",
                    "http://d-kartcache.nrk.no/tiles/ut_topo_light/{zoomlevel}/{x}/{y}.jpg"),
                new TileSourceRecord("UTTopoLightTwo", "UT Topo Light 2 (Norway)", "UTTopoLight", "",
                    "http://ut.no/", 5, 16, 256, false, GetAcceptImageWebHeaderCollection(),
                    "https://tilesprod.ut.no/tilestache/ut_topo_light/{zoomlevel}/{x}/{y}.jpg"),

                //new TileSourceRecord("Lantmateriet", "Lantmateriet (Sweden)", "Lantmateriet", "",
                //    "http://www.lantmateriet.se/", 5, 16, 256, false, GetAcceptImageWebHeaderCollection(),
                //    "http://kso.etjanster.lantmateriet.se/karta/topowebb/v1/wmts?SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0&LAYER=topowebb&STYLE=default&TILEMATRIXSET=3006&TILEMATRIX={zoomlevel}&TILEROW={y}&TILECOL={x}&FORMAT=image/png"),
                // "http://kso.etjanster.lantmateriet.se/karta/topowebb/v1/wmts?SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0&LAYER=topowebb&STYLE=default&TILEMATRIXSET=3006&TILEMATRIX=13&TILEROW=14371&TILECOL=13384&FORMAT=image/png"
                // this is tricky, maybe because it returns HttpRequestHeader.TransferEncoding = chunked (but that should be all right), 
                // maybe because it makes a call within the call and it needs special headers, cookies etc.
                //new TileSourceRecord("LanskartaSe", "Lanskarta (Sweden)","LanskartaSe", "",
                //    "http://ext-webbgis.lansstyrelsen.se/sverigeslanskarta/proxy/proxy.ashx?http://maps.lantmateriet.se/topowebb/v1/wmts/1.0.0/topowebb/default/3006/{zoomlevel}/{y}/{x}.png",
                //    "http://www.lansstyrelsen.se/", 3, 17, 256, false, GetAcceptImageWebHeaderCollection()),
                // referer = "http://ext-webbgis.lansstyrelsen.se/sverigeslanskarta/?visibleLayerNames=L%C3%A4nsstyrelsens%20kontor&zoomLevel=4&x=524106.125&y=6883110.65625"
                // not very reliable
                new TileSourceRecord("KartatKapsiFiTerrain", "Kartat Kapsi Terrain (FI)","KartatKapsiFiTerrain", "",
                    "http://kartat.kapsi.fi/", 2, 17, 256, false, GetAcceptImageWebHeaderCollection(),
                    "http://tiles.kartat.kapsi.fi/peruskartta/{zoomlevel}/{x}/{y}.jpg"),
                new TileSourceRecord("KartatKapsiFiBackground", "Kartat Kapsi Background (FI)","KartatKapsiFiBackground", "",
                    "http://kartat.kapsi.fi/", 2, 17, 256, false, GetAcceptImageWebHeaderCollection(),
                    "http://tiles.kartat.kapsi.fi/taustakartta/{zoomlevel}/{x}/{y}.jpg"),
                // strange projection, https
                //new TileSourceRecord("Maanmittauslaitos", "Maanmittauslaitos (FI)","Maanmittauslaitos", "",
                //    "https://karttamoottori.maanmittauslaitos.fi/maasto/wmts/1.0.0/maastokartta/default/ETRS-TM35FIN/{zoomlevel}/{y}/{x}.png",
                //    "http://www.maanmittauslaitos.fi/", 2, 15, 256, false, GetAcceptImageWebHeaderCollection()),
                new TileSourceRecord("OrdnanceSurvey", "Ordnance Survey (UK)","OrdnanceSurvey", "",
                    "http://www.ordnancesurvey.co.uk/opendata/viewer/index.html", 7, 17, 256, false, GetAcceptImageWebHeaderCollection(),
                    "http://a.os.openstreetmap.org/sv/{zoomlevel}/{x}/{y}.png",
                    "http://b.os.openstreetmap.org/sv/{zoomlevel}/{x}/{y}.png",
                    "http://c.os.openstreetmap.org/sv/{zoomlevel}/{x}/{y}.png"),
                new TileSourceRecord("UmpPoland", "Ump Poland","UmpPoland", "",
                    "http://ump.waw.pl/", 1, 17, 256, false, GetAcceptImageWebHeaderCollection(),
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
                //new TileSourceRecord("FreemapSlovakia", "Freemap Slovakia","FreemapSlovakia", "",                
                //    "http://www.freemap.sk/layers/allinone/?/BN/{zoomlevel}/{x}/{y}.png",
                //    "http://www.freemap.sk/", 0, 17, 256, false, GetAcceptImageWebHeaderCollection()),
                // a good map of spain is http://sigpac.mapama.gob.es/SDG/raster/MTN25@3857/14.8026.10210.img, for zoom 14 and 15, and you must replace MTN25 with MTN200 for smaller zooms
                new TileSourceRecord("IgnEs", "Ign (Spain)","IgnEs", "",
                    "http://www.ign.es/", 6, 16, 256, false, GetAcceptImageWebHeaderCollection(),
                    "http://www.ign.es/wmts/mapa-raster?layer=MTN&style=default&tilematrixset=GoogleMapsCompatible&Service=WMTS&Request=GetTile&Version=1.0.0&Format=image/jpeg&TileMatrix={zoomlevel}&TileCol={x}&TileRow={y}"),
                // Keeps giving 404, I think they use special coordinates
                //new TileSourceRecord("Minambiente", "Minambiente (Italy)","Minambiente", "",
                //    "http://www.pcn.minambiente.it/", 4, 18, 512, false, GetAcceptImageWebHeaderCollection(),
                //    "http://www.pcn.minambiente.it/arcgis/rest/services/immagini/ortofoto_colore_12/MapServer/tile/{zoomlevel}/{y}/{x}"),
                // http://www.pcn.minambiente.it/arcgis/rest/services/immagini/ortofoto_colore_12/MapServer/tile/13/5657/5621
                // http://www.pcn.minambiente.it/arcgis/rest/services/immagini/ortofoto_colore_12/MapServer/tile/18/37543/38249

            };
            // this is rather useless
            //new TileSourceRecord("CambLaosThaiViet", "OSM Cambodia Laos Thai Vietnam","CambLaosThaiViet", "",
            //    "http://a.tile.osm-tools.org/osm_then/{zoomlevel}/{x}/{y}.png",
            //    "http://osm-tools.org/", 5, 19, 256, false, GetAcceptImageWebHeaderCollection()),
            // this has become very unreliable
            //new TileSourceRecord("NSWTopo", "LPI NSW Topographic Map (AU)","NSWTopo", "",
            //    "http://maps4.six.nsw.gov.au/arcgis/rest/services/sixmaps/LPI_Imagery_Best/MapServer/tile/{zoomlevel}/{y}/{x}",
            //    "http://www.lpi.nsw.gov.au/", 4, 16, 256, false, GetAcceptImageWebHeaderCollection()),

#if NOSTORE
            output.Add(new TileSourceRecord("MyTopo", "My Topo (N America)", "MyTopo", "",
                "http://www.mytopo.com/", 10, 16, 256, false, GetAcceptImageWebHeaderCollection(),
                "http://tileserver.trimbleoutdoors.com/SecureTile/TileHandler.ashx?mapType=Topo&x={x}&y={y}&z={zoomlevel}"));

#endif

#if NOSTORE
            // also try http://server.arcgisonline.com/ArcGIS/rest/services/World_Topo_Map/MapServer/tile/{zoomlevel}/{y}/{x}
            output.Add(new TileSourceRecord("ArcGIS", "ArcGIS World Topo Map", "ArcGIS", "",
                "", 0, 16, 256, false, GetAcceptImageWebHeaderCollection(),
                "http://services.arcgisonline.com/ArcGIS/rest/services/World_Topo_Map/MapServer/tile/{zoomlevel}/{y}/{x}"));
#endif
            // This has its own coordinates
            //output.Add(new TileSourceRecord("OSMSchweizTopo", "OSM Schweiz Topo (CH)", "OSMSchweizTopo", "",
            //    "http://www.mapplus.ch", 7, 16, 256, false, GetAcceptImageWebHeaderCollection(),
            //    "http://www.mapplus.ch/ts1/1.0.0/osm_schweiz_topo_2016/{zoomlevel}/{x}/{y}.png"));

#if NOSTORE
            // also try http://wmts109.geo.admin.ch/1.0.0/ch.swisstopo.pixelkarte-farbe/default/current/21781/18/12/18.jpeg
            // with Referer = "https://map.schweizmobil.ch/" in the header.
            // Occhio che ha le sue coordinate particolari!
            output.Add(new TileSourceRecord("Swisstopo", "Swisstopo (CH)", "Swisstopo", "",
                "", 7, 16, 256, false, GetDefaultWebHeaderCollection(),
                "http://mpa1.mapplus.ch/swisstopo/{zoomlevel}/{x}/{y}.jpg",
                "http://mpa2.mapplus.ch/swisstopo/{zoomlevel}/{x}/{y}.jpg",
                "http://mpa3.mapplus.ch/swisstopo/{zoomlevel}/{x}/{y}.jpg",
                "http://mpa4.mapplus.ch/swisstopo/{zoomlevel}/{x}/{y}.jpg"));
#endif

#if NOSTORE
            output.Add(new TileSourceRecord("CalTopo", "CalTopo (USA)", "CalTopo", "",
                "http://caltopo.com", 5, 18, 256, false, GetAcceptImageWebHeaderCollection(),
                "http://caltopo.com/resource/imagery/mapbuilder/cs-60-40-c21BB6100-h22-a21-r22-t22d-m21-p21/{zoomlevel}/{x}/{y}.png"));
#endif

#if NOSTORE
            output.Add(new TileSourceRecord("CalTopo2", "CalTopo 2 (USA)", "CalTopo2", "",
                "", 5, 16, 256, false, GetAcceptImageWebHeaderCollection(),
                "http://s3-us-west-1.amazonaws.com/caltopo/topo/{zoomlevel}/{x}/{y}.png?v=1"));
#endif
            return output;
        }
        public static TileSourceRecord GetDefaultTileSource()
        {
            return new TileSourceRecord(DefaultTileSourceTechName, DefaultTileSourceDisplayName, "", "", DefaultTileSourceProviderUriString, MinMinZoom, MaxMaxZoom, DefaultTilePixelSize, false, GetDefaultWebHeaderCollection(), DefaultTileSourceUriString);
        }
        public static TileSourceRecord GetAllTileSource()
        {
            return new TileSourceRecord(AllTileSourceTechName, AllTileSourceDisplayName, "", "", DummyTileSourceProviderUriString, DummyTileSourceMinZoom, DummyTileSourceMaxZoom, DummyTileSourceTilePixelSize, false, GetDefaultWebHeaderCollection(), DummyTileSourceUriString);
        }
        public static TileSourceRecord GetNoTileSource()
        {
            return new TileSourceRecord(NoTileSourceTechName, NoTileSourceDisplayName, "", "", DummyTileSourceProviderUriString, DummyTileSourceMinZoom, DummyTileSourceMaxZoom, DummyTileSourceTilePixelSize, false, GetDefaultWebHeaderCollection(), DummyTileSourceUriString);
        }
        public static TileSourceRecord GetSampleTileSource()
        {
            return new TileSourceRecord(SampleTileSourceTechName, SampleTileSourceTechName, "", "", DefaultTileSourceProviderUriString, MinMinZoom, SampleMaxZoom, DefaultTilePixelSize, false, GetDefaultWebHeaderCollection(), SampleUriString);
        }

        public void ApplyHeadersToWebRequest(WebRequest request)
        {
            if (RequestHeaders != null)
            {
                foreach (var item in RequestHeaders)
                {
                    HttpRequestHeader headerKey;
                    if (Enum.TryParse(item.Key, out headerKey))
                    {
                        request.Headers[headerKey] = item.Value;
                    }
                }
            }

        }
    }
}