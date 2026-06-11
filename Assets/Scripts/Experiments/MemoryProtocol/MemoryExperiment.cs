using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum MemoryPhase
{
    Intro,
    Showing,
    Recall,
    Helping,
    Success,
    Failed,
}

/// <summary>
/// Эксперимент 02: «Протокол памяти» (категория «одиночное испытание»).
///
/// Испытание независимое: игрок и сосед-NPC (программист) проходят его параллельно,
/// и оба МОГУТ выжить. Игрок воспроизводит последовательности символов нарастающей
/// длины. Общий таймер протокола выводится из сложности — суммы длин всех раундов.
///
/// Ошибка не убивает: ввод сбрасывается на начало, но общий таймер продолжает идти
/// (он не сбрасывается). Игрок гибнет только если не успел пройти все раунды.
///
/// Социальный крючок — положительная сумма. После того как последовательность показана
/// (фаза ввода), игрок в ЛЮБОЙ момент может переключиться на помощь соседу (E) — это
/// тайминг-мини-игра, идущая за счёт ОБЩЕГО ТАЙМЕРА игрока. Помощь длится столько,
/// сколько игрок хочет: повторное нажатие E прекращает её и возвращает к вводу.
/// Каждое точное попадание — очко помощи; очки копятся за всю игру и повышают ШАНС
/// выживания соседа (показан на экране). Если сосед выжил — он благодарен (рост
/// отношений и обещанная помощь после эксперимента).
///
/// Спецификация — Design/EXPERIMENT_02_SPEC.md, форма плейтеста — Design/PLAYTEST_02.md.
/// </summary>
public class MemoryExperiment : MonoBehaviour
{
    private const string ExperimentId = "experiment.memory-protocol";
    private const int SymbolCount = 4;
    private const int MaxRounds = 5;

    // Параметры тайминг-мини-игры помощи.
    private const float SweepSpeed = 200f;
    private const float ZoneMin = 65f;
    private const float ZoneMax = 115f;

    [Header("Таймер (выводится из сложности)")]
    [Tooltip("Базовый запас времени, секунд.")]
    [SerializeField] private float timeBase = 5f;
    [Tooltip("Секунд на каждый символ во всех последовательностях.")]
    [SerializeField] private float timePerSymbol = 1.5f;

    [Header("Показ")]
    [Tooltip("Сколько секунд горит каждый символ при показе.")]
    [SerializeField] private float symbolOnTime = 0.7f;

    [Header("Помощь соседу")]
    [Tooltip("Прибавка к шансу выживания соседа за одно очко помощи (доля, 0.015 = 1.5%).")]
    [SerializeField] private float chancePerPoint = 0.015f;
    [Tooltip("Максимальный шанс выживания соседа.")]
    [SerializeField] private float maxNeighborChance = 0.95f;
    [Tooltip("Награда к отношениям, если сосед выжил благодаря помощи.")]
    [SerializeField] private int gratitudeReward = 3;

    private static readonly Color[] SymbolColors =
    {
        new(0.85f, 0.25f, 0.25f),
        new(0.30f, 0.80f, 0.40f),
        new(0.30f, 0.55f, 0.95f),
        new(0.92f, 0.80f, 0.25f),
    };

    private readonly List<int> sequence = new();
    private SpriteRenderer[] tiles;
    private float[] tileFlashUntil;
    private Camera cam;

    private MemoryPhase phase = MemoryPhase.Intro;
    private int introPage;

    private int round = 1;
    private int recallIndex;
    private float timeLeft;
    private int highlightedTile = -1;
    private float wrongFlashUntil;
    private float helpToggleCooldown;

    // Тайминг-мини-игра помощи.
    private float helpAngle;
    private float helpFlashUntil;
    private bool helpFlashGood;

    // Сосед.
    private readonly NpcId neighbor = NpcId.Programmer;
    private int helpPoints;                // копится за всю игру, не сбрасывается
    private bool neighborSurvived;

    // Входные параметры из контракта.
    private bool hasImplant;
    private int neighborRelationship;
    private bool relationshipBonusPending;
    private bool relationshipPenaltyPending;

    private bool resultSubmitted;
    private string endText = "";
    private GUIStyle titleStyle;
    private GUIStyle bodyStyle;
    private GUIStyle centerStyle;

    private void Awake()
    {
        ExperimentContext context = RunState.BuildContext(ExperimentId);
        hasImplant = context.HasImplant(ImplantId.ReactiveFeet) || context.HasImplant(ImplantId.EyeImplant);
        neighborRelationship = context.RelationshipTo(neighbor);
        relationshipBonusPending = neighborRelationship >= 2;
        relationshipPenaltyPending = neighborRelationship <= -1;

        timeLeft = ComputeTotalTime();
        BuildWorld();
    }

    /// <summary>Таймер протокола = база + время на каждый символ всех раундов (зависит от сложности).</summary>
    private float ComputeTotalTime()
    {
        int totalSymbols = 0;
        for (int r = 1; r <= MaxRounds; r++) totalSymbols += r + 2;
        return timeBase + timePerSymbol * totalSymbols;
    }

    private void Update()
    {
        HandleGlobalInput();

        bool activePlay = phase == MemoryPhase.Showing || phase == MemoryPhase.Recall ||
                          phase == MemoryPhase.Helping;
        if (activePlay)
        {
            timeLeft -= Time.deltaTime;
            if (timeLeft <= 0f)
            {
                timeLeft = 0f;
                Finish(playerSurvived: false);
            }
        }

        if (phase == MemoryPhase.Recall) UpdateRecall();
        else if (phase == MemoryPhase.Helping) UpdateHelping();

        PaintTiles();
    }

    private void HandleGlobalInput()
    {
        if (Keyboard.current == null) return;

        if (phase == MemoryPhase.Intro && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            introPage++;
            if (introPage >= 2) StartRound();
            return;
        }

        if (phase == MemoryPhase.Success || phase == MemoryPhase.Failed)
        {
            if (Keyboard.current.eKey.wasPressedThisFrame) RunState.ReturnToPrison();
            else if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            }
        }
    }

    // ---- Раунды игрока ----

    private void StartRound()
    {
        sequence.Clear();
        int length = round + 2;
        for (int i = 0; i < length; i++) sequence.Add(Random.Range(0, SymbolCount));
        StartCoroutine(ShowSequence());
    }

    private IEnumerator ShowSequence()
    {
        phase = MemoryPhase.Showing;
        highlightedTile = -1;
        yield return new WaitForSeconds(0.5f);

        int repeats = 1;
        if (hasImplant) repeats++;
        if (relationshipBonusPending) { repeats++; relationshipBonusPending = false; }

        float onTime = symbolOnTime;
        if (relationshipPenaltyPending) { onTime *= 0.6f; relationshipPenaltyPending = false; }

        for (int r = 0; r < repeats; r++)
        {
            foreach (int symbol in sequence)
            {
                highlightedTile = symbol;
                yield return new WaitForSeconds(onTime);
                highlightedTile = -1;
                yield return new WaitForSeconds(0.2f);
            }
            yield return new WaitForSeconds(0.35f);
        }

        recallIndex = 0;
        phase = MemoryPhase.Recall;
    }

    private void UpdateRecall()
    {
        // Помощь доступна в любой момент ввода (после показа), за счёт общего таймера.
        if (CanToggleHelp() && Keyboard.current.eKey.wasPressedThisFrame)
        {
            phase = MemoryPhase.Helping;
            helpToggleCooldown = Time.time + 0.25f;
            helpAngle = 0f;
            return;
        }

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
            recallIndex = 0;
            wrongFlashUntil = Time.time + 0.35f;
        }
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

    private void RoundComplete()
    {
        if (round >= MaxRounds)
        {
            Finish(playerSurvived: true);
            return;
        }
        round++;
        StartRound();
    }

    // ---- Тайминг-мини-игра помощи ----

    private bool CanToggleHelp() => Keyboard.current != null && Time.time >= helpToggleCooldown;

    private void UpdateHelping()
    {
        helpAngle = Mathf.Repeat(helpAngle + Time.deltaTime * SweepSpeed, 180f);

        // Прекратить помощь и вернуться к вводу — ввод начинается с начала.
        if (CanToggleHelp() && Keyboard.current.eKey.wasPressedThisFrame)
        {
            helpToggleCooldown = Time.time + 0.25f;
            recallIndex = 0;
            phase = MemoryPhase.Recall;
            return;
        }

        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            bool hit = helpAngle >= ZoneMin && helpAngle <= ZoneMax;
            helpFlashUntil = Time.time + 0.25f;
            helpFlashGood = hit;
            if (hit) helpPoints++;
        }
    }

    // ---- Завершение ----

    private float NeighborChance() => Mathf.Clamp(helpPoints * chancePerPoint, 0f, maxNeighborChance);

    private void Finish(bool playerSurvived)
    {
        if (phase == MemoryPhase.Success || phase == MemoryPhase.Failed) return;

        neighborSurvived = Random.value < NeighborChance();
        phase = playerSurvived ? MemoryPhase.Success : MemoryPhase.Failed;
        endText = BuildEndText(playerSurvived, neighborSurvived);

        SubmitResult(playerSurvived);
    }

    private string BuildEndText(bool playerSurvived, bool neighborOk)
    {
        string neighborLine = neighborOk
            ? "Программист выжил — он благодарен и обещает помочь позже."
            : "Программист не выжил.";
        if (playerSurvived)
            return $"Ты прошёл протокол. {neighborLine}";
        return $"Время вышло — ты не успел и погиб. {neighborLine}";
    }

    private void SubmitResult(bool survived)
    {
        if (resultSubmitted) return;
        resultSubmitted = true;

        NpcAction action = helpPoints > 0 ? NpcAction.Helped : NpcAction.Ignored;

        var result = new ExperimentResult
        {
            ExperimentId = ExperimentId,
            PlayerSurvived = survived,
            PlayerWon = survived,
        };
        result.Record(neighbor, neighborSurvived, action);
        result.Flags.Add($"memory-protocol.neighbor.{action.ToString().ToLowerInvariant()}");

        if (neighborSurvived && helpPoints > 0)
        {
            result.RelationshipDeltas[neighbor] = gratitudeReward;
            result.Flags.Add("memory-protocol.neighbor.rewarded");
        }

        RunState.SubmitResult(result);
    }

    // ---- Визуализация ----

    private void BuildWorld()
    {
        cam = Camera.main;
        if (cam == null)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cam = cameraObject.AddComponent<Camera>();
        }
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.transform.position = new Vector3(0f, 0.5f, -10f);
        cam.backgroundColor = new Color(0.10f, 0.11f, 0.16f);

        tiles = new SpriteRenderer[SymbolCount];
        tileFlashUntil = new float[SymbolCount];
        float startX = -(SymbolCount - 1);
        for (int i = 0; i < SymbolCount; i++)
        {
            var go = new GameObject($"Symbol {i + 1}");
            go.transform.position = new Vector3(startX + i * 2f, 1.2f, 0f);
            go.transform.localScale = Vector3.one * 1.5f;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PrototypeSprites.Square;
            sr.color = Dim(SymbolColors[i]);
            tiles[i] = sr;
        }
    }

    private void PaintTiles()
    {
        if (tiles == null) return;
        bool wrong = Time.time < wrongFlashUntil;
        for (int i = 0; i < tiles.Length; i++)
        {
            bool bright = phase switch
            {
                MemoryPhase.Showing => i == highlightedTile,
                MemoryPhase.Recall => Time.time < tileFlashUntil[i],
                _ => false,
            };
            Color c = bright ? SymbolColors[i] : Dim(SymbolColors[i]);
            if (wrong) c = Color.Lerp(c, new Color(0.6f, 0.1f, 0.1f), 0.6f);
            tiles[i].color = c;
        }
    }

    private static Color Dim(Color color)
    {
        return new Color(color.r * 0.28f, color.g * 0.28f, color.b * 0.28f, 1f);
    }

    private void OnGUI()
    {
        EnsureStyles();

        if (phase == MemoryPhase.Intro)
        {
            string text = introPage < 1
                ? "Администрация: пройдите протокол памяти до конца таймера. Не успевшие — ликвидация. Пройти могут оба."
                : "Клавиши 1–4 — символы. Ошибка не убивает: вводи заново, таймер идёт. В любой момент ввода можно нажать E и помогать программисту — но это идёт за счёт твоего времени.";
            DrawDialog(text, "SPACE — продолжить");
            return;
        }

        // HUD: компактный блок сверху слева, без перекрытий.
        GUI.Label(new Rect(16, 10, 360, 30), $"Время: {FormatTime(timeLeft)}", titleStyle);
        GUI.Label(new Rect(16, 42, 360, 24), $"Раунд: {Mathf.Min(round, MaxRounds)}/{MaxRounds}", bodyStyle);

        int chancePercent = Mathf.RoundToInt(NeighborChance() * 100f);
        DrawBar(16, 70, 320, 22, NeighborChance(), new Color(0.95f, 0.55f, 0.20f),
            $"Шанс спасти программиста: {chancePercent}%  (очки {helpPoints})");

        float cy = 150f;
        if (phase == MemoryPhase.Showing)
        {
            DrawCenter(cy, "Запоминай последовательность...");
        }
        else if (phase == MemoryPhase.Recall)
        {
            string hint = Time.time < wrongFlashUntil ? "Ошибка — вводи с начала!" : $"Повтори: {recallIndex}/{sequence.Count}";
            DrawCenter(cy, hint);
            DrawCenter(cy + 34f, "E — помогать программисту (за твоё время)");
        }
        else if (phase == MemoryPhase.Helping)
        {
            DrawCenter(cy, "Помощь: жми SPACE в зелёной зоне. E — перестать помогать.");
            DrawHelpQte();
        }
        else if (phase == MemoryPhase.Success || phase == MemoryPhase.Failed)
        {
            DrawDialog(endText, "E — вернуться в тюрьму, R — повторить");
        }
    }

    private void DrawHelpQte()
    {
        Rect box = new(Screen.width / 2f - 140, Screen.height - 230, 280, 170);
        GUI.Box(box, "");
        GUI.Label(new Rect(box.x + 20, box.y + 12, 240, 26), $"Очки помощи: {helpPoints}", bodyStyle);

        if (Time.time < helpFlashUntil)
        {
            GUI.color = helpFlashGood ? Color.green : new Color(0.9f, 0.3f, 0.3f);
            GUI.Label(new Rect(box.x + 175, box.y + 12, 95, 26), helpFlashGood ? "+1!" : "мимо", titleStyle);
            GUI.color = Color.white;
        }

        Vector2 center = new(box.center.x, box.yMax - 26f);
        float radius = 85f;
        for (int angle = 0; angle <= 180; angle += 5)
        {
            float rad = angle * Mathf.Deg2Rad;
            Vector2 point = center + new Vector2(Mathf.Cos(rad), -Mathf.Sin(rad)) * radius;
            bool target = angle >= ZoneMin && angle <= ZoneMax;
            GUI.color = target ? Color.green : Color.gray;
            GUI.DrawTexture(new Rect(point.x - 3, point.y - 3, 6, 6), Texture2D.whiteTexture);
        }

        float pointerRad = helpAngle * Mathf.Deg2Rad;
        Vector2 pointer = center + new Vector2(Mathf.Cos(pointerRad), -Mathf.Sin(pointerRad)) * radius;
        GUI.color = Color.yellow;
        GUI.DrawTexture(new Rect(pointer.x - 7, pointer.y - 7, 14, 14), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    private void DrawBar(float x, float y, float w, float h, float fraction, Color fill, string label)
    {
        Color prev = GUI.color;
        GUI.color = new Color(0.15f, 0.15f, 0.18f, 0.9f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
        GUI.color = fill;
        GUI.DrawTexture(new Rect(x, y, w * Mathf.Clamp01(fraction), h), Texture2D.whiteTexture);
        GUI.color = prev;
        GUI.Label(new Rect(x + 8, y + 1, w - 12, h), label, bodyStyle);
    }

    private void DrawCenter(float y, string text)
    {
        GUI.Label(new Rect(Screen.width / 2f - 320, y, 640, 30), text, centerStyle);
    }

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
            fontSize = 21,
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
