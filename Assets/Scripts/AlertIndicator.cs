using UnityEngine;

/// <summary>
/// Общий индикатор тревоги над источником обзора (MGS-стиль): шкала заполнения и
/// глиф «?»/«!». Вынесён из <see cref="GuardPatrol"/>, чтобы охрана и
/// <see cref="SurveillanceCamera"/> рисовали одинаковую обратную связь, а правки
/// визуала жили в одном месте. Методы вызываются только из OnGUI.
/// </summary>
public static class AlertIndicator
{
    private static readonly Color WarnColor = new(1f, 0.85f, 0.1f);   // подозрение (?)
    private static readonly Color AlarmColor = new(1f, 0.2f, 0.15f);  // полная тревога (!)

    /// <summary>Полоска тревоги: жёлтая при подозрении → красная при полной тревоге.</summary>
    public static void DrawMeter(Camera cam, Vector3 headWorldPos, float level, bool fullAlert)
    {
        if (cam == null) return;
        Vector3 screen = cam.WorldToScreenPoint(headWorldPos);
        if (screen.z < 0f) return;

        const float width = 46f;
        const float height = 7f;
        float x = screen.x - width * 0.5f;
        float y = Screen.height - screen.y;

        Color prev = GUI.color;

        // Рамка/фон.
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(x - 1f, y - 1f, width + 2f, height + 2f), PixelTexture);

        // Заполнение: жёлтый при подозрении → красный при полной тревоге.
        GUI.color = fullAlert ? AlarmColor : Color.Lerp(WarnColor, AlarmColor, level);
        GUI.DrawTexture(new Rect(x, y, width * Mathf.Clamp01(level), height), PixelTexture);

        GUI.color = prev;
    }

    /// <summary>Глиф «?» (подозрение) или «!» (полная тревога) над источником.</summary>
    public static void DrawGlyph(Camera cam, Vector3 glyphWorldPos, string glyph)
    {
        if (cam == null || string.IsNullOrEmpty(glyph)) return;
        Vector3 screen = cam.WorldToScreenPoint(glyphWorldPos);
        if (screen.z < 0f) return;

        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 28,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        style.normal.textColor = glyph == "!" ? AlarmColor : WarnColor;

        Rect rect = new Rect(screen.x - 16f, Screen.height - screen.y - 40f, 32f, 32f);
        GUI.Label(rect, glyph, style);
    }

    private static Texture2D pixelTexture;
    private static Texture2D PixelTexture
    {
        get
        {
            if (pixelTexture == null)
            {
                pixelTexture = new Texture2D(1, 1);
                pixelTexture.SetPixel(0, 0, Color.white);
                pixelTexture.Apply();
            }
            return pixelTexture;
        }
    }
}
