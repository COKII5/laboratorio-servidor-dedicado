using System.Linq;
using UnityEngine;

namespace Lab.Part2
{
    public class Track : MonoBehaviour
    {
        [SerializeField, Tooltip("En orden de recorrido. El 0 es la meta.")]
        Transform[] checkpoints = new Transform[0];
        [SerializeField, Min(0.1f), Tooltip("Distancia horizontal a la que se considera pasado un checkpoint.")]
        float passRadius = 2f;
        [SerializeField] Color idleColor = new Color(0.55f, 0.55f, 0.55f);
        [SerializeField] Color nextColor = new Color(1f, 0.85f, 0.25f);
        [SerializeField, Min(0f)] float flashSeconds = 0.5f;

        Renderer[] renderers;
        float[] flashUntil;
        Color[] flashColors;
        int highlighted = -1;

        public int Count => checkpoints.Length;
        public bool IsValid(int index) => index >= 0 && index < checkpoints.Length;
        public Vector3 Position(int index) => checkpoints[index].position;

        public bool Contains(int index, Vector3 point)
        {
            Vector3 offset = point - checkpoints[index].position;
            offset.y = 0f;
            return offset.sqrMagnitude <= passRadius * passRadius;
        }

        public void Highlight(int index) => highlighted = index;

        public void Flash(int index, Color color)
        {
            flashUntil[index] = Time.time + flashSeconds;
            flashColors[index] = color;
        }

        void Awake()
        {
            renderers = checkpoints.Select(checkpoint => checkpoint.GetComponent<Renderer>()).ToArray();
            flashUntil = new float[checkpoints.Length];
            flashColors = new Color[checkpoints.Length];
        }

        void Update()
        {
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].material.color = CurrentColor(i);
        }

        Color CurrentColor(int index)
        {
            if (Time.time < flashUntil[index]) return flashColors[index];
            if (index == highlighted) return nextColor;
            return idleColor;
        }
    }
}
