﻿using LolloGPS.Data;
using LolloGPS.Data.Constants;
using LolloGPS.Data.Runtime;
using LolloGPS.Suspension;
using System;
using System.Collections;
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
// what's new in VS2015 https://msdn.microsoft.com/en-us/library/bb386063.aspx // TODO check out EventSource now supports writing to the Event log
// new null conditional operators https://msdn.microsoft.com/en-us/library/dn986595.aspx

namespace LolloGPS.Core
{
	/// <summary>
	/// Provides application-specific behavior to supplement the default Application class.
	/// </summary>
	public sealed partial class App : Application
	{
		private static PersistentData _persistentData = null; // PersistentData.GetInstance();
		public static PersistentData PersistentData { get { return _persistentData; } }
		private static Data.Runtime.RuntimeData _myRuntimeData = null; // RuntimeData.GetInstance();
		public static Data.Runtime.RuntimeData MyRuntimeData { get { return _myRuntimeData; } }

		private static bool _isVibrationDevicePresent = Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.Phone.Devices.Notification.VibrationDevice");

		#region construct and dispose
		/// <summary>
		/// Initializes the singleton application object.  This is the first line of authored code
		/// executed, and as such is the logical equivalent of main() or WinMain().
		/// </summary>
		public App()
		{
			Microsoft.ApplicationInsights.WindowsAppInitializer.InitializeAsync(
				Microsoft.ApplicationInsights.WindowsCollectors.Metadata |
				Microsoft.ApplicationInsights.WindowsCollectors.Session);

			Resuming += OnResuming;
			Suspending += OnSuspending;
			UnhandledException += OnApp_UnhandledException;

			InitializeComponent();
		}

		private void OpenData()
		{
			_persistentData = PersistentData.GetInstance();
			_myRuntimeData = RuntimeData.GetInstance();

			PersistentData.OpenTileCacheDb();
			PersistentData.OpenMainDb();

			_isDataOpen = true;

			_myRuntimeData.Activate();
		}
		#endregion construct and dispose

		#region event handlers
		private async void OnApp_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			// this didn't work when the telephone force-shuts the app, it might work now that the call is async
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
			Logger.Add_TPL("OnLaunched()", Logger.ForegroundLogFilename, Logger.Severity.Info);

			OpenData();
			if (!await Licenser.CheckLicensedAsync() /*|| _myRuntimeData.IsBuying*/) return;

			try
			{
				Frame rootFrame = GetCreateRootFrame(e);
				NavigateToRootFrameContent(rootFrame);

				// Ensure the current window is active
				Window.Current.Activate();

				var main = rootFrame.Content as Main;
				await main.OpenAsync(true).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename).ConfigureAwait(false);
			}
		}

		/// <summary>
		/// Invoked when Navigation to a certain page fails
		/// </summary>
		/// <param name="sender">The Frame which failed navigation</param>
		/// <param name="e">Details about the navigation failure</param>
		void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
		{
			throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
		}

		/// <summary>
		/// Invoked when application execution is being suspended.  Application state is saved
		/// without knowing whether the application will be terminated or resumed with the contents
		/// of memory still intact.
		/// </summary>
		/// <param name="sender">The source of the suspend request.</param>
		/// <param name="e">Details about the suspend request.</param>
		private async void OnSuspending(object sender, SuspendingEventArgs e)
		{
			Debugger.Break();
			var deferral = e.SuspendingOperation.GetDeferral();

			await CloseAll().ConfigureAwait(false);
			Logger.Add_TPL("OnSuspending ended", Logger.ForegroundLogFilename, Logger.Severity.Info);

			deferral.Complete();
		}

		private volatile bool _isDataOpen = false;
		public bool IsDataOpen { get { return _isDataOpen; } }
		/// <summary>
		/// Invoked when the app is resumed without being terminated.
		/// You should handle the Resuming event only if you need to refresh any displayed content that might have changed while the app is suspended. 
		/// You do not need to restore other app state when the app resumes.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private async void OnResuming(object sender, object e)
		{
			Debugger.Break();
			Logger.Add_TPL("OnResuming()", Logger.ForegroundLogFilename, Logger.Severity.Info);

			OpenData();
			if (!await Licenser.CheckLicensedAsync() /*|| _myRuntimeData.IsBuying*/) return;

			if (IsRootFrameMain)
			{
				var main = (Window.Current.Content as Frame).Content as Main;
				// Settings and data are already in.
				// However, reread the history coz the background task may have changed it while I was suspended.

				await PersistentData.LoadHistoryFromDbAsync(false);
				await main.OpenAsync(false).ConfigureAwait(false);

				// In simple cases, I don't need to deregister events when suspending and reregister them when resuming, 
				// but I deregister them when suspending to make sure long running tasks are really stopped.
				// This also includes the background task state check.
				// If I stop registering and deregistering events, I must explicitly check for the background state in GPSInteractor, 
				// which may have changed when the app was suspended. For example, the user barred this app running in background while the app was suspended.
			}
			Logger.Add_TPL("OnResuming() ended", Logger.ForegroundLogFilename, Logger.Severity.Info);
		}
		//protected override void OnFileOpenPickerActivated(FileOpenPickerActivatedEventArgs args) // this one never fires...

		/// <summary>
		/// Fires when attempting to open a file, which is associated with the application. Test it with the app running and closed.
		/// </summary>
		protected override async void OnFileActivated(FileActivatedEventArgs e)
		{
			Logger.Add_TPL("App.xaml.cs.OnFileActivated() starting with kind = " + e.Kind.ToString() + " and previous execution state = " + e.PreviousExecutionState.ToString(),
				Logger.ForegroundLogFilename + " and verb = " + e.Verb,
				Logger.Severity.Info);

			try
			{
				OpenData();
				if (!await Licenser.CheckLicensedAsync() /*|| _myRuntimeData.IsBuying*/) return;

				if (e?.Files?[0]?.Path?.Length > 4 && e.Files[0].Path.EndsWith(ConstantData.GPX_EXTENSION, StringComparison.OrdinalIgnoreCase))
				{
					bool isAppAlreadyRunning = IsRootFrameMain;

					Frame rootFrame = GetCreateRootFrame(e);
					if (!isAppAlreadyRunning)
					{
						NavigateToRootFrameContent(rootFrame);
						Window.Current.Activate();
					}

					List<PersistentData.Tables> whichTables = null;
					var fileOpener = Main_VM.GetInstance() as IFileActivatable;
					if (fileOpener != null)
					{
						try
						{
							if (isAppAlreadyRunning)
							{
								Logger.Add_TPL("OnFileActivated() is about to open a file, app already running", Logger.ForegroundLogFilename, Logger.Severity.Info);

								whichTables = await fileOpener.LoadFileIntoDbAsync(e as FileActivatedEventArgs);
								if (whichTables != null)
								{
									// get file data from DB into UI
									foreach (var series in whichTables)
									{
										await PersistentData.LoadSeriesFromDbAsync(series);
									}
									// centre view on the file data
									if (whichTables.Count > 0 && rootFrame?.Content as Main != null)
									{
										Main main = rootFrame.Content as Main;
										if (whichTables[0] == PersistentData.Tables.Landmarks)
										{
											Task centreView = main.MyVM.CentreOnLandmarksAsync();
										}
										else if (whichTables[0] == PersistentData.Tables.Route0)
										{
											Task centreView = main.MyVM.CentreOnRoute0Async();
										}
									}
								}
							}
							else
							{
								Logger.Add_TPL("OnFileActivated() is about to open a file, app not running", Logger.ForegroundLogFilename, Logger.Severity.Info);

								whichTables = await fileOpener.LoadFileIntoDbAsync(e as FileActivatedEventArgs);
								var main = rootFrame.Content as Main;
								if (main != null)
								{
									await main.OpenAsync(true);
									// get file data from DB into UI. // LOLLO TODO MAYBE avoid reading the same series twice?
									foreach (var series in whichTables)
									{
										await PersistentData.LoadSeriesFromDbAsync(series);
									}
									// centre view on the file data
									if (whichTables != null && whichTables.Count > 0)
									{
										if (whichTables[0] == PersistentData.Tables.Landmarks)
										{
											Task centreView = main.MyVM.CentreOnLandmarksAsync();
										}
										else if (whichTables[0] == PersistentData.Tables.Route0)
										{
											Task centreView = main.MyVM.CentreOnRoute0Async();
										}
									}
								}
							}
						}
						catch (Exception ex)
						{
							await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename).ConfigureAwait(false);
						}
						finally
						{
							RuntimeData.SetIsSettingsRead_UI(true);
							RuntimeData.SetIsDBDataRead_UI(true);
						}
					}
				}
				else
				{
					//when opening a lol file, which is required for the log, the application starts and crashes back "elegantly". Very unlikely anyway, because it is not an ordinary extension.
				}
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename).ConfigureAwait(false);
			}
		}
		#endregion event handlers

		#region services
		public async Task Quit()
		{
			await CloseAll();
			Exit();
		}
		private async Task CloseAll()
		{
			Logger.Add_TPL("CloseAll() started", Logger.ForegroundLogFilename, Logger.Severity.Info);

			_isDataOpen = false;

			// unregister events and stop long running tasks.
			if (IsRootFrameMain)
			{
				Main main = (Window.Current.Content as Frame).Content as Main;
				main.Close();
			}
			// back up the app settings
			await SuspensionManager.SaveSettingsAsync(PersistentData).ConfigureAwait(false);
			// lock the DBs
			// await PersistentData.CloseMainDb().ConfigureAwait(false);
			PersistentData.CloseMainDb();
			await PersistentData.CloseTileCacheAsync().ConfigureAwait(false);

			MyRuntimeData?.Dispose();

			Logger.Add_TPL("CloseAll() ended", Logger.ForegroundLogFilename, Logger.Severity.Info);
		}
		private bool IsRootFrameAvailable
		{
			get
			{
				return Window.Current?.Content is Frame;
			}
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
			if (IsRootFrameAvailable)
			{
				rootFrame = Window.Current.Content as Frame;
				rootFrame.Name = "RootFrame";
			}

			if (rootFrame == null)  // Do not repeat app initialization when the Window already has content, just ensure that the window is active
			{
				Logger.Add_TPL("Creating root frame", Logger.ForegroundLogFilename, Logger.Severity.Info);
				// Create a Frame to act as the navigation context and navigate to the first page
				rootFrame = new Frame() { UseLayoutRounding = true };
				rootFrame.Name = "RootFrame";

				// Set the default language
				//rootFrame.Language = Windows.Globalization.ApplicationLanguages.Languages[0];
				rootFrame.Language = Windows.Globalization.Language.CurrentInputMethodLanguageTag; //this is important and decides for the whole app

				// Place the frame in the current Window
				Window.Current.Content = rootFrame;
			}
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
					Logger.Add_TPL("Failed to create initial page", Logger.ForegroundLogFilename);
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
		#endregion services
	}

	/// <summary>
	/// Implement this interface if your page invokes the file open picker
	/// API.
	/// </summary>
	interface IFileActivatable
	{
		/// <summary>
		/// This method is invoked when the app is opened via a file association
		/// files
		/// </summary>
		/// <param name="args">Activated event args object that contains returned files from file open </param>
		Task<List<PersistentData.Tables>> LoadFileIntoDbAsync(FileActivatedEventArgs args);
	}
}
