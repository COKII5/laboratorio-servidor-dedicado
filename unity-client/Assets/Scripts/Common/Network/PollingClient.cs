using System;
using System.Collections;
using System.Diagnostics;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

namespace Lab.Common
{
    /// <summary>
    /// Configuración y peticiones comunes a los clientes de la Parte 1 y de la Parte 2.
    /// </summary>
    public abstract class PollingClient : MonoBehaviour
    {
        [Header("Servicio")]
        [SerializeField] string baseUrl;
        [SerializeField, Min(1), Tooltip("Segundos antes de abortar una petición sin respuesta.")]
        int requestTimeoutSeconds = 5;

        [Header("Identificadores")]
        [SerializeField] string gameId;
        [SerializeField] string localPlayerId = "p1";
        [SerializeField] string remotePlayerId = "p2";

        [Header("Sondeo")]
        [SerializeField, Min(1), Tooltip("Intervalo de sondeo Δt en milisegundos.")]
        int intervalMs = 200;

        [Header("Medición")]
        [SerializeField, Min(0), Tooltip("FPS objetivo con VSync desactivado (0 = no modificar). La corrutina detecta el fin de una petición en el frame siguiente: más FPS, medición más fina.")]
        int targetFrameRate = 240;

        public event Action PlayersChanged;
        public event Action<float, long> GetCompleted;

        public string BaseUrl => baseUrl;
        public string GameId => gameId;
        public string LocalPlayerId => localPlayerId;
        public string RemotePlayerId => remotePlayerId;
        public int IntervalMs { get => intervalMs; set => intervalMs = Mathf.Max(1, value); }
        public int GetCount { get; private set; }
        public int PostCount { get; private set; }
        public float LastGetRttMs { get; private set; }
        public string LastError { get; protected set; }

        protected abstract string DefaultBaseUrl { get; }
        protected abstract string DefaultGameId { get; }
        protected abstract string LogTag { get; }

        void Reset()
        {
            baseUrl = DefaultBaseUrl;
            gameId = DefaultGameId;
        }

        protected virtual void Awake()
        {
            ApplyCommandLine();
            ValidateSettings();

            if (targetFrameRate > 0)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = targetFrameRate;
            }
        }

        protected virtual void Start()
        {
            OnPlayersChanged();
            StartCoroutine(RepeatEveryInterval(Poll));
        }

        public void SwapPlayers()
        {
            (localPlayerId, remotePlayerId) = (remotePlayerId, localPlayerId);
            OnPlayersChanged();
            Debug.Log($"{LogTag} Esta instancia ahora es '{localPlayerId}' y observa a '{remotePlayerId}'.");
        }

        protected virtual void OnPlayersChanged() => PlayersChanged?.Invoke();

        protected abstract IEnumerator Poll();

        // Si un paso tarda más que Δt, el siguiente empieza en cuanto termina, sin acumular ciclos atrasados.
        protected IEnumerator RepeatEveryInterval(Func<IEnumerator> step)
        {
            double nextTick = RealtimeClock.Now;
            while (true)
            {
                yield return step();
                nextTick = Math.Max(nextTick + intervalMs / 1000.0, RealtimeClock.Now);
                yield return RealtimeClock.WaitUntil(nextTick);
            }
        }

        protected IEnumerator SendGet(string url, Action<UnityWebRequest> handleResponse)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = requestTimeoutSeconds;
                var clock = Stopwatch.StartNew();
                yield return request.SendWebRequest();
                clock.Stop();

                GetCount++;
                float rttMs = (float)clock.Elapsed.TotalMilliseconds;
                if (request.responseCode != 0) LastGetRttMs = rttMs;
                GetCompleted?.Invoke(rttMs, request.responseCode);
                handleResponse(request);
            }
        }

        protected IEnumerator SendPost(string url, string json, Action<UnityWebRequest> handleResponse)
        {
            using (UnityWebRequest request = UnityWebRequest.Post(url, json, "application/json"))
            {
                request.timeout = requestTimeoutSeconds;
                yield return request.SendWebRequest();

                PostCount++;
                handleResponse(request);
            }
        }

        protected bool TryParseJson<T>(string json, out T value) where T : class
        {
            try
            {
                value = JsonConvert.DeserializeObject<T>(json);
                return value != null;
            }
            catch (JsonException exception)
            {
                Debug.LogWarning($"{LogTag} Respuesta no válida: {json} ({exception.Message})");
                value = null;
                return false;
            }
        }

        protected static string DescribeFailure(UnityWebRequest request) =>
            request.responseCode == 0 ? request.error : $"HTTP {request.responseCode} {request.error}";

        protected static string Escape(string pathSegment) => Uri.EscapeDataString(pathSegment);

        protected string ServiceUrl(string path) => $"{baseUrl.TrimEnd('/')}/{path}";

        // Permiten abrir dos copias del mismo ejecutable como jugadores distintos sin recompilar.
        void ApplyCommandLine()
        {
            if (CommandLineArgs.TryGet("-url", out string url)) baseUrl = url;
            if (CommandLineArgs.TryGet("-gameId", out string game)) gameId = game;
            if (CommandLineArgs.TryGet("-playerId", out string player)) localPlayerId = player;
            if (CommandLineArgs.TryGet("-remoteId", out string remote)) remotePlayerId = remote;
            if (CommandLineArgs.TryGet("-dt", out string interval))
            {
                if (int.TryParse(interval, out int parsedMs) && parsedMs > 0)
                    IntervalMs = parsedMs;
                else
                    Debug.LogWarning($"{LogTag} -dt '{interval}' no es un entero positivo; se usa Δt = {intervalMs} ms.");
            }
        }

        void ValidateSettings()
        {
            bool validUrl = Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
            if (!validUrl)
            {
                Debug.LogWarning($"{LogTag} La URL del servicio '{baseUrl}' no es válida; se usa {DefaultBaseUrl}.");
                baseUrl = DefaultBaseUrl;
            }

            if (string.IsNullOrWhiteSpace(gameId))
            {
                Debug.LogWarning($"{LogTag} Falta el identificador de partida; se usa '{DefaultGameId}'.");
                gameId = DefaultGameId;
            }

            if (string.IsNullOrWhiteSpace(localPlayerId) || string.IsNullOrWhiteSpace(remotePlayerId) || localPlayerId == remotePlayerId)
                Debug.LogError($"{LogTag} Los identificadores local ('{localPlayerId}') y remoto ('{remotePlayerId}') deben ser distintos y no vacíos.");
        }
    }
}
