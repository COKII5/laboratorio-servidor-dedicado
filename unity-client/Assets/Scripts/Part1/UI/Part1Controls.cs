using System.Text;
using Lab.Common;
using UnityEngine;

namespace Lab.Part1
{
    [RequireComponent(typeof(PositionSyncClient), typeof(LatencyExperiment), typeof(SamplingExperiment))]
    public class Part1Controls : SyncClientControls
    {
        PositionSyncClient client;
        LatencyExperiment latency;
        SamplingExperiment sampling;

        protected override PollingClient Client => client;
        protected override string Title => "Parte 1 · sincronización por estado";
        protected override string ShortcutsHelp => "I interpolar · L exp. A · B exp. B";

        void Awake()
        {
            client = GetComponent<PositionSyncClient>();
            latency = GetComponent<LatencyExperiment>();
            sampling = GetComponent<SamplingExperiment>();
        }

        protected override void HandleKeys()
        {
            if (Input.GetKeyDown(KeyCode.I)) client.InterpolateRemote = !client.InterpolateRemote;
            if (Input.GetKeyDown(KeyCode.L)) latency.Toggle();
            if (Input.GetKeyDown(KeyCode.B)) sampling.StartBurst();
        }

        protected override void StartExperiment(string name)
        {
            switch (name)
            {
                case "latency": latency.Toggle(); break;
                case "burst": sampling.StartBurst(); break;
                default: Debug.LogWarning($"[Parte1] Experimento desconocido: {name}. Use latency o burst."); break;
            }
        }

        protected override string DescribeConnection() => client.State switch
        {
            PositionSyncClient.RemoteState.NotPublished => "el remoto aún no ha publicado (404)",
            PositionSyncClient.RemoteState.Receiving => "<color=#7CFC9A>recibiendo posición</color>",
            PositionSyncClient.RemoteState.Error => $"<color=#FF7B7B>error: {client.LastError}</color>",
            _ => "esperando la primera respuesta",
        };

        protected override void AppendDetails(StringBuilder text)
        {
            text.AppendLine($"Interpolación: {(client.InterpolateRemote ? "sí" : "no")}");
            text.AppendLine($"Exp. A: {latency.Status}");
            foreach (string summary in latency.Summaries) text.AppendLine($"   {summary}");
            text.AppendLine($"Exp. B (publica): {sampling.PublisherStatus}");
            text.AppendLine($"Exp. B (observa): {sampling.ObserverStatus}");
        }
    }
}
