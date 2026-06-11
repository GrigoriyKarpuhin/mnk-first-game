using UnityEngine;

/// <summary>
/// Характеристики бота-заключённого — общий фундамент для всех экспериментов.
/// Эксперименты описывают свои формальные правила и применяют их с поправкой на эти
/// характеристики и на общий анлак (<see cref="Luck"/>). Значения в диапазоне 0..1.
/// </summary>
public struct BotTraits
{
    /// <summary>Общая компетентность: влияет на успех проверок (объезд, раунды теста).</summary>
    public float Skill;

    /// <summary>Склонность мешать игроку (подрезать, толкать).</summary>
    public float Aggression;

    /// <summary>Осторожность у опасностей: выше — реже падает/ошибается.</summary>
    public float Caution;

    public BotTraits(float skill, float aggression, float caution)
    {
        Skill = skill;
        Aggression = aggression;
        Caution = caution;
    }

    /// <summary>Случайный набор характеристик для статиста.</summary>
    public static BotTraits Randomized()
        => new(Random.Range(0.55f, 0.95f), Random.Range(0f, 1f), Random.Range(0.45f, 0.9f));
}

/// <summary>
/// Общий «процент невезения». Любая проверка навыка проходит через него, поэтому даже
/// умелый бот иногда лажает, а неумелый иногда вывозит. Один глобальный рычаг.
/// </summary>
public static class Luck
{
    /// <summary>Базовая доля неудачи, срезающая успех у всех (0..1).</summary>
    public static float UnluckChance = 0.12f;

    /// <summary>Проверка навыка с учётом общего анлака. true — успех.</summary>
    public static bool Roll(float skill)
        => Random.value < Mathf.Clamp01(skill) * (1f - Mathf.Clamp01(UnluckChance));
}
