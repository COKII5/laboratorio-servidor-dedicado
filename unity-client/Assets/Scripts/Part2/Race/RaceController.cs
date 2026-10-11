using Lab.Common;
using UnityEngine;

namespace Lab.Part2
{
    /// <summary>
    /// Carrera del jugador local. Cada acción se aplica de inmediato aquí y se envía como evento
    /// para que la otra instancia la reproduzca.
    /// </summary>
    [RequireComponent(typeof(LocalPlayerMover))]
    public class RaceController : MonoBehaviour
    {
        const int FinishLineCheckpoint = 0;

        [SerializeField] EventSyncClient client;
        [SerializeField] Track track;
        [SerializeField, Min(1f)] float boostMultiplier = 2f;
        [SerializeField, Min(0.1f)] float boostSeconds = 1.5f;
        [SerializeField, Min(0f)] float boostCooldownSeconds = 3f;

        public int Lap { get; private set; } = 1;
        public int NextCheckpoint { get; private set; } = 1;
        public float LapSeconds => Time.time - lapStartTime;

        public string BoostStatus
        {
            get
            {
                if (Time.time < boostEndsAt) return "activo";
                if (Time.time < boostReadyAt) return $"recargando {boostReadyAt - Time.time:F1} s";
                return "listo";
            }
        }

        LocalPlayerMover mover;
        float lapStartTime;
        float boostEndsAt;
        float boostReadyAt;

        Color LocalColor => PlayerColors.For(client.LocalPlayerId, client.RemotePlayerId);

        void Awake() => mover = GetComponent<LocalPlayerMover>();
        void OnEnable() => client.PlayersChanged += Repaint;
        void OnDisable() => client.PlayersChanged -= Repaint;

        void Start()
        {
            lapStartTime = Time.time;
            track.Highlight(NextCheckpoint);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space) && Time.time >= boostReadyAt) UseBoost();
            mover.SpeedMultiplier = Time.time < boostEndsAt ? boostMultiplier : 1f;

            if (track.Contains(NextCheckpoint, transform.position)) PassCheckpoint();
        }

        void UseBoost()
        {
            boostEndsAt = Time.time + boostSeconds;
            boostReadyAt = boostEndsAt + boostCooldownSeconds;
            client.Send(RaceEvents.BoostUsed, new PositionPayload(transform.position));
        }

        void PassCheckpoint()
        {
            int passed = NextCheckpoint;
            client.Send(RaceEvents.CheckpointPassed, new CheckpointPayload(passed, Lap, transform.position));
            track.Flash(passed, LocalColor);

            // La meta solo se vuelve el siguiente checkpoint después de pasar por los demás.
            if (passed == FinishLineCheckpoint) CompleteLap();

            NextCheckpoint = (passed + 1) % track.Count;
            track.Highlight(NextCheckpoint);
        }

        void CompleteLap()
        {
            client.Send(RaceEvents.LapCompleted, new LapPayload { Lap = Lap, LapTime = LapSeconds });
            Lap++;
            lapStartTime = Time.time;
        }

        void Repaint() => PlayerColors.Paint(transform, LocalColor);
    }
}
