using System;
using System.Threading;
using Utilz;

namespace LolloGPS.GPSInteraction
{
    public static class GetLocBackgroundTaskSemaphoreManager
    {
        // there is only one way to find out if the core application is running, if we want to be independent of app crashes; 
        // otherwise, we can use the registry - but it's too fragile if the app crashes.
        // the only way is with a named Mutex (compare SuspensionManager) or a named semaphore.
        //private const string BACKGROUND_TASK_PROTECTOR_SEMAPHORE_NAME = "GPSHikingMate10_GetLocBackgroundTaskProtectorSemaphore";
        //private static readonly Semaphore _backgroundTaskProtectorSemaphore = new Semaphore(1, 1, BACKGROUND_TASK_PROTECTOR_SEMAPHORE_NAME);
        private const string BACKGROUND_TASK_SEMAPHORE_NAME = "GPSHikingMate10_GetLocBackgroundTaskSemaphore";
        private static Semaphore _backgroundTaskSemaphore = null;

        /// <summary>
        /// This method is not thread safe, call it within a semaphore. This is faster than making it thread safe with a protector semaphore.
        /// </summary>
        /// <returns></returns>
        public static bool SetMainAppIsRunningAndActive()
        {
            try
            {
                //_backgroundTaskProtectorSemaphore.WaitOne(200);
                if (_backgroundTaskSemaphore == null) _backgroundTaskSemaphore = new Semaphore(1, 1, BACKGROUND_TASK_SEMAPHORE_NAME);
                _backgroundTaskSemaphore.WaitOne();
                Logger.Add_TPL("SetMainAppIsRunningAndActive() ending", Logger.BackgroundLogFilename, Logger.Severity.Info, false);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Add_TPL(ex.ToString(), Logger.BackgroundLogFilename);
                return false;
            }
            //finally
            //{
            //    SemaphoreExtensions.TryRelease(_backgroundTaskProtectorSemaphore);
            //}
        }
        /// <summary>
        /// This method is not thread safe, call it within a semaphore. This is faster than making it thread safe with a protector semaphore.
        /// </summary>
        public static void SetMainAppIsNotRunningOrNotActive()
        {
            //try
            //{
            //_backgroundTaskProtectorSemaphore.WaitOne(200);
            SemaphoreExtensions.TryRelease(_backgroundTaskSemaphore);
            SemaphoreExtensions.TryDispose(_backgroundTaskSemaphore);
            _backgroundTaskSemaphore = null;

            Logger.Add_TPL("SetMainAppIsNotRunningOrNotActive() ending", Logger.BackgroundLogFilename, Logger.Severity.Info, false);
            //Semaphore semaphoreOpen = null;
            //bool test = Semaphore.TryOpenExisting(BACKGROUND_TASK_SEMAPHORE_NAME, out semaphoreOpen);
            //}
            //catch (Exception ex)
            //{
            //    Logger.Add_TPL(ex.ToString(), Logger.BackgroundLogFilename);
            //}
            //finally
            //{
            //    SemaphoreExtensions.TryRelease(_backgroundTaskProtectorSemaphore);
            //}
        }
        public static bool GetMainAppIsRunningAndActive()
        {
            Semaphore semaphoreOpen = null;
            bool result = Semaphore.TryOpenExisting(BACKGROUND_TASK_SEMAPHORE_NAME, out semaphoreOpen);
            return result;
        }
    }
}