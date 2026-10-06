using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Lab.Common;
using UnityEngine;

namespace Lab.Part1
{
    /// <summary>
    /// Experimento A. Para cada Δt registra N tiempos de ida y vuelta del GET y las peticiones
    /// por segundo que genera esta instancia (GET + POST). La carga del sistema completo es la
    /// suma de las dos instancias, así que el experimento debe correr en ambas a la vez.
    /// </summary>
    [RequireComponent(typeof(PositionSyncClient))]
    public class LatencyExperiment : MonoBehaviour
    {
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

            foreach (int dt in intervalsMs)
            {
                int previousMs = client.IntervalMs;
                client.IntervalMs = dt;
                Status = $"Δt = {dt} ms: estabilizando";
                // Dos ciclos de margen para que ambos bucles ya usen el nuevo Δt.
                yield return new WaitForSecondsRealtime(2 * Mathf.Max(dt, previousMs) / 1000f);

                samples.Clear();
                int getsBefore = client.GetCount;
                int postsBefore = client.PostCount;
                double start = Time.realtimeSinceStartupAsDouble;
                collecting = true;
                while (samples.Count < samplesPerInterval)
                {
                    Status = $"Δt = {dt} ms: {samples.Count}/{samplesPerInterval} muestras";
                    yield return null;
                }
                collecting = false;

                double seconds = Time.realtimeSinceStartupAsDouble - start;
                Save(runStamp, dt, seconds, client.GetCount - getsBefore, client.PostCount - postsBefore);
            }

            Finish($"terminado ({intervalsMs.Length} intervalos)");
        }

        void OnGetCompleted(float rttMs, long responseCode)
        {
            // Los errores de red (código 0) no son un viaje de ida y vuelta completo.
            if (collecting && responseCode != 0 && samples.Count < samplesPerInterval)
                samples.Add(rttMs);
        }

        void Save(string runStamp, int dt, double seconds, int gets, int posts)
        {
            float[] sorted = samples.OrderBy(x => x).ToArray();
            float mean = sorted.Average();
            float std = Mathf.Sqrt(sorted.Sum(x => (x - mean) * (x - mean)) / sorted.Length);
            float median = Percentile(sorted, 0.50f);
            float p95 = Percentile(sorted, 0.95f);
            double getRate = gets / seconds;
            double postRate = posts / seconds;
            string player = client.LocalPlayerId;

            ResultsWriter.AppendRows($"expA_muestras_{player}.csv",
                "fecha,jugador,dt_ms,muestra,rtt_ms",
                samples.Select((rtt, i) => string.Join(",", runStamp, player, dt, i + 1, ResultsWriter.Number(rtt))));

            string path = ResultsWriter.AppendRow($"expA_resumen_{player}.csv",
                "fecha,jugador,dt_ms,muestras,rtt_promedio_ms,rtt_desv_ms,rtt_min_ms,rtt_mediana_ms,rtt_p95_ms,rtt_max_ms,duracion_s,get_por_s,post_por_s,total_por_s",
                string.Join(",", runStamp, player, dt, sorted.Length,
                    ResultsWriter.Number(mean), ResultsWriter.Number(std), ResultsWriter.Number(sorted[0]),
                    ResultsWriter.Number(median), ResultsWriter.Number(p95), ResultsWriter.Number(sorted[^1]),
                    ResultsWriter.Number(seconds), ResultsWriter.Number(getRate), ResultsWriter.Number(postRate),
                    ResultsWriter.Number(getRate + postRate)));

            string summary = $"Δt {dt} ms: ping {mean:F1} ms (p95 {p95:F1}) · {getRate + postRate:F1} req/s en esta instancia";
            summaries.Add(summary);
            Debug.Log($"[Exp A] {summary}. Guardado en {path}");
        }

        /// <summary>Percentil por rango más cercano sobre un arreglo ordenado.</summary>
        static float Percentile(float[] sorted, float p) =>
            sorted[Mathf.Clamp(Mathf.CeilToInt(p * sorted.Length) - 1, 0, sorted.Length - 1)];

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
