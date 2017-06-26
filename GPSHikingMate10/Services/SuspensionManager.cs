using LolloGPS.Data;
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Utilz;
using Windows.Storage;

namespace LolloGPS.Suspension
{
    /// <summary>
    /// SuspensionManager captures global session state to simplify process lifetime management
    /// for an application.  Note that session state will be automatically cleared under a variety
    /// of conditions and should only be used to store information that would be convenient to
    /// carry across sessions, but that should be discarded when an application crashes or is
    /// upgraded.
    /// </summary>

    public static class SuspensionManager
    {
        private static readonly SemaphoreSlimSafeRelease _loadSaveSemaphore = new SemaphoreSlimSafeRelease(1, 1);
        private const string SettingsFilename = "LolloSessionData.xml";
        //private static readonly Type[] KnownTypes = {typeof(IReadOnlyList<string>), typeof(string[])};
        // LOLLO NOTE important! The Mutex can work across AppDomains (ie across main app and background task) but only if you give it a name!
        // Also, if you declare initially owned true, the second thread trying to cross it will stay locked forever. So, declare it false.
        // All this is not well documented.

        public static async Task<PersistentData> LoadSettingsAsync() //List<string> errorMessages)
        {
            string errorMessage = string.Empty;
            PersistentData newPersistentData = null;

            try
            {
                await _loadSaveSemaphore.WaitAsync().ConfigureAwait(false);
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
                        DataContractSerializer serializer = new DataContractSerializer(typeof(PersistentData)); //, KnownTypes);
                        iinStream.Position = 0;
                        newPersistentData = (PersistentData)(serializer.ReadObject(iinStream));
                        await iinStream.FlushAsync().ConfigureAwait(false);

                        if (IsLatestDataStructure(newPersistentData))
                        {
                            newPersistentData = PersistentData.GetInstanceWithProperties(newPersistentData);
                        }
                        else
                        {
                            errorMessage = "could not restore the settings: they have an old structure";
                            await Logger.AddAsync(errorMessage, Logger.FileErrorLogFilename).ConfigureAwait(false);
                            newPersistentData = PersistentData.GetInstance();
                        }
                    }
                }
            }
            catch (DataAlreadyBoundException exc)
            {
                // no error message: the app has probably come out of suspension, so go ahead with the current instance
                await Logger.AddAsync(exc?.ToString(), Logger.FileErrorLogFilename).ConfigureAwait(false);
                newPersistentData = PersistentData.GetInstance();
            }
            catch (System.Xml.XmlException exc)
            {
                errorMessage = $"XmlException: could not restore the settings: settings reset";
                await Logger.AddAsync(exc?.ToString(), Logger.FileErrorLogFilename).ConfigureAwait(false);
                newPersistentData = PersistentData.GetInstance();
            }
            catch (Exception exc)
            {
                errorMessage = $"could not restore the settings: {exc?.Message}";
                await Logger.AddAsync(exc?.ToString(), Logger.FileErrorLogFilename).ConfigureAwait(false);
                newPersistentData = PersistentData.GetInstance();
            }
            finally
            {
                newPersistentData.LastMessage = errorMessage;
                SemaphoreSlimSafeRelease.TryRelease(_loadSaveSemaphore);
            }
            return newPersistentData;
        }

        /// <summary>
        /// Change this method to reflect the latest structure changes, whenever you make one.
        /// </summary>
        /// <param name="persistentData"></param>
        /// <returns></returns>
        private static bool IsLatestDataStructure(PersistentData persistentData)
        {
            if (persistentData == null || persistentData.TileSourcez == null) return false;

            if (persistentData.CurrentTileSources == null) return false;

            return true;
        }

        public static async Task SaveSettingsAsync(PersistentData persistentData)
        {
            try
            {
                await _loadSaveSemaphore.WaitAsync().ConfigureAwait(false);
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
                await Logger.AddAsync(ex.ToString(), Logger.FileErrorLogFilename).ConfigureAwait(false);
            }
            finally
            {
                SemaphoreSlimSafeRelease.TryRelease(_loadSaveSemaphore);
            }
        }
    }
}