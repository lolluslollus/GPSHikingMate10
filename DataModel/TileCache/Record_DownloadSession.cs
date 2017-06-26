using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Serialization;
using Utilz;
using Windows.Devices.Geolocation;

namespace LolloGPS.Data.TileCache
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
        private readonly IReadOnlyList<TileSourceRecord> _tileSources;
        public IReadOnlyList<TileSourceRecord> TileSources
        {
            get { return _tileSources; }
        }
        /// <summary>
        /// Initialises an instance starting from scratch.
        /// Throws <see cref="InvalidDownloadSessionArgumentsException"/> if parameters are no good.
        /// </summary>
        /// <param name="gbb"></param>
        /// <param name="tileSources"></param>
        /// <param name="maxMaxZoom"></param>
        /// <exception cref="InvalidDownloadSessionArgumentsException"/>
        public DownloadSession(GeoboundingBox gbb, ICollection<TileSourceRecord> tileSources, int maxMaxZoom)
        {
            if (gbb == null) throw new InvalidDownloadSessionArgumentsException("DownloadSession ctor: gbb is null");
            if (gbb.NorthwestCorner.Latitude == gbb.SoutheastCorner.Latitude
                && gbb.NorthwestCorner.Longitude == gbb.SoutheastCorner.Longitude) throw new InvalidDownloadSessionArgumentsException("DownloadSession ctor: NW corner same as SE corner");
            if (tileSources?.Any() != true) throw new InvalidDownloadSessionArgumentsException("DownloadSession ctor: no tile sources to download");

            var downloadableTileSources = tileSources.Where(ts2 => !ts2.IsDefault && !ts2.IsFileSource && !ts2.IsAll && !ts2.IsNone);
            if (downloadableTileSources?.Any() != true) throw new InvalidDownloadSessionArgumentsException("DownloadSession ctor: no tile sources suitable for download");

            int minZoom = 99; int maxZoom = -1;
            // first try to find the min and max zooms from the base layer tile source
            foreach (var ts in downloadableTileSources)
            {
                if (ts.IsOverlay) continue;
                maxZoom = Math.Max(ts.MaxZoom, maxZoom);
                minZoom = Math.Min(ts.MinZoom, minZoom);
            }
            // no base layer tile source: check all tile sources
            if (minZoom == 99 || maxZoom == -1)
            {
                foreach (var ts in downloadableTileSources)
                {
                    maxZoom = Math.Max(ts.MaxZoom, maxZoom);
                    minZoom = Math.Min(ts.MinZoom, minZoom);
                }
            }
            maxZoom = Math.Min(maxZoom, maxMaxZoom);
            //if (minZoom > maxZoom) LolloMath.Swap(ref minZoom, ref maxZoom);
            //if (minZoom > maxZoom) minZoom = maxZoom = 0;

            string zoomErrorMsg = TileSourceRecord.CheckMinMaxZoom(minZoom, maxZoom);
            if (!string.IsNullOrEmpty(zoomErrorMsg)) throw new InvalidDownloadSessionArgumentsException("DownloadSession ctor: " + zoomErrorMsg);

            _maxZoom = maxZoom;
            _minZoom = minZoom;

            _tileSources = GetTileSourcesWithReducedZooms(downloadableTileSources, maxZoom, minZoom);

            _nwCorner = gbb.NorthwestCorner;
            _seCorner = gbb.SoutheastCorner;
        }
        /// <summary>
        /// Initialises an instance starting from another instance.
        /// Throws <see cref="InvalidDownloadSessionArgumentsException"/> if params are no good.
        /// </summary>
        /// <param name="minZoom"></param>
        /// <param name="maxZoom"></param>
        /// <param name="nwCorner"></param>
        /// <param name="seCorner"></param>
        /// <param name="tileSources"></param>
        /// <exception cref="InvalidDownloadSessionArgumentsException"/>
        public DownloadSession(int minZoom, int maxZoom, BasicGeoposition nwCorner, BasicGeoposition seCorner, IEnumerable<TileSourceRecord> tileSources)
        {
            if (tileSources?.Any() != true) throw new InvalidDownloadSessionArgumentsException("DownloadSession ctor: cannot find a tile source with the given name");
            if (nwCorner.Latitude == seCorner.Latitude && nwCorner.Longitude == seCorner.Longitude) throw new InvalidDownloadSessionArgumentsException("DownloadSession ctor: NW corner same as SE corner");
            string zoomErrorMsg = TileSourceRecord.CheckMinMaxZoom(minZoom, maxZoom);
            if (!string.IsNullOrEmpty(zoomErrorMsg)) throw new InvalidDownloadSessionArgumentsException("DownloadSession ctor: " + zoomErrorMsg);

            _tileSources = GetTileSourcesWithReducedZooms(tileSources, maxZoom, minZoom);
            _nwCorner = nwCorner;
            _seCorner = seCorner;
            _minZoom = minZoom;
            _maxZoom = maxZoom;
        }
        private IReadOnlyList<TileSourceRecord> GetTileSourcesWithReducedZooms(IEnumerable<TileSourceRecord> tileSources, int maxZoom, int minZoom)
        {
            return tileSources.Select(ts =>
            {
                var tsClone = WritableTileSourceRecord.Clone(ts);
                var maxZoomReduced = Math.Min(tsClone.MaxZoom, maxZoom);
                var minZoomReduced = Math.Max(tsClone.MinZoom, minZoom);
                //if (minZoomReduced > maxZoomReduced) maxZoomReduced = minZoomReduced = 0;
                tsClone.MaxZoom = maxZoomReduced;
                tsClone.MinZoom = minZoomReduced;
                return tsClone as TileSourceRecord;
            }).ToList().AsReadOnly();
        }
    }
    public sealed class InvalidDownloadSessionArgumentsException : ArgumentException
    {
        public InvalidDownloadSessionArgumentsException() : base() { }
        public InvalidDownloadSessionArgumentsException(string message) : base(message) { }
    }
}