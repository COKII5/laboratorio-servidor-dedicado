using System;
using UnityEngine;

namespace Lab.Common
{
    public class InfoPanel
    {
        const float Margin = 10f;
        const float CharactersPerLine = 40f;
        const float LinesPerScreen = 40f;
        const float MinFontSize = 11f;
        const float MaxFontSize = 20f;

        GUIStyle style;

        public bool Visible { get; private set; } = true;

        public void ToggleVisible() => Visible = !Visible;

        public void Draw(Func<string> buildText)
        {
            if (!Visible)
            {
                GUI.Label(new Rect(Margin, Margin, 300f, 24f), "H: mostrar panel");
                return;
            }

            style ??= new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                richText = true,
                wordWrap = true,
                padding = new RectOffset(10, 10, 8, 8),
            };
            // La letra escala con la ventana para que se lea con las dos instancias lado a lado.
            style.fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height / LinesPerScreen, MinFontSize, MaxFontSize));

            float width = Mathf.Min(style.fontSize * CharactersPerLine, Screen.width - 2 * Margin);
            GUILayout.BeginArea(new Rect(Margin, Margin, width, Screen.height - 2 * Margin));
            GUILayout.Box(buildText(), style);
            GUILayout.EndArea();
        }
    }
}
