using UnityEngine;

/// <summary>
/// Ручная закрытая зона: прямоугольник клеток, который добавляется к базовым
/// закрытым зонам (<see cref="GameGrid.IsRestrictedCell"/>). Позиция маркера —
/// левый-нижний угол; <see cref="widthCells"/>/<see cref="heightCells"/> задают размер.
/// Внутри закрытой зоны действуют те же правила (охрана расстреливает при конвое и т.п.).
/// </summary>
public class RestrictedZoneMarker : MonoBehaviour
{
    [Tooltip("Ширина зоны в клетках (вправо от позиции маркера).")]
    [Min(1)] public int widthCells = 4;

    [Tooltip("Высота зоны в клетках (вверх от позиции маркера).")]
    [Min(1)] public int heightCells = 3;

    /// <summary>Прямоугольник клеток зоны (левый-нижний угол = клетка под маркером).</summary>
    public RectInt CellRect(GameGrid grid)
    {
        Vector2Int origin = grid.WorldToGrid(transform.position);
        return new RectInt(origin.x, origin.y, Mathf.Max(1, widthCells), Mathf.Max(1, heightCells));
    }

    private void OnDrawGizmos()
    {
        GameGrid grid = LevelMarkerGizmos.Grid();
        if (grid == null)
        {
            Gizmos.color = new Color(0.95f, 0.2f, 0.15f, 0.5f);
            Gizmos.DrawWireCube(transform.position, new Vector3(widthCells, heightCells, 0.1f));
            return;
        }

        Vector2Int o = grid.WorldToGrid(transform.position);
        int w = Mathf.Max(1, widthCells);
        int h = Mathf.Max(1, heightCells);

        // Заливка по клеткам.
        Gizmos.color = new Color(0.95f, 0.2f, 0.15f, 0.22f);
        for (int dx = 0; dx < w; dx++)
        {
            for (int dy = 0; dy < h; dy++)
            {
                Gizmos.DrawCube(grid.GridToWorld(o.x + dx, o.y + dy), new Vector3(0.96f, 0.96f, 0.05f));
            }
        }

        // Контур всей зоны.
        Vector3 min = grid.GridToWorld(o.x, o.y);
        Vector3 max = grid.GridToWorld(o.x + w - 1, o.y + h - 1);
        Vector3 center = (min + max) * 0.5f;
        Gizmos.color = new Color(1f, 0.3f, 0.22f, 0.85f);
        Gizmos.DrawWireCube(center, new Vector3(Mathf.Abs(max.x - min.x) + 1f, Mathf.Abs(max.y - min.y) + 1f, 0.06f));
    }
}
