using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Utilz;
using Windows.Devices.Geolocation;

namespace LolloGPS.Data.Leeching
{
    [DataContract]
    public sealed class DownloadSession
    {

        private BasicGeoposition _NWCorner;
        [DataMember]
        public BasicGeoposition NWCorner
        {
            get { return _NWCorner; }
            private set { _NWCorner = value; }
        }

        private BasicGeoposition _SECorner;
        [DataMember]
        public BasicGeoposition SECorner
        {
            get { return _SECorner; }
			private set { _SECorner = value; }
        }

        private int _minZoom;
        [DataMember]
        public int MinZoom
        {
            get { return _minZoom; }
			private set { _minZoom = value; }
        }

        private int _maxZoom;
        [DataMember]
        public int MaxZoom
        {
            get { return _maxZoom; }
			private set { _maxZoom = value; }
        }

        private string _tileSourceTechName;
        [DataMember]
        public string TileSourceTechName
        {
            get { return _tileSourceTechName; }
			private set { _tileSourceTechName = value; }
        }

        public bool IsValid
        {
            get
            {
                return string.IsNullOrEmpty(TileSourceRecord.CheckMinMaxZoom(_minZoom, _maxZoom))
                    && PersistentData.GetInstance().GetTileSourceWithTechName(_tileSourceTechName) != null;
            }
        }

        public DownloadSession() { }

        public DownloadSession(int minZoom, int maxZoom, GeoboundingBox gbb, string tileSourceTechName)
        {
            MinZoom = minZoom;
            MaxZoom = maxZoom;
            NWCorner = gbb.NorthwestCorner;
            SECorner = gbb.SoutheastCorner;
            TileSourceTechName = tileSourceTechName;

			if (_minZoom > _maxZoom) LolloMath.Swap(ref _minZoom, ref _maxZoom);

			var tsr = PersistentData.GetInstance().GetTileSourceWithTechName(_tileSourceTechName);
			if (tsr != null)
			{
				MinZoom = Math.Max(_minZoom, tsr.MinZoom);
				MaxZoom = Math.Min(_maxZoom, tsr.MaxZoom);
			}

			if (_minZoom > _maxZoom) LolloMath.Swap(ref _minZoom, ref _maxZoom); // maniman
		}

		public static void Clone(DownloadSession source, ref DownloadSession target)
        {
            if (source != null)
            {
                if (target == null) target = new DownloadSession();

                target.NWCorner = source.NWCorner;
                target.SECorner = source.SECorner;
                target.MinZoom = source.MinZoom;
                target.MaxZoom = source.MaxZoom;
                target.TileSourceTechName = source.TileSourceTechName;
            }
        }
    }
}