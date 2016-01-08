using LolloGPS.Data.Constants;
using LolloGPS.Data.Leeching;
using SQLite;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Utilz;
using Windows.ApplicationModel.Core;
using Windows.Devices.Geolocation;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls.Maps;

// There is a sqlite walkthrough at:
// http://social.technet.microsoft.com/wiki/contents/articles/29149.windows-phone-8-1-sqlite-part-one.aspx

namespace LolloGPS.Data
{
	[DataContract]
	public sealed class PersistentData : ObservableData //, INotifyDataErrorInfo //does not work
	{
		public enum Tables { History, Route0, Landmarks, nil }
		public static string GetTextForSeries(PersistentData.Tables whichSeries)
		{
			switch (whichSeries)
			{
				case Tables.History:
					return "Tracking history";
				case Tables.Route0:
					return "Route";
				case Tables.Landmarks:
					return "Landmarks";
				case Tables.nil:
					return "No series";
				default:
					return "";
			}
		}
		public const int MaxRecordsInRoute = short.MaxValue;
		public const int MaxRecordsInHistory = short.MaxValue;
		private const int MaxLandmarks1 = 100;
		private const int MaxLandmarks2 = 250;
		private const int MaxLandmarks3 = 600;
		private const int MaxLandmarks4 = 1500;
		private const int MaxLandmarks5 = 4000;

		public static readonly int MaxRecordsInLandmarks = MaxLandmarks5;
		public const uint MinBackgroundUpdatePeriodInMinutes = 15u;
		public const uint MaxBackgroundUpdatePeriodInMinutes = 120u;
		public const uint MinReportIntervalInMilliSec = 3000u;
		public const uint MaxReportIntervalInMilliSec = 900000u;
		public const uint MinAccuracyInMetres = 1u;
		public const string DefaultPositionSource = ConstantData.APPNAME;
		private const int DefaultSelectedIndex_Base1 = 0;

		private static SemaphoreSlimSafeRelease _historySemaphore = new SemaphoreSlimSafeRelease(1, 1);
		private static SemaphoreSlimSafeRelease _route0Semaphore = new SemaphoreSlimSafeRelease(1, 1);
		private static SemaphoreSlimSafeRelease _landmarksSemaphore = new SemaphoreSlimSafeRelease(1, 1);
		private static SemaphoreSlimSafeRelease _tileSourcezSemaphore = new SemaphoreSlimSafeRelease(1, 1);

		#region events
		public event EventHandler CurrentChanged;
		#endregion events

		#region construct dispose and clone
		private static PersistentData _instance;
		private static readonly object _instanceLock = new object();
		public static PersistentData GetInstance()
		{
			lock (_instanceLock)
			{
				if (_instance == null)
				{
					_instance = new PersistentData();
				}
				return _instance;
			}
		}

		public static async Task SetInstanceAsync(PersistentData newData, List<PointRecord> history, List<PointRecord> route0, List<PointRecord> landmarks)
		{
			try
			{
				PersistentData dataToBeChanged = PersistentData.GetInstance();
				//I must clone memberwise, otherwise the current event handlers get lost
				CloneNonDbProperties_internal(newData, ref dataToBeChanged);

				if (history != null)
				{
					try
					{
						await _historySemaphore.WaitAsync().ConfigureAwait(false);
						dataToBeChanged.History.Clear();
						dataToBeChanged.History.AddRange(history);
						dataToBeChanged.SetCurrentToLast();
					}
					finally
					{
						SemaphoreSlimSafeRelease.TryRelease(_historySemaphore);
					}
				}
				if (route0 != null)
				{
					try
					{
						await _route0Semaphore.WaitAsync().ConfigureAwait(false);
						dataToBeChanged.Route0.Clear();
						dataToBeChanged.Route0.AddRange(route0);
					}
					finally
					{
						SemaphoreSlimSafeRelease.TryRelease(_route0Semaphore);
					}
				}
				if (landmarks != null)
				{
					try
					{
						await _landmarksSemaphore.WaitAsync().ConfigureAwait(false);
						dataToBeChanged.Landmarks.Clear();
						dataToBeChanged.Landmarks.AddRange(landmarks);
					}
					finally
					{
						SemaphoreSlimSafeRelease.TryRelease(_landmarksSemaphore);
					}
				}
			}
			catch (Exception exc0)
			{
				Logger.Add_TPL(exc0.ToString(), Logger.PersistentDataLogFilename);
			}
		}
		public PersistentData CloneNonDbProperties()
		{
			PersistentData output = new PersistentData();
			CloneNonDbProperties_internal(this, ref output);
			return output;
		}
		private static void CloneNonDbProperties_internal(PersistentData source, ref PersistentData target)
		{
			if (source != null && target != null)
			{
				PointRecord.Clone(source.Selected, ref target._selected);
				target.RaisePropertyChanged(nameof(PersistentData.Selected));

				if (!source.Target.IsEmpty()) PointRecord.Clone(source.Target, ref target._target);
				target.RaisePropertyChanged(nameof(PersistentData.Target));

				target.SelectedSeries = source.SelectedSeries;
				target.BackgroundUpdatePeriodInMinutes = source.BackgroundUpdatePeriodInMinutes;
				target.DesiredAccuracyInMeters = source.DesiredAccuracyInMeters;
				target.ReportIntervalInMilliSec = source.ReportIntervalInMilliSec;
				// target.LastMessage = source.LastMessage; // we don't want to repeat the last message whenever one starts the app
				target.IsTracking = source.IsTracking;
				target.IsBackgroundEnabled = source.IsBackgroundEnabled;
				target.IsCentreOnCurrent = source.IsCentreOnCurrent;
				target.IsShowAim = source.IsShowAim;
				target.IsShowAimOnce = source.IsShowAimOnce;
				target.IsShowSpeed = source.IsShowSpeed;
				target.MapStyle = source.MapStyle;
				target.MapLastLat = source.MapLastLat;
				target.MapLastLon = source.MapLastLon;
				target.MapLastHeading = source.MapLastHeading;
				target.MapLastPitch = source.MapLastPitch;
				target.MapLastZoom = source.MapLastZoom;
				target.IsMapCached = source.IsMapCached;
				target.IsTilesDownloadDesired = source.IsTilesDownloadDesired;
				target.MaxDesiredZoomForDownloadingTiles = source.MaxDesiredZoomForDownloadingTiles;
				target.TapTolerance = source.TapTolerance;
				target.IsShowDegrees = source.IsShowDegrees;
				target.IsKeepAlive = source.IsKeepAlive;
				target.IsShowingAltitudeProfiles = source.IsShowingAltitudeProfiles;
				target.SelectedPivotIndex = source.SelectedPivotIndex;
				target.IsShowingPivot = source.IsShowingPivot;
				target.IsBackButtonEnabled = source.IsBackButtonEnabled;
				target.IsAllowMeteredConnection = source.IsAllowMeteredConnection;

				TileSourceRecord.Clone(source.TestTileSource, ref target._testTileSource);
				target.RaisePropertyChanged(nameof(PersistentData.TestTileSource));

				TileSourceRecord.Clone(source.CurrentTileSource, ref target._currentTileSource);
				target.RaisePropertyChanged(nameof(PersistentData.CurrentTileSource));

				if (source.TileSourcez != null && source.TileSourcez.Count > 0) // start with the default values
				{
					foreach (var srcItem in source.TileSourcez.Where(a => a.IsDeletable)) // add custom map sources
					{
						target.TileSourcez.Add(new TileSourceRecord(srcItem.TechName, srcItem.DisplayName, srcItem.UriString, srcItem.ProviderUriString, srcItem.MinZoom, srcItem.MaxZoom, srcItem.TilePixelSize, srcItem.IsDeletable)); //srcItem.IsTesting, srcItem.IsValid));
					}
				}
				target.RaisePropertyChanged(nameof(PersistentData.TileSourcez));

				DownloadSession.Clone(source.LastDownloadSession, ref target._lastDownloadSession);
				target.RaisePropertyChanged(nameof(PersistentData.LastDownloadSession));
			}
		}
		static PersistentData()
		{
			var memUsageLimit = Windows.System.MemoryManager.AppMemoryUsageLimit; // 33966739456 on PC
			Logger.Add_TPL("mem usage limit = " + memUsageLimit, Logger.ForegroundLogFilename, Logger.Severity.Info);
			if (memUsageLimit < 1e+9) MaxRecordsInLandmarks = MaxLandmarks1;
			else if (memUsageLimit < 2e+9) MaxRecordsInLandmarks = MaxLandmarks2;
			else if (memUsageLimit < 4e+9) MaxRecordsInLandmarks = MaxLandmarks3;
			else if (memUsageLimit < 8e+9) MaxRecordsInLandmarks = MaxLandmarks4;
			else MaxRecordsInLandmarks = MaxLandmarks5;
			//Logger.Add_TPL("MaxRecordsInLandmarks = " + MaxRecordsInLandmarks, Logger.ForegroundLogFilename, Logger.Severity.Info);
		}
		private PersistentData()
		{
			_landmarks = new SwitchableObservableCollection<PointRecord>((uint)MaxRecordsInLandmarks);
		}

		/// <summary>
		/// Unlocks the TileCache DB.
		/// </summary>
		public static void OpenTileCacheDb()
		{
			TileCache.LolloSQLiteConnectionPoolMT.Open();
		}
		/// <summary>
		/// Unlocks the main DB.
		/// </summary>
		public static void OpenMainDb()
		{
			LolloSQLiteConnectionPoolMT.Open();
		}
		/// <summary>
		/// Only call this from a task, which is not the main one. 
		/// Otherwise, you will screw up the db open / closed logic.
		/// </summary>
		/// <param name="dbAction"></param>
		/// <returns></returns>
		public static Task<bool> RunDbOpInOtherTaskAsync(Func<bool> action)
		{
			return LolloSQLiteConnectionPoolMT.RunInOtherTaskAsync(action);
		}
		/// <summary>
		/// Waits for current DB operations to terminate and then locks the DB.
		/// </summary>
		public async static Task CloseTileCacheAsync()
		{
			await TileCache.LolloSQLiteConnectionPoolMT.CloseAsync().ConfigureAwait(false);
		}
		/// <summary>
		/// Waits for current DB operations to terminate and then locks the DB.
		/// </summary>
		public static void CloseMainDb()
		{
			// await LolloSQLiteConnectionPoolMT.Close().ConfigureAwait(false);
			LolloSQLiteConnectionPoolMT.Close();
		}
		#endregion construct dispose and clone

		#region properties
		private bool _isShowingPivot = false;
		[DataMember]
		public bool IsShowingPivot { get { return _isShowingPivot; } set { _isShowingPivot = value; RaisePropertyChanged_UI(); IsBackButtonEnabled = _isShowingPivot; } }

		private bool _isBackButtonEnabled = false;
		[DataMember]
		public bool IsBackButtonEnabled { get { return _isBackButtonEnabled; } set { _isBackButtonEnabled = value; RaisePropertyChanged_UI(); } }

		private int _selectedPivotIndex = -1;
		[DataMember]
		public int SelectedPivotIndex { get { return _selectedPivotIndex; } set { if (_selectedPivotIndex != value) { _selectedPivotIndex = value; RaisePropertyChanged(); } } }

		private bool _isShowingAltitudeProfiles = false;
		[DataMember]
		public bool IsShowingAltitudeProfiles { get { return _isShowingAltitudeProfiles; } set { if (_isShowingAltitudeProfiles != value) { _isShowingAltitudeProfiles = value; RaisePropertyChanged(); } } }

		private PointRecord _current = new PointRecord();
		[IgnoreDataMember] // we pick Current from History
		public PointRecord Current
		{
			get { return _current; }
			set
			{
				PointRecord oldValue = _current;
				_current = value;
				if (_current == null && oldValue == null) { }
				else if (_current == null && oldValue != null) { }
				else if (_current != null && oldValue == null) { RaisePropertyChanged_UI(); }
				else if (_current.Longitude != oldValue.Longitude || _current.Latitude != oldValue.Latitude || _current.TimePoint != oldValue.TimePoint) { RaisePropertyChanged_UI(); }
			}
		}
		private bool _isCentreOnCurrent = true;
		[DataMember]
		public bool IsCentreOnCurrent { get { return _isCentreOnCurrent; } set { if (_isCentreOnCurrent != value) { _isCentreOnCurrent = value; RaisePropertyChanged(); } } }
		private bool _isShowAim = false;
		[DataMember]
		public bool IsShowAim { get { return _isShowAim; } set { if (_isShowAim != value) { _isShowAim = value; RaisePropertyChanged(); } } }
		private bool _isShowAimOnce = false;
		[DataMember]
		public bool IsShowAimOnce { get { return _isShowAimOnce; } set { if (_isShowAimOnce != value) { _isShowAimOnce = value; RaisePropertyChanged(); } } }
		private PointRecord _selected = new PointRecord(); // { PositionSource = DefaultPositionSource };
		[DataMember]
		public PointRecord Selected { get { return _selected; } private set { _selected = value; RaisePropertyChanged(); } }
		private Tables _selectedSeries = Tables.nil;
		[DataMember]
		public Tables SelectedSeries { get { return _selectedSeries; } private set { _selectedSeries = value; RaisePropertyChanged(); } }
		private int _selectedIndex_Base1 = DefaultSelectedIndex_Base1;
		[DataMember]
		public int SelectedIndex_Base1 { get { return _selectedIndex_Base1; } private set { _selectedIndex_Base1 = value; RaisePropertyChanged(); } }
		private SwitchableObservableCollection<PointRecord> _history = new SwitchableObservableCollection<PointRecord>(MaxRecordsInHistory);
		//[DataMember] // we save the history into the DB now!
		[IgnoreDataMember]
		public SwitchableObservableCollection<PointRecord> History { get { return _history; } private set { _history = value; RaisePropertyChanged(); } }
		private SwitchableObservableCollection<PointRecord> _route0 = new SwitchableObservableCollection<PointRecord>(MaxRecordsInRoute);
		//[DataMember] // we save this into the DB so we don't serialise it anymore
		[IgnoreDataMember]
		public SwitchableObservableCollection<PointRecord> Route0 { get { return _route0; } private set { _route0 = value; RaisePropertyChanged(); } }
		private SwitchableObservableCollection<PointRecord> _landmarks = null; // new SwitchableObservableCollection<PointRecord>(MaxRecordsInLandmarks);
																			   //[DataMember] // we save this into the DB so we don't serialise it anymore
		[IgnoreDataMember]
		public SwitchableObservableCollection<PointRecord> Landmarks { get { return _landmarks; } private set { _landmarks = value; RaisePropertyChanged(); } }

		private uint _backgroundUpdatePeriodInMinutes = MinBackgroundUpdatePeriodInMinutes; //15u;  //TODO windows phone has 30 minutes, not 15: this may need fixing
		[DataMember]
		public uint BackgroundUpdatePeriodInMinutes
		{
			get { return _backgroundUpdatePeriodInMinutes; }
			set
			{
				uint oldValue = _backgroundUpdatePeriodInMinutes;
				if (value < MinBackgroundUpdatePeriodInMinutes)
				{
					_backgroundUpdatePeriodInMinutes = MinBackgroundUpdatePeriodInMinutes;
				}
				else
				{
					_backgroundUpdatePeriodInMinutes = value;
				}
				if (oldValue != _backgroundUpdatePeriodInMinutes)
				{
					RaisePropertyChanged();
				}
			}
		}
		private uint _desiredAccuracyInMeters = 10u; // 10 is high accuracy
		[DataMember]
		public uint DesiredAccuracyInMeters
		{
			get { return _desiredAccuracyInMeters; }
			set
			{
				uint oldValue = _desiredAccuracyInMeters;
				if (value < MinAccuracyInMetres)
				{
					_desiredAccuracyInMeters = MinAccuracyInMetres;
				}
				else
				{
					_desiredAccuracyInMeters = value;
				}
				if (_desiredAccuracyInMeters != oldValue)
				{
					RaisePropertyChanged();
				}
			}
		}
		private uint _reportIntervalInMilliSec = MinReportIntervalInMilliSec;
		[DataMember]
		public uint ReportIntervalInMilliSec
		{
			get { return _reportIntervalInMilliSec; }
			set
			{
				uint oldValue = _reportIntervalInMilliSec;
				if (value < MinReportIntervalInMilliSec)
				{
					_reportIntervalInMilliSec = MinReportIntervalInMilliSec;
				}
				else
				{
					_reportIntervalInMilliSec = value;
				}
				if (_reportIntervalInMilliSec != oldValue)
				{
					RaisePropertyChanged();
				}
			}
		}
		[IgnoreDataMember]
		public uint MinReportIntervalInMilliSecProp { get { return MinReportIntervalInMilliSec; } }
		[IgnoreDataMember]
		public uint MaxReportIntervalInMilliSecProp { get { return MaxReportIntervalInMilliSec; } }
		[IgnoreDataMember]
		public uint MinBackgroundUpdatePeriodInMinutesProp { get { return MinBackgroundUpdatePeriodInMinutes; } }
		[IgnoreDataMember]
		public uint MaxBackgroundUpdatePeriodInMinutesProp { get { return MaxBackgroundUpdatePeriodInMinutes; } }
		[IgnoreDataMember]
		public int MaxRecordsInLandmarksProp { get { return MaxRecordsInLandmarks; } }
		private string _lastMessage = string.Empty;
		[DataMember]
		public string LastMessage { get { return _lastMessage; } set { _lastMessage = value; RaisePropertyChanged_UI(); } }
		private bool _isShowSpeed = false;
		[DataMember]
		public bool IsShowSpeed { get { return _isShowSpeed; } set { _isShowSpeed = value; RaisePropertyChanged(); } }
		private bool _isTracking = false;
		[DataMember]
		public bool IsTracking { get { return _isTracking; } set { _isTracking = value; RaisePropertyChanged(); } }
		private volatile bool _isBackgroundEnabled = false;
		[DataMember]
		public bool IsBackgroundEnabled { get { return _isBackgroundEnabled; } set { _isBackgroundEnabled = value; RaisePropertyChanged(); } }
		private double _tapTolerance = 20.0;
		[DataMember]
		public double TapTolerance { get { return _tapTolerance; } set { _tapTolerance = value; RaisePropertyChanged(); } }
		private bool _isShowDegrees = false;
		[DataMember]
		public bool IsShowDegrees { get { return _isShowDegrees; } set { _isShowDegrees = value; RaisePropertyChanged(); } }
		private bool _isKeepAlive = false;
		[DataMember]
		public bool IsKeepAlive { get { return _isKeepAlive; } set { _isKeepAlive = value; RaisePropertyChanged(); } }
		private bool _isAllowMeteredConnection = false;
		[DataMember]
		public bool IsAllowMeteredConnection { get { return _isAllowMeteredConnection; } set { _isAllowMeteredConnection = value; RaisePropertyChanged(); } }

		private MapStyle _mapStyle = MapStyle.Terrain;
		[DataMember]
		public MapStyle MapStyle { get { return _mapStyle; } set { _mapStyle = value; RaisePropertyChanged(); } }
		private bool _isMapCached = false;
		[DataMember]
		public bool IsMapCached { get { return _isMapCached; } set { if (_isMapCached != value) { _isMapCached = value; RaisePropertyChanged(); } } }
		private double _mapLastLat = default(double);
		[DataMember]
		public double MapLastLat { get { return _mapLastLat; } set { _mapLastLat = value; RaisePropertyChanged(); } }
		private double _mapLastLon = default(double);
		[DataMember]
		public double MapLastLon { get { return _mapLastLon; } set { _mapLastLon = value; RaisePropertyChanged(); } }
		private double _mapLastZoom = 2.0;
		[DataMember]
		public double MapLastZoom { get { return _mapLastZoom; } set { _mapLastZoom = value; RaisePropertyChanged(); } }
		private double _mapLastHeading = 0.0;
		[DataMember]
		public double MapLastHeading { get { return _mapLastHeading; } set { _mapLastHeading = value; RaisePropertyChanged(); } }
		private double _mapLastPitch = 0.0;
		[DataMember]
		public double MapLastPitch { get { return _mapLastPitch; } set { _mapLastPitch = value; RaisePropertyChanged(); } }

		private volatile bool _isTilesDownloadDesired = false;
		[DataMember]
		public bool IsTilesDownloadDesired { get { return _isTilesDownloadDesired; } set { if (_isTilesDownloadDesired != value) { _isTilesDownloadDesired = value; RaisePropertyChanged_UI(); } } }
		private int _maxZoomForDownloadingTiles = -1;
		[DataMember]
		public int MaxDesiredZoomForDownloadingTiles { get { return _maxZoomForDownloadingTiles; } set { _maxZoomForDownloadingTiles = value; RaisePropertyChanged_UI(); } }
		public void SetIsTilesDownloadDesired(bool isTilesDownloadDesired, int maxZoom)
		{
			// we must handle these two variables together because they belong together.
			// an event handler registered on one and reading both may catch the change in the first before the second has changed.
			bool isIsTilesDownloadDesiredChanged = isTilesDownloadDesired != _isTilesDownloadDesired;
			bool isMaxZoomChanged = maxZoom != _maxZoomForDownloadingTiles;

			_isTilesDownloadDesired = isTilesDownloadDesired;
			_maxZoomForDownloadingTiles = maxZoom;

			if (isIsTilesDownloadDesiredChanged) RaisePropertyChanged_UI(nameof(PersistentData.IsTilesDownloadDesired));
			if (isMaxZoomChanged) RaisePropertyChanged_UI(nameof(PersistentData.MaxDesiredZoomForDownloadingTiles));
		}
		private volatile DownloadSession _lastDownloadSession;
		[DataMember]
		public DownloadSession LastDownloadSession { get { return _lastDownloadSession; } set { _lastDownloadSession = value; } }

		private PointRecord _target = new PointRecord() { PositionSource = DefaultPositionSource };
		[DataMember]
		public PointRecord Target { get { return _target; } private set { _target = value; RaisePropertyChanged(); } }

		private volatile SwitchableObservableCollection<TileSourceRecord> _tileSourcez = new SwitchableObservableCollection<TileSourceRecord>(TileSourceRecord.GetDefaultTileSources());
		[DataMember]
		public SwitchableObservableCollection<TileSourceRecord> TileSourcez { get { return _tileSourcez; } set { _tileSourcez = value; RaisePropertyChanged(); } }

		private TileSourceRecord _testTileSource = TileSourceRecord.GetSampleTileSource();
		[DataMember]
		public TileSourceRecord TestTileSource { get { return _testTileSource; } set { _testTileSource = value; RaisePropertyChanged(); } }

		private TileSourceRecord _currentTileSource = TileSourceRecord.GetDefaultTileSource();
		[DataMember]
		public TileSourceRecord CurrentTileSource { get { return _currentTileSource; } set { if (_currentTileSource == null || !_currentTileSource.IsEqualTo(value)) { _currentTileSource = value; RaisePropertyChanged(); } } }
		#endregion properties

		#region all series methods
		public Task LoadSeriesFromDbAsync(Tables whichTable, bool isShowMessageEvenIfSuccess = true)
		{
			switch (whichTable)
			{
				case Tables.History:
					return LoadHistoryFromDbAsync(isShowMessageEvenIfSuccess);
				case Tables.Route0:
					return LoadRoute0FromDbAsync(isShowMessageEvenIfSuccess);
				case Tables.Landmarks:
					return LoadLandmarksFromDbAsync(isShowMessageEvenIfSuccess);
				default:
					return Task.CompletedTask;
			}
		}
		/// <summary>
		/// Deletes the selected point from the series it belongs to.
		/// </summary>
		/// <returns></returns>
		public async Task<bool> DeleteSelectedPointFromSeriesAsync()
		{
			SwitchableObservableCollection<PointRecord> series = null;
			SemaphoreSlimSafeRelease semaphore = null;
			if (Selected != null)
			{
				if (_selectedSeries == Tables.History)
				{
					series = History;
					semaphore = _historySemaphore;
				}
				else if (_selectedSeries == Tables.Route0)
				{
					series = Route0;
					semaphore = _route0Semaphore;
				}
				else if (_selectedSeries == Tables.Landmarks)
				{
					series = Landmarks;
					semaphore = _landmarksSemaphore;
				}
			}
			if (series != null && semaphore != null && Selected != null)
			{
				try
				{
					await semaphore.WaitAsync();
					var matchingPointInSeries = series.FirstOrDefault(a => a.Latitude == Selected.Latitude && a.Longitude == Selected.Longitude);
					if (matchingPointInSeries != null)
					{
						if (series.IndexOf(matchingPointInSeries) == 0)
						{
							SelectNeighbourRecordFromAnySeries(1);
						}
						else SelectNeighbourRecordFromAnySeries(-1);

						if (series.Remove(matchingPointInSeries))
						{
							Task delete = null;
							if (_selectedSeries == Tables.History) delete = DBManager.DeleteFromHistoryAsync(matchingPointInSeries);
							else if (_selectedSeries == Tables.Route0) delete = DBManager.DeleteFromRoute0Async(matchingPointInSeries);
							else if (_selectedSeries == Tables.Landmarks) delete = DBManager.DeleteFromLandmarksAsync(matchingPointInSeries);

							// if I have removed the last record from the series
							if (series.Count == 0)
							{
								Selected = new PointRecord();
								SelectedIndex_Base1 = DefaultSelectedIndex_Base1;
								SelectedSeries = Tables.nil;
								LastMessage = "Series deleted";
								return true;
							}
							else
							{
								SelectNeighbourRecordFromAnySeries(0);
								LastMessage = "Series updated";
								return true;
							}
						}
						else
						{
							LastMessage = "Error updating data";
							return false;
						}
					}
					else
					{
						LastMessage = "Error updating data";
						return false;
					}
				}
				catch (OutOfMemoryException) // TODO this should never happen. If it does, lower MaxRecordsInLandmarks
				{
					var howMuchMemoryLeft = GC.GetTotalMemory(true);
					Logger.Add_TPL("OutOfMemoryException in PersistentData.RemovePointFromSeries()", Logger.PersistentDataLogFilename);
				}
				catch (Exception ex)
				{
					Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
				}
				finally
				{
					SemaphoreSlimSafeRelease.TryRelease(semaphore);
				}
			}
			LastMessage = "Error updating data";
			return false;
		}
		public async Task RunFunctionUnderSemaphore(Action func, Tables whichTable)
		{
			SemaphoreSlimSafeRelease semaphore = null;
			switch (whichTable)
			{
				case Tables.History:
					semaphore = _historySemaphore;
					break;
				case Tables.Route0:
					semaphore = _route0Semaphore;
					break;
				case Tables.Landmarks:
					semaphore = _landmarksSemaphore;
					break;
				default:
					return;
			}
			try
			{
				await semaphore.WaitAsync(); //.ConfigureAwait(false);
				func();
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(semaphore);
			}
		}
		#endregion all series methods

		#region historyMethods
		public static List<PointRecord> GetHistoryFromDB()
		{
			return DBManager.GetHistory();
		}
		public async Task LoadHistoryFromDbAsync(bool isShowMessageEvenIfSuccess = true)
		{
			List<PointRecord> dataRecords = await DBManager.GetHistoryAsync().ConfigureAwait(false);

			try
			{
				await _historySemaphore.WaitAsync().ConfigureAwait(false);

				await RunInUiThreadAsync(delegate
				{
					_history.Clear();

					if (dataRecords != null)
					{
						try
						{
							if (isShowMessageEvenIfSuccess) LastMessage = "History updated";
							_history.AddRange(dataRecords.Where(a => !a.IsEmpty()));
						}
						catch (IndexOutOfRangeException)
						{
							LastMessage = "Only part of the history is drawn";
						}
						catch (OutOfMemoryException) // TODO this should never happen. If it does, lower MaxRecordsInHistory
						{
							var howMuchMemoryLeft = GC.GetTotalMemory(true);
							LastMessage = "Only part of the history is drawn";
							Logger.Add_TPL("OutOfMemoryException in PersistentData.SetHistory()", Logger.PersistentDataLogFilename);
						}
					}

					SetCurrentToLast();
				}).ConfigureAwait(false);
			}
			catch (Exception exc0)
			{
				await Logger.AddAsync(exc0.ToString(), Logger.PersistentDataLogFilename).ConfigureAwait(false);
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(_historySemaphore);
			}
		}
		public async Task ResetHistoryAsync()
		{
			try
			{
				await _historySemaphore.WaitAsync();
				History.Clear();
				await DBManager.DeleteAllFromHistoryAsync().ConfigureAwait(false);
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(_historySemaphore);
			}
		}
		public async Task<bool> AddHistoryRecordAsync(PointRecord dataRecord, bool checkMaxEntries)
		{
			try
			{
				await _historySemaphore.WaitAsync();

				bool result = false;
				await RunInUiThreadAsync(delegate
				{
					result = AddHistoryRecord2(dataRecord, checkMaxEntries);
				}).ConfigureAwait(false);
				return result;
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(_historySemaphore);
			}
		}
		public bool AddHistoryRecordOnlyDb(PointRecord dataRecord, bool checkMaxEntries)
		{
			try
			{
				_historySemaphore.Wait();
				bool isOk = AddHistoryRecord2OnlyDb(dataRecord, checkMaxEntries);
				return isOk;
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(_historySemaphore);
			}
		}

		private bool AddHistoryRecord2(PointRecord dataRecord, bool checkMaxEntries)
		{
			if (dataRecord != null && !dataRecord.IsEmpty() && History.Count < MaxRecordsInHistory)
			{
				try
				{
					//int index = GetIndexCheckingDateAscending(dataRecord, this); // no more needed if we update on the go!
					//History.Insert(index, dataRecord);
					_history.Add(dataRecord); // we don't need to need to clone the record first, if the callers always instantiate a new record

					Task insert = DBManager.InsertIntoHistoryAsync(dataRecord, checkMaxEntries);

					Current = dataRecord;
					LastMessage = dataRecord.Status;

					CurrentChanged?.Invoke(this, EventArgs.Empty);
					return true;
				}
				catch (IndexOutOfRangeException)
				{
					LastMessage = "Too many records in trk history, max is " + MaxRecordsInHistory;
					return false;
				}
				catch (OutOfMemoryException) // TODO this should never happen. If it does, lower MaxRecordsInHistory
				{
					var howMuchMemoryLeft = GC.GetTotalMemory(true);
					LastMessage = "too many records in trk history";
					Logger.Add_TPL("OutOfMemoryException in PersistentData.AddHistoryRecord()", Logger.PersistentDataLogFilename);
					return false;
				}
			}
			else
			{
				LastMessage = "could not get a fix";
				return false;
			}
		}
		private bool AddHistoryRecord2OnlyDb(PointRecord dataRecord, bool checkMaxEntries)
		{
			if (dataRecord != null && !dataRecord.IsEmpty() && History.Count < MaxRecordsInHistory)
			{
				try
				{
					bool isOk = DBManager.InsertIntoHistory(dataRecord, checkMaxEntries);
					return isOk;
				}
				catch (IndexOutOfRangeException ex0)
				{
					Logger.Add_TPL(ex0.ToString(), Logger.PersistentDataLogFilename);
					return false;
				}
				catch (OutOfMemoryException ex1) // TODO this should never happen. If it does, lower MaxRecordsInHistory
				{
					var howMuchMemoryLeft = GC.GetTotalMemory(true);
					Logger.Add_TPL(ex1.ToString(), Logger.PersistentDataLogFilename);
					return false;
				}
			}
			else
			{
				return false;
			}
		}

		private void SetCurrentToLast()
		{
			if (History != null && History.Count > 0)
			{
				PointRecord newCurrent = History[History.Count - 1];
				Current = newCurrent;
			}
		}
		#endregion historyMethods

		#region route0Methods
		public static List<PointRecord> GetRoute0FromDB()
		{
			return DBManager.GetRoute0();
		}
		public async Task LoadRoute0FromDbAsync(bool isShowMessageEvenIfSuccess = true)
		{
			List<PointRecord> dataRecords = await DBManager.GetRoute0Async().ConfigureAwait(false);

			try
			{
				await _route0Semaphore.WaitAsync().ConfigureAwait(false);

				await RunInUiThreadAsync(delegate
				{
					_route0.Clear();

					if (dataRecords != null)
					{
						try
						{
							if (isShowMessageEvenIfSuccess) LastMessage = "Route updated";
							_route0.AddRange(dataRecords.Where(a => !a.IsEmpty()));
						}
						catch (IndexOutOfRangeException)
						{
							LastMessage = "Only part of the route is drawn";
						}
						catch (OutOfMemoryException) // TODO this should never happen. If it does, lower MaxRecordsInRoute
						{
							var howMuchMemoryLeft = GC.GetTotalMemory(true);
							LastMessage = "Only part of the route is drawn";
							Logger.Add_TPL("OutOfMemoryException in PersistentData.SetRoute0()", Logger.PersistentDataLogFilename);
						}
					}
				}).ConfigureAwait(false);
			}
			catch (Exception exc0)
			{
				await Logger.AddAsync(exc0.ToString(), Logger.PersistentDataLogFilename).ConfigureAwait(false);
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(_route0Semaphore);
			}
		}
		public static Task SetRoute0InDBAsync(IEnumerable<PointRecord> dataRecords)
		{
			return DBManager.ReplaceRoute0Async(dataRecords, true);
		}
		public async Task ResetRoute0Async()
		{
			try
			{
				await _route0Semaphore.WaitAsync();
				Route0.Clear();
				await DBManager.DeleteAllFromRoute0Async().ConfigureAwait(false);
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(_route0Semaphore);
			}
		}
		#endregion route0Methods

		#region landmarksMethods
		public static List<PointRecord> GetLandmarksFromDB()
		{
			return DBManager.GetLandmarks();
		}
		public async Task LoadLandmarksFromDbAsync(bool isShowMessageEvenIfSuccess = true)
		{
			List<PointRecord> dataRecords = await DBManager.GetLandmarksAsync().ConfigureAwait(false);

			try
			{
				await _landmarksSemaphore.WaitAsync().ConfigureAwait(false);

				await RunInUiThreadAsync(delegate
				{
					_landmarks.Clear();

					if (dataRecords != null)
					{
						try
						{
							if (isShowMessageEvenIfSuccess) LastMessage = "Landmarks updated";
							_landmarks.AddRange(dataRecords.Where(a => !a.IsEmpty()));
						}
						catch (IndexOutOfRangeException)
						{
							LastMessage = "Only some landmarks are drawn";
						}
						catch (OutOfMemoryException) // TODO this should never happen. If it does, lower MaxRecordsInLandmarks
						{
							var howMuchMemoryLeft = GC.GetTotalMemory(true);
							LastMessage = "Only some landmarks are drawn";
							Logger.Add_TPL("OutOfMemoryException in PersistentData.SetLandmarks()", Logger.PersistentDataLogFilename);
						}
					}
				}).ConfigureAwait(false);
			}
			catch (Exception exc0)
			{
				await Logger.AddAsync(exc0.ToString(), Logger.PersistentDataLogFilename).ConfigureAwait(false);
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(_landmarksSemaphore);
			}
		}
		public static Task SetLandmarksInDBAsync(IEnumerable<PointRecord> dataRecords)
		{
			return DBManager.ReplaceLandmarksAsync(dataRecords, true);
		}
		public async Task ResetLandmarksAsync()
		{
			try
			{
				await _landmarksSemaphore.WaitAsync();
				Landmarks.Clear();
				await DBManager.DeleteAllFromLandmarksAsync().ConfigureAwait(false);
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(_landmarksSemaphore);
			}
		}
		public async Task<bool> TryAddPointToLandmarksAsync(PointRecord point)
		{
			if (point != null && !point.IsEmpty())
			{
				try
				{
					await _landmarksSemaphore.WaitAsync();
					var samePointInLandmarks = Landmarks.FirstOrDefault(a => a.Latitude == point.Latitude && a.Longitude == point.Longitude);
					if (samePointInLandmarks != null)
					{
						int index = Landmarks.IndexOf(samePointInLandmarks);
						if (index > -1)
						{
							Landmarks[index] = point;
							Task update = DBManager.UpdateLandmarksAsync(point, false);
							LastMessage = "Landmarks updated";
							return true;
						}
						else
						{
							LastMessage = "Error updating landmarks";
							return false;
						}
					}
					else
					{
						if (Landmarks.Count < MaxRecordsInLandmarks)
						{
							Landmarks.Add(point);
							Task insert = DBManager.InsertIntoLandmarksAsync(point, false);
							LastMessage = "Target added to landmarks";
							return true;
						}
						else
						{
							LastMessage = string.Format("Too many landmarks, max is {0}", MaxRecordsInLandmarks);
							return false;
						}
					}
				}
				catch (IndexOutOfRangeException)
				{
					LastMessage = string.Format("Too many landmarks, max is {0}", MaxRecordsInLandmarks);
					return false;
				}
				catch (OutOfMemoryException) // TODO this should never happen. If it does, lower MaxRecordsInLandmarks
				{
					var howMuchMemoryLeft = GC.GetTotalMemory(true);
					LastMessage = string.Format("Too many landmarks, max is {0}", MaxRecordsInLandmarks);
					Logger.Add_TPL("OutOfMemoryException in PersistentData.TryAddTargetToLandmarks()", Logger.PersistentDataLogFilename);
					return false;
				}
				finally
				{
					SemaphoreSlimSafeRelease.TryRelease(_landmarksSemaphore);
				}
			}
			LastMessage = "Error updating landmarks";
			return false;
		}
		public Task<bool> TryAddTargetToLandmarksAsync()
		{
			PointRecord targetClone = null;
			PointRecord.Clone(Target, ref targetClone);
			return TryAddPointToLandmarksAsync(targetClone);
		}
		#endregion landmarksMethods

		#region selectedRecordMethods
		public bool IsSelectedRecordFromAnySeriesFirst()
		{
			if (_selectedSeries == Tables.nil || _selected == null) return false;
			else if (_selectedSeries == Tables.History && _history.Count > 0) return _history[0].Equals(_selected);
			else if (_selectedSeries == Tables.Route0 && _route0.Count > 0) return _route0[0].Equals(_selected);
			else if (_selectedSeries == Tables.Landmarks && _landmarks.Count > 0) return _landmarks[0].Equals(_selected);
			else return false;
		}
		public bool IsSelectedRecordFromAnySeriesLast()
		{
			if (_selectedSeries == Tables.nil || _selected == null) return false;
			else if (_selectedSeries == Tables.History && _history.Count > 0) return _history[_history.Count - 1].Equals(_selected);
			else if (_selectedSeries == Tables.Route0 && _route0.Count > 0) return _route0[_route0.Count - 1].Equals(_selected);
			else if (_selectedSeries == Tables.Landmarks && _landmarks.Count > 0) return _landmarks[_landmarks.Count - 1].Equals(_selected);
			else return false;
		}
		public bool IsSelectedSeriesNonNullAndNonEmpty()
		{
			if (_selectedSeries == Tables.nil || _selected == null) return false;
			else if (_selectedSeries == Tables.History) return _history.Count > 0;
			else if (_selectedSeries == Tables.Route0) return _route0.Count > 0;
			else if (_selectedSeries == Tables.Landmarks) return _landmarks.Count > 0;
			else return false;
		}
		public void SelectRecordFromSeries(PointRecord dataRecord, PersistentData.Tables whichTable, int index = -1)
		{
			if (dataRecord != null && !dataRecord.IsEmpty())
			{
				Selected = dataRecord;
				SelectedSeries = whichTable;
				if (index == -1)
				{
					if (whichTable == Tables.History) SelectedIndex_Base1 = History.IndexOf(dataRecord) + 1;
					else if (whichTable == Tables.Route0) SelectedIndex_Base1 = Route0.IndexOf(dataRecord) + 1;
					else if (whichTable == Tables.Landmarks) SelectedIndex_Base1 = Landmarks.IndexOf(dataRecord) + 1;
					else SelectedIndex_Base1 = DefaultSelectedIndex_Base1;
				}
				else
				{
					SelectedIndex_Base1 = index + 1;
				}
			}
			else if (Current != null && !Current.IsEmpty()) // fallback
			{
				Selected = Current;
				SelectedSeries = whichTable;
			}
		}
		public void SelectNeighbourRecordFromAnySeries(int step)
		{
			if (_selectedSeries == Tables.nil || Selected == null) return;
			else if (_selectedSeries == Tables.History) SelectNeighbourRecord(_history, Tables.History, step);
			else if (_selectedSeries == Tables.Route0) SelectNeighbourRecord(_route0, Tables.Route0, step);
			else if (_selectedSeries == Tables.Landmarks) SelectNeighbourRecord(_landmarks, Tables.Landmarks, step);
		}
		public PointRecord GetRecordBeforeSelectedFromAnySeries()
		{
			if (_selectedSeries == Tables.nil || Selected == null || SelectedIndex_Base1 - 2 < 0) return null;
			else if (_selectedSeries == Tables.History) return History[SelectedIndex_Base1 - 2];
			else if (_selectedSeries == Tables.Route0) return Route0[SelectedIndex_Base1 - 2];
			else if (_selectedSeries == Tables.Landmarks) return Landmarks[SelectedIndex_Base1 - 2];
			return null;
		}
		private void SelectNeighbourRecord(Collection<PointRecord> series, PersistentData.Tables whichSeries, int step)
		{
			int newIndex = series.IndexOf(Selected) + step;
			if (newIndex < 0) newIndex = 0;
			if (newIndex >= series.Count) newIndex = series.Count - 1;
			if (series.Count > newIndex && newIndex >= 0) SelectRecordFromSeries(series[newIndex], whichSeries, newIndex);

		}
		#endregion selectedRecordMethods

		#region tileSourcesMethods
		/// <summary>
		/// Checks TestTileSource and, if good, adds it to TileSourcez and sets CurrentTileSource to it.
		/// Note that the user might press "test" multiple times, so I may clutter TileSourcez with test records.
		/// </summary>
		/// <returns></returns>
		public async Task<Tuple<bool, string>> TryInsertTestTileSourceIntoTileSourcezAsync()
		{
			try
			{
				await _tileSourcezSemaphore.WaitAsync();
				if (TestTileSource == null) return Tuple.Create<bool, string>(false, "Record not found");
				if (string.IsNullOrWhiteSpace(TestTileSource.TechName)) return Tuple.Create<bool, string>(false, "Name is empty");
				// set non-screen properties
				TestTileSource.DisplayName = TestTileSource.TechName; // we always set it automatically
				TestTileSource.IsDeletable = true;
				string errorMsg = TestTileSource.Check(true);

				if (!string.IsNullOrEmpty(errorMsg))
				{
					return Tuple.Create<bool, string>(false, errorMsg);
				}
				else
				{
					string successMessage = string.Format("Source {0} added", TestTileSource.DisplayName);
					var recordsWithSameName = TileSourcez.Where(a => a.TechName == TestTileSource.TechName || a.DisplayName == TestTileSource.TechName);
					if (recordsWithSameName != null)
					{
						if (recordsWithSameName.Count() > 1)
						{
							return Tuple.Create<bool, string>(false, string.Format("Tile source {0} cannot be changed", TestTileSource.TechName));
						}
						else if (recordsWithSameName.Count() == 1)
						{
							var recordWithSameName = recordsWithSameName.First();
							if (recordWithSameName.IsDeletable)
							{
								TileSourcez.Remove(recordWithSameName);
								successMessage = string.Format("Source {0} changed", TestTileSource.DisplayName);
							}
							else
							{
								return Tuple.Create<bool, string>(false, string.Format("Tile source {0} cannot be changed", TestTileSource.TechName));
							}
						}
					}

					TileSourceRecord newRecord = null;
					TileSourceRecord.Clone(TestTileSource, ref newRecord); // do not overwrite the current instance
					TileSourcez.Add(newRecord);
					RaisePropertyChanged(nameof(PersistentData.TileSourcez));
					CurrentTileSource = newRecord;

					return Tuple.Create<bool, string>(true, successMessage);
				}
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(_tileSourcezSemaphore);
			}
		}
		public async Task RemoveTileSourcesAsync(TileSourceRecord tileSource)
		{
			try
			{
				await _tileSourcezSemaphore.WaitAsync().ConfigureAwait(false);
				await RunInUiThreadAsync(delegate
				{
					if (tileSource.IsAll)
					{
						Collection<TileSourceRecord> tsTBDeleted = new Collection<TileSourceRecord>();
						foreach (var item in TileSourcez.Where(a => a.IsDeletable))
						{
							// restore default if removing current tile source
							if (CurrentTileSource.TechName == item.TechName) CurrentTileSource = TileSourceRecord.GetDefaultTileSource();
							tsTBDeleted.Add(item);
							// TileSourcez.Remove(item); // nope, it dumps if you modify a collection while looping over it
						}
						foreach (var item in tsTBDeleted)
						{
							TileSourcez.Remove(item);
						}
					}
					else if (tileSource.IsDeletable)
					{
						// restore default if removing current tile source
						if (CurrentTileSource.TechName == tileSource.TechName) CurrentTileSource = TileSourceRecord.GetDefaultTileSource();
						TileSourcez.Remove(tileSource);
					}
					RaisePropertyChanged(nameof(TileSourcez));
				}).ConfigureAwait(false);
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(_tileSourcezSemaphore);
			}
		}
		#endregion tileSourcesMethods

		#region otherMethods
		//public void SetTargetToGeopoint(PointRecord dr)
		//{
		//	if (dr != null)
		//	{
		//		PointRecord.Clone(dr, ref _target);
		//		RaisePropertyChanged(nameof(PersistentData.Target));
		//	}
		//}
		//public void SetTargetToGeopoint(BasicGeoposition gp)
		//{
		//    Target = new PointRecord()
		//    {
		//        Latitude = gp.Latitude,
		//        Longitude = gp.Longitude,
		//        Altitude = gp.Altitude,
		//    };
		//	RaisePropertyChanged(nameof(PersistentData.Target));
		//}
		public void CycleMapStyle()
		{
			switch (MapStyle)
			{
				case MapStyle.Aerial:
					MapStyle = MapStyle.None; break;
				case MapStyle.AerialWithRoads:
					MapStyle = MapStyle.None; break;
				case MapStyle.None:
					MapStyle = MapStyle.Terrain; break;
				case MapStyle.Road:
					MapStyle = MapStyle.Aerial; break;
				case MapStyle.Terrain:
					MapStyle = MapStyle.Aerial; break;
				default:
					break;
			}
		}
		//private static int GetIndexCheckingDateAscending(PointRecord dataRecord, PersistentData myData)
		//{
		//    int index = 0;
		//    if (myData.History.Count > 0)
		//    {
		//        try
		//        {
		//            index = myData.History.IndexOf(myData.History.Last(a => a.TimePoint < dataRecord.TimePoint)) + 1;
		//        }
		//        catch (Exception)
		//        {
		//            index = 0;
		//            Logger.Add_TPL("ERROR: IndexOf could not find anything prior to the current record", Logger.PersistentDataLogFilename);
		//        }
		//    }
		//    if (index < 0)
		//    {
		//        Logger.Add_TPL("ERROR: index = " + index + " but it cannot be < 0", Logger.PersistentDataLogFilename);
		//        Debug.WriteLine("ERROR: index = " + index + " but it cannot be < 0");
		//        index = 0;
		//    }
		//    if (index > myData.History.Count)
		//    {
		//        Logger.Add_TPL("ERROR: index = " + index + " but it cannot be > History.Count = ", Logger.PersistentDataLogFilename);
		//        Debug.WriteLine("ERROR: index = " + index + " but it cannot be > History.Count = " + myData.History.Count);
		//        index = myData.History.Count;
		//    }
		//    return index;
		//}
		#endregion otherMethods
	}

	#region conversion
	public static class AngleConverterHelper
	{
		public const int MaxDecimalPlaces = 3;
		public const int TenPowerMaxDecimalPlaces = 1000;
		public static string Float_To_DegMinSec_NoDec_String(object value, object parameter)
		{
			int deg;
			int min;
			int sec;
			int dec;
			Float_To_DegMinSecDec(value, parameter, out deg, out min, out sec, out dec);
			return (deg + "°" + min + "'" + sec + "\"") as string; //we skip dec
		}

		public static string[] Float_To_DegMinSecDec_Array(object value, object parameter)
		{
			int deg;
			int min;
			int sec;
			int dec;
			Float_To_DegMinSecDec(value, parameter, out deg, out min, out sec, out dec);
			string[] strArray = new string[4];
			strArray[0] = deg.ToString();
			strArray[1] = min.ToString();
			strArray[2] = sec.ToString();
			strArray[3] = dec.ToString();
			return strArray;
		}

		private static void Float_To_DegMinSecDec(object value, object parameter, out int deg, out int min, out int sec, out int dec)
		{
			Double coord = 0.0;
			if (Double.TryParse(value.ToString(), out coord))
			{
				deg = (int)Math.Truncate(coord);
				min = (int)Math.Abs(Math.Truncate((coord - deg) * 60.0));
				Double secDbl = Math.Abs(Math.Abs(coord - deg) * 3600 - min * 60);
				sec = (Int32)secDbl;
				dec = (Int32)((secDbl - sec) * TenPowerMaxDecimalPlaces);

				Debug.WriteLine(coord);
				Int32 sign = Math.Sign(deg);
				if (sign == 0) sign = 1;
				Debug.WriteLine(sign * Math.Abs(dec) / (Double)TenPowerMaxDecimalPlaces / 3600.0 + sign * Math.Abs(sec) / 3600.0 + sign * Math.Abs(min) / 60.0 + deg); //this is the inverse function, by the way
			}
			else
			{
				deg = min = sec = dec = 0;
				Debug.WriteLine("ERROR: double expected");
			}
			//if (parameter != null) sec = Math.Round(sec, System.Convert.ToInt32(parameter.ToString())); // in case we need this again in future...
		}

		public static Double DegMinSecDec_To_Float(string degStr, string minStr, string secStr, string decStr)
		{
			Int32 deg = 0;
			Int32.TryParse(degStr, out deg);
			Int32 min = 0;
			Int32.TryParse(minStr, out min);
			Int32 sec = 0;
			Int32.TryParse(secStr, out sec);
			Int32 dec = 0;
			Int32.TryParse(decStr, out dec);
			Int32 sign = Math.Sign(deg);
			if (sign == 0) sign = 1;
			return sign * Math.Abs(dec) / (Double)TenPowerMaxDecimalPlaces / 3600.0 + sign * Math.Abs(sec) / 3600.0 + sign * Math.Abs(min) / 60.0 + deg;
		}
	}
	#endregion conversion
}