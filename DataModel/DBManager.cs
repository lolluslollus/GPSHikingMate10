﻿using SQLite;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Utilz;
using Windows.Storage;

// Download SQLite for Windows univ app 10, install it, restart visual studio. no nuget or github required.
// Add a reference to it in the project that uses it.
namespace LolloGPS.Data
{
	internal static class DBManager
	{
		private static readonly string _historyDbPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "History.db");
		private static readonly string _route0DbPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "Route0.db");
		private static readonly string _landmarksDbPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "Landmarks.db");
		private static readonly bool _isStoreDateTimeAsTicks = true;
		private static readonly SQLiteOpenFlags _openFlags = SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.NoMutex | SQLiteOpenFlags.ProtectionNone;
		internal static Semaphore _HistorySemaphore = new Semaphore(1, 1, "GPSHikingMate10_HistorySemaphore");
		internal static Semaphore _Route0Semaphore = new Semaphore(1, 1, "GPSHikingMate10_Route0Semaphore");
		internal static Semaphore _LandmarksSemaphore = new Semaphore(1, 1, "GPSHikingMate10_LandmarksSemaphore");

		internal static async Task UpdateHistoryAsync(PointRecord record, bool runSync)
		{
			try
			{
				if (runSync) Update<PointRecord>(_historyDbPath, _openFlags, record, _HistorySemaphore);
				else await UpdateAsync<PointRecord>(_historyDbPath, _openFlags, record, _HistorySemaphore).ConfigureAwait(false);
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
			}
		}
		internal static async Task UpdateRoute0Async(PointRecord record, bool runSync)
		{
			try
			{
				if (runSync) Update<PointRecord>(_route0DbPath, _openFlags, record, _Route0Semaphore);
				else await UpdateAsync<PointRecord>(_route0DbPath, _openFlags, record, _Route0Semaphore).ConfigureAwait(false);
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
			}
		}
		internal static async Task UpdateLandmarksAsync(PointRecord record, bool runSync)
		{
			try
			{
				if (runSync) Update<PointRecord>(_landmarksDbPath, _openFlags, record, _LandmarksSemaphore);
				else await UpdateAsync<PointRecord>(_landmarksDbPath, _openFlags, record, _LandmarksSemaphore).ConfigureAwait(false);
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
			}
		}
		internal static async Task<bool> InsertIntoHistoryAsync(PointRecord record, bool checkMaxEntries)
		{
			try
			{
				return await InsertAsync<PointRecord>(_historyDbPath, _openFlags, record, checkMaxEntries, _HistorySemaphore).ConfigureAwait(false);
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
				return false;
			}
		}
		internal static bool InsertIntoHistory(PointRecord record, bool checkMaxEntries)
		{
			try
			{
				return Insert<PointRecord>(_historyDbPath, _openFlags, record, checkMaxEntries, _HistorySemaphore);
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
				return false;
			}
		}
		internal async static Task<List<PointRecord>> GetHistoryAsync()
		{
			List<PointRecord> history = null;
			try
			{
				history = await ReadTableAsync<PointRecord>(_historyDbPath, _openFlags, _HistorySemaphore).ConfigureAwait(false);
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
				return null;
			}
			return history;
		}
		internal static List<PointRecord> GetHistory()
		{
			List<PointRecord> history = null;
			try
			{
				history = ReadTable<PointRecord>(_historyDbPath, _openFlags, _HistorySemaphore);
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
				return null;
			}
			return history;
		}
		internal async static Task<List<PointRecord>> GetRoute0Async()
		{
			List<PointRecord> route0 = null;
			try
			{
				route0 = await ReadTableAsync<PointRecord>(_route0DbPath, _openFlags, _Route0Semaphore).ConfigureAwait(false);
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
				return null;
			}
			return route0;
		}
		internal static List<PointRecord> GetRoute0()
		{
			List<PointRecord> route0 = null;
			try
			{
				route0 = ReadTable<PointRecord>(_route0DbPath, _openFlags, _Route0Semaphore);
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
				return null;
			}
			return route0;
		}
		internal async static Task<List<PointRecord>> GetLandmarksAsync()
		{
			List<PointRecord> landmarks = null;
			try
			{
				landmarks = await ReadTableAsync<PointRecord>(_landmarksDbPath, _openFlags, _LandmarksSemaphore).ConfigureAwait(false);
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
				return null;
			}
			return landmarks;
		}
		internal static List<PointRecord> GetLandmarks()
		{
			List<PointRecord> landmarks = null;
			try
			{
				landmarks = ReadTable<PointRecord>(_landmarksDbPath, _openFlags, _LandmarksSemaphore);
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
				return null;
			}
			return landmarks;
		}
		internal static async Task InsertIntoLandmarksAsync(PointRecord record, bool checkMaxEntries)
		{
			try
			{
				await InsertAsync<PointRecord>(_landmarksDbPath, _openFlags, record, checkMaxEntries, _LandmarksSemaphore).ConfigureAwait(false);
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
			}
		}
		internal static async Task DeleteFromHistoryAsync(PointRecord record)
		{
			try
			{
				await DeleteAsync<PointRecord>(_historyDbPath, _openFlags, record, _HistorySemaphore).ConfigureAwait(false);
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
			}
		}
		internal static async Task DeleteFromRoute0Async(PointRecord record)
		{
			try
			{
				await DeleteAsync<PointRecord>(_route0DbPath, _openFlags, record, _Route0Semaphore).ConfigureAwait(false);
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
			}
		}
		internal static async Task DeleteFromLandmarksAsync(PointRecord record)
		{
			try
			{
				await DeleteAsync<PointRecord>(_landmarksDbPath, _openFlags, record, _LandmarksSemaphore).ConfigureAwait(false);
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
			}
		}

		internal static async Task ReplaceRoute0Async(IEnumerable<PointRecord> dataRecords, bool checkMaxEntries)
		{
			try
			{
				await ReplaceAllAsync<PointRecord>(_route0DbPath, _openFlags, dataRecords, checkMaxEntries, _Route0Semaphore).ConfigureAwait(false);
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
			}
		}
		internal static async Task ReplaceLandmarksAsync(IEnumerable<PointRecord> dataRecords, bool checkMaxEntries)
		{
			try
			{
				await ReplaceAllAsync<PointRecord>(_landmarksDbPath, _openFlags, dataRecords, checkMaxEntries, _LandmarksSemaphore).ConfigureAwait(false);
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
			}
		}
		internal static async Task DeleteAllFromHistoryAsync()
		{
			try
			{
				await DeleteAllAsync<PointRecord>(_historyDbPath, _openFlags, _HistorySemaphore).ConfigureAwait(false);
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
			}
		}
		internal static async Task DeleteAllFromRoute0Async()
		{
			try
			{
				await DeleteAllAsync<PointRecord>(_route0DbPath, _openFlags, _Route0Semaphore).ConfigureAwait(false);
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
			}
		}
		internal static async Task DeleteAllFromLandmarksAsync()
		{
			try
			{
				await DeleteAllAsync<PointRecord>(_landmarksDbPath, _openFlags, _LandmarksSemaphore).ConfigureAwait(false);
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
			}
		}
		private static int GetHowManyEntriesMax(string dbPath)
		{
			if (dbPath == _historyDbPath) return PersistentData.MaxRecordsInHistory;
			else if (dbPath == _route0DbPath) return PersistentData.MaxRecordsInRoute;
			else if (dbPath == _landmarksDbPath) return PersistentData.MaxRecordsInLandmarks;
			return 0;
		}


		private static Task ReplaceAllAsync<T>(string dbPath, SQLiteOpenFlags openFlags, IEnumerable<T> items, bool checkMaxEntries, Semaphore semaphore)
		{
			return Task.Run(delegate
			{
				if (LolloSQLiteConnectionPoolMT.IsClosed) return;

				try
				{
					semaphore.WaitOne();
					if (LolloSQLiteConnectionPoolMT.IsClosed) return;

					IEnumerable<T> items_mt = null;
					if (checkMaxEntries)
					{
						items_mt = items.Take<T>(GetHowManyEntriesMax(dbPath));
					}
					else
					{
						items_mt = items;
					}

					var connectionString = new SQLiteConnectionString(dbPath, _isStoreDateTimeAsTicks);

					try
					{
						var conn = LolloSQLiteConnectionPoolMT.GetConnection(connectionString, openFlags);
						int aResult = conn.CreateTable(typeof(T));
						conn.DeleteAll<T>();
						conn.InsertAll(items_mt);
					}
					finally
					{
						LolloSQLiteConnectionPoolMT.ResetConnection(connectionString.ConnectionString);
					}
				}
				finally
				{
					SemaphoreExtensions.TryRelease(semaphore);
				}
			});
		}
		private static Task<List<T>> ReadTableAsync<T>(string dbPath, SQLiteOpenFlags openFlags, Semaphore semaphore) where T : new()
		{
			return Task.Run(delegate
			{
				return ReadTable<T>(dbPath, openFlags, semaphore);
			});
		}
		private static List<T> ReadTable<T>(string dbPath, SQLiteOpenFlags openFlags, Semaphore semaphore) where T : new()
		{
			if (LolloSQLiteConnectionPoolMT.IsClosed) return null;

			List<T> result = null;
			try
			{
				semaphore.WaitOne();
				if (LolloSQLiteConnectionPoolMT.IsClosed) return null;

				var connectionString = new SQLiteConnectionString(dbPath, _isStoreDateTimeAsTicks);
				try
				{
					var conn = LolloSQLiteConnectionPoolMT.GetConnection(connectionString, openFlags);
					int aResult = conn.CreateTable(typeof(T));
					var query = conn.Table<T>();
					result = query.ToList<T>();
				}
				finally
				{
					LolloSQLiteConnectionPoolMT.ResetConnection(connectionString.ConnectionString);
				}
			}
			finally
			{
				SemaphoreExtensions.TryRelease(semaphore);
			}
			return result;
		}
		private static Task DeleteAllAsync<T>(string dbPath, SQLiteOpenFlags openFlags, Semaphore semaphore)
		{
			return Task.Run(delegate
			{
				if (LolloSQLiteConnectionPoolMT.IsClosed) return;

				try
				{
					semaphore.WaitOne();
					if (LolloSQLiteConnectionPoolMT.IsClosed) return;

					var connectionString = new SQLiteConnectionString(dbPath, _isStoreDateTimeAsTicks);
					try
					{
						var conn = LolloSQLiteConnectionPoolMT.GetConnection(connectionString, openFlags);
						int aResult = conn.CreateTable(typeof(T));
						conn.DeleteAll<T>();
					}
					finally
					{
						LolloSQLiteConnectionPoolMT.ResetConnection(connectionString.ConnectionString);
					}
				}
				finally
				{
					SemaphoreExtensions.TryRelease(semaphore);
				}
			});
		}
		private static Task<bool> InsertAsync<T>(string dbPath, SQLiteOpenFlags openFlags, object item, bool checkMaxEntries, Semaphore semaphore) where T : new()
		{
			return Task.Run(delegate
			{
				return Insert<T>(dbPath, openFlags, item, checkMaxEntries, semaphore);
			});
		}
		private static bool Insert<T>(string dbPath, SQLiteOpenFlags openFlags, object item, bool checkMaxEntries, Semaphore semaphore) where T : new()
		{
			if (LolloSQLiteConnectionPoolMT.IsClosed) return false;

			bool result = false;
			try
			{
				semaphore.WaitOne();
				if (LolloSQLiteConnectionPoolMT.IsClosed) return false;

				//bool isTesting = true;
				//if (isTesting)
				//{
				//    for (long i = 0; i < 10000000; i++) //wait a few seconds, for testing
				//    {
				//        string aaa = i.ToString();
				//    }
				//}

				var connectionString = new SQLiteConnectionString(dbPath, _isStoreDateTimeAsTicks);
				try
				{
					var conn = LolloSQLiteConnectionPoolMT.GetConnection(connectionString, openFlags);

					int aResult = conn.CreateTable(typeof(T));
					if (checkMaxEntries)
					{
						var query = conn.Table<T>();
						var count = query.Count();
						if (count < GetHowManyEntriesMax(dbPath)) result = conn.Insert(item) > 0;
					}
					else
					{
						result = conn.Insert(item) > 0;
					}
				}
				finally
				{
					LolloSQLiteConnectionPoolMT.ResetConnection(connectionString.ConnectionString);
				}
			}
			finally
			{
				SemaphoreExtensions.TryRelease(semaphore);
			}
			Debug.WriteLine("Insert<T> returned " + result);
			return result;
		}
		private static Task DeleteAsync<T>(string dbPath, SQLiteOpenFlags openFlags, object item, Semaphore semaphore) where T : new()
		{
			return Task.Run(delegate
			{
				Delete<T>(dbPath, openFlags, item, semaphore);
			});
		}
		private static void Delete<T>(string dbPath, SQLiteOpenFlags openFlags, object item, Semaphore semaphore) where T : new()
		{
			if (LolloSQLiteConnectionPoolMT.IsClosed) return;

			try
			{
				semaphore.WaitOne();
				if (LolloSQLiteConnectionPoolMT.IsClosed) return;

				//bool isTesting = true;
				//if (isTesting)
				//{
				//    for (long i = 0; i < 10000000; i++) //wait a few seconds, for testing
				//    {
				//        string aaa = i.ToString();
				//    }
				//}

				var connectionString = new SQLiteConnectionString(dbPath, _isStoreDateTimeAsTicks);
				try
				{
					var conn = LolloSQLiteConnectionPoolMT.GetConnection(connectionString, openFlags);
					int aResult = conn.CreateTable(typeof(T));
					int deleteResult = conn.Delete(item);
					Debug.WriteLine("DB delete returned " + deleteResult);
				}
				finally
				{
					LolloSQLiteConnectionPoolMT.ResetConnection(connectionString.ConnectionString);
				}
			}
			finally
			{
				SemaphoreExtensions.TryRelease(semaphore);
			}
		}
		private static Task UpdateAsync<T>(string dbPath, SQLiteOpenFlags openFlags, object item, Semaphore semaphore) where T : new()
		{
			return Task.Run(delegate
			{
				Update<T>(dbPath, openFlags, item, semaphore);
			});
		}
		private static void Update<T>(string dbPath, SQLiteOpenFlags openFlags, object item, Semaphore semaphore) where T : new()
		{
			if (LolloSQLiteConnectionPoolMT.IsClosed) return;

			try
			{
				semaphore.WaitOne();
				if (LolloSQLiteConnectionPoolMT.IsClosed) return;

				var connectionString = new SQLiteConnectionString(dbPath, _isStoreDateTimeAsTicks);
				try
				{
					var conn = LolloSQLiteConnectionPoolMT.GetConnection(connectionString, openFlags);
					int aResult = conn.CreateTable(typeof(T));
					{
						int test = conn.Update(item);
						Debug.WriteLine(test + " records updated");
					}
				}
				finally
				{
					LolloSQLiteConnectionPoolMT.ResetConnection(connectionString.ConnectionString);
				}
			}
			finally
			{
				SemaphoreExtensions.TryRelease(semaphore);
			}
		}
	}

	internal static class LolloSQLiteConnectionPoolMT
	{
		private sealed class Entry : IDisposable
		{
			public SQLiteConnectionString ConnectionString { get; private set; }
			public SQLiteConnection Connection { get; private set; }

			public Entry(SQLiteConnectionString connectionString, SQLiteOpenFlags openFlags)
			{
				ConnectionString = connectionString;
				Connection = new SQLiteConnection(connectionString.DatabasePath, openFlags, connectionString.StoreDateTimeAsTicks);
			}

			public void Dispose()
			{
				if (Connection != null)
				{
					Connection.Dispose();
					Connection = null;
				}
			}
		}

		private static readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();
		private static Semaphore _entriesSemaphore = new Semaphore(1, 1, "GPSHikingMate10_SQLiteEntriesSemaphore");

		private static volatile bool _isClosed = true;
		public static bool IsClosed { get { return _isClosed; } }
		private static Semaphore _isClosedSemaphore = new Semaphore(1, 1, "GPSHikingMate10_SQLiteIsClosedSemaphore");

		private static Semaphore _dbActionInOtherTaskSemaphore = new Semaphore(1, 1, "GPSHikingMate10_SQLiteDbActionInOtherTaskSemaphore");

		internal static SQLiteConnection GetConnection(SQLiteConnectionString connectionString, SQLiteOpenFlags openFlags)
		{
			Entry entry = null;
			try
			{
				_entriesSemaphore.WaitOne();
				string key = connectionString.ConnectionString;

				if (!_entries.TryGetValue(key, out entry))
				{
					entry = new Entry(connectionString, openFlags);
					_entries[key] = entry;
				}
			}
			catch (Exception) { } // semaphore disposed
			finally
			{
				SemaphoreExtensions.TryRelease(_entriesSemaphore);
			}
			if (entry != null) return entry.Connection;
			return null;
		}

		/// <summary>
		/// Closes a given connection managed by this pool. 
		/// </summary>
		internal static void ResetConnection(string connectionString)
		{
			try
			{
				_entriesSemaphore.WaitOne();
				Entry entry;
				if (_entries.TryGetValue(connectionString, out entry))
				{
					entry.Dispose();
					_entries.Remove(connectionString);
				}
			}
			catch (Exception) { } // semaphore disposed
			finally
			{
				SemaphoreExtensions.TryRelease(_entriesSemaphore);
			}
		}

		public static void Close()
		{
			try
			{
				if (!_isClosed)
				{
					Close2();
				}
			}
			finally
			{
				SemaphoreExtensions.TryRelease(_isClosedSemaphore);
			}
		}
		private static void Close2()
		{
			if (!_isClosed)
			{
				_isClosed = true;
			}
		}

		public static void Open()
		{
			try
			{
				if (_isClosed)
				{
					_isClosedSemaphore.WaitOne();
					if (_isClosed)
					{
						Open2();
					}
				}
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
			}
		}
		private static void Open2()
		{
			if (_isClosed)
			{
				_isClosed = false;
			}
		}

		/// <summary>
		/// Only call this from a task, which is not the main one. 
		/// Otherwise, you will screw up the db open / closed logic.
		/// </summary>
		/// <param name="dbAction"></param>
		/// <returns></returns>
		public static bool RunInOtherTask(Func<bool> dbAction)
		{
			bool isOk = false;
			try
			{
				_dbActionInOtherTaskSemaphore.WaitOne();
				Open2();
				isOk = dbAction();
			}
			catch (Exception ex)
			{
				isOk = false;
				Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
			}
			finally
			{
				try
				{
					Close2();
				}
				finally
				{
					SemaphoreExtensions.TryRelease(_dbActionInOtherTaskSemaphore);
				}
			}
			return isOk;
		}
	}
}