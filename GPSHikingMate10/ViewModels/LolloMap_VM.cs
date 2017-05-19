using LolloGPS.Data;
using LolloGPS.Data.Runtime;
using LolloGPS.Data.TileCache;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Utilz;
using Utilz.Data;
using Windows.UI.Xaml.Controls.Maps;

/*
 * LOLLO NOTE
  Something changed here. HttpMapTileDataSource used to do it for me: when UriRequested fired, 
  I could give it an uri pointing to the web or to a file in my local storage. 
  The latter stopped working, so I had to go the CustomMapTileDataSource route, 
  but the app is less fluid and there is extra code to convert bytes to bitmaps and then to stream references.
 */

namespace LolloGPS.Core
{
	public sealed class LolloMapVM : OpenableObservableData
	{
		// http://josm.openstreetmap.de/wiki/Maps

		private MainVM _myMainVM = null;
		public MainVM MyMainVM { get { return _myMainVM; } private set { _myMainVM = value; RaisePropertyChanged_UI(); } }
		public PersistentData PersistentData { get { return App.PersistentData; } }
		public RuntimeData RuntimeData { get { return App.RuntimeData; } }

		private readonly IGeoBoundingBoxProvider _gbbProvider = null;
		private readonly IList<MapTileSource> _mapTileSources = null;
		private readonly TileDownloader _tileDownloader = null;
		private TileCacheReaderWriter _tileCache = null;
		private HttpMapTileDataSource _httpMapTileDataSource = null;
		private CustomMapTileDataSource _customMapTileDataSource = null;

		#region construct and dispose
		public LolloMapVM(IList<MapTileSource> mapTileSources, IGeoBoundingBoxProvider gbbProvider, MainVM mainVM)
		{
			MyMainVM = mainVM;
			//MyMainVM.LolloMapVM = this;
			_gbbProvider = gbbProvider;
			_mapTileSources = mapTileSources;
			_tileDownloader = new TileDownloader(gbbProvider);
		}
		protected override async Task OpenMayOverrideAsync(object args = null)
		{
			await _tileDownloader.OpenAsync();
			AddHandler_DataChanged();
			Task download = Task.Run(UpdateDownloadTilesAfterConditionsChangedAsync);
			//await OpenAlternativeMap_Http_Async().ConfigureAwait(false);
			await OpenAlternativeMap_Custom_Async().ConfigureAwait(false);
		}
		private async Task OpenAlternativeMap_Http_Async()
		{
			var tileSource = await PersistentData.GetCurrentTileSourceCloneAsync().ConfigureAwait(false);
			if (tileSource == null) return;

			if (tileSource.IsDefault)
			{
				await RunInUiThreadAsync(delegate
				{
					CloseAlternativeMap_Http();
					PersistentData.MapStyle = MapStyle.Terrain;
				}).ConfigureAwait(false);
				return;
			};

			var tileCache = _tileCache = new TileCacheReaderWriter(tileSource, PersistentData.IsMapCached);
			await RunInUiThreadAsync(delegate
			{
				CloseAlternativeMap_Http();
				//PersistentData.MapStyle = MapStyle.None;
				_httpMapTileDataSource = new HttpMapTileDataSource()
				{
					// UriFormatString = tileCache.GetWebUriFormat(), not required coz we catch the event OnDataSource_UriRequested
					AllowCaching = false, //true, // we do our own caching
				};

				var mapTileSource = new MapTileSource(
					_httpMapTileDataSource,
					// The MapControl won't request the uri if the zoom is outside its bounds.
					// To force it, I set the widest possible bounds, which is OK coz the map control does not limit the zoom to its tile source bounds anyway.
					new MapZoomLevelRange() { Max = TileSourceRecord.MaxMaxZoom, Min = TileSourceRecord.MinMinZoom })
				{
					AllowOverstretch = true,
					IsRetryEnabled = true,
					IsFadingEnabled = false,
					IsTransparencyEnabled = false,
					Layer = MapTileLayer.BackgroundOverlay, // we may have an overlay
					TilePixelSize = tileCache.GetTilePixelSize(),
					ZIndex = 999,
					//ZoomLevelRange = new MapZoomLevelRange() { Max = tileCache.GetMaxZoom(), Min = tileCache.GetMinZoom() },
				};

				_mapTileSources.Add(mapTileSource);
				_httpMapTileDataSource.UriRequested += OnHttpDataSource_UriRequested;
			}).ConfigureAwait(false);
		}
		private async Task OpenAlternativeMap_Custom_Async()
		{
			var tileSource = await PersistentData.GetCurrentTileSourceCloneAsync().ConfigureAwait(false);
			if (tileSource == null) return;

			if (tileSource.IsDefault)
			{
				await RunInUiThreadAsync(delegate
				{
					CloseAlternativeMap_Custom();
					PersistentData.MapStyle = MapStyle.Terrain;
				}).ConfigureAwait(false);
				return;
			};

			var tileCache = _tileCache = new TileCacheReaderWriter(tileSource, PersistentData.IsMapCached);
			await RunInUiThreadAsync(delegate
			{
				CloseAlternativeMap_Custom();
				//PersistentData.MapStyle = MapStyle.None;
				_customMapTileDataSource = new CustomMapTileDataSource();

				var mapTileSource = new MapTileSource(
					_customMapTileDataSource,
					// The MapControl won't request the uri if the zoom is outside its bounds.
					// To force it, I set the widest possible bounds, which is OK coz the map control does not limit the zoom to its tile source bounds anyway.
					new MapZoomLevelRange() { Max = TileSourceRecord.MaxMaxZoom, Min = TileSourceRecord.MinMinZoom })
				{
					AllowOverstretch = true,
					IsRetryEnabled = true,
					IsFadingEnabled = false,
					IsTransparencyEnabled = false,
					Layer = MapTileLayer.BackgroundOverlay, // we may have an overlay
					TilePixelSize = tileCache.GetTilePixelSize(),
					ZIndex = 999,
					//ZoomLevelRange = new MapZoomLevelRange() { Max = tileCache.GetMaxZoom(), Min = tileCache.GetMinZoom() },
				};

				_mapTileSources.Add(mapTileSource);
				_customMapTileDataSource.BitmapRequested += OnCustomDataSource_BitmapRequested;
			}).ConfigureAwait(false);
		}
		protected override async Task CloseMayOverrideAsync()
		{
			RemoveHandler_DataChanged();
			//await RunInUiThreadAsync(CloseAlternativeMap_Http).ConfigureAwait(false);
			await RunInUiThreadAsync(CloseAlternativeMap_Custom).ConfigureAwait(false);
			var td = _tileDownloader;
			if (td != null) await td.CloseAsync().ConfigureAwait(false);
		}

		private void CloseAlternativeMap_Http()
		{
			var ds = _httpMapTileDataSource;
			if (ds != null) ds.UriRequested -= OnHttpDataSource_UriRequested;
			_mapTileSources?.Clear();
		}
		private void CloseAlternativeMap_Custom()
		{
			var ds = _customMapTileDataSource;
			if (ds != null) ds.BitmapRequested -= OnCustomDataSource_BitmapRequested;
			_mapTileSources?.Clear();
		}
		#endregion construct and dispose

		#region event handling
		private bool _isDataChangedHandlerActive = false;
		private void AddHandler_DataChanged()
		{
			if (!_isDataChangedHandlerActive && PersistentData != null && RuntimeData != null)
			{
				PersistentData.PropertyChanged += OnPersistentData_PropertyChanged;
				RuntimeData.PropertyChanged += OnRuntimeData_PropertyChanged;
				_isDataChangedHandlerActive = true;
			}
		}
		private void RemoveHandler_DataChanged()
		{
			if (PersistentData != null && RuntimeData != null)
			{
				PersistentData.PropertyChanged -= OnPersistentData_PropertyChanged;
				RuntimeData.PropertyChanged -= OnRuntimeData_PropertyChanged;
				_isDataChangedHandlerActive = false;
			}
		}

		private void OnPersistentData_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(PersistentData.IsTilesDownloadDesired))
			{
				Task down = RunFunctionIfOpenAsyncT_MT(UpdateDownloadTilesAfterConditionsChangedAsync);
			}
			else if (e.PropertyName == nameof(PersistentData.CurrentTileSource))
			{
				Task reopen = RunFunctionIfOpenAsyncT(async delegate
				{
					//await OpenAlternativeMap_Http_Async().ConfigureAwait(false);
					await OpenAlternativeMap_Custom_Async().ConfigureAwait(false);
				});
			}
			else if (e.PropertyName == nameof(PersistentData.IsMapCached))
			{
				Task updIsCaching = RunFunctionIfOpenAsyncA(delegate
				{
					var tc = _tileCache;
					if (tc != null) tc.IsCaching = PersistentData.IsMapCached;
				});
			}
		}
		private void OnRuntimeData_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(RuntimeData.IsConnectionAvailable))
			{
				Task resume = RunFunctionIfOpenAsyncT_MT(UpdateDownloadTilesAfterConditionsChangedAsync);
			}
		}
		private async void OnHttpDataSource_UriRequested(HttpMapTileDataSource sender, MapTileUriRequestedEventArgs args)
		{
			var deferral = args.Request.GetDeferral();
			try
			{
				//args.Request.Uri = new Uri("ms-appx:///Assets/pointer_start-72.png");
				var tc = _tileCache;
				if (tc == null) return;
				var newUri = await tc.GetTileUriAsync(args.X, args.Y, 0, args.ZoomLevel, CancToken).ConfigureAwait(false);
				if (newUri != null)
				{
					args.Request.Uri = newUri;
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex);
				// Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename, Logger.Severity.Info);
			}
			finally
			{
				deferral.Complete();
			}
		}
		private async void OnCustomDataSource_BitmapRequested(CustomMapTileDataSource sender, MapTileBitmapRequestedEventArgs args)
		{
			var deferral = args.Request.GetDeferral();
			try
			{
				var tc = _tileCache;
				if (tc == null) return;
				var pixelRef = await tc.GetTileStreamRefAsync(args.X, args.Y, 0, args.ZoomLevel, CancToken).ConfigureAwait(false);
				if (pixelRef != null) args.Request.PixelData = pixelRef;
			}
			catch (Exception exc)
			{
				Debug.WriteLine("Exception in OnCustomDataSource_BitmapRequested(); " + exc.ToString() + exc.Message);
			}
			finally
			{
				deferral.Complete();
			}
		}
		#endregion event handling


		#region services
		/// <summary>
		/// Start or resume downloading tiles if so desired
		/// run this on the threadpool because it can last forever
		/// </summary>
		/// <returns></returns>
		private async Task UpdateDownloadTilesAfterConditionsChangedAsync()
		{
			if (PersistentData.IsTilesDownloadDesired && RuntimeData.IsConnectionAvailable)
			{
				Tuple<int, int> downloadResult = await _tileDownloader.StartOrResumeDownloadTilesAsync().ConfigureAwait(false);
				if (downloadResult != null) MyMainVM.SetLastMessage_UI(downloadResult.Item1 + " of " + downloadResult.Item2 + " tiles downloaded");
			}
		}
		public async Task<List<Tuple<int, int>>> GetHowManyTiles4DifferentZoomsAsync()
		{
			var result = new List<Tuple<int, int>>();
			var td = _tileDownloader;
			if (td == null) return result;
			await RunFunctionIfOpenAsyncT(async delegate { result = await td.GetHowManyTiles4DifferentZooms4CurrentConditionsAsync().ConfigureAwait(false); }).ConfigureAwait(false);
			return result;
		}

		public void CancelDownloadByUser()
		{
			_tileDownloader?.CancelDownloadByUser();
		}

		public async Task<bool> AddMapCentreToCheckpoints()
		{
			var gbbProvider = _gbbProvider;
			if (gbbProvider == null || PersistentData == null) return false;

			var centre = await gbbProvider.GetCentreAsync();
			// this stupid control does not know the altitude, it gives crazy high numbers
			centre.Altitude = MainVM.RoundAndRangeAltitude(centre.Altitude, false);
			return await PersistentData.TryAddPointToCheckpointsAsync(new PointRecord() { Altitude = centre.Altitude, Latitude = centre.Latitude, Longitude = centre.Longitude, }).ConfigureAwait(false);
		}
		#endregion services
	}
}