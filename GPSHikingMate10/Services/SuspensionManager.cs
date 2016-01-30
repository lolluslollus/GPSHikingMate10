using LolloGPS.Data;
using System;
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
		private const string SessionDataFilename = "LolloSessionData.xml";

		// LOLLO NOTE important! The Mutex can work across AppDomains (ie across main app and background task) but only if you give it a name!
		// Also, if you declare initially owned true, the second thread trying to cross it will stay locked forever. So, declare it false.
		// All this is not well documented.
		public static async Task LoadDbDataAndSettingsAsync(bool readDataFromDb, bool readSettingsFromDb)
		{
			string errorMessage = string.Empty;

			try
			{
				if (readSettingsFromDb)
				{
					StorageFile file = await ApplicationData.Current.LocalFolder.CreateFileAsync(SessionDataFilename, CreationCollisionOption.OpenIfExists).AsTask().ConfigureAwait(false);

					//string ssss = null; //this is useful when you debug and want to see the file as a string
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
							PersistentData newPersistentData = (PersistentData)(serializer.ReadObject(iinStream));
							await iinStream.FlushAsync().ConfigureAwait(false);

							await PersistentData.SetInstanceNonDbPropertiesAsync(newPersistentData).ConfigureAwait(false);
						}
					}
					Debug.WriteLine("ended reading settings");
				}
			}
			catch (System.Xml.XmlException ex)
			{
				errorMessage = "could not restore the settings";
				// readDataFromDb = true; // try not to lose the series at least
				await Logger.AddAsync(ex.ToString(), Logger.FileErrorLogFilename);
			}
			catch (Exception ex) // if an error happens here, you will lose the settings, history, last route and last checkpoints. 
								 // better quit then. But what if it happens again?
								 // This happened once, on the phone: a funny error message came up, after I opened a hyperlink contained in a location and went back to the app.
								 // LOLLO TODO try to reproduce it
			{
				errorMessage = "could not restore the settings";
				// readDataFromDb = true; // try not to lose the series at least
				await Logger.AddAsync(ex.ToString(), Logger.FileErrorLogFilename);
			}

			try
			{
				if (readDataFromDb)
				{
					Task loadHistory = PersistentData.GetInstance()?.LoadHistoryFromDbAsync(false);
					Task loadRoute0 = PersistentData.GetInstance()?.LoadRoute0FromDbAsync(false);
					Task loadCheckpoints = PersistentData.GetInstance()?.LoadCheckpointsFromDbAsync(false);

					await Task.WhenAll(loadHistory, loadRoute0, loadCheckpoints).ConfigureAwait(false);
					Debug.WriteLine("ended reading data from db");
				}
			}
			catch (Exception ex) // if an error happens here, you will lose the settings, history, last route and last checkpoints. 
								 // better quit then. But what if it happens again?
								 // This happened once, with a funny error message, after I opened a hyperlink contained in a location and went back to the app.
			{
				if (string.IsNullOrWhiteSpace(errorMessage)) errorMessage = "could not restore the data";
				else errorMessage += " and could not restore the data";

				await Logger.AddAsync(ex.ToString(), Logger.FileErrorLogFilename);
			}

			if (!string.IsNullOrWhiteSpace(errorMessage)) PersistentData.GetInstance().LastMessage = errorMessage;
		}

		public static async Task SaveSettingsAsync(PersistentData allDataOriginal)
		{
			PersistentData allDataClone = allDataOriginal.CloneNonDbProperties();
			//for (int i = 0; i < 100000000; i++) //wait a few seconds, for testing
			//{
			//    string aaa = i.ToString();
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
				Debug.WriteLine("ended saving settings");
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.FileErrorLogFilename);
			}
		}
	}
}