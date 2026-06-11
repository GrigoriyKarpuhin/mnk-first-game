using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum MemoryPhase
{
    Intro,
    Roaming,
    Testing,
    Helping,
    Resolved,
}

public enum PrisonerState
{
    Playing,
    Struggling,
    Done,
    Failed,
}

/// <summary>
/// Эксперимент 02: «Протокол памяти» — комната с пультами.
///
/// В комнате у пультов сидят заключённые (игрок + NPC). У каждого свой тест на память:
/// нужно повторять вспыхивающие последовательности символов. Игрок ходит по комнате
/// (WASD), подходит к своему пульту и проходит тест. NPC проходят свои тесты сами —
/// успех каждого раунда решается их характеристиками и общим анлаком (Luck.Roll(Skill)).
/// Слабые застревают и ждут помощи.
///
/// Помогать другим можно ТОЛЬКО после того, как игрок прошёл свой тест: тогда он
/// подходит к застрявшему и проходит короткий тест за него. Спасённый именованный NPC
/// благодарен (рост отношений). Всё ограничено общим таймером.
///
/// Спецификация — Design/EXPERIMENT_02_SPEC.md, форма плейтеста — Design/PLAYTEST_02.md.
/// </summary>
public class MemoryExperiment : MonoBehaviour
{
    private const string ExperimentId = "experiment.memory-protocol";
    private const int SymbolCount = 4;

    [Header("Prototype Balance")]
    [SerializeField] private float totalTime = 70f;
    [SerializeField] private float playerSpeed = 5f;
    [SerializeField] private float symbolOnTime = 0.55f;
    [SerializeField] private int ownRounds = 4;
    [SerializeField] private int npcRoundsNeeded = 3;
    [Header("Мини-игра помощи (ритм)")]
    [SerializeField] private int helpHitsNeeded = 6;
    [SerializeField] private float noteFallSpeed = 0.55f;
    [SerializeField] private float noteSpawnInterval = 0.6f;
    [SerializeField] private int gratitudeReward = 3;

    private static readonly Color[] SymbolColors =
    {
        new(0.85f, 0.25f, 0.25f),
        new(0.30f, 0.80f, 0.40f),
        new(0.30f, 0.55f, 0.95f),
        new(0.92f, 0.80f, 0.25f),
    };

    private static readonly Color[] GenericColors =
    {
        new(0.70f, 0.45f, 0.85f),
        new(0.40f, 0.75f, 0.80f),
        new(0.80f, 0.70f, 0.35f),
    };

    private sealed class Prisoner
    {
        public string Name;
        public NpcId Id;
        public bool IsNamed;
        public BotTraits Traits;
        public Vector2 ConsolePos;
        public GameObject Avatar;
        public Color BaseColor;

        public PrisonerState State = PrisonerState.Playing;
        public int RoundsDone;
        public float NextRoundAt;
        public float RoundTime;
        public bool HelpedByPlayer;
        public bool Encountered;
    }

    private readonly List<Prisoner> npcs = new();
    private Camera cam;
    private Transform playerAvatar;
    private Vector2 playerConsole;

    private MemoryPhase phase = MemoryPhase.Intro;
    private int introPage;
    private float timeLeft;
    private bool playerDone;
    private bool playerSurvived;

    // Свой тест на память.
    private int testRound;
    private readonly List<int> sequence = new();
    private int recallIndex;
    private bool testShowing;
    private int highlightedTile = -1;
    private readonly float[] tileFlashUntil = new float[SymbolCount];
    private float wrongFlashUntil;

    // Мини-игра помощи: ритм с падающими нотами по 4 дорожкам.
    private sealed class FallingNote { public int Lane; public float P; }
    private readonly List<FallingNote> notes = new();
    private Prisoner helpTarget;
    private float helpProgress;
    private int helpCombo;
    private float noteSpawnTimer;
    private readonly float[] laneFlashUntil = new float[SymbolCount];
    private readonly bool[] laneFlashGood = new bool[SymbolCount];

    // Подсказка взаимодействия (вычисляется в Roaming).
    private string promptText = "";
    private Prisoner promptHelpTarget;

    private string endText = "";
    private GUIStyle titleStyle;
    private GUIStyle bodyStyle;
    private GUIStyle centerStyle;

    private void Awake()
    {
        timeLeft = totalTime;
        BuildRoom();
    }

    private void Update()
    {
        HandleGlobalInput();

        bool active = phase == MemoryPhase.Roaming || phase == MemoryPhase.Testing ||
                      phase == MemoryPhase.Helping;
        if (active)
        {
            timeLeft -= Time.deltaTime;
            if (timeLeft <= 0f)
            {
                Resolve();
                return;
            }
            UpdateNpcs();
        }

        if (phase == MemoryPhase.Roaming) UpdateRoaming();
        else if (phase == MemoryPhase.Testing) UpdateTesting();
        else if (phase == MemoryPhase.Helping) UpdateHelping();

        UpdateAvatarColors();
    }

    private void HandleGlobalInput()
    {
        if (Keyboard.current == null) return;

        if (phase == MemoryPhase.Intro && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            introPage++;
            if (introPage >= 2) StartOwnTest(); // сразу играем свой тест
            return;
        }

        if (phase == MemoryPhase.Resolved)
        {
            if (Keyboard.current.eKey.wasPressedThisFrame) RunState.ReturnToPrison();
            else if (Keyboard.current.rKey.wasPressedThisFrame)
                UnityEngine.SceneManagement.SceneManager.LoadScene(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }
    }

    // ---- NPC: проходят свои тесты по характеристикам и анлаку ----

    private void UpdateNpcs()
    {
        foreach (Prisoner npc in npcs)
        {
            if (npc.State != PrisonerState.Playing) continue;
            if (Time.time < npc.NextRoundAt) continue;

            // Формальное правило: пройти npcRoundsNeeded раундов. Успех раунда — по навыку и анлаку.
            if (Luck.Roll(npc.Traits.Skill))
            {
                npc.RoundsDone++;
                if (npc.RoundsDone >= npcRoundsNeeded) npc.State = PrisonerState.Done;
                else npc.NextRoundAt = Time.time + npc.RoundTime;
            }
            else
            {
                npc.State = PrisonerState.Struggling; // застрял, ждёт помощи
            }
        }
    }

    // ---- Перемещение игрока и взаимодействие ----

    private void UpdateRoaming()
    {
        Vector3 pos = playerAvatar.position;
        Vector2 input = Vector2.zero;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) input.y += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) input.y -= 1f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) input.x -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) input.x += 1f;
        }
        if (input.sqrMagnitude > 1f) input.Normalize();
        pos += (Vector3)(input * playerSpeed * Time.deltaTime);
        pos.x = Mathf.Clamp(pos.x, -7.2f, 7.2f);
        pos.y = Mathf.Clamp(pos.y, -4.2f, 3.4f);
        playerAvatar.position = pos;

        ResolvePrompt(pos);

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame && promptHelpTarget != null)
            StartHelp(promptHelpTarget);

        // После своего теста можно уйти из комнаты досрочно.
        if (playerDone && Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
            Resolve();
    }

    private void ResolvePrompt(Vector3 pos)
    {
        promptText = "";
        promptHelpTarget = null;

        Prisoner nearest = null;
        float best = 1.8f;
        foreach (Prisoner npc in npcs)
        {
            float d = Vector2.Distance(pos, npc.ConsolePos);
            if (d < best) { best = d; nearest = npc; }
        }

        if (nearest != null && nearest.State == PrisonerState.Struggling)
        {
            nearest.Encountered = true;
            promptHelpTarget = nearest;
            promptText = $"E — помочь {nearest.Name}";
        }
    }

    // ---- Тест на память (свой / за NPC) ----

    private void StartOwnTest()
    {
        testRound = 1;
        phase = MemoryPhase.Testing;
        StartRound();
    }

    private void StartRound()
    {
        sequence.Clear();
        int length = testRound + 2;
        for (int i = 0; i < length; i++) sequence.Add(Random.Range(0, SymbolCount));
        StartCoroutine(ShowSequence());
    }

    private IEnumerator ShowSequence()
    {
        testShowing = true;
        highlightedTile = -1;
        yield return new WaitForSeconds(0.45f);
        foreach (int symbol in sequence)
        {
            highlightedTile = symbol;
            yield return new WaitForSeconds(symbolOnTime);
            highlightedTile = -1;
            yield return new WaitForSeconds(0.18f);
        }
        recallIndex = 0;
        testShowing = false;
    }

    private void UpdateTesting()
    {
        if (testShowing) return;

        int pressed = ReadSymbolInput();
        if (pressed < 0) return;
        tileFlashUntil[pressed] = Time.time + 0.18f;

        if (pressed == sequence[recallIndex])
        {
            recallIndex++;
            if (recallIndex >= sequence.Count) RoundComplete();
        }
        else
        {
            recallIndex = 0; // ошибка не убивает: ввод с начала, общий таймер идёт
            wrongFlashUntil = Time.time + 0.35f;
        }
    }

    private void RoundComplete()
    {
        if (testRound >= ownRounds)
        {
            playerDone = true;
            phase = MemoryPhase.Roaming;
            return;
        }
        testRound++;
        StartRound();
    }

    // ---- Мини-игра помощи: ритм с падающими нотами ----

    private void StartHelp(Prisoner target)
    {
        helpTarget = target;
        helpProgress = 0f;
        helpCombo = 0;
        noteSpawnTimer = 0.3f;
        notes.Clear();
        phase = MemoryPhase.Helping;
    }

    private void UpdateHelping()
    {
        if (helpTarget == null || helpTarget.State != PrisonerState.Struggling)
        {
            phase = MemoryPhase.Roaming; // цель пропала
            return;
        }

        // Выход из помощи.
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            phase = MemoryPhase.Roaming;
            return;
        }

        // Спавн нот.
        noteSpawnTimer -= Time.deltaTime;
        if (noteSpawnTimer <= 0f)
        {
            noteSpawnTimer = noteSpawnInterval;
            notes.Add(new FallingNote { Lane = Random.Range(0, SymbolCount), P = 0f });
        }

        // Движение нот; пропущенные у линии — сбивают комбо и откатывают прогресс.
        for (int i = notes.Count - 1; i >= 0; i--)
        {
            notes[i].P += noteFallSpeed * Time.deltaTime;
            if (notes[i].P > 1.06f)
            {
                notes.RemoveAt(i);
                helpCombo = 0;
                helpProgress = Mathf.Max(0f, helpProgress - 0.12f);
            }
        }

        // Ввод по дорожкам.
        int lane = ReadLaneInput();
        if (lane >= 0) HitLane(lane);

        if (helpProgress >= 1f) HelpComplete();
    }

    private void HitLane(int lane)
    {
        int bestIndex = -1;
        float bestDistance = 0.28f;        // окно попадания у линии
        for (int i = 0; i < notes.Count; i++)
        {
            if (notes[i].Lane != lane) continue;
            float dist = Mathf.Abs(notes[i].P - 1f);
            if (dist < bestDistance) { bestDistance = dist; bestIndex = i; }
        }

        laneFlashUntil[lane] = Time.time + 0.18f;
        if (bestIndex >= 0)
        {
            notes.RemoveAt(bestIndex);
            helpCombo++;
            helpProgress = Mathf.Min(1f, helpProgress + 1f / helpHitsNeeded);
            laneFlashGood[lane] = true;
        }
        else
        {
            helpCombo = 0;
            laneFlashGood[lane] = false;
        }
    }

    private void HelpComplete()
    {
        helpTarget.State = PrisonerState.Done;
        helpTarget.HelpedByPlayer = true;
        helpTarget = null;
        phase = MemoryPhase.Roaming;
    }

    private static int ReadLaneInput()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return -1;
        if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) return 0;
        if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) return 1;
        if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) return 2;
        if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) return 3;
        return -1;
    }

    private static int ReadSymbolInput()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return -1;
        if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) return 0;
        if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) return 1;
        if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) return 2;
        if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) return 3;
        return -1;
    }

    // ---- Завершение ----

    private void Resolve()
    {
        if (phase == MemoryPhase.Resolved) return;
        playerSurvived = playerDone;

        int saved = 0;
        int total = npcs.Count;
        foreach (Prisoner npc in npcs)
        {
            if (npc.State != PrisonerState.Done) npc.State = PrisonerState.Failed;
            if (npc.State == PrisonerState.Done) saved++;
        }

        endText = playerSurvived
            ? $"Ты прошёл протокол. Выжило заключённых: {saved}/{total}."
            : $"Время вышло — свой тест не пройден, ты погиб. Выжило: {saved}/{total}.";

        phase = MemoryPhase.Resolved;
        SubmitResult();
    }

    private void SubmitResult()
    {
        var result = new ExperimentResult
        {
            ExperimentId = ExperimentId,
            PlayerSurvived = playerSurvived,
            PlayerWon = playerSurvived,
        };

        bool helpedAnyone = false;
        foreach (Prisoner npc in npcs)
        {
            bool survived = npc.State == PrisonerState.Done;
            NpcAction action = npc.HelpedByPlayer ? NpcAction.Helped
                : (npc.State == PrisonerState.Failed && npc.Encountered && playerDone) ? NpcAction.Ignored
                : NpcAction.None;
            if (npc.HelpedByPlayer) helpedAnyone = true;

            if (npc.IsNamed)
            {
                result.Record(npc.Id, survived, action);
                if (npc.HelpedByPlayer) result.RelationshipDeltas[npc.Id] = gratitudeReward;
            }
        }
        if (helpedAnyone) result.Flags.Add("memory-protocol.helped-someone");

        RunState.SubmitResult(result);
    }

    // ---- Мир ----

    private void BuildRoom()
    {
        cam = Camera.main;
        if (cam == null)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cam = cameraObject.AddComponent<Camera>();
        }
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        cam.transform.position = new Vector3(0f, 0f, -10f);
        cam.backgroundColor = new Color(0.19f, 0.30f, 0.47f);

        BuildFloorAndWalls();

        int npcCount = Mathf.Clamp(RunState.RaceParticipants, 1, 4);
        int stations = npcCount + 1;
        float spacing = 3f;
        float consoleY = 2.5f;
        float startX = -(stations - 1) * spacing * 0.5f;

        // Станция 0 — пульт игрока (синий).
        playerConsole = new Vector2(startX, consoleY);
        CreateConsole(playerConsole, new Color(0.45f, 0.65f, 0.95f));

        ExperimentContext context = RunState.BuildContext(ExperimentId);
        for (int i = 0; i < npcCount; i++)
        {
            var npc = new Prisoner();
            Vector2 consolePos = new(startX + (i + 1) * spacing, consoleY);
            npc.ConsolePos = consolePos;
            npc.Traits = BotTraits.Randomized();
            npc.RoundTime = Random.Range(3f, 5f);
            npc.NextRoundAt = Time.time + Random.Range(1.5f, 3.5f);

            string spriteBase;
            if (i == 0)
            {
                npc.Name = "Программист";
                npc.Id = NpcId.Programmer;
                npc.IsNamed = true;
                npc.BaseColor = HasArt("npc_programmer") ? Color.white : new Color(1f, 0.65f, 0.15f);
                spriteBase = "npc_programmer";
            }
            else if (i == 1)
            {
                npc.Name = "Заключённая 2";
                npc.Id = NpcId.Competitor;
                npc.IsNamed = true;
                npc.BaseColor = HasArt("girl") ? Color.white : new Color(0.9f, 0.25f, 0.65f);
                spriteBase = "girl";
            }
            else
            {
                npc.Name = $"Заключённый {i + 1}";
                npc.IsNamed = false;
                npc.BaseColor = HasArt("prisoner_generic") ? Color.white : GenericColors[(i - 2) % GenericColors.Length];
                spriteBase = "prisoner_generic";
            }

            CreateConsole(consolePos, new Color(0.78f, 0.74f, 0.70f));
            npc.Avatar = RaceVisuals.Character(npc.Name, spriteBase,
                consolePos + new Vector2(0f, 1.2f), WorldMetrics.CharacterScale, npc.BaseColor, 6);
            npcs.Add(npc);
        }

        // Игрок спавнится сразу у своего пульта (его тест стартует сразу же).
        playerAvatar = RaceVisuals.Character("Игрок", "player", playerConsole + new Vector2(0f, -1.2f),
            WorldMetrics.CharacterScale, HasArt("player") ? Color.white : new Color(0.2f, 0.65f, 1f), 7).transform;
    }

    /// <summary>Пол шахматкой и стены по краям — в стиле первой игры.</summary>
    private void BuildFloorAndWalls()
    {
        Color floorA = new(0.60f, 0.50f, 0.40f);
        Color floorB = new(0.56f, 0.46f, 0.36f);
        Color wallTop = new(0.50f, 0.35f, 0.20f);
        Color wallSide = new(0.35f, 0.25f, 0.15f);
        const int minX = -8, maxX = 8, minY = -5, maxY = 5;

        bool hasArt = HasArt("race_dirt");
        Color wallTint = hasArt ? Color.white : wallTop;
        Color wallSideTint = hasArt ? Color.white : wallSide;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                bool border = x == minX || x == maxX || y == minY || y == maxY;
                if (border)
                {
                    RaceVisuals.Art("Wall", "wall_top", new Vector2(x, y), Vector2.one * WorldMetrics.TileOverlap, wallTint, -8);
                    if (y == maxY)
                        RaceVisuals.Art("Wall Side", "wall_side", new Vector2(x, y - 0.18f),
                            new Vector2(WorldMetrics.TileOverlap, 0.55f), wallSideTint, -7);
                }
                else
                {
                    // Спрайт пола почти белый: шахматка остаётся за счёт тонировки.
                    Color f = (x + y) % 2 == 0 ? floorA : floorB;
                    RaceVisuals.Art("Floor", "race_dirt", new Vector2(x, y), Vector2.one * WorldMetrics.TileOverlap, f, -20);
                }
            }
        }
    }

    private static bool HasArt(string spriteName)
        => Resources.Load<Sprite>("Sprites/" + spriteName) != null;

    private void CreateConsole(Vector2 pos, Color color)
        => RaceVisuals.Art("Console", "console", pos, new Vector2(2.2f, 1.1f), color, -3);

    private void UpdateAvatarColors()
    {
        foreach (Prisoner npc in npcs)
        {
            if (npc.Avatar == null) continue;
            var sr = npc.Avatar.GetComponent<SpriteRenderer>();
            if (sr == null) continue;
            sr.color = npc.State switch
            {
                PrisonerState.Done => new Color(0.3f, 0.85f, 0.45f),
                PrisonerState.Struggling => new Color(1f, 0.55f, 0.12f),
                PrisonerState.Failed => new Color(0.35f, 0.08f, 0.08f),
                _ => npc.BaseColor,
            };
        }
    }

    // ---- UI ----

    private void OnGUI()
    {
        EnsureStyles();

        if (phase == MemoryPhase.Intro)
        {
            string text = introPage < 1
                ? "Администрация: каждый проходит протокол памяти за своим пультом. Не прошедшие до конца таймера — ликвидация."
                : "Сейчас начнётся твой тест (плитки 1–4). Пройдёшь — ходи по комнате (WASD) и помогай застрявшим (E).";
            DrawDialog(text, "SPACE — продолжить");
            return;
        }

        // HUD.
        GUI.Label(new Rect(16, 12, 420, 30), $"Время: {FormatTime(timeLeft)}", titleStyle);
        GUI.Label(new Rect(16, 44, 420, 26),
            playerDone ? "Свой тест: пройден. Можно помогать (Enter — уйти)." : "Свой тест: не пройден.", bodyStyle);

        if (phase == MemoryPhase.Roaming && !string.IsNullOrEmpty(promptText))
            DrawCenter(120f, promptText);

        if (phase == MemoryPhase.Testing)
        {
            DrawCenter(70f, "Твой тест");
            string hint = Time.time < wrongFlashUntil ? "Ошибка — вводи с начала!"
                : testShowing ? "Запоминай..." : $"Повтори: {recallIndex}/{sequence.Count}";
            DrawCenter(104f, hint);
            DrawTiles();
        }

        if (phase == MemoryPhase.Helping)
        {
            string who = helpTarget != null ? helpTarget.Name : "";
            DrawCenter(80f, $"Помогаешь {who}: лови ноты по 1–4!   ESC — бросить");
            if (helpCombo >= 3) DrawCenter(104f, $"Комбо ×{helpCombo}!");
            DrawRhythm();
        }

        if (phase == MemoryPhase.Resolved)
            DrawDialog(endText, "E — вернуться в тюрьму, R — повторить");
    }

    private void DrawTiles()
    {
        float w = 70f, gap = 16f;
        float totalW = SymbolCount * w + (SymbolCount - 1) * gap;
        float x0 = Screen.width / 2f - totalW / 2f;
        float y = Screen.height - 150f;
        Color prev = GUI.color;
        for (int i = 0; i < SymbolCount; i++)
        {
            bool bright = (testShowing && i == highlightedTile) || Time.time < tileFlashUntil[i];
            Color c = SymbolColors[i];
            GUI.color = bright ? c : new Color(c.r * 0.3f, c.g * 0.3f, c.b * 0.3f, 1f);
            var rect = new Rect(x0 + i * (w + gap), y, w, w);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x, rect.y + w / 2f - 14f, w, 28f), (i + 1).ToString(), centerStyle);
        }
        GUI.color = prev;
    }

    private void DrawRhythm()
    {
        const float laneW = 64f, gap = 8f, top = 130f, height = 300f;
        float panelW = SymbolCount * laneW + (SymbolCount - 1) * gap;
        float x0 = Screen.width / 2f - panelW / 2f;
        float hitY = top + height * 0.82f;
        Color prev = GUI.color;

        GUI.color = new Color(0.12f, 0.12f, 0.16f, 0.92f);
        GUI.DrawTexture(new Rect(x0 - 8, top - 8, panelW + 16, height + 16), Texture2D.whiteTexture);

        for (int i = 0; i < SymbolCount; i++)
        {
            float lx = x0 + i * (laneW + gap);
            Color baseC = SymbolColors[i];
            bool flash = Time.time < laneFlashUntil[i];
            GUI.color = flash
                ? (laneFlashGood[i] ? Color.green : new Color(0.9f, 0.3f, 0.3f))
                : new Color(baseC.r * 0.22f, baseC.g * 0.22f, baseC.b * 0.22f, 1f);
            GUI.DrawTexture(new Rect(lx, top, laneW, height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(lx, hitY + 10f, laneW, 24f), (i + 1).ToString(), centerStyle);
        }

        GUI.color = new Color(0.95f, 0.95f, 1f, 0.85f);
        GUI.DrawTexture(new Rect(x0 - 8, hitY, panelW + 16, 4f), Texture2D.whiteTexture);

        foreach (FallingNote n in notes)
        {
            float lx = x0 + n.Lane * (laneW + gap);
            float ny = top + Mathf.Clamp01(n.P) * (hitY - top);
            GUI.color = SymbolColors[n.Lane];
            GUI.DrawTexture(new Rect(lx + 6f, ny, laneW - 12f, 18f), Texture2D.whiteTexture);
        }

        // Полоса спасения.
        GUI.color = new Color(0.15f, 0.15f, 0.18f, 0.95f);
        GUI.DrawTexture(new Rect(x0, top + height + 16f, panelW, 18f), Texture2D.whiteTexture);
        GUI.color = new Color(0.3f, 0.85f, 0.45f);
        GUI.DrawTexture(new Rect(x0, top + height + 16f, panelW * Mathf.Clamp01(helpProgress), 18f), Texture2D.whiteTexture);
        GUI.color = prev;
    }

    private void DrawCenter(float y, string text)
        => GUI.Label(new Rect(Screen.width / 2f - 320f, y, 640f, 30f), text, centerStyle);

    private void DrawDialog(string text, string hint)
    {
        Rect box = new(40, Screen.height - 180, Screen.width - 80, 140);
        GUI.Box(box, "");
        GUI.Label(new Rect(box.x + 25, box.y + 20, box.width - 50, 70), text, bodyStyle);
        GUI.Label(new Rect(box.x + 25, box.y + 95, box.width - 50, 30), hint, bodyStyle);
    }

    private static string FormatTime(float seconds)
    {
        int total = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        return $"{total / 60:00}:{total % 60:00}";
    }

    private void EnsureStyles()
    {
        titleStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white },
        };
        bodyStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 17,
            wordWrap = true,
            normal = { textColor = Color.white },
        };
        centerStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperCenter,
            normal = { textColor = Color.white },
        };
    }
}
