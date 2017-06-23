using LolloGPS.Data.Leeching;
using LolloGPS.Data.Runtime;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Utilz;
using Utilz.Data;
using Windows.Devices.Geolocation;
using Windows.Storage;
using Windows.UI.Xaml.Controls.Maps;


// There is a sqlite walkthrough at:
// http://social.technet.microsoft.com/wiki/contents/articles/29149.windows-phone-8-1-sqlite-part-one.aspx

namespace LolloGPS.Data
{
    [DataContract]
    public sealed class PersistentData : ObservableData, IGpsDataModel //, INotifyDataErrorInfo //does not work
    {
        #region enums
        public enum Tables { History, Route0, Checkpoints, Nil }
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
                case Tables.Nil:
                    return "No series";
                default:
                    return "";
            }
        }
        public static class CheckpointSymbols
        {
            public static string Circle { get { return "Circle"; } }
            public static string Cross { get { return "Cross"; } }
            public static string Ecs { get { return "Ecs"; } }
            public static string Square { get { return "Square"; } }
            public static string Triangle { get { return "Triangle"; } }
        }
        #endregion enums

        #region constants
        public const int MaxRecordsInRoute = short.MaxValue;
        public const int MaxRecordsInHistory = short.MaxValue;
        private const int MaxCheckpoints1 = 500; // was 100
        private const int MaxCheckpoints2 = 1000; // was 200
        private const int MaxCheckpoints3 = 1500; // was 500
        private const int MaxCheckpoints4 = 2000; // was 1000

        public static readonly int MaxRecordsInCheckpoints = MaxCheckpoints4;

        public const int MaxCurrentTileSources = 8;
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
        public const double DefaultTapTolerance = 10.0;

        public const string DefaultPositionSource = ConstantData.APPNAME;
        private const int DefaultSelectedIndex_Base1 = 0;

        private static readonly SemaphoreSlimSafeRelease _historySemaphore = new SemaphoreSlimSafeRelease(1, 1);
        private static readonly SemaphoreSlimSafeRelease _route0Semaphore = new SemaphoreSlimSafeRelease(1, 1);
        private static readonly SemaphoreSlimSafeRelease _checkpointsSemaphore = new SemaphoreSlimSafeRelease(1, 1);
        private static readonly SemaphoreSlimSafeRelease _tileSourcezSemaphore = new SemaphoreSlimSafeRelease(1, 1);
        #endregion constants

        #region events
        public event EventHandler CurrentChanged;
        public event EventHandler<Tables> RefreshSeriesRequested;
        #endregion events

        #region lifecycle
        private static PersistentData _instance;
        private static readonly object _instanceLocker = new object();
        public static PersistentData GetInstance()
        {
            lock (_instanceLocker)
            {
                return _instance ?? (_instance = new PersistentData());
            }
        }

        /// <summary>
        /// This method is not thread safe: it must be called before any UI or any property changed handlers start.
        /// In particular, it may trick the <see cref="PropertyChanged"/> subscribers away from the properties that are set here.
        /// If any such subscribers are active, this method throws <see cref="DataAlreadyBoundException"/>.
        /// If the supplied <paramref name="source"/> argument is null, this method throws <see cref="ArgumentException"/>.
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>        
        public static PersistentData GetInstanceWithProperties(PersistentData source)
        {
            if (source == null) throw new ArgumentException("PersistentData.GetInstanceWithClonedNonDbProperties was called with source == null");
            lock (_instanceLocker)
            {
                if (_instance == null) _instance = new PersistentData();
                // make sure no UI has been initialised yet
                if (source.IsAnyoneListening()) throw new DataAlreadyBoundException("PersistentData.GetInstanceWithClonedNonDbProperties must not be called when any UI is active");
                if (_instance.IsAnyoneListening()) throw new DataAlreadyBoundException("PersistentData.GetInstanceWithClonedNonDbProperties must not be called when any UI is active");
                // initialise non-serialised properties
                source._checkpoints = new SwitchableObservableCollection<PointRecord>(MaxRecordsInCheckpoints);
                source._history = new SwitchableObservableCollection<PointRecord>(MaxRecordsInHistory);
                source._route0 = new SwitchableObservableCollection<PointRecord>(MaxRecordsInRoute);
                source.SetCurrentToLast();
                // replace the stock tile sourcez without uninstalling the app
                if (_instance?.TileSourcez != null)
                {
                    int ii = source.TileSourcez.Count - 1;
                    for (int i = ii; i >= 0; i--)
                    {
                        var ts = source.TileSourcez[i];
                        if (ts == null || ts.IsDeletable) continue; // do not touch custom tile sources
                        // I may say, do not delete sources, which are no more available. However, this way we fill up with junk.
                        // Better would be to clear their caches; but for that to succeed, I must leave them in the list. What a mess.
                        // if (!_instance.TileSourcez.Any(tso => tso.TechName == ts.TechName)) continue;
                        // Easier: we only delete these obsolete sources from the list; if one wants a clean phone, one will reinstall the app, or clear the whole cache.
                        source.TileSourcez.RemoveAt(i);
                    }
                    ii = _instance.TileSourcez.Count - 1;
                    for (int i = ii; i >= 0; i--)
                    {
                        var ts = _instance.TileSourcez[i];
                        if (ts == null || ts.IsDeletable) continue;
                        source.TileSourcez.Insert(0, ts);
                    }
                }
                // set the singleton instance
                _instance = source;
                return _instance;
            }
        }
        /*
        /// <summary>
        /// Clones memberwise, so the current event handlers are preserved.
        /// If this method is called properly, PropertyChanged will always be null, because it is not thread safe.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        private static void CloneNonDbProperties2(PersistentData source, ref PersistentData target)
        {
            if (source == null) throw new ArgumentException("PersistentData.CloneNonDbProperties2 was called with source == null");
            if (target == null) throw new ArgumentException("PersistentData.CloneNonDbProperties2 was called with target == null");
            if (target.IsAnyoneListening()) throw new InvalidOperationException("PersistentData.CloneNonDbProperties2 must not be called when any UI is active");

            PointRecord.Clone(source.Selected, ref target._selected);

            if (!source.Target.IsEmpty()) PointRecord.Clone(source.Target, ref target._target);

            target.SelectedSeries = source.SelectedSeries;
            target.BackgroundUpdatePeriodInMinutes = source.BackgroundUpdatePeriodInMinutes;
            target.DesiredAccuracyInMeters = source.DesiredAccuracyInMeters;
            target.ReportIntervalInMilliSec = source.ReportIntervalInMilliSec;
            // target.LastMessage = source.LastMessage; // we don't want to repeat the last message whenever one starts the app
            target.IsForegroundTracking = source.IsForegroundTracking;
            target.IsBackgroundTracking = source.IsBackgroundTracking;
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
            //target.MaxDesiredZoomForDownloadingTiles = source.MaxDesiredZoomForDownloadingTiles;
            target.TapTolerance = source.TapTolerance;
            target.IsShowDegrees = source.IsShowDegrees;
            target.IsKeepAlive = source.IsKeepAlive;
            target.IsShowingAltitudeProfiles = source.IsShowingAltitudeProfiles;
            target.SelectedPivotIndex = source.SelectedPivotIndex;
            target.IsShowingPivot = source.IsShowingPivot;
            target.IsBackButtonEnabled = source.IsBackButtonEnabled;
            target.IsAllowMeteredConnection = source.IsAllowMeteredConnection; // this must always fire coz it is th eonly prop referenced in RuntimeData, which is the only entity that may exist before this.
            target.AltLastVScroll = source.AltLastVScroll;
            target.IsShowImperialUnits = source.IsShowImperialUnits;
            target.HistoryAltitudeI0 = source.HistoryAltitudeI0;
            target.HistoryAltitudeI1 = source.HistoryAltitudeI1;
            target.Route0AltitudeI0 = source.Route0AltitudeI0;
            target.Route0AltitudeI1 = source.Route0AltitudeI1;
            target.CheckpointsAltitudeI0 = source.CheckpointsAltitudeI0;
            target.CheckpointsAltitudeI1 = source.CheckpointsAltitudeI1;

            TileSourceRecord.Clone(source.TestTileSource, ref target._testTileSource);

            TileSourceRecord.Clone(source.CurrentTileSource, ref target._currentTileSource);

            if (source.TileSourcez != null && source.TileSourcez.Any()) // start with the default values
            {
                foreach (var srcItem in source.TileSourcez.Where(tileSource => tileSource.IsDeletable)) // add custom map sources
                {
                    target.TileSourcez.Add(new TileSourceRecord(srcItem.TechName, srcItem.DisplayName, srcItem.FolderName, srcItem.CopyrightNotice, srcItem.UriString, srcItem.ProviderUriString, srcItem.MinZoom, srcItem.MaxZoom, srcItem.TilePixelSize, srcItem.IsDeletable, srcItem.RequestHeaders));
                }
            }

            DownloadSession.Clone(source._lastDownloadSession, ref target._lastDownloadSession);
        }
        */
        static PersistentData()
        {
            var memUsageLimit = Windows.System.MemoryManager.AppMemoryUsageLimit; // 33966739456 on PC, less than 200000000 on phone
            Logger.Add_TPL("mem usage limit = " + memUsageLimit, Logger.AppEventsLogFilename, Logger.Severity.Info);
            if (memUsageLimit < 1e+9) MaxRecordsInCheckpoints = MaxCheckpoints1;
            else if (memUsageLimit < 2e+9) MaxRecordsInCheckpoints = MaxCheckpoints2;
            else if (memUsageLimit < 4e+9) MaxRecordsInCheckpoints = MaxCheckpoints3;
            else MaxRecordsInCheckpoints = MaxCheckpoints4;
        }
        private PersistentData()
        {
            // initialise non-serialised properties
            _checkpoints = new SwitchableObservableCollection<PointRecord>(MaxRecordsInCheckpoints);
            _history = new SwitchableObservableCollection<PointRecord>(MaxRecordsInHistory);
            _route0 = new SwitchableObservableCollection<PointRecord>(MaxRecordsInRoute);
            SetCurrentToLast();
        }

        public static void OpenMainDb()
        {
            LolloSQLiteConnectionPoolMT.Open();
        }
        /// <summary>
        /// Only call this from a task, which is not the main one. 
        /// Otherwise, you will screw up the db open / closed logic.
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        public static bool RunDbOpInOtherTask(Func<bool> action)
        {
            return LolloSQLiteConnectionPoolMT.RunInOtherTask(action);
        }
        /// <summary>
        /// Waits for current DB operations to terminate and then locks the DB.
        /// </summary>
        public static void CloseMainDb()
        {
            LolloSQLiteConnectionPoolMT.Close();
        }
        #endregion lifecycle

        #region properties
        private volatile bool _isShowingPivot = false;
        [DataMember]
        public bool IsShowingPivot { get { return _isShowingPivot; } set { _isShowingPivot = value; RaisePropertyChanged_UI(); IsBackButtonEnabled = _isShowingPivot; } }

        private volatile bool _isBackButtonEnabled = false;
        [DataMember]
        public bool IsBackButtonEnabled { get { return _isBackButtonEnabled; } set { _isBackButtonEnabled = value; RaisePropertyChanged_UI(); } }

        private volatile int _selectedPivotIndex = 1;
        [DataMember]
        public int SelectedPivotIndex { get { return _selectedPivotIndex; } set { if (_selectedPivotIndex != value) { _selectedPivotIndex = value; RaisePropertyChanged_UI(); } } }

        private volatile bool _isShowingAltitudeProfiles = false;
        [DataMember]
        public bool IsShowingAltitudeProfiles { get { return _isShowingAltitudeProfiles; } set { if (_isShowingAltitudeProfiles != value) { _isShowingAltitudeProfiles = value; RaisePropertyChanged_UI(); } } }

        private volatile PointRecord _current = null;
        [IgnoreDataMember] // we pick Current from History, which we also don't serialise, and we also init in the ctor
        public PointRecord Current
        {
            get { return _current; }
            private set
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

        private volatile PointRecord _selected = new PointRecord(); // { PositionSource = DefaultPositionSource };
        [DataMember]
        public PointRecord Selected { get { return _selected; } private set { _selected = value; RaisePropertyChanged(); } }
        private volatile Tables _selectedSeries = Tables.Nil;
        [DataMember]
        public Tables SelectedSeries { get { return _selectedSeries; } private set { _selectedSeries = value; RaisePropertyChanged(); } }
        private volatile int _selectedIndex_Base1 = DefaultSelectedIndex_Base1;
        [DataMember]
        public int SelectedIndex_Base1 { get { return _selectedIndex_Base1; } private set { _selectedIndex_Base1 = value; RaisePropertyChanged(); } }

        private SwitchableObservableCollection<PointRecord> _checkpoints = null;
        [IgnoreDataMember] // we save the checkpoints into the DB so we don't serialise it, and we init it in the ctor
        public SwitchableObservableCollection<PointRecord> Checkpoints { get { return _checkpoints; } }
        private SwitchableObservableCollection<PointRecord> _history = null;
        [IgnoreDataMember] // we save the history into the DB so we don't serialise it, and we init it in the ctor
        public SwitchableObservableCollection<PointRecord> History { get { return _history; } }
        private SwitchableObservableCollection<PointRecord> _route0 = null;
        [IgnoreDataMember] // we save the route into the DB so we don't serialise it, and we init it in the ctor
        public SwitchableObservableCollection<PointRecord> Route0 { get { return _route0; } }

        private volatile uint _backgroundUpdatePeriodInMinutes = DefaultBackgroundUpdatePeriodInMinutes;
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
        private volatile uint _desiredAccuracyInMeters = DefaultDesiredAccuracyInMetres;
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
        private volatile uint _reportIntervalInMilliSec = DefaultReportIntervalInMilliSec;
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
        public uint MinReportIntervalInMilliSecProp { get { return MinReportIntervalInMilliSec; } }
        [IgnoreDataMember]
        public uint MaxReportIntervalInMilliSecProp { get { return MaxReportIntervalInMilliSec; } }
        [IgnoreDataMember]
        public uint MinBackgroundUpdatePeriodInMinutesProp { get { return MinBackgroundUpdatePeriodInMinutes; } }
        [IgnoreDataMember]
        public uint MaxBackgroundUpdatePeriodInMinutesProp { get { return MaxBackgroundUpdatePeriodInMinutes; } }
        [IgnoreDataMember]
        public uint MinDesiredAccuracyInMetresProp { get { return MinDesiredAccuracyInMetres; } }
        [IgnoreDataMember]
        public uint MaxDesiredAccuracyInMetresProp { get { return MaxDesiredAccuracyInMetres; } }
        [IgnoreDataMember]
        public int MaxRecordsInCheckpointsProp { get { return MaxRecordsInCheckpoints; } }

        private volatile string _lastMessage = string.Empty;
        [DataMember]
        public string LastMessage { get { return _lastMessage; } set { _lastMessage = value; RaisePropertyChanged_UI(); } }
        private bool _isShowSpeed = false;
        [DataMember]
        public bool IsShowSpeed { get { return _isShowSpeed; } set { _isShowSpeed = value; RaisePropertyChanged_UI(); } }
        private volatile bool _isForegroundTracking = false;
        [DataMember]
        public bool IsForegroundTracking { get { return _isForegroundTracking; } set { if (_isForegroundTracking != value) { _isForegroundTracking = value; RaisePropertyChanged_UI(); } } }
        private volatile bool _isBackgroundTracking = false;
        [DataMember]
        public bool IsBackgroundTracking { get { return _isBackgroundTracking; } set { if (_isBackgroundTracking != value) { _isBackgroundTracking = value; RaisePropertyChanged_UI(); } } }
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
        public double MinTapToleranceProp { get { return MinTapTolerance; } }
        [IgnoreDataMember]
        public double MaxTapToleranceProp { get { return MaxTapTolerance; } }

        private bool _isShowDegrees = true;
        [DataMember]
        public bool IsShowDegrees { get { return _isShowDegrees; } set { if (_isShowDegrees != value) { _isShowDegrees = value; RaisePropertyChanged_UI(); RaisePropertyChanged_UI(nameof(Current)); } } }
        private bool _isKeepAlive = false;
        [DataMember]
        public bool IsKeepAlive { get { return _isKeepAlive; } set { _isKeepAlive = value; RaisePropertyChanged_UI(); } }
        private bool _isAllowMeteredConnection = false;
        [DataMember]
        public bool IsAllowMeteredConnection { get { return _isAllowMeteredConnection; } set { _isAllowMeteredConnection = value; RuntimeData.GetInstance().UpdateIsConnectionAvailable(); RaisePropertyChanged_UI(); } }

        private MapStyle _mapStyle = MapStyle.Terrain;
        [DataMember]
        public MapStyle MapStyle { get { return _mapStyle; } set { if (_mapStyle != value) { _mapStyle = value; RaisePropertyChanged_UI(); } } }
        public void CycleMapStyle()
        {
            switch (MapStyle)
            {
                case MapStyle.None:
                    MapStyle = MapStyle.Terrain; break;
                default:
                    MapStyle = MapStyle.None; break;
            }
        }

        private volatile bool _isMapCached = true; //false;
        [DataMember]
        public bool IsMapCached { get { return _isMapCached; } set { if (_isMapCached != value) { _isMapCached = value; RaisePropertyChanged_UI(); } } }
        private double _mapLastLat = default(double);
        [DataMember]
        public double MapLastLat { get { return _mapLastLat; } set { _mapLastLat = value; } }
        private double _mapLastLon = default(double);
        [DataMember]
        public double MapLastLon { get { return _mapLastLon; } set { _mapLastLon = value; } }
        private double _mapLastZoom = 2.0;
        [DataMember]
        public double MapLastZoom { get { return _mapLastZoom; } set { _mapLastZoom = value; } }
        private double _mapLastHeading = 0.0;
        [DataMember]
        public double MapLastHeading { get { return _mapLastHeading; } set { _mapLastHeading = value; } }
        private double _mapLastPitch = 0.0;
        [DataMember]
        public double MapLastPitch { get { return _mapLastPitch; } set { _mapLastPitch = value; } }

        private static readonly object _lastDownloadLocker = new object();
        [DataMember]
        private bool _isTilesDownloadDesired = false; // no volatile here: I have a locker so I use it, it's much faster (not that volatile is slow anyway)
        public bool IsTilesDownloadDesired
        {
            get
            {
                lock (_lastDownloadLocker)
                {
                    return _isTilesDownloadDesired;
                }
            }
        }
        [DataMember]
        private int _maxDesiredZoomForDownloadingTiles = -1; // no volatile here: I have a locker so I use it, it's much faster (not that volatile is slow anyway)

        [DataMember]
        private DownloadSession _lastDownloadSession; // no volatile here: I have a locker so I use it, it's much faster (not that volatile is slow anyway)

        private static readonly object _targetLocker = new object();
        [DataMember]
        private PointRecord _target = new PointRecord() { PositionSource = DefaultPositionSource };
        public PointRecord Target { get { lock (_targetLocker) { return _target; } } }

        [DataMember]
        private readonly SwitchableObservableCollection<TileSourceRecord> _tileSourcez = new SwitchableObservableCollection<TileSourceRecord>(TileSourceRecord.GetStockTileSources());
        public SwitchableObservableCollection<TileSourceRecord> TileSourcez { get { return _tileSourcez; } }

        private volatile WritableTileSourceRecord _testTileSource = WritableTileSourceRecord.GetSampleTileSource();
        [DataMember]
        public WritableTileSourceRecord TestTileSource { get { return _testTileSource; } private set { _testTileSource = value; RaisePropertyChanged_UI(); } }

        [DataMember]
        private readonly SwitchableObservableCollection<TileSourceRecord> _currentTileSources = new SwitchableObservableCollection<TileSourceRecord>(TileSourceRecord.GetDefaultTileSourceList());
        public SwitchableObservableCollection<TileSourceRecord> CurrentTileSources { get { return _currentTileSources; } }

        private volatile bool _isTileSourcezBusy = false;
        [IgnoreDataMember]
        public bool IsTileSourcezBusy { get { return _isTileSourcezBusy; } private set { if (_isTileSourcezBusy != value) { _isTileSourcezBusy = value; RaisePropertyChanged_UI(); } } }

        private double _lastAltitudeLastVScroll = 0.0;
        [DataMember]
        public double LastAltLastVScroll { get { return _lastAltitudeLastVScroll; } set { _lastAltitudeLastVScroll = value; } }

        private volatile bool _isShowImperialUnits = false;
        [DataMember]
        public bool IsShowImperialUnits { get { return _isShowImperialUnits; } set { if (_isShowImperialUnits != value) { _isShowImperialUnits = value; RaisePropertyChanged_UI(); RaisePropertyChanged_UI(nameof(Current)); } } }

        private static readonly object _seriesAltitudeI0I1Locker = new object();
        private const int SeriesAltitudeI0Default = 0;
        private const int SeriesAltitudeI1Default = int.MaxValue;
        private volatile int _historyAltitudeI0 = SeriesAltitudeI0Default;
        [DataMember]
        public int HistoryAltitudeI0 { get { return _historyAltitudeI0; } private set { if (_historyAltitudeI0 != value && value > -1) { _historyAltitudeI0 = value; } } }
        private volatile int _historyAltitudeI1 = SeriesAltitudeI1Default;
        [DataMember]
        public int HistoryAltitudeI1 { get { return _historyAltitudeI1; } private set { if (_historyAltitudeI1 != value && value > 0) { _historyAltitudeI1 = value; } } }

        private volatile int _route0AltitudeI0 = SeriesAltitudeI0Default;
        [DataMember]
        public int Route0AltitudeI0 { get { return _route0AltitudeI0; } private set { if (_route0AltitudeI0 != value && value > -1) { _route0AltitudeI0 = value; } } }
        private volatile int _route0AltitudeI1 = SeriesAltitudeI1Default;
        [DataMember]
        public int Route0AltitudeI1 { get { return _route0AltitudeI1; } private set { if (_route0AltitudeI1 != value && value > 0) { _route0AltitudeI1 = value; } } }

        private volatile int _checkpointsAltitudeI0 = SeriesAltitudeI0Default;
        [DataMember]
        public int CheckpointsAltitudeI0 { get { return _checkpointsAltitudeI0; } private set { if (_checkpointsAltitudeI0 != value && value > -1) { _checkpointsAltitudeI0 = value; } } }
        private volatile int _checkpointsAltitudeI1 = SeriesAltitudeI1Default;
        [DataMember]
        public int CheckpointsAltitudeI1 { get { return _checkpointsAltitudeI1; } private set { if (_checkpointsAltitudeI1 != value && value > 0) { _checkpointsAltitudeI1 = value; } } }
        #endregion properties

        #region all series methods
        public void RefreshSeries(Tables whichSeries)
        {
            if (whichSeries == Tables.Nil) return;
            RefreshSeriesRequested?.Invoke(this, whichSeries);
        }
        public async Task<IReadOnlyCollection<PointRecord>> GetSeriesCloneAsync(Tables whichSeries)
        {
            switch (whichSeries)
            {
                case Tables.Checkpoints: return (await DBManager.GetCheckpointsAsync().ConfigureAwait(false)).AsReadOnly();
                case Tables.History: return (await DBManager.GetHistoryAsync().ConfigureAwait(false)).AsReadOnly();
                case Tables.Route0: return (await DBManager.GetRoute0Async().ConfigureAwait(false)).AsReadOnly();
                default: return null;
            }
        }
        /// <summary>
        /// Deletes the selected point from the series it belongs to.
        /// </summary>
        /// <returns></returns>
        public async Task<bool> DeleteSelectedPointFromSeriesAsync()
        {
            SwitchableObservableCollection<PointRecord> series = null;
            SemaphoreSlimSafeRelease seriesSemaphore = null;
            var selectedSeries = _selectedSeries;
            if (_selected != null)
            {
                if (selectedSeries == Tables.History)
                {
                    series = History;
                    seriesSemaphore = _historySemaphore;
                }
                else if (selectedSeries == Tables.Route0)
                {
                    series = Route0;
                    seriesSemaphore = _route0Semaphore;
                }
                else if (selectedSeries == Tables.Checkpoints)
                {
                    series = Checkpoints;
                    seriesSemaphore = _checkpointsSemaphore;
                }
            }
            if (series != null && seriesSemaphore != null && Selected != null)
            {
                try
                {
                    await seriesSemaphore.WaitAsync();
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
                            if (selectedSeries == Tables.History) delete = DBManager.DeleteFromHistoryAsync(matchingPointInSeries);
                            else if (selectedSeries == Tables.Route0) delete = DBManager.DeleteFromRoute0Async(matchingPointInSeries);
                            else if (selectedSeries == Tables.Checkpoints) delete = DBManager.DeleteFromCheckpointsAsync(matchingPointInSeries);

                            // if I have removed the last record from the series
                            if (!series.Any())
                            {
                                Selected = new PointRecord();
                                SelectedIndex_Base1 = DefaultSelectedIndex_Base1;
                                SelectedSeries = Tables.Nil;
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
                    SemaphoreSlimSafeRelease.TryRelease(seriesSemaphore);
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
        private void ResetSeriesAltitudeI0I1(Tables whichTable)
        {
            lock (_seriesAltitudeI0I1Locker)
            {
                switch (whichTable)
                {
                    case Tables.History:
                        HistoryAltitudeI0 = SeriesAltitudeI0Default;
                        HistoryAltitudeI1 = SeriesAltitudeI1Default;
                        return;
                    case Tables.Route0:
                        Route0AltitudeI0 = SeriesAltitudeI0Default;
                        Route0AltitudeI1 = SeriesAltitudeI1Default;
                        return;
                    case Tables.Checkpoints:
                        CheckpointsAltitudeI0 = SeriesAltitudeI0Default;
                        CheckpointsAltitudeI1 = SeriesAltitudeI1Default;
                        return;
                    default:
                        return;
                }
            }
        }
        public void SetSeriesAltitudeI0I1(Tables whichTable, int i0, int i1)
        {
            lock (_seriesAltitudeI0I1Locker)
            {
                switch (whichTable)
                {
                    case Tables.History:
                        HistoryAltitudeI0 = Math.Max(i0, 0);
                        HistoryAltitudeI1 = Math.Min(i1, History.Count - 1);
                        return;
                    case Tables.Route0:
                        Route0AltitudeI0 = Math.Max(i0, 0);
                        Route0AltitudeI1 = Math.Min(i1, Route0.Count - 1);
                        return;
                    case Tables.Checkpoints:
                        CheckpointsAltitudeI0 = Math.Max(i0, 0);
                        CheckpointsAltitudeI1 = Math.Min(i1, Checkpoints.Count - 1);
                        return;
                    default:
                        return;
                }
            }
        }
        #endregion all series methods

        #region historyMethods
        public async Task<int> LoadHistoryFromDbAsync(bool isResetAltitideI0I1, bool useUIThread)
        {
            int result = -1;
            List<PointRecord> dataRecords = await DBManager.GetHistoryAsync().ConfigureAwait(false);

            try
            {
                await _historySemaphore.WaitAsync().ConfigureAwait(false);
                if (isResetAltitideI0I1) ResetSeriesAltitudeI0I1(Tables.History);
                if (useUIThread) await RunInUiThreadAsync(delegate { result = LoadHistoryFromDb2(dataRecords); }).ConfigureAwait(false);
                else result = LoadHistoryFromDb2(dataRecords);
            }
            catch (Exception ex)
            {
                await Logger.AddAsync(ex.ToString(), Logger.PersistentDataLogFilename).ConfigureAwait(false);
            }
            finally
            {
                SemaphoreSlimSafeRelease.TryRelease(_historySemaphore);
            }
            return result;
        }

        private int LoadHistoryFromDb2(List<PointRecord> dataRecords)
        {
            int result = -1;
            try
            {
                _history.ReplaceAll(dataRecords?.Where(newRecord => !newRecord.IsEmpty()));
                result = _history.Count;
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
            return result;
        }
        public async Task ResetHistoryAsync()
        {
            try
            {
                await _historySemaphore.WaitAsync();
                ResetSeriesAltitudeI0I1(Tables.History);
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
            if (dataRecord != null && !dataRecord.IsEmpty() && _history?.Count < MaxRecordsInHistory)
            {
                try
                {
                    //int index = GetIndexCheckingDateAscending(dataRecord, this); // no more needed if we update on the go!
                    //History.Insert(index, dataRecord);
                    _history.Add(dataRecord); // we don't need to need to clone the record first, if the callers always instantiate a new record

                    if (!string.IsNullOrEmpty(dataRecord.Status)) LastMessage = dataRecord.Status;
                    else LastMessage = "point added to trk history @ " + dataRecord.TimePoint.ToString(CultureInfo.CurrentUICulture);

                    Current = dataRecord;
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
            if (dataRecord == null || dataRecord.IsEmpty()) return false;

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
            return false;
        }

        private void SetCurrentToLast()
        {
            var history = _history;
            if (history != null && history.Any())
            {
                var newCurrent = history.LastOrDefault();
                Current = newCurrent;
            }
            else if (Current == null)
            {
                Current = new PointRecord();
            }
        }
        #endregion historyMethods

        #region route0Methods
        public async Task<int> LoadRoute0FromDbAsync(bool isResetAltitideI0I1, bool inUIThread)
        {
            int result = -1;
            List<PointRecord> dataRecords = await DBManager.GetRoute0Async().ConfigureAwait(false);

            try
            {
                await _route0Semaphore.WaitAsync().ConfigureAwait(false);
                if (isResetAltitideI0I1) ResetSeriesAltitudeI0I1(Tables.Route0);
                if (inUIThread) await RunInUiThreadAsync(delegate { result = LoadRoute0FromDb2(dataRecords); }).ConfigureAwait(false);
                else result = LoadRoute0FromDb2(dataRecords);
            }
            catch (Exception ex)
            {
                await Logger.AddAsync(ex.ToString(), Logger.PersistentDataLogFilename).ConfigureAwait(false);
            }
            finally
            {
                SemaphoreSlimSafeRelease.TryRelease(_route0Semaphore);
            }
            return result;
        }
        private int LoadRoute0FromDb2(List<PointRecord> dataRecords)
        {
            int result = -1;

            try
            {
                _route0.ReplaceAll(dataRecords?.Where(newRecord => !newRecord.IsEmpty()));
                result = _route0.Count;
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
            return result;
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
                ResetSeriesAltitudeI0I1(Tables.Route0);
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
        public async Task<int> LoadCheckpointsFromDbAsync(bool isResetAltitideI0I1, bool inUIThread)
        {
            int result = -1;
            List<PointRecord> dataRecords = await DBManager.GetCheckpointsAsync().ConfigureAwait(false);

            try
            {
                await _checkpointsSemaphore.WaitAsync().ConfigureAwait(false);
                if (isResetAltitideI0I1) ResetSeriesAltitudeI0I1(Tables.Checkpoints);
                if (inUIThread) await RunInUiThreadAsync(delegate { result = LoadCheckpointsFromDb2(dataRecords); }).ConfigureAwait(false);
                else result = LoadCheckpointsFromDb2(dataRecords);
            }
            finally
            {
                SemaphoreSlimSafeRelease.TryRelease(_checkpointsSemaphore);
            }
            return result;
        }

        private int LoadCheckpointsFromDb2(List<PointRecord> dataRecords)
        {
            int result = -1;
            try
            {
                _checkpoints.ReplaceAll(dataRecords?.Where(newRecord => !newRecord.IsEmpty()));
                result = _checkpoints.Count;
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
            return result;
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
                ResetSeriesAltitudeI0I1(Tables.Checkpoints);
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
        // LOLLO we should put some locks here, but all the selected series stuff is about the point info panel, 
        // which is not critical because one cannot change much while it's open, other than add a point to the history if the tracking is on.
        // So we leave it unlocked.
        public bool IsSelectedRecordFromAnySeriesFirst()
        {
            var selectedSeries = _selectedSeries;
            var selectedRecord = _selected;
            if (selectedSeries == Tables.Nil || selectedRecord == null) return false;
            else if (selectedSeries == Tables.History && _history.Any()) return _history[0].Equals(selectedRecord);
            else if (selectedSeries == Tables.Route0 && _route0.Any()) return _route0[0].Equals(selectedRecord);
            else if (selectedSeries == Tables.Checkpoints && _checkpoints.Any()) return _checkpoints[0].Equals(selectedRecord);
            else return false;
        }
        public bool IsSelectedRecordFromAnySeriesLast()
        {
            var selectedSeries = _selectedSeries;
            var selectedRecord = _selected;
            if (selectedSeries == Tables.Nil || selectedRecord == null) return false;
            else if (selectedSeries == Tables.History && _history.Any()) return _history.LastOrDefault()?.Equals(selectedRecord) == true;
            else if (selectedSeries == Tables.Route0 && _route0.Any()) return _route0.LastOrDefault()?.Equals(selectedRecord) == true;
            else if (selectedSeries == Tables.Checkpoints && _checkpoints.Any()) return _checkpoints.LastOrDefault()?.Equals(selectedRecord) == true;
            else return false;
        }
        public bool IsSelectedSeriesNonNullAndNonEmpty()
        {
            var selectedSeries = _selectedSeries;
            var selectedRecord = _selected;
            if (selectedSeries == Tables.Nil || selectedRecord == null) return false;
            else if (selectedSeries == Tables.History) return _history.Any();
            else if (selectedSeries == Tables.Route0) return _route0.Any();
            else if (selectedSeries == Tables.Checkpoints) return _checkpoints.Any();
            else return false;
        }
        public IReadOnlyList<PointRecord> GetSelectedSeries()
        {
            var selectedSeries = _selectedSeries;
            switch (selectedSeries)
            {
                case Tables.Checkpoints: return _checkpoints;
                case Tables.History: return _history;
                case Tables.Route0: return _route0;
                default: return new List<PointRecord>();
            }
        }
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
            var selectedSeries = _selectedSeries;
            if (selectedSeries == Tables.Nil || _selected == null) return;
            else if (selectedSeries == Tables.History) SelectNeighbourRecord(_history, Tables.History, step);
            else if (selectedSeries == Tables.Route0) SelectNeighbourRecord(_route0, Tables.Route0, step);
            else if (selectedSeries == Tables.Checkpoints) SelectNeighbourRecord(_checkpoints, Tables.Checkpoints, step);
        }
        public PointRecord GetRecordBeforeSelectedFromAnySeries()
        {
            var selectedSeries = _selectedSeries;
            var selectedIndex_Base1 = _selectedIndex_Base1;
            if (selectedSeries == Tables.Nil || _selected == null || selectedIndex_Base1 - 2 < 0) return null;
            if (selectedSeries == Tables.History) return History[selectedIndex_Base1 - 2];
            if (selectedSeries == Tables.Route0) return Route0[selectedIndex_Base1 - 2];
            if (selectedSeries == Tables.Checkpoints) return Checkpoints[selectedIndex_Base1 - 2];
            return null;
        }
        private void SelectNeighbourRecord(IList<PointRecord> series, Tables whichSeries, int step)
        {
            int newIndex = series.IndexOf(_selected) + step;
            if (newIndex < 0) newIndex = 0;
            if (newIndex >= series.Count) newIndex = series.Count - 1;
            if (series.Count > newIndex && newIndex >= 0) SelectRecordFromSeries(series[newIndex], whichSeries, newIndex);

        }
        #endregion selectedRecordMethods

        #region tileSourcesMethods
        public async Task<string> AddCurrentTileSourceAsync(TileSourceRecord newTileSource)
        {
            if (newTileSource == null) return $"Error adding tile source";
            try
            {
                await _tileSourcezSemaphore.WaitAsync();
                IsTileSourcezBusy = true;

                if (_currentTileSources.Count >= MaxCurrentTileSources) return $"Too many tile sources, max {MaxCurrentTileSources}";
                await AddCurrentTileSource2Async(newTileSource).ConfigureAwait(false);
                return null;
            }
            catch (Exception ex)
            {
                Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
                return $"Error adding tile source";
            }
            finally
            {
                IsTileSourcezBusy = false;
                SemaphoreSlimSafeRelease.TryRelease(_tileSourcezSemaphore);
            }
        }
        private Task AddCurrentTileSource2Async(TileSourceRecord newTileSource)
        {
            if (newTileSource == null) return Task.CompletedTask;
            if (_currentTileSources.Any(ts => ts.TechName.Equals(newTileSource.TechName))) return Task.CompletedTask;

            return RunInUiThreadAsync(delegate
            {
                if (!newTileSource.IsOverlay)
                {
                    var existingBaseTileSource = _currentTileSources.FirstOrDefault(ts => !ts.IsOverlay);
                    if (existingBaseTileSource != null) _currentTileSources.Remove(existingBaseTileSource);
                }
                _currentTileSources.Add(newTileSource);
                RaisePropertyChanged_UI(nameof(CurrentTileSources));
            });
        }
        /// <summary>
        /// Checks TestTileSource and, if good, adds it to TileSourcez and sets CurrentTileSource to it.
        /// Note that the user might press "test" multiple times, so I may clutter TileSourcez with test records.
        /// </summary>
        /// <returns></returns>
        // LOLLO TODO add a custom tile source, then use it and download a few tiles. Repeat. 
        // The custom tile source will appear multiple times in the maps - available sources list: this is wrong. Very difficult to reproduce!
        public async Task<Tuple<bool, string>> TryInsertTestTileSourceIntoTileSourcezAsync()
        {
            try
            {
                await _tileSourcezSemaphore.WaitAsync();
                IsTileSourcezBusy = true;

                // the following is not atomic because there may be concurrent action in the UI thread. 
                // However, this method is currently called in the UI thread, and this is not critical anyway.
                var testTileSource = WritableTileSourceRecord.Clone(_testTileSource);
                // exit if wrong data - it should never happen
                if (testTileSource == null) return Tuple.Create(false, "Record not found");
                if (string.IsNullOrWhiteSpace(testTileSource.TechName)) return Tuple.Create(false, "Name is empty");
                // set non-screen properties
                testTileSource.FolderName = testTileSource.DisplayName = testTileSource.TechName; // we always set it automatically
                testTileSource.IsDeletable = true;
                // some more checks
                string errorMsg = testTileSource.Check(); if (!string.IsNullOrEmpty(errorMsg)) return Tuple.Create(false, errorMsg);
                // try the insertion
                Tuple<bool, string> tryInsert = await RunInUiThreadAsyncTT(() => TryInsertTestTileSourceIntoTileSourcez2Async(testTileSource)).ConfigureAwait(false);
                return tryInsert;
                //Task <Tuple<bool, string>> tryInsert = null;
                //await RunInUiThreadAsync(() => tryInsert = TryInsertTestTileSourceIntoTileSourcez2Async(testTileSource)).ConfigureAwait(false);
                //return await tryInsert.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
                return Tuple.Create(false, $"Error with tile source {_testTileSource?.DisplayName ?? string.Empty}");
            }
            finally
            {
                IsTileSourcezBusy = false;
                SemaphoreSlimSafeRelease.TryRelease(_tileSourcezSemaphore);
            }
        }
        private async Task<Tuple<bool, string>> TryInsertTestTileSourceIntoTileSourcez2Async(WritableTileSourceRecord testTs)
        {
            if (testTs == null) return Tuple.Create(false, "Tile source cannot be changed");

            Tuple<bool, string> result = null;
            var recordsWithSameName = _tileSourcez.Where(tileSource =>
                tileSource.TechName == testTs.TechName || tileSource.DisplayName == testTs.TechName || tileSource.FolderName == testTs.TechName);
            if (recordsWithSameName?.Count() == 1)
            {
                var recordWithSameName = recordsWithSameName.First();
                if (await RemoveTileSource2Async(recordWithSameName.TechName))
                    result = Tuple.Create(true, $"Source {testTs.DisplayName} changed");
                else
                    result = Tuple.Create(false, $"Tile source {testTs.DisplayName} cannot be changed");
            }
            else if (recordsWithSameName?.Count() > 1)
            {
                result = Tuple.Create(false, $"Tile source {testTs.DisplayName} cannot be changed");
            }

            if (result == null || result.Item1)
            {
                var newTs = TileSourceRecord.Clone(testTs); // do not overwrite the current instance
                _tileSourcez.Add(newTs);
                RaisePropertyChanged(nameof(TileSourcez));

                Logger.Add_TPL(newTs.ToString() + " added to _tileSourcez", Logger.ForegroundLogFilename, Logger.Severity.Info);
                Logger.Add_TPL($"_tileSourcez now has {_tileSourcez.Count} records", Logger.ForegroundLogFilename, Logger.Severity.Info);

                await AddCurrentTileSource2Async(newTs);

                TestTileSource = testTs;
                if (result == null) result = Tuple.Create(true, $"Source {testTs.DisplayName} added");
            }
            return result;
        }
        public enum ClearCacheResult { Ok, Error, Cancelled }
        public async Task<ClearCacheResult> TryClearCacheAsync(TileSourceRecord tileSource, bool isAlsoRemoveSources, CancellationToken cancToken)
        {
            if (tileSource == null || tileSource.IsNone || tileSource.IsDefault) return ClearCacheResult.Error;

            try
            {
                await _tileSourcezSemaphore.WaitAsync(cancToken).ConfigureAwait(false);
                IsTileSourcezBusy = true;

                List<string> folderNamesToBeDeleted = await GetFolderNamesToBeDeletedAsync(tileSource, cancToken).ConfigureAwait(false);
                if (folderNamesToBeDeleted?.Any() != true) return ClearCacheResult.Ok;

                var tileSourcesFolder = await ApplicationData.Current.LocalCacheFolder.TryGetItemAsync(ConstantData.TILE_SOURCES_DIR_NAME).AsTask(cancToken).ConfigureAwait(false) as StorageFolder;
                if (tileSourcesFolder == null) return ClearCacheResult.Ok;

                foreach (var folderName in folderNamesToBeDeleted.Where(fn => !string.IsNullOrWhiteSpace(fn)))
                {
                    try
                    {
                        if (cancToken.IsCancellationRequested) return ClearCacheResult.Cancelled;

                        var tileSourceFolder = await tileSourcesFolder.TryGetItemAsync(folderName).AsTask(cancToken).ConfigureAwait(false);
                        if (tileSourceFolder == null) continue;
                        // recreate the folder instead of deleting it: there may be someone waiting for this folder, like a TileCacheReaderWriter.
                        await tileSourcesFolder.CreateFolderAsync(folderName, CreationCollisionOption.ReplaceExisting).AsTask().ConfigureAwait(false);
                        // remove tile source from collection last.
                        if (isAlsoRemoveSources) await RemoveTileSource2Async(folderName).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) { return ClearCacheResult.Cancelled; }
                    catch (ObjectDisposedException) { return ClearCacheResult.Cancelled; }
                    catch (FileNotFoundException) { Debug.WriteLine("FileNotFound in ClearCacheAsync()"); }
                    catch (Exception ex) { Logger.Add_TPL("ERROR in ClearCacheAsync: " + ex.Message + ex.StackTrace, Logger.PersistentDataLogFilename); }
                }
                return ClearCacheResult.Ok;
            }
            finally
            {
                IsTileSourcezBusy = false;
                SemaphoreSlimSafeRelease.TryRelease(_tileSourcezSemaphore);
            }
        }
        private async Task<bool> RemoveTileSource2Async(string tileSourceTechName)
        {
            if (string.IsNullOrWhiteSpace(tileSourceTechName)) return false;

            await RemoveCurrentTileSource2Async(tileSourceTechName).ConfigureAwait(false);

            var tileSourceToBeDeleted = _tileSourcez.FirstOrDefault(ts => ts.TechName.Equals(tileSourceTechName));
            if (tileSourceToBeDeleted?.IsDeletable != true) return false;

            await RunInUiThreadAsync(delegate
            {
                _tileSourcez.Remove(tileSourceToBeDeleted);
                RaisePropertyChanged(nameof(TileSourcez));
            }).ConfigureAwait(false);

            return true;
        }
        private Task RemoveCurrentTileSource2Async(string tileSourceTechName)
        {
            if (string.IsNullOrWhiteSpace(tileSourceTechName)) return Task.CompletedTask;

            var tileSourceToBeDeleted = _currentTileSources.FirstOrDefault(ts => ts.TechName.Equals(tileSourceTechName));
            if (tileSourceToBeDeleted == null) return Task.CompletedTask;

            return RunInUiThreadAsync(delegate
            {
                // restore default if removing current tile source
                if (!tileSourceToBeDeleted.IsOverlay) _currentTileSources.Add(TileSourceRecord.GetDefaultTileSource());
                _currentTileSources.Remove(tileSourceToBeDeleted);
                RaisePropertyChanged(nameof(CurrentTileSources));
            });
        }
        public async Task RemoveCurrentTileSourceAsync(TileSourceRecord tileSource)
        {
            if (tileSource == null) return;
            try
            {
                await _tileSourcezSemaphore.WaitAsync();
                IsTileSourcezBusy = true;

                await RemoveCurrentTileSource2Async(tileSource?.TechName).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
            }
            finally
            {
                IsTileSourcezBusy = false;
                SemaphoreSlimSafeRelease.TryRelease(_tileSourcezSemaphore);
            }
        }
        public async Task<TileSourceRecord> GetTileSourceClone(TileSourceRecord tileSource)
        {
            if (tileSource == null) return null;

            try
            {
                await _tileSourcezSemaphore.WaitAsync().ConfigureAwait(false);
                return TileSourceRecord.Clone(tileSource);
            }
            catch (Exception ex)
            {
                Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
                return null;
            }
            finally
            {
                SemaphoreSlimSafeRelease.TryRelease(_tileSourcezSemaphore);
            }
        }
        public async Task<ICollection<TileSourceRecord>> GetAllTileSourcezCloneAsync()
        {
            ICollection<TileSourceRecord> result = new Collection<TileSourceRecord>();

            try
            {
                await _tileSourcezSemaphore.WaitAsync().ConfigureAwait(false);

                return GetAllTileSourcezClone2();
            }
            catch (Exception ex)
            {
                Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
                return new Collection<TileSourceRecord>();
            }
            finally
            {
                SemaphoreSlimSafeRelease.TryRelease(_tileSourcezSemaphore);
            }
        }
        private ICollection<TileSourceRecord> GetAllTileSourcezClone2()
        {
            var result = new Collection<TileSourceRecord>();

            foreach (var ts in _tileSourcez)
            {
                if (ts == null) continue;
                var tsClone = TileSourceRecord.Clone(ts);
                result.Add(tsClone);
            }

            return result;
        }
        public async Task<ICollection<TileSourceRecord>> GetCurrentTileSourcezCloneAsync()
        {
            try
            {
                await _tileSourcezSemaphore.WaitAsync().ConfigureAwait(false);

                return GetCurrentTileSourcezClone2();
            }
            catch (Exception ex)
            {
                Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
                return new Collection<TileSourceRecord>();
            }
            finally
            {
                SemaphoreSlimSafeRelease.TryRelease(_tileSourcezSemaphore);
            }
        }
        private ICollection<TileSourceRecord> GetCurrentTileSourcezClone2()
        {
            var result = new Collection<TileSourceRecord>();

            foreach (var ts in _currentTileSources)
            {
                if (ts == null) continue;
                var tsClone = TileSourceRecord.Clone(ts);
                result.Add(tsClone);
            }

            return result;
        }
        public async Task<TileSourceRecord> GetCurrentBaseTileSourceCloneAsync()
        {
            try
            {
                await _tileSourcezSemaphore.WaitAsync().ConfigureAwait(false);
                var currentTileSources = GetCurrentTileSourcezClone2();
                return currentTileSources.FirstOrDefault(ts => !ts.IsOverlay);
            }
            catch (Exception ex)
            {
                Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
                return null;
            }
            finally
            {
                SemaphoreSlimSafeRelease.TryRelease(_tileSourcezSemaphore);
            }
        }
        private static async Task<List<string>> GetFolderNamesToBeDeletedAsync(TileSourceRecord tileSource, CancellationToken cancToken)
        {
            var result = new List<string>();
            if (tileSource == null) return result;
            if (!tileSource.IsAll && !tileSource.IsNone && !string.IsNullOrWhiteSpace(tileSource.FolderName))
            {
                result.Add(tileSource.FolderName);
            }
            else if (tileSource.IsAll)
            {
                var tileSourcesFolder = await ApplicationData.Current.LocalCacheFolder.TryGetItemAsync(ConstantData.TILE_SOURCES_DIR_NAME).AsTask(cancToken).ConfigureAwait(false) as StorageFolder;
                if (tileSourcesFolder == null) return result;
                // clean everything, even old folders, which were created for tile sources, which are no more available.
                // The folder names are the tile source tech names.
                var subFolders = await tileSourcesFolder.GetFoldersAsync().AsTask(cancToken).ConfigureAwait(false);
                result = subFolders.Select(sf => sf.Name).ToList();

                //foreach (var item in _tileSourcez)
                //{
                //    if (!item.IsDefault && !string.IsNullOrWhiteSpace(item.FolderName))
                //    {
                //        result.Add(item.FolderName);
                //    }
                //}
            }
            return result;
        }
        #endregion tileSourcesMethods

        #region download session methods
        /// <summary>
        /// Sets props to create a download session
        /// </summary>
        /// <param name="isTilesDownloadDesired"></param>
        /// <param name="maxZoom"></param>
        /// <param name="resetLastDownloadSession"></param>
        /// <returns></returns>
        public Task SetTilesDownloadPropsAsync(bool isTilesDownloadDesired, int maxZoom, bool resetLastDownloadSession)
        {
            return RunInUiThreadAsync(delegate
            {
                lock (_lastDownloadLocker)
                {
                    SetTilesDownloadProps2(isTilesDownloadDesired, maxZoom, resetLastDownloadSession);
                }
            });
        }
        private void SetTilesDownloadProps2(bool isTilesDownloadDesired, int maxZoom, bool resetLastDownloadSession)
        {
            if (resetLastDownloadSession) _lastDownloadSession = null;

            // we must handle these two variables together because they belong together.
            // an event handler registered on one and reading both may catch the change in the first before the second has changed.
            bool isIsTilesDownloadDesiredChanged = isTilesDownloadDesired != _isTilesDownloadDesired;

            _isTilesDownloadDesired = isTilesDownloadDesired;
            _maxDesiredZoomForDownloadingTiles = maxZoom;

            if (isIsTilesDownloadDesiredChanged) RaisePropertyChanged(nameof(IsTilesDownloadDesired));
        }
        public async Task<DownloadSession> InitOrReinitDownloadSessionAsync(GeoboundingBox gbb)
        {
            if (gbb == null) return null;

            try
            {
                await _tileSourcezSemaphore.WaitAsync().ConfigureAwait(false);
                IsTileSourcezBusy = true;

                lock (_lastDownloadLocker)
                {
                    try
                    {
                        // last download completed: start a new one with the current tile source
                        if (_lastDownloadSession == null)
                        {
                            var currentTssClone = GetCurrentTileSourcezClone2();
                            _lastDownloadSession = new DownloadSession(gbb, currentTssClone, _maxDesiredZoomForDownloadingTiles);
                            // return a clone
                            return new DownloadSession(_lastDownloadSession.MinZoom, _lastDownloadSession.MaxZoom, _lastDownloadSession.NWCorner, _lastDownloadSession.SECorner, _lastDownloadSession.TileSources);
                        }
                        // last download did not complete: start a new one with the old tile sources
                        // of course, we don't touch the unfinished download session
                        else
                        {
                            var allTssClone = GetAllTileSourcezClone2();
                            var lastDownloadTileSources_StillAvailable = new Collection<TileSourceRecord>();
                            // the tile sources metadata may have changed: reread it basing on the tech names
                            foreach (var techName in _lastDownloadSession.TileSources.Select(ts => ts.TechName))
                            {
                                var tileSource = allTssClone.FirstOrDefault(ts => ts.TechName.Equals(techName));
                                if (tileSource != null) lastDownloadTileSources_StillAvailable.Add(tileSource);
                            }
                            if (lastDownloadTileSources_StillAvailable.Any())
                            {
                                return new DownloadSession(_lastDownloadSession.MinZoom, _lastDownloadSession.MaxZoom, _lastDownloadSession.NWCorner, _lastDownloadSession.SECorner, lastDownloadTileSources_StillAvailable);
                            }
                        }
                    }
                    catch (InvalidDownloadSessionArgumentsException ex)
                    {
                        SetTilesDownloadProps2(false, 0, true);
                        Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
                    }
                    catch (Exception ex)
                    {
                        Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
                    }
                }
            }
            finally
            {
                IsTileSourcezBusy = false;
                SemaphoreSlimSafeRelease.TryRelease(_tileSourcezSemaphore);
            }
            return null;
        }
        public async Task<DownloadSession> GetLargestPossibleDownloadSession4CurrentTileSourcesAsync(GeoboundingBox gbb)
        {
            if (gbb == null) return null;
            var currentTss = await GetCurrentTileSourcezCloneAsync().ConfigureAwait(false);
            return new DownloadSession(gbb, currentTss, 99);
        }
        #endregion download session methods
    }


    public interface IGpsDataModel : INotifyPropertyChanged
    {
        uint DesiredAccuracyInMeters { get; }
        uint ReportIntervalInMilliSec { get; }
        uint BackgroundUpdatePeriodInMinutes { get; }
        uint GetDefaultDesiredAccuracyInMetres { get; }
        bool IsForegroundTracking { get; }
        bool IsBackgroundTracking { get; set; }

        string LastMessage { get; set; }

        Task<bool> AddHistoryRecordAsync(PointRecord dataRecord, bool checkMaxEntries);
    }


    public class DataAlreadyBoundException : Exception
    {
        public DataAlreadyBoundException() : base() { }
        public DataAlreadyBoundException(string message) : base(message) { }
    }
}