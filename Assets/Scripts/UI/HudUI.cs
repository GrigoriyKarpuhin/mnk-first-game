using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Постоянный HUD в терминальном стиле: полоса управления, день/время/фаза,
/// CRT-панель здоровья C-4821, статус скрытности и активная цель. Заменяет
/// прежний OnGUI-рисунок в <see cref="Player"/> и <see cref="DayDirector"/>.
///
/// В отличие от OnGUI, канвас — ScreenSpaceCamera и потому виден в
/// headless-скриншоте. Строится лениво; каждый кадр <see cref="Refresh"/>
/// подтягивает состояние игрока и <see cref="RunState"/>.
/// </summary>
public sealed class HudUI : MonoBehaviour
{
    private const int HpSegmentCount = 10;

    private static HudUI instance;

    private Canvas canvas;
    private Text controlsLabel;
    private Text dayLabel;
    private Image[] hpCells;
    private Text hpNumber;
    private GameObject stealthPanel;
    private Text stealthLabel;
    private GameObject objectivePanel;
    private Text objectiveLabel;

    public static HudUI Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("HudUI");
                instance = go.AddComponent<HudUI>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        BuildUI();
    }

    private void BuildUI()
    {
        canvas = UIKit.CreateRootCanvas(gameObject, UITheme.SortHud);
        var raycaster = gameObject.GetComponent<GraphicRaycaster>();
        if (raycaster != null) raycaster.enabled = false; // HUD не перехватывает клики

        // Полоса управления — на всю ширину сверху, с переносом (не вылезает за рамку).
        Image controls = UIKit.CreateTerminalPanel("Controls", canvas.transform, out RectTransform controlsContent, scanlines: false, brackets: false);
        RectTransform cr = controls.rectTransform;
        cr.anchorMin = new Vector2(0f, 1f);
        cr.anchorMax = new Vector2(1f, 1f);
        cr.pivot = new Vector2(0.5f, 1f);
        cr.anchoredPosition = new Vector2(0f, -UITheme.Space2);
        cr.sizeDelta = new Vector2(-UITheme.Space2 * 2f, 46f);
        controlsLabel = UIKit.CreateText("Text", controlsContent, UITheme.TypeCaption, TextAnchor.UpperLeft, UITheme.TextStencil);
        controlsLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
        controlsLabel.verticalOverflow = VerticalWrapMode.Truncate;
        UIKit.FullStretch(controlsLabel.rectTransform);

        // Панель статуса: день/время + здоровье (под полосой управления).
        Image status = UIKit.CreateTerminalPanel("Status", canvas.transform, out RectTransform statusContent, scanlines: false);
        UIKit.Anchor(status.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(UITheme.Space2, -(UITheme.Space2 + 46f + UITheme.Space1)), new Vector2(360f, 104f));

        dayLabel = UIKit.CreateText("Day", statusContent, UITheme.TypeLabel, TextAnchor.UpperLeft, UITheme.TextPrimary);
        // Одна строка без переноса: иначе при другом масштабе окна перенос
        // «съедает» строку в однострочном прямоугольнике (см. фидбэк).
        UIKit.TopRect(dayLabel.rectTransform, 0f, 0f, 0f, 18f);

        Text hpId = UIKit.CreateStencilLabel("C-4821 · HP", statusContent, TextAnchor.UpperLeft);
        hpId.color = UITheme.TextMuted;
        UIKit.TopRect(hpId.rectTransform, 0f, 24f, 0f, 16f);

        hpNumber = UIKit.CreateText("HPNum", statusContent, UITheme.TypeLabel, TextAnchor.UpperRight, UITheme.TextBright);
        hpNumber.fontStyle = FontStyle.Bold;
        UIKit.Anchor(hpNumber.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, -24f), new Vector2(96f, 18f));

        BuildHealth(statusContent);

        // Статус скрытности (верх-центр).
        Image stealth = UIKit.CreateTerminalPanel("Stealth", canvas.transform, out RectTransform stealthContent, scanlines: false);
        stealthPanel = stealth.gameObject;
        UIKit.Anchor(stealth.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -UITheme.Space2), new Vector2(190f, 34f));
        stealthLabel = UIKit.CreateText("Text", stealthContent, UITheme.TypeLabel, TextAnchor.MiddleCenter, UITheme.Accent);
        stealthLabel.fontStyle = FontStyle.Bold;
        UIKit.FullStretch(stealthLabel.rectTransform);
        stealthPanel.SetActive(false);

        // Активная цель (под панелью здоровья).
        Image objective = UIKit.CreateTerminalPanel("Objective", canvas.transform, out RectTransform objectiveContent, scanlines: false);
        objectivePanel = objective.gameObject;
        UIKit.Anchor(objective.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(UITheme.Space2, -164f), new Vector2(470f, 40f));
        Text objTag = UIKit.CreateStencilLabel("ЦЕЛЬ", objectiveContent, TextAnchor.UpperLeft);
        objTag.color = UITheme.TextMuted;
        UIKit.Anchor(objTag.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            Vector2.zero, new Vector2(0f, 14f));
        objectiveLabel = UIKit.CreateText("Text", objectiveContent, UITheme.TypeLabel, TextAnchor.UpperLeft, UITheme.TextPrimary);
        objectiveLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
        UIKit.Anchor(objectiveLabel.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -16f), new Vector2(0f, -16f));
        objectivePanel.SetActive(false);
    }

    /// <summary>Сегментированная шкала здоровья на токенах UI-kit (без спрайта).</summary>
    private void BuildHealth(RectTransform parent)
    {
        hpCells = new Image[HpSegmentCount];
        for (int i = 0; i < HpSegmentCount; i++)
        {
            Image seg = UIKit.CreatePanel($"HP {i}", parent, UITheme.Success);
            seg.raycastTarget = false;
            RectTransform r = seg.rectTransform;
            r.anchorMin = new Vector2(i / (float)HpSegmentCount, 0f);
            r.anchorMax = new Vector2((i + 1) / (float)HpSegmentCount, 0f);
            r.pivot = new Vector2(0.5f, 0f);
            r.offsetMin = new Vector2(1.5f, 6f);
            r.offsetMax = new Vector2(-1.5f, 26f);
            hpCells[i] = seg;
        }
    }

    private int lastRefreshFrame = -100;

    // HUD ведёт только Player.Update. Если кадр прошёл без Refresh (мы вне тюрьмы —
    // например, в эксперименте, где своего Player нет), гасим канвас, чтобы
    // прежний HUD не висел поверх чужой сцены.
    private void LateUpdate()
    {
        if (canvas != null && canvas.enabled && Time.frameCount - lastRefreshFrame > 1)
        {
            canvas.enabled = false;
        }
    }

    /// <summary>Каждый кадр из <see cref="Player.Update"/>: подтянуть состояние.</summary>
    public void Refresh(Player player)
    {
        if (canvas == null) BuildUI();
        lastRefreshFrame = Time.frameCount;

        // HUD прячется под полноэкранными модалками (журнал/доска/карта), но
        // остаётся во время диалога (диалог — нижняя панель).
        bool blocked = QuestJournalUI.IsOpen || InvestigationBoardUI.IsOpen || PrisonMapUI.IsOpen;
        canvas.enabled = !blocked;
        if (blocked) return;

        controlsLabel.text = BuildControls(player);
        dayLabel.text = $"ДЕНЬ {RunState.Day} · {DaySchedule.FormatTime(RunState.MinuteOfDay)} · {RunState.ScheduleLabel}";

        RefreshHealth(player);
        RefreshStealth(player);

        bool hasObjective = !string.IsNullOrEmpty(RunState.ActiveObjective);
        objectivePanel.SetActive(hasObjective);
        if (hasObjective) objectiveLabel.text = RunState.ActiveObjective;
    }

    private void RefreshHealth(Player player)
    {
        int per = Mathf.Max(1, player.MaxHealth / HpSegmentCount);
        int lit = Mathf.Clamp(Mathf.CeilToInt(player.CurrentHealth / (float)per), 0, HpSegmentCount);
        Color litColor = lit <= 2 ? UITheme.DangerBright : UITheme.Success;
        for (int i = 0; i < hpCells.Length; i++)
        {
            hpCells[i].color = i < lit ? litColor : UITheme.Well;
        }
        hpNumber.text = player.CurrentHealth.ToString();
    }

    private void RefreshStealth(Player player)
    {
        string label = null;
        Color color = UITheme.Accent;
        if (player.IsHidden) { label = "СКРЫТ"; color = UITheme.TextBright; }
        else if (player.IsInCover) { label = "В УКРЫТИИ"; color = UITheme.Accent; }
        else if (player.IsCrouching) { label = "КРАДЁТСЯ"; color = UITheme.Warning; }

        stealthPanel.SetActive(label != null);
        if (label != null)
        {
            stealthLabel.text = label;
            stealthLabel.color = color;
        }
    }

    private static string BuildControls(Player player)
    {
        string eye = RunState.HasImplant(ImplantId.EyeImplant)
            ? (RunState.EyeImplantActive ? "R выкл. глаз" : "R глаз")
            : "R —";
        string feet = RunState.HasReactiveFeet ? "Q стопы" : "Q —";
        string mask = RunState.HasImplant(ImplantId.MaskingImplant)
            ? RunState.MaskingImplantActive
                ? $"T маск. {Mathf.CeilToInt(RunState.MaskingImplantRemaining)}с"
                : RunState.MaskingImplantCooldownRemaining > 0f
                    ? $"T маск. {Mathf.CeilToInt(RunState.MaskingImplantCooldownRemaining)}с"
                    : "T маскировка"
            : "T —";
        string rest = RunState.IsRestingInBed ? " · отдых x10" : "";
        string fKey = player.IsCarrying ? "F бросить тело" : "F со спины/поднять";
        return $"WASD ходить · Ctrl красться · G бросок · E действие · M карта · J журнал · B доска · " +
               $"{fKey} · {feet} · {eye} · {mask} · Предметы {RunState.PrisonItemCount}{rest}";
    }
}
