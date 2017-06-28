using System;
using System.Collections.Generic;

namespace LolloGPS.Data.TileCache
{
    public static class ProgressHelper
    {
        internal static Stack<int> GetStepsToReport(int totalCnt, int maxProgressStepsToReport)
        {
            int howManyProgressStepsIWantToReport = Math.Min(maxProgressStepsToReport, totalCnt);

            var stepsWhenIWantToRaiseProgress = new Stack<int>(howManyProgressStepsIWantToReport);
            if (howManyProgressStepsIWantToReport > 0)
            {
                for (int i = howManyProgressStepsIWantToReport; i > 0; i--)
                {
                    stepsWhenIWantToRaiseProgress.Push(totalCnt * i / howManyProgressStepsIWantToReport);
                }
            }
            return stepsWhenIWantToRaiseProgress;
        }
    }
}