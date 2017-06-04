using System;
using System.Threading;
using Utilz;

namespace LolloGPS.GPSInteraction
{
    public static class GetLocBackgroundTaskSemaphoreManager
    {
        // there is only one way of finding out if the core application is running, if we want to be independent of app crashes; 
        // otherwise, we can use the registry - but it's too fragile if the app crashes.
        // the only way is with a named Mutex (compare SuspensionManager) or a named semaphore.

        private const string SEMAPHORE_NAME = "GPSHikingMate10_GetLocBackgroundTaskSemaphore";
        private static readonly Semaphore _backgroundTaskSemaphore = new Semaphore(1, 1, SEMAPHORE_NAME);
        public static bool TryWait()
        {
            try
            {
                _backgroundTaskSemaphore.WaitOne();
                return true;
            }
            catch (Exception ex)
            {
                Logger.Add_TPL(ex.ToString(), Logger.BackgroundLogFilename);
            }
            return false;
        }
        public static void Release()
        {
            SemaphoreExtensions.TryRelease(_backgroundTaskSemaphore);
        }
        public static bool TryOpenExisting()
        {
            Semaphore semaphoreOpen = null;
            bool result = Semaphore.TryOpenExisting(SEMAPHORE_NAME, out semaphoreOpen);
            return result;
        }
    }
}