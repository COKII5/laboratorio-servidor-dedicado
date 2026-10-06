using System.Collections;
using System.Text;
using Lab.Common;
using UnityEngine;

namespace Lab.Part1
{
    /// <summary>
    /// Atajos de teclado, argumentos de línea de comandos y panel en pantalla de la Parte 1.
    /// El panel muestra Δt, ping y peticiones por segundo, útil también para los videos.
    /// </summary>
    [RequireComponent(typeof(PositionSyncClient), typeof(LatencyExperiment), typeof(SamplingExperiment))]
    public class Part1Controls : MonoBehaviour
    {
        PositionSyncClient client;
        LatencyExperiment latency;
        SamplingExperiment sampling;
        bool panelVisible = true;
        GUIStyle panelStyle;

        // Peticiones por segundo de esta instancia, medidas en ventanas de un segundo.
        int windowGetCount;
        int windowPostCount;
        double windowStart;
        double getRate;
        double postRate;

        void Awake()
        {
            client = GetComponent<PositionSyncClient>();
            latency = GetComponent<LatencyExperiment>();
            sampling = GetComponent<SamplingExperiment>();
        }

        /// <summary>En una compilación, "-experiment latency" o "-experiment burst" inicia un experimento al arrancar.</summary>
        IEnumerator Start()
        {
            if (!CommandLineArgs.TryGet("-experiment", out string experiment)) yield break;

            // Margen para que la otra instancia termine de arrancar.
            yield return new WaitForSecondsRealtime(2f);
            switch (experiment)
            {
                case "latency": latency.Toggle(); break;
                case "burst": sampling.StartBurst(); break;
                default: Debug.LogWarning($"[Parte1] Experimento desconocido: {experiment}. Use latency o burst."); break;
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) client.IntervalMs = 50;
            if (Input.GetKeyDown(KeyCode.Alpha2)) client.IntervalMs = 200;
            if (Input.GetKeyDown(KeyCode.Alpha3)) client.IntervalMs = 1000;
            if (Input.GetKeyDown(KeyCode.Tab)) client.SwapPlayers();
            if (Input.GetKeyDown(KeyCode.I)) client.InterpolateRemote = !client.InterpolateRemote;
            if (Input.GetKeyDown(KeyCode.L)) latency.Toggle();
            if (Input.GetKeyDown(KeyCode.B)) sampling.StartBurst();
            if (Input.GetKeyDown(KeyCode.H)) panelVisible = !panelVisible;

            double now = Time.realtimeSinceStartupAsDouble;
            double elapsed = now - windowStart;
            if (elapsed >= 1.0)
            {
                getRate = (client.GetCount - windowGetCount) / elapsed;
                postRate = (client.PostCount - windowPostCount) / elapsed;
                windowGetCount = client.GetCount;
                windowPostCount = client.PostCount;
                windowStart = now;
            }
        }

        void OnGUI()
        {
            if (!panelVisible)
            {
                GUI.Label(new Rect(10, 10, 300, 24), "H: mostrar panel");
                return;
            }

            panelStyle ??= new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                richText = true,
                wordWrap = true,
                padding = new RectOffset(10, 10, 8, 8),
            };
            panelStyle.fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height / 38f, 11f, 22f));

            var text = new StringBuilder();
            text.AppendLine("<b>Parte 1 · sincronización por estado</b>");
            text.AppendLine($"Partida <b>{client.GameId}</b> · local <b>{client.LocalPlayerId}</b> · remoto <b>{client.RemotePlayerId}</b>");
            text.AppendLine($"Δt = <b>{client.IntervalMs} ms</b> · {client.BaseUrl}");
            text.AppendLine($"Remoto: {DescribeRemote()}");
            text.AppendLine($"Ping GET: {client.LastGetRttMs:F1} ms · esta instancia: GET {getRate:F1}/s + POST {postRate:F1}/s");
            text.AppendLine($"Interpolación: {(client.InterpolateRemote ? "sí" : "no")}");
            text.AppendLine($"Exp. A: {latency.Status}");
            foreach (string summary in latency.Summaries) text.AppendLine($"   {summary}");
            text.AppendLine($"Exp. B (publica): {sampling.PublisherStatus}");
            text.AppendLine($"Exp. B (observa): {sampling.ObserverStatus}");
            text.AppendLine($"Resultados: {ResultsWriter.Folder}");
            text.Append("<i>WASD mover · 1/2/3 Δt 50/200/1000 · Tab cambiar jugador · I interpolar · L exp. A · B exp. B · H ocultar</i>");

            float width = Mathf.Min(panelStyle.fontSize * 40f, Screen.width - 20f);
            GUILayout.BeginArea(new Rect(10, 10, width, Screen.height - 20));
            GUILayout.Box(text.ToString(), panelStyle);
            GUILayout.EndArea();
        }

        string DescribeRemote()
        {
            switch (client.State)
            {
                case PositionSyncClient.RemoteState.NotPublished: return "aún no ha publicado (404)";
                case PositionSyncClient.RemoteState.Receiving: return "<color=#7CFC9A>recibiendo posición</color>";
                case PositionSyncClient.RemoteState.Error: return $"<color=#FF7B7B>error: {client.LastError}</color>";
                default: return "esperando la primera respuesta";
            }
        }
    }
}
