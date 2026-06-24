using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum ProgrammerQuestStage
{
    NotStarted,
    Ignored,
    Accepted,
    TransmitterAcquired,
    AnalyzingTransmitter,
    DayTwoQuestAvailable,
    DataSourceNeeded,
    DataSourceAcquired,
    ComputeAccessNeeded,
    ComputeAccessAcquired,
    SignalAmplifierNeeded,
    SignalAmplifierAcquired,
    Completed,
    Rejected,
}

public enum CompetitorQuestStage
{
    Unknown,
    Tracking,
    ReachedStaffRoom,
    Overheard,
    SmokeScheduleKnown,
    GardenAccess,
}

public enum EvidenceId
{
    AdaptiveExperimentSystem,
    HiddenSystemsNeedEyeImplant,
    EngineeringTransmitter,
    CompetitorVentRoute,
    CompetitorGuardMeeting,
    GardenKey,
    StaffSmokeBreakSchedule,
    GardenConnectsWings,
    AfterLightsOutPassage,
    StaffQuietZoneNote,
    ExperimentReportsSocialTesting,
}

public enum DeductionId
{
    PredictExperimentData,
    UnofficialStaffRoutes,
    GardenIsStaffHub,
    GardenRouteToBlockC,
    StaffQuietZoneAccess,
    SocialExperimentPurpose,
    CameraBlindSpotRoute,
}

public enum DayPhase
{
    MorningFreeTime,
    ExperimentAssembly,
    Experiment,
    AfternoonFreeTime,
    LightsOut,
    EscortedToExperiment,
    EscortedToCell,
}

public static class DaySchedule
{
    public const int WakeUpMinute = 8 * 60;
    public const int ExperimentAnnouncementMinute = 12 * 60;
    public const int ExperimentDeadlineMinute = 12 * 60 + 15;
    public const int AfternoonStartMinute = 13 * 60;
    public const int LightsOutMinute = 21 * 60;

    public static bool IsExperimentCheckInWindow(int minuteOfDay)
    {
        return minuteOfDay >= ExperimentAnnouncementMinute &&
               minuteOfDay < ExperimentDeadlineMinute;
    }

    public static DayPhase PhaseForMinute(int minuteOfDay)
    {
        if (minuteOfDay >= LightsOutMinute) return DayPhase.LightsOut;
        if (IsExperimentCheckInWindow(minuteOfDay)) return DayPhase.ExperimentAssembly;
        if (minuteOfDay >= AfternoonStartMinute) return DayPhase.AfternoonFreeTime;
        return DayPhase.MorningFreeTime;
    }

    public static string FormatTime(int minuteOfDay)
    {
        int hours = Mathf.Clamp(minuteOfDay / 60, 0, 23);
        int minutes = Mathf.Clamp(minuteOfDay % 60, 0, 59);
        return $"{hours:00}:{minutes:00}";
    }
}

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
    // Отношения по шкале 0–100 (нейтраль 50). Уровни см. RelationshipLevels.
    private static readonly Dictionary<NpcId, int> relationships = new()
    {
        { NpcId.Programmer, 65 },  // стартует «приятелем» (полоса 60–79)
        { NpcId.Competitor, 50 },
    };

    // Изменение отношений в диалогах: небольшой/крупный сдвиг по шкале 0–100.
    public const int RelationshipNudgeSmall = 10;
    public const int RelationshipNudgeBig = 20;

    // Множитель для сырых дельт из экспериментов (они в «уровневых» единицах ±1..±2).
    public const int RelationshipDeltaScale = 8;

    private static readonly Dictionary<NpcId, bool> participants = new()
    {
        { NpcId.Programmer, true },
        { NpcId.Competitor, true },
    };

    private static readonly HashSet<ImplantId> implants = new();
    private static readonly HashSet<EvidenceId> evidence = new();
    private static readonly HashSet<DeductionId> deductions = new();
    private static readonly List<string> playedExperiments = new();
    private static ProgrammerQuestStage programmerQuest = ProgrammerQuestStage.NotStarted;
    private static CompetitorQuestStage competitorQuest = CompetitorQuestStage.Unknown;
    private static int minuteOfDay = DaySchedule.WakeUpMinute;
    private static DayPhase dayPhase = DayPhase.MorningFreeTime;
    private static bool pendingExperimentSummary;
    private static bool restingInBed;
    private static bool helpedCompetitorInLastExperiment;
    private static bool eyeImplantActive;
    private static bool programmerPredictionUnlocked;
    private static bool prisonReturnSpawnPending;
    private static bool hasQueuedExperiment;
    private static string queuedExperimentId;
    private static string queuedExperimentSceneName;
    private static string queuedExperimentDisplayName;
    private static ImplantId? queuedPredictedImplant;

    // Цель спасения для квестов: кого попросили вытащить в ближайшем испытании
    // и был ли он спасён по итогу. Без репутации — это самостоятельный хук.
    private static NpcId? rescueTarget;
    private static bool rescueTargetSaved;

    /// <summary>Игровой день / сложность. Растёт по мере прохождения экспериментов.</summary>
    public static int Day { get; set; } = 1;

    public static int MinuteOfDay => minuteOfDay;
    public static DayPhase DayPhase => dayPhase;
    public static bool HasPendingExperimentSummary => pendingExperimentSummary && LastResult != null;
    public static bool IsRestingInBed => restingInBed;
    public static bool EyeImplantActive => eyeImplantActive;
    public static bool HelpedCompetitorInLastExperiment => helpedCompetitorInLastExperiment;
    public static bool ProgrammerPredictionUnlocked => programmerPredictionUnlocked;
    public static bool HasQueuedExperimentPreview => hasQueuedExperiment;
    public static string QueuedExperimentDisplayName => queuedExperimentDisplayName;
    public static ImplantId? QueuedPredictedImplant => queuedPredictedImplant;
    public static bool ProgrammerRouteNeedsTechWing =>
        programmerQuest == ProgrammerQuestStage.DataSourceNeeded ||
        programmerQuest == ProgrammerQuestStage.DataSourceAcquired ||
        programmerQuest == ProgrammerQuestStage.ComputeAccessNeeded ||
        programmerQuest == ProgrammerQuestStage.ComputeAccessAcquired ||
        programmerQuest == ProgrammerQuestStage.SignalAmplifierNeeded ||
        programmerQuest == ProgrammerQuestStage.SignalAmplifierAcquired ||
        programmerQuest == ProgrammerQuestStage.Completed;

    /// <summary>Сколько заключённых-ботов бежит в гонке. Задаётся состоянием игры.</summary>
    public static int RaceParticipants { get; set; } = 4;

    /// <summary>Результат последнего завершённого эксперимента (для карты и веток).</summary>
    public static ExperimentResult LastResult { get; private set; }

    /// <summary>Уже сыгранные в этом забеге эксперименты.</summary>
    public static IReadOnlyList<string> PlayedExperiments => playedExperiments;
    public static IEnumerable<EvidenceId> DiscoveredEvidence => evidence;
    public static IEnumerable<DeductionId> DiscoveredDeductions => deductions;
    public static ProgrammerQuestStage ProgrammerQuest => programmerQuest;
    public static CompetitorQuestStage CompetitorQuest => competitorQuest;

    public static string ActiveObjective => competitorQuest switch
    {
        CompetitorQuestStage.Tracking => "Квест: проследить за заключённой, не выдавая себя.",
        CompetitorQuestStage.ReachedStaffRoom => "Квест: подслушать разговор в комнате персонала.",
        CompetitorQuestStage.SmokeScheduleKnown => "Квест: проверьте расписание перекуров персонала у сада.",
        CompetitorQuestStage.GardenAccess => "Квест: исследуйте сад и неофициальные маршруты персонала.",
        _ => programmerQuest switch
        {
            ProgrammerQuestStage.Accepted => "Квест: проникнуть в инженерную зону и найти передатчик.",
            ProgrammerQuestStage.TransmitterAcquired => "Квест: вернуть передатчик программисту.",
            ProgrammerQuestStage.DayTwoQuestAvailable => "Квест: программист разобрал часть данных передатчика.",
            ProgrammerQuestStage.DataSourceNeeded => "Квест: найти источник данных системы в блоке C.",
            ProgrammerQuestStage.DataSourceAcquired => "Квест: вернуть источник данных программисту.",
            ProgrammerQuestStage.ComputeAccessNeeded => "Квест: добыть вычислительный доступ в архиве данных.",
            ProgrammerQuestStage.ComputeAccessAcquired => "Квест: вернуть модуль доступа программисту.",
            ProgrammerQuestStage.SignalAmplifierNeeded => "Квест: добыть усилитель сигнала в релейной комнате.",
            ProgrammerQuestStage.SignalAmplifierAcquired => "Квест: вернуть усилитель сигнала программисту.",
            ProgrammerQuestStage.Completed => "Квест завершён: программист может предсказывать награду эксперимента.",
            _ => null,
        }
    };

    /// <summary>Кого активный квест просит спасти в ближайшем испытании (null — никого).</summary>
    public static NpcId? RescueTarget => rescueTarget;

    /// <summary>Был ли запрошенный квестом NPC спасён в последнем испытании.</summary>
    public static bool RescueTargetSaved => rescueTargetSaved;

    /// <summary>Квест просит спасти указанного NPC в ближайшем испытании.</summary>
    public static void RequestRescue(NpcId target)
    {
        rescueTarget = target;
        rescueTargetSaved = false;
    }

    /// <summary>Снять задачу спасения (например, после её выполнения квестом).</summary>
    public static void ClearRescue()
    {
        rescueTarget = null;
        rescueTargetSaved = false;
    }

    // Глобальное состояние тревоги (alarm). Поднимается камерами/системами охраны;
    // другие системы могут на него реагировать. Пока — самостоятельный хук.
    private static bool alarmActive;

    /// <summary>Активна ли тревога в учреждении.</summary>
    public static bool IsAlarmActive => alarmActive;

    /// <summary>Поднять тревогу. Возвращает true, если состояние изменилось.</summary>
    public static bool RaiseAlarm()
    {
        if (alarmActive) return false;
        alarmActive = true;
        return true;
    }

    /// <summary>Снять тревогу.</summary>
    public static void ClearAlarm() => alarmActive = false;

    public static string ProgrammerObjective => programmerQuest switch
    {
        ProgrammerQuestStage.Accepted => "Квест: проникнуть в инженерную зону и найти передатчик.",
        ProgrammerQuestStage.TransmitterAcquired => "Квест: вернуть передатчик программисту.",
        ProgrammerQuestStage.DayTwoQuestAvailable => "Квест: поговорить с программистом о новых данных.",
        ProgrammerQuestStage.DataSourceNeeded => "Квест: найти источник данных системы в блоке C.",
        ProgrammerQuestStage.DataSourceAcquired => "Квест: вернуть источник данных программисту.",
        ProgrammerQuestStage.ComputeAccessNeeded => "Квест: добыть вычислительный доступ в архиве данных.",
        ProgrammerQuestStage.ComputeAccessAcquired => "Квест: вернуть модуль доступа программисту.",
        ProgrammerQuestStage.SignalAmplifierNeeded => "Квест: добыть усилитель сигнала в релейной комнате.",
        ProgrammerQuestStage.SignalAmplifierAcquired => "Квест: вернуть усилитель сигнала программисту.",
        ProgrammerQuestStage.Completed => "Квест завершён: программист может предсказывать награду эксперимента.",
        _ => null,
    };

    public static string ScheduleLabel => dayPhase switch
    {
        DayPhase.MorningFreeTime => "Свободное время",
        DayPhase.ExperimentAssembly => "Сбор на эксперимент",
        DayPhase.Experiment => "Эксперимент",
        DayPhase.AfternoonFreeTime => "Свободное время после эксперимента",
        DayPhase.LightsOut => "Отбой",
        DayPhase.EscortedToExperiment => "Принудительная отправка",
        DayPhase.EscortedToCell => "Нарушение отбоя",
        _ => "Распорядок",
    };

    public static bool CanStartExperimentNow =>
        dayPhase == DayPhase.ExperimentAssembly &&
        DaySchedule.IsExperimentCheckInWindow(minuteOfDay);

    public static void AdvanceTime(int minutes)
    {
        if (minutes <= 0 ||
            dayPhase == DayPhase.Experiment ||
            dayPhase == DayPhase.EscortedToExperiment ||
            dayPhase == DayPhase.EscortedToCell)
        {
            return;
        }

        SetTime(minuteOfDay + minutes);
    }

    public static void SetTime(int newMinuteOfDay)
    {
        minuteOfDay = Mathf.Clamp(newMinuteOfDay, DaySchedule.WakeUpMinute, DaySchedule.LightsOutMinute);
        if (dayPhase != DayPhase.Experiment &&
            dayPhase != DayPhase.EscortedToExperiment &&
            dayPhase != DayPhase.EscortedToCell)
        {
            dayPhase = DaySchedule.PhaseForMinute(minuteOfDay);
        }
    }

    public static void StartNewDay()
    {
        Day++;
        minuteOfDay = DaySchedule.WakeUpMinute;
        dayPhase = DayPhase.MorningFreeTime;
        restingInBed = false;
        eyeImplantActive = false;
        LastResult = null;
        pendingExperimentSummary = false;
        helpedCompetitorInLastExperiment = false;
        ClearQueuedExperimentPreview();

        if (programmerQuest == ProgrammerQuestStage.AnalyzingTransmitter)
        {
            programmerQuest = ProgrammerQuestStage.DayTwoQuestAvailable;
        }
    }

    public static void BeginForcedExperimentEscort()
    {
        restingInBed = false;
        dayPhase = DayPhase.EscortedToExperiment;
    }

    public static void BeginForcedLightsOutEscort()
    {
        restingInBed = false;
        dayPhase = DayPhase.EscortedToCell;
    }

    public static void ArriveAtCellForLightsOut()
    {
        dayPhase = DayPhase.LightsOut;
        minuteOfDay = DaySchedule.LightsOutMinute;
        restingInBed = false;
    }

    public static void BeginRestingInBed()
    {
        if (dayPhase == DayPhase.LightsOut ||
            dayPhase == DayPhase.Experiment ||
            dayPhase == DayPhase.EscortedToExperiment ||
            dayPhase == DayPhase.EscortedToCell)
        {
            return;
        }

        restingInBed = true;
    }

    public static void StopRestingInBed()
    {
        restingInBed = false;
    }

    public static void ResetRun()
    {
        relationships[NpcId.Programmer] = 65;
        relationships[NpcId.Competitor] = 50;
        participants[NpcId.Programmer] = true;
        participants[NpcId.Competitor] = true;
        implants.Clear();
        evidence.Clear();
        deductions.Clear();
        playedExperiments.Clear();
        PrisonItems.Clear();
        programmerQuest = ProgrammerQuestStage.NotStarted;
        competitorQuest = CompetitorQuestStage.Unknown;
        minuteOfDay = DaySchedule.WakeUpMinute;
        dayPhase = DayPhase.MorningFreeTime;
        pendingExperimentSummary = false;
        restingInBed = false;
        helpedCompetitorInLastExperiment = false;
        eyeImplantActive = false;
        alarmActive = false;
        programmerPredictionUnlocked = false;
        prisonReturnSpawnPending = false;
        ClearQueuedExperimentPreview();
        LastResult = null;
        Day = 1;
        RaceParticipants = 4;
        PlayerPrefs.DeleteKey(ReactiveFeetKey);
        PlayerPrefs.Save();
    }

    public static void RestartRunInPrison()
    {
        ResetRun();
        SceneManager.LoadScene(PrisonScene);
    }

    public static void MarkExperimentSummaryShown()
    {
        pendingExperimentSummary = false;
    }

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

    /// <summary>Текущее отношение игрока к NPC по шкале 0–100 (нейтраль 50, если не задано).</summary>
    public static int RelationshipTo(NpcId npc)
    {
        return relationships.TryGetValue(npc, out int value) ? value : RelationshipLevels.Neutral;
    }

    /// <summary>Уровень отношения игрока к NPC (враг…друг).</summary>
    public static RelationshipLevel RelationshipLevelTo(NpcId npc) =>
        RelationshipLevels.For(RelationshipTo(npc));

    /// <summary>NPC, отображаемые в соц-панели журнала (по порядку).</summary>
    public static readonly NpcId[] SocialNpcs = { NpcId.Programmer, NpcId.Competitor };

    /// <summary>Отображаемое имя NPC (единый источник для UI и итогов дня).</summary>
    public static string NpcDisplayName(NpcId npc) => npc switch
    {
        NpcId.Programmer => "Программист",
        NpcId.Competitor => "Заключённая",
        _ => npc.ToString(),
    };

    /// <summary>Ресурс-портрет NPC в Resources/Sprites (как в диалогах).</summary>
    public static string NpcPortraitResource(NpcId npc) => npc switch
    {
        NpcId.Programmer => "npc_programmer",
        NpcId.Competitor => "girl",
        _ => null,
    };

    /// <summary>Изменить отношение игрока к NPC на величину delta (с зажимом в 0–100).</summary>
    public static void AdjustRelationship(NpcId npc, int delta)
    {
        int next = RelationshipTo(npc) + delta;
        relationships[npc] = Mathf.Clamp(next, RelationshipLevels.Min, RelationshipLevels.Max);
    }

    public static bool AddEvidence(EvidenceId id)
    {
        return evidence.Add(id);
    }

    public static bool HasEvidence(EvidenceId id) => evidence.Contains(id);
    public static bool HasDeduction(DeductionId id) => deductions.Contains(id);

    public static DeductionId? TryConnectEvidence(EvidenceId first, EvidenceId second)
    {
        if (first == second || !HasEvidence(first) || !HasEvidence(second)) return null;

        DeductionId? deduction = ResolveDeduction(first, second);
        if (!deduction.HasValue) return null;

        deductions.Add(deduction.Value);
        return deduction.Value;
    }

    private static DeductionId? ResolveDeduction(EvidenceId first, EvidenceId second)
    {
        bool Has(EvidenceId a, EvidenceId b)
        {
            return (first == a && second == b) || (first == b && second == a);
        }

        if (Has(EvidenceId.AdaptiveExperimentSystem, EvidenceId.EngineeringTransmitter))
        {
            return DeductionId.PredictExperimentData;
        }

        if (Has(EvidenceId.CompetitorVentRoute, EvidenceId.GardenKey) ||
            Has(EvidenceId.CompetitorGuardMeeting, EvidenceId.GardenKey))
        {
            return DeductionId.UnofficialStaffRoutes;
        }

        if (Has(EvidenceId.StaffSmokeBreakSchedule, EvidenceId.GardenConnectsWings))
        {
            return DeductionId.GardenIsStaffHub;
        }

        if (Has(EvidenceId.GardenKey, EvidenceId.GardenConnectsWings))
        {
            return DeductionId.GardenRouteToBlockC;
        }

        if (Has(EvidenceId.CompetitorGuardMeeting, EvidenceId.StaffQuietZoneNote))
        {
            return DeductionId.StaffQuietZoneAccess;
        }

        if (Has(EvidenceId.AdaptiveExperimentSystem, EvidenceId.ExperimentReportsSocialTesting))
        {
            return DeductionId.SocialExperimentPurpose;
        }

        if (Has(EvidenceId.HiddenSystemsNeedEyeImplant, EvidenceId.CompetitorVentRoute))
        {
            return DeductionId.CameraBlindSpotRoute;
        }

        return null;
    }

    public static string EvidenceTitle(EvidenceId id)
    {
        return id switch
        {
            EvidenceId.AdaptiveExperimentSystem => "Система подбирает эксперименты",
            EvidenceId.HiddenSystemsNeedEyeImplant => "Скрытые системы видны через глазной имплант",
            EvidenceId.EngineeringTransmitter => "Передатчик лежит в инженерной зоне",
            EvidenceId.CompetitorVentRoute => "Заключённая пользуется санитарным служебным маршрутом",
            EvidenceId.CompetitorGuardMeeting => "Заключённая встречается с надзирателем",
            EvidenceId.GardenKey => "Ключ от нового входа в сад",
            EvidenceId.StaffSmokeBreakSchedule => "Расписание перекуров персонала",
            EvidenceId.GardenConnectsWings => "Сад соединяет крылья тюрьмы",
            EvidenceId.AfterLightsOutPassage => "После отбоя есть ещё один проход",
            EvidenceId.StaffQuietZoneNote => "Записка упоминает тихую зону",
            EvidenceId.ExperimentReportsSocialTesting => "Отчёты описывают социальные решения",
            _ => id.ToString(),
        };
    }

    public static string EvidenceDescription(EvidenceId id)
    {
        return id switch
        {
            EvidenceId.AdaptiveExperimentSystem =>
                "Программист утверждает, что тюрьма подбирает испытания под текущий состав заключённых.",
            EvidenceId.HiddenSystemsNeedEyeImplant =>
                "Камеры, зоны сканирования и скрытые механизмы нельзя нормально увидеть без глазного импланта.",
            EvidenceId.EngineeringTransmitter =>
                "Передатчик из инженерной зоны может помочь подключиться к системе подбора экспериментов.",
            EvidenceId.CompetitorVentRoute =>
                "Заключённая прошла через закрытую санитарную комнату персонала и появилась в служебном крыле.",
            EvidenceId.CompetitorGuardMeeting =>
                "В комнате персонала заключённая встретилась с отдельным надзирателем.",
            EvidenceId.GardenKey =>
                "В подслушанном разговоре прозвучало, что старый вход в сад закрыли, а ключ от нового входа есть у заключённой.",
            EvidenceId.StaffSmokeBreakSchedule =>
                "Заключённая дала расписание, когда персонал выходит в сад курить и говорить вне формального маршрута.",
            EvidenceId.GardenConnectsWings =>
                "Сад используется персоналом как узел между первым крылом и блоком C.",
            EvidenceId.AfterLightsOutPassage =>
                "В подслушанном разговоре прозвучал проход, которым можно воспользоваться после отбоя.",
            EvidenceId.StaffQuietZoneNote =>
                "В служебной записке упомянута тихая зона, где персонал встречается вне обычного маршрута.",
            EvidenceId.ExperimentReportsSocialTesting =>
                "Отчёты показывают, что администрация фиксирует помощь, предательство и другие социальные решения.",
            _ => "",
        };
    }

    public static string DeductionTitle(DeductionId id)
    {
        return id switch
        {
            DeductionId.PredictExperimentData => "Передатчик может раскрыть данные экспериментов",
            DeductionId.UnofficialStaffRoutes => "У персонала есть неофициальные маршруты",
            DeductionId.GardenIsStaffHub => "Сад — место слухов персонала",
            DeductionId.GardenRouteToBlockC => "Через сад можно выйти к блоку C",
            DeductionId.StaffQuietZoneAccess => "Тихая зона связана с личными встречами персонала",
            DeductionId.SocialExperimentPurpose => "Эксперименты проверяют моральное поведение",
            DeductionId.CameraBlindSpotRoute => "Санитарный служебный маршрут может обходить наблюдение",
            _ => id.ToString(),
        };
    }

    public static string DeductionDescription(DeductionId id)
    {
        return id switch
        {
            DeductionId.PredictExperimentData =>
                "Если система подбирает испытания автоматически, передатчик может заранее дать тип испытания, участников или главный риск.",
            DeductionId.UnofficialStaffRoutes =>
                "Маршрут заключённой и ключ от сада указывают на скрытую сеть служебных перемещений.",
            DeductionId.GardenIsStaffHub =>
                "Если персонал регулярно выходит в сад, это место можно использовать для подслушивания слухов разных routes.",
            DeductionId.GardenRouteToBlockC =>
                "Ключ от сада и его положение между крыльями означают, что сад может открыть путь к блоку C.",
            DeductionId.StaffQuietZoneAccess =>
                "Записка и встреча с надзирателем указывают, что тихая зона используется для неофициальных договорённостей.",
            DeductionId.SocialExperimentPurpose =>
                "Отчёты и система подбора вместе показывают: администрация изучает не только выживание, но и моральный выбор.",
            DeductionId.CameraBlindSpotRoute =>
                "Если санитарный служебный путь работает, глазной имплант поможет понять, какие камеры его закрывают или пропускают.",
            _ => "",
        };
    }

    public static void StartCompetitorTracking()
    {
        if (competitorQuest == CompetitorQuestStage.Unknown)
        {
            competitorQuest = CompetitorQuestStage.Tracking;
        }
    }

    public static void MarkCompetitorReachedStaffRoom()
    {
        if (competitorQuest == CompetitorQuestStage.Tracking)
        {
            competitorQuest = CompetitorQuestStage.ReachedStaffRoom;
            AddEvidence(EvidenceId.CompetitorVentRoute);
        }
    }

    public static void MarkCompetitorConversationOverheard()
    {
        if (competitorQuest != CompetitorQuestStage.Overheard)
        {
            competitorQuest = CompetitorQuestStage.Overheard;
            AddEvidence(EvidenceId.CompetitorGuardMeeting);
            AddEvidence(EvidenceId.GardenKey);
        }
    }

    public static void MarkCompetitorSmokeScheduleGiven()
    {
        if (competitorQuest == CompetitorQuestStage.Unknown ||
            competitorQuest == CompetitorQuestStage.Tracking ||
            competitorQuest == CompetitorQuestStage.ReachedStaffRoom ||
            competitorQuest == CompetitorQuestStage.Overheard)
        {
            competitorQuest = CompetitorQuestStage.SmokeScheduleKnown;
        }

        AddEvidence(EvidenceId.StaffSmokeBreakSchedule);
        AddEvidence(EvidenceId.GardenConnectsWings);
    }

    public static void MarkGardenAccessOpened()
    {
        if (competitorQuest != CompetitorQuestStage.GardenAccess)
        {
            competitorQuest = CompetitorQuestStage.GardenAccess;
        }
        AddEvidence(EvidenceId.GardenConnectsWings);
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

    public static bool IsImplantActive(ImplantId implant)
    {
        return implant == ImplantId.EyeImplant && eyeImplantActive && HasImplant(implant);
    }

    public static bool ToggleEyeImplant()
    {
        if (!HasImplant(ImplantId.EyeImplant)) return false;
        eyeImplantActive = !eyeImplantActive;
        return true;
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

        context.RescueTarget = rescueTarget;

        return context;
    }

    /// <summary>
    /// Принять результат эксперимента: сохранить его, отметить игру сыгранной,
    /// синхронизировать импланты и отношения. Применение к карте — задача Потока A.
    /// </summary>
    public static void SubmitResult(ExperimentResult result)
    {
        LastResult = result;
        pendingExperimentSummary = result != null;
        ClearQueuedExperimentPreview();
        if (result == null) return;
        helpedCompetitorInLastExperiment = false;

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
            AdjustRelationship(delta.Key, delta.Value * RelationshipDeltaScale);
        }

        if (result.Actions.TryGetValue(NpcId.Competitor, out NpcAction competitorAction) &&
            competitorAction == NpcAction.Helped &&
            result.NpcSurvived.TryGetValue(NpcId.Competitor, out bool competitorSurvived) &&
            competitorSurvived)
        {
            helpedCompetitorInLastExperiment = true;
        }

        // Сигнал для квеста: спасён ли запрошенный NPC в этом испытании.
        if (rescueTarget.HasValue &&
            result.Actions.TryGetValue(rescueTarget.Value, out NpcAction action) &&
            action == NpcAction.Helped)
        {
            rescueTargetSaved = true;
        }
    }

    public static void EnterExperiment()
    {
        dayPhase = DayPhase.Experiment;
        SceneManager.LoadScene(ExperimentScene);
    }

    public static void EnterSelectedExperiment()
    {
        if (hasQueuedExperiment)
        {
            dayPhase = DayPhase.Experiment;
            string sceneName = queuedExperimentSceneName;
            ClearQueuedExperimentPreview();
            if (!string.IsNullOrEmpty(sceneName))
            {
                SceneManager.LoadScene(sceneName);
                return;
            }
        }

        var pool = Resources.Load<ExperimentPool>("ExperimentPool");
        if (pool != null)
        {
            var played = new HashSet<string>(PlayedExperiments);
            ExperimentDefinition def = ExperimentSelector.Select(
                pool.Experiments, Day, ParticipantCount, played, new System.Random());
            if (def != null)
            {
                EnterExperiment(def);
                return;
            }
        }

        EnterExperiment();
    }

    public static bool EnsureExperimentPreview()
    {
        if (!programmerPredictionUnlocked) return false;
        if (hasQueuedExperiment) return true;

        ExperimentDefinition def = SelectNextExperimentDefinition();
        if (def == null) return false;

        hasQueuedExperiment = true;
        queuedExperimentId = def.Id;
        queuedExperimentSceneName = def.SceneName;
        queuedExperimentDisplayName = def.DisplayName;
        queuedPredictedImplant = PredictRewardFor(def.Id);
        return true;
    }

    private static ExperimentDefinition SelectNextExperimentDefinition()
    {
        var pool = Resources.Load<ExperimentPool>("ExperimentPool");
        if (pool == null) return null;

        var played = new HashSet<string>(PlayedExperiments);
        int seed = Day * 7919 + ParticipantCount * 313 + played.Count * 37;
        return ExperimentSelector.Select(pool.Experiments, Day, ParticipantCount, played, new System.Random(seed));
    }

    private static ImplantId? PredictRewardFor(string experimentId)
    {
        return experimentId switch
        {
            "experiment.obstacle-course" => ImplantId.ReactiveFeet,
            "experiment.bluff-duel" => ImplantId.EyeImplant,
            _ => null,
        };
    }

    private static void ClearQueuedExperimentPreview()
    {
        hasQueuedExperiment = false;
        queuedExperimentId = null;
        queuedExperimentSceneName = null;
        queuedExperimentDisplayName = null;
        queuedPredictedImplant = null;
    }

    /// <summary>Загрузить сцену выбранного из пула эксперимента.</summary>
    public static void EnterExperiment(ExperimentDefinition def)
    {
        if (def == null || string.IsNullOrEmpty(def.SceneName))
        {
            EnterExperiment();
            return;
        }

        dayPhase = DayPhase.Experiment;
        SceneManager.LoadScene(def.SceneName);
    }

    public static void ReturnToPrison()
    {
        minuteOfDay = DaySchedule.AfternoonStartMinute;
        dayPhase = DayPhase.AfternoonFreeTime;
        prisonReturnSpawnPending = true;
        SceneManager.LoadScene(PrisonScene);
    }

    public static bool ConsumePrisonReturnSpawn()
    {
        bool pending = prisonReturnSpawnPending;
        prisonReturnSpawnPending = false;
        return pending;
    }

    public static int PrisonItemCount => PrisonItems.Count;

    public static bool HasPrisonItem(PrisonItemId itemId)
    {
        return PrisonItems.Contains(itemId);
    }

    public static void AddPrisonItem(PrisonItemId itemId)
    {
        PrisonItems.Add(itemId);
        if (itemId == PrisonItemId.KitchenManifest)
        {
            AddEvidence(EvidenceId.StaffQuietZoneNote);
        }
        else if (itemId == PrisonItemId.ExperimentReports)
        {
            AddEvidence(EvidenceId.ExperimentReportsSocialTesting);
        }
        else if (itemId == PrisonItemId.Transmitter)
        {
            AddEvidence(EvidenceId.EngineeringTransmitter);
        }

        if (itemId == PrisonItemId.Transmitter && programmerQuest == ProgrammerQuestStage.Accepted)
        {
            programmerQuest = ProgrammerQuestStage.TransmitterAcquired;
        }
        else if (itemId == PrisonItemId.DataSource && programmerQuest == ProgrammerQuestStage.DataSourceNeeded)
        {
            programmerQuest = ProgrammerQuestStage.DataSourceAcquired;
        }
        else if (itemId == PrisonItemId.ComputeModule && programmerQuest == ProgrammerQuestStage.ComputeAccessNeeded)
        {
            programmerQuest = ProgrammerQuestStage.ComputeAccessAcquired;
        }
        else if (itemId == PrisonItemId.SignalAmplifier && programmerQuest == ProgrammerQuestStage.SignalAmplifierNeeded)
        {
            programmerQuest = ProgrammerQuestStage.SignalAmplifierAcquired;
        }
    }

    public static void AcceptProgrammerQuest()
    {
        if (programmerQuest != ProgrammerQuestStage.NotStarted &&
            programmerQuest != ProgrammerQuestStage.Ignored)
        {
            return;
        }

        programmerQuest = HasPrisonItem(PrisonItemId.Transmitter)
            ? ProgrammerQuestStage.TransmitterAcquired
            : ProgrammerQuestStage.Accepted;
        AdjustRelationship(NpcId.Programmer, RelationshipNudgeSmall);
        AddPrisonItem(PrisonItemId.Screwdriver);
    }

    public static void IgnoreProgrammer()
    {
        if (programmerQuest != ProgrammerQuestStage.NotStarted) return;
        programmerQuest = ProgrammerQuestStage.Ignored;
        AdjustRelationship(NpcId.Programmer, -RelationshipNudgeSmall);
    }

    public static void RejectProgrammerQuest()
    {
        if (programmerQuest != ProgrammerQuestStage.NotStarted &&
            programmerQuest != ProgrammerQuestStage.Ignored)
        {
            return;
        }

        programmerQuest = ProgrammerQuestStage.Rejected;
        AdjustRelationship(NpcId.Programmer, -RelationshipNudgeBig);
    }

    public static bool CompleteProgrammerQuest()
    {
        if (programmerQuest != ProgrammerQuestStage.TransmitterAcquired) return false;
        programmerQuest = ProgrammerQuestStage.AnalyzingTransmitter;
        AdjustRelationship(NpcId.Programmer, RelationshipNudgeSmall);
        return true;
    }

    public static bool BeginProgrammerDataSourceQuest()
    {
        if (programmerQuest != ProgrammerQuestStage.DayTwoQuestAvailable) return false;
        programmerQuest = HasPrisonItem(PrisonItemId.DataSource)
            ? ProgrammerQuestStage.DataSourceAcquired
            : ProgrammerQuestStage.DataSourceNeeded;
        return true;
    }

    public static bool TurnInProgrammerDataSource()
    {
        if (programmerQuest != ProgrammerQuestStage.DataSourceAcquired) return false;
        programmerQuest = HasPrisonItem(PrisonItemId.ComputeModule)
            ? ProgrammerQuestStage.ComputeAccessAcquired
            : ProgrammerQuestStage.ComputeAccessNeeded;
        AdjustRelationship(NpcId.Programmer, 1);
        return true;
    }

    public static bool TurnInProgrammerComputeAccess()
    {
        if (programmerQuest != ProgrammerQuestStage.ComputeAccessAcquired) return false;
        programmerQuest = HasPrisonItem(PrisonItemId.SignalAmplifier)
            ? ProgrammerQuestStage.SignalAmplifierAcquired
            : ProgrammerQuestStage.SignalAmplifierNeeded;
        AdjustRelationship(NpcId.Programmer, 1);
        return true;
    }

    public static bool CompleteProgrammerRoute()
    {
        if (programmerQuest != ProgrammerQuestStage.SignalAmplifierAcquired) return false;
        programmerQuest = ProgrammerQuestStage.Completed;
        programmerPredictionUnlocked = true;
        AdjustRelationship(NpcId.Programmer, 2);
        return true;
    }

    public static void MarkProgrammerAnalyzingTransmitter()
    {
        if (programmerQuest == ProgrammerQuestStage.TransmitterAcquired)
        {
            programmerQuest = ProgrammerQuestStage.AnalyzingTransmitter;
        }
    }
}
