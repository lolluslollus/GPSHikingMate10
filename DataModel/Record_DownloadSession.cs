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
        private readonly int _maxZoom;
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

        public DownloadSession(GeoboundingBox gbb, ICollection<TileSourceRecord> tileSources, int maxMaxZoom)
        {
            if (gbb == null) throw new ArgumentException("DownloadSession ctor: gbb is null");
            if (gbb.NorthwestCorner.Latitude == gbb.SoutheastCorner.Latitude
                && gbb.NorthwestCorner.Longitude == gbb.SoutheastCorner.Longitude) throw new ArgumentException("DownloadSession ctor: NW corner same as SE corner");
            if (tileSources?.Any() != true) throw new ArgumentException("DownloadSession ctor: cannot find a tile source with the given name");

            _nwCorner = gbb.NorthwestCorner;
            _seCorner = gbb.SoutheastCorner;
            _tileSourceTechNames = tileSources.Select(ts => ts.TechName).ToList().AsReadOnly();

            int minZoom = 99; int maxZoom = -1;
            // first try to find the min and max zooms from the base layer tile source
            foreach (var ts in tileSources)
            {
                if (ts.IsOverlay) continue;
                maxZoom = Math.Max(ts.MaxZoom, maxZoom);
                minZoom = Math.Min(ts.MinZoom, minZoom);
            }
            // no base layer tile source: check all tile sources
            if (minZoom == 99 || maxZoom == -1)
            {
                foreach (var ts in tileSources)
                {
                    maxZoom = Math.Max(ts.MaxZoom, maxZoom);
                    minZoom = Math.Min(ts.MinZoom, minZoom);
                }
            }
            maxZoom = Math.Min(maxZoom, maxMaxZoom);
            if (minZoom > maxZoom) LolloMath.Swap(ref minZoom, ref maxZoom);

            string zoomErrorMsg = TileSourceRecord.CheckMinMaxZoom(minZoom, maxZoom);
            if (!string.IsNullOrEmpty(zoomErrorMsg)) throw new ArgumentException("DownloadSession ctor: " + zoomErrorMsg);

            _maxZoom = maxZoom;
            _minZoom = minZoom;
        }
        // ctor for cloning
        public DownloadSession(int minZoom, int maxZoom, BasicGeoposition nwCorner, BasicGeoposition seCorner, IEnumerable<string> tileSourcesTechNames)
        {
            if (tileSourcesTechNames?.Any() != true) throw new ArgumentException("DownloadSession ctor: cannot find a tile source with the given name");
            if (nwCorner.Latitude == seCorner.Latitude && nwCorner.Longitude == seCorner.Longitude) throw new ArgumentException("DownloadSession ctor: NW corner same as SE corner");
            string zoomErrorMsg = TileSourceRecord.CheckMinMaxZoom(minZoom, maxZoom);
            if (!string.IsNullOrEmpty(zoomErrorMsg)) throw new ArgumentException("DownloadSession ctor: " + zoomErrorMsg);

            _tileSourceTechNames = tileSourcesTechNames.ToList(); // clone // LOLLO TODO check if this really clones
            _nwCorner = nwCorner;
            _seCorner = seCorner;
            _minZoom = minZoom;
            _maxZoom = maxZoom;
        }

        public static DownloadSession Clone(DownloadSession source)
        {
            if (source == null) return null;
            return new DownloadSession(source._minZoom, source._maxZoom, source._nwCorner, source._seCorner, source._tileSourceTechNames);
        }
    }
}