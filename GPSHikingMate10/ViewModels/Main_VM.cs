using GPX;
using LolloBaseUserControls;
using LolloGPS.Data;
using LolloGPS.Data.Constants;
using LolloGPS.Data.Runtime;
using LolloGPS.Data.TileCache;
using LolloGPS.GPSInteraction;
using LolloGPS.Suspension;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Utilz;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Phone.UI.Input;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Core;
using Windows.UI.Xaml;

namespace LolloGPS.Core
{
	public sealed class Main_VM : ObservableData, BackPressedRaiser, IMapApController, IFileActivatable
	{
		#region events
		public event EventHandler<BackOrHardSoftKeyPressedEventArgs> BackOrHardSoftKeyPressed;
		#endregion events

		#region properties
		//public const string WhichTable = "WhichTable";
		//public const string FileCreationDateTime = "FileCreationDateTime";
		private const double MIN_ALTITUDE_ABS = .1;
		private const double MAX_ALTITUDE_ABS = 10000.0;


		private LolloMap_VM _myLolloMap_VM = null;
		public LolloMap_VM MyLolloMap_VM { get { return _myLolloMap_VM; } set { _myLolloMap_VM = value; RaisePropertyChanged(); } }

		private AltitudeProfiles_VM _myAltitudeProfiles_VM = null;
		public AltitudeProfiles_VM MyAltitudeProfiles_VM { get { return _myAltitudeProfiles_VM; } set { _myAltitudeProfiles_VM = value; RaisePropertyChanged(); } }

		public PersistentData MyPersistentData { get { return App.PersistentData; } }
		public RuntimeData MyRuntimeData { get { return App.MyRuntimeData; } }

		private GPSInteractor _myGPSInteractor = null;
		public GPSInteractor MyGPSInteractor { get { return _myGPSInteractor; } }

		private bool _isClearCustomCacheEnabled = false;
		public bool IsClearCustomCacheEnabled { get { return _isClearCustomCacheEnabled; } set { if (_isClearCustomCacheEnabled != value) { _isClearCustomCacheEnabled = value; RaisePropertyChanged_UI(); } } }
		private bool _isClearCacheEnabled = false;
		public bool IsClearCacheEnabled { get { return _isClearCacheEnabled; } set { if (_isClearCacheEnabled != value) { _isClearCacheEnabled = value; RaisePropertyChanged_UI(); } } }
		private bool _isCacheBtnEnabled = false;
		public bool IsCacheBtnEnabled { get { return _isCacheBtnEnabled; } set { if (_isCacheBtnEnabled != value) { _isCacheBtnEnabled = value; RaisePropertyChanged_UI(); } } }
		private bool _isLeechingEnabled = false;
		public bool IsLeechingEnabled { get { return _isLeechingEnabled; } set { if (_isLeechingEnabled != value) { _isLeechingEnabled = value; RaisePropertyChanged_UI(); } } }

		private string _testTileSourceErrorMsg = "";
		public string TestTileSourceErrorMsg { get { return _testTileSourceErrorMsg; } set { _testTileSourceErrorMsg = value; RaisePropertyChanged(); } }

		//private bool _isShowingLogsPanel = false;
		//public bool IsShowingLogsPanel { get { return _isShowingLogsPanel; } set { _isShowingLogsPanel = value; RaisePropertyChanged(); } }

		private bool _isLastMessageVisible = false;
		public bool IsLastMessageVisible { get { return _isLastMessageVisible; } set { if (_isLastMessageVisible != value) { _isLastMessageVisible = value; RaisePropertyChanged(); } } }

		private bool _isLoading = false;
		public bool IsLoading { get { return _isLoading; } private set { _isLoading = value; RaisePropertyChanged_UI(); } }

		private bool _isSaving = false;
		public bool IsSaving { get { return _isSaving; } private set { _isSaving = value; RaisePropertyChanged_UI(); } }

		private string _logText;
		public string LogText { get { return _logText; } set { _logText = value; RaisePropertyChanged(); } }

		#endregion properties

		#region construct and dispose
		private static Main_VM _instance = null;
		private static readonly object _instanceLocker = new object();
		public static Main_VM GetInstance()
		{
			lock (_instanceLocker)
			{
				if (_instance == null) _instance = new Main_VM();
				return _instance;
			}
		}
		private Main_VM()
		{
			_myGPSInteractor = new GPSInteractor(MyPersistentData);
			//Application.Current.Suspending += OnSuspending;
			//Application.Current.Resuming += OnResuming;
		}

		//private static SemaphoreSlimSafeRelease _openCloseSemaphore = new SemaphoreSlimSafeRelease(1, 1);
		//private volatile bool _isOpen = false;
		internal async Task OpenAsync(bool readDataFromDb, bool readSettingsFromDb)
		{
			//if (_isOpen) return;
			try
			{
				//await _openCloseSemaphore.WaitAsync();
				//if (_isOpen) return;
				//await Logger.AddAsync("opening", Logger.ForegroundLogFilename, Logger.Severity.Info);
				//bool isTesting = true; // LOLLO remove when done testing
				//if (isTesting)
				//{
				//    for (long i = 0; i < 100000000; i++) //wait a few seconds, for testing
				//    {
				//        string aaa = i.ToString();
				//    }
				//}

				// disable UI commands
				RuntimeData.SetIsDBDataRead_UI(false);

				if (readSettingsFromDb) await SuspensionManager.LoadSettingsAndDbDataAsync(readDataFromDb, readSettingsFromDb).ConfigureAwait(false);

				await _myGPSInteractor.OpenAsync();
				UpdateClearCacheButtonIsEnabled();
				UpdateClearCustomCacheButtonIsEnabled();
				UpdateCacheButtonIsEnabled();
				UpdateDownloadButtonIsEnabled();

				KeepAlive.UpdateKeepAlive(MyPersistentData.IsKeepAlive);

				AddHandlers_DataChanged();

				//_isOpen = true;
				//await Logger.AddAsync("opened", Logger.ForegroundLogFilename, Logger.Severity.Info);
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename);
			}
			//finally
			//{
			//	SemaphoreSlimSafeRelease.TryRelease(_openCloseSemaphore);
			//}
		}
		internal async Task CloseAsync()
		{
			//if (!_isOpen) return;
			try
			{
				//await _openCloseSemaphore.WaitAsync();
				//if (!_isOpen) return;
				//Logger.Add_TPL("closing", Logger.ForegroundLogFilename, Logger.Severity.Info);

				RemoveHandlers_DataChanged();
				_myGPSInteractor.Deactivate();
				KeepAlive.StopKeepAlive();

				CancelPendingTasks(); // after removing the handlers

				//bool isTesting = true; // LOLLO remove when done testing
				//if (isTesting)
				//{
				//    for (long i = 0; i < 100000000; i++) //wait a few seconds, for testing
				//    {
				//        string aaa = i.ToString();
				//    }
				//}
				//_isOpen = false;
				//Logger.Add_TPL("closed", Logger.ForegroundLogFilename, Logger.Severity.Info);
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename);
			}
			//finally
			//{
			//	SemaphoreSlimSafeRelease.TryRelease(_openCloseSemaphore);
			//}
		}
		private void CancelPendingTasks()
		{
			// Do not dispose the cts's here. Dispose is done in the exception handler that catches the OperationCanceled exception. 
			// If you do it here, the exception handler will throw an ObjectDisposed exception
			_fileOpenContinuationCts?.Cancel();
			_fileOpenPickerCts?.Cancel();
			_fileSavePickerCts?.Cancel();
		}
		//private volatile bool _isSuspended = false;
		//private async void OnResuming(object sender, object e)
		//{
		//	//_isSuspended = false;
		//	await ContinueAfterFileOpenPickAsync().ConfigureAwait(false);
		//}

		//private void OnSuspending(object sender, Windows.ApplicationModel.SuspendingEventArgs e)
		//{
		//	var deferral = e.SuspendingOperation.GetDeferral();
		//	_isSuspended = true;
		//	deferral.Complete();
		//}
		#endregion construct and dispose

		#region updaters
		internal void UpdateClearCustomCacheButtonIsEnabled()
		{
			IsClearCustomCacheEnabled = // !(MyPersistentData.IsTilesDownloadDesired && MyRuntimeData.IsConnectionAvailable) &&
				MyPersistentData.TileSourcez.FirstOrDefault(a => a.IsDeletable) != null &&
				TileCache.ProcessingQueue.IsFree;
		}
		internal void UpdateClearCacheButtonIsEnabled()
		{
			IsClearCacheEnabled = // !(MyPersistentData.IsTilesDownloadDesired && MyRuntimeData.IsConnectionAvailable) &&
				TileCache.ProcessingQueue.IsFree;
		}
		internal void UpdateCacheButtonIsEnabled()
		{
			IsCacheBtnEnabled = // !MyPersistentData.CurrentTileSource.IsTesting && 
				!MyPersistentData.CurrentTileSource.IsDefault
				&& TileCache.ProcessingQueue.IsFree;
		}
		internal void UpdateDownloadButtonIsEnabled()
		{
			IsLeechingEnabled = !MyPersistentData.IsTilesDownloadDesired
				&& !MyPersistentData.CurrentTileSource.IsDefault
				&& MyRuntimeData.IsConnectionAvailable
				&& TileCache.ProcessingQueue.IsFree;
		}
		#endregion updaters

		#region event handlers
		private bool _isDataChangedHandlerActive = false;
		private void AddHandlers_DataChanged()
		{
			if (!_isDataChangedHandlerActive)
			{
				_isDataChangedHandlerActive = true;
				MyPersistentData.PropertyChanged += OnPersistentData_PropertyChanged;
				MyRuntimeData.PropertyChanged += OnRuntimeData_PropertyChanged;
				TileCache.ProcessingQueue.PropertyChanged += OnProcessingQueue_PropertyChanged;
			}
		}
		private void RemoveHandlers_DataChanged()
		{
			if (MyPersistentData != null)
			{
				MyPersistentData.PropertyChanged -= OnPersistentData_PropertyChanged;
				MyRuntimeData.PropertyChanged -= OnRuntimeData_PropertyChanged;
				TileCache.ProcessingQueue.PropertyChanged -= OnProcessingQueue_PropertyChanged;
				_isDataChangedHandlerActive = false;
			}
		}
		private void OnPersistentData_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(PersistentData.IsShowDegrees))
			{
				MyPersistentData.Current.Latitude = MyPersistentData.Current.Latitude; // trigger PropertyChanged to make it reraw the fields bound to it
				MyPersistentData.Current.Longitude = MyPersistentData.Current.Longitude; // trigger PropertyChanged to make it reraw the fields bound to it
			}
			else if (e.PropertyName == nameof(PersistentData.IsKeepAlive))
			{
				KeepAlive.UpdateKeepAlive(MyPersistentData.IsKeepAlive);
			}
			else if (e.PropertyName == nameof(PersistentData.IsTilesDownloadDesired))
			{
				UpdateDownloadButtonIsEnabled();
			}
			else if (e.PropertyName == nameof(PersistentData.CurrentTileSource))
			{
				UpdateDownloadButtonIsEnabled();
				UpdateCacheButtonIsEnabled();
			}
			else if (e.PropertyName == nameof(PersistentData.TileSourcez))
			{
				UpdateClearCustomCacheButtonIsEnabled();
			}
		}

		private void OnRuntimeData_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(RuntimeData.IsConnectionAvailable))
			{
				UpdateDownloadButtonIsEnabled();
			}
		}
		private void OnProcessingQueue_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(TileCache.ProcessingQueue.IsFree))
			{
				UpdateClearCacheButtonIsEnabled();
				UpdateClearCustomCacheButtonIsEnabled();
				UpdateCacheButtonIsEnabled();
				UpdateDownloadButtonIsEnabled();
			}
		}

		#endregion event handlers

		#region services
		public void GoBackMyButtonSoft()
		{
			var args = new BackOrHardSoftKeyPressedEventArgs();
			BackOrHardSoftKeyPressed?.Invoke(this, args);
			if (!args.Handled) MyPersistentData.IsShowingPivot = false;
		}
		public void GoBackTabletSoft(object sender, BackRequestedEventArgs e)
		{
			if (MyPersistentData.IsBackButtonEnabled && e != null) e.Handled = true;
			var args = new BackOrHardSoftKeyPressedEventArgs();
			BackOrHardSoftKeyPressed?.Invoke(sender, args);
			if (!args.Handled) MyPersistentData.IsShowingPivot = false;
		}
		public void GoBackHard(object sender, BackPressedEventArgs e)
		{
			if (MyPersistentData.IsBackButtonEnabled && e != null) e.Handled = true;
			var args = new BackOrHardSoftKeyPressedEventArgs();
			BackOrHardSoftKeyPressed?.Invoke(sender, args);
			if (!args.Handled) MyPersistentData.IsShowingPivot = false;
		}
		public void SetLastMessage_UI(string message)
		{
			PersistentData.GetInstance().LastMessage = message;
		}
		public async void GetAFix()
		{
			var fix = await _myGPSInteractor.GetGeoLocationAsync().ConfigureAwait(false);
		}
		public async Task<int> TryClearCacheAsync(TileSourceRecord tileSource, bool isAlsoRemoveSources)
		{
			int howManyRecordsDeleted = 0;
			if (!tileSource.IsNone && !tileSource.IsDefault)
			{
				//MyPersistentData.IsMapCached = false; // stop caching if you want to delete the cache // no!

				howManyRecordsDeleted = await Task.Run(delegate
				{
					return TileCache.TryClearAsync(tileSource);
				}).ConfigureAwait(false);

				if (isAlsoRemoveSources && howManyRecordsDeleted >= 0)
				{
					await MyPersistentData.RemoveTileSourcesAsync(tileSource);
				}

				// output messages
				if (!tileSource.IsAll)
				{
					if (howManyRecordsDeleted > 0)
					{
						SetLastMessage_UI(howManyRecordsDeleted + " " + tileSource.DisplayName + " records deleted");
					}
					else if (howManyRecordsDeleted == 0)
					{
						if (isAlsoRemoveSources) SetLastMessage_UI(tileSource.DisplayName + " is gone");
						else SetLastMessage_UI(tileSource.DisplayName + " cache is empty");
					}
					else
					{
						SetLastMessage_UI(tileSource.DisplayName + " cache is busy");
					}
				}
				else if (tileSource.IsAll)
				{
					if (howManyRecordsDeleted > 0)
					{
						SetLastMessage_UI(howManyRecordsDeleted + " records deleted");
					}
					else if (howManyRecordsDeleted == 0)
					{
						SetLastMessage_UI("Cache empty");
					}
					else
					{
						SetLastMessage_UI("Cache busy");
					}
				}
			}
			return howManyRecordsDeleted;
		}
		public async Task<List<Tuple<int, int>>> GetHowManyTiles4DifferentZoomsAsync()
		{
			if (_myLolloMap_VM != null)
			{
				var output = await _myLolloMap_VM.GetHowManyTiles4DifferentZoomsAsync();
				return output;
			}
			else return new List<Tuple<int, int>>();
		}
		public void CancelDownloadByUser()
		{
			if (_myLolloMap_VM != null) _myLolloMap_VM.CancelDownloadByUser();
		}

		public async Task StartUserTestingTileSourceAsync() // when i click "test"
		{
			Tuple<bool, string> result = await MyPersistentData.TryInsertTestTileSourceIntoTileSourcezAsync();

			if (result?.Item1 == true)
			{
				TestTileSourceErrorMsg = string.Empty; // ok
				MyPersistentData.IsShowingPivot = false;
			}
			else TestTileSourceErrorMsg = result.Item2;         // error

			SetLastMessage_UI(result.Item2);
		}
		/// <summary>
		/// Makes sure the numbers make sense:
		/// their absolute value must not be too large or too small.
		/// </summary>
		/// <param name="dblIn"></param>
		/// <returns></returns>
		internal static double RoundAndRangeAltitude(double dblIn)
		{
			if (Math.Abs(dblIn) < MIN_ALTITUDE_ABS)
			{
				return 0.0;
			}
			else
			{
				if (dblIn > MAX_ALTITUDE_ABS) return MAX_ALTITUDE_ABS;
				if (dblIn < -MAX_ALTITUDE_ABS) return -MAX_ALTITUDE_ABS;
				return dblIn;
			}
		}
		#endregion services

		#region IMapApController
		public async Task CentreOnRoute0Async()
		{
			MyPersistentData.IsShowingPivot = false;
			if (_myAltitudeProfiles_VM != null) await _myAltitudeProfiles_VM.CentreOnRoute0Async();
			if (_myLolloMap_VM != null) await _myLolloMap_VM.CentreOnRoute0Async().ConfigureAwait(false);
		}
		public async Task CentreOnHistoryAsync()
		{
			MyPersistentData.IsShowingPivot = false;
			if (_myAltitudeProfiles_VM != null) await _myAltitudeProfiles_VM.CentreOnHistoryAsync();
			if (_myLolloMap_VM != null) await _myLolloMap_VM.CentreOnHistoryAsync().ConfigureAwait(false);
		}
		public async Task CentreOnLandmarksAsync()
		{
			MyPersistentData.IsShowingPivot = false;
			if (_myAltitudeProfiles_VM != null) await _myAltitudeProfiles_VM.CentreOnLandmarksAsync();
			if (_myLolloMap_VM != null) await _myLolloMap_VM.CentreOnLandmarksAsync().ConfigureAwait(false);
		}
		public async Task CentreOnTargetAsync()
		{
			MyPersistentData.IsShowingPivot = false;
			// await _myAltitudeProfiles_VM?.CentreOnTargetAsync(); // useless, just here to respect interface IMapApController
			if (_myLolloMap_VM != null) await _myLolloMap_VM.CentreOnTargetAsync().ConfigureAwait(false);
		}
		public Task Goto2DAsync()
		{
			MyPersistentData.IsShowingPivot = false;
			// _myAltitudeProfiles_VM.Goto2D(); // useless, just here to respect interface IMapApController
			return _myLolloMap_VM?.Goto2DAsync();
		}
		#endregion IMapApController

		#region save and load with picker
		private SemaphoreSlimSafeRelease _loadSaveSemaphore = new SemaphoreSlimSafeRelease(1, 1);
		internal CancellationTokenSource _fileSavePickerCts = null;
		internal CancellationTokenSource _fileOpenPickerCts = null;

		private async Task<bool> SetIsLoading(bool newValue)
		{
			bool isNewValUnlikeOldVal = false;
			try
			{
				await _loadSaveSemaphore.WaitAsync().ConfigureAwait(false);
				isNewValUnlikeOldVal = RegistryAccess.GetValue(ConstantData.RegIsLoadingFile) != newValue.ToString();
				if (isNewValUnlikeOldVal) RegistryAccess.SetValue(ConstantData.RegIsLoadingFile, newValue.ToString());
				IsLoading = newValue;
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(_loadSaveSemaphore);
			}
			return isNewValUnlikeOldVal;
		}

		private async Task<bool> SetIsSaving(bool newValue)
		{
			bool isNewValUnlikeOldVal = false;
			try
			{
				await _loadSaveSemaphore.WaitAsync().ConfigureAwait(false);
				isNewValUnlikeOldVal = RegistryAccess.GetValue(ConstantData.RegIsSavingFile) != newValue.ToString();
				if (isNewValUnlikeOldVal) RegistryAccess.SetValue(ConstantData.RegIsSavingFile, newValue.ToString());
				IsSaving = newValue;
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(_loadSaveSemaphore);
			}
			return isNewValUnlikeOldVal;
		}

		internal async Task PickSaveSeriesToFileAsync(PersistentData.Tables whichSeries, string fileNameSuffix)
		{
			if (!await SetIsSaving(true)) return;

			SwitchableObservableCollection<PointRecord> series = null;
			if (whichSeries == PersistentData.Tables.History) series = MyPersistentData.History;
			else if (whichSeries == PersistentData.Tables.Route0) series = MyPersistentData.Route0;
			else if (whichSeries == PersistentData.Tables.Landmarks) series = MyPersistentData.Landmarks;
			else return;

			// disable UI commands
			RuntimeData.SetIsDBDataRead_UI(false);
			SetLastMessage_UI("saving GPX file...");

			DateTime fileCreationDateTime = DateTime.Now;

			var file = await Pickers.PickSaveFileAsync(new string[] { ConstantData.GPX_EXTENSION }, fileCreationDateTime.ToString(ConstantData.GpxDateTimeFormat, CultureInfo.InvariantCulture) + fileNameSuffix).ConfigureAwait(false);

			if (file != null)
			{
				await SaveSeriesToFileAsync(series, whichSeries, fileCreationDateTime, file).ConfigureAwait(false);
			}
			else
			{
				SetLastMessage_UI("Saving cancelled");
			}

			// reactivate UI commands
			RuntimeData.SetIsDBDataRead_UI(true);

			await SetIsSaving(false).ConfigureAwait(false);
		}
		private async Task SaveSeriesToFileAsync(SwitchableObservableCollection<PointRecord> series, PersistentData.Tables whichSeries, DateTime fileCreationDateTime, StorageFile file)
		{
			if (_fileSavePickerCts != null) return;

			Tuple<bool, string> result = Tuple.Create(false, "");
			try
			{
				await Task.Run(async delegate
				{
					// initialise cancellation token
					_fileSavePickerCts = new CancellationTokenSource();
					CancellationToken token = _fileSavePickerCts.Token;
					token.ThrowIfCancellationRequested();
					// save file
					result = await ReaderWriter.SaveAsync(file, series, fileCreationDateTime, whichSeries, token).ConfigureAwait(false);
					token.ThrowIfCancellationRequested();
				}).ConfigureAwait(false);
			}
			catch (Exception) { }
			finally
			{
				// dispose of cancellation token
				_fileSavePickerCts?.Dispose();
				_fileSavePickerCts = null;
				// inform the user about the result
				if (result != null && result.Item1) SetLastMessage_UI(result.Item2);
				else SetLastMessage_UI(string.Format("could not save {0}", PersistentData.GetTextForSeries(whichSeries)));
			}
		}

		internal async Task PickLoadSeriesFromFileAsync(PersistentData.Tables whichSeries)
		{
			if (!await SetIsLoading(true)) return;

			//RegistryAccess.SetValue(ConstantData.RegWhichSeries, whichSeries.ToString());

			// disable UI commands
			RuntimeData.SetIsDBDataRead_UI(false);
			SetLastMessage_UI("reading GPX file...");

			var file = await Pickers.PickOpenFileAsync(new string[] { ConstantData.GPX_EXTENSION }).ConfigureAwait(false);
			if (file != null)
			{
				await LoadSeriesFromFileAsync(file, whichSeries).ConfigureAwait(false);
			}
			else
			{
				SetLastMessage_UI("Loading cancelled");
			}

			// reactivate UI commands
			RuntimeData.SetIsDBDataRead_UI(true);

			await SetIsLoading(false).ConfigureAwait(false);
		}

		// LOLLO TODO check https://social.msdn.microsoft.com/Forums/sqlserver/en-US/13002ba6-6e59-47b8-a746-c05525953c5a/uwpfileopenpicker-bugs-in-win-10-mobile-when-not-debugging?forum=wpdevelop
		// for picker details

		private async Task LoadSeriesFromFileAsync(StorageFile file, PersistentData.Tables whichSeries)
		{
			if (_fileOpenPickerCts != null || file == null || whichSeries == PersistentData.Tables.nil) return;

			Tuple<bool, string> result = Tuple.Create(false, "");
			try
			{
				await Task.Run(async delegate
				{
					// initialise cancellation token
					_fileOpenPickerCts = new CancellationTokenSource();
					CancellationToken token = _fileOpenPickerCts.Token;
					token.ThrowIfCancellationRequested();
					// load the file
					result = await ReaderWriter.LoadSeriesFromFileIntoDbAsync(file, whichSeries, token).ConfigureAwait(false);
					token.ThrowIfCancellationRequested();
					// update the UI with the file data
					if (result?.Item1 == true)
					{
						switch (whichSeries)
						{
							case PersistentData.Tables.History:
								break;
							case PersistentData.Tables.Route0:
								await MyPersistentData.LoadRoute0FromDbAsync(false).ConfigureAwait(false);
								await CentreOnRoute0Async().ConfigureAwait(false);
								break;
							case PersistentData.Tables.Landmarks:
								await MyPersistentData.LoadLandmarksFromDbAsync(false).ConfigureAwait(false);
								await CentreOnLandmarksAsync().ConfigureAwait(false);
								break;
							case PersistentData.Tables.nil:
								break;
							default:
								break;
						}
					}
				}).ConfigureAwait(false);
			}
			catch (Exception) { }
			finally
			{
				// dispose of cancellation token
				_fileOpenPickerCts?.Dispose();
				_fileOpenPickerCts = null;
				// inform the user about the outcome
				if (result?.Item1 == true)
				{
					SetLastMessage_UI(result.Item2);
					MyPersistentData.IsShowingPivot = false;
				}
				else SetLastMessage_UI("could not read file");
			}
		}
		#endregion save and load with picker

		#region continuations
		internal CancellationTokenSource _fileOpenContinuationCts = null;
		public async Task<List<PersistentData.Tables>> LoadFileIntoDbAsync(FileActivatedEventArgs args)
		{
			List<PersistentData.Tables> output = new List<PersistentData.Tables>();

			if (args?.Files?.Count > 0 && args.Files[0] is StorageFile && _fileOpenContinuationCts == null)
			{
				Tuple<bool, string> landmarksResult = Tuple.Create(false, "");
				Tuple<bool, string> route0Result = Tuple.Create(false, "");

				try
				{
					// initialise cancellation token
					_fileOpenContinuationCts = new CancellationTokenSource();
					CancellationToken token = _fileOpenContinuationCts.Token;
					SetLastMessage_UI("reading GPX file...");
					// load the file, attempting to read landmarks and route. GPX files can contain both.
					StorageFile file_mt = args.Files[0] as StorageFile;
					token.ThrowIfCancellationRequested();
					landmarksResult = await ReaderWriter.LoadSeriesFromFileIntoDbAsync(file_mt, PersistentData.Tables.Landmarks, token).ConfigureAwait(false);
					token.ThrowIfCancellationRequested();
					route0Result = await ReaderWriter.LoadSeriesFromFileIntoDbAsync(file_mt, PersistentData.Tables.Route0, token).ConfigureAwait(false);
				}
				catch (Exception) { }
				finally
				{
					// dispose of cancellation token
					_fileOpenContinuationCts?.Dispose();
					_fileOpenContinuationCts = null;
					// inform the user about the result
					if ((landmarksResult == null || !landmarksResult.Item1) && (route0Result == null || !route0Result.Item1)) SetLastMessage_UI("could not read file");
					else SetLastMessage_UI(route0Result.Item2 + " and " + landmarksResult.Item2);
					// fill output
					if (landmarksResult?.Item1 == true) output.Add(PersistentData.Tables.Landmarks);
					if (route0Result?.Item1 == true) output.Add(PersistentData.Tables.Route0);
				}
			}

			return output;
		}
		#endregion continuations
	}
	public interface IMapApController
	{
		Task CentreOnHistoryAsync();
		Task CentreOnLandmarksAsync();
		Task CentreOnRoute0Async();
		Task CentreOnTargetAsync();
		Task Goto2DAsync();
	}
}
