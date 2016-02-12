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
				await _openCloseSemaphore.WaitAsync();
				Logger.Add_TPL("Main.OpenAsync just started, it is in the semaphore", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
				if (_isOpen) return YesNoError.No;

				_mainVM = new MainVM(IsWideEnough, MyLolloMap, MyAltitudeProfiles);
				await _mainVM.OpenAsync();
				RaisePropertyChanged_UI(nameof(MainVM));
				await Task.Delay(1); // just in case

				var openMainVmMessage = PersistentData.LastMessage;

				Task alt0 = UpdateAltitudeColumnWidthAsync();
				Task alt1 = UpdateAltitudeColumnMaxWidthAsync();
				Task butt = UpdateIsExtraButtonsEnabledAsync();

				Logger.Add_TPL("Main.OpenAsync is about to open its child controls", Logger.AppEventsLogFilename, Logger.Severity.Info, false);

				await MyLolloMap.OpenAsync();
				await MyAltitudeProfiles.OpenAsync();
				MyMapsPanel.LolloMapVM = MyLolloMap.LolloMapVM;
				await MyMapsPanel.OpenAsync();
				await MyCustomMapsPanel.OpenAsync();

				AddHandlers();
				PersistentData.LastMessage = openMainVmMessage; // output the msg into the UI

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

				await RunInUiThreadAsync(EndAllAnimations);

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

		private void OnPersistentData_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(PersistentData.LastMessage))
			{
				if (!string.IsNullOrWhiteSpace(PersistentData.LastMessage))
				{
					Task gt = RunInUiThreadAsync(delegate
					{
						EndAllAnimations();
						ShowHideLastMessageFlashyStoryboard.Begin();
					});
				}
			}
			else if (e.PropertyName == nameof(PersistentData.IsShowingAltitudeProfiles))
			{
				Task alt = UpdateAltitudeColumnMaxWidthAsync();
				Task butt = UpdateIsExtraButtonsEnabledAsync();
			}
		}

		private bool _isLastMessageVisible = false;

		private void EndAllAnimations()
		{
			ShowHideLastMessageFlashyStoryboard.SkipToFill();
			ShowHideLastMessageDiscreetStoryboard.SkipToFill();
		}
		private void OnLastMessage_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
		{
			if (!_isLastMessageVisible && !string.IsNullOrWhiteSpace(_mainVM.PersistentData.LastMessage))
			{
				_isLastMessageVisible = true;
				EndAllAnimations();
				ShowHideLastMessageDiscreetStoryboard.Begin();
			}
			else if (_isLastMessageVisible)
			{
				_isLastMessageVisible = false;
				EndAllAnimations();
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
			MyLolloMap?.LolloMapVM?.CancelDownloadByUser();
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
				AltitudeColumn.MaxWidth = !PersistentData.IsShowingAltitudeProfiles ? 0 : double.PositiveInfinity;
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
				AltitudeColumn.Width = IsWideEnough ? new GridLength(1.0, GridUnitType.Star) : new GridLength(0.0);

				Grid.SetColumn(MyAltitudeProfiles, IsWideEnough ? 1 : 0);
			});
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
	}
}