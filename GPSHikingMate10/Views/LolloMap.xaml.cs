﻿using Utilz.Controlz;
using LolloGPS.Data;
using LolloGPS.Data.Runtime;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Utilz;
using Windows.Devices.Geolocation;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls.Maps;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236
// the polyline cannot be replaced with a route. The problem is, class MapRoute has no constructor and it is sealed. 
// The only way to create a route seems to be MapRouteFinder, which takes *ages* and it is not what we want here, either.
// Otherwise, you can look here: http://xamlmapcontrol.codeplex.com/SourceControl/latest#MapControl/MapPolyline.cs
// or here: http://phone.codeplex.com/SourceControl/latest
namespace LolloGPS.Core
{
    public sealed partial class LolloMap : OpenableObservableControl, IGeoBoundingBoxProvider, IMapAltProfCentrer, IInfoPanelEventReceiver
    {
        #region properties
        // LOLLO NOTE checkpoints are still expensive to draw, no matter what I tried, 
        // so I set their limit to a low number, which grows with the available memory.
        // This is in the static ctor of PersistentData.
        // It would be nice to have more checkpoints though.
        // I tried MapIcon instead of Images: they are much slower loading but respond much better to map movements! Ellipses are slower than images.
        // Things look better with win 10 on a pc, so I used icons, which don't seem slower than images anymore.
        // Things look better with 10.0.15063: I can now load 5x more checkpoints at a comparable speed.
        // There are still problems tho, updated to 10.0.15063:
        // All MapElements shift a bit when zooming in a lot and panning the map.
        // MapIcons show a grey line between the should-be point and the actual point where the icon is centred.
        // Even using images only for the start, current and end pointers, they glitch when panning the map.
        // The MapItemsControl throws weird errors and it loads slowly.
        internal const double SCALE_IMAGE_WIDTH = 300.0;

        internal const int HISTORY_TAB_INDEX = 20;
        internal const int ROUTE0_TAB_INDEX = 10;
        internal const int CHECKPOINT_TAB_INDEX = 30;
        //internal const string CheckpointTag = "Checkpoint";
        internal const int START_STOP_TAB_INDEX = 40;

        internal const double MIN_LAT = -85.0511;
        internal const double MAX_LAT = 85.0511;
        internal const double MIN_LAT_NEARLY = -80.0;
        internal const double MAX_LAT_NEARLY = 80.0;
        internal const double MIN_LON = -180.0;
        internal const double MAX_LON = 180.0;

        private readonly MapPolyline _mapPolylineRoute0 = new MapPolyline()
        {
            MapTabIndex = ROUTE0_TAB_INDEX,
            StrokeColor = ((SolidColorBrush)(Application.Current.Resources["Route0Brush"])).Color,
            StrokeDashed = false,
        };

        private readonly MapPolyline _mapPolylineHistory = new MapPolyline()
        {
            MapTabIndex = HISTORY_TAB_INDEX,
            StrokeColor = ((SolidColorBrush)(Application.Current.Resources["HistoryBrush"])).Color,
            StrokeDashed = false,
        };
        //private static Image _imageStartHistory = null;
        //private static Image _imageEndHistory = null;
        //private static Image _imageFlyoutPoint = null;

        private readonly MapIcon _iconEndHistory = new MapIcon()
        {
            CollisionBehaviorDesired = MapElementCollisionBehavior.RemainVisible,
            MapTabIndex = START_STOP_TAB_INDEX,
            NormalizedAnchorPoint = _pointersAnchorPoint,
            Visible = true,
        };
        private readonly MapIcon _iconFlyoutPoint = new MapIcon()
        {
            CollisionBehaviorDesired = MapElementCollisionBehavior.RemainVisible,
            MapTabIndex = START_STOP_TAB_INDEX,
            NormalizedAnchorPoint = _pointersAnchorPoint,
            Visible = false,
        };
        private readonly MapIcon _iconStartHistory = new MapIcon()
        {
            CollisionBehaviorDesired = MapElementCollisionBehavior.RemainVisible,
            MapTabIndex = START_STOP_TAB_INDEX,
            NormalizedAnchorPoint = _pointersAnchorPoint,
            Visible = true,
        };

        //private static List<Image> _checkpointImages = new List<Image>();
        //private static BitmapImage _checkpointImageSource = null;
        private readonly RandomAccessStreamReference _checkpointIconStreamReference;
        //private readonly BitmapSource _checkpointImageSource;

        private static readonly Point _checkpointsAnchorPoint = new Point(0.5, 0.5);
        private static readonly Point _pointersAnchorPoint = new Point(0.5, 0.625);

        public PersistentData PersistentData => App.PersistentData;
        public RuntimeData RuntimeData => App.RuntimeData;
        private LolloMapVM _lolloMapVM = null;
        public LolloMapVM LolloMapVM => _lolloMapVM;

        public MainVM MainVM
        {
            get { return (MainVM)GetValue(MainVMProperty); }
            set { SetValue(MainVMProperty, value); }
        }
        public static readonly DependencyProperty MainVMProperty =
            DependencyProperty.Register("MainVM", typeof(MainVM), typeof(LolloMap), new PropertyMetadata(null));

        private static WeakReference _myMapInstance = null;
        internal static MapControl GetMapControlInstance() // for the converters
        {
            return _myMapInstance?.Target as MapControl;
        }

        private bool _isHistoryInMap = false;
        private bool _isRoute0InMap = false;
        private bool _isFlyoutPointInMap = false;

        private readonly SemaphoreSlimSafeRelease _drawSemaphore = new SemaphoreSlimSafeRelease(1, 1);
        #endregion properties


        #region lifecycle
        public LolloMap()
        {
            InitializeComponent();

            ElementsSize screenSize = GetElementsSize();
            switch (screenSize)
            {
                case ElementsSize.Small:
                    _mapPolylineRoute0.StrokeThickness = (double)(Application.Current.Resources["Route0Thickness_SmallScreen"]);
                    _mapPolylineHistory.StrokeThickness = (double)(Application.Current.Resources["HistoryThickness_SmallScreen"]);
                    //_imageStartHistory = new Image() { Source = new BitmapImage(new Uri("ms-appx:///Assets/pointer_start-36.png")) { CreateOptions = BitmapCreateOptions.None }, Stretch = Stretch.None };
                    //_imageEndHistory = new Image() { Source = new BitmapImage(new Uri("ms-appx:///Assets/pointer_end-36.png")) { CreateOptions = BitmapCreateOptions.None }, Stretch = Stretch.None };
                    //_imageFlyoutPoint = new Image() { Source = new BitmapImage(new Uri("ms-appx:///Assets/pointer_current-36.png")) { CreateOptions = BitmapCreateOptions.None }, Stretch = Stretch.None };
                    _iconEndHistory.Image = RandomAccessStreamReference.CreateFromUri(new Uri("ms-appx:///Assets/pointer_end-36.png", UriKind.Absolute));
                    _iconFlyoutPoint.Image = RandomAccessStreamReference.CreateFromUri(new Uri("ms-appx:///Assets/pointer_current-36.png", UriKind.Absolute));
                    _iconStartHistory.Image = RandomAccessStreamReference.CreateFromUri(new Uri("ms-appx:///Assets/pointer_start-36.png", UriKind.Absolute));
                    //_checkpointImageSource = new BitmapImage(new Uri("ms-appx:///Assets/pointer_checkpoint-20.png"));
                    _checkpointIconStreamReference = RandomAccessStreamReference.CreateFromUri(new Uri("ms-appx:///Assets/pointer_checkpoint-20.png", UriKind.Absolute));
                    break;
                case ElementsSize.Large:
                    _mapPolylineRoute0.StrokeThickness = (double)(Application.Current.Resources["Route0Thickness_LargeScreen"]);
                    _mapPolylineHistory.StrokeThickness = (double)(Application.Current.Resources["HistoryThickness_LargeScreen"]);
                    //_imageStartHistory = new Image() { Source = new BitmapImage(new Uri("ms-appx:///Assets/pointer_start-144.png")) { CreateOptions = BitmapCreateOptions.None }, Stretch = Stretch.None };
                    //_imageEndHistory = new Image() { Source = new BitmapImage(new Uri("ms-appx:///Assets/pointer_end-144.png")) { CreateOptions = BitmapCreateOptions.None }, Stretch = Stretch.None };
                    //_imageFlyoutPoint = new Image() { Source = new BitmapImage(new Uri("ms-appx:///Assets/pointer_current-144.png")) { CreateOptions = BitmapCreateOptions.None }, Stretch = Stretch.None };
                    _iconEndHistory.Image = RandomAccessStreamReference.CreateFromUri(new Uri("ms-appx:///Assets/pointer_end-144.png", UriKind.Absolute));
                    _iconFlyoutPoint.Image = RandomAccessStreamReference.CreateFromUri(new Uri("ms-appx:///Assets/pointer_current-144.png", UriKind.Absolute));
                    _iconStartHistory.Image = RandomAccessStreamReference.CreateFromUri(new Uri("ms-appx:///Assets/pointer_start-144.png", UriKind.Absolute));
                    //_checkpointImageSource = new BitmapImage(new Uri("ms-appx:///Assets/pointer_checkpoint-80.png"));
                    _checkpointIconStreamReference = RandomAccessStreamReference.CreateFromUri(new Uri("ms-appx:///Assets/pointer_checkpoint-80.png", UriKind.Absolute));
                    break;
                default:
                    _mapPolylineRoute0.StrokeThickness = (double)(Application.Current.Resources["Route0Thickness_MediumScreen"]);
                    _mapPolylineHistory.StrokeThickness = (double)(Application.Current.Resources["HistoryThickness_MediumScreen"]);
                    //_imageStartHistory = new Image() { Source = new BitmapImage(new Uri("ms-appx:///Assets/pointer_start-72.png")) { CreateOptions = BitmapCreateOptions.None }, Stretch = Stretch.None };
                    //_imageEndHistory = new Image() { Source = new BitmapImage(new Uri("ms-appx:///Assets/pointer_end-72.png")) { CreateOptions = BitmapCreateOptions.None }, Stretch = Stretch.None };
                    //_imageFlyoutPoint = new Image() { Source = new BitmapImage(new Uri("ms-appx:///Assets/pointer_current-72.png")) { CreateOptions = BitmapCreateOptions.None }, Stretch = Stretch.None };
                    _iconEndHistory.Image = RandomAccessStreamReference.CreateFromUri(new Uri("ms-appx:///Assets/pointer_end-72.png", UriKind.Absolute));
                    _iconFlyoutPoint.Image = RandomAccessStreamReference.CreateFromUri(new Uri("ms-appx:///Assets/pointer_current-72.png", UriKind.Absolute));
                    _iconStartHistory.Image = RandomAccessStreamReference.CreateFromUri(new Uri("ms-appx:///Assets/pointer_start-72.png", UriKind.Absolute));
                    //_checkpointImageSource = new BitmapImage(new Uri("ms-appx:///Assets/pointer_checkpoint-40.png"));
                    _checkpointIconStreamReference = RandomAccessStreamReference.CreateFromUri(new Uri("ms-appx:///Assets/pointer_checkpoint-40.png", UriKind.Absolute));
                    break;
            }

            _myMapInstance = new WeakReference(MyMap);

            MyMap.Center = new Geopoint(new BasicGeoposition() { Latitude = 0.0, Longitude = 0.0 });
            MyMap.Style = PersistentData.MapStyle;
            //MyMap.DesiredPitch = 0.0;
            //MyMap.Heading = 0;
            //MyMap.PedestrianFeaturesVisible = false;
            //MyMap.TrafficFlowVisible = false;
            MyMap.LandmarksVisible = false;
            MyMap.MapServiceToken = "xeuSS1khfrzYWD2AMjHz~nlORxc1UiNhK4lHJ8e4L4Q~AuehF7PQr8xsMsMLfbH3LgNQSRPIV8nrjjF0MgFOByiWhJHqeQNFChUUqChPyxW6";
            MyMap.ColorScheme = MapColorScheme.Light; //.Dark
            if (RuntimeData.IsTouchDevicePresent) MyMap.ZoomInteractionMode = MapInteractionMode.GestureOnly;
            else MyMap.ZoomInteractionMode = MapInteractionMode.PointerKeyboardAndControl;
            if (RuntimeData.IsTouchDevicePresent) MyMap.RotateInteractionMode = MapInteractionMode.GestureOnly;
            else MyMap.RotateInteractionMode = MapInteractionMode.PointerKeyboardAndControl;
            MyMap.TiltInteractionMode = MapInteractionMode.Disabled;
            //MyMap.MapElements.Clear(); // no!
        }
        private enum ElementsSize { Small, Medium, Large }
        private ElementsSize GetElementsSize()
        {
            double rawPixelsPerViewPixel = 2.0;
            try
            {
                var displayInformation = Windows.Graphics.Display.DisplayInformation.GetForCurrentView();
                rawPixelsPerViewPixel = displayInformation.RawPixelsPerViewPixel;
                Logger.Add_TPL($"rawPixelsPerViewPixel={rawPixelsPerViewPixel}", Logger.ForegroundLogFilename, Logger.Severity.Info);
            }
            catch (Exception ex)
            {
                Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
            }
            if (rawPixelsPerViewPixel < 1.5) return ElementsSize.Small;
            if (rawPixelsPerViewPixel < 2.5) return ElementsSize.Medium;
            return ElementsSize.Large;
        }
        protected override async Task OpenMayOverrideAsync(object args = null)
        {
            Logger.Add_TPL("LolloMap started OpenMayOverrideAsync()", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
            bool isResuming = args != null && (LifecycleEvents)args == LifecycleEvents.Resuming;
            bool isFileActivating = args != null && (LifecycleEvents)args == LifecycleEvents.NavigatedToAfterFileActivated;

            _isHistoryInMap = false;
            _isRoute0InMap = false;
            _isFlyoutPointInMap = false;

            _lolloMapVM = new LolloMapVM(MyMap.TileSources, this, MainVM);
            await _lolloMapVM.OpenAsync(args);
            RaisePropertyChanged_UI(nameof(LolloMapVM));

            await CompassControl.OpenAsync(args);

            AddHandlers();

            // if the app is file activating, ignore the series that are already present; 
            // the newly read series will draw automatically once they are pushed in and (the chosen one) will be centred with an external command.
            if (isFileActivating) return;

            Task drawH = Task.Run(DrawHistoryAsync);
            if (!isResuming || MainVM.WhichSeriesJustLoaded == PersistentData.Tables.Route0)
            {
                Task drawR = Task.Run(DrawRoute0Async);
            }
            if (!isResuming || MainVM.WhichSeriesJustLoaded == PersistentData.Tables.Checkpoints)
            {
                Task drawC = Task.Run(DrawCheckpointsMapIconsAsync);
                //Task drawC = Task.Run(DrawCheckpointsImagesAsync);
                //Task drawC = Task.Run(DrawCheckpointsMapItemsAsync);
            }
            if (!isResuming)
            {
                var whichSeriesIsJustLoaded = MainVM.WhichSeriesJustLoaded; // I read it now to avoid switching threads later,
                // since dependency props must be read in the UI thread.
                Task restore = Task.Run(() => RestoreViewCenteringAsync(whichSeriesIsJustLoaded));
            }
        }

        protected override async Task CloseMayOverrideAsync(object args = null)
        {
            RemoveHandlers();
            await CompassControl.CloseAsync(args);
            await _lolloMapVM.CloseAsync(args).ConfigureAwait(false);
        }
        #endregion lifecycle


        #region services
        private async Task RestoreViewCenteringAsync(PersistentData.Tables whichSeriesIsJustLoaded)
        {
            try
            {
                await _drawSemaphore.WaitAsync(CancToken).ConfigureAwait(false);

                if (whichSeriesIsJustLoaded == PersistentData.Tables.Nil)
                {
                    Task restore = null;
                    await RunInUiThreadAsync(delegate
                    {
                        double lat = PersistentData.MapLastLat; // always reference these variables in the UI thread, to avoid locks, coz they can be speed-critical
                        double lon = PersistentData.MapLastLon;
                        var gp = new Geopoint(new BasicGeoposition { Latitude = lat, Longitude = lon });
                        double zoom = PersistentData.MapLastZoom;
                        double heading = PersistentData.MapLastHeading;
                        double pitch = PersistentData.MapLastPitch;

                        restore = MyMap.TrySetViewAsync(gp, zoom, heading, pitch, MapAnimationKind.None).AsTask();
                    }).ConfigureAwait(false);
                    await restore.ConfigureAwait(false);
                }
                //else if (whichSeriesJustLoaded == PersistentData.Tables.History) await CentreOnSeriesAsync(PersistentData.History).ConfigureAwait(false);
                //else if (whichSeriesJustLoaded == PersistentData.Tables.Route0) await CentreOnSeriesAsync(PersistentData.Route0).ConfigureAwait(false);
                //else if (whichSeriesJustLoaded == PersistentData.Tables.Checkpoints) await CentreOnSeriesAsync(PersistentData.Checkpoints).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
            }
            finally
            {
                SemaphoreSlimSafeRelease.TryRelease(_drawSemaphore);
            }
        }

        public Task CentreOnHistoryAsync()
        {
            return CentreOnSeriesAsync(PersistentData.History);
        }
        public Task CentreOnRoute0Async()
        {
            return CentreOnSeriesAsync(PersistentData.Route0);
        }
        public Task CentreOnCheckpointsAsync()
        {
            return CentreOnSeriesAsync(PersistentData.Checkpoints);
        }
        private Task CentreOnSeriesAsync(IReadOnlyList<PointRecord> coll)
        {
            try
            {
                if (coll == null) return Task.CompletedTask;
                int cnt = coll.Count;
                if (cnt < 1) return Task.CompletedTask;
                if (cnt == 1 && coll[0] != null && !coll[0].IsEmpty())
                {
                    Geopoint target = new Geopoint(new BasicGeoposition() { Latitude = coll[0].Latitude, Longitude = coll[0].Longitude });
                    return RunInUiThreadAsync(delegate
                    {
                        Task set = MyMap.TrySetViewAsync(target).AsTask(); //, CentreZoomLevel);
                    });
                }
                else if (cnt > 1)
                {
                    double minLongitude = coll.Min(a => a.Longitude);
                    double maxLongitude = coll.Max(a => a.Longitude);
                    double minLatitude = coll.Min(a => a.Latitude);
                    double maxLatitude = coll.Max(a => a.Latitude);

                    var bounds = new GeoboundingBox(
                        new BasicGeoposition { Latitude = maxLatitude, Longitude = minLongitude },
                        new BasicGeoposition { Latitude = minLatitude, Longitude = maxLongitude });
                    return RunInUiThreadAsync(delegate
                    {
                        Task set = MyMap.TrySetViewBoundsAsync(bounds,
                            new Thickness(20), //this is the margin to use in the view
                            MapAnimationKind.Default).AsTask();
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
            }
            return Task.CompletedTask;
        }

        public Task CentreOnCurrentAsync()
        {
            return CentreOnPointAsync(PersistentData.Current, true);
        }
        public Task CentreOnTargetAsync()
        {
            return CentreOnPointAsync(PersistentData.Target, false);
        }
        private Task CentreOnPointAsync(PointRecord point, bool goToEquatorIfPointNull)
        {
            try
            {
                if (point == null && !goToEquatorIfPointNull) return Task.CompletedTask;

                var location = new Geopoint(new BasicGeoposition
                {
                    Altitude = point?.Altitude ?? 0.0,
                    Latitude = point?.Latitude ?? 0.0,
                    Longitude = point?.Longitude ?? 0.0
                });
                return RunInUiThreadAsync(delegate
                {
                    Task c2 = MyMap.TrySetViewAsync(location).AsTask(); //, CentreZoomLevel);
                });
            }
            catch (Exception ex)
            {
                Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
            }
            return Task.CompletedTask;
        }

        public Task Goto2DAsync()
        {
            return RunInUiThreadAsync(delegate
            {
                MyMap.DesiredPitch = 0.0;
                MyMap.Heading = 0.0;
            });
        }

        private async Task DrawHistoryAsync()
        {
            try
            {
                await _drawSemaphore.WaitAsync(CancToken).ConfigureAwait(false);

                List<BasicGeoposition> basicGeoPositions = PersistentData.History.Select(item => new BasicGeoposition() { Altitude = 0.0, Latitude = item.Latitude, Longitude = item.Longitude }).ToList();
                if (!basicGeoPositions.Any())
                {
                    basicGeoPositions.Add(new BasicGeoposition() { Altitude = 0.0, Latitude = PersistentData.Current.Latitude, Longitude = PersistentData.Current.Longitude });
                }

                if (CancToken.IsCancellationRequested) return;

                await RunInUiThreadAsync(delegate
                {
                    // LOLLO TODO when zooming in and panning, the polylines and the MapIcons move about. It was very hard to find a combination of parameters to minimise this,
                    // and they still move about a bit.
                    _mapPolylineHistory.Path = new Geopath(basicGeoPositions, AltitudeReferenceSystem.Ellipsoid); //.Geoid // .Unspecified // .Ellipsoid // .Terrain // //.Surface instead of destroying and redoing, it would be nice to just add the latest point; 
                                                                                                                  // stupidly, _mapPolylineRoute0.Path.Positions is an IReadOnlyList.

                    //MapControl.SetLocation(_imageStartHistory, new Geopoint(basicGeoPositions[0]));
                    //MapControl.SetLocation(_imageEndHistory, new Geopoint(basicGeoPositions.Last()));
                    _iconStartHistory.Location = new Geopoint(basicGeoPositions[0]);
                    _iconEndHistory.Location = new Geopoint(basicGeoPositions.Last());
                    //Better even: use binding; sadly, it is broken for the moment

                    if (CancToken.IsCancellationRequested) return;

                    if (!_isHistoryInMap)
                    {
                        if (!MyMap.MapElements.Contains(_mapPolylineHistory)) MyMap.MapElements.Add(_mapPolylineHistory);
                        //if (!MyMap.Children.Contains(_imageStartHistory)) { MyMap.Children.Add(_imageStartHistory); MapControl.SetNormalizedAnchorPoint(_imageStartHistory, _pointersAnchorPoint); }
                        //if (!MyMap.Children.Contains(_imageEndHistory)) { MyMap.Children.Add(_imageEndHistory); MapControl.SetNormalizedAnchorPoint(_imageEndHistory, _pointersAnchorPoint); }
                        if (!MyMap.MapElements.Contains(_iconStartHistory)) MyMap.MapElements.Add(_iconStartHistory);
                        if (!MyMap.MapElements.Contains(_iconEndHistory)) MyMap.MapElements.Add(_iconEndHistory);
                        _isHistoryInMap = true;
                    }
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
            }
            finally
            {
                SemaphoreSlimSafeRelease.TryRelease(_drawSemaphore);
            }
        }

        private async Task DrawRoute0Async()
        {
            try
            {
                await _drawSemaphore.WaitAsync(CancToken).ConfigureAwait(false);

                List<BasicGeoposition> basicGeoPositions = PersistentData.Route0.Select(item => new BasicGeoposition() { Altitude = 0.0, Latitude = item.Latitude, Longitude = item.Longitude }).ToList();
                if (!basicGeoPositions.Any())
                {
                    basicGeoPositions.Add(new BasicGeoposition() { Altitude = 0.0, Latitude = PersistentData.Current.Latitude, Longitude = PersistentData.Current.Longitude });
                }

                if (CancToken.IsCancellationRequested) return;

                await RunInUiThreadAsync(delegate
                {
                    // LOLLO TODO when zooming in and panning, the polylines and the MapIcons move about. It was very hard to find a combination of parameters to minimise this,
                    // and they still move about a bit.
                    _mapPolylineRoute0.Path = new Geopath(basicGeoPositions, AltitudeReferenceSystem.Ellipsoid); //.Geoid // .Unspecified // .Ellipsoid // .Terrain // .Surface instead of destroying and redoing, it would be nice to just add the latest point; 

                    if (CancToken.IsCancellationRequested) return;

                    if (!_isRoute0InMap)
                    {
                        if (!MyMap.MapElements.Contains(_mapPolylineRoute0))
                        {
                            MyMap.MapElements.Add(_mapPolylineRoute0);
                        }
                        _isRoute0InMap = true;
                    }
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
            }
            finally
            {
                SemaphoreSlimSafeRelease.TryRelease(_drawSemaphore);
            }
        }

        private async Task DrawCheckpointsMapIconsAsync()
        {
            try
            {
                await _drawSemaphore.WaitAsync(CancToken).ConfigureAwait(false);

                List<Geopoint> geoPoints = PersistentData.Checkpoints.Select(item => new Geopoint(new BasicGeoposition() { Altitude = item.Altitude, Latitude = item.Latitude, Longitude = item.Longitude })).ToList();

                if (CancToken.IsCancellationRequested) return;

                await RunInUiThreadAsync(delegate
                {
                    InitCheckpoints_MapIcons();

                    if (CancToken.IsCancellationRequested) return;

#if DEBUG
                    Stopwatch sw0 = new Stopwatch(); sw0.Start();
#endif

                    int j = 0;
                    int howManyMapElements = MyMap.MapElements.Count;
                    int howManyGeopoints = geoPoints.Count;
                    for (int i = 0; i < howManyGeopoints; i++)
                    {
                        while (j < howManyMapElements && (!(MyMap.MapElements[j] is MapIcon) || MyMap.MapElements[j].MapTabIndex != CHECKPOINT_TAB_INDEX))
                        {
                            j++; // MapElement is not a checkpoint: skip to the next element
                        }

                        var mapIcon = MyMap.MapElements[j] as MapIcon;
                        if (mapIcon != null)
                        {
                            mapIcon.Location = geoPoints[i];
                            //(MyMap.MapElements[j] as MapIcon).NormalizedAnchorPoint = new Point(0.5, 0.5);
                            //mapIcon.Title = "LOLLO TODO"; // style the titles
                            mapIcon.Visible = true; // set it last, in the attempt of getting a little more speed
                        }
                        j++;
                    }

                    if (CancToken.IsCancellationRequested) return;

                    for (int i = howManyGeopoints; i < PersistentData.MaxRecordsInCheckpoints; i++)
                    {
                        while (j < howManyMapElements && (!(MyMap.MapElements[j] is MapIcon) || MyMap.MapElements[j].MapTabIndex != CHECKPOINT_TAB_INDEX))
                        {
                            j++; // MapElement is not a checkpoint: skip to the next element
                        }
                        MyMap.MapElements[j].Visible = false;
                        j++;
                    }
                    //Logger.Add_TPL(geoPoints.Count.ToString() + " checkpoints drawn", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
#if DEBUG
                    sw0.Stop(); Debug.WriteLine("attaching checkpoints to map took " + sw0.ElapsedMilliseconds + " msec");
#endif
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
            }
            finally
            {
                SemaphoreSlimSafeRelease.TryRelease(_drawSemaphore);
            }
        }


        private bool InitCheckpoints_MapIcons()
        {
#if DEBUG
            Stopwatch sw0 = new Stopwatch(); sw0.Start();
#endif
            bool isInit = false;
            if (MyMap.MapElements.Count < PersistentData.MaxRecordsInCheckpoints) // only init when you really need it
            {
                Debug.WriteLine("InitCheckpoints_MapIcons() is initialising the checkpoints, because there really are some");
                for (int i = 0; i < PersistentData.MaxRecordsInCheckpoints; i++)
                {
                    MapIcon newIcon = new MapIcon()
                    {
                        CollisionBehaviorDesired = MapElementCollisionBehavior.RemainVisible,
                        Image = _checkpointIconStreamReference,
                        MapTabIndex = CHECKPOINT_TAB_INDEX,
                        NormalizedAnchorPoint = _checkpointsAnchorPoint,
                        Visible = false
                    };

                    MyMap.MapElements.Add(newIcon);
                }
                isInit = true;
                Debug.WriteLine("Checkpoints were initialised");
            }
            else
            {
                isInit = true;
                Debug.WriteLine("Checkpoints were already initialised");
            }
#if DEBUG
            sw0.Stop(); Debug.WriteLine("Initialising checkpoints took " + sw0.ElapsedMilliseconds + " msec");
#endif
            return isInit;
        }
        /*
        private async Task DrawCheckpointsImagesAsync()
        {
            try
            {
                await _drawSemaphore.WaitAsync(CancToken).ConfigureAwait(false);

                List<Geopoint> geoPoints = PersistentData.Checkpoints.Select(item => new Geopoint(new BasicGeoposition() { Altitude = item.Altitude, Latitude = item.Latitude, Longitude = item.Longitude })).ToList();

                if (CancToken.IsCancellationRequested) return;

                await RunInUiThreadAsync(delegate
                {
                    InitCheckpoints_Images();

                    if (CancToken.IsCancellationRequested) return;

#if DEBUG
                    Stopwatch sw0 = new Stopwatch(); sw0.Start();
#endif

                    int howManyMapChildren = MyMap.Children.Count;
                    int howManyGeopoints = geoPoints.Count;
                    for (int i = 0; i < howManyGeopoints; i++)
                    {
                        var image = MyMap.Children[i] as FrameworkElement;
                        if (image != null) {
                            image.Visibility = Visibility.Visible;
                            MapControl.SetLocation(image, geoPoints[i]);
                            MapControl.SetNormalizedAnchorPoint(image, _checkpointsAnchorPoint);
                        }
                    }

                    if (CancToken.IsCancellationRequested) return;

                    for (int i = howManyGeopoints; i < PersistentData.MaxRecordsInCheckpoints; i++)
                    {
                        var image = MyMap.Children[i] as FrameworkElement;
                        if (image != null)
                        {
                            image.Visibility = Visibility.Collapsed;
                        }
                    }
                    //Logger.Add_TPL(geoPoints.Count.ToString() + " checkpoints drawn", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
#if DEBUG
                    sw0.Stop(); Debug.WriteLine("attaching checkpoints to map took " + sw0.ElapsedMilliseconds + " msec");
#endif
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
            }
            finally
            {
                SemaphoreSlimSafeRelease.TryRelease(_drawSemaphore);
            }
        }
        */
        /*
        private bool InitCheckpoints_Images()
        {
#if DEBUG
            Stopwatch sw0 = new Stopwatch(); sw0.Start();
#endif
            bool isInit = false;
            if (MyMap.Children.Count < PersistentData.MaxRecordsInCheckpoints) // only init when you really need it
            {
                Debug.WriteLine("InitCheckpoints_Images() is initialising the checkpoints, because there really are some");
                for (int i = 0; i < PersistentData.MaxRecordsInCheckpoints; i++)
                {
                    //MyMap.Children.Add(_checkpointBaseImage);
                    MyMap.Children.Add(new Image() { Source = _checkpointImageSource, Stretch = Stretch.None, Tag = CheckpointTag, Visibility = Visibility.Collapsed });
                }
                isInit = true;
                Debug.WriteLine("Checkpoints were initialised");
            }
            else
            {
                isInit = true;
                Debug.WriteLine("Checkpoints were already initialised");
            }
#if DEBUG
            sw0.Stop(); Debug.WriteLine("Initialising checkpoints took " + sw0.ElapsedMilliseconds + " msec");
#endif
            return isInit;
        }
        */
        /*
        public class PointOfInterest
        {
            public string DisplayName { get; set; }
            public Geopoint Location { get; set; }
            public BitmapSource ImageSource { get; set; }
            public Point NormalizedAnchorPoint { get; set; }
        }
        
        private async Task DrawCheckpointsMapItemsAsync()
        {
            try
            {
                await _drawSemaphore.WaitAsync(CancToken).ConfigureAwait(false);

                List<PointOfInterest> checkpoints = PersistentData.Checkpoints.Select(item => new PointOfInterest()
                {
                    ImageSource = _checkpointImageSource,
                    Location = new Geopoint(new BasicGeoposition() { Altitude = item.Altitude, Latitude = item.Latitude, Longitude = item.Longitude }),
                    NormalizedAnchorPoint = _checkpointsAnchorPoint

                }).ToList();

                if (CancToken.IsCancellationRequested) return;

                await RunInUiThreadAsync(delegate
                {
                    MapItems.ItemsSource = checkpoints;
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
            }
            finally
            {
                SemaphoreSlimSafeRelease.TryRelease(_drawSemaphore);
            }
        }
        */
        private Task HideFlyoutPointAsync()
        {
            return RunInUiThreadAsync(delegate
            {
                //_imageFlyoutPoint.Visibility = Visibility.Collapsed;
                _iconFlyoutPoint.Visible = false;
            });
        }

        private Task DrawFlyoutPointAsync()
        {
            return RunInUiThreadAsync(delegate
            {
                //_imageFlyoutPoint.Visibility = Visibility.Visible;
                _iconFlyoutPoint.Visible = true;

                //MapControl.SetLocation(_imageFlyoutPoint, new Geopoint(new BasicGeoposition() { Altitude = 0.0, Latitude = PersistentData.Selected.Latitude, Longitude = PersistentData.Selected.Longitude }));
                _iconFlyoutPoint.Location = new Geopoint(new BasicGeoposition() { Altitude = 0.0, Latitude = PersistentData.Selected.Latitude, Longitude = PersistentData.Selected.Longitude });

                if (!_isFlyoutPointInMap)
                {
                    //if (!MyMap.Children.Contains(_imageFlyoutPoint)) { MyMap.Children.Add(_imageFlyoutPoint); MapControl.SetNormalizedAnchorPoint(_imageFlyoutPoint, _pointersAnchorPoint); }
                    if (!MyMap.MapElements.Contains(_iconFlyoutPoint)) MyMap.MapElements.Add(_iconFlyoutPoint);
                    _isFlyoutPointInMap = true;
                }
            });
        }
        #endregion services


        #region IGeoBoundingBoxProvider
        private const double ABSURD_LON = 999.0;
        private static void UpdateMinLon(ref double minLon, BasicGeoposition pos)
        {
            minLon = minLon != ABSURD_LON ? Math.Min(pos.Longitude, minLon) : pos.Longitude;
        }

        private static void UpdateMaxLon(ref double maxLon, BasicGeoposition pos)
        {
            maxLon = maxLon != ABSURD_LON ? Math.Max(pos.Longitude, maxLon) : pos.Longitude;
        }

        public async Task<GeoboundingBox> GetMinMaxLatLonAsync()
        {
            GeoboundingBox output = null;
            await RunInUiThreadAsync(delegate
            {
                Geopoint topLeftGeopoint = new Geopoint(new BasicGeoposition() { Latitude = MAX_LAT, Longitude = 0.0 });
                Geopoint topRightGeopoint = new Geopoint(new BasicGeoposition() { Latitude = MAX_LAT, Longitude = 0.0 });
                Geopoint bottomLeftGeopoint = new Geopoint(new BasicGeoposition() { Latitude = MIN_LAT, Longitude = 0.0 });
                Geopoint bottomRightGeopoint = new Geopoint(new BasicGeoposition() { Latitude = MIN_LAT, Longitude = 0.0 });
                try
                {
                    // when you zoom out and then in with the north pole in the middle of the map, 
                    // then start downloading tiles, MyMap.GetLocationFromOffset() may throw, so we have some extra complexity.
                    double minLon = ABSURD_LON;
                    double maxLon = ABSURD_LON;
                    try
                    {
                        MyMap.GetLocationFromOffset(new Point(0.0, 0.0), out topLeftGeopoint);
                        UpdateMinLon(ref minLon, topLeftGeopoint.Position);
                        UpdateMaxLon(ref maxLon, topLeftGeopoint.Position);
                    }
                    catch { }
                    try
                    {
                        MyMap.GetLocationFromOffset(new Point(MyMap.ActualWidth, 0.0), out topRightGeopoint);
                        UpdateMinLon(ref minLon, topRightGeopoint.Position);
                        UpdateMaxLon(ref maxLon, topRightGeopoint.Position);
                    }
                    catch { }
                    try
                    {
                        MyMap.GetLocationFromOffset(new Point(0.0, MyMap.ActualHeight), out bottomLeftGeopoint);
                        UpdateMinLon(ref minLon, bottomLeftGeopoint.Position);
                        UpdateMaxLon(ref maxLon, bottomLeftGeopoint.Position);
                    }
                    catch { }
                    try
                    {
                        MyMap.GetLocationFromOffset(new Point(MyMap.ActualWidth, MyMap.ActualHeight), out bottomRightGeopoint);
                        UpdateMinLon(ref minLon, bottomRightGeopoint.Position);
                        UpdateMaxLon(ref maxLon, bottomRightGeopoint.Position);
                    }
                    catch { }

                    double minLat = Math.Min(Math.Min(Math.Min(topLeftGeopoint.Position.Latitude, topRightGeopoint.Position.Latitude), bottomLeftGeopoint.Position.Latitude), bottomRightGeopoint.Position.Latitude);
                    double maxLat = Math.Max(Math.Max(Math.Max(topLeftGeopoint.Position.Latitude, topRightGeopoint.Position.Latitude), bottomLeftGeopoint.Position.Latitude), bottomRightGeopoint.Position.Latitude);

                    AdjustMinMaxLatLon(ref minLat, ref maxLat, ref minLon, ref maxLon);

                    output = new GeoboundingBox(new BasicGeoposition() { Latitude = maxLat, Longitude = minLon }, new BasicGeoposition() { Latitude = minLat, Longitude = maxLon });
                }
                catch (Exception ex)
                {
                    Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
                    output = null;
                }
            }).ConfigureAwait(false);
            return output;
        }
        private static void AdjustMinMaxLatLon(ref double minLat, ref double maxLat, ref double minLon, ref double maxLon)
        {
            if (minLat < MIN_LAT) minLat = MIN_LAT;
            if (maxLat < MIN_LAT) maxLat = MIN_LAT;
            if (minLat > MAX_LAT) minLat = MAX_LAT;
            if (maxLat > MAX_LAT) maxLat = MAX_LAT;

            if (minLat > maxLat) LolloMath.Swap(ref minLat, ref maxLat);

            while (minLon < MIN_LON) minLon += 360.0;
            while (maxLon < MIN_LON) maxLon += 360.0;
            while (minLon > MAX_LON) minLon -= 360.0;
            while (maxLon > MAX_LON) maxLon -= 360.0;

            if (minLon > maxLon) LolloMath.Swap(ref minLon, ref maxLon);
        }

        public async Task<BasicGeoposition> GetCentreAsync()
        {
            BasicGeoposition result = default(BasicGeoposition);
            await RunInUiThreadAsync(delegate
            {
                result = MyMap.Center.Position;
            });
            return result;
        }
        #endregion IGeoBoundingBoxProvider


        #region user event handlers
        public event EventHandler<ShowManyPointDetailsRequestedArgs> ShowManyPointDetailsRequested;
        public sealed class ShowManyPointDetailsRequestedArgs : EventArgs
        {
            private readonly List<PointRecord> _selectedRecords;
            public List<PointRecord> SelectedRecords { get { return _selectedRecords; } }
            private readonly List<PersistentData.Tables> _selectedSeriess;
            public List<PersistentData.Tables> SelectedSeriess { get { return _selectedSeriess; } }
            public ShowManyPointDetailsRequestedArgs(List<PointRecord> selectedRecords, List<PersistentData.Tables> selectedSeriess)
            {
                _selectedRecords = selectedRecords;
                _selectedSeriess = selectedSeriess;
            }
        }

        private void OnMap_Tapped(MapControl sender, MapInputEventArgs args)
        {
            Task resp = RunFunctionIfOpenAsyncA(delegate
            {
                try
                {
                    double tappedScreenPointX = args.Position.X;
                    double tappedScreenPointY = args.Position.Y;

                    // pick up the four points delimiting a square centered on the display point that was tapped. Cull the square so it fits into the displayed area.
                    Point topLeftPoint = new Point(Math.Max(tappedScreenPointX - PersistentData.TapTolerance, 0), Math.Max(tappedScreenPointY - PersistentData.TapTolerance, 0));
                    Point topRightPoint = new Point(Math.Min(tappedScreenPointX + PersistentData.TapTolerance, MyMap.ActualWidth), Math.Max(tappedScreenPointY - PersistentData.TapTolerance, 0));
                    Point bottomLeftPoint = new Point(Math.Max(tappedScreenPointX - PersistentData.TapTolerance, 0), Math.Min(tappedScreenPointY + PersistentData.TapTolerance, MyMap.ActualHeight));
                    Point bottomRightPoint = new Point(Math.Min(tappedScreenPointX + PersistentData.TapTolerance, MyMap.ActualWidth), Math.Min(tappedScreenPointY + PersistentData.TapTolerance, MyMap.ActualHeight));

                    Geopoint topLeftGeoPoint = null;
                    Geopoint topRightGeoPoint = null;
                    Geopoint bottomLeftGeoPoint = null;
                    Geopoint bottomRightGeoPoint = null;

                    // pick up the geographic coordinates of those points
                    MyMap.GetLocationFromOffset(topLeftPoint, out topLeftGeoPoint);
                    MyMap.GetLocationFromOffset(topRightPoint, out topRightGeoPoint);
                    MyMap.GetLocationFromOffset(bottomLeftPoint, out bottomLeftGeoPoint);
                    MyMap.GetLocationFromOffset(bottomRightPoint, out bottomRightGeoPoint);

                    // work out the maxes and mins, so you can ignore rotation, pitch etc
                    double minLat = Math.Min(bottomRightGeoPoint.Position.Latitude, Math.Min(bottomLeftGeoPoint.Position.Latitude, Math.Min(topLeftGeoPoint.Position.Latitude, topRightGeoPoint.Position.Latitude)));
                    double maxLat = Math.Max(bottomRightGeoPoint.Position.Latitude, Math.Max(bottomLeftGeoPoint.Position.Latitude, Math.Max(topLeftGeoPoint.Position.Latitude, topRightGeoPoint.Position.Latitude)));
                    double minLon = Math.Min(bottomRightGeoPoint.Position.Longitude, Math.Min(bottomLeftGeoPoint.Position.Longitude, Math.Min(topLeftGeoPoint.Position.Longitude, topRightGeoPoint.Position.Longitude)));
                    double maxLon = Math.Max(bottomRightGeoPoint.Position.Longitude, Math.Max(bottomLeftGeoPoint.Position.Longitude, Math.Max(topLeftGeoPoint.Position.Longitude, topRightGeoPoint.Position.Longitude)));

                    //if a point falls within the square, show its details
                    List<PointRecord> selectedRecords = new List<PointRecord>();
                    List<PersistentData.Tables> selectedSeriess = new List<PersistentData.Tables>();
                    foreach (var item in PersistentData.History)
                    {
                        if (item.Latitude < maxLat && item.Latitude > minLat && item.Longitude > minLon && item.Longitude < maxLon)
                        {
                            selectedRecords.Add(item);
                            selectedSeriess.Add(PersistentData.Tables.History);
                            break; // max 1 record each series
                        }
                    }
                    foreach (var item in PersistentData.Route0)
                    {
                        if (item.Latitude < maxLat && item.Latitude > minLat && item.Longitude > minLon && item.Longitude < maxLon)
                        {
                            selectedRecords.Add(item);
                            selectedSeriess.Add(PersistentData.Tables.Route0);
                            break; // max 1 record each series
                        }
                    }
                    foreach (var item in PersistentData.Checkpoints)
                    {
                        if (item.Latitude < maxLat && item.Latitude > minLat && item.Longitude > minLon && item.Longitude < maxLon)
                        {
                            selectedRecords.Add(item);
                            selectedSeriess.Add(PersistentData.Tables.Checkpoints);
                            break; // max 1 record each series
                        }
                    }
                    if (selectedRecords.Any())
                    {
                        Task vibrate = Task.Run(() => RuntimeData.ShortVibration());
                        ShowManyPointDetailsRequested?.Invoke(this, new ShowManyPointDetailsRequestedArgs(selectedRecords, selectedSeriess));
                    }
                }
                catch (Exception ex) // there may be errors if I tap in an awkward place, such as the arctic
                {
                    Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
                }
            });
        }

        public async void OnInfoPanelPointChanged(object sender, EventArgs e)
        {
            // leave if there is no selected point
            if (!PersistentData.IsSelectedSeriesNonNullAndNonEmpty()) return;
            // draw the selected point
            await DrawFlyoutPointAsync().ConfigureAwait(false);
            // if this panel is being displayed, centre it
            if (MainVM?.IsWideEnough == true || !PersistentData.IsShowingAltitudeProfiles) await CentreOnPointAsync(PersistentData.Selected, false);
        }

        public void OnInfoPanelClosed(object sender, object e)
        {
            Task hide = HideFlyoutPointAsync();
        }

        private async void OnAim_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            Task vibrate = Task.Run(() => RuntimeData.ShortVibration());

            await _lolloMapVM.AddMapCentreToCheckpoints(); // .ConfigureAwait(false);
            if (PersistentData.IsShowAimOnce)
            {
                PersistentData.IsShowAim = false;
            }
        }

        private void OnAim_Holding(object sender, Windows.UI.Xaml.Input.HoldingRoutedEventArgs e)
        {
            PersistentData.IsShowAim = false;
        }

        private void OnProvider_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            Task edge = RunFunctionIfOpenAsyncT(async delegate
            {
                try
                {
                    string uriString = PersistentData.CurrentTileSource.ProviderUriString;
                    if (!string.IsNullOrWhiteSpace(uriString) && RuntimeData.IsConnectionAvailable)
                    {
                        await Launcher.LaunchUriAsync(new Uri(uriString, UriKind.Absolute));
                    }
                }
                catch (Exception) { }
            });
        }
        #endregion user event handlers


        #region data event handlers
        private void AddHandlers()
        {
            if (PersistentData != null)
            {
                PersistentData.PropertyChanged += OnPersistentData_PropertyChanged;
                PersistentData.CurrentChanged += OnPersistentData_CurrentChanged;
                PersistentData.History.CollectionChanged += OnHistory_CollectionChanged;
                PersistentData.Route0.CollectionChanged += OnRoute0_CollectionChanged;
                PersistentData.Checkpoints.CollectionChanged += OnCheckpoints_CollectionChanged;
            }
        }

        private void RemoveHandlers()
        {
            if (PersistentData != null)
            {
                PersistentData.PropertyChanged -= OnPersistentData_PropertyChanged;
                PersistentData.CurrentChanged -= OnPersistentData_CurrentChanged;
                PersistentData.History.CollectionChanged -= OnHistory_CollectionChanged;
                PersistentData.Route0.CollectionChanged -= OnRoute0_CollectionChanged;
                PersistentData.Checkpoints.CollectionChanged -= OnCheckpoints_CollectionChanged;
            }
        }

        private void OnPersistentData_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PersistentData.MapStyle))
            {
                Task ms = RunInUiThreadAsync(delegate
                {
                    MyMap.Style = PersistentData.MapStyle;
                });
            }
            else if (e.PropertyName == nameof(PersistentData.IsShowImperialUnits))
            {
                Task iu = RunInUiThreadAsync(delegate
                {
                    if (PersistentData?.Current != null) PersistentData.Current.SpeedInMetreSec = PersistentData.Current.SpeedInMetreSec;

                    double oldZoom = MyMap.ZoomLevel;
                    double newZoom = oldZoom > MyMap.MinZoomLevel ? Math.Max(MyMap.MinZoomLevel, oldZoom * .99) : MyMap.MinZoomLevel * 1.01;
                    Task det = MyMap.TryZoomToAsync(newZoom).AsTask().ContinueWith(delegate
                    {
                        return MyMap.TryZoomToAsync(oldZoom).AsTask();
                    });
                    // MyMap.ZoomLevel = MyMap.ZoomLevel; does nothing
                });
            }
        }

        private void OnPersistentData_CurrentChanged(object sender, EventArgs e)
        {
            // I must not run to the current point when starting, I want to stick to the last frame when last suspended instead.
            // Unless the tracking is on and the autocentre too.
            if (PersistentData?.IsCentreOnCurrent == true && RuntimeData.IsAllowCentreOnCurrent /*&& !MainVM.IsPointInfoPanelOpen*/)
            {
                Task cen = RunFunctionIfOpenAsyncT(CentreOnCurrentAsync);
            }
        }

        private void OnHistory_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action != NotifyCollectionChangedAction.Reset || !PersistentData.History.Any())
            {
                Task draw = RunFunctionIfOpenAsyncT_MT(async delegate
                {
                    await DrawHistoryAsync().ConfigureAwait(false);
                    // if (e.Action == NotifyCollectionChangedAction.Replace) await CentreOnHistoryAsync().ConfigureAwait(false);
                });
            }
        }

        private void OnRoute0_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action != NotifyCollectionChangedAction.Reset || !PersistentData.Route0.Any())
            {
                Task draw = RunFunctionIfOpenAsyncT_MT(async delegate
                {
                    await DrawRoute0Async().ConfigureAwait(false);
                    // if (e.Action == NotifyCollectionChangedAction.Replace) await CentreOnRoute0Async().ConfigureAwait(false);
                });
            }
        }

        private void OnCheckpoints_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action != NotifyCollectionChangedAction.Reset || !PersistentData.Checkpoints.Any())
            {
                Task draw = RunFunctionIfOpenAsyncT_MT(async delegate
                {
                    // await DrawCheckpointsMapItemsAsync().ConfigureAwait(false);
                    await DrawCheckpointsMapIconsAsync().ConfigureAwait(false);
                    //await DrawCheckpointsImagesAsync().ConfigureAwait(false);
                    // if (e.Action == NotifyCollectionChangedAction.Replace) await CentreOnCheckpointsAsync().ConfigureAwait(false);
                });
            }
        }
        private void OnZoomLevelChanged(MapControl sender, object args)
        {
            if (!IsOpen) return;
            PersistentData.MapLastZoom = sender.ZoomLevel;
        }

        private void OnHeadingChanged(MapControl sender, object args)
        {
            if (!IsOpen) return;
            PersistentData.MapLastHeading = MyMap.Heading;
        }

        private void OnPitchChanged(MapControl sender, object args)
        {
            if (!IsOpen) return;
            PersistentData.MapLastPitch = MyMap.Pitch;
        }

        private void OnCenterChanged(MapControl sender, object args)
        {
            if (!IsOpen) return;
            double? lat = MyMap.Center?.Position.Latitude;
            double? lon = MyMap.Center?.Position.Longitude;
            if (lat != null && lon != null)
            {
                PersistentData.MapLastLat = (double)lat;
                PersistentData.MapLastLon = (double)lon;
            }
        }
        #endregion data event handlers
    }

    #region converters
    public class HeadingConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return 0.0;
            double mapHeading = 0.0;
            double.TryParse(value.ToString(), out mapHeading);
            return -mapHeading;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new Exception("should never get here");
        }
    }

    public class ScaleSizeConverter : IValueConverter
    {
        //private const double VerticalHalfCircM = 20004000.0;
        private const double LatitudeToMetres = 111133.33333333333; //vertical half circumference of earth / 180 degrees
        private static double _lastZoom = 0.0; //remember this to avoid repeating the same calculation
        private static double _imageScaleTransform = 1.0;
        private static string _distRoundedFormatted = "~";
        private static double _rightLabelX = LolloMap.SCALE_IMAGE_WIDTH;

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            MapControl mapControl = LolloMap.GetMapControlInstance();
            double currentZoom = 0.0;
            if (mapControl != null)
            {
                double.TryParse(value.ToString(), out currentZoom);

                if (currentZoom != _lastZoom)
                {
                    try
                    {
                        CalcAlongMeridians(mapControl);
                        _lastZoom = currentZoom; //remember this to avoid repeating the same calculation
                    }
                    catch (Exception ex)
                    {
                        // there may be exceptions if I am in a very awkward place in the map, such as the arctic, and at funny heading angles.
                        // I took care of most, so we log the rest.
                        Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
                    }
                }
            }
            string param = parameter.ToString();
            if (param == "imageScaleTransform")
            {
                return _imageScaleTransform;
            }
            else if (param == "distRounded")
            {
                return _distRoundedFormatted;
            }
            else if (param == "rightLabelX")
            {
                return _rightLabelX;
            }
            else if (param == "techZoom")
            {
                return currentZoom.ToString("zoom #0.#", CultureInfo.CurrentUICulture);
            }
            Debug.WriteLine("ERROR: XAML used a wrong parameter, or no parameter");
            return 1.0; //should never get here
        }

        // LOLLO the mercator formulas are at http://wiki.openstreetmap.org/wiki/Mercator
        // and http://wiki.openstreetmap.org/wiki/EPSG:3857
        // and http://www.maptiler.org/google-maps-coordinates-tile-bounds-projection/
        private static void CalcAlongParallels_Old(MapControl mapControl)
        {
            if (mapControl != null)
            {
                // we work out the distance moving along the meridians, because the parallels are always at the same distance at all latitudes
                // in practise, we can also move along the parallels, it works anyway.
                // we put a hypothetical bar on the map, and we ask the map to measure where it starts and ends.
                // then we compare the bar length with the scale length, so we find out the distance in metres between the scale ends.

                double hypotheticalMeasureBarLength = mapControl.ActualWidth / 3.0; // I use a shortish bar length so it always fits snugly in the MapControl. Too short would be inaccurate.
                                                                                    // The map may be shifted badly; these maps can shift vertically so badly, that the north or the south pole can be in the centre of the control.
                                                                                    // If this happens, we place the hypothetical bar lower or higher, so it is always safely on the Earth.
                                                                                    // Since these shifts happen vertically, it is easier to use a horizontal hypothetical bar.

                double hypotheticalMeasureBarX1 = mapControl.ActualWidth / 2.0;
                double hypotheticalMeasureBarY1 = mapControl.ActualHeight / 2.0;
                if (hypotheticalMeasureBarY1 <= 0.0 || hypotheticalMeasureBarX1 <= 0.0) return;

                Geopoint centre = mapControl.Center;
                if (centre.Position.Latitude >= LolloMap.MAX_LAT_NEARLY) { hypotheticalMeasureBarY1 *= 1.5; /*Debug.WriteLine("halfMapHeight + ");*/ }
                else if (centre.Position.Latitude <= LolloMap.MIN_LAT_NEARLY) { hypotheticalMeasureBarY1 *= .5; /*Debug.WriteLine("halfMapHeight - ");*/ }

                double headingRadians = mapControl.Heading * ConstantData.DEG_TO_RAD;

                //Point pointN = new Point(halfMapWidth, halfMapHeight);

                //                    Point pointS = new Point(halfMapWidth, halfMapHeight + barLength);//this returns funny results when the map is turned: I must always measure along the meridians
                //Point pointS = new Point(halfMapWidth + hypotheticalMeridianBarLength * Math.Sin(headingRadians), halfMapHeight + hypotheticalMeridianBarLength * Math.Cos(headingRadians));
                //double checkIpotenusaMustBeSameAsBarLength = Math.Sqrt((pointN.X - pointS.X) * (pointN.X - pointS.X) + (pointN.Y - pointS.Y) * (pointN.Y - pointS.Y)); //remove when done testing
                //Geopoint locationN = null; Geopoint locationS = null;
                //mapControl.GetLocationFromOffset(pointN, out locationN);
                //mapControl.GetLocationFromOffset(pointS, out locationS);

                Point pointW = new Point(hypotheticalMeasureBarX1, hypotheticalMeasureBarY1);
                Point pointE = new Point(
                    hypotheticalMeasureBarX1 + hypotheticalMeasureBarLength * Math.Sin(ConstantData.PI_HALF - headingRadians),
                    hypotheticalMeasureBarY1 - hypotheticalMeasureBarLength * Math.Cos(ConstantData.PI_HALF - headingRadians));
                // if(pointE.X > 360.0 || pointW.X > 360.0) { }
                Geopoint locationW = null;
                Geopoint locationE = null; // PointE = 315, 369 throws an error coz locationE cannot be resolved. It happens on start, not every time.
                mapControl.GetLocationFromOffset(pointW, out locationW);
                mapControl.GetLocationFromOffset(pointE, out locationE);

                //double scaleEndsDistanceMetres = Math.Abs(locationN.Position.Latitude - locationS.Position.Latitude) * LatitudeToMetres * LolloMap.SCALE_IMAGE_WIDTH / (hypotheticalMeridianBarLength + 1); //need the abs for when the map is rotated;
                double scaleEndsDistanceMetres = Math.Abs(locationE.Position.Longitude - locationW.Position.Longitude) * LatitudeToMetres * LolloMap.SCALE_IMAGE_WIDTH / (hypotheticalMeasureBarLength + 1); //need the abs for when the map is rotated;

                double distInChosenUnit = 0.0;
                double distInChosenUnitRounded = 0.0;

                if (PersistentData.GetInstance().IsShowImperialUnits)
                {
                    if (scaleEndsDistanceMetres > ConstantData.MILE_TO_M)
                    {
                        distInChosenUnit = scaleEndsDistanceMetres / ConstantData.MILE_TO_M;
                        distInChosenUnitRounded = GetDistRounded(distInChosenUnit);
                        _distRoundedFormatted = distInChosenUnitRounded.ToString(CultureInfo.CurrentUICulture) + " mi";
                    }
                    else
                    {
                        distInChosenUnit = scaleEndsDistanceMetres * ConstantData.M_TO_FOOT;
                        distInChosenUnitRounded = GetDistRounded(distInChosenUnit);
                        _distRoundedFormatted = distInChosenUnitRounded.ToString(CultureInfo.CurrentUICulture) + " ft";
                    }
                }
                else
                {
                    if (scaleEndsDistanceMetres > ConstantData.KM_TO_M)
                    {
                        distInChosenUnit = scaleEndsDistanceMetres / ConstantData.KM_TO_M;
                        distInChosenUnitRounded = GetDistRounded(distInChosenUnit);
                        _distRoundedFormatted = distInChosenUnitRounded.ToString(CultureInfo.CurrentUICulture) + " km";
                    }
                    else
                    {
                        distInChosenUnit = scaleEndsDistanceMetres;
                        distInChosenUnitRounded = GetDistRounded(distInChosenUnit);
                        _distRoundedFormatted = distInChosenUnitRounded.ToString(CultureInfo.CurrentUICulture) + " m";
                    }
                }

                if (distInChosenUnit != 0.0) _imageScaleTransform = distInChosenUnitRounded / distInChosenUnit;
                else _imageScaleTransform = 999.999;

                _rightLabelX = LolloMap.SCALE_IMAGE_WIDTH * _imageScaleTransform;
            }
        }
        private static void CalcAlongMeridians(MapControl mapControl)
        {
            if (mapControl != null)
            {
                // we work out the distance moving along the meridians, because the parallels are always at the same distance at all latitudes
                // in practise, we can also move along the parallels, it works anyway.
                // we put a hypothetical bar on the map, and we ask the map to measure where it starts and ends.
                // then we compare the bar length with the scale length, so we find out the distance in metres between the scale ends.

                double hypotheticalMeasureBarLength = Math.Min(mapControl.ActualHeight, mapControl.ActualWidth) * .25; // I use a shortish bar length so it always fits snugly in the MapControl. Too short would be inaccurate.
                                                                                                                       // The map may be shifted badly; these maps can shift vertically so badly, that the north or the south pole can be in the centre of the control.
                                                                                                                       // If this happens, we place the hypothetical bar lower or higher, so it is always safely on the Earth.
                                                                                                                       // Since these shifts happen vertically, it is easier to use a horizontal hypothetical bar.

                double hypotheticalMeasureBarX1 = 0.0;
                double hypotheticalMeasureBarY1 = 0.0;
                if (mapControl.Center.Position.Latitude >= LolloMap.MAX_LAT_NEARLY)
                {
                    if (mapControl.Heading < 90.0 || mapControl.Heading > 270.0) hypotheticalMeasureBarY1 = mapControl.ActualHeight * .6;
                    else hypotheticalMeasureBarY1 = mapControl.ActualHeight * .4;
                    if (mapControl.Heading < 180.0) hypotheticalMeasureBarX1 = mapControl.ActualWidth * .9;
                    else hypotheticalMeasureBarX1 = mapControl.ActualWidth * .1;
                }
                else if (mapControl.Center.Position.Latitude <= LolloMap.MIN_LAT_NEARLY)
                {
                    if (mapControl.Heading < 90.0 || mapControl.Heading > 270.0) hypotheticalMeasureBarY1 = mapControl.ActualHeight * .1;
                    else hypotheticalMeasureBarY1 = mapControl.ActualHeight * .9;
                    if (mapControl.Heading < 180.0) hypotheticalMeasureBarX1 = mapControl.ActualWidth * .1;
                    else hypotheticalMeasureBarX1 = mapControl.ActualWidth * .9;
                }
                else
                {
                    hypotheticalMeasureBarY1 = mapControl.ActualHeight * .5;
                    hypotheticalMeasureBarX1 = mapControl.ActualWidth * .5;
                }
                if (hypotheticalMeasureBarY1 <= 0.0 || hypotheticalMeasureBarX1 <= 0.0) return;

                double headingRadians = mapControl.Heading * ConstantData.DEG_TO_RAD;
                var pointN = new Point(hypotheticalMeasureBarX1, hypotheticalMeasureBarY1);
                var pointS = new Point(
                    hypotheticalMeasureBarX1 + hypotheticalMeasureBarLength * Math.Sin(headingRadians),
                    hypotheticalMeasureBarY1 + hypotheticalMeasureBarLength * Math.Cos(headingRadians));
                //double checkIpotenusaMustBeSameAsBarLength = Math.Sqrt((pointN.X - pointS.X) * (pointN.X - pointS.X) + (pointN.Y - pointS.Y) * (pointN.Y - pointS.Y)); //remove when done testing
                //Debug.WriteLine("ipotenusa = " + checkIpotenusaMustBeSameAsBarLength);
                //Debug.WriteLine("bar length = " + hypotheticalMeasureBarLength);
                Geopoint locationN = null;
                Geopoint locationS = null; // PointE = 315, 369 throws an error coz locationE cannot be resolved. It happens on start, not every time.
                mapControl.GetLocationFromOffset(pointN, out locationN);
                mapControl.GetLocationFromOffset(pointS, out locationS);

                double scaleEndsDistanceMetres = Math.Abs(locationN.Position.Latitude - locationS.Position.Latitude) * LatitudeToMetres * LolloMap.SCALE_IMAGE_WIDTH / (hypotheticalMeasureBarLength + 1); //need the abs for when the map is rotated;

                double distInChosenUnit = 0.0;
                double distInChosenUnitRounded = 0.0;

                if (PersistentData.GetInstance().IsShowImperialUnits)
                {
                    if (scaleEndsDistanceMetres > ConstantData.MILE_TO_M)
                    {
                        distInChosenUnit = scaleEndsDistanceMetres / ConstantData.MILE_TO_M;
                        distInChosenUnitRounded = GetDistRounded(distInChosenUnit);
                        _distRoundedFormatted = distInChosenUnitRounded.ToString(CultureInfo.CurrentUICulture) + " mi";
                    }
                    else
                    {
                        distInChosenUnit = scaleEndsDistanceMetres * ConstantData.M_TO_FOOT;
                        distInChosenUnitRounded = GetDistRounded(distInChosenUnit);
                        _distRoundedFormatted = distInChosenUnitRounded.ToString(CultureInfo.CurrentUICulture) + " ft";
                    }
                }
                else
                {
                    if (scaleEndsDistanceMetres > ConstantData.KM_TO_M)
                    {
                        distInChosenUnit = scaleEndsDistanceMetres / ConstantData.KM_TO_M;
                        distInChosenUnitRounded = GetDistRounded(distInChosenUnit);
                        _distRoundedFormatted = distInChosenUnitRounded.ToString(CultureInfo.CurrentUICulture) + " km";
                    }
                    else
                    {
                        distInChosenUnit = scaleEndsDistanceMetres;
                        distInChosenUnitRounded = GetDistRounded(distInChosenUnit);
                        _distRoundedFormatted = distInChosenUnitRounded.ToString(CultureInfo.CurrentUICulture) + " m";
                    }
                }

                if (distInChosenUnit != 0.0) _imageScaleTransform = distInChosenUnitRounded / distInChosenUnit;
                else _imageScaleTransform = 999.999;

                _rightLabelX = LolloMap.SCALE_IMAGE_WIDTH * _imageScaleTransform;
            }
        }

        /// <summary>
        /// work out next lower round distance because the scale must always be very round
        /// </summary>
        /// <param name="distMetres"></param>
        /// <returns>double</returns>
        private static double GetDistRounded(double distMetres)
        {
            string distMetres_String = Math.Truncate(distMetres).ToString(CultureInfo.InvariantCulture);

            int howManyZeroesToBeAdded = distMetres_String.Length - 1;

            string distStrRounded = distMetres_String.Substring(0, 1);
            if (distStrRounded == "3" || distStrRounded == "4") distStrRounded = "2";
            else if (distStrRounded == "6" || distStrRounded == "7" || distStrRounded == "8" || distStrRounded == "9") distStrRounded = "5";

            for (int i = 0; i < howManyZeroesToBeAdded; i++)
            {
                distStrRounded += "0";
            }
            double distRounded = 0.0;
            double.TryParse(distStrRounded, out distRounded);
            return distRounded;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new Exception("should never get here");
        }
    }
    #endregion converters
}