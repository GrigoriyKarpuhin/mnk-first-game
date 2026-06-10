using UnityEngine;

/// <summary>
/// Декор переднего плана (люстры, арки, и т.д.)
/// Рисуется ПОВЕРХ игрока и стен.
/// </summary>
public class ForegroundDecor : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private Color decorColor = new Color(0.8f, 0.7f, 0.3f);
    [SerializeField] private Vector2 size = new Vector2(2f, 1f);

    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        CreateVisual();
        UpdateSortingOrder();
    }

    private void CreateVisual()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }

        spriteRenderer.sprite = CreateDecorSprite();
        spriteRenderer.color = decorColor;
        transform.localScale = new Vector3(size.x, size.y, 1);
    }

    private Sprite CreateDecorSprite()
    {
        int width = 64;
        int height = 32;
        var texture = new Texture2D(width, height);
        var pixels = new Color[width * height];

        // Простая форма люстры/балки
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Основная полоса
                bool isMainBar = y >= height / 3 && y <= height * 2 / 3;
                // Украшения по краям
                bool isDecor = (x < 8 || x >= width - 8) && y < height / 2;
                
                pixels[y * width + x] = (isMainBar || isDecor) ? Color.white : Color.clear;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        texture.filterMode = FilterMode.Point;

        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 32);
    }

    private void UpdateSortingOrder()
    {
        if (spriteRenderer != null)
        {
            // Foreground - всегда поверх игрока и стен
            spriteRenderer.sortingOrder = SortingLayers.Foreground(transform.position.y);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(decorColor.r, decorColor.g, decorColor.b, 0.5f);
        Gizmos.DrawCube(transform.position, new Vector3(size.x, size.y, 0.1f));
    }
#endif
}
