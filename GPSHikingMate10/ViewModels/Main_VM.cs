﻿using GPX;
using LolloBaseUserControls;
using LolloGPS.Data;
using LolloGPS.Data.Constants;
using LolloGPS.Data.Runtime;
using LolloGPS.Data.TileCache;
using LolloGPS.GPSInteraction;
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

namespace LolloGPS.Core
{
	public sealed class Main_VM : ObservableData, BackPressedRaiser, IMapApController
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
		}
		internal async Task ActivateAsync()
		{
			//bool isTesting = true; // LOLLO remove when done testing
			//if (isTesting)
			//{
			//    for (long i = 0; i < 100000000; i++) //wait a few seconds, for testing
			//    {
			//        String aaa = i.ToString();
			//    }
			//}

			await _myGPSInteractor.ActivateAsync();
			UpdateClearCacheButtonIsEnabled();
			UpdateClearCustomCacheButtonIsEnabled();
			UpdateCacheButtonIsEnabled();
			UpdateDownloadButtonIsEnabled();
			AddHandler_DataChanged();
		}
		internal void Deactivate()
		{
			RemoveHandler_DataChanged();
			_myGPSInteractor.Deactivate();

			//bool isTesting = true; // LOLLO remove when done testing
			//if (isTesting)
			//{
			//    for (long i = 0; i < 100000000; i++) //wait a few seconds, for testing
			//    {
			//        String aaa = i.ToString();
			//    }
			//}
		}
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
		private Boolean _isDataChangedHandlerActive = false;
		private void AddHandler_DataChanged()
		{
			if (!_isDataChangedHandlerActive)
			{
				MyPersistentData.PropertyChanged += OnPersistentData_PropertyChanged;
				MyRuntimeData.PropertyChanged += OnRuntimeData_PropertyChanged;
				TileCache.ProcessingQueue.PropertyChanged += OnProcessingQueue_PropertyChanged;
				_isDataChangedHandlerActive = true;
			}
		}
		private void RemoveHandler_DataChanged()
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
			await _myAltitudeProfiles_VM?.CentreOnRoute0Async();
			if (_myLolloMap_VM != null) await _myLolloMap_VM.CentreOnRoute0Async().ConfigureAwait(false);
		}
		public async Task CentreOnHistoryAsync()
		{
			MyPersistentData.IsShowingPivot = false;
			await _myAltitudeProfiles_VM?.CentreOnHistoryAsync();
			if (_myLolloMap_VM != null) await _myLolloMap_VM.CentreOnHistoryAsync().ConfigureAwait(false);
		}
		public async Task CentreOnLandmarksAsync()
		{
			MyPersistentData.IsShowingPivot = false;
			await _myAltitudeProfiles_VM?.CentreOnLandmarksAsync();
			if (_myLolloMap_VM != null) await _myLolloMap_VM.CentreOnLandmarksAsync().ConfigureAwait(false);
		}
		public async Task CentreOnTargetAsync()
		{
			MyPersistentData.IsShowingPivot = false;
			// await _myAltitudeProfiles_VM?.CentreOnTargetAsync(); // useless, just here tor espect interface IMapApController
			if (_myLolloMap_VM != null) await _myLolloMap_VM.CentreOnTargetAsync().ConfigureAwait(false);
		}
		public void Goto2D()
		{
			MyPersistentData.IsShowingPivot = false;
			// _myAltitudeProfiles_VM.Goto2D(); // useless, just here tor espect interface IMapApController
			_myLolloMap_VM.Goto2D();
		}
		#endregion IMapApController

		#region save and load with picker
		internal CancellationTokenSource _fileSavePickerCts = null;
		internal async Task PickSaveSeriesToFileAsync(PersistentData.Tables whichSeries, string fileNameSuffix)
		{
			FileSavePicker savePicker = new FileSavePicker();
			string dateTimeFormat = "yyyyMMddTHHmmssZ";
			DateTime fileCreationDateTime = DateTime.Now;
			savePicker.SuggestedFileName = fileCreationDateTime.ToString(dateTimeFormat, CultureInfo.InvariantCulture) + fileNameSuffix;
			savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
			savePicker.FileTypeChoices.Add("GPX", new List<string>() { ConstantData.GPX_EXTENSION });

			StorageFile file = await savePicker.PickSaveFileAsync();
			if (file != null)
			{
				await SaveSeriesToFileAsync(whichSeries, fileCreationDateTime, file).ConfigureAwait(false); // TODO check this with a win 10 phone
			}
			else
			{
				SetLastMessage_UI("Saving cancelled");
			}
		}
		private async Task SaveSeriesToFileAsync(PersistentData.Tables whichSeries, DateTime fileCreationDateTime, StorageFile file)
		{
			if (_fileSavePickerCts != null) return;

			Tuple<bool, string> result = Tuple.Create(false, "");
			try
			{
				SwitchableObservableCollection<PointRecord> series = null;
				if (whichSeries == PersistentData.Tables.History) series = MyPersistentData.History;
				else if (whichSeries == PersistentData.Tables.Route0) series = MyPersistentData.Route0;
				else if (whichSeries == PersistentData.Tables.Landmarks) series = MyPersistentData.Landmarks;
				else return;
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

		internal CancellationTokenSource _fileOpenPickerCts = null;
		internal async Task PickLoadSeriesFromFileAsync(PersistentData.Tables whichSeries)
		{
			FileOpenPicker openPicker = new FileOpenPicker();
			openPicker.ViewMode = PickerViewMode.List;//.Thumbnail;
			openPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
			openPicker.FileTypeFilter.Add(ConstantData.GPX_EXTENSION); //LOLLO I could add many more extensions here, and turn it into a file explorer...
			try
			{
				StorageFile file = await openPicker.PickSingleFileAsync(); // LOLLO TODO this works on a win10 phone but 1000 landmarks are too many: check available memory and reduce max landkarks to match.
																		   // check https://msdn.microsoft.com/en-us/library/windows/apps/jj681682(v=vs.105).aspx
				await LoadSeriesFromFileAsync(file, whichSeries);
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex.ToString());
				MyPersistentData.LastMessage = string.Format("error loading {0}", PersistentData.GetTextForSeries(whichSeries));
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}
		}
		private async Task LoadSeriesFromFileAsync(StorageFile file, PersistentData.Tables whichSeries)
		{
			if (_fileOpenPickerCts != null) return;

			Tuple<bool, string> result = Tuple.Create(false, "");
			try
			{
				await Task.Run(async delegate
				{
					// initialise cancellation token
					_fileOpenPickerCts = new CancellationTokenSource();
					CancellationToken token = _fileOpenPickerCts.Token;
					token.ThrowIfCancellationRequested();
					// disable UI commands
					RuntimeData.SetIsDBDataRead_UI(false);
					SetLastMessage_UI("reading GPX file...");                    
					// load the file
					result = await ReaderWriter.LoadSeriesFromFileIntoDbAsync(file, whichSeries, token);
					token.ThrowIfCancellationRequested();
					// update the UI with the file data
					if (result?.Item1 == true)
					{
						switch (whichSeries)
						{
							case PersistentData.Tables.History:
								break;
							case PersistentData.Tables.Route0:
								await MyPersistentData.LoadRoute0FromDbAsync(false); //.ConfigureAwait(false);
								await CentreOnRoute0Async().ConfigureAwait(false);
								break;
							case PersistentData.Tables.Landmarks:
								await MyPersistentData.LoadLandmarksFromDbAsync(false); //.ConfigureAwait(false);
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
				// reactivate UI commands
				RuntimeData.SetIsDBDataRead_UI(true);
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
		internal async Task<List<PersistentData.Tables>> LoadFileIntoDbAsync(FileActivatedEventArgs args)
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
					landmarksResult = await ReaderWriter.LoadSeriesFromFileIntoDbAsync(file_mt, PersistentData.Tables.Landmarks, token).ConfigureAwait(false); // TODO maybe do not show the "loading..." opaque overlay, deactivate the landmarks buttons instead.
					token.ThrowIfCancellationRequested();
					route0Result = await ReaderWriter.LoadSeriesFromFileIntoDbAsync(file_mt, PersistentData.Tables.Route0, token).ConfigureAwait(false); // TODO maybe do not show the "loading..." opaque overlay, deactivate the routes and save history buttons instead
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
		void Goto2D();
	}
}
