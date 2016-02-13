using SQLite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
		internal static readonly SQLiteOpenFlags _openFlags = SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.NoMutex | SQLiteOpenFlags.ProtectionNone;
		internal static readonly SemaphoreSlimSafeRelease _writingSemaphore = new SemaphoreSlimSafeRelease(1, 1);

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
		internal static async Task<bool> TryInsertOrIgnoreIntoTileCacheAsync(TileCacheRecord record)
		{
			try
			{
				return await InsertOrIgnoreRecordAsync(_openFlags, record).ConfigureAwait(false);
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
		//private static int GetHowManyEntriesMax()
		//{
		//	return TileCacheReaderWriter.MaxRecords;
		//}

		private static Task<TileCacheRecord> ReadRecordAsync(SQLiteOpenFlags openFlags, TileCacheRecord primaryKey) // where T : new()
		{
			return Task.Run(() => ReadRecord(openFlags, primaryKey));
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
		private static Task<Tuple<bool, int>> DeleteAsync(SQLiteOpenFlags openFlags, string tileSourceTechName)
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

							var deleteAllCommand = conn.CreateCommand("delete from \"TileCache\" where TileSource = \"" + tileSourceTechName + "\"");
							howManyRecordsProcessed = deleteAllCommand.ExecuteNonQuery();

							var countCommand = conn.CreateCountRecordsCommand(tileSourceTechName) as TileCacheCommand;
							howManyRecordsLeft = countCommand.CountRecords();

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
		private static Task<bool> InsertOrIgnoreRecordAsync(SQLiteOpenFlags openFlags, TileCacheRecord item)
		{
			return Task.Run(() => InsertOrIgnoreRecord(openFlags, item));
		}
		private static bool InsertOrIgnoreRecord(SQLiteOpenFlags openFlags, TileCacheRecord item)
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

					var commandInsert = conn.CreateCommand(sCommandInsert);
					int res = commandInsert.ExecuteNonQuery();

					Debug.WriteLine("InsertRecord() has successfully run the command " + sCommandInsert + " with res = " + res);
					result = true;  //(res > 0);
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
			return Task.Run(() => UpdateRecord(openFlags, item));
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

					var commandUpdate = conn.CreateCommand(sCommandUpdate);
					int res = commandUpdate.ExecuteNonQuery();
					result = (res > 0);
					if (res <= 0 && conn.Trace) Debug.WriteLine("res = " + res);
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
		private sealed class ConnectionEntry : IDisposable
		{
			private volatile SQLiteConnectionString _connectionString = null;
			private SQLiteConnectionString ConnectionString { get { return _connectionString; } set { _connectionString = value; } }
			private volatile TileSQLiteConnection _connection = null;
			public TileSQLiteConnection Connection { get { return _connection; } private set { _connection = value; } }

			public ConnectionEntry(SQLiteConnectionString connectionString, SQLiteOpenFlags openFlags)
			{
				ConnectionString = connectionString;
				Connection = new TileSQLiteConnection(connectionString.DatabasePath, openFlags, connectionString.StoreDateTimeAsTicks);
			}

			public void Dispose()
			{
				Connection?.Dispose();
				Connection = null;
			}
		}

		static readonly Dictionary<string, ConnectionEntry> _connectionEntries = new Dictionary<string, ConnectionEntry>();
		private static readonly SemaphoreSlimSafeRelease _connectionEntriesSemaphore = new SemaphoreSlimSafeRelease(1, 1);
		private static readonly SemaphoreSlimSafeRelease _isOpenSemaphore = new SemaphoreSlimSafeRelease(1, 1);
		private static volatile bool _isOpen = false;
		public static bool IsOpen { get { return _isOpen; } }
		internal static TileSQLiteConnection GetConnection(SQLiteConnectionString connectionString, SQLiteOpenFlags openFlags)
		{
			ConnectionEntry entry = null;
			try
			{
				_connectionEntriesSemaphore.Wait();
				string key = connectionString.ConnectionString;

				if (!_connectionEntries.TryGetValue(key, out entry))
				{
					entry = new ConnectionEntry(connectionString, openFlags);
					_connectionEntries[key] = entry;
				}
			}
			catch (Exception ex)
			{
				// Ignore semaphore disposed or semaphore null exceptions
				if (SemaphoreSlimSafeRelease.IsAlive(_connectionEntriesSemaphore))
					Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(_connectionEntriesSemaphore);
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
				_connectionEntriesSemaphore.Wait();
				foreach (var entry in _connectionEntries.Values)
				{
					entry.Dispose();
				}
				_connectionEntries.Clear();
			}
			catch (Exception ex)
			{
				// Ignore semaphore disposed or semaphore null exceptions
				if (SemaphoreSlimSafeRelease.IsAlive(DBManager._writingSemaphore) && SemaphoreSlimSafeRelease.IsAlive(_connectionEntriesSemaphore))
					Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(_connectionEntriesSemaphore);
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
				_connectionEntriesSemaphore.Wait();
				ConnectionEntry entry;
				if (_connectionEntries.TryGetValue(connectionString, out entry))
				{
					entry.Dispose();
					_connectionEntries.Remove(connectionString);
				}
			}
			catch (Exception ex)
			{
				// Ignore semaphore disposed or semaphore null exceptions
				if (SemaphoreSlimSafeRelease.IsAlive(_connectionEntriesSemaphore))
					Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(_connectionEntriesSemaphore);
			}
		}
		public static async Task CloseAsync()
		{
			if (!_isOpen) return;
			try
			{
				// block any new db operations
				_isOpen = false;
				await TileCacheClearer.GetInstance().CloseAsync().ConfigureAwait(false);
				await TileCacheProcessingQueue.GetInstance().CloseAsync().ConfigureAwait(false);
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
					_isOpen = true; // must come before the following
					await TileCacheProcessingQueue.GetInstance().OpenAsync().ConfigureAwait(false);
					await TileCacheClearer.GetInstance().OpenAsync().ConfigureAwait(false);
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
		public SQLiteCommand CreateCountRecordsCommand(string tileSourceTechName)
		{
			string sCommand = "select count(*) from \"TileCache\" where TileSource = \"" + tileSourceTechName + "\"";
			return CreateCommand(sCommand);
		}
	}
	internal sealed class TileCacheCommand : SQLiteCommand
	{
		public TileCacheCommand(SQLiteConnection conn) : base(conn) { }
		public TileCacheRecord GetOneRecord()
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

					TileCacheRecord obj = new TileCacheRecord
					{
						TileSourceTechName = SQLite3.ColumnString(stmt, 0),
						X = SQLite3.ColumnInt(stmt, 1),
						Y = SQLite3.ColumnInt(stmt, 2),
						Z = SQLite3.ColumnInt(stmt, 3),
						Zoom = SQLite3.ColumnInt(stmt, 4),
						FileName = SQLite3.ColumnString(stmt, 5)
					};

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

		public int CountRecords()
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
					return SQLite3.ColumnInt(stmt, 0);
				}
			}
			finally
			{
				SQLite3.Finalize(stmt);
			}
			return -1;
		}
	}
}