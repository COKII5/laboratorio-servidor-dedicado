using Lab.Common;
using UnityEngine;

namespace Lab.Part2
{
    /// <summary>
    /// Auto del jugador remoto. No recibe su posición continua: solo reacciona a sus eventos.
    /// </summary>
    public class RemoteRacerView : MonoBehaviour
    {
        const float PulseSeconds = 0.35f;
        const float PulseScaleGain = 0.5f;

        [SerializeField] EventSyncClient client;
        [SerializeField] Track track;
        [SerializeField, Min(0.1f)] float moveSpeed = 15f;

        public int Boosts { get; private set; }
        public int Checkpoints { get; private set; }
        public int Laps { get; private set; }
        public string LastLap { get; private set; } = "—";

        Vector3 target;
        Vector3 baseScale;
        float pulseUntil;
        Color color;

        void Awake()
        {
            target = transform.position;
            baseScale = transform.localScale;
        }

        void OnEnable()
        {
            client.RemoteEventReceived += OnRemoteEvent;
            client.PlayersChanged += Repaint;
        }

        void OnDisable()
        {
            client.RemoteEventReceived -= OnRemoteEvent;
            client.PlayersChanged -= Repaint;
        }

        void Update()
        {
            transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);

            float pulse = Mathf.Clamp01((pulseUntil - Time.time) / PulseSeconds);
            transform.localScale = baseScale * (1f + PulseScaleGain * pulse);
        }

        void OnRemoteEvent(EventDto received)
        {
            switch (received.Type)
            {
                case RaceEvents.BoostUsed:
                    ShowBoost();
                    break;
                case RaceEvents.CheckpointPassed:
                    MoveToCheckpoint(received);
                    break;
                case RaceEvents.LapCompleted:
                    CountLap(received);
                    break;
                default:
                    Debug.LogWarning($"[Parte2] Tipo de evento desconocido en #{received.Seq}: {received.Type}");
                    break;
            }
        }

        void ShowBoost()
        {
            Boosts++;
            pulseUntil = Time.time + PulseSeconds;
        }

        void MoveToCheckpoint(EventDto received)
        {
            if (!received.TryReadPayload(out CheckpointPayload passed) || !track.IsValid(passed.Checkpoint))
            {
                Debug.LogWarning($"[Parte2] CheckpointPassed con datos no válidos en el evento #{received.Seq}.");
                return;
            }

            Checkpoints++;
            Vector3 checkpointPosition = track.Position(passed.Checkpoint);
            target = new Vector3(checkpointPosition.x, transform.position.y, checkpointPosition.z);
            track.Flash(passed.Checkpoint, color);
        }

        void CountLap(EventDto received)
        {
            if (!received.TryReadPayload(out LapPayload completedLap))
            {
                Debug.LogWarning($"[Parte2] LapCompleted con datos no válidos en el evento #{received.Seq}.");
                return;
            }

            Laps++;
            LastLap = $"vuelta {completedLap.Lap} en {completedLap.LapTime:F2} s";
        }

        void Repaint()
        {
            color = PlayerColors.For(client.RemotePlayerId, client.LocalPlayerId);
            PlayerColors.Paint(transform, color);
        }
    }
}
