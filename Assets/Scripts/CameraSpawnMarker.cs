using UnityEngine;

/// <summary>Сторона, в которую смотрит маркер (камера). Маппится на Vector2Int.</summary>
public enum FacingDir { Up, Down, Left, Right }

/// <summary>
/// Точка спавна камеры наблюдения. <see cref="GameGrid"/> читает маркеры при старте
/// и создаёт камеры по их позиции/направлению/реакции (ручной левел-дизайн).
/// </summary>
public class CameraSpawnMarker : MonoBehaviour
{
    [Tooltip("Имя камеры в иерархии.")]
    public string cameraName = "Камера";

    [Tooltip("Куда смотрит камера (монтируется на стене, смотрит внутрь комнаты).")]
    public FacingDir facing = FacingDir.Down;

    [Tooltip("Дальность обзора в клетках.")]
    [Min(1)] public int range = 6;

    [Tooltip("Метка зоны (для логики/отладки).")]
    public string zone = "";

    [Tooltip("Реакция при обнаружении: None — только смотрит; SummonGuards — зовёт охрану; Alarm — общая тревога.")]
    public CameraResponse response = CameraResponse.None;

    public Vector2Int FacingVector => facing switch
    {
        FacingDir.Up => Vector2Int.up,
        FacingDir.Down => Vector2Int.down,
        FacingDir.Left => Vector2Int.left,
        _ => Vector2Int.right,
    };

    private void OnDrawGizmos()
    {
        GameGrid grid = LevelMarkerGizmos.Grid();
        Vector3 p = LevelMarkerGizmos.Snap(transform.position);

        // Корпус камеры.
        Gizmos.color = new Color(0.2f, 0.9f, 0.9f, 0.9f);
        Gizmos.DrawCube(p, new Vector3(0.6f, 0.6f, 0.1f));

        if (grid == null) return;

        Vector2Int origin = grid.WorldToGrid(transform.position);
        Vector2Int f = FacingVector;

        // Реальная зона обзора: те же правила, что и в игре (узкий сектор камеры,
        // мёртвая зона под креплением, перекрытие стенами/укрытиями).
        Gizmos.color = new Color(0.2f, 0.9f, 0.9f, 0.13f);
        for (int dy = -range; dy <= range; dy++)
        {
            for (int dx = -range; dx <= range; dx++)
            {
                Vector2Int cell = new Vector2Int(origin.x + dx, origin.y + dy);
                if (cell == origin) continue;
                if (!VisionMath.CanCameraSeeCell(grid, origin, f, range, cell)) continue;
                Gizmos.DrawCube(grid.GridToWorld(cell.x, cell.y), new Vector3(0.9f, 0.9f, 0.05f));
            }
        }
    }
}
