using System.Collections;
using UnityEngine;

namespace Lab.Common
{
    public static class RealtimeClock
    {
        public static double Now => Time.realtimeSinceStartupAsDouble;

        public static IEnumerator WaitUntil(double time)
        {
            while (Now < time) yield return null;
        }

        public static double MeanIntervalMs(double firstTime, double lastTime, int count) =>
            count > 1 ? (lastTime - firstTime) * 1000.0 / (count - 1) : 0;
    }
}
