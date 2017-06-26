using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Utilz;

namespace LolloGPS.Data.TileCache
{
    /// <summary>
    /// As soon as a file (ie a unique combination of TileSource, X, Y, Z and Zoom) is in process, this class stores it.
    /// As soon as no files are in process, this class can run a delegate, if it was scheduled.
    /// </summary>
    internal static class ProcessingQueue
    {
        #region properties
        private static readonly List<string> _fileNamesInProcess = new List<string>();
        private static Func<Task> _funcAsSoonAsFree = null;
        private static readonly SemaphoreSlimSafeRelease _processingQueueSemaphore = new SemaphoreSlimSafeRelease(1, 1);
        #endregion properties

        #region services
        /// <summary>
        /// Not working on this set of data? Mark it as busy, closing the gate for other threads.
        /// Already working on this set of data? Say so.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        internal static async Task<bool> TryAddToQueueAsync(string fileName)
        {
            try
            {
                await _processingQueueSemaphore.WaitAsync().ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(fileName) && !_fileNamesInProcess.Contains(fileName))
                {
                    _fileNamesInProcess.Add(fileName);
                    return true;
                }
                return false;
            }
            catch { return false; }
            finally
            {
                SemaphoreSlimSafeRelease.TryRelease(_processingQueueSemaphore);
            }
        }
        /// <summary>
        /// Not working on this set of data anymore? Mark it as free, opening the gate for other threads.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        internal static async Task RemoveFromQueueAsync(string fileName)
        {
            try
            {
                await _processingQueueSemaphore.WaitAsync().ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(fileName))
                {
                    _fileNamesInProcess.Remove(fileName);
                    await TryRunFuncAsSoonAsFree().ConfigureAwait(false);
                }
            }
            catch { }
            finally
            {
                SemaphoreSlimSafeRelease.TryRelease(_processingQueueSemaphore);
            }
        }
        /// <summary>
        /// Schedules a delegate to be run as soon as no data is being processed.
        /// If it can run it now, it will wait until the method has exited.
        /// </summary>
        /// <param name="func"></param>
        /// <returns></returns>
        internal static async Task<bool> TryScheduleTaskAsync(Func<Task> func, CancellationToken cancToken)
        {
            try
            {
                await _processingQueueSemaphore.WaitAsync().ConfigureAwait(false);
                if (_funcAsSoonAsFree != null) return false;
                _funcAsSoonAsFree = func;

                Task runFunc = Task.Run(async delegate // use separate thread to avoid deadlock
                {
                    // the following will run after the current method is over because it queues before the semaphore.
                    try
                    {
                        await _processingQueueSemaphore.WaitAsync().ConfigureAwait(false);
                        await TryRunFuncAsSoonAsFree().ConfigureAwait(false);
                    }
                    finally
                    {
                        SemaphoreSlimSafeRelease.TryRelease(_processingQueueSemaphore);
                    }
                }, cancToken);

                return true;
            }
            catch { return false; }
            finally
            {
                SemaphoreSlimSafeRelease.TryRelease(_processingQueueSemaphore);
            }
        }

        /// <summary>
        /// This method must be run inside the semaphore
        /// </summary>
        /// <returns></returns>
        private static async Task<bool> TryRunFuncAsSoonAsFree()
        {
            if (!_fileNamesInProcess.Any() && _funcAsSoonAsFree != null)
            {
                try
                {
                    await _funcAsSoonAsFree().ConfigureAwait(false);
                }
                finally
                {
                    _funcAsSoonAsFree = null;
                }
                return true;
            }
            return false;
        }
        #endregion services
    }

}
