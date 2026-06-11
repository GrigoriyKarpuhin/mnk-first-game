using System.Collections.Generic;

/// <summary>
/// Логика случайного выбора эксперимента из пула. Чистый C# без зависимостей
/// от Unity-ассетов и времени, чтобы покрыть EditMode-тестами.
///
/// Правила (предложение для согласования команды, см. ROADMAP.md, открытый вопрос #2):
/// - выбираются только доступные по дню/сложности и числу участников игры
///   (порядок открытия категорий задаётся через MinDay в определениях);
/// - по умолчанию эксперимент не повторяется в рамках одного забега, пока пул
///   не исчерпан (открытый вопрос #3);
/// - выбор детерминирован при фиксированном <see cref="System.Random"/> (seed).
/// </summary>
public static class ExperimentSelector
{
    /// <summary>
    /// Выбрать эксперимент. Возвращает null, если ни одна игра не доступна.
    /// </summary>
    /// <param name="pool">Все определения пула.</param>
    /// <param name="day">Игровой день / сложность.</param>
    /// <param name="participantCount">Сколько участников доступно.</param>
    /// <param name="playedThisRun">Идентификаторы уже сыгранных в забеге игр.</param>
    /// <param name="rng">Источник случайности (инъекция для тестов).</param>
    /// <param name="avoidRepeats">Избегать повтора, пока есть несыгранные.</param>
    public static ExperimentDefinition Select(
        IReadOnlyList<ExperimentDefinition> pool,
        int day,
        int participantCount,
        ICollection<string> playedThisRun,
        System.Random rng,
        bool avoidRepeats = true)
    {
        if (pool == null || rng == null) return null;

        var available = new List<ExperimentDefinition>();
        foreach (ExperimentDefinition def in pool)
        {
            if (def != null && def.IsAvailable(day, participantCount))
            {
                available.Add(def);
            }
        }

        if (available.Count == 0) return null;

        List<ExperimentDefinition> candidates = available;
        if (avoidRepeats && playedThisRun != null && playedThisRun.Count > 0)
        {
            var fresh = new List<ExperimentDefinition>();
            foreach (ExperimentDefinition def in available)
            {
                if (!playedThisRun.Contains(def.Id)) fresh.Add(def);
            }

            // Если все доступные уже сыграны — пул исчерпан, разрешаем повтор.
            if (fresh.Count > 0) candidates = fresh;
        }

        return candidates[rng.Next(candidates.Count)];
    }
}
