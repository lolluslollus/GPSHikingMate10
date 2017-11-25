using LolloGPS.Data;
using LolloGPS.Data.Runtime;
using LolloGPS.Services;
using LolloGPS.Suspension;
using System;
using Utilz;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

// LOLLO NOTE new app lifecycle https://msdn.microsoft.com/windows/uwp/launch-resume/app-lifecycle

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
    public sealed partial class App : Application, ISuspenderResumer
    {
        #region properties
        private static PersistentData _persistentData = null;
        public static PersistentData PersistentData => _persistentData;
        private static RuntimeData _runtimeData = null;
        public static RuntimeData RuntimeData => _runtimeData;

        private static readonly SemaphoreSlimSafeRelease _startingSemaphore = new SemaphoreSlimSafeRelease(1, 1);
        #endregion properties

        #region events
        public event SuspendingEventHandler SuspendStarted;
        public event EventHandler ResumeStarted;
        // interesting: the new event handlers with more control
        //event EventHandler ISuspenderResumer.ResumeStarted
        //{
        //    add
        //    {
        //        throw new NotImplementedException();
        //    }

        //    remove
        //    {
        //        throw new NotImplementedException();
        //    }
        //}
        //event SuspendingEventHandler ISuspenderResumer.SuspendStarted
        //{
        //    add
        //    {
        //        throw new NotImplementedException();
        //    }

        //    remove
        //    {
        //        throw new NotImplementedException();
        //    }
        //}
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

            Resuming += OnResuming;
            Suspending += OnSuspending;
            UnhandledException += OnUnhandledException;

            InitializeComponent();

            Logger.Add_TPL("App ctor ended OK", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
        }
        #endregion lifecycle


        #region event handlers
        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used when the application is launched to open a specific file, to display
        /// search results, and so forth.
        /// This is also invoked when the app is resumed after being terminated.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override async void OnLaunched(LaunchActivatedEventArgs e)
        {
            Logger.Add_TPL("OnLaunched started with " + " arguments = " + e.Arguments + " and kind = " + e.Kind.ToString() + " and prelaunch activated = " + e.PrelaunchActivated + " and prev exec state = " + e.PreviousExecutionState.ToString(),
                Logger.AppEventsLogFilename,
                Logger.Severity.Info,
                false);

            e.SplashScreen.Dismissed -= OnSplashScreen_Dismissed;
            e.SplashScreen.Dismissed += OnSplashScreen_Dismissed;

            try
            {
                await _startingSemaphore.WaitAsync();
                _runtimeData = RuntimeData.GetInstance();
                _persistentData = await SuspensionManager.LoadSettingsAsync();

                Frame rootFrame = GetCreateRootFrame(e);
                NavigateToRootFrameContent(rootFrame, Utilz.Controlz.OpenableObservablePage.NavigationParameters.Launched);
                // Ensure the current window is active
                Window.Current.Activate();
            }
            catch (Exception ex)
            {
                await Logger.AddAsync(ex.ToString(), Logger.AppEventsLogFilename).ConfigureAwait(false);
            }
            finally
            {
                SemaphoreSlimSafeRelease.TryRelease(_startingSemaphore);
            }
        }

        /// <summary>
        /// Let the app launch, then check if the license is OK. Otherwise, there will be trouble with the cert kit and light slowdowns on launch.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private async void OnSplashScreen_Dismissed(SplashScreen sender, object args)
        {
            if (sender != null) sender.Dismissed -= OnSplashScreen_Dismissed;
            if (!await Licenser.GetInstance().CheckLicensedAsync()) Quit();
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
        /// <param name="args">Details about the suspend request.</param>
        private async void OnSuspending(object sender, SuspendingEventArgs args)
        {
            var deferral = args.SuspendingOperation.GetDeferral();
            Logger.Add_TPL("OnSuspending started with suspending operation deadline = " + args.SuspendingOperation.Deadline.ToString(),
                Logger.AppEventsLogFilename,
                Logger.Severity.Info,
                false);
            try
            {
                // first of all
                await SuspensionManager.SaveSettingsAsync(_persistentData);

                // try closing the subscribers tidily...
                // notify the subscribers (eg Main.cs)
                SuspendStarted?.Invoke(this, args);
                // make sure the subscribers are all closed.
                await SuspenderResumerExtensions.WaitForIOpenableSubscribers(this, SuspendStarted?.GetInvocationList(), false).ConfigureAwait(false);

                //first to come, last to go
                RuntimeData?.Close();

                Logger.Add_TPL("OnSuspending ended OK", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
            }
            catch (Exception ex)
            {
                Logger.Add_TPL(ex.ToString(), Logger.AppEventsLogFilename);
            }
            finally
            {
                deferral.Complete();
            }
        }

        /// <summary>
        /// Invoked when the app is resumed without being terminated.
        /// You should handle the Resuming event only if you need to refresh any displayed content that might have changed while the app is suspended. 
        /// You do not need to restore other app state when the app resumes.
        /// </summary>
        private async void OnResuming(object sender, object e)
        {
            // In simple cases, I don't need to deregister events when suspending and reregister them when resuming, 
            // but I deregister them when suspending to make sure long running tasks are really stopped.
            // This also includes the background task state check.
            // If I stop handling events, I must explicitly check for the background state in GPSInteractor, 
            // which may have changed when the app was suspended. For example, the user barred this app running in background while the app was suspended.
            // This is done in the MainVM, which is subscribed to ResumeStarted.
            Logger.Add_TPL("OnResuming started", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
            try
            {
                await _startingSemaphore.WaitAsync();
                _runtimeData = RuntimeData.GetInstance();
                // notify the subscribers; we don't do anything else here.
                ResumeStarted?.Invoke(this, EventArgs.Empty);
                // make sure the subscribers are all open before proceeding
                await SuspenderResumerExtensions.WaitForIOpenableSubscribers(this, ResumeStarted?.GetInvocationList(), true).ConfigureAwait(false);
                Logger.Add_TPL("the subscribers to ResumeStarted are open", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
            }
            catch (Exception ex)
            {
                await Logger.AddAsync(ex.ToString(), Logger.AppEventsLogFilename);
            }
            finally
            {
                SemaphoreSlimSafeRelease.TryRelease(_startingSemaphore);
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
                await _startingSemaphore.WaitAsync();

                bool isAppAlreadyRunning = IsRootFrameMain;
                if (!isAppAlreadyRunning)
                {
                    if (!await Licenser.GetInstance().CheckLicensedAsync()) return;
                }

                if (args?.Files?[0]?.Path?.Length > 4 && args.Files[0].Path.EndsWith(ConstantData.GPX_EXTENSION, StringComparison.OrdinalIgnoreCase))
                {
                    _runtimeData = RuntimeData.GetInstance();

                    Frame rootFrame = null;
                    if (!isAppAlreadyRunning)
                    {
                        _persistentData = await SuspensionManager.LoadSettingsAsync();
                        rootFrame = GetCreateRootFrame(args);
                        NavigateToRootFrameContent(rootFrame, Utilz.Controlz.OpenableObservablePage.NavigationParameters.FileActivated);
                        Window.Current.Activate();
                    }
                    else
                    {
                        rootFrame = GetCreateRootFrame(args);
                    }

                    var main = rootFrame.Content as Main;
                    if (main == null) throw new Exception("OnFileActivated: main is null");
                    await main.FileActivateAsync(args).ConfigureAwait(false);
                }
                Logger.Add_TPL("OnFileActivated() ended ok", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
            }
            catch (Exception ex)
            {
                await Logger.AddAsync(ex.ToString(), Logger.AppEventsLogFilename).ConfigureAwait(false);
            }
            finally
            {
                SemaphoreSlimSafeRelease.TryRelease(_startingSemaphore);
            }
        }

        private async void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            // this does not always work when the device force-shuts the app
            LogException(e?.Exception);
            await Logger.AddAsync($"UnhandledException: {e.Exception.ToString()}{Environment.NewLine}---StackTrace:---{Environment.NewLine}{e.Exception.StackTrace}{Environment.NewLine}---InnerException:---{Environment.NewLine}{e.Exception.InnerException?.ToString()}---InnerException.StackTrace:---{Environment.NewLine}{e.Exception.InnerException?.StackTrace}", Logger.AppExceptionLogFilename).ConfigureAwait(false);
        }
        #endregion event handlers


        #region services
        public void LogException(Exception exc)
        {
            if (exc == null) Logger.Add_TPL("Unknown exception", Logger.AppEventsLogFilename);
            else Logger.Add_TPL($"UnhandledException: {exc.ToString()}{Environment.NewLine}---StackTrace:---{Environment.NewLine}{exc.StackTrace}{Environment.NewLine}---InnerException:---{Environment.NewLine}{exc.InnerException?.ToString()}---InnerException.StackTrace:---{Environment.NewLine}{exc.InnerException?.StackTrace}", Logger.AppExceptionLogFilename);
        }
        public void Quit()
        {
            RuntimeData?.Close();
            Exit();
        }
        private bool IsRootFrameMain
        {
            get
            {
                return (Window.Current?.Content as Frame)?.Content is Main;
            }
        }
        private Frame GetCreateRootFrame(IActivatedEventArgs e)
        {
            Frame rootFrame = null;
            if (Window.Current?.Content is Frame)
            {
                rootFrame = Window.Current.Content as Frame;
            }

            if (rootFrame == null)  // Do not repeat app initialization when the Window already has content, just ensure that the window is active
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame
                {
                    UseLayoutRounding = true,
                    // Set the default language
                    //rootFrame.Language = Windows.Globalization.ApplicationLanguages.Languages[0];
                    // LOLLO NOTE this is important and decides for the whole app
                    Language = Windows.Globalization.Language.CurrentInputMethodLanguageTag
                };

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }

            rootFrame.Name = "RootFrame";
            return rootFrame;
        }

        private void NavigateToRootFrameContent(Frame rootFrame, object navigationParameter)
        {
            if (rootFrame != null && rootFrame.Content == null)
            {
                // When the navigation stack isn't restored navigate to the first page,
                // configuring the new page by passing required information as a navigation
                // parameter (in theory, but no parameter here, we simply navigate)
                if (!rootFrame.Navigate(typeof(Main), navigationParameter))
                {
                    Logger.Add_TPL("Failed to create initial page", Logger.AppEventsLogFilename);
                    throw new Exception("Failed to create initial page");
                }
            }
        }
        #endregion services
    }
}