using LolloGPS.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private const string SettingsFilename = "LolloSessionData.xml";
        /*
        private const string CheckpointsFilename = "LolloCheckpoints.xml";
        private const string HistoryFilename = "LolloHistory.xml";
        private const string Route0Filename = "LolloRoute0.xml";

        private static Collection<PointRecord> GetTable(PersistentData persistentData, PersistentData.Tables whichTable)
        {
            switch (whichTable)
            {
                case PersistentData.Tables.Checkpoints:
                    return persistentData.Checkpoints;
                case PersistentData.Tables.History:
                    return persistentData.History;
                case PersistentData.Tables.Route0:
                    return persistentData.Route0;
                default:
                    return null;
            }
        }
        private static string GetFileName(PersistentData.Tables whichTable)
        {
            switch (whichTable)
            {
                case PersistentData.Tables.Checkpoints:
                    return CheckpointsFilename;
                case PersistentData.Tables.History:
                    return HistoryFilename;
                case PersistentData.Tables.Route0:
                    return Route0Filename;
                default:
                    return null;
            }
        }
        */
        // LOLLO NOTE important! The Mutex can work across AppDomains (ie across main app and background task) but only if you give it a name!
        // Also, if you declare initially owned true, the second thread trying to cross it will stay locked forever. So, declare it false.
        // All this is not well documented.
        /*
        public static async Task LoadDbDataAndSettingsAsync(bool doCheckpoints, bool doHistory, bool doRoute0, bool doSettings)
        {
            PersistentData persistentData = null;
            List<string> errorMessages = new List<string>();

            if (doSettings) persistentData = await LoadSettingsAsync(errorMessages).ConfigureAwait(false);
            if (persistentData == null) persistentData = PersistentData.GetInstance();

            Task loadCheckpoints = persistentData.LoadCheckpointsFromDbAsync(false, true);
            Task loadHistory = persistentData.LoadHistoryFromDbAsync(false, true);
            Task loadRoute0 = persistentData.LoadRoute0FromDbAsync(false, true);

            //Task loadCheckpoints = doCheckpoints ? LoadSeriesAsync(persistentData, PersistentData.Tables.Checkpoints, errorMessages) : Task.CompletedTask;
            //Task loadHistory = doHistory ? LoadSeriesAsync(persistentData, PersistentData.Tables.History, errorMessages) : Task.CompletedTask;
            //Task loadRoute0 = doRoute0 ? LoadSeriesAsync(persistentData, PersistentData.Tables.Route0, errorMessages) : Task.CompletedTask;
            await Task.WhenAll(loadCheckpoints, loadHistory, loadRoute0).ConfigureAwait(false);

            if (errorMessages.Count > 0) persistentData.LastMessage = errorMessages[0];
        }
        */
        public static async Task<PersistentData> LoadSettingsAsync() //List<string> errorMessages)
        {
            string errorMessage = string.Empty;
            PersistentData newPersistentData = null;

            try
            {
                StorageFile file = await ApplicationData.Current.LocalCacheFolder.CreateFileAsync(SettingsFilename, CreationCollisionOption.OpenIfExists).AsTask().ConfigureAwait(false);

                //string ssss = null; //this is useful when you debug and want to see the file as a string
                //using (IInputStream inStream = await file.OpenSequentialReadAsync())
                //{
                //    using (StreamReader streamReader = new StreamReader(inStream.AsStreamForRead()))
                //    {
                //      ssss = streamReader.ReadToEnd();
                //    }
                //}

                using (var inStream = await file.OpenSequentialReadAsync().AsTask().ConfigureAwait(false))
                {
                    using (var iinStream = inStream.AsStreamForRead())
                    {
                        DataContractSerializer serializer = new DataContractSerializer(typeof(PersistentData));
                        iinStream.Position = 0;
                        newPersistentData = (PersistentData)(serializer.ReadObject(iinStream));
                        await iinStream.FlushAsync().ConfigureAwait(false);

                        newPersistentData = await PersistentData.GetInstanceWithClonedNonDbPropertiesAsync(newPersistentData).ConfigureAwait(false);
                    }
                }
            }
            catch (System.Xml.XmlException ex)
            {
                errorMessage = $"could not restore the settings: {ex.Message}";
                await Logger.AddAsync(errorMessage, Logger.FileErrorLogFilename).ConfigureAwait(false);
                newPersistentData = PersistentData.GetInstance();
            }
            catch (Exception ex)
            {
                errorMessage = $"could not restore the settings: {ex.Message}";
                await Logger.AddAsync(errorMessage, Logger.FileErrorLogFilename).ConfigureAwait(false);
                newPersistentData = PersistentData.GetInstance();
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(errorMessage)) newPersistentData.LastMessage = errorMessage;
            }
            return newPersistentData;
        }
        /*
        private static async Task LoadSeriesAsync(PersistentData persistentData, PersistentData.Tables whichTable, List<string> errorMessages)
        {
            string errorMessage = string.Empty;

            Collection<PointRecord> table = GetTable(persistentData, whichTable);
            string fileName = GetFileName(whichTable);

            if (table == null || string.IsNullOrWhiteSpace(fileName)) throw new Exception("LoadSeriesAsync does not know which table to load");
            table.Clear();

            try
            {
                var file = await ApplicationData.Current.LocalCacheFolder.CreateFileAsync(fileName, CreationCollisionOption.OpenIfExists).AsTask().ConfigureAwait(false);

                using (var inStream = await file.OpenSequentialReadAsync().AsTask().ConfigureAwait(false))
                {
                    using (var iinStream = inStream.AsStreamForRead())
                    {
                        var serializer = new DataContractSerializer(typeof(Collection<PointRecord>));
                        iinStream.Position = 0;
                        var coll = (Collection<PointRecord>)(serializer.ReadObject(iinStream));
                        await iinStream.FlushAsync().ConfigureAwait(false);

                        foreach (var item in coll)
                        {
                            table.Add(item);
                        }
                    }
                }
            }
            catch (System.Xml.XmlException ex)
            {
                errorMessage = $"could not restore the {whichTable}: {ex.Message}";
                await Logger.AddAsync(errorMessage, Logger.FileErrorLogFilename);
            }
            catch (Exception ex)
            {
                errorMessage = $"could not restore the {whichTable}: {ex.Message}";
                await Logger.AddAsync(errorMessage, Logger.FileErrorLogFilename);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(errorMessage)) errorMessages.Add(errorMessage);
            }
        }
        */
        public static async Task SaveSettingsAsync(PersistentData persistentData)
        {
            try
            {
                using (var memoryStream = new MemoryStream())
                {
                    var sessionDataSerializer = new DataContractSerializer(typeof(PersistentData));
                    // DataContractSerializer serializer = new DataContractSerializer(typeof(PersistentData), new DataContractSerializerSettings() { SerializeReadOnlyTypes = true });
                    // DataContractSerializer sessionDataSerializer = new DataContractSerializer(typeof(PersistentData), _knownTypes);
                    // DataContractSerializer sessionDataSerializer = new DataContractSerializer(typeof(PersistentData), new DataContractSerializerSettings() { KnownTypes = _knownTypes, SerializeReadOnlyTypes = true, PreserveObjectReferences = true });
                    sessionDataSerializer.WriteObject(memoryStream, persistentData);

                    var sessionDataFile = await ApplicationData.Current.LocalCacheFolder.CreateFileAsync(
                        SettingsFilename, CreationCollisionOption.ReplaceExisting).AsTask().ConfigureAwait(false);
                    using (Stream fileStream = await sessionDataFile.OpenStreamForWriteAsync().ConfigureAwait(false))
                    {
                        memoryStream.Seek(0, SeekOrigin.Begin);
                        await memoryStream.CopyToAsync(fileStream).ConfigureAwait(false);
                        await memoryStream.FlushAsync().ConfigureAwait(false);
                        await fileStream.FlushAsync().ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Add_TPL(ex.ToString(), Logger.FileErrorLogFilename);
            }
        }
        /*
        private static async Task SaveSeriesAsync(Collection<PointRecord> coll, PersistentData.Tables whichTable)
        {
            string fileName = GetFileName(whichTable);

            try
            {
                using (var memoryStream = new MemoryStream())
                {
                    var sessionDataSerializer = new DataContractSerializer(typeof(Collection<PointRecord>));
                    sessionDataSerializer.WriteObject(memoryStream, coll);

                    var file = await ApplicationData.Current.LocalCacheFolder.CreateFileAsync(
                        fileName, CreationCollisionOption.ReplaceExisting).AsTask().ConfigureAwait(false);
                    using (Stream fileStream = await file.OpenStreamForWriteAsync().ConfigureAwait(false))
                    {
                        memoryStream.Seek(0, SeekOrigin.Begin);
                        await memoryStream.CopyToAsync(fileStream).ConfigureAwait(false);
                        await memoryStream.FlushAsync().ConfigureAwait(false);
                        await fileStream.FlushAsync().ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Add_TPL(ex.ToString(), Logger.FileErrorLogFilename);
            }
        }
    */
    }
}