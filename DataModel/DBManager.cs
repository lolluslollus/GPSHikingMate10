﻿using SQLite;
using System;
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
    public static class DBManager
    {
        private static readonly string _historyDbPath = Path.Combine(ApplicationData.Current.LocalCacheFolder.Path, "History.db");
        private static readonly string _route0DbPath = Path.Combine(ApplicationData.Current.LocalCacheFolder.Path, "Route0.db");
        private static readonly string _checkpointsDbPath = Path.Combine(ApplicationData.Current.LocalCacheFolder.Path, "Checkpoints.db");
        private static readonly bool _isStoreDateTimeAsTicks = true;
        private static readonly SQLiteOpenFlags _openFlags = SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.NoMutex | SQLiteOpenFlags.ProtectionNone;
        internal static readonly Semaphore _HistorySemaphore = new Semaphore(1, 1, "GPSHikingMate10_HistorySemaphore");
        internal static readonly Semaphore _Route0Semaphore = new Semaphore(1, 1, "GPSHikingMate10_Route0Semaphore");
        internal static readonly Semaphore _CheckpointsSemaphore = new Semaphore(1, 1, "GPSHikingMate10_CheckpointsSemaphore");

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
        internal static async Task UpdateCheckpointsAsync(PointRecord record, bool runSync)
        {
            try
            {
                if (runSync) Update<PointRecord>(_checkpointsDbPath, _openFlags, record, _CheckpointsSemaphore);
                else await UpdateAsync<PointRecord>(_checkpointsDbPath, _openFlags, record, _CheckpointsSemaphore).ConfigureAwait(false);
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
        public static async Task<List<PointRecord>> GetHistoryAsync()
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
        public static async Task<List<PointRecord>> GetRoute0Async()
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
        public static async Task<List<PointRecord>> GetCheckpointsAsync()
        {
            List<PointRecord> checkpoints = null;
            try
            {
                checkpoints = await ReadTableAsync<PointRecord>(_checkpointsDbPath, _openFlags, _CheckpointsSemaphore).ConfigureAwait(false);
            }
            catch (Exception exc)
            {
                Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
                return null;
            }
            return checkpoints;
        }
        internal static List<PointRecord> GetCheckpoints()
        {
            List<PointRecord> checkpoints = null;
            try
            {
                checkpoints = ReadTable<PointRecord>(_checkpointsDbPath, _openFlags, _CheckpointsSemaphore);
            }
            catch (Exception exc)
            {
                Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
                return null;
            }
            return checkpoints;
        }
        internal static async Task InsertIntoCheckpointsAsync(PointRecord record, bool checkMaxEntries)
        {
            try
            {
                await InsertAsync<PointRecord>(_checkpointsDbPath, _openFlags, record, checkMaxEntries, _CheckpointsSemaphore).ConfigureAwait(false);
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
        internal static async Task DeleteFromCheckpointsAsync(PointRecord record)
        {
            try
            {
                await DeleteAsync<PointRecord>(_checkpointsDbPath, _openFlags, record, _CheckpointsSemaphore).ConfigureAwait(false);
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
                await ReplaceAllAsync(_route0DbPath, _openFlags, dataRecords, checkMaxEntries, _Route0Semaphore).ConfigureAwait(false);
            }
            catch (Exception exc)
            {
                Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
            }
        }
        internal static async Task ReplaceCheckpointsAsync(IEnumerable<PointRecord> dataRecords, bool checkMaxEntries)
        {
            try
            {
                await ReplaceAllAsync(_checkpointsDbPath, _openFlags, dataRecords, checkMaxEntries, _CheckpointsSemaphore).ConfigureAwait(false);
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
        internal static async Task DeleteAllFromCheckpointsAsync()
        {
            try
            {
                await DeleteAllAsync<PointRecord>(_checkpointsDbPath, _openFlags, _CheckpointsSemaphore).ConfigureAwait(false);
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
            else if (dbPath == _checkpointsDbPath) return PersistentData.MaxRecordsInCheckpoints;
            return 0;
        }


        private static Task ReplaceAllAsync<T>(string dbPath, SQLiteOpenFlags openFlags, IEnumerable<T> items, bool checkMaxEntries, Semaphore semaphore)
        {
            return Task.Run(delegate
            {
                if (!LolloSQLiteConnectionPoolMT.IsOpen) return;

                try
                {
                    semaphore.WaitOne();
                    if (!LolloSQLiteConnectionPoolMT.IsOpen) return;

                    IEnumerable<T> items_mt = checkMaxEntries ? items.Take(GetHowManyEntriesMax(dbPath)) : items;

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
            return Task.Run(() => ReadTable<T>(dbPath, openFlags, semaphore));
        }
        private static List<T> ReadTable<T>(string dbPath, SQLiteOpenFlags openFlags, Semaphore semaphore) where T : new()
        {
            if (!LolloSQLiteConnectionPoolMT.IsOpen) return null;

            List<T> result = null;
            try
            {
                semaphore.WaitOne();
                if (!LolloSQLiteConnectionPoolMT.IsOpen) return null;

                var connectionString = new SQLiteConnectionString(dbPath, _isStoreDateTimeAsTicks);
                try
                {
                    var conn = LolloSQLiteConnectionPoolMT.GetConnection(connectionString, openFlags);
                    int aResult = conn.CreateTable(typeof(T));
                    var query = conn.Table<T>();
                    result = query.ToList();
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
                if (!LolloSQLiteConnectionPoolMT.IsOpen) return;

                try
                {
                    semaphore.WaitOne();
                    if (!LolloSQLiteConnectionPoolMT.IsOpen) return;

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
            return Task.Run(() => Insert<T>(dbPath, openFlags, item, checkMaxEntries, semaphore));
        }
        private static bool Insert<T>(string dbPath, SQLiteOpenFlags openFlags, object item, bool checkMaxEntries, Semaphore semaphore) where T : new()
        {
            if (!LolloSQLiteConnectionPoolMT.IsOpen) return false;

            bool result = false;
            try
            {
                semaphore.WaitOne();
                if (!LolloSQLiteConnectionPoolMT.IsOpen) return false;

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
            if (!LolloSQLiteConnectionPoolMT.IsOpen) return;

            try
            {
                semaphore.WaitOne();
                if (!LolloSQLiteConnectionPoolMT.IsOpen) return;

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
            if (!LolloSQLiteConnectionPoolMT.IsOpen) return;

            try
            {
                semaphore.WaitOne();
                if (!LolloSQLiteConnectionPoolMT.IsOpen) return;

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
        private sealed class ConnectionEntry : IDisposable
        {
            private volatile SQLiteConnectionString _connectionString = null;
            private SQLiteConnectionString ConnectionString { get { return _connectionString; } set { _connectionString = value; } }
            private volatile SQLiteConnection _connection = null;
            public SQLiteConnection Connection { get { return _connection; } private set { _connection = value; } }

            public ConnectionEntry(SQLiteConnectionString connectionString, SQLiteOpenFlags openFlags)
            {
                ConnectionString = connectionString;
                Connection = new SQLiteConnection(connectionString.DatabasePath, openFlags, connectionString.StoreDateTimeAsTicks);
            }

            public void Dispose()
            {
                Connection?.Dispose();
                Connection = null;
            }
        }

        private static readonly Dictionary<string, ConnectionEntry> _connectionsDict = new Dictionary<string, ConnectionEntry>();
        private static readonly Semaphore _connectionsDictSemaphore = new Semaphore(1, 1, "GPSHikingMate10_SQLiteConnectionEntriesSemaphore");

        private static volatile bool _isOpen = false;
        public static bool IsOpen { get { return _isOpen; } }
        private static readonly Semaphore _isOpenSemaphore = new Semaphore(1, 1, "GPSHikingMate10_SQLiteIsOpenSemaphore");

        //private static readonly Semaphore _dbActionInOtherTaskSemaphore = new Semaphore(1, 1, "GPSHikingMate10_SQLiteDbActionInOtherTaskSemaphore");

        internal static SQLiteConnection GetConnection(SQLiteConnectionString connectionString, SQLiteOpenFlags openFlags)
        {
            ConnectionEntry conn = null;
            try
            {
                _connectionsDictSemaphore.WaitOne();
                string key = connectionString.ConnectionString;

                if (!_connectionsDict.TryGetValue(key, out conn))
                {
                    conn = new ConnectionEntry(connectionString, openFlags);
                    _connectionsDict[key] = conn;
                }
            }
            catch (Exception) { } // semaphore disposed
            finally
            {
                SemaphoreExtensions.TryRelease(_connectionsDictSemaphore);
            }
            if (conn != null) return conn.Connection;
            return null;
        }

        /// <summary>
        /// Closes a given connection managed by this pool. 
        /// </summary>
        internal static void ResetConnection(string connectionString)
        {
            if (connectionString == null) return;

            try
            {
                _connectionsDictSemaphore.WaitOne();
                ConnectionEntry conn = null;
                if (_connectionsDict.TryGetValue(connectionString, out conn))
                {
                    conn.Dispose();
                    _connectionsDict.Remove(connectionString);
                }
            }
            catch (Exception) { } // semaphore disposed
            finally
            {
                SemaphoreExtensions.TryRelease(_connectionsDictSemaphore);
            }
        }

        public static void Close()
        {
            if (!_isOpen) return;
            try
            {
                _isOpenSemaphore.WaitOne();
                if (!_isOpen) return;
                _isOpen = false;
            }
            catch (Exception ex)
            {
                Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
            }
            finally
            {
                SemaphoreExtensions.TryRelease(_isOpenSemaphore);
            }
        }

        public static void Open()
        {
            if (_isOpen) return;
            try
            {
                _isOpenSemaphore.WaitOne();
                if (_isOpen) return;
                _isOpen = true;
            }
            catch (Exception ex)
            {
                Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
            }
            finally
            {
                SemaphoreExtensions.TryRelease(_isOpenSemaphore);
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
            bool wasOpen = _isOpen;
            try
            {
                //_dbActionInOtherTaskSemaphore.WaitOne();
                _isOpenSemaphore.WaitOne();
                wasOpen = _isOpen;
                _isOpen = true;
                return dbAction();
            }
            catch (Exception ex)
            {
                Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
                return false;
            }
            finally
            {
                _isOpen = wasOpen;
                SemaphoreExtensions.TryRelease(_isOpenSemaphore);
                //SemaphoreExtensions.TryRelease(_dbActionInOtherTaskSemaphore);
            }
        }
    }
}