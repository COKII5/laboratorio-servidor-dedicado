using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Lab.Part2
{
    public class EventsPage
    {
        [JsonProperty("events", Required = Required.Always)] public List<EventDto> Events { get; set; }
        [JsonProperty("last_seq", Required = Required.Always)] public int LastSeq { get; set; }
    }

    public class EventDto
    {
        [JsonProperty("seq", Required = Required.Always)] public int Seq { get; set; }
        [JsonProperty("player_id", Required = Required.Always)] public string PlayerId { get; set; }
        [JsonProperty("type", Required = Required.Always)] public string Type { get; set; }
        [JsonProperty("payload")] public JObject Payload { get; set; }

        public bool TryReadPayload<T>(out T payload) where T : class
        {
            try
            {
                payload = Payload?.ToObject<T>();
            }
            catch (JsonException)
            {
                payload = null;
            }
            return payload != null;
        }
    }

    public class OutgoingEvent
    {
        [JsonProperty("player_id")] public string PlayerId { get; set; }
        [JsonProperty("type")] public string Type { get; set; }
        [JsonProperty("payload")] public object Payload { get; set; }
        [JsonProperty("event_id")] public string EventId { get; set; }
    }
}
