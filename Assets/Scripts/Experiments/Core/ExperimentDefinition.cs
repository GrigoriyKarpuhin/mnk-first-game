using UnityEngine;

/// <summary>
/// Описание одного эксперимента в пуле. Данные, а не логика: какую сцену грузить,
/// к какой категории относится, при какой сложности и числе участников доступен.
/// Конкретные правила игры живут в сцене/скрипте эксперимента.
/// </summary>
[CreateAssetMenu(menuName = "Game/Experiment Definition", fileName = "Experiment")]
public class ExperimentDefinition : ScriptableObject
{
    [Header("Идентификация")]
    [Tooltip("Стабильный идентификатор. Используется в контракте и списке сыгранных.")]
    [SerializeField] private string id = "experiment.id";

    [Tooltip("Отображаемое название для игрока.")]
    [SerializeField] private string displayName = "Эксперимент";

    [SerializeField] private ExperimentCategory category = ExperimentCategory.Solo;

    [Tooltip("Имя сцены для загрузки. Должна быть в Build Settings.")]
    [SerializeField] private string sceneName = "";

    [Header("Доступность")]
    [Tooltip("Минимальный игровой день/сложность, с которого игра входит в пул.")]
    [SerializeField] private int minDay = 1;

    [Tooltip("Максимальный день/сложность включительно. 0 или меньше — без верхней границы.")]
    [SerializeField] private int maxDay = 0;

    [Tooltip("Сколько участников нужно (для отбора по числу выживших NPC).")]
    [SerializeField] private int minParticipants = 1;
    [SerializeField] private int maxParticipants = 4;

    [Tooltip("Снять, если эксперимент пока только на бумаге и не имеет играбельной сцены.")]
    [SerializeField] private bool implemented = true;

    public string Id => id;
    public string DisplayName => displayName;
    public ExperimentCategory Category => category;
    public string SceneName => sceneName;
    public int MinDay => minDay;
    public int MaxDay => maxDay;
    public int MinParticipants => minParticipants;
    public int MaxParticipants => maxParticipants;
    public bool Implemented => implemented;

    /// <summary>Доступен ли эксперимент при данном дне/сложности и числе участников.</summary>
    public bool IsAvailable(int day, int participantCount)
    {
        if (!implemented) return false;
        if (day < minDay) return false;
        if (maxDay > 0 && day > maxDay) return false;
        if (participantCount < minParticipants) return false;
        if (participantCount > maxParticipants) return false;
        return true;
    }
}
