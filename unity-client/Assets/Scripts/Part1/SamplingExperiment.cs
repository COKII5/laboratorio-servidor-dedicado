using System.Collections;
using System.Collections.Generic;
using Lab.Common;
using UnityEngine;

namespace Lab.Part1
{
    /// <summary>
    /// Experimento B. La instancia que lo inicia publica posX = 1..N, uno cada 20 ms; la otra
    /// cuenta cuántos valores distintos alcanza a ver con su Δt. Ambas instancias observan
    /// siempre, así que basta con iniciarlo en la que publica.
    /// Para distinguir la ráfaga del movimiento normal, sus posiciones van a una altura que el
    /// jugador nunca alcanza, y un valor previo posX = 0 marca el inicio.
    /// </summary>
    [RequireComponent(typeof(PositionSyncClient))]
    public class SamplingExperiment : MonoBehaviour
    {
        const float MarkerHeight = 2f;

        [SerializeField, Min(1)] int valueCount = 20;
        [SerializeField, Min(1)] int spacingMs = 20;
        [SerializeField, Min(0f), Tooltip("Pausa antes y después de la ráfaga. Debe superar el Δt más grande para que el observador alcance a ver el inicio (posX = 0) y el último valor.")]
        float settleSeconds = 1.5f;

        public string PublisherStatus { get; private set; } = "sin ejecutar";
        public string ObserverStatus { get; private set; } = "esperando una ráfaga del otro jugador";

        PositionSyncClient client;
        Coroutine burst;
        bool observing;
        readonly List<int> observed = new List<int>();  // valores distintos en orden de llegada

        void Awake() => client = GetComponent<PositionSyncClient>();
        void OnEnable() => client.RemotePositionReceived += OnRemotePosition;
        void OnDisable() => client.RemotePositionReceived -= OnRemotePosition;

        [ContextMenu("Publicar ráfaga del experimento B")]
        public void StartBurst()
        {
            if (burst == null) burst = StartCoroutine(PublishBurst());
        }

        IEnumerator PublishBurst()
        {
            client.PublishingPaused = true;
            PublisherStatus = "publicando el valor de inicio (posX = 0)";
            yield return client.SendPosition(MarkerPosition(0));
            yield return new WaitForSecondsRealtime(settleSeconds);

            var sendTimes = new double[valueCount];
            double start = Time.realtimeSinceStartupAsDouble;
            for (int i = 1; i <= valueCount; i++)
            {
                double due = start + (i - 1) * spacingMs / 1000.0;
                while (Time.realtimeSinceStartupAsDouble < due) yield return null;

                sendTimes[i - 1] = Time.realtimeSinceStartupAsDouble;
                PublisherStatus = $"publicando {i}/{valueCount}";
                yield return client.SendPosition(MarkerPosition(i));
            }

            double durationMs = (sendTimes[^1] - sendTimes[0]) * 1000.0;
            double meanSpacingMs = valueCount > 1 ? durationMs / (valueCount - 1) : 0;
            string path = ResultsWriter.AppendRow($"expB_publicador_{client.LocalPlayerId}.csv",
                "fecha,jugador,valores,separacion_objetivo_ms,separacion_media_ms,duracion_ms",
                string.Join(",", ResultsWriter.Timestamp(), client.LocalPlayerId, valueCount, spacingMs,
                    ResultsWriter.Number(meanSpacingMs), ResultsWriter.Number(durationMs)));
            Debug.Log($"[Exp B] Ráfaga publicada: {valueCount} valores en {durationMs:F0} ms (separación media {meanSpacingMs:F1} ms). Guardado en {path}");

            // El último valor se mantiene un rato para que el observador lo vea aun con Δt grande.
            PublisherStatus = "manteniendo el último valor";
            yield return new WaitForSecondsRealtime(settleSeconds);
            client.PublishingPaused = false;
            PublisherStatus = $"listo: {valueCount} valores, separación media {meanSpacingMs:F1} ms";
            burst = null;
        }

        static PositionDto MarkerPosition(int value) =>
            new PositionDto { posX = value, posY = MarkerHeight, posZ = 0f };

        void OnRemotePosition(PositionDto position)
        {
            if (!Mathf.Approximately(position.posY, MarkerHeight))
            {
                // El otro jugador volvió a su posición normal sin que se viera el último valor.
                if (observing) FinishObservation(complete: false);
                return;
            }

            int value = Mathf.RoundToInt(position.posX);
            if (value == 0)
            {
                observing = true;
                observed.Clear();
                ObserverStatus = "ráfaga detectada, observando";
                return;
            }

            // Si la ráfaga empezó antes de ver el valor de inicio, no se cuenta.
            if (!observing) return;

            if (!observed.Contains(value)) observed.Add(value);
            ObserverStatus = $"observando: {string.Join(" ", observed)}";
            if (value == valueCount) FinishObservation(complete: true);
        }

        void FinishObservation(bool complete)
        {
            observing = false;
            string values = string.Join(" ", observed);
            string path = ResultsWriter.AppendRow($"expB_observador_{client.LocalPlayerId}.csv",
                "fecha,observador,dt_ms,valores_distintos,valores,resultado",
                string.Join(",", ResultsWriter.Timestamp(), client.LocalPlayerId, client.IntervalMs, observed.Count,
                    values, complete ? "completo" : "interrumpido"));

            ObserverStatus = $"Δt {client.IntervalMs} ms: vio {observed.Count} de {valueCount} ({values})";
            Debug.Log($"[Exp B] {ObserverStatus}. Guardado en {path}");
        }
    }
}
