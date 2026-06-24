using UnityEngine;

/// <summary>
/// Накопитель «тревоги» охранника (0..1). Отделён от <see cref="GuardPatrol"/>,
/// как <see cref="VisionMath"/> отделён от источников обзора, — чтобы лестницу
/// обнаружения можно было покрыть юнит-тестами без сцены.
///
/// Чем ближе игрок в конусе (меньше normalizedDistance) — тем быстрее растёт уровень.
/// Когда игрока не видно — уровень медленно спадает. Это даёт MGS-подобный
/// переход «заметил → подозрение (?) → полная тревога (!)» вместо мгновенной погони.
/// </summary>
public sealed class AwarenessMeter
{
    /// <summary>Текущий уровень тревоги, 0 (спокоен) .. 1 (полная тревога).</summary>
    public float Level { get; private set; }

    /// <summary>
    /// Обновить уровень за кадр.
    /// </summary>
    /// <param name="visible">Виден ли игрок прямо сейчас.</param>
    /// <param name="normalizedDistance">0 — вплотную, 1 — у края конуса.</param>
    /// <param name="dt">Шаг времени (Time.deltaTime).</param>
    /// <param name="gainNear">Скорость роста, когда игрок вплотную.</param>
    /// <param name="gainFar">Скорость роста, когда игрок у края конуса.</param>
    /// <param name="decay">Скорость спада, когда игрока не видно.</param>
    public void Tick(bool visible, float normalizedDistance, float dt,
                     float gainNear, float gainFar, float decay)
    {
        if (visible)
        {
            float gain = Mathf.Lerp(gainNear, gainFar, Mathf.Clamp01(normalizedDistance));
            Level += gain * dt;
        }
        else
        {
            Level -= decay * dt;
        }

        Level = Mathf.Clamp01(Level);
    }

    /// <summary>Полностью сбросить тревогу (вернулся на патруль).</summary>
    public void Reset() => Level = 0f;

    /// <summary>Выставить максимум (форсированная тревога: камера/расписание).</summary>
    public void SetMax() => Level = 1f;
}
