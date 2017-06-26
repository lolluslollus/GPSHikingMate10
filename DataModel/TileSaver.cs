﻿using LolloGPS.Data.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Utilz;
using Windows.Storage;

namespace LolloGPS.Data
{
    public static class TileSaver
    {
        public const int MaxProgressStepsToReport = 25;
        private static readonly RuntimeData _runtimeData = RuntimeData.GetInstance();
        private static readonly object _ctsLocker = new object();
        private static SafeCancellationTokenSource _localCts;
        private static CancellationTokenSource _linkedCts;

        #region events
        public static event EventHandler<double> SaveProgressChanged;
        private static void RaiseProgressChanged(double progress)
        {
            _runtimeData.SetDownloadProgressValue_UI(progress);
            SaveProgressChanged?.Invoke(null, progress);
        }
        #endregion events

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
            try
            {
                if (outerCancToken.IsCancellationRequested) return 0;
                if (tileSource == null || tileSource.IsNone || tileSource.IsDefault || tileSource.IsAll || tileSource.IsFileSource || destinationFolder == null) return 0;
                RaiseProgressChanged(0.0);

                var myCancToken = GetLinkedCancToken(outerCancToken);

                var tileSourcesRootFolder = await ApplicationData.Current.LocalCacheFolder.TryGetItemAsync(ConstantData.TILE_SOURCES_DIR_NAME).AsTask(myCancToken).ConfigureAwait(false) as StorageFolder;
                if (tileSourcesRootFolder == null) return 0;
                if (myCancToken.IsCancellationRequested) return 0;
                var tileSourceFolder = await tileSourcesRootFolder.TryGetItemAsync(tileSource.FolderName).AsTask(myCancToken).ConfigureAwait(false) as StorageFolder;
                if (tileSourceFolder == null) return 0;

                if (myCancToken.IsCancellationRequested) return 0;
                var files = await tileSourceFolder.GetFilesAsync().AsTask(myCancToken).ConfigureAwait(false);
                if (myCancToken.IsCancellationRequested) return 0;

                int currentCnt = 0;
                int totalCnt = files.Count;
                if (totalCnt == 0) return 0;

                int[] stepsWhenIWantToRaiseProgress = GetStepsToReport(totalCnt);

                // we don't want too much parallelism coz it will be dead slow when cancelling on a small device. 4 looks OK, we can try a bit more.
                Parallel.ForEach(files, new ParallelOptions() { CancellationToken = myCancToken, MaxDegreeOfParallelism = 4 }, file =>
                {
                    var copiedFile = file.CopyAsync(destinationFolder, file.Name, NameCollisionOption.ReplaceExisting).AsTask(myCancToken).Result;
                    currentCnt++;
                    if (totalCnt > 0 && stepsWhenIWantToRaiseProgress.Contains(currentCnt)) RaiseProgressChanged((double)currentCnt / (double)totalCnt);
                });

                return currentCnt;
            }
            catch (ObjectDisposedException) { return 0; }
            catch (OperationCanceledException) { return 0; }
            catch (Exception exc)
            {
                Logger.Add_TPL(exc.ToString(), Logger.FileErrorLogFilename);
                return 0;
            }
            finally
            {
                _linkedCts?.Dispose();
                _localCts?.Dispose();
                RaiseProgressChanged(1.0);
            }
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
    }
}
