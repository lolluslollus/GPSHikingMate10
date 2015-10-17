using LolloGPS.Data;
using LolloGPS.GPSInteraction;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Utilz;
using Windows.ApplicationModel.Background;
using Windows.Devices.Geolocation;

namespace BackgroundTasks
{
    // check this: http://msdn.microsoft.com/en-us/library/windowsphone/develop/hh202942(v=vs.105).aspx
    //
    // A background task always implements the IBackgroundTask interface. 
    // It also requires its own project, compiled as a windows runtime component.
    // Reference it in the main project and set CopyLocal = true
    // Add BackgroundTasks.GetLocationBackgroundTask (ie NamespaceName.className) to the "declarations" section of the appmanifest.
    // Throughout the background task, calls to CoreApplication.MainView.CoreWindow.Dispatcher result in errors.
    // Debugging may be easier on x86.
    public sealed class GetLocationBackgroundTask : IBackgroundTask
    {
        CancellationTokenSource _cts = null;
        BackgroundTaskDeferral _deferral = null;
        IBackgroundTaskInstance _taskInstance = null;
        bool _isCancellationAllowedNow = true;
        //
        // The Run method is the entry point of a background task.
        //
        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            try
            {
                // LOLLO Background tasks may take up max 40 MB. We should keep it as stupid as possible,
                // so we only add a line to the db without reading anything.
                // Query BackgroundWorkCost
                // Guidance: If BackgroundWorkCost is high, then perform only the minimum amount
                // of work in the background task and return immediately.
                //
                //
                // Associate a cancellation handler with the background task.
                //
                _cts = new CancellationTokenSource();
                CancellationToken token = _cts.Token;

                _taskInstance = taskInstance;
                _taskInstance.Canceled += new BackgroundTaskCanceledEventHandler(OnCanceled);
                _deferral = _taskInstance.GetDeferral();

                // LOLLO the following fails with an uncatchable exception "System.ArgumentException use of undefined keyword value 1 for event taskscheduled"
                // only in the background task and only if called before GetDeferral and only if awaited
                Logger.Add_TPL("GetLocationBackgroundTask started", Logger.BackgroundLogFilename, Logger.Severity.Info);

                if (!GetLocBackgroundTaskSemaphoreManager.TryOpenExisting()) // if app is not running. Otherwise, skip.
                {
                    PersistentData myData = PersistentData.GetInstance(); // note that PersistentData.GetInstance() is always an empty instance, because i am in a separate process
                    Debug.WriteLine("GetLocationBackgroundTask done initialising variables");

                    //TODO maybe add a location trigger ? There is such a thing! But the appmanifest does not seem to support it...

                    _taskInstance.Progress = 1; // we don't need this but we leave it in case we change something and we want to check when the bkg task starts.

                    // I took away the following to save performance and memory (max 40 MB is allowed in background tasks)
                    //SuspensionManager.LoadDataAsync(_myData, false).Wait(token); //read the last saved settings and the history from the db, skipping the route.

                    //this takes time
                    Geolocator geolocator = new Geolocator() { DesiredAccuracyInMeters = myData.DesiredAccuracyInMeters }; //, ReportInterval = myDataModel.ReportIntervalInMilliSec };
                    Geoposition pos = null;
                    if (geolocator != null) pos = await geolocator.GetGeopositionAsync().AsTask(token).ConfigureAwait(false);

                    Debug.WriteLine("GetLocationBackgroundTask done getting geoposition");

                    // save to the db, synchronously, otherwise the background task may be cancelled before the db is updated.
                    // this would fail to save the new value and leave around named semaphores, which block everything else.

                    if (_taskInstance != null) _taskInstance.Canceled -= OnCanceled; // do not interrupt the db operation
                    if (pos != null)
                    {
                        _isCancellationAllowedNow = false;
                        if (PersistentData.IsMainDbClosed)
                        {
                            Debug.WriteLine("GetLocationBackgroundTask found the main db closed");
                            PersistentData.OpenMainDb();
                            bool isSaved = GPSInteractor.AppendGeoPosition(myData, pos, true);
                            await PersistentData.CloseMainDbAsync().ConfigureAwait(false);
                            Debug.WriteLine("GetLocationBackgroundTask saved into db: " + isSaved);
                        }
                        else
                        {
                            Debug.WriteLine("GetLocationBackgroundTask found the main db open");
                            // if the main db is unlocked shortly, this may fail to save the record: you can bear with it, it is very unlikely anyway.
                            bool isSaved = GPSInteractor.AppendGeoPosition(myData, pos, true);
                            Debug.WriteLine("GetLocationBackgroundTask saved into db: " + isSaved);
                            // if you want to be sure the record is saved:
                            //if (!isSaved && PersistentData.IsMainDbClosed)
                            //{
                            //    PersistentData.OpenMainDb();
                            //    GPSInteractor.AppendGeoPosition(myData, pos, true);
                            //    await PersistentData.CloseMainDbAsync().ConfigureAwait(false);
                            //}
                        }
                        _isCancellationAllowedNow = true;
                    }
                    Debug.WriteLine("GetLocationBackgroundTask done saving geoposition");
                }
            }
            catch (Exception exc0)
            {
                await Logger.AddAsync(exc0.ToString(), Logger.BackgroundLogFilename).ConfigureAwait(false);
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                if (_taskInstance != null) _taskInstance.Canceled -= OnCanceled;
                Logger.Add_TPL("GetLocationBackgroundTask ended", Logger.BackgroundLogFilename, Logger.Severity.Info);
                _deferral?.Complete();
            }
        }

        private async void OnCanceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            if (_isCancellationAllowedNow)
            {
                _cts?.Cancel();
            }
            await Logger.AddAsync("Ending method GetLocationBackgroundTask.OnCanceledAsync() with reason = " + reason + "; _isCancellationAllowedNow = " + _isCancellationAllowedNow, Logger.BackgroundCancelledLogFilename, Logger.Severity.Info).ConfigureAwait(false);
        }

        //
        // Simulate the background task activity.
        //
        //private void PeriodicTimerCallback(ThreadPoolTimer timer)
        //{
        //    if ((_cancelRequested == false) && (_progress < 100))
        //    {
        //        _progress += 10;
        //        _taskInstance.Progress = _progress;
        //    }
        //    else
        //    {
        //        _periodicTimer?.Cancel();

        //        var settings = ApplicationData.Current.LocalSettings;
        //        var key = _taskInstance.Task.Name;

        //        //
        //        // Write to LocalSettings to indicate that this background task ran.
        //        //
        //        settings.Values[key] = (_progress < 100) ? "Canceled with reason: " + _cancelReason.ToString() : "Completed";
        //        Debug.WriteLine("Background " + _taskInstance.Task.Name + settings.Values[key]);

        //        //
        //        // Indicate that the background task has completed.
        //        //
        //        _deferral.Complete();
        //    }
        //}
    }
}