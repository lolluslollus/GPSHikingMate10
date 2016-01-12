using LolloBaseUserControls;
using LolloGPS.Data;
using LolloGPS.Data.Constants;
using LolloGPS.Data.Files;
using LolloGPS.Data.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Utilz;
using Windows.Phone.UI.Input;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

// The Pivot Application template is documented at http://go.microsoft.com/fwlink/?LinkID=391641
namespace LolloGPS.Core
{
	public sealed partial class Main : ObservablePage
	{
		public PersistentData MyPersistentData { get { return App.PersistentData; } }
		public RuntimeData MyRuntimeData { get { return App.MyRuntimeData; } }

		private Main_VM _myVM = Main_VM.GetInstance();
		public Main_VM MyVM { get { return _myVM; } }

		private static SemaphoreSlimSafeRelease _openCloseSemaphore = new SemaphoreSlimSafeRelease(1, 1);
		private volatile bool _isOpen = false;


		#region lifecycle
		public Main()
		{
			InitializeComponent();
			NavigationCacheMode = NavigationCacheMode.Disabled;
#if !NOSTORE
			MyPivot.Items.Remove(LogsButton);
#endif
		}

		public async Task OpenAsync(bool readDataFromDb, bool readSettingsFromDb)
		{
			if (_isOpen) return;
			try
			{
				await _openCloseSemaphore.WaitAsync();
				if (_isOpen) return;

				_myVM = Main_VM.GetInstance();
				await _myVM.OpenAsync(readDataFromDb, readSettingsFromDb);

				await MyLolloMap.OpenAsync();
				MyAltitudeProfiles.Open();
				AddHandlers();

				_isOpen = true;
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

		public async Task CloseAsync()
		{
			if (!_isOpen) return;
			try
			{
				await _openCloseSemaphore.WaitAsync();
				if (!_isOpen) return;

				RemoveHandlers();

				_animationTimer?.Dispose();
				_animationTimer = null;

				var vm = _myVM;
				if (vm != null)
				{
					await vm.CloseAsync();
				}
				MyLolloMap?.Close();
				MyAltitudeProfiles?.Close();

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
		private bool _isDataChangedHandlerActive = false;
		private void AddHandlers()
		{
			if (!_isDataChangedHandlerActive)
			{
				_isDataChangedHandlerActive = true;
				if (MyPersistentData != null) MyPersistentData.PropertyChanged += OnPersistentData_PropertyChanged;
				if (MyRuntimeData.IsHardwareButtonsAPIPresent) HardwareButtons.BackPressed += OnHardwareButtons_BackPressed;
				Windows.UI.Core.SystemNavigationManager.GetForCurrentView().BackRequested += OnTabletSoftwareButton_BackPressed;
			}
		}

		private void RemoveHandlers()
		{
			if (MyPersistentData != null) MyPersistentData.PropertyChanged -= OnPersistentData_PropertyChanged;
			if (MyRuntimeData.IsHardwareButtonsAPIPresent) HardwareButtons.BackPressed -= OnHardwareButtons_BackPressed;
			Windows.UI.Core.SystemNavigationManager.GetForCurrentView().BackRequested -= OnTabletSoftwareButton_BackPressed;
			_isDataChangedHandlerActive = false;
		}
		private void OnHardwareButtons_BackPressed(object sender, BackPressedEventArgs e)
		{
			_myVM.GoBackHard(sender, e);
		}

		private void OnTabletSoftwareButton_BackPressed(object sender, Windows.UI.Core.BackRequestedEventArgs e)
		{
			_myVM.GoBackTabletSoft(sender, e);
		}

		private DispatcherTimerPlus _animationTimer = null;

		private async void OnPersistentData_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(PersistentData.LastMessage))
			{
				if (!string.IsNullOrWhiteSpace(MyPersistentData.LastMessage))
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
						await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename).ConfigureAwait(false);
					}
				}
			}
		}
		private void SetShowForAWhileOnly()
		{
			_myVM.IsLastMessageVisible = true;
		}
		private void StopShowingNotice()
		{
			_myVM.IsLastMessageVisible = false;
		}
		private void OnLastMessage_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
		{
			if (!_myVM.IsLastMessageVisible && !string.IsNullOrWhiteSpace(_myVM.MyPersistentData.LastMessage))
			{
				_myVM.IsLastMessageVisible = true;
			}
			else
			{
				_myVM.IsLastMessageVisible = false;
			}
		}
		private void OnGetAFixNow_Click(object sender, RoutedEventArgs e)
		{
			Task vibrate = Task.Run(() => App.ShortVibration());
			_myVM.GetAFix();
		}

		//private async void OnClearHistory_Click(object sender, RoutedEventArgs e)
		//{
		//	//raise confirmation popup
		//	var dialog = new Windows.UI.Popups.MessageDialog("This will delete all the data. Are you sure?", "Confirm deletion");
		//	UICommand yesCommand = new UICommand("Yes", (command) => { });
		//	UICommand noCommand = new UICommand("No", (command) => { });
		//	dialog.Commands.Add(yesCommand);
		//	dialog.Commands.Add(noCommand);
		//	// Set the command that will be invoked by default
		//	dialog.DefaultCommandIndex = 1;
		//	// Show the message dialog
		//	IUICommand reply = await dialog.ShowAsync().AsTask();
		//	if (reply == yesCommand) { Task res = MyPersistentData.ResetHistoryAsync(); }
		//}

		//private void OnCenterRoute_Click(object sender, RoutedEventArgs e)
		//{
		//	Task cr = _myVM.CentreOnRoute0Async();
		//}
		//private void OnCenterHistory_Click(object sender, RoutedEventArgs e)
		//{
		//	Task ch = _myVM.CentreOnHistoryAsync();
		//}
		//private void OnCenterLandmarks_Click(object sender, RoutedEventArgs e)
		//{
		//	Task cl = _myVM.CentreOnLandmarksAsync();
		//}
		private void OnPointsPanel_CentreOnTargetRequested(object sender, EventArgs e)
		{
			Task ct = _myVM.CentreOnTargetAsync();
		}
		private void OnMapsGoto2DRequested(object sender, EventArgs e)
		{
			Task gt = _myVM.Goto2DAsync();
		}

		//private void OnLoadRoute0_Click(object sender, RoutedEventArgs e)
		//{
		//	Task lr = _myVM.PickLoadSeriesFromFileAsync(PersistentData.Tables.Route0);
		//}

		//private void OnSaveTrackingHistory_Click(object sender, RoutedEventArgs e)
		//{
		//	Task sth = _myVM.PickSaveSeriesToFileAsync(PersistentData.Tables.History, "_" + ConstantData.APPNAME_ALL_IN_ONE + "_Route");
		//}

		//private void OnSaveRoute0_Click(object sender, RoutedEventArgs e)
		//{
		//	Task sr = _myVM.PickSaveSeriesToFileAsync(PersistentData.Tables.Route0, "_" + ConstantData.APPNAME_ALL_IN_ONE + "_Route");
		//}

		//private void OnClearRoute0_Click(object sender, RoutedEventArgs e)
		//{
		//	Task rr = MyPersistentData.ResetRoute0Async();
		//}

		//private void OnLoadLandmarks_Click(object sender, RoutedEventArgs e)
		//{
		//	Task ll = _myVM.PickLoadSeriesFromFileAsync(PersistentData.Tables.Landmarks);
		//}

		//private void OnClearLandmarks_Click(object sender, RoutedEventArgs e)
		//{
		//	Task cll = MyPersistentData.ResetLandmarksAsync();
		//}

		//private void OnSaveLandmarks_Click(object sender, RoutedEventArgs e)
		//{
		//	Task sl = _myVM.PickSaveSeriesToFileAsync(PersistentData.Tables.Landmarks, "_" + ConstantData.APPNAME_ALL_IN_ONE + "_Landmarks");
		//}

		private void OnCancelDownload_Click(object sender, RoutedEventArgs e)
		{
			_myVM.SetLastMessage_UI("Cancelling download");
			_myVM.CancelDownloadByUser();
		}

		private async void OnTestFiles_Click(object sender, RoutedEventArgs e)
		{
			string txt = await FileData.GetAllFilesInLocalFolderAsync().ConfigureAwait(false);

			await RunInUiThreadAsync(delegate
			{
				_myVM.LogText = txt;
			}).ConfigureAwait(false);
		}

		private async void OnLogButton_Click(object sender, RoutedEventArgs e)
		{
			string cnt = (sender as Button).Content.ToString();
			if (cnt == "FileError")
			{
				_myVM.LogText = await Logger.ReadAsync(Logger.FileErrorLogFilename);
			}
			else if (cnt == "MyPersistentData")
			{
				_myVM.LogText = await Logger.ReadAsync(Logger.PersistentDataLogFilename);
			}
			else if (cnt == "Fgr")
			{
				_myVM.LogText = await Logger.ReadAsync(Logger.ForegroundLogFilename);
			}
			else if (cnt == "Bgr")
			{
				_myVM.LogText = await Logger.ReadAsync(Logger.BackgroundLogFilename);
			}
			else if (cnt == "BgrCanc")
			{
				_myVM.LogText = await Logger.ReadAsync(Logger.BackgroundCancelledLogFilename);
			}
			else if (cnt == "AppExc")
			{
				_myVM.LogText = await Logger.ReadAsync(Logger.AppExceptionLogFilename);
			}
			else if (cnt == "Clear")
			{
				Logger.ClearAll();
			}
		}
		private void OnLogText_Unloaded(object sender, RoutedEventArgs e)
		{
			_myVM.LogText = string.Empty;
		}

		private void OnOpenPivot_Click(object sender, RoutedEventArgs e)
		{
			MyPersistentData.IsShowingPivot = !MyPersistentData.IsShowingPivot;
		}

		private void OnAltitude_Click(object sender, RoutedEventArgs e)
		{
			MyPersistentData.IsShowingPivot = false;
		}

		private void OnMapStyleButton_Click(object sender, RoutedEventArgs e)
		{
			MyPersistentData.CycleMapStyle();
		}

		private void OnBack_Click(object sender, RoutedEventArgs e)
		{
			_myVM.GoBackMyButtonSoft();
		}
		#endregion event handling
	}
}
