using LolloGPS.Data;
using Utilz.Data;
using LolloGPS.Data.Leeching;
using LolloGPS.Data.Runtime;
using LolloGPS.Data.TileCache;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Utilz;
using Windows.Devices.Geolocation;

namespace LolloGPS.Core
{
	public interface IGeoBoundingBoxProvider
	{
		Task<GeoboundingBox> GetMinMaxLatLonAsync();
		Task<BasicGeoposition> GetCentreAsync();
	}
	public class TileDownloader : OpenableObservableData
	{
		#region properties
		public const int MaxProgressStepsToReport = 25;

		private readonly RuntimeData _runtimeData = RuntimeData.GetInstance();

		private readonly object _isCancLocker = new object();
		private bool _isCancelledBySuspend = false;
		public bool IsCancelledBySuspend
		{
			get
			{
				lock (_isCancLocker)
				{
					return _isCancelledBySuspend;
				}
			}
			private set
			{
				lock (_isCancLocker)
				{
					_isCancelledBySuspend = value;
					UpdateIsCancelled();
				}
			}
		}

		private bool _isCancelledByUser = false;
		public bool IsCancelledByUser
		{
			get
			{
				lock (_isCancLocker)
				{
					return _isCancelledByUser;
				}
			}
			private set
			{
				lock (_isCancLocker)
				{
					_isCancelledByUser = value;
					UpdateIsCancelled();
				}
			}
		}
		private bool _isCancelled = false;
		public bool IsCancelled
		{
			get
			{
				lock (_isCancLocker)
				{
					return _isCancelled;
				}
			}
			private set
			{
				_isCancelled = value;
				if (_isCancelled) Cts?.CancelSafe(true);
			}
		}
		private void UpdateIsCancelled()
		{
			lock (_isCancLocker)
			{
				IsCancelled = _isCancelledBySuspend || _isCancelledByUser || !(_runtimeData.IsConnectionAvailable);
			}
		}

		protected readonly IGeoBoundingBoxProvider _gbbProvider = null;
		#endregion properties

		#region events
		public event EventHandler<double> SaveProgressChanged;
		private void RaiseSaveProgressChanged(double progress)
		{
			RuntimeData.SetDownloadProgressValue_UI(progress);
			var listener = SaveProgressChanged;
			if (listener != null)
			{
				listener(this, progress);
			}
		}
		#endregion events

		#region lifecycle
		public TileDownloader(IGeoBoundingBoxProvider gbbProvider)
		{
			_gbbProvider = gbbProvider;
		}
		protected override Task OpenMayOverrideAsync()
		{
			IsCancelledBySuspend = false;
			IsCancelledByUser = false;
			AddRuntimeHandler();
			return Task.CompletedTask;
		}
		protected override Task CloseMayOverrideAsync()
		{
			IsCancelledBySuspend = true;
			RemoveRuntimeHandler();
			return Task.CompletedTask;
		}
		#endregion lifecycle

		#region event handlers
		private bool _isRuntimeHandlerActive = false;
		private void AddRuntimeHandler()
		{
			if (!_isRuntimeHandlerActive && _runtimeData != null)
			{
				_isRuntimeHandlerActive = true;
				_runtimeData.PropertyChanged += OnRuntimeData_PropertyChanged;
			}
		}
		private void RemoveRuntimeHandler()
		{
			if (_runtimeData != null)
			{
				_runtimeData.PropertyChanged -= OnRuntimeData_PropertyChanged;
			}
			_isRuntimeHandlerActive = false;
		}
		private void OnRuntimeData_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			UpdateIsCancelled();
		}
		#endregion event handlers

		#region save services
		private static readonly SemaphoreSlimSafeRelease _saveSemaphore = new SemaphoreSlimSafeRelease(1, 1);
		internal void CancelDownloadByUser()
		{
			IsCancelledByUser = true;
		}
		public async Task<Tuple<int, int>> StartOrResumeDownloadTilesAsync()
		{
			var output = Tuple.Create(0, 0);
			var persistentData = PersistentData.GetInstance();

			try
			{
				await _saveSemaphore.WaitAsync().ConfigureAwait(false);
				IsCancelledByUser = false;
				var tileCache = persistentData.StartOrResumeDownloadSession(await _gbbProvider.GetMinMaxLatLonAsync().ConfigureAwait(false));

				DownloadTiles_RespondingToCancel(tileCache, persistentData.LastDownloadSession);
			}
			catch (Exception ex)
			{
				//CloseDownload(persistentData, false);
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}
			finally
			{
				// even if something went wrong (maybe the new session is not valid), do not leave the download open!
				CloseDownload(persistentData, true);
				SemaphoreSlimSafeRelease.TryRelease(_saveSemaphore);
			}
			return output;
		}
		private Tuple<int, int> DownloadTiles_RespondingToCancel(TileCache tileCache, DownloadSession session)
		{
			var output = Tuple.Create(0, 0);
			if (RuntimeData.GetInstance().IsConnectionAvailable && tileCache != null && session != null)
			{
				output = SaveTiles_RespondingToCancel(tileCache, session);
			}
			return output;
		}
		private void CloseDownload(PersistentData persistentData, bool isSuccess)
		{
			// maybe the user cancelled: that means they are happy with this download, or at least we can consider it complete.
			if (IsCancelledByUser)
			{
				persistentData.SetIsTilesDownloadDesired(false, 0);
				persistentData.LastDownloadSession = null;
			}
			// unless it was interrupted by suspension or connection going missing, the download is no more required because it finished: mark it.
			else if (!IsCancelledBySuspend && RuntimeData.GetInstance().IsConnectionAvailable)
			{
				if (isSuccess) persistentData.SetIsTilesDownloadDesired(false, 0);
				persistentData.LastDownloadSession = null;
			}
		}
		private Tuple<int, int> SaveTiles_RespondingToCancel(TileCache tileCache, DownloadSession session)
		{
			RaiseSaveProgressChanged(0.0);

			int totalCnt = 0;
			int currentOkCnt = 0;
			int currentCnt = 0;

			if (!SafeCancellationTokenSource.IsNullOrCancellationRequestedSafe(Cts))
			{
				List<TileCacheRecord> requiredTilesOrderedByZoom = GetTileData_RespondingToCancel(session);
				totalCnt = requiredTilesOrderedByZoom.Count;

				if (!SafeCancellationTokenSource.IsNullOrCancellationRequestedSafe(Cts) && totalCnt > 0)
				{
					int howManyProgressStepsIWantToReport = Math.Min(MaxProgressStepsToReport, totalCnt);

					int[] stepsWhenIWantToRaiseProgress = new int[howManyProgressStepsIWantToReport];
					if (howManyProgressStepsIWantToReport > 0)
					{
						for (int i = 0; i < howManyProgressStepsIWantToReport; i++)
						{
							stepsWhenIWantToRaiseProgress[i] = totalCnt * i / howManyProgressStepsIWantToReport;
						}
					}
					// LOLLO TODO check if this parallelisation bothers certain providers. It is faster than without, by 1.2 to 2 x. 
					// Otherwise, see if you can download the tiles in the background. 
					// Let's see: a background task would mean using named semaphores in the tile cache db.
					// Then we need to share the processing queue between bkg and frg. How? If we can't, that may work anyway,
					// but we risk double-downloading certain tiles. If we stick to the new CollisionBehaviour
					// in the tile cache, which seems better, we also risk double-saving them.
#if DEBUG
					Stopwatch sw0 = new Stopwatch();
					sw0.Start();
#endif
					try
					{
						Parallel.ForEach(requiredTilesOrderedByZoom, new ParallelOptions() { CancellationToken = SafeCancellationTokenSource.GetCancellationTokenSafe(Cts) }, (tile) =>
						{
							bool isOk = tileCache.TrySaveTileAsync(tile.X, tile.Y, tile.Z, tile.Zoom).Result;
							if (isOk) currentOkCnt++;

							currentCnt++;
							if (totalCnt > 0 && stepsWhenIWantToRaiseProgress.Contains(currentCnt)) RaiseSaveProgressChanged((double)currentCnt / (double)totalCnt);
						});
					}
					catch (OperationCanceledException) { } // comes from the canc token
					catch (ObjectDisposedException) { } // comes from the canc token
					catch (Exception ex) { Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename); }

					//foreach (var tile in requiredTilesOrderedByZoom)
					//{
					//	bool isOk = await tileCache.SaveTileAsync(tile.X, tile.Y, tile.Z, tile.Zoom).ConfigureAwait(false);
					//	if (isOk) currentOkCnt++;

					//	currentCnt++;
					//	if (totalCnt > 0)
					//	{
					//		if (stepsWhenIWantToRaiseProgress.Contains(currentCnt)) RaiseSaveProgressChanged((double)currentCnt / (double)totalCnt);
					//	}
					//	if (IsCancelled) break;
					//}
#if DEBUG
					sw0.Stop();
					Debug.WriteLine("sw0.ElapsedMilliseconds " + sw0.ElapsedMilliseconds + " currentCnt " + currentCnt + " currentOkCnt " + currentOkCnt);
#endif
				}
			}
			RaiseSaveProgressChanged(1.0);
			return Tuple.Create(currentOkCnt, totalCnt);
		}
		#endregion save services

		#region read services
		public async Task<List<Tuple<int, int>>> GetHowManyTiles4DifferentZooms4CurrentConditionsAsync()
		{
			PersistentData persistentData = PersistentData.GetInstance();
			var currentTileSource_mt = persistentData.CurrentTileSource;
			//var isMapCached_mt = persistentData.IsMapCached;

			IsCancelledByUser = false;
			var output = new List<Tuple<int, int>>();

			try
			{
				//TileCache tileCache = new TileCache(currentTileSource_mt, isMapCached_mt);

				var gbb = await _gbbProvider.GetMinMaxLatLonAsync().ConfigureAwait(false);
				if (gbb != null)
				{
					// now I have a geobounding box that certainly encloses the screen.
					DownloadSession session = new DownloadSession(
						//tileCache.GetMinZoom(),
						currentTileSource_mt.MinZoom,
						//tileCache.GetMaxZoom(),
						currentTileSource_mt.MaxZoom,
						gbb,
						//tileCache.TileSource.TechName);
						currentTileSource_mt.TechName);
					if (session.IsValid)
					{
						List<TileCacheRecord> tilesOrderedByZoom = GetTileData_RespondingToCancel(session);

						for (int zoom = session.MinZoom; zoom <= session.MaxZoom; zoom++)
						{
							int howManyAtOrBeforeZoom = tilesOrderedByZoom.Count(a => a.Zoom <= zoom);
							output.Add(Tuple.Create(zoom, howManyAtOrBeforeZoom));
						}
					}
				}
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}
			return output;
		}

		protected List<TileCacheRecord> GetTileData_RespondingToCancel(DownloadSession session)
		{
			var output = new List<TileCacheRecord>();

			if (!SafeCancellationTokenSource.IsNullOrCancellationRequestedSafe(Cts) && session != null &&
					(session.NWCorner.Latitude != session.SECorner.Latitude || session.NWCorner.Longitude != session.SECorner.Longitude))
			{
				int totalCnt = 0;
				for (int zoom = session.MinZoom; zoom <= session.MaxZoom; zoom++)
				{
					TileCacheRecord topLeftTile = new TileCacheRecord(session.TileSourceTechName, Lon2TileX(session.NWCorner.Longitude, zoom), Lat2TileY(session.NWCorner.Latitude, zoom), 0, zoom); // Alaska
					TileCacheRecord bottomRightTile = new TileCacheRecord(session.TileSourceTechName, Lon2TileX(session.SECorner.Longitude, zoom), Lat2TileY(session.SECorner.Latitude, zoom), 0, zoom); // New Zealand
					int maxX4Zoom = MaxTilexX4Zoom(zoom);
					Debug.WriteLine("topLeftTile.X = " + topLeftTile.X + " topLeftTile.Y = " + topLeftTile.Y + " bottomRightTile.X = " + bottomRightTile.X + " bottomRightTile.Y = " + bottomRightTile.Y + " and zoom = " + zoom);

					bool exit = false;
					bool hasJumpedDateLine = false;

					int x = topLeftTile.X;
					while (!exit)
					{
						for (int y = topLeftTile.Y; y <= bottomRightTile.Y; y++)
						{
							output.Add(new TileCacheRecord(session.TileSourceTechName, x, y, 0, zoom));
							totalCnt++;
							if (IsMustBreak(totalCnt))
							{
								exit = true;
								break;
							}
						}

						x++;
						if (x > bottomRightTile.X)
						{
							if (topLeftTile.X > bottomRightTile.X && !hasJumpedDateLine)
							{
								if (x > maxX4Zoom)
								{
									x = 0;
									hasJumpedDateLine = true;
								}
							}
							else
							{
								exit = true;
							}
						}
					}
					if (IsMustBreak(totalCnt)) break;
				}
			}
			return output;
		}
		private bool IsMustBreak(int totalCnt)
		{
			return totalCnt > ConstantData.MAX_TILES_TO_LEECH || SafeCancellationTokenSource.IsNullOrCancellationRequestedSafe(Cts);
		}
		#endregion read services

		// LOLLO NOTE check the mercator formulas at http://wiki.openstreetmap.org/wiki/Mercator
		// and http://wiki.openstreetmap.org/wiki/EPSG:3857
		// and http://www.maptiler.org/google-maps-coordinates-tile-bounds-projection/

		#region private services
		private static double toRad = ConstantData.DEG_TO_RAD;
		private static double toDeg = ConstantData.RAD_TO_DEG;
		protected static int Lon2TileX(double lonDeg, int z)
		{
			//                   N * (lon + 180) / 360
			return Math.Max((int)(Math.Floor((lonDeg + 180.0) / 360.0 * Math.Pow(2.0, z))), 0);
		}
		protected static int Lat2TileY(double latDeg, int z)
		{
			//                   N *  { 1 - log[ tan ( lat ) + sec ( lat ) ] / Pi } / 2
			//      sec(x) = 1 / cos(x)
			//return (int)(Math.Floor((1.0 - Math.Log(Math.Tan(latDeg * Math.PI / 180.0) + 1.0 / Math.Cos(latDeg * Math.PI / 180.0)) / Math.PI) / 2.0 * Math.Pow(2.0, z)));
			return Math.Max((int)(Math.Floor((1.0 - Math.Log(Math.Tan(latDeg * toRad) + 1.0 / Math.Cos(latDeg * toRad)) / Math.PI) / 2.0 * Math.Pow(2.0, z))), 0);
		}
		protected static int MaxTilexX4Zoom(int z)
		{
			return Lon2TileX(179.9999999, z);
		}
		protected static int Zoom2TileN(int z)
		{
			return (int)Math.Pow(2.0, Convert.ToDouble(z));
		}
		protected static double TileX2Lon(int x, int z)
		{
			return x / Math.Pow(2.0, z) * 360.0 - 180;
		}
		protected static double TileY2Lat(int y, int z)
		{
			double n = Math.PI - ConstantData.PI_DOUBLE * y / Math.Pow(2.0, z);
			//return 180.0 / Math.PI * Math.Atan(0.5 * (Math.Exp(n) - Math.Exp(-n)));
			return toDeg * Math.Atan(Math.Sinh(n));
		}
		#endregion private services
	}

	/*
     * Reproject the coordinates to the Mercator projection (from EPSG:4326 to EPSG:3857): 
        x = lon
        y = arsinh(tan(lat)) = log[tan(lat) + sec(lat)]
        (lat and lon are in radians)
     * Transform range of x and y to 0 – 1 and shift origin to top left corner: 
        x = [1 + (x / π)] / 2
        y = [1 − (y / π)] / 2

     * Calculate the number of tiles across the map, n, using 2zoom
     * Multiply x and y by n. Round results down to give tilex and tiley.
*/
	// some more notes

	// http://msdn.microsoft.com/en-us/library/windowsphone/develop/windows.ui.xaml.controls.maps.aspx
	// http://pietschsoft.com/post/2009/11/13/Prototype_OpenStreetMap_Silverlight_Control_using_Bing_Maps_SDK_and_DeepEarth

	// openstreetmap http://tile.openstreetmap.org/{2}/{0}/{1}.png that is, zoom, x, y, that is, zoom, long, lat
	// or http://a.tile.openstreetmap.org/{zoomlevel}/{x}/{y}.png
	// with zoom = 2,
	// x and y go between 0 and 3. y = 3 is antarctica, y = 0 is the arctic. No decimals are allowed.z goes between 2 and 19.
	// with zoom = 1, x and y go between 0 and 1
	// with zoom 3, x and y go between 0 and 7
	// basically, the max value is Pow(2,zoom) -1, for both x and y
	// the zero x is at meridian 180°
	// the zero y is at 85.0511 N (they use a Mercator projection) 85.0511 is the result of arctan(sinh(π)). 
	// the min zoom is 0 (zoomed out)
	// the max zoom is 19 (zoomed in)
	// MapControl goes between 1 and 20, instead
	// similar is openaerialmap http://tile.openaerialmap.org/tiles/1.0.0/openaerialmap-900913/{2}/{0}/{1}.jpg
	// otherwise, we can use http://nominatim.openstreetmap.org/search.php?q=8%2C+44%2C+9%2C+49&viewbox=-168.76%2C66.24%2C168.76%2C-66.24&polygon=1 not really, that one looks for names
	// http://wiki.openstreetmap.org/wiki/Nominatim
	//
	// http://pietschsoft.com/post/2009/03/20/Virtual-Earth-Silverlight-Overlay-OpenStreetMap2c-OpenAerialMap-and-Yahoo-Map-Imagery-using-Custom-Tile-Layers!
	//yahoo http://us.maps2.yimg.com/us.png.maps.yimg.com/png?v=3.52&t=m&x={0}&y={1}&z={2}
	//         public override Uri GetUri(int x, int y, int zoomLevel) 
	//{
	//    // The math used here was copied from the DeepEarth Project (http://deepearth.codeplex.com) 
	//    double posY;
	//    double zoom = 18 - zoomLevel;
	//    double num4 = Math.Pow(2.0, zoomLevel) / 2.0;

	//    if (y < num4)
	//    {
	//        posY = (num4 - Convert.ToDouble(y)) - 1.0;
	//    }
	//    else
	//    {
	//        posY = ((Convert.ToDouble(y) + 1) - num4) * -1.0;
	//    }
	// more here: http://wiki.openstreetmap.org/wiki/Slippy_map_tilenames
	// hence:
	//            int long2tilex(double lon, int z) 
	//{ 
	//    return (int)(floor((lon + 180.0) / 360.0 * pow(2.0, z))); 
	//}

	//int lat2tiley(double lat, int z)
	//{ 
	//    return (int)(floor((1.0 - log( tan(lat * M_PI/180.0) + 1.0 / cos(lat * M_PI/180.0)) / M_PI) / 2.0 * pow(2.0, z))); 
	//}

	//double tilex2long(int x, int z) 
	//{
	//    return x / pow(2.0, z) * 360.0 - 180;
	//}

	//double tiley2lat(int y, int z) 
	//{
	//    double n = M_PI - 2.0 * M_PI * y / pow(2.0, z);
	//    return 180.0 / M_PI * atan(0.5 * (exp(n) - exp(-n)));
	//}
	//    return new Uri(string.Format(this.UriFormat, x, posY, zoom));
	//}

	//            public PointF WorldToTilePos(double lon, double lat, int zoom)
	//{
	//    PointF p = new Point();
	//    p.X = (float)((lon + 180.0) / 360.0 * (1 << zoom));
	//    p.Y = (float)((1.0 - Math.Log(Math.Tan(lat * Math.PI / 180.0) + 
	//        1.0 / Math.Cos(lat * Math.PI / 180.0)) / Math.PI) / 2.0 * (1 << zoom));

	//    return p;
	//}

	//public PointF TileToWorldPos(double tile_x, double tile_y, int zoom) 
	//{
	//    PointF p = new Point();
	//    double n = Math.PI - ((2.0 * Math.PI * tile_y) / Math.Pow(2.0, zoom));

	//    p.X = (float)((tile_x / Math.Pow(2.0, zoom) * 360.0) - 180.0);
	//    p.Y = (float)(180.0 / Math.PI * Math.Atan(Math.Sinh(n)));

	//    return p;
	//}
	//HttpMapTileDataSource dataSource = new HttpMapTileDataSource() { UriFormatString = "{UriScheme}://ecn.t0.tiles.virtualearth.net/tiles/r{quadkey}.jpeg?g=129&mkt=en-us&shading=hill&stl=H" };
	//HttpMapTileDataSource dataSource = new HttpMapTileDataSource() { UriFormatString = "http://tile.openstreetmap.org/2/0/1.png" }; //  "{UriScheme}://ecn.t0.tiles.virtualearth.net/tiles/r{quadkey}.jpeg?g=129&mkt=en-us&shading=hill&stl=H" };

}