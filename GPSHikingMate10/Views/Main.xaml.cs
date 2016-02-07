using Utilz.Controlz;
using LolloGPS.Data;
using LolloGPS.Data.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Utilz;
using Windows.ApplicationModel.Activation;
using Windows.Phone.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

// The Pivot Application template is documented at http://go.microsoft.com/fwlink/?LinkID=391641
namespace LolloGPS.Core
{
	public sealed partial class Main : ObservablePage, IInfoPanelEventReceiver
	{
		#region properties
		public PersistentData PersistentData { get { return App.PersistentData; } }
		public RuntimeData RuntimeData { get { return App.RuntimeData; } }

		private MainVM _mainVM = null;
		public MainVM MainVM { get { return _mainVM; } }

		private static readonly SemaphoreSlimSafeRelease _openCloseSemaphore = new SemaphoreSlimSafeRelease(1, 1);
		private volatile bool _isOpen = false;



		public bool IsWideEnough
		{
			get { return (bool)GetValue(IsWideEnoughProperty); }
			set { SetValue(IsWideEnoughProperty, value); }
		}
		public static readonly DependencyProperty IsWideEnoughProperty =
			DependencyProperty.Register("IsWideEnough", typeof(bool), typeof(Main), new PropertyMetadata(false, OnIsWideEnoughChanged));
		private static void OnIsWideEnoughChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
		{
			var vm = (obj as Main).MainVM;
			if (vm != null) vm.IsWideEnough = (bool)(args.NewValue);
			Task alt0 = (obj as Main).UpdateIsExtraButtonsEnabledAsync();
			Task alt1 = (obj as Main).UpdateAltitudeColumnWidthAsync();
		}

		private bool _isExtraButtonsEnabled = false;
		public bool IsExtraButtonsEnabled { get { return _isExtraButtonsEnabled; } private set { if (_isExtraButtonsEnabled != value) { _isExtraButtonsEnabled = value; RaisePropertyChanged_UI(); } } }

		#endregion properties


		#region lifecycle
		public Main()
		{
			InitializeComponent();
			NavigationCacheMode = NavigationCacheMode.Disabled;
#if !NOSTORE
			MyPivot.Items.Remove(LogsButton);
#endif
		}

		public enum YesNoError { Yes, No, Error };
		/// <summary>
		/// This method must run in the UI thread
		/// </summary>
		/// <returns></returns>				 
		public async Task<YesNoError> OpenAsync()
		{
			if (_isOpen) return YesNoError.No;
			try
			{
				Logger.Add_TPL("Main.OpenAsync just started, it is in the semaphore", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
				await _openCloseSemaphore.WaitAsync();
				if (_isOpen) return YesNoError.No;

				_mainVM = new MainVM(IsWideEnough);
				await _mainVM.OpenAsync();
				RaisePropertyChanged_UI(nameof(MainVM));
				await Task.Delay(1); // just in case, try not to hog the ui thread too long

				Task alt0 = UpdateAltitudeColumnWidthAsync();
				Task alt1 = UpdateAltitudeColumnMaxWidthAsync();
				Task butt = UpdateIsExtraButtonsEnabledAsync();

				await MyLolloMap.OpenAsync();
				await MyAltitudeProfiles.OpenAsync();
				await MyMapsPanel.OpenAsync();
				await MyCustomMapsPanel.OpenAsync();

				AddHandlers();

				_isOpen = true;

				Logger.Add_TPL("Main.OpenAsync ended OK", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
				return YesNoError.Yes;
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename);
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(_openCloseSemaphore);
			}
			return YesNoError.Error;
		}

		public async Task CloseAsync()
		{
			Debug.WriteLine("Main.CloseAsync() entered CloseAsync");
			if (!_isOpen) return;
			Debug.WriteLine("Main.CloseAsync() is in CloseAsync and is open");
			try
			{
				await _openCloseSemaphore.WaitAsync();
				Debug.WriteLine("Main.CloseAsync() entered the semaphore");
				if (!_isOpen) return;

				RemoveHandlers();

				_animationTimer?.Dispose();
				_animationTimer = null;

				var mainVM = _mainVM;
				if (mainVM != null)
				{
					await mainVM.CloseAsync();
				}
				Debug.WriteLine("Main.CloseAsync() closed MainVM");

				await MyPointInfoPanel.CloseAsync();
				await MyLolloMap.CloseAsync();
				await MyAltitudeProfiles.CloseAsync();
				Debug.WriteLine("Main.CloseAsync() closed the altitude");

				//_owner.Storyboard_NewMessage.SkipToFill();

				_isOpen = false;
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename);
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(_openCloseSemaphore);
			}
		}
		#endregion lifecycle


		#region event handling
		private volatile bool _isDataChangedHandlerActive = false;
		private void AddHandlers()
		{
			if (!_isDataChangedHandlerActive)
			{
				_isDataChangedHandlerActive = true;
				if (PersistentData != null) PersistentData.PropertyChanged += OnPersistentData_PropertyChanged;
				if (RuntimeData.IsHardwareButtonsAPIPresent) HardwareButtons.BackPressed += OnHardwareButtons_BackPressed;
				var naviManager = Windows.UI.Core.SystemNavigationManager.GetForCurrentView();
				if (naviManager != null) naviManager.BackRequested += OnTabletSoftwareButton_BackPressed;
			}
		}

		private void RemoveHandlers()
		{
			if (PersistentData != null) PersistentData.PropertyChanged -= OnPersistentData_PropertyChanged;
			if (RuntimeData.IsHardwareButtonsAPIPresent) HardwareButtons.BackPressed -= OnHardwareButtons_BackPressed;
			var naviManager = Windows.UI.Core.SystemNavigationManager.GetForCurrentView();
			if (naviManager != null) naviManager.BackRequested -= OnTabletSoftwareButton_BackPressed;
			_isDataChangedHandlerActive = false;
		}

		private void OnBack_Click(object sender, RoutedEventArgs e)
		{
			_mainVM?.GoBackMyButtonSoft();
		}

		private void OnHardwareButtons_BackPressed(object sender, BackPressedEventArgs e)
		{
			_mainVM?.GoBackHard(sender, e);
		}

		private void OnTabletSoftwareButton_BackPressed(object sender, Windows.UI.Core.BackRequestedEventArgs e)
		{
			_mainVM?.GoBackTabletSoft(sender, e);
		}

		private volatile DispatcherTimerPlus _animationTimer = null;

		private void OnPersistentData_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(PersistentData.LastMessage))
			{
				if (!string.IsNullOrWhiteSpace(PersistentData.LastMessage))
				{
					Task gt = RunInUiThreadAsync(delegate
					{
						try
						{
							if (_animationTimer == null)
							{
								_animationTimer = new DispatcherTimerPlus(StopShowingNotice, 5);
							}
							else
							{
								_animationTimer.Stop();
							}
							SetShowForAWhileOnly();
							_animationTimer.Start();
							//Storyboard_NewMessage.SkipToFill(); // LOLLO disable Storyboard_NewMessage to see if crash goes away
							//Storyboard_NewMessage.Begin();
						}
						catch (Exception ex)
						{
							Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
						}
					});
				}
			}
			else if (e.PropertyName == nameof(PersistentData.IsShowingAltitudeProfiles))
			{
				Task alt = UpdateAltitudeColumnMaxWidthAsync();
				Task butt = UpdateIsExtraButtonsEnabledAsync();
			}
		}

		private void OnLastMessage_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
		{
			if (!_mainVM.IsLastMessageVisible && !string.IsNullOrWhiteSpace(_mainVM.PersistentData.LastMessage))
			{
				_mainVM.IsLastMessageVisible = true;
			}
			else
			{
				_mainVM.IsLastMessageVisible = false;
			}
		}
		private void OnGetAFixNow_Click(object sender, RoutedEventArgs e)
		{
			Task vibrate = Task.Run(() => App.ShortVibration());
			_mainVM.GetAFix();
		}

		private void OnGotoLast_Click(object sender, RoutedEventArgs e)
		{
			Task vibrate = Task.Run(() => App.ShortVibration());
			Task go = _mainVM.CentreOnCurrentAsync();
		}

		private void OnPointsPanel_CentreOnTargetRequested(object sender, EventArgs e)
		{
			Task ct = _mainVM.CentreOnTargetAsync();
		}
		private void OnMapsGoto2DRequested(object sender, EventArgs e)
		{
			Task gt = _mainVM.Goto2DAsync();
		}

		private void OnCancelDownload_Click(object sender, RoutedEventArgs e)
		{
			_mainVM.SetLastMessage_UI("Cancelling download");
			_mainVM.CancelDownloadByUser();
		}

		private async void OnTestFiles_Click(object sender, RoutedEventArgs e)
		{
			string txt = await FileDirectoryExtensions.GetAllFilesInLocalFolderAsync().ConfigureAwait(false);

			await RunInUiThreadAsync(delegate
			{
				_mainVM.LogText = txt;
			}).ConfigureAwait(false);
		}

		private async void OnLogButton_Click(object sender, RoutedEventArgs e)
		{
			string cnt = (sender as Button).Content.ToString();
			if (cnt == "FileError")
			{
				_mainVM.LogText = await Logger.ReadAsync(Logger.FileErrorLogFilename);
			}
			else if (cnt == "PersistentData")
			{
				_mainVM.LogText = await Logger.ReadAsync(Logger.PersistentDataLogFilename);
			}
			else if (cnt == "Fgr")
			{
				_mainVM.LogText = await Logger.ReadAsync(Logger.ForegroundLogFilename);
			}
			else if (cnt == "Bgr")
			{
				_mainVM.LogText = await Logger.ReadAsync(Logger.BackgroundLogFilename);
			}
			else if (cnt == "BgrCanc")
			{
				_mainVM.LogText = await Logger.ReadAsync(Logger.BackgroundCancelledLogFilename);
			}
			else if (cnt == "AppExc")
			{
				_mainVM.LogText = await Logger.ReadAsync(Logger.AppExceptionLogFilename);
			}
			else if (cnt == "AppEvents")
			{
				_mainVM.LogText = await Logger.ReadAsync(Logger.AppEventsLogFilename);
			}
			else if (cnt == "Clear")
			{
				Logger.ClearAll();
			}
		}
		private void OnLogText_Unloaded(object sender, RoutedEventArgs e)
		{
			_mainVM.LogText = string.Empty;
		}

		private void OnToggleOpenPivot_Click(object sender, RoutedEventArgs e)
		{
			//PersistentData.IsShowingPivot = !PersistentData.IsShowingPivot;
		}

		private void OnAltitude_Click(object sender, RoutedEventArgs e)
		{
			PersistentData.IsShowingPivot = false;
		}

		private void OnMapStyleButton_Click(object sender, RoutedEventArgs e)
		{
			PersistentData.CycleMapStyle();
		}
		#endregion event handling


		#region services
		private Task UpdateAltitudeColumnMaxWidthAsync()
		{
			return RunInUiThreadAsync(delegate
			{
				if (!PersistentData.IsShowingAltitudeProfiles) AltitudeColumn.MaxWidth = 0;
				else AltitudeColumn.MaxWidth = double.PositiveInfinity;
			});
		}
		private Task UpdateIsExtraButtonsEnabledAsync()
		{
			return RunInUiThreadAsync(delegate
			{
				IsExtraButtonsEnabled = IsWideEnough || !PersistentData.IsShowingAltitudeProfiles;
			});
		}
		private Task UpdateAltitudeColumnWidthAsync()
		{
			return RunInUiThreadAsync(delegate
			{
				if (IsWideEnough) AltitudeColumn.Width = new GridLength(1.0, GridUnitType.Star);
				else AltitudeColumn.Width = new GridLength(0.0);

				if (IsWideEnough) Grid.SetColumn(MyAltitudeProfiles, 1);
				else Grid.SetColumn(MyAltitudeProfiles, 0);
			});
		}
		private void SetShowForAWhileOnly()
		{
			_mainVM.IsLastMessageVisible = true;
		}
		private void StopShowingNotice()
		{
			_mainVM.IsLastMessageVisible = false;
		}
		#endregion services


		#region point info panel
		public void OnInfoPanelPointChanged(object sender, EventArgs e)
		{
			if (PersistentData.IsShowingAltitudeProfiles) MyAltitudeProfiles.OnInfoPanelPointChanged(sender, e);
			MyLolloMap.OnInfoPanelPointChanged(sender, e);

			try
			{
				if (!PersistentData.IsSelectedSeriesNonNullAndNonEmpty())
				{
					SelectedPointPopup.IsOpen = false;
				}
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}
		}

		public void OnInfoPanelClosed(object sender, object e)
		{
			if (PersistentData.IsShowingAltitudeProfiles) MyAltitudeProfiles.OnInfoPanelClosed(sender, e);
			MyLolloMap.OnInfoPanelClosed(sender, e);
		}

		private void OnShowOnePointDetailsRequested(object sender, AltitudeProfiles.ShowOnePointDetailsRequestedArgs e)
		{
			if (IsWideEnough || !PersistentData.IsShowingAltitudeProfiles)
			{
				Task centre = MyLolloMap.CentreOnSeriesAsync(e.SelectedSeries);
			}
			MyPointInfoPanel.SetDetails(e.SelectedRecord, e.SelectedSeries);
			SelectedPointPopup.IsOpen = true;
		}

		private void OnShowManyPointDetailsRequested(object sender, LolloMap.ShowManyPointDetailsRequestedArgs e)
		{
			MyPointInfoPanel.SetDetails(e.SelectedRecords, e.SelectedSeriess);
			SelectedPointPopup.IsOpen = true;
		}
		#endregion point info panel


		#region open app through file
		/// <summary>
		/// This method is called in a separate task on low-memory phones
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public Task<List<PersistentData.Tables>> LoadFileIntoDbAsync(FileActivatedEventArgs args)
		{
			return MainVM?.LoadFileIntoDbAsync(args);
		}
		#endregion open app through file
	}
}