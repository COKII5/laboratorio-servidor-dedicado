using System.Collections;
using System.Text;
using UnityEngine;

namespace Lab.Common
{
    /// <summary>
    /// Teclas y panel compartidos por las dos partes: Δt, cambio de jugador y experimentos por línea de comandos.
    /// </summary>
    public abstract class SyncClientControls : MonoBehaviour
    {
        const float ExperimentStartDelaySeconds = 2f;
        static readonly KeyCode[] IntervalKeys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3 };
        static readonly int[] IntervalPresetsMs = { 50, 200, 1000 };

        readonly InfoPanel panel = new InfoPanel();
        readonly RequestRateMeter rateMeter = new RequestRateMeter();

        protected abstract PollingClient Client { get; }
        protected abstract string Title { get; }
        protected abstract string ShortcutsHelp { get; }

        protected abstract void HandleKeys();
        protected abstract void StartExperiment(string name);
        protected abstract string DescribeConnection();
        protected abstract void AppendDetails(StringBuilder text);

        IEnumerator Start()
        {
            if (!CommandLineArgs.TryGet("-experiment", out string experiment)) yield break;

            // Margen para que la otra instancia termine de arrancar.
            yield return new WaitForSecondsRealtime(ExperimentStartDelaySeconds);
            StartExperiment(experiment);
        }

        void Update()
        {
            for (int i = 0; i < IntervalKeys.Length; i++)
            {
                if (Input.GetKeyDown(IntervalKeys[i])) Client.IntervalMs = IntervalPresetsMs[i];
            }
            if (Input.GetKeyDown(KeyCode.Tab)) Client.SwapPlayers();
            if (Input.GetKeyDown(KeyCode.H)) panel.ToggleVisible();
            HandleKeys();

            rateMeter.Sample(Client.GetCount, Client.PostCount);
        }

        void OnGUI() => panel.Draw(BuildPanelText);

        string BuildPanelText()
        {
            var text = new StringBuilder();
            text.AppendLine($"<b>{Title}</b>");
            text.AppendLine($"Partida <b>{Client.GameId}</b> · local <b>{Client.LocalPlayerId}</b> · remoto <b>{Client.RemotePlayerId}</b>");
            text.AppendLine($"Δt = <b>{Client.IntervalMs} ms</b> · {Client.BaseUrl}");
            text.AppendLine($"Estado: {DescribeConnection()}");
            text.AppendLine($"Ping GET: {Client.LastGetRttMs:F1} ms · esta instancia: GET {rateMeter.GetsPerSecond:F1}/s + POST {rateMeter.PostsPerSecond:F1}/s");
            AppendDetails(text);
            text.AppendLine($"Resultados: {ResultsWriter.Folder}");
            text.Append($"<i>WASD mover · {ShortcutsHelp} · 1/2/3 Δt {string.Join("/", IntervalPresetsMs)} · Tab cambiar jugador · H ocultar</i>");
            return text.ToString();
        }
    }
}
