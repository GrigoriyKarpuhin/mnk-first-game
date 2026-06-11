using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Пул экспериментов как ассет. Хранит список определений, из которого
/// <see cref="ExperimentSelector"/> выбирает игру. Сам выбор не делает —
/// только отдаёт данные, чтобы логику выбора можно было покрыть тестами.
/// </summary>
[CreateAssetMenu(menuName = "Game/Experiment Pool", fileName = "ExperimentPool")]
public class ExperimentPool : ScriptableObject
{
    [SerializeField] private ExperimentDefinition[] experiments = new ExperimentDefinition[0];

    public IReadOnlyList<ExperimentDefinition> Experiments => experiments;

    /// <summary>Определения, доступные при данном дне/сложности и числе участников.</summary>
    public List<ExperimentDefinition> Available(int day, int participantCount)
    {
        var result = new List<ExperimentDefinition>();
        foreach (ExperimentDefinition def in experiments)
        {
            if (def != null && def.IsAvailable(day, participantCount))
            {
                result.Add(def);
            }
        }
        return result;
    }

    /// <summary>Найти определение по идентификатору или null.</summary>
    public ExperimentDefinition Find(string id)
    {
        foreach (ExperimentDefinition def in experiments)
        {
            if (def != null && def.Id == id) return def;
        }
        return null;
    }
}
