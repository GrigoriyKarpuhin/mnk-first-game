using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Два режима сообщений:
/// - короткое системное уведомление, которое исчезает само;
/// - модальный разговор в большой нижней панели с портретом и вариантами ответа.
/// </summary>
public class DialogueUI : MonoBehaviour
{
    private static readonly Color DialogueBackground = new Color(0.055f, 0.075f, 0.07f, 0.97f);
    private static readonly Color DialogueBorder = new Color(0.38f, 0.78f, 0.58f, 1f);
    private static readonly Color ChoiceNormal = new Color(0.12f, 0.22f, 0.18f, 0.98f);
    private static readonly Color ChoiceHover = new Color(0.20f, 0.38f, 0.29f, 1f);

    private static DialogueUI instance;

    private Canvas canvas;
    private GameObject notificationPanel;
    private Text notificationLabel;
    private GameObject dialoguePanel;
    private Image portrait;
    private GameObject portraitFrame;
    private Text speakerLabel;
    private Text dialogueLabel;
    private Text continueLabel;
    private Button continueButton;
    private float hideAt;
    private readonly List<Button> choiceButtons = new();
    private readonly List<Text> choiceLabels = new();
    private readonly List<Action> choiceActions = new();
    private DialogueLine[] sequenceLines;
    private int sequenceIndex;
    private bool modal;
    private bool waitingForContinue;
    private float timeScaleBeforeDialogue = 1f;

    public readonly struct DialogueChoice
    {
        public readonly string Text;
        public readonly Action Action;

        public DialogueChoice(string text, Action action)
        {
            Text = text;
            Action = action;
        }
    }

    public readonly struct DialogueLine
    {
        public readonly string Speaker;
        public readonly string Text;
        public readonly string PortraitResource;

        public DialogueLine(string speaker, string text, string portraitResource = null)
        {
            Speaker = speaker;
            Text = text;
            PortraitResource = portraitResource;
        }
    }

    public static bool IsModalOpen =>
        (instance != null && instance.modal) || QuestJournalUI.IsOpen || InvestigationBoardUI.IsOpen;
    public static bool IsDialogueOpen => instance != null && instance.modal;

    public static DialogueUI Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("DialogueUI");
                instance = go.AddComponent<DialogueUI>();
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
        EnsureEventSystem();

        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        BuildNotification();
        BuildDialoguePanel();
    }

    private void BuildNotification()
    {
        notificationPanel = CreatePanel("Notification", canvas.transform, new Color(0f, 0f, 0f, 0.82f));
        RectTransform panelRect = notificationPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 24f);
        panelRect.sizeDelta = new Vector2(680f, 72f);

        notificationLabel = CreateText("Text", notificationPanel.transform, 22, TextAnchor.MiddleCenter);
        Stretch(notificationLabel.rectTransform, 18f, 10f, 18f, 10f);
        notificationLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
        notificationLabel.verticalOverflow = VerticalWrapMode.Truncate;
        notificationPanel.SetActive(false);
    }

    private void BuildDialoguePanel()
    {
        dialoguePanel = CreatePanel("Dialogue", canvas.transform, DialogueBackground);
        AddBorder(dialoguePanel.transform, DialogueBorder, 3f);

        RectTransform panelRect = dialoguePanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.04f, 0f);
        panelRect.anchorMax = new Vector2(0.96f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 22f);
        panelRect.sizeDelta = new Vector2(0f, 360f);

        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(dialoguePanel.transform, false);
        Stretch(content.GetComponent<RectTransform>(), 24f, 22f, 220f, 22f);

        speakerLabel = CreateText("Speaker", content.transform, 26, TextAnchor.UpperLeft);
        speakerLabel.fontStyle = FontStyle.Bold;
        speakerLabel.color = DialogueBorder;
        RectTransform speakerRect = speakerLabel.rectTransform;
        speakerRect.anchorMin = new Vector2(0f, 1f);
        speakerRect.anchorMax = new Vector2(1f, 1f);
        speakerRect.pivot = new Vector2(0.5f, 1f);
        speakerRect.anchoredPosition = Vector2.zero;
        speakerRect.sizeDelta = new Vector2(0f, 38f);

        dialogueLabel = CreateText("Dialogue Text", content.transform, 25, TextAnchor.UpperLeft);
        dialogueLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
        dialogueLabel.verticalOverflow = VerticalWrapMode.Truncate;
        RectTransform dialogueRect = dialogueLabel.rectTransform;
        dialogueRect.anchorMin = new Vector2(0f, 1f);
        dialogueRect.anchorMax = new Vector2(1f, 1f);
        dialogueRect.pivot = new Vector2(0.5f, 1f);
        dialogueRect.anchoredPosition = new Vector2(0f, -44f);
        dialogueRect.sizeDelta = new Vector2(0f, 108f);

        continueLabel = CreateText("Continue Hint", dialoguePanel.transform, 17, TextAnchor.LowerRight);
        continueLabel.text = "E / Space / клик";
        continueLabel.color = new Color(0.72f, 0.86f, 0.78f);
        RectTransform continueRect = continueLabel.rectTransform;
        continueRect.anchorMin = new Vector2(1f, 0f);
        continueRect.anchorMax = new Vector2(1f, 0f);
        continueRect.pivot = new Vector2(1f, 0f);
        continueRect.anchoredPosition = new Vector2(-22f, 14f);
        continueRect.sizeDelta = new Vector2(180f, 26f);

        portraitFrame = CreatePanel("Portrait Frame", dialoguePanel.transform, new Color(0.09f, 0.14f, 0.12f, 1f));
        AddBorder(portraitFrame.transform, DialogueBorder, 2f);
        RectTransform portraitFrameRect = portraitFrame.GetComponent<RectTransform>();
        portraitFrameRect.anchorMin = new Vector2(1f, 0.5f);
        portraitFrameRect.anchorMax = new Vector2(1f, 0.5f);
        portraitFrameRect.pivot = new Vector2(1f, 0.5f);
        portraitFrameRect.anchoredPosition = new Vector2(-22f, 0f);
        portraitFrameRect.sizeDelta = new Vector2(174f, 270f);

        var portraitObject = new GameObject("Portrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        portraitObject.transform.SetParent(portraitFrame.transform, false);
        portrait = portraitObject.GetComponent<Image>();
        portrait.preserveAspect = true;
        portrait.color = Color.white;
        Stretch(portrait.rectTransform, 14f, 14f, 14f, 14f);

        continueButton = dialoguePanel.AddComponent<Button>();
        continueButton.transition = Selectable.Transition.None;
        continueButton.onClick.AddListener(ContinueDialogue);

        dialoguePanel.SetActive(false);
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;

        var eventSystemObject = new GameObject("EventSystem");
        DontDestroyOnLoad(eventSystemObject);
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
    }

    private void Update()
    {
        if (modal)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (waitingForContinue &&
                (keyboard.eKey.wasPressedThisFrame ||
                 keyboard.spaceKey.wasPressedThisFrame ||
                 keyboard.enterKey.wasPressedThisFrame))
            {
                ContinueDialogue();
                return;
            }

            if (!waitingForContinue)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) SelectChoice(0);
                else if (keyboard.digit2Key.wasPressedThisFrame) SelectChoice(1);
                else if (keyboard.digit3Key.wasPressedThisFrame) SelectChoice(2);
                else if (keyboard.digit4Key.wasPressedThisFrame) SelectChoice(3);
            }
            return;
        }

        if (notificationPanel != null && notificationPanel.activeSelf && Time.time >= hideAt)
        {
            notificationPanel.SetActive(false);
        }
    }

    public void Show(string message, float duration = 1.6f)
    {
        EnsureBuilt();
        notificationLabel.text = message;
        notificationPanel.SetActive(true);
        hideAt = Time.time + Mathf.Max(0.1f, duration);
    }

    public void ShowDialogue(string speaker, string message, string portraitResource = null)
    {
        EnsureBuilt();
        sequenceLines = null;
        sequenceIndex = 0;
        OpenDialogue(speaker, message, portraitResource, true);
    }

    public void ShowDialogueSequence(params DialogueLine[] lines)
    {
        EnsureBuilt();
        if (lines == null || lines.Length == 0) return;

        sequenceLines = lines;
        sequenceIndex = 0;
        DialogueLine line = sequenceLines[sequenceIndex];
        OpenDialogue(line.Speaker, line.Text, line.PortraitResource, true);
    }

    public void ShowChoices(
        string speaker,
        string message,
        string portraitResource,
        params DialogueChoice[] choices)
    {
        EnsureBuilt();
        sequenceLines = null;
        sequenceIndex = 0;
        OpenDialogue(speaker, message, portraitResource, false);
        CreateChoices(choices);
    }

    /// <summary>Совместимость с ранними вызовами без имени и портрета.</summary>
    public void ShowChoices(string message, params DialogueChoice[] choices)
    {
        ShowChoices(string.Empty, message, null, choices);
    }

    private void OpenDialogue(string speaker, string message, string portraitResource, bool canContinue)
    {
        notificationPanel.SetActive(false);
        ClearChoices();
        if (!modal)
        {
            timeScaleBeforeDialogue = Time.timeScale;
            Time.timeScale = 0f;
        }
        modal = true;
        waitingForContinue = canContinue;
        continueButton.enabled = canContinue;
        continueLabel.gameObject.SetActive(canContinue);
        speakerLabel.text = speaker ?? string.Empty;
        speakerLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(speaker));
        dialogueLabel.text = message ?? string.Empty;

        Sprite portraitSprite = string.IsNullOrWhiteSpace(portraitResource)
            ? null
            : Resources.Load<Sprite>($"Sprites/{portraitResource}");
        portrait.sprite = portraitSprite;
        portraitFrame.SetActive(portraitSprite != null);

        dialoguePanel.SetActive(true);
    }

    private void CreateChoices(DialogueChoice[] choices)
    {
        int count = Mathf.Max(1, choices.Length);
        float gap = 8f;
        float availableHeight = 166f;
        float buttonHeight = (availableHeight - gap * (count - 1)) / count;
        for (int i = 0; i < choices.Length; i++)
        {
            int choiceIndex = i;
            Button button = CreateChoiceButton(
                $"{i + 1}. {choices[i].Text}",
                i,
                count,
                buttonHeight,
                gap);
            button.onClick.AddListener(() => SelectChoice(choiceIndex));
            choiceButtons.Add(button);
            choiceActions.Add(choices[i].Action);
        }
    }

    private Button CreateChoiceButton(string text, int index, int count, float height, float gap)
    {
        GameObject buttonObject = CreatePanel(
            $"Choice {index + 1}",
            dialoguePanel.transform,
            new Color(0.15f, 0.34f, 0.25f, 0.34f));
        buttonObject.transform.SetAsLastSibling();
        Button button = buttonObject.AddComponent<Button>();
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.45f, 1.45f, 1.45f, 1f);
        colors.selectedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
        colors.pressedColor = DialogueBorder;
        button.colors = colors;

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(-110f, 22f + (count - index - 1) * (height + gap));
        rect.sizeDelta = new Vector2(-268f, height);

        Text choiceLabel = CreateText($"Choice Label {index + 1}", dialoguePanel.transform, 20, TextAnchor.MiddleLeft);
        choiceLabel.text = text;
        choiceLabel.fontStyle = FontStyle.Bold;
        choiceLabel.color = Color.white;
        choiceLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
        choiceLabel.verticalOverflow = VerticalWrapMode.Truncate;
        choiceLabel.raycastTarget = false;
        RectTransform labelRect = choiceLabel.rectTransform;
        labelRect.anchorMin = rect.anchorMin;
        labelRect.anchorMax = rect.anchorMax;
        labelRect.pivot = rect.pivot;
        labelRect.anchoredPosition = rect.anchoredPosition + new Vector2(14f, 0f);
        labelRect.sizeDelta = rect.sizeDelta + new Vector2(-24f, 0f);
        choiceLabel.transform.SetAsLastSibling();
        choiceLabels.Add(choiceLabel);
        return button;
    }

    private void SelectChoice(int index)
    {
        if (!modal || waitingForContinue || index < 0 || index >= choiceActions.Count) return;
        Action action = choiceActions[index];
        CloseDialogue();
        action?.Invoke();
    }

    private void ContinueDialogue()
    {
        if (sequenceLines != null && sequenceIndex + 1 < sequenceLines.Length)
        {
            sequenceIndex++;
            DialogueLine line = sequenceLines[sequenceIndex];
            OpenDialogue(line.Speaker, line.Text, line.PortraitResource, true);
            return;
        }

        CloseDialogue();
    }

    private void CloseDialogue()
    {
        if (!modal) return;
        modal = false;
        waitingForContinue = false;
        sequenceLines = null;
        sequenceIndex = 0;
        Time.timeScale = timeScaleBeforeDialogue;
        ClearChoices();
        dialoguePanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (modal) Time.timeScale = timeScaleBeforeDialogue;
    }

    private void ClearChoices()
    {
        foreach (Button button in choiceButtons)
        {
            if (button != null) Destroy(button.gameObject);
        }
        foreach (Text choiceLabel in choiceLabels)
        {
            if (choiceLabel != null) Destroy(choiceLabel.gameObject);
        }
        choiceButtons.Clear();
        choiceLabels.Clear();
        choiceActions.Clear();
    }

    private void EnsureBuilt()
    {
        if (canvas == null || notificationPanel == null || dialoguePanel == null) BuildUI();
    }

    private static GameObject CreatePanel(string name, Transform parent, Color color)
    {
        var panelObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelObject.transform.SetParent(parent, false);
        panelObject.GetComponent<Image>().color = color;
        return panelObject;
    }

    private static Text CreateText(string name, Transform parent, int fontSize, TextAnchor alignment)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(parent, false);
        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.supportRichText = true;
        return text;
    }

    private static void AddBorder(Transform parent, Color color, float thickness)
    {
        CreateBorder("Top", parent, color, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -thickness), new Vector2(0f, thickness));
        CreateBorder("Bottom", parent, color, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, thickness));
        CreateBorder("Left", parent, color, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(thickness, 0f));
        CreateBorder("Right", parent, color, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-thickness, 0f), new Vector2(thickness, 0f));
    }

    private static void CreateBorder(
        string name,
        Transform parent,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        GameObject border = CreatePanel(name, parent, color);
        RectTransform rect = border.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }

    private static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }
}

public sealed class QuestJournalUI : MonoBehaviour
{
    private static readonly Color Background = new Color(0.025f, 0.045f, 0.055f, 0.97f);
    private static readonly Color Panel = new Color(0.08f, 0.11f, 0.12f, 0.98f);
    private static readonly Color Accent = new Color(0.38f, 0.78f, 0.58f, 1f);
    private static QuestJournalUI instance;

    private Canvas canvas;
    private GameObject root;
    private Text statusLabel;
    private Text titleLabel;
    private Text descriptionLabel;
    private Text stepsLabel;
    private readonly List<Button> taskButtons = new();
    private int selectedTask;
    private float previousTimeScale = 1f;

    public static bool IsOpen => instance != null && instance.root != null && instance.root.activeSelf;

    public static void Toggle()
    {
        if (instance == null)
        {
            var go = new GameObject("Quest Journal UI");
            instance = go.AddComponent<QuestJournalUI>();
            DontDestroyOnLoad(go);
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
        BuildUI();
    }

    private void BuildUI()
    {
        EnsureEventSystem();

        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1100;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        root = CreatePanel("Journal", canvas.transform, Background);
        Stretch(root.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);

        Text journalTitle = CreateText("Journal Title", root.transform, 40, TextAnchor.UpperLeft);
        journalTitle.text = "ЖУРНАЛ";
        journalTitle.fontStyle = FontStyle.Bold;
        journalTitle.color = Accent;
        RectTransform journalTitleRect = journalTitle.rectTransform;
        journalTitleRect.anchorMin = new Vector2(0f, 1f);
        journalTitleRect.anchorMax = new Vector2(1f, 1f);
        journalTitleRect.pivot = new Vector2(0.5f, 1f);
        journalTitleRect.anchoredPosition = new Vector2(0f, -32f);
        journalTitleRect.sizeDelta = new Vector2(-80f, 56f);

        Text closeHint = CreateText("Close Hint", root.transform, 18, TextAnchor.UpperRight);
        closeHint.text = "J / Esc — закрыть";
        closeHint.color = new Color(0.7f, 0.78f, 0.75f);
        RectTransform closeRect = closeHint.rectTransform;
        closeRect.anchorMin = new Vector2(0f, 1f);
        closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(0.5f, 1f);
        closeRect.anchoredPosition = new Vector2(0f, -42f);
        closeRect.sizeDelta = new Vector2(-80f, 32f);

        GameObject listPanel = CreatePanel("Task List", root.transform, Panel);
        RectTransform listRect = listPanel.GetComponent<RectTransform>();
        listRect.anchorMin = new Vector2(0.04f, 0.08f);
        listRect.anchorMax = new Vector2(0.32f, 0.84f);
        listRect.offsetMin = Vector2.zero;
        listRect.offsetMax = Vector2.zero;
        AddBorder(listPanel.transform, Accent, 2f);

        Text listHeader = CreateText("List Header", listPanel.transform, 22, TextAnchor.UpperLeft);
        listHeader.text = "ЗАДАЧИ";
        listHeader.fontStyle = FontStyle.Bold;
        listHeader.color = Accent;
        Stretch(listHeader.rectTransform, 20f, 20f, 20f, 20f);

        CreateTaskButton(listPanel.transform, 0, "1. Кто я и почему я здесь?");
        CreateTaskButton(listPanel.transform, 1, "2. Особая стратегия заключённой");
        CreateTaskButton(listPanel.transform, 2, "3. Передатчик для программиста");

        GameObject detailsPanel = CreatePanel("Details", root.transform, Panel);
        RectTransform detailsRect = detailsPanel.GetComponent<RectTransform>();
        detailsRect.anchorMin = new Vector2(0.35f, 0.08f);
        detailsRect.anchorMax = new Vector2(0.96f, 0.84f);
        detailsRect.offsetMin = Vector2.zero;
        detailsRect.offsetMax = Vector2.zero;
        AddBorder(detailsPanel.transform, Accent, 2f);

        statusLabel = CreateText("Status", detailsPanel.transform, 18, TextAnchor.UpperLeft);
        statusLabel.color = Accent;
        SetTopRect(statusLabel.rectTransform, 24f, 22f, 24f, 30f);

        titleLabel = CreateText("Title", detailsPanel.transform, 30, TextAnchor.UpperLeft);
        titleLabel.fontStyle = FontStyle.Bold;
        SetTopRect(titleLabel.rectTransform, 24f, 58f, 24f, 48f);

        descriptionLabel = CreateText("Description", detailsPanel.transform, 21, TextAnchor.UpperLeft);
        descriptionLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
        descriptionLabel.verticalOverflow = VerticalWrapMode.Truncate;
        SetTopRect(descriptionLabel.rectTransform, 24f, 126f, 24f, 130f);

        stepsLabel = CreateText("Steps", detailsPanel.transform, 20, TextAnchor.UpperLeft);
        stepsLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
        stepsLabel.verticalOverflow = VerticalWrapMode.Truncate;
        RectTransform stepsRect = stepsLabel.rectTransform;
        stepsRect.anchorMin = new Vector2(0f, 0f);
        stepsRect.anchorMax = new Vector2(1f, 0f);
        stepsRect.pivot = new Vector2(0.5f, 0f);
        stepsRect.anchoredPosition = new Vector2(0f, 24f);
        stepsRect.sizeDelta = new Vector2(-48f, 180f);

        root.SetActive(false);
    }

    private void Update()
    {
        if (!IsOpen || Keyboard.current == null) return;
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Close();
            return;
        }

        if (Keyboard.current.digit1Key.wasPressedThisFrame) SelectTask(0);
        else if (Keyboard.current.digit2Key.wasPressedThisFrame) SelectTask(1);
        else if (Keyboard.current.digit3Key.wasPressedThisFrame) SelectTask(2);
        else if (Keyboard.current.upArrowKey.wasPressedThisFrame) SelectTask(selectedTask - 1);
        else if (Keyboard.current.downArrowKey.wasPressedThisFrame) SelectTask(selectedTask + 1);
    }

    private void Open()
    {
        Refresh();
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        root.SetActive(true);
    }

    private void Close()
    {
        root.SetActive(false);
        Time.timeScale = previousTimeScale;
    }

    private void Refresh()
    {
        RefreshTaskButtons();
        if (selectedTask == 0)
        {
            statusLabel.text = "СКВОЗНОЙ КВЕСТ";
            titleLabel.text = "Кто я и почему я здесь?";
            descriptionLabel.text =
                "Вы очнулись в экспериментальной тюрьме без воспоминаний о своей личности и прошлом. " +
                "Администрация явно знает о вас больше, чем говорит. Ответы могут быть связаны с настоящей целью экспериментов.";
            stepsLabel.text =
                $"{Mark(true)} Осознать, что вы ничего не помните.\n\n" +
                $"{Mark(false)} Узнать, кем вы были до заключения.\n\n" +
                $"{Mark(false)} Понять, почему администрация выбрала именно вас.\n\n" +
                $"{Mark(false)} Восстановить полную картину произошедшего.";
            return;
        }

        if (selectedTask == 1)
        {
            CompetitorQuestStage competitorStage = RunState.CompetitorQuest;
            statusLabel.text = competitorStage switch
            {
                CompetitorQuestStage.Unknown => "НЕ НАЧАТО",
                CompetitorQuestStage.GardenAccess => "ОТКРЫТ МАРШРУТ",
                CompetitorQuestStage.SmokeScheduleKnown => "РАСПИСАНИЕ ПОЛУЧЕНО",
                CompetitorQuestStage.Overheard => "УЛИКА ПОЛУЧЕНА",
                _ => "АКТИВНО",
            };
            titleLabel.text = "Особая стратегия заключённой";
            descriptionLabel.text =
                "Программист слышал, что заключённая действует по собственному расписанию и иногда исчезает из общей зоны. " +
                "Прямой разговор с ней почти ничего не даст, но наблюдение может раскрыть её связи с персоналом.";
            stepsLabel.text = BuildCompetitorSteps(competitorStage);
            return;
        }

        ProgrammerQuestStage stage = RunState.ProgrammerQuest;
        statusLabel.text = stage switch
        {
            ProgrammerQuestStage.Completed => "ЗАВЕРШЕНО",
            ProgrammerQuestStage.AnalyzingTransmitter => "ОЖИДАНИЕ",
            ProgrammerQuestStage.DayTwoQuestAvailable => "НОВЫЕ ДАННЫЕ",
            ProgrammerQuestStage.Rejected => "ПРОВАЛЕНО",
            ProgrammerQuestStage.Ignored => "ОТЛОЖЕНО",
            ProgrammerQuestStage.NotStarted => "НЕ НАЧАТО",
            _ => "АКТИВНО",
        };
        titleLabel.text = "Передатчик для программиста";
        descriptionLabel.text =
            "Программист считает, что автоматизированная система подбирает эксперименты под заключённых. " +
            "Передатчик из инженерной зоны позволит ему попытаться получать часть данных заранее.";
        stepsLabel.text = BuildSteps(stage);
    }

    private void CreateTaskButton(Transform parent, int index, string title)
    {
        GameObject buttonObject = CreatePanel($"Task {index + 1}", parent, Color.white);
        var button = buttonObject.AddComponent<Button>();
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
        colors.selectedColor = Color.white;
        colors.pressedColor = Accent;
        button.colors = colors;
        button.onClick.AddListener(() => SelectTask(index));
        taskButtons.Add(button);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -68f - index * 78f);
        rect.sizeDelta = new Vector2(-32f, 68f);

        Text text = CreateText("Text", buttonObject.transform, 19, TextAnchor.MiddleLeft);
        text.text = title;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        Stretch(text.rectTransform, 12f, 6f, 8f, 6f);
    }

    private void SelectTask(int index)
    {
        selectedTask = Mathf.Clamp(index, 0, taskButtons.Count - 1);
        Refresh();
    }

    private void RefreshTaskButtons()
    {
        for (int i = 0; i < taskButtons.Count; i++)
        {
            Image image = taskButtons[i].GetComponent<Image>();
            image.color = i == selectedTask
                ? new Color(0.18f, 0.38f, 0.29f, 1f)
                : new Color(0.10f, 0.14f, 0.15f, 1f);
        }
    }

    private static string BuildSteps(ProgrammerQuestStage stage)
    {
        bool accepted = stage == ProgrammerQuestStage.Accepted ||
                        stage == ProgrammerQuestStage.TransmitterAcquired ||
                        stage == ProgrammerQuestStage.Completed;
        bool acquired = stage == ProgrammerQuestStage.TransmitterAcquired ||
                        stage == ProgrammerQuestStage.Completed;
        bool analyzing = stage == ProgrammerQuestStage.AnalyzingTransmitter ||
                         stage == ProgrammerQuestStage.DayTwoQuestAvailable ||
                         stage == ProgrammerQuestStage.Completed;
        bool dayTwo = stage == ProgrammerQuestStage.DayTwoQuestAvailable;

        return $"{Mark(accepted)} Договориться с программистом.\n\n" +
               $"{Mark(acquired)} Проникнуть в инженерную зону и найти передатчик.\n\n" +
               $"{Mark(analyzing)} Вернуть передатчик программисту.\n\n" +
               $"{Mark(dayTwo)} Дождаться, пока программист разберёт часть данных.";
    }

    private static string BuildCompetitorSteps(CompetitorQuestStage stage)
    {
        bool started = stage != CompetitorQuestStage.Unknown;
        bool reached = stage == CompetitorQuestStage.ReachedStaffRoom ||
                       stage == CompetitorQuestStage.Overheard ||
                       stage == CompetitorQuestStage.SmokeScheduleKnown ||
                       stage == CompetitorQuestStage.GardenAccess;
        bool overheard = stage == CompetitorQuestStage.Overheard ||
                         stage == CompetitorQuestStage.SmokeScheduleKnown ||
                         stage == CompetitorQuestStage.GardenAccess;
        bool schedule = stage == CompetitorQuestStage.SmokeScheduleKnown ||
                        stage == CompetitorQuestStage.GardenAccess;
        bool garden = stage == CompetitorQuestStage.GardenAccess;

        return $"{Mark(started)} Узнать слух о заключённой у программиста.\n\n" +
               $"{Mark(reached)} Проследить за её утренним маршрутом.\n\n" +
               $"{Mark(overheard)} Подслушать разговор в комнате персонала.\n\n" +
               $"{Mark(schedule)} Получить расписание перекуров после помощи в эксперименте.\n\n" +
               $"{Mark(garden)} Найти путь через сад к блоку C.";
    }

    private static string Mark(bool done) => done ? "<color=#75D99A>✓</color>" : "○";

    private void OnDestroy()
    {
        if (IsOpen) Time.timeScale = previousTimeScale;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;

        var eventSystemObject = new GameObject("EventSystem");
        DontDestroyOnLoad(eventSystemObject);
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
    }

    private static GameObject CreatePanel(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        return go;
    }

    private static Text CreateText(string name, Transform parent, int fontSize, TextAnchor alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        Text text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.supportRichText = true;
        return text;
    }

    private static void AddBorder(Transform parent, Color color, float thickness)
    {
        CreateBorder(parent, color, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -thickness), new Vector2(0f, thickness));
        CreateBorder(parent, color, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, thickness));
        CreateBorder(parent, color, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(thickness, 0f));
        CreateBorder(parent, color, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-thickness, 0f), new Vector2(thickness, 0f));
    }

    private static void CreateBorder(Transform parent, Color color, Vector2 min, Vector2 max, Vector2 position, Vector2 size)
    {
        GameObject border = CreatePanel("Border", parent, color);
        RectTransform rect = border.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void SetTopRect(RectTransform rect, float left, float top, float right, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2((left - right) * 0.5f, -top);
        rect.sizeDelta = new Vector2(-(left + right), height);
    }
}
