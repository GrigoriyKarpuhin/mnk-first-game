using System;
using UnityEngine;

/// <summary>
/// Где держать переносимое тело относительно игрока: на клетке позади (откуда игрок
/// только что шагнул). Если позади непроходимо — на клетке самого игрока. Чистая
/// логика, покрыта юнит-тестами (<see cref="CarryMathTests"/>).
/// </summary>
public static class CarryMath
{
    public static Vector2Int TrailCell(Func<int, int, bool> isWalkable, Vector2Int playerCell, Vector2Int facing)
    {
        Vector2Int behind = playerCell - ThrowMath.Cardinal(facing);
        if (isWalkable != null && isWalkable(behind.x, behind.y)) return behind;
        return playerCell;
    }
}
