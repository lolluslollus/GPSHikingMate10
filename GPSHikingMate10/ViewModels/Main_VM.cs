using GPX;
using Utilz.Controlz;
using LolloGPS.Data;
using LolloGPS.Data.Runtime;
using LolloGPS.Data.TileCache;
using LolloGPS.GPSInteraction;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Utilz;
using Utilz.Data;
using Windows.ApplicationModel.Activation;
using Windows.Phone.UI.Input;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using System.Threading;

namespace LolloGPS.Core
{
	public sealed class MainVM : OpenableObservableData, IBackPressedRaiser, IMapApController //, IFileActivatable
	{
		#region IBackPressedRaiser
		public event EventHandler<BackOrHardSoftKeyPressedEventArgs> BackOrHardSoftKeyPressed;
		#endregion IBackPressedRaiser


		#region properties
		private const double MIN_ALTITUDE_M_ABS = .1;
		private const double MAX_ALTITUDE_M_ABS = 10000.0;
		//private static readonly double MIN_ALTITUDE_FT_ABS = MIN_ALTITUDE_M_ABS * ConstantData.M_TO_FOOT;
		private static readonly double MAX_ALTITUDE_FT_ABS = MAX_ALTITUDE_M_ABS * ConstantData.M_TO_FOOT;


		private LolloMapVM _lolloMapVM = null;
		public LolloMapVM LolloMapVM { get { return _lolloMapVM; } set { _lolloMapVM = value; RaisePropertyChanged_UI(); } }

		private AltitudeProfilesVM _altitudeProfilesVM = null;
		public AltitudeProfilesVM AltitudeProfilesVM { get { return _altitudeProfilesVM; } set { _altitudeProfilesVM = value; RaisePropertyChanged_UI(); } }

		public PersistentData PersistentData { get { return App.PersistentData; } }
		public RuntimeData RuntimeData { get { return App.RuntimeData; } }

		private readonly TileCacheClearer _tileCacheClearer = null;

		private readonly GPSInteractor _gpsInteractor = null;
		public GPSInteractor GPSInteractor { get { return _gpsInteractor; } }

		// the following bools should be volatile, instead we choose to only read and write them in the UI thread.
		private bool _isClearCustomCacheEnabled = false;
		public bool IsClearCustomCacheEnabled { get { return _isClearCustomCacheEnabled; } private set { if (_isClearCustomCacheEnabled != value) { _isClearCustomCacheEnabled = value; RaisePropertyChanged(); } } }
		private bool _isClearCacheEnabled = false;
		public bool IsClearCacheEnabled { get { return _isClearCacheEnabled; } private set { if (_isClearCacheEnabled != value) { _isClearCacheEnabled = value; RaisePropertyChanged(); } } }
		private bool _isCacheBtnEnabled = false;
		public bool IsCacheBtnEnabled { get { return _isCacheBtnEnabled; } private set { if (_isCacheBtnEnabled != value) { _isCacheBtnEnabled = value; RaisePropertyChanged(); } } }
		private bool _isLeechingEnabled = false;
		public bool IsLeechingEnabled { get { return _isLeechingEnabled; } private set { if (_isLeechingEnabled != value) { _isLeechingEnabled = value; RaisePropertyChanged(); } } }
		private bool _isTestBtnEnabled = false;
		public bool IsTestCustomTileSourceEnabled { get { return _isTestBtnEnabled; } private set { if (_isTestBtnEnabled != value) { _isTestBtnEnabled = value; RaisePropertyChanged(); } } }
		private bool _isChangeTileSourceEnabled = false;
		public bool IsChangeTileSourceEnabled { get { return _isChangeTileSourceEnabled; } private set { if (_isChangeTileSourceEnabled != value) { _isChangeTileSourceEnabled = value; RaisePropertyChanged(); } } }

		private string _testTileSourceErrorMsg = "";
		public string TestTileSourceErrorMsg { get { return _testTileSourceErrorMsg; } private set { _testTileSourceErrorMsg = value; RaisePropertyChanged_UI(); } }

		private bool _isWideEnough = false;
		public bool IsWideEnough { get { return _isWideEnough; } set { if (_isWideEnough != value) { _isWideEnough = value; RaisePropertyChanged_UI(); } } }

		private readonly object _isMessageVisibleLocker = new object();
		private bool _isLastMessageVisible = false;
		public bool IsLastMessageVisible
		{
			get
			{
				lock (_isMessageVisibleLocker)
				{
					return _isLastMessageVisible;
				}
			}
			set
			{
				lock (_isMessageVisibleLocker)
				{
					if (_isLastMessageVisible != value) { _isLastMessageVisible = value; RaisePropertyChanged_UI(); }
				}
			}
		}

		private readonly object _loadSaveLocker = new object();
		public bool IsLoading
		{
			get
			{
				lock (_loadSaveLocker)
				{
					return RegistryAccess.GetValue(ConstantData.REG_LOAD_SERIES_IS_LOADING) == true.ToString();
				}
			}
			private set
			{
				lock (_loadSaveLocker)
				{
					if (RegistryAccess.TrySetValue(ConstantData.REG_LOAD_SERIES_IS_LOADING, value.ToString()))
					{
						RaisePropertyChangedUrgent_UI();
					}
				}
			}
		}
		private bool TrySetIsLoading(bool newValue)
		{
			lock (_loadSaveLocker)
			{
				if (IsLoading == newValue) return false;
				else
				{
					IsLoading = newValue;
					return true;
				}
			}
		}

		public bool IsSaving
		{
			get
			{
				lock (_loadSaveLocker)
				{
					return RegistryAccess.GetValue(ConstantData.REG_SAVE_SERIES_IS_SAVING) == true.ToString();
				}
			}
			private set
			{
				lock (_loadSaveLocker)
				{
					if (RegistryAccess.TrySetValue(ConstantData.REG_SAVE_SERIES_IS_SAVING, value.ToString()))
					{
						RaisePropertyChangedUrgent_UI();
					}
				}
			}
		}
		private bool TrySetIsSaving(bool newValue)
		{
			lock (_loadSaveLocker)
			{
				if (IsSaving == newValue) return false;
				else
				{
					IsSaving = newValue;
					return true;
				}
			}
		}

		private string _logText;
		public string LogText { get { return _logText; } set { _logText = value; RaisePropertyChanged_UI(); } }

		private PersistentData.Tables _whichSeriesJustLoaded = PersistentData.Tables.nil;
		public PersistentData.Tables WhichSeriesJustLoaded { get { return _whichSeriesJustLoaded; } }
		#endregion properties

		#region construct and dispose
		public MainVM(bool isWideEnough)
		{
			_gpsInteractor = GPSInteractor.GetInstance(PersistentData);
			_tileCacheClearer = TileCacheClearer.GetInstance();
			IsWideEnough = isWideEnough;
		}

		protected override async Task OpenMayOverrideAsync()
		{
			try
			{
				_whichSeriesJustLoaded = PersistentData.Tables.nil;

				await _gpsInteractor.OpenAsync();
				RuntimeData.GetInstance().IsAllowCentreOnCurrent = true;
				AddHandlers_DataChanged();

				UpdateIsClearCacheEnabled();
				UpdateIsClearCustomCacheEnabled();
				UpdateIsCacheBtnEnabled();
				UpdateIsLeechingEnabled();
				UpdateIsChangeTileSourceEnabled();
				UpdateIsTestCustomTileSourceEnabled();

				await RunInUiThreadAsync(delegate
				{
					KeepAlive.UpdateKeepAlive(PersistentData.IsKeepAlive);
				}).ConfigureAwait(false);

				if (IsLoading)
				{
					var file = await Pickers.GetLastPickedOpenFileAsync().ConfigureAwait(false);
					if (file != null)
					{
						PersistentData.Tables whichSeries = PersistentData.Tables.nil;
						if (Enum.TryParse(
							RegistryAccess.GetValue(ConstantData.REG_LOAD_SERIES_WHICH_SERIES),
							out whichSeries))
						{
							await ContinueAfterPickLoadSeriesFromFileAsync(file, whichSeries).ConfigureAwait(false);
						}
					}
				}
				if (IsSaving)
				{
					var file = await Pickers.GetLastPickedSaveFileAsync().ConfigureAwait(false);
					if (file != null)
					{
						PersistentData.Tables whichSeries = PersistentData.Tables.nil;
						if (Enum.TryParse(
							RegistryAccess.GetValue(ConstantData.REG_SAVE_SERIES_WHICH_SERIES),
							out whichSeries))
						{
							DateTime fileCreationDateTime = default(DateTime);
							if (DateTime.TryParseExact(
								RegistryAccess.GetValue(ConstantData.REG_SAVE_SERIES_FILE_CREATION_DATE_TIME),
								ConstantData.GPX_DATE_TIME_FORMAT,
								CultureInfo.CurrentUICulture,
								DateTimeStyles.None,
								out fileCreationDateTime))
							{
								var series = PersistentData.GetSeries(whichSeries);
								await ContinueAfterPickSaveSeriesToFileAsync(series, whichSeries, fileCreationDateTime, file).ConfigureAwait(false);
							}
						}
					}
				}

				Logger.Add_TPL("MainVM.OpenMayOverrideAsync() ran OK", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename);
			}
			finally
			{
				IsLoading = false;
				IsSaving = false;
				// _whichSeriesJustLoaded = PersistentData.Tables.nil;
			}
		}
		protected override async Task CloseMayOverrideAsync()
		{
			try
			{
				RemoveHandlers_DataChanged();
				await RunInUiThreadAsync(delegate
				{
					KeepAlive.StopKeepAlive();
				}).ConfigureAwait(false);
				await _gpsInteractor.CloseAsync().ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename);
			}
		}
		#endregion construct and dispose

		#region updaters
		internal void UpdateIsClearCustomCacheEnabled()
		{
			Task ui = RunInUiThreadAsync(delegate
			{
				IsClearCustomCacheEnabled =
				PersistentData.TileSourcez.FirstOrDefault(ts => ts.IsDeletable) != null && // not atomic, not volatile, not critical
				!_tileCacheClearer.IsClearingScheduled
				&& !PersistentData.IsTileSourcezBusy;
			});
		}
		internal void UpdateIsClearCacheEnabled()
		{
			Task ui = RunInUiThreadAsync(delegate
			{
				IsClearCacheEnabled = !_tileCacheClearer.IsClearingScheduled
				&& !PersistentData.IsTileSourcezBusy;
			});
		}
		internal void UpdateIsCacheBtnEnabled()
		{
			Task ui = RunInUiThreadAsync(delegate
			{
				IsCacheBtnEnabled =
					PersistentData.CurrentTileSource?.IsDefault == false;
				// && !TileCacheProcessingQueue.GetInstance().IsClearingScheduled;
			});
		}
		internal void UpdateIsLeechingEnabled()
		{
			Task ui = RunInUiThreadAsync(delegate
			{
				IsLeechingEnabled = !PersistentData.IsTilesDownloadDesired
				&& PersistentData.CurrentTileSource?.IsDefault == false
				&& RuntimeData.IsConnectionAvailable
				&& !_tileCacheClearer.IsClearingScheduled
				&& !PersistentData.IsTileSourcezBusy;
			});
		}
		internal void UpdateIsChangeTileSourceEnabled()
		{
			Task ui = RunInUiThreadAsync(delegate
			{
				IsChangeTileSourceEnabled = !_tileCacheClearer.IsClearingScheduled
				&& !PersistentData.IsTileSourcezBusy;
			});
		}
		internal void UpdateIsTestCustomTileSourceEnabled()
		{
			Task ui = RunInUiThreadAsync(delegate
			{
				IsTestCustomTileSourceEnabled = !_tileCacheClearer.IsClearingScheduled
				&& !PersistentData.IsTileSourcezBusy
				&& RuntimeData.IsConnectionAvailable;
			});
		}

		#endregion updaters

		#region event handlers
		private bool _isDataChangedHandlerActive = false;
		private void AddHandlers_DataChanged()
		{
			if (!_isDataChangedHandlerActive)
			{
				_isDataChangedHandlerActive = true;
				PersistentData.PropertyChanged += OnPersistentData_PropertyChanged;
				RuntimeData.PropertyChanged += OnRuntimeData_PropertyChanged;
				TileCacheClearer.IsClearingScheduledChanged += OnTileCache_IsClearingScheduledChanged;
				TileCacheClearer.CacheCleared += OnTileCache_CacheCleared;
			}
		}

		private void RemoveHandlers_DataChanged()
		{
			if (PersistentData != null)
			{
				PersistentData.PropertyChanged -= OnPersistentData_PropertyChanged;
				RuntimeData.PropertyChanged -= OnRuntimeData_PropertyChanged;
				TileCacheClearer.IsClearingScheduledChanged -= OnTileCache_IsClearingScheduledChanged;
				TileCacheClearer.CacheCleared -= OnTileCache_CacheCleared;
				_isDataChangedHandlerActive = false;
			}
		}
		private void OnPersistentData_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(PersistentData.IsKeepAlive))
			{
				Task ka = RunInUiThreadAsync(delegate
				{
					KeepAlive.UpdateKeepAlive(PersistentData.IsKeepAlive);
				});
			}
			else if (e.PropertyName == nameof(PersistentData.IsTilesDownloadDesired))
			{
				Task gt = RunInUiThreadAsync(delegate
				{
					UpdateIsLeechingEnabled();
					//UpdateTestButtonIsEnabled();
				});
			}
			else if (e.PropertyName == nameof(PersistentData.CurrentTileSource))
			{
				Task gt = RunInUiThreadAsync(delegate
				{
					UpdateIsLeechingEnabled();
					UpdateIsCacheBtnEnabled();
				});
			}
			else if (e.PropertyName == nameof(PersistentData.TileSourcez))
			{
				Task gt = RunInUiThreadAsync(delegate
				{
					UpdateIsClearCustomCacheEnabled();
				});
			}
			else if (e.PropertyName == nameof(PersistentData.IsTileSourcezBusy))
			{
				Task gt = RunInUiThreadAsync(delegate
				{
					UpdateIsClearCacheEnabled();
					UpdateIsClearCustomCacheEnabled();
					UpdateIsLeechingEnabled();
					UpdateIsChangeTileSourceEnabled();
					UpdateIsTestCustomTileSourceEnabled();
				});
			}
		}

		private void OnRuntimeData_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(RuntimeData.IsConnectionAvailable))
			{
				Task gt = RunInUiThreadAsync(delegate
				{
					UpdateIsLeechingEnabled();
					UpdateIsTestCustomTileSourceEnabled();
				});
			}
		}

		private void OnTileCache_IsClearingScheduledChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			UpdateIsClearCacheEnabled();
			UpdateIsClearCustomCacheEnabled();
			UpdateIsLeechingEnabled();
			UpdateIsChangeTileSourceEnabled();
			UpdateIsTestCustomTileSourceEnabled();
		}
		private void OnTileCache_CacheCleared(object sender, TileCacheClearer.CacheClearedEventArgs args)
		{
			// output messages
			if (args.TileSource.IsAll)
			{
				if (args.HowManyRecordsDeleted > 0)
				{
					PersistentData.LastMessage = (args.HowManyRecordsDeleted + " records deleted");
				}
				else if (args.IsCacheCleared)
				{
					PersistentData.LastMessage = ("Cache empty");
				}
				else
				{
					PersistentData.LastMessage = ("Cache busy");
				}
			}
			else
			{
				if (args.HowManyRecordsDeleted > 0)
				{
					PersistentData.LastMessage = (args.HowManyRecordsDeleted + " " + args.TileSource.DisplayName + " records deleted");
				}
				else if (args.IsCacheCleared)
				{
					PersistentData.LastMessage = (args.TileSource.DisplayName + " cache is empty");
				}
				else
				{
					PersistentData.LastMessage = (args.TileSource.DisplayName + " cache is busy");
				}
			}
		}
		#endregion event handlers

		#region services
		public void GoBackMyButtonSoft()
		{
			var args = new BackOrHardSoftKeyPressedEventArgs();
			BackOrHardSoftKeyPressed?.Invoke(this, args);
			if (!args.Handled) PersistentData.IsShowingPivot = false;
		}
		public void GoBackTabletSoft(object sender, BackRequestedEventArgs e)
		{
			if (PersistentData.IsBackButtonEnabled && e != null) e.Handled = true;
			var args = new BackOrHardSoftKeyPressedEventArgs();
			BackOrHardSoftKeyPressed?.Invoke(sender, args);
			if (!args.Handled) PersistentData.IsShowingPivot = false;
		}
		public void GoBackHard(object sender, BackPressedEventArgs e)
		{
			if (PersistentData.IsBackButtonEnabled && e != null) e.Handled = true;
			var args = new BackOrHardSoftKeyPressedEventArgs();
			BackOrHardSoftKeyPressed?.Invoke(sender, args);
			if (!args.Handled) PersistentData.IsShowingPivot = false;
		}
		public void SetLastMessage_UI(string message)
		{
			App.PersistentData.LastMessage = message;
		}
		public void GetAFix()
		{
			var gpsInt = _gpsInteractor;
			if (gpsInt != null)
			{
				Task getLoc = Task.Run(gpsInt.GetGeoLocationAppendingHistoryAsync);
			}
		}
		public async Task ScheduleClearCacheAsync(TileSourceRecord tileSource, bool isAlsoRemoveSources)
		{
			bool isScheduled = await Task.Run(delegate { return _tileCacheClearer.TryScheduleClearCacheAsync(tileSource, isAlsoRemoveSources); }).ConfigureAwait(false);
			if (isScheduled) PersistentData.LastMessage = "cache will be cleared asap";
			else PersistentData.LastMessage = "cache busy";

			//PersistentData.IsMapCached = false; // stop caching if you want to delete the cache // no!
		}

		public async Task<List<Tuple<int, int>>> GetHowManyTiles4DifferentZoomsAsync()
		{
			var result = new List<Tuple<int, int>>();
			await RunFunctionIfOpenAsyncT(async delegate
			{
				var lmVM = _lolloMapVM;
				if (lmVM != null) result = await lmVM.GetHowManyTiles4DifferentZoomsAsync();
			}).ConfigureAwait(false);
			return result;
		}
		public void CancelDownloadByUser()
		{
			_lolloMapVM?.CancelDownloadByUser();
		}

		public Task StartUserTestingTileSourceAsync()
		{
			return RunFunctionIfOpenAsyncT(async delegate
			{
				Tuple<bool, string> result = await PersistentData.TryInsertTestTileSourceIntoTileSourcezAsync();

				if (result?.Item1 == true)
				{
					TestTileSourceErrorMsg = string.Empty; // ok
					PersistentData.IsShowingPivot = false;
				}
				else TestTileSourceErrorMsg = result.Item2; // error

				SetLastMessage_UI(result.Item2);
			});
		}
		/// <summary>
		/// Makes sure the numbers make sense:
		/// their absolute value must not be too large or too small.
		/// </summary>
		/// <param name="dblIn"></param>
		/// <returns></returns>
		internal static double RoundAndRangeAltitude(double dblIn, bool isImperialUnits)
		{
			if (Math.Abs(dblIn) < MIN_ALTITUDE_M_ABS)
			{
				return 0.0;
			}
			else
			{
				if (isImperialUnits)
				{
					if (dblIn > MAX_ALTITUDE_M_ABS) return MAX_ALTITUDE_FT_ABS;
					if (dblIn < -MAX_ALTITUDE_M_ABS) return -MAX_ALTITUDE_FT_ABS;
					return dblIn * ConstantData.M_TO_FOOT;
				}
				else
				{
					if (dblIn > MAX_ALTITUDE_M_ABS) return MAX_ALTITUDE_M_ABS;
					if (dblIn < -MAX_ALTITUDE_M_ABS) return -MAX_ALTITUDE_M_ABS;
					return dblIn;
				}
			}
		}
		internal void NavigateToUri(string uri)
		{
			try
			{
				if (!string.IsNullOrWhiteSpace(uri))
				{
					var ub = new UriBuilder(uri);
					Task launch = Launcher.LaunchUriAsync(ub.Uri/*, new LauncherOptions() { DesiredRemainingView = ViewSizePreference.UseHalf }*/).AsTask();
				}
			}
			catch { }
		}
		public async Task SetTargetToCurrentAsync()
		{
			GPSInteractor gpsInteractor = GPSInteractor.GetInstance(PersistentData);
			if (gpsInteractor == null) return;

			Task vibrate = Task.Run(() => App.ShortVibration());

			var currrent = await gpsInteractor.GetGeoLocationAppendingHistoryAsync();
			var persistentData = PersistentData;
			if (currrent != null && persistentData != null)
			{
				Task upd = currrent.UpdateUIEditablePropertiesAsync(persistentData.Target, PersistentData.Tables.History).ContinueWith(delegate
				{
					PointRecord currentClone = null;
					PointRecord.Clone(currrent, ref currentClone);
					Task add = persistentData.TryAddPointToCheckpointsAsync(currentClone);
				});
			}
		}
		public Task SetTilesDownloadPropsAsync(int maxZoom)
		{
			return RunFunctionIfOpenAsyncT(async delegate
			{
				await PersistentData.SetTilesDownloadPropsAsync(true, maxZoom, false).ConfigureAwait(false);
				PersistentData.IsShowingPivot = false;
			});
		}
		public Task SetCurrentTileSourceAsync(TileSourceRecord tileSource)
		{
			return RunFunctionIfOpenAsyncT(delegate
			{
				return PersistentData.SetCurrentTileSourceAsync(tileSource);
			});
		}
		#endregion services


		#region IMapApController
		public Task CentreOnRoute0Async()
		{
			PersistentData.IsShowingPivot = false;
			Task alt = _altitudeProfilesVM?.CentreOnRoute0Async() ?? Task.CompletedTask;
			Task map = _lolloMapVM?.CentreOnRoute0Async() ?? Task.CompletedTask;
			return Task.WhenAll(alt, map);
		}
		public Task CentreOnHistoryAsync()
		{
			PersistentData.IsShowingPivot = false;
			Task alt = _altitudeProfilesVM?.CentreOnHistoryAsync() ?? Task.CompletedTask;
			Task map = _lolloMapVM?.CentreOnHistoryAsync() ?? Task.CompletedTask;
			return Task.WhenAll(alt, map);
		}
		public Task CentreOnCheckpointsAsync()
		{
			PersistentData.IsShowingPivot = false;
			Task alt = _altitudeProfilesVM?.CentreOnCheckpointsAsync() ?? Task.CompletedTask;
			Task map = _lolloMapVM?.CentreOnCheckpointsAsync() ?? Task.CompletedTask;
			return Task.WhenAll(alt, map);
		}
		public Task CentreOnSeriesAsync(PersistentData.Tables series)
		{
			if (series == PersistentData.Tables.History) return CentreOnHistoryAsync();
			else if (series == PersistentData.Tables.Route0) return CentreOnRoute0Async();
			else if (series == PersistentData.Tables.Checkpoints) return CentreOnCheckpointsAsync();
			else return Task.CompletedTask;
		}
		public Task CentreOnTargetAsync()
		{
			PersistentData.IsShowingPivot = false;
			// await _myAltitudeProfiles_VM?.CentreOnTargetAsync(); // useless
			return _lolloMapVM?.CentreOnTargetAsync();
		}
		public Task CentreOnCurrentAsync()
		{
			PersistentData.IsShowingPivot = false;
			Task cenA = _altitudeProfilesVM?.CentreOnCurrentAsync() ?? Task.CompletedTask;
			Task cenM = _lolloMapVM?.CentreOnCurrentAsync() ?? Task.CompletedTask;
			return Task.WhenAll(cenA, cenM);
		}
		public Task Goto2DAsync()
		{
			PersistentData.IsShowingPivot = false;
			Task alt = _altitudeProfilesVM?.Goto2DAsync() ?? Task.CompletedTask;
			Task map = _lolloMapVM?.Goto2DAsync() ?? Task.CompletedTask;
			return Task.WhenAll(alt, map);
		}
		#endregion IMapApController


		#region save and load with picker
		internal async Task PickSaveSeriesToFileAsync(PersistentData.Tables whichSeries, string fileNameSuffix)
		{
			if (!TrySetIsSaving(true) || whichSeries == PersistentData.Tables.nil) return;
			SetLastMessage_UI("saving GPX file...");

			SwitchableObservableCollection<PointRecord> series = PersistentData.GetSeries(whichSeries);
			DateTime fileCreationDateTime = DateTime.Now;

			var file = await Pickers.PickSaveFileAsync(new string[] { ConstantData.GPX_EXTENSION }, fileCreationDateTime.ToString(ConstantData.GPX_DATE_TIME_FORMAT_ONLY_LETTERS_AND_NUMBERS, CultureInfo.InvariantCulture) + fileNameSuffix).ConfigureAwait(false);
			if (file != null)
			{
				RegistryAccess.TrySetValue(ConstantData.REG_SAVE_SERIES_WHICH_SERIES, whichSeries.ToString());
				RegistryAccess.TrySetValue(ConstantData.REG_SAVE_SERIES_FILE_CREATION_DATE_TIME, fileCreationDateTime.ToString(ConstantData.GPX_DATE_TIME_FORMAT, CultureInfo.CurrentUICulture));

				// LOLLO NOTE at this point, OnResuming() has just started, if the app was suspended. 
				await RunFunctionIfOpenAsyncT(delegate
				{
					return ContinueAfterPickSaveSeriesToFileAsync(series, whichSeries, fileCreationDateTime, file);
				}).ConfigureAwait(false);
			}
			else
			{
				SetLastMessage_UI("Saving cancelled");
				IsSaving = false;
			}
		}

		/// <summary>
		/// This method is called in a separate task on low-memory phones
		/// </summary>
		/// <param name="series"></param>
		/// <param name="whichSeries"></param>
		/// <param name="fileCreationDateTime"></param>
		/// <param name="file"></param>
		/// <returns></returns>
		private async Task ContinueAfterPickSaveSeriesToFileAsync(SwitchableObservableCollection<PointRecord> series, PersistentData.Tables whichSeries, DateTime fileCreationDateTime, StorageFile file)
		{
			Logger.Add_TPL("ContinueAfterPickSaveSeriesToFileAsync() started with file == null = " + (file == null).ToString() + " and whichSeries = " + whichSeries + " and isOpen = " + _isOpen, Logger.AppEventsLogFilename, Logger.Severity.Info, false);

			Tuple<bool, string> result = Tuple.Create(false, "");
			try
			{
				if (file != null && whichSeries != PersistentData.Tables.nil)
				{
					if (CancToken == null || CancToken.IsCancellationRequested) return;
					await Task.Run(async delegate
					{
						if (CancToken == null || CancToken.IsCancellationRequested) return;
						SetLastMessage_UI("saving GPX file...");

						result = await ReaderWriter.SaveAsync(file, series, fileCreationDateTime, whichSeries, CancToken).ConfigureAwait(false);
					}).ConfigureAwait(false);
				}
			}
			catch (Exception) { }
			finally
			{
				// inform the user about the result
				if (result != null && result.Item1) SetLastMessage_UI(result.Item2);
				else if (whichSeries != PersistentData.Tables.nil) SetLastMessage_UI(string.Format("could not save {0}", PersistentData.GetTextForSeries(whichSeries)));
				else SetLastMessage_UI(string.Format("could not save file"));

				IsSaving = false;
			}
		}

		internal async Task PickLoadSeriesFromFileAsync(PersistentData.Tables whichSeries)
		{
			if (!TrySetIsLoading(true)) return;
			SetLastMessage_UI("reading GPX file...");

			var file = await Pickers.PickOpenFileAsync(new string[] { ConstantData.GPX_EXTENSION }).ConfigureAwait(false);
			if (file != null)
			{
				RegistryAccess.TrySetValue(ConstantData.REG_LOAD_SERIES_WHICH_SERIES, whichSeries.ToString());

				Logger.Add_TPL("Pick open file about to open series = " + whichSeries.ToString(), Logger.AppEventsLogFilename, Logger.Severity.Info, false);
				// LOLLO NOTE at this point, OnResuming() has just started, if the app was suspended. 
				bool isDone = await RunFunctionIfOpenAsyncT(delegate
				{
					return ContinueAfterPickLoadSeriesFromFileAsync(file, whichSeries);
				}).ConfigureAwait(false);
				Logger.Add_TPL("Pick open file has opened series = " + isDone.ToString(), Logger.AppEventsLogFilename, Logger.Severity.Info, false);
			}
			else
			{
				Logger.Add_TPL("Pick open file cancelled, series = " + whichSeries.ToString(), Logger.AppEventsLogFilename, Logger.Severity.Info, false);
				SetLastMessage_UI("Loading cancelled");
				IsLoading = false;
			}
		}

		// LOLLO NOTE check https://social.msdn.microsoft.com/Forums/sqlserver/en-US/13002ba6-6e59-47b8-a746-c05525953c5a/uwpfileopenpicker-bugs-in-win-10-mobile-when-not-debugging?forum=wpdevelop
		// and AnalyticsVersionInfo.DeviceFamily
		// for picker details

		private async Task ContinueAfterPickLoadSeriesFromFileAsync(StorageFile file, PersistentData.Tables whichSeries)
		{
			Logger.Add_TPL("ContinueAfterPickLoadSeriesFromFileAsync() started with file == null = " + (file == null).ToString() + " and whichSeries = " + whichSeries + " and isOpen = " + _isOpen, Logger.AppEventsLogFilename, Logger.Severity.Info, false);

			Tuple<bool, string> result = Tuple.Create(false, "");
			try
			{
				if (file != null && whichSeries != PersistentData.Tables.nil)
				{
					if (CancToken == null || CancToken.IsCancellationRequested) return;
					await Task.Run(async delegate
					{
						SetLastMessage_UI("reading GPX file...");

						if (CancToken == null || CancToken.IsCancellationRequested) return;

						// load the file
						result = await ReaderWriter.LoadSeriesFromFileIntoDbAsync(file, whichSeries, CancToken).ConfigureAwait(false);
						if (CancToken == null || CancToken.IsCancellationRequested) return;
						Logger.Add_TPL("ContinueAfterPickLoadSeriesFromFileAsync() loaded series into db", Logger.AppEventsLogFilename, Logger.Severity.Info, false);

						// update the UI with the file data
						if (result?.Item1 == true)
						{
							switch (whichSeries)
							{
								case PersistentData.Tables.History:
									_whichSeriesJustLoaded = PersistentData.Tables.History;
									break;
								case PersistentData.Tables.Route0:
									int cntR = await PersistentData.LoadRoute0FromDbAsync(false).ConfigureAwait(false);
									Logger.Add_TPL("ContinueAfterPickLoadSeriesFromFileAsync() loaded " + cntR + " route0 points into UI", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
									_whichSeriesJustLoaded = PersistentData.Tables.Route0;
									await CentreOnRoute0Async().ConfigureAwait(false);
									break;
								case PersistentData.Tables.Checkpoints:
									int cntC = await PersistentData.LoadCheckpointsFromDbAsync(false).ConfigureAwait(false);
									Logger.Add_TPL("ContinueAfterPickLoadSeriesFromFileAsync() loaded " + cntC + " checkpoints into UI", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
									_whichSeriesJustLoaded = PersistentData.Tables.Checkpoints;
									await CentreOnCheckpointsAsync().ConfigureAwait(false);
									break;
								case PersistentData.Tables.nil:
									_whichSeriesJustLoaded = PersistentData.Tables.nil;
									break;
								default:
									_whichSeriesJustLoaded = PersistentData.Tables.nil;
									break;
							}
						}
					}).ConfigureAwait(false);
				}
			}
			catch (Exception) { }
			finally
			{
				// inform the user about the outcome
				if (result?.Item1 == true)
				{
					SetLastMessage_UI(result.Item2);
					PersistentData.IsShowingPivot = false;
				}
				else if (whichSeries != PersistentData.Tables.nil) SetLastMessage_UI(string.Format("could not load {0}", PersistentData.GetTextForSeries(whichSeries)));
				else SetLastMessage_UI("could not load file");

				IsLoading = false;
			}
			Logger.Add_TPL("ContinueAfterPickLoadSeriesFromFileAsync() ended", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
		}
		#endregion save and load with picker


		#region open app through file
		/// <summary>
		/// This method is called in a separate task on low-memory phones
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public async Task<List<PersistentData.Tables>> LoadFileIntoDbAsync(FileActivatedEventArgs args)
		{
			List<PersistentData.Tables> result = new List<PersistentData.Tables>();

			if (args?.Files?.Count > 0 && args.Files[0] is StorageFile)
			{
				Tuple<bool, string> checkpointsResult = Tuple.Create(false, "");
				Tuple<bool, string> route0Result = Tuple.Create(false, "");

				try
				{
					if (CancToken == null || CancToken.IsCancellationRequested) return result;
					SetLastMessage_UI("reading GPX file...");
					// load the file, attempting to read checkpoints and route. GPX files can contain both.
					StorageFile file_mt = args.Files[0] as StorageFile;
					if (CancToken == null || CancToken.IsCancellationRequested) return result;
					checkpointsResult = await ReaderWriter.LoadSeriesFromFileIntoDbAsync(file_mt, PersistentData.Tables.Checkpoints, CancToken).ConfigureAwait(false);
					if (CancToken == null || CancToken.IsCancellationRequested) return result;
					route0Result = await ReaderWriter.LoadSeriesFromFileIntoDbAsync(file_mt, PersistentData.Tables.Route0, CancToken).ConfigureAwait(false);
				}
				catch (Exception) { }
				finally
				{
					// inform the user about the result
					if ((checkpointsResult == null || !checkpointsResult.Item1) && (route0Result == null || !route0Result.Item1)) SetLastMessage_UI("could not read file");
					else if (checkpointsResult?.Item1 == true && route0Result?.Item1 == true)
					{
						SetLastMessage_UI(route0Result.Item2 + " and " + checkpointsResult.Item2);
					}
					else if (route0Result?.Item1 == true)
					{
						SetLastMessage_UI(route0Result.Item2);
					}
					else if (checkpointsResult?.Item1 == true)
					{
						SetLastMessage_UI(checkpointsResult.Item2);
					}
					// fill output
					if (checkpointsResult?.Item1 == true) result.Add(PersistentData.Tables.Checkpoints);
					if (route0Result?.Item1 == true) result.Add(PersistentData.Tables.Route0);
				}
			}
			return result;
		}
		#endregion open app through file
	}

	public interface IMapApController
	{
		Task CentreOnHistoryAsync();
		Task CentreOnCheckpointsAsync();
		Task CentreOnRoute0Async();
		Task CentreOnSeriesAsync(PersistentData.Tables series);
		Task CentreOnTargetAsync();
		Task CentreOnCurrentAsync();
		Task Goto2DAsync();
	}
}