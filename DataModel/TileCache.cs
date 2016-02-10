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
using Windows.Storage;
using Windows.Storage.Streams;

namespace LolloGPS.Data.TileCache
{
	// public enum TileSources { Nokia, OpenStreetMap, Swisstopo, Wanderreitkarte, OrdnanceSurvey, ForUMaps, OpenSeaMap, UTTopoLight, ArcGIS }

	public sealed class TileCacheReaderWriter
	{
		public const string MimeTypeImageAny = "image/*"; // "image/png"
														  //public const string ImageToCheck = "image";
		public const int MaxRecords = 65535;
		public const int WebRequestTimeoutMsec = 65535;

		private readonly TileSourceRecord _tileSource = TileSourceRecord.GetDefaultTileSource(); //TileSources.Nokia;
																								 /// <summary>
																								 /// The tile source will give its name to the file folder
																								 /// </summary>
		public TileSourceRecord TileSource { get { return _tileSource; } }

		private readonly StorageFolder _imageFolder = null;

		private readonly object _isCachingLocker = new object();
		private bool _isCaching = false;
		/// <summary>
		/// Gets if this cache writes away (ie caches) the data it picks up.
		/// Only relevant for supplying map tiles on the fly.
		/// We could read this from PersistentData whenever we need it, but it does not work well.
		/// </summary>
		public bool IsCaching { get { lock (_isCachingLocker) { return _isCaching; } } set { lock (_isCachingLocker) { _isCaching = value; } } }

		private readonly string _webUriFormat = string.Empty;
		private const string _tileFileFormat = "{3}_{0}_{1}_{2}";

		private readonly TileCacheProcessingQueue _queue = null;

		#region construct and dispose
		/// <summary>
		/// Make sure you supply a thread-safe tile source, ie a clone, to preserve atomicity
		/// </summary>
		/// <param name="tileSource"></param>
		/// <param name="isCaching"></param>
		public TileCacheReaderWriter(TileSourceRecord tileSource, bool isCaching)
		{
			if (tileSource == null) throw new ArgumentNullException("TileCache ctor was given tileSource == null");

			_queue = TileCacheProcessingQueue.GetInstance();

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

			_isCaching = isCaching;
			_imageFolder = ApplicationData.Current.LocalFolder.CreateFolderAsync(_tileSource.TechName, CreationCollisionOption.OpenIfExists).AsTask().Result;
		}
		#endregion construct and dispose

		#region getters
		private Uri GetUriForFile(string fileName)
		{
			return new UriBuilder(Path.Combine(_imageFolder.Path, fileName)).Uri;
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
		private string GetFileNameFromKey(int x, int y, int z, int zoom)
		{
			return string.Format(_tileFileFormat, zoom, x, y, _tileSource.TechName);
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

		public async Task<Uri> GetTileUri(int x, int y, int z, int zoom, CancellationToken cancToken)
		{
			if (cancToken == null || cancToken.IsCancellationRequested) return null;

			// out of range? get out, no more thoughts. The MapControl won't request the uri if the zoom is outside its bounds, so it won't get here.
			// To force it here, I always set the widest possible bounds, which is OK coz the map control does not limit the zoom to its tile source bounds.
			//if (zoom < GetMinZoom() || zoom > GetMaxZoom()) return null;
			if (zoom < GetMinZoom()) return _mustZoomInUri;
			else if (zoom > GetMaxZoom()) return _mustZoomOutUri;

			// get the filename that uniquely identifies TileSource, X, Y, Z and Zoom
			string fileName = GetFileNameFromKey(x, y, z, zoom);
			// not working on this set of data? Mark it as busy, closing the gate for other threads
			// already working on this set of data? Don't duplicate web requests or file accesses or any extra work and return null

			// I must return null if I haven't got the tile yet, otherwise the caller will stop searching and present an empty tile forever
			if (!await _queue.TryAddToQueueAsync(fileName).ConfigureAwait(false)) return null; // return GetUriForFile(fileName); NO!

			// from now on, any returns must happen after removing the current fileName from the processing queue, to reopen the gate!
			Uri result = null;

			try
			{
				string sWebUri = GetWebUri(x, y, z, zoom);
				// try to get this tile from the cache
				var tileCacheRecordFromDb = await TileCacheRecord.GetTileCacheRecordFromDbAsync(_tileSource, x, y, z, zoom).ConfigureAwait(false);
				//Debug.WriteLine("GetTileUri() calculated" + Environment.NewLine
				//    + "_imageFolder = " + (_imageFolder == null ? "null" : _imageFolder.Path) + Environment.NewLine
				//    + "fileName = " + fileName + Environment.NewLine
				//    + "fileNameFromDb = " + fileNameFromDb + Environment.NewLine
				//    + "sWebUri = " + sWebUri + Environment.NewLine);

				// tile is not in cache
				if (tileCacheRecordFromDb == null)
				{
					if (RuntimeData.GetInstance().IsConnectionAvailable)
					{
						//Debug.WriteLine("IsCaching = " + _isCaching);
						// tile not in cache and caching on: download the tile, save it and return an uri pointing at it (ie at its file) 
						if (_isCaching) // this is cheaper than checking IsCaching, which has a lock. It works and it is not critical anyway.
						{
							if (await (TrySaveTile2Async(sWebUri, x, y, z, zoom, fileName, cancToken)).ConfigureAwait(false)) result = GetUriForFile(fileName);
						}
						// tile not in cache and cache off: return the web uri of the tile
						else
						{
							result = new Uri(sWebUri);
						}
					}
				}
				// tile is in cache: return an uri pointing at it (ie at its file)
				else
				{
					result = GetUriForFile(fileName);
					if (fileName != tileCacheRecordFromDb.FileName)
					{
						await UpdateFileNameAsync(tileCacheRecordFromDb, fileName, _tileSource.TechName, x, y, z, zoom).ConfigureAwait(false);
					}
				}
			}
			catch (OperationCanceledException) { }
			catch (Exception ex)
			{
				Debug.WriteLine("ERROR in GetTileUri(): " + ex.Message + ex.StackTrace);
			}
			finally
			{
				await _queue.RemoveFromQueueAsync(fileName).ConfigureAwait(false);
			}

			return result;
		}

		public async Task<bool> TrySaveTileAsync(int x, int y, int z, int zoom, CancellationToken cancToken)
		{
			if (cancToken == null || cancToken.IsCancellationRequested) return false;

			// get the filename that uniquely identifies TileSource, X, Y, Z and Zoom
			string fileName = GetFileNameFromKey(x, y, z, zoom);
			// not working on this set of data? Mark it as busy, closing the gate for other threads.
			// already working on this set of data? Don't duplicate web requests of file accesses or any extra work and return false.
			// if I am not caching and another TileCache is working on the same tile at the same time, tough: this tile won't be downloaded.
			if (!await _queue.TryAddToQueueAsync(fileName).ConfigureAwait(false)) return false;
			// from now on, any returns must happen after removing the current fileName from the processing queue, to reopen the gate!
			bool result = false;

			try
			{
				string sWebUri = GetWebUri(x, y, z, zoom);
				// try to get this tile from the cache
				var tileCacheRecordFromDb = await TileCacheRecord.GetTileCacheRecordFromDbAsync(_tileSource, x, y, z, zoom).ConfigureAwait(false);

				// tile is not in cache
				if (tileCacheRecordFromDb == null)
				{
					// tile is not in cache: download it and save it
					if (RuntimeData.GetInstance().IsConnectionAvailable)
					{
						result = await (TrySaveTile2Async(sWebUri, x, y, z, zoom, fileName, cancToken)).ConfigureAwait(false);
					}
				}
				// tile is in cache: return ok
				else
				{
					result = true;
					if (fileName != tileCacheRecordFromDb.FileName)
					{
						await UpdateFileNameAsync(tileCacheRecordFromDb, fileName, _tileSource.TechName, x, y, z, zoom).ConfigureAwait(false);
					}
				}
			}
			catch (OperationCanceledException) { result = false; }
			catch (Exception ex)
			{
				Debug.WriteLine("ERROR in SaveTileAsync(): " + ex.Message + ex.StackTrace);
			}
			finally
			{
				await _queue.RemoveFromQueueAsync(fileName).ConfigureAwait(false);
			}
			return result;
		}

		private async Task<bool> TrySaveTile2Async(string sWebUri, int x, int y, int z, int zoom, string fileName, CancellationToken cancToken)
		{
			if (cancToken == null || cancToken.IsCancellationRequested) return false;
			bool result = false;
			int where = 0;

			try
			{
				var request = WebRequest.CreateHttp(sWebUri); // request.Accept = MimeTypeImageAny;
				request.AllowReadStreamBuffering = true;
				request.ContinueTimeout = WebRequestTimeoutMsec;

				where = 2;

				//var tf = new TaskFactory(_queue.CancellationToken);

				//request.GetResponseAsync().Wait(_queue.CancellationToken);

				// LOLLO TODO we need a way to abort requests when cancelling, see if this is right. It does work.
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
					if (cancToken == null || cancToken.IsCancellationRequested) return false;
					if (IsWebResponseHeaderOk(response))
					{
						where = 3;
						using (var responseStream = response.GetResponseStream()) // note that I cannot read the length of this stream, nor change its position
						{
							where = 4;
							// read response stream into a new record. 
							// This extra step is the price to pay if we want to check the stream content
							var newRecord = new TileCacheRecord(_tileSource.TechName, x, y, z, zoom) { FileName = fileName, Img = new byte[response.ContentLength] };
							await responseStream.ReadAsync(newRecord.Img, 0, (int)response.ContentLength).ConfigureAwait(false);

							if (cancToken == null || cancToken.IsCancellationRequested) return false;
							if (IsWebResponseContentOk(newRecord))
							{
								// If I am here, the file does not exist. You never know tho, so we use CreationCollisionOption.ReplaceExisting just in case.
								var newFile = await _imageFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting).AsTask().ConfigureAwait(false);
								using (var writeStream = await newFile.OpenStreamForWriteAsync().ConfigureAwait(false))
								{
									//Debug.WriteLine("GetTileUri() found: " + Environment.NewLine
									//    + "writeStream.Length = " + writeStream.Length + Environment.NewLine
									//    + "response.ContentLength = " + response.ContentLength + Environment.NewLine
									//    + "Uri4File will be = " + GetUriForFile(fileName) + Environment.NewLine);

									//if (writeStream.Length > 0) // file already exists, it should never happen. 
									// This only makes sense with CreationCollisionOption.OpenIfExists in the CreateFileAsync() above.
									//{
									//	result = true;
									//	where = 99;
									//	Logger.Add_TPL("GetTileUri() avoided overwriting a file with name = " + fileName + " and returned its uri = " + GetUriForFile(fileName), Logger.ForegroundLogFilename, Logger.Severity.Info, false);
									//}
									//else
									//{
									where = 7;
									writeStream.Seek(0, SeekOrigin.Begin); // we don't need it but it does not hurt
									await writeStream.WriteAsync(newRecord.Img, 0, newRecord.Img.Length).ConfigureAwait(false); // I cannot use readStream.CopyToAsync() coz, after reading readStream, its cursor has advanced and we cannot turn it back
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
										bool isInserted = await DBManager.TryInsertOrIgnoreIntoTileCacheAsync(newRecord).ConfigureAwait(false);
										if (isInserted)
										{
											result = true;
											where = 11;
										}
									}
									//}
								}
							}
						}
					}
				}
				if (!result) Debug.WriteLine("TrySaveTileAsync() could not save; it made it to where = " + where);
			}
			catch (OperationCanceledException) { return false; }
			catch (Exception ex)
			{
				Debug.WriteLine("ERROR in TrySaveTileAsync(): " + ex.Message + ex.StackTrace + Environment.NewLine + " I made it to where = " + where);
			}
			return result;
		}

		private async Task<bool> UpdateFileNameAsync(TileCacheRecord oldTileCacheRecord, string newFileName, string techName, int x, int y, int z, int zoom)
		{
			Debug.WriteLine("ERROR in GetTileStreamRef() or GetTileUri(): file name in db is " + oldTileCacheRecord + " but the calculated file name is " + newFileName);
			bool output = false;
			if (oldTileCacheRecord == null) return false;
			if (oldTileCacheRecord.FileName != newFileName && _imageFolder != null)
			{
				try
				{
					var file = await _imageFolder.GetFileAsync(oldTileCacheRecord.FileName).AsTask().ConfigureAwait(false);
					if (file != null)
					{
						// same as when you clear: update the file first, then the db, it the file update went well.
						await file.RenameAsync(newFileName).AsTask().ConfigureAwait(false);
						oldTileCacheRecord.FileName = newFileName;
						output = await DBManager.UpdateTileCacheRecordAsync(oldTileCacheRecord);
					}
				}
				catch (Exception ex)
				{
					Debug.WriteLine("Exception in UpdateFileNameAsync(): " + ex.Message + ex.StackTrace);
				}
			}
			if (output) Debug.WriteLine("The error was fixed");
			else Debug.WriteLine("The error was not fixed");
			return output;
		}

		private static bool IsWebResponseContentOk(TileCacheRecord newRecord)
		{
			int howManyBytesToCheck = 100;
			if (newRecord.Img.Length > howManyBytesToCheck)
			{
				try
				{
					for (int i = newRecord.Img.Length - 1; i > newRecord.Img.Length - howManyBytesToCheck; i--)
					{
						if (newRecord.Img[i] != 0)
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

		private static bool IsWebResponseHeaderOk(WebResponse response)
		{
			return response.ContentLength > 0; //  && response.ContentType.Contains(ImageToCheck);
											   // swisstopo answers with a binary/octet-stream
		}
		#endregion  services
	}

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

		private readonly TileCacheProcessingQueue _queue = null;

		private static readonly SemaphoreSlimSafeRelease _clearPropsSemaphore = new SemaphoreSlimSafeRelease(1, 1);
		private static readonly object _instanceLocker = new object();
		private static TileCacheClearer _instance = null;
		#endregion properties


		#region events
		public static event PropertyChangedEventHandler IsClearingScheduledChanged;
		public static event EventHandler<CacheClearedEventArgs> CacheCleared;
		public sealed class CacheClearedEventArgs : EventArgs
		{
			private TileSourceRecord _tileSource = null;
			public TileSourceRecord TileSource { get { return _tileSource; } }
			private bool _isAlsoRemoveSources = false;
			public bool IsAlsoRemoveSources { get { return _isAlsoRemoveSources; } }
			private bool _isCacheCleared = false;
			public bool IsCacheCleared { get { return _isCacheCleared; } }
			private int _howManyRecordsDeleted = 0;
			public int HowManyRecordsDeleted { get { return _howManyRecordsDeleted; } }

			public CacheClearedEventArgs(TileSourceRecord tileSource, bool isAlsoRemoveSources, bool isCacheCleared, int howManyRecordsDeleted)
			{
				_tileSource = tileSource;
				_isAlsoRemoveSources = isAlsoRemoveSources;
				_isCacheCleared = isCacheCleared;
				_howManyRecordsDeleted = howManyRecordsDeleted;
			}
		}
		#endregion events


		#region ctor
		public static TileCacheClearer GetInstance()
		{
			lock (_instanceLocker)
			{
				if (_instance == null) _instance = new TileCacheClearer();
				return _instance;
			}
		}

		private TileCacheClearer()
		{
			_queue = TileCacheProcessingQueue.GetInstance();
		}
		#endregion ctor


		#region lifecycle
		protected override async Task OpenMayOverrideAsync()
		{
			// open the queue so you can use it
			await _queue.OpenAsync().ConfigureAwait(false);
			// resume clearing cache if it was interrupted
			var cacheClearingProps = await GetIsClearingCacheProps().ConfigureAwait(false);
			if (cacheClearingProps != null) // we don't want to hog anything, we schedule it for later.
			{
				await TryScheduleClearCache2Async(cacheClearingProps.TileSource, cacheClearingProps.IsAlsoRemoveSources, false).ConfigureAwait(false);
				//_runAsSoonAsOpen = delegate
				//{
				//	return
				//		TryScheduleClearCacheAsync(
				//		cacheClearingProps.TileSource,
				//		cacheClearingProps.IsAlsoRemoveSources,
				//		false);
				//};
			}
		}
		#endregion lifecycle


		#region core
		private async Task ClearCacheAsync(TileSourceRecord tileSource, bool isAlsoRemoveSources)
		{
			Debug.WriteLine("ClearCacheAsync() started");

			var tryCancResult = await PersistentData.GetInstance().TryClearCacheAsync(tileSource, isAlsoRemoveSources, CancToken).ConfigureAwait(false);
			if (tryCancResult.Item1 == PersistentData.ClearCacheResult.Error)
			{
				await SetIsClearingCacheProps(null, false).ConfigureAwait(false);
				IsClearingScheduled = false;
				CacheCleared?.Invoke(null, new CacheClearedEventArgs(tileSource, isAlsoRemoveSources, false, tryCancResult.Item2));
				Debug.WriteLine("ClearCacheAsync() ended with error");
			}
			else if (tryCancResult.Item1 == PersistentData.ClearCacheResult.OK)
			{
				await SetIsClearingCacheProps(null, false).ConfigureAwait(false);
				IsClearingScheduled = false;
				CacheCleared?.Invoke(null, new CacheClearedEventArgs(tileSource, isAlsoRemoveSources, true, tryCancResult.Item2));
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
			if (tileSource != null && !tileSource.IsNone && !tileSource.IsDefault)
			{
				if (writeAwayTheProps) await SetIsClearingCacheProps(tileSource, isAlsoRemoveSources).ConfigureAwait(false);
				IsClearingScheduled = await _queue.TryScheduleTaskAsync(delegate
				{
					return ClearCacheAsync(tileSource, isAlsoRemoveSources);
				}).ConfigureAwait(false);
				return IsClearingScheduled;
			}
			return false;
		}
		private static async Task SetIsClearingCacheProps(TileSourceRecord tileSource, bool isAlsoRemoveSources)
		{
			try
			{
				await _clearPropsSemaphore.WaitAsync().ConfigureAwait(false);

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
				SemaphoreSlimSafeRelease.TryRelease(_clearPropsSemaphore);
			}
		}
		private static async Task<CacheClearedEventArgs> GetIsClearingCacheProps()
		{
			try
			{
				await _clearPropsSemaphore.WaitAsync().ConfigureAwait(false);

				string isAlsoRemoveSourcesString = RegistryAccess.GetValue(ConstantData.REG_CLEARING_CACHE_IS_REMOVE_SOURCES);
				string tileSourceString = RegistryAccess.GetValue(ConstantData.REG_CLEARING_CACHE_TILE_SOURCE);
				if (string.IsNullOrWhiteSpace(tileSourceString)) return null;

				var tileSource = await RegistryAccess.GetObject<TileSourceRecord>(ConstantData.REG_CLEARING_CACHE_TILE_SOURCE).ConfigureAwait(false);
				return new CacheClearedEventArgs(tileSource, isAlsoRemoveSourcesString.Equals(true.ToString()), false, 0);
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(_clearPropsSemaphore);
			}
		}
		#endregion utils
	}
	/// <summary>
	/// As soon as a file (ie a unique combination of TileSource, X, Y, Z and Zoom) is in process, this class stores it.
	/// As soon as no files are in process, this class can run a delegate, if it was scheduled.
	/// </summary>
	public sealed class TileCacheProcessingQueue : OpenableObservableData
	{
		#region properties
		public CancellationToken CancellationToken { get { return CancToken; } }

		private List<string> _fileNames_InProcess = new List<string>();
		private Func<Task> _funcAsSoonAsFree = null;

		private static readonly object _instanceLocker = new object();
		private static TileCacheProcessingQueue _instance = null;
		public static TileCacheProcessingQueue GetInstance()
		{
			lock (_instanceLocker)
			{
				if (_instance == null) _instance = new TileCacheProcessingQueue();
				return _instance;
			}
		}

		private TileCacheProcessingQueue() { }
		#endregion properties


		#region lifecycle
		protected override Task CloseMayOverrideAsync()
		{
			_funcAsSoonAsFree = null;
			_fileNames_InProcess.Clear();
			return Task.CompletedTask;
		}
		#endregion lifecycle


		#region services
		/// <summary>
		/// Not working on this set of data? Mark it as busy, closing the gate for other threads.
		/// Already working on this set of data? Say so.
		/// </summary>
		/// <param name="fileName"></param>
		/// <returns></returns>
		internal Task<bool> TryAddToQueueAsync(string fileName)
		{
			return RunFunctionIfOpenAsyncB(delegate
			{
				if (!string.IsNullOrWhiteSpace(fileName) && !_fileNames_InProcess.Contains(fileName))
				{
					_fileNames_InProcess.Add(fileName);
					return true;
				}
				return false;
			});
		}
		/// <summary>
		/// Not working on this set of data anymore? Mark it as free, opening the gate for other threads.
		/// </summary>
		/// <param name="fileName"></param>
		/// <returns></returns>
		internal Task RemoveFromQueueAsync(string fileName)
		{
			return RunFunctionIfOpenAsyncT(async delegate
			{
				if (!string.IsNullOrWhiteSpace(fileName))
				{
					_fileNames_InProcess.Remove(fileName);
					await TryRunFuncAsSoonAsFree().ConfigureAwait(false);
				}
			});
		}
		/// <summary>
		/// Schedules a delegate to be run as soon as no data is being processed.
		/// If it can run it now, it will wait until the method has exited.
		/// </summary>
		/// <param name="func"></param>
		/// <returns></returns>
		internal Task<bool> TryScheduleTaskAsync(Func<Task> func)
		{
			return RunFunctionIfOpenAsyncB(delegate
			{
				if (_funcAsSoonAsFree == null)
				{
					_funcAsSoonAsFree = func;

					Task runFunc = Task.Run(async delegate // use separate thread to avoid deadlock
					{
						// the following will run after the current method is over because it queues before the semaphore.
						await RunFunctionIfOpenAsyncT(delegate
						{
							return TryRunFuncAsSoonAsFree(); // will run now if the cache is free, otherwise later
						}).ConfigureAwait(false);
					});

					return true;
				}
				return false;
			});
		}

		/// <summary>
		/// This method must be run inside the semaphore
		/// </summary>
		/// <returns></returns>
		private async Task<bool> TryRunFuncAsSoonAsFree()
		{
			if (_fileNames_InProcess.Count == 0 && _funcAsSoonAsFree != null)
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
		public string TileSourceTechName { get { return _tileSourceTechName; } set { _tileSourceTechName = value; } }
		public int X { get { return _x; } set { _x = value; } }
		public int Y { get { return _y; } set { _y = value; } }
		public int Z { get { return _z; } set { _z = value; } }
		public int Zoom { get { return _zoom; } set { _zoom = value; } }
		public string FileName { get { return _fileName; } set { _fileName = value; } }
		public byte[] Img { get { return _img; } set { _img = value; } } // this field has a setter, so SQLite may use it

		private string _tileSourceTechName = string.Empty; // = TileSources.Nokia;
		private int _x = 0;
		private int _y = 0;
		private int _z = 0;
		private int _zoom = 2;
		private string _fileName = "";
		private byte[] _img = null;

		public TileCacheRecord() { } // for the db query
		public TileCacheRecord(string tileSourceTechName, int x, int y, int z, int zoom)
		{
			_tileSourceTechName = tileSourceTechName;
			_x = x;
			_y = y;
			_z = z;
			_zoom = zoom;
		}

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
		internal static async Task<RandomAccessStreamReference> GetPixelStreamRefFromImgFolder(StorageFolder imageFolder, string fileName)
		{
			try
			{
				byte[] pixels = null;
				using (var readStream = await imageFolder.OpenStreamForReadAsync(fileName).ConfigureAwait(false))
				{
					pixels = await GetPixelArrayFromByteStream(readStream.AsRandomAccessStream()).ConfigureAwait(false);
				}
				return await GetPixelStreamRefFromPixelArray(pixels).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
				return null;
			}
		}
		internal async static Task<RandomAccessStreamReference> GetPixelStreamRefFromByteArray(byte[] imgBytes)
		{
			try
			{
				byte[] pixels = await GetPixelArrayFromByteArray(imgBytes).ConfigureAwait(false);
				return await GetPixelStreamRefFromPixelArray(pixels).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
				return null;
			}
		}

		private async static Task<RandomAccessStreamReference> GetPixelStreamRefFromPixelArray(byte[] pixels)
		{
			if (pixels == null || pixels.Length == 0) return null;

			// write pixels into a stream and return a reference to it
			// no Dispose() in the following!
			InMemoryRandomAccessStream inMemoryRandomAccessStream = new InMemoryRandomAccessStream();
			using (IOutputStream outputStream = inMemoryRandomAccessStream.GetOutputStreamAt(0)) // this seems to make it a little more stable
			{
				using (DataWriter writer = new DataWriter(outputStream))
				{
					writer.WriteBytes(pixels);
					await writer.StoreAsync().AsTask().ConfigureAwait(false);
					await writer.FlushAsync().AsTask().ConfigureAwait(false);
					writer.DetachStream(); // otherwise Dispose() will murder the stream
				}
				return RandomAccessStreamReference.CreateFromStream(inMemoryRandomAccessStream);
			}
		}

		private static async Task<byte[]> GetPixelArrayFromByteArray(byte[] source)
		{
			try
			{
				byte[] pixels = null;
				using (InMemoryRandomAccessStream dbStream = new InMemoryRandomAccessStream())
				{
					using (IOutputStream dbOutputStream = dbStream.GetOutputStreamAt(0)) // this seems to make it a little more stable
					{
						using (DataWriter dbStreamWriter = new DataWriter(dbOutputStream))
						{
							dbStreamWriter.WriteBytes(source);
							await dbStreamWriter.StoreAsync().AsTask().ConfigureAwait(false);
							await dbStreamWriter.FlushAsync().AsTask().ConfigureAwait(false);
							dbStreamWriter.DetachStream(); // otherwise Dispose() will murder the stream
						}


						var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(dbStream).AsTask().ConfigureAwait(false);
						//var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(Windows.Graphics.Imaging.BitmapDecoder.PngDecoderId, dbStream).AsTask().ConfigureAwait(false);
						//var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(jpegDecoder.CodecId, dbStream).AsTask().ConfigureAwait(false);
						//LOLLO the image can easily be 250K when the source only takes 10K. We need some compression! I am trying PNG decoder right now.
						// I can also try with the settings below - it actually seems not! I think the freaking output is always 262144 bytes coz it's really all the pixels.

						var pixelProvider = await decoder.GetPixelDataAsync(
							Windows.Graphics.Imaging.BitmapPixelFormat.Rgba8,
							Windows.Graphics.Imaging.BitmapAlphaMode.Straight,
							new Windows.Graphics.Imaging.BitmapTransform(), // { ScaledHeight = 256, ScaledWidth = 256, InterpolationMode = Windows.Graphics.Imaging.BitmapInterpolationMode.NearestNeighbor }, // { InterpolationMode = ??? }
							Windows.Graphics.Imaging.ExifOrientationMode.RespectExifOrientation,
						//Windows.Graphics.Imaging.ColorManagementMode.ColorManageToSRgb).AsTask().ConfigureAwait(false);
						Windows.Graphics.Imaging.ColorManagementMode.DoNotColorManage).AsTask().ConfigureAwait(false);

						pixels = pixelProvider.DetachPixelData();
					}
				}
				return pixels;
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
				return null;
			}
		}
		private static async Task<byte[]> GetPixelArrayFromByteStream(IRandomAccessStream source)
		{
			try
			{
				byte[] pixels = null;
				var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(source).AsTask().ConfigureAwait(false);

				var pixelProvider = await decoder.GetPixelDataAsync(
					Windows.Graphics.Imaging.BitmapPixelFormat.Rgba8,
					Windows.Graphics.Imaging.BitmapAlphaMode.Straight,
					new Windows.Graphics.Imaging.BitmapTransform(), // { InterpolationMode = ??? }
					Windows.Graphics.Imaging.ExifOrientationMode.RespectExifOrientation,
					//Windows.Graphics.Imaging.ColorManagementMode.ColorManageToSRgb).AsTask().ConfigureAwait(false);
					Windows.Graphics.Imaging.ColorManagementMode.DoNotColorManage).AsTask().ConfigureAwait(false);

				pixels = pixelProvider.DetachPixelData();
				return pixels;
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
				return null;
			}
		}
	}
}