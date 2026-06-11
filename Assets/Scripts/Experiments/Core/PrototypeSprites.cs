using UnityEngine;

/// <summary>
/// Простые процедурные спрайты (квадрат и круг) для временной графики
/// прототипов экспериментов. Общий источник для всех экспериментов, чтобы
/// не дублировать генерацию текстур.
/// </summary>
internal static class PrototypeSprites
{
    private static Sprite square;
    private static Sprite circle;

    public static Sprite Square => square != null ? square : square = BuildSquare();
    public static Sprite Circle => circle != null ? circle : circle = BuildCircle();

    private static Sprite BuildSquare()
    {
        Texture2D texture = new(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
    }

    private static Sprite BuildCircle()
    {
        const int size = 32;
        Texture2D texture = new(size, size);
        Color[] pixels = new Color[size * size];
        Vector2 center = Vector2.one * (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                pixels[y * size + x] = Vector2.Distance(new Vector2(x, y), center) <= size * 0.45f
                    ? Color.white
                    : Color.clear;
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
