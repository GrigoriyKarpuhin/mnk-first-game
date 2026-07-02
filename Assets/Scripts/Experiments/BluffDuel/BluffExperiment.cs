using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Фазы дуэли «Верю / Не верю».</summary>
public enum BluffPhase
{
    Intro,
    PlayerDeclare,   // игрок выкладывает карты и объявляет ранг
    OpponentDecide,  // ИИ решает «Верю / Не верю» о ходе игрока
    OpponentDeclare, // ИИ выкладывает свои карты
    PlayerDecide,    // игрок решает «Верю / Не верю» о ходе ИИ
    Resolve,         // показ исхода вскрытия (пауза)
    Resolved,        // конец партии
}

/// <summary>
/// Эксперимент 03: «Верю / Не верю» — дуэль на блефе один на один.
///
/// Категория 5 (один на один). Сражение с NPC = психологическая дуэль: кладёшь карты
/// рубашкой вниз с объявлением ранга (правда или блеф), соперник решает «Верю/Не верю»;
/// на вскрытии проигравший забирает всю стопку. Побеждает первый, опустошивший руку, —
/// проигравший «под угрозой». Навык — чтение телла соперника и дедукция по своим картам,
/// а не скрытый бросок (требование брифа).
///
/// Движок реализует только базовую игру. Все усложнения — модификаторы <see cref="BluffRule"/>,
/// набираемые по дню в <see cref="BluffRuleSet.ForDay"/> (см. BluffRules.cs). Карты —
/// в BluffCards.cs. Спецификация — Design/EXPERIMENT_03_SPEC.md, плейтест — Design/PLAYTEST_03.md.
/// </summary>
public class BluffExperiment : MonoBehaviour
{
    private const string ExperimentId = "experiment.bluff-duel";

    [Header("Баланс прототипа")]
    [SerializeField] private int maxPerTurn = 3;        // карт за один ход
    [SerializeField] private float aiThinkTime = 1.1f;  // пауза «соперник думает»
    [SerializeField] private float revealTime = 2.0f;   // показ вскрытия
    [SerializeField] private int gratitudeReward = 3;   // отношения за пощаду
    [SerializeField] private int betrayalPenalty = 3;   // отношения за добивание союзника

    private ExperimentContext ctx;
    private BluffMatch match;
    private BluffPhase phase = BluffPhase.Intro;

    // Соперник.
    private NpcId opponentId;
    private string opponentName = "Соперник";
    private NpcDisposition disposition = NpcDisposition.Neutral;
    private BotTraits oppTraits;
    private bool hasCounterImplant;     // EyeImplant как счётчик-подсказка

    // Ввод объявления игрока.
    private int cursor;                 // курсор по руке
    private readonly List<int> tray = new(); // выбранные индексы карт
    private int claimRank;              // заявляемый ранг (для вольного объява)

    // Темп ИИ и показ исхода.
    private float aiTimer;
    private float opponentTell;         // 0..1: насколько уверенно выглядит соперник (выше — честнее)
    private string revealText = "";
    private bool tableFaceUp;           // во время показа вскрытия карты на столе раскрыты

    private string endText = "";
    private bool playerWon;
    private bool resultSubmitted;

    private Camera cam;
    private GUIStyle titleStyle, bodyStyle, centerStyle, cardStyle, cornerStyle;

    private void Awake()
    {
        ctx = RunState.BuildContext(ExperimentId);
        hasCounterImplant = ctx.HasImplant(ImplantId.EyeImplant);
        PickOpponent();
        oppTraits = BotTraits.Randomized();

        var rng = new System.Random();
        var rules = BluffRuleSet.ForDay(ctx.Day, ctx);
        match = new BluffMatch(rules, rng);

        var deck = new BluffDeck(rng);
        deck.Deal(match.PlayerHand, match.OpponentHand);
        match.PlayerHand.SortByRank();
        match.OpponentHand.SortByRank();
        match.RequiredRank = 0;
        match.Turn = Side.Player;
        claimRank = match.RequiredRank;

        BuildTable();
    }

    /// <summary>Выбрать соперника из живых NPC; отношение задаёт его расположение.</summary>
    private void PickOpponent()
    {
        // Конкурент — соперник по умолчанию; если мёртв, берём программиста.
        if (ctx.IsAlive(NpcId.Competitor)) opponentId = NpcId.Competitor;
        else if (ctx.IsAlive(NpcId.Programmer)) opponentId = NpcId.Programmer;
        else opponentId = NpcId.Competitor; // деградация: всё равно играем

        opponentName = opponentId == NpcId.Programmer ? "Программист" : "Конкурент";
        disposition = Disposition.For(ctx.RelationshipTo(opponentId));
    }

    private void Update()
    {
        HandleGlobalInput();

        switch (phase)
        {
            case BluffPhase.PlayerDeclare: UpdatePlayerDeclare(); break;
            case BluffPhase.OpponentDecide: UpdateOpponentDecide(); break;
            case BluffPhase.OpponentDeclare: UpdateOpponentDeclare(); break;
            case BluffPhase.PlayerDecide: UpdatePlayerDecide(); break;
            case BluffPhase.Resolve: UpdateResolve(); break;
        }
    }

    private void HandleGlobalInput()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        if (phase == BluffPhase.Intro && kb.spaceKey.wasPressedThisFrame)
        {
            BeginTurn();
            return;
        }

        if (phase == BluffPhase.Resolved)
        {
            if (kb.eKey.wasPressedThisFrame) RunState.ReturnToPrison();
            else if (kb.rKey.wasPressedThisFrame)
                RestartOrRetryAfterResult();
        }
    }

    private void RestartOrRetryAfterResult()
    {
        if (playerWon) UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        else RunState.RestartRunInPrison();
    }

    // ---- Начало хода ----

    private void BeginTurn()
    {
        if (CheckWinner()) return;

        match.Rules.OnTurnStart(match, match.Turn);
        if (match.Turn == Side.Player)
        {
            match.PlayerHand.SortByRank();
            cursor = Mathf.Clamp(cursor, 0, Mathf.Max(0, match.PlayerHand.Count - 1));
            tray.Clear();
            claimRank = match.RequiredRank;
            phase = BluffPhase.PlayerDeclare;
        }
        else
        {
            aiTimer = aiThinkTime;
            phase = BluffPhase.OpponentDeclare;
        }
    }

    // ---- Объявление игрока ----

    private void UpdatePlayerDeclare()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null || match.PlayerHand.Count == 0) return;

        if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)
            cursor = (cursor - 1 + match.PlayerHand.Count) % match.PlayerHand.Count;
        if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)
            cursor = (cursor + 1) % match.PlayerHand.Count;

        // Добавить/убрать карту под курсором из выкладки.
        if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
        {
            if (tray.Contains(cursor)) tray.Remove(cursor);
            else if (tray.Count < maxPerTurn) tray.Add(cursor);
        }
        if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame)
            tray.Remove(cursor);

        // Игрок всегда сам выбирает заявляемый ранг (Q/E), независимо от правил.
        if (kb.qKey.wasPressedThisFrame) claimRank = (claimRank - 1 + BluffDeck.RankCount) % BluffDeck.RankCount;
        if (kb.eKey.wasPressedThisFrame) claimRank = (claimRank + 1) % BluffDeck.RankCount;

        TryRuleActions(kb);

        if ((kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) && tray.Count > 0)
            CommitPlayerDeclaration();
    }

    private void CommitPlayerDeclaration()
    {
        int rank = claimRank; // игрок всегда заявляет выбранный им ранг

        // Снять выбранные карты (по убыванию индекса, чтобы не сбить порядок).
        var indices = new List<int>(tray);
        indices.Sort();
        indices.Reverse();
        var played = new List<Card>();
        foreach (int i in indices) played.Add(match.PlayerHand.RemoveAt(i));
        played.Reverse(); // вернуть исходный порядок выбора

        RegisterDeclaration(Side.Player, rank, played);

        aiTimer = aiThinkTime;
        phase = BluffPhase.OpponentDecide;
    }

    private void RegisterDeclaration(Side actor, int rank, List<Card> played)
    {
        match.LastDeclarer = actor;
        match.DeclaredRank = rank;
        match.LastPlayed.Clear();
        match.LastPlayed.AddRange(played);
        match.Rules.OnDeclare(match, actor, rank, played);
    }

    // ---- Реакция ИИ на ход игрока ----

    private void UpdateOpponentDecide()
    {
        aiTimer -= Time.deltaTime;
        if (aiTimer > 0f) return;

        bool challenge = OpponentChallengesPlayer();
        if (challenge) ResolveChallenge(Side.Opponent);
        else AcceptDeclaration(Side.Opponent);
    }

    /// <summary>Решение ИИ вскрывать ли игрока: дедукция по своей руке + подозрение от стиля.</summary>
    private bool OpponentChallengesPlayer()
    {
        if (match.GuaranteedLieCount(Side.Opponent) > 0) return true; // доказуемая ложь по картам ИИ

        float suspicion = 0.12f + 0.12f * (match.LastPlayed.Count - 1);
        if (disposition == NpcDisposition.Hostile) suspicion += 0.25f;
        else if (disposition == NpcDisposition.Friendly) suspicion -= 0.10f;
        suspicion = Mathf.Clamp01(suspicion);
        return match.Rng.NextDouble() < suspicion;
    }

    // ---- Объявление ИИ ----

    private void UpdateOpponentDeclare()
    {
        aiTimer -= Time.deltaTime;
        if (aiTimer > 0f) return;

        int rank = ChooseOpponentClaimRank();
        var played = ChooseOpponentCards(rank, out bool lying);
        RegisterDeclaration(Side.Opponent, rank, played);

        // Телл: честный выглядит уверенно; блефующий — нервно, но навык маскирует.
        opponentTell = lying
            ? Mathf.Clamp01(0.30f + oppTraits.Skill * 0.45f + Random.Range(-0.12f, 0.12f))
            : Mathf.Clamp01(0.82f + Random.Range(-0.10f, 0.10f));

        phase = BluffPhase.PlayerDecide;
    }

    private int ChooseOpponentClaimRank()
    {
        if (!match.Rules.FreeDeclare) return match.RequiredRank;

        // Вольный объяв: называем ранг, которого больше всего на руке (чаще честно).
        int best = 0, bestCount = -1;
        for (int r = 0; r < BluffDeck.RankCount; r++)
        {
            int c = match.OpponentHand.CountOfRank(r);
            if (c > bestCount) { bestCount = c; best = r; }
        }
        return best;
    }

    private List<Card> ChooseOpponentCards(int rank, out bool lying)
    {
        var result = new List<Card>();
        List<int> honest = match.OpponentHand.IndicesOfRank(rank);

        if (honest.Count > 0)
        {
            // Честный ход: сбрасываем карты нужного ранга (агрессивный — больше за раз).
            int want = disposition == NpcDisposition.Hostile ? maxPerTurn : 2;
            int n = Mathf.Min(honest.Count, want);
            TakeOpponentCards(honest, n, result);
            lying = false;
        }
        else
        {
            // Блеф: нечем ходить честно — кладём чужие карты (предпочитая старшие, их труднее сбросить).
            lying = true;
            int n = disposition == NpcDisposition.Hostile ? Random.Range(1, 3) : 1;
            n = Mathf.Min(n, match.OpponentHand.Count, maxPerTurn);
            var byRankDesc = new List<int>();
            for (int i = 0; i < match.OpponentHand.Count; i++) byRankDesc.Add(i);
            byRankDesc.Sort((a, b) => match.OpponentHand.Cards[b].Rank.CompareTo(match.OpponentHand.Cards[a].Rank));
            TakeOpponentCards(byRankDesc, n, result);
        }
        return result;
    }

    /// <summary>Снять n карт по списку индексов (по убыванию, чтобы индексы не сбивались).</summary>
    private void TakeOpponentCards(List<int> indices, int n, List<Card> into)
    {
        var chosen = indices.GetRange(0, Mathf.Min(n, indices.Count));
        chosen.Sort();
        chosen.Reverse();
        foreach (int i in chosen) into.Add(match.OpponentHand.RemoveAt(i));
    }

    // ---- Решение игрока ----

    private void UpdatePlayerDecide()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        TryRuleActions(kb);

        if (kb.vKey.wasPressedThisFrame) AcceptDeclaration(Side.Player);
        else if (kb.nKey.wasPressedThisFrame) ResolveChallenge(Side.Player);
    }

    // ---- Разрешение ----

    /// <summary>
    /// Поверил: карты уходят в сброс (объявивший от них избавился). Показываем вскрытие
    /// и верность доверия (правильно поверил или попался на блеф), как и при «не верю».
    /// </summary>
    private void AcceptDeclaration(Side believer)
    {
        Side declarer = match.LastDeclarer;
        bool wasLie = match.LastDeclarationWasLie();
        string claim = $"{match.LastPlayed.Count}× {BluffDeck.RankNames[match.DeclaredRank]}";

        if (believer == Side.Player)
            revealText = wasLie
                ? $"Ты поверил — а это был блеф ({claim})! Карты ушли в сброс."
                : $"Ты верно поверил ({claim}). Карты в сброс.";
        else
            revealText = $"{opponentName} поверил тебе ({claim}). Карты в сброс.";

        match.AdvanceRank();
        match.Turn = believer;           // поверивший ходит следующим
        tableFaceUp = true;              // раскрыть карты на показе
        aiTimer = revealTime;
        phase = BluffPhase.Resolve;
        // Карты не возвращаются ни в одну руку — после показа они исчезают (сброшены).
    }

    /// <summary>
    /// Вскрытие поштучно (без общей стопки): пойман на блефе — объявивший забирает СВОИ
    /// карты назад; зря усомнился — карты честного объявившего уходят усомнившемуся.
    /// </summary>
    private void ResolveChallenge(Side challenger)
    {
        bool wasLie = match.LastDeclarationWasLie();
        Side declarer = match.LastDeclarer;
        string claim = $"{match.LastPlayed.Count}× {BluffDeck.RankNames[match.DeclaredRank]}";
        string declarerName = declarer == Side.Player ? "Ты" : opponentName;

        if (wasLie)
        {
            match.HandOf(declarer).AddRange(match.LastPlayed); // забирает свои карты обратно
            match.HandOf(declarer).SortByRank();
            match.Turn = challenger;        // верно усомнившийся ходит следующим
            revealText = challenger == Side.Player
                ? $"Ты раскусил блеф ({claim})! {opponentName} забирает карты обратно."
                : $"{opponentName} раскусил твой блеф ({claim})! Забираешь карты обратно.";
        }
        else
        {
            match.HandOf(challenger).AddRange(match.LastPlayed); // карты честного уходят усомнившемуся
            match.HandOf(challenger).SortByRank();
            match.Turn = declarer;          // честный объявивший продолжает
            revealText = challenger == Side.Player
                ? $"Зря не поверил ({claim}) — это была правда. Берёшь его карты."
                : $"{opponentName} зря усомнился ({claim}) — это была правда. Берёт твои карты.";
        }

        match.Rules.OnChallengeResolved(match, challenger, wasLie);
        match.RequiredRank = 0;            // после вскрытия — свежий розыгрыш
        tableFaceUp = true;                // показать вскрытые карты
        aiTimer = revealTime;
        phase = BluffPhase.Resolve;
    }

    private void UpdateResolve()
    {
        aiTimer -= Time.deltaTime;
        if (aiTimer > 0f) return;
        tableFaceUp = false;
        match.LastPlayed.Clear();
        if (CheckWinner()) return;
        BeginTurn(); // ведёт match.Turn, выставленный при вскрытии
    }

    /// <summary>Если чья-то рука пуста — партия окончена. Возвращает true, если победитель найден.</summary>
    private bool CheckWinner()
    {
        if (match.PlayerHand.IsEmpty) { Finish(true); return true; }
        if (match.OpponentHand.IsEmpty) { Finish(false); return true; }
        return false;
    }

    private void Finish(bool won)
    {
        if (phase == BluffPhase.Resolved) return;
        playerWon = won;

        string reason = won
            ? "ты первым избавился от всех карт."
            : "соперник первым избавился от всех карт.";
        endText = won
            ? $"Победа: {reason} {opponentName} — под угрозой."
            : $"Поражение: {reason} Под угрозой — ты.";

        phase = BluffPhase.Resolved;
        SubmitResult();
    }

    // ---- Контракт ----

    private void SubmitResult()
    {
        if (resultSubmitted) return;
        resultSubmitted = true;

        bool oppAlly = disposition == NpcDisposition.Friendly;
        bool mercyUsed = match.ActionUsed("mercy");
        bool plantUsed = match.ActionUsed("plant");

        NpcAction action;
        int delta = 0;
        var flags = new List<string>();

        if (playerWon)
        {
            // Победа = соперник погибает. Нюанс: добил союзника или устранил соперника.
            if (oppAlly)
            {
                action = NpcAction.Betrayed;
                delta = -betrayalPenalty;
                flags.Add("bluff-duel.betrayed-ally");
            }
            else
            {
                action = NpcAction.Harmed;
                flags.Add("bluff-duel.buried-rival");
            }
            if (plantUsed) flags.Add("bluff-duel.used-sabotage");
        }
        else
        {
            // Поражение = игрок под угрозой. Пощада превращает проигрыш в осознанную жертву.
            if (mercyUsed)
            {
                action = NpcAction.Helped;
                delta = gratitudeReward;
                flags.Add("bluff-duel.spared-ally");
                flags.Add("bluff-duel.threw-game");
            }
            else
            {
                action = NpcAction.Ignored;
            }
        }

        var result = new ExperimentResult
        {
            ExperimentId = ExperimentId,
            PlayerSurvived = playerWon,
            PlayerWon = playerWon,
        };
        result.Record(opponentId, survived: !playerWon, action);
        if (delta != 0) result.RelationshipDeltas[opponentId] = delta;
        foreach (string f in flags) result.Flags.Add(f);

        // Награда за победу: имплант-счётчик, если его ещё нет.
        if (playerWon && !hasCounterImplant)
        {
            result.OfferedImplant = ImplantId.EyeImplant;
            result.ImplantAccepted = true;
        }

        RunState.SubmitResult(result);
    }

    // ---- Действия правил ----

    private void TryRuleActions(Keyboard kb)
    {
        foreach (RuleAction a in match.Rules.ActionsFor(match))
        {
            if (a.Available != null && !a.Available(match)) continue;
            if (HotkeyPressed(kb, a.Hotkey)) a.Invoke(match);
        }
    }

    private static bool HotkeyPressed(Keyboard kb, char c)
    {
        return c switch
        {
            'x' => kb.xKey.wasPressedThisFrame,
            'c' => kb.cKey.wasPressedThisFrame,
            'p' => kb.pKey.wasPressedThisFrame,
            'm' => kb.mKey.wasPressedThisFrame,
            _ => false,
        };
    }

    // ---- Мир ----

    private void BuildTable()
    {
        cam = Camera.main;
        if (cam == null)
        {
            var camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            cam = camObj.AddComponent<Camera>();
        }
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        cam.transform.position = new Vector3(0f, 0f, -10f);
        cam.backgroundColor = new Color(0.13f, 0.17f, 0.14f); // холодный зелёный подтон (ART_STYLE)

        BuildFloorAndWalls();

        // Спрайт соперника напротив (стиль временный, фолбэк — круг).
        string spriteBase = opponentId == NpcId.Programmer ? "npc_programmer" : "girl";
        Color tint = HasArt(spriteBase) ? Color.white : new Color(0.9f, 0.3f, 0.4f);
        RaceVisuals.Character(opponentName, spriteBase, new Vector2(0f, 3.0f),
            WorldMetrics.CharacterScale, tint, 6);
    }

    private void BuildFloorAndWalls()
    {
        Color floorA = new(0.24f, 0.30f, 0.25f);
        Color floorB = new(0.20f, 0.26f, 0.21f);
        Color wall = new(0.16f, 0.20f, 0.17f);
        const int minX = -8, maxX = 8, minY = -5, maxY = 5;

        bool hasArt = HasArt("race_dirt");
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                bool border = x == minX || x == maxX || y == minY || y == maxY;
                if (border)
                {
                    RaceVisuals.Art("Wall", "wall_top", new Vector2(x, y),
                        Vector2.one * WorldMetrics.TileOverlap, hasArt ? Color.white : wall, -8);
                }
                else
                {
                    Color f = (x + y) % 2 == 0 ? floorA : floorB;
                    RaceVisuals.Art("Floor", "race_dirt", new Vector2(x, y),
                        Vector2.one * WorldMetrics.TileOverlap, f, -20);
                }
            }
        }
    }

    private static bool HasArt(string spriteName) => Resources.Load<Sprite>("Sprites/" + spriteName) != null;

    // ---- UI ----

    private void OnGUI()
    {
        EnsureStyles();

        if (phase == BluffPhase.Intro) { DrawIntro(); return; }

        DrawHud();
        DrawOpponentArea();
        DrawTable();
        DrawPlayerHand();
        DrawActionBar();

        float promptY = Screen.height * 0.56f; // текст решения/показа — под столом, над рукой
        switch (phase)
        {
            case BluffPhase.PlayerDeclare: DrawCenter(promptY, DeclarePrompt()); break;
            case BluffPhase.OpponentDecide: DrawThinking(promptY, $"{opponentName} обдумывает твоё объявление..."); break;
            case BluffPhase.OpponentDeclare: DrawThinking(promptY, $"{opponentName} ходит..."); break;
            case BluffPhase.PlayerDecide: DrawDecidePanel(promptY); break;
            case BluffPhase.Resolve: DrawReveal(promptY); break;
        }

        if (phase == BluffPhase.Resolved)
            ExperimentSummaryView.Draw("E — вернуться в тюрьму, R — переиграть", endText);
    }

    /// <summary>Текст ожидания + движущийся прогресс-бар (соперник думает).</summary>
    private void DrawThinking(float y, string text)
    {
        DrawCenter(y, text);
        DrawProgress(y + 30f, 1f - Mathf.Clamp01(aiTimer / Mathf.Max(0.01f, aiThinkTime)));
    }

    /// <summary>Показ вскрытия: итог + движущийся прогресс-бар до продолжения.</summary>
    private void DrawReveal(float y)
    {
        DrawCenter(y, revealText);
        DrawProgress(y + 30f, 1f - Mathf.Clamp01(aiTimer / Mathf.Max(0.01f, revealTime)));
    }

    private void DrawProgress(float y, float t)
    {
        float bx = Screen.width / 2f - 150f;
        GUI.color = new Color(0.15f, 0.18f, 0.16f);
        GUI.DrawTexture(new Rect(bx, y, 300f, 10f), Texture2D.whiteTexture);
        GUI.color = new Color(0.51f, 0.92f, 0.59f);
        GUI.DrawTexture(new Rect(bx, y, 300f * Mathf.Clamp01(t), 10f), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    private void DrawIntro()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Дуэль на блефе против: {opponentName} ({DispositionWord()}).");
        sb.AppendLine("Кладёшь карты рубашкой вниз и объявляешь ранг (правда или блеф). Соперник —");
        sb.AppendLine("«Верю» или «Не верю». Поверит — карты в сброс (ты от них избавился).");
        sb.AppendLine("Поймает на блефе — заберёшь их назад. Зря усомнится — твои карты уйдут ему.");
        sb.AppendLine("Побеждает первый, кто избавится от всех карт. Проигравший — под угрозой.");
        sb.AppendLine();
        sb.AppendLine("Активные правила сегодня:");
        foreach (string s in match.Rules.Summaries()) sb.AppendLine("• " + s);
        if (hasCounterImplant) sb.AppendLine("• Имплант-счётчик: видишь оценку блефа соперника.");

        Rect box = new(40, 60, Screen.width - 80, Screen.height - 160);
        ImguiTheme.Panel(box);
        GUI.Label(new Rect(box.x + 24, box.y + 20, box.width - 48, box.height - 80), sb.ToString(), bodyStyle);
        GUI.Label(new Rect(box.x + 24, box.y + box.height - 44, box.width - 48, 30),
            "Управление: ◄►/AD — курсор, ▲/Space — выбрать карту, Q/E — выбрать ранг, Enter — объявить, V — верю, N — не верю.   SPACE — начать.",
            bodyStyle);
    }

    private void DrawHud()
    {
        string rankInfo = match.Rules.FreeDeclare ? "вольный объяв" : $"ранг раунда: {BluffDeck.RankNames[match.RequiredRank]}";
        GUI.Label(new Rect(16, 12, 700, 26), $"День {ctx.Day} · {rankInfo}", titleStyle);

        string oppCount = match.Rules.HideOpponentCount && !match.PeekActive ? "?" : match.OpponentHand.Count.ToString();
        GUI.Label(new Rect(16, 40, 700, 24),
            $"{opponentName}: карт {oppCount}    ·    Ты: карт {match.PlayerHand.Count}", bodyStyle);
    }

    private void DrawOpponentArea()
    {
        // Рука соперника наверху: рубашки, либо вскрытая (Peek), либо «?» при тумане.
        int show = match.OpponentHand.Count;
        bool reveal = match.PeekActive;
        bool hideCount = match.Rules.HideOpponentCount && !match.PeekActive;
        if (hideCount) show = Mathf.Min(show, 8);

        float w = 36f, gap = 5f, h = 50f;
        float total = show * w + (show - 1) * gap;
        float x0 = Screen.width / 2f - total / 2f;
        float y = 64f;
        for (int i = 0; i < show; i++)
        {
            var rect = new Rect(x0 + i * (w + gap), y, w, h);
            if (reveal && i < match.OpponentHand.Count) DrawCard(rect, match.OpponentHand.Cards[i], true);
            else DrawCardBack(rect);
        }
    }

    /// <summary>Карты текущего хода — в центре стола (рубашкой, либо раскрыты на показе).</summary>
    private void DrawTable()
    {
        float cx = Screen.width / 2f;
        float cy = Screen.height * 0.30f;
        int shown = match.LastPlayed.Count;
        float w = 60f, gap = 8f, h = 84f;
        float totalW = shown * w + Mathf.Max(0, shown - 1) * gap;
        for (int i = 0; i < shown; i++)
        {
            var rect = new Rect(cx - totalW / 2f + i * (w + gap), cy, w, h);
            if (tableFaceUp) DrawCard(rect, match.LastPlayed[i], true);
            else DrawCardBack(rect);
        }

        string claim = match.LastDeclarer == Side.None || shown == 0
            ? "На столе пусто"
            : $"{(match.LastDeclarer == Side.Player ? "Ты" : opponentName)} заявил: {shown}× {BluffDeck.RankNames[match.DeclaredRank]}";
        GUI.Label(new Rect(cx - 220f, cy + h + 6f, 440f, 24f), claim, centerStyle);
    }

    private void DrawPlayerHand()
    {
        match.PlayerHand.SortByRank();
        int n = match.PlayerHand.Count;
        if (n == 0) return;

        bool declaring = phase == BluffPhase.PlayerDeclare;
        float w = 54f, gap = 6f, cardH = 88f;
        float total = n * w + (n - 1) * gap;
        float x0 = Mathf.Max(12f, Screen.width / 2f - total / 2f);
        // В свой ход рука видна целиком у низа; не в свой — уезжает вниз, видны только шапки.
        float y = declaring ? Screen.height - cardH - 16f : Screen.height - 30f;

        for (int i = 0; i < n; i++)
        {
            var rect = new Rect(x0 + i * (w + gap), y, w, cardH);
            bool inTray = declaring && tray.Contains(i);
            if (inTray) rect.y -= 22f; // выбранные приподняты
            DrawCard(rect, match.PlayerHand.Cards[i], true);
            if (declaring && i == cursor)
            {
                GUI.color = new Color(0.51f, 0.92f, 0.59f); // зелёный курсор (ART_STYLE)
                GUI.DrawTexture(new Rect(rect.x - 3, rect.y - 4, rect.width + 6, 4), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
        }
    }

    private void DrawActionBar()
    {
        if (phase != BluffPhase.PlayerDeclare && phase != BluffPhase.PlayerDecide) return;
        // Список действий правил — слева сверху, вертикально (не перекрывает руку и стол).
        float x = 16f, y = 92f;
        foreach (RuleAction a in match.Rules.ActionsFor(match))
        {
            bool avail = a.Available == null || a.Available(match);
            GUI.color = avail ? Color.white : new Color(1f, 1f, 1f, 0.4f);
            GUI.Label(new Rect(x, y, 360, 22), "[" + a.Label + "]", bodyStyle);
            y += 24f;
        }
        GUI.color = Color.white;
    }

    private string DeclarePrompt()
    {
        return $"Объяви {tray.Count}× {BluffDeck.RankNames[claimRank]}  (Q/E — сменить ранг) — Enter. " +
               $"Выбрано карт: {tray.Count}/{maxPerTurn}.";
    }

    private void DrawDecidePanel(float y)
    {
        // Чтение соперника: качественный телл всегда; число — только с имплантом.
        string tellWord = opponentTell > 0.7f ? "держится уверенно"
            : opponentTell > 0.45f ? "слегка напряжён" : "явно нервничает";
        DrawCenter(y, $"{opponentName} {tellWord}. Верю (V) или Не верю (N)?");

        // Полоса уверенности (телл) — статичная для текущего объявления.
        float bx = Screen.width / 2f - 150f, by = y + 30f;
        GUI.Label(new Rect(bx, by - 20f, 300f, 18f), "Уверенность соперника:", bodyStyle);
        GUI.color = new Color(0.15f, 0.18f, 0.16f);
        GUI.DrawTexture(new Rect(bx, by, 300f, 14f), Texture2D.whiteTexture);
        GUI.color = new Color(0.35f, 0.77f, 0.44f);
        GUI.DrawTexture(new Rect(bx, by, 300f * opponentTell, 14f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // Дедукция по своим картам (всегда доступна — это навык, не RNG).
        int guaranteed = match.GuaranteedLieCount(Side.Player);
        if (guaranteed > 0)
            DrawCenter(by + 24f, $"Дедукция: минимум {guaranteed} из заявленных карт — точно ложь!");

        // Имплант-счётчик: численная оценка блефа.
        if (hasCounterImplant)
        {
            int bluffPct = Mathf.RoundToInt(Mathf.Clamp01(0.88f - opponentTell) * 100f);
            DrawCenter(by + 46f, $"Счётчик-имплант: вероятность блефа ≈ {bluffPct}%.");
        }
    }

    // ---- Примитивы карт ----

    private void DrawCard(Rect rect, Card card, bool faceUp)
    {
        if (!faceUp) { DrawCardBack(rect); return; }
        GUI.color = new Color(0.93f, 0.93f, 0.88f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = new Color(0.06f, 0.09f, 0.07f);
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 2f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        Color ink = card.IsRed ? new Color(0.75f, 0.18f, 0.18f) : new Color(0.08f, 0.10f, 0.09f);
        // Угловая метка сверху — видна, даже когда карта «выглядывает» шапкой снизу.
        cornerStyle.normal.textColor = ink;
        GUI.Label(new Rect(rect.x + 4f, rect.y + 2f, rect.width - 6f, 18f),
            card.RankName + card.SuitGlyph, cornerStyle);
        // Крупная метка по центру (видна, когда карта на виду целиком).
        if (rect.height > 40f)
        {
            cardStyle.normal.textColor = ink;
            GUI.Label(new Rect(rect.x, rect.y + rect.height / 2f - 14f, rect.width, 30f),
                card.RankName + card.SuitGlyph, cardStyle);
        }
    }

    private void DrawCardBack(Rect rect)
    {
        GUI.color = new Color(0.17f, 0.30f, 0.20f); // зелёная рубашка
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = new Color(0.34f, 0.50f, 0.36f);
        GUI.DrawTexture(new Rect(rect.x + 4, rect.y + 4, rect.width - 8, rect.height - 8), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    private string DispositionWord() => disposition switch
    {
        NpcDisposition.Hostile => "враждебен",
        NpcDisposition.Friendly => "дружелюбен",
        _ => "нейтрален",
    };

    private void DrawCenter(float y, string text)
        => GUI.Label(new Rect(Screen.width / 2f - 360f, y, 720f, 30f), text, centerStyle);

    private void EnsureStyles()
    {
        ImguiTheme.Apply();
        titleStyle = ImguiTheme.Title;
        bodyStyle = ImguiTheme.Body;
        centerStyle = ImguiTheme.Centered;
        cardStyle ??= new GUIStyle(GUI.skin.label)
        {
            font = UIKit.Font, fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
        };
        cornerStyle ??= new GUIStyle(GUI.skin.label)
        {
            font = UIKit.Font, fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft,
        };
    }
}
