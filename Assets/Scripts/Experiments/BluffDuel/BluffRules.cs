using System.Collections.Generic;

/// <summary>
/// Расширяемая система правил дуэли «Верю / Не верю» (эксперимент 03).
///
/// Сердце задачи: движок <c>BluffExperiment</c> реализует только базовую игру
/// (объявление карт, «Верю/Не верю», разрешение вскрытия). Все усложнения — это
/// модификаторы <see cref="BluffRule"/>, наложенные поверх. Это тот же приём, что и
/// <c>RaceObstacle</c> в полосе препятствий: новое усложнение добавляется наследованием
/// и одной строкой в <see cref="BluffRuleSet.ForDay"/> — логику движка менять не нужно.
///
/// Точки расширения, которые движок спрашивает у набора правил:
/// - флаги-свойства (<see cref="BluffRule.AllowsFreeDeclare"/>, <see cref="BluffRule.HidesOpponentCount"/>);
/// - хуки событий (<see cref="BluffRule.OnTurnStart"/>, <see cref="BluffRule.OnDeclare"/>,
///   <see cref="BluffRule.OnChallengeResolved"/>);
/// - дополнительные действия игрока (<see cref="BluffRule.Actions"/>) — рисуются в HUD как кнопки.
/// </summary>

/// <summary>Дополнительное действие игрока, привнесённое правилом (кнопка в HUD + горячая клавиша).</summary>
public sealed class RuleAction
{
    public string Id;                              // уникальный ключ (для разовых действий)
    public string Label;                           // подпись в HUD
    public char Hotkey;                            // клавиша запуска
    public System.Func<BluffMatch, bool> Available; // доступно ли прямо сейчас
    public System.Action<BluffMatch> Invoke;        // эффект
}

/// <summary>
/// Состояние одной партии дуэли. Общая модель для движка и правил. Чистая (без Unity):
/// движок задаёт ввод/таймеры, а партия хранит карты, стопку и кто ходит.
/// </summary>
public sealed class BluffMatch
{
    public readonly Hand PlayerHand = new();
    public readonly Hand OpponentHand = new();
    public readonly BluffRuleSet Rules;
    public readonly System.Random Rng;

    /// <summary>Требуемый ранг текущего хода (растёт по кругу, если не включён вольный объяв).</summary>
    public int RequiredRank;

    /// <summary>Чей сейчас ход объявлять.</summary>
    public Side Turn = Side.Player;

    // Последнее объявление (для вскрытия и дедукции).
    public Side LastDeclarer = Side.None;
    public int DeclaredRank;                       // что заявил ходящий (в вольном режиме — выбор игрока)
    public readonly List<Card> LastPlayed = new(); // что реально положено

    /// <summary>Включён ли разовый «подсмотр» руки соперника (правило Peek).</summary>
    public bool PeekActive;

    private readonly HashSet<string> usedActions = new();

    public BluffMatch(BluffRuleSet rules, System.Random rng)
    {
        Rules = rules;
        Rng = rng;
    }

    public Hand HandOf(Side side) => side == Side.Player ? PlayerHand : OpponentHand;
    public Side Other(Side side) => side == Side.Player ? Side.Opponent : Side.Player;

    /// <summary>Было ли последнее объявление ложью (хоть одна карта не заявленного ранга).</summary>
    public bool LastDeclarationWasLie()
    {
        foreach (Card card in LastPlayed)
        {
            if (card.Rank != DeclaredRank) return true;
        }
        return false;
    }

    /// <summary>Перевести ход к следующему рангу по кругу.</summary>
    public void AdvanceRank() => RequiredRank = (RequiredRank + 1) % BluffDeck.RankCount;

    // ---- Разовые действия правил ----

    public bool ActionUsed(string id) => usedActions.Contains(id);
    public void MarkUsed(string id) => usedActions.Add(id);

    /// <summary>Убрать из руки одну карту самого редкого (труднее всего сбрасываемого) ранга.</summary>
    public void DiscardRarest(Side side)
    {
        Hand hand = HandOf(side);
        if (hand.IsEmpty) return;

        int bestRank = -1, bestCount = int.MaxValue;
        for (int r = 0; r < BluffDeck.RankCount; r++)
        {
            int c = hand.CountOfRank(r);
            if (c > 0 && c < bestCount) { bestCount = c; bestRank = r; }
        }
        List<int> indices = hand.IndicesOfRank(bestRank);
        if (indices.Count > 0) hand.RemoveAt(indices[0]);
    }

    /// <summary>Подсунуть стороне случайную карту (раздувает её руку — вред).</summary>
    public void PlantRandom(Side side)
    {
        var card = new Card((CardSuit)Rng.Next(BluffDeck.SuitCount), Rng.Next(BluffDeck.RankCount));
        HandOf(side).Add(card);
    }

    /// <summary>Забрать до count карт из руки соперника к себе (жертва прогресса — помощь сопернику).</summary>
    public void TakeFromOpponent(Side taker, int count)
    {
        Hand from = HandOf(Other(taker));
        Hand to = HandOf(taker);
        for (int i = 0; i < count && !from.IsEmpty; i++)
        {
            to.Add(from.RemoveAt(from.Count - 1));
        }
    }

    /// <summary>
    /// Дедукция доубтера: сколько заявленных карт ГАРАНТИРОВАННО ложь по картам на руке.
    /// Всего копий ранга = SuitCount; на руке у зрителя их v; значит снаружи доступно
    /// не больше SuitCount - v. Если заявлено больше — разница обязана быть ложью.
    /// </summary>
    public int GuaranteedLieCount(Side viewer)
    {
        int held = HandOf(viewer).CountOfRank(DeclaredRank);
        int availableElsewhere = BluffDeck.SuitCount - held;
        return System.Math.Max(0, LastPlayed.Count - availableElsewhere);
    }
}

/// <summary>
/// Базовое правило-модификатор. По умолчанию ничего не меняет — наследник переопределяет
/// нужные точки. Добавить усложнение = новый sealed-наследник + строка в <see cref="BluffRuleSet.ForDay"/>.
/// </summary>
public abstract class BluffRule
{
    public abstract string Name { get; }

    /// <summary>Однострочное описание для интро — игрок знает правила заранее (требование брифа).</summary>
    public abstract string Summary { get; }

    /// <summary>Объявлять можно любой ранг, а не следующий по кругу (сложнее читать).</summary>
    public virtual bool AllowsFreeDeclare => false;

    /// <summary>Скрыть от игрока число карт соперника (неполная информация).</summary>
    public virtual bool HidesOpponentCount => false;

    public virtual void OnTurnStart(BluffMatch match, Side actor) { }
    public virtual void OnDeclare(BluffMatch match, Side actor, int declaredRank, IReadOnlyList<Card> played) { }
    public virtual void OnChallengeResolved(BluffMatch match, Side challenger, bool wasLie) { }

    /// <summary>Дополнительные действия игрока, привнесённые правилом.</summary>
    public virtual IEnumerable<RuleAction> Actions(BluffMatch match) { yield break; }
}

// ---- Стартовые правила (каждое — небольшой sealed-класс) ----

/// <summary>Разовый сброс: убрать из своей руки одну лишнюю карту (помочь себе). [запрошено]</summary>
public sealed class CardSwapSelfRule : BluffRule
{
    public override string Name => "Ловкость рук";
    public override string Summary => "X — раз за партию убрать из своей руки лишнюю карту.";

    public override IEnumerable<RuleAction> Actions(BluffMatch match)
    {
        yield return new RuleAction
        {
            Id = "swap-self",
            Label = "X: убрать свою карту",
            Hotkey = 'x',
            Available = m => !m.ActionUsed("swap-self") && !m.PlayerHand.IsEmpty,
            Invoke = m => { m.DiscardRarest(Side.Player); m.MarkUsed("swap-self"); },
        };
    }
}

/// <summary>Разово подсунуть сопернику лишнюю карту (раздуть его руку — вред). [запрошено]</summary>
public sealed class CardPlantRule : BluffRule
{
    public override string Name => "Подброс";
    public override string Summary => "C — раз за партию подсунуть сопернику лишнюю карту.";

    public override IEnumerable<RuleAction> Actions(BluffMatch match)
    {
        yield return new RuleAction
        {
            Id = "plant",
            Label = "C: подсунуть карту сопернику",
            Hotkey = 'c',
            Available = m => !m.ActionUsed("plant"),
            Invoke = m => { m.PlantRandom(Side.Opponent); m.MarkUsed("plant"); },
        };
    }
}

/// <summary>Разовый подсмотр руки соперника (чтение; обостряется EyeImplant в движке).</summary>
public sealed class PeekRule : BluffRule
{
    public override string Name => "Чужой расклад";
    public override string Summary => "P — раз за партию подсмотреть руку соперника.";

    public override IEnumerable<RuleAction> Actions(BluffMatch match)
    {
        yield return new RuleAction
        {
            Id = "peek",
            Label = "P: подсмотреть руку соперника",
            Hotkey = 'p',
            Available = m => !m.ActionUsed("peek"),
            Invoke = m => { m.PeekActive = true; m.MarkUsed("peek"); },
        };
    }
}

/// <summary>Социальная помощь: забрать часть карт соперника-союзника себе (жертва). </summary>
public sealed class MercyPassRule : BluffRule
{
    public override string Name => "Пощада";
    public override string Summary => "M — раз за партию взять 2 карты соперника себе (дать ему уйти вперёд).";

    public override IEnumerable<RuleAction> Actions(BluffMatch match)
    {
        yield return new RuleAction
        {
            Id = "mercy",
            Label = "M: взять 2 карты соперника",
            Hotkey = 'm',
            Available = m => !m.ActionUsed("mercy") && !m.OpponentHand.IsEmpty,
            Invoke = m => { m.TakeFromOpponent(Side.Player, 2); m.MarkUsed("mercy"); },
        };
    }
}

/// <summary>Вольный объяв: можно называть любой ранг, не следующий по кругу.</summary>
public sealed class FreeDeclareRule : BluffRule
{
    public override string Name => "Вольный объяв";
    public override string Summary => "Можно объявлять любой ранг, не по порядку — соперника труднее читать.";
    public override bool AllowsFreeDeclare => true;
}

/// <summary>Туман: не видно, сколько карт у соперника (неполная информация).</summary>
public sealed class BlindHandRule : BluffRule
{
    public override string Name => "Туман";
    public override string Summary => "Не видно, сколько карт на руке у соперника.";
    public override bool HidesOpponentCount => true;
}

/// <summary>
/// Цена сомнения: ошибочный вызов «не верю» вознаграждает честного — тот сбрасывает
/// одну свою карту. Пример правила через хук события, а не флаг.
/// </summary>
public sealed class DoubtPenaltyRule : BluffRule
{
    public override string Name => "Цена сомнения";
    public override string Summary => "Ошибся с «не верю» — честный соперник сбрасывает одну карту.";

    public override void OnChallengeResolved(BluffMatch match, Side challenger, bool wasLie)
    {
        // Вызов был неверным (объявление оказалось правдой) — поощряем честного объявившего.
        if (!wasLie)
        {
            match.DiscardRarest(match.LastDeclarer);
        }
    }
}

/// <summary>
/// Набор активных правил партии. Композиция: движок спрашивает набор (флаги, хуки,
/// действия), а не отдельные правила. Эскалация состава — в <see cref="ForDay"/>.
/// </summary>
public sealed class BluffRuleSet
{
    private readonly List<BluffRule> rules;

    public BluffRuleSet(List<BluffRule> rules) => this.rules = rules;

    public IReadOnlyList<BluffRule> Rules => rules;

    public bool FreeDeclare
    {
        get { foreach (BluffRule r in rules) if (r.AllowsFreeDeclare) return true; return false; }
    }

    public bool HideOpponentCount
    {
        get { foreach (BluffRule r in rules) if (r.HidesOpponentCount) return true; return false; }
    }

    public void OnTurnStart(BluffMatch m, Side actor)
    {
        foreach (BluffRule r in rules) r.OnTurnStart(m, actor);
    }

    public void OnDeclare(BluffMatch m, Side actor, int rank, IReadOnlyList<Card> played)
    {
        foreach (BluffRule r in rules) r.OnDeclare(m, actor, rank, played);
    }

    public void OnChallengeResolved(BluffMatch m, Side challenger, bool wasLie)
    {
        foreach (BluffRule r in rules) r.OnChallengeResolved(m, challenger, wasLie);
    }

    public IEnumerable<RuleAction> ActionsFor(BluffMatch m)
    {
        foreach (BluffRule r in rules)
        {
            foreach (RuleAction a in r.Actions(m)) yield return a;
        }
    }

    public List<string> Summaries()
    {
        var list = new List<string>();
        foreach (BluffRule r in rules) list.Add($"{r.Name}: {r.Summary}");
        return list;
    }

    /// <summary>
    /// ЕДИНАЯ ТОЧКА ЭСКАЛАЦИИ: день/сложность → состав правил. Добавить усложнение к
    /// какому-то дню = одна строка здесь. <paramref name="ctx"/> зарезервирован под
    /// тонкую настройку от имплантов/отношений.
    /// </summary>
    public static BluffRuleSet ForDay(int day, ExperimentContext ctx)
    {
        // Моральное ядро категории «один на один» доступно всегда.
        var rules = new List<BluffRule> { new MercyPassRule() };

        if (day >= 2) rules.Add(new CardSwapSelfRule());
        if (day >= 3) { rules.Add(new CardPlantRule()); rules.Add(new DoubtPenaltyRule()); }
        if (day >= 4)
        {
            rules.Add(new FreeDeclareRule());
            rules.Add(new BlindHandRule());
            rules.Add(new PeekRule());
        }

        return new BluffRuleSet(rules);
    }
}
