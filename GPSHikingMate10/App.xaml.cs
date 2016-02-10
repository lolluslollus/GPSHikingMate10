using LolloGPS.Data;
using LolloGPS.Data.Runtime;
using LolloGPS.Suspension;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Utilz;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.Phone.Devices.Notification;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

// The Pivot Application template is documented at http://go.microsoft.com/fwlink/?LinkID=391641
// decent get started tutorial: http://msdn.microsoft.com/en-us/library/windows/apps/Hh986968.aspx
// to request map token https://msdn.microsoft.com/en-us/library/windows/apps/xaml/mt219694.aspx
// check https://msdn.microsoft.com/en-us/library/windows/apps/dn706236.aspx for problems when starting on the simulator
// check out https://msdn.microsoft.com/en-us/library/windows/apps/dn609832.aspx for win 10 universal apps
// what's new in VS2015 https://msdn.microsoft.com/en-us/library/bb386063.aspx
// new null conditional operators https://msdn.microsoft.com/en-us/library/dn986595.aspx

namespace LolloGPS.Core
{
	/// <summary>
	/// Provides application-specific behavior to supplement the default Application class.
	/// </summary>
	public sealed partial class App : Application
	{
		#region properties
		private static PersistentData _persistentData = null; // PersistentData.GetInstance();
		public static PersistentData PersistentData { get { return _persistentData; } }
		private static RuntimeData _runtimeData = null; // RuntimeData.GetInstance();
		public static RuntimeData RuntimeData { get { return _runtimeData; } }
		
		private static readonly bool _isVibrationDevicePresent = Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.Phone.Devices.Notification.VibrationDevice");
		private static readonly SemaphoreSlimSafeRelease _resumingActivatingSemaphore = new SemaphoreSlimSafeRelease(1, 1);

		private static volatile bool _isResuming = false;
		public static bool IsResuming { get { return _isResuming; } private set { _isResuming = value; } }
		#endregion properties


		#region events
		// these events are useful to avoid "the action was marshalled for a different thread" errors. 
		// We don't want to hog the UI thread just to catch these, and we want the response to be immediate.
		public static event EventHandler ResumingStatic;
		public static event SuspendingEventHandler SuspendingStatic;
		#endregion events


		#region lifecycle
		/// <summary>
		/// Initializes the singleton application object.  This is the first line of authored code
		/// executed, and as such is the logical equivalent of main() or WinMain().
		/// </summary>
		public App()
		{
			Microsoft.ApplicationInsights.WindowsAppInitializer.InitializeAsync(
				Microsoft.ApplicationInsights.WindowsCollectors.Metadata |
				Microsoft.ApplicationInsights.WindowsCollectors.Session);

			UnhandledException += OnApp_UnhandledException;
			Resuming += OnResuming;
			Suspending += OnSuspending;

			InitializeComponent();

			Logger.Add_TPL("App ctor ended OK", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
		}

		private async Task OpenDataAsync()
		{
			_persistentData = PersistentData.GetInstance();
			_runtimeData = RuntimeData.GetInstance();

			await PersistentData.OpenTileCacheDbAsync().ConfigureAwait(false);
			PersistentData.OpenMainDb();
		}

		private async Task CloseAllAsync()
		{
			Debug.WriteLine("CloseAllAsync() started");
			// lock the DBs
			PersistentData.CloseMainDb();
			Debug.WriteLine("CloseAllAsync() closed the main db");
			await PersistentData.CloseTileCacheAsync(); //.ConfigureAwait(false);
			Debug.WriteLine("CloseAllAsync() closed the tile cache");
			// unregister events and stop long running tasks.
			Main main = null;
			await RunInUiThreadAsync(delegate
			{
				main = (Window.Current?.Content as Frame)?.Content as Main;
			});
			if (main != null)
			{
				await main.CloseAsync().ConfigureAwait(false);
			}

			Debug.WriteLine("CloseAllAsync() closed the UI");
			//// lock the DBs
			//PersistentData.CloseMainDb();
			//await PersistentData.CloseTileCacheAsync().ConfigureAwait(false);
			// back up the app settings
			await SuspensionManager.SaveSettingsAsync(PersistentData).ConfigureAwait(false);
			Debug.WriteLine("CloseAllAsync() saved the settings");
			RuntimeData?.Close();
		}
		#endregion lifecycle


		#region event handlers
		private async void OnApp_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			// this does not always work when the device force-shuts the app
			await Logger.AddAsync("UnhandledException: " + e.Exception.ToString(), Logger.AppExceptionLogFilename);
		}

		/// <summary>
		/// Invoked when the application is launched normally by the end user.  Other entry points
		/// will be used when the application is launched to open a specific file, to display
		/// search results, and so forth.
		/// This is also invoked when the app is resumed after being terminated.
		/// </summary>
		/// <param name="e">Details about the launch request and process.</param>
		protected async override void OnLaunched(LaunchActivatedEventArgs e)
		{
			Logger.Add_TPL("OnLaunched started with " + " arguments = " + e.Arguments + " and kind = " + e.Kind.ToString() + " and prelaunch activated = " + e.PrelaunchActivated + " and prev exec state = " + e.PreviousExecutionState.ToString(),
				Logger.AppEventsLogFilename,
				Logger.Severity.Info,
				false);

			await OpenDataAsync();
			if (!await Licenser.GetInstance().CheckLicensedAsync() /*|| _runtimeData.IsBuying*/) return;

			try
			{
				Frame rootFrame = GetCreateRootFrame(e);
				NavigateToRootFrameContent(rootFrame);
				// Ensure the current window is active
				Window.Current.Activate();
				// disable UI commands
				RuntimeData.SetIsDBDataRead_UI(false);

				var main = rootFrame.Content as Main;
				await SuspensionManager.LoadDbDataAndSettingsAsync(true, true);
				//Task readData = SuspensionManager.LoadDbDataAndSettingsAsync(true, true); // this makes trouble, 
				// it is pointless to do too much in separate threads when the UI is only ready once the data is loaded anyway.
				// the UI does not freeze, this is as much as it make sense to achieve.
				var yne = await main.OpenAsync().ConfigureAwait(false);
				Logger.Add_TPL("OnLaunched opened main with result = " + yne, Logger.AppEventsLogFilename, Logger.Severity.Info, false);
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.AppEventsLogFilename).ConfigureAwait(false);
			}

			// enable UI commands
			RuntimeData.SetIsSettingsRead_UI(true);
			RuntimeData.SetIsDBDataRead_UI(true);

			Logger.Add_TPL("OnLaunched ended", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
		}

		/// <summary>
		/// Invoked when Navigation to a certain page fails
		/// </summary>
		/// <param name="sender">The Frame which failed navigation</param>
		/// <param name="e">Details about the navigation failure</param>
		private async void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
		{
			await Logger.AddAsync(e.Exception.ToString(), Logger.AppEventsLogFilename);
			throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
		}

		/// <summary>
		/// Invoked when application execution is being suspended.  Application state is saved
		/// without knowing whether the application will be terminated or resumed with the contents
		/// of memory still intact.
		/// LOLLO NOTE This method must complete within the deadline, which is 5 sec with a slow phone.
		/// </summary>
		/// <param name="sender">The source of the suspend request.</param>
		/// <param name="e">Details about the suspend request.</param>
		private async void OnSuspending(object sender, SuspendingEventArgs e)
		{
			var deferral = e.SuspendingOperation.GetDeferral();

			Logger.Add_TPL("OnSuspending started with suspending operation deadline = " + e.SuspendingOperation.Deadline.ToString(),
				Logger.AppEventsLogFilename,
				Logger.Severity.Info,
				false);

			SuspendingStatic?.Invoke(this, e);

			await CloseAllAsync().ConfigureAwait(false);
			Logger.Add_TPL("OnSuspending ended OK", Logger.AppEventsLogFilename, Logger.Severity.Info, false);

			deferral.Complete();
		}

		/// <summary>
		/// Invoked when the app is resumed without being terminated.
		/// You should handle the Resuming event only if you need to refresh any displayed content that might have changed while the app is suspended. 
		/// You do not need to restore other app state when the app resumes.
		/// </summary>
		private async void OnResuming(object sender, object e)
		{
			Logger.Add_TPL("OnResuming started", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
			try
			{
				await _resumingActivatingSemaphore.WaitAsync();
				ResumingStatic?.Invoke(this, EventArgs.Empty);
				IsResuming = true;
				Logger.Add_TPL("OnResuming started is in the semaphore", Logger.AppEventsLogFilename, Logger.Severity.Info, false);

				await OpenDataAsync();
				if (!await Licenser.GetInstance().CheckLicensedAsync() /*|| _runtimeData.IsBuying*/) return;

				if (IsRootFrameMain)
				{
					// disable UI commands
					RuntimeData.SetIsDBDataRead_UI(false);

					var main = (Window.Current.Content as Frame).Content as Main;
					// Settings and data are already in.
					// However, reread the history coz the background task may have changed it while I was suspended.
					await PersistentData.LoadHistoryFromDbAsync(false);
					Logger.Add_TPL("OnResuming() has read history from db", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
					// In simple cases, I don't need to deregister events when suspending and reregister them when resuming, 
					// but I deregister them when suspending to make sure long running tasks are really stopped.
					// This also includes the background task state check.
					// If I stop registering and deregistering events, I must explicitly check for the background state in GPSInteractor, 
					// which may have changed when the app was suspended. For example, the user barred this app running in background while the app was suspended.
					var yne = await main.OpenAsync().ConfigureAwait(false);
					Logger.Add_TPL("OnResuming() has opened main with result = " + yne, Logger.AppEventsLogFilename, Logger.Severity.Info, false);
				}
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.AppEventsLogFilename);
			}
			finally
			{
				// enable UI commands
				RuntimeData.SetIsSettingsRead_UI(true);
				RuntimeData.SetIsDBDataRead_UI(true);

				Logger.Add_TPL("OnResuming ended", Logger.AppEventsLogFilename, Logger.Severity.Info, false);

				IsResuming = false;
				SemaphoreSlimSafeRelease.TryRelease(_resumingActivatingSemaphore);
			}
		}
		//protected override void OnFileOpenPickerActivated(FileOpenPickerActivatedEventArgs args) // this one never fires...

		/// <summary>
		/// Fires when attempting to open a file, which is associated with the application. 
		/// Test it with the app both running and closed.
		/// LOLLO NOTE if resuming (ie the app was running), the system starts this method an instant after OnResuming(). 
		/// The awaits cause both methods to run in parallel, alternating on the UI thread.
		/// </summary>
		protected override async void OnFileActivated(FileActivatedEventArgs args)
		{
			Logger.Add_TPL("OnFileActivated() starting with kind = " + args.Kind.ToString() + " and previous execution state = " + args.PreviousExecutionState.ToString() + " and verb = " + args.Verb,
				Logger.AppEventsLogFilename,
				Logger.Severity.Info,
				false);
			try
			{
				await _resumingActivatingSemaphore.WaitAsync();
				Logger.Add_TPL("OnFileActivated() is in the semaphore", Logger.AppEventsLogFilename, Logger.Severity.Info, false);

				await OpenDataAsync();

				bool isAppAlreadyRunning = IsRootFrameMain;
				if (!isAppAlreadyRunning)
				{
					if (!await Licenser.GetInstance().CheckLicensedAsync() /*|| _runtimeData.IsBuying*/) return;
					Logger.Add_TPL("OnFileActivated() checked the license", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
				}

				if (args?.Files?[0]?.Path?.Length > 4 && args.Files[0].Path.EndsWith(ConstantData.GPX_EXTENSION, StringComparison.OrdinalIgnoreCase))
				{
					Frame rootFrame = GetCreateRootFrame(args);
					if (!isAppAlreadyRunning)
					{
						NavigateToRootFrameContent(rootFrame);
						Window.Current.Activate();
					}
					// disable UI commands
					RuntimeData.SetIsDBDataRead_UI(false);

					var main = rootFrame.Content as Main;
					if (main != null)
					{
						await SuspensionManager.LoadDbDataAndSettingsAsync(false, !isAppAlreadyRunning);
						var yne = await main.OpenAsync();
						Logger.Add_TPL("OnFileActivated() opened main with result = " + yne + ", app already running", Logger.AppEventsLogFilename, Logger.Severity.Info, false);

						var whichTables = await main.LoadFileIntoDbAsync(args as FileActivatedEventArgs);
						Logger.Add_TPL("OnFileActivated() got whichTables", Logger.AppEventsLogFilename, Logger.Severity.Info, false);

						if (isAppAlreadyRunning)
						{
							if (whichTables != null)
							{
								// get file data from DB into UI
								foreach (var series in whichTables)
								{
									await PersistentData.LoadSeriesFromDbAsync(series, false);
									Logger.Add_TPL("just got series " + series.ToString() + " into UI", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
								}
							}
						}
						else
						{
							// get all data from DB into UI
							await SuspensionManager.LoadDbDataAndSettingsAsync(true, false);
						}

						// centre view on the file data
						if (whichTables?.Count > 0)
						{
							var mainVM = main?.MainVM;
							if (mainVM != null)
							{
								if (whichTables[0] == PersistentData.Tables.Checkpoints)
								{
									Task centreView = Task.Run(mainVM.CentreOnCheckpointsAsync);
								}
								else if (whichTables[0] == PersistentData.Tables.Route0)
								{
									Task centreView = Task.Run(mainVM.CentreOnRoute0Async);
								}
							}
						}

						Logger.Add_TPL("OnFileActivated() ended proc OK", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
					}
				}
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.AppEventsLogFilename);
			}
			finally
			{
				// enable UI commands
				RuntimeData.SetIsSettingsRead_UI(true);
				RuntimeData.SetIsDBDataRead_UI(true);

				Logger.Add_TPL("OnFileActivated() ended", Logger.AppEventsLogFilename, Logger.Severity.Info, false);

				SemaphoreSlimSafeRelease.TryRelease(_resumingActivatingSemaphore);
			}
		}
		#endregion event handlers


		#region services
		public async Task Quit()
		{
			await CloseAllAsync();
			Exit();
		}
		private bool IsRootFrameMain
		{
			get
			{
				return (Window.Current?.Content as Frame)?.Content is Main;
			}
		}
		private Frame GetCreateRootFrame(IActivatedEventArgs e) //(LaunchActivatedEventArgs e) was
		{
			Frame rootFrame = null;
			if (Window.Current?.Content is Frame)
			{
				rootFrame = Window.Current.Content as Frame;
			}

			if (rootFrame == null)  // Do not repeat app initialization when the Window already has content, just ensure that the window is active
			{
				// Create a Frame to act as the navigation context and navigate to the first page
				rootFrame = new Frame() { UseLayoutRounding = true };

				// Set the default language
				//rootFrame.Language = Windows.Globalization.ApplicationLanguages.Languages[0];
				rootFrame.Language = Windows.Globalization.Language.CurrentInputMethodLanguageTag; // LOLLO NOTE this is important and decides for the whole app

				// Place the frame in the current Window
				Window.Current.Content = rootFrame;
			}

			rootFrame.Name = "RootFrame";
			return rootFrame;
		}

		private void NavigateToRootFrameContent(Frame rootFrame)
		{
			if (rootFrame != null && rootFrame.Content == null)
			{
				// Logger.Add_TPL("rootFrame.Content == null, about to navigate to Main", Logger.ForegroundLogFilename);
				// When the navigation stack isn't restored navigate to the first page,
				// configuring the new page by passing required information as a navigation
				// parameter (in theory, but no parameter here, we simply navigate)
				if (!rootFrame.Navigate(typeof(Main)))
				{
					Logger.Add_TPL("Failed to create initial page", Logger.AppEventsLogFilename);
					throw new Exception("Failed to create initial page");
				}
			}
		}

		public static void ShortVibration()
		{
			if (_isVibrationDevicePresent)
			{
				VibrationDevice myDevice = VibrationDevice.GetDefault();
				myDevice.Vibrate(TimeSpan.FromSeconds(.12));
			}
		}

		private async Task RunInUiThreadAsync(DispatchedHandler action)
		{
			try
			{
				if (CoreApplication.MainView.CoreWindow.Dispatcher?.HasThreadAccess == true)
				{
					action();
				}
				else
				{
					await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, action).AsTask().ConfigureAwait(false);
				}
			}
			catch (InvalidOperationException) // called from a background task: ignore
			{ }
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
			}
		}
		#endregion services
	}

	//interface IFileActivatable
	//{
	//	/// <summary>
	//	/// This method is invoked when the app is opened via a file association
	//	/// files
	//	/// </summary>
	//	/// <param name="args">Activated event args object that contains returned files from file open </param>
	//	Task<List<PersistentData.Tables>> LoadFileIntoDbAsync(FileActivatedEventArgs args);
	//}
}