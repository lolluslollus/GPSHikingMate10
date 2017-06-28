using LolloGPS.Calcs;
using LolloGPS.Data.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Utilz;
using Utilz.Data;
using Windows.Devices.Geolocation;

namespace LolloGPS.Data.TileCache
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

        public void CancelDownloadByUser()
        {
            IsCancelledByUser = true;
        }
        #endregion event handlers

        #region services
        public async Task<List<Tuple<int, int>>> StartOrResumeDownloadTilesAsync(CancellationToken cancToken)
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

                    var session = await persistentData.InitOrReinitDownloadSessionAsync(gbb, cancToken).ConfigureAwait(false);
                    if (session == null) return;
                    // result = SaveTiles_RespondingToCancel(tileCacheAndSession.Item1, tileCacheAndSession.Item2);
                    foreach (var ts in session.TileSources)
                    {
                        results.Add(await Task.Run(() =>
                            SaveTiles2(session, ts, cancToken), cancToken).ConfigureAwait(false));
                    }
                }
                catch (OperationCanceledException) { }
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
        private Tuple<int, int> SaveTiles2(DownloadSession session, TileSourceRecord tileSource, CancellationToken cancToken)
        {
            int totalCnt = 0;
            int currentOkCnt = 0;
            int currentCnt = 0;
            CancellationTokenSource cancTokenSourceLinked = null;

            try
            {
                if (cancToken.IsCancellationRequested) return Tuple.Create(0, 0);
                if (session == null || !_runtimeData.IsConnectionAvailable) return Tuple.Create(0, 0);

                _runtimeData.SetDownloadProgressValue_UI(0.01);
                cancTokenSourceLinked = CancellationTokenSource.CreateLinkedTokenSource(cancToken, SuspendCts.Token, UserCts.Token, ConnCts.Token);
                var linkedCancToken = cancTokenSourceLinked.Token;

                var requiredTilesOrderedByZoom = TileCoordinates.GetTileCoordinates4MultipleZoomLevels(session.NWCorner, session.SECorner, tileSource.MaxZoom, tileSource.MinZoom, ConstantData.MAX_TILES_TO_LEECH, linkedCancToken);
                totalCnt = requiredTilesOrderedByZoom.Count;
                if (linkedCancToken.IsCancellationRequested) return Tuple.Create(0, 0);
                if (totalCnt == 0) return Tuple.Create(0, 0);

                var tileCacheReaderWriter = new TileCacheReaderWriter(tileSource, false, false, null, linkedCancToken);
                if (linkedCancToken.IsCancellationRequested) return Tuple.Create(0, 0);

                var stepsWhenIWantToRaiseProgress = ProgressHelper.GetStepsToReport(totalCnt, MaxProgressStepsToReport);
                var progressLocker = new object();

                // LOLLO NOTE this parallelisation is faster than without, by 1.2 to 2 x.
                // This cannot be done in a background task because it is too memory consuming (June 2017).
                // we don't want too much parallelism coz it will be dead slow when cancelling on a small device. 4 looks OK, we can try a bit more.
                Parallel.ForEach(requiredTilesOrderedByZoom, new ParallelOptions() { CancellationToken = linkedCancToken, MaxDegreeOfParallelism = 4 }, tile =>
                {
                    bool isOk = tileCacheReaderWriter.TrySaveTileAsync(tile.X, tile.Y, tile.Z, tile.Zoom, linkedCancToken).Result;
                    lock (progressLocker)
                    {
                        if (isOk) currentOkCnt++;
                        currentCnt++;
                        if (stepsWhenIWantToRaiseProgress.Count == 0) return;
                        if (stepsWhenIWantToRaiseProgress.Peek() == currentCnt) _runtimeData.SetDownloadProgressValue_UI((double)stepsWhenIWantToRaiseProgress.Pop() / (double)totalCnt);
                        //if (totalCnt > 0 && stepsWhenIWantToRaiseProgress.Contains(currentCnt)) _runtimeData.SetDownloadProgressValue_UI((double)currentCnt / (double)totalCnt);
                    }
                });

                return Tuple.Create(currentOkCnt, totalCnt);
            }
            catch (ObjectDisposedException) { return Tuple.Create(currentOkCnt, totalCnt); } // comes from the canc token
            catch (OperationCanceledException) { return Tuple.Create(currentOkCnt, totalCnt); } // comes from the canc token
            catch (Exception ex)
            {
                Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
                return Tuple.Create(currentOkCnt, totalCnt);
            }
            finally
            {
                cancTokenSourceLinked?.Dispose();
                _runtimeData.SetDownloadProgressValue_UI(1.0);
            }
        }
        public async Task<List<Tuple<int, int>>> GetHowManyTiles4DifferentZooms4CurrentConditionsAsync(CancellationToken cancToken)
        {
            var output = new List<Tuple<int, int>>();

            await RunFunctionIfOpenAsyncT(async delegate
            {
                try
                {
                    if (cancToken.IsCancellationRequested) return;
                    IsCancelledByUser = false;

                    var gbb = await _gbbProvider.GetMinMaxLatLonAsync().ConfigureAwait(false);
                    if (gbb == null) return;
                    // now I have a geobounding box that certainly encloses the screen.
                    var session = await PersistentData.GetInstance().GetLargestPossibleDownloadSession4CurrentTileSourcesAsync(gbb, cancToken).ConfigureAwait(false);
                    if (session == null) return;
                    // we want this method to be quick, so we don't loop over the layers, but only use the session data once
                    var tilesOrderedByZoom = TileCoordinates.GetTileCoordinates4MultipleZoomLevels(session.NWCorner, session.SECorner, session.MaxZoom, session.MinZoom, ConstantData.MAX_TILES_TO_LEECH, cancToken);
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
        #endregion services
    }
}