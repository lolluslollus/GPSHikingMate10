using LolloGPS.Data;
using LolloListChooser;
using System;
using System.Linq;
using System.Collections.ObjectModel;
using System.Globalization;
using Windows.Devices.Geolocation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls.Maps;
using Windows.UI.Xaml.Data;

namespace LolloGPS.Converters
{
    public class LogToLinearConverter : IValueConverter
    {
        private const double Power = 10.0;
        private const double ScaleFactor = 20.0;
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return 0;
            uint logarithm = 0;
            try
            {
                logarithm = System.Convert.ToUInt32(Math.Log(System.Convert.ToDouble(value), Power) * ScaleFactor);
            }
            catch (Exception)
            {
                return 0;
            }
            return logarithm;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return 0;
            uint exponential = 0;
            try
            {
                exponential = System.Convert.ToUInt32(Math.Pow(Power, System.Convert.ToDouble(value) / ScaleFactor));
            }
            catch (Exception)
            {
                return 0;
            }
            return exponential;
        }
    }

    public class MsecToSecConverter : IValueConverter
    {
        private const double Factor = 1000.0;
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return 0;
            uint sec = 0;
            try
            {
                sec = System.Convert.ToUInt32(System.Convert.ToDouble(value) / Factor);
            }
            catch (Exception)
            {
                return 0;
            }
            return sec;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new Exception("this is a one-way bonding, it should never come here");
        }
    }

    //public class BooleanToMapStyleConverter : IValueConverter
    //{
    //    public object Convert(object value, Type targetType, object parameter, string language)
    //    {
    //        if (value == null || !(value is MapStyle)) return false;
    //        MapStyle mapStyle = (MapStyle)value;
    //        if (mapStyle == MapStyle.Terrain) return false;
    //        else return true;
    //    }

    //    public object ConvertBack(object value, Type targetType, object parameter, string language)
    //    {
    //        if (value == null || !(value is Boolean)) return MapStyle.Terrain;
    //        Boolean isAerial = (Boolean)value;
    //        if (isAerial) return MapStyle.Aerial;
    //        else return MapStyle.Terrain;
    //    }
    //}

    public class BooleanToVisibleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null || !(value is Boolean)) return Visibility.Collapsed;
            Boolean boo = (Boolean)value;
            if (boo) return Visibility.Visible;
            else return Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new Exception("this is a one-way bonding, it should never come here");
        }
    }

    public class BooleanToCollapsedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null || !(value is Boolean)) return Visibility.Visible;
            Boolean boo = (Boolean)value;
            if (boo) return Visibility.Collapsed;
            else return Visibility.Visible;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new Exception("this is a one-way bonding, it should never come here");
        }
    }

    public class TrueToFalseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null || !(value is Boolean)) return true;
            Boolean boo = (Boolean)value;
            if (boo) return false;
            else return true;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new Exception("this is a one-way bonding, it should never come here");
        }
    }

    public class SeriesIsEmptyToVisibleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null || !(value is Int32)) return Visibility.Visible;
            int val = (int)value;
            if (val > 0) return Visibility.Collapsed;
            else return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new Exception("this is a one-way bonding, it should never come here");
        }
    }

    public class SeriesIsEmptyToCollapsedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null || !(value is Int32)) return Visibility.Collapsed;
            int val = (int)value;
            if (val > 0) return Visibility.Visible;
            else return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new Exception("this is a one-way bonding, it should never come here");
        }
    }

    //public class HistoryIsAnyToVisibleConverter : IValueConverter
    //{
    //    public object Convert(object value, Type targetType, object parameter, string language)
    //    {
    //        return Visibility.Visible;
    //    }

    //    public object ConvertBack(object value, Type targetType, object parameter, string language)
    //    {
    //        throw new Exception("this is a one-way bonding, it should never come here");
    //    }
    //}

    public class SeriesCountGreaterThanZeroToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null || !(value is Int32)) return false;
            int val = (int)value;
            if (val > 0) return true;
            else return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new Exception("this is a one-way bonding, it should never come here");
        }
    }

    public class LandmarksCountLowerThanMaxToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null || !(value is int)) return false;
            int val = (int)value;
            if (val < PersistentData.MaxRecordsInLandmarks) return true;
            else return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new Exception("this is a one-way bonding, it should never come here");
        }
    }

    public class LandmarksCountEqualMaxToVisibleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null || !(value is Int32)) return Visibility.Visible;
            int val = (int)value;
            if (val < PersistentData.MaxRecordsInLandmarks) return Visibility.Collapsed;
            else return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new Exception("this is a one-way bonding, it should never come here");
        }
    }

    public class CurrentToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null || !(value is PointRecord)) return false;
            PointRecord val = (PointRecord)value;
            if (!val.IsEmpty()) return true;
            else return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new Exception("this is a one-way bonding, it should never come here");
        }
    }

    public class CurrentToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null || !(value is PointRecord)) return Visibility.Visible;
            PointRecord val = (PointRecord)value;
            if (!val.IsEmpty()) return Visibility.Collapsed;
            else return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new Exception("this is a one-way bonding, it should never come here");
        }
    }

    public class StringNotEmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return Visibility.Collapsed;
            if (string.IsNullOrWhiteSpace(value.ToString())) return Visibility.Collapsed;
            else return Visibility.Visible;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new Exception("this is a one-way bonding, it should never come here");
        }
    }

    public class StringFormatterConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            string format = parameter.ToString();
            string output = string.Empty;
            try
            {
                output = string.Format(CultureInfo.CurrentUICulture, format, new object[1] { value });
            }
            catch (FormatException)
            {
                output = value.ToString();
            }
            return output;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new Exception("this is a one-way bonding, it should never come here");
        }
    }

    public class NumberNotNullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language) // TODO check this for locales and formats
        {
            if (value == null) return Visibility.Collapsed;
            if (string.IsNullOrWhiteSpace(value.ToString()) || value.ToString().Equals(default(double).ToString()) || value.ToString().Equals(default(int).ToString())) return Visibility.Collapsed;
            else return Visibility.Visible;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new Exception("this is a one-way bonding, it should never come here");
        }
    }

    public class DateNotNullToVisibilityConverter : IValueConverter // TODO check this for locales and formats
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return Visibility.Collapsed;
            if (string.IsNullOrWhiteSpace(value.ToString()) || value.ToString().Equals(default(DateTime).ToString())) return Visibility.Collapsed;
            else return Visibility.Visible;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new Exception("this is a one-way bonding, it should never come here");
        }
    }

    public class HyperLinkLabelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value != null && !string.IsNullOrWhiteSpace(value.ToString())) return value.ToString();
            else return "Hyperlink";
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new Exception("this is a one-way bonding, it should never come here");
        }
    }

    public class AngleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            PersistentData myData = PersistentData.GetInstance();
            if (myData.IsShowDegrees == false)
            {
                if (value == null) return default(Double).ToString("#0.#", CultureInfo.CurrentUICulture);
                Double dbl = default(double);
                Double.TryParse(value.ToString(), out dbl);
                return dbl.ToString("#0.#", CultureInfo.CurrentUICulture);
            }
            else
            {
                return AngleConverterHelper.Float_To_DegMinSec_NoDec_String(value, parameter);
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new Exception("This is a one-way binding, so I should never get here");
        }
    }

    public class AngleConverterDeg : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return AngleConverterHelper.Float_To_DegMinSecDec_Array(value, parameter)[0];
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new Exception("this is a one-way binding, it should never get here");
        }
    }
    public class AngleConverterMin : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return AngleConverterHelper.Float_To_DegMinSecDec_Array(value, parameter)[1];
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new Exception("this is a one-way binding, it should never get here");
        }

    }
    public class AngleConverterSec : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return AngleConverterHelper.Float_To_DegMinSecDec_Array(value, parameter)[2];
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new Exception("this is a one-way binding, it should never get here");
        }
    }
    public class AngleConverterDec : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return AngleConverterHelper.Float_To_DegMinSecDec_Array(value, parameter)[3];
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new Exception("this is a one-way binding, it should never get here");
        }
    }
    public class FloatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return default(Double).ToString("#0.#", CultureInfo.CurrentUICulture);
            Double dbl = default(double);
            Double.TryParse(value.ToString(), out dbl);
            return dbl.ToString("#0.########", CultureInfo.CurrentUICulture);
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new Exception("this is a one-way binding, it should never get here");
        }
    }

    public class MetreSecToKmHConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return SpeedConverter.GetSpeed(value, true);
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new Exception("this is a one-way binding, it should never come here");
        }
    }
    public class MetreSecToKmHConverter_OnlyNumber : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return SpeedConverter.GetSpeed(value, false);
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new Exception("this is a one-way binding, it should never come here");
        }
    }
    internal class SpeedConverter
    {
        internal static object GetSpeed(object value, bool showUnit)
        {
            string format0 = showUnit ? "#0.# Kmh" : "#0.#";
            string format1 = showUnit ? "# Kmh" : "#";
            if (value == null) return default(Double).ToString(format0, CultureInfo.CurrentUICulture);
            Double dbl = default(Double);
            if (Double.TryParse(value.ToString(), out dbl) && !Double.IsNaN(dbl) && !Double.IsInfinity(dbl) && !Double.IsNegativeInfinity(dbl) && !Double.IsPositiveInfinity(dbl))
            {
                dbl *= 3.6;
            }
            if (Double.IsNaN(dbl) || Double.IsInfinity(dbl) || Double.IsNegativeInfinity(dbl) || Double.IsPositiveInfinity(dbl)) return "";
            if (dbl < 10) return dbl.ToString(format0, CultureInfo.CurrentUICulture);
            else return dbl.ToString(format1, CultureInfo.CurrentUICulture);
        }
    }

    public class SeriesTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null || !(value is PersistentData.Tables)) return String.Empty;
            return PersistentData.GetTextForSeries((PersistentData.Tables)value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new Exception("should never get here");
        }
    }

    public class SeriesToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null || !(value is PersistentData.Tables) || parameter == null) return Visibility.Collapsed;
            PersistentData.Tables whichSeries = PersistentData.Tables.nil;
            if (!Enum.TryParse<PersistentData.Tables>(parameter.ToString(), out whichSeries)) return Visibility.Collapsed;
            if (whichSeries == (PersistentData.Tables)value) return Visibility.Visible;
            else return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new Exception("should never get here");
        }
    }

    public class MapCacheIsEnabledConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null || !(value is TileSourceRecord)) return false;
            TileSourceRecord tileSource = (TileSourceRecord)value;
            if (tileSource.IsDefault) return false;
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new Exception("should never get here");
        }
    }

    //public class MapCacheToVisibilityConverter : IValueConverter
    //{
    //    public object Convert(object value, Type targetType, object parameter, string language)
    //    {
    //        if (value == null || !(value is TileSourceRecord)) return Visibility.Collapsed;
    //        TileSourceRecord tileSource = (TileSourceRecord)value;
    //        if (tileSource.TechName == TileSourceRecord.DefaultTileSourceTechName) return Visibility.Collapsed;
    //        return Visibility.Visible;
    //    }

    //    public object ConvertBack(object value, Type targetType, object parameter, string language)
    //    {
    //        throw new Exception("should never get here");
    //    }
    //}

    public class TileSourceToVisibleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null || !(value is TileSourceRecord)) return Visibility.Collapsed;
            TileSourceRecord tileSource = (TileSourceRecord)value;
            if (tileSource.IsDefault) return Visibility.Collapsed;
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new Exception("should never get here");
        }
    }

    public class HiPositionAccuracyToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null || !(value is PositionAccuracy)) return false;
            if ((PositionAccuracy)value == PositionAccuracy.Default) return false;
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value == null || !(value is Boolean)) return PositionAccuracy.Default;
            if ((Boolean)value) return PositionAccuracy.High;
            return PositionAccuracy.Default;
        }
    }
    public class MapStyleToGlyphConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (!(value is MapStyle)) return "N";
            MapStyle mapStyle = (MapStyle)value;

            switch (mapStyle)
            {
                case MapStyle.Aerial:
                    return "A";
                case MapStyle.AerialWithRoads:
                    return "AR";
                case MapStyle.None:
                    return "N";
                case MapStyle.Road:
                    return "R";
                case MapStyle.Terrain:
                    return "T";
                default:
                    return "Dunno";
            }            
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new Exception("this is a one-way binding, it should never get here");
        }
    }

    public class TileSourceToTextAndTagConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null || !(value is TileSourceRecord)) return false;
            var tileSource = (TileSourceRecord)value;
            return new TextAndTag(tileSource.DisplayName, tileSource);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value == null || !(value is TextAndTag)) return TileSourceRecord.GetDefaultTileSource();
            var tat = (TextAndTag)value;
            return (TileSourceRecord)(tat.Tag);
        }
    }

    public class TileSourcezToTextAndTagsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null || !(value is Collection<TileSourceRecord>)) return false;
            var tileSources = (Collection<TileSourceRecord>)value;
            Collection<TextAndTag> output = new Collection<TextAndTag>();
            bool isClearingCache = (parameter != null && parameter.ToString() == "forClearingCache");
            bool isClearingCustomCache = (parameter != null && parameter.ToString() == "forClearingCustomCache");
            bool isSelecting = (parameter != null && parameter.ToString() == "forSelecting");
            // clear none
            if (isClearingCache || isClearingCustomCache)
            {
                var none = TileSourceRecord.GetNoTileSource();
                output.Add(new TextAndTag(none.DisplayName, none));
            }
            // clear all
            if (isClearingCache)
            {
                var all = TileSourceRecord.GetAllTileSource();
                output.Add(new TextAndTag(all.DisplayName, all));
            }
            // clear all custom sources one by one
            if (isClearingCustomCache)
            {
                foreach (var item in tileSources.Where(a => a.IsDeletable))
                {
                    output.Add(new TextAndTag(item.DisplayName, item));
                }
            }
            // clear all sources one by one
            else if (isClearingCache)
            {
                foreach (var item in tileSources.Where(a => !a.IsDefault))
                {
                    output.Add(new TextAndTag(item.DisplayName, item));
                }
            }
            // select all sources one by one
            else
            {
                foreach (var item in tileSources)
                {
                    output.Add(new TextAndTag(item.DisplayName, item));
                }
            }
            return output;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new Exception("this is a one-way binding, it should never get here");
        }
    }

    public class MapSourceToItsDescriptionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null || !(value is TileSourceRecord)) return false;
            return (value as TileSourceRecord).DisplayName;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new Exception("this is a one-way binding, it should never come here");
        }
    }    
}
