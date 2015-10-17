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
        //private bool _isComplete = false;
        //[DataMember]
        //public bool IsComplete
        //{
        //    get { return _isComplete; }
        //    set { _isComplete = value; }
        //}

        private BasicGeoposition _NWCorner;
        [DataMember]
        public BasicGeoposition NWCorner
        {
            get { return _NWCorner; }
            set { _NWCorner = value; }
        }

        private BasicGeoposition _SECorner;
        [DataMember]
        public BasicGeoposition SECorner
        {
            get { return _SECorner; }
            set { _SECorner = value; }
        }

        private int _minZoom;
        [DataMember]
        public int MinZoom
        {
            get { return _minZoom; }
            set { _minZoom = value; }
        }

        private int _maxZoom;
        [DataMember]
        public int MaxZoom
        {
            get { return _maxZoom; }
            set { _maxZoom = value; }
        }

        private string _tileSourceTechName;
        [DataMember]
        public string TileSourceTechName
        {
            get { return _tileSourceTechName; }
            set { _tileSourceTechName = value; }
        }

        public bool IsValid
        {
            get
            {
                return string.IsNullOrEmpty(TileSourceRecord.CheckMinMaxZoom(MinZoom, MaxZoom))
                    && PersistentData.GetInstance().TileSourcez.FirstOrDefault(a => a.TechName == TileSourceTechName) != null;
            }
        }

        public DownloadSession() { }

        public DownloadSession(int minZoom, int maxZoom, BasicGeoposition nwCorner, BasicGeoposition seCorner, string tileSourceTechName)
        {
            //IsComplete = false;
            MinZoom = minZoom;
            MaxZoom = maxZoom;
            NWCorner = nwCorner;
            SECorner = seCorner;
            TileSourceTechName = tileSourceTechName;

            AdjustZooms();
        }
        public static void Clone(DownloadSession source, ref DownloadSession target)
        {
            if (source != null)
            {
                if (target == null) target = new DownloadSession();

                //target.IsComplete = source.IsComplete;
                target.NWCorner = source.NWCorner;
                target.SECorner = source.SECorner;
                target.MinZoom = source.MinZoom;
                target.MaxZoom = source.MaxZoom;
                target.TileSourceTechName = source.TileSourceTechName;
            }
        }

        public void AdjustZooms()
        {
            //if (!_isComplete)
            //{
                if (MinZoom > MaxZoom) LolloMath.Swap(ref _minZoom, ref _maxZoom);

                PersistentData persistentData = PersistentData.GetInstance();
                TileSourceRecord tsr = persistentData.TileSourcez.FirstOrDefault(a => a.TechName == TileSourceTechName);
                if (tsr != null)
                {
                    LolloGPS.Data.TileCache.TileCache tileCache = new LolloGPS.Data.TileCache.TileCache(tsr, persistentData.IsMapCached);
                    MinZoom = Math.Max(MinZoom, tileCache.GetMinZoom());
                    MaxZoom = Math.Min(MaxZoom, tileCache.GetMaxZoom());
                }

                if (MinZoom > MaxZoom) LolloMath.Swap(ref _minZoom, ref _maxZoom); // maniman
            //}
        }
    }
}
