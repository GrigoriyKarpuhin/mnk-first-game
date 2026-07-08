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
    GardenMeetingScheduled,
    GardenMeetingComplete,
    SmokeScheduleKnown,
    GardenAccess,
    GuardPostLead,
    ArchiveKeyAcquired,
    EscapeArchiveFound,
    EscapeFolderGivenToRaquel,
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
    CookServiceRoute,
    ScientistGardenRumor,
    EscapedPrisonerRumor,
    GuardPostIdentityScan,
    ArchiveKeys,
    EscapeeArchiveFolder,
    GuardEscapePostAnalysis,
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
    PastEscapeWeakness,
    SoloEscapeRoute,
    RaquelEscapePlanning,
}

public readonly struct EvidenceConnectionRecord
{
    public EvidenceConnectionRecord(EvidenceId first, EvidenceId second, DeductionId? deduction)
    {
        First = first;
        Second = second;
        Deduction = deduction;
    }

    public EvidenceId First { get; }
    public EvidenceId Second { get; }
    public DeductionId? Deduction { get; }
    public bool HasDeduction => Deduction.HasValue;

    public bool Contains(EvidenceId id) => First == id || Second == id;
    public EvidenceId Other(EvidenceId id) => First == id ? Second : First;
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

public enum ActiveQuestId
{
    Identity,
    Raquel,
    Programmer,
}

public enum CraftMaterialId
{
    Chemicals,
    QualityChemicals,
    ScrapMetal,
    QualityScrapMetal,
    Microchips,
    QualityMicrochips,
}

public enum CraftedItemId
{
    None,
    Medkit,
    NoiseBeacon,
    SmokeBomb,
    EmpGrenade,
    HologramGrenade,
}

public readonly struct CraftCost
{
    public CraftCost(CraftMaterialId material, int amount)
    {
        Material = material;
        Amount = amount;
    }

    public CraftMaterialId Material { get; }
    public int Amount { get; }
}

public readonly struct ReactiveSecurityCamera
{
    public readonly Vector2Int Cell;
    public readonly Vector2Int Facing;

    public ReactiveSecurityCamera(Vector2Int cell, Vector2Int facing)
    {
        Cell = cell;
        Facing = facing;
    }
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
    private const float MaskingDurationSeconds = 30f;
    private const float MaskingCooldownSeconds = 300f;
    private static readonly HashSet<PrisonItemId> PrisonItems = new HashSet<PrisonItemId>();

    // Исследованные комнаты для карты (id из RoomGraph). Копится ЧЕРЕЗ дни: игрок должен
    // видеть, где уже был. Очищается только в ResetRun (не в StartNewDay). id стабилен,
    // потому что GameGrid строит граф один раз при закрытых дверях (см. GameGrid.RoomGraph).
    private static readonly HashSet<int> visitedRoomIds = new HashSet<int>();

    // In-memory состояние забега. Для прототипа межзабеговое сохранение не требуется,
    // кроме реактивных стоп (PlayerPrefs), как и в первом эксперименте.
    // Отношения по шкале 0–100 (нейтраль 50). Уровни см. RelationshipLevels.
    private static readonly Dictionary<NpcId, int> relationships = new()
    {
        { NpcId.Programmer, 65 },  // стартует «приятелем» (полоса 60–79)
        { NpcId.Competitor, 50 },
        { NpcId.MedicMechanic, 50 },
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
        { NpcId.MedicMechanic, true },
    };

    private static readonly HashSet<ImplantId> implants = new();
    private static readonly Dictionary<CraftMaterialId, int> craftMaterials = new();
    private static readonly Dictionary<CraftedItemId, int> craftedItems = new();
    private static readonly Dictionary<ImplantId, int> implantUpgradeLevels = new();
    private static readonly HashSet<string> lootedResourceCaches = new();
    private static readonly HashSet<EvidenceId> evidence = new();
    private static readonly HashSet<DeductionId> deductions = new();
    private static readonly Dictionary<string, EvidenceConnectionRecord> evidenceConnectionAttempts = new();
    private static readonly List<string> playedExperiments = new();
    private static ProgrammerQuestStage programmerQuest = ProgrammerQuestStage.NotStarted;
    private static CompetitorQuestStage competitorQuest = CompetitorQuestStage.Unknown;
    private static ActiveQuestId activeQuest = ActiveQuestId.Identity;
    private static int minuteOfDay = DaySchedule.WakeUpMinute;
    private static DayPhase dayPhase = DayPhase.MorningFreeTime;
    private static bool pendingExperimentSummary;
    private static bool restingInBed;
    private static bool helpedCompetitorInLastExperiment;
    private static bool metMedicMechanic;
    private static bool medicMechanicFirstCraftRewarded;
    private static bool medicMechanicFirstUpgradeRewarded;
    private static bool eyeImplantActive;
    private static float maskingActiveUntil;
    private static float maskingCooldownUntil;
    private static bool programmerPredictionUnlocked;
    private static bool prisonReturnSpawnPending;
    private static bool hasQueuedExperiment;
    private static string queuedExperimentId;
    private static string queuedExperimentSceneName;
    private static string queuedExperimentDisplayName;
    private static ImplantId? queuedPredictedImplant;
    private static readonly List<Vector2Int> pendingSecurityCameraIncidents = new();
    private static readonly List<ReactiveSecurityCamera> activeReactiveSecurityCameras = new();

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
    public static bool MaskingImplantActive =>
        HasImplant(ImplantId.MaskingImplant) && Time.time < maskingActiveUntil;
    public static float MaskingImplantRemaining =>
        MaskingImplantActive ? Mathf.Max(0f, maskingActiveUntil - Time.time) : 0f;
    public static float MaskingImplantCooldownRemaining =>
        HasImplant(ImplantId.MaskingImplant) ? Mathf.Max(0f, maskingCooldownUntil - Time.time) : 0f;
    public static bool HelpedCompetitorInLastExperiment => helpedCompetitorInLastExperiment;
    public static bool ProgrammerPredictionUnlocked => programmerPredictionUnlocked;
    public static bool HasQueuedExperimentPreview => hasQueuedExperiment;
    public static string QueuedExperimentDisplayName => queuedExperimentDisplayName;
    public static ImplantId? QueuedPredictedImplant => queuedPredictedImplant;
    public static IReadOnlyList<ReactiveSecurityCamera> ActiveReactiveSecurityCameras =>
        activeReactiveSecurityCameras;
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
    public static IEnumerable<EvidenceConnectionRecord> EvidenceConnectionAttempts => evidenceConnectionAttempts.Values;
    public static ProgrammerQuestStage ProgrammerQuest => programmerQuest;
    public static CompetitorQuestStage CompetitorQuest => competitorQuest;
    public static ActiveQuestId ActiveQuest => activeQuest;

    public static void SetActiveQuest(ActiveQuestId quest)
    {
        activeQuest = quest;
    }

    public static string ActiveObjective => activeQuest switch
    {
        ActiveQuestId.Raquel => CompetitorObjective,
        ActiveQuestId.Programmer => ProgrammerObjective,
        _ => IdentityObjective,
    };

    public static string IdentityObjective =>
        "Квест: понять, кто вы и почему оказались в этой тюрьме.";

    public static string CompetitorObjective => competitorQuest switch
    {
        CompetitorQuestStage.Tracking => "Квест: проследить за Ракель, не выдавая себя.",
        CompetitorQuestStage.ReachedStaffRoom => "Квест: подслушать разговор в комнате персонала.",
        CompetitorQuestStage.Overheard => "Квест: помогите Ракель в эксперименте, чтобы она пошла на контакт.",
        CompetitorQuestStage.GardenMeetingScheduled => "Квест: встретиться с Ракель у входа в сад в 19:00.",
        CompetitorQuestStage.GardenMeetingComplete => "Квест: используйте расписание Ракель и подслушайте персонал в саду.",
        CompetitorQuestStage.SmokeScheduleKnown => "Квест: проверьте расписание персонала у сада.",
        CompetitorQuestStage.GardenAccess => "Квест: исследуйте сад и неофициальные маршруты персонала.",
        CompetitorQuestStage.GuardPostLead => "Квест: проникнуть на пост охраны под маскировкой и добыть доступ к архиву.",
        CompetitorQuestStage.ArchiveKeyAcquired => "Квест: найти в архиве папку о сбежавшем заключённом.",
        CompetitorQuestStage.EscapeArchiveFound => "Квест: решить, отдать ли папку Ракель или использовать её самому.",
        CompetitorQuestStage.EscapeFolderGivenToRaquel => "Квест: Ракель готова обсуждать план побега. Продолжение ветки требует новых зацепок.",
        _ => "Квест: узнать, почему Ракель исчезает из общей зоны.",
    };

    public static bool TryGetActiveQuestTarget(out Vector2Int cell, out string label)
    {
        return activeQuest switch
        {
            ActiveQuestId.Raquel => TryGetRaquelQuestTarget(out cell, out label),
            ActiveQuestId.Programmer => TryGetProgrammerQuestTarget(out cell, out label),
            _ => NoQuestTarget(out cell, out label),
        };
    }

    private static bool TryGetRaquelQuestTarget(out Vector2Int cell, out string label)
    {
        switch (competitorQuest)
        {
            case CompetitorQuestStage.Unknown:
            case CompetitorQuestStage.Tracking:
                cell = BlockCPlayableLayout.CompetitorCell;
                label = "Ракель";
                return true;
            case CompetitorQuestStage.ReachedStaffRoom:
                cell = BlockCPlayableLayout.StaffRoomDoor;
                label = "Комната персонала";
                return true;
            case CompetitorQuestStage.Overheard:
                cell = BlockCPlayableLayout.ExperimentAssembly;
                label = "Вход в эксперименты";
                return true;
            case CompetitorQuestStage.GardenMeetingScheduled:
                cell = BlockCPlayableLayout.GardenDoor;
                label = "Вход в сад";
                return true;
            case CompetitorQuestStage.GardenMeetingComplete:
            case CompetitorQuestStage.SmokeScheduleKnown:
            case CompetitorQuestStage.GardenAccess:
                cell = BlockCPlayableLayout.GardenSmokeSpot;
                label = "Сад";
                return true;
            case CompetitorQuestStage.GuardPostLead:
                cell = BlockCPlayableLayout.GuardPostScanner;
                label = "Пост охраны";
                return true;
            case CompetitorQuestStage.ArchiveKeyAcquired:
                cell = BlockCPlayableLayout.EscapeArchiveFolder;
                label = "Архив";
                return true;
            case CompetitorQuestStage.EscapeArchiveFound:
                cell = BlockCPlayableLayout.CompetitorSpawn;
                label = "Ракель";
                return true;
            default:
                return NoQuestTarget(out cell, out label);
        }
    }

    private static bool TryGetProgrammerQuestTarget(out Vector2Int cell, out string label)
    {
        switch (programmerQuest)
        {
            case ProgrammerQuestStage.Accepted:
                cell = BlockCPlayableLayout.Transmitter;
                label = "Инженерная зона";
                return true;
            case ProgrammerQuestStage.TransmitterAcquired:
            case ProgrammerQuestStage.DayTwoQuestAvailable:
            case ProgrammerQuestStage.DataSourceAcquired:
            case ProgrammerQuestStage.ComputeAccessAcquired:
            case ProgrammerQuestStage.SignalAmplifierAcquired:
                cell = BlockCPlayableLayout.ProgrammerSpawn;
                label = "Программист";
                return true;
            case ProgrammerQuestStage.DataSourceNeeded:
                cell = BlockCPlayableLayout.DataSourceObjective;
                label = "Технологическое крыло";
                return true;
            case ProgrammerQuestStage.ComputeAccessNeeded:
                cell = BlockCPlayableLayout.ComputeModuleObjective;
                label = "Архив данных";
                return true;
            case ProgrammerQuestStage.SignalAmplifierNeeded:
                cell = BlockCPlayableLayout.SignalAmplifierObjective;
                label = "Релейная комната";
                return true;
            default:
                return NoQuestTarget(out cell, out label);
        }
    }

    private static bool NoQuestTarget(out Vector2Int cell, out string label)
    {
        cell = default;
        label = null;
        return false;
    }

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
        maskingActiveUntil = 0f;
        LastResult = null;
        pendingExperimentSummary = false;
        helpedCompetitorInLastExperiment = false;
        metMedicMechanic = false;
        medicMechanicFirstCraftRewarded = false;
        medicMechanicFirstUpgradeRewarded = false;
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
        relationships[NpcId.MedicMechanic] = 50;
        participants[NpcId.Programmer] = true;
        participants[NpcId.Competitor] = true;
        participants[NpcId.MedicMechanic] = true;
        implants.Clear();
        craftMaterials.Clear();
        craftedItems.Clear();
        implantUpgradeLevels.Clear();
        lootedResourceCaches.Clear();
        evidence.Clear();
        deductions.Clear();
        evidenceConnectionAttempts.Clear();
        playedExperiments.Clear();
        PrisonItems.Clear();
        visitedRoomIds.Clear();
        programmerQuest = ProgrammerQuestStage.NotStarted;
        competitorQuest = CompetitorQuestStage.Unknown;
        activeQuest = ActiveQuestId.Identity;
        minuteOfDay = DaySchedule.WakeUpMinute;
        dayPhase = DayPhase.MorningFreeTime;
        pendingExperimentSummary = false;
        restingInBed = false;
        helpedCompetitorInLastExperiment = false;
        eyeImplantActive = false;
        maskingActiveUntil = 0f;
        maskingCooldownUntil = 0f;
        alarmActive = false;
        programmerPredictionUnlocked = false;
        prisonReturnSpawnPending = false;
        ClearQueuedExperimentPreview();
        LastResult = null;
        Day = 1;
        RaceParticipants = 4;
        pendingSecurityCameraIncidents.Clear();
        activeReactiveSecurityCameras.Clear();
        PlayerPrefs.DeleteKey(ReactiveFeetKey);
        PlayerPrefs.Save();
    }

    public static void QueueSecurityCameraIncident(Vector2Int cell)
    {
        if (HasNearbyPendingSecurityIncident(cell, 5)) return;
        pendingSecurityCameraIncidents.Add(cell);
    }

    public static List<Vector2Int> ConsumePendingSecurityCameraIncidents()
    {
        var incidents = new List<Vector2Int>(pendingSecurityCameraIncidents);
        pendingSecurityCameraIncidents.Clear();
        return incidents;
    }

    public static void RegisterReactiveSecurityCamera(Vector2Int cell, Vector2Int facing)
    {
        foreach (ReactiveSecurityCamera camera in activeReactiveSecurityCameras)
        {
            if (camera.Cell == cell) return;
        }

        activeReactiveSecurityCameras.Add(new ReactiveSecurityCamera(cell, facing));
    }

    private static bool HasNearbyPendingSecurityIncident(Vector2Int cell, int maxDistance)
    {
        foreach (Vector2Int incident in pendingSecurityCameraIncidents)
        {
            Vector2Int d = incident - cell;
            if (Mathf.Abs(d.x) + Mathf.Abs(d.y) <= maxDistance) return true;
        }

        foreach (ReactiveSecurityCamera camera in activeReactiveSecurityCameras)
        {
            Vector2Int d = camera.Cell - cell;
            if (Mathf.Abs(d.x) + Mathf.Abs(d.y) <= maxDistance) return true;
        }

        return false;
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
    public static readonly NpcId[] SocialNpcs = { NpcId.Programmer, NpcId.Competitor, NpcId.MedicMechanic };

    /// <summary>Отображаемое имя NPC (единый источник для UI и итогов дня).</summary>
    public static string NpcDisplayName(NpcId npc) => npc switch
    {
        NpcId.Programmer => "Программист",
        NpcId.Competitor => "Ракель",
        NpcId.MedicMechanic => "Медик-механик",
        _ => npc.ToString(),
    };

    /// <summary>Ресурс-портрет NPC в Resources/Sprites (как в диалогах).</summary>
    public static string NpcPortraitResource(NpcId npc) => npc switch
    {
        NpcId.Programmer => "npc_programmer",
        NpcId.Competitor => "girl",
        NpcId.MedicMechanic => "inmate_c1752",
        _ => null,
    };

    /// <summary>Изменить отношение игрока к NPC на величину delta (с зажимом в 0–100).</summary>
    public static void AdjustRelationship(NpcId npc, int delta)
    {
        int next = RelationshipTo(npc) + delta;
        relationships[npc] = Mathf.Clamp(next, RelationshipLevels.Min, RelationshipLevels.Max);
    }

    public static bool MarkMedicMechanicIntroduced()
    {
        if (metMedicMechanic) return false;
        metMedicMechanic = true;
        AdjustRelationship(NpcId.MedicMechanic, RelationshipNudgeSmall);
        return true;
    }

    public static int MaterialCount(CraftMaterialId material) =>
        craftMaterials.TryGetValue(material, out int value) ? value : 0;

    public static int CraftedItemCount(CraftedItemId item) =>
        item != CraftedItemId.None && craftedItems.TryGetValue(item, out int value) ? value : 0;

    public static int ImplantUpgradeLevel(ImplantId implant) =>
        implantUpgradeLevels.TryGetValue(implant, out int level) ? level : 0;

    public static void AddCraftMaterial(CraftMaterialId material, int amount)
    {
        if (amount <= 0) return;
        craftMaterials[material] = MaterialCount(material) + amount;
    }

    public static bool IsResourceCacheLooted(string cacheId) =>
        !string.IsNullOrEmpty(cacheId) && lootedResourceCaches.Contains(cacheId);

    public static void MarkResourceCacheLooted(string cacheId)
    {
        if (!string.IsNullOrEmpty(cacheId)) lootedResourceCaches.Add(cacheId);
    }

    public static bool IsCraftRecipeUnlocked(CraftedItemId item)
    {
        int relationship = RelationshipTo(NpcId.MedicMechanic);
        return item switch
        {
            CraftedItemId.Medkit => relationship >= 40,
            CraftedItemId.NoiseBeacon => relationship >= 40,
            CraftedItemId.SmokeBomb => relationship >= 60,
            CraftedItemId.EmpGrenade => relationship >= 75,
            CraftedItemId.HologramGrenade => relationship >= 75,
            _ => false,
        };
    }

    public static CraftCost[] CraftRecipeCost(CraftedItemId item) => item switch
    {
        CraftedItemId.Medkit => new[]
        {
            new CraftCost(CraftMaterialId.Chemicals, 2),
            new CraftCost(CraftMaterialId.ScrapMetal, 1),
        },
        CraftedItemId.NoiseBeacon => new[]
        {
            new CraftCost(CraftMaterialId.ScrapMetal, 2),
            new CraftCost(CraftMaterialId.Microchips, 1),
        },
        CraftedItemId.SmokeBomb => new[]
        {
            new CraftCost(CraftMaterialId.Chemicals, 3),
            new CraftCost(CraftMaterialId.ScrapMetal, 1),
        },
        CraftedItemId.EmpGrenade => new[]
        {
            new CraftCost(CraftMaterialId.QualityScrapMetal, 1),
            new CraftCost(CraftMaterialId.Microchips, 2),
            new CraftCost(CraftMaterialId.QualityMicrochips, 1),
        },
        CraftedItemId.HologramGrenade => new[]
        {
            new CraftCost(CraftMaterialId.QualityScrapMetal, 1),
            new CraftCost(CraftMaterialId.QualityMicrochips, 2),
        },
        _ => new CraftCost[0],
    };

    public static bool TryCraft(CraftedItemId item, out string message)
    {
        if (!IsCraftRecipeUnlocked(item))
        {
            message = "Медик-механик пока не доверяет вам этот рецепт.";
            return false;
        }

        CraftCost[] costs = CraftRecipeCost(item);
        if (!HasMaterials(costs, out message)) return false;

        SpendMaterials(costs);
        craftedItems[item] = CraftedItemCount(item) + 1;
        bool gainedTrust = false;
        if (!medicMechanicFirstCraftRewarded)
        {
            medicMechanicFirstCraftRewarded = true;
            AdjustRelationship(NpcId.MedicMechanic, RelationshipNudgeSmall);
            gainedTrust = true;
        }
        message = gainedTrust
            ? $"Создано: {CraftedItemName(item)}. Медик-механик стал доверять вам больше."
            : $"Создано: {CraftedItemName(item)}.";
        return true;
    }

    public static bool TryConsumeCraftedItem(CraftedItemId item, out string message)
    {
        if (item == CraftedItemId.None)
        {
            message = "Быстрый слот пуст.";
            return false;
        }

        int count = CraftedItemCount(item);
        if (count <= 0)
        {
            message = $"{CraftedItemName(item)} отсутствует.";
            return false;
        }

        craftedItems[item] = count - 1;
        message = null;
        return true;
    }

    public static bool IsImplantUpgradeUnlocked(ImplantId implant)
    {
        if (!HasImplant(implant)) return false;
        return ImplantUpgradeLevel(implant) < 2 &&
               RelationshipTo(NpcId.MedicMechanic) >= ImplantUpgradeRequiredRelationship(implant);
    }

    public static int ImplantUpgradeRequiredRelationship(ImplantId implant)
    {
        int currentLevel = ImplantUpgradeLevel(implant);
        if (currentLevel >= 2) return 0;
        return currentLevel == 0 ? 60 : 75;
    }

    public static CraftCost[] ImplantUpgradeCost(ImplantId implant)
    {
        int nextLevel = ImplantUpgradeLevel(implant) + 1;
        return implant switch
        {
            ImplantId.EyeImplant when nextLevel == 1 => new[]
            {
                new CraftCost(CraftMaterialId.Microchips, 2),
                new CraftCost(CraftMaterialId.QualityMicrochips, 1),
            },
            ImplantId.EyeImplant when nextLevel == 2 => new[]
            {
                new CraftCost(CraftMaterialId.QualityMicrochips, 2),
                new CraftCost(CraftMaterialId.QualityScrapMetal, 1),
            },
            ImplantId.MaskingImplant when nextLevel == 1 => new[]
            {
                new CraftCost(CraftMaterialId.QualityChemicals, 1),
                new CraftCost(CraftMaterialId.Microchips, 2),
            },
            ImplantId.MaskingImplant when nextLevel == 2 => new[]
            {
                new CraftCost(CraftMaterialId.QualityChemicals, 2),
                new CraftCost(CraftMaterialId.QualityMicrochips, 1),
            },
            ImplantId.ReactiveFeet when nextLevel == 1 => new[]
            {
                new CraftCost(CraftMaterialId.ScrapMetal, 3),
                new CraftCost(CraftMaterialId.QualityScrapMetal, 1),
            },
            ImplantId.ReactiveFeet when nextLevel == 2 => new[]
            {
                new CraftCost(CraftMaterialId.QualityScrapMetal, 2),
                new CraftCost(CraftMaterialId.Microchips, 1),
            },
            _ => new CraftCost[0],
        };
    }

    public static bool TryUpgradeImplant(ImplantId implant, out string message)
    {
        if (!HasImplant(implant))
        {
            message = "Этот имплант ещё не установлен.";
            return false;
        }

        int currentLevel = ImplantUpgradeLevel(implant);
        if (currentLevel >= 2)
        {
            message = "Имплант уже улучшен до максимума.";
            return false;
        }

        int requiredRelationship = ImplantUpgradeRequiredRelationship(implant);
        if (RelationshipTo(NpcId.MedicMechanic) < requiredRelationship)
        {
            message = $"Для этого улучшения нужно отношение с медиком-механиком {requiredRelationship}+.";
            return false;
        }

        CraftCost[] costs = ImplantUpgradeCost(implant);
        if (!HasMaterials(costs, out message)) return false;

        SpendMaterials(costs);
        implantUpgradeLevels[implant] = currentLevel + 1;
        bool gainedTrust = false;
        if (!medicMechanicFirstUpgradeRewarded)
        {
            medicMechanicFirstUpgradeRewarded = true;
            AdjustRelationship(NpcId.MedicMechanic, RelationshipNudgeSmall);
            gainedTrust = true;
        }
        message = gainedTrust
            ? $"{ImplantName(implant)} улучшен до уровня {currentLevel + 1}. Медик-механик стал доверять вам больше."
            : $"{ImplantName(implant)} улучшен до уровня {currentLevel + 1}.";
        return true;
    }

    public static string MaterialName(CraftMaterialId material) => material switch
    {
        CraftMaterialId.Chemicals => "Химикаты",
        CraftMaterialId.QualityChemicals => "Качественные химикаты",
        CraftMaterialId.ScrapMetal => "Металлолом",
        CraftMaterialId.QualityScrapMetal => "Качественный металлолом",
        CraftMaterialId.Microchips => "Микросхемы",
        CraftMaterialId.QualityMicrochips => "Качественные микросхемы",
        _ => material.ToString(),
    };

    public static string CraftedItemName(CraftedItemId item) => item switch
    {
        CraftedItemId.None => "Пусто",
        CraftedItemId.Medkit => "Аптечка",
        CraftedItemId.NoiseBeacon => "Шумовой маячок",
        CraftedItemId.SmokeBomb => "Дымовая шашка",
        CraftedItemId.EmpGrenade => "ЭМИ-граната",
        CraftedItemId.HologramGrenade => "Голографическая граната",
        _ => item.ToString(),
    };

    public static string CraftedItemDescription(CraftedItemId item) => item switch
    {
        CraftedItemId.None => "Слот быстрого доступа пуст.",
        CraftedItemId.Medkit => "Восстановление здоровья. В прототипе пока копится в инвентаре.",
        CraftedItemId.NoiseBeacon => "Расходник для стелса: бросается мышью и уводит ближайшую охрану на шум.",
        CraftedItemId.SmokeBomb => "Будущий предмет для разрыва линии видимости.",
        CraftedItemId.EmpGrenade => "Будущий предмет против камер и электронных замков.",
        CraftedItemId.HologramGrenade => "Будущий предмет для ложной цели охраны.",
        _ => "",
    };

    public static string ImplantName(ImplantId implant) => implant switch
    {
        ImplantId.ReactiveFeet => "Реактивные стопы",
        ImplantId.EyeImplant => "Глазной имплант",
        ImplantId.MaskingImplant => "Маскировочный имплант",
        _ => implant.ToString(),
    };

    public static string ImplantUpgradeDescription(ImplantId implant) => implant switch
    {
        ImplantId.EyeImplant => "Улучшение будущего радиуса анализа скрытых систем.",
        ImplantId.MaskingImplant => "Увеличивает длительность маскировки и сокращает откат.",
        ImplantId.ReactiveFeet => "Улучшение будущей мобильности рывка.",
        _ => "",
    };

    public static string FormatCost(CraftCost[] costs)
    {
        if (costs == null || costs.Length == 0) return "нет стоимости";
        var parts = new List<string>();
        foreach (CraftCost cost in costs)
        {
            parts.Add($"{MaterialName(cost.Material)} x{cost.Amount}");
        }
        return string.Join(", ", parts);
    }

    private static bool HasMaterials(CraftCost[] costs, out string message)
    {
        foreach (CraftCost cost in costs)
        {
            if (MaterialCount(cost.Material) < cost.Amount)
            {
                message = $"Не хватает: {MaterialName(cost.Material)} x{cost.Amount}.";
                return false;
            }
        }

        message = null;
        return true;
    }

    private static void SpendMaterials(CraftCost[] costs)
    {
        foreach (CraftCost cost in costs)
        {
            craftMaterials[cost.Material] = Mathf.Max(0, MaterialCount(cost.Material) - cost.Amount);
        }
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
        evidenceConnectionAttempts[EvidenceConnectionKey(first, second)] =
            new EvidenceConnectionRecord(first, second, deduction);

        if (!deduction.HasValue) return null;

        deductions.Add(deduction.Value);
        return deduction.Value;
    }

    public static bool TryGetEvidenceConnectionAttempt(
        EvidenceId first,
        EvidenceId second,
        out EvidenceConnectionRecord record)
    {
        return evidenceConnectionAttempts.TryGetValue(EvidenceConnectionKey(first, second), out record);
    }

    public static DeductionId? PreviewEvidenceConnection(EvidenceId first, EvidenceId second)
    {
        if (first == second) return null;
        return ResolveDeduction(first, second);
    }

    public static bool TryGetDeductionSources(DeductionId deduction, out EvidenceId first, out EvidenceId second)
    {
        foreach (EvidenceConnectionRecord record in evidenceConnectionAttempts.Values)
        {
            if (record.Deduction == deduction)
            {
                first = record.First;
                second = record.Second;
                return true;
            }
        }

        EvidenceId[] discovered = new EvidenceId[evidence.Count];
        evidence.CopyTo(discovered);
        for (int i = 0; i < discovered.Length; i++)
        {
            for (int j = i + 1; j < discovered.Length; j++)
            {
                if (ResolveDeduction(discovered[i], discovered[j]) == deduction)
                {
                    first = discovered[i];
                    second = discovered[j];
                    return true;
                }
            }
        }

        first = default;
        second = default;
        return false;
    }

    private static string EvidenceConnectionKey(EvidenceId first, EvidenceId second)
    {
        int a = (int)first;
        int b = (int)second;
        return a < b ? $"{a}:{b}" : $"{b}:{a}";
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

        if (Has(EvidenceId.EscapedPrisonerRumor, EvidenceId.GuardEscapePostAnalysis))
        {
            return DeductionId.PastEscapeWeakness;
        }

        if (Has(EvidenceId.EscapeeArchiveFolder, EvidenceId.GuardEscapePostAnalysis))
        {
            return DeductionId.SoloEscapeRoute;
        }

        if (Has(EvidenceId.EscapeeArchiveFolder, EvidenceId.CompetitorGuardMeeting) ||
            Has(EvidenceId.EscapeeArchiveFolder, EvidenceId.StaffSmokeBreakSchedule))
        {
            return DeductionId.RaquelEscapePlanning;
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
            EvidenceId.CompetitorVentRoute => "Ракель пользуется санитарным служебным маршрутом",
            EvidenceId.CompetitorGuardMeeting => "Ракель встречается с надзирателем",
            EvidenceId.GardenKey => "Доступ Ракель к новому входу в сад",
            EvidenceId.StaffSmokeBreakSchedule => "Расписание персонала в саду",
            EvidenceId.GardenConnectsWings => "Сад соединяет крылья тюрьмы",
            EvidenceId.CookServiceRoute => "Повара знают бытовые проходы",
            EvidenceId.ScientistGardenRumor => "Учёные обсуждают поведенческий протокол",
            EvidenceId.EscapedPrisonerRumor => "Охрана вспоминает прошлый побег",
            EvidenceId.GuardPostIdentityScan => "Пост охраны проверяет личность",
            EvidenceId.ArchiveKeys => "Ключи архива лежат на посту охраны",
            EvidenceId.EscapeeArchiveFolder => "Папка о сбежавшем заключённом",
            EvidenceId.GuardEscapePostAnalysis => "Разбор ошибок охраны после побега",
            EvidenceId.AfterLightsOutPassage => "После отбоя есть ещё один проход",
            EvidenceId.StaffQuietZoneNote => "Записка упоминает тихую зону",
            EvidenceId.ExperimentReportsSocialTesting => "Отчёты описывают социальные решения",
            _ => id.ToString(),
        };
    }

    public static string EvidenceShortTitle(EvidenceId id)
    {
        return id switch
        {
            EvidenceId.AdaptiveExperimentSystem => "Система испытаний",
            EvidenceId.HiddenSystemsNeedEyeImplant => "Скрытые системы",
            EvidenceId.EngineeringTransmitter => "Передатчик",
            EvidenceId.CompetitorVentRoute => "Маршрут Ракель",
            EvidenceId.CompetitorGuardMeeting => "Встреча с охраной",
            EvidenceId.GardenKey => "Вход в сад",
            EvidenceId.StaffSmokeBreakSchedule => "Расписание сада",
            EvidenceId.GardenConnectsWings => "Сад между крыльями",
            EvidenceId.CookServiceRoute => "Маршрут поваров",
            EvidenceId.ScientistGardenRumor => "Протокол учёных",
            EvidenceId.EscapedPrisonerRumor => "Прошлый побег",
            EvidenceId.GuardPostIdentityScan => "Сканер поста",
            EvidenceId.ArchiveKeys => "Ключи архива",
            EvidenceId.EscapeeArchiveFolder => "Папка беглеца",
            EvidenceId.GuardEscapePostAnalysis => "Разбор охраны",
            EvidenceId.AfterLightsOutPassage => "Проход после отбоя",
            EvidenceId.StaffQuietZoneNote => "Тихая зона",
            EvidenceId.ExperimentReportsSocialTesting => "Социальные отчёты",
            _ => EvidenceTitle(id),
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
                "Ракель прошла через закрытую санитарную комнату персонала и появилась в служебном крыле.",
            EvidenceId.CompetitorGuardMeeting =>
                "В комнате персонала Ракель встретилась с отдельным надзирателем.",
            EvidenceId.GardenKey =>
                "В подслушанном разговоре прозвучало, что старый вход в сад закрыли, а новым входом может пользоваться Ракель.",
            EvidenceId.StaffSmokeBreakSchedule =>
                "Ракель дала расписание, когда персонал выходит в сад курить и говорить вне формального маршрута.",
            EvidenceId.GardenConnectsWings =>
                "Сад используется персоналом как узел между первым крылом и блоком C.",
            EvidenceId.CookServiceRoute =>
                "Повара используют сад как короткий путь между кухонным обслуживанием двух крыльев.",
            EvidenceId.ScientistGardenRumor =>
                "Учёные говорят о протоколе, который измеряет не только выживание, но и готовность использовать других.",
            EvidenceId.EscapedPrisonerRumor =>
                "Охранники упомянули заключённого, который уже смог сбежать из этой тюрьмы.",
            EvidenceId.GuardPostIdentityScan =>
                "Доступ к посту охраны проходит через сканирование личности. Обычный заключённый его не пройдёт.",
            EvidenceId.ArchiveKeys =>
                "На посту охраны хранится доступ к архиву служебных разборов.",
            EvidenceId.EscapeeArchiveFolder =>
                "В архиве есть папка о заключённом, который использовал слабые места охраны и вышел наружу.",
            EvidenceId.GuardEscapePostAnalysis =>
                "Высшие чины охраны описали, какие ошибки позволили прошлый побег и какие меры должны закрыть маршрут.",
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
            DeductionId.PastEscapeWeakness => "Прошлый побег указывает на слабость охраны",
            DeductionId.SoloEscapeRoute => "Можно восстановить одиночный маршрут побега",
            DeductionId.RaquelEscapePlanning => "Папка может стать основой совместного плана с Ракель",
            _ => id.ToString(),
        };
    }

    public static string DeductionShortTitle(DeductionId id)
    {
        return id switch
        {
            DeductionId.PredictExperimentData => "Прогноз испытаний",
            DeductionId.UnofficialStaffRoutes => "Тайные маршруты",
            DeductionId.GardenIsStaffHub => "Сад как узел слухов",
            DeductionId.GardenRouteToBlockC => "Путь в блок C",
            DeductionId.StaffQuietZoneAccess => "Доступ в тихую зону",
            DeductionId.SocialExperimentPurpose => "Моральный эксперимент",
            DeductionId.CameraBlindSpotRoute => "Слепой маршрут",
            DeductionId.PastEscapeWeakness => "Слабость охраны",
            DeductionId.SoloEscapeRoute => "Одиночный побег",
            DeductionId.RaquelEscapePlanning => "План с Ракель",
            _ => DeductionTitle(id),
        };
    }

    public static string DeductionDescription(DeductionId id)
    {
        return id switch
        {
            DeductionId.PredictExperimentData =>
                "Если система подбирает испытания автоматически, передатчик может заранее дать тип испытания, участников или главный риск.",
            DeductionId.UnofficialStaffRoutes =>
                "Маршрут Ракель и доступ к саду указывают на скрытую сеть служебных перемещений.",
            DeductionId.GardenIsStaffHub =>
                "Если персонал регулярно выходит в сад, это место можно использовать для подслушивания слухов разных routes.",
            DeductionId.GardenRouteToBlockC =>
                "Доступ к саду и его положение между крыльями означают, что сад может открыть путь к блоку C.",
            DeductionId.StaffQuietZoneAccess =>
                "Записка и встреча с надзирателем указывают, что тихая зона используется для неофициальных договорённостей.",
            DeductionId.SocialExperimentPurpose =>
                "Отчёты и система подбора вместе показывают: администрация изучает не только выживание, но и моральный выбор.",
            DeductionId.CameraBlindSpotRoute =>
                "Если санитарный служебный путь работает, глазной имплант поможет понять, какие камеры его закрывают или пропускают.",
            DeductionId.PastEscapeWeakness =>
                "Слух охраны и служебный разбор подтверждают: система уже давала сбой, и этот сбой можно искать заново.",
            DeductionId.SoloEscapeRoute =>
                "Папка и анализ охраны дают материал для будущего одиночного побега, но маршрут ещё нужно проверить на карте.",
            DeductionId.RaquelEscapePlanning =>
                "Если отдать папку Ракель, её знания о персонале можно соединить с техническим разбором старого побега.",
            _ => "",
        };
    }

    public static void StartCompetitorTracking()
    {
        if (competitorQuest == CompetitorQuestStage.Unknown)
        {
            competitorQuest = CompetitorQuestStage.Tracking;
            activeQuest = ActiveQuestId.Raquel;
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
        if (competitorQuest < CompetitorQuestStage.Overheard)
        {
            competitorQuest = CompetitorQuestStage.Overheard;
            AddEvidence(EvidenceId.CompetitorGuardMeeting);
            AddEvidence(EvidenceId.GardenKey);
        }
    }

    public static bool ShouldScheduleRaquelGardenMeeting =>
        competitorQuest == CompetitorQuestStage.Overheard && helpedCompetitorInLastExperiment;

    public static bool IsRaquelGardenMeetingWindow =>
        minuteOfDay >= 19 * 60 && minuteOfDay < 19 * 60 + 45;

    public static void ScheduleRaquelGardenMeeting()
    {
        if (competitorQuest == CompetitorQuestStage.Overheard && helpedCompetitorInLastExperiment)
        {
            competitorQuest = CompetitorQuestStage.GardenMeetingScheduled;
        }
    }

    public static bool CompleteRaquelGardenMeeting()
    {
        if (competitorQuest != CompetitorQuestStage.GardenMeetingScheduled &&
            competitorQuest != CompetitorQuestStage.Overheard)
        {
            return false;
        }

        competitorQuest = CompetitorQuestStage.GardenMeetingComplete;
        AddImplant(ImplantId.MaskingImplant);
        AddEvidence(EvidenceId.StaffSmokeBreakSchedule);
        AddEvidence(EvidenceId.GardenConnectsWings);
        AdjustRelationship(NpcId.Competitor, RelationshipNudgeSmall);
        return true;
    }

    public static void MarkCompetitorSmokeScheduleGiven()
    {
        if (competitorQuest == CompetitorQuestStage.Unknown ||
            competitorQuest == CompetitorQuestStage.Tracking ||
            competitorQuest == CompetitorQuestStage.ReachedStaffRoom ||
            competitorQuest == CompetitorQuestStage.Overheard ||
            competitorQuest == CompetitorQuestStage.GardenMeetingScheduled ||
            competitorQuest == CompetitorQuestStage.GardenMeetingComplete)
        {
            competitorQuest = CompetitorQuestStage.SmokeScheduleKnown;
        }

        AddEvidence(EvidenceId.StaffSmokeBreakSchedule);
        AddEvidence(EvidenceId.GardenConnectsWings);
    }

    public static void MarkGardenAccessOpened()
    {
        if (competitorQuest < CompetitorQuestStage.GardenAccess)
        {
            competitorQuest = CompetitorQuestStage.GardenAccess;
        }
        AddEvidence(EvidenceId.GardenConnectsWings);
    }

    public static void MarkGardenConversationHeard(EvidenceId conversationEvidence)
    {
        AddEvidence(conversationEvidence);
        if (conversationEvidence == EvidenceId.EscapedPrisonerRumor &&
            competitorQuest < CompetitorQuestStage.GuardPostLead)
        {
            competitorQuest = CompetitorQuestStage.GuardPostLead;
        }
    }

    public static void MarkArchiveKeyAcquired()
    {
        AddPrisonItem(PrisonItemId.ArchiveKey);
        AddEvidence(EvidenceId.GuardPostIdentityScan);
        AddEvidence(EvidenceId.ArchiveKeys);
        if (competitorQuest < CompetitorQuestStage.ArchiveKeyAcquired)
        {
            competitorQuest = CompetitorQuestStage.ArchiveKeyAcquired;
        }
    }

    public static void MarkEscapeArchiveFound()
    {
        AddPrisonItem(PrisonItemId.EscapeArchiveFolder);
        AddEvidence(EvidenceId.EscapeeArchiveFolder);
        AddEvidence(EvidenceId.GuardEscapePostAnalysis);
        if (competitorQuest < CompetitorQuestStage.EscapeArchiveFound)
        {
            competitorQuest = CompetitorQuestStage.EscapeArchiveFound;
        }
    }

    public static bool GiveEscapeFolderToRaquel()
    {
        if (!HasPrisonItem(PrisonItemId.EscapeArchiveFolder)) return false;
        competitorQuest = CompetitorQuestStage.EscapeFolderGivenToRaquel;
        AddEvidence(EvidenceId.EscapeeArchiveFolder);
        AddEvidence(EvidenceId.GuardEscapePostAnalysis);
        AdjustRelationship(NpcId.Competitor, RelationshipNudgeSmall);
        deductions.Add(DeductionId.RaquelEscapePlanning);
        return true;
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
        if (implant == ImplantId.EyeImplant) return eyeImplantActive && HasImplant(implant);
        if (implant == ImplantId.MaskingImplant) return MaskingImplantActive;
        return false;
    }

    public static bool ToggleEyeImplant()
    {
        if (!HasImplant(ImplantId.EyeImplant)) return false;
        eyeImplantActive = !eyeImplantActive;
        return true;
    }

    public static bool TryActivateMaskingImplant(out string message)
    {
        if (!HasImplant(ImplantId.MaskingImplant))
        {
            message = "Маскировочный имплант не установлен.";
            return false;
        }

        if (MaskingImplantActive)
        {
            message = $"Маскировка уже активна: {Mathf.CeilToInt(MaskingImplantRemaining)} сек.";
            return false;
        }

        float cooldown = MaskingImplantCooldownRemaining;
        if (cooldown > 0f)
        {
            message = $"Маскировочный имплант перезаряжается: {Mathf.CeilToInt(cooldown)} сек.";
            return false;
        }

        int upgradeLevel = ImplantUpgradeLevel(ImplantId.MaskingImplant);
        float duration = MaskingDurationSeconds + upgradeLevel * 10f;
        float cooldownDuration = Mathf.Max(180f, MaskingCooldownSeconds - upgradeLevel * 45f);
        maskingActiveUntil = Time.time + duration;
        maskingCooldownUntil = Time.time + cooldownDuration;
        message = "Маскировочный имплант активен. Охрана и камеры принимают вас за надзирателя.";
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
            ScheduleRaquelGardenMeeting();
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
        LoadExperimentSceneOrFallback(ExperimentScene);
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
                LoadExperimentSceneOrFallback(sceneName);
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
        LoadExperimentSceneOrFallback(def.SceneName);
    }

    private static void LoadExperimentSceneOrFallback(string sceneName)
    {
        string target = CanLoadScene(sceneName) ? sceneName : ExperimentScene;
        if (target != sceneName)
        {
            Debug.LogWarning($"Experiment scene '{sceneName}' is not in Build Settings. Falling back to '{ExperimentScene}'.");
        }

        SceneManager.LoadScene(target);
    }

    private static bool CanLoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return false;
        if (SceneUtility.GetBuildIndexByScenePath(sceneName) >= 0) return true;
        if (SceneUtility.GetBuildIndexByScenePath($"Assets/Scenes/{sceneName}.unity") >= 0) return true;
        return false;
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

    /// <summary>Отметить комнату (id из RoomGraph) посещённой. Копится через дни.</summary>
    public static void MarkRoomVisited(int roomId)
    {
        if (roomId >= 0) visitedRoomIds.Add(roomId);
    }

    /// <summary>Заходил ли игрок в комнату с данным id хотя бы раз за забег.</summary>
    public static bool IsRoomVisited(int roomId) => visitedRoomIds.Contains(roomId);

    public static int PrisonItemCount => PrisonItems.Count;

    public static bool HasPrisonItem(PrisonItemId itemId)
    {
        return PrisonItems.Contains(itemId);
    }

    public static void AddPrisonItem(PrisonItemId itemId)
    {
        if (itemId == PrisonItemId.None || itemId == PrisonItemId.Unavailable) return;
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
        else if (itemId == PrisonItemId.ArchiveKey)
        {
            AddEvidence(EvidenceId.ArchiveKeys);
        }
        else if (itemId == PrisonItemId.EscapeArchiveFolder)
        {
            AddEvidence(EvidenceId.EscapeeArchiveFolder);
            AddEvidence(EvidenceId.GuardEscapePostAnalysis);
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
        activeQuest = ActiveQuestId.Programmer;
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
