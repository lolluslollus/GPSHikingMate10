﻿using LolloGPS.Data;
using LolloGPS.GPSInteraction;
using System;
using System.Diagnostics;
using System.Text;
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
    // Don't forget to add a reference to the bkg task project to the calling app
    public sealed class GetLocationBackgroundTask : IBackgroundTask
    {
        private volatile SafeCancellationTokenSource _cts = null;
        private volatile BackgroundTaskDeferral _deferral = null;
        private volatile IBackgroundTaskInstance _taskInstance = null;
        private volatile StringBuilder _stringBuilder = null;

        /// <summary>
        /// The Run method is the entry point of a background task.
        /// </summary>
        /// <param name="taskInstance"></param>
        public void Run(IBackgroundTaskInstance taskInstance)
        {
            try
            {
                // LOLLO Background tasks may take up max 40 MB. There is also a time limit I believe.
                // We should keep it as stupid as possible,
                // so we only add a line to the db without reading anything.

                // Query BackgroundWorkCost
                // Guidance: If BackgroundWorkCost is high, then perform only the minimum amount
                // of work in the background task and return immediately.

                _deferral = taskInstance.GetDeferral();
                _taskInstance = taskInstance;
                _taskInstance.Canceled += OnCanceled;
                _cts = new SafeCancellationTokenSource();
                var cancToken = _cts.Token;
                _stringBuilder = new StringBuilder();

                // LOLLO the following fails with an uncatchable exception "System.ArgumentException use of undefined keyword value 1 for event taskscheduled"
                // only in the background task and only if called before GetDeferral and only if awaited
                //Logger.Add_TPL("GetLocationBackgroundTask started", Logger.BackgroundTaskLogFilename, Logger.Severity.Info, false);
                _stringBuilder.AppendLine($"GetLocationBackgroundTask starting at {DateTime.Now.ToString(Logger.DATE_TIME_FORMAT)}");

                if (GetLocBackgroundTaskSemaphoreManager.GetMainAppIsRunningAndActive())
                {
                    _stringBuilder.AppendLine($"GetLocationBackgroundTask returning coz the app is running and active at {DateTime.Now.ToString(Logger.DATE_TIME_FORMAT)}");
                    return; // the app is running, it will catch the background task running: do nothing
                }

                // _taskInstance.Progress = 1; // we don't need this but we leave it in case we change something and we want to check when the bkg task starts.

                // I took away the following to save performance and memory (max 40 MB is allowed in background tasks)
                //SuspensionManager.LoadDataAsync(_myData, false).Wait(token); //read the last saved settings and the history from the db, skipping the route.

                ////step by step process
                //Geolocator geolocator = new Geolocator() { DesiredAccuracyInMeters = PersistentData.DefaultDesiredAccuracyInMetres }; //, ReportInterval = myDataModel.ReportIntervalInMilliSec };
                //Geoposition pos = null;
                //if (geolocator != null)
                //{
                //	pos = await geolocator.GetGeopositionAsync().AsTask(token).ConfigureAwait(false);
                //}
                //PointRecord newDataRecord = null;
                //if (pos != null)
                //{
                //	newDataRecord = GPSInteractor.GetNewHistoryRecord(pos);
                //}
                //bool isSaved = false;
                //if (newDataRecord != null)
                //{
                //	isSaved = PersistentData.RunDbOpInOtherTask(delegate
                //	{
                //		return PersistentData.TryAddHistoryRecordOnlyDb(newDataRecord, true);
                //	});
                //}

                // memory saving process
                var pos = GetGeopositionAsync(cancToken).Result;
                var newDataRecord = GPSInteractor.GetNewHistoryRecord(pos);
                if (cancToken.IsCancellationRequested)
                {
                    _stringBuilder.AppendLine($"GetLocationBackgroundTask returning coz cancellation was requested at {DateTime.Now.ToString(Logger.DATE_TIME_FORMAT)}");
                    return;
                }

                _stringBuilder.AppendLine("GetLocationBackgroundTask is about to save the new geoPosition");
                bool isSaved = PersistentData.RunDbOpInOtherTask(() => PersistentData.TryAddHistoryRecordOnlyDb(newDataRecord, true));
                _stringBuilder.AppendLine($"GetLocationBackgroundTask has saved the new geoPosition: {isSaved}");
            }
            catch (ObjectDisposedException) // comes from the cts
            {
                _stringBuilder.AppendLine($"GetLocationBackgroundTask ran into ObjectDisposedException at {DateTime.Now.ToString(Logger.DATE_TIME_FORMAT)}");
            }
            catch (OperationCanceledException) // comes from the cts
            {
                _stringBuilder.AppendLine($"GetLocationBackgroundTask ran into OperationCanceledException at {DateTime.Now.ToString(Logger.DATE_TIME_FORMAT)}");
            }
            catch (Exception ex)
            {
                _stringBuilder.AppendLine(ex.ToString());
            }
            finally
            {
                var ts = _taskInstance;
                if (ts != null) ts.Canceled -= OnCanceled;
                _cts?.Dispose();
                _cts = null;
                Logger.AddAsync($"GetLocationBackgroundTask ended {_stringBuilder.ToString()}", Logger.BackgroundTaskLogFilename, Logger.Severity.Info, false).Wait();
                _deferral?.Complete();
            }
        }

        private void OnCanceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            _stringBuilder.AppendLine($"GetLocationBackgroundTask.OnCanceled() fired with reason = {reason}");
            _cts?.CancelSafe(true);
        }

        private static Task<Geoposition> GetGeopositionAsync(CancellationToken cancToken)
        {
            var gl = new Geolocator() { DesiredAccuracyInMeters = PersistentData.DefaultDesiredAccuracyInMetres }; //, ReportInterval = myDataModel.ReportIntervalInMilliSec };
            if (gl == null) return null;
            return gl.GetGeopositionAsync().AsTask(cancToken);
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
        //        _periodicTimer?.CancelSafe();

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