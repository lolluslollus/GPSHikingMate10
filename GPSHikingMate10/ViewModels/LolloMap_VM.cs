using LolloGPS.Data;
using LolloGPS.Data.Runtime;
using LolloGPS.Data.TileCache;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Utilz.Data;
using Windows.UI.Xaml.Controls.Maps;

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
		private MapTileSource _mapTileSource = null;
		//private CustomMapTileDataSource _tileDataSource_custom = null;
		private readonly IList<MapTileSource> _mapTileSources = null;
		private readonly TileDownloader _tileDownloader = null;
		private TileCacheReaderWriter _tileCache = null;
		private HttpMapTileDataSource _tileDataSource_http = null;


		#region construct and dispose
		public LolloMapVM(IList<MapTileSource> mapTileSources, IGeoBoundingBoxProvider gbbProvider, MainVM mainVM)
		{
			MyMainVM = mainVM;
			//MyMainVM.LolloMapVM = this;
			_gbbProvider = gbbProvider;
			_mapTileSources = mapTileSources;
			_tileDownloader = new TileDownloader(gbbProvider);
		}
		protected override async Task OpenMayOverrideAsync()
		{
			await _tileDownloader.OpenAsync();
			AddHandler_DataChanged();
			Task download = Task.Run(UpdateDownloadTilesAfterConditionsChangedAsync);
			await OpenAlternativeMap_Http_Async().ConfigureAwait(false);
		}
		private async Task OpenAlternativeMap_Http_Async()
		{
			var tileSource = await PersistentData.GetCurrentTileSourceClone().ConfigureAwait(false);
			if (tileSource == null || tileSource.IsDefault) return;

			bool isCaching = PersistentData.IsMapCached;
			_tileCache = new TileCacheReaderWriter(tileSource, isCaching);

			await RunInUiThreadAsync(delegate
			{
				_tileDataSource_http = new HttpMapTileDataSource()
				{
					// UriFormatString = _tileCache.GetWebUriFormat(), not required coz we catch the event OnDataSource_UriRequested
					AllowCaching = false, //true, // we do our own caching
				};

				_mapTileSource = new MapTileSource(
					_tileDataSource_http,
					// new MapZoomLevelRange() { Max = _tileCache.GetMaxZoom(), Min = _tileCache.GetMinZoom() })
					// The MapControl won't request the uri if the zoom is outside its bounds.
					// To force it, I set the widest possible bounds, which is OK coz the map control does not limit the zoom to its tile source bounds anyway.
					new MapZoomLevelRange() { Max = TileSourceRecord.MaxMaxZoom, Min = TileSourceRecord.MinMinZoom })
				{
					// Layer = MapTileLayer.BackgroundReplacement,
					Layer = MapTileLayer.BackgroundOverlay, // show the Nokia map when the alternative source is not available, otherwise it goes all blank (ie black)
															// Layer = (MapTileLayer.BackgroundOverlay | MapTileLayer.RoadOverlay), // still does not hide the roads
					AllowOverstretch = true,
					//ZoomLevelRange = new MapZoomLevelRange() { Max = _tileCache.GetMaxZoom(), Min = _tileCache.GetMinZoom() },
					IsRetryEnabled = true,
					TilePixelSize = _tileCache.GetTilePixelSize(),
					IsFadingEnabled = false, //true,
					ZIndex = 999,
				};

				if (!_mapTileSources.Contains(_mapTileSource))
				{
					_mapTileSources.Add(_mapTileSource);
					_tileDataSource_http.UriRequested += OnDataSource_UriRequested;
				}
			}).ConfigureAwait(false);
			// Logger.Add_TPL("OpenAlternativeMap_Http ended", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
			//_myMap.Opacity = .1; // show the Nokia map when the alternative source is not available
			//_myMap.Style = MapStyle.None; //so our map will cover the original completely
		}
		//private void ActivateAlternativeMap_Custom()
		//{
		//    _tileDataSource_custom = new CustomMapTileDataSource();

		//    _mapTileSource = new MapTileSource(_tileDataSource_custom)
		//    {
		//        // Layer = MapTileLayer.BackgroundReplacement,
		//        Layer = MapTileLayer.BackgroundOverlay, // show the Nokia map when the alternative source is not available, otherwise it goes all blank (ie black)
		//        AllowOverstretch = true,
		//        ZoomLevelRange = new MapZoomLevelRange() { Max = _tileCache.GetMaxZoom(), Min = _tileCache.GetMinZoom() },
		//        IsRetryEnabled = true,
		//        TilePixelSize = _tileCache.GetTilePixelSize(),
		//        IsFadingEnabled = true,
		//        ZIndex = 999,
		//    };

		//    if (!_mapTileSources.Contains(_mapTileSource))
		//    {
		//        _mapTileSources.Add(_mapTileSource);
		//        _tileDataSource_custom.BitmapRequested += OnCustomDataSource_BitmapRequested;
		//    }

		//    //_myMap.Opacity = .1; // show the Nokia map when the alternative source is not available
		//    //_myMap.Style = MapStyle.None; //so our map will cover the original completely
		//}
		protected override async Task CloseMayOverrideAsync()
		{
			RemoveHandler_DataChanged();
			await _tileDownloader.CloseAsync().ConfigureAwait(false);
			await CloseAlternativeMap_Http_Async().ConfigureAwait(false);
		}

		private Task CloseAlternativeMap_Http_Async()
		{
			return RunInUiThreadAsync(delegate
			{
				if (_tileDataSource_http != null) _tileDataSource_http.UriRequested -= OnDataSource_UriRequested;
				//if (_tileDataSource_custom != null) _tileDataSource_custom.BitmapRequested -= OnCustomDataSource_BitmapRequested;
				var mts = _mapTileSource;
				if (mts != null) _mapTileSources?.Remove(mts);
			});
			// Logger.Add_TPL("CloseAlternativeMap_Http ended", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
		}
		#endregion construct and dispose
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
					await CloseAlternativeMap_Http_Async().ConfigureAwait(false);
					await OpenAlternativeMap_Http_Async().ConfigureAwait(false);
				});
			}
			else if (e.PropertyName == nameof(PersistentData.IsMapCached))
			{
				Task updIsCaching = RunFunctionIfOpenAsyncA(delegate
				{
					var tileCache = _tileCache;
					if (tileCache != null) tileCache.IsCaching = PersistentData.IsMapCached;
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
		private async void OnDataSource_UriRequested(HttpMapTileDataSource sender, MapTileUriRequestedEventArgs args)
		{
			var deferral = args.Request.GetDeferral();
			try
			{
				// I could make _tileCache volatile, because it can be read and changed in different threads 
				// and there is no guarantee that they happen in sync, because of this very method. 
				// However, it seems very subtle so I leave it because we want the maximum performance here.
				var newUri = await _tileCache.GetTileUri(args.X, args.Y, 0, args.ZoomLevel, CancToken).ConfigureAwait(false);
				if (newUri != null) args.Request.Uri = newUri;
			}
			catch (Exception /*ex*/)
			{
				// Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename, Logger.Severity.Info);
			}
			deferral.Complete();
		}
		//private async void OnCustomDataSource_BitmapRequested(CustomMapTileDataSource sender, MapTileBitmapRequestedEventArgs args)
		//{
		//    var deferral = args.Request.GetDeferral();
		//    try
		//    {
		//        var pixelRef = await _tileCache.GetTileStreamRef(args.X, args.Y, 0, args.ZoomLevel).ConfigureAwait(false);
		//        if (pixelRef != null) args.Request.PixelData = pixelRef;
		//    }
		//    catch (Exception exc)
		//    {
		//        Debug.WriteLine("Exception in OnCustomDataSource_BitmapRequested(); " + exc.ToString() + exc.Message);
		//    }
		//    deferral.Complete();
		//}
		#endregion event handling


		#region services
		public async Task<List<Tuple<int, int>>> GetHowManyTiles4DifferentZoomsAsync()
		{
			var result = new List<Tuple<int, int>>();
			var tileDownloader = _tileDownloader;
			if (tileDownloader != null)
			{
				await RunFunctionIfOpenAsyncT(async delegate { result = await tileDownloader.GetHowManyTiles4DifferentZooms4CurrentConditionsAsync().ConfigureAwait(false); }).ConfigureAwait(false);
			}
			return result;
		}

		public void CancelDownloadByUser()
		{
			_tileDownloader?.CancelDownloadByUser();
		}

		public async Task<bool> AddMapCentreToCheckpoints()
		{
			if (_gbbProvider != null && PersistentData != null)
			{
				var centre = await _gbbProvider.GetCentreAsync();
				// this stupid control does not know the altitude, it gives crazy high numbers
				centre.Altitude = MainVM.RoundAndRangeAltitude(centre.Altitude, false);
				return await PersistentData.TryAddPointToCheckpointsAsync(new PointRecord() { Altitude = centre.Altitude, Latitude = centre.Latitude, Longitude = centre.Longitude, }).ConfigureAwait(false);
			}
			return false;
		}
		#endregion services
	}
}