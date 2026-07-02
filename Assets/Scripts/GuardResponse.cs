using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Выбор охранников для локального вызова на подмогу: ближайшие к месту инцидента,
/// но не больше заданного числа и не дальше радиуса. Чистая логика (только клетки и
/// флаги пригодности), поэтому её используют и камеры, и сами охранники, и юнит-тесты
/// (<see cref="GuardResponseTests"/>). Держит тревогу локальной и читаемой: будим
/// немногих ближайших, а не всю тюрьму.
/// </summary>
public static class GuardResponse
{
    /// <summary>
    /// Индексы ближайших к <paramref name="target"/> пригодных охранников, отсортированные
    /// по возрастанию дистанции, не более <paramref name="count"/> и в пределах
    /// <paramref name="maxDistance"/> клеток (по квадрату дистанции на сетке).
    /// </summary>
    public static List<int> SelectNearest(
        IReadOnlyList<Vector2Int> cells,
        IReadOnlyList<bool> eligible,
        Vector2Int target,
        int count,
        int maxDistance)
    {
        var result = new List<int>();
        if (cells == null || count <= 0) return result;

        int maxSq = maxDistance * maxDistance;

        var candidates = new List<int>();
        for (int i = 0; i < cells.Count; i++)
        {
            if (eligible != null && (i >= eligible.Count || !eligible[i])) continue;
            if (DistanceSq(cells[i], target) > maxSq) continue;
            candidates.Add(i);
        }

        candidates.Sort((a, b) =>
            DistanceSq(cells[a], target).CompareTo(DistanceSq(cells[b], target)));

        int take = Mathf.Min(count, candidates.Count);
        for (int i = 0; i < take; i++) result.Add(candidates[i]);
        return result;
    }

    public static int DistanceSq(Vector2Int a, Vector2Int b)
    {
        int dx = a.x - b.x;
        int dy = a.y - b.y;
        return dx * dx + dy * dy;
    }
}
