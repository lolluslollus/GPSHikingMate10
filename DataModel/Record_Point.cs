using SQLite;
using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace LolloGPS.Data
{
	[DataContract]
	public sealed class PointRecord : ObservableData
	{
		#region properties
		private double _latitude = default(double);
		private double _longitude = default(double);
		private double _altitude = default(double);
		private double _accuracy = default(double);
		private double _altitudeAccuracy = default(double);
		private UInt32 _howManySatellites = default(UInt32);
		//private double _positionDilutionOfPrecision = default(double);
		//private double _horizontalDilutionOfPrecision = default(double);
		//private double _verticalDilutionOfPrecision = default(double);
		private string _positionSource = string.Empty;
		private double _speedInMetreSec = default(double);
		private string _status = string.Empty;
		private DateTime _timePoint = default(DateTime);
		//private string _gpsName = string.Empty;
		//private string _gpsComment = string.Empty;
		private string _humanDescription = string.Empty;
		private string _hyperLink = null;
		private string _hyperLinkText = string.Empty;

		[PrimaryKey, AutoIncrement] // required by SQLite
		public int Id { get; set; } // required by SQLite

		// LOLLO NOTE many things had become extremely slow because I had set RaisePropertyChanged_UI instead of RaisePropertyChanged everywhere in the following
		// RaisePropertyChanged is a multiple faster.

		[DataMember]
		public double Latitude { get { return _latitude; } set { _latitude = value; RaisePropertyChanged(); } }
		[DataMember]
		public double Longitude { get { return _longitude; } set { _longitude = value; RaisePropertyChanged(); } }
		[DataMember]
		public double Altitude { get { return _altitude; } set { _altitude = value; RaisePropertyChanged(); } }
		[DataMember]
		public double Accuracy { get { return _accuracy; } set { _accuracy = value; RaisePropertyChanged(); } }
		[DataMember]
		public double AltitudeAccuracy { get { return _altitudeAccuracy; } set { _altitudeAccuracy = value; RaisePropertyChanged(); } }
		[DataMember]
		public UInt32 HowManySatellites { get { return _howManySatellites; } set { _howManySatellites = value; RaisePropertyChanged(); } }
		//[DataMember]
		//public double PositionDilutionOfPrecision { get { return _positionDilutionOfPrecision; } set { _positionDilutionOfPrecision = value; RaisePropertyChanged("PositionDilutionOfPrecision"); } }
		//[DataMember]
		//public double HorizontalDilutionOfPrecision { get { return _horizontalDilutionOfPrecision; } set { _horizontalDilutionOfPrecision = value; RaisePropertyChanged("HorizontalDilutionOfPrecision"); } }
		//[DataMember]
		//public double VerticalDilutionOfPrecision { get { return _verticalDilutionOfPrecision; } set { _verticalDilutionOfPrecision = value; RaisePropertyChanged("LatitudeVerticalDilutionOfPrecision"); } }
		[DataMember]
		public string PositionSource { get { return _positionSource; } set { _positionSource = value; RaisePropertyChanged(); } }
		[DataMember]
		public double SpeedInMetreSec { get { return _speedInMetreSec; } set { _speedInMetreSec = value; RaisePropertyChanged(); } }
		[DataMember]
		public string Status { get { return _status; } set { _status = value; RaisePropertyChanged(); } }
		[DataMember]
		public DateTime TimePoint { get { return _timePoint; } set { _timePoint = value; RaisePropertyChanged(); } }
		//[DataMember]
		//public string GPSName { get { return _gpsName; } set { _gpsName = value; RaisePropertyChanged(); } }
		//[DataMember]
		//public string GPSComment { get { return _gpsComment; } set { _gpsComment = value; RaisePropertyChanged(); } }
		[DataMember]
		public string HumanDescription { get { return _humanDescription; } set { if (_humanDescription != value) { _humanDescription = value; RaisePropertyChanged(); } } }
		[DataMember]
		public string HyperLink { get { return _hyperLink; } set { _hyperLink = value; RaisePropertyChanged(); } }
		[DataMember]
		public string HyperLinkText { get { return _hyperLinkText; } set { _hyperLinkText = value; RaisePropertyChanged(); } }
		#endregion properties

		public PointRecord() { }
		public static void Clone(PointRecord source, ref PointRecord target)
		{
			if (source != null)
			{
				if (target == null) target = new PointRecord();

				target.Accuracy = source.Accuracy;
				target.Altitude = source.Altitude;
				target.AltitudeAccuracy = source.AltitudeAccuracy;
				target.HowManySatellites = source.HowManySatellites;
				target.Latitude = source.Latitude;
				target.Longitude = source.Longitude;
				target.PositionSource = source.PositionSource;
				target.SpeedInMetreSec = source.SpeedInMetreSec;
				target.Status = source.Status;
				//target.HorizontalDilutionOfPrecision = source.HorizontalDilutionOfPrecision;
				//target.PositionDilutionOfPrecision = source.PositionDilutionOfPrecision;
				//target.VerticalDilutionOfPrecision = source.VerticalDilutionOfPrecision;
				target.TimePoint = source.TimePoint;
				//target.GPSName = source.GPSName;
				//target.GPSComment = source.GPSComment;
				target.HumanDescription = source.HumanDescription;
				target.HyperLink = source.HyperLink;
				target.HyperLinkText = source.HyperLinkText;
			}
		}
		public bool IsEqualTo(PointRecord comp)
		{
			if (comp == null) return false;
			return
				Accuracy == comp.Accuracy &&
				Altitude == comp.Altitude &&
				AltitudeAccuracy == comp.AltitudeAccuracy &&
				HowManySatellites == comp.HowManySatellites &&
				Latitude == comp.Latitude &&
				Longitude == comp.Longitude &&
				PositionSource == comp.PositionSource &&
				SpeedInMetreSec == comp.SpeedInMetreSec &&
				Status == comp.Status &&
				//HorizontalDilutionOfPrecision == comp.HorizontalDilutionOfPrecision &&
				//PositionDilutionOfPrecision == comp.PositionDilutionOfPrecision &&
				//VerticalDilutionOfPrecision == comp.VerticalDilutionOfPrecision &&
				TimePoint == comp.TimePoint &&
				//GPSName == comp.GPSName &&
				//GPSComment == comp.GPSComment &&
				HumanDescription == comp.HumanDescription &&
				HyperLink == comp.HyperLink &&
				HyperLinkText == comp.HyperLinkText;
		}

		public async Task UpdateUIEditablePropertiesAsync(PointRecord newValue, PersistentData.Tables whichSeries)
		{
			if (IsEqualTo(newValue)) return;
			await RunInUiThreadAsync(delegate
			{
				HumanDescription = newValue.HumanDescription;
				HyperLink = newValue.HyperLink;
				HyperLinkText = newValue.HyperLinkText;
			}).ConfigureAwait(false);

			UpdateDb(whichSeries);
		}

		public async Task UpdateHumanDescriptionAsync(string newValue, PersistentData.Tables whichSeries)
		{
			if (HumanDescription == newValue) return;
			await RunInUiThreadAsync(delegate
			{
				HumanDescription = newValue;
			}).ConfigureAwait(false);

			UpdateDb(whichSeries);
		}

		public async Task UpdateHyperlinkAsync(string newValue, PersistentData.Tables whichSeries)
		{
			if (HyperLink == newValue) return;
			await RunInUiThreadAsync(delegate
			{
				HyperLink = newValue;
			}).ConfigureAwait(false);

			UpdateDb(whichSeries);
		}

		public async Task UpdateHyperlinkTextAsync(string newValue, PersistentData.Tables whichSeries)
		{
			if (HyperLinkText == newValue) return;
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
				case PersistentData.Tables.Landmarks:
					Task updateLandmarks = DBManager.UpdateLandmarksAsync(this, false);
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
