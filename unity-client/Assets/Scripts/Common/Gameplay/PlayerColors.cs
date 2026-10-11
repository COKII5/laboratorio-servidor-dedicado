using UnityEngine;

namespace Lab.Common
{
    public static class PlayerColors
    {
        static readonly Color First = new Color(0.20f, 0.55f, 0.95f);
        static readonly Color Second = new Color(0.95f, 0.45f, 0.15f);

        // Se decide por orden de identificador para que cada jugador tenga el mismo color en las dos instancias.
        public static Color For(string playerId, string otherPlayerId) =>
            string.CompareOrdinal(playerId, otherPlayerId) <= 0 ? First : Second;

        public static void Paint(Component target, Color color)
        {
            if (target != null && target.TryGetComponent(out Renderer renderer))
                renderer.material.color = color;
        }
    }
}
