using System;
using UnityEngine;

/// <summary>
/// Геометрия броска отвлекающего предмета: куда «долетит» звук по направлению взгляда.
/// Чистая логика без Unity-объектов, поэтому покрыта юнит-тестами (<see cref="ThrowMathTests"/>).
/// </summary>
public static class ThrowMath
{
    /// <summary>
    /// Клетка приземления броска. Идём из <paramref name="from"/> по направлению
    /// <paramref name="facing"/> до <paramref name="range"/> клеток и возвращаем
    /// ПОСЛЕДНЮЮ проходимую клетку перед стеной/укрытием/дверью. Если первый шаг
    /// упирается в непроходимое — возвращаем саму <paramref name="from"/>.
    /// Нулевое направление нормализуется вправо.
    /// </summary>
    public static Vector2Int LandingCell(Func<int, int, bool> isWalkable, Vector2Int from, Vector2Int facing, int range)
    {
        Vector2Int dir = Cardinal(facing);
        Vector2Int landing = from;
        for (int i = 1; i <= range; i++)
        {
            Vector2Int cell = from + dir * i;
            if (isWalkable == null || !isWalkable(cell.x, cell.y)) break;
            landing = cell;
        }
        return landing;
    }

    /// <summary>Приводит направление к одной из 4 сторон; нулевое — вправо (как реактивные стопы).</summary>
    public static Vector2Int Cardinal(Vector2Int facing)
    {
        if (facing.x == 0 && facing.y == 0) return Vector2Int.right;
        if (Mathf.Abs(facing.x) >= Mathf.Abs(facing.y))
            return new Vector2Int(facing.x > 0 ? 1 : -1, 0);
        return new Vector2Int(0, facing.y > 0 ? 1 : -1);
    }
}
