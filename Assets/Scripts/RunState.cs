using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Общее состояние забега и точка обмена данными между картой и экспериментами.
/// Конкретный эксперимент не хранит глобальное состояние сам (см. ROADMAP.md):
/// он берёт <see cref="BuildContext"/> на входе и кладёт результат в
/// <see cref="LastResult"/> через <see cref="SubmitResult"/>.
/// </summary>
public static class RunState
{
    public const string PrisonScene = "SampleScene";
    public const string ExperimentScene = "Experiment01";

    private const string ReactiveFeetKey = "run.reactive-feet";
    private static readonly HashSet<PrisonItemId> PrisonItems = new HashSet<PrisonItemId>();

    // In-memory состояние забега. Для прототипа межзабеговое сохранение не требуется,
    // кроме реактивных стоп (PlayerPrefs), как и в первом эксперименте.
    private static readonly Dictionary<NpcId, int> relationships = new()
    {
        { NpcId.Programmer, 1 },
        { NpcId.Competitor, 0 },
    };

    private static readonly Dictionary<NpcId, bool> participants = new()
    {
        { NpcId.Programmer, true },
        { NpcId.Competitor, true },
    };

    private static readonly HashSet<ImplantId> implants = new();
    private static readonly List<string> playedExperiments = new();

    /// <summary>Игровой день / сложность. Растёт по мере прохождения экспериментов.</summary>
    public static int Day { get; set; } = 1;

    /// <summary>Сколько заключённых-ботов бежит в гонке. Задаётся состоянием игры.</summary>
    public static int RaceParticipants { get; set; } = 4;

    /// <summary>Результат последнего завершённого эксперимента (для карты и веток).</summary>
    public static ExperimentResult LastResult { get; private set; }

    /// <summary>Уже сыгранные в этом забеге эксперименты.</summary>
    public static IReadOnlyList<string> PlayedExperiments => playedExperiments;

    /// <summary>Сколько участников доступно эксперименту: живые NPC плюс игрок.</summary>
    public static int ParticipantCount
    {
        get
        {
            int count = 1;
            foreach (bool alive in participants.Values)
            {
                if (alive) count++;
            }
            return count;
        }
    }

    public static bool HasReactiveFeet
    {
        get => PlayerPrefs.GetInt(ReactiveFeetKey, 0) == 1;
        set
        {
            PlayerPrefs.SetInt(ReactiveFeetKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    /// <summary>Текущее отношение игрока к NPC (0, если не задано).</summary>
    public static int RelationshipTo(NpcId npc)
    {
        return relationships.TryGetValue(npc, out int value) ? value : 0;
    }

    /// <summary>Изменить отношение игрока к NPC на величину delta.</summary>
    public static void AdjustRelationship(NpcId npc, int delta)
    {
        relationships[npc] = RelationshipTo(npc) + delta;
    }

    /// <summary>Выдать игроку имплант.</summary>
    public static void AddImplant(ImplantId implant)
    {
        if (implant == ImplantId.ReactiveFeet) HasReactiveFeet = true;
        else implants.Add(implant);
    }

    /// <summary>Установлен ли имплант (реактивные стопы читаются из PlayerPrefs).</summary>
    public static bool HasImplant(ImplantId implant)
    {
        if (implant == ImplantId.ReactiveFeet) return HasReactiveFeet;
        return implants.Contains(implant);
    }

    /// <summary>Собрать входной контекст для эксперимента из текущего состояния забега.</summary>
    public static ExperimentContext BuildContext(string experimentId)
    {
        var context = new ExperimentContext
        {
            ExperimentId = experimentId,
            Day = Day,
        };

        foreach (KeyValuePair<NpcId, bool> pair in participants)
        {
            context.Participants[pair.Key] = pair.Value;
            context.Relationships[pair.Key] = RelationshipTo(pair.Key);
        }

        if (HasReactiveFeet) context.Implants.Add(ImplantId.ReactiveFeet);
        foreach (ImplantId implant in implants) context.Implants.Add(implant);

        return context;
    }

    /// <summary>
    /// Принять результат эксперимента: сохранить его, отметить игру сыгранной,
    /// синхронизировать импланты и отношения. Применение к карте — задача Потока A.
    /// </summary>
    public static void SubmitResult(ExperimentResult result)
    {
        LastResult = result;
        if (result == null) return;

        if (!string.IsNullOrEmpty(result.ExperimentId) &&
            !playedExperiments.Contains(result.ExperimentId))
        {
            playedExperiments.Add(result.ExperimentId);
        }

        if (result.ImplantAccepted && result.OfferedImplant.HasValue)
        {
            AddImplant(result.OfferedImplant.Value);
        }

        foreach (KeyValuePair<NpcId, bool> survived in result.NpcSurvived)
        {
            participants[survived.Key] = survived.Value;
        }

        foreach (KeyValuePair<NpcId, int> delta in result.RelationshipDeltas)
        {
            AdjustRelationship(delta.Key, delta.Value);
        }
    }

    public static void EnterExperiment()
    {
        SceneManager.LoadScene(ExperimentScene);
    }

    /// <summary>Загрузить сцену выбранного из пула эксперимента.</summary>
    public static void EnterExperiment(ExperimentDefinition def)
    {
        if (def == null || string.IsNullOrEmpty(def.SceneName))
        {
            EnterExperiment();
            return;
        }

        SceneManager.LoadScene(def.SceneName);
    }

    public static void ReturnToPrison()
    {
        SceneManager.LoadScene(PrisonScene);
    }

    public static int PrisonItemCount => PrisonItems.Count;

    public static bool HasPrisonItem(PrisonItemId itemId)
    {
        return PrisonItems.Contains(itemId);
    }

    public static void AddPrisonItem(PrisonItemId itemId)
    {
        PrisonItems.Add(itemId);
    }
}
