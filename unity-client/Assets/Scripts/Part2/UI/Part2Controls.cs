using System.Collections.Generic;
using System.Text;
using Lab.Common;
using UnityEngine;

namespace Lab.Part2
{
    [RequireComponent(typeof(EventSyncClient), typeof(EventExperiment))]
    public class Part2Controls : SyncClientControls
    {
        const int EventLogLines = 8;

        [SerializeField] RaceController race;
        [SerializeField] RemoteRacerView remoteRacer;

        EventSyncClient client;
        EventExperiment experiment;
        readonly Queue<string> eventLog = new Queue<string>();

        protected override PollingClient Client => client;
        protected override string Title => "Parte 2 · sincronización por eventos · Carreras";
        protected override string ShortcutsHelp => "Espacio boost · C exp. C";

        void Awake()
        {
            client = GetComponent<EventSyncClient>();
            experiment = GetComponent<EventExperiment>();
        }

        void OnEnable() => client.RemoteEventReceived += LogEvent;
        void OnDisable() => client.RemoteEventReceived -= LogEvent;

        protected override void HandleKeys()
        {
            if (Input.GetKeyDown(KeyCode.C)) experiment.StartBurst();
        }

        protected override void StartExperiment(string name)
        {
            if (name == "events")
                experiment.StartBurst();
            else
                Debug.LogWarning($"[Parte2] Experimento desconocido: {name}. Use events.");
        }

        protected override string DescribeConnection() => client.State switch
        {
            EventSyncClient.ServiceState.Ok => "<color=#7CFC9A>servicio OK</color>",
            EventSyncClient.ServiceState.Error => $"<color=#FF7B7B>error: {client.LastError}</color>",
            _ => "conectando",
        };

        protected override void AppendDetails(StringBuilder text)
        {
            text.AppendLine($"last_seq {client.LastSeq} · enviados {client.SentCount} · en cola {client.PendingCount} · reintentos {client.RetryCount} · recibidos {client.ReceivedCount}");
            text.AppendLine($"Tú: vuelta {race.Lap} · siguiente checkpoint {race.NextCheckpoint} · {race.LapSeconds:F1} s · boost {race.BoostStatus}");
            text.AppendLine($"Remoto: boosts {remoteRacer.Boosts} · checkpoints {remoteRacer.Checkpoints} · vueltas {remoteRacer.Laps} · última {remoteRacer.LastLap}");
            text.AppendLine($"Exp. C (envía): {experiment.PublisherStatus}");
            text.AppendLine($"Exp. C (observa): {experiment.ObserverStatus}");
            text.AppendLine("Últimos eventos recibidos:");
            foreach (string line in eventLog) text.AppendLine($"   {line}");
        }

        void LogEvent(EventDto received)
        {
            string line = $"#{received.Seq} {received.PlayerId} {Describe(received)}";
            Debug.Log($"[Parte2] Recibido {line}");
            eventLog.Enqueue(line);
            while (eventLog.Count > EventLogLines) eventLog.Dequeue();
        }

        static string Describe(EventDto received)
        {
            switch (received.Type)
            {
                case RaceEvents.BoostUsed:
                    return BurstPayload.TryRead(received, out BurstPayload burst) ? $"BoostUsed (ráfaga, {burst.Index})" : "BoostUsed";
                case RaceEvents.CheckpointPassed:
                    return received.TryReadPayload(out CheckpointPayload passed)
                        ? $"CheckpointPassed {passed.Checkpoint} · vuelta {passed.Lap}"
                        : "CheckpointPassed (datos no válidos)";
                case RaceEvents.LapCompleted:
                    return received.TryReadPayload(out LapPayload completedLap)
                        ? $"LapCompleted vuelta {completedLap.Lap} · {completedLap.LapTime:F2} s"
                        : "LapCompleted (datos no válidos)";
                default:
                    return received.Type;
            }
        }
    }
}
