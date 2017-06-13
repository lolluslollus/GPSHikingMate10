using LolloGPS.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Utilz;
using Utilz.Data;

namespace LolloGPS.Core
{
	public sealed class AltitudeProfilesVM : ObservableData
	{
		private const double ALTITUDE_SCALE_MARGIN_WHEN_ALL_EQUAL = 50.0;

		internal AltitudeProfilesVM(){}

		internal void InitialiseChartData(Collection<PointRecord> coll, bool respectDatesAndTimes, bool sortIfRespectingDatesAndTimes, int firstIndex, int lastIndex,
			ref double maxAltitude, ref double minAltitude, ref double maxTime, ref double minTime, ref string maxTimeString, ref string minTimeString, ref double[,] boundPoints)
		{
			if (coll == null || !coll.Any()) return;
#if DEBUG
			Stopwatch sw0 = new Stopwatch(); sw0.Start();
#endif
			var collSubset = coll.Where((pointRecord, index) => { return index >= firstIndex && index <= lastIndex; }).ToList();
			InitialiseChartDataWithinBounds(collSubset, respectDatesAndTimes, sortIfRespectingDatesAndTimes, firstIndex, lastIndex,
				ref maxAltitude, ref minAltitude, ref maxTime, ref minTime, ref maxTimeString, ref minTimeString, ref boundPoints);
#if DEBUG
			sw0.Stop();
			Debug.WriteLine(string.Format("Initialising chart data (including sorting) took {0} msec", sw0.ElapsedMilliseconds));
#endif
		}

		private void InitialiseChartDataWithinBounds(IList<PointRecord> coll, bool respectDatesAndTimes, bool sortIfRespectingDatesAndTimes, int firstIndex, int lastIndex,
			ref double maxAltitude, ref double minAltitude, ref double maxTime, ref double minTime, ref string maxTimeString, ref string minTimeString, ref double[,] outPoints)
		{
			if (coll == null || !coll.Any()) return;

			bool isImperialUnits = PersistentData.GetInstance().IsShowImperialUnits;
			double[,] points = new double[coll.Count, 2];

			bool isDateTimeAlwaysPresent = GetDatesAndTimesAlwaysPresentAndDifferent(coll, respectDatesAndTimes);

			// set first point, maxes and mins
			if (isDateTimeAlwaysPresent) points[0, 0] = coll[0].TimePoint.ToBinary();
			else points[0, 0] = 0.0;
			points[0, 1] = MainVM.RoundAndRangeAltitude(coll[0].Altitude, isImperialUnits);

			minTime = maxTime = points[0, 0];
			minAltitude = maxAltitude = points[0, 1];
			// set next points, maxes and mins - if any further points
			for (int i = 1; i < coll.Count; i++)
			{
				if (isDateTimeAlwaysPresent) points[i, 0] = coll[i].TimePoint.ToBinary();
				else points[i, 0] = i;
				points[i, 1] = MainVM.RoundAndRangeAltitude(coll[i].Altitude, isImperialUnits);

				if (points[i, 0] > maxTime) maxTime = points[i, 0];
				if (points[i, 0] < minTime) minTime = points[i, 0];
				if (points[i, 1] > maxAltitude) maxAltitude = points[i, 1];
				if (points[i, 1] < minAltitude) minAltitude = points[i, 1];
			}
			// if we have one piece of data only, make a max and a min, which are far apart enough, 
			// to get the X grid lines left and right and the Y grid lines top and bottom
			if (minTime == maxTime)
			{
				if (minTime == 0.0) // ticks
				{
					minTime = -.5;
					maxTime = .5;
				}
				else // DateTime.ToBinary()
				{
					minTime /= 2; // reduce it: if you increase it, you may get an overflow
				}
				if (minTime > maxTime) LolloMath.Swap(ref minTime, ref maxTime);
			}
			if (isDateTimeAlwaysPresent)
			{
				maxTimeString = DateTime.FromBinary(Convert.ToInt64(maxTime)).ToString(CultureInfo.CurrentUICulture);
				minTimeString = DateTime.FromBinary(Convert.ToInt64(minTime)).ToString(CultureInfo.CurrentUICulture);
			}
			else
			{
				maxTimeString = lastIndex.ToString(CultureInfo.CurrentUICulture);
				minTimeString = firstIndex.ToString(CultureInfo.CurrentUICulture);
			}
			if (minAltitude == maxAltitude)
			{
				minAltitude -= ALTITUDE_SCALE_MARGIN_WHEN_ALL_EQUAL;
				maxAltitude += ALTITUDE_SCALE_MARGIN_WHEN_ALL_EQUAL;
			}
#if DEBUG
			Stopwatch sw0 = new Stopwatch(); sw0.Start();
#endif
			if (isDateTimeAlwaysPresent && sortIfRespectingDatesAndTimes && points.GetUpperBound(0) > 0) outPoints = Sort(points);
			else outPoints = points;
#if DEBUG
			sw0.Stop();
			Debug.WriteLine(string.Format("Sorting chart data took {0} msec", sw0.ElapsedMilliseconds));
#endif
		}
		private static bool GetDatesAndTimesAlwaysPresentAndDifferent(IList<PointRecord> coll, bool respectDatesAndTimes)
		{
			bool isDateTimeAlwaysPresent = true;
			if (respectDatesAndTimes)
			{
				if (coll.Count > 1 && coll.First().TimePoint == coll.Last().TimePoint)
				{
					isDateTimeAlwaysPresent = false;
				}
				else
				{
					if (coll.Any(t => t.TimePoint == default(DateTime) || t.TimePoint.Date == default(DateTime).Date))
					{
						isDateTimeAlwaysPresent = false;
					}
				}
			}
			else // series like checkpoints do not get real date and time: even if some may have it, they are really only a sequence.
			{
				isDateTimeAlwaysPresent = false;
			}
			return isDateTimeAlwaysPresent;
		}
		private static double[,] Sort(double[,] points)
		{
			Stopwatch sw0 = new Stopwatch(); sw0.Start();
			int size = points.GetUpperBound(0) + 1;
			double[,] sortedPoints = new double[size, 2];

			var indexArray = new int[size];
			for (int i = 0; i < size; i++)
			{
				indexArray[i] = i;
			}

			Array.Sort(indexArray, new RectangularComparer(points));

			for (int i = 0; i < size; i++)
			{
				sortedPoints[i, 0] = points[indexArray[i], 0];
				sortedPoints[i, 1] = points[indexArray[i], 1];
			}

			sw0.Stop();
			Debug.WriteLine(string.Format("SORTING took {0} msec", sw0.ElapsedMilliseconds));

			return sortedPoints;
		}
		private class RectangularComparer : IComparer
		{
			// maintain a reference to the 2-dimensional array being sorted
			private readonly double[,] _myArray;

			// constructor initializes the sortArray reference
			public RectangularComparer(double[,] arrayToBeSorted)
			{
				_myArray = arrayToBeSorted;
			}

			public int Compare(object one, object two)
			{
				// one and two are row indexes into the sortArray
				//int iOne = (int)one;
				//int iTwo = (int)two;

				// compare the items in the sortArray
				return _myArray[(int)one, 0].CompareTo(_myArray[(int)two, 0]);
			}
		}
	}
}