using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Рисует зону видимости источника обзора (<see cref="IVisionSource"/>) прямо в мире —
/// «фонарик» охраны или зону камеры. Строит полупрозрачный меш из видимых клеток,
/// переиспользуя ту же геометрию, что и детект игрока (VisionMath / CanSeeCell).
///
/// Заменяет визуализацию конусов, которая раньше жила в миникарте (PrisonMinimap).
/// </summary>
public sealed class VisionConeRenderer : MonoBehaviour
{
    private IVisionSource source;
    private GameObject owner;
    private GameGrid grid;
    private MeshRenderer meshRenderer;
    private Mesh mesh;
    private Color color = new Color(1f, 0.62f, 0.18f, 0.22f);

    private readonly List<Vector3> vertices = new();
    private readonly List<int> triangles = new();
    private readonly List<Color> colors = new();

    private Vector2Int lastPosition = new(int.MinValue, int.MinValue);
    private Vector2Int lastFacing;
    private bool lastActive;
    private Color lastColor;
    private bool dirty = true;

    /// <summary>Создать рендер конуса для источника обзора (как отдельный объект).</summary>
    public static VisionConeRenderer Attach(IVisionSource visionSource, GameObject ownerObject, Color coneColor)
    {
        var go = new GameObject("Vision Cone");
        var renderer = go.AddComponent<VisionConeRenderer>();
        renderer.color = coneColor;
        renderer.Initialize(visionSource, ownerObject);
        return renderer;
    }

    private void Initialize(IVisionSource visionSource, GameObject ownerObject)
    {
        source = visionSource;
        owner = ownerObject;
        grid = visionSource.Grid;

        mesh = new Mesh { name = "VisionCone" };
        mesh.MarkDynamic();

        var filter = gameObject.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;

        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.sortingOrder = SortingLayers.VisionCone;
    }

    /// <summary>Сменить цвет конуса (например, янтарный → красный при тревоге).</summary>
    public void SetColor(Color newColor)
    {
        color = newColor;
        dirty = true;
    }

    private void LateUpdate()
    {
        // Владелец (охранник/камера) уничтожен — убираем за собой конус.
        if (owner == null)
        {
            Destroy(gameObject);
            return;
        }

        if (source == null || grid == null) return;

        bool active = source.VisionActive;
        meshRenderer.enabled = active;
        if (!active) return;

        if (!dirty &&
            source.GridPosition == lastPosition &&
            source.Facing == lastFacing &&
            active == lastActive &&
            color == lastColor)
        {
            return;
        }

        Rebuild();

        lastPosition = source.GridPosition;
        lastFacing = source.Facing;
        lastActive = active;
        lastColor = color;
        dirty = false;
    }

    private void Rebuild()
    {
        vertices.Clear();
        triangles.Clear();
        colors.Clear();

        Vector2Int origin = source.GridPosition;
        int range = source.VisionRange;
        float half = grid.CellSize * 0.5f;

        for (int dx = -range; dx <= range; dx++)
        {
            for (int dy = -range; dy <= range; dy++)
            {
                var cell = new Vector2Int(origin.x + dx, origin.y + dy);
                if (!source.CanSeeCell(cell)) continue;
                // Рисуем конус только по проходимому полу — не залезаем на стены/двери.
                if (!grid.IsWalkable(cell.x, cell.y)) continue;

                Vector3 center = grid.GridToWorld(cell.x, cell.y);
                center.z = 0f;
                AddQuad(center, half);
            }
        }

        mesh.Clear();
        if (vertices.Count == 0) return;

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetColors(colors);
    }

    private void AddQuad(Vector3 center, float half)
    {
        int baseIndex = vertices.Count;

        vertices.Add(center + new Vector3(-half, -half, 0f));
        vertices.Add(center + new Vector3(-half, half, 0f));
        vertices.Add(center + new Vector3(half, half, 0f));
        vertices.Add(center + new Vector3(half, -half, 0f));

        colors.Add(color);
        colors.Add(color);
        colors.Add(color);
        colors.Add(color);

        triangles.Add(baseIndex);
        triangles.Add(baseIndex + 1);
        triangles.Add(baseIndex + 2);
        triangles.Add(baseIndex);
        triangles.Add(baseIndex + 2);
        triangles.Add(baseIndex + 3);
    }

    private void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
        if (meshRenderer != null && meshRenderer.sharedMaterial != null) Destroy(meshRenderer.sharedMaterial);
    }
}
