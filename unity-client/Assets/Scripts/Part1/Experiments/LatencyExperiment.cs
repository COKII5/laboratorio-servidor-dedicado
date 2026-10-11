using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Lab.Common;
using UnityEngine;

namespace Lab.Part1
{
    /// <summary>
    /// Experimento A. La carga del sistema completo es la suma de las dos instancias, así que
    /// debe correr en ambas a la vez.
    /// </summary>
    [RequireComponent(typeof(PositionSyncClient))]
    public class LatencyExperiment : MonoBehaviour
    {
        const int SettleCycles = 2;

        [SerializeField] int[] intervalsMs = { 50, 200, 1000 };
        [SerializeField, Min(1)] int samplesPerInterval = 100;

        public string Status { get; private set; } = "sin ejecutar";
        public IReadOnlyList<string> Summaries => summaries;

        PositionSyncClient client;
        Coroutine routine;
        int originalIntervalMs;
        bool collecting;
        readonly List<float> samples = new List<float>();
        readonly List<string> summaries = new List<string>();

        void Awake() => client = GetComponent<PositionSyncClient>();

        [ContextMenu("Iniciar o detener el experimento A")]
        public void Toggle()
        {
            if (routine == null)
            {
                routine = StartCoroutine(Run());
            }
            else
            {
                StopCoroutine(routine);
                Finish("detenido");
            }
        }

        IEnumerator Run()
        {
            originalIntervalMs = client.IntervalMs;
            client.GetCompleted += OnGetCompleted;
            summaries.Clear();
            string runStamp = ResultsWriter.Timestamp();
            Debug.Log($"[Exp A] Inicio en '{client.LocalPlayerId}'. Debe correr al mismo tiempo en la otra instancia.");

            foreach (int intervalMs in intervalsMs)
                yield return MeasureInterval(runStamp, intervalMs);

            Finish($"terminado ({intervalsMs.Length} intervalos)");
        }

        IEnumerator MeasureInterval(string runStamp, int intervalMs)
        {
            int previousMs = client.IntervalMs;
            client.IntervalMs = intervalMs;
            Status = $"Δt = {intervalMs} ms: estabilizando";
            // Margen para que los dos bucles del cliente ya usen el nuevo Δt.
            yield return new WaitForSecondsRealtime(SettleCycles * Mathf.Max(intervalMs, previousMs) / 1000f);

            samples.Clear();
            int getsBefore = client.GetCount;
            int postsBefore = client.PostCount;
            double start = RealtimeClock.Now;
            collecting = true;
            while (samples.Count < samplesPerInterval)
            {
                Status = $"Δt = {intervalMs} ms: {samples.Count}/{samplesPerInterval} muestras";
                yield return null;
            }
            collecting = false;

            double seconds = RealtimeClock.Now - start;
            Report(runStamp, intervalMs, seconds, (client.GetCount - getsBefore) / seconds, (client.PostCount - postsBefore) / seconds);
        }

        void OnGetCompleted(float rttMs, long responseCode)
        {
            // Un error de red (código 0) no es un viaje de ida y vuelta completo.
            if (collecting && responseCode != 0 && samples.Count < samplesPerInterval)
                samples.Add(rttMs);
        }

        void Report(string runStamp, int intervalMs, double seconds, double getRate, double postRate)
        {
            var stats = new LatencyStats(samples);
            string player = client.LocalPlayerId;

            ResultsWriter.AppendRows($"expA_muestras_{player}.csv",
                "fecha,jugador,dt_ms,muestra,rtt_ms",
                samples.Select((rttMs, index) => string.Join(",", runStamp, player, intervalMs, index + 1, ResultsWriter.Number(rttMs))));

            ResultsWriter.AppendRow($"expA_resumen_{player}.csv",
                "fecha,jugador,dt_ms,muestras,rtt_promedio_ms,rtt_desv_ms,rtt_min_ms,rtt_mediana_ms,rtt_p95_ms,rtt_max_ms,duracion_s,get_por_s,post_por_s,total_por_s",
                string.Join(",", runStamp, player, intervalMs, stats.Count,
                    ResultsWriter.Number(stats.Mean), ResultsWriter.Number(stats.StandardDeviation), ResultsWriter.Number(stats.Min),
                    ResultsWriter.Number(stats.Median), ResultsWriter.Number(stats.P95), ResultsWriter.Number(stats.Max),
                    ResultsWriter.Number(seconds), ResultsWriter.Number(getRate), ResultsWriter.Number(postRate),
                    ResultsWriter.Number(getRate + postRate)));

            string summary = $"Δt {intervalMs} ms: ping {stats.Mean:F1} ms (p95 {stats.P95:F1}) · {getRate + postRate:F1} req/s en esta instancia";
            summaries.Add(summary);
            Debug.Log($"[Exp A] {summary}");
        }

        void Finish(string status)
        {
            collecting = false;
            client.GetCompleted -= OnGetCompleted;
            client.IntervalMs = originalIntervalMs;
            routine = null;
            Status = status;
        }
    }
}
