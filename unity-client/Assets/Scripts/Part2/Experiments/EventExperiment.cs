using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Lab.Common;
using UnityEngine;

namespace Lab.Part2
{
    /// <summary>
    /// Experimento C. El observador cuenta los eventos tal como los entrega el servicio, antes de
    /// que el cliente descarte repetidos, para que el resultado mida el servicio y no el filtro.
    /// </summary>
    [RequireComponent(typeof(EventSyncClient))]
    public class EventExperiment : MonoBehaviour
    {
        const int BurstIdLength = 8;

        [SerializeField, Min(1)] int eventCount = 20;
        [SerializeField, Min(1)] int spacingMs = 20;

        public string PublisherStatus { get; private set; } = "sin ejecutar";
        public string ObserverStatus { get; private set; } = "esperando una ráfaga del otro jugador";

        EventSyncClient client;
        Coroutine burst;

        string observedBurstId;
        string finishedBurstId;
        int pollsWithEvents;
        readonly List<int> observedSeqs = new List<int>();
        readonly List<int> observedIndexes = new List<int>();

        void Awake() => client = GetComponent<EventSyncClient>();
        void OnEnable() => client.EventsFetched += OnEventsFetched;
        void OnDisable() => client.EventsFetched -= OnEventsFetched;

        [ContextMenu("Enviar ráfaga del experimento C")]
        public void StartBurst()
        {
            if (burst == null) burst = StartCoroutine(SendBurst());
        }

        IEnumerator SendBurst()
        {
            string burstId = Guid.NewGuid().ToString("N").Substring(0, BurstIdLength);
            double start = RealtimeClock.Now;
            double lastQueued = start;

            for (int index = 1; index <= eventCount; index++)
            {
                yield return RealtimeClock.WaitUntil(start + (index - 1) * spacingMs / 1000.0);
                lastQueued = RealtimeClock.Now;
                client.Send(RaceEvents.BoostUsed, new BurstPayload { Index = index, BurstId = burstId });
                PublisherStatus = $"enviando {index}/{eventCount}";
            }

            PublisherStatus = "esperando a que el servicio registre todos";
            while (client.PendingCount > 0) yield return null;

            double meanSpacingMs = RealtimeClock.MeanIntervalMs(start, lastQueued, eventCount);
            double registeredMs = (RealtimeClock.Now - start) * 1000.0;
            ResultsWriter.AppendRow($"expC_publicador_{client.LocalPlayerId}.csv",
                "fecha,jugador,rafaga,eventos,separacion_objetivo_ms,separacion_media_ms,duracion_hasta_registrar_ms",
                string.Join(",", ResultsWriter.Timestamp(), client.LocalPlayerId, burstId, eventCount, spacingMs,
                    ResultsWriter.Number(meanSpacingMs), ResultsWriter.Number(registeredMs)));

            PublisherStatus = $"listo: {eventCount} eventos, separación media {meanSpacingMs:F1} ms, registrados en {registeredMs:F0} ms";
            Debug.Log($"[Exp C] Ráfaga {burstId}: {PublisherStatus}");
            burst = null;
        }

        void OnEventsFetched(IReadOnlyList<EventDto> events)
        {
            bool anyFromBurst = false;
            foreach (EventDto received in events)
            {
                if (received.PlayerId == client.LocalPlayerId) continue;
                if (!BurstPayload.TryRead(received, out BurstPayload payload) || payload.BurstId == finishedBurstId) continue;

                if (payload.BurstId != observedBurstId) StartObserving(payload.BurstId);
                observedSeqs.Add(received.Seq);
                observedIndexes.Add(payload.Index);
                anyFromBurst = true;
            }

            if (!anyFromBurst) return;
            pollsWithEvents++;
            ObserverStatus = $"ráfaga {observedBurstId}: {observedIndexes.Distinct().Count()} de {eventCount}";
            if (observedIndexes.Contains(eventCount)) FinishObservation();
        }

        void StartObserving(string burstId)
        {
            observedBurstId = burstId;
            observedSeqs.Clear();
            observedIndexes.Clear();
            pollsWithEvents = 0;
        }

        void FinishObservation()
        {
            int receivedCount = observedIndexes.Distinct().Count();
            int repeatedCount = observedSeqs.Count - observedSeqs.Distinct().Count();
            bool inOrder = IsNonDecreasing(observedIndexes) && IsNonDecreasing(observedSeqs);

            ResultsWriter.AppendRow($"expC_observador_{client.LocalPlayerId}.csv",
                "fecha,observador,rafaga,dt_ms,recibidos,faltantes,en_orden,repetidos,consultas_con_eventos",
                string.Join(",", ResultsWriter.Timestamp(), client.LocalPlayerId, observedBurstId, client.IntervalMs, receivedCount,
                    eventCount - receivedCount, inOrder ? "si" : "no", repeatedCount, pollsWithEvents));

            ObserverStatus = $"Δt {client.IntervalMs} ms: {receivedCount} de {eventCount} · en orden: {(inOrder ? "sí" : "no")} · repetidos: {repeatedCount} · {pollsWithEvents} consultas";
            Debug.Log($"[Exp C] {ObserverStatus}");
            finishedBurstId = observedBurstId;
            observedBurstId = null;
        }

        static bool IsNonDecreasing(IReadOnlyList<int> values)
        {
            for (int i = 1; i < values.Count; i++)
            {
                if (values[i] < values[i - 1]) return false;
            }
            return true;
        }
    }
}
