using System.Collections.Generic;

/// <summary>
/// Общий контракт между глобальной картой и экспериментами.
/// Описан в Design/ROADMAP.md (раздел «Контракт между картой и экспериментами»).
///
/// Конкретный эксперимент НЕ хранит глобальное состояние сам: он получает
/// <see cref="ExperimentContext"/> на входе и возвращает <see cref="ExperimentResult"/>
/// на выходе. Карта (Поток A) сама решает, как применить результат.
/// </summary>

/// <summary>Структура взаимодействия эксперимента (пять подтверждённых категорий брифа).</summary>
public enum ExperimentCategory
{
    /// <summary>Категория 1: каждый сам за себя (полоса препятствий).</summary>
    FreeForAll,

    /// <summary>Категория 2: одиночное испытание (головоломка, память, наблюдательность).</summary>
    Solo,

    /// <summary>Категория 3: команда против команды.</summary>
    TeamVsTeam,

    /// <summary>Категория 4: разборки внутри команды (голосование, предательство).</summary>
    InternalConflict,

    /// <summary>Категория 5: один на один (блэкджек, дуэль).</summary>
    OneOnOne,
}

/// <summary>
/// Стабильные идентификаторы NPC. Часть «общей зоны» из ROADMAP.md:
/// меняется только по согласованию команды.
/// </summary>
public enum NpcId
{
    Programmer,
    Competitor,
}

/// <summary>
/// Стабильные идентификаторы имплантов. Часть «общей зоны» из ROADMAP.md.
/// </summary>
public enum ImplantId
{
    /// <summary>Реактивные стопы — рывок по Q (награда полосы препятствий).</summary>
    ReactiveFeet,

    /// <summary>Глазной имплант — видимость камер и зон сканирования (ветка программиста).</summary>
    EyeImplant,

    /// <summary>Маскировочный имплант — временно имитирует облик охраны (ветка Ракель).</summary>
    MaskingImplant,
}

/// <summary>
/// Запоминаемое действие игрока по отношению к конкретному NPC внутри эксперимента.
/// Промежуточные исходы важнее простого «помог/не помог» (см. EXPERIMENT_01_SPEC.md).
/// </summary>
public enum NpcAction
{
    None,
    Helped,
    Harmed,
    Betrayed,
    Ignored,
}

/// <summary>
/// Расположение NPC к игроку, выведенное из отношений. Основа социального поведения
/// ботов в экспериментах: враждебные мешают игроку, дружелюбные помогают.
/// </summary>
public enum NpcDisposition
{
    Hostile,
    Neutral,
    Friendly,
}

/// <summary>
/// Пять уровней отношения игрока к NPC по шкале 0–100.
/// От худшего к лучшему: враг → неприязнь → нейтрально → приятель → друг.
/// </summary>
public enum RelationshipLevel
{
    Enemy,
    Dislike,
    Neutral,
    Acquaintance,
    Friend,
}

/// <summary>
/// Перевод числового отношения (0–100) в уровень и подпись.
/// Единая точка правил для UI и поведения. Полосы по 20 очков:
/// 0–19 враг, 20–39 неприязнь, 40–59 нейтрально, 60–79 приятель, 80–100 друг.
/// </summary>
public static class RelationshipLevels
{
    public const int Min = 0;
    public const int Max = 100;
    public const int Neutral = 50;

    public static RelationshipLevel For(int score)
    {
        if (score < 20) return RelationshipLevel.Enemy;
        if (score < 40) return RelationshipLevel.Dislike;
        if (score < 60) return RelationshipLevel.Neutral;
        if (score < 80) return RelationshipLevel.Acquaintance;
        return RelationshipLevel.Friend;
    }

    public static string Label(RelationshipLevel level)
    {
        return level switch
        {
            RelationshipLevel.Enemy => "Враг",
            RelationshipLevel.Dislike => "Неприязнь",
            RelationshipLevel.Neutral => "Нейтрально",
            RelationshipLevel.Acquaintance => "Приятель",
            RelationshipLevel.Friend => "Друг",
            _ => "Нейтрально",
        };
    }

    public static string Label(int score) => Label(For(score));
}

/// <summary>Перевод числового отношения в расположение. Единая точка правил для всех экспериментов.</summary>
public static class Disposition
{
    // Шкала 0–100: враждебно — неприязнь и ниже (&lt;40), дружелюбно — приятель и выше (&gt;=60).
    public const int HostileBelow = 40;
    public const int FriendlyAtOrAbove = 60;

    public static NpcDisposition For(int relationship)
    {
        if (relationship < HostileBelow) return NpcDisposition.Hostile;
        if (relationship >= FriendlyAtOrAbove) return NpcDisposition.Friendly;
        return NpcDisposition.Neutral;
    }
}

/// <summary>
/// Вход эксперимента. Эксперимент читает этот контекст и не обращается к карте напрямую.
/// </summary>
public sealed class ExperimentContext
{
    /// <summary>Какой эксперимент загружен (идентификатор определения из пула).</summary>
    public string ExperimentId;

    /// <summary>Номер игрового дня / уровень сложности для масштабирования.</summary>
    public int Day;

    /// <summary>Участники и флаг «жив ли» на момент старта.</summary>
    public readonly Dictionary<NpcId, bool> Participants = new();

    /// <summary>Отношения игрока с участниками. Больше — лучше (произвольная шкала прототипа).</summary>
    public readonly Dictionary<NpcId, int> Relationships = new();

    /// <summary>Установленные игроку импланты (доступные способности).</summary>
    public readonly HashSet<ImplantId> Implants = new();

    /// <summary>
    /// Кого активный квест просит спасти в этом испытании (null — никого).
    /// Эксперимент использует это, чтобы гарантированно поставить нужного бота
    /// в положение, требующее помощи игрока.
    /// </summary>
    public NpcId? RescueTarget;

    /// <summary>Отношение к участнику или 0, если связь не задана.</summary>
    public int RelationshipTo(NpcId npc)
    {
        return Relationships.TryGetValue(npc, out int value) ? value : 0;
    }

    /// <summary>Установлен ли указанный имплант.</summary>
    public bool HasImplant(ImplantId implant) => Implants.Contains(implant);

    /// <summary>Жив ли участник на старте эксперимента.</summary>
    public bool IsAlive(NpcId npc) => Participants.TryGetValue(npc, out bool alive) && alive;
}

/// <summary>
/// Выход эксперимента. Заполняется конкретным экспериментом и передаётся
/// общей системе (см. RunState.LastResult). Карту напрямую не меняет.
/// </summary>
public sealed class ExperimentResult
{
    /// <summary>Какой эксперимент произвёл результат.</summary>
    public string ExperimentId;

    /// <summary>Выжил ли игрок: продолжать или завершать забег.</summary>
    public bool PlayerSurvived;

    /// <summary>Игрок занял первое место / выиграл (основа награды).</summary>
    public bool PlayerWon;

    /// <summary>Игрок принял предложенный имплант (если предлагался).</summary>
    public bool ImplantAccepted;

    /// <summary>Имплант, который был предложен в этом эксперименте (если был).</summary>
    public ImplantId? OfferedImplant;

    /// <summary>Кто из NPC выжил (true) или погиб (false).</summary>
    public readonly Dictionary<NpcId, bool> NpcSurvived = new();

    /// <summary>Запоминаемые действия игрока к каждому NPC.</summary>
    public readonly Dictionary<NpcId, NpcAction> Actions = new();

    /// <summary>
    /// Изменения отношений по итогу эксперимента (например, благодарность за спасение).
    /// Применяются общей системой при приёме результата.
    /// </summary>
    public readonly Dictionary<NpcId, int> RelationshipDeltas = new();

    /// <summary>Специальные флаги для запуска квестов и событий на карте.</summary>
    public readonly List<string> Flags = new();

    /// <summary>Зафиксировать исход NPC и действие игрока к нему.</summary>
    public void Record(NpcId npc, bool survived, NpcAction action)
    {
        NpcSurvived[npc] = survived;
        Actions[npc] = action;
    }
}
