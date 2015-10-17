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
    sealed class DBManager
    {
        internal static readonly string _tileCacheDbPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "TileCache.db");
        internal static readonly bool _isStoreDateTimeAsTicks = true;
        internal static readonly SQLiteOpenFlags _openFlags = SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create; //.FullMutex;
        internal static SemaphoreSlimSafeRelease _writingSemaphore = new SemaphoreSlimSafeRelease(1, 1);

        internal static async Task<TileCacheRecord> GetTileRecordAsync(TileSourceRecord tileSource, int x, int y, int z, int zoom)
        {
            try
            {
                TileCacheRecord primaryKey = new TileCacheRecord(tileSource.TechName, x, y, z, zoom);
                return await LolloSQLiteConnectionMT.ReadRecordAsync(_tileCacheDbPath, _openFlags, _isStoreDateTimeAsTicks, _writingSemaphore, primaryKey).ConfigureAwait(false);
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
                return await LolloSQLiteConnectionMT.InsertRecordAsync(_tileCacheDbPath, _openFlags, _isStoreDateTimeAsTicks, record, checkMaxEntries, _writingSemaphore).ConfigureAwait(false);
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
                return await LolloSQLiteConnectionMT.UpdateRecordAsync(_tileCacheDbPath, _openFlags, _isStoreDateTimeAsTicks, record, _writingSemaphore).ConfigureAwait(false);
            }
            catch (Exception exc)
            {
                Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
            }
            return false;
        }
        internal static async Task<int> DeleteTileCacheAsync(string folderNameToBeDeleted)
        {
            int howManyRecordsDeleted = -3;
            try
            {
                string[] folderNamesToBeDeleted = new string[1] { folderNameToBeDeleted };
                howManyRecordsDeleted = await LolloSQLiteConnectionMT.DeleteAsync(_tileCacheDbPath, _openFlags, _isStoreDateTimeAsTicks, _writingSemaphore, folderNamesToBeDeleted).ConfigureAwait(false);
            }
            catch (Exception exc)
            {
                Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
            }
            return howManyRecordsDeleted;
        }
        internal static int GetHowManyEntriesMax(string dbPath)
        {
            return TileCache.MaxRecords;
        }

        private class LolloSQLiteConnectionMT
        {
            public static Task<TileCacheRecord> ReadRecordAsync(String dbPath, SQLiteOpenFlags openFlags, Boolean storeDateTimeAsTicks, SemaphoreSlimSafeRelease semaphore, TileCacheRecord primaryKey) // where T : new()
            {
                return Task.Run<TileCacheRecord>(delegate
                {
                    return ReadRecord(dbPath, openFlags, storeDateTimeAsTicks, semaphore, primaryKey);
                });
            }
            private static TileCacheRecord ReadRecord(String dbPath, SQLiteOpenFlags openFlags, Boolean storeDateTimeAsTicks, SemaphoreSlimSafeRelease semaphore, TileCacheRecord primaryKey) // where T : new()
            {
                if (LolloSQLiteConnectionPoolMT.IsClosed) return null;
                TileCacheRecord pk_mt = primaryKey;
                TileCacheRecord result = null;
                var connectionString = new SQLiteConnectionString(dbPath, storeDateTimeAsTicks);

                try
                {
                    semaphore.Wait();
                    if (!LolloSQLiteConnectionPoolMT.IsClosed)
                    {
                        var conn = LolloSQLiteConnectionPoolMT.GetConnection(connectionString, openFlags);
                        var command = conn.CreateGetOneRecordCommand(pk_mt) as TileCacheCommand;
                        result = command.GetOneRecord();
                    }
                }
                catch (Exception exc)
                {
                    Debug.WriteLine(exc.ToString() + exc.Message);
                }
                finally // LOLLO TEST: more tolerant semaphores when reading
                {
                    SemaphoreSlimSafeRelease.TryRelease(semaphore);
                }
                return result;
            }
            public static Task<int> DeleteAsync(String dbPath, SQLiteOpenFlags openFlags, Boolean storeDateTimeAsTicks, SemaphoreSlimSafeRelease semaphore, string[] folderNamesToBeDeleted)
            {
                return Task.Run(delegate
                {
                    if (LolloSQLiteConnectionPoolMT.IsClosed) return -2;
                    var connectionString = new SQLiteConnectionString(dbPath, storeDateTimeAsTicks);
                    int howManyRecordsProcessed = -1;

                    try
                    {
                        semaphore.Wait();
                        if (!LolloSQLiteConnectionPoolMT.IsClosed)
                        {
                            var conn = LolloSQLiteConnectionPoolMT.GetConnection(connectionString, openFlags);
                            foreach (var item in folderNamesToBeDeleted)
                            {
                                var deleteAllCommand = conn.CreateCommand("delete from \"TileCache\" where TileSource = \"" + item + "\"");
                                howManyRecordsProcessed = deleteAllCommand.ExecuteNonQuery();
                                if (conn.Trace) Debug.WriteLine(howManyRecordsProcessed + " records were deleted");
                            }
                        }
                    }
                    catch (Exception exc)
                    {
                        Debug.WriteLine("ERROR in DeleteAsync(): " + exc.ToString() + exc.Message);
                    }
                    finally
                    {
                        SemaphoreSlimSafeRelease.TryRelease(semaphore);
                    }
                    return howManyRecordsProcessed;
                });
            }
            public static Task<bool> InsertRecordAsync(String dbPath, SQLiteOpenFlags openFlags, Boolean storeDateTimeAsTicks, TileCacheRecord item, bool checkMaxEntries, SemaphoreSlimSafeRelease semaphore)
            {
                return Task.Run(delegate
                {
                    return InsertRecord(dbPath, openFlags, storeDateTimeAsTicks, item, checkMaxEntries, semaphore);
                });
            }
            private static bool InsertRecord(String dbPath, SQLiteOpenFlags openFlags, Boolean storeDateTimeAsTicks, TileCacheRecord item, bool checkMaxEntries, SemaphoreSlimSafeRelease semaphore)
            {
                if (LolloSQLiteConnectionPoolMT.IsClosed) return false;
                TileCacheRecord item_mt = item;
                var connectionString = new SQLiteConnectionString(dbPath, storeDateTimeAsTicks);
                bool result = false;

                try
                {
                    semaphore.Wait();
                    if (!LolloSQLiteConnectionPoolMT.IsClosed)
                    {
                        var conn = LolloSQLiteConnectionPoolMT.GetConnection(connectionString, openFlags);

                        string sCommandInsert = "insert or ignore into \"TileCache\" (\"TileSource\", \"X\", \"Y\", \"Z\", \"Zoom\", \"FileName\") values (";
                        sCommandInsert += ("\"" + item_mt.TileSourceTechName + "\", ");
                        sCommandInsert += item_mt.X + ", ";
                        sCommandInsert += item_mt.Y + ", ";
                        sCommandInsert += item_mt.Z + ", ";
                        sCommandInsert += item_mt.Zoom + ", ";
                        sCommandInsert += ("\"" + item_mt.FileName + "\")");

                        //if (checkMaxEntries)
                        //{
                        //    var count = conn.Table<TileCacheRecord>().Count();//TODO maybe If you want to use this, 
                        // make a specialised count command like CreateGetOneRecordCommand()
                        //    if (count < GetHowManyEntriesMax(dbPath))
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
                    Debug.WriteLine("ERROR on InsertRecord: " + ex.ToString());
                }
                finally
                {
                    SemaphoreSlimSafeRelease.TryRelease(semaphore);
                }
                return result;
            }
            public static Task<bool> UpdateRecordAsync(String dbPath, SQLiteOpenFlags openFlags, Boolean storeDateTimeAsTicks, TileCacheRecord item, SemaphoreSlimSafeRelease semaphore)
            {
                return Task.Run(delegate
                {
                    return UpdateRecord(dbPath, openFlags, storeDateTimeAsTicks, item, semaphore);
                });
            }

            private static bool UpdateRecord(String dbPath, SQLiteOpenFlags openFlags, Boolean storeDateTimeAsTicks, TileCacheRecord item, SemaphoreSlimSafeRelease semaphore)
            {
                if (LolloSQLiteConnectionPoolMT.IsClosed) return false;
                TileCacheRecord item_mt = item;
                var connectionString = new SQLiteConnectionString(dbPath, storeDateTimeAsTicks);
                bool result = false;

                try
                {
                    semaphore.Wait();
                    if (!LolloSQLiteConnectionPoolMT.IsClosed)
                    {
                        var conn = LolloSQLiteConnectionPoolMT.GetConnection(connectionString, openFlags);

                        string sCommandUpdate = "update or ignore \"TileCache\" set "; // (\"TileSource\", \"X\", \"Y\", \"Z\", \"Zoom\", \"FileName\") values (";
                        sCommandUpdate += ("\"TileSource\" = \"" + item_mt.TileSourceTechName + "\", ");
                        sCommandUpdate += ("\"X\" = " + item_mt.X + ", ");
                        sCommandUpdate += ("\"Y\" = " + item_mt.Y + ", ");
                        sCommandUpdate += ("\"Z\" = " + item_mt.Z + ", ");
                        sCommandUpdate += ("\"Zoom\" = " + item_mt.Zoom + ", ");
                        sCommandUpdate += ("\"FileName\" = \"" + item_mt.FileName + "\"");
                        sCommandUpdate += (" where ");
                        sCommandUpdate += ("\"TileSource\" = \"" + item_mt.TileSourceTechName + "\" and ");
                        sCommandUpdate += ("\"X\" = \"" + item_mt.X + "\" and ");
                        sCommandUpdate += ("\"Y\" = \"" + item_mt.Y + "\" and ");
                        sCommandUpdate += ("\"Z\" = \"" + item_mt.Z + "\" and ");
                        sCommandUpdate += ("\"Zoom\" = \"" + item_mt.Zoom + "\"");
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
                    Debug.WriteLine("ERROR on InsertRecord: " + ex.ToString());
                }
                finally
                {
                    SemaphoreSlimSafeRelease.TryRelease(semaphore);
                }
                return result;
            }
        }
    }

    internal static class LolloSQLiteConnectionPoolMT
    {
        private class Entry
        {
            public SQLiteConnectionString ConnectionString { get; private set; }
            public TileSQLiteConnection Connection { get; private set; }

            public Entry(SQLiteConnectionString connectionString, SQLiteOpenFlags openFlags)
            {
                ConnectionString = connectionString;
                Connection = new TileSQLiteConnection(connectionString.DatabasePath, openFlags, connectionString.StoreDateTimeAsTicks);
            }

            public void OnApplicationSuspended()
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
        private static SemaphoreSlimSafeRelease _isClosedSemaphore = new SemaphoreSlimSafeRelease(1, 1);
        private static volatile bool _isClosed = true;
        /// <summary>
        /// Gets a value telling if the DB is suspended.
        /// </summary>
        public static bool IsClosed { get { return _isClosed; } }
        public static TileSQLiteConnection GetConnection(SQLiteConnectionString connectionString, SQLiteOpenFlags openFlags)
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
            catch (Exception) { } // semaphore disposed
            finally
            {
                SemaphoreSlimSafeRelease.TryRelease(_entriesSemaphore);
            }
            if (entry != null) return entry.Connection;
            return null;
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
                    entry.OnApplicationSuspended();
                }
                _entries.Clear();
            }
            catch (Exception) { } // semaphore disposed
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
                    entry.OnApplicationSuspended();
                    _entries.Remove(connectionString);
                }
            }
            catch (Exception) { } // semaphore disposed
            finally
            {
                try
                {
                    SemaphoreSlimSafeRelease.TryRelease(_entriesSemaphore);
                }
                catch (Exception) { } // semaphore disposed
            }
        }
        /// <summary>
        /// Call this method when the application is suspended.
        /// </summary>
        /// <remarks>Behaviour here is to close any open connections.</remarks>
        public static Task CloseAsync()
        {
            try
            {
                _isClosed = true;
                return Task.Run(() => ResetAllConnections());
            }
            finally
            {
                SemaphoreSlimSafeRelease.TryRelease(_isClosedSemaphore);
            }
        }
        public static void Open()
        {
            try
            {
                if (_isClosed)
                {
                    _isClosedSemaphore.Wait();
                    if (_isClosed)
                    {
                        _isClosed = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Add_TPL(ex.ToString(), Logger.BackgroundLogFilename);
            }
        }
    }

    public sealed class TileSQLiteConnection : SQLiteConnection
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
    public sealed class TileCacheCommand : SQLiteCommand
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