using LolloGPS.Data;
using LolloGPS.Data.Constants;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Utilz;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.Core;
using Windows.Devices.Geolocation;
using Windows.Foundation;
using Windows.UI.Core;

namespace LolloGPS.GPSInteraction
{
    public sealed class GPSInteractor : ObservableData
    {
        private PersistentData _myPersistentData = null;
        private Geolocator _geolocator = null;
        private static SemaphoreSlimSafeRelease _bkgTrackingSemaphore = null;
        private CancellationTokenSource _getLocationCts = null;

        private bool _isGpsWorking = false;
        public bool IsGPSWorking { get { return _isGpsWorking; } private set { _isGpsWorking = value; RaisePropertyChanged_UI(); } }

        private volatile IBackgroundTaskRegistration _getlocTask = null;

        #region construct and dispose
        public GPSInteractor(PersistentData persistentData)
        {
            _myPersistentData = persistentData;
            _geolocator = new Geolocator()
            {
                DesiredAccuracyInMeters = _myPersistentData.DesiredAccuracyInMeters,
                ReportInterval = _myPersistentData.ReportIntervalInMilliSec,
                //MovementThreshold = _myPersistentData.MovementThresholdInMetres,
                //DesiredAccuracy = _myPersistentData.PositAccuracy,
            };

            // Only with windows phone: You must set the MovementThreshold for 
            // distance-based tracking or ReportInterval for
            // periodic-based tracking before adding event handlers.
        }
        public async Task ActivateAsync()
        {
            try
            {
                if (!SemaphoreSlimSafeRelease.IsAlive(_bkgTrackingSemaphore)) _bkgTrackingSemaphore = new SemaphoreSlimSafeRelease(1, 1);
                await Task.Run(async delegate
                {
                    try
                    {
                        await _bkgTrackingSemaphore.WaitAsync().ConfigureAwait(false);
                        await TryUpdatePropsAfterPropsChanged_Background().ConfigureAwait(false);
                        UpdateAfterIsTrackingChanged_Foreground(false);
                        AddHandler_DataModelProperty();
                    }
                    catch (Exception ex)
                    {
                        if (SemaphoreSlimSafeRelease.IsAlive(_bkgTrackingSemaphore))
                            Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
                    }
                    finally
                    {
                        SemaphoreSlimSafeRelease.TryRelease(_bkgTrackingSemaphore);
                    }
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename).ConfigureAwait(false);
            }
        }
        public void Deactivate()
        {
            RemoveHandlers_GeoLocator();
            RemoveHandlers_DataModelProperty();
            RemoveHandlers_GetLocBackgroundTask();
            CancelPendingTasks(); // after removing the handlers
        }
        private void CancelPendingTasks()
        {
            _getLocationCts?.Cancel();
            //_getLocationCts.Dispose(); This is done in the exception handler that catches the IsCanceled exception. If you do it here, the exception handler will throw an ObjectDisposed exception
            //_getLocationCts = null;
            SemaphoreSlimSafeRelease.TryDispose(_bkgTrackingSemaphore);
        }
        #endregion construct and dispose

        #region event handling
        private Boolean _isGeoLocatorHandlersActive = false;
        private Boolean _isDataModelHandlersActive = false;
        private Boolean _isGetLocTaskHandlersActive = false;

        private async void OnPersistentData_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // use semaphores to deal with interlinked properties, which contain cross references.
            // semaphores must be async so they don't block the UI thread.
            // these operations can be cancelled by disposing and rector'ing their semaphores. This may cause exceptions, which are silently caught below.
            // In general, beware of this deadlock trap with semaphores: enter a non-async semaphore, call the same method recursively, you will lock the thread forever.
            // This is an awaitable semaphore, so it does not make trouble.

            try
            {
                await _bkgTrackingSemaphore.WaitAsync().ConfigureAwait(false); // both properties affect the same parameter, so I need a semaphore.

                if (e.PropertyName == nameof(PersistentData.DesiredAccuracyInMeters)
                || e.PropertyName == nameof(PersistentData.ReportIntervalInMilliSec))
                //|| e.PropertyName == "MovementThresholdInMetres" || e.PropertyName == "PositAccuracy") // no! either these or the ones I use, they are conflicting
                {
                    if (_geolocator != null)
                    {
                        RemoveHandlers_GeoLocator();
                        _geolocator = new Geolocator()
                        {
                            DesiredAccuracyInMeters = _myPersistentData.DesiredAccuracyInMeters,
                            ReportInterval = _myPersistentData.ReportIntervalInMilliSec,
                        };
                        if (_myPersistentData.IsTracking) AddHandlers_GeoLocator();
                    }
                }
                else if (e.PropertyName == nameof(PersistentData.IsTracking))
                {
                    UpdateAfterIsTrackingChanged_Foreground(true);
                }
                else if (e.PropertyName == nameof(PersistentData.BackgroundUpdatePeriodInMinutes)
                || e.PropertyName == nameof(PersistentData.IsBackgroundEnabled))
                {
                    // LOLLO the semaphore.Wait used to be here, it now encircles the whole method
                    // both properties affect the same parameter, so I need a semaphore. 
                    // In fact, all properties are interlinked, so I put all in a semaphore.
                    if (e.PropertyName == nameof(PersistentData.BackgroundUpdatePeriodInMinutes))
                    {
                        if (_myPersistentData.IsBackgroundEnabled)
                        {
                            UnregisterGetLocBackgroundTask_All(); // need to deregister
                            await TryUpdatePropsAfterPropsChanged_Background().ConfigureAwait(false); // and reregister with the new value (if registration is possible at this time)
                        }
                    }
                    else if (e.PropertyName == nameof(PersistentData.IsBackgroundEnabled))
                    {
                        bool isOk = await TryUpdatePropsAfterPropsChanged_Background().ConfigureAwait(false);
                        // if just switched on and ok, get a location now. The foreground tracking does it, so we do it here as well
                        if (isOk && _myPersistentData.IsBackgroundEnabled)
                        {
                            var loc = await GetGeoLocationAsync().ConfigureAwait(false);
                            //if (loc != null && _myPersistentData != null) // disable bkg task if the app has no access to location? No, the user might grant it later
                            //{
                            //    if (loc.Item1)
                            //    {
                            //        Task dis = CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () => _myPersistentData.IsBackgroundEnabled = false).AsTask();
                            //    }
                            //}
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (SemaphoreSlimSafeRelease.IsAlive(_bkgTrackingSemaphore))
                    Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
            }
            finally
            {
                SemaphoreSlimSafeRelease.TryRelease(_bkgTrackingSemaphore);
            }
        }
        private void AddHandler_DataModelProperty()
        {
            if (!_isDataModelHandlersActive)
            {
                if (_myPersistentData != null)
                {
                    _myPersistentData.PropertyChanged += OnPersistentData_PropertyChanged;
                    _isDataModelHandlersActive = true;
                }
            }
        }
        private void RemoveHandlers_DataModelProperty()
        {
            if (_myPersistentData != null)
            {
                _myPersistentData.PropertyChanged -= OnPersistentData_PropertyChanged;
                _isDataModelHandlersActive = false;
            }
        }
        private void AddHandlers_GeoLocator()
        {
            if (!_isGeoLocatorHandlersActive)
            {
                _geolocator.PositionChanged += OnGeolocator_PositionChangedAsync;
                //_geolocator.StatusChanged += OnGeolocator_StatusChangedAsync;
                _isGeoLocatorHandlersActive = true;
            }
        }

        private void RemoveHandlers_GeoLocator()
        {
            if (_geolocator != null)
            {
                _geolocator.PositionChanged -= OnGeolocator_PositionChangedAsync;
                //_geolocator.StatusChanged -= OnGeolocator_StatusChangedAsync;
                _isGeoLocatorHandlersActive = false;
            }
        }
        private void AddHandlers_GetLocBackgroundTask()
        {
            if (!_isGetLocTaskHandlersActive)
            {
                if (_getlocTask != null)
                {
                    GetLocBackgroundTaskSemaphoreManager.TryWait();
                    _getlocTask.Completed += new BackgroundTaskCompletedEventHandler(OnGetLocBackgroundTaskCompleted);
                    _isGetLocTaskHandlersActive = true;
                }
            }
        }

        private void RemoveHandlers_GetLocBackgroundTask()
        {
            if (_getlocTask != null)
            {
                _getlocTask.Completed -= OnGetLocBackgroundTaskCompleted;
                _isGetLocTaskHandlersActive = false;
            }
            GetLocBackgroundTaskSemaphoreManager.Release();
        }
        #endregion event handling

        #region foreground tracking
        private void StartTracking(bool setUserMessage)
        {
            //this method runs in a UI thread
            if (setUserMessage) SetLastMessage_UI("Tracking on, waiting for update...");
            AddHandlers_GeoLocator();
        }

        private void StopTracking(bool setUserMessage)
        {
            //this method runs in a UI thread
            if (setUserMessage) SetLastMessage_UI("Tracking off");
            RemoveHandlers_GeoLocator();
        }

        //async private void OnGeolocator_StatusChangedAsync(Geolocator sender, StatusChangedEventArgs e)
        //{
        //    //this method runs in a worker thread and dispatches its junk to the ui thread
        //    CoreDispatcher dispatcher = Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher;
        //    await dispatcher.RunAsync(CoreDispatcherPriority.Normal, delegate
        //    {
        //        switch (e.Status)
        //        {
        //            case PositionStatus.Ready:
        //                // Location platform is providing valid data.
        //                SetLastMessage_UI("Ready");
        //                break;

        //            case PositionStatus.Initializing:
        //                // Location platform is acquiring a fix. It may or may not have data. Or the data may be less accurate.
        //                SetLastMessage_UI("Initializing");
        //                break;

        //            case PositionStatus.NoData:
        //                // Location platform could not obtain location data.
        //                SetLastMessage_UI("No data");
        //                break;

        //            case PositionStatus.Disabled:
        //                // The permission to access location data is denied by the user or other policies.
        //                SetLastMessage_UI("Disabled");
        //                break;

        //            case PositionStatus.NotInitialized:
        //                // The location platform is not initialized. This indicates that the application has not made a request for location data.
        //                SetLastMessage_UI("Not initialized");
        //                break;

        //            case PositionStatus.NotAvailable:
        //                // The location platform is not available on this version of the OS.
        //                SetLastMessage_UI("Not available");
        //                break;

        //            default:
        //                SetLastMessage_UI("Unknown");
        //                break;
        //        }
        //    });
        //}

        private async void OnGeolocator_PositionChangedAsync(Geolocator sender, PositionChangedEventArgs e)
        {
            if (_getLocationCts == null) // get out if another request is already pending
            {
                _getLocationCts = new CancellationTokenSource();
                CancellationToken token = _getLocationCts.Token;
                try
                {
                    //CoreDispatcher dispatcher = CoreApplication.MainView.CoreWindow.Dispatcher;
                    //await dispatcher.RunAsync(CoreDispatcherPriority.Normal, delegate
                    //{
                    if (e != null)
                    {
                        Geoposition pos = e.Position;
                        await AppendGeoPositionAsync(_myPersistentData, pos, false).ConfigureAwait(false);
                    }
                    //}).AsTask(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { } // ignore
                catch (Exception exc)
                {
                    SetLastMessage_UI(exc.Message);
                    Debug.WriteLine("OnGeolocator_PositionChangedAsync threw " + exc.ToString());
                }
                finally
                {
                    _getLocationCts?.Dispose();
                    _getLocationCts = null;
                }
            }
        }

        private void UpdateAfterIsTrackingChanged_Foreground(bool setUserMessage)
        {
            if (_myPersistentData.IsTracking)
            {
                StartTracking(setUserMessage);
            }
            else
            {
                StopTracking(setUserMessage);
            }
        }
        #endregion foreground tracking

        #region background task
        private static IBackgroundTaskRegistration GetTaskIfAlreadyRegistered()
        {
            foreach (var cur in BackgroundTaskRegistration.AllTasks)
            {
                if (cur.Value.Name == ConstantData.GET_LOCATION_BACKGROUND_TASK_NAME)
                {
                    return cur.Value;
                }
            }
            return null;
        }

        private async Task<Tuple<bool, string>> TryActivateGetLocBackgroundTaskAsync()
        {
            // this method can run in any thread

            bool isOk = false;
            string msg = string.Empty;

            string errorMsg = string.Empty;
            BackgroundAccessStatus backgroundAccessStatus = BackgroundAccessStatus.Unspecified;
            _getlocTask = GetTaskIfAlreadyRegistered();

            if (_getlocTask == null) // bkg task not registered yet: register it
            {
                try
                {
                    //maniman
                    UnregisterGetLocBackgroundTask_All();

                    // Get permission for a background task from the user. If the user has already answered once,
                    // this does nothing and the user must manually update their preference via PC Settings.
                    backgroundAccessStatus = await BackgroundExecutionManager.RequestAccessAsync().AsTask().ConfigureAwait(false);

                    // Regardless of the answer, register the background task. If the user later adds this application
                    // to the lock screen, the background task will be ready to run.
                    // Create a new background task builder
                    BackgroundTaskBuilder geolocTaskBuilder = new BackgroundTaskBuilder();

                    geolocTaskBuilder.Name = ConstantData.GET_LOCATION_BACKGROUND_TASK_NAME;
                    geolocTaskBuilder.TaskEntryPoint = ConstantData.GET_LOCATION_BACKGROUND_TASK_ENTRY_POINT;

                    // Create a new timer triggering at a 15 minute interval 
                    uint period = _myPersistentData.BackgroundUpdatePeriodInMinutes; // less than 15 will throw an exception, this is how this class works
                    var trigger = new TimeTrigger(period, false);
                    // Associate the timer trigger with the background task builder
                    geolocTaskBuilder.SetTrigger(trigger);

                    // Register the background task
                    _getlocTask = geolocTaskBuilder.Register();
                }
                catch (Exception ex)
                {
                    errorMsg = ex.ToString();
                    backgroundAccessStatus = BackgroundAccessStatus.Denied;
                }
            }
            else // bkg task registered: see if it is ok
            {
                try
                {
                    backgroundAccessStatus = BackgroundExecutionManager.GetAccessStatus();
                }
                catch (Exception ex)
                {
                    errorMsg = ex.ToString();
                    backgroundAccessStatus = BackgroundAccessStatus.Denied;
                }
            }
            switch (backgroundAccessStatus)
            {
                case BackgroundAccessStatus.Unspecified:
                    RemoveHandlers_GetLocBackgroundTask();
                    msg = "Cannot run in background, enable it in the \"Battery Saver\" app";
                    break;
                case BackgroundAccessStatus.Denied:
                    RemoveHandlers_GetLocBackgroundTask();
                    if (string.IsNullOrWhiteSpace(errorMsg)) msg = "Cannot run in background, enable it in Settings - Privacy - Background apps";
                    else msg = errorMsg;
                    break;
                default:
                    // Associate an event handler with the new background task
                    AddHandlers_GetLocBackgroundTask();
                    msg = "Background task on";
                    isOk = true;
                    break;
            }
            return Tuple.Create<bool, string>(isOk, msg);
        }

        private void UnregisterGetLocBackgroundTask_Current()
        {
            if (_getlocTask != null)
            {
                RemoveHandlers_GetLocBackgroundTask();
                _getlocTask.Unregister(true);
                _getlocTask = null;
            }
        }

        private void UnregisterGetLocBackgroundTask_All()
        {
            UnregisterGetLocBackgroundTask_Current();
            foreach (var item in BackgroundTaskRegistration.AllTasks)
            {
                if (item.Value.Name == ConstantData.GET_LOCATION_BACKGROUND_TASK_NAME)
                {
                    _getlocTask = item.Value;
                    UnregisterGetLocBackgroundTask_Current();
                }
            }
        }

        private void OnGetLocBackgroundTaskCompleted(IBackgroundTaskRegistration sender, BackgroundTaskCompletedEventArgs e)
        {
            Debug.WriteLine("BackgroundTask completed, event caught");
            Task getLoc = GetGeoLocationAsync();
            //if (loc != null && _myPersistentData != null) // disable bkg task if the app has no access to location? No, the user might grant it later
            //{
            //    if (loc.Item1)
            //    {
            //        Task dis = CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () => _myPersistentData.IsBackgroundEnabled = false).AsTask();
            //    }
            //}
        }

        private async Task<bool> TryUpdatePropsAfterPropsChanged_Background()
        {
            //this runs in the UI thread
            if (_myPersistentData.IsBackgroundEnabled)
            {
                Tuple<bool, string> result = await TryActivateGetLocBackgroundTaskAsync().ConfigureAwait(false);
                // in case of failure (eg the user revoked background permissions when the app was suspended or off), reset the variables
                if (!result.Item1)
                {
                    UnregisterGetLocBackgroundTask_All();
                }
                // notify the user of the failure
                if (!result.Item1 && !string.IsNullOrWhiteSpace(result.Item2))
                {
                    SetLastMessage_UI(result.Item2);
                }
                // BODGE change the value back to false in a deferred cycle, otherwise the control won't update, even if the property will.
                // the trouble seems to lie in the ToggleSwitch style: both mine and MS default don't work.
                // it could also be a problem with the binding engine, which I have seen already.
                // In any case, this change must take place on the UI thread, so no issue really.
                if (!result.Item1)
                {
                    IAsyncAction qqq = CoreApplication.MainView.CoreWindow.Dispatcher.RunIdleAsync((a) => _myPersistentData.IsBackgroundEnabled = false);
                }
                return result.Item1;
            }
            else
            {
                UnregisterGetLocBackgroundTask_All();
                return true;
            }
        }
        #endregion background task

        #region services
        /// <summary>
        /// The bool is true if the background task must be disabled
        /// The PointRecord contains the result
        /// </summary>
        /// <returns></returns>
        public async Task<Tuple<bool, PointRecord>> GetGeoLocationAsync()
        {
            //this method runs in a UI thread
            PointRecord dataRecord = null;
            var result = Tuple.Create<bool, PointRecord>(false, null); // the bool tells if the background task must be disabled

            if (_getLocationCts == null) // get out if another request is already pending
            {
                IsGPSWorking = true;
                SetLastMessage_UI("getting current location...");

                _getLocationCts = new CancellationTokenSource();             // Get cancellation token
                CancellationToken token = _getLocationCts.Token;
                try
                {
                    Geoposition pos = null;
                    if (_geolocator != null)
                    {
                        pos = await _geolocator.GetGeopositionAsync().AsTask(token).ConfigureAwait(false);
                        dataRecord = await AppendGeoPositionAsync(_myPersistentData, pos, false).ConfigureAwait(false);
                        result = Tuple.Create(false, dataRecord);
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    // SetLastMessage_UI("UnauthorizedAccessException " + uae.Message);
                    result = Tuple.Create<bool, PointRecord>(true, null);
                    SetLastMessage_UI("Give the app permission to access your location (Settings - Privacy - Location)");
                }
                catch (TaskCanceledException) // inherits from OperationCanceled
                {
                    result = Tuple.Create<bool, PointRecord>(false, null);
                    SetLastMessage_UI("location acquisition cancelled");
                }
                catch (Exception ex)
                {
                    result = Tuple.Create<bool, PointRecord>(false, null);
                    SetLastMessage_UI("cannot get location");
                    await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename).ConfigureAwait(false);
                }
                finally
                {
                    _getLocationCts?.Dispose();
                    _getLocationCts = null;
                    if (dataRecord != null)
                    {
                        if (CoreApplication.MainView.CoreWindow.Dispatcher.HasThreadAccess)
                        {
                            _myPersistentData?.SelectRecordFromSeries(_myPersistentData.Current, PersistentData.Tables.History);
                        }
                        else
                        {
                            IAsyncAction ui = CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, delegate
                            {
                                _myPersistentData?.SelectRecordFromSeries(_myPersistentData.Current, PersistentData.Tables.History);
                            });
                        }
                    }
                    IsGPSWorking = false;
                }
            }
            return result;
        }

        private static async Task<PointRecord> AppendGeoPositionAsync(PersistentData persistentData, Geoposition pos, bool checkMaxEntries)
        {
            PointRecord newDataRecord = new PointRecord();
            InitNewHistoryRecord(pos, newDataRecord);

            if (await persistentData.AddHistoryRecordAsync(newDataRecord, checkMaxEntries).ConfigureAwait(false)) return newDataRecord;
            else return null;
        }

        private static void InitNewHistoryRecord(Geoposition pos, PointRecord newDataRecord)
        {
            newDataRecord.Latitude = pos.Coordinate.Point.Position.Latitude;
            newDataRecord.Longitude = pos.Coordinate.Point.Position.Longitude;
            newDataRecord.Altitude = pos.Coordinate.Point.Position.Altitude;
            newDataRecord.Accuracy = pos.Coordinate.Accuracy;
            newDataRecord.AltitudeAccuracy = pos.Coordinate.AltitudeAccuracy.GetValueOrDefault();// == null ? default(Double) : pos.Coordinate.AltitudeAccuracy;
            newDataRecord.PositionSource = pos.Coordinate.PositionSource.ToString();
            newDataRecord.TimePoint = pos.Coordinate.Timestamp.DateTime;
            newDataRecord.SpeedInMetreSec = pos.Coordinate.Speed ?? default(Double);
        }

        public static bool AppendGeoPositionOnlyDb(PersistentData persistentData, Geoposition pos, bool checkMaxEntries)
        {
            PointRecord newDataRecord = new PointRecord();
            InitNewHistoryRecord(pos, newDataRecord);
            bool isOk = persistentData.AddHistoryRecordOnlyDb(newDataRecord, checkMaxEntries);
            return isOk;
        }

        private void SetLastMessage_UI(string message)
        {
            if (_myPersistentData != null) _myPersistentData.LastMessage = message;
            //// if (_persistentData != null) _persistentData.LastMessage = message;
            //if (CoreApplication.MainView.CoreWindow.Dispatcher.HasThreadAccess)
            //{
            //    if (_myPersistentData != null) _myPersistentData.LastMessage = message;
            //}
            //else
            //{
            //    IAsyncAction ui = CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, delegate
            //    {
            //        if (_myPersistentData != null) _myPersistentData.LastMessage = message;
            //    });
            //}
        }
        #endregion services
    }
}