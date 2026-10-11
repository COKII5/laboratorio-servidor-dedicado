using System;
using System.Collections;
using Lab.Common;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Lab.Part1
{
    /// <summary>
    /// Sincronización por estado: publica la posición local y consulta la remota cada Δt.
    /// </summary>
    public class PositionSyncClient : PollingClient
    {
        public enum RemoteState { Waiting, NotPublished, Receiving, Error }

        [Header("Escena")]
        [SerializeField] Transform localPlayer;
        [SerializeField] Transform remotePlayer;
        [SerializeField, Tooltip("Mueve al jugador remoto suavemente hacia la última posición recibida en lugar de hacerlo saltar.")]
        bool interpolateRemote;
        [SerializeField, Min(0.1f)] float interpolationSpeed = 10f;

        public event Action<PositionDto> RemotePositionReceived;

        public bool InterpolateRemote { get => interpolateRemote; set => interpolateRemote = value; }
        public bool PublishingPaused { get; set; }
        public RemoteState State { get; private set; } = RemoteState.Waiting;

        protected override string DefaultBaseUrl => "http://localhost:5005";
        protected override string DefaultGameId => "partida1";
        protected override string LogTag => "[Parte1]";

        bool postInFlight;
        bool lastPostFailed;
        Vector3 remoteTarget;

        protected override void Start()
        {
            base.Start();
            StartCoroutine(RepeatEveryInterval(PublishLocalPosition));
        }

        void Update()
        {
            if (remotePlayer == null || !remotePlayer.gameObject.activeSelf) return;

            remotePlayer.position = interpolateRemote
                ? Vector3.Lerp(remotePlayer.position, remoteTarget, 1f - Mathf.Exp(-interpolationSpeed * Time.deltaTime))
                : remoteTarget;
        }

        // Espera a que termine el POST en curso: nunca hay dos a la vez.
        public IEnumerator SendPosition(PositionDto position)
        {
            while (postInFlight) yield return null;

            postInFlight = true;
            yield return SendPost(PlayerUrl(LocalPlayerId), JsonConvert.SerializeObject(position), ReportPostResult);
            postInFlight = false;
        }

        protected override IEnumerator Poll() => SendGet(PlayerUrl(RemotePlayerId), HandleRemotePosition);

        protected override void OnPlayersChanged()
        {
            State = RemoteState.Waiting;
            LastError = null;
            if (remotePlayer != null) remotePlayer.gameObject.SetActive(false);
            PlayerColors.Paint(localPlayer, PlayerColors.For(LocalPlayerId, RemotePlayerId));
            PlayerColors.Paint(remotePlayer, PlayerColors.For(RemotePlayerId, LocalPlayerId));
            base.OnPlayersChanged();
        }

        IEnumerator PublishLocalPosition()
        {
            if (!PublishingPaused && localPlayer != null)
                yield return SendPosition(new PositionDto(localPlayer.position));
        }

        // Solo se registra el cambio de estado para no llenar la consola cada Δt.
        void ReportPostResult(UnityWebRequest request)
        {
            bool failed = request.responseCode != 201;
            if (failed && !lastPostFailed)
                Debug.LogWarning($"{LogTag} POST falló ({DescribeFailure(request)}). Se sigue intentando cada Δt.");
            else if (!failed && lastPostFailed)
                Debug.Log($"{LogTag} POST vuelve a funcionar.");
            lastPostFailed = failed;
        }

        void HandleRemotePosition(UnityWebRequest request)
        {
            // El 404 es normal al inicio (el remoto aún no publica) y no trae un cuerpo útil.
            if (request.responseCode == 404)
            {
                SetState(RemoteState.NotPublished, null);
                return;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                SetState(RemoteState.Error, DescribeFailure(request));
                return;
            }

            if (!TryParseJson(request.downloadHandler.text, out PositionDto position))
            {
                SetState(RemoteState.Error, "respuesta no válida");
                return;
            }

            SetState(RemoteState.Receiving, null);
            ShowRemoteAt(position);
        }

        void ShowRemoteAt(PositionDto position)
        {
            remoteTarget = position.ToVector3();
            if (remotePlayer != null && !remotePlayer.gameObject.activeSelf)
            {
                // Con la primera posición aparece directamente ahí, sin interpolar desde el origen.
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
                    Debug.Log($"{LogTag} '{RemotePlayerId}' todavía no ha publicado (404). Se sigue sondeando.");
                    break;
                case RemoteState.Receiving:
                    Debug.Log($"{LogTag} Recibiendo la posición de '{RemotePlayerId}'.");
                    break;
                case RemoteState.Error:
                    Debug.LogWarning($"{LogTag} Error al consultar a '{RemotePlayerId}': {error}. Se sigue sondeando.");
                    break;
            }
        }

        string PlayerUrl(string playerId) => ServiceUrl($"server/{Escape(GameId)}/{Escape(playerId)}");
    }
}
