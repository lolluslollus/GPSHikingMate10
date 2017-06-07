using LolloGPS.Data.Runtime;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Utilz;
using Utilz.Data;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace LolloGPS.Data.TileCache
{
    // public enum TileSources { Nokia, OpenStreetMap, Swisstopo, Wanderreitkarte, OrdnanceSurvey, ForUMaps, OpenSeaMap, UTTopoLight, ArcGIS }

    public sealed class TileCacheReaderWriter
    {
        public const string MimeTypeImagePrefix = "image/";
        public const string MimeTypeImageAny = "image/*"; // "image/png"
                                                          //public const string ImageToCheck = "image";
        public const int MaxRecords = 65535;
        public const int WebRequestTimeoutMsec = 65535;

        private readonly TileSourceRecord _tileSource = TileSourceRecord.GetDefaultTileSource(); //TileSources.Nokia;
        private readonly StorageFolder _imageFolder = null;

        //private readonly object _isCachingLocker = new object();
        //private volatile bool _isCaching = false;
        /// <summary>
        /// Gets if this cache writes away (ie caches) the data it picks up.
        /// Only relevant for supplying map tiles on the fly.
        /// We could read this from PersistentData whenever we need it, but it does not work well.
        /// </summary>
        //public bool IsCaching { get { lock (_isCachingLocker) { return _isCaching; } } set { lock (_isCachingLocker) { _isCaching = value; } } }
        //public bool IsCaching { get { return _isCaching; } set { _isCaching = value; } }

        private readonly bool _isReturnLocalUris = false;
        public bool IsReturnLocalUris { get { return _isReturnLocalUris; } }

        private readonly string _webUriFormat = string.Empty;
        private const string _tileFileFormat = "{3}_{0}_{1}_{2}";

        #region lifecycle
        /// <summary>
        /// Make sure you supply a thread-safe tile source, ie a clone, to preserve atomicity
        /// </summary>
        /// <param name="tileSource"></param>
        /// <param name="isCaching"></param>
        public TileCacheReaderWriter(TileSourceRecord tileSource, bool isCaching, bool isReturnLocalUris)
        {
            if (tileSource == null) throw new ArgumentNullException("TileCache ctor was given tileSource == null");

            TileSourceRecord.Clone(tileSource, ref _tileSource);
            try
            {
                _webUriFormat = _tileSource.UriString.Replace(TileSourceRecord.ZoomLevelPlaceholder, TileSourceRecord.ZoomLevelPlaceholder_Internal);
                _webUriFormat = _webUriFormat.Replace(TileSourceRecord.XPlaceholder, TileSourceRecord.XPlaceholder_Internal);
                _webUriFormat = _webUriFormat.Replace(TileSourceRecord.YPlaceholder, TileSourceRecord.YPlaceholder_Internal);
            }
            catch (Exception exc)
            {
                Debug.WriteLine("Exception in TileCache.ctor: " + exc.Message + exc.StackTrace);
            }

            //_isCaching = isCaching;
            _isReturnLocalUris = isReturnLocalUris;
            _imageFolder = ApplicationData.Current.LocalCacheFolder.CreateFolderAsync(_tileSource.FolderName, CreationCollisionOption.OpenIfExists).AsTask().Result;
        }
        #endregion lifecycle

        #region getters
        private Uri GetUriForFile(string fileName)
        {
            // LOLLO TODO check this method, it never seems to satisfy the local and the http tile source.   
            if (_isReturnLocalUris)
            {
                // return new Uri("ms-appx:///Assets/aim-120.png", UriKind.Absolute); // this works

                var address = $"ms-appdata:///localcache/{_imageFolder.Name}/{fileName}"; // this fails
                var localUri = new Uri(address, UriKind.Absolute);
                return localUri;
            }

            // should work when requesting any uri, but it fails after the MapControl was updated in 10.0.15063.
            // this is why I create the bitmaps... when those imbeciles fix it, we can go back to returning uris like before.
            var filePath = Path.Combine(_imageFolder.Path, fileName);
            var uri = new Uri(filePath, UriKind.Absolute);
            return uri;
        }
        /// <summary>
        /// gets the web uri of the tile (TileSource, X, Y, Z and Zoom)
        /// </summary>
        private string GetWebUri(int x, int y, int z, int zoom)
        {
            try
            {
                return string.Format(_webUriFormat, zoom, x, y);
            }
            catch (Exception exc)
            {
                Debug.WriteLine("Exception in TileCache.GetWebUri(): " + exc.Message + exc.StackTrace);
                return string.Empty;
            }
        }
        /// <summary>
        /// gets the filename that uniquely identifies a tile (TileSource, X, Y, Z and Zoom)
        /// ProcessingQueue is based on a list of strings, which are nothing else than the file names,
        /// so every different tile source must produce a different file name, 
        /// even if X, Y, Z and Zoom are equal.
        /// </summary>
        private string GetFileNameNoExtension(int x, int y, int z, int zoom)
        {
            return string.Format(_tileFileFormat, zoom, x, y, _tileSource.FolderName);
        }

        public int GetTilePixelSize()
        {
            return _tileSource.TilePixelSize;
        }
        public int GetMinZoom()
        {
            return _tileSource.MinZoom;
        }
        public int GetMaxZoom()
        {
            return _tileSource.MaxZoom;
        }
        #endregion getters


        #region services
        private static readonly Uri _mustZoomInUri = new Uri("ms-appx:///Assets/TileMustZoomIn-256.png", UriKind.Absolute);
        private static readonly Uri _mustZoomOutUri = new Uri("ms-appx:///Assets/TileMustZoomOut-256.png", UriKind.Absolute);

        private string GetFileNameFromFileCache(string fileNameNoExtension, StorageFolder folder)
        {
            try
            {
                var fileNames = System.IO.Directory.GetFiles(_imageFolder.Path, fileNameNoExtension + "*");
                //var files = Directory.GetFileSystemEntries(_imageFolder.Path, fileNameNoExtension);
                if (fileNames?.Length > 0) return Path.GetFileName(fileNames[0]);
            }
            catch { }
            return null;
        }

        public async Task<IRandomAccessStreamReference> GetTileStreamRefAsync(int x, int y, int z, int zoom, CancellationToken cancToken)
        {
            if (cancToken.IsCancellationRequested) return null;
            var uri = await GetTileUriAsync(x, y, z, zoom, cancToken).ConfigureAwait(false);
            if (uri == null) return null;
            if (cancToken.IsCancellationRequested) return null;

            if (uri.Scheme == "file")
            {
                if (!uri.Segments.Any()) return null;
                return await PixelHelper.GetPixelStreamRefFromFile(_imageFolder, uri.Segments.Last(), cancToken).ConfigureAwait(false);
            }
            if (uri.Scheme == "ms-appx")
            {
                if (!uri.Segments.Any()) return null;
                var assetsFolder = await Windows.ApplicationModel.Package.Current.InstalledLocation.GetFolderAsync("Assets").AsTask().ConfigureAwait(false);
                return await PixelHelper.GetPixelStreamRefFromFile(assetsFolder, uri.Segments.Last(), cancToken).ConfigureAwait(false);
            }
            return null;
            /*
            // it's a proper web request: do it. However, this will never be required coz every call is now cached!
            return await Task.Run(async () =>
            {
                try
                {
                    var request = WebRequest.CreateHttp(uri);
                    request.Accept = MimeTypeImageAny;
                    request.AllowReadStreamBuffering = true;
                    request.ContinueTimeout = WebRequestTimeoutMsec;

                    cancToken.Register(delegate
                    {
                        try
                        {
                            request?.Abort();
                            Debug.WriteLine("web request aborted");
                        }
                        catch
                        {
                            Debug.WriteLine("web request aborted with error");
                        }
                    }, false);

                    using (var response = await request.GetResponseAsync().ConfigureAwait(false))
                    {
                        if (cancToken.IsCancellationRequested) return null;
                        if (IsWebResponseHeaderOk(response))
                        {
                            using (var responseStream = response.GetResponseStream()) // note that I cannot read the length of this stream, nor change its position
                            {                                
                                // this works, too, but it is slower, unintuitively:
                                //var pixels = await PixelHelper.GetPixelArrayFromRandomAccessStream(responseStream.AsRandomAccessStream()).ConfigureAwait(false);
                                //if (cancToken.IsCancellationRequested) return null;
                                //return await PixelHelper.GetStreamRefFromArray(pixels).ConfigureAwait(false);                                

                                var img = new byte[response.ContentLength];
                                await responseStream.ReadAsync(img, 0, (int)response.ContentLength, cancToken).ConfigureAwait(false);

                                if (cancToken.IsCancellationRequested) return null;
                                if (IsWebResponseContentOk(img))
                                {
                                    return await PixelHelper.GetPixelStreamRefFromByteArray(img).ConfigureAwait(false);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.ToString());
                    //Debug.WriteLine(uri.AbsoluteUri);
                }

                return null;
            }).ConfigureAwait(false);
*/
        }

        public async Task<Uri> GetTileUriAsync(int x, int y, int z, int zoom, CancellationToken cancToken)
        {
            // I must return null if I haven't got the tile yet, otherwise the caller will stop searching and present an empty tile forever
            if (cancToken.IsCancellationRequested) return null;

            // out of range? get out, no more thoughts. The MapControl won't request the uri if the zoom is outside its bounds, so it won't get here.
            // To force it here, I always set the widest possible bounds, which is OK coz the map control does not limit the zoom to its tile source bounds.
            //if (zoom < GetMinZoom() || zoom > GetMaxZoom()) return null;
            if (zoom < GetMinZoom()) return _mustZoomInUri;
            else if (zoom > GetMaxZoom()) return _mustZoomOutUri;

            // get the filename that uniquely identifies TileSource, X, Y, Z and Zoom
            string fileNameNoExtension = GetFileNameNoExtension(x, y, z, zoom);
            // not working on this set of data? Mark it as busy, closing the gate for other threads
            // already working on this set of data? Don't duplicate web requests or file accesses or any extra work and return null

            if (!await ProcessingQueue.TryAddToQueueAsync(fileNameNoExtension).ConfigureAwait(false)) return null; // return GetUriForFile(fileName); NO!
            // from now on, any returns must happen after removing the current fileName from the processing queue, to reopen the gate!

            try
            {
                if (cancToken.IsCancellationRequested) return null;
                // try to get this tile from the cache
                //var tileCacheRecordFromDb = await TileCacheRecord.GetTileCacheRecordFromDbAsync(_tileSource, x, y, z, zoom).ConfigureAwait(false);
                var fileNameWithExtension = GetFileNameFromFileCache(fileNameNoExtension, _imageFolder); // this is 6x faster than using the DB, with few records and with thousands
                if (cancToken.IsCancellationRequested) return null;
                // tile is not in cache
                //if (tileCacheRecordFromDb == null)
                if (fileNameWithExtension == null)
                {
                    if (!RuntimeData.GetInstance().IsConnectionAvailable) return null;

                    string sWebUri = GetWebUri(x, y, z, zoom);
                    //Debug.WriteLine("IsCaching = " + _isCaching);
                    // tile not in cache and caching on: download the tile, save it and return an uri pointing at it (ie at its file) 
                    //if (_isCaching)
                    //{
                    fileNameWithExtension = await TrySaveTile2Async(sWebUri, x, y, z, zoom, fileNameNoExtension, cancToken).ConfigureAwait(false);
                    if (fileNameWithExtension != null) return GetUriForFile(fileNameWithExtension);
                    //}
                    //// tile not in cache and cache off: return the web uri of the tile
                    //else
                    //{
                    //result = new Uri(sWebUri);
                    //}
                }
                // tile is in cache: return an uri pointing at it (ie at its file)
                else
                {
                    //result = GetUriForFile(tileCacheRecordFromDb.FileName);
                    return GetUriForFile(fileNameWithExtension);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.WriteLine("ERROR in GetTileUri(): " + ex.Message + ex.StackTrace);
            }
            finally
            {
                //await ProcessingQueue.RemoveFromQueueAsync(fileNameNoExtension).ConfigureAwait(false);
                Task remove = ProcessingQueue.RemoveFromQueueAsync(fileNameNoExtension);
            }
            return null;
        }

        public async Task<bool> TrySaveTileAsync(int x, int y, int z, int zoom, CancellationToken cancToken)
        {
            if (cancToken.IsCancellationRequested) return false;

            // get the filename that uniquely identifies TileSource, X, Y, Z and Zoom
            var fileNameNoExtension = GetFileNameNoExtension(x, y, z, zoom);
            // not working on this set of data? Mark it as busy, closing the gate for other threads.
            // already working on this set of data? Don't duplicate web requests of file accesses or any extra work and return false.
            // if I am not caching and another TileCache is working on the same tile at the same time, tough: this tile won't be downloaded.
            if (!await ProcessingQueue.TryAddToQueueAsync(fileNameNoExtension).ConfigureAwait(false)) return false;
            // from now on, any returns must happen after removing the current fileName from the processing queue, to reopen the gate!
            bool result = false;

            try
            {
                if (cancToken.IsCancellationRequested) return false;
                // try to get this tile from the cache
                //var tileCacheRecordFromDb = await TileCacheRecord.GetTileCacheRecordFromDbAsync(_tileSource, x, y, z, zoom).ConfigureAwait(false);
                var fileNameWithExtension = GetFileNameFromFileCache(fileNameNoExtension, _imageFolder); // this is 6x faster than using the DB
                if (cancToken.IsCancellationRequested) return false;

                // tile is not in cache
                // if (tileCacheRecordFromDb == null)
                if (fileNameWithExtension == null)
                {
                    // tile is not in cache: download it and save it
                    if (RuntimeData.GetInstance().IsConnectionAvailable)
                    {
                        string sWebUri = GetWebUri(x, y, z, zoom);
                        result = await (TrySaveTile2Async(sWebUri, x, y, z, zoom, fileNameNoExtension, cancToken)).ConfigureAwait(false) != null;
                    }
                }
                // tile is in cache: return ok
                else
                {
                    result = true;
                }
            }
            catch (OperationCanceledException) { result = false; }
            catch (Exception ex)
            {
                Debug.WriteLine("ERROR in SaveTileAsync(): " + ex.Message + ex.StackTrace);
            }
            finally
            {
                //await ProcessingQueue.RemoveFromQueueAsync(fileNameNoExtension).ConfigureAwait(false);
                Task remove = ProcessingQueue.RemoveFromQueueAsync(fileNameNoExtension);
            }
            return result;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sWebUri"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        /// <param name="zoom"></param>
        /// <param name="fileNameNoExtension"></param>
        /// <param name="cancToken"></param>
        /// <returns>file name with extension</returns>
        private async Task<string> TrySaveTile2Async(string sWebUri, int x, int y, int z, int zoom, string fileNameNoExtension, CancellationToken cancToken)
        {
            if (cancToken.IsCancellationRequested) return null;
            int where = 0;

            try
            {
                var request = WebRequest.CreateHttp(sWebUri);
                _tileSource.ApplyHeadersToWebRequest(request);
                request.AllowReadStreamBuffering = true;
                request.ContinueTimeout = WebRequestTimeoutMsec;

                where = 2;

                cancToken.Register(delegate
                {
                    try
                    {
                        request?.Abort();
                        Debug.WriteLine("web request aborted");
                    }
                    catch
                    {
                        Debug.WriteLine("web request aborted with error");
                    }
                }, false);

                using (var response = await request.GetResponseAsync().ConfigureAwait(false))
                {
                    if (cancToken.IsCancellationRequested) return null;

                    if ((response as System.Net.HttpWebResponse).StatusCode == HttpStatusCode.OK && response.ContentLength > 0)
                    {
                        where = 3;
                        using (var responseStream = response.GetResponseStream()) // note that I cannot read the length of this stream, nor change its position
                        {
                            where = 4;
                            // read response stream into a new record. 
                            // This extra step is the price to pay if we want to check the stream content
                            var fileNameWithExtension = GetFileNameWithExtension(fileNameNoExtension, response);
                            var imgBytes = new byte[response.ContentLength];
                            var newRecord = new TileCacheRecord(x, y, z, zoom);
                            if (cancToken.IsCancellationRequested) return null;
                            if (!string.IsNullOrWhiteSpace(fileNameWithExtension))
                            {
                                where = 5;
                                await responseStream.ReadAsync(imgBytes, 0, (int)response.ContentLength, cancToken).ConfigureAwait(false);

                                if (cancToken.IsCancellationRequested) return null;
                                if (IsWebResponseContentOk(imgBytes))
                                {
                                    where = 6;
                                    // If I am here, the file does not exist yet. You never know tho, so we use CreationCollisionOption.ReplaceExisting just in case.
                                    var newFile = await _imageFolder.CreateFileAsync(fileNameWithExtension, CreationCollisionOption.ReplaceExisting).AsTask(cancToken).ConfigureAwait(false);
                                    using (var writeStream = await newFile.OpenStreamForWriteAsync().ConfigureAwait(false))
                                    {
                                        where = 7;
                                        writeStream.Seek(0, SeekOrigin.Begin); // we don't need it but it does not hurt
                                        await writeStream.WriteAsync(imgBytes, 0, imgBytes.Length).ConfigureAwait(false); // I cannot use readStream.CopyToAsync() coz, after reading readStream, its cursor has advanced and we cannot turn it back
                                        where = 8;
                                        writeStream.Flush();
                                        // check file vs stream
                                        var fileSize = await newFile.GetFileSizeAsync().ConfigureAwait(false);
                                        //var fileProps = await newFile.GetBasicPropertiesAsync().AsTask().ConfigureAwait(false);
                                        //var fileSize = fileProps.Size;
                                        where = 9;
                                        if ((long)fileSize == writeStream.Length && writeStream.Length > 0)
                                        {
                                            where = 10;
                                            //bool isInserted = await DBManager.TryInsertOrIgnoreIntoTileCacheAsync(newRecord).ConfigureAwait(false);
                                            //if (isInserted)
                                            //{
                                            return fileNameWithExtension;
                                            //	where = 11;
                                            //}
                                        }
                                        //}
                                    }
                                }
                            }
                        }
                    }
                }
                Debug.WriteLine("TrySaveTileAsync() could not save; it made it to where = " + where);
            }
            catch (OperationCanceledException) { return null; }
            catch (Exception ex)
            {
                if (!ex.Message.Contains("404"))
                {
                    Debug.WriteLine("ERROR in TrySaveTileAsync(): " + ex.Message + ex.StackTrace + Environment.NewLine + " I made it to where = " + where);
                }
            }
            return null;
        }
        private static bool IsWebResponseContentOk(byte[] img)
        {
            int howManyBytesToCheck = 100;
            if (img.Length > howManyBytesToCheck)
            {
                try
                {
                    for (int i = img.Length - 1; i > img.Length - howManyBytesToCheck; i--)
                    {
                        if (img[i] != 0)
                        {
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
                    Debug.WriteLine(ex.ToString());
                    return false;
                }
            }
            //isStreamOk = newRecord.Img.FirstOrDefault(a => a != 0) != null; // this may take too long, so we only check the last 100 bytes
            return false;
        }

        private static string GetFileNameWithExtension(string fileNameNoExtension, WebResponse response)
        {
            if (response == null || response.ContentType == null) return null;
            var mimeType = response.ContentType;
            if (mimeType.Contains(MimeTypeImagePrefix))
            {
                return Path.ChangeExtension(fileNameNoExtension, mimeType.Replace(MimeTypeImagePrefix, ""));
            }
            else if (!string.IsNullOrWhiteSpace(response.ResponseUri?.AbsolutePath))
            {
                var extension = Path.GetExtension(response.ResponseUri.AbsolutePath);
                return Path.ChangeExtension(fileNameNoExtension, extension);
            }
            return null;
        }

        #endregion  services
    }
    // LOLLO TODO MAYBE before and after clearing, say how much disk space you saved
    /// <summary>
    /// Cache clearer and cache reader writer cannot be the same thing because they have different purposes and properties. The former is a singleton.
    /// </summary>
    public sealed class TileCacheClearer : OpenableObservableData
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

        private static readonly SemaphoreSlimSafeRelease _tileCacheClearerSemaphore = new SemaphoreSlimSafeRelease(1, 1);
        private static readonly object _instanceLocker = new object();
        private static TileCacheClearer _instance = null;
        #endregion properties


        #region events
        public static event PropertyChangedEventHandler IsClearingScheduledChanged;
        public static event EventHandler<CacheClearedEventArgs> CacheCleared;
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
        #endregion events


        #region ctor
        public static TileCacheClearer GetInstance()
        {
            lock (_instanceLocker)
            {
                return _instance ?? (_instance = new TileCacheClearer());
            }
        }

        private TileCacheClearer() { }
        #endregion ctor


        #region lifecycle
        protected override async Task OpenMayOverrideAsync(object args = null)
        {
            // resume clearing cache if it was interrupted
            var cacheClearingProps = await GetIsClearingCacheProps().ConfigureAwait(false);
            if (cacheClearingProps != null) // we don't want to hog anything, we schedule it for later.
            {
                await TryScheduleClearCache2Async(cacheClearingProps.TileSource, cacheClearingProps.IsAlsoRemoveSources, false).ConfigureAwait(false);
            }
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
        #endregion core


        #region utils
        public Task<bool> TryScheduleClearCacheAsync(TileSourceRecord tileSource, bool isAlsoRemoveSources)
        {
            return RunFunctionIfOpenAsyncTB(async delegate
            {
                var tileSourceClone = await PersistentData.GetInstance().GetTileSourceClone(tileSource).ConfigureAwait(false);
                return await TryScheduleClearCache2Async(tileSourceClone, isAlsoRemoveSources, true).ConfigureAwait(false);
            });
        }

        /// <summary>
        /// This method must run inside the semaphore
        /// </summary>
        /// <returns></returns>
        private async Task<bool> TryScheduleClearCache2Async(TileSourceRecord tileSource, bool isAlsoRemoveSources, bool writeAwayTheProps)
        {
            if (tileSource == null || tileSource.IsNone || tileSource.IsDefault) return false;

            if (writeAwayTheProps) await SetIsClearingCacheProps(tileSource, isAlsoRemoveSources).ConfigureAwait(false);
            IsClearingScheduled = await ProcessingQueue.TryScheduleTaskAsync(() => ClearCacheAsync(tileSource, isAlsoRemoveSources), CancToken).ConfigureAwait(false);
            return IsClearingScheduled;
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

    /// <summary>
    /// TileCacheRecord like in the db
    /// </summary>
    public sealed class TileCacheRecord
    {
        public int X { get { return _x; } }
        public int Y { get { return _y; } }
        public int Z { get { return _z; } }
        public int Zoom { get { return _zoom; } }

        private readonly int _x = 0;
        private readonly int _y = 0;
        private readonly int _z = 0;
        private readonly int _zoom = 2;

        public TileCacheRecord(int x, int y, int z, int zoom)
        {
            _x = x;
            _y = y;
            _z = z;
            _zoom = zoom;
        }
        /*
		internal static Task<TileCacheRecord> GetTileCacheRecordFromDbAsync(TileSourceRecord tileSource, int x, int y, int z, int zoom)
		{
			try
			{
				return DBManager.GetTileRecordAsync(tileSource, x, y, z, zoom);
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}
			return null;
		}
		*/
    }
    internal static class PixelHelper
    {
        private static readonly BitmapTransform _bitmapTransform = new BitmapTransform() { InterpolationMode = BitmapInterpolationMode.Linear };
        internal static async Task<RandomAccessStreamReference> GetPixelStreamRefFromFile(StorageFolder imageFolder, string fileName, CancellationToken cancToken)
        {
            try
            {
                if (cancToken.IsCancellationRequested) return null;
                byte[] pixels = null;
                using (var readStream = await imageFolder.OpenStreamForReadAsync(fileName).ConfigureAwait(false))
                {
                    if (cancToken.IsCancellationRequested) return null;
                    //pixels = await GetPixelArrayFromByteStream(readStream.AsRandomAccessStream()).ConfigureAwait(false);
                    pixels = await GetPixelArrayFromRandomAccessStream(readStream.AsRandomAccessStream()).ConfigureAwait(false);
                    if (cancToken.IsCancellationRequested) return null;
                }
                return await GetStreamRefFromArray(pixels).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
                return null;
            }
        }
        /*
        internal static async Task<RandomAccessStreamReference> GetPixelStreamRefFromByteArray(byte[] imgBytes)
        {
            try
            {
                byte[] pixels = await GetPixelArrayFromByteArray(imgBytes).ConfigureAwait(false);
                return await GetStreamRefFromArray(pixels).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
                return null;
            }
        }
        */
        /*
        private static async Task<byte[]> GetPixelArrayFromByteArray(byte[] bytes)
        {
            try
            {
                using (InMemoryRandomAccessStream imraStream = new InMemoryRandomAccessStream())
                {
                    using (IOutputStream imraOutputStream = imraStream.GetOutputStreamAt(0)) // this seems to make it a little more stable
                    {
                        using (DataWriter StreamWriter = new DataWriter(imraOutputStream))
                        {
                            StreamWriter.WriteBytes(bytes);
                            await StreamWriter.StoreAsync().AsTask().ConfigureAwait(false);
                            await StreamWriter.FlushAsync().AsTask().ConfigureAwait(false);
                            StreamWriter.DetachStream(); // otherwise Dispose() will murder the stream
                        }

                        return await GetPixelArrayFromRandomAccessStream(imraStream).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
                return null;
            }
        }
        */
        private static async Task<byte[]> GetPixelArrayFromRandomAccessStream(IRandomAccessStream source)
        {
#if DEBUG
            var sw = new Stopwatch(); sw.Start();
#endif
            try
            {
                var decoder = await BitmapDecoder.CreateAsync(source).AsTask().ConfigureAwait(false);
                //var decoder = await BitmapDecoder.CreateAsync(BitmapDecoder.PngDecoderId, source).AsTask().ConfigureAwait(false);
                //var decoder = await BitmapDecoder.CreateAsync(jpegDecoder.CodecId, dbStream).AsTask().ConfigureAwait(false);
                // LOLLO TODO the image can easily be 250K when the source only takes 10K. We need some compression! I am trying PNG decoder right now.
                // I can also try with the settings below - it actually seems not! I think the freaking output is always 262144 bytes coz it's really all the pixels.

                var pixelProvider = await decoder.GetPixelDataAsync(
                    BitmapPixelFormat.Rgba8,
                    //BitmapAlphaMode.Straight,
                    BitmapAlphaMode.Ignore, // faster
                    _bitmapTransform,
                    ExifOrientationMode.RespectExifOrientation,
                //ColorManagementMode.ColorManageToSRgb).AsTask().ConfigureAwait(false);
                ColorManagementMode.DoNotColorManage).AsTask().ConfigureAwait(false);

                return pixelProvider.DetachPixelData();
            }
            catch (Exception ex)
            {
                Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
                return null;
            }
#if DEBUG
            finally
            {
                sw.Stop();
                Debug.WriteLine("GetPixelArrayFromRandomAccessStream has taken " + sw.ElapsedTicks + " ticks");
            }
#endif
        }
        private static async Task<RandomAccessStreamReference> GetStreamRefFromArray(byte[] array)
        {
            if (array == null || array.Length == 0) return null;

            // write pixels into a stream and return a reference to it
            // no Dispose() in the following!
            InMemoryRandomAccessStream inMemoryRandomAccessStream = new InMemoryRandomAccessStream();
            using (IOutputStream outputStream = inMemoryRandomAccessStream.GetOutputStreamAt(0)) // this seems to make it a little more stable
            {
                using (DataWriter writer = new DataWriter(outputStream))
                {
                    writer.WriteBytes(array);
                    await writer.StoreAsync().AsTask().ConfigureAwait(false);
                    await writer.FlushAsync().AsTask().ConfigureAwait(false);
                    writer.DetachStream(); // otherwise Dispose() will murder the stream
                }
                return RandomAccessStreamReference.CreateFromStream(inMemoryRandomAccessStream);
            }
        }
        /*
        public static async Task<IRandomAccessStreamReference> GetRedTileStreamRefAsync()
        { // this is sample code from MS
            int pixelHeight = 256;
            int pixelWidth = 256;
            int bpp = 4;

            byte[] bytes = new byte[pixelHeight * pixelWidth * bpp];

            for (int yy = 0; yy < pixelHeight; yy++)
            {
                for (int xx = 0; xx < pixelWidth; xx++)
                {
                    int pixelIndex = yy * pixelWidth + xx;
                    int byteIndex = pixelIndex * bpp;

                    // Set the current pixel bytes.
                    bytes[byteIndex] = 0xff;        // Red
                    bytes[byteIndex + 1] = 0x00;    // Green
                    bytes[byteIndex + 2] = 0x00;    // Blue
                    bytes[byteIndex + 3] = 0x80;    // Alpha (0xff = fully opaque)
                }
            }

            // Create RandomAccessStream from byte array.
            InMemoryRandomAccessStream randomAccessStream = new InMemoryRandomAccessStream();
            IOutputStream outputStream = randomAccessStream.GetOutputStreamAt(0);
            DataWriter writer = new DataWriter(outputStream);
            writer.WriteBytes(bytes);
            await writer.StoreAsync();
            await writer.FlushAsync();
            return RandomAccessStreamReference.CreateFromStream(randomAccessStream);
        }
        */
    }
}