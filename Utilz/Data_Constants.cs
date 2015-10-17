using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace LolloGPS.Data.Constants
{
    public static class ConstantData
    {
        public const String DATE_TIME_FORMAT = "yyyy-MM-ddTHH:mm:ssZ";
        public const string MYMAIL = "lollus@hotmail.co.uk";
        public const string APPNAME = "GPS Hiking Mate for Windows 10";
        public const string APPNAME_ALL_IN_ONE = "GPSHikingMate";
        public const string BUY_URI = @"ms-windows-store://pdp/?ProductId=9NBLGGH1Z7LM"; // this id comes from the dashboard
        public const string RATE_URI = @"ms-windows-store://review/?ProductId=9NBLGGH1Z7LM"; // this id comes from the dashboard
        public const string GPX_EXTENSION = ".gpx";
        public const string GET_LOCATION_BACKGROUND_TASK_NAME = "GetLocationBackgroundTask";
        public const string GET_LOCATION_BACKGROUND_TASK_ENTRY_POINT = "BackgroundTasks.GetLocationBackgroundTask";

        //#region construct
        //private static ConstantData _instance;
        //private static readonly object _instanceLock = new object();
        //public static ConstantData GetInstance()
        //{
        //    lock (_instanceLock)
        //    {
        //        if (_instance == null)
        //        {
        //            _instance = new ConstantData();
        //        }
        //        return _instance;
        //    }
        //}
        //#endregion construct
        public static string AppName { get { return ConstantData.APPNAME; } }
        private static string _version = Package.Current.Id.Version.Major.ToString()
            + "."
            + Package.Current.Id.Version.Minor.ToString()
            + "."
            + Package.Current.Id.Version.Build.ToString()
            + "."
            + Package.Current.Id.Version.Revision.ToString();
        public static string Version { get { return "Version " + _version; } }

    }
}
