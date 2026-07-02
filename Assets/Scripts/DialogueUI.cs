using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Два режима сообщений:
/// - короткое системное уведомление, которое исчезает само;
/// - модальный разговор в большой нижней панели с портретом и вариантами ответа.
/// </summary>
public class DialogueUI : MonoBehaviour
{
    private static DialogueUI instance;

    private Canvas canvas;
    private GameObject notificationPanel;
    private Text notificationLabel;
    private GameObject dialoguePanel;
    private RectTransform dialogueContent;
    private Image portrait;
    private GameObject portraitFrame;
    private Text speakerLabel;
    private Text dialogueLabel;
    private Text continueLabel;
    private Button continueButton;
    private float hideAt;
    private readonly List<Button> choiceButtons = new();
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
        (instance != null && instance.modal) || QuestJournalUI.IsOpen || InvestigationBoardUI.IsOpen || PrisonMapUI.IsOpen;
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
        canvas = UIKit.CreateRootCanvas(gameObject, UITheme.SortDialogue);

        BuildNotification();
        BuildDialoguePanel();
    }

    private void BuildNotification()
    {
        notificationPanel = UIKit.CreateToast(canvas.transform, out notificationLabel);
        var panelRect = (RectTransform)notificationPanel.transform;
        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -UITheme.Space6);
        panelRect.sizeDelta = new Vector2(680f, 72f);
    }

    private void BuildDialoguePanel()
    {
        Image panel = UIKit.CreateTerminalPanel("Dialogue", canvas.transform, out RectTransform content);
        dialoguePanel = panel.gameObject;
        dialogueContent = content;

        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = new Vector2(0.04f, 0f);
        panelRect.anchorMax = new Vector2(0.96f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, UITheme.Space6);
        panelRect.sizeDelta = new Vector2(0f, 360f);

        speakerLabel = UIKit.CreateText("Speaker", content, UITheme.TypeTitle, TextAnchor.UpperLeft, UITheme.Accent);
        speakerLabel.fontStyle = FontStyle.Bold;
        RectTransform speakerRect = speakerLabel.rectTransform;
        speakerRect.anchorMin = new Vector2(0f, 1f);
        speakerRect.anchorMax = new Vector2(1f, 1f);
        speakerRect.pivot = new Vector2(0.5f, 1f);
        speakerRect.anchoredPosition = Vector2.zero;
        speakerRect.sizeDelta = new Vector2(-200f, 40f);

        dialogueLabel = UIKit.CreateText("Dialogue Text", content, UITheme.TypeBody, TextAnchor.UpperLeft, UITheme.TextPrimary);
        dialogueLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
        dialogueLabel.verticalOverflow = VerticalWrapMode.Truncate;
        RectTransform dialogueRect = dialogueLabel.rectTransform;
        dialogueRect.anchorMin = new Vector2(0f, 1f);
        dialogueRect.anchorMax = new Vector2(1f, 1f);
        dialogueRect.pivot = new Vector2(0.5f, 1f);
        dialogueRect.anchoredPosition = new Vector2(0f, -48f);
        dialogueRect.sizeDelta = new Vector2(-200f, 120f);

        continueLabel = UIKit.CreateStencilLabel("E / SPACE / КЛИК", content, TextAnchor.LowerRight);
        RectTransform continueRect = continueLabel.rectTransform;
        continueRect.anchorMin = new Vector2(1f, 0f);
        continueRect.anchorMax = new Vector2(1f, 0f);
        continueRect.pivot = new Vector2(1f, 0f);
        continueRect.anchoredPosition = new Vector2(-4f, 2f);
        continueRect.sizeDelta = new Vector2(240f, 24f);

        portraitFrame = UIKit.CreateTerminalPanel("Portrait Frame", content, out RectTransform portraitContent, scanlines: false).gameObject;
        RectTransform portraitFrameRect = (RectTransform)portraitFrame.transform;
        portraitFrameRect.anchorMin = new Vector2(1f, 0.5f);
        portraitFrameRect.anchorMax = new Vector2(1f, 0.5f);
        portraitFrameRect.pivot = new Vector2(1f, 0.5f);
        portraitFrameRect.anchoredPosition = new Vector2(0f, 0f);
        portraitFrameRect.sizeDelta = new Vector2(174f, 250f);

        var portraitObject = new GameObject("Portrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        portraitObject.transform.SetParent(portraitContent, false);
        portrait = portraitObject.GetComponent<Image>();
        portrait.preserveAspect = true;
        portrait.color = Color.white;
        UIKit.FullStretch(portrait.rectTransform);

        continueButton = dialoguePanel.AddComponent<Button>();
        continueButton.transition = Selectable.Transition.None;
        continueButton.onClick.AddListener(ContinueDialogue);

        dialoguePanel.SetActive(false);
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
        float gap = UITheme.Space2;
        float availableHeight = 150f;
        float buttonHeight = (availableHeight - gap * (count - 1)) / count;
        for (int i = 0; i < choices.Length; i++)
        {
            int choiceIndex = i;
            Button button = UIKit.CreateButton(
                $"{i + 1}. {choices[i].Text}",
                dialogueContent,
                () => SelectChoice(choiceIndex),
                out Text label);
            label.alignment = TextAnchor.MiddleLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;

            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(-100f, UITheme.Space2 + (count - i - 1) * (buttonHeight + gap));
            rect.sizeDelta = new Vector2(-248f, buttonHeight);

            choiceButtons.Add(button);
            choiceActions.Add(choices[i].Action);
        }
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
        choiceButtons.Clear();
        choiceActions.Clear();
    }

    private void EnsureBuilt()
    {
        if (canvas == null || notificationPanel == null || dialoguePanel == null) BuildUI();
    }
}

public sealed class QuestJournalUI : MonoBehaviour
{
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

    // Соц-панель (отношения) в стиле Stardew Valley.
    private const int SocialTaskIndex = 3;
    private const int RelationshipPips = 10;
    private GameObject socialPanel;
    private readonly List<SocialRow> socialRows = new();

    private sealed class SocialRow
    {
        public NpcId Npc;
        public Image Portrait;
        public Text Name;
        public Text Level;
        public Text Score;
        public Image[] Pips;
    }

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
        canvas = UIKit.CreateRootCanvas(gameObject, UITheme.SortJournal);

        root = UIKit.CreatePanel("Journal", canvas.transform, UITheme.Surface).gameObject;
        UIKit.FullStretch((RectTransform)root.transform);

        Text journalTitle = UIKit.CreateText("Journal Title", root.transform, UITheme.TypeDisplay, TextAnchor.UpperLeft, UITheme.TextBright);
        journalTitle.text = "ЖУРНАЛ";
        journalTitle.fontStyle = FontStyle.Bold;
        RectTransform journalTitleRect = journalTitle.rectTransform;
        journalTitleRect.anchorMin = new Vector2(0f, 1f);
        journalTitleRect.anchorMax = new Vector2(1f, 1f);
        journalTitleRect.pivot = new Vector2(0.5f, 1f);
        journalTitleRect.anchoredPosition = new Vector2(0f, -32f);
        journalTitleRect.sizeDelta = new Vector2(-80f, 56f);

        Text closeHint = UIKit.CreateStencilLabel("J / ESC — ЗАКРЫТЬ", root.transform, TextAnchor.UpperRight);
        RectTransform closeRect = closeHint.rectTransform;
        closeRect.anchorMin = new Vector2(0f, 1f);
        closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(0.5f, 1f);
        closeRect.anchoredPosition = new Vector2(0f, -44f);
        closeRect.sizeDelta = new Vector2(-80f, 28f);

        Image listPanel = UIKit.CreateTerminalPanel("Task List", root.transform, out RectTransform listContent);
        RectTransform listRect = listPanel.rectTransform;
        listRect.anchorMin = new Vector2(0.04f, 0.08f);
        listRect.anchorMax = new Vector2(0.32f, 0.84f);
        listRect.offsetMin = Vector2.zero;
        listRect.offsetMax = Vector2.zero;

        Text listHeader = UIKit.CreateStencilLabel("ЗАДАЧИ", listContent, TextAnchor.UpperLeft);
        listHeader.color = UITheme.Accent;
        UIKit.TopRect(listHeader.rectTransform, 4f, 0f, 4f, 26f);

        CreateTaskButton(listContent, 0, "1. Кто я и почему я здесь?");
        CreateTaskButton(listContent, 1, "2. Особая стратегия Ракель");
        CreateTaskButton(listContent, 2, "3. Передатчик для программиста");
        CreateTaskButton(listContent, 3, "4. Отношения");

        Image detailsPanel = UIKit.CreateTerminalPanel("Details", root.transform, out RectTransform detailsContent);
        RectTransform detailsRect = detailsPanel.rectTransform;
        detailsRect.anchorMin = new Vector2(0.35f, 0.08f);
        detailsRect.anchorMax = new Vector2(0.96f, 0.84f);
        detailsRect.offsetMin = Vector2.zero;
        detailsRect.offsetMax = Vector2.zero;

        statusLabel = UIKit.CreateText("Status", detailsContent, UITheme.TypeLabel, TextAnchor.UpperLeft, UITheme.Accent);
        UIKit.TopRect(statusLabel.rectTransform, 4f, 0f, 4f, 24f);

        titleLabel = UIKit.CreateText("Title", detailsContent, UITheme.TypeTitle, TextAnchor.UpperLeft, UITheme.TextBright);
        titleLabel.fontStyle = FontStyle.Bold;
        UIKit.TopRect(titleLabel.rectTransform, 4f, 30f, 4f, 40f);

        descriptionLabel = UIKit.CreateText("Description", detailsContent, UITheme.TypeBody, TextAnchor.UpperLeft, UITheme.TextPrimary);
        descriptionLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
        descriptionLabel.verticalOverflow = VerticalWrapMode.Truncate;
        UIKit.TopRect(descriptionLabel.rectTransform, 4f, 82f, 4f, 116f);

        stepsLabel = UIKit.CreateText("Steps", detailsContent, UITheme.TypeBody, TextAnchor.UpperLeft, UITheme.TextMuted);
        stepsLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
        stepsLabel.verticalOverflow = VerticalWrapMode.Truncate;
        RectTransform stepsRect = stepsLabel.rectTransform;
        stepsRect.anchorMin = new Vector2(0f, 0f);
        stepsRect.anchorMax = new Vector2(1f, 0f);
        stepsRect.pivot = new Vector2(0.5f, 0f);
        stepsRect.anchoredPosition = new Vector2(0f, 8f);
        stepsRect.sizeDelta = new Vector2(-8f, 210f);

        BuildSocialPanel(detailsContent);

        root.SetActive(false);
    }

    private void BuildSocialPanel(Transform detailsParent)
    {
        socialPanel = UIKit.CreatePanel("Social", detailsParent, Color.clear).gameObject;
        UIKit.FullStretch((RectTransform)socialPanel.transform);

        Text header = UIKit.CreateText("Social Header", socialPanel.transform, UITheme.TypeTitle, TextAnchor.UpperLeft, UITheme.TextBright);
        header.text = "Отношения";
        header.fontStyle = FontStyle.Bold;
        UIKit.TopRect(header.rectTransform, 4f, 0f, 4f, 40f);

        float rowHeight = 92f;
        for (int i = 0; i < RunState.SocialNpcs.Length; i++)
        {
            BuildSocialRow(RunState.SocialNpcs[i], i, rowHeight);
        }

        socialPanel.SetActive(false);
    }

    private void BuildSocialRow(NpcId npc, int index, float rowHeight)
    {
        Image rowImage = UIKit.CreatePanel($"Social Row {index}", socialPanel.transform, UITheme.RowNormal);
        UIKit.AddFrame(rowImage.rectTransform, UITheme.BorderDim, UITheme.BorderDim, UITheme.BorderThin, 0f);
        RectTransform rowRect = rowImage.rectTransform;
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.pivot = new Vector2(0.5f, 1f);
        rowRect.anchoredPosition = new Vector2(0f, -50f - index * (rowHeight + 10f));
        rowRect.sizeDelta = new Vector2(-8f, rowHeight);

        // Портрет слева.
        var portraitObject = new GameObject("Portrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        portraitObject.transform.SetParent(rowImage.transform, false);
        var portrait = portraitObject.GetComponent<Image>();
        portrait.preserveAspect = true;
        RectTransform portraitRect = portrait.rectTransform;
        portraitRect.anchorMin = new Vector2(0f, 0.5f);
        portraitRect.anchorMax = new Vector2(0f, 0.5f);
        portraitRect.pivot = new Vector2(0f, 0.5f);
        portraitRect.anchoredPosition = new Vector2(10f, 0f);
        portraitRect.sizeDelta = new Vector2(rowHeight - 16f, rowHeight - 16f);

        Sprite portraitSprite = LoadPortrait(RunState.NpcPortraitResource(npc));
        portrait.sprite = portraitSprite;
        portrait.enabled = portraitSprite != null;

        // Имя и уровень.
        Text nameText = UIKit.CreateText("Name", rowImage.transform, UITheme.TypeHeader, TextAnchor.UpperLeft, UITheme.TextPrimary);
        nameText.fontStyle = FontStyle.Bold;
        nameText.text = RunState.NpcDisplayName(npc);
        UIKit.TopRect(nameText.rectTransform, rowHeight + 2f, 8f, 120f, 26f);

        Text levelText = UIKit.CreateText("Level", rowImage.transform, UITheme.TypeLabel, TextAnchor.UpperLeft, UITheme.TextMuted);
        UIKit.TopRect(levelText.rectTransform, rowHeight + 2f, 36f, 120f, 22f);

        Text scoreText = UIKit.CreateText("Score", rowImage.transform, UITheme.TypeLabel, TextAnchor.UpperRight, UITheme.TextMuted);
        RectTransform scoreRect = scoreText.rectTransform;
        scoreRect.anchorMin = new Vector2(1f, 1f);
        scoreRect.anchorMax = new Vector2(1f, 1f);
        scoreRect.pivot = new Vector2(1f, 1f);
        scoreRect.anchoredPosition = new Vector2(-12f, -8f);
        scoreRect.sizeDelta = new Vector2(120f, 22f);

        // Полоса из дискретных «пунктов» (как сердечки в Stardew).
        var pips = new Image[RelationshipPips];
        float pipSize = 16f;
        float pipGap = 4f;
        for (int p = 0; p < RelationshipPips; p++)
        {
            Image pipImage = UIKit.CreatePanel($"Pip {p}", rowImage.transform, Color.white);
            RectTransform pipRect = pipImage.rectTransform;
            pipRect.anchorMin = new Vector2(0f, 0.5f);
            pipRect.anchorMax = new Vector2(0f, 0.5f);
            pipRect.pivot = new Vector2(0f, 0.5f);
            pipRect.anchoredPosition = new Vector2(rowHeight + 2f + p * (pipSize + pipGap), -18f);
            pipRect.sizeDelta = new Vector2(pipSize, pipSize);
            pips[p] = pipImage;
        }

        socialRows.Add(new SocialRow
        {
            Npc = npc,
            Portrait = portrait,
            Name = nameText,
            Level = levelText,
            Score = scoreText,
            Pips = pips,
        });
    }

    private static Sprite LoadPortrait(string resource) =>
        string.IsNullOrWhiteSpace(resource) ? null : Resources.Load<Sprite>($"Sprites/{resource}");

    private void RefreshSocial()
    {
        foreach (SocialRow row in socialRows)
        {
            int score = RunState.RelationshipTo(row.Npc);
            RelationshipLevel level = RelationshipLevels.For(score);
            Color levelColor = LevelColor(level);

            row.Level.text = RelationshipLevels.Label(level);
            row.Level.color = levelColor;
            row.Score.text = $"{score}/100";

            int filled = Mathf.RoundToInt(score / 100f * RelationshipPips);
            for (int p = 0; p < row.Pips.Length; p++)
            {
                row.Pips[p].color = p < filled ? levelColor : UITheme.PanelRaised;
            }
        }
    }

    private static Color LevelColor(RelationshipLevel level) => level switch
    {
        RelationshipLevel.Enemy => new Color(0.86f, 0.27f, 0.24f),
        RelationshipLevel.Dislike => new Color(0.86f, 0.55f, 0.27f),
        RelationshipLevel.Neutral => new Color(0.72f, 0.74f, 0.72f),
        RelationshipLevel.Acquaintance => new Color(0.55f, 0.8f, 0.5f),
        RelationshipLevel.Friend => new Color(0.38f, 0.85f, 0.6f),
        _ => Color.white,
    };

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
        else if (Keyboard.current.digit4Key.wasPressedThisFrame) SelectTask(3);
        else if (Keyboard.current.upArrowKey.wasPressedThisFrame) SelectTask(selectedTask - 1);
        else if (Keyboard.current.downArrowKey.wasPressedThisFrame) SelectTask(selectedTask + 1);
    }

    private void Open()
    {
        selectedTask = TaskIndexForActiveQuest();
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

        bool social = selectedTask == SocialTaskIndex;
        socialPanel.SetActive(social);
        statusLabel.gameObject.SetActive(!social);
        titleLabel.gameObject.SetActive(!social);
        descriptionLabel.gameObject.SetActive(!social);
        stepsLabel.gameObject.SetActive(!social);
        if (social)
        {
            RefreshSocial();
            return;
        }

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
                CompetitorQuestStage.EscapeFolderGivenToRaquel => "ПЛАНИРОВАНИЕ ПОБЕГА",
                CompetitorQuestStage.EscapeArchiveFound => "ПАПКА НАЙДЕНА",
                CompetitorQuestStage.ArchiveKeyAcquired => "ДОСТУП К АРХИВУ",
                CompetitorQuestStage.GuardPostLead => "ПОСТ ОХРАНЫ",
                CompetitorQuestStage.GardenAccess => "ОТКРЫТ МАРШРУТ",
                CompetitorQuestStage.GardenMeetingComplete => "САД ОТКРЫТ",
                CompetitorQuestStage.GardenMeetingScheduled => "ВСТРЕЧА В 19:00",
                CompetitorQuestStage.SmokeScheduleKnown => "РАСПИСАНИЕ ПОЛУЧЕНО",
                CompetitorQuestStage.Overheard => "УЛИКА ПОЛУЧЕНА",
                _ => "АКТИВНО",
            };
            titleLabel.text = "Особая стратегия Ракель";
            descriptionLabel.text =
                "Программист слышал, что Ракель действует по собственному расписанию и иногда исчезает из общей зоны. " +
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
            ProgrammerQuestStage.DataSourceAcquired => "ВЕРНУТЬ МОДУЛЬ",
            ProgrammerQuestStage.ComputeAccessAcquired => "ВЕРНУТЬ МОДУЛЬ",
            ProgrammerQuestStage.SignalAmplifierAcquired => "ВЕРНУТЬ МОДУЛЬ",
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
        Button button = UIKit.CreateListRow(title, parent, () => SelectTask(index), out _, out Text text);
        text.verticalOverflow = VerticalWrapMode.Truncate;
        taskButtons.Add(button);

        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -34f - index * 72f);
        rect.sizeDelta = new Vector2(0f, 64f);
    }

    private void SelectTask(int index)
    {
        selectedTask = Mathf.Clamp(index, 0, taskButtons.Count - 1);
        if (selectedTask == 0) RunState.SetActiveQuest(ActiveQuestId.Identity);
        else if (selectedTask == 1) RunState.SetActiveQuest(ActiveQuestId.Raquel);
        else if (selectedTask == 2) RunState.SetActiveQuest(ActiveQuestId.Programmer);
        Refresh();
    }

    private void RefreshTaskButtons()
    {
        for (int i = 0; i < taskButtons.Count; i++)
        {
            Image image = taskButtons[i].GetComponent<Image>();
            bool active = i == TaskIndexForActiveQuest();
            image.color = i == selectedTask
                ? UITheme.Selected
                : active ? UITheme.RowActive : UITheme.RowNormal;
        }
    }

    private static int TaskIndexForActiveQuest() => RunState.ActiveQuest switch
    {
        ActiveQuestId.Raquel => 1,
        ActiveQuestId.Programmer => 2,
        _ => 0,
    };

    private static string BuildSteps(ProgrammerQuestStage stage)
    {
        bool accepted = stage == ProgrammerQuestStage.Accepted ||
                        stage == ProgrammerQuestStage.TransmitterAcquired ||
                        stage == ProgrammerQuestStage.AnalyzingTransmitter ||
                        stage == ProgrammerQuestStage.DayTwoQuestAvailable ||
                        stage == ProgrammerQuestStage.DataSourceNeeded ||
                        stage == ProgrammerQuestStage.DataSourceAcquired ||
                        stage == ProgrammerQuestStage.ComputeAccessNeeded ||
                        stage == ProgrammerQuestStage.ComputeAccessAcquired ||
                        stage == ProgrammerQuestStage.SignalAmplifierNeeded ||
                        stage == ProgrammerQuestStage.SignalAmplifierAcquired ||
                        stage == ProgrammerQuestStage.Completed;
        bool acquired = stage == ProgrammerQuestStage.TransmitterAcquired ||
                        stage == ProgrammerQuestStage.AnalyzingTransmitter ||
                        stage == ProgrammerQuestStage.DayTwoQuestAvailable ||
                        stage == ProgrammerQuestStage.DataSourceNeeded ||
                        stage == ProgrammerQuestStage.DataSourceAcquired ||
                        stage == ProgrammerQuestStage.ComputeAccessNeeded ||
                        stage == ProgrammerQuestStage.ComputeAccessAcquired ||
                        stage == ProgrammerQuestStage.SignalAmplifierNeeded ||
                        stage == ProgrammerQuestStage.SignalAmplifierAcquired ||
                        stage == ProgrammerQuestStage.Completed;
        bool analyzing = stage == ProgrammerQuestStage.AnalyzingTransmitter ||
                         stage == ProgrammerQuestStage.DayTwoQuestAvailable ||
                         stage == ProgrammerQuestStage.DataSourceNeeded ||
                         stage == ProgrammerQuestStage.DataSourceAcquired ||
                         stage == ProgrammerQuestStage.ComputeAccessNeeded ||
                         stage == ProgrammerQuestStage.ComputeAccessAcquired ||
                         stage == ProgrammerQuestStage.SignalAmplifierNeeded ||
                         stage == ProgrammerQuestStage.SignalAmplifierAcquired ||
                         stage == ProgrammerQuestStage.Completed;
        bool dayTwo = stage == ProgrammerQuestStage.DayTwoQuestAvailable ||
                      stage == ProgrammerQuestStage.DataSourceNeeded ||
                      stage == ProgrammerQuestStage.DataSourceAcquired ||
                      stage == ProgrammerQuestStage.ComputeAccessNeeded ||
                      stage == ProgrammerQuestStage.ComputeAccessAcquired ||
                      stage == ProgrammerQuestStage.SignalAmplifierNeeded ||
                      stage == ProgrammerQuestStage.SignalAmplifierAcquired ||
                      stage == ProgrammerQuestStage.Completed;
        bool dataSource = stage == ProgrammerQuestStage.DataSourceAcquired ||
                          stage == ProgrammerQuestStage.ComputeAccessNeeded ||
                          stage == ProgrammerQuestStage.ComputeAccessAcquired ||
                          stage == ProgrammerQuestStage.SignalAmplifierNeeded ||
                          stage == ProgrammerQuestStage.SignalAmplifierAcquired ||
                          stage == ProgrammerQuestStage.Completed;
        bool compute = stage == ProgrammerQuestStage.ComputeAccessAcquired ||
                       stage == ProgrammerQuestStage.SignalAmplifierNeeded ||
                       stage == ProgrammerQuestStage.SignalAmplifierAcquired ||
                       stage == ProgrammerQuestStage.Completed;
        bool amplifier = stage == ProgrammerQuestStage.SignalAmplifierAcquired ||
                         stage == ProgrammerQuestStage.Completed;
        bool completed = stage == ProgrammerQuestStage.Completed;

        return $"{Mark(accepted)} Договориться с программистом.\n\n" +
               $"{Mark(acquired)} Проникнуть в инженерную зону и найти передатчик.\n\n" +
               $"{Mark(analyzing)} Вернуть передатчик программисту.\n\n" +
               $"{Mark(dayTwo)} Дождаться, пока программист разберёт часть данных.\n\n" +
               $"{Mark(dataSource)} Добыть источник данных в блоке C.\n\n" +
               $"{Mark(compute)} Добыть модуль доступа в архиве данных.\n\n" +
               $"{Mark(amplifier)} Добыть усилитель сигнала в релейной комнате.\n\n" +
               $"{Mark(completed)} Открыть прогноз награды-импланта за следующий эксперимент.";
    }

    private static string BuildCompetitorSteps(CompetitorQuestStage stage)
    {
        bool started = stage != CompetitorQuestStage.Unknown;
        bool reached = stage == CompetitorQuestStage.ReachedStaffRoom ||
                       stage == CompetitorQuestStage.Overheard ||
                       stage == CompetitorQuestStage.GardenMeetingScheduled ||
                       stage == CompetitorQuestStage.GardenMeetingComplete ||
                       stage == CompetitorQuestStage.SmokeScheduleKnown ||
                       stage == CompetitorQuestStage.GardenAccess ||
                       stage == CompetitorQuestStage.GuardPostLead ||
                       stage == CompetitorQuestStage.ArchiveKeyAcquired ||
                       stage == CompetitorQuestStage.EscapeArchiveFound ||
                       stage == CompetitorQuestStage.EscapeFolderGivenToRaquel;
        bool overheard = stage == CompetitorQuestStage.Overheard ||
                         stage == CompetitorQuestStage.GardenMeetingScheduled ||
                         stage == CompetitorQuestStage.GardenMeetingComplete ||
                         stage == CompetitorQuestStage.SmokeScheduleKnown ||
                         stage == CompetitorQuestStage.GardenAccess ||
                         stage == CompetitorQuestStage.GuardPostLead ||
                         stage == CompetitorQuestStage.ArchiveKeyAcquired ||
                         stage == CompetitorQuestStage.EscapeArchiveFound ||
                         stage == CompetitorQuestStage.EscapeFolderGivenToRaquel;
        bool meeting = stage == CompetitorQuestStage.GardenMeetingScheduled ||
                       stage == CompetitorQuestStage.GardenMeetingComplete ||
                       stage == CompetitorQuestStage.SmokeScheduleKnown ||
                       stage == CompetitorQuestStage.GardenAccess ||
                       stage == CompetitorQuestStage.GuardPostLead ||
                       stage == CompetitorQuestStage.ArchiveKeyAcquired ||
                       stage == CompetitorQuestStage.EscapeArchiveFound ||
                       stage == CompetitorQuestStage.EscapeFolderGivenToRaquel;
        bool schedule = stage == CompetitorQuestStage.SmokeScheduleKnown ||
                        stage == CompetitorQuestStage.GardenMeetingComplete ||
                        stage == CompetitorQuestStage.GardenAccess ||
                        stage == CompetitorQuestStage.GuardPostLead ||
                        stage == CompetitorQuestStage.ArchiveKeyAcquired ||
                        stage == CompetitorQuestStage.EscapeArchiveFound ||
                        stage == CompetitorQuestStage.EscapeFolderGivenToRaquel;
        bool garden = stage == CompetitorQuestStage.GardenAccess ||
                      stage == CompetitorQuestStage.GuardPostLead ||
                      stage == CompetitorQuestStage.ArchiveKeyAcquired ||
                      stage == CompetitorQuestStage.EscapeArchiveFound ||
                      stage == CompetitorQuestStage.EscapeFolderGivenToRaquel;
        bool guardPost = stage == CompetitorQuestStage.GuardPostLead ||
                         stage == CompetitorQuestStage.ArchiveKeyAcquired ||
                         stage == CompetitorQuestStage.EscapeArchiveFound ||
                         stage == CompetitorQuestStage.EscapeFolderGivenToRaquel;
        bool archive = stage == CompetitorQuestStage.ArchiveKeyAcquired ||
                       stage == CompetitorQuestStage.EscapeArchiveFound ||
                       stage == CompetitorQuestStage.EscapeFolderGivenToRaquel;
        bool folder = stage == CompetitorQuestStage.EscapeArchiveFound ||
                      stage == CompetitorQuestStage.EscapeFolderGivenToRaquel;
        bool handedOff = stage == CompetitorQuestStage.EscapeFolderGivenToRaquel;

        return $"{Mark(started)} Узнать слух о Ракель у программиста.\n\n" +
               $"{Mark(reached)} Проследить за её утренним маршрутом.\n\n" +
               $"{Mark(overheard)} Подслушать разговор в комнате персонала.\n\n" +
               $"{Mark(meeting)} Помочь Ракель на эксперименте и встретиться у сада в 19:00.\n\n" +
               $"{Mark(schedule)} Получить расписание персонала и маскировочный имплант.\n\n" +
               $"{Mark(garden)} Подслушать персонал в саду.\n\n" +
               $"{Mark(guardPost)} Найти наводку на прошлый побег и попасть на пост охраны.\n\n" +
               $"{Mark(archive)} Добыть доступ к архиву.\n\n" +
               $"{Mark(folder)} Найти папку о сбежавшем заключённом.\n\n" +
               $"{Mark(handedOff)} Передать папку Ракель для планирования побега.";
    }

    private static string Mark(bool done) => done ? "<color=#75D99A>✓</color>" : "○";

    private void OnDestroy()
    {
        if (IsOpen) Time.timeScale = previousTimeScale;
    }
}
