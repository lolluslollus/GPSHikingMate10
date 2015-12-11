using LolloGPS.Data;
using LolloGPS.Data.Constants;
using LolloGPS.Data.Files;
using LolloGPS.Data.Runtime;
using LolloGPS.Suspension;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Utilz;
using Windows.ApplicationModel.Activation;
using Windows.Phone.UI.Input;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

// The Pivot Application template is documented at http://go.microsoft.com/fwlink/?LinkID=391641
namespace LolloGPS.Core
{
	public sealed partial class Main : Page, IFileActivatable
	{
		public PersistentData MyPersistentData { get { return App.PersistentData; } }
		public RuntimeData MyRuntimeData { get { return App.MyRuntimeData; } }

		private Main_VM _myVM = Main_VM.GetInstance();
		public Main_VM MyVM { get { return _myVM; } }

		private Activator _activator = null;
		private class Activator
		{
			private volatile Main _owner = null;
			private volatile bool _isReadDataWhenActivating = false;
			internal Activator(Main owner)
			{
				_owner = owner;
			}
			internal async Task OnLoaded(bool readData)
			{
				_isReadDataWhenActivating = readData;
				
				try
				{
					await _owner.MyRuntimeData.RunFunctionUnderSemaphoreT(async delegate
					   {
						   if (_owner.MyRuntimeData.IsCommandsActive)
						   {
							   await ActivateAsync().ConfigureAwait(false);
						   }
						   else
						   {
							   AddHandler_Activate();
						   }
					   }).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename).ConfigureAwait(false);
				}
			}
			private async Task ActivateAsync()
			{
				try
				{
					Logger.Add_TPL("Main.Activator.Activate() started; RuntimeData.IsSettingsRead = "
						+ _owner.MyRuntimeData.IsSettingsRead.ToString()
						+ "; RuntimeData.IsDBDataRead = "
						+ _owner.MyRuntimeData.IsDBDataRead.ToString()
						+ "; RuntimeData.IsCommandsActive = "
						+ _owner.MyRuntimeData.IsCommandsActive.ToString(),
						Logger.ForegroundLogFilename,
						Logger.Severity.Info);

					if (_isReadDataWhenActivating) await SuspensionManager.ReadDataAsync();

					await _owner.MyVM.ActivateAsync();
					KeepAlive.UpdateKeepAlive(_owner.MyPersistentData.IsKeepAlive);
					await _owner.MyLolloMap.ActivateAsync();
					_owner.MyAltitudeProfiles.Activate();
					_owner.AddHandlers();

					Logger.Add_TPL("Main.Activator.Activate() ended all right", Logger.ForegroundLogFilename, Logger.Severity.Info);
				}
				catch (Exception ex)
				{
					Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
				}
			}
			public void Deactivate()
			{
				try
				{
					RemoveHandler_Activate();
					_owner?.MyVM?.Deactivate();
					_owner?.RemoveHandlers();
					_owner?.MyLolloMap?.Deactivate();
					_owner?.MyAltitudeProfiles?.Deactivate();
					KeepAlive.StopKeepAlive();
					//_owner.Storyboard_NewMessage.SkipToFill();
					CancelPendingTasks(); // after removing the handlers
				}
				catch (Exception ex)
				{
					Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
				}

			}
			private void CancelPendingTasks()
			{
				// Do not dispose the cts's here. Dispose is done in the exception handler that catches the OperationCanceled exception. 
				// If you do it here, the exception handler will throw an ObjectDisposed exception
				_owner?.MyVM?._fileOpenContinuationCts?.Cancel();
				_owner?.MyVM?._fileOpenPickerCts?.Cancel();
				_owner?.MyVM?._fileSavePickerCts?.Cancel();
			}

			private Boolean _isActivateHandlerActive = false;
			private void AddHandler_Activate()
			{
				if (!_isActivateHandlerActive && _owner != null && _owner.MyRuntimeData != null)
				{
					_owner.MyRuntimeData.PropertyChanged += OnMyRuntimeData_PropertyChanged_4Activate;
					_isActivateHandlerActive = true;
				}
			}
			private void RemoveHandler_Activate()
			{
				if (_owner != null && _owner.MyRuntimeData != null)
				{
					_owner.MyRuntimeData.PropertyChanged -= OnMyRuntimeData_PropertyChanged_4Activate;
					_isActivateHandlerActive = false;
				}
			}
			//private async void OnMyRuntimeData_PropertyChanged_4Activate(object sender, System.ComponentModel.PropertyChangedEventArgs e)
			//{
			//    try
			//    {
			//        if ((e.PropertyName == nameof(RuntimeData.IsSettingsRead) || e.PropertyName == nameof(RuntimeData.IsDBDataRead))
			//            && _owner != null && _owner.MyRuntimeData != null
			//            && _owner.MyRuntimeData.IsSettingsRead && _owner.MyRuntimeData.IsDBDataRead)
			//        {
			//            RemoveHandler_Activate();
			//            await ActivateAsync().ConfigureAwait(false);
			//        }
			//    }
			//    catch (Exception ex)
			//    {
			//        Logger.Add_TPL("Main.Activator.OnMyRuntimeData_PropertyChanged_4Activate() caught an exception: " + ex.Message + Environment.NewLine + ex.StackTrace, Logger.ForegroundLogFilename);
			//    }
			//}
			private async void OnMyRuntimeData_PropertyChanged_4Activate(object sender, System.ComponentModel.PropertyChangedEventArgs e)
			{
				if (e.PropertyName == nameof(RuntimeData.IsCommandsActive)
					&& _owner != null && _owner.MyRuntimeData != null && _owner.MyRuntimeData.IsCommandsActive)
				{
					try
					{
						await _owner.MyRuntimeData.RunFunctionUnderSemaphoreT(async delegate
						{
							if (e.PropertyName == nameof(RuntimeData.IsCommandsActive)
								&& _owner != null && _owner.MyRuntimeData != null && _owner.MyRuntimeData.IsCommandsActive)
							{
								RemoveHandler_Activate();
								await ActivateAsync().ConfigureAwait(false);
							}
						}).ConfigureAwait(false);
					}
					catch (Exception ex)
					{
						Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
					}
				}
			}
		}

		#region construct and dispose
		public Main()
		{
			InitializeComponent();
			NavigationCacheMode = NavigationCacheMode.Enabled;
#if !NOSTORE
			MyPivot.Items.Remove(LogsButton);
#endif
			_activator = new Activator(this);
		}

		//protected override void OnNavigatedFrom(NavigationEventArgs e)
		//{
		//    base.OnNavigatedFrom(e);
		//}

		//private void OnUnloaded(object sender, RoutedEventArgs e)
		//{
		//    Debug.WriteLine("Starting Main.OnUnloaded()");
		//    //_activator.Deactivate();
		//    //            base.OnNavigatingFrom(e);
		//}
		protected override void OnNavigatedTo(NavigationEventArgs e)
		{
			Logger.Add_TPL("Main navigated to", Logger.ForegroundLogFilename, Logger.Severity.Info);
			//Debug.WriteLine("Starting Main.OnNavigatedTo(); RuntimeData.IsSettingsRead = " + MyRuntimeData.IsSettingsRead.ToString());
			//base.OnNavigatedTo(e);

		}
		private void OnLoaded(object sender, RoutedEventArgs e)
		{
			Logger.Add_TPL("Main loaded", Logger.ForegroundLogFilename, Logger.Severity.Info);
			Task readDataAndActivate = _activator.OnLoaded(true);
		}
		//protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
		//{
		//    Debug.WriteLine("Starting Main.OnNavigatingFrom()");
		//    //_activator.Deactivate();
		//    //base.OnNavigatingFrom(e);
		//}
		public void OnResuming()
		{
			Task reactivate = _activator.OnLoaded(false);
		}

		public void Deactivate()
		{
			_animationTimer?.Dispose();
			_animationTimer = null;
			_activator.Deactivate();
		}

		#endregion construct and dispose

		#region event handling
		private Boolean _isDataChangedHandlerActive = false;
		private void AddHandlers()
		{
			if (!_isDataChangedHandlerActive)
			{
				if (MyPersistentData != null) MyPersistentData.PropertyChanged += OnPersistentData_PropertyChanged;
				if (MyRuntimeData.IsHardwareButtonsAPIPresent) HardwareButtons.BackPressed += OnHardwareButtons_BackPressed;
				Windows.UI.Core.SystemNavigationManager.GetForCurrentView().BackRequested += OnTabletSoftwareButton_BackPressed;
				_isDataChangedHandlerActive = true;
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
			MyVM.GoBackHard(sender, e);
		}

		private void OnTabletSoftwareButton_BackPressed(object sender, Windows.UI.Core.BackRequestedEventArgs e)
		{
			MyVM.GoBackTabletSoft(sender, e);
		}

		DispatcherTimerPlus _animationTimer = null;    

		private void OnPersistentData_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
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
					catch (Exception exc)
					{
						Debug.WriteLine("OnPersistentData_PropertyChanged threw " + exc.ToString());
					}
				}
			}
		}
		private void SetShowForAWhileOnly()
		{
			MyVM.IsLastMessageVisible = true;
		}
		private void StopShowingNotice()
		{
			MyVM.IsLastMessageVisible = false;
		}
		private void OnLastMessage_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
		{
			if (!MyVM.IsLastMessageVisible && !string.IsNullOrWhiteSpace(MyVM.MyPersistentData.LastMessage))
			{
				MyVM.IsLastMessageVisible = true;
			}
			else
			{
				MyVM.IsLastMessageVisible = false;
			}
		}
		private void OnGetAFixNow_Click(object sender, RoutedEventArgs e)
		{
			Task vibrate = Task.Run(() => App.ShortVibration());
			_myVM.GetAFix();
		}

		private async void OnClearHistory_Click(object sender, RoutedEventArgs e)
		{
			//raise confirmation popup
			var dialog = new Windows.UI.Popups.MessageDialog("This will delete all the data. Are you sure?", "Confirm deletion");
			UICommand yesCommand = new UICommand("Yes", (command) => { });
			UICommand noCommand = new UICommand("No", (command) => { });
			dialog.Commands.Add(yesCommand);
			dialog.Commands.Add(noCommand);
			// Set the command that will be invoked by default
			dialog.DefaultCommandIndex = 1;
			// Show the message dialog
			IUICommand reply = await dialog.ShowAsync().AsTask();
			if (reply == yesCommand) { Task res = MyPersistentData.ResetHistoryAsync(); }
		}

		private async void OnCenterRoute_Click(object sender, RoutedEventArgs e)
		{
			await _myVM.CentreOnRoute0Async().ConfigureAwait(false);
		}
		private async void OnCenterHistory_Click(object sender, RoutedEventArgs e)
		{
			await _myVM.CentreOnHistoryAsync().ConfigureAwait(false);
		}
		private async void OnCenterLandmarks_Click(object sender, RoutedEventArgs e)
		{
			await _myVM.CentreOnLandmarksAsync().ConfigureAwait(false);
		}
		private async void OnPointsPanel_CentreOnTargetRequested(object sender, EventArgs e)
		{
			await _myVM.CentreOnTargetAsync().ConfigureAwait(false);
		}
		private void OnMapsGoto2DRequested(object sender, EventArgs e)
		{
			_myVM.Goto2D();
		}

		private async void OnLoadRoute0_Click(object sender, RoutedEventArgs e)
		{
			await _myVM.PickLoadSeriesFromFileAsync(PersistentData.Tables.Route0).ConfigureAwait(false);
		}

		private async void OnSaveTrackingHistory_Click(object sender, RoutedEventArgs e)
		{
			await _myVM.PickSaveSeriesToFileAsync(PersistentData.Tables.History, "_" + ConstantData.APPNAME_ALL_IN_ONE + "_Route").ConfigureAwait(false);
		}

		private async void OnSaveRoute0_Click(object sender, RoutedEventArgs e)
		{
			await _myVM.PickSaveSeriesToFileAsync(PersistentData.Tables.Route0, "_" + ConstantData.APPNAME_ALL_IN_ONE + "_Route").ConfigureAwait(false);
		}

		private async void OnClearRoute0_Click(object sender, RoutedEventArgs e)
		{
			await MyPersistentData.ResetRoute0Async().ConfigureAwait(false);
		}

		private async void OnLoadLandmarks_Click(object sender, RoutedEventArgs e)
		{
			await _myVM.PickLoadSeriesFromFileAsync(PersistentData.Tables.Landmarks).ConfigureAwait(false);
		}

		private async void OnClearLandmarks_Click(object sender, RoutedEventArgs e)
		{
			await MyPersistentData.ResetLandmarksAsync().ConfigureAwait(false);
		}

		private async void OnSaveLandmarks_Click(object sender, RoutedEventArgs e)
		{
			await _myVM.PickSaveSeriesToFileAsync(PersistentData.Tables.Landmarks, "_" + ConstantData.APPNAME_ALL_IN_ONE + "_Landmarks").ConfigureAwait(false);
		}

		private void OnCancelDownload_Click(object sender, RoutedEventArgs e)
		{
			_myVM.SetLastMessage_UI("Cancelling download");
			_myVM.CancelDownloadByUser();            
		}

		private async void OnTestFiles_Click(object sender, RoutedEventArgs e)
		{
			string txt = await FileData.GetAllFilesInLocalFolderAsync().ConfigureAwait(false);
			await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, delegate
			{
				MyVM.LogText = txt;
			}).AsTask().ConfigureAwait(false);            
		}

		private async void OnLogButton_Click(object sender, RoutedEventArgs e)
		{
			String cnt = (sender as Button).Content.ToString();
			if (cnt == "FileError")
			{
				MyVM.LogText = await Logger.ReadAsync(Logger.FileErrorLogFilename);
			}
			else if (cnt == "MyPersistentData")
			{
				MyVM.LogText = await Logger.ReadAsync(Logger.PersistentDataLogFilename);
			}
			else if (cnt == "Fgr")
			{
				MyVM.LogText = await Logger.ReadAsync(Logger.ForegroundLogFilename);
			}
			else if (cnt == "Bgr")
			{
				MyVM.LogText = await Logger.ReadAsync(Logger.BackgroundLogFilename);
			}
			else if (cnt == "BgrCanc")
			{
				MyVM.LogText = await Logger.ReadAsync(Logger.BackgroundCancelledLogFilename);
			}
			else if (cnt == "AppExc")
			{
				MyVM.LogText = await Logger.ReadAsync(Logger.AppExceptionLogFilename);
			}
			else if (cnt == "Clear")
			{
				Logger.ClearAll();
			}
		}
		private void OnLogText_Unloaded(object sender, RoutedEventArgs e)
		{
			MyVM.LogText = String.Empty;
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
			MyVM.GoBackMyButtonSoft();
		}

		#endregion event handling

		#region continuations
		public Task<List<PersistentData.Tables>> LoadFileIntoDbAsync(FileActivatedEventArgs args)
		{
			return _myVM.LoadFileIntoDbAsync(args);
		}
		#endregion continuations
	}
}
