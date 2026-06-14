using System.Collections.Generic;

/// <summary>
/// Чистая карточная модель дуэли «Верю / Не верю» (эксперимент 03). Без Unity-зависимостей,
/// поэтому тестируется отдельно — как <see cref="ExperimentSelector"/>. Движок
/// (<c>BluffExperiment</c>) и правила (<c>BluffRule</c>) работают поверх этих типов.
///
/// Ранг карты — индекс в <see cref="BluffDeck.RankNames"/> (0..RankCount-1). Для темпа
/// колода урезанная: 6 рангов × 4 масти = 24 карты, по 12 на сторону.
/// </summary>

/// <summary>Сторона дуэли. Часть контракта движка и правил.</summary>
public enum Side { None, Player, Opponent }

/// <summary>Масть. Отдельное имя (не «Suit»), чтобы не конфликтовать со свойством Card.Suit.</summary>
public enum CardSuit { Clubs, Diamonds, Hearts, Spades }

/// <summary>Одна карта. Неизменяемая модель.</summary>
public readonly struct Card
{
    public readonly CardSuit Suit;
    public readonly int Rank;

    public Card(CardSuit suit, int rank)
    {
        Suit = suit;
        Rank = rank;
    }

    public string RankName => BluffDeck.RankNames[Rank];

    public string SuitGlyph => Suit switch
    {
        CardSuit.Clubs => "♣",     // ♣
        CardSuit.Diamonds => "♦",  // ♦
        CardSuit.Hearts => "♥",    // ♥
        _ => "♠",                  // ♠
    };

    /// <summary>Красная масть (черви/бубны) — для отрисовки карт.</summary>
    public bool IsRed => Suit == CardSuit.Diamonds || Suit == CardSuit.Hearts;

    public override string ToString() => RankName + SuitGlyph;
}

/// <summary>Колода: построение, тасование (сидируемое) и раздача поровну.</summary>
public sealed class BluffDeck
{
    /// <summary>Имена рангов от младшего к старшему: В/Д/К/Т — валет/дама/король/туз.</summary>
    public static readonly string[] RankNames = { "9", "10", "В", "Д", "К", "Т" };
    public const int RankCount = 6;
    public const int SuitCount = 4;

    private readonly List<Card> cards = new();

    public BluffDeck(System.Random rng)
    {
        for (int r = 0; r < RankCount; r++)
        {
            for (int s = 0; s < SuitCount; s++)
            {
                cards.Add(new Card((CardSuit)s, r));
            }
        }

        // Фишер–Йейтс на сидируемом System.Random — раздача воспроизводима при отладке.
        for (int i = cards.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (cards[i], cards[j]) = (cards[j], cards[i]);
        }
    }

    /// <summary>Раздать колоду поровну двум рукам и опустошить колоду.</summary>
    public void Deal(Hand player, Hand opponent)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            (i % 2 == 0 ? player : opponent).Add(cards[i]);
        }
        cards.Clear();
    }
}

/// <summary>Рука одной стороны.</summary>
public sealed class Hand
{
    private readonly List<Card> cards = new();

    public IReadOnlyList<Card> Cards => cards;
    public int Count => cards.Count;
    public bool IsEmpty => cards.Count == 0;

    public void Add(Card card) => cards.Add(card);
    public void AddRange(IEnumerable<Card> range) => cards.AddRange(range);
    public void Remove(Card card) => cards.Remove(card);

    public Card RemoveAt(int index)
    {
        Card card = cards[index];
        cards.RemoveAt(index);
        return card;
    }

    /// <summary>Сколько карт указанного ранга на руке.</summary>
    public int CountOfRank(int rank)
    {
        int n = 0;
        foreach (Card card in cards)
        {
            if (card.Rank == rank) n++;
        }
        return n;
    }

    /// <summary>Есть ли карта требуемого ранга (честный ход / логика ИИ).</summary>
    public bool HasRank(int rank) => CountOfRank(rank) > 0;

    /// <summary>Индексы карт указанного ранга (для честного автоподбора).</summary>
    public List<int> IndicesOfRank(int rank)
    {
        var result = new List<int>();
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i].Rank == rank) result.Add(i);
        }
        return result;
    }

    public void SortByRank()
        => cards.Sort((a, b) => a.Rank != b.Rank ? a.Rank.CompareTo(b.Rank) : a.Suit.CompareTo(b.Suit));
}
