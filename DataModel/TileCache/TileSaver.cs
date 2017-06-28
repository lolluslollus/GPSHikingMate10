using LolloGPS.Data.Runtime;
using System;
using System.Threading;
using System.Threading.Tasks;
using Utilz;
using Windows.Storage;

namespace LolloGPS.Data.TileCache
{
    public static class TileSaver
    {
        public const int MaxProgressStepsToReport = 25;
        private static readonly RuntimeData _runtimeData = RuntimeData.GetInstance();
        private static readonly object _ctsLocker = new object();
        private static SafeCancellationTokenSource _localCts;
        private static CancellationTokenSource _linkedCts;

        public static void Cancel()
        {
            lock (_ctsLocker)
            {
                _localCts?.CancelSafe(true);
            }
        }
        private static CancellationToken GetLinkedCancToken(CancellationToken outerCancToken)
        {
            lock (_ctsLocker)
            {
                _localCts?.Dispose();
                _localCts = new SafeCancellationTokenSource();
                _linkedCts = CancellationTokenSource.CreateLinkedTokenSource(outerCancToken, _localCts.Token);

                return _linkedCts.Token;
            }
        }
        public static async Task<int> TrySaveCacheAsync(TileSourceRecord tileSource, StorageFolder destinationFolder, CancellationToken outerCancToken)
        {
            int currentCnt = 0;

            try
            {
                if (outerCancToken.IsCancellationRequested) return 0;
                if (tileSource == null || tileSource.IsNone || tileSource.IsDefault || tileSource.IsAll || tileSource.IsFileSource || destinationFolder == null) return 0;

                _runtimeData.SetSaveProgressValue_UI(0.01);
                var myCancToken = GetLinkedCancToken(outerCancToken);

                var tileSourcesRootFolder = await ApplicationData.Current.LocalCacheFolder.TryGetItemAsync(ConstantData.TILE_SOURCES_DIR_NAME).AsTask(myCancToken).ConfigureAwait(false) as StorageFolder;
                if (tileSourcesRootFolder == null) return 0;
                if (myCancToken.IsCancellationRequested) return 0;

                var tileSourceFolder = await tileSourcesRootFolder.TryGetItemAsync(tileSource.FolderName).AsTask(myCancToken).ConfigureAwait(false) as StorageFolder;
                if (tileSourceFolder == null) return 0;
                if (myCancToken.IsCancellationRequested) return 0;

                var files = await tileSourceFolder.GetFilesAsync().AsTask(myCancToken).ConfigureAwait(false);
                if (myCancToken.IsCancellationRequested) return 0;

                int totalCnt = files.Count;
                if (totalCnt == 0) return 0;

                var stepsWhenIWantToRaiseProgress = ProgressHelper.GetStepsToReport(totalCnt, MaxProgressStepsToReport);
                var progressLocker = new object();

                // we don't want too much parallelism coz it will be dead slow when cancelling on a small device. 4 looks OK, we can try a bit more.
                Parallel.ForEach(files, new ParallelOptions() { CancellationToken = myCancToken, MaxDegreeOfParallelism = 4 }, storageFile =>
                {
                    var copiedFile = storageFile.CopyAsync(destinationFolder, storageFile.Name, NameCollisionOption.ReplaceExisting).AsTask(myCancToken).Result;
                    lock (progressLocker)
                    {
                        currentCnt++;
                        if (stepsWhenIWantToRaiseProgress.Count == 0) return;
                        if (stepsWhenIWantToRaiseProgress.Peek() == currentCnt) _runtimeData.SetSaveProgressValue_UI((double)stepsWhenIWantToRaiseProgress.Pop() / (double)totalCnt);
                    }
                });

                return currentCnt;
            }
            catch (ObjectDisposedException) { return currentCnt; }
            catch (OperationCanceledException) { return currentCnt; }
            catch (Exception exc)
            {
                Logger.Add_TPL(exc.ToString(), Logger.FileErrorLogFilename);
                return currentCnt;
            }
            finally
            {
                _linkedCts?.Dispose();
                _localCts?.Dispose();
                _runtimeData.SetSaveProgressValue_UI(1.0);
            }
        }
    }
}