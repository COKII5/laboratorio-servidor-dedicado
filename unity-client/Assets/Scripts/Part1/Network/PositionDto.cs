using Newtonsoft.Json;
using UnityEngine;

namespace Lab.Part1
{
    // Los nombres JSON los define el servicio de referencia.
    public class PositionDto
    {
        [JsonProperty("posX", Required = Required.Always)] public float X { get; set; }
        [JsonProperty("posY", Required = Required.Always)] public float Y { get; set; }
        [JsonProperty("posZ", Required = Required.Always)] public float Z { get; set; }

        public PositionDto() { }

        public PositionDto(Vector3 position)
        {
            X = position.x;
            Y = position.y;
            Z = position.z;
        }

        public Vector3 ToVector3() => new Vector3(X, Y, Z);
    }
}
