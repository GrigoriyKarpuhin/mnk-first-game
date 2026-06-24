using UnityEngine;

/// <summary>
/// Общий хелпер для маркеров ручного левел-дизайна. Маркеры — это пустые
/// GameObject'ы с компонентами <see cref="GuardSpawnMarker"/>, <see cref="WaypointMarker"/>,
/// <see cref="CoverObstacleMarker"/>, <see cref="HideSpotMarker"/>, расставленные в сцене.
/// <see cref="GameGrid"/> читает их при старте и строит охрану/препятствия/укрытия,
/// привязывая позиции к клеткам грида.
/// </summary>
public static class LevelMarkerGizmos
{
    private static GameGrid cachedGrid;

    /// <summary>Привязать позицию к центру клетки грида (если грид найден в сцене).</summary>
    public static Vector3 Snap(Vector3 world)
    {
        GameGrid grid = Grid();
        return grid != null ? grid.SnapToCell(world) : world;
    }

    public static GameGrid Grid()
    {
        if (cachedGrid == null) cachedGrid = Object.FindFirstObjectByType<GameGrid>();
        return cachedGrid;
    }
}
