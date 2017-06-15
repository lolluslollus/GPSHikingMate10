using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Serialization;
using Utilz;
using Windows.Devices.Geolocation;

namespace LolloGPS.Data.Leeching
{
    [DataContract]
    [KnownType(typeof(string[]))]
    [KnownType(typeof(IReadOnlyList<string>))]
    [KnownType(typeof(ReadOnlyCollection<string>))]
    public sealed class DownloadSession
    {
        [DataMember]
        private readonly BasicGeoposition _nwCorner;
        public BasicGeoposition NWCorner
        {
            get { return _nwCorner; }
        }

        [DataMember]
        private readonly BasicGeoposition _seCorner;
        public BasicGeoposition SECorner
        {
            get { return _seCorner; }
        }

        [DataMember]
        private readonly int _minZoom;
        public int MinZoom
        {
            get { return _minZoom; }
        }

        [DataMember]
        private int _maxZoom;
        public int MaxZoom
        {
            get { return _maxZoom; }
        }

        [DataMember]
        private readonly IReadOnlyList<string> _tileSourceTechNames;
        public IReadOnlyList<string> TileSourceTechNames
        {
            get { return _tileSourceTechNames; }
        }

        public bool IsZoomsValid()
        {
            return string.IsNullOrEmpty(TileSourceRecord.CheckMinMaxZoom(_minZoom, _maxZoom));
        }

        public DownloadSession(int minZoom, int maxZoom, GeoboundingBox gbb, ICollection<TileSourceRecord> tileSources) : this(minZoom, maxZoom)
        {
            if (gbb == null) throw new ArgumentException("DownloadSession ctor: gbb is null");
            if (tileSources == null || tileSources.Count == 0) throw new ArgumentException("DownloadSession ctor: cannot find a tile source with the given name");

            _nwCorner = gbb.NorthwestCorner;
            _seCorner = gbb.SoutheastCorner;
            _tileSourceTechNames = tileSources.Select(ts => ts.TechName).ToList().AsReadOnly();
        }
        // ctor for cloning
        private DownloadSession(int minZoom, int maxZoom, BasicGeoposition nwCorner, BasicGeoposition seCorner, IEnumerable<string> tileSourcesTechNames) : this(minZoom, maxZoom)
        {
            _nwCorner = nwCorner;
            _seCorner = seCorner;
            _tileSourceTechNames = tileSourcesTechNames.ToList(); // clone // LOLLO TODO check if this really clones
        }

        private DownloadSession(int minZoom, int maxZoom)
        {
            _minZoom = minZoom;
            _maxZoom = maxZoom;

            if (_minZoom > _maxZoom) LolloMath.Swap(ref _minZoom, ref _maxZoom);

            string zoomErrorMsg = TileSourceRecord.CheckMinMaxZoom(_minZoom, _maxZoom);
            if (!string.IsNullOrEmpty(zoomErrorMsg)) throw new ArgumentException("DownloadSession ctor: " + zoomErrorMsg);
        }
        public static DownloadSession Clone(DownloadSession source)
        {
            if (source == null) return null;
            return new DownloadSession(source._minZoom, source._maxZoom, source._nwCorner, source._seCorner, source._tileSourceTechNames);
        }
    }
}