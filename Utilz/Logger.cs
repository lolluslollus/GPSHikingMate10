using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Email;
using Windows.Storage;
using Windows.Storage.Streams;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.CompilerServices;
using System.Text;
using LolloGPS.Data.Constants;

namespace Utilz
{
	public sealed class LogData
	{
		private const long MaxSizeBytes = 16000;
		//private static WeakReference _instance;
		//internal static Logs GetInstance()
		//{
		//    if (_instance == null || !_instance.IsAlive || _instance.Target == null)
		//    {
		//        _instance = new WeakReference(new Logs());
		//    }
		//    return _instance.Target as Logs;
		//}

		private String _body = String.Empty;
		internal String Body { get { return _body; } set { _body = value; } }

		internal static void AddLineStatic(LogData logData, String msg, DateTime when)
		{
			logData._body += System.Environment.NewLine + when; // DateTime.Now;
			logData._body += System.Environment.NewLine + msg;
			logData._body += System.Environment.NewLine;
			logData.CullWithinMaxSize();
		}

		private void RemoveFirstEntry()
		{
			for (int i = 0; i < 3; i++)
			{
				try
				{
					int firstCRIndex = _body.IndexOf(System.Environment.NewLine) + 1;
					if (firstCRIndex < _body.Length)
					{
						_body = _body.Substring(firstCRIndex);
					}
				}
				catch (Exception exc)
				{
					_body = _body.Substring(20);
					Debug.WriteLine("ERROR in LogData: " + exc.ToString());
				}
			}
		}

		private void CullWithinMaxSize()
		{
			if (_body.Length > MaxSizeBytes)
			{
				RemoveFirstEntry();
			}
		}
	}

	//public class RWLockFactory
	//{
	//    private static readonly ReaderWriterLockSlim _rwlFileError = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
	//    private static readonly ReaderWriterLockSlim _rwlForeground = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
	//    private static readonly ReaderWriterLockSlim _rwlBackground = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
	//    private static readonly ReaderWriterLockSlim _rwlAppException = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
	//    private static readonly ReaderWriterLockSlim _rwlBackgroundCancelled = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
	//    private static readonly ReaderWriterLockSlim _rwlAllData = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
	//    //public static ReaderWriterLockSlim GetLock(string whichLog)
	//    //{
	//    //    if (whichLog == Logger.FileErrorLogFilename) return _rwlFileError;
	//    //    if (whichLog == Logger.ForegroundLogFilename) return _rwlForeground;
	//    //    if (whichLog == Logger.BackgroundLogFilename) return _rwlBackground;
	//    //    if (whichLog == Logger.AppExceptionLogFilename) return _rwlAppException;
	//    //    if (whichLog == Logger.BackgroundCancelledLogFilename) return _rwlBackgroundCancelled;
	//    //    if (whichLog == Logger.PersistentDataLogFilename) return _rwlAllData;
	//    //    throw new Exception("RWLockFactory found no lock for log " + whichLog);
	//    //}
	//    public static object GetLockObject(string whichLog)
	//    {
	//        if (whichLog.Equals(Logger.FileErrorLogFilename)) return Logger.FileErrorLock;
	//        else if (whichLog.Equals(Logger.ForegroundLogFilename)) return Logger.ForegroundLock;
	//        else if (whichLog.Equals(Logger.BackgroundLogFilename)) return Logger.BackgroundLock;
	//        else if (whichLog.Equals(Logger.AppExceptionLogFilename)) return Logger.AppExceptionLock;
	//        else if (whichLog.Equals(Logger.BackgroundCancelledLogFilename)) return Logger.BackgroundCancelledLock;
	//        else if (whichLog.Equals(Logger.PersistentDataLogFilename)) return Logger.AllDataLock;
	//        else throw new Exception("RWLockFactory found no lock object for log " + whichLog);
	//    }

	//    public static bool GetLock(string whichLog)
	//    {
	//        if (whichLog.Equals(Logger.FileErrorLogFilename)) return Logger._isFileErrorLocked;
	//        else if (whichLog.Equals(Logger.ForegroundLogFilename)) return Logger._isForegroundLocked;
	//        else if (whichLog.Equals(Logger.BackgroundLogFilename)) return Logger._isBackgroundLocked;
	//        else if (whichLog.Equals(Logger.AppExceptionLogFilename)) return Logger._isAppExceptionLocked;
	//        else if (whichLog.Equals(Logger.BackgroundCancelledLogFilename)) return Logger._isBackgroundCancelledLocked;
	//        else if (whichLog.Equals(Logger.PersistentDataLogFilename)) return Logger._isAllDataLocked;
	//        else throw new Exception("RWLockFactory found no lock for log " + whichLog);
	//    }

	//    public static void SetLock(string whichLog, bool newValue)
	//    {
	//        if (whichLog.Equals(Logger.FileErrorLogFilename)) Logger._isFileErrorLocked = newValue;
	//        else if (whichLog.Equals(Logger.ForegroundLogFilename)) Logger._isForegroundLocked = newValue;
	//        else if (whichLog.Equals(Logger.BackgroundLogFilename)) Logger._isBackgroundLocked = newValue;
	//        else if (whichLog.Equals(Logger.AppExceptionLogFilename)) Logger._isAppExceptionLocked = newValue;
	//        else if (whichLog.Equals(Logger.BackgroundCancelledLogFilename)) Logger._isBackgroundCancelledLocked = newValue;
	//        else if (whichLog.Equals(Logger.PersistentDataLogFilename)) Logger._isAllDataLocked = newValue;
	//        else throw new Exception("RWLockFactory found no lock for log " + whichLog);
	//    }
	//}

	public sealed class Logger
	{
		// mixing multithreading with multitasking is not a good idea.
		// multithreading requires locks, multitasking may or may not initiate new threads, and the locks may stand in the way.
		// within a method that should be run by multiple threads, there should be no awaits!

		//logger http://code.msdn.microsoft.com/wpapps/A-logging-solution-for-c407d880

		public const string LogFolderName = "Logs"; // it is placed in the app local folder
		public const string FileErrorLogFilename = "_FileErrorLog.lol";
		public const string ForegroundLogFilename = "_ForegroundLog.lol";
		public const string BackgroundLogFilename = "_BackgroundLog.lol";
		public const string AppExceptionLogFilename = "_AppExceptionLog.lol";
		public const string BackgroundCancelledLogFilename = "_BackgroundCancelledLog.lol";
		public const string PersistentDataLogFilename = "_PersistentData.lol";

		private static async Task ReadLogAsync(String fileName, LogData logData)
		{
			StorageFile file = await GetFileAsync(fileName).ConfigureAwait(false);
			if (file != null)
			{
				await ReadLogAsync2(file, logData).ConfigureAwait(false);
			}
		}

		private static async Task ReadSaveLogAsync(String fileName, LogData logData, String msg, DateTime when, Action<LogData, String, DateTime> myDelegate)
		{
			StorageFile file = await GetFileAsync(fileName).ConfigureAwait(false);
			if (file != null)
			{
				await ReadLogAsync2(file, logData).ConfigureAwait(false);
				myDelegate(logData, msg, when);
				await SaveLogAsync2(file, logData).ConfigureAwait(false);
			}
			else
			{
				myDelegate(logData, msg, when);
				await SaveLogAsync2(fileName, logData).ConfigureAwait(false);
			}
		}

		private static async Task<StorageFile> GetFileAsync(String fileName)
		{
			if (!String.IsNullOrWhiteSpace(fileName))
			{
				try
				{
					StorageFolder folder = await GetFolderAsync().ConfigureAwait(false);
					if (folder != null)
					{
						return await folder.GetFileAsync(fileName).AsTask().ConfigureAwait(false);
					}
					else
					{
						Debug.WriteLine("ERROR: log folder not found");
					}
				}
				catch (System.IO.FileNotFoundException exc0)
				{
					//put up with it: it may be the first time we use this file
					Debug.WriteLine("no worries but: " + exc0.ToString());
				}
				catch (Exception exc1)
				{
					Debug.WriteLine("ERROR: " + exc1.ToString());
				}
			}
			else
			{
				Debug.WriteLine("ERROR: log file name is empty");
			}
			return null;
		}

		private static async Task ReadLogAsync2(StorageFile file, LogData logData)
		{
			if (file != null)
			{
				try
				{
					using (IInputStream inStream = await file.OpenSequentialReadAsync().AsTask().ConfigureAwait(false))
					{
						using (StreamReader streamReader = new StreamReader(inStream.AsStreamForRead()))
						{
							logData.Body = await streamReader.ReadToEndAsync().ConfigureAwait(false);
						}
					}
				}
				catch (System.IO.FileNotFoundException exc0)
				{
					//put up with it: it may be the first time we use this file
					Debug.WriteLine("no worries but: " + exc0.ToString());
				}
				catch (Exception exc1)
				{
					Debug.WriteLine("ERROR: " + exc1.ToString());
				}
			}
			else
			{
				Debug.WriteLine("ERROR: log file is null");
			}
		}

		private static async Task SaveLogAsync2(StorageFile file, LogData logData)
		{
			if (file != null)
			{
				try
				{
					//String sss0 = null;
					//String sss1 = null;
					//using (Stream fileStreamForRead = await file.OpenStreamForReadAsync().ConfigureAwait(false))
					//{
					//    //fileStream.Seek(0, SeekOrigin.Begin);
					//    using (StreamReader streamReader = new StreamReader(fileStreamForRead))
					//    {
					//        sss0 = await streamReader.ReadToEndAsync().ConfigureAwait(false);
					//    }
					//}
					using (Stream fileStreamForWrite = await file.OpenStreamForWriteAsync().ConfigureAwait(false))
					{
						fileStreamForWrite.SetLength(0); //otherwise, it will append instead of replacing
						fileStreamForWrite.Seek(0, SeekOrigin.Begin);
						using (StreamWriter streamWriter = new StreamWriter(fileStreamForWrite))
						{
							await streamWriter.WriteAsync(logData.Body).ConfigureAwait(false);
							await fileStreamForWrite.FlushAsync().ConfigureAwait(false);
							await streamWriter.FlushAsync().ConfigureAwait(false);
						}
					}
				}
				catch (Exception exc)
				{
					Debug.WriteLine("ERROR: " + exc.ToString());
				}
			}
			else
			{
				Debug.WriteLine("ERROR: log file is null");
			}
		}

		private static async Task SaveLogAsync2(String fileName, LogData logData)
		{
			StorageFile file = null;
			try
			{
				StorageFolder folder = await GetFolderAsync().ConfigureAwait(false);
				if (folder != null)
				{
					if (!String.IsNullOrWhiteSpace(fileName))
					{
						file = await folder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting)
							.AsTask().ConfigureAwait(false);
						if (file != null)
						{
							CachedFileManager.DeferUpdates(file);
							using (Stream fileStream = await file.OpenStreamForWriteAsync().ConfigureAwait(false))
							{
								using (StreamWriter streamWriter = new StreamWriter(fileStream))
								{
									await streamWriter.WriteAsync(logData.Body).ConfigureAwait(false);
									await fileStream.FlushAsync().ConfigureAwait(false);
									await streamWriter.FlushAsync().ConfigureAwait(false);
								}
							}
						}
						else
						{
							Debug.WriteLine("ERROR: could not create log file " + fileName);
						}
					}
					else
					{
						Debug.WriteLine("ERROR: log file name is empty");
					}
				}
				else
				{
					Debug.WriteLine("ERROR: log folder not found");
				}
			}
			catch (Exception exc)
			{
				Debug.WriteLine("ERROR: " + exc.ToString());
			}
			if (file != null) await CachedFileManager.CompleteUpdatesAsync(file).AsTask().ConfigureAwait(false);
		}

		private static StorageFolder _storageFolder = null;
		private static async Task<StorageFolder> GetFolderAsync()
		{
			if (_storageFolder != null) return _storageFolder;
			//StorageFolder sdCardRoot = null;
			//StorageFolder logFolder = null;

			//StorageFolder externalDevices = Windows.Storage.KnownFolders.RemovableDevices;
			//if (externalDevices == null) return GetDefaultFolder();
			////allFolders = await externalDevices.GetFoldersAsync();
			//// Get the first child folder, which represents the SD card.
			//sdCardRoot = (await externalDevices.GetFoldersAsync().AsTask().ConfigureAwait(false)).FirstOrDefault();
			//if (sdCardRoot == null) return GetDefaultFolder();
			//logFolder = await sdCardRoot.CreateFolderAsync(LogFolderName, CreationCollisionOption.OpenIfExists).AsTask().ConfigureAwait(false);
			//_storageFolder = logFolder;

			_storageFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(LogFolderName, CreationCollisionOption.OpenIfExists).AsTask().ConfigureAwait(false);
			return _storageFolder;
		}

		public enum Severity { Info, Error };
		private static readonly SemaphoreSlimSafeRelease _semaphore = new SemaphoreSlimSafeRelease(1, 1); // , "LOLLOLoggerSemaphore");
		
		public static void Add_TPL(string msg, string fileName,
			Severity severity = Severity.Error,
			[CallerMemberName] string memberName = "",
			[CallerFilePath] string sourceFilePath = "",
			[CallerLineNumber] int sourceLineNumber = 0)
		{
			try
			{
				//Task ttt = AddAsync(msg, fileName); // was
				string fullMessage = GetFullMsg(severity, memberName, sourceFilePath, sourceLineNumber, msg);
				Debug.WriteLine(fullMessage);
				Task ttt = Task.Run(() => Add2Async(fullMessage, fileName));
			}
			catch (Exception exc)
			{
				Debug.WriteLine("ERROR in Logger: " + exc.ToString());
			}
		}

		public static async Task AddAsync(string msg, string fileName,
			Severity severity = Severity.Error,
			[CallerMemberName] string memberName = "",
			[CallerFilePath] string sourceFilePath = "",
			[CallerLineNumber] int sourceLineNumber = 0)
		{
			try
			{
				string fullMessage = GetFullMsg(severity, memberName, sourceFilePath, sourceLineNumber, msg);
				Debug.WriteLine(fullMessage);
				await Task.Run(() => { return Add2Async(fullMessage, fileName); }).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				Debug.WriteLine("ERROR in Logger.AddAsync: " + ex.ToString());
			}
		}

		private static string GetFullMsg(Severity severity, string memberName, string sourceFilePath, int sourceLineNumber, string msg)
		{
			if (severity == Severity.Error)
				return string.Format("ERROR in {0}, source {1}, line {2}: {3}", memberName, sourceFilePath, sourceLineNumber, msg);
			else
				return string.Format("INFO from {0}, source {1}, line {2}: {3}", memberName, sourceFilePath, sourceLineNumber, msg);
		}
		private static async Task Add2Async(string msg, string fileName)
		{
			try
			{
				DateTime when = DateTime.Now;
				await _semaphore.WaitAsync().ConfigureAwait(false);
				LogData logData = new LogData();
				//Debug.WriteLine("the thread id is " + Environment.CurrentManagedThreadId + " before the await");
				await ReadSaveLogAsync(fileName, logData, msg, when, new Action<LogData, string, DateTime>(LogData.AddLineStatic)).ConfigureAwait(false);
				//Debug.WriteLine("the thread id is " + Environment.CurrentManagedThreadId + " after the await");
			}
			catch (Exception exc)
			{
				if (SemaphoreSlimSafeRelease.IsAlive(_semaphore))
					Debug.WriteLine("ERROR in Logger: " + exc.ToString());
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(_semaphore);
			}
			// await SendEmailWithLogsAsync("lollus@hotmail.co.uk"); // maybe move Logger into the utils and use the right email address and maybe new parameter "sendemailifcrash"
			// On second thought, the email could be annoying and scary. Better leave the option to send it in the "About" panel only.
		}
		public static async Task SendEmailWithLogsAsync(string recipient)
		{
			EmailRecipient emailRecipient = new EmailRecipient(recipient);

			EmailMessage emailMsg = new EmailMessage();
			emailMsg.Subject = string.Format("Feedback from {0} with logs", ConstantData.AppName);
			emailMsg.To.Add(emailRecipient);
			//emailMsg.Body = await ReadAllLogsIntoStringAsync(); // LOLLO this only works with a short body...

			string body = await ReadAllLogsIntoStringAsync();
			
			using (var ms = new InMemoryRandomAccessStream())
			{
				using (var sw = new StreamWriter(ms.AsStreamForWrite(), Encoding.Unicode))
				{
					await sw.WriteAsync(body);
					await sw.FlushAsync();

					emailMsg.SetBodyStream(EmailMessageBodyKind.PlainText, RandomAccessStreamReference.CreateFromStream(ms));

					await EmailManager.ShowComposeNewEmailAsync(emailMsg).AsTask().ConfigureAwait(false);
				}
			}
		}
		public static async Task<string> ReadAllLogsIntoStringAsync()
		{
			var sb = new StringBuilder();
			sb.Append(await ReadOneLogIntoStringAsync(AppExceptionLogFilename).ConfigureAwait(false));
			sb.Append(await ReadOneLogIntoStringAsync(BackgroundCancelledLogFilename).ConfigureAwait(false));
			sb.Append(await ReadOneLogIntoStringAsync(BackgroundLogFilename).ConfigureAwait(false));
			sb.Append(await ReadOneLogIntoStringAsync(FileErrorLogFilename).ConfigureAwait(false));
			sb.Append(await ReadOneLogIntoStringAsync(ForegroundLogFilename).ConfigureAwait(false));
			sb.Append(await ReadOneLogIntoStringAsync(PersistentDataLogFilename).ConfigureAwait(false));
			return sb.ToString();
		}
		public static async Task<string> ReadOneLogIntoStringAsync(string filename)
		{
			string output = string.Empty;
			output += filename;
			output += Environment.NewLine;
			output += await ReadAsync(filename);
			output += Environment.NewLine;
			return output;
		}
		private static async Task ClearAsync(String fileName)
		{
			try
			{
				LogData logData = new LogData();
				await SaveLogAsync2(fileName, logData).ConfigureAwait(false);
			}
			catch (Exception exc)
			{
				Debug.WriteLine("ERROR in Logger: " + exc.ToString());
			}
		}

		public static void ClearAll()
		{
			Task.Run(async delegate
			//            await ThreadPool.RunAsync(async (qqq) =>
			{
				try
				{
					await _semaphore.WaitAsync().ConfigureAwait(false);
					await ClearAsync(FileErrorLogFilename).ConfigureAwait(false);
					await ClearAsync(PersistentDataLogFilename).ConfigureAwait(false);
					await ClearAsync(ForegroundLogFilename).ConfigureAwait(false);
					await ClearAsync(BackgroundLogFilename).ConfigureAwait(false);
					await ClearAsync(BackgroundCancelledLogFilename).ConfigureAwait(false);
					await ClearAsync(AppExceptionLogFilename).ConfigureAwait(false);
				}
				catch (Exception exc)
				{
					if (SemaphoreSlimSafeRelease.IsAlive(_semaphore))
						Debug.WriteLine("ERROR in Logger: " + exc.ToString());
				}
				finally
				{
					SemaphoreSlimSafeRelease.TryRelease(_semaphore);
				}
			});
		}

		public static async Task<String> ReadAsync(String fileName)
		{
			try
			{
				LogData logData = new LogData();
				await ReadLogAsync(fileName, logData).ConfigureAwait(false);
				return logData.Body;
			}
			catch (Exception exc)
			{
				Debug.WriteLine("ERROR in Logger: " + exc.ToString());
			}
			return null;
		}

	}
}