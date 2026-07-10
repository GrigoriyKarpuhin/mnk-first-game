using UnityEngine;

/// <summary>
/// Короткий воздушный след удара: дуга появляется у корпуса и быстро гаснет.
/// Эффект процедурный, чтобы не зависеть от отдельных VFX-спрайтов.
/// </summary>
public sealed class AttackTrace : MonoBehaviour
{
    private const int PointCount = 7;
    private const float Lifetime = 0.22f;
    private const float Grow = 1.08f;

    private static Material lineMaterial;

    private readonly LineRenderer[] lines = new LineRenderer[3];
    private readonly Color[] startColors = new Color[3];
    private readonly Color[] endColors = new Color[3];
    private Transform owner;
    private float age;

    public static void Spawn(Transform owner, Vector2Int direction, float cellSize, int sortingOrder)
    {
        if (direction == Vector2Int.zero) direction = Vector2Int.right;

        var go = new GameObject("Attack Air Trace");
        if (owner != null)
        {
            go.transform.position = owner.position;
        }
        var trace = go.AddComponent<AttackTrace>();
        trace.owner = owner;
        trace.Build(direction, Mathf.Max(0.1f, cellSize), sortingOrder);
    }

    private void Build(Vector2Int directionCell, float cellSize, int sortingOrder)
    {
        Vector2 forward = new Vector2(directionCell.x, directionCell.y).normalized;
        Vector2 side = new Vector2(-forward.y, forward.x);

        for (int i = 0; i < lines.Length; i++)
        {
            var child = new GameObject($"Trace {i + 1}");
            child.transform.SetParent(transform, false);

            LineRenderer line = child.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = PointCount;
            line.material = LineMaterial;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.sortingOrder = sortingOrder + i;
            line.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0.05f + i * 0.015f),
                new Keyframe(0.55f, 0.11f - i * 0.018f),
                new Keyframe(1f, 0f));

            Color start = i == 0
                ? new Color(1f, 0.96f, 0.72f, 0.86f)
                : new Color(0.62f, 0.95f, 1f, 0.36f - i * 0.08f);
            Color end = i == 0
                ? new Color(1f, 0.56f, 0.18f, 0.05f)
                : new Color(0.35f, 0.72f, 1f, 0.02f);
            line.startColor = start;
            line.endColor = end;
            startColors[i] = start;
            endColors[i] = end;

            FillArc(line, forward, side, cellSize, i);
            lines[i] = line;
        }
    }

    private static void FillArc(LineRenderer line, Vector2 forward, Vector2 side, float cellSize, int echo)
    {
        bool vertical = Mathf.Abs(forward.y) > Mathf.Abs(forward.x);
        bool upward = forward.y > 0.5f;
        bool downward = forward.y < -0.5f;

        float halfSpan = vertical ? 0.42f : 0.34f;
        // forwardBase — вылет якоря вдоль направления удара. Для бока держит
        // кулак у самой руки (замер по player_side_fight_1: ~0.66 юнита от
        // центра), иначе след повисает у корпуса, не долетая до кисти — в
        // отличие от down/up, halfSpan бока перпендикулярен вылету и не
        // маскирует недолёт.
        float forwardBase = upward ? 0.36f : downward ? 0.16f : 0.66f;
        float forwardBulge = upward ? 0.18f : downward ? 0.13f : 0.15f;
        // Итоговая anchor.y (height + forward.y*forwardBase) должна совпадать с
        // реальной высотой кулака над ступнями в спрайтах удара. Для up — кулак
        // теперь заведён над головой (кадр удара вверх переделан), реальная
        // высота ~1.27 юнита при CharacterScale=1.55, а не ~0.97 как у down/side.
        float height = upward ? 0.91f : downward ? 1.13f : 0.97f;
        // Кулак удара вверх занесён над плечом не по центру силуэта (замер по
        // player_up_fight_1: ~-0.19 юнита от центра), а forward для up/down не
        // даёт горизонтальной компоненты — сдвигаем якорь отдельно.
        float sideBias = upward ? -0.19f : 0f;
        Vector2 anchor = Vector2.up * height * cellSize
            + forward * forwardBase * cellSize
            - forward * echo * 0.035f * cellSize
            + side * (echo - 1) * 0.025f * cellSize
            + Vector2.right * sideBias * cellSize;

        for (int p = 0; p < PointCount; p++)
        {
            float t = PointCount == 1 ? 0f : p / (PointCount - 1f);
            float signed = Mathf.Lerp(-1f, 1f, t);
            float crown = Mathf.Sin(t * Mathf.PI);
            Vector2 local = anchor
                + side * signed * halfSpan * cellSize
                + forward * crown * forwardBulge * cellSize;
            line.SetPosition(p, new Vector3(local.x, local.y, 0f));
        }
    }

    private void Update()
    {
        if (owner != null)
        {
            transform.position = owner.position;
        }

        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / Lifetime);
        transform.localScale = Vector3.one * Mathf.Lerp(1f, Grow, t);

        for (int i = 0; i < lines.Length; i++)
        {
            LineRenderer line = lines[i];
            if (line == null) continue;
            Color start = startColors[i];
            Color end = endColors[i];
            float fade = 1f - t;
            start.a *= fade;
            end.a *= fade;
            line.startColor = start;
            line.endColor = end;
        }

        if (age >= Lifetime) Destroy(gameObject);
    }

    private static Material LineMaterial
    {
        get
        {
            if (lineMaterial == null)
            {
                lineMaterial = new Material(Shader.Find("Sprites/Default"));
            }

            return lineMaterial;
        }
    }
}
