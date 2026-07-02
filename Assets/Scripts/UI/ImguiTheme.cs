using UnityEngine;

/// <summary>
/// Тема для оставшегося IMGUI (экраны экспериментов). uGUI-часть игры уже на
/// <see cref="UITheme"/>/<see cref="UIKit"/>, но мини-игры экспериментов рисуются
/// через OnGUI — этот хелпер даёт им тот же терминальный шрифт, палитру и «хром»
/// (тёмная панель + зелёная рамка), чтобы всё выглядело единообразно.
///
/// В начале каждого OnGUI вызывать <see cref="Apply"/> — он ставит шрифт
/// терминала на GUI.skin, и все стили ниже (и стандартные Label/Box) начинают
/// рисоваться им.
/// </summary>
public static class ImguiTheme
{
    private static GUIStyle title, header, body, hint, centered, button;
    private static Texture2D pixel;

    private static Texture2D Pixel
    {
        get
        {
            if (pixel == null)
            {
                pixel = new Texture2D(1, 1);
                pixel.SetPixel(0, 0, Color.white);
                pixel.Apply();
            }
            return pixel;
        }
    }

    /// <summary>Ставит терминальный шрифт на GUI.skin. Звать в начале OnGUI.</summary>
    public static void Apply()
    {
        Font f = UIKit.Font;
        if (GUI.skin.font != f) GUI.skin.font = f;
    }

    public static GUIStyle Title => title ??= Make(UITheme.TypeTitle, UITheme.TextBright, FontStyle.Bold);
    public static GUIStyle Header => header ??= Make(UITheme.TypeHeader, UITheme.Accent, FontStyle.Bold);
    public static GUIStyle Body => body ??= Make(UITheme.TypeBody, UITheme.TextPrimary, FontStyle.Normal, wrap: true);
    public static GUIStyle Hint => hint ??= Make(UITheme.TypeLabel, UITheme.TextStencil, FontStyle.Normal, wrap: true);
    public static GUIStyle Centered => centered ??= Make(UITheme.TypeBody, UITheme.TextPrimary, FontStyle.Bold, TextAnchor.MiddleCenter, true);

    public static GUIStyle Button
    {
        get
        {
            if (button == null)
            {
                button = new GUIStyle(GUI.skin.button)
                {
                    font = UIKit.Font,
                    fontSize = UITheme.TypeBody,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true,
                };
                button.normal.textColor = UITheme.TextPrimary;
                button.hover.textColor = UITheme.TextBright;
                button.active.textColor = UITheme.OnAccent;
            }
            return button;
        }
    }

    private static GUIStyle Make(int size, Color color, FontStyle style, TextAnchor align = TextAnchor.UpperLeft, bool wrap = false)
    {
        var s = new GUIStyle(GUI.skin.label)
        {
            font = UIKit.Font,
            fontSize = size,
            fontStyle = style,
            alignment = align,
            wordWrap = wrap,
        };
        s.normal.textColor = color;
        return s;
    }

    /// <summary>Заливка прямоугольника цветом (через 1×1 текстуру).</summary>
    public static void Fill(Rect r, Color color)
    {
        Color prev = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(r, Pixel);
        GUI.color = prev;
    }

    /// <summary>Рамка по периметру прямоугольника.</summary>
    public static void Border(Rect r, Color color, float thickness = 2f)
    {
        Fill(new Rect(r.x, r.y, r.width, thickness), color);
        Fill(new Rect(r.x, r.yMax - thickness, r.width, thickness), color);
        Fill(new Rect(r.x, r.y, thickness, r.height), color);
        Fill(new Rect(r.xMax - thickness, r.y, thickness, r.height), color);
    }

    /// <summary>Терминальная панель: тёмная заливка + зелёная рамка.</summary>
    public static void Panel(Rect r)
    {
        Fill(r, UITheme.Surface);
        Border(r, UITheme.Border);
    }

    /// <summary>Панель на весь экран-затемнение (для интро/итогов).</summary>
    public static void Screen(Rect r)
    {
        Fill(r, UITheme.Panel);
        Border(r, UITheme.Border);
    }
}
