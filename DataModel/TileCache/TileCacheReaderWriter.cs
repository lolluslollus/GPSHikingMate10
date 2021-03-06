﻿using LolloGPS.Data.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Utilz;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Controls.Maps;

namespace LolloGPS.Data.TileCache
{
    // public enum TileSources { Nokia, OpenStreetMap, Swisstopo, Wanderreitkarte, OrdnanceSurvey, ForUMaps, OpenSeaMap, UTTopoLight, ArcGIS }

    public sealed class TileCacheReaderWriter
    {
        public const string MimeTypeImagePrefix = "image/";
        public const string MimeTypeImageAny = "image/*";
        public static readonly string[] AllowedExtensions = { "bmp", "jpeg", "jpg", "png" };
        public static readonly string[] AllowedExtensionsTolerant = AllowedExtensions.Concat(AllowedExtensions.Select(ext => $".{ext}")).ToArray();
        public const int MaxRecords = 65535;
        public const int WebRequestTimeoutMsec = 2048; //65535;
        private static readonly Uri _tileMustZoomInUri = new Uri("ms-appx:///Assets/TileMustZoomIn-256.png", UriKind.Absolute);
        private static readonly Uri _tileMustZoomOutUri = new Uri("ms-appx:///Assets/TileMustZoomOut-256.png", UriKind.Absolute);
        private static readonly Uri _tileEmptyUri = new Uri("ms-appx:///Assets/TileEmpty-256.png", UriKind.Absolute);

        private readonly Uri _mustZoomInTileUri;
        private readonly Uri _mustZoomOutTileUri;
        private readonly TileSourceRecord _tileSource = TileSourceRecord.GetDefaultTileSource(); //TileSources.Nokia;
        private readonly StorageFolder _tileCacheFolder = null;
        private readonly StorageFolder _tileSourceFolder = null;
        private readonly StorageFolder _assetsFolder = null;
        private readonly MapTileDataSource _mapTileDataSource = null;
        public MapTileDataSource MapTileDataSource { get { return _mapTileDataSource; } }

        private int _webUriFormatIndex = 0;
        private readonly IReadOnlyList<string> _sourceUriFormats;
        private const string _tileFileFormat = "{3}_{0}_{1}_{2}";

        //private readonly object _isCachingLocker = new object();
        //private volatile bool _isCaching = false;
        /// <summary>
        /// Gets if this cache writes away (ie caches) the data it picks up.
        /// Only relevant for supplying map tiles on the fly.
        /// We could read this from PersistentData whenever we need it, but it does not work well.
        /// </summary>
        //public bool IsCaching { get { lock (_isCachingLocker) { return _isCaching; } } set { lock (_isCachingLocker) { _isCaching = value; } } }
        //public bool IsCaching { get { return _isCaching; } set { _isCaching = value; } }

        // this is only for testing
        private readonly bool _isReturnLocalUris = false;
        public bool IsReturnLocalUris { get { return _isReturnLocalUris; } }

        #region lifecycle
        /// <summary>
        /// Make sure you supply a thread-safe tile source, ie a clone, to preserve atomicity
        /// </summary>
        /// <exception cref="ArgumentException"/>
        /// <exception cref="OperationCanceledException"/>
        public TileCacheReaderWriter(TileSourceRecord tileSource, bool isCaching, bool isReturnLocalUris, MapTileDataSource mapTileDataSource, CancellationToken cancToken)
        {
            _tileSource = tileSource ?? throw new ArgumentNullException("TileCache ctor was given tileSource == null");
            if (cancToken.IsCancellationRequested) throw new OperationCanceledException(cancToken);
            //_isCaching = isCaching;
            _isReturnLocalUris = isReturnLocalUris;
            _assetsFolder = Windows.ApplicationModel.Package.Current.InstalledLocation.GetFolderAsync("Assets").AsTask(cancToken).Result;

            if (cancToken.IsCancellationRequested) throw new OperationCanceledException(cancToken);
            if (GetIsFileSource())
            {
                if (string.IsNullOrWhiteSpace(tileSource.TileSourceFileName)) throw new ArgumentNullException("TileCache ctor was given no file names for file tile source");
                var fileUriFormats = new List<string>();
                try
                {
                    string fileUriFormat = tileSource.TileSourceFileName.Replace(TileSourceRecord.ZoomLevelPlaceholder, TileSourceRecord.ZoomLevelPlaceholder_Internal);
                    fileUriFormat = fileUriFormat.Replace(TileSourceRecord.XPlaceholder, TileSourceRecord.XPlaceholder_Internal);
                    fileUriFormat = fileUriFormat.Replace(TileSourceRecord.YPlaceholder, TileSourceRecord.YPlaceholder_Internal);
                    fileUriFormats.Add(fileUriFormat);
                }
                catch (Exception exc)
                {
                    Logger.Add_TPL(exc.ToString(), Logger.ForegroundLogFilename);
                    throw new ArgumentNullException("TileCache ctor was given an invalid TileSourceFileName");
                }
                _sourceUriFormats = fileUriFormats.AsReadOnly(); // this list will always have 1 item only

                try
                {
                    _tileSourceFolder = Pickers.GetPreviouslyPickedFolderAsync(tileSource.TileSourceFolderPath, cancToken).Result;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception exc)
                {
                    Logger.Add_TPL(exc.ToString(), Logger.ForegroundLogFilename);
                    throw new ArgumentNullException("TileCache ctor was given an invalid directory for file tile source");
                }
            }
            else
            {
                var webUriFormats = new List<string>();
                foreach (var uriString in _tileSource.UriStrings)
                {
                    try
                    {
                        string webUriFormat = uriString.Replace(TileSourceRecord.ZoomLevelPlaceholder, TileSourceRecord.ZoomLevelPlaceholder_Internal);
                        webUriFormat = webUriFormat.Replace(TileSourceRecord.XPlaceholder, TileSourceRecord.XPlaceholder_Internal);
                        webUriFormat = webUriFormat.Replace(TileSourceRecord.YPlaceholder, TileSourceRecord.YPlaceholder_Internal);
                        webUriFormats.Add(webUriFormat);
                    }
                    catch (Exception exc)
                    {
                        Logger.Add_TPL(exc.ToString(), Logger.ForegroundLogFilename);
                    }
                }
                _sourceUriFormats = webUriFormats.AsReadOnly();

                var tileCacheFolder = ApplicationData.Current.LocalCacheFolder.CreateFolderAsync(ConstantData.TILE_SOURCES_DIR_NAME, CreationCollisionOption.OpenIfExists).AsTask(cancToken).Result;
                _tileCacheFolder = tileCacheFolder.CreateFolderAsync(_tileSource.FolderName, CreationCollisionOption.OpenIfExists).AsTask(cancToken).Result;
            }

            if (cancToken.IsCancellationRequested) throw new OperationCanceledException(cancToken);
            _mapTileDataSource = mapTileDataSource;

            _mustZoomInTileUri = _tileSource.IsOverlay ? _tileEmptyUri : _tileMustZoomInUri;
            _mustZoomOutTileUri = _tileSource.IsOverlay ? _tileEmptyUri : _tileMustZoomOutUri;
        }
        #endregion lifecycle

        #region getters
        private Uri GetUriForLocalTile(string fileName)
        {
            // LOLLO TODO check this method, it never seems to satisfy the local and the http tile source as of June 2017.
            // The old MapControl was more tolerant.
            // Fortunately, we still have the custom tile source, which works fine.
            if (_isReturnLocalUris)
            {
                // return new Uri("ms-appx:///Assets/aim-120.png", UriKind.Absolute); // this works

                var address = $"ms-appdata:///localcache/TileSources/{_tileCacheFolder.Name}/{fileName}"; // this fails
                var localUri = new Uri(address, UriKind.Absolute);
                return localUri;
            }

            // should work when requesting any uri, but it fails after the MapControl was updated in 10.0.15063.
            // this is why I create the bitmaps... when those imbeciles fix it, we can go back to returning uris like before.
            var filePath = Path.Combine(_tileCacheFolder.Path, fileName);
            var uri = new Uri(filePath, UriKind.Absolute);
            return uri;
        }
        /// <summary>
        /// Similar to GetWebUriString4Tile, which you could also use, 
        /// but this is faster and does not do any coordinate conversions.
        /// </summary>
        private string GetFileNameWithExtensionFromFileSource(int x, int y, int z, int zoom)
        {
            try
            {
                return string.Format(_sourceUriFormats[0], zoom, x, y);
            }
            catch (Exception exc)
            {
                Debug.WriteLine("Exception in TileCacheReaderWriter.GetFileUriString(): " + exc.Message + exc.StackTrace);
                return string.Empty;
            }
        }
        private string GetFileNameWithExtensionFromCache(string fileNameNoExtension)
        {
            try
            {
                var fileNames = System.IO.Directory.GetFiles(_tileCacheFolder.Path, fileNameNoExtension + "*");
                //var files = Directory.GetFileSystemEntries(_imageFolder.Path, fileNameNoExtension);
                if (fileNames?.Length > 0) return Path.GetFileName(fileNames[0]);
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Gets the web uri to retrieve a tile. You can convert the coordinates here, in future.
        /// </summary>
        private string GetWebUriString4Tile(int x, int y, int z, int zoom)
        {
            try
            {
                // this is not critical, so we don't lock, but mind the multithreading
                var newIndex = _webUriFormatIndex;
                if (newIndex >= _sourceUriFormats.Count - 1) newIndex = 0;
                else newIndex++;
                _webUriFormatIndex = newIndex;
                //if (_tileSource.TechName.Equals("Schweizmobil"))
                //{
                //    int zoomHalf = zoom / 2;
                //    double zoomDouble = zoom;
                //    double sqrt2 = Math.Sqrt(2.0);
                //    //int newZoom = Convert.ToInt32(zoomDouble / 2.0 + 9.0);
                //    int newZoom = zoom + zoom - 2; // zoomHalf + 9;
                //    // the northmost point seems to be at 48.3 N
                //    // the southernmost at 45.4 N
                //    // at level 8, there is one row and one column.
                //    // at newZoom = 9 to 11, there are 1 row and 2 columns.
                //    // at newZoom = 12, there are 2 rows and 2 columns.
                //    // at newZoom = 13 to 14, there are 2 rows and 3 columns.
                //    // at newZoom = 15, there are 3 rows and 4 columns.
                //    // at 16, there are 5 rows and 8 columns. The look changes.
                //    // -> Between 16 and 17 there is a jump: the coordinates increase more than double.
                //    // at 17, there are 13 rows and 19 columns.
                //    // at 18, 23 rows and 35 columns. There are in fact 25 rows, the bottom two are white. This happens often.
                //    // -> Between 18 at 19 there is a funny jump: the coordinates don't double, they go up a bit more!!
                //    // at 19, 59 rows and 84 columns. There are in fact 63 rows, the bottom 4 are white.
                //    // at 20, 113 rows and 133 columns.
                //    // at 21, 207 rows and 266 columns.
                //    // at 22, 432 rows and 286 columns.
                //    // -> Between 22 and 23 there is another jump: the coordinates increase only about 27%.
                //    // at 23, 516 rows and 458 columns.
                //    // at 24, 719 rows and 476 columns.
                //    // at 25, https://wmts105.geo.admin.ch/1.0.0/ch.swisstopo.landeskarte-farbe-10/default/current/21781/25/996/681.png takes over. 
                //    // It doubles regularly.
                //    // 27 is the top level.
                //    // All this applies to both swisstopo maps.

                //    int newX = x - Convert.ToInt32(Math.Pow(2.0, Math.Sqrt(zoomDouble)));
                //    int newY = y - Convert.ToInt32(Math.Pow(2.0, Math.Sqrt(zoomDouble)));
                //    // LOLLO TODO make conversion work
                //    //swisstopo.geodesy.gpsref.ApproxSwissProj.WGS84toLV03(lat, lon, 6378137.0, ref xx, ref yy, ref zz);
                //    return string.Format(_sourceUriFormats[newIndex], newZoom, newX, newY);
                //}
                return string.Format(_sourceUriFormats[newIndex], zoom, x, y);
            }
            catch (Exception exc)
            {
                Debug.WriteLine("Exception in TileCacheReaderWriter.GetWebUriString(): " + exc.Message + exc.StackTrace);
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
        private static bool IsWebResponseContentOk(byte[] img)
        {
            int howManyBytesToCheck = 100;
            if (img.Length <= howManyBytesToCheck) return false;

            try
            {
                for (int i = img.Length - 1; i > img.Length - howManyBytesToCheck; i--)
                {
                    if (img[i] != 0)
                    {
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                // Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
                Debug.WriteLine(ex.ToString());
                return false;
            }
            //isStreamOk = newRecord.Img.FirstOrDefault(a => a != 0) != null; // this may take too long, so we only check the last 100 bytes
        }

        private static string GetExtension(string fileNameNoExtension, WebResponse response)
        {
            if (response == null || response.ContentType == null) return null;

            string extension = null;
            if (!string.IsNullOrWhiteSpace(response?.ContentType))
            {
                var contentTypeSegments = response.ContentType.Split(';');
                foreach (var segment in contentTypeSegments)
                {
                    if (segment.Contains(MimeTypeImagePrefix))
                    {
                        extension = segment.Trim().Replace(MimeTypeImagePrefix, string.Empty);
                        break;
                    }
                }
            }
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = Path.GetExtension(response.ResponseUri.AbsolutePath);
            }

            if (!AllowedExtensionsTolerant.Any(ext => ext == extension)) extension = null;
            return extension;
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
        public bool GetIsFileSource()
        {
            return _tileSource.IsFileSource == true;
        }
        #endregion getters

        #region services
        public async Task<IRandomAccessStreamReference> GetTileStreamRefAsync(int x, int y, int z, int zoom, CancellationToken cancToken)
        {
            if (cancToken.IsCancellationRequested) return null;
            if (GetIsFileSource())
            {
                if (zoom < GetMinZoom()) return await PixelHelper.GetPixelStreamRefFromFile(_assetsFolder, _mustZoomInTileUri.Segments.Last(), cancToken).ConfigureAwait(false);
                if (zoom > GetMaxZoom()) return await PixelHelper.GetPixelStreamRefFromFile(_assetsFolder, _mustZoomOutTileUri.Segments.Last(), cancToken).ConfigureAwait(false);
                return await PixelHelper.GetPixelStreamRefFromFile(_tileSourceFolder, GetFileNameWithExtensionFromFileSource(x, y, z, zoom), cancToken).ConfigureAwait(false);
            }
            else
            {
                var uri = await GetTileUriAsync(x, y, z, zoom, cancToken).ConfigureAwait(false);
                if (uri == null) return null;
                if (cancToken.IsCancellationRequested) return null;

                if (uri.Scheme == "file")
                {
                    if (!uri.Segments.Any()) return null;
                    return await PixelHelper.GetPixelStreamRefFromFile(_tileCacheFolder, uri.Segments.Last(), cancToken).ConfigureAwait(false);
                }
                if (uri.Scheme == "ms-appx")
                {
                    if (!uri.Segments.Any()) return null;
                    return await PixelHelper.GetPixelStreamRefFromFile(_assetsFolder, uri.Segments.Last(), cancToken).ConfigureAwait(false);
                }
                return null;
            }
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
            if (zoom < GetMinZoom()) return _mustZoomInTileUri;
            if (zoom > GetMaxZoom()) return _mustZoomOutTileUri;

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
                var fileNameWithExtension = GetFileNameWithExtensionFromCache(fileNameNoExtension); // this is 6x faster than using the DB, with few records and with thousands
                if (cancToken.IsCancellationRequested) return null;
                // tile is not in cache
                if (fileNameWithExtension == null)
                {
                    if (!RuntimeData.GetInstance().IsConnectionAvailable) return null;

                    fileNameWithExtension = await TrySaveTile2Async(x, y, z, zoom, fileNameNoExtension, cancToken).ConfigureAwait(false);
                    if (cancToken.IsCancellationRequested) return null;
                    // if (fileNameWithExtension == null) return null; 
                    // LOLLO TODO experiment:
                    // zero tolerance when navigating online. If you really want you map, you download it.
                    if (fileNameWithExtension == null) return _tileEmptyUri;
                    return GetUriForLocalTile(fileNameWithExtension);
                }
                // tile is in cache: return an uri pointing at it (ie at its file)
                else
                {
                    return GetUriForLocalTile(fileNameWithExtension);
                }
            }
            catch (OperationCanceledException) { return null; }
            catch (Exception ex)
            {
                Debug.WriteLine("ERROR in GetTileUri(): " + ex.Message + ex.StackTrace);
                return null;
            }
            finally
            {
                //await ProcessingQueue.RemoveFromQueueAsync(fileNameNoExtension).ConfigureAwait(false);
                Task remove = ProcessingQueue.RemoveFromQueueAsync(fileNameNoExtension);
            }
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
                var fileNameWithExtension = GetFileNameWithExtensionFromCache(fileNameNoExtension); // this is 6x faster than using the DB
                if (cancToken.IsCancellationRequested) return false;

                // tile is not in cache
                // if (tileCacheRecordFromDb == null)
                if (fileNameWithExtension == null)
                {
                    // tile is not in cache: download it and save it
                    if (RuntimeData.GetInstance().IsConnectionAvailable)
                    {
                        result = await (TrySaveTile2Async(x, y, z, zoom, fileNameNoExtension, cancToken)).ConfigureAwait(false) != null;
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
        private async Task<string> TrySaveTile2Async(int x, int y, int z, int zoom, string fileNameNoExtension, CancellationToken cancToken)
        {
            if (cancToken.IsCancellationRequested) return null;
            int where = 0;

            try
            {
                string sWebUri = GetWebUriString4Tile(x, y, z, zoom);
                var request = WebRequest.CreateHttp(sWebUri);
                _tileSource.ApplyHeadersToWebRequest(request);
                request.AllowReadStreamBuffering = true;
                request.ContinueTimeout = WebRequestTimeoutMsec;
                //request.CookieContainer = new CookieContainer();
                //request.UseDefaultCredentials = true;

                where = 2;

                using (var cancTokenRegistration = cancToken.Register(delegate
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
                }, false))
                {
                    using (var response = await request.GetResponseAsync().ConfigureAwait(false))
                    {
                        if (cancToken.IsCancellationRequested) return null;
                        if ((response as HttpWebResponse)?.StatusCode != HttpStatusCode.OK || response.ContentLength <= 0) return null;
                        where = 3;
                        using (var responseStream = response.GetResponseStream()) // note that I cannot read the length of this stream, nor change its position
                        {
                            where = 4;
                            // read response stream into a new record. 
                            // This extra step is the price to pay if we want to check the stream content
                            var fileNameWithExtension = Path.ChangeExtension(fileNameNoExtension, GetExtension(fileNameNoExtension, response));
                            if (string.IsNullOrWhiteSpace(fileNameWithExtension)) return null;
                            if (cancToken.IsCancellationRequested) return null;
                            var imgBytes = new byte[response.ContentLength];
                            //var newRecord = new TileCacheRecord(x, y, z, zoom);
                            where = 5;
                            await responseStream.ReadAsync(imgBytes, 0, (int)response.ContentLength, cancToken).ConfigureAwait(false);
                            if (cancToken.IsCancellationRequested) return null;
                            if (!IsWebResponseContentOk(imgBytes)) return null;
                            where = 6;
                            // If I am here, the file does not exist yet. You never know tho, so we use CreationCollisionOption.ReplaceExisting just in case.
                            var newFile = await _tileCacheFolder.CreateFileAsync(fileNameWithExtension, CreationCollisionOption.ReplaceExisting).AsTask(cancToken).ConfigureAwait(false);
                            using (var writeStream = await newFile.OpenStreamForWriteAsync().ConfigureAwait(false))
                            {
                                where = 7;
                                writeStream.Seek(0, SeekOrigin.Begin); // we don't need it but it does not hurt
                                await writeStream.WriteAsync(imgBytes, 0, imgBytes.Length).ConfigureAwait(false); // I cannot use readStream.CopyToAsync() coz, after reading readStream, its cursor has advanced and we cannot turn it back
                                where = 8;
                                writeStream.Flush();
                                if (writeStream.Length <= 0) return null;
                                where = 9;
                                // check file vs stream
                                var fileSize = await newFile.GetFileSizeAsync().ConfigureAwait(false);
                                where = 10;
                                if ((long)fileSize != writeStream.Length) return null;
                                where = 11;
                                return fileNameWithExtension;
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException) { return null; } // (ex.Response as System.Net.HttpWebResponse).StatusDescription
            catch (WebException ex) { return null; }
            catch (Exception ex)
            {
                Debug.WriteLine("ERROR in TrySaveTile2Async(): " + ex.Message + ex.StackTrace + Environment.NewLine + " I made it to where = " + where);
            }
#if DEBUG
            finally
            {
                Debug.WriteLine("TrySaveTile2Async() made it to where = " + where);
            }
#endif
            return null;
        }
        #endregion  services
    }
}