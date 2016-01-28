using SQLite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Utilz;
using Windows.Storage;

// Download SQLite for Windows 10, install it, restart visual studio. no nuget or github required.
// Add a reference to it in the project that uses it.
namespace LolloGPS.Data.TileCache
{
	internal static class DBManager
	{
		internal static readonly string _tileCacheDbPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "TileCache.db");
		internal static readonly bool _isStoreDateTimeAsTicks = true;
		internal static readonly SQLiteOpenFlags _openFlags = SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.NoMutex | SQLiteOpenFlags.ProtectionNone; //.FullMutex; // LOLLO TODO try no mutex
		internal static SemaphoreSlimSafeRelease _writingSemaphore = new SemaphoreSlimSafeRelease(1, 1);

		internal static async Task<TileCacheRecord> GetTileRecordAsync(TileSourceRecord tileSource, int x, int y, int z, int zoom)
		{
			try
			{
				TileCacheRecord primaryKey = new TileCacheRecord(tileSource.TechName, x, y, z, zoom);
				return await ReadRecordAsync(_openFlags, primaryKey).ConfigureAwait(false);
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
				return null;
			}
		}
		internal static async Task<bool> TryInsertIntoTileCacheAsync(TileCacheRecord record, bool checkMaxEntries)
		{
			try
			{
				return await InsertRecordAsync(_openFlags, record, checkMaxEntries).ConfigureAwait(false);
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
			}
			return false;
		}
		internal static async Task<bool> UpdateTileCacheRecordAsync(TileCacheRecord record)
		{
			try
			{
				return await UpdateRecordAsync(_openFlags, record).ConfigureAwait(false);
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
			}
			return false;
		}
		internal static async Task<Tuple<bool, int>> DeleteTileCacheAsync(string folderNameToBeDeleted)
		{
			try
			{
				return await DeleteAsync(_openFlags, folderNameToBeDeleted).ConfigureAwait(false);
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
			}
			return Tuple.Create(false, 0);
		}
		private static int GetHowManyEntriesMax()
		{
			return TileCache.MaxRecords;
		}

		private static Task<TileCacheRecord> ReadRecordAsync(SQLiteOpenFlags openFlags, TileCacheRecord primaryKey) // where T : new()
		{
			return Task.Run(delegate
			{
				return ReadRecord(openFlags, primaryKey);
			});
		}
		private static TileCacheRecord ReadRecord(SQLiteOpenFlags openFlags, TileCacheRecord primaryKey) // where T : new()
		{
			if (!LolloSQLiteConnectionPoolMT.IsOpen) return null;
			TileCacheRecord result = null;

			try
			{
				_writingSemaphore.Wait();
				if (LolloSQLiteConnectionPoolMT.IsOpen)
				{
					var connectionString = new SQLiteConnectionString(_tileCacheDbPath, _isStoreDateTimeAsTicks);
					var conn = LolloSQLiteConnectionPoolMT.GetConnection(connectionString, openFlags);
					var command = conn.CreateGetOneRecordCommand(primaryKey) as TileCacheCommand;
					result = command.GetOneRecord();
				}
			}
			catch (Exception ex)
			{
				// Ignore semaphore disposed or semaphore null exceptions
				if (SemaphoreSlimSafeRelease.IsAlive(_writingSemaphore))
					Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(_writingSemaphore);
			}
			return result;
		}
		private static Task<Tuple<bool, int>> DeleteAsync(SQLiteOpenFlags openFlags, string folderNameToBeDeleted)
		{
			return Task.Run(delegate
			{
				int howManyRecordsProcessed = -1;
				var howManyRecordsLeft = 1;
				if (LolloSQLiteConnectionPoolMT.IsOpen)
				{
					try
					{
						_writingSemaphore.Wait();
						if (LolloSQLiteConnectionPoolMT.IsOpen)
						{
							var connectionString = new SQLiteConnectionString(_tileCacheDbPath, _isStoreDateTimeAsTicks);
							var conn = LolloSQLiteConnectionPoolMT.GetConnection(connectionString, openFlags);

							var deleteAllCommand = conn.CreateCommand("delete from \"TileCache\" where TileSource = \"" + folderNameToBeDeleted + "\"");
							howManyRecordsProcessed = deleteAllCommand.ExecuteNonQuery();

							howManyRecordsLeft = conn.Table<TileCacheRecord>().Count(); // LOLLO TODO check this

							if (conn.Trace) Debug.WriteLine(howManyRecordsProcessed + " records were deleted");
						}
					}
					catch (Exception ex)
					{
						// Ignore semaphore disposed or semaphore null exceptions
						if (SemaphoreSlimSafeRelease.IsAlive(_writingSemaphore))
							Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
					}
					finally
					{
						SemaphoreSlimSafeRelease.TryRelease(_writingSemaphore);
					}
				}
				return Tuple.Create(howManyRecordsLeft < 1, howManyRecordsProcessed);
			});
		}
		private static Task<bool> InsertRecordAsync(SQLiteOpenFlags openFlags, TileCacheRecord item, bool checkMaxEntries)
		{
			return Task.Run(delegate
			{
				return InsertRecord(openFlags, item, checkMaxEntries);
			});
		}
		private static bool InsertRecord(SQLiteOpenFlags openFlags, TileCacheRecord item, bool checkMaxEntries)
		{
			if (!LolloSQLiteConnectionPoolMT.IsOpen) return false;
			bool result = false;

			try
			{
				_writingSemaphore.Wait();
				if (LolloSQLiteConnectionPoolMT.IsOpen)
				{
					var connectionString = new SQLiteConnectionString(_tileCacheDbPath, _isStoreDateTimeAsTicks);
					var conn = LolloSQLiteConnectionPoolMT.GetConnection(connectionString, openFlags);

					string sCommandInsert = "insert or ignore into \"TileCache\" (\"TileSource\", \"X\", \"Y\", \"Z\", \"Zoom\", \"FileName\") values (";
					sCommandInsert += ("\"" + item.TileSourceTechName + "\", ");
					sCommandInsert += item.X + ", ";
					sCommandInsert += item.Y + ", ";
					sCommandInsert += item.Z + ", ";
					sCommandInsert += item.Zoom + ", ";
					sCommandInsert += ("\"" + item.FileName + "\")");

					//if (checkMaxEntries) // LOLLO TODO do it or get rid of it
					//{
					//    var count = conn.Table<TileCacheRecord>().Count();// LOLLO NOTE If you want to use this, 
					// make a specialised count command like CreateGetOneRecordCommand()
					//    if (count < GetHowManyEntriesMax())
					//    {
					//        var commandInsert = conn.CreateCommand(sCommandInsert);
					//        int res = commandInsert.ExecuteNonQuery();
					//        result = (res > 0);
					//    }
					//}
					//else
					//{
					var commandInsert = conn.CreateCommand(sCommandInsert);
					int res = commandInsert.ExecuteNonQuery();

					Debug.WriteLine("InsertRecord() has run the command " + sCommandInsert);
					result = (res > 0);
					if (res <= 0 && conn.Trace) Debug.WriteLine("res = " + res);
					//}
				}
			}
			catch (Exception ex)
			{
				// Ignore semaphore disposed or semaphore null exceptions
				if (SemaphoreSlimSafeRelease.IsAlive(_writingSemaphore))
					Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(_writingSemaphore);
			}
			return result;
		}
		private static Task<bool> UpdateRecordAsync(SQLiteOpenFlags openFlags, TileCacheRecord item)
		{
			return Task.Run(delegate
			{
				return UpdateRecord(openFlags, item);
			});
		}
		private static bool UpdateRecord(SQLiteOpenFlags openFlags, TileCacheRecord item)
		{
			if (!LolloSQLiteConnectionPoolMT.IsOpen) return false;
			bool result = false;

			try
			{
				_writingSemaphore.Wait();
				if (LolloSQLiteConnectionPoolMT.IsOpen)
				{
					var connectionString = new SQLiteConnectionString(_tileCacheDbPath, _isStoreDateTimeAsTicks);
					var conn = LolloSQLiteConnectionPoolMT.GetConnection(connectionString, openFlags);

					string sCommandUpdate = "update or ignore \"TileCache\" set "; // (\"TileSource\", \"X\", \"Y\", \"Z\", \"Zoom\", \"FileName\") values (";
					sCommandUpdate += ("\"TileSource\" = \"" + item.TileSourceTechName + "\", ");
					sCommandUpdate += ("\"X\" = " + item.X + ", ");
					sCommandUpdate += ("\"Y\" = " + item.Y + ", ");
					sCommandUpdate += ("\"Z\" = " + item.Z + ", ");
					sCommandUpdate += ("\"Zoom\" = " + item.Zoom + ", ");
					sCommandUpdate += ("\"FileName\" = \"" + item.FileName + "\"");
					sCommandUpdate += (" where ");
					sCommandUpdate += ("\"TileSource\" = \"" + item.TileSourceTechName + "\" and ");
					sCommandUpdate += ("\"X\" = \"" + item.X + "\" and ");
					sCommandUpdate += ("\"Y\" = \"" + item.Y + "\" and ");
					sCommandUpdate += ("\"Z\" = \"" + item.Z + "\" and ");
					sCommandUpdate += ("\"Zoom\" = \"" + item.Zoom + "\"");
					//sCommandUpdate += ")";

					var commandUpdate = conn.CreateCommand(sCommandUpdate);
					int res = commandUpdate.ExecuteNonQuery();
					result = (res > 0);
					if (res <= 0 && conn.Trace) Debug.WriteLine("res = " + res);
					//}
				}
			}
			catch (Exception ex)
			{
				// Ignore semaphore disposed or semaphore null exceptions
				if (SemaphoreSlimSafeRelease.IsAlive(_writingSemaphore))
					Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(_writingSemaphore);
			}
			return result;
		}
	}

	internal static class LolloSQLiteConnectionPoolMT
	{
		private sealed class Entry : IDisposable
		{
			public SQLiteConnectionString ConnectionString { get; private set; }
			public TileSQLiteConnection Connection { get; private set; }

			public Entry(SQLiteConnectionString connectionString, SQLiteOpenFlags openFlags)
			{
				ConnectionString = connectionString;
				Connection = new TileSQLiteConnection(connectionString.DatabasePath, openFlags, connectionString.StoreDateTimeAsTicks);
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

		static readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();
		private static SemaphoreSlimSafeRelease _entriesSemaphore = new SemaphoreSlimSafeRelease(1, 1);
		private static SemaphoreSlimSafeRelease _isOpenSemaphore = new SemaphoreSlimSafeRelease(1, 1);
		private static volatile bool _isOpen = false;
		/// <summary>
		/// Gets a value telling if the DB is suspended.
		/// </summary>
		public static bool IsOpen { get { return _isOpen; } }
		internal static TileSQLiteConnection GetConnection(SQLiteConnectionString connectionString, SQLiteOpenFlags openFlags)
		{
			Entry entry = null;
			try
			{
				_entriesSemaphore.Wait();
				string key = connectionString.ConnectionString;

				if (!_entries.TryGetValue(key, out entry))
				{
					entry = new Entry(connectionString, openFlags);
					_entries[key] = entry;
				}
			}
			catch (Exception ex)
			{
				// Ignore semaphore disposed or semaphore null exceptions
				if (SemaphoreSlimSafeRelease.IsAlive(_entriesSemaphore))
					Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(_entriesSemaphore);
			}
			return entry?.Connection;
		}

		/// <summary>
		/// Closes all connections managed by this pool.
		/// </summary>
		private static void ResetAllConnections()
		{
			try
			{
				DBManager._writingSemaphore.Wait();
				_entriesSemaphore.Wait();
				foreach (var entry in _entries.Values)
				{
					entry.Dispose();
				}
				_entries.Clear();
			}
			catch (Exception ex)
			{
				// Ignore semaphore disposed or semaphore null exceptions
				if (SemaphoreSlimSafeRelease.IsAlive(DBManager._writingSemaphore) && SemaphoreSlimSafeRelease.IsAlive(_entriesSemaphore))
					Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(_entriesSemaphore);
				SemaphoreSlimSafeRelease.TryRelease(DBManager._writingSemaphore);
			}
		}
		/// <summary>
		/// Closes a given connection managed by this pool. 
		/// </summary>
		internal static void ResetConnection(string connectionString)
		{
			try
			{
				_entriesSemaphore.Wait();
				Entry entry;
				if (_entries.TryGetValue(connectionString, out entry))
				{
					entry.Dispose();
					_entries.Remove(connectionString);
				}
			}
			catch (Exception ex)
			{
				// Ignore semaphore disposed or semaphore null exceptions
				if (SemaphoreSlimSafeRelease.IsAlive(_entriesSemaphore))
					Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(_entriesSemaphore);
			}
		}
		/// <summary>
		/// Close any open connections.
		/// </summary>
		public static async Task CloseAsync()
		{
			if (!_isOpen) return;
			try
			{
				// block any new db operations
				_isOpen = false;
				await TileCacheProcessingQueue.CloseAsync().ConfigureAwait(false);
				// wait until there is a free slot between operations taking place
				// and break off all queued operations
				await Task.Run(() => ResetAllConnections()).ConfigureAwait(false);
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(_isOpenSemaphore);
			}
		}
		public static async Task OpenAsync()
		{
			if (_isOpen) return;
			try
			{
				_isOpenSemaphore.Wait();
				if (!_isOpen)
				{
					await TileCacheProcessingQueue.OpenAsync().ConfigureAwait(false);
					_isOpen = true;
				}
			}
			catch (Exception ex)
			{
				// Ignore semaphore disposed or semaphore null exceptions
				if (SemaphoreSlimSafeRelease.IsAlive(_isOpenSemaphore))
					Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
			}
		}
	}

	internal sealed class TileSQLiteConnection : SQLiteConnection
	{
		public TileSQLiteConnection(string databasePath, SQLiteOpenFlags openFlags, bool storeDateTimeAsTicks = false)
			: base(databasePath, openFlags, storeDateTimeAsTicks)
		{
			//string sDropCommand = "drop table if exists \"TileCache\"";
			//var dropTableCommand = CreateCommand(sDropCommand);
			//int res0 = dropTableCommand.ExecuteNonQuery();
			//            string sCreateCommand = "create table if not exists \"TileCache\"(\n\"TileSource\" varchar not null ,\n\"X\" integer not null ,\n\"Y\" integer not null ,\n\"Z\" integer not null ,\n\"Zoom\" integer not null ,\n\"FileName\" varchar , primary key(TileSource, X, Y, Z, Zoom))";
			//string sCreateCommand = "create table if not exists \"TileCache\"(\n\"TileSource\" varchar not null ,\n\"X\" integer not null ,\n\"Y\" integer not null ,\n\"Z\" integer not null ,\n\"Zoom\" integer not null ,\n\"FileName\" varchar, primary key(TileSource, X, Y, Z, Zoom)) without rowid ";
			string sCreateCommand = "create table if not exists \"TileCache\"(\"TileSource\" varchar not null, \"X\" integer not null, \"Y\" integer not null, \"Z\" integer not null, \"Zoom\" integer not null, \"FileName\" varchar not null, primary key(TileSource, X, Y, Z, Zoom)) without rowid ";
			var createTableCommand = CreateCommand(sCreateCommand);
			int res = createTableCommand.ExecuteNonQuery();
			//var createIndexCommand = _conn.CreateCommand("create unique index if not exists \"Index0\" on \"TileCache\"(\"XYZ\")");
			//int res1 = createIndexCommand.ExecuteNonQuery();
			Debug.WriteLine("connection opened, table created with result " + res);
			Trace = false; // true;
		}
		protected override SQLiteCommand NewCommand()
		{
			return new TileCacheCommand(this);
			//            return base.NewCommand();
		}
		public SQLiteCommand CreateGetOneRecordCommand(TileCacheRecord pk)
		{
			string sCommand = "select * from \"TileCache\" where TileSource = \"" + pk.TileSourceTechName + "\" and X = \"" + pk.X + "\" and Y = \"" + pk.Y + "\" and Z = \"" + pk.Z + "\" and Zoom = \"" + pk.Zoom + "\"";
			return CreateCommand(sCommand);
		}
	}
	internal sealed class TileCacheCommand : SQLiteCommand
	{
		public TileCacheCommand(SQLiteConnection conn) : base(conn) { }
		public LolloGPS.Data.TileCache.TileCacheRecord GetOneRecord()
		{
			if (_conn.Trace)
			{
				Debug.WriteLine("Executing Query: " + this);
			}

			var stmt = Prepare();
			try
			{
				int howManyCols = SQLite3.ColumnCount(stmt);
				if (_conn.Trace)
				{
					Debug.WriteLine("SQLite says that the table has " + howManyCols + " columns");
				}

				var stepResult = SQLite3.Step(stmt);
				if (_conn.Trace)
				{
					Debug.WriteLine("SQLite step returned " + stepResult);
				}
				if (stepResult == SQLite3.Result.Row)
				{
					if (_conn.Trace)
					{
						Debug.WriteLine("SQLite found one record");
					}

					TileCacheRecord obj = new TileCacheRecord();

					obj.TileSourceTechName = SQLite3.ColumnString(stmt, 0);
					obj.X = SQLite3.ColumnInt(stmt, 1);
					obj.Y = SQLite3.ColumnInt(stmt, 2);
					obj.Z = SQLite3.ColumnInt(stmt, 3);
					obj.Zoom = SQLite3.ColumnInt(stmt, 4);
					obj.FileName = SQLite3.ColumnString(stmt, 5);
					if (_conn.Trace)
					{
						Debug.WriteLine("Query returned: " + obj.TileSourceTechName + " " + obj.X + " " + obj.Y + " " + obj.Z + " " + obj.Zoom + " " + obj.FileName); //  + " and readTileSource was " + readTileSource
					}
					return obj;
				}
			}
			finally
			{
				SQLite3.Finalize(stmt);
			}
			return null;
		}
	}
}