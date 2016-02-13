using LolloGPS.Data;
using System;
using System.Linq;
using System.Collections.ObjectModel;
using System.Globalization;
using Windows.Devices.Geolocation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls.Maps;
using Windows.UI.Xaml.Data;
using System.Diagnostics;
using Utilz.Controlz;

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

	public class BooleanToVisibleConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			if (!(value is bool)) return Visibility.Collapsed;
			bool boo = (bool)value;
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
			if (!(value is bool)) return Visibility.Visible;
			bool boo = (bool)value;
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
			if (!(value is bool)) return true;
			bool boo = (bool)value;
			if (boo) return false;
			else return true;
		}
		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			throw new Exception("this is a one-way bonding, it should never come here");
		}
	}

	public class IntIsNullToVisibleConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			if (!(value is int)) return Visibility.Visible;
			int val = (int)value;
			if (val > 0) return Visibility.Collapsed;
			else return Visibility.Visible;
		}

		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			throw new Exception("this is a one-way bonding, it should never come here");
		}
	}

	public class IntIsNullToFalseConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			if (!(value is int)) return false;
			int val = (int)value;
			if (val > 0) return true;
			else return false;
		}

		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			throw new Exception("this is a one-way bonding, it should never come here");
		}
	}

	public class IntIsNullToCollapsedConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			if (!(value is int)) return Visibility.Collapsed;
			int val = (int)value;
			if (val > 0) return Visibility.Visible;
			else return Visibility.Collapsed;
		}

		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			throw new Exception("this is a one-way bonding, it should never come here");
		}
	}

	public class SeriesCountGreaterThanZeroToBooleanConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			if (!(value is int)) return false;
			int val = (int)value;
			if (val > 0) return true;
			else return false;
		}

		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			throw new Exception("this is a one-way binding, it should never come here");
		}
	}

	public class CheckpointCountLowerThanMaxToTrueConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			if (!(value is int)) return false;
			int val = (int)value;
			if (val < PersistentData.MaxRecordsInCheckpoints) return true;
			else return false;
		}

		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			throw new Exception("this is a one-way binding, it should never come here");
		}
	}

	public class CheckpointCountEqualMaxToVisibleConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			if (!(value is int)) return Visibility.Visible;
			int val = (int)value;
			if (val < PersistentData.MaxRecordsInCheckpoints) return Visibility.Collapsed;
			else return Visibility.Visible;
		}

		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			throw new Exception("this is a one-way bonding, it should never come here");
		}
	}

	public class PointRecordEmptyToFalseConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			if (!(value is PointRecord)) return false;
			PointRecord val = (PointRecord)value;
			if (!val.IsEmpty()) return true;
			else return false;
		}

		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			throw new Exception("this is a one-way bonding, it should never come here");
		}
	}

	public class PointRecordEmptyToVisibleConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			if (!(value is PointRecord)) return Visibility.Visible;
			PointRecord val = (PointRecord)value;
			if (!val.IsEmpty()) return Visibility.Collapsed;
			else return Visibility.Visible;
		}

		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			throw new Exception("this is a one-way bonding, it should never come here");
		}
	}

	public class PointRecordEmptyToCollapsedConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			if (!(value is PointRecord)) return Visibility.Collapsed;
			PointRecord val = (PointRecord)value;
			if (!val.IsEmpty()) return Visibility.Visible;
			else return Visibility.Collapsed;
		}

		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			throw new Exception("this is a one-way bonding, it should never come here");
		}
	}

	public class StringNotEmptyToVisibleConverter : IValueConverter
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

	public class StringNotEmptyToTrueConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			if (value == null) return false;
			if (string.IsNullOrWhiteSpace(value.ToString())) return false;
			else return true;
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
			throw new Exception("this is a one-way binding, it should never come here");
		}
	}

	public class FloatNotNullToVisibleConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			if (value == null) return Visibility.Collapsed;
			if (string.IsNullOrWhiteSpace(value.ToString()) || value.ToString().Equals(default(double).ToString(CultureInfo.CurrentUICulture)) || value.ToString().Equals(default(int).ToString())) return Visibility.Collapsed;
			else return Visibility.Visible;
		}
		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			throw new Exception("this is a one-way binding, it should never come here");
		}
	}

	public class DateNotNullToVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			if (value == null) return Visibility.Collapsed;

			DateTime dt = default(DateTime);
			if (!DateTime.TryParse(value.ToString(), CultureInfo.CurrentUICulture, DateTimeStyles.None, out dt))
			{
				DateTime.TryParse(value.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out dt);
			}

			if (dt == default(DateTime)) return Visibility.Collapsed;
			else return Visibility.Visible;

		}
		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			throw new Exception("this is a one-way bonding, it should never come here");
		}
	}

	public class AngleConverterLat : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			return AngleConverterHelper.LatitudeToString(value, parameter);
		}

		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			throw new Exception("This is a one-way binding, so I should never get here");
		}
	}

	public class AngleConverterLon : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			return AngleConverterHelper.LongitudeToString(value, parameter);
		}

		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			throw new Exception("This is a one-way binding, so I should never get here");
		}
	}

	public class AngleConverterDeg_Abs : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			return AngleConverterHelper.Float_To_DegMinSecDec_Array(value, parameter, true)[0];
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
			return AngleConverterHelper.Float_To_DegMinSecDec_Array(value, parameter, false)[1];
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
			return AngleConverterHelper.Float_To_DegMinSecDec_Array(value, parameter, false)[2];
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
			return AngleConverterHelper.Float_To_DegMinSecDec_Array(value, parameter, false)[3];
		}

		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			throw new Exception("this is a one-way binding, it should never get here");
		}
	}

	public class FloatConverter8DecimalsAbs : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			if (value == null) return default(double).ToString("#0.#", CultureInfo.CurrentUICulture);

			return FloatConverterHelper.Convert(value, parameter, true, @"#0.########");
		}
		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			throw new Exception("this is a one-way binding, it should never get here");
		}
	}

	public class FloatConverter1Decimals : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			if (value == null) return default(double).ToString("#0.#", CultureInfo.CurrentUICulture);

			return FloatConverterHelper.Convert(value, parameter, false, @"#0.#");
		}
		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			throw new Exception("this is a one-way binding, it should never get here");
		}
	}

	public class FloatConverterNoDecimals : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			if (value == null) return default(double).ToString("#0.#", CultureInfo.CurrentUICulture);

			return FloatConverterHelper.Convert(value, parameter, false, @"#0");
		}
		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			throw new Exception("this is a one-way binding, it should never get here");
		}
	}

	public static class FloatConverterHelper
	{
		public static object Convert(object value, object parameter, bool abs, string format = @"#0.########")
		{
			if (value == null) return default(double).ToString("#0.#", CultureInfo.CurrentUICulture);

			bool isImperialUnits = PersistentData.GetInstance().IsShowImperialUnits;

			double dbl = default(double);
			double.TryParse(value.ToString(), out dbl);

			if (parameter != null)
			{
				if (parameter.ToString() == "M")
				{
					if (isImperialUnits)
					{
						format += " ft";
						dbl *= ConstantData.M_TO_FOOT;
					}
					else format += " m";
				}
				else if (parameter.ToString() == "KM")
				{
					if (isImperialUnits)
					{
						format += " mi";
						dbl *= ConstantData.KM_TO_MILE;
					}
					else format += " km";
				}
				if (parameter.ToString() == "M_KM")
				{
					if (isImperialUnits)
					{
						if (dbl > ConstantData.MILE_TO_M)
						{
							format += " mi";
							dbl *= (ConstantData.KM_TO_MILE / 1000.0);
						}
						else
						{
							format += " ft";
							dbl *= ConstantData.M_TO_FOOT;
						}
					}
					else
					{
						if (dbl > 1000.0)
						{
							format += " km";
							dbl /= 1000.0;
						}
						else
						{
							format += " m";
						}
					}
				}
				else if (parameter.ToString() == "KMH")
				{
					if (isImperialUnits)
					{
						format += " mph";
						dbl *= ConstantData.KM_TO_MILE;
					}
					else format += " kmh";
				}
			}

			if (abs) return Math.Abs(dbl).ToString(format, CultureInfo.CurrentUICulture);
			else return dbl.ToString(format, CultureInfo.CurrentUICulture);
		}
	}

	public class FloatToSignConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			return AngleConverterHelper.FloatToSign(value);
		}
		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			throw new Exception("this is a one-way binding, it should never get here");
		}
	}

	public class FloatLatitudeToNSConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			return AngleConverterHelper.LatitudeToNS(value);
		}
		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			throw new Exception("this is a one-way binding, it should never get here");
		}
	}

	public class FloatLongitudeToEWConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			return AngleConverterHelper.LongitudeToEW(value);
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

	internal class SpeedConverter
	{
		internal static object GetSpeed(object value, bool showUnit)
		{
			string format0 = string.Empty;
			string format1 = string.Empty;
			bool isImperialUnits = PersistentData.GetInstance().IsShowImperialUnits;
			if (isImperialUnits)
			{
				format0 = showUnit ? "#0.# mph" : "#0.#";
				format1 = showUnit ? "# mph" : "#";
			}
			else
			{
				format0 = showUnit ? "#0.# Kmh" : "#0.#";
				format1 = showUnit ? "# Kmh" : "#";
			}

			if (value == null) return default(double).ToString(format0, CultureInfo.CurrentUICulture);
			double dbl = default(double);
			if (double.TryParse(value.ToString(), out dbl) && !double.IsNaN(dbl) && !double.IsInfinity(dbl) && !double.IsNegativeInfinity(dbl) && !double.IsPositiveInfinity(dbl))
			{
				if (isImperialUnits) dbl *= (3.6 * ConstantData.KM_TO_MILE);
				else dbl *= 3.6;
			}
			if (double.IsNaN(dbl) || double.IsInfinity(dbl) || double.IsNegativeInfinity(dbl) || double.IsPositiveInfinity(dbl)) return "";
			if (dbl < 10) return dbl.ToString(format0, CultureInfo.CurrentUICulture);
			else return dbl.ToString(format1, CultureInfo.CurrentUICulture);
		}
	}

	public class SeriesTextConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			if (!(value is PersistentData.Tables)) return string.Empty;
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
			if (!(value is PersistentData.Tables) || parameter == null) return Visibility.Collapsed;
			PersistentData.Tables whichSeries = PersistentData.Tables.Nil;
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
			if (!(value is TileSourceRecord)) return false;
			TileSourceRecord tileSource = (TileSourceRecord)value;
			if (tileSource.IsDefault) return false;
			return true;
		}

		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			throw new Exception("should never get here");
		}
	}

	public class TileSourceToVisibleConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			if (!(value is TileSourceRecord)) return Visibility.Collapsed;
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
			if (!(value is PositionAccuracy)) return false;
			if ((PositionAccuracy)value == PositionAccuracy.Default) return false;
			return true;
		}

		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			if (!(value is bool)) return PositionAccuracy.Default;
			if ((bool)value) return PositionAccuracy.High;
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
			if (!(value is TileSourceRecord)) return false;
			var tileSource = (TileSourceRecord)value;
			return new TextAndTag(tileSource.DisplayName, tileSource);
		}

		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			if (!(value is TextAndTag)) return TileSourceRecord.GetDefaultTileSource();
			var tat = (TextAndTag)value;
			return (TileSourceRecord)(tat.Tag);
		}
	}

	public class TileSourcezToTextAndTagsConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			if (!(value is Collection<TileSourceRecord>)) return false;
			var tileSources = (Collection<TileSourceRecord>)value;
			Collection<TextAndTag> output = new Collection<TextAndTag>();
			bool isClearingCache = (parameter?.ToString() == "forClearingCache");
			bool isClearingCustomCache = (parameter?.ToString() == "forClearingCustomCache");
			bool isSelecting = (parameter?.ToString() == "forSelecting");
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
			if (!(value is TileSourceRecord)) return false;
			return (value as TileSourceRecord).DisplayName;
		}
		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			throw new Exception("this is a one-way binding, it should never come here");
		}
	}

	public static class AngleConverterHelper
	{
		public const int MaxDecimalPlaces = 6;
		public static readonly int TenPowerMaxDecimalPlaces = (int)Math.Pow(10.0, MaxDecimalPlaces);
		public const string FLOAT_LAT_LON_FORMAT = "#0.######";

		public static string LatitudeToString(object value, object parameter)
		{
			PersistentData myData = PersistentData.GetInstance();
			if (myData.IsShowDegrees == false)
			{
				return FloatToNumberString(value);
			}
			else
			{
				return FloatToDegMinSecString_NoDec(value, parameter, false);
			}

		}

		public static string LongitudeToString(object value, object parameter)
		{
			PersistentData myData = PersistentData.GetInstance();
			if (myData.IsShowDegrees == false)
			{
				return FloatToNumberString(value);
			}
			else
			{
				return FloatToDegMinSecString_NoDec(value, parameter, true);
			}
		}

		private static string FloatToNumberString(object value)
		{
			if (value == null) return default(double).ToString("#0.#", CultureInfo.CurrentUICulture);
			double dbl = default(double);
			double.TryParse(value.ToString(), out dbl);
			return dbl.ToString(FLOAT_LAT_LON_FORMAT, CultureInfo.CurrentUICulture);
		}

		private static string FloatToDegMinSecString_NoDec(object value, object parameter, bool longitude)
		{
			int deg;
			int min;
			int sec;
			int dec;
			Float_To_DegMinSecDec(value, parameter, out deg, out min, out sec, out dec);
			if (longitude) return (Math.Abs(deg) + "°" + min + "'" + sec + "\"" + LongitudeToEW(deg)) as string; //we skip dec
			else return (Math.Abs(deg) + "°" + min + "'" + sec + "\"" + LatitudeToNS(deg)) as string; //we skip dec
		}

		public static string[] Float_To_DegMinSecDec_Array(object value, object parameter, bool abs)
		{
			int deg;
			int min;
			int sec;
			int dec;
			Float_To_DegMinSecDec(value, parameter, out deg, out min, out sec, out dec);
			string[] strArray = new string[4];
			if (abs) strArray[0] = Math.Abs(deg).ToString(); else strArray[0] = deg.ToString();
			strArray[1] = min.ToString();
			strArray[2] = sec.ToString();
			strArray[3] = dec.ToString();
			return strArray;
		}

		private static void Float_To_DegMinSecDec(object value, object parameter, out int deg, out int min, out int sec, out int dec)
		{
			double coord = 0.0;
			if (double.TryParse(value.ToString(), out coord))
			{
				deg = (int)Math.Truncate(coord);
				min = (int)Math.Abs(Math.Truncate((coord - deg) * 60.0));
				double secDbl = Math.Abs(Math.Abs(coord - deg) * 3600 - min * 60);
				sec = (int)secDbl;
				dec = (int)((secDbl - sec) * TenPowerMaxDecimalPlaces);

				int sign = Math.Sign(deg);
				if (sign == 0) sign = 1;
			}
			else
			{
				deg = min = sec = dec = 0;
			}
		}

		public static double DegMinSecDec_To_Float(string degStr, string minStr, string secStr, string decStr)
		{
			int deg = 0;
			int.TryParse(degStr, out deg);
			int min = 0;
			int.TryParse(minStr, out min);
			int sec = 0;
			int.TryParse(secStr, out sec);
			int dec = 0;
			int.TryParse(decStr, out dec);
			int sign = Math.Sign(deg);
			if (sign == 0) sign = 1;
			return sign * Math.Abs(dec) / (double)TenPowerMaxDecimalPlaces / 3600.0 + sign * Math.Abs(sec) / 3600.0 + sign * Math.Abs(min) / 60.0 + deg;
		}

		public static string FloatToSign(object value)
		{
			if (value == null) return "?";
			double dbl = default(double);
			double.TryParse(value.ToString(), out dbl);

			if (dbl < default(double)) return "-";
			//else if (dbl == default(double)) return " ";
			else return "+";
		}

		public static object LatitudeToNS(object value)
		{
			if (value == null) return "?";
			double dbl = default(double);
			double.TryParse(value.ToString(), out dbl);

			if (dbl < default(double)) return "S";
			//else if (dbl == default(double)) return " ";
			else return "N";
		}

		public static object LongitudeToEW(object value)
		{
			if (value == null) return "?";
			double dbl = default(double);
			double.TryParse(value.ToString(), out dbl);

			if (dbl < default(double)) return "W";
			//else if (dbl == default(double)) return " ";
			else return "E";
		}
	}
}