using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class InvestigationBoardUI : MonoBehaviour
{
    private enum BoardMode
    {
        Connections,
        Conclusions,
    }

    private static readonly Color Background = new(0.025f, 0.035f, 0.04f, 0.97f);
    private static readonly Color Panel = new(0.07f, 0.10f, 0.105f, 0.98f);
    private static readonly Color PanelSoft = new(0.10f, 0.14f, 0.145f, 0.98f);
    private static readonly Color Selected = new(0.16f, 0.29f, 0.23f, 1f);
    private static readonly Color Muted = new(0.12f, 0.13f, 0.13f, 1f);
    private static readonly Color Accent = new(0.84f, 0.67f, 0.36f, 1f);
    private static readonly Color Cyan = new(0.42f, 0.88f, 0.95f, 1f);
    private static readonly Color Failed = new(0.45f, 0.18f, 0.16f, 1f);
    private static readonly Color Tried = new(0.29f, 0.28f, 0.22f, 1f);

    private static InvestigationBoardUI instance;

    private BoardMode mode = BoardMode.Connections;
    private EvidenceId? focusedEvidence;
    private EvidenceId? firstEvidence;
    private EvidenceId? secondEvidence;
    private EvidenceId? detailEvidence;
    private DeductionId? detailDeduction;
    private string resultMessage = "Выберите улику, затем вторую улику для проверки связи.";
    private Vector2 evidenceScroll;
    private Vector2 candidateScroll;
    private Vector2 conclusionScroll;
    private Vector2 detailScroll;
    private float previousTimeScale = 1f;
    private EvidenceId? lastClickedEvidence;
    private float lastClickAt;

    private GUIStyle titleStyle;
    private GUIStyle headerStyle;
    private GUIStyle bodyStyle;
    private GUIStyle smallStyle;
    private GUIStyle buttonStyle;
    private GUIStyle centeredStyle;

    public static bool IsOpen => instance != null && instance.enabled;

    public static void Toggle()
    {
        if (instance == null)
        {
            var go = new GameObject("Investigation Board UI");
            instance = go.AddComponent<InvestigationBoardUI>();
            DontDestroyOnLoad(go);
            instance.enabled = false;
        }

        if (IsOpen) instance.Close();
        else instance.Open();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        enabled = false;
    }

    private void Update()
    {
        if (!IsOpen || Keyboard.current == null) return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (HasOpenDetail()) CloseDetail();
            else Close();
            return;
        }

        if (Keyboard.current.bKey.wasPressedThisFrame)
        {
            Close();
            return;
        }

        if (HasOpenDetail()) return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame) mode = BoardMode.Connections;
        if (Keyboard.current.digit2Key.wasPressedThisFrame) mode = BoardMode.Conclusions;

        if (mode == BoardMode.Connections && Keyboard.current.cKey.wasPressedThisFrame)
        {
            ConnectSelected();
        }
    }

    private void Open()
    {
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        enabled = true;
    }

    private void Close()
    {
        CloseDetail();
        enabled = false;
        Time.timeScale = previousTimeScale;
    }

    private void OnGUI()
    {
        int previousDepth = GUI.depth;
        GUI.depth = -1000;
        try
        {
            BuildStyles();

            GUI.color = Background;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            const float margin = 34f;
            GUI.Label(new Rect(margin, 20f, Screen.width - margin * 2f, 48f), "ДОСКА РАССЛЕДОВАНИЯ", titleStyle);
            GUI.Label(
                new Rect(margin, 58f, Screen.width - margin * 2f, 26f),
                "B — закрыть · Esc — назад · 1 — связи улик · 2 — выводы · двойной клик — подробности",
                smallStyle);

            DrawTabs(new Rect(margin, 90f, Screen.width - margin * 2f, 44f));

            Rect content = new(margin, 150f, Screen.width - margin * 2f, Screen.height - 184f);
            if (mode == BoardMode.Connections) DrawConnectionsScreen(content);
            else DrawConclusionsScreen(content);

            if (HasOpenDetail())
            {
                DrawDetailOverlay();
            }
        }
        finally
        {
            GUI.depth = previousDepth;
        }
    }

    private void DrawTabs(Rect rect)
    {
        float width = Mathf.Min(300f, rect.width * 0.5f - 8f);
        DrawTabButton(new Rect(rect.x, rect.y, width, rect.height), "1. СВЯЗИ УЛИК", BoardMode.Connections);
        DrawTabButton(new Rect(rect.x + width + 12f, rect.y, width, rect.height), "2. УМОЗАКЛЮЧЕНИЯ", BoardMode.Conclusions);
    }

    private void DrawTabButton(Rect rect, string label, BoardMode targetMode)
    {
        GUI.color = mode == targetMode ? Selected : PanelSoft;
        if (GUI.Button(rect, label, buttonStyle))
        {
            mode = targetMode;
        }
        GUI.color = Color.white;
    }

    private void DrawConnectionsScreen(Rect rect)
    {
        float leftWidth = rect.width * 0.34f;
        float centerWidth = rect.width * 0.29f;
        Rect evidenceRect = new(rect.x, rect.y, leftWidth, rect.height);
        Rect centerRect = new(evidenceRect.xMax + 18f, rect.y, centerWidth, rect.height);
        Rect candidatesRect = new(centerRect.xMax + 18f, rect.y, rect.xMax - centerRect.xMax - 18f, rect.height);

        DrawPanel(evidenceRect, "УЛИКИ");
        DrawPanel(centerRect, "СОЕДИНЕНИЕ");
        DrawPanel(candidatesRect, "ПРОВЕРЕННЫЕ И ВОЗМОЖНЫЕ СВЯЗИ");

        DrawEvidenceList(new Rect(evidenceRect.x + 16f, evidenceRect.y + 52f, evidenceRect.width - 32f, evidenceRect.height - 68f));
        DrawConnectionPanel(new Rect(centerRect.x + 16f, centerRect.y + 52f, centerRect.width - 32f, centerRect.height - 68f));
        DrawCandidateConnections(new Rect(candidatesRect.x + 16f, candidatesRect.y + 52f, candidatesRect.width - 32f, candidatesRect.height - 68f));
    }

    private void DrawEvidenceList(Rect rect)
    {
        List<EvidenceId> items = GetDiscoveredEvidence();
        if (items.Count == 0)
        {
            GUI.Label(rect, "Улик пока нет. Ищите факты в диалогах, предметах и подслушанных разговорах.", bodyStyle);
            return;
        }

        evidenceScroll = GUI.BeginScrollView(rect, evidenceScroll, new Rect(0f, 0f, rect.width - 18f, items.Count * 64f));
        float y = 0f;
        foreach (EvidenceId id in items)
        {
            bool selected = firstEvidence == id || secondEvidence == id;
            bool focused = focusedEvidence == id;
            Rect buttonRect = new(0f, y, rect.width - 24f, 52f);
            GUI.color = focused ? Selected : selected ? Tried : Muted;
            if (GUI.Button(buttonRect, "", buttonStyle))
            {
                HandleEvidenceClick(id);
            }
            GUI.color = Color.white;

            GUI.Label(new Rect(buttonRect.x + 12f, buttonRect.y + 8f, buttonRect.width - 24f, 24f), RunState.EvidenceShortTitle(id), headerStyle);
            GUI.Label(new Rect(buttonRect.x + 12f, buttonRect.y + 31f, buttonRect.width - 24f, 18f), ConnectionSummaryFor(id), smallStyle);
            y += 64f;
        }
        GUI.EndScrollView();
    }

    private string ConnectionSummaryFor(EvidenceId id)
    {
        int triedCount = 0;
        int successCount = 0;
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

    private void DrawConnectionPanel(Rect rect)
    {
        string first = firstEvidence.HasValue ? RunState.EvidenceShortTitle(firstEvidence.Value) : "Первая улика";
        string second = secondEvidence.HasValue ? RunState.EvidenceShortTitle(secondEvidence.Value) : "Вторая улика";

        GUI.Label(new Rect(rect.x, rect.y, rect.width, 28f), "Выбранные факты", headerStyle);
        DrawSlot(new Rect(rect.x, rect.y + 42f, rect.width, 72f), first, firstEvidence.HasValue);
        DrawSlot(new Rect(rect.x, rect.y + 124f, rect.width, 72f), second, secondEvidence.HasValue);

        GUI.color = Accent;
        if (GUI.Button(new Rect(rect.x, rect.y + 216f, rect.width, 44f), "Соединить улики  (C)", buttonStyle))
        {
            ConnectSelected();
        }
        GUI.color = Color.white;

        if (GUI.Button(new Rect(rect.x, rect.y + 270f, rect.width, 34f), "Очистить выбор", buttonStyle))
        {
            ClearSelection("Выбор очищен.");
        }

        GUI.Label(new Rect(rect.x, rect.y + 326f, rect.width, rect.height - 326f), resultMessage, bodyStyle);
    }

    private void DrawSlot(Rect rect, string text, bool filled)
    {
        GUI.color = filled ? Selected : new Color(0.06f, 0.07f, 0.075f, 1f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(rect.x + 12f, rect.y + 10f, rect.width - 24f, rect.height - 20f), text, centeredStyle);
    }

    private void DrawCandidateConnections(Rect rect)
    {
        List<EvidenceId> items = GetDiscoveredEvidence();
        if (!focusedEvidence.HasValue)
        {
            GUI.Label(rect, "Выберите улику слева. Здесь появится список фактов, с которыми её уже пробовали соединить или ещё можно проверить.", bodyStyle);
            return;
        }

        EvidenceId focused = focusedEvidence.Value;
        GUI.Label(new Rect(rect.x, rect.y, rect.width, 52f), $"Выбрано: {RunState.EvidenceShortTitle(focused)}", headerStyle);

        int candidateCount = Mathf.Max(0, items.Count - 1);
        candidateScroll = GUI.BeginScrollView(
            new Rect(rect.x, rect.y + 58f, rect.width, rect.height - 58f),
            candidateScroll,
            new Rect(0f, 0f, rect.width - 18f, candidateCount * 70f));

        float y = 0f;
        foreach (EvidenceId other in items)
        {
            if (other == focused) continue;

            bool selected = secondEvidence == other || firstEvidence == other;
            string status = ConnectionStatus(focused, other, out Color color);
            Rect itemRect = new(0f, y, rect.width - 24f, 58f);
            GUI.color = selected ? Selected : color;
            if (GUI.Button(itemRect, "", buttonStyle))
            {
                SelectEvidencePair(focused, other);
            }
            GUI.color = Color.white;

            GUI.Label(new Rect(itemRect.x + 12f, itemRect.y + 7f, itemRect.width - 24f, 24f), RunState.EvidenceShortTitle(other), headerStyle);
            GUI.Label(new Rect(itemRect.x + 12f, itemRect.y + 32f, itemRect.width - 24f, 20f), status, smallStyle);
            y += 70f;
        }
        GUI.EndScrollView();
    }

    private string ConnectionStatus(EvidenceId first, EvidenceId second, out Color color)
    {
        if (RunState.TryGetEvidenceConnectionAttempt(first, second, out EvidenceConnectionRecord record))
        {
            if (record.Deduction.HasValue)
            {
                color = new Color(0.12f, 0.25f, 0.28f, 1f);
                return $"Уже связано: {RunState.DeductionShortTitle(record.Deduction.Value)}";
            }

            color = Failed;
            return "Уже пробовали: вывода нет";
        }

        color = Muted;
        return "Не проверено";
    }

    private void DrawConclusionsScreen(Rect rect)
    {
        DrawPanel(rect, "УМОЗАКЛЮЧЕНИЯ");

        Rect bodyRect = new(rect.x + 22f, rect.y + 58f, rect.width - 44f, rect.height - 82f);
        List<DeductionId> items = GetDeductions();
        if (items.Count == 0)
        {
            GUI.Label(bodyRect, "Выводов пока нет. Вернитесь на вкладку связей и соедините две подходящие улики.", bodyStyle);
            return;
        }

        conclusionScroll = GUI.BeginScrollView(bodyRect, conclusionScroll, new Rect(0f, 0f, bodyRect.width - 18f, items.Count * 138f));
        float y = 0f;
        foreach (DeductionId id in items)
        {
            DrawDeductionChain(new Rect(0f, y, bodyRect.width - 24f, 112f), id);
            y += 138f;
        }
        GUI.EndScrollView();
    }

    private void DrawDeductionChain(Rect rect, DeductionId deduction)
    {
        GUI.color = new Color(0.04f, 0.055f, 0.06f, 0.9f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        if (!RunState.TryGetDeductionSources(deduction, out EvidenceId first, out EvidenceId second))
        {
            Rect deductionOnly = new(rect.x + rect.width * 0.28f, rect.y + 22f, rect.width * 0.44f, 68f);
            DrawNode(deductionOnly, RunState.DeductionShortTitle(deduction), Cyan, () => OpenDeductionDetail(deduction));
            GUI.Label(new Rect(rect.x + 16f, rect.y + 84f, rect.width - 32f, 22f), "Вывод открыт событием route, без ручного соединения на доске.", smallStyle);
            return;
        }

        float nodeWidth = rect.width * 0.25f;
        Rect left = new(rect.x + 16f, rect.y + 22f, nodeWidth, 68f);
        Rect center = new(rect.x + rect.width * 0.5f - nodeWidth * 0.55f, rect.y + 12f, nodeWidth * 1.1f, 88f);
        Rect right = new(rect.xMax - nodeWidth - 16f, rect.y + 22f, nodeWidth, 68f);

        DrawLine(new Vector2(left.xMax, left.center.y), new Vector2(center.x, center.center.y), Accent);
        DrawLine(new Vector2(center.xMax, center.center.y), new Vector2(right.x, right.center.y), Accent);
        DrawNode(left, RunState.EvidenceShortTitle(first), PanelSoft, () => OpenEvidenceDetail(first));
        DrawNode(center, RunState.DeductionShortTitle(deduction), Cyan, () => OpenDeductionDetail(deduction));
        DrawNode(right, RunState.EvidenceShortTitle(second), PanelSoft, () => OpenEvidenceDetail(second));
    }

    private void DrawNode(Rect rect, string label, Color color, Action onClick)
    {
        GUI.color = color;
        if (GUI.Button(rect, "", buttonStyle))
        {
            onClick?.Invoke();
        }
        GUI.color = Color.white;
        GUI.Label(new Rect(rect.x + 10f, rect.y + 8f, rect.width - 20f, rect.height - 16f), label, centeredStyle);
    }

    private static void DrawLine(Vector2 a, Vector2 b, Color color)
    {
        Matrix4x4 matrix = GUI.matrix;
        Color oldColor = GUI.color;
        float angle = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
        float length = Vector2.Distance(a, b);
        GUI.color = color;
        GUIUtility.RotateAroundPivot(angle, a);
        GUI.DrawTexture(new Rect(a.x, a.y - 1f, length, 2f), Texture2D.whiteTexture);
        GUI.matrix = matrix;
        GUI.color = oldColor;
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
            return;
        }

        if (firstEvidence.Value == id)
        {
            ClearSelection("Выбор очищен.");
            return;
        }

        secondEvidence = id;
        resultMessage = "Нажмите «Соединить улики», чтобы проверить связь.";
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
    }

    private void ConnectSelected()
    {
        if (!firstEvidence.HasValue || !secondEvidence.HasValue)
        {
            resultMessage = "Нужно выбрать две разные улики.";
            return;
        }

        if (firstEvidence.Value == secondEvidence.Value)
        {
            resultMessage = "Нельзя соединить улику с самой собой.";
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
            return;
        }

        resultMessage = alreadyTried && previous.Deduction == deduction
            ? $"Этот вывод уже был открыт: {RunState.DeductionTitle(deduction.Value)}."
            : $"Новый вывод: {RunState.DeductionTitle(deduction.Value)}\n\n{RunState.DeductionDescription(deduction.Value)}";
    }

    private void ClearSelection(string message)
    {
        firstEvidence = null;
        secondEvidence = null;
        focusedEvidence = null;
        resultMessage = message;
    }

    private void OpenEvidenceDetail(EvidenceId id)
    {
        detailEvidence = id;
        detailDeduction = null;
        detailScroll = Vector2.zero;
    }

    private void OpenDeductionDetail(DeductionId id)
    {
        detailDeduction = id;
        detailEvidence = null;
        detailScroll = Vector2.zero;
    }

    private bool HasOpenDetail() => detailEvidence.HasValue || detailDeduction.HasValue;

    private void CloseDetail()
    {
        detailEvidence = null;
        detailDeduction = null;
    }

    private void DrawDetailOverlay()
    {
        string label = detailEvidence.HasValue ? "УЛИКА" : "ВЫВОД";
        string title = detailEvidence.HasValue
            ? RunState.EvidenceTitle(detailEvidence.Value)
            : RunState.DeductionTitle(detailDeduction.Value);
        string description = detailEvidence.HasValue
            ? RunState.EvidenceDescription(detailEvidence.Value)
            : RunState.DeductionDescription(detailDeduction.Value);

        GUI.color = new Color(0f, 0f, 0f, 0.76f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float width = Mathf.Min(860f, Screen.width - 120f);
        float height = Mathf.Min(540f, Screen.height - 120f);
        Rect panelRect = new(
            (Screen.width - width) * 0.5f,
            (Screen.height - height) * 0.5f,
            width,
            height);
        DrawPanel(panelRect, label);

        GUI.Label(new Rect(panelRect.x + 28f, panelRect.y + 64f, panelRect.width - 56f, 60f), title, titleStyle);
        Rect textRect = new(panelRect.x + 28f, panelRect.y + 144f, panelRect.width - 56f, panelRect.height - 220f);
        float contentWidth = textRect.width - 26f;
        float contentHeight = Mathf.Max(textRect.height, bodyStyle.CalcHeight(new GUIContent(description), contentWidth));
        detailScroll = GUI.BeginScrollView(textRect, detailScroll, new Rect(0f, 0f, textRect.width - 18f, contentHeight));
        GUI.Label(new Rect(0f, 0f, contentWidth, contentHeight), description, bodyStyle);
        GUI.EndScrollView();

        if (GUI.Button(new Rect(panelRect.xMax - 180f, panelRect.yMax - 58f, 148f, 36f), "Закрыть", buttonStyle))
        {
            CloseDetail();
        }
        GUI.Label(new Rect(panelRect.x + 28f, panelRect.yMax - 54f, panelRect.width - 240f, 28f), "Esc — назад к доске", smallStyle);
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

    private void DrawPanel(Rect rect, string title)
    {
        GUI.color = Panel;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Accent;
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.y, 2f, rect.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMax - 2f, rect.y, 2f, rect.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(rect.x + 16f, rect.y + 14f, rect.width - 32f, 28f), title, headerStyle);
    }

    private void BuildStyles()
    {
        if (titleStyle != null) return;

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 34,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Accent },
        };
        headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            wordWrap = true,
            normal = { textColor = Color.white },
        };
        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 17,
            wordWrap = true,
            normal = { textColor = Color.white },
        };
        smallStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            wordWrap = true,
            normal = { textColor = new Color(0.84f, 0.88f, 0.84f) },
        };
        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 16,
            wordWrap = true,
            alignment = TextAnchor.MiddleCenter,
        };
        centeredStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 17,
            fontStyle = FontStyle.Bold,
            wordWrap = true,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white },
        };
    }

    private void OnDestroy()
    {
        if (IsOpen) Time.timeScale = previousTimeScale;
    }
}
