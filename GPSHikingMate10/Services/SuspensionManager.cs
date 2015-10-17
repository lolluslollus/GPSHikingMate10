using LolloGPS.Data;
using LolloGPS.Data.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Utilz;
using Windows.Storage;
using Windows.Storage.Streams;

namespace LolloGPS.Suspension
{
    /// <summary>
    /// SuspensionManager captures global session state to simplify process lifetime management
    /// for an application.  Note that session state will be automatically cleared under a variety
    /// of conditions and should only be used to store information that would be convenient to
    /// carry across sessions, but that should be discarded when an application crashes or is
    /// upgraded.
    /// </summary>

    public sealed class SuspensionManager
    {
        //private static Dictionary<string, object> _sessionState = new Dictionary<string, object>();
        //private static List<Type> _knownTypes = new List<Type>() { typeof(PointRecord) };
        //private const string SessionStateFilename = "_sessionState.xml"; //we don't need this
        private const string SessionDataFilename = "LolloSessionData.xml";
        /// <summary>
        /// Provides access to global session state for the current session.  This state is
        /// serialized by <see cref="SaveSessionAsync"/> and restored by
        /// <see cref="RestoreSessionAsync"/>, so values must be serializable by
        /// <see cref="DataContractSerializer"/> and should be as compact as possible.  Strings
        /// and other self-contained data types are strongly recommended.
        /// </summary>
        //public static Dictionary<string, object> SessionState //we don't need this
        //{
        //    get { return _sessionState; }
        //}

        /// <summary>
        /// List of custom types provided to the <see cref="DataContractSerializer"/> when
        /// reading and writing session state.  Initially empty, additional types may be
        /// added to customize the serialization process.
        /// </summary>
        //public static List<Type> KnownTypes
        //{
        //    get { return _knownTypes; }
        //}

        // LOLLO important! The Mutex can work across AppDomains (ie across main app and background task) but only if you give it a name!
        // Also, if you declare initially owned true, the second thread trying to cross it will stay locked forever. So, declare it false.
        // All this is not well documented.
        private static PersistentData _newPersistentData = null;
        private static List<PointRecord> _history = null;
        private static List<PointRecord> _route0 = null;
        private static List<PointRecord> _landmarks = null;
        public static async Task LoadSettingsAndDbDataAsync()
        {
            string errorMessage = string.Empty;
            PersistentData oldPersistentData = PersistentData.GetInstance();

            try
            {
                // read settings
                StorageFile file = await ApplicationData.Current.LocalFolder.GetFileAsync(SessionDataFilename).AsTask().ConfigureAwait(false);

                //String ssss = null; //this is useful when you debug and want to see the file as a string
                //using (IInputStream inStream = await file.OpenSequentialReadAsync())
                //{
                //    using (StreamReader streamReader = new StreamReader(inStream.AsStreamForRead()))
                //    {
                //      ssss = streamReader.ReadToEnd();
                //    }
                //}

                using (IInputStream inStream = await file.OpenSequentialReadAsync().AsTask().ConfigureAwait(false))
                {
                    using (var iinStream = inStream.AsStreamForRead())
                    {
                        DataContractSerializer serializer = new DataContractSerializer(typeof(PersistentData));
                        iinStream.Position = 0;
                        _newPersistentData = (PersistentData)(serializer.ReadObject(iinStream));
                        await iinStream.FlushAsync().ConfigureAwait(false);
                    }
                }
                Debug.WriteLine("ended reading non-tabular data");

                // read db data
                _history = PersistentData.GetHistoryFromDB();
                _route0 = PersistentData.GetRoute0FromDB();
                _landmarks = PersistentData.GetLandmarksFromDB();
                Debug.WriteLine("ended reading db data");
            }
            catch (FileNotFoundException ex) //ignore file not found, this may be the first run just after installing
            {
                errorMessage = "starting afresh";
                await Logger.AddAsync(ex.ToString(), Logger.FileErrorLogFilename);
            }
            catch (Exception ex)                 //must be tolerant or the app might crash when starting
            {
                errorMessage = "could not restore the data, starting afresh";
                await Logger.AddAsync(ex.ToString(), Logger.FileErrorLogFilename);
            }
            if (!string.IsNullOrWhiteSpace(errorMessage))
            {
                if (_newPersistentData == null) _newPersistentData = PersistentData.GetInstance().CloneNonDbProperties();
                _newPersistentData.LastMessage = errorMessage;
            }

            RuntimeData.SetIsSettingsRead_UI(true);
            RuntimeData.SetIsDBDataRead_UI(true);

            Debug.WriteLine("ended method LoadDataAsyncNoLocks()");
        }
        public static async Task ReadDataAsync()
        {
            Logger.Add_TPL("SuspensionManager.ReadData() started", Logger.ForegroundLogFilename, Logger.Severity.Info);
            await PersistentData.SetInstanceAsync(_newPersistentData,
                _history,
                _route0,
                _landmarks).ConfigureAwait(false);

        }
        public static async Task SaveSettingsAsync(PersistentData allDataOriginal)
        {
            PersistentData allDataClone = allDataOriginal.CloneNonDbProperties();
            //for (int i = 0; i < 100000000; i++) //wait a few seconds, for testing
            //{
            //    String aaa = i.ToString();
            //}

            try
            {
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    DataContractSerializer sessionDataSerializer = new DataContractSerializer(typeof(PersistentData));
                    // DataContractSerializer serializer = new DataContractSerializer(typeof(PersistentData), new DataContractSerializerSettings() { SerializeReadOnlyTypes = true });
                    // DataContractSerializer sessionDataSerializer = new DataContractSerializer(typeof(PersistentData), _knownTypes);
                    // DataContractSerializer sessionDataSerializer = new DataContractSerializer(typeof(PersistentData), new DataContractSerializerSettings() { KnownTypes = _knownTypes, SerializeReadOnlyTypes = true, PreserveObjectReferences = true });
                    sessionDataSerializer.WriteObject(memoryStream, allDataClone);

                    StorageFile sessionDataFile = await ApplicationData.Current.LocalFolder.CreateFileAsync(
                        SessionDataFilename, CreationCollisionOption.ReplaceExisting).AsTask().ConfigureAwait(false);
                    using (Stream fileStream = await sessionDataFile.OpenStreamForWriteAsync().ConfigureAwait(false))
                    {
                        memoryStream.Seek(0, SeekOrigin.Begin);
                        await memoryStream.CopyToAsync(fileStream).ConfigureAwait(false);
                        await memoryStream.FlushAsync().ConfigureAwait(false);
                        await fileStream.FlushAsync().ConfigureAwait(false);
                    }
                }
                Debug.WriteLine("ended method SaveDataAsyncNoLocks()");
            }
            catch (Exception ex)
            {
                Logger.Add_TPL(ex.ToString(), Logger.FileErrorLogFilename);
            }
        }
    }
}