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
		//private static PersistentData _newPersistentData = null;
		//private static List<PointRecord> _history = null;
		//private static List<PointRecord> _route0 = null;
		//private static List<PointRecord> _landmarks = null;
		public static async Task LoadSettingsAndDbDataAsync(bool readDataFromDb, bool readSettingsFromDb)
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
					Debug.WriteLine("ended reading non-tabular data");
				}
			}
			catch (System.Xml.XmlException ex)
			{
				errorMessage = "could not restore the settings, starting afresh";
				PersistentData.GetInstance().LastMessage = errorMessage;
				readDataFromDb = true; // try not to lose the series at least
				await Logger.AddAsync(ex.ToString(), Logger.FileErrorLogFilename);
			}
			catch (Exception ex) // if an error happens here, you will lose the settings, history, last route and last landmarks. 
								 // better quit then. But what if it happens again?
								 // This happened once, on the phone: a funny error message came up, after I opened a hyperlink contained in a location and went back to the app.
								 // LOLLO TODO try to reproduce it
			{
				errorMessage = "could not restore the settings, starting afresh";
				PersistentData.GetInstance().LastMessage = errorMessage;
				readDataFromDb = true; // try not to lose the series at least
				await Logger.AddAsync(ex.ToString(), Logger.FileErrorLogFilename);
			}

			try
			{
				if (readDataFromDb)
				{
					//Task loadHistory = Task.Run(delegate { PersistentData.GetInstance()?.LoadHistoryFromDbAsync(false); });
					//Task loadRoute0 = Task.Run(delegate { PersistentData.GetInstance()?.LoadRoute0FromDbAsync(false); });
					//Task loadLandmarks = Task.Run(delegate { PersistentData.GetInstance()?.LoadLandmarksFromDbAsync(false); });

					Task loadHistory = PersistentData.GetInstance()?.LoadHistoryFromDbAsync(false);
					Task loadRoute0 = PersistentData.GetInstance()?.LoadRoute0FromDbAsync(false);
					Task loadLandmarks = PersistentData.GetInstance()?.LoadLandmarksFromDbAsync(false);

					await Task.WhenAll(loadHistory, loadRoute0, loadLandmarks).ConfigureAwait(false);
					//history = PersistentData.GetHistoryFromDB();
					//route0 = PersistentData.GetRoute0FromDB();
					//landmarks = PersistentData.GetLandmarksFromDB();
				}
			}
			catch (Exception ex) // if an error happens here, you will lose the settings, history, last route and last landmarks. 
								 // better quit then. But what if it happens again?
								 // This happened once, with a funny error message, after I opened a hyperlink contained in a location and went back to the app.
			{
				errorMessage = "could not restore the data, starting afresh";
				PersistentData.GetInstance().LastMessage = errorMessage;
				await Logger.AddAsync(ex.ToString(), Logger.FileErrorLogFilename);
			}

			Logger.Add_TPL("ended method LoadSettingsAndDbDataAsync()", Logger.FileErrorLogFilename, Logger.Severity.Info);
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
				Debug.WriteLine("ended method SaveDataAsyncNoLocks()");
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.FileErrorLogFilename);
			}
		}
	}
}