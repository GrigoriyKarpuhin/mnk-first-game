using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class InvestigationBoardUI : MonoBehaviour
{
    private static readonly Color Background = new Color(0.025f, 0.035f, 0.04f, 0.96f);
    private static readonly Color Panel = new Color(0.07f, 0.10f, 0.105f, 0.98f);
    private static readonly Color Accent = new Color(0.84f, 0.67f, 0.36f, 1f);
    private static InvestigationBoardUI instance;

    private EvidenceId? firstEvidence;
    private EvidenceId? secondEvidence;
    private EvidenceId? detailEvidence;
    private DeductionId? detailDeduction;
    private string resultMessage = "Выберите две улики и соедините их.";
    private Vector2 evidenceScroll;
    private Vector2 deductionScroll;
    private Vector2 detailScroll;
    private float previousTimeScale = 1f;
    private EvidenceId? lastClickedEvidence;
    private DeductionId? lastClickedDeduction;
    private float lastClickAt;

    private GUIStyle titleStyle;
    private GUIStyle headerStyle;
    private GUIStyle bodyStyle;
    private GUIStyle smallStyle;
    private GUIStyle buttonStyle;

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
            if (HasOpenDetail())
            {
                CloseDetail();
            }
            else
            {
                Close();
            }
            return;
        }

        if (Keyboard.current.bKey.wasPressedThisFrame)
        {
            Close();
            return;
        }

        if (!HasOpenDetail() && Keyboard.current.cKey.wasPressedThisFrame)
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
            Rect titleRect = new Rect(margin, 22f, Screen.width - margin * 2f, 48f);
            GUI.Label(titleRect, "ДОСКА РАССЛЕДОВАНИЯ", titleStyle);
            GUI.Label(new Rect(margin, 58f, Screen.width - margin * 2f, 28f), "B — закрыть · Esc — назад · C — соединить · двойной клик — открыть карточку", smallStyle);

            float top = 92f;
            float height = Screen.height - top - 34f;
            Rect evidenceRect = new Rect(margin, top, Screen.width * 0.36f, height);
            Rect centerRect = new Rect(evidenceRect.xMax + 18f, top, Screen.width * 0.27f, height);
            Rect deductionsRect = new Rect(centerRect.xMax + 18f, top, Screen.width - centerRect.xMax - margin - 18f, height);

            DrawPanel(evidenceRect, "УЛИКИ");
            DrawPanel(centerRect, "СВЯЗЬ");
            DrawPanel(deductionsRect, "ВЫВОДЫ");

            DrawEvidenceList(new Rect(evidenceRect.x + 16f, evidenceRect.y + 52f, evidenceRect.width - 32f, evidenceRect.height - 68f));
            DrawConnectionPanel(new Rect(centerRect.x + 16f, centerRect.y + 52f, centerRect.width - 32f, centerRect.height - 68f));
            DrawDeductions(new Rect(deductionsRect.x + 16f, deductionsRect.y + 52f, deductionsRect.width - 32f, deductionsRect.height - 68f));

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

    private void DrawEvidenceList(Rect rect)
    {
        List<EvidenceId> items = GetDiscoveredEvidence();
        if (items.Count == 0)
        {
            GUI.Label(rect, "Улик пока нет. Ищите факты в диалогах, предметах и подслушанных разговорах.", bodyStyle);
            return;
        }

        evidenceScroll = GUI.BeginScrollView(rect, evidenceScroll, new Rect(0f, 0f, rect.width - 18f, items.Count * 68f));
        float y = 0f;
        foreach (EvidenceId id in items)
        {
            bool selected = firstEvidence == id || secondEvidence == id;
            GUI.color = selected ? new Color(0.18f, 0.30f, 0.24f, 1f) : new Color(0.10f, 0.13f, 0.14f, 1f);
            Rect buttonRect = new Rect(0f, y, rect.width - 24f, 56f);
            if (GUI.Button(buttonRect, "", buttonStyle))
            {
                HandleEvidenceClick(id);
            }
            GUI.color = Color.white;

            GUI.Label(new Rect(buttonRect.x + 12f, buttonRect.y + 10f, buttonRect.width - 24f, 36f), RunState.EvidenceTitle(id), headerStyle);
            y += 68f;
        }
        GUI.EndScrollView();
    }

    private void DrawConnectionPanel(Rect rect)
    {
        string first = firstEvidence.HasValue ? RunState.EvidenceTitle(firstEvidence.Value) : "Первая улика не выбрана";
        string second = secondEvidence.HasValue ? RunState.EvidenceTitle(secondEvidence.Value) : "Вторая улика не выбрана";

        GUI.Label(new Rect(rect.x, rect.y, rect.width, 28f), "Выбранные факты", headerStyle);
        DrawSlot(new Rect(rect.x, rect.y + 42f, rect.width, 74f), first, firstEvidence.HasValue);
        DrawSlot(new Rect(rect.x, rect.y + 128f, rect.width, 74f), second, secondEvidence.HasValue);

        GUI.color = Accent;
        if (GUI.Button(new Rect(rect.x, rect.y + 226f, rect.width, 42f), "Соединить улики", buttonStyle))
        {
            ConnectSelected();
        }
        GUI.color = Color.white;

        if (GUI.Button(new Rect(rect.x, rect.y + 278f, rect.width, 32f), "Очистить выбор", buttonStyle))
        {
            firstEvidence = null;
            secondEvidence = null;
            resultMessage = "Выберите две улики и соедините их.";
        }

        GUI.Label(new Rect(rect.x, rect.y + 336f, rect.width, rect.height - 336f), resultMessage, bodyStyle);
    }

    private void DrawSlot(Rect rect, string text, bool filled)
    {
        GUI.color = filled ? new Color(0.16f, 0.22f, 0.18f, 1f) : new Color(0.07f, 0.08f, 0.09f, 1f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(rect.x + 12f, rect.y + 12f, rect.width - 24f, rect.height - 24f), text, bodyStyle);
    }

    private void DrawDeductions(Rect rect)
    {
        List<DeductionId> items = GetDeductions();
        if (items.Count == 0)
        {
            GUI.Label(rect, "Выводов пока нет. Соедините две подходящие улики.", bodyStyle);
            return;
        }

        deductionScroll = GUI.BeginScrollView(rect, deductionScroll, new Rect(0f, 0f, rect.width - 18f, items.Count * 68f));
        float y = 0f;
        foreach (DeductionId id in items)
        {
            Rect itemRect = new Rect(0f, y, rect.width - 24f, 56f);
            GUI.color = new Color(0.12f, 0.12f, 0.10f, 1f);
            if (GUI.Button(itemRect, "", buttonStyle))
            {
                HandleDeductionClick(id);
            }
            GUI.color = Color.white;
            GUI.Label(new Rect(itemRect.x + 12f, itemRect.y + 10f, itemRect.width - 24f, 36f), RunState.DeductionTitle(id), headerStyle);
            y += 68f;
        }
        GUI.EndScrollView();
    }

    private void HandleEvidenceClick(EvidenceId id)
    {
        if (lastClickedEvidence == id && Time.unscaledTime - lastClickAt < 0.35f)
        {
            OpenEvidenceDetail(id);
            return;
        }

        lastClickedEvidence = id;
        lastClickedDeduction = null;
        lastClickAt = Time.unscaledTime;
        SelectEvidence(id);
    }

    private void HandleDeductionClick(DeductionId id)
    {
        if (lastClickedDeduction == id && Time.unscaledTime - lastClickAt < 0.35f)
        {
            OpenDeductionDetail(id);
            return;
        }

        lastClickedDeduction = id;
        lastClickedEvidence = null;
        lastClickAt = Time.unscaledTime;
    }

    private void SelectEvidence(EvidenceId id)
    {
        if (!firstEvidence.HasValue || (firstEvidence.HasValue && secondEvidence.HasValue))
        {
            firstEvidence = id;
            secondEvidence = null;
            resultMessage = "Выберите вторую улику.";
            return;
        }

        if (firstEvidence.Value == id)
        {
            firstEvidence = null;
            secondEvidence = null;
            resultMessage = "Выбор очищен.";
            return;
        }

        secondEvidence = id;
        resultMessage = "Нажмите «Соединить улики».";
    }

    private void ConnectSelected()
    {
        if (!firstEvidence.HasValue || !secondEvidence.HasValue)
        {
            resultMessage = "Нужно выбрать две разные улики.";
            return;
        }

        DeductionId? deduction = RunState.TryConnectEvidence(firstEvidence.Value, secondEvidence.Value);
        if (!deduction.HasValue)
        {
            resultMessage = "Эти факты пока не складываются в рабочий вывод.";
            return;
        }

        resultMessage = $"Новый вывод: {RunState.DeductionTitle(deduction.Value)}\n\n{RunState.DeductionDescription(deduction.Value)}";
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

        GUI.color = new Color(0f, 0f, 0f, 0.72f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float width = Mathf.Min(820f, Screen.width - 120f);
        float height = Mathf.Min(520f, Screen.height - 120f);
        Rect panelRect = new Rect(
            (Screen.width - width) * 0.5f,
            (Screen.height - height) * 0.5f,
            width,
            height);
        DrawPanel(panelRect, label);

        GUI.Label(new Rect(panelRect.x + 28f, panelRect.y + 64f, panelRect.width - 56f, 56f), title, titleStyle);
        Rect textRect = new Rect(panelRect.x + 28f, panelRect.y + 136f, panelRect.width - 56f, panelRect.height - 210f);
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
    }

    private void OnDestroy()
    {
        if (IsOpen) Time.timeScale = previousTimeScale;
    }
}
