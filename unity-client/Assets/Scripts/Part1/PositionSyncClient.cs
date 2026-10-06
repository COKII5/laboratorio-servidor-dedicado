using System;
using System.Collections;
using System.Diagnostics;
using Lab.Common;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

namespace Lab.Part1
{
    /// <summary>
    /// Sincronización por estado (Parte 1). Publica la posición del jugador local cada Δt y
    /// consulta cada Δt la última posición conocida del jugador remoto.
    /// Cada tipo de petición corre en su propia corrutina y espera a que termine la anterior,
    /// así que nunca hay dos peticiones del mismo tipo en curso.
    /// </summary>
    public class PositionSyncClient : MonoBehaviour
    {
        public enum RemoteState { Waiting, NotPublished, Receiving, Error }

        static readonly Color FirstPlayerColor = new Color(0.20f, 0.55f, 0.95f);
        static readonly Color SecondPlayerColor = new Color(0.95f, 0.45f, 0.15f);

        [Header("Servicio")]
        [SerializeField] string baseUrl = "http://localhost:5005";
        [SerializeField, Min(1), Tooltip("Segundos antes de abortar una petición sin respuesta.")]
        int requestTimeoutSeconds = 5;

        [Header("Identificadores")]
        [SerializeField] string gameId = "partida1";
        [SerializeField] string localPlayerId = "p1";
        [SerializeField] string remotePlayerId = "p2";

        [Header("Sondeo")]
        [SerializeField, Min(1), Tooltip("Intervalo de sondeo Δt en milisegundos. Se usa para publicar y para consultar.")]
        int intervalMs = 200;

        [Header("Escena")]
        [SerializeField] Transform localPlayer;
        [SerializeField] Transform remotePlayer;
        [SerializeField, Tooltip("Mueve al jugador remoto suavemente hacia la última posición recibida en lugar de hacerlo saltar.")]
        bool interpolateRemote;
        [SerializeField, Min(0.1f)] float interpolationSpeed = 10f;

        [Header("Medición")]
        [SerializeField, Min(0), Tooltip("FPS objetivo con VSync desactivado (0 = no modificar). La corrutina detecta el fin de una petición en el frame siguiente, así que el ping medido incluye hasta un frame de espera: más FPS, medición más fina.")]
        int targetFrameRate = 240;

        /// <summary>Tiempo de ida y vuelta en ms y código HTTP (0 si no hubo respuesta).</summary>
        public event Action<float, long> GetCompleted;
        public event Action<float, long> PostCompleted;
        public event Action<PositionDto> RemotePositionReceived;

        public string BaseUrl => baseUrl;
        public string GameId => gameId;
        public string LocalPlayerId => localPlayerId;
        public string RemotePlayerId => remotePlayerId;
        public int IntervalMs { get => intervalMs; set => intervalMs = Mathf.Max(1, value); }
        public bool InterpolateRemote { get => interpolateRemote; set => interpolateRemote = value; }

        /// <summary>Detiene la publicación periódica; el experimento B la usa para publicar sus propios valores.</summary>
        public bool PublishingPaused { get; set; }
        public int GetCount { get; private set; }
        public int PostCount { get; private set; }
        public float LastGetRttMs { get; private set; }
        public RemoteState State { get; private set; } = RemoteState.Waiting;
        public string LastError { get; private set; }

        bool postInFlight;
        bool lastPostFailed;
        Vector3 remoteTarget;

        void Awake()
        {
            // Una compilación arranca con los valores del Inspector; estos argumentos permiten
            // abrir dos copias del mismo ejecutable como jugadores distintos.
            if (CommandLineArgs.TryGet("-url", out string url)) baseUrl = url;
            if (CommandLineArgs.TryGet("-gameId", out string game)) gameId = game;
            if (CommandLineArgs.TryGet("-playerId", out string player)) localPlayerId = player;
            if (CommandLineArgs.TryGet("-remoteId", out string remote)) remotePlayerId = remote;
            if (CommandLineArgs.TryGet("-dt", out string dt) && int.TryParse(dt, out int ms)) IntervalMs = ms;

            if (targetFrameRate > 0)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = targetFrameRate;
            }
        }

        void Start()
        {
            ResetRemote();
            StartCoroutine(PublishLoop());
            StartCoroutine(PollLoop());
        }

        void Update()
        {
            if (remotePlayer == null || !remotePlayer.gameObject.activeSelf) return;

            remotePlayer.position = interpolateRemote
                ? Vector3.Lerp(remotePlayer.position, remoteTarget, 1f - Mathf.Exp(-interpolationSpeed * Time.deltaTime))
                : remoteTarget;
        }

        /// <summary>
        /// Intercambia los identificadores local y remoto. Las dos instancias arrancan con la
        /// misma escena, así que en una de ellas hay que intercambiarlos.
        /// </summary>
        public void SwapPlayers()
        {
            (localPlayerId, remotePlayerId) = (remotePlayerId, localPlayerId);
            ResetRemote();
            Debug.Log($"[Parte1] Esta instancia ahora es '{localPlayerId}' y observa a '{remotePlayerId}'.");
        }

        /// <summary>Publica una posición. Si ya hay un POST en curso, espera a que termine.</summary>
        public IEnumerator SendPosition(PositionDto position)
        {
            while (postInFlight) yield return null;
            postInFlight = true;

            string json = JsonUtility.ToJson(position);
            using (UnityWebRequest request = UnityWebRequest.Post(PlayerUrl(localPlayerId), json, "application/json"))
            {
                request.timeout = requestTimeoutSeconds;
                var clock = Stopwatch.StartNew();
                yield return request.SendWebRequest();
                clock.Stop();

                postInFlight = false;
                PostCount++;

                // Solo se registra el cambio de estado para no llenar la consola cada Δt.
                bool failed = request.responseCode != 201;
                if (failed && !lastPostFailed)
                    Debug.LogWarning($"[Parte1] POST falló ({request.responseCode} {request.error}). Se sigue intentando cada Δt.");
                else if (!failed && lastPostFailed)
                    Debug.Log("[Parte1] POST vuelve a funcionar.");
                lastPostFailed = failed;

                PostCompleted?.Invoke((float)clock.Elapsed.TotalMilliseconds, request.responseCode);
            }
        }

        IEnumerator PublishLoop()
        {
            double nextTick = Time.realtimeSinceStartupAsDouble;
            while (true)
            {
                if (!PublishingPaused && localPlayer != null)
                    yield return SendPosition(new PositionDto(localPlayer.position));

                nextTick = NextTick(nextTick);
                while (Time.realtimeSinceStartupAsDouble < nextTick) yield return null;
            }
        }

        IEnumerator PollLoop()
        {
            double nextTick = Time.realtimeSinceStartupAsDouble;
            while (true)
            {
                yield return FetchRemotePosition();

                nextTick = NextTick(nextTick);
                while (Time.realtimeSinceStartupAsDouble < nextTick) yield return null;
            }
        }

        /// <summary>
        /// Cada ciclo empieza Δt después del anterior. Si una petición tarda más que Δt, el
        /// siguiente ciclo empieza en cuanto termina, sin acumular ciclos atrasados.
        /// </summary>
        double NextTick(double previousTick) =>
            Math.Max(previousTick + intervalMs / 1000.0, Time.realtimeSinceStartupAsDouble);

        IEnumerator FetchRemotePosition()
        {
            using (UnityWebRequest request = UnityWebRequest.Get(PlayerUrl(remotePlayerId)))
            {
                request.timeout = requestTimeoutSeconds;
                var clock = Stopwatch.StartNew();
                yield return request.SendWebRequest();
                clock.Stop();

                GetCount++;
                float rttMs = (float)clock.Elapsed.TotalMilliseconds;

                if (request.result == UnityWebRequest.Result.Success && TryParse(request.downloadHandler.text, out PositionDto position))
                {
                    LastGetRttMs = rttMs;
                    SetState(RemoteState.Receiving, null);
                    ApplyRemotePosition(position);
                }
                else if (request.responseCode == 404)
                {
                    // Normal al inicio: el jugador remoto aún no ha publicado. El 404 no trae un
                    // cuerpo útil, así que se decide solo por el código y se sigue sondeando.
                    LastGetRttMs = rttMs;
                    SetState(RemoteState.NotPublished, null);
                }
                else
                {
                    SetState(RemoteState.Error, request.responseCode == 0 ? request.error : $"HTTP {request.responseCode} {request.error}");
                }

                GetCompleted?.Invoke(rttMs, request.responseCode);
            }
        }

        static bool TryParse(string json, out PositionDto position)
        {
            try
            {
                position = JsonUtility.FromJson<PositionDto>(json);
                return true;
            }
            catch (ArgumentException e)
            {
                Debug.LogWarning($"[Parte1] Respuesta no válida: {json} ({e.Message})");
                position = default;
                return false;
            }
        }

        void ApplyRemotePosition(PositionDto position)
        {
            remoteTarget = position.ToVector3();
            if (remotePlayer != null && !remotePlayer.gameObject.activeSelf)
            {
                // Primera posición recibida: aparece directamente ahí, sin interpolar desde el origen.
                remotePlayer.position = remoteTarget;
                remotePlayer.gameObject.SetActive(true);
            }

            RemotePositionReceived?.Invoke(position);
        }

        void SetState(RemoteState state, string error)
        {
            if (state == State && error == LastError) return;
            State = state;
            LastError = error;

            switch (state)
            {
                case RemoteState.NotPublished:
                    Debug.Log($"[Parte1] '{remotePlayerId}' todavía no ha publicado (404). Se sigue sondeando.");
                    break;
                case RemoteState.Receiving:
                    Debug.Log($"[Parte1] Recibiendo la posición de '{remotePlayerId}'.");
                    break;
                case RemoteState.Error:
                    Debug.LogWarning($"[Parte1] Error al consultar a '{remotePlayerId}': {error}. Se sigue sondeando.");
                    break;
            }
        }

        void ResetRemote()
        {
            State = RemoteState.Waiting;
            LastError = null;
            if (remotePlayer != null) remotePlayer.gameObject.SetActive(false);
            ApplyColors();
        }

        /// <summary>
        /// El identificador menor se pinta azul y el otro naranja, así cada jugador tiene el
        /// mismo color en las dos instancias.
        /// </summary>
        void ApplyColors()
        {
            bool localIsFirst = string.CompareOrdinal(localPlayerId, remotePlayerId) <= 0;
            Paint(localPlayer, localIsFirst ? FirstPlayerColor : SecondPlayerColor);
            Paint(remotePlayer, localIsFirst ? SecondPlayerColor : FirstPlayerColor);
        }

        static void Paint(Transform target, Color color)
        {
            if (target != null && target.TryGetComponent(out Renderer renderer))
                renderer.material.color = color;
        }

        string PlayerUrl(string playerId) =>
            $"{baseUrl.TrimEnd('/')}/server/{Uri.EscapeDataString(gameId)}/{Uri.EscapeDataString(playerId)}";
    }
}
