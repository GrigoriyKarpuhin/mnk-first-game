using UnityEngine;

/// <summary>
/// Источник обзора на сетке: охрана с «фонариком» или камера наблюдения.
/// Единый контракт, по которому <see cref="VisionConeRenderer"/> рисует зону видимости,
/// а сами источники проверяют, виден ли игрок.
/// </summary>
public interface IVisionSource
{
    GameGrid Grid { get; }
    Vector2Int GridPosition { get; }
    Vector2Int Facing { get; }
    int VisionRange { get; }

    /// <summary>Активен ли обзор (false — например, охранник оглушён: конус прячется).</summary>
    bool VisionActive { get; }

    /// <summary>Видна ли клетка из этого источника (дальность, угол конуса и стены).</summary>
    bool CanSeeCell(Vector2Int cell);
}

/// <summary>
/// Геометрия обзора на сетке.
/// Охрана использует широкий "фонарик", камеры — более узкий сектор с мёртвыми
/// зонами у крепления и по бокам.
/// </summary>
public static class VisionMath
{
    /// <summary>Видна ли цель охраннику из origin при заданном facing/дальности.</summary>
    public static bool CanSeeCell(GameGrid grid, Vector2Int origin, Vector2Int facing, int range, Vector2Int target)
    {
        if (grid == null) return false;

        Vector2Int delta = target - origin;
        int forwardDistance = delta.x * facing.x + delta.y * facing.y;
        if (forwardDistance <= 0 || forwardDistance > range) return false;

        Vector2Int side = new Vector2Int(-facing.y, facing.x);
        int sideDistance = Mathf.Abs(delta.x * side.x + delta.y * side.y);
        if (sideDistance > forwardDistance) return false;

        return HasClearLineOfSight(grid, origin, target);
    }

    /// <summary>
    /// Видна ли цель стационарной камере. Камера не должна работать как широкий
    /// конус охранника: под креплением есть мёртвая зона, а боковой охват
    /// появляется только на дистанции.
    /// </summary>
    public static bool CanCameraSeeCell(GameGrid grid, Vector2Int origin, Vector2Int facing, int range, Vector2Int target) =>
        CanCameraSeeCell(grid, origin, facing, range, target, 0f);

    public static bool CanCameraSeeCell(
        GameGrid grid,
        Vector2Int origin,
        Vector2Int facing,
        int range,
        Vector2Int target,
        float scanOffsetCells)
    {
        if (grid == null) return false;

        Vector2Int delta = target - origin;
        int forwardDistance = delta.x * facing.x + delta.y * facing.y;

        // Клетка прямо под камерой остаётся слепой: игрок может проскочить под
        // креплением, если добрался до стены/угла.
        if (forwardDistance <= 1 || forwardDistance > range) return false;

        Vector2Int side = new Vector2Int(-facing.y, facing.x);
        int sideDistance = delta.x * side.x + delta.y * side.y;

        // Луч не "телепортируется" вбок целиком, а поворачивается от камеры:
        // рядом с креплением центр почти неподвижен, на дальнем конце смещается сильнее.
        float scanT = Mathf.InverseLerp(1f, Mathf.Max(2, range), forwardDistance);
        float beamCenter = scanOffsetCells * scanT;

        // Узкий сектор с плавным расширением. Это не широкий конус охранника:
        // около камеры боков почти нет, дальше луч слегка расходится.
        float halfWidth = Mathf.Lerp(0.35f, 0.95f, scanT);
        if (Mathf.Abs(sideDistance - beamCenter) > halfWidth) return false;

        return HasClearLineOfSight(grid, origin, target);
    }

    /// <summary>Алгоритм Брезенхэма: нет ли стен между origin и target.</summary>
    public static bool HasClearLineOfSight(GameGrid grid, Vector2Int origin, Vector2Int target)
    {
        int x = origin.x;
        int y = origin.y;
        int dx = Mathf.Abs(target.x - x);
        int dy = Mathf.Abs(target.y - y);
        int stepX = x < target.x ? 1 : -1;
        int stepY = y < target.y ? 1 : -1;
        int error = dx - dy;

        while (x != target.x || y != target.y)
        {
            int doubledError = error * 2;
            if (doubledError > -dy)
            {
                error -= dy;
                x += stepX;
            }

            if (doubledError < dx)
            {
                error += dx;
                y += stepY;
            }

            if ((x != target.x || y != target.y) && grid.BlocksVision(x, y)) return false;
        }

        return true;
    }
}
