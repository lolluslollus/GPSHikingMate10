using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using Utilz;
using Utilz.Data;
using Windows.Storage;

namespace LolloGPS.Data.TileCache
{
    // LOLLO TODO MAYBE before and after clearing, say how much disk space you saved
    /// <summary>
    /// CacheClearerSaver and CacheReaderWriter cannot be the same thing because they have different purposes and properties. 
    /// The former is a singleton.
    /// </summary>
    public sealed class TileCacheClearerSaver : OpenableObservableData
    {
        #region properties
        private volatile bool _isClearingScheduled = false;
        public bool IsClearingScheduled
        {
            get { return _isClearingScheduled; }
            private set
            {
                if (_isClearingScheduled != value)
                {
                    _isClearingScheduled = value;
                    IsClearingScheduledChanged?.Invoke(null, new PropertyChangedEventArgs(nameof(IsClearingScheduled)));
                }
            }
        }

        private volatile bool _isSavingScheduled = false;
        public bool IsSavingScheduled
        {
            get { return _isSavingScheduled; }
            private set
            {
                if (_isSavingScheduled != value)
                {
                    _isSavingScheduled = value;
                    IsSavingScheduledChanged?.Invoke(null, new PropertyChangedEventArgs(nameof(IsSavingScheduled)));
                }
            }
        }

        private static readonly SemaphoreSlimSafeRelease _tileCacheClearerSemaphore = new SemaphoreSlimSafeRelease(1, 1);
        private static readonly object _instanceLocker = new object();
        private static TileCacheClearerSaver _instance = null;
        #endregion properties


        #region events
        public static event PropertyChangedEventHandler IsClearingScheduledChanged;
        public static event PropertyChangedEventHandler IsSavingScheduledChanged;
        public static event EventHandler<CacheClearedEventArgs> CacheCleared;
        public static event EventHandler<CacheSavedEventArgs> CacheSaved;
        public sealed class CacheClearedEventArgs : EventArgs
        {
            private readonly TileSourceRecord _tileSource = null;
            public TileSourceRecord TileSource { get { return _tileSource; } }
            private readonly bool _isAlsoRemoveSources = false;
            public bool IsAlsoRemoveSources { get { return _isAlsoRemoveSources; } }
            private readonly bool _isCacheCleared = false;
            public bool IsCacheCleared { get { return _isCacheCleared; } }
            //private readonly int _howManyRecordsDeleted = 0;
            //public int HowManyRecordsDeleted { get { return _howManyRecordsDeleted; } }

            public CacheClearedEventArgs(TileSourceRecord tileSource, bool isAlsoRemoveSources, bool isCacheCleared/*, int howManyRecordsDeleted*/)
            {
                _tileSource = tileSource;
                _isAlsoRemoveSources = isAlsoRemoveSources;
                _isCacheCleared = isCacheCleared;
                //_howManyRecordsDeleted = howManyRecordsDeleted;
            }
        }
        public sealed class CacheSavedEventArgs : EventArgs
        {
            private readonly TileSourceRecord _tileSource = null;
            public TileSourceRecord TileSource { get { return _tileSource; } }
            private readonly bool _isCacheSaved = false;
            public bool IsCacheSaved { get { return _isCacheSaved; } }
            private readonly int _howManyRecordsSaved = 0;
            public int HowManyRecordsSaved { get { return _howManyRecordsSaved; } }

            public CacheSavedEventArgs(TileSourceRecord tileSource, bool isCacheSaved, int howManyRecordsSaved)
            {
                _tileSource = tileSource;
                _isCacheSaved = isCacheSaved;
                _howManyRecordsSaved = howManyRecordsSaved;
            }
        }
        #endregion events


        #region lifecycle
        public static TileCacheClearerSaver GetInstance()
        {
            lock (_instanceLocker)
            {
                return _instance ?? (_instance = new TileCacheClearerSaver());
            }
        }

        private TileCacheClearerSaver() { }

        protected override async Task OpenMayOverrideAsync(object args = null)
        {
            // resume clearing cache if it was interrupted
            var cacheClearingProps = await GetIsClearingCacheProps().ConfigureAwait(false);
            if (cacheClearingProps != null) // we don't want to hog anything, we schedule it for later.
            {
                await TryScheduleClearCache2Async(cacheClearingProps.TileSource, cacheClearingProps.IsAlsoRemoveSources, false).ConfigureAwait(false);
            }
            // we don't resume the saving if it was interrupted
        }
        #endregion lifecycle


        #region core
        private async Task ClearCacheAsync(TileSourceRecord tileSource, bool isAlsoRemoveSources)
        {
            Debug.WriteLine("ClearCacheAsync() started");

            var tryCancResult = await PersistentData.GetInstance().TryClearCacheAsync(tileSource, isAlsoRemoveSources, CancToken).ConfigureAwait(false);
            if (tryCancResult/*.Item1*/ == PersistentData.ClearCacheResult.Error)
            {
                await SetIsClearingCacheProps(null, false).ConfigureAwait(false);
                IsClearingScheduled = false;
                CacheCleared?.Invoke(null, new CacheClearedEventArgs(tileSource, isAlsoRemoveSources, false/*, tryCancResult.Item2*/));
                Debug.WriteLine("ClearCacheAsync() ended with error");
            }
            else if (tryCancResult/*.Item1*/ == PersistentData.ClearCacheResult.Ok)
            {
                await SetIsClearingCacheProps(null, false).ConfigureAwait(false);
                IsClearingScheduled = false;
                CacheCleared?.Invoke(null, new CacheClearedEventArgs(tileSource, isAlsoRemoveSources, true/*, tryCancResult.Item2*/));
                Debug.WriteLine("ClearCacheAsync() ended OK");
            }
            else
            {
                Debug.WriteLine("ClearCacheAsync() cancelled");
            }

            //// test begin
            //await GetAllFilesInLocalFolder().ConfigureAwait(false);
            //// test end
        }
        private async Task SaveCacheAsync(TileSourceRecord tileSource, StorageFolder destinationFolder)
        {
            Logger.Add_TPL("SaveCacheAsync() started", Logger.AppEventsLogFilename, Logger.Severity.Info, false);

            var trySaveResult = await PersistentData.GetInstance().TrySaveCacheAsync(tileSource, destinationFolder, CancToken).ConfigureAwait(false);
            Logger.Add_TPL($"SaveCacheAsync() ended with result = {trySaveResult}", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
            IsSavingScheduled = false;
            CacheSaved?.Invoke(null, new CacheSavedEventArgs(tileSource, trySaveResult > 0, trySaveResult));
        }
        #endregion core


        #region utils
        public Task<bool> TryScheduleClearCacheAsync(TileSourceRecord tileSource, bool isAlsoRemoveSources)
        {
            return RunFunctionIfOpenAsyncTB(async delegate
            {
                var tileSourceClone = await PersistentData.GetInstance().GetTileSourceClone(tileSource, CancToken).ConfigureAwait(false);
                return await TryScheduleClearCache2Async(tileSourceClone, isAlsoRemoveSources, true).ConfigureAwait(false);
            });
        }
        public Task<bool> TryScheduleSaveCacheAsync(TileSourceRecord tileSource, StorageFolder destinationFolder)
        {
            return RunFunctionIfOpenAsyncTB(async delegate
            {
                var tileSourceClone = await PersistentData.GetInstance().GetTileSourceClone(tileSource, CancToken).ConfigureAwait(false);
                return await TryScheduleSaveCache2Async(tileSourceClone, destinationFolder).ConfigureAwait(false);
            });
        }

        private async Task<bool> TryScheduleClearCache2Async(TileSourceRecord tileSource, bool isAlsoRemoveSources, bool writeAwayTheProps)
        {
            if (tileSource == null || tileSource.IsNone || tileSource.IsDefault) return false;

            if (writeAwayTheProps) await SetIsClearingCacheProps(tileSource, isAlsoRemoveSources).ConfigureAwait(false);
            IsClearingScheduled = await ProcessingQueue.TryScheduleTaskAsync(() => ClearCacheAsync(tileSource, isAlsoRemoveSources), CancToken).ConfigureAwait(false);
            return IsClearingScheduled;
        }
        private async Task<bool> TryScheduleSaveCache2Async(TileSourceRecord tileSource, StorageFolder destinationFolder)
        {
            if (tileSource == null || tileSource.IsNone || tileSource.IsDefault) return false;

            IsSavingScheduled = await ProcessingQueue.TryScheduleTaskAsync(() => SaveCacheAsync(tileSource, destinationFolder), CancToken).ConfigureAwait(false);
            return IsSavingScheduled;
        }
        private static async Task SetIsClearingCacheProps(TileSourceRecord tileSource, bool isAlsoRemoveSources)
        {
            try
            {
                await _tileCacheClearerSemaphore.WaitAsync().ConfigureAwait(false);

                if (tileSource == null)
                {
                    RegistryAccess.TrySetValue(ConstantData.REG_CLEARING_CACHE_IS_REMOVE_SOURCES, false.ToString());
                    RegistryAccess.TrySetValue(ConstantData.REG_CLEARING_CACHE_TILE_SOURCE, string.Empty);
                }
                else
                {
                    if (await RegistryAccess.TrySetObject(ConstantData.REG_CLEARING_CACHE_TILE_SOURCE, tileSource).ConfigureAwait(false))
                    {
                        RegistryAccess.TrySetValue(ConstantData.REG_CLEARING_CACHE_IS_REMOVE_SOURCES, isAlsoRemoveSources.ToString());
                    }
                }
            }
            finally
            {
                SemaphoreSlimSafeRelease.TryRelease(_tileCacheClearerSemaphore);
            }
        }
        private static async Task<CacheClearedEventArgs> GetIsClearingCacheProps()
        {
            try
            {
                await _tileCacheClearerSemaphore.WaitAsync().ConfigureAwait(false);

                string isAlsoRemoveSourcesString = RegistryAccess.GetValue(ConstantData.REG_CLEARING_CACHE_IS_REMOVE_SOURCES);
                string tileSourceString = RegistryAccess.GetValue(ConstantData.REG_CLEARING_CACHE_TILE_SOURCE);
                if (string.IsNullOrWhiteSpace(tileSourceString)) return null;

                var tileSource = await RegistryAccess.GetObject<TileSourceRecord>(ConstantData.REG_CLEARING_CACHE_TILE_SOURCE).ConfigureAwait(false);
                return new CacheClearedEventArgs(tileSource, isAlsoRemoveSourcesString.Equals(true.ToString()), false/*, 0*/);
            }
            finally
            {
                SemaphoreSlimSafeRelease.TryRelease(_tileCacheClearerSemaphore);
            }
        }
        #endregion utils
    }
}
