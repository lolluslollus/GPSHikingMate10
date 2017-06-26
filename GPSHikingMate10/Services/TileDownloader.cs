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
using System.Threading;
using LolloGPS.Calcs;

namespace LolloGPS.Core
{
    public interface IGeoBoundingBoxProvider
    {
        Task<GeoboundingBox> GetMinMaxLatLonAsync();
        Task<BasicGeoposition> GetCentreAsync();
    }
    public class TileDownloader : OpenableObservableData // not sealed to help unit tests
    {
        #region properties
        public const int MaxProgressStepsToReport = 25;

        private readonly RuntimeData _runtimeData = RuntimeData.GetInstance();

        private readonly object _cancSuspendLocker = new object();
        private SafeCancellationTokenSource _suspendCts = null;
        private SafeCancellationTokenSource SuspendCts
        {
            get { lock (_cancSuspendLocker) { return _suspendCts; } }
        }
        private bool _isSuspended = false;
        public bool IsSuspended
        {
            get
            {
                lock (_cancSuspendLocker)
                {
                    return _isSuspended;
                }
            }
            private set
            {
                lock (_cancSuspendLocker)
                {
                    _isSuspended = value;
                    if (_isSuspended) _suspendCts?.CancelSafe(true);
                    else { _suspendCts?.Dispose(); _suspendCts = new SafeCancellationTokenSource(); }
                }
            }
        }

        private readonly object _cancUserLocker = new object();
        private SafeCancellationTokenSource _userCts = null;
        private SafeCancellationTokenSource UserCts
        {
            get { lock (_cancUserLocker) { return _userCts; } }
        }
        private bool _isCancelledByUser = false;
        public bool IsCancelledByUser
        {
            get
            {
                lock (_cancUserLocker)
                {
                    return _isCancelledByUser;
                }
            }
            private set
            {
                lock (_cancUserLocker)
                {
                    _isCancelledByUser = value;
                    if (_isCancelledByUser) _userCts?.CancelSafe(true);
                    else { _userCts?.Dispose(); _userCts = new SafeCancellationTokenSource(); }
                }
            }
        }

        private readonly object _cancConnLocker = new object();
        private SafeCancellationTokenSource _connCts = null;
        private SafeCancellationTokenSource ConnCts
        {
            get { lock (_cancConnLocker) { return _connCts; } }
        }
        private void UpdateConnCts()
        {
            lock (_cancConnLocker)
            {
                if (_runtimeData.IsConnectionAvailable) { _connCts?.Dispose(); _connCts = new SafeCancellationTokenSource(); }
                else { _connCts?.CancelSafe(true); }
            }
        }

        protected readonly IGeoBoundingBoxProvider _gbbProvider = null;
        #endregion properties

        #region lifecycle
        public TileDownloader(IGeoBoundingBoxProvider gbbProvider)
        {
            _gbbProvider = gbbProvider;
        }
        protected override Task OpenMayOverrideAsync(object args = null)
        {
            _runtimeData.PropertyChanged += OnRuntimeData_PropertyChanged;
            IsSuspended = false;
            IsCancelledByUser = false;
            UpdateConnCts();
            return Task.CompletedTask;
        }

        protected override Task CloseMayOverrideAsync(object args = null)
        {
            bool isSuspending = args != null && (LifecycleEvents)args == LifecycleEvents.Suspending;
            if (isSuspending) IsSuspended = true;

            _runtimeData.PropertyChanged -= OnRuntimeData_PropertyChanged;
            lock (_cancUserLocker)
            {
                _userCts?.Dispose();
                _userCts = null;
            }
            lock (_cancConnLocker)
            {
                _connCts?.Dispose();
                _connCts = null;
            }
            lock (_cancSuspendLocker)
            {
                _suspendCts?.Dispose();
                _suspendCts = null;
            }
            return Task.CompletedTask;
        }
        #endregion lifecycle

        #region event handlers
        private void OnRuntimeData_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RuntimeData.IsConnectionAvailable))
            {
                UpdateConnCts();
            }
        }

        internal void CancelDownloadByUser()
        {
            IsCancelledByUser = true;
        }
        #endregion event handlers

        #region save services
        public async Task<List<Tuple<int, int>>> StartOrResumeDownloadTilesAsync()
        {
            var results = new List<Tuple<int, int>>();
            await RunFunctionIfOpenAsyncT(async delegate
            {
                var persistentData = PersistentData.GetInstance();
                if (!persistentData.IsTilesDownloadDesired || !_runtimeData.IsConnectionAvailable) return;

                // if persistentData.IsTilesDownloadDesired changes in the coming ticks, tough! Not atomic, but not critical at all.                
                try
                {
                    IsCancelledByUser = false;

                    var gbb = await _gbbProvider.GetMinMaxLatLonAsync().ConfigureAwait(false);
                    if (gbb == null) return;

                    var session = await persistentData.InitOrReinitDownloadSessionAsync(gbb).ConfigureAwait(false);
                    if (session == null) return;
                    // result = SaveTiles_RespondingToCancel(tileCacheAndSession.Item1, tileCacheAndSession.Item2);
                    foreach (var ts in session.TileSources)
                    {
                        results.Add(await Task.Run(() =>
                            SaveTiles_RespondingToCancel2(session, ts), CancToken).ConfigureAwait(false));
                    }
                }
                catch (Exception ex)
                {
                    Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
                }
                finally
                {
                    // even if something went wrong (maybe the new session is not valid), do not leave the download open!
                    await CloseDownload2Async(persistentData, true).ConfigureAwait(false);
                }
            }).ConfigureAwait(false);
            return results;
        }
        private async Task CloseDownload2Async(PersistentData persistentData, bool isSuccess)
        {
            // maybe the user cancelled: that means they are happy with this download, or at least we can consider it complete.
            if (IsCancelledByUser)
            {
                await persistentData.SetTilesDownloadPropsAsync(false, 0, true).ConfigureAwait(false);
            }
            // unless it was interrupted by suspension or connection going missing, the download is no more required because it finished: mark it.
            else if (!IsSuspended && _runtimeData.IsConnectionAvailable)
            {
                if (isSuccess) await persistentData.SetTilesDownloadPropsAsync(false, 0, true).ConfigureAwait(false);
            }
        }
        private Tuple<int, int> SaveTiles_RespondingToCancel2(DownloadSession session, TileSourceRecord tileSource)
        {
            _runtimeData.SetDownloadProgressValue_UI(0.0);

            int totalCnt = 0;
            int currentOkCnt = 0;
            int currentCnt = 0;

            if (session != null && _runtimeData.IsConnectionAvailable)
            {
                var requiredTilesOrderedByZoom = GetTileData_RespondingToCancel(session.NWCorner, session.SECorner, tileSource.MaxZoom, tileSource.MinZoom);
                totalCnt = requiredTilesOrderedByZoom.Count;

                if (totalCnt > 0)
                {
                    int[] stepsWhenIWantToRaiseProgress = GetStepsToReport(totalCnt);
                    // LOLLO NOTE this parallelisation is faster than without, by 1.2 to 2 x.
                    // This cannot be done in a background task because it is too memory consuming (June 2017).
#if DEBUG
                    Stopwatch sw0 = new Stopwatch();
                    sw0.Start();
#endif
                    CancellationTokenSource cancTokenSourceLinked = null;
                    try
                    {
                        var tileCache = new TileCacheReaderWriter(tileSource, false, false);

                        cancTokenSourceLinked = CancellationTokenSource.CreateLinkedTokenSource(
                            CancToken, SuspendCts.Token, UserCts.Token, ConnCts.Token/*, ProcessingQueue.GetInstance().CancellationToken*/);
                        var cancToken = cancTokenSourceLinked.Token;

                        if (cancToken != null && !cancToken.IsCancellationRequested)
                        {
                            // we don't want too much parallelism coz it will be dead slow when cancelling on a small device. 4 looks OK, we can try a bit more.
                            Parallel.ForEach(requiredTilesOrderedByZoom, new ParallelOptions() { CancellationToken = cancToken, MaxDegreeOfParallelism = 4 }, tile =>
                            {
                                bool isOk = tileCache.TrySaveTileAsync(tile.X, tile.Y, tile.Z, tile.Zoom, cancToken).Result;
                                if (isOk) currentOkCnt++;

                                currentCnt++;
                                if (totalCnt > 0 && stepsWhenIWantToRaiseProgress.Contains(currentCnt)) _runtimeData.SetDownloadProgressValue_UI((double)currentCnt / (double)totalCnt);
                            });
                        }
                    }
                    catch (OperationCanceledException) { } // comes from the canc token
                    catch (ObjectDisposedException) { } // comes from the canc token
                    catch (Exception ex) { Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename); }
                    finally
                    {
                        cancTokenSourceLinked?.Dispose();
                    }
#if DEBUG
                    sw0.Stop();
                    Debug.WriteLine("sw0.ElapsedMilliseconds " + sw0.ElapsedMilliseconds + " currentCnt " + currentCnt + " currentOkCnt " + currentOkCnt);
#endif
                }
            }
            _runtimeData.SetDownloadProgressValue_UI(1.0);
            return Tuple.Create(currentOkCnt, totalCnt);
        }
        private static int[] GetStepsToReport(int totalCnt)
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
            return stepsWhenIWantToRaiseProgress;
        }
        #endregion save services

        #region read services
        public async Task<List<Tuple<int, int>>> GetHowManyTiles4DifferentZooms4CurrentConditionsAsync()
        {
            var output = new List<Tuple<int, int>>();

            await RunFunctionIfOpenAsyncT(async delegate
            {
                try
                {
                    IsCancelledByUser = false;

                    var gbb = await _gbbProvider.GetMinMaxLatLonAsync().ConfigureAwait(false);
                    if (gbb == null) return;
                    // now I have a geobounding box that certainly encloses the screen.
                    var session = await PersistentData.GetInstance().GetLargestPossibleDownloadSession4CurrentTileSourcesAsync(gbb).ConfigureAwait(false);
                    if (session == null) return;
                    // we want this method to be quick, so we don't loop over the layers, but only use the session data once
                    var tilesOrderedByZoom = GetTileData_RespondingToCancel(session.NWCorner, session.SECorner, session.MaxZoom, session.MinZoom);
                    if (tilesOrderedByZoom == null) return;
                    for (int zoom = session.MinZoom; zoom <= session.MaxZoom; zoom++)
                    {
                        int howManyAtOrBeforeZoom = tilesOrderedByZoom.Count(a => a.Zoom <= zoom);
                        output.Add(Tuple.Create(zoom, howManyAtOrBeforeZoom));
                    }
                }
                catch (Exception ex)
                {
                    Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
                }
            }).ConfigureAwait(false);

            return output;
        }

        protected List<TileCacheRecord> GetTileData_RespondingToCancel(BasicGeoposition nwCorner, BasicGeoposition seCorner, int maxZoom, int minZoom)
        {
            var output = new List<TileCacheRecord>();
            if (nwCorner.Latitude == seCorner.Latitude && nwCorner.Longitude == seCorner.Longitude || maxZoom < minZoom) return output;

            CancellationTokenSource cancTokenSourceLinked = null;
            try
            {
                cancTokenSourceLinked = CancellationTokenSource.CreateLinkedTokenSource(CancToken, SuspendCts.Token, UserCts.Token, ConnCts.Token);
                var cancToken = cancTokenSourceLinked.Token;

                if (cancToken.IsCancellationRequested) return output;

                int totalCnt = 0;
                for (int zoom = minZoom; zoom <= maxZoom; zoom++)
                {
                    var topLeftTile = new TileCacheRecord(PseudoMercator.Lon2TileX(nwCorner.Longitude, zoom), PseudoMercator.Lat2TileY(nwCorner.Latitude, zoom), 0, zoom); // Alaska
                    var bottomRightTile = new TileCacheRecord(PseudoMercator.Lon2TileX(seCorner.Longitude, zoom), PseudoMercator.Lat2TileY(seCorner.Latitude, zoom), 0, zoom); // New Zealand
                    int maxX4Zoom = PseudoMercator.MaxTilexX4Zoom(zoom);
                    Debug.WriteLine("topLeftTile.X = " + topLeftTile.X + " topLeftTile.Y = " + topLeftTile.Y + " bottomRightTile.X = " + bottomRightTile.X + " bottomRightTile.Y = " + bottomRightTile.Y + " and zoom = " + zoom);

                    bool exit = false;
                    bool hasJumpedDateLine = false;

                    int x = topLeftTile.X;
                    while (!exit)
                    {
                        for (int y = topLeftTile.Y; y <= bottomRightTile.Y; y++)
                        {
                            output.Add(new TileCacheRecord(x, y, 0, zoom));
                            totalCnt++;
                            if (totalCnt > ConstantData.MAX_TILES_TO_LEECH || cancToken.IsCancellationRequested)
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
                    if (totalCnt > ConstantData.MAX_TILES_TO_LEECH || cancToken.IsCancellationRequested) break;
                }
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            {
                Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
            }
            finally
            {
                cancTokenSourceLinked?.Dispose();
            }
            return output;
        }
        #endregion read services
    }
}