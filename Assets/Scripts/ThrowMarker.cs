using UnityEngine;

/// <summary>
/// Короткий визуальный «пинг» в месте приземления броска: расходящееся кольцо,
/// которое подсказывает игроку, куда ушёл звук. Чисто косметика — само себя удаляет.
/// </summary>
public class ThrowMarker : MonoBehaviour
{
    private const float Lifetime = 1f;
    private static Sprite ringSprite;

    private SpriteRenderer spriteRenderer;
    private float age;

    /// <summary>Создаёт пинг в мировой точке клетки приземления.</summary>
    public static void Spawn(GameGrid grid, Vector2Int cell)
    {
        if (grid == null) return;

        var go = new GameObject("Throw Marker");
        go.transform.position = grid.GridToWorld(cell.x, cell.y);
        go.transform.localScale = Vector3.one * grid.CellSize;
        go.AddComponent<ThrowMarker>();
    }

    private void Awake()
    {
        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = RingSprite;
        spriteRenderer.color = new Color(1f, 0.92f, 0.4f, 0.8f);
        spriteRenderer.sortingOrder = SortingLayers.Entity(transform.position.y) + 20;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / Lifetime);

        // Кольцо расходится и гаснет.
        transform.localScale = Vector3.one * Mathf.Lerp(0.4f, 1.6f, t);
        Color c = spriteRenderer.color;
        c.a = Mathf.Lerp(0.8f, 0f, t);
        spriteRenderer.color = c;

        if (age >= Lifetime) Destroy(gameObject);
    }

    /// <summary>Кольцо (тонкая окружность), сгенерированное один раз и переиспользуемое.</summary>
    private static Sprite RingSprite
    {
        get
        {
            if (ringSprite != null) return ringSprite;

            const int size = 64;
            var texture = new Texture2D(size, size);
            var pixels = new Color[size * size];
            Vector2 center = Vector2.one * (size - 1) * 0.5f;
            float outer = size * 0.46f;
            float inner = size * 0.34f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center);
                    pixels[y * size + x] = d <= outer && d >= inner ? Color.white : Color.clear;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            texture.filterMode = FilterMode.Bilinear;

            ringSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return ringSprite;
        }
    }
}
