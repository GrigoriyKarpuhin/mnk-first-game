using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Доска расследования в стиле CRT-терминала: вкладки «связи улик» и «выводы»,
/// прокручиваемые списки, слоты соединения и граф-цепочки вывода. Прежний
/// immediate-mode OnGUI заменён на retained uGUI: содержимое пересобирается
/// в <see cref="Refresh"/> при каждом изменении состояния (экран модальный,
/// timeScale=0 — это дёшево). Теперь доска видна в headless-скриншоте.
/// </summary>
public sealed class InvestigationBoardUI : MonoBehaviour
{
    private enum BoardMode { Connections, Conclusions }

    private static readonly Color NodeEvidence = UITheme.PanelRaised;
    private static readonly Color NodeDeduction = UITheme.Accent;
    private static readonly Color StatusSuccess = UITheme.Hex("1e3a2a");

    private static InvestigationBoardUI instance;

    private BoardMode mode = BoardMode.Connections;
    private EvidenceId? focusedEvidence;
    private EvidenceId? firstEvidence;
    private EvidenceId? secondEvidence;
    private EvidenceId? detailEvidence;
    private DeductionId? detailDeduction;
    private string resultMessage = "Выберите улику, затем вторую улику для проверки связи.";
    private float previousTimeScale = 1f;
    private EvidenceId? lastClickedEvidence;
    private float lastClickAt;
    private bool open;

    private Canvas canvas;
    private GameObject root;
    private Button[] tabs;
    private RectTransform contentRoot;
    private GameObject detailRoot;
    private Text detailTag;
    private Text detailTitle;
    private Text detailBody;

    public static bool IsOpen => instance != null && instance.open;

    public static void Toggle()
    {
        if (IsOpen) CloseCurrent();
        else OpenCurrent();
    }

    public static void OpenCurrent()
    {
        if (instance == null)
        {
            var go = new GameObject("Investigation Board UI");
            instance = go.AddComponent<InvestigationBoardUI>();
            DontDestroyOnLoad(go);
        }

        instance.Open();
    }

    public static void CloseCurrent()
    {
        if (instance == null || !instance.open) return;
        instance.Close();
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
        canvas = UIKit.CreateRootCanvas(gameObject, UITheme.SortBoard);

        root = UIKit.CreatePanel("Board", canvas.transform, UITheme.Surface).gameObject;
        UIKit.FullStretch((RectTransform)root.transform);

        Text title = UIKit.CreateText("Title", root.transform, UITheme.TypeDisplay, TextAnchor.UpperLeft, UITheme.TextBright);
        title.text = "ДОСКА РАССЛЕДОВАНИЯ";
        title.fontStyle = FontStyle.Bold;
        UIKit.Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(UITheme.Space8, -20f), new Vector2(-UITheme.Space8 * 2f, 48f));

        Text hint = UIKit.CreateStencilLabel(
            "B — ЗАКРЫТЬ · ESC — НАЗАД · 1 — СВЯЗИ УЛИК · 2 — ВЫВОДЫ · ДВОЙНОЙ КЛИК — ПОДРОБНОСТИ",
            root.transform, TextAnchor.UpperLeft);
        UIKit.Anchor(hint.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(UITheme.Space8, -64f), new Vector2(-UITheme.Space8 * 2f, 22f));

        var tabBarObj = new GameObject("Tabs", typeof(RectTransform));
        tabBarObj.transform.SetParent(root.transform, false);
        var tabBarRect = tabBarObj.GetComponent<RectTransform>();
        UIKit.Anchor(tabBarRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(UITheme.Space8, -92f), new Vector2(620f, 40f));
        tabs = UIKit.CreateTabBar(tabBarRect, out _, "1. СВЯЗИ УЛИК", "2. УМОЗАКЛЮЧЕНИЯ");
        tabs[0].onClick.AddListener(() => SetMode(BoardMode.Connections));
        tabs[1].onClick.AddListener(() => SetMode(BoardMode.Conclusions));

        contentRoot = UIKit.CreatePanel("Content", root.transform, Color.clear).rectTransform;
        contentRoot.GetComponent<Image>().raycastTarget = false;
        UIKit.Stretch(contentRoot, UITheme.Space8, UITheme.Space6, UITheme.Space8, 150f);

        BuildDetailOverlay();

        root.SetActive(false);
    }

    private void BuildDetailOverlay()
    {
        detailRoot = UIKit.CreatePanel("Detail", canvas.transform, UITheme.Backdrop).gameObject;
        UIKit.FullStretch((RectTransform)detailRoot.transform);

        Image panel = UIKit.CreateTerminalPanel("Detail Panel", detailRoot.transform, out RectTransform content);
        UIKit.Anchor(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(820f, 520f));

        detailTag = UIKit.CreateStencilLabel("УЛИКА", content, TextAnchor.UpperLeft);
        detailTag.color = UITheme.Accent;
        UIKit.TopRect(detailTag.rectTransform, 4f, 0f, 4f, 20f);

        detailTitle = UIKit.CreateText("Title", content, UITheme.TypeTitle, TextAnchor.UpperLeft, UITheme.TextBright);
        detailTitle.fontStyle = FontStyle.Bold;
        detailTitle.horizontalOverflow = HorizontalWrapMode.Wrap;
        UIKit.TopRect(detailTitle.rectTransform, 4f, 26f, 4f, 40f);

        RectTransform bodyScroll = UIKit.CreateScrollView("Body", content, out _);
        UIKit.Stretch((RectTransform)bodyScroll.parent, 4f, 56f, 4f, 78f);
        detailBody = UIKit.CreateText("BodyText", bodyScroll, UITheme.TypeBody, TextAnchor.UpperLeft, UITheme.TextPrimary);
        detailBody.horizontalOverflow = HorizontalWrapMode.Wrap;
        UIKit.Anchor(detailBody.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            Vector2.zero, new Vector2(-12f, 400f));

        Button closeBtn = UIKit.CreateButton("Закрыть  ·  Esc", content, CloseDetail);
        UIKit.Anchor(closeBtn.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(0f, 0f), new Vector2(200f, 40f));

        detailRoot.SetActive(false);
    }

    private void Update()
    {
        if (!open || Keyboard.current == null) return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (HasOpenDetail()) CloseDetail();
            else Close();
            return;
        }
        if (HasOpenDetail()) return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame) SetMode(BoardMode.Connections);
        if (Keyboard.current.digit2Key.wasPressedThisFrame) SetMode(BoardMode.Conclusions);
        if (mode == BoardMode.Connections && Keyboard.current.cKey.wasPressedThisFrame) ConnectSelected();
    }

    private void SetMode(BoardMode next)
    {
        mode = next;
        Refresh();
    }

    private void Open()
    {
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        open = true;
        root.SetActive(true);
        CloseDetail();
        Refresh();
    }

    private void Close()
    {
        CloseDetail();
        open = false;
        root.SetActive(false);
        Time.timeScale = previousTimeScale;
    }

    // === Пересборка содержимого ===========================================

    private void Refresh()
    {
        for (int i = 0; i < tabs.Length; i++)
        {
            var img = tabs[i].GetComponent<Image>();
            bool active = (int)mode == i;
            var colors = tabs[i].colors;
            colors.normalColor = active ? UITheme.Selected : UITheme.ButtonNormal;
            tabs[i].colors = colors;
            img.color = Color.white;
        }

        for (int i = contentRoot.childCount - 1; i >= 0; i--) Destroy(contentRoot.GetChild(i).gameObject);

        if (mode == BoardMode.Connections) BuildConnections();
        else BuildConclusions();
    }

    private void BuildConnections()
    {
        RectTransform evidencePanel = Column("УЛИКИ", 0f, 0.34f, out RectTransform evContent);
        RectTransform centerPanel = Column("СОЕДИНЕНИЕ", 0.355f, 0.63f, out RectTransform ceContent);
        RectTransform candPanel = Column("ПРОВЕРЕННЫЕ И ВОЗМОЖНЫЕ СВЯЗИ", 0.645f, 1f, out RectTransform caContent);

        BuildEvidenceList(evContent);
        BuildConnectionPanel(ceContent);
        BuildCandidateList(caContent);
    }

    private RectTransform Column(string header, float minX, float maxX, out RectTransform content)
    {
        Image panel = UIKit.CreateTerminalPanel($"Col:{header}", contentRoot, out content, scanlines: false);
        RectTransform r = panel.rectTransform;
        r.anchorMin = new Vector2(minX, 0f);
        r.anchorMax = new Vector2(maxX, 1f);
        r.offsetMin = new Vector2(minX == 0f ? 0f : UITheme.Space2, 0f);
        r.offsetMax = new Vector2(maxX == 1f ? 0f : -UITheme.Space2, 0f);

        Text h = UIKit.CreateStencilLabel(header, content, TextAnchor.UpperLeft);
        h.color = UITheme.Accent;
        UIKit.TopRect(h.rectTransform, 0f, 0f, 0f, 22f);
        return panel.rectTransform;
    }

    private void BuildEvidenceList(RectTransform panelContent)
    {
        List<EvidenceId> items = GetDiscoveredEvidence();
        RectTransform list = UIKit.CreateScrollView("EvList", panelContent, out _);
        UIKit.Stretch((RectTransform)list.parent, 0f, 0f, 0f, 30f);

        if (items.Count == 0)
        {
            Text empty = UIKit.CreateText("Empty", list, UITheme.TypeBody, TextAnchor.UpperLeft, UITheme.TextMuted);
            empty.text = "Улик пока нет. Ищите факты в диалогах, предметах и подслушанных разговорах.";
            empty.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIKit.Anchor(empty.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(-8f, 120f));
            return;
        }

        float y = 0f;
        foreach (EvidenceId id in items)
        {
            bool selected = firstEvidence == id || secondEvidence == id;
            bool focused = focusedEvidence == id;
            Color bg = focused ? UITheme.Selected : selected ? UITheme.RowActive : UITheme.RowNormal;
            EvidenceId captured = id;
            AddTwoLineRow(list, ref y, 60f, RunState.EvidenceShortTitle(id), ConnectionSummaryFor(id), bg,
                () => HandleEvidenceClick(captured));
        }
        UIKit.SetScrollContentHeight(list, y);
    }

    private void BuildConnectionPanel(RectTransform panelContent)
    {
        string first = firstEvidence.HasValue ? RunState.EvidenceShortTitle(firstEvidence.Value) : "Первая улика";
        string second = secondEvidence.HasValue ? RunState.EvidenceShortTitle(secondEvidence.Value) : "Вторая улика";

        Slot(panelContent, 34f, first, firstEvidence.HasValue);
        Slot(panelContent, 116f, second, secondEvidence.HasValue);

        Button connect = UIKit.CreateButton("Соединить улики  (C)", panelContent, ConnectSelected, out Text connectLabel);
        connectLabel.color = UITheme.OnAccent;
        var cc = connect.colors; cc.normalColor = UITheme.Accent; cc.highlightedColor = UITheme.TextBright; connect.colors = cc;
        UIKit.TopRect(connect.GetComponent<RectTransform>(), 0f, 208f, 0f, 44f);

        Button clear = UIKit.CreateButton("Очистить выбор", panelContent, () => ClearSelection("Выбор очищен."));
        UIKit.TopRect(clear.GetComponent<RectTransform>(), 0f, 260f, 0f, 34f);

        Text result = UIKit.CreateText("Result", panelContent, UITheme.TypeLabel, TextAnchor.UpperLeft, UITheme.TextPrimary);
        result.text = resultMessage;
        result.horizontalOverflow = HorizontalWrapMode.Wrap;
        UIKit.TopRect(result.rectTransform, 0f, 306f, 0f, 200f);
    }

    private void Slot(RectTransform parent, float top, string text, bool filled)
    {
        Image slot = UIKit.CreatePanel("Slot", parent, filled ? UITheme.Selected : UITheme.Well);
        UIKit.AddFrame(slot.rectTransform, UITheme.BorderDim, UITheme.BorderDim, UITheme.BorderThin, 0f);
        UIKit.TopRect(slot.rectTransform, 0f, top, 0f, 72f);
        Text t = UIKit.CreateText("T", slot.transform, UITheme.TypeBody, TextAnchor.MiddleCenter, UITheme.TextPrimary);
        t.text = text;
        t.fontStyle = FontStyle.Bold;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        UIKit.Stretch(t.rectTransform, 10f, 6f, 10f, 6f);
    }

    private void BuildCandidateList(RectTransform panelContent)
    {
        RectTransform list = UIKit.CreateScrollView("CandList", panelContent, out _);
        UIKit.Stretch((RectTransform)list.parent, 0f, 0f, 0f, 30f);

        if (!focusedEvidence.HasValue)
        {
            Text hint = UIKit.CreateText("Hint", list, UITheme.TypeBody, TextAnchor.UpperLeft, UITheme.TextMuted);
            hint.text = "Выберите улику слева. Здесь появятся факты, с которыми её пробовали соединить или ещё можно проверить.";
            hint.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIKit.Anchor(hint.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(-8f, 140f));
            return;
        }

        EvidenceId focused = focusedEvidence.Value;
        float y = 0f;
        foreach (EvidenceId other in GetDiscoveredEvidence())
        {
            if (other == focused) continue;
            bool selected = secondEvidence == other || firstEvidence == other;
            string status = ConnectionStatus(focused, other, out Color statusColor);
            EvidenceId captured = other;
            AddTwoLineRow(list, ref y, 64f, RunState.EvidenceShortTitle(other), status,
                selected ? UITheme.Selected : statusColor, () => SelectEvidencePair(focused, captured));
        }
        UIKit.SetScrollContentHeight(list, y);
    }

    private void BuildConclusions()
    {
        Image panel = UIKit.CreateTerminalPanel("Conclusions", contentRoot, out RectTransform content, scanlines: false);
        UIKit.FullStretch(panel.rectTransform);
        Text h = UIKit.CreateStencilLabel("УМОЗАКЛЮЧЕНИЯ", content, TextAnchor.UpperLeft);
        h.color = UITheme.Accent;
        UIKit.TopRect(h.rectTransform, 0f, 0f, 0f, 22f);

        List<DeductionId> items = GetDeductions();
        RectTransform list = UIKit.CreateScrollView("ConcList", content, out _);
        UIKit.Stretch((RectTransform)list.parent, 0f, 0f, 0f, 30f);

        if (items.Count == 0)
        {
            Text empty = UIKit.CreateText("Empty", list, UITheme.TypeBody, TextAnchor.UpperLeft, UITheme.TextMuted);
            empty.text = "Выводов пока нет. Вернитесь на вкладку связей и соедините две подходящие улики.";
            empty.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIKit.Anchor(empty.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(-8f, 120f));
            return;
        }

        float y = 0f;
        foreach (DeductionId id in items)
        {
            BuildDeductionChain(list, ref y, id);
        }
        UIKit.SetScrollContentHeight(list, y);
    }

    private void BuildDeductionChain(RectTransform parent, ref float y, DeductionId deduction)
    {
        const float rowH = 118f;
        Image rowBg = UIKit.CreatePanel($"Chain:{deduction}", parent, UITheme.Well);
        UIKit.Anchor(rowBg.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -y), new Vector2(-16f, rowH));
        y += rowH + 12f;

        DeductionId captured = deduction;
        if (!RunState.TryGetDeductionSources(deduction, out EvidenceId first, out EvidenceId second))
        {
            Node(rowBg.rectTransform, new Vector2(0f, 12f), 300f, 62f, RunState.DeductionShortTitle(deduction), NodeDeduction,
                () => OpenDeductionDetail(captured));
            Text note = UIKit.CreateText("Note", rowBg.transform, UITheme.TypeLabel, TextAnchor.LowerCenter, UITheme.TextMuted);
            note.text = "Вывод открыт событием, без ручного соединения.";
            UIKit.Anchor(note.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(560f, 20f));
            return;
        }

        EvidenceId capFirst = first, capSecond = second;
        // Линии между узлами (позиции в локальных координатах rowBg, pivot центр).
        UIKit.CreateLine(rowBg.transform, new Vector2(-190f, 0f), new Vector2(-70f, 0f), UITheme.Accent);
        UIKit.CreateLine(rowBg.transform, new Vector2(70f, 0f), new Vector2(190f, 0f), UITheme.Accent);
        Node(rowBg.rectTransform, new Vector2(-300f, 0f), 220f, 62f, RunState.EvidenceShortTitle(first), NodeEvidence, () => OpenEvidenceDetail(capFirst));
        Node(rowBg.rectTransform, new Vector2(0f, 0f), 250f, 78f, RunState.DeductionShortTitle(deduction), NodeDeduction, () => OpenDeductionDetail(captured));
        Node(rowBg.rectTransform, new Vector2(300f, 0f), 220f, 62f, RunState.EvidenceShortTitle(second), NodeEvidence, () => OpenEvidenceDetail(capSecond));
    }

    private void Node(RectTransform parent, Vector2 center, float w, float h, string label, Color color, Action onClick)
    {
        Button b = UIKit.CreateButton(label, parent, () => onClick?.Invoke(), out Text t);
        t.color = color == NodeDeduction ? UITheme.OnAccent : UITheme.TextPrimary;
        t.fontSize = UITheme.TypeLabel;
        var colors = b.colors; colors.normalColor = color; b.colors = colors;
        UIKit.Anchor(b.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), center, new Vector2(w, h));
    }

    /// <summary>Строка списка с заголовком и подписью-статусом (внутри scroll-content).</summary>
    private void AddTwoLineRow(RectTransform content, ref float y, float height, string title, string sub, Color bg, Action onClick)
    {
        UIKit.CreateListRow(string.Empty, content, () => onClick(), out Image rowBg, out Text _);
        rowBg.color = bg;
        UIKit.AddFrame(rowBg.rectTransform, UITheme.BorderDim, UITheme.BorderDim, UITheme.BorderThin, 0f);
        UIKit.Anchor(rowBg.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -y), new Vector2(-16f, height - 6f));

        Text titleText = UIKit.CreateText("T", rowBg.transform, UITheme.TypeBody, TextAnchor.UpperLeft, UITheme.TextPrimary);
        titleText.text = title;
        titleText.fontStyle = FontStyle.Bold;
        titleText.raycastTarget = false;
        titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
        UIKit.Anchor(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(-20f, 24f));

        Text subText = UIKit.CreateText("S", rowBg.transform, UITheme.TypeLabel, TextAnchor.UpperLeft, UITheme.TextMuted);
        subText.text = sub;
        subText.raycastTarget = false;
        UIKit.Anchor(subText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -32f), new Vector2(-20f, 18f));

        y += height;
    }

    // === Данные / статусы (логика без изменений) ==========================

    private string ConnectionSummaryFor(EvidenceId id)
    {
        int triedCount = 0, successCount = 0;
        foreach (EvidenceConnectionRecord record in RunState.EvidenceConnectionAttempts)
        {
            if (!record.Contains(id)) continue;
            triedCount++;
            if (record.HasDeduction) successCount++;
        }
        if (triedCount == 0) return "Связи ещё не проверялись";
        if (successCount > 0) return $"Проверено: {triedCount}, выводов: {successCount}";
        return $"Проверено: {triedCount}, выводов нет";
    }

    private string ConnectionStatus(EvidenceId first, EvidenceId second, out Color color)
    {
        if (RunState.TryGetEvidenceConnectionAttempt(first, second, out EvidenceConnectionRecord record))
        {
            if (record.Deduction.HasValue)
            {
                color = StatusSuccess;
                return $"Уже связано: {RunState.DeductionShortTitle(record.Deduction.Value)}";
            }
            color = UITheme.Danger;
            return "Уже пробовали: вывода нет";
        }
        color = UITheme.RowNormal;
        return "Не проверено";
    }

    private void HandleEvidenceClick(EvidenceId id)
    {
        if (lastClickedEvidence == id && Time.unscaledTime - lastClickAt < 0.35f)
        {
            OpenEvidenceDetail(id);
            return;
        }
        lastClickedEvidence = id;
        lastClickAt = Time.unscaledTime;
        SelectEvidence(id);
    }

    private void SelectEvidence(EvidenceId id)
    {
        focusedEvidence = id;
        if (!firstEvidence.HasValue || (firstEvidence.HasValue && secondEvidence.HasValue))
        {
            firstEvidence = id;
            secondEvidence = null;
            resultMessage = "Теперь выберите вторую улику справа или в списке слева.";
            Refresh();
            return;
        }
        if (firstEvidence.Value == id)
        {
            ClearSelection("Выбор очищен.");
            return;
        }
        secondEvidence = id;
        resultMessage = "Нажмите «Соединить улики», чтобы проверить связь.";
        Refresh();
    }

    private void SelectEvidencePair(EvidenceId first, EvidenceId second)
    {
        focusedEvidence = first;
        firstEvidence = first;
        secondEvidence = second;
        resultMessage = RunState.TryGetEvidenceConnectionAttempt(first, second, out EvidenceConnectionRecord record)
            ? record.Deduction.HasValue
                ? $"Эта связь уже дала вывод: {RunState.DeductionTitle(record.Deduction.Value)}."
                : "Эту пару уже проверяли: рабочего вывода нет."
            : "Пара выбрана. Нажмите «Соединить улики».";
        Refresh();
    }

    private void ConnectSelected()
    {
        if (!firstEvidence.HasValue || !secondEvidence.HasValue)
        {
            resultMessage = "Нужно выбрать две разные улики.";
            Refresh();
            return;
        }
        if (firstEvidence.Value == secondEvidence.Value)
        {
            resultMessage = "Нельзя соединить улику с самой собой.";
            Refresh();
            return;
        }

        bool alreadyTried = RunState.TryGetEvidenceConnectionAttempt(firstEvidence.Value, secondEvidence.Value, out EvidenceConnectionRecord previous);
        DeductionId? deduction = RunState.TryConnectEvidence(firstEvidence.Value, secondEvidence.Value);
        focusedEvidence = firstEvidence.Value;

        if (!deduction.HasValue)
        {
            resultMessage = alreadyTried && !previous.HasDeduction
                ? "Эта пара уже проверялась: рабочего вывода нет."
                : "Эти факты пока не складываются в рабочий вывод. Попытка сохранена на доске.";
        }
        else
        {
            resultMessage = alreadyTried && previous.Deduction == deduction
                ? $"Этот вывод уже был открыт: {RunState.DeductionTitle(deduction.Value)}."
                : $"Новый вывод: {RunState.DeductionTitle(deduction.Value)}\n\n{RunState.DeductionDescription(deduction.Value)}";
        }
        Refresh();
    }

    private void ClearSelection(string message)
    {
        firstEvidence = null;
        secondEvidence = null;
        focusedEvidence = null;
        resultMessage = message;
        Refresh();
    }

    private void OpenEvidenceDetail(EvidenceId id)
    {
        detailEvidence = id;
        detailDeduction = null;
        ShowDetail();
    }

    private void OpenDeductionDetail(DeductionId id)
    {
        detailDeduction = id;
        detailEvidence = null;
        ShowDetail();
    }

    private bool HasOpenDetail() => detailEvidence.HasValue || detailDeduction.HasValue;

    private void ShowDetail()
    {
        detailTag.text = detailEvidence.HasValue ? "УЛИКА" : "ВЫВОД";
        detailTitle.text = detailEvidence.HasValue
            ? RunState.EvidenceTitle(detailEvidence.Value)
            : RunState.DeductionTitle(detailDeduction.Value);
        detailBody.text = detailEvidence.HasValue
            ? RunState.EvidenceDescription(detailEvidence.Value)
            : RunState.DeductionDescription(detailDeduction.Value);
        detailRoot.SetActive(true);
    }

    private void CloseDetail()
    {
        detailEvidence = null;
        detailDeduction = null;
        if (detailRoot != null) detailRoot.SetActive(false);
    }

    private static List<EvidenceId> GetDiscoveredEvidence()
    {
        var list = new List<EvidenceId>();
        foreach (EvidenceId id in Enum.GetValues(typeof(EvidenceId)))
        {
            if (RunState.HasEvidence(id)) list.Add(id);
        }
        return list;
    }

    private static List<DeductionId> GetDeductions()
    {
        var list = new List<DeductionId>();
        foreach (DeductionId id in Enum.GetValues(typeof(DeductionId)))
        {
            if (RunState.HasDeduction(id)) list.Add(id);
        }
        return list;
    }

    private void OnDestroy()
    {
        if (open) Time.timeScale = previousTimeScale;
        if (instance == this) instance = null;
    }
}
