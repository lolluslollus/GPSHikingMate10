using LolloBaseUserControls;
using LolloGPS.Data;
using LolloGPS.Data.Constants;
using LolloGPS.Data.Runtime;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236
// the polyline cannot be replaced with a route. The problem is, class MapRoute has no constructor and it is sealed. 
// The only way to create a route seems to be MapRouteFinder, which takes *ages* and it is not what we want here, either.
// Otherwise, you can look here: http://xamlmapcontrol.codeplex.com/SourceControl/latest#MapControl/MapPolyline.cs
// or here: http://phone.codeplex.com/SourceControl/latest
namespace LolloGPS.Core
{
	public sealed partial class LolloMap : OpObsOrControl, IGeoBoundingBoxProvider, IMapApController, IInfoPanelEventReceiver
	{
		#region properties
		// LOLLO TODO landmarks are still expensive to draw, no matter what I tried, so I set their limit to a low number, depending on the available memory. 
		// It would be nice to have more landmarks though.
		// I tried MapIcon instead of Images: they are much slower loading but respond better to map movements! Ellipses are slower than images.
		// Things look better with win 10 on a pc, so I raised the landmark limit depending on the available memory and used icons, which don't seem slower than images anymore.
		// The MapItemsControl throws weird errors and it loads slowly.
		internal const double SCALE_IMAGE_WIDTH = 300.0;

		internal const int HISTORY_TAB_INDEX = 20;
		internal const int ROUTE0_TAB_INDEX = 10;
		internal const int LANDMARK_TAB_INDEX = 30;
		//internal const string LandmarkTag = "Landmark";
		internal const int START_STOP_TAB_INDEX = 40;

		internal const double MIN_LAT = -85.0511;
		internal const double MAX_LAT = 85.0511;
		internal const double MIN_LAT_NEARLY = -80.0;
		internal const double MAX_LAT_NEARLY = 80.0;
		internal const double MIN_LON = -180.0;
		internal const double MAX_LON = 180.0;

		//private Point _landmarksNormalisedIconPoint = new Point(0.5, 0.5);
		//public Point LandmarksNormalisedIconPoint { get { return _landmarksNormalisedIconPoint; } }

		private MapPolyline _mapPolylineRoute0 = new MapPolyline()
		{
			StrokeColor = ((SolidColorBrush)(App.Current.Resources["Route0Brush"])).Color,
			StrokeThickness = (double)(App.Current.Resources["Route0Thickness"]),
			MapTabIndex = ROUTE0_TAB_INDEX,
		};

		private MapPolyline _mapPolylineHistory = new MapPolyline()
		{
			StrokeColor = ((SolidColorBrush)(App.Current.Resources["HistoryBrush"])).Color,
			StrokeThickness = (double)(App.Current.Resources["HistoryThickness"]),
			MapTabIndex = HISTORY_TAB_INDEX,
		};
		//private static Image _imageStartHistory = new Image() { Source = new BitmapImage(new Uri("ms-appx:///Assets/pointer_start-36.png")) { CreateOptions = BitmapCreateOptions.None }, Stretch = Stretch.None };
		//private static Image _imageEndHistory = new Image() { Source = new BitmapImage(new Uri("ms-appx:///Assets/pointer_end-36.png")) { CreateOptions = BitmapCreateOptions.None }, Stretch = Stretch.None };
		//private static Image _imageFlyoutPoint = new Image() { Source = new BitmapImage(new Uri("ms-appx:///Assets/pointer_current-36.png")) { CreateOptions = BitmapCreateOptions.None }, Stretch = Stretch.None };
		private MapIcon _iconStartHistory = new MapIcon()
		{
			CollisionBehaviorDesired = MapElementCollisionBehavior.RemainVisible,
			Image = RandomAccessStreamReference.CreateFromUri(new Uri("ms-appx:///Assets/pointer_start-36.png", UriKind.Absolute)),
			MapTabIndex = START_STOP_TAB_INDEX,
			NormalizedAnchorPoint = new Point(0.5, 0.625),
			Visible = true,
		};
		private MapIcon _iconEndHistory = new MapIcon()
		{
			CollisionBehaviorDesired = MapElementCollisionBehavior.RemainVisible,
			Image = RandomAccessStreamReference.CreateFromUri(new Uri("ms-appx:///Assets/pointer_end-36.png", UriKind.Absolute)),
			MapTabIndex = START_STOP_TAB_INDEX,
			NormalizedAnchorPoint = new Point(0.5, 0.625),
			Visible = true,
		};
		private MapIcon _iconFlyoutPoint = new MapIcon()
		{
			CollisionBehaviorDesired = MapElementCollisionBehavior.RemainVisible,
			Image = RandomAccessStreamReference.CreateFromUri(new Uri("ms-appx:///Assets/pointer_current-36.png", UriKind.Absolute)),
			MapTabIndex = START_STOP_TAB_INDEX,
			NormalizedAnchorPoint = new Point(0.5, 0.625),
			Visible = false,
		};
		private RandomAccessStreamReference _landmarkIconStreamReference;
		//private static Image _landmarkBaseImage = new Image() { Source = new BitmapImage(new Uri("ms-appx:///Assets/pointer_landmark-8.png")) { CreateOptions = BitmapCreateOptions.None }, Stretch = Stretch.None };
		// this uses a new "simple" icon with only 4 bits, so it's much faster to draw
		//private static Image _landmarkBaseImage = new Image() { Source = new BitmapImage(new Uri("ms-appx:///Assets/pointer_landmark_simple-8.png")) { CreateOptions = BitmapCreateOptions.None }, Stretch = Stretch.None };
		//private static Image _landmarkBaseImage = new Image() { Source = new BitmapImage(new Uri("ms-appx:///Assets/pointer_landmark_simple-16.png")) { CreateOptions = BitmapCreateOptions.None }, Stretch = Stretch.None };

		//private static Image _landmarkBaseImage = new Image() { Source = new BitmapImage(new Uri("ms-appx:///Assets/pointer_landmark-20.png")) { CreateOptions = BitmapCreateOptions.None }, Stretch = Stretch.None };
		//private static List<Image> _landmarkImages = new List<Image>();

		private readonly Point LANDMARKS_ANCHOR_POINT = new Point(0.5, 0.5);

		public PersistentData MyPersistentData { get { return App.PersistentData; } }
		public RuntimeData MyRuntimeData { get { return App.MyRuntimeData; } }
		private LolloMap_VM _myVM = null;
		public LolloMap_VM MyVM { get { return _myVM; } }
		public Main_VM MyMainVM { get { return Main_VM.GetInstance(); } }

		private static WeakReference _myMapInstance = null;
		internal static MapControl GetMapControlInstance() // for the converters
		{
			return _myMapInstance?.Target as MapControl;
		}
		#endregion properties


		#region lifecycle
		public LolloMap()
		{
			InitializeComponent();
			_myVM = new LolloMap_VM(MyMap.TileSources, this as IGeoBoundingBoxProvider, this as IMapApController);

			_myMapInstance = new WeakReference(MyMap);

			MyMap.Style = MyPersistentData.MapStyle;
			MyMap.DesiredPitch = 0.0;
			MyMap.Heading = 0;
			MyMap.TrafficFlowVisible = false;
			MyMap.LandmarksVisible = true;
			MyMap.MapServiceToken = "xeuSS1khfrzYWD2AMjHz~nlORxc1UiNhK4lHJ8e4L4Q~AuehF7PQr8xsMsMLfbH3LgNQSRPIV8nrjjF0MgFOByiWhJHqeQNFChUUqChPyxW6"; // "b77a5c561934e089"; // "t8Ko1RpGcknITinQoF1IdA"; // "b77a5c561934e089";
			MyMap.PedestrianFeaturesVisible = true;
			MyMap.ColorScheme = MapColorScheme.Light; //.Dark
			MyMap.ZoomInteractionMode = MapInteractionMode.GestureOnly; // .GestureAndControl;
																		//MyMap.MapElements.Clear(); // no!
		}
		protected override async Task OpenMayOverrideAsync()
		{
			MyMap.Style = MyPersistentData.MapStyle; // maniman
			_landmarkIconStreamReference = RandomAccessStreamReference.CreateFromUri(new Uri("ms-appx:///Assets/pointer_landmark-20.png", UriKind.Absolute));
			await RestoreViewAsync();
			_myVM.Open();

			InitMapElements();

			AddHandlers();

			DrawHistory();
			// when resuming, skip drawing the series, which do not update in the background
			if (!((App)Application.Current).IsResuming)
			{
				DrawRoute0();
				DrawLandmarks();
			}
		}

		protected override Task CloseMayOverrideAsync()
		{
			RemoveHandlers();
			// save last map settings
			try
			{
				MyPersistentData.MapLastLat = MyMap.Center.Position.Latitude;
				MyPersistentData.MapLastLon = MyMap.Center.Position.Longitude;
				MyPersistentData.MapLastHeading = MyMap.Heading;
				MyPersistentData.MapLastPitch = MyMap.Pitch;
				MyPersistentData.MapLastZoom = MyMap.ZoomLevel;
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}
			_myVM?.Close();
			//MyPointInfoPanel?.Close();

			return Task.CompletedTask;
		}
		///// <summary>
		///// This is the only Activate that is async. It cannot take too long, it merely centers and zooms the map.
		///// Otherwise, we don't want to hog the UI thread with our Activate() !!
		///// </summary>
		///// <returns></returns>
		//public async Task OpenAsync()
		//{
		//	if (_isClosing) return;
		//	try
		//	{
		//		MyMap.Style = MyPersistentData.MapStyle; // maniman
		//		_landmarkIconStreamReference = RandomAccessStreamReference.CreateFromUri(new Uri("ms-appx:///Assets/pointer_landmark-20.png", UriKind.Absolute));
		//		await RestoreViewAsync();
		//		_myVM.Open();

		//		InitMapElements();

		//		if (_isClosing) return;
		//		DrawHistory();
		//		DrawRoute0();

		//		if (_isClosing) return;
		//		await DrawLandmarksAsync();
		//		if (_isClosing) return;
		//		AddHandlerThisEvents();

		//		Logger.Add_TPL("LolloMap opened", Logger.AppEventsLogFilename, Logger.Severity.Info);

		//	}
		//	catch (Exception ex)
		//	{
		//		await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename).ConfigureAwait(false);
		//	}
		//}
		//public void Close()
		//{
		//	try
		//	{
		//		_isClosing = true;

		//		RemoveHandlerThisEvents();
		//		// save last map settings
		//		try
		//		{
		//			MyPersistentData.MapLastLat = MyMap.Center.Position.Latitude;
		//			MyPersistentData.MapLastLon = MyMap.Center.Position.Longitude;
		//			MyPersistentData.MapLastHeading = MyMap.Heading;
		//			MyPersistentData.MapLastPitch = MyMap.Pitch;
		//			MyPersistentData.MapLastZoom = MyMap.ZoomLevel;
		//		}
		//		catch (Exception ex)
		//		{
		//			Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
		//		}
		//		_myVM?.Close();
		//		MyPointInfoPanel?.Close();
		//	}
		//	catch (Exception ex)
		//	{
		//		Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
		//	}
		//	finally
		//	{
		//		_isClosing = false;
		//	}
		//}
		#endregion lifecycle


		#region services
		private Task RestoreViewAsync()
		{
			try
			{
				Geopoint gp = new Geopoint(new BasicGeoposition() { Latitude = MyPersistentData.MapLastLat, Longitude = MyPersistentData.MapLastLon });
				return RunInUiThreadAsync(delegate
				{
					Task set = MyMap.TrySetViewAsync(gp, MyPersistentData.MapLastZoom, MyPersistentData.MapLastHeading, MyPersistentData.MapLastPitch, MapAnimationKind.None).AsTask();
				});
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}
			return Task.CompletedTask;
		}

		private Task CentreOnCurrentAsync()
		{
			try
			{
				Geopoint newCentre = null;
				if (MyPersistentData.Current != null && !MyPersistentData.Current.IsEmpty())
				{
					newCentre = new Geopoint(new BasicGeoposition() { Latitude = MyPersistentData.Current.Latitude, Longitude = MyPersistentData.Current.Longitude });
				}
				else
				{
					newCentre = new Geopoint(new BasicGeoposition() { Latitude = 0.0, Longitude = 0.0 });
				}
				return RunInUiThreadAsync(delegate
				{
					Task set = MyMap.TrySetViewAsync(newCentre).AsTask(); //, CentreZoomLevel);
				});
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}
			return Task.CompletedTask;
		}
		private Task CentreAsync(Collection<PointRecord> coll)
		{
			try
			{
				if (coll == null || coll.Count < 1) return Task.CompletedTask;
				else if (coll.Count == 1 && coll[0] != null && !coll[0].IsEmpty())
				{
					Geopoint target = new Geopoint(new BasicGeoposition() { Latitude = coll[0].Latitude, Longitude = coll[0].Longitude });
					return RunInUiThreadAsync(delegate
					{
						Task set = MyMap.TrySetViewAsync(target).AsTask(); //, CentreZoomLevel);
					});
				}
				else if (coll.Count > 1)
				{
					double _minLongitude = default(double);
					double _maxLongitude = default(double);
					double _minLatitude = default(double);
					double _maxLatitude = default(double);

					_minLongitude = coll.Min(a => a.Longitude);
					_maxLongitude = coll.Max(a => a.Longitude);
					_minLatitude = coll.Min(a => a.Latitude);
					_maxLatitude = coll.Max(a => a.Latitude);

					return RunInUiThreadAsync(delegate
					{
						Task set = MyMap.TrySetViewBoundsAsync(new GeoboundingBox(
							new BasicGeoposition() { Latitude = _maxLatitude, Longitude = _minLongitude },
							new BasicGeoposition() { Latitude = _minLatitude, Longitude = _maxLongitude }),
							new Thickness(20), //this is the margin to use in the view
							MapAnimationKind.Default
							).AsTask();
					});
				}
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}
			return Task.CompletedTask;
		}
		public Task CentreOnLandmarksAsync()
		{
			return CentreAsync(MyPersistentData.Landmarks);
		}
		public Task CentreOnRoute0Async()
		{
			return CentreAsync(MyPersistentData.Route0);
		}
		public Task CentreOnHistoryAsync()
		{
			return CentreAsync(MyPersistentData.History);
		}
		public Task CentreOnSeriesAsync(PersistentData.Tables series)
		{
			if (series == PersistentData.Tables.History) return CentreOnHistoryAsync();
			else if (series == PersistentData.Tables.Route0) return CentreOnRoute0Async();
			else if (series == PersistentData.Tables.Landmarks) return CentreOnLandmarksAsync();
			else return Task.CompletedTask;
		}
		public Task CentreOnTargetAsync()
		{
			try
			{
				if (MyPersistentData != null && MyPersistentData.Target != null)
				{
					Geopoint location = new Geopoint(new BasicGeoposition()
					{
						Altitude = MyPersistentData.Target.Altitude,
						Latitude = MyPersistentData.Target.Latitude,
						Longitude = MyPersistentData.Target.Longitude
					});
					return RunInUiThreadAsync(delegate
					{
						Task c2 = MyMap.TrySetViewAsync(location).AsTask(); //, CentreZoomLevel);
					});
				}
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}
			return Task.CompletedTask;
		}
		private Task CentreOnSelectedPointAsync()
		{
			try
			{
				if (MyPersistentData != null && MyPersistentData.Selected != null)
				{
					Geopoint location = new Geopoint(new BasicGeoposition() { Latitude = MyPersistentData.Selected.Latitude, Longitude = MyPersistentData.Selected.Longitude });
					return RunInUiThreadAsync(delegate
					{
						Task set = MyMap.TrySetViewAsync(location).AsTask(); //, CentreZoomLevel);
					});
				}
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

		private bool _isHistoryInMap = false;
		private bool _isRoute0InMap = false;
		private bool _isFlyoutPointInMap = false;
		/// <summary>
		/// Initialises all map elements except for landmarks, which have their dedicated method
		/// </summary>
		private void InitMapElements()
		{
			_isHistoryInMap = false;
			_isRoute0InMap = false;
			_isFlyoutPointInMap = false;
		}
		private void DrawHistory()
		{
			try
			{
				List<BasicGeoposition> basicGeoPositions = new List<BasicGeoposition>();
				try
				{
					foreach (var item in MyPersistentData.History)
					{
						basicGeoPositions.Add(new BasicGeoposition() { Altitude = item.Altitude, Latitude = item.Latitude, Longitude = item.Longitude });
					}
				}
				catch (OutOfMemoryException)
				{
					var howMuchMemoryLeft = GC.GetTotalMemory(true); // LOLLO this is probably too late! Let's hope it does not happen since MyPersistentData puts a limit on the points.
				}

				if (basicGeoPositions.Count > 0)
				{
					_mapPolylineHistory.Path = new Geopath(basicGeoPositions); // instead of destroying and redoing, it would be nice to just add the latest point; 
																			   // stupidly, _mapPolylineRoute0.Path.Positions is an IReadOnlyList.
																			   //MapControl.SetLocation(_imageStartHistory, new Geopoint(basicGeoPositions[0]));
																			   //MapControl.SetLocation(_imageEndHistory, new Geopoint(basicGeoPositions[basicGeoPositions.Count - 1]));
					_iconStartHistory.Location = new Geopoint(basicGeoPositions[0]);
					_iconEndHistory.Location = new Geopoint(basicGeoPositions[basicGeoPositions.Count - 1]);
				}
				//Better even: use binding; sadly, it is broken for the moment
				else
				{
					BasicGeoposition lastGeoposition = new BasicGeoposition() { Altitude = MyPersistentData.Current.Altitude, Latitude = MyPersistentData.Current.Latitude, Longitude = MyPersistentData.Current.Longitude };
					basicGeoPositions.Add(lastGeoposition);
					_mapPolylineHistory.Path = new Geopath(basicGeoPositions);
					//MapControl.SetLocation(_imageStartHistory, new Geopoint(lastGeoposition));
					//MapControl.SetLocation(_imageEndHistory, new Geopoint(lastGeoposition));
					_iconStartHistory.Location = new Geopoint(lastGeoposition);
					_iconEndHistory.Location = new Geopoint(lastGeoposition);
				}

				if (!_isHistoryInMap)
				{
					if (!MyMap.MapElements.Contains(_mapPolylineHistory)) MyMap.MapElements.Add(_mapPolylineHistory);
					//if (!MyMap.Children.Contains(_imageStartHistory)) MyMap.Children.Add(_imageStartHistory);
					//if (!MyMap.Children.Contains(_imageEndHistory)) MyMap.Children.Add(_imageEndHistory);
					if (!MyMap.MapElements.Contains(_iconStartHistory)) MyMap.MapElements.Add(_iconStartHistory);
					if (!MyMap.MapElements.Contains(_iconEndHistory)) MyMap.MapElements.Add(_iconEndHistory);
					_isHistoryInMap = true;
				}

			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}
		}

		private void DrawRoute0()
		{
			try
			{
				List<BasicGeoposition> basicGeoPositions = new List<BasicGeoposition>();
				try
				{
					foreach (var item in MyPersistentData.Route0)
					{
						basicGeoPositions.Add(new BasicGeoposition() { Altitude = item.Altitude, Latitude = item.Latitude, Longitude = item.Longitude });
					}
				}
				catch (OutOfMemoryException)
				{
					var howMuchMemoryLeft = GC.GetTotalMemory(true); // LOLLO this is probably too late! Let's hope it does not happen since MyPersistentData puts a limit on the points.
				}

				if (basicGeoPositions.Count > 0)
				{
					_mapPolylineRoute0.Path = new Geopath(basicGeoPositions); // instead of destroying and redoing, it would be nice to just add the latest point; 
				}                                                             // stupidly, _mapPolylineRoute0.Path.Positions is an IReadOnlyList.
																			  //Better even: use binding; sadly, it is broken for the moment
				else
				{
					BasicGeoposition lastGeoposition = new BasicGeoposition() { Altitude = MyPersistentData.Current.Altitude, Latitude = MyPersistentData.Current.Latitude, Longitude = MyPersistentData.Current.Longitude };
					basicGeoPositions.Add(lastGeoposition);
					_mapPolylineRoute0.Path = new Geopath(basicGeoPositions);
				}

				if (!_isRoute0InMap)
				{
					if (!MyMap.MapElements.Contains(_mapPolylineRoute0))
					{
						MyMap.MapElements.Add(_mapPolylineRoute0);
					}
					_isRoute0InMap = true;
				}
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}
		}

		//private SwitchableObservableCollection<Geopoint> _landmarkLocations = new SwitchableObservableCollection<Geopoint>();
		//public SwitchableObservableCollection<Geopoint> LandmarkLocations { get { return _landmarkLocations; } }

		//public async Task DrawLandmarksAsync()
		//{
		//	if (_isClosing) return;
		//	Logger.Add_TPL("about to draw landmarks", Logger.AppEventsLogFilename, Logger.Severity.Info);

		//	List<Geopoint> geoPoints = new List<Geopoint>();
		//	try
		//	{
		//		foreach (var item in MyPersistentData.Landmarks)
		//		{
		//			geoPoints.Add(new Geopoint(new BasicGeoposition() { Altitude = item.Altitude, Latitude = item.Latitude, Longitude = item.Longitude }));
		//		}
		//	}
		//	catch (OutOfMemoryException)
		//	{
		//		var howMuchMemoryLeft = GC.GetTotalMemory(true); // LOLLO this is probably too late! Let's hope it does not happen since MyPersistentData puts a limit on the points.
		//	}
		//	finally
		//	{
		//		if (!_isClosing)
		//		{
		//			_landmarkLocations.ReplaceRange(geoPoints);
		//			Logger.Add_TPL("landmarks drawn", Logger.AppEventsLogFilename, Logger.Severity.Info);
		//		}
		//	}
		//}
		private void DrawLandmarks()
		{
			// this method is always called within _isOpenSemaphore, so I don't need to protect the following with a dedicated semaphore
			if (!InitLandmarks())
			{
				Debug.WriteLine("No landmarks to be drawn, skipping");
				return;
			}

#if DEBUG
			Stopwatch sw0 = new Stopwatch(); sw0.Start();
#endif

			try
			{
				List<Geopoint> geoPoints = new List<Geopoint>();
				try
				{
					foreach (var item in MyPersistentData.Landmarks)
					{
						geoPoints.Add(new Geopoint(new BasicGeoposition() { Altitude = item.Altitude, Latitude = item.Latitude, Longitude = item.Longitude }));
					}
				}
				catch (OutOfMemoryException)
				{
					var howMuchMemoryLeft = GC.GetTotalMemory(true); // LOLLO this is probably too late! Let's hope it does not happen since MyPersistentData puts a limit on the points.
				}
#if DEBUG
				sw0.Stop(); Debug.WriteLine("Making geopoints for landmarks took " + sw0.ElapsedMilliseconds + " msec");
				sw0.Restart();
#endif
				try
				{
					int j = 0;
					for (int i = 0; i < geoPoints.Count; i++)
					{
						while (j < MyMap.MapElements.Count && (!(MyMap.MapElements[j] is MapIcon) || MyMap.MapElements[j].MapTabIndex != LANDMARK_TAB_INDEX))
						{
							j++; // MapElement is not a landmark: skip to the next element
						}

						(MyMap.MapElements[j] as MapIcon).Location = geoPoints[i];
						//(MyMap.MapElements[j] as MapIcon).NormalizedAnchorPoint = new Point(0.5, 0.5);
						MyMap.MapElements[j].Visible = true; // set it last, in the attempt of getting a little more speed
						j++;
					}
					//if (_isClosing) return;
					for (int i = geoPoints.Count; i < PersistentData.MaxRecordsInLandmarks; i++)
					{
						while (j < MyMap.MapElements.Count && (!(MyMap.MapElements[j] is MapIcon) || MyMap.MapElements[j].MapTabIndex != LANDMARK_TAB_INDEX))
						{
							j++; // MapElement is not a landmark: skip to the next element
						}
						MyMap.MapElements[j].Visible = false;
						j++;
					}
					//Logger.Add_TPL(geoPoints.Count.ToString() + " landmarks drawn", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
				}
				catch (OutOfMemoryException)
				{
					var howMuchMemoryLeft = GC.GetTotalMemory(true); // LOLLO this is probably too late! Let's hope it does not happen since MyPersistentData puts a limit on the points.
				}
#if DEBUG
				sw0.Stop(); Debug.WriteLine("attaching icons to map took " + sw0.ElapsedMilliseconds + " msec");
#endif
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}
		}

		private bool InitLandmarks()
		{
#if DEBUG
			Stopwatch sw0 = new Stopwatch(); sw0.Start();
#endif
			bool isInit = false;
			if (MyMap.MapElements.Count < PersistentData.MaxRecordsInLandmarks) // only init when you really need it
			{
				Debug.WriteLine("InitLandmarks() is initialising the landmarks, because there really are some");
				for (int i = 0; i < PersistentData.MaxRecordsInLandmarks; i++)
				{
					MapIcon newIcon = new MapIcon()
					{
						CollisionBehaviorDesired = MapElementCollisionBehavior.RemainVisible,
						Image = _landmarkIconStreamReference,
						// Image = RandomAccessStreamReference.CreateFromUri(new Uri("ms-appx:///Assets/pointer_landmark-20.png", UriKind.Absolute)),
						MapTabIndex = LANDMARK_TAB_INDEX,
						NormalizedAnchorPoint = LANDMARKS_ANCHOR_POINT, // new Point(0.5, 0.5),
						Visible = false
					};

					MyMap.MapElements.Add(newIcon);
				}
				isInit = true;
			}
			else
			{
				isInit = true;
			}
#if DEBUG
			sw0.Stop(); Debug.WriteLine("Initialising landmarks took " + sw0.ElapsedMilliseconds + " msec");
#endif
			return isInit;
		}
		private void HideFlyoutPoint()
		{
			//_imageFlyoutPoint.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
			_iconFlyoutPoint.Visible = false;
		}
		private void DrawFlyoutPoint()
		{
			//_imageFlyoutPoint.Visibility = Windows.UI.Xaml.Visibility.Visible;
			_iconFlyoutPoint.Visible = true;

			//MapControl.SetLocation(_imageFlyoutPoint, new Geopoint(new BasicGeoposition() { Altitude = 0.0, Latitude = MyPersistentData.Selected.Latitude, Longitude = MyPersistentData.Selected.Longitude }));
			_iconFlyoutPoint.Location = new Geopoint(new BasicGeoposition() { Altitude = 0.0, Latitude = MyPersistentData.Selected.Latitude, Longitude = MyPersistentData.Selected.Longitude });

			if (!_isFlyoutPointInMap)
			{
				if (!MyMap.MapElements.Contains(_iconFlyoutPoint))
				{
					MyMap.MapElements.Add(_iconFlyoutPoint);
				}
				_isFlyoutPointInMap = true;
			}
		}
		#endregion services


		#region IGeoBoundingBoxProvider
		private const double ABSURD_LON = 999.0;
		private static void UpdateMinLon(ref double minLon, BasicGeoposition pos)
		{
			if (minLon != ABSURD_LON) minLon = Math.Min(pos.Longitude, minLon);
			else minLon = pos.Longitude;
		}
		private static void UpdateMaxLon(ref double maxLon, BasicGeoposition pos)
		{
			if (maxLon != ABSURD_LON) maxLon = Math.Max(pos.Longitude, maxLon);
			else maxLon = pos.Longitude;
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
					// LOLLO when you zoom out and then in with the north pole
					// in the middle of the map, then start downloading tiles, the following may fail, so we have fallbacks.
					// LOLLO TODO double check it
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
		public class ShowManyPointDetailsRequestedArgs : EventArgs
		{
			private List<PointRecord> _selectedRecords;
			public List<PointRecord> SelectedRecords { get { return _selectedRecords; } }
			private List<PersistentData.Tables> _selectedSeriess;
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
					Point topLeftPoint = new Point(Math.Max(tappedScreenPointX - MyPersistentData.TapTolerance, 0), Math.Max(tappedScreenPointY - MyPersistentData.TapTolerance, 0));
					Point topRightPoint = new Point(Math.Min(tappedScreenPointX + MyPersistentData.TapTolerance, MyMap.ActualWidth), Math.Max(tappedScreenPointY - MyPersistentData.TapTolerance, 0));
					Point bottomLeftPoint = new Point(Math.Max(tappedScreenPointX - MyPersistentData.TapTolerance, 0), Math.Min(tappedScreenPointY + MyPersistentData.TapTolerance, MyMap.ActualHeight));
					Point bottomRightPoint = new Point(Math.Min(tappedScreenPointX + MyPersistentData.TapTolerance, MyMap.ActualWidth), Math.Min(tappedScreenPointY + MyPersistentData.TapTolerance, MyMap.ActualHeight));

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
					foreach (var item in MyPersistentData.History)
					{
						if (item.Latitude < maxLat && item.Latitude > minLat && item.Longitude > minLon && item.Longitude < maxLon)
						{
							selectedRecords.Add(item);
							selectedSeriess.Add(PersistentData.Tables.History);
							break; // max 1 record each series
						}
					}
					foreach (var item in MyPersistentData.Route0)
					{
						if (item.Latitude < maxLat && item.Latitude > minLat && item.Longitude > minLon && item.Longitude < maxLon)
						{
							selectedRecords.Add(item);
							selectedSeriess.Add(PersistentData.Tables.Route0);
							break; // max 1 record each series
						}
					}
					foreach (var item in MyPersistentData.Landmarks)
					{
						if (item.Latitude < maxLat && item.Latitude > minLat && item.Longitude > minLon && item.Longitude < maxLon)
						{
							selectedRecords.Add(item);
							selectedSeriess.Add(PersistentData.Tables.Landmarks);
							break; // max 1 record each series
						}
					}
					if (selectedRecords.Count > 0)
					{
						Task vibrate = Task.Run(() => App.ShortVibration());
						ShowManyPointDetailsRequested?.Invoke(this, new ShowManyPointDetailsRequestedArgs(selectedRecords, selectedSeriess));
						//MyPointInfoPanel.SetDetails(selectedRecords, selectedSeriess);
						//SelectedPointPopup.IsOpen = true;
						//// SelectedPointFlyout.ShowAt(MyMap);
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
			if (MyPersistentData.IsSelectedSeriesNonNullAndNonEmpty())
			{
				DrawFlyoutPoint();
				await CentreOnSelectedPointAsync();
			}
			//else
			//{
			//	SelectedPointPopup.IsOpen = false;
			//}
		}

		public void OnInfoPanelClosed(object sender, object e)
		{
			HideFlyoutPoint();
		}

		private void OnMap_Holding(MapControl sender, MapInputEventArgs args)
		{
			Task resp = RunFunctionIfOpenAsyncA(delegate
			{
				Task vibrate = Task.Run(() => App.ShortVibration());
				Task cen = CentreOnCurrentAsync();
			});
		}

		private async void OnAim_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
		{
			Task vibrate = Task.Run(() => App.ShortVibration());

			await MyVM.AddMapCentreToLandmarks();
			if (MyPersistentData.IsShowAimOnce)
			{
				MyPersistentData.IsShowAim = false;
			}
		}

		private void OnAim_Holding(object sender, Windows.UI.Xaml.Input.HoldingRoutedEventArgs e)
		{
			MyPersistentData.IsShowAim = false;
		}

		private void OnProvider_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
		{
			Task edge = RunFunctionIfOpenAsyncT(async delegate
			{
				try
				{
					if (!string.IsNullOrWhiteSpace(MyPersistentData.CurrentTileSource.ProviderUriString) && MyRuntimeData.IsConnectionAvailable)
					{
						await Launcher.LaunchUriAsync(new Uri(MyPersistentData.CurrentTileSource.ProviderUriString, UriKind.Absolute));
					}
				}
				catch (Exception) { }
			});
		}
		#endregion user event handlers


		#region data event handlers
		private void OnPersistentData_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(PersistentData.MapStyle))
			{
				Task gt = RunInUiThreadAsync(delegate
				{
					MyMap.Style = MyPersistentData.MapStyle;
				});
			}
			else if (e.PropertyName == nameof(PersistentData.IsShowImperialUnits))
			{
				Task gt = RunInUiThreadAsync(delegate
				{
					if (MyPersistentData?.Current != null) MyPersistentData.Current.SpeedInMetreSec = MyPersistentData.Current.SpeedInMetreSec;

					double oldZoom = MyMap.ZoomLevel;
					double newZoom = oldZoom > MyMap.MinZoomLevel ? Math.Max(MyMap.MinZoomLevel, oldZoom * .99) : MyMap.MinZoomLevel * 1.01;
					Task det = MyMap.TryZoomToAsync(newZoom).AsTask().ContinueWith(delegate
					{
						return MyMap.TryZoomToAsync(oldZoom).AsTask();
					});
					// MyMap.ZoomLevel = MyMap.ZoomLevel; does nothing
				});
			}
			//else if (e.PropertyName == "IsCentreOnCurrent")
			//{
			//    if (MyPersistentData != null && MyPersistentData.IsCentreOnCurrent)
			//    {
			//        CentreOnCurrent();
			//    }
			//}
		}

		private void OnPersistentData_CurrentChanged(object sender, EventArgs e)
		{
			// I must not run to the current point when starting, I want to stick to the last frame when last suspended instead.
			// Unless the tracking is on and the autocentre too.
			if (MyPersistentData?.IsCentreOnCurrent == true && MyRuntimeData.IsAllowCentreOnCurrent)
			{
				Task cen = CentreOnCurrentAsync();
			}
		}

		private void OnHistory_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			if (e.Action != System.Collections.Specialized.NotifyCollectionChangedAction.Reset || MyPersistentData.History?.Count == 0)
			{
				Task draw = RunFunctionIfOpenAsyncT(delegate
				{
					return RunInUiThreadAsync(delegate
					{
						DrawHistory();
					});
				});
			}
		}

		private void OnRoute0_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			if (e.Action != System.Collections.Specialized.NotifyCollectionChangedAction.Reset || MyPersistentData.Route0?.Count == 0)
			{
				Task draw = RunFunctionIfOpenAsyncT(delegate
				{
					return RunInUiThreadAsync(delegate
					{
						DrawRoute0();
					});
				});
			}
		}

		private void OnLandmarks_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			if (e.Action != System.Collections.Specialized.NotifyCollectionChangedAction.Reset || MyPersistentData.Landmarks?.Count == 0)
			{
				Task draw = RunFunctionIfOpenAsyncT(delegate
				{
					return RunInUiThreadAsync(delegate
					{
						DrawLandmarks();
					});
				});
			}
		}

		private bool _isHandlerActive = false;
		private void AddHandlers()
		{
			if (MyPersistentData != null && !_isHandlerActive)
			{
				_isHandlerActive = true;
				MyPersistentData.PropertyChanged += OnPersistentData_PropertyChanged;
				MyPersistentData.CurrentChanged += OnPersistentData_CurrentChanged;
				MyPersistentData.History.CollectionChanged += OnHistory_CollectionChanged;
				MyPersistentData.Route0.CollectionChanged += OnRoute0_CollectionChanged;
				MyPersistentData.Landmarks.CollectionChanged += OnLandmarks_CollectionChanged;
			}
		}

		private void RemoveHandlers()
		{
			if (MyPersistentData != null)
			{
				MyPersistentData.PropertyChanged -= OnPersistentData_PropertyChanged;
				MyPersistentData.CurrentChanged -= OnPersistentData_CurrentChanged;
				MyPersistentData.History.CollectionChanged -= OnHistory_CollectionChanged;
				MyPersistentData.Route0.CollectionChanged -= OnRoute0_CollectionChanged;
				MyPersistentData.Landmarks.CollectionChanged -= OnLandmarks_CollectionChanged;
				_isHandlerActive = false;
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
		private static double _lastZoomScale = 0.0; //remember this to avoid repeating the same calculation
		private static double _imageScaleTransform = 1.0;
		private static string _distRoundedFormatted = "1 m";
		private static double _rightLabelX = LolloMap.SCALE_IMAGE_WIDTH;

		public object Convert(object value, Type targetType, object parameter, string language)
		{
			MapControl mapControl = LolloMap.GetMapControlInstance();
			double currentZoomScale = 0.0;
			if (mapControl != null)
			{
				double.TryParse(value.ToString(), out currentZoomScale);

				if (currentZoomScale != _lastZoomScale)
				{
					try
					{
						Calc(mapControl);
						_lastZoomScale = currentZoomScale; //remember this to avoid repeating the same calculation
					}
					catch (Exception ex)
					{
						// there may be exceptions if I am in a very awkward place in the map, such as the arctic.
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
				return currentZoomScale.ToString("zoom #0.#", CultureInfo.CurrentUICulture);
			}
			Debug.WriteLine("ERROR: XAML used a wrong parameter, or no parameter");
			return 1.0; //should never get here
		}

		// LOLLO the mercator formulas are at http://wiki.openstreetmap.org/wiki/Mercator
		// and http://wiki.openstreetmap.org/wiki/EPSG:3857
		// and http://www.maptiler.org/google-maps-coordinates-tile-bounds-projection/
		private enum DistanceUnits { Metres, Kilometres, Feet, Miles }
		private static void Calc(MapControl mapControl)
		{
			if (mapControl != null)
			{
				// we work out the distance moving along the meridians, because the parallels are always at the same distance at all latitudes
				// in practise, we can also move along the parallels, it works anyway
				// we put a hypothetical bar on the map, and we ask the map to measure where it starts and ends.
				// then we compare the bar length with the scale length, so we find out the distance in metres between the scale ends.
				double hypotheticalParallelBarX1 = mapControl.ActualWidth / 2.0;
				double hypotheticalMeridianBarY1 = mapControl.ActualHeight / 2.0;
				if (hypotheticalMeridianBarY1 <= 0 || hypotheticalParallelBarX1 <= 0) return;
				double hypotheticalMeridianBarLength = 99.0; // I use a shortish bar length so it always fits snugly in the MapControl. Too short would be inaccurate.
															 // The map may be shifted badly; these maps can shift vertically so badly, that the north or the south pole can be in the centre of the control.
															 // If this happens, we place the hypothetical bar lower or higher, so it is always safely on the Earth.
															 // Since these shifts happen vertically, it is easier to use a horizontal hypothetical bar.
				Geopoint centre = mapControl.Center;
				if (centre.Position.Latitude >= LolloMap.MAX_LAT_NEARLY) { hypotheticalMeridianBarY1 *= 1.5; /*Debug.WriteLine("halfMapHeight + ");*/ }
				else if (centre.Position.Latitude <= LolloMap.MIN_LAT_NEARLY) { hypotheticalMeridianBarY1 *= .5; /*Debug.WriteLine("halfMapHeight - ");*/ }

				double headingRadians = mapControl.Heading * ConstantData.DEG_TO_RAD;

				//Point pointN = new Point(halfMapWidth, halfMapHeight);

				//                    Point pointS = new Point(halfMapWidth, halfMapHeight + barLength);//this returns funny results when the map is turned: I must always measure along the meridians
				//Point pointS = new Point(halfMapWidth + hypotheticalMeridianBarLength * Math.Sin(headingRadians), halfMapHeight + hypotheticalMeridianBarLength * Math.Cos(headingRadians));
				//double checkIpotenusaMustBeSameAsBarLength = Math.Sqrt((pointN.X - pointS.X) * (pointN.X - pointS.X) + (pointN.Y - pointS.Y) * (pointN.Y - pointS.Y)); //remove when done testing
				//Geopoint locationN = null; Geopoint locationS = null;
				//mapControl.GetLocationFromOffset(pointN, out locationN);
				//mapControl.GetLocationFromOffset(pointS, out locationS);

				Point pointW = new Point(hypotheticalParallelBarX1, hypotheticalMeridianBarY1);
				Point pointE = new Point(hypotheticalParallelBarX1 + hypotheticalMeridianBarLength * Math.Sin(headingRadians + ConstantData.PI_HALF), hypotheticalMeridianBarY1 + hypotheticalMeridianBarLength * Math.Cos(headingRadians + ConstantData.PI_HALF));
				Geopoint locationW = null;
				Geopoint locationE = null;
				mapControl.GetLocationFromOffset(pointW, out locationW);
				mapControl.GetLocationFromOffset(pointE, out locationE);

				//double scaleEndsDistanceMetres = Math.Abs(locationN.Position.Latitude - locationS.Position.Latitude) * LatitudeToMetres * LolloMap.SCALE_IMAGE_WIDTH / (hypotheticalMeridianBarLength + 1); //need the abs for when the map is rotated;
				double scaleEndsDistanceMetres = Math.Abs(locationE.Position.Longitude - locationW.Position.Longitude) * LatitudeToMetres * LolloMap.SCALE_IMAGE_WIDTH / (hypotheticalMeridianBarLength + 1); //need the abs for when the map is rotated;

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
			string distStrRounded = distMetres_String.Substring(0, 1);
			for (int i = 0; i < distMetres_String.Length - 1; i++)
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