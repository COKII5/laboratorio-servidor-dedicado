using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Lab.Part1
{
    public readonly struct LatencyStats
    {
        public int Count { get; }
        public float Mean { get; }
        public float StandardDeviation { get; }
        public float Min { get; }
        public float Median { get; }
        public float P95 { get; }
        public float Max { get; }

        public LatencyStats(IEnumerable<float> samplesMs)
        {
            float[] sorted = samplesMs.OrderBy(rttMs => rttMs).ToArray();
            float mean = sorted.Average();

            Count = sorted.Length;
            Mean = mean;
            StandardDeviation = Mathf.Sqrt(sorted.Sum(rttMs => (rttMs - mean) * (rttMs - mean)) / sorted.Length);
            Min = sorted[0];
            Median = NearestRankPercentile(sorted, 0.50f);
            P95 = NearestRankPercentile(sorted, 0.95f);
            Max = sorted[sorted.Length - 1];
        }

        static float NearestRankPercentile(float[] sorted, float fraction) =>
            sorted[Mathf.Clamp(Mathf.CeilToInt(fraction * sorted.Length) - 1, 0, sorted.Length - 1)];
    }
}
