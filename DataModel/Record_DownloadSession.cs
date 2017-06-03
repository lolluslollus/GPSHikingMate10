using System;
using System.Runtime.Serialization;
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

        public bool IsZoomsValid()
		{
			return string.IsNullOrEmpty(TileSourceRecord.CheckMinMaxZoom(_minZoom, _maxZoom));
		}

		public DownloadSession() { }

		/// <summary>
		/// Instantiates a DownloadSession
		/// </summary>
		/// <param name="minZoom"></param>
		/// <param name="maxZoom"></param>
		/// <param name="gbb"></param>
		/// <param name="tileSource"></param>
		/// <exception cref="ArgumentException"/>
		public DownloadSession(int minZoom, int maxZoom, GeoboundingBox gbb, TileSourceRecord tileSource)
		{
			if (tileSource == null) throw new ArgumentException("DownloadSession ctor: cannot find a tile source with the given name");

			MinZoom = minZoom;
			MaxZoom = maxZoom;
			NWCorner = gbb.NorthwestCorner;
			SECorner = gbb.SoutheastCorner;
			TileSourceTechName = tileSource.TechName;            

			if (_minZoom > _maxZoom) LolloMath.Swap(ref _minZoom, ref _maxZoom);

			MinZoom = Math.Max(_minZoom, tileSource.MinZoom);
			MaxZoom = Math.Min(_maxZoom, tileSource.MaxZoom);

			if (_minZoom > _maxZoom) LolloMath.Swap(ref _minZoom, ref _maxZoom); // maniman

			string zoomErrorMsg = TileSourceRecord.CheckMinMaxZoom(_minZoom, _maxZoom);
			if (!string.IsNullOrEmpty(zoomErrorMsg)) throw new ArgumentException("DownloadSession ctor: " + zoomErrorMsg);
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