using UnityEngine;

/// <summary>
/// «Настороженность» охранника после того, как он потерял игрока и вернулся на маршрут:
/// какое-то время он ловит игрока быстрее обычного. Чистая логика (таймер → множитель),
/// покрыта юнит-тестами (<see cref="CautionStateTests"/>).
/// </summary>
public static class CautionState
{
    /// <summary>
    /// Множитель прироста тревоги: <paramref name="multiplier"/> пока таймер &gt; 0,
    /// иначе 1. Применяется только к НАБОРУ тревоги (не к спаду).
    /// </summary>
    public static float GainMultiplier(float cautionTimer, float multiplier)
    {
        return cautionTimer > 0f ? Mathf.Max(1f, multiplier) : 1f;
    }
}
