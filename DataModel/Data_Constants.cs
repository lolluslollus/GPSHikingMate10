using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace LolloGPS.Data
{
    public static class ConstantData
    {
        public const string GPX_DATE_TIME_FORMAT = "yyyy-MM-ddTHH:mm:ssZ";
        public const string GPX_DATE_TIME_FORMAT_ONLY_LETTERS_AND_NUMBERS = "yyyyMMddTHHmmssZ";
        public const string MYMAIL = "lollus@hotmail.co.uk";
        public const string APPNAME = "GPS Hiking Mate for Windows 10";
        public const string APPNAME_ALL_IN_ONE = "GPSHikingMate";
        public const string BUY_URI = @"ms-windows-store://pdp/?ProductId=9NBLGGH1Z7LM"; // this id comes from the dashboard
        public const string RATE_URI = @"ms-windows-store://review/?ProductId=9NBLGGH1Z7LM"; // this id comes from the dashboard
        public const string GPX_EXTENSION = ".gpx";
        public const string GET_LOCATION_BACKGROUND_TASK_NAME = "GetLocationBackgroundTask";
        public const string GET_LOCATION_BACKGROUND_TASK_ENTRY_POINT = "BackgroundTasks.GetLocationBackgroundTask";
        public const string PRIVACY_POLICY_URL = "https://1drv.ms/w/s!AidtRscM9dFkhtYdY4GpJVOLVXyI5Q";
        public const string REG_CLEARING_CACHE_TILE_SOURCE = "IsClearingCache_TileSource";
        public const string REG_CLEARING_CACHE_IS_REMOVE_SOURCES = "IsClearingCache_IsAlsoRemoveSources";
        public const string TILE_SOURCES_DIR_NAME = "TileSources";
        //public const double MILE_TO_KM = 1.609344;
        public const double MILE_TO_M = 1609.344;
        public const double KM_TO_MILE = 0.621371192237;
        //public const double FOOT_TO_M = 0.3048;
        public const double M_TO_FOOT = 3.2808;
        public const double MILE_TO_FOOT = 5280.0;
        public const double KM_TO_M = 1000.0;
        public static readonly double PI_DOUBLE = Math.PI * 2.0;
        public static readonly double PI_HALF = Math.PI / 2.0;
        public static readonly double DEG_TO_RAD = Math.PI / 180.0;
        public static readonly double RAD_TO_DEG = 180.0 / Math.PI;
        public const int MAX_TILES_TO_LEECH = 10000;
        //public const string RegIsSavingFile = "IsSavingFile";
        //public const string RegIsLoadingFile = "IsLoadingFile";
        //public const string RegWhichSeries = "WhichSeries";

        public const ulong MaxFileSize = (ulong)10000000;
        public const int TRIAL_LENGTH_DAYS = 7;

        public static string AppName { get { return APPNAME; } }
        private static readonly string _version = Package.Current.Id.Version.Major.ToString()
            + "."
            + Package.Current.Id.Version.Minor.ToString()
            + "."
            + Package.Current.Id.Version.Build.ToString()
            + "."
            + Package.Current.Id.Version.Revision.ToString();
        public static string Version { get { return "Version " + _version; } }
    }
}
