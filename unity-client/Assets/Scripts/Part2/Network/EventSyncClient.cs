using System;
using System.Collections;
using System.Collections.Generic;
using Lab.Common;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Lab.Part2
{
    /// <summary>
    /// Sincronización por eventos: envía cada acción local y consulta cada Δt solo los eventos
    /// posteriores al último seq recibido. Los envíos salen de una cola, uno a la vez y en orden.
    /// </summary>
    public class EventSyncClient : PollingClient
    {
        public enum ServiceState { Connecting, Ok, Error }

        class PendingEvent
        {
            public string Type { get; set; }
            public string Body { get; set; }
        }

        [Header("Envío")]
        [SerializeField, Min(0f), Tooltip("Pausa antes de reintentar un envío que falló por red.")]
        float retryDelaySeconds = 0.5f;

        public event Action<IReadOnlyList<EventDto>> EventsFetched;
        public event Action<EventDto> RemoteEventReceived;

        public ServiceState State { get; private set; } = ServiceState.Connecting;
        public int LastSeq { get; private set; }
        public int PendingCount => outbox.Count;
        public int SentCount { get; private set; }
        public int RetryCount { get; private set; }
        public int ReceivedCount { get; private set; }

        protected override string DefaultBaseUrl => "http://localhost:5006";
        protected override string DefaultGameId => "carrera1";
        protected override string LogTag => "[Parte2]";

        readonly Queue<PendingEvent> outbox = new Queue<PendingEvent>();
        bool joined;

        protected override void Start()
        {
            base.Start();
            StartCoroutine(SendLoop());
        }

        public void Send(string type, object payload)
        {
            // El event_id se fija al encolar para que todos los reintentos lleven el mismo
            // y el servicio no registre el evento dos veces.
            string body = JsonConvert.SerializeObject(new OutgoingEvent
            {
                PlayerId = LocalPlayerId,
                Type = type,
                Payload = payload,
                EventId = Guid.NewGuid().ToString("N"),
            });
            outbox.Enqueue(new PendingEvent { Type = type, Body = body });
        }

        IEnumerator SendLoop()
        {
            while (true)
            {
                if (outbox.Count == 0)
                {
                    yield return null;
                    continue;
                }

                bool settled = false;
                yield return SendPost(EventsUrl(), outbox.Peek().Body, request => settled = HandleSendResult(request));
                if (!settled) yield return new WaitForSecondsRealtime(retryDelaySeconds);
            }
        }

        bool HandleSendResult(UnityWebRequest request)
        {
            PendingEvent pending = outbox.Peek();
            long code = request.responseCode;

            // 200 indica un reenvío que el servicio ya tenía registrado.
            if (code == 201 || code == 200)
            {
                outbox.Dequeue();
                SentCount++;
                SetState(ServiceState.Ok, null);
                return true;
            }

            // Un 4xx (por ejemplo, 422 por datos faltantes) no se arregla reintentando.
            if (code >= 400 && code < 500)
            {
                outbox.Dequeue();
                Debug.LogError($"{LogTag} El servicio rechazó {pending.Type} ({code}): {request.downloadHandler.text}");
                return true;
            }

            RetryCount++;
            SetState(ServiceState.Error, DescribeFailure(request));
            return false;
        }

        protected override IEnumerator Poll()
        {
            // La primera consulta solo averigua el último seq, para no reproducir eventos
            // ocurridos antes de entrar a la partida.
            int since = joined ? LastSeq : int.MaxValue;
            return SendGet($"{EventsUrl()}?since={since}", HandleEventsResponse);
        }

        void HandleEventsResponse(UnityWebRequest request)
        {
            if (request.result != UnityWebRequest.Result.Success)
            {
                SetState(ServiceState.Error, DescribeFailure(request));
                return;
            }

            if (!TryParseJson(request.downloadHandler.text, out EventsPage page))
            {
                SetState(ServiceState.Error, "respuesta no válida");
                return;
            }

            SetState(ServiceState.Ok, null);
            if (!joined)
                JoinAt(page.LastSeq);
            else if (page.LastSeq < LastSeq)
                RestartFromZero(page.LastSeq);
            else
                Dispatch(page.Events);
        }

        void JoinAt(int lastSeq)
        {
            joined = true;
            LastSeq = lastSeq;
            Debug.Log($"{LogTag} Conectado a '{GameId}'. Se reciben los eventos posteriores al {LastSeq}.");
        }

        // El servicio guarda los eventos en memoria: si se reinicia, el seq vuelve a empezar.
        void RestartFromZero(int serviceLastSeq)
        {
            Debug.LogWarning($"{LogTag} El servicio volvió a seq {serviceLastSeq} (este cliente iba en {LastSeq}); probablemente se reinició. Se piden de nuevo desde 0.");
            LastSeq = 0;
        }

        void Dispatch(IReadOnlyList<EventDto> events)
        {
            EventsFetched?.Invoke(events);
            foreach (EventDto received in events)
            {
                // Garantiza que cada evento se aplique una sola vez aunque el servicio lo repita.
                if (received.Seq <= LastSeq) continue;
                LastSeq = received.Seq;

                // Los eventos propios ya se aplicaron al producirse.
                if (received.PlayerId == LocalPlayerId) continue;

                ReceivedCount++;
                RemoteEventReceived?.Invoke(received);
            }
        }

        void SetState(ServiceState state, string error)
        {
            if (state == State && error == LastError) return;
            State = state;
            LastError = error;

            if (state == ServiceState.Error)
                Debug.LogWarning($"{LogTag} Error con el servicio de eventos: {error}. Se sigue intentando.");
            else if (state == ServiceState.Ok)
                Debug.Log($"{LogTag} Servicio de eventos disponible.");
        }

        string EventsUrl() => ServiceUrl($"games/{Escape(GameId)}/events");
    }
}
