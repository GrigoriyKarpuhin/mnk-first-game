using UnityEngine;

/// <summary>
/// Маленькая тень под персонажем. Она привязывает спрайт к полу и показывает
/// точку, где персонаж реально стоит на клетке.
/// </summary>
public class CharacterGroundShadow : MonoBehaviour
{
    private const float DefaultWidth = 0.56f;
    private const float DefaultHeight = 0.22f;
    private const float DefaultYOffset = 0.06f;
    private const int ShadowOrderOffset = -8;

    private static Sprite circleSprite;

    [SerializeField] private float worldWidth = DefaultWidth;
    [SerializeField] private float worldHeight = DefaultHeight;
    [SerializeField] private float worldYOffset = DefaultYOffset;
    [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, 0.34f);

    private SpriteRenderer ownerRenderer;
    private SpriteRenderer shadowRenderer;

    public static CharacterGroundShadow Attach(GameObject target)
    {
        if (target == null) return null;
        var shadow = target.GetComponent<CharacterGroundShadow>();
        if (shadow == null) shadow = target.AddComponent<CharacterGroundShadow>();
        shadow.EnsureShadow();
        return shadow;
    }

    private void Awake()
    {
        EnsureShadow();
    }

    private void LateUpdate()
    {
        EnsureShadow();
        UpdateShadowTransform();
        shadowRenderer.sortingOrder = ShadowSortingOrder();
    }

    private void EnsureShadow()
    {
        if (shadowRenderer != null) return;

        ownerRenderer = GetComponent<SpriteRenderer>();

        Transform existing = transform.Find("Ground Shadow");
        GameObject shadowObject = existing != null ? existing.gameObject : new GameObject("Ground Shadow");
        shadowObject.transform.SetParent(transform, false);

        shadowRenderer = shadowObject.GetComponent<SpriteRenderer>();
        if (shadowRenderer == null) shadowRenderer = shadowObject.AddComponent<SpriteRenderer>();
        shadowRenderer.sprite = CircleSprite;
        shadowRenderer.color = shadowColor;
        shadowRenderer.sortingOrder = ShadowSortingOrder();

        UpdateShadowTransform();
    }

    private void UpdateShadowTransform()
    {
        if (shadowRenderer == null) return;

        Transform shadowTransform = shadowRenderer.transform;
        Vector3 scale = transform.lossyScale;
        float scaleX = Mathf.Max(0.0001f, Mathf.Abs(scale.x));
        float scaleY = Mathf.Max(0.0001f, Mathf.Abs(scale.y));

        shadowTransform.localPosition = new Vector3(0f, worldYOffset / scaleY, 0f);
        shadowTransform.localRotation = Quaternion.identity;
        shadowTransform.localScale = new Vector3(worldWidth / scaleX, worldHeight / scaleY, 1f);
    }

    private int ShadowSortingOrder()
    {
        if (ownerRenderer == null) ownerRenderer = GetComponent<SpriteRenderer>();
        return ownerRenderer != null
            ? ownerRenderer.sortingOrder - 1
            : SortingLayers.Entity(transform.position.y) + ShadowOrderOffset;
    }

    private static Sprite CircleSprite
    {
        get
        {
            if (circleSprite != null) return circleSprite;

            const int size = 64;
            var texture = new Texture2D(size, size);
            var pixels = new Color[size * size];
            Vector2 center = Vector2.one * (size - 1) * 0.5f;
            float radius = size * 0.46f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    pixels[y * size + x] = Vector2.Distance(new Vector2(x, y), center) <= radius
                        ? Color.white
                        : Color.clear;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            texture.filterMode = FilterMode.Point;

            circleSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return circleSprite;
        }
    }
}
