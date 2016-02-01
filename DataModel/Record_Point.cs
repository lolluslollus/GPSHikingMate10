using SQLite;
using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Utilz.Data;

namespace LolloGPS.Data
{
	[DataContract]
	public sealed class PointRecord : ObservableData
	{
		#region properties
		//private double _positionDilutionOfPrecision = default(double);
		//private double _horizontalDilutionOfPrecision = default(double);
		//private double _verticalDilutionOfPrecision = default(double);
		//private string _gpsName = string.Empty;
		//private string _gpsComment = string.Empty;

		private int _id = 0;
		[PrimaryKey, AutoIncrement] // required by SQLite
		public int Id { get { return _id; } set { _id = value; } } // required by SQLite

		// LOLLO NOTE many things had become extremely slow because I had set RaisePropertyChanged_UI instead of RaisePropertyChanged everywhere in the following
		// RaisePropertyChanged is a multiple faster.

		private double _latitude = default(double);
		[DataMember]
		public double Latitude { get { return _latitude; } set { _latitude = value; RaisePropertyChanged(); } }

		private double _longitude = default(double);
		[DataMember]
		public double Longitude { get { return _longitude; } set { _longitude = value; RaisePropertyChanged(); } }

		private double _altitude = default(double);
		[DataMember]
		public double Altitude { get { return _altitude; } set { _altitude = value; RaisePropertyChanged(); } }

		private double _accuracy = default(double);
		[DataMember]
		public double Accuracy { get { return _accuracy; } set { _accuracy = value; RaisePropertyChanged(); } }

		private double _altitudeAccuracy = default(double);
		[DataMember]
		public double AltitudeAccuracy { get { return _altitudeAccuracy; } set { _altitudeAccuracy = value; RaisePropertyChanged(); } }

		private uint _howManySatellites = default(uint);
		[DataMember]
		public uint HowManySatellites { get { return _howManySatellites; } set { _howManySatellites = value; RaisePropertyChanged(); } }
		//[DataMember]
		//public double PositionDilutionOfPrecision { get { return _positionDilutionOfPrecision; } set { _positionDilutionOfPrecision = value; RaisePropertyChanged("PositionDilutionOfPrecision"); } }
		//[DataMember]
		//public double HorizontalDilutionOfPrecision { get { return _horizontalDilutionOfPrecision; } set { _horizontalDilutionOfPrecision = value; RaisePropertyChanged("HorizontalDilutionOfPrecision"); } }
		//[DataMember]
		//public double VerticalDilutionOfPrecision { get { return _verticalDilutionOfPrecision; } set { _verticalDilutionOfPrecision = value; RaisePropertyChanged("LatitudeVerticalDilutionOfPrecision"); } }

		private string _positionSource = string.Empty;
		[DataMember]
		public string PositionSource { get { return _positionSource; } set { _positionSource = value; RaisePropertyChanged(); } }

		private double _speedInMetreSec = default(double);
		[DataMember]
		public double SpeedInMetreSec { get { return _speedInMetreSec; } set { _speedInMetreSec = value; RaisePropertyChanged(); } }

		private string _status = string.Empty;
		[DataMember]
		public string Status { get { return _status; } set { _status = value; RaisePropertyChanged(); } }

		private DateTime _timePoint = default(DateTime);
		[DataMember]
		public DateTime TimePoint { get { return _timePoint; } set { _timePoint = value; RaisePropertyChanged(); } }
		//[DataMember]
		//public string GPSName { get { return _gpsName; } set { _gpsName = value; RaisePropertyChanged(); } }
		//[DataMember]
		//public string GPSComment { get { return _gpsComment; } set { _gpsComment = value; RaisePropertyChanged(); } }

		private string _humanDescription = string.Empty;
		[DataMember]
		public string HumanDescription { get { return _humanDescription; } set { if (_humanDescription != value) { _humanDescription = value; RaisePropertyChanged(); } } }

		private string _hyperLink = null;
		[DataMember]
		public string HyperLink { get { return _hyperLink; } set { _hyperLink = value; RaisePropertyChanged(); } }

		private string _hyperLinkText = string.Empty;
		[DataMember]
		public string HyperLinkText { get { return _hyperLinkText; } set { _hyperLinkText = value; RaisePropertyChanged(); } }
		#endregion properties

		public PointRecord() { }
		public static void Clone(PointRecord source, ref PointRecord target)
		{
			if (source != null)
			{
				if (target == null) target = new PointRecord();

				target.Accuracy = source._accuracy;
				target.Altitude = source._altitude;
				target.AltitudeAccuracy = source._altitudeAccuracy;
				target.HowManySatellites = source._howManySatellites;
				target.Latitude = source._latitude;
				target.Longitude = source._longitude;
				target.PositionSource = source._positionSource;
				target.SpeedInMetreSec = source._speedInMetreSec;
				target.Status = source._status;
				//target.HorizontalDilutionOfPrecision = source.HorizontalDilutionOfPrecision;
				//target.PositionDilutionOfPrecision = source.PositionDilutionOfPrecision;
				//target.VerticalDilutionOfPrecision = source.VerticalDilutionOfPrecision;
				target.TimePoint = source._timePoint;
				//target.GPSName = source.GPSName;
				//target.GPSComment = source.GPSComment;
				target.HumanDescription = source._humanDescription;
				target.HyperLink = source._hyperLink;
				target.HyperLinkText = source._hyperLinkText;
			}
		}
		public bool IsEqualTo(PointRecord comp)
		{
			if (comp == null) return false;
			return
				_accuracy == comp._accuracy &&
				_altitude == comp._altitude &&
				_altitudeAccuracy == comp._altitudeAccuracy &&
				_howManySatellites == comp._howManySatellites &&
				_latitude == comp._latitude &&
				_longitude == comp._longitude &&
				_positionSource == comp._positionSource &&
				_speedInMetreSec == comp._speedInMetreSec &&
				_status == comp._status &&
				//HorizontalDilutionOfPrecision == comp.HorizontalDilutionOfPrecision &&
				//PositionDilutionOfPrecision == comp.PositionDilutionOfPrecision &&
				//VerticalDilutionOfPrecision == comp.VerticalDilutionOfPrecision &&
				_timePoint == comp._timePoint &&
				//GPSName == comp.GPSName &&
				//GPSComment == comp.GPSComment &&
				_humanDescription == comp._humanDescription &&
				_hyperLink == comp._hyperLink &&
				_hyperLinkText == comp._hyperLinkText;
		}

		public async Task UpdateUIEditablePropertiesAsync(PointRecord newValue, PersistentData.Tables whichSeries)
		{
			if (IsEqualTo(newValue)) return;
			await RunInUiThreadAsync(delegate
			{
				HumanDescription = newValue?._humanDescription;
				HyperLink = newValue?._hyperLink;
				HyperLinkText = newValue?._hyperLinkText;
			}).ConfigureAwait(false);

			UpdateDb(whichSeries);
		}

		public async Task UpdateHumanDescriptionAsync(string newValue, PersistentData.Tables whichSeries)
		{
			if (_humanDescription == newValue) return;
			await RunInUiThreadAsync(delegate
			{
				HumanDescription = newValue;
			}).ConfigureAwait(false);

			UpdateDb(whichSeries);
		}

		public async Task UpdateHyperlinkAsync(string newValue, PersistentData.Tables whichSeries)
		{
			if (_hyperLink == newValue) return;
			await RunInUiThreadAsync(delegate
			{
				HyperLink = newValue;
			}).ConfigureAwait(false);

			UpdateDb(whichSeries);
		}

		public async Task UpdateHyperlinkTextAsync(string newValue, PersistentData.Tables whichSeries)
		{
			if (_hyperLinkText == newValue) return;
			await RunInUiThreadAsync(delegate
			{
				HyperLinkText = newValue;
			}).ConfigureAwait(false);

			UpdateDb(whichSeries);
		}

		private void UpdateDb(PersistentData.Tables whichSeries)
		{
			switch (whichSeries)
			{
				case PersistentData.Tables.History:
					Task updateHistory = DBManager.UpdateHistoryAsync(this, false);
					break;
				case PersistentData.Tables.Route0:
					Task updateRoute0 = DBManager.UpdateRoute0Async(this, false);
					break;
				case PersistentData.Tables.Checkpoints:
					Task updateCheckpoints = DBManager.UpdateCheckpointsAsync(this, false);
					break;
				default:
					break;
			}
		}

		public bool IsEmpty()
		{
			return !(
				_latitude != default(double)
				|| _longitude != default(double)
				|| _altitude != default(double)
				|| !string.IsNullOrWhiteSpace(_positionSource));
		}
	}
}