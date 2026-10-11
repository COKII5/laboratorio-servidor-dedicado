using Newtonsoft.Json;
using UnityEngine;

namespace Lab.Part2
{
    public static class RaceEvents
    {
        public const string BoostUsed = "BoostUsed";
        public const string CheckpointPassed = "CheckpointPassed";
        public const string LapCompleted = "LapCompleted";
    }

    public class PositionPayload
    {
        [JsonProperty("x")] public float X { get; set; }
        [JsonProperty("y")] public float Y { get; set; }
        [JsonProperty("z")] public float Z { get; set; }

        public PositionPayload() { }

        public PositionPayload(Vector3 position)
        {
            X = position.x;
            Y = position.y;
            Z = position.z;
        }
    }

    public class CheckpointPayload : PositionPayload
    {
        [JsonProperty("checkpoint", Required = Required.Always)] public int Checkpoint { get; set; }
        [JsonProperty("lap", Required = Required.Always)] public int Lap { get; set; }

        public CheckpointPayload() { }

        public CheckpointPayload(int checkpoint, int lap, Vector3 position) : base(position)
        {
            Checkpoint = checkpoint;
            Lap = lap;
        }
    }

    public class LapPayload
    {
        [JsonProperty("lap", Required = Required.Always)] public int Lap { get; set; }
        [JsonProperty("lap_time", Required = Required.Always)] public float LapTime { get; set; }
    }

    // BoostUsed enviado por el experimento C, marcado con su posición dentro de la ráfaga.
    public class BurstPayload
    {
        [JsonProperty("index")] public int Index { get; set; }
        [JsonProperty("burst_id")] public string BurstId { get; set; }

        public static bool TryRead(EventDto received, out BurstPayload payload)
        {
            payload = null;
            return received.Type == RaceEvents.BoostUsed
                && received.TryReadPayload(out payload)
                && !string.IsNullOrEmpty(payload.BurstId);
        }
    }
}
