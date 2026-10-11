using System.Collections;
using System.Collections.Generic;
using Lab.Common;
using UnityEngine;

namespace Lab.Part1
{
    /// <summary>
    /// Experimento B. Las posiciones de la ráfaga van a una altura que el jugador nunca alcanza,
    /// para distinguirlas del movimiento normal, y el valor posX = 0 marca el inicio.
    /// </summary>
    [RequireComponent(typeof(PositionSyncClient))]
    public class SamplingExperiment : MonoBehaviour
    {
        const float MarkerHeight = 2f;
        const int StartMarkerValue = 0;

        [SerializeField, Min(1)] int valueCount = 20;
        [SerializeField, Min(1)] int spacingMs = 20;
        [SerializeField, Min(0f), Tooltip("Pausa antes y después de la ráfaga. Debe superar el Δt más grande para que el observador alcance a ver el inicio (posX = 0) y el último valor.")]
        float settleSeconds = 1.5f;

        public string PublisherStatus { get; private set; } = "sin ejecutar";
        public string ObserverStatus { get; private set; } = "esperando una ráfaga del otro jugador";

        PositionSyncClient client;
        Coroutine burst;
        bool observing;
        readonly List<int> observedValues = new List<int>();

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
            yield return client.SendPosition(MarkerPosition(StartMarkerValue));
            yield return new WaitForSecondsRealtime(settleSeconds);

            double start = RealtimeClock.Now;
            double lastSent = start;
            for (int value = 1; value <= valueCount; value++)
            {
                yield return RealtimeClock.WaitUntil(start + (value - 1) * spacingMs / 1000.0);
                lastSent = RealtimeClock.Now;
                PublisherStatus = $"publicando {value}/{valueCount}";
                yield return client.SendPosition(MarkerPosition(value));
            }

            double durationMs = (lastSent - start) * 1000.0;
            double meanSpacingMs = RealtimeClock.MeanIntervalMs(start, lastSent, valueCount);
            SavePublisherResult(meanSpacingMs, durationMs);

            // El último valor se mantiene para que el observador lo vea aun con un Δt grande.
            PublisherStatus = "manteniendo el último valor";
            yield return new WaitForSecondsRealtime(settleSeconds);
            client.PublishingPaused = false;
            PublisherStatus = $"listo: {valueCount} valores, separación media {meanSpacingMs:F1} ms";
            burst = null;
        }

        static PositionDto MarkerPosition(int value) => new PositionDto(new Vector3(value, MarkerHeight, 0f));

        void SavePublisherResult(double meanSpacingMs, double durationMs)
        {
            ResultsWriter.AppendRow($"expB_publicador_{client.LocalPlayerId}.csv",
                "fecha,jugador,valores,separacion_objetivo_ms,separacion_media_ms,duracion_ms",
                string.Join(",", ResultsWriter.Timestamp(), client.LocalPlayerId, valueCount, spacingMs,
                    ResultsWriter.Number(meanSpacingMs), ResultsWriter.Number(durationMs)));
            Debug.Log($"[Exp B] Ráfaga publicada: {valueCount} valores en {durationMs:F0} ms (separación media {meanSpacingMs:F1} ms).");
        }

        void OnRemotePosition(PositionDto position)
        {
            if (!Mathf.Approximately(position.Y, MarkerHeight))
            {
                // El otro jugador volvió a su posición normal sin que se viera el último valor.
                if (observing) FinishObservation(complete: false);
                return;
            }

            int value = Mathf.RoundToInt(position.X);
            if (value == StartMarkerValue)
            {
                observing = true;
                observedValues.Clear();
                ObserverStatus = "ráfaga detectada, observando";
                return;
            }

            // Una ráfaga que empezó antes de ver el valor de inicio no se cuenta.
            if (!observing) return;

            if (!observedValues.Contains(value)) observedValues.Add(value);
            ObserverStatus = $"observando: {string.Join(" ", observedValues)}";
            if (value == valueCount) FinishObservation(complete: true);
        }

        void FinishObservation(bool complete)
        {
            observing = false;
            string values = string.Join(" ", observedValues);
            ResultsWriter.AppendRow($"expB_observador_{client.LocalPlayerId}.csv",
                "fecha,observador,dt_ms,valores_distintos,valores,resultado",
                string.Join(",", ResultsWriter.Timestamp(), client.LocalPlayerId, client.IntervalMs, observedValues.Count,
                    values, complete ? "completo" : "interrumpido"));

            ObserverStatus = $"Δt {client.IntervalMs} ms: vio {observedValues.Count} de {valueCount} ({values})";
            Debug.Log($"[Exp B] {ObserverStatus}");
        }
    }
}
