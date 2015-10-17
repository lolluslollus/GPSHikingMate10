using System;
using System.Diagnostics;

namespace Utilz
{
    public sealed class KeepAlive
    {
        //private static List<Windows.System.Display.DisplayRequest> AppDisplayRequests = new List<Windows.System.Display.DisplayRequest>();
        private static Windows.System.Display.DisplayRequest _appDisplayRequest = null; //new Windows.System.Display.DisplayRequest();
        private const long LongMax = 2147483647L;
        private static long _displayRequestRefCount = 0;

        public static void UpdateKeepAlive(Boolean isMustKeepAlive)
        {
            try
            {
                if (isMustKeepAlive) SetTrue();
                else SetFalse();
            }
            catch (Exception ex)
            {
                Logger.Add_TPL(ex.ToString(), Logger.AppExceptionLogFilename);
            }
        }

        private static void SetFalse()
        {
            if (_displayRequestRefCount > 0)
            {
                if (_appDisplayRequest == null) _appDisplayRequest = new Windows.System.Display.DisplayRequest();
                _appDisplayRequest.RequestRelease();
                _displayRequestRefCount--;
            }
        }

        private static void SetTrue()
        {
            if (_displayRequestRefCount < LongMax)
            {
                if (_appDisplayRequest == null) _appDisplayRequest = new Windows.System.Display.DisplayRequest();
                _appDisplayRequest.RequestActive();
                _displayRequestRefCount++;
            }
        }

        public static void StopKeepAlive()
        {
            try
            {
                while(_displayRequestRefCount > 0) SetFalse();
            }
            catch (Exception ex)
            {
                Logger.Add_TPL(ex.ToString(), Logger.AppExceptionLogFilename);
            }
        }
        //public static void ReleaseKeepAlive()
        //{
        //    // release all display requests // do I need this?
        //}
    }
}
