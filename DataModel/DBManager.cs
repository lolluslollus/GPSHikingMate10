using SQLite;
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
    sealed class DBManager
    {
        private static readonly string _historyDbPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "History.db");
        private static readonly string _route0DbPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "Route0.db");
        private static readonly string _landmarksDbPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "Landmarks.db");
        private static readonly bool _isStoreDateTimeAsTicks = true;
        private static readonly SQLiteOpenFlags _openFlags = SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create; //.FullMutex;
        internal static Semaphore _HistorySemaphore = new Semaphore(1, 1, "GPSHikingMate10_HistorySemaphore");
        internal static Semaphore _Route0Semaphore = new Semaphore(1, 1, "GPSHikingMate10_Route0Semaphore");
        internal static Semaphore _LandmarksSemaphore = new Semaphore(1, 1, "GPSHikingMate10_LandmarksSemaphore");

        internal static async Task UpdateHistoryAsync(PointRecord record, bool runSync)
        {
            try
            {
                if (runSync) LolloSQLiteConnectionMT.Update<PointRecord>(_historyDbPath, _openFlags, _isStoreDateTimeAsTicks, record, _HistorySemaphore);
                else await LolloSQLiteConnectionMT.UpdateAsync<PointRecord>(_historyDbPath, _openFlags, _isStoreDateTimeAsTicks, record, _HistorySemaphore).ConfigureAwait(false);
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
                if (runSync) LolloSQLiteConnectionMT.Update<PointRecord>(_route0DbPath, _openFlags, _isStoreDateTimeAsTicks, record, _Route0Semaphore);
                else await LolloSQLiteConnectionMT.UpdateAsync<PointRecord>(_route0DbPath, _openFlags, _isStoreDateTimeAsTicks, record, _Route0Semaphore).ConfigureAwait(false);
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
                if (runSync) LolloSQLiteConnectionMT.Update<PointRecord>(_landmarksDbPath, _openFlags, _isStoreDateTimeAsTicks, record, _LandmarksSemaphore);
                else await LolloSQLiteConnectionMT.UpdateAsync<PointRecord>(_landmarksDbPath, _openFlags, _isStoreDateTimeAsTicks, record, _LandmarksSemaphore).ConfigureAwait(false);
            }
            catch (Exception exc)
            {
                Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
            }
        }
        internal static async Task InsertIntoHistoryAsync(PointRecord record, bool checkMaxEntries)
        {
            try
            {
                await LolloSQLiteConnectionMT.InsertAsync<PointRecord>(_historyDbPath, _openFlags, _isStoreDateTimeAsTicks, record, checkMaxEntries, _HistorySemaphore).ConfigureAwait(false);
            }
            catch (Exception exc)
            {
                Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
            }
        }
        internal static void InsertIntoHistory(PointRecord record, bool checkMaxEntries)
        {
            try
            {
                LolloSQLiteConnectionMT.Insert<PointRecord>(_historyDbPath, _openFlags, _isStoreDateTimeAsTicks, record, checkMaxEntries, _HistorySemaphore);
            }
            catch (Exception exc)
            {
                Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
            }
        }
        public async static Task<List<PointRecord>> GetHistoryAsync()
        {
            List<PointRecord> history = null;
            try
            {
                history = await LolloSQLiteConnectionMT.ReadTableAsync<PointRecord>(_historyDbPath, _openFlags, _isStoreDateTimeAsTicks, _HistorySemaphore).ConfigureAwait(false);
            }
            catch (Exception exc)
            {
                Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
                return null;
            }
            return history;
        }
        public static List<PointRecord> GetHistory()
        {
            List<PointRecord> history = null;
            try
            {
                history = LolloSQLiteConnectionMT.ReadTable<PointRecord>(_historyDbPath, _openFlags, _isStoreDateTimeAsTicks, _HistorySemaphore);
            }
            catch (Exception exc)
            {
                Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
                return null;
            }
            return history;
        }
        public async static Task<List<PointRecord>> GetRoute0Async()
        {
            List<PointRecord> route0 = null;
            try
            {
                route0 = await LolloSQLiteConnectionMT.ReadTableAsync<PointRecord>(_route0DbPath, _openFlags, _isStoreDateTimeAsTicks, _Route0Semaphore).ConfigureAwait(false);
            }
            catch (Exception exc)
            {
                Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
                return null;
            }
            return route0;
        }
        public static List<PointRecord> GetRoute0()
        {
            List<PointRecord> route0 = null;
            try
            {
                route0 = LolloSQLiteConnectionMT.ReadTable<PointRecord>(_route0DbPath, _openFlags, _isStoreDateTimeAsTicks, _Route0Semaphore);
            }
            catch (Exception exc)
            {
                Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
                return null;
            }
            return route0;
        }
        public async static Task<List<PointRecord>> GetLandmarksAsync()
        {
            List<PointRecord> landmarks = null;
            try
            {
                landmarks = await LolloSQLiteConnectionMT.ReadTableAsync<PointRecord>(_landmarksDbPath, _openFlags, _isStoreDateTimeAsTicks, _LandmarksSemaphore).ConfigureAwait(false);
            }
            catch (Exception exc)
            {
                Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
                return null;
            }
            return landmarks;
        }
        public static List<PointRecord> GetLandmarks()
        {
            List<PointRecord> landmarks = null;
            try
            {
                landmarks = LolloSQLiteConnectionMT.ReadTable<PointRecord>(_landmarksDbPath, _openFlags, _isStoreDateTimeAsTicks, _LandmarksSemaphore);
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
                await LolloSQLiteConnectionMT.InsertAsync<PointRecord>(_landmarksDbPath, _openFlags, _isStoreDateTimeAsTicks, record, checkMaxEntries, _LandmarksSemaphore).ConfigureAwait(false);
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
                await LolloSQLiteConnectionMT.DeleteAsync<PointRecord>(_historyDbPath, _openFlags, _isStoreDateTimeAsTicks, record, _HistorySemaphore).ConfigureAwait(false);
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
                await LolloSQLiteConnectionMT.DeleteAsync<PointRecord>(_route0DbPath, _openFlags, _isStoreDateTimeAsTicks, record, _Route0Semaphore).ConfigureAwait(false);
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
                await LolloSQLiteConnectionMT.DeleteAsync<PointRecord>(_landmarksDbPath, _openFlags, _isStoreDateTimeAsTicks, record, _LandmarksSemaphore).ConfigureAwait(false);
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
                await LolloSQLiteConnectionMT.ReplaceAllAsync<PointRecord>(_route0DbPath, _openFlags, _isStoreDateTimeAsTicks, dataRecords, checkMaxEntries, _Route0Semaphore).ConfigureAwait(false);
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
                await LolloSQLiteConnectionMT.ReplaceAllAsync<PointRecord>(_landmarksDbPath, _openFlags, _isStoreDateTimeAsTicks, dataRecords, checkMaxEntries, _LandmarksSemaphore).ConfigureAwait(false);
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
                await LolloSQLiteConnectionMT.DeleteAllAsync<PointRecord>(_historyDbPath, _openFlags, _isStoreDateTimeAsTicks, _HistorySemaphore).ConfigureAwait(false);
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
                await LolloSQLiteConnectionMT.DeleteAllAsync<PointRecord>(_route0DbPath, _openFlags, _isStoreDateTimeAsTicks, _Route0Semaphore).ConfigureAwait(false);
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
                await LolloSQLiteConnectionMT.DeleteAllAsync<PointRecord>(_landmarksDbPath, _openFlags, _isStoreDateTimeAsTicks, _LandmarksSemaphore).ConfigureAwait(false);
            }
            catch (Exception exc)
            {
                Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
            }
        }
        internal static int GetHowManyEntriesMax(string dbPath)
        {
            if (dbPath == _historyDbPath) return PersistentData.MaxRecordsInHistory;
            else if (dbPath == _route0DbPath) return PersistentData.MaxRecordsInRoute;
            else if (dbPath == _landmarksDbPath) return PersistentData.MaxRecordsInLandmarks;
            return 0;
        }

        private class LolloSQLiteConnectionMT
        {
            public static Task ReplaceAllAsync<T>(String dbPath, SQLiteOpenFlags openFlags, Boolean storeDateTimeAsTicks, IEnumerable<T> items, bool checkMaxEntries, Semaphore semaphore)
            {
                return Task.Run(delegate
                {
                    if (LolloSQLiteConnectionPoolMT.IsClosed) return;

                    Exception exc0 = null;
                    try
                    {
                        semaphore.WaitOne();
                        if (!LolloSQLiteConnectionPoolMT.IsClosed)
                        {
                            IEnumerable<T> items_mt = null;
                            if (checkMaxEntries)
                            {
                                items_mt = items.Take<T>(GetHowManyEntriesMax(dbPath));
                            }
                            else
                            {
                                items_mt = items;
                            }
                            var connectionString = new SQLiteConnectionString(dbPath, storeDateTimeAsTicks);
                            var conn = LolloSQLiteConnectionPoolMT.GetConnection(connectionString, openFlags);
                            try
                            {
                                int aResult = conn.CreateTable(typeof(T));
                                conn.DeleteAll<T>();
                                conn.InsertAll(items_mt);
                            }
                            catch (Exception exc1) { exc0 = exc1; }
                            LolloSQLiteConnectionPoolMT.ResetConnection(connectionString.ConnectionString);
                        }
                    }
                    catch (Exception) { } // semaphore disposed
                    finally
                    {
                        SemaphoreExtensions.TryRelease(semaphore);
                    }
                    if (exc0 != null) throw exc0;
                });
            }
            public static Task<List<T>> ReadTableAsync<T>(String dbPath, SQLiteOpenFlags openFlags, Boolean storeDateTimeAsTicks, Semaphore semaphore) where T : new()
            {
                return Task.Run(delegate
                {
                    return ReadTable<T>(dbPath, openFlags, storeDateTimeAsTicks, semaphore);
                });
            }
            public static List<T> ReadTable<T>(String dbPath, SQLiteOpenFlags openFlags, Boolean storeDateTimeAsTicks, Semaphore semaphore) where T : new()
            {
                if (LolloSQLiteConnectionPoolMT.IsClosed) return null;

                List<T> result = null;
                Exception exc0 = null;
                try
                {
                    semaphore.WaitOne();
                    if (!LolloSQLiteConnectionPoolMT.IsClosed)
                    {
                        var connectionString = new SQLiteConnectionString(dbPath, storeDateTimeAsTicks);
                        var conn = LolloSQLiteConnectionPoolMT.GetConnection(connectionString, openFlags);
                        try
                        {
                            int aResult = conn.CreateTable(typeof(T));
                            var query = conn.Table<T>();
                            result = query.ToList<T>();
                        }
                        catch (Exception exc1) { exc0 = exc1; }
                        LolloSQLiteConnectionPoolMT.ResetConnection(connectionString.ConnectionString);
                    }
                }
                catch (Exception) { } // semaphore disposed
                finally
                {
                    SemaphoreExtensions.TryRelease(semaphore);
                }
                if (exc0 != null) throw exc0;
                return result;
            }
            public static Task DeleteAllAsync<T>(String dbPath, SQLiteOpenFlags openFlags, Boolean storeDateTimeAsTicks, Semaphore semaphore)
            {
                return Task.Run(delegate
                {
                    if (LolloSQLiteConnectionPoolMT.IsClosed) return;

                    Exception exc0 = null;
                    try
                    {
                        semaphore.WaitOne();
                        if (!LolloSQLiteConnectionPoolMT.IsClosed)
                        {
                            var connectionString = new SQLiteConnectionString(dbPath, storeDateTimeAsTicks);
                            var conn = LolloSQLiteConnectionPoolMT.GetConnection(connectionString, openFlags);
                            try
                            {
                                int aResult = conn.CreateTable(typeof(T));
                                conn.DeleteAll<T>();
                            }
                            catch (Exception exc1) { exc0 = exc1; }
                            LolloSQLiteConnectionPoolMT.ResetConnection(connectionString.ConnectionString);
                        }
                    }
                    catch (Exception) { } // semaphore disposed
                    finally
                    {
                        SemaphoreExtensions.TryRelease(semaphore);
                    }
                    if (exc0 != null) throw exc0;
                });
            }
            public static Task InsertAsync<T>(String dbPath, SQLiteOpenFlags openFlags, Boolean storeDateTimeAsTicks, object item, bool checkMaxEntries, Semaphore semaphore) where T : new()
            {
                return Task.Run(delegate
                {
                    Insert<T>(dbPath, openFlags, storeDateTimeAsTicks, item, checkMaxEntries, semaphore);
                });
            }
            public static void Insert<T>(String dbPath, SQLiteOpenFlags openFlags, Boolean storeDateTimeAsTicks, object item, bool checkMaxEntries, Semaphore semaphore) where T : new()
            {
                if (LolloSQLiteConnectionPoolMT.IsClosed) return;

                //Debug.WriteLine("Insert<T>: semaphore entered with dbpath = " + dbPath);
                Exception exc0 = null;
                try
                {
                    semaphore.WaitOne();
                    if (!LolloSQLiteConnectionPoolMT.IsClosed)
                    {
                        //bool isTesting = true;
                        //if (isTesting)
                        //{
                        //    for (long i = 0; i < 10000000; i++) //wait a few seconds, for testing
                        //    {
                        //        String aaa = i.ToString();
                        //    }
                        //}

                        object item_mt = item;
                        var connectionString = new SQLiteConnectionString(dbPath, storeDateTimeAsTicks);
                        var conn = LolloSQLiteConnectionPoolMT.GetConnection(connectionString, openFlags);
                        try
                        {
                            int aResult = conn.CreateTable(typeof(T));
                            if (checkMaxEntries)
                            {
                                var query = conn.Table<T>();
                                var count = query.Count();
                                if (count < GetHowManyEntriesMax(dbPath)) conn.Insert(item_mt);
                            }
                            else
                            {
                                conn.Insert(item_mt);
                            }
                            //var query2 = conn.Table<T>(); //delete when done testing
                            //var count2 = query2.Count(); //delete when done testing
                            //var dummy = "WW"; //delete when done testing
                        }
                        catch (Exception exc1) { exc0 = exc1; }
                        LolloSQLiteConnectionPoolMT.ResetConnection(connectionString.ConnectionString);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
                }
                finally
                {
                    SemaphoreExtensions.TryRelease(semaphore);
                }
                if (exc0 != null) throw exc0;
            }
            public static Task DeleteAsync<T>(String dbPath, SQLiteOpenFlags openFlags, Boolean storeDateTimeAsTicks, object item, Semaphore semaphore) where T : new()
            {
                return Task.Run(delegate
                {
                    Delete<T>(dbPath, openFlags, storeDateTimeAsTicks, item, semaphore);
                });
            }
            public static void Delete<T>(String dbPath, SQLiteOpenFlags openFlags, Boolean storeDateTimeAsTicks, object item, Semaphore semaphore) where T : new()
            {
                if (LolloSQLiteConnectionPoolMT.IsClosed) return;

                //Debug.WriteLine("Insert<T>: semaphore entered with dbpath = " + dbPath);
                Exception exc0 = null;
                try
                {
                    semaphore.WaitOne();
                    if (!LolloSQLiteConnectionPoolMT.IsClosed)
                    {
                        //bool isTesting = true;
                        //if (isTesting)
                        //{
                        //    for (long i = 0; i < 10000000; i++) //wait a few seconds, for testing
                        //    {
                        //        String aaa = i.ToString();
                        //    }
                        //}

                        object item_mt = item;
                        var connectionString = new SQLiteConnectionString(dbPath, storeDateTimeAsTicks);
                        var conn = LolloSQLiteConnectionPoolMT.GetConnection(connectionString, openFlags);
                        try
                        {
                            int aResult = conn.CreateTable(typeof(T));
                            int deleteResult = conn.Delete(item_mt);
                            Debug.WriteLine("DB delete returned " + deleteResult);
                        }
                        catch (Exception exc1) { exc0 = exc1; }
                        LolloSQLiteConnectionPoolMT.ResetConnection(connectionString.ConnectionString);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
                }
                finally
                {
                    SemaphoreExtensions.TryRelease(semaphore);
                }
                if (exc0 != null) throw exc0;
            }
            public static Task UpdateAsync<T>(String dbPath, SQLiteOpenFlags openFlags, Boolean storeDateTimeAsTicks, object item, Semaphore semaphore) where T : new()
            {
                return Task.Run(delegate
                {
                    Update<T>(dbPath, openFlags, storeDateTimeAsTicks, item, semaphore);
                });
            }
            public static void Update<T>(String dbPath, SQLiteOpenFlags openFlags, Boolean storeDateTimeAsTicks, object item, Semaphore semaphore) where T : new()
            {
                if (LolloSQLiteConnectionPoolMT.IsClosed) return;

                Exception exc0 = null;
                try
                {
                    semaphore.WaitOne();
                    if (!LolloSQLiteConnectionPoolMT.IsClosed)
                    {
                        var connectionString = new SQLiteConnectionString(dbPath, storeDateTimeAsTicks);
                        var conn = LolloSQLiteConnectionPoolMT.GetConnection(connectionString, openFlags);
                        try
                        {
                            int aResult = conn.CreateTable(typeof(T));
                            {
                                int test = conn.Update(item);
                                Debug.WriteLine(test + "records updated");
                            }
                        }
                        catch (Exception exc1) { exc0 = exc1; }
                        LolloSQLiteConnectionPoolMT.ResetConnection(connectionString.ConnectionString);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
                }
                finally
                {
                    SemaphoreExtensions.TryRelease(semaphore);
                }
                if (exc0 != null) throw exc0;
            }
        }
    }

    internal static class LolloSQLiteConnectionPoolMT
    {
        private class Entry
        {
            public SQLiteConnectionString ConnectionString { get; private set; }
            public SQLiteConnection Connection { get; private set; }

            public Entry(SQLiteConnectionString connectionString, SQLiteOpenFlags openFlags)
            {
                ConnectionString = connectionString;
                Connection = new SQLiteConnection(connectionString.DatabasePath, openFlags, connectionString.StoreDateTimeAsTicks);
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

        private static readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();
        private static Semaphore _entriesSemaphore = new Semaphore(1, 1, EntriesSemaphoreName);
        private const string EntriesSemaphoreName = "GPSHikingMate10_SQLiteEntriesSemaphore";
        private static Semaphore _isClosedSemaphore = new Semaphore(1, 1, IsClosedSemaphoreName);
        private const string IsClosedSemaphoreName = "GPSHikingMate10_SQLiteIsClosedSemaphore";
        private static volatile bool _isClosed = true;
        /// <summary>
        /// Gets a value to tell if the DB is suspended or active.
        /// </summary>
        public static bool IsClosed { get { return _isClosed; } }

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
        /// Closes all connections managed by this pool.
        /// </summary>
        private static void ResetAllConnections()
        {
            try
            {
                DBManager._HistorySemaphore.WaitOne();
                DBManager._Route0Semaphore.WaitOne();
                DBManager._LandmarksSemaphore.WaitOne();
                _entriesSemaphore.WaitOne();
                foreach (var entry in _entries.Values)
                {
                    entry.OnApplicationSuspended();
                }
                _entries.Clear();
            }
            catch (Exception) { } // semaphore disposed
            finally
            {
                SemaphoreExtensions.TryRelease(_entriesSemaphore);
                SemaphoreExtensions.TryRelease(DBManager._LandmarksSemaphore);
                SemaphoreExtensions.TryRelease(DBManager._Route0Semaphore);
                SemaphoreExtensions.TryRelease(DBManager._HistorySemaphore);
            }
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
                    entry.OnApplicationSuspended();
                    _entries.Remove(connectionString);
                }
            }
            catch (Exception) { } // semaphore disposed
            finally
            {
                SemaphoreExtensions.TryRelease(_entriesSemaphore);
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
                SemaphoreExtensions.TryRelease(_isClosedSemaphore);
            }
        }
        /// <summary>
        /// Call this method when the application is resumed.
        /// </summary>
        public static void Open()
        {
            try
            {
                if (_isClosed)
                {
                    _isClosedSemaphore.WaitOne();
                    if (_isClosed)
                    {
                        _isClosed = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
            }
        }
        //public static bool TryOpenIsClosedSemaphore()
        //{
        //    Semaphore semaphore = null;
        //    return Semaphore.TryOpenExisting(IsClosedSemaphoreName, out semaphore);
        //}
    }
}