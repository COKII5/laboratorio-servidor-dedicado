using System;
using UnityEngine;

namespace Lab.Part1
{
    /// <summary>Cuerpo JSON del servicio de referencia: {"posX":f, "posY":f, "posZ":f}.</summary>
    [Serializable]
    public struct PositionDto
    {
        public float posX;
        public float posY;
        public float posZ;

        public PositionDto(Vector3 position)
        {
            posX = position.x;
            posY = position.y;
            posZ = position.z;
        }

        public Vector3 ToVector3() => new Vector3(posX, posY, posZ);
    }
}
