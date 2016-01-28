﻿using LolloGPS.Data.Runtime;
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
using Windows.Storage;
using Windows.Storage.Streams;

namespace LolloGPS.Data.TileCache
{
	// public enum TileSources { Nokia, OpenStreetMap, Swisstopo, Wanderreitkarte, OrdnanceSurvey, ForUMaps, OpenSeaMap, UTTopoLight, ArcGIS }

	public sealed class TileCache
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

		private StorageFolder _imageFolder = null;

		private volatile bool _isCaching = false;
		/// <summary>
		/// Gets if this cache writes away (ie caches) the data it picks up.
		/// Only relevant for supplying map tiles on the fly.
		/// We could read this from PersistentData whenever we need it, but it does not work well.
		/// </summary>
		public bool IsCaching { get { return _isCaching; } set { _isCaching = value; } }

		private readonly string _webUriFormat = string.Empty;
		private const string _tileFileFormat = "{3}_{0}_{1}_{2}";

		#region construct and dispose
		public TileCache(TileSourceRecord tileSource, bool isCaching)
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
		public static void ScheduleClear(TileSourceRecord tileSource, bool isAlsoRemoveSources)
		{
			Debug.WriteLine("About to call ProcessingQueue.ClearCacheIfQueueEmptyAsync");
			Task.Run(delegate { Task sch = TileCacheProcessingQueue.ScheduleClearCacheAsync(tileSource, isAlsoRemoveSources); });
			Debug.WriteLine("returned from ProcessingQueue.ClearCacheIfQueueEmptyAsync");
		}

		public async Task<Uri> GetTileUri(int x, int y, int z, int zoom)
		{
			// out of range? get out, no more thoughts
			if (zoom < GetMinZoom() || zoom > GetMaxZoom()) return null; // LOLLO TODO maybe return a tile with the message "wrong zoom"
																		 // get the filename that uniquely identifies TileSource, X, Y, Z and Zoom
			string fileName = GetFileNameFromKey(x, y, z, zoom);
			// not working on this set of data? Mark it as busy, closing the gate for other threads
			// already working on this set of data? Don't duplicate web requests of file accesses or any extra work and return null
			if (!await TileCacheProcessingQueue.TryAddToQueueAsync(fileName).ConfigureAwait(false)) return GetUriForFile(fileName); // was return null; // LOLLO TODO check this, it's new
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
						// tile not in cache and caching on: download the tile, save it and return an uri pointing at it (ie at its file) 
						if (IsCaching)
						{
							if (await (TrySaveTileAsync(sWebUri, x, y, z, zoom, fileName)).ConfigureAwait(false)) result = GetUriForFile(fileName);
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
			catch (Exception ex)
			{
				Debug.WriteLine("ERROR in GetTileUri(): " + ex.Message + ex.StackTrace);
			}

			await TileCacheProcessingQueue.RemoveFromQueueAsync(fileName).ConfigureAwait(false);
			return result;
		}

		public async Task<bool> SaveTileAsync(int x, int y, int z, int zoom) //int x, int y, int z, int zoom)
		{
			// get the filename that uniquely identifies TileSource, X, Y, Z and Zoom
			string fileName = GetFileNameFromKey(x, y, z, zoom);
			// not working on this set of data? Mark it as busy, closing the gate for other threads
			// already working on this set of data? Don't duplicate web requests of file accesses or any extra work and return null
			if (!await TileCacheProcessingQueue.TryAddToQueueAsync(fileName).ConfigureAwait(false)) return false;
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
						result = await (TrySaveTileAsync(sWebUri, x, y, z, zoom, fileName)).ConfigureAwait(false);
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
			catch (Exception ex)
			{
				Debug.WriteLine("ERROR in SaveTileAsync(): " + ex.Message + ex.StackTrace);
			}

			await TileCacheProcessingQueue.RemoveFromQueueAsync(fileName).ConfigureAwait(false);
			return result;
		}

		private async Task<bool> TrySaveTileAsync(string sWebUri, int x, int y, int z, int zoom, string fileName)
		{
			bool result = false;
			int where = 0;

			var request = WebRequest.CreateHttp(sWebUri); // request.Accept = MimeTypeImageAny;
			request.AllowReadStreamBuffering = true;
			request.ContinueTimeout = WebRequestTimeoutMsec;

			where = 2;
			using (var response = await request.GetResponseAsync().ConfigureAwait(false))
			{
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

						if (IsWebResponseContentOk(newRecord))
						{
							StorageFile newFile = await _imageFolder.CreateFileAsync(fileName, CreationCollisionOption.OpenIfExists).AsTask().ConfigureAwait(false);
							using (var writeStream = await newFile.OpenStreamForWriteAsync().ConfigureAwait(false))
							{
								//Debug.WriteLine("GetTileUri() found: " + Environment.NewLine
								//    + "writeStream.Length = " + writeStream.Length + Environment.NewLine
								//    + "response.ContentLength = " + response.ContentLength + Environment.NewLine
								//    + "Uri4File will be = " + GetUriForFile(fileName) + Environment.NewLine);
								if (writeStream.Length > 0) // file already exists, do not overwrite it - extra caution, it should never happen 
															// LOLLO TODO check this: it may be wiser to overwrite the file. Just replace the collision option.
								{
									where = 99;
									// output = GetUriForFile(fileName);
									result = true;
									Logger.Add_TPL("GetTileUri() avoided overwriting a file with name = " + fileName + " and returned its uri = " + GetUriForFile(fileName), Logger.ForegroundLogFilename, Logger.Severity.Info, false);
								}
								else
								{
									where = 7;
									await writeStream.WriteAsync(newRecord.Img, 0, newRecord.Img.Length).ConfigureAwait(false); // I cannot use readStream.CopyToAsync() coz, after reading readStream, its cursor has advanced and we cannot turn it back
									where = 8;
									writeStream.Flush();
									// check file vs stream
									var fileProps = await newFile.GetBasicPropertiesAsync().AsTask().ConfigureAwait(false);
									var fileSize = fileProps.Size;
									where = 9;
									if ((long)fileSize == writeStream.Length && writeStream.Length > 0)
									{
										where = 10;
										bool isInserted = await DBManager.TryInsertIntoTileCacheAsync(newRecord).ConfigureAwait(false);
										// output = GetUriForFile(fileName);
										result = true;
										if (isInserted) where = 11;
									}
								}
							}
						}
					}
				}
			}
			if (!result) Debug.WriteLine("TrySaveTileAsync() could not save; it made it to where = " + where);
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
						// like when you clear: update the file first, then the db, it the file update went well.
						await file.RenameAsync(newFileName).AsTask().ConfigureAwait(false);
						oldTileCacheRecord.FileName = newFileName;
						output = await DBManager.UpdateTileCacheRecordAsync(oldTileCacheRecord); // LOLLO TODO make sure this update updates: check the primary key
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
					Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
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

	/// <summary>
	/// As soon as a file (ie a unique combination of TileSource, X, Y, Z and Zoom) is in process, this class stores it.
	/// </summary>
	public static class TileCacheProcessingQueue
	{
		#region properties
		private static SemaphoreSlimSafeRelease _semaphore = null; // new SemaphoreSlimSafeRelease(1, 1);

		private static volatile bool _isFree = true;
		public static bool IsFree
		{
			get { return _isFree; }
			private set
			{
				if (_isFree != value)
				{
					_isFree = value;
					IsFreeChanged?.Invoke(null, new PropertyChangedEventArgs(nameof(IsFree)));
				}
			}
		}

		private static List<string> _fileNames_InProcess = new List<string>();
		private static List<Func<Task>> _funcsAsSoonAsFree = new List<Func<Task>>();
		private static volatile bool _isOpen = false;
		private static SmartCancellationTokenSource _cts = null;
		private static CancellationToken _cancToken;
		#endregion properties


		#region events
		public static event PropertyChangedEventHandler IsFreeChanged;
		public static event EventHandler<CacheClearedEventArgs> CacheCleared;

		public class CacheClearedEventArgs : EventArgs
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


		#region lifecycle
		public static async Task OpenAsync()
		{
			if (!_isOpen)
			{
				if (!SemaphoreSlimSafeRelease.IsAlive(_semaphore)) _semaphore = new SemaphoreSlimSafeRelease(1, 1);
				try
				{
					await _semaphore.WaitAsync().ConfigureAwait(false);
					if (!_isOpen)
					{
						_cts = new SmartCancellationTokenSource(); // LOLLO TODO test this new cts and token handling. 
						// This one in particular will make trouble on a phone with low memory when the user opens a file picker.
						_cancToken = _cts.Token;

						_isOpen = true;
					}
				}
				catch (Exception ex)
				{
					if (SemaphoreSlimSafeRelease.IsAlive(_semaphore)) Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
				}
				finally
				{
					SemaphoreSlimSafeRelease.TryRelease(_semaphore);
				}
			}
		}

		public static async Task CloseAsync()
		{
			if (_isOpen)
			{
				_cts?.CancelSafe(true);

				try
				{
					await _semaphore.WaitAsync().ConfigureAwait(false);
					_cts?.Dispose();
					_cts = null;

					if (_isOpen)
					{
						_funcsAsSoonAsFree.Clear();
						_fileNames_InProcess.Clear();
						_isOpen = false;
					}
				}
				catch (Exception ex)
				{
					if (SemaphoreSlimSafeRelease.IsAlive(_semaphore)) Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
				}
				finally
				{
					SemaphoreSlimSafeRelease.TryRelease(_semaphore);
					SemaphoreSlimSafeRelease.TryDispose(_semaphore);
					_semaphore = null;
				}
			}
		}
		#endregion lifecycle


		#region services
		/// <summary>
		/// Not working on this set of data? Mark it as busy, closing the gate for other threads.
		/// Already working on this set of data? Say so.
		/// </summary>
		/// <param name="fileName"></param>
		/// <returns></returns>
		internal static async Task<bool> TryAddToQueueAsync(string fileName)
		{
			if (_isOpen)
			{
				try
				{
					await _semaphore.WaitAsync().ConfigureAwait(false);
					if (_isOpen)
					{
						if (!string.IsNullOrWhiteSpace(fileName) && !_fileNames_InProcess.Contains(fileName))
						{
							_fileNames_InProcess.Add(fileName);
							IsFree = (_fileNames_InProcess.Count == 0);
							await TryRunFuncsAsSoonAsFree().ConfigureAwait(false);

							return true;
						}
					}
				}
				catch (Exception) { } // semaphore disposed
				finally
				{
					SemaphoreSlimSafeRelease.TryRelease(_semaphore);
				}
			}
			return false;
		}
		internal static async Task RemoveFromQueueAsync(string fileName)
		{
			if (_isOpen)
			{
				try
				{
					await _semaphore.WaitAsync().ConfigureAwait(false);
					if (_isOpen)
					{
						if (!string.IsNullOrWhiteSpace(fileName))
						{
							_fileNames_InProcess.Remove(fileName);
							IsFree = (_fileNames_InProcess.Count == 0);
							await TryRunFuncsAsSoonAsFree().ConfigureAwait(false);
						}
					}
				}
				catch (Exception) { } // semaphore disposed
				finally
				{
					SemaphoreSlimSafeRelease.TryRelease(_semaphore);
				}
			}
		}

		internal static async Task ScheduleClearCacheAsync(TileSourceRecord tileSource, bool isAlsoRemoveSources)
		{
			if (_isOpen)
			{
				try
				{
					await _semaphore.WaitAsync().ConfigureAwait(false);
					if (_isOpen)
					{
						if (tileSource != null)
						{
							_funcsAsSoonAsFree.Add(delegate { return ClearCacheAsync(tileSource, isAlsoRemoveSources, _cancToken); });
							if (IsFree)
							{
								await TryRunFuncsAsSoonAsFree().ConfigureAwait(false);
							}
						}
					}
				}
				catch (Exception) { } // semaphore disposed
				finally
				{
					SemaphoreSlimSafeRelease.TryRelease(_semaphore);
				}
			}
		}

		/// <summary>
		/// This method must be run inside the semaphore
		/// </summary>
		/// <returns></returns>
		private static async Task<bool> TryRunFuncsAsSoonAsFree()
		{
			if (IsFree && _funcsAsSoonAsFree.Count > 0)
			{
				IsFree = false;

				foreach (var func in _funcsAsSoonAsFree)
				{
					await func().ConfigureAwait(false);
				}
				_funcsAsSoonAsFree.Clear();

				IsFree = true;

				return true;
			}
			return false;
		}

		private static async Task ClearCacheAsync(TileSourceRecord tileSource, bool isAlsoRemoveSources, CancellationToken token)
		{
			Debug.WriteLine("ClearCacheAsync() started");

			if (token == null || token.IsCancellationRequested) return;

			if (tileSource == null)
			{
				CacheCleared?.Invoke(null, new CacheClearedEventArgs(tileSource, isAlsoRemoveSources, false, 0));
				return;
			}

			int howManyRecordsDeletedTotal = 0;

			//// test begin
			//await GetAllFilesInLocalFolder().ConfigureAwait(false);
			//// test end

			string[] folderNamesToBeDeleted = GetFolderNamesToBeDeleted(tileSource);
			if (folderNamesToBeDeleted != null && folderNamesToBeDeleted.Length > 0)
			{
				StorageFolder localFolder = ApplicationData.Current.LocalFolder;

				foreach (var folderName in folderNamesToBeDeleted.Where(fn => !string.IsNullOrWhiteSpace(fn)))
				{
					try
					{
						if (token == null || token.IsCancellationRequested) return;

						/*	Delete db entries first.
						 *  It's not terrible if some files are not deleted and the db thinks they are:
							they will be downloaded again, and not resaved (with the current logic).
						 *  It's terrible if files are deleted and the db thinks they are still there,
							because they will never be downloaded again, and the tiles will be forever empty.
						 */
						var dbResult = await DBManager.DeleteTileCacheAsync(folderName).ConfigureAwait(false);

						if (token == null || token.IsCancellationRequested) return; // LOLLO TODO test this cts and what exceptions it may raise

						if (dbResult.Item1)
						{
							// delete the files next.
							var imageFolder = await localFolder.GetFolderAsync(folderName).AsTask().ConfigureAwait(false);
							await imageFolder.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().ConfigureAwait(false);
							howManyRecordsDeletedTotal += dbResult.Item2;
						}
						else
						{
							CacheCleared?.Invoke(null, new CacheClearedEventArgs(tileSource, isAlsoRemoveSources, false, howManyRecordsDeletedTotal));
							return;
						}
					}
					catch (FileNotFoundException)
					{
						Debug.WriteLine("FileNotFound in ClearCacheAsync()");
					}
					catch (OperationCanceledException)
					{
						Debug.WriteLine("OperationCanceledException in ClearCacheAsync()");
					}
					catch (ObjectDisposedException)
					{
						Debug.WriteLine("ObjectDisposedException in ClearCacheAsync()");
					}
					catch (Exception ex)
					{
						Logger.Add_TPL("ERROR in ClearCacheAsync: " + ex.Message + ex.StackTrace, Logger.PersistentDataLogFilename);
					}
				}
			}
			CacheCleared?.Invoke(null, new CacheClearedEventArgs(tileSource, isAlsoRemoveSources, true, howManyRecordsDeletedTotal));
			Debug.WriteLine("ClearCacheAsync() ended");
		}

		private static string[] GetFolderNamesToBeDeleted(TileSourceRecord tileSource)
		{
			string[] folderNamesToBeDeleted = null;
			PersistentData _persistentData = PersistentData.GetInstance();
			if (!tileSource.IsAll && !tileSource.IsNone)
			{
				folderNamesToBeDeleted = new string[1] { tileSource.TechName };
			}
			else if (tileSource.IsAll)
			{
				folderNamesToBeDeleted = new string[_persistentData.TileSourcez.Count - 1];
				int cnt = 0;
				foreach (var item in _persistentData.TileSourcez)
				{
					if (!item.IsDefault)
					{
						folderNamesToBeDeleted[cnt] = item.TechName;
						cnt++;
					}
				}
			}
			return folderNamesToBeDeleted;
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

		//internal async static Task<string> GetFilenameFromDbAsync(TileSourceRecord tileSource, int x, int y, int z, int zoom)
		//{
		//	try
		//	{
		//		TileCacheRecord record = await DBManager.GetTileRecordAsync(tileSource, x, y, z, zoom).ConfigureAwait(false);
		//		if (record != null) return record.FileName;
		//	}
		//	catch (Exception ex)
		//	{
		//		Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
		//	}
		//	return null;
		//}
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