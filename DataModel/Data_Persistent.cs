﻿using Utilz.Data;
using LolloGPS.Data.Leeching;
using SQLite;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Utilz;
using Windows.UI.Xaml.Controls.Maps;


// There is a sqlite walkthrough at:
// http://social.technet.microsoft.com/wiki/contents/articles/29149.windows-phone-8-1-sqlite-part-one.aspx

namespace LolloGPS.Data
{
	[DataContract]
	public sealed class PersistentData : ObservableData, IGPSDataModel //, INotifyDataErrorInfo //does not work
	{
		public enum Tables { History, Route0, Checkpoints, nil }
		public static string GetTextForSeries(Tables whichSeries)
		{
			switch (whichSeries)
			{
				case Tables.History:
					return "Tracking history";
				case Tables.Route0:
					return "Route";
				case Tables.Checkpoints:
					return "Checkpoints";
				case Tables.nil:
					return "No series";
				default:
					return "";
			}
		}
		public const int MaxRecordsInRoute = short.MaxValue;
		public const int MaxRecordsInHistory = short.MaxValue;
		private const int MaxCheckpoints1 = 100;
		private const int MaxCheckpoints2 = 200;
		private const int MaxCheckpoints3 = 500;
		private const int MaxCheckpoints4 = 1000;

		public static readonly int MaxRecordsInCheckpoints = MaxCheckpoints4;

		public const uint MinBackgroundUpdatePeriodInMinutes = 15u;
		public const uint MaxBackgroundUpdatePeriodInMinutes = 120u;
		public const uint DefaultBackgroundUpdatePeriodInMinutes = 15u;
		public const uint MinReportIntervalInMilliSec = 3000u;
		public const uint MaxReportIntervalInMilliSec = 900000u;
		public const uint DefaultReportIntervalInMilliSec = 3000u;
		public const uint MinDesiredAccuracyInMetres = 1u;
		public const uint MaxDesiredAccuracyInMetres = 100u;
		public const uint DefaultDesiredAccuracyInMetres = 10u; // high accuracy
		public uint GetDefaultDesiredAccuracyInMetres { get { return DefaultDesiredAccuracyInMetres; } }
		public const double MinTapTolerance = 1.0;
		public const double MaxTapTolerance = 50.0;
		public const double DefaultTapTolerance = 20.0;

		public const string DefaultPositionSource = ConstantData.APPNAME;
		private const int DefaultSelectedIndex_Base1 = 0;

		private static SemaphoreSlimSafeRelease _historySemaphore = new SemaphoreSlimSafeRelease(1, 1);
		private static SemaphoreSlimSafeRelease _route0Semaphore = new SemaphoreSlimSafeRelease(1, 1);
		private static SemaphoreSlimSafeRelease _checkpointsSemaphore = new SemaphoreSlimSafeRelease(1, 1);
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

		public static Task SetInstanceNonDbPropertiesAsync(PersistentData from)
		{
			try
			{
				return RunInUiThreadAsync(delegate
				{
					var dataToBeChanged = GetInstance();
					//I must clone memberwise, otherwise the current event handlers get lost
					CloneNonDbProperties_internal(from, ref dataToBeChanged);
				});
			}
			catch (Exception ex)
			{
				return Logger.AddAsync(ex.ToString(), Logger.PersistentDataLogFilename);
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
				target.RaisePropertyChanged(nameof(Selected));

				if (!source.Target.IsEmpty()) PointRecord.Clone(source.Target, ref target._target);
				target.RaisePropertyChanged(nameof(Target));

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
				target.AltLastVScroll = source.AltLastVScroll;
				target.IsShowImperialUnits = source.IsShowImperialUnits;

				TileSourceRecord.Clone(source.TestTileSource, ref target._testTileSource);
				target.RaisePropertyChanged(nameof(TestTileSource));

				TileSourceRecord.Clone(source.CurrentTileSource, ref target._currentTileSource);
				target.RaisePropertyChanged(nameof(CurrentTileSource));

				if (source.TileSourcez != null && source.TileSourcez.Count > 0) // start with the default values
				{
					foreach (var srcItem in source.TileSourcez.Where(tileSource => tileSource.IsDeletable)) // add custom map sources
					{
						target.TileSourcez.Add(new TileSourceRecord(srcItem.TechName, srcItem.DisplayName, srcItem.UriString, srcItem.ProviderUriString, srcItem.MinZoom, srcItem.MaxZoom, srcItem.TilePixelSize, srcItem.IsDeletable)); //srcItem.IsTesting, srcItem.IsValid));
					}
				}
				target.RaisePropertyChanged(nameof(TileSourcez));

				DownloadSession.Clone(source.LastDownloadSession, ref target._lastDownloadSession);
				target.RaisePropertyChanged(nameof(LastDownloadSession));
			}
		}
		static PersistentData()
		{
			var memUsageLimit = Windows.System.MemoryManager.AppMemoryUsageLimit; // 33966739456 on PC
			Logger.Add_TPL("mem usage limit = " + memUsageLimit, Logger.AppEventsLogFilename, Logger.Severity.Info);
			if (memUsageLimit < 1e+9) MaxRecordsInCheckpoints = MaxCheckpoints1;
			else if (memUsageLimit < 2e+9) MaxRecordsInCheckpoints = MaxCheckpoints2;
			else if (memUsageLimit < 4e+9) MaxRecordsInCheckpoints = MaxCheckpoints3;
			else MaxRecordsInCheckpoints = MaxCheckpoints4;
		}
		private PersistentData()
		{
			_checkpoints = new SwitchableObservableCollection<PointRecord>((uint)MaxRecordsInCheckpoints);
		}

		public static Task OpenTileCacheDbAsync()
		{
			return TileCache.LolloSQLiteConnectionPoolMT.OpenAsync();
		}

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
		public static bool RunDbOpInOtherTask(Func<bool> action)
		{
			return LolloSQLiteConnectionPoolMT.RunInOtherTask(action);
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
		public int SelectedPivotIndex { get { return _selectedPivotIndex; } set { if (_selectedPivotIndex != value) { _selectedPivotIndex = value; RaisePropertyChanged_UI(); } } }

		private bool _isShowingAltitudeProfiles = false;
		[DataMember]
		public bool IsShowingAltitudeProfiles { get { return _isShowingAltitudeProfiles; } set { if (_isShowingAltitudeProfiles != value) { _isShowingAltitudeProfiles = value; RaisePropertyChanged_UI(); } } }

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
		public bool IsCentreOnCurrent { get { return _isCentreOnCurrent; } set { if (_isCentreOnCurrent != value) { _isCentreOnCurrent = value; RaisePropertyChanged_UI(); } } }
		private bool _isShowAim = false;
		[DataMember]
		public bool IsShowAim { get { return _isShowAim; } set { if (_isShowAim != value) { _isShowAim = value; RaisePropertyChanged_UI(); } } }
		private bool _isShowAimOnce = false;
		[DataMember]
		public bool IsShowAimOnce { get { return _isShowAimOnce; } set { if (_isShowAimOnce != value) { _isShowAimOnce = value; RaisePropertyChanged_UI(); } } }
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
		[IgnoreDataMember] // we save the history into the DB 
		public SwitchableObservableCollection<PointRecord> History { get { return _history; } private set { _history = value; RaisePropertyChanged(); } }
		private SwitchableObservableCollection<PointRecord> _route0 = new SwitchableObservableCollection<PointRecord>(MaxRecordsInRoute);
		[IgnoreDataMember] // we save the route into the DB 
		public SwitchableObservableCollection<PointRecord> Route0 { get { return _route0; } private set { _route0 = value; RaisePropertyChanged(); } }
		private SwitchableObservableCollection<PointRecord> _checkpoints = null; // new SwitchableObservableCollection<PointRecord>(MaxRecordsInCheckpoints); // we init this in the static ctor
		[IgnoreDataMember] // we save the checkpoints into the DB 
		public SwitchableObservableCollection<PointRecord> Checkpoints { get { return _checkpoints; } private set { _checkpoints = value; RaisePropertyChanged(); } }

		private uint _backgroundUpdatePeriodInMinutes = DefaultBackgroundUpdatePeriodInMinutes;
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
				else if (value > MaxBackgroundUpdatePeriodInMinutes)
				{
					_backgroundUpdatePeriodInMinutes = MinBackgroundUpdatePeriodInMinutes;
				}
				else
				{
					_backgroundUpdatePeriodInMinutes = value;
				}
				if (oldValue != _backgroundUpdatePeriodInMinutes)
				{
					RaisePropertyChanged_UI();
				}
			}
		}
		private uint _desiredAccuracyInMeters = DefaultDesiredAccuracyInMetres;
		[DataMember]
		public uint DesiredAccuracyInMeters
		{
			get { return _desiredAccuracyInMeters; }
			set
			{
				uint oldValue = _desiredAccuracyInMeters;
				if (value < MinDesiredAccuracyInMetres)
				{
					_desiredAccuracyInMeters = MinDesiredAccuracyInMetres;
				}
				else if (value > MaxDesiredAccuracyInMetres)
				{
					_desiredAccuracyInMeters = MaxDesiredAccuracyInMetres;
				}
				else
				{
					_desiredAccuracyInMeters = value;
				}
				if (_desiredAccuracyInMeters != oldValue)
				{
					RaisePropertyChanged_UI();
				}
			}
		}
		private uint _reportIntervalInMilliSec = DefaultReportIntervalInMilliSec;
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
				else if (value > MaxReportIntervalInMilliSec)
				{
					_reportIntervalInMilliSec = MaxReportIntervalInMilliSec;
				}
				else
				{
					_reportIntervalInMilliSec = value;
				}
				if (_reportIntervalInMilliSec != oldValue)
				{
					RaisePropertyChanged_UI();
				}
			}
		}
		[IgnoreDataMember]
		[Ignore]
		public uint MinReportIntervalInMilliSecProp { get { return MinReportIntervalInMilliSec; } }
		[IgnoreDataMember]
		[Ignore]
		public uint MaxReportIntervalInMilliSecProp { get { return MaxReportIntervalInMilliSec; } }
		[IgnoreDataMember]
		[Ignore]
		public uint MinBackgroundUpdatePeriodInMinutesProp { get { return MinBackgroundUpdatePeriodInMinutes; } }
		[IgnoreDataMember]
		[Ignore]
		public uint MaxBackgroundUpdatePeriodInMinutesProp { get { return MaxBackgroundUpdatePeriodInMinutes; } }
		[IgnoreDataMember]
		[Ignore]
		public uint MinDesiredAccuracyInMetresProp { get { return MinDesiredAccuracyInMetres; } }
		[IgnoreDataMember]
		[Ignore]
		public uint MaxDesiredAccuracyInMetresProp { get { return MaxDesiredAccuracyInMetres; } }
		[IgnoreDataMember]
		[Ignore]
		public int MaxRecordsInCheckpointsProp { get { return MaxRecordsInCheckpoints; } }

		private string _lastMessage = string.Empty;
		[DataMember]
		public string LastMessage { get { return _lastMessage; } set { _lastMessage = value; RaisePropertyChanged_UI(); } }
		private bool _isShowSpeed = false;
		[DataMember]
		public bool IsShowSpeed { get { return _isShowSpeed; } set { _isShowSpeed = value; RaisePropertyChanged_UI(); } }
		private bool _isTracking = false;
		[DataMember]
		public bool IsTracking { get { return _isTracking; } set { _isTracking = value; RaisePropertyChanged_UI(); } }
		private volatile bool _isBackgroundEnabled = false;
		[DataMember]
		public bool IsBackgroundEnabled { get { return _isBackgroundEnabled; } set { _isBackgroundEnabled = value; RaisePropertyChanged_UI(); } }
		private double _tapTolerance = DefaultTapTolerance;
		[DataMember]
		public double TapTolerance
		{
			get { return _tapTolerance; }
			set
			{
				if (value > MaxTapTolerance) _tapTolerance = MaxTapTolerance;
				else if (value < MinTapTolerance) _tapTolerance = MinTapTolerance;
				else _tapTolerance = value;
				RaisePropertyChanged_UI();
			}
		}
		[IgnoreDataMember]
		[Ignore]
		public double MinTapToleranceProp { get { return MinTapTolerance; } }
		[IgnoreDataMember]
		[Ignore]
		public double MaxTapToleranceProp { get { return MaxTapTolerance; } }

		private bool _isShowDegrees = true;
		[DataMember]
		public bool IsShowDegrees { get { return _isShowDegrees; } set { if (_isShowDegrees != value) { _isShowDegrees = value; RaisePropertyChanged_UI(); RaisePropertyChanged_UI(nameof(Current)); } } }
		private bool _isKeepAlive = false;
		[DataMember]
		public bool IsKeepAlive { get { return _isKeepAlive; } set { _isKeepAlive = value; RaisePropertyChanged_UI(); } }
		private bool _isAllowMeteredConnection = false;
		[DataMember]
		public bool IsAllowMeteredConnection { get { return _isAllowMeteredConnection; } set { _isAllowMeteredConnection = value; RaisePropertyChanged_UI(); } }

		private MapStyle _mapStyle = MapStyle.Terrain;
		[DataMember]
		public MapStyle MapStyle { get { return _mapStyle; } set { _mapStyle = value; RaisePropertyChanged_UI(); } }
		private volatile bool _isMapCached = false;
		[DataMember]
		public bool IsMapCached { get { return _isMapCached; } set { if (_isMapCached != value) { _isMapCached = value; RaisePropertyChanged_UI(); } } }
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

			if (isIsTilesDownloadDesiredChanged) RaisePropertyChanged_UI(nameof(IsTilesDownloadDesired));
			if (isMaxZoomChanged) RaisePropertyChanged_UI(nameof(MaxDesiredZoomForDownloadingTiles));
		}
		private volatile DownloadSession _lastDownloadSession;
		[DataMember]
		public DownloadSession LastDownloadSession { get { return _lastDownloadSession; } set { _lastDownloadSession = value; } }

		private PointRecord _target = new PointRecord() { PositionSource = DefaultPositionSource };
		[DataMember]
		public PointRecord Target { get { return _target; } private set { _target = value; RaisePropertyChanged(); } }

		// LOLLO TODO when using several custom map sources, they may be repeated in the list. Try starting the app multiple times. This is difficult to reproduce.
		private volatile SwitchableObservableCollection<TileSourceRecord> _tileSourcez = new SwitchableObservableCollection<TileSourceRecord>(TileSourceRecord.GetDefaultTileSources());
		[DataMember]
		public SwitchableObservableCollection<TileSourceRecord> TileSourcez { get { return _tileSourcez; } set { _tileSourcez = value; RaisePropertyChanged(); } }

		private TileSourceRecord _testTileSource = TileSourceRecord.GetSampleTileSource();
		[DataMember]
		public TileSourceRecord TestTileSource { get { return _testTileSource; } set { _testTileSource = value; RaisePropertyChanged(); } }

		private TileSourceRecord _currentTileSource = TileSourceRecord.GetDefaultTileSource();
		[DataMember]
		public TileSourceRecord CurrentTileSource { get { return _currentTileSource; } set { if (_currentTileSource == null || !_currentTileSource.IsEqualTo(value)) { _currentTileSource = value; RaisePropertyChanged(); } } }

		private double _altLastScroll = 0.0;
		[DataMember]
		public double AltLastVScroll { get { return _altLastScroll; } set { _altLastScroll = value; RaisePropertyChanged(); } }

		private bool _isShowImperialUnits = false;
		[DataMember]
		public bool IsShowImperialUnits { get { return _isShowImperialUnits; } set { if (_isShowImperialUnits != value) { _isShowImperialUnits = value; RaisePropertyChanged_UI(); RaisePropertyChanged_UI(nameof(Current)); } } }
		#endregion properties

		#region all series methods
		public Task LoadSeriesFromDbAsync(Tables whichTable, bool isShowMessageEvenIfSuccess)
		{
			switch (whichTable)
			{
				case Tables.History:
					return LoadHistoryFromDbAsync(isShowMessageEvenIfSuccess);
				case Tables.Route0:
					return LoadRoute0FromDbAsync(isShowMessageEvenIfSuccess);
				case Tables.Checkpoints:
					return LoadCheckpointsFromDbAsync(isShowMessageEvenIfSuccess);
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
				else if (_selectedSeries == Tables.Checkpoints)
				{
					series = Checkpoints;
					semaphore = _checkpointsSemaphore;
				}
			}
			if (series != null && semaphore != null && Selected != null)
			{
				try
				{
					await semaphore.WaitAsync();
					var matchingPointInSeries = series.FirstOrDefault(point => point.Latitude == Selected.Latitude && point.Longitude == Selected.Longitude);
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
							else if (_selectedSeries == Tables.Checkpoints) delete = DBManager.DeleteFromCheckpointsAsync(matchingPointInSeries);

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
				catch (OutOfMemoryException)
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
				case Tables.Checkpoints:
					semaphore = _checkpointsSemaphore;
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
		public async Task LoadHistoryFromDbAsync(bool isShowMessageEvenIfSuccess)
		{
			List<PointRecord> dataRecords = await DBManager.GetHistoryAsync().ConfigureAwait(false);

			try
			{
				await _historySemaphore.WaitAsync().ConfigureAwait(false);

				await RunInUiThreadAsync(delegate
				{
					try
					{
						_history.ReplaceRange(dataRecords?.Where(newRecord => !newRecord.IsEmpty()));
						if (isShowMessageEvenIfSuccess) LastMessage = "History updated";
					}
					catch (IndexOutOfRangeException)
					{
						LastMessage = "Only part of the history is drawn";
					}
					catch (OutOfMemoryException)
					{
						var howMuchMemoryLeft = GC.GetTotalMemory(true);
						LastMessage = "Only part of the history is drawn";
						Logger.Add_TPL("OutOfMemoryException in PersistentData.SetHistory()", Logger.PersistentDataLogFilename);
					}
					catch (Exception ex)
					{
						Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
					}
					SetCurrentToLast();
				}).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.PersistentDataLogFilename).ConfigureAwait(false);
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
				await RunInUiThreadAsync(delegate
				{
					_history.Clear();
				}).ConfigureAwait(false);
				await DBManager.DeleteAllFromHistoryAsync().ConfigureAwait(false);
				LastMessage = "trk history cleared";
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
				if (result) await DBManager.InsertIntoHistoryAsync(dataRecord, checkMaxEntries).ConfigureAwait(false);
				return result;
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(_historySemaphore);
			}
		}

		private bool AddHistoryRecord2(PointRecord dataRecord, bool checkMaxEntries)
		{
			if (!dataRecord?.IsEmpty() == true && _history?.Count < MaxRecordsInHistory)
			{
				try
				{
					//int index = GetIndexCheckingDateAscending(dataRecord, this); // no more needed if we update on the go!
					//History.Insert(index, dataRecord);
					_history.Add(dataRecord); // we don't need to need to clone the record first, if the callers always instantiate a new record

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
				catch (OutOfMemoryException)
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
		public static bool AddHistoryRecordOnlyDb(PointRecord dataRecord, bool checkMaxEntries)
		{
			if (dataRecord != null && !dataRecord.IsEmpty())
			{
				try
				{
					return DBManager.InsertIntoHistory(dataRecord, checkMaxEntries);
				}
				catch (IndexOutOfRangeException ex)
				{
					Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
				}
				catch (OutOfMemoryException ex)
				{
					var howMuchMemoryLeft = GC.GetTotalMemory(true);
					Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
				}
				catch (Exception ex)
				{
					Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
				}
			}
			return false;
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
		public async Task LoadRoute0FromDbAsync(bool isShowMessageEvenIfSuccess)
		{
			List<PointRecord> dataRecords = await DBManager.GetRoute0Async().ConfigureAwait(false);

			try
			{
				await _route0Semaphore.WaitAsync().ConfigureAwait(false);

				await RunInUiThreadAsync(delegate
				{
					try
					{
						_route0.ReplaceRange(dataRecords?.Where(newRecord => !newRecord.IsEmpty()));
						if (isShowMessageEvenIfSuccess) LastMessage = "Route updated";
					}
					catch (IndexOutOfRangeException)
					{
						LastMessage = "Only part of the route is drawn";
					}
					catch (OutOfMemoryException)
					{
						var howMuchMemoryLeft = GC.GetTotalMemory(true);
						LastMessage = "Only part of the route is drawn";
						Logger.Add_TPL("OutOfMemoryException in PersistentData.SetRoute0()", Logger.PersistentDataLogFilename);
					}
					catch (Exception ex)
					{
						Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
					}
				}).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.PersistentDataLogFilename).ConfigureAwait(false);
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
				await RunInUiThreadAsync(delegate
				{
					_route0.Clear();
				}).ConfigureAwait(false);
				await DBManager.DeleteAllFromRoute0Async().ConfigureAwait(false);
				LastMessage = "route cleared";
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(_route0Semaphore);
			}
		}
		#endregion route0Methods

		#region checkpointsMethods
		public async Task LoadCheckpointsFromDbAsync(bool isShowMessageEvenIfSuccess)
		{
			List<PointRecord> dataRecords = await DBManager.GetCheckpointsAsync().ConfigureAwait(false);

			try
			{
				await _checkpointsSemaphore.WaitAsync().ConfigureAwait(false);

				await RunInUiThreadAsync(delegate
				{
					try
					{
						_checkpoints.ReplaceRange(dataRecords?.Where(newRecord => !newRecord.IsEmpty()));
						if (isShowMessageEvenIfSuccess) LastMessage = "Checkpoints updated";
					}
					catch (IndexOutOfRangeException)
					{
						LastMessage = "Only some checkpoints are drawn";
					}
					catch (OutOfMemoryException)
					{
						var howMuchMemoryLeft = GC.GetTotalMemory(true);
						LastMessage = "Only some checkpoints are drawn";
						Logger.Add_TPL("OutOfMemoryException in PersistentData.SetCheckpoints()", Logger.PersistentDataLogFilename);
					}
					catch (Exception ex)
					{
						Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
					}
				}).ConfigureAwait(false);
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(_checkpointsSemaphore);
			}
		}
		public static Task SetCheckpointsInDBAsync(IEnumerable<PointRecord> dataRecords)
		{
			return DBManager.ReplaceCheckpointsAsync(dataRecords, true);
		}
		public async Task ResetCheckpointsAsync()
		{
			try
			{
				await _checkpointsSemaphore.WaitAsync();
				await RunInUiThreadAsync(delegate
				{
					_checkpoints.Clear();
				}).ConfigureAwait(false);
				await DBManager.DeleteAllFromCheckpointsAsync().ConfigureAwait(false);
				LastMessage = "checkpoints cleared";
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(_checkpointsSemaphore);
			}
		}
		public async Task<bool> TryAddPointToCheckpointsAsync(PointRecord newPoint)
		{
			if (newPoint != null && !newPoint.IsEmpty())
			{
				try
				{
					await _checkpointsSemaphore.WaitAsync();
					var samePointInCheckpoints = _checkpoints.FirstOrDefault(oldPoint => oldPoint.Latitude == newPoint.Latitude && oldPoint.Longitude == newPoint.Longitude);
					if (samePointInCheckpoints != null)
					{
						int index = _checkpoints.IndexOf(samePointInCheckpoints);
						if (index > -1)
						{
							await RunInUiThreadAsync(delegate
							{
								var id = _checkpoints[index].Id;
								Checkpoints[index] = newPoint;
								Checkpoints[index].Id = id; // otherwise I overwrite Id, and db update will not update
							}).ConfigureAwait(false);
							await DBManager.UpdateCheckpointsAsync(newPoint, false).ConfigureAwait(false);
							LastMessage = "Data merged into checkpoints";
							return true;
						}
						else
						{
							LastMessage = "Error updating checkpoints";
							return false;
						}
					}
					else
					{
						if (_checkpoints.Count < MaxRecordsInCheckpoints)
						{
							await RunInUiThreadAsync(delegate
							{
								_checkpoints.Add(newPoint);
							}).ConfigureAwait(false);
							await DBManager.InsertIntoCheckpointsAsync(newPoint, false).ConfigureAwait(false);
							LastMessage = "Data added to checkpoints";
							return true;
						}
						else
						{
							LastMessage = string.Format("Too many checkpoints, max is {0}", MaxRecordsInCheckpoints);
							return false;
						}
					}
				}
				catch (IndexOutOfRangeException)
				{
					LastMessage = string.Format("Too many checkpoints, max is {0}", MaxRecordsInCheckpoints);
					return false;
				}
				catch (OutOfMemoryException)
				{
					var howMuchMemoryLeft = GC.GetTotalMemory(true);
					LastMessage = string.Format("Too many checkpoints, max is {0}", MaxRecordsInCheckpoints);
					Logger.Add_TPL("OutOfMemoryException in PersistentData.TryAddTargetToCheckpoints()", Logger.PersistentDataLogFilename);
					return false;
				}
				catch (Exception ex)
				{
					Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
					return false;
				}
				finally
				{
					SemaphoreSlimSafeRelease.TryRelease(_checkpointsSemaphore);
				}
			}
			LastMessage = "Error updating checkpoints";
			return false;
		}
		public Task<bool> TryAddTargetCloneToCheckpointsAsync()
		{
			PointRecord targetClone = null;
			PointRecord.Clone(Target, ref targetClone);
			return TryAddPointToCheckpointsAsync(targetClone);
		}
		#endregion checkpointsMethods

		#region selectedRecordMethods
		public bool IsSelectedRecordFromAnySeriesFirst()
		{
			if (_selectedSeries == Tables.nil || _selected == null) return false;
			else if (_selectedSeries == Tables.History && _history.Count > 0) return _history[0].Equals(_selected);
			else if (_selectedSeries == Tables.Route0 && _route0.Count > 0) return _route0[0].Equals(_selected);
			else if (_selectedSeries == Tables.Checkpoints && _checkpoints.Count > 0) return _checkpoints[0].Equals(_selected);
			else return false;
		}
		public bool IsSelectedRecordFromAnySeriesLast()
		{
			if (_selectedSeries == Tables.nil || _selected == null) return false;
			else if (_selectedSeries == Tables.History && _history.Count > 0) return _history[_history.Count - 1].Equals(_selected);
			else if (_selectedSeries == Tables.Route0 && _route0.Count > 0) return _route0[_route0.Count - 1].Equals(_selected);
			else if (_selectedSeries == Tables.Checkpoints && _checkpoints.Count > 0) return _checkpoints[_checkpoints.Count - 1].Equals(_selected);
			else return false;
		}
		public bool IsSelectedSeriesNonNullAndNonEmpty()
		{
			if (_selectedSeries == Tables.nil || _selected == null) return false;
			else if (_selectedSeries == Tables.History) return _history.Count > 0;
			else if (_selectedSeries == Tables.Route0) return _route0.Count > 0;
			else if (_selectedSeries == Tables.Checkpoints) return _checkpoints.Count > 0;
			else return false;
		}
		//public void SelectCurrentHistoryRecord()
		//{
		//	SelectRecordFromSeries(Current, Tables.History);
		//}
		public void SelectRecordFromSeries(PointRecord dataRecord, Tables whichTable, int index = -1)
		{
			if (dataRecord != null && !dataRecord.IsEmpty())
			{
				Selected = dataRecord;
				SelectedSeries = whichTable;
				if (index == -1)
				{
					if (whichTable == Tables.History) SelectedIndex_Base1 = History.IndexOf(dataRecord) + 1;
					else if (whichTable == Tables.Route0) SelectedIndex_Base1 = Route0.IndexOf(dataRecord) + 1;
					else if (whichTable == Tables.Checkpoints) SelectedIndex_Base1 = Checkpoints.IndexOf(dataRecord) + 1;
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
			else if (_selectedSeries == Tables.Checkpoints) SelectNeighbourRecord(_checkpoints, Tables.Checkpoints, step);
		}
		public PointRecord GetRecordBeforeSelectedFromAnySeries()
		{
			if (_selectedSeries == Tables.nil || Selected == null || SelectedIndex_Base1 - 2 < 0) return null;
			else if (_selectedSeries == Tables.History) return History[SelectedIndex_Base1 - 2];
			else if (_selectedSeries == Tables.Route0) return Route0[SelectedIndex_Base1 - 2];
			else if (_selectedSeries == Tables.Checkpoints) return Checkpoints[SelectedIndex_Base1 - 2];
			return null;
		}
		private void SelectNeighbourRecord(Collection<PointRecord> series, Tables whichSeries, int step)
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
				if (TestTileSource == null) return Tuple.Create(false, "Record not found");
				if (string.IsNullOrWhiteSpace(TestTileSource.TechName)) return Tuple.Create(false, "Name is empty");
				// set non-screen properties
				TestTileSource.DisplayName = TestTileSource.TechName; // we always set it automatically
				TestTileSource.IsDeletable = true;
				string errorMsg = TestTileSource.Check(true);

				if (!string.IsNullOrEmpty(errorMsg))
				{
					return Tuple.Create(false, errorMsg);
				}
				else
				{
					string successMessage = string.Format("Source {0} added", TestTileSource.DisplayName);
					var recordsWithSameName = TileSourcez.Where(tileSource => tileSource.TechName == TestTileSource.TechName || tileSource.DisplayName == TestTileSource.TechName);
					if (recordsWithSameName != null)
					{
						if (recordsWithSameName.Count() > 1)
						{
							return Tuple.Create(false, string.Format("Tile source {0} cannot be changed", TestTileSource.TechName));
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
								return Tuple.Create(false, string.Format("Tile source {0} cannot be changed", TestTileSource.TechName));
							}
						}
					}

					TileSourceRecord newRecord = null;
					TileSourceRecord.Clone(TestTileSource, ref newRecord); // do not overwrite the current instance
					TileSourcez.Add(newRecord);
					RaisePropertyChanged(nameof(TileSourcez));
					CurrentTileSource = newRecord;

					return Tuple.Create(true, successMessage);
				}
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
				return Tuple.Create(false, string.Format("Error with tile source {0}", TestTileSource.TechName));
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
						foreach (var item in TileSourcez.Where(ts => ts.IsDeletable))
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
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(_tileSourcezSemaphore);
			}
		}
		#endregion tileSourcesMethods

		#region otherMethods
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


	public interface IGPSDataModel : INotifyPropertyChanged
	{
		uint DesiredAccuracyInMeters { get; }
		uint ReportIntervalInMilliSec { get; }
		uint BackgroundUpdatePeriodInMinutes { get; }
		uint GetDefaultDesiredAccuracyInMetres { get; }
		bool IsTracking { get; }
		bool IsBackgroundEnabled { get; set; }

		string LastMessage { get; set; }

		Task<bool> AddHistoryRecordAsync(PointRecord dataRecord, bool checkMaxEntries);
	}
}