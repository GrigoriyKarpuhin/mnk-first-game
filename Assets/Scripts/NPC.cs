using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NPC - простое автономное движение по гриду
/// </summary>
public class NPC : MonoBehaviour
{
    [Header("Sprite (оставь пустым если используешь Animator)")]
    [SerializeField] private Sprite npcSprite;

    [Header("Visual Settings")]
    [SerializeField] private Color npcColor = new Color(1f, 0.6f, 0.2f);
    [SerializeField] private float moveSpeed = 8f;

    [Header("AI Settings")]
    [SerializeField] private bool enableMovement = false;
    [SerializeField] private float moveInterval = 0.6f;

    [Header("Interaction Hint")]
    [SerializeField] private float hintHeight = 0.9f;
    [SerializeField] private float hintRange = 1.6f;
    [SerializeField] private string hintText = "E";

    // Текущая позиция на гриде
    private int gridX;
    private int gridY;

    // Целевая позиция для плавного движения
    private Vector3 targetPosition;
    private bool isMoving;

    // Направление движения (для анимаций)
    private Vector2 facingDirection = Vector2.right;

    // Ссылка на грид
    private GameGrid grid;

    // Компоненты
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private WorldMarker hintMarker;
    private Player player;

    private float moveTimer;
    private string spriteResourceName = "inmate_c1752";

    private static readonly Vector2Int[] Directions =
    {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1),
    };

    /// <summary>
    /// Инициализация NPC
    /// </summary>
    public void Initialize(GameGrid gameGrid, int startX, int startY)
    {
        grid = gameGrid;
        gridX = startX;
        gridY = startY;

        CreateVisual();
        CreateHint();

        targetPosition = grid.GridToWorld(gridX, gridY);
        transform.position = targetPosition;

        UpdateSortingOrder();
        moveTimer = moveInterval;
    }

    public void SetSpriteResource(string resourceName)
    {
        if (!string.IsNullOrEmpty(resourceName)) spriteResourceName = resourceName;
    }

    private void CreateVisual()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();

        if (animator != null)
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }
            transform.localScale = Vector3.one * WorldMetrics.CharacterScale;
            return;
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }

        // Пиксель-арт по умолчанию из Resources/Sprites, если не задан в инспекторе.
        if (npcSprite == null)
        {
            npcSprite = Resources.Load<Sprite>($"Sprites/{spriteResourceName}");
        }

        if (npcSprite != null)
        {
            spriteRenderer.sprite = SpriteWalkAnimator.FeetAnchored(npcSprite);
            spriteRenderer.color = Color.white;
            float spriteSize = Mathf.Max(npcSprite.bounds.size.x, npcSprite.bounds.size.y);
            transform.localScale = Vector3.one * grid.CellSize * WorldMetrics.CharacterScale / spriteSize;
            // У C-1752 пока нет walk-кадров → аниматор не подключится, останется
            // статичная стойка (NPC всё равно стоит на месте у входа в эксперимент).
            SpriteWalkAnimator.TryAttach(gameObject, spriteResourceName);
        }
        else
        {
            spriteRenderer.sprite = CreateCircleSprite();
            spriteRenderer.color = npcColor;
            transform.localScale = Vector3.one * grid.CellSize * WorldMetrics.CharacterScale;
        }

        CharacterGroundShadow.Attach(gameObject);
    }

    private void CreateHint()
    {
        // Подсказка над NPC — через общий WorldMarker (screen-space uGUI), а не
        // 3D TextMesh: TextMesh с динамическим шрифтом рисует кашу, а маркер
        // корректно показывает терминальный шрифт и виден в headless-скриншоте.
        hintMarker = UIKit.CreateWorldMarker("NpcHint", transform,
            Vector3.up * hintHeight, Camera.main, wantLabel: true);
        hintMarker.SetVisible(false);
    }

    private Sprite CreateCircleSprite()
    {
        int size = 64;
        var texture = new Texture2D(size, size);
        var pixels = new Color[size * size];

        float center = size / 2f;
        float radius = size / 2f - 2;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                pixels[y * size + x] = dist <= radius ? Color.white : Color.clear;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        texture.filterMode = FilterMode.Bilinear;

        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private void Update()
    {
        UpdateMovement();
        UpdateAnimation();
        if (enableMovement)
        {
            HandleAI();
        }
        UpdateHint();
    }

    private void UpdateHint()
    {
        if (hintMarker == null) return;

        if (player == null)
        {
            player = FindFirstObjectByType<Player>();
        }
        if (player == null) return;

        bool blocked = DialogueUI.IsModalOpen;
        float distance = Vector2.Distance(transform.position, player.transform.position);
        bool shouldShow = !blocked && distance <= hintRange;
        hintMarker.SetLabel(shouldShow ? UIKit.ColorTag(UITheme.Accent, hintText) : null);
        hintMarker.SetVisible(shouldShow);
    }

    private void HandleAI()
    {
        if (grid == null) return;
        if (isMoving) return;

        moveTimer -= Time.deltaTime;
        if (moveTimer > 0f) return;
        moveTimer = moveInterval;

        int startIndex = Random.Range(0, Directions.Length);
        for (int i = 0; i < Directions.Length; i++)
        {
            Vector2Int dir = Directions[(startIndex + i) % Directions.Length];
            if (TryMove(dir.x, dir.y))
            {
                break;
            }
        }
    }

    protected bool IsMoving => isMoving;
    protected GameGrid Grid => grid;

    protected bool TryMoveToCell(Vector2Int cell, bool allowDoorCells = false)
    {
        int dx = cell.x - gridX;
        int dy = cell.y - gridY;
        if (Mathf.Abs(dx) + Mathf.Abs(dy) != 1) return false;

        if (dx != 0) facingDirection = new Vector2(dx, 0);
        else if (dy != 0) facingDirection = new Vector2(0, dy);

        if (IsNpcWalkable(cell.x, cell.y, allowDoorCells))
        {
            if (allowDoorCells && grid.GetTileType(cell.x, cell.y) == TileType.Door)
            {
                grid.OpenDoorForNpc(cell);
            }
            gridX = cell.x;
            gridY = cell.y;
            targetPosition = grid.GridToWorld(gridX, gridY);
            isMoving = true;
            return true;
        }

        return false;
    }

    protected bool IsNpcWalkable(int x, int y, bool allowDoorCells = false)
    {
        if (grid.IsWalkable(x, y)) return true;
        return allowDoorCells && grid.CanNpcTraverseDoor(new Vector2Int(x, y));
    }

    private bool TryMove(int dx, int dy)
    {
        return TryMoveToCell(new Vector2Int(gridX + dx, gridY + dy));
    }

    private void UpdateMovement()
    {
        if (!isMoving) return;

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            moveSpeed * Time.deltaTime
        );

        UpdateSortingOrder();

        if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
        {
            transform.position = targetPosition;
            isMoving = false;
        }
    }

    private void UpdateSortingOrder()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = SortingLayers.Entity(transform.position.y);
        }
        // Подсказка теперь screen-space WorldMarker — сортировка ей не нужна.
    }

    private void UpdateAnimation()
    {
        if (animator == null) return;

        animator.SetInteger("AnimState", isMoving ? 2 : 0);

        if (facingDirection.x > 0)
        {
            transform.localScale = new Vector3(-Mathf.Abs(WorldMetrics.CharacterScale), WorldMetrics.CharacterScale, 1);
        }
        else if (facingDirection.x < 0)
        {
            transform.localScale = new Vector3(Mathf.Abs(WorldMetrics.CharacterScale), WorldMetrics.CharacterScale, 1);
        }
    }

    public Vector2Int GridPosition => new Vector2Int(gridX, gridY);

    public virtual void Interact()
    {
        if (!RunState.CanStartExperimentNow)
        {
            if (RunState.DayPhase == DayPhase.MorningFreeTime)
            {
                DialogueUI.Instance.Show("Вход в эксперименты пока закрыт. Ждите объявления в 12:00.", 2.2f);
            }
            else if (RunState.DayPhase == DayPhase.AfternoonFreeTime || RunState.DayPhase == DayPhase.LightsOut)
            {
                DialogueUI.Instance.Show("Сегодняшний эксперимент уже завершён.", 2f);
            }
            else if (RunState.DayPhase == DayPhase.EscortedToExperiment)
            {
                DialogueUI.Instance.Show("Надзиратели уже идут за вами.", 1.8f);
            }
            else
            {
                DialogueUI.Instance.Show("Сейчас нельзя войти в эксперимент.", 1.8f);
            }

            return;
        }

        // Демо квест-хука: пока квест программиста активен, просим спасти его
        // в ближайшем испытании. Реагирует на это только «Бег» (читает
        // ExperimentContext.RescueTarget); прочие испытания цель игнорируют.
        if (RunState.ProgrammerQuest == ProgrammerQuestStage.Accepted)
        {
            RunState.RequestRescue(NpcId.Programmer);
        }

        // Выбираем эксперимент из пула (если он собран в Resources), иначе —
        // дефолт на полосу препятствий. Сам выбор — в ExperimentSelector.
        RunState.EnterSelectedExperiment();
    }
}

public sealed class ProgrammerNPC : NPC
{
    public override void Interact()
    {
        switch (RunState.ProgrammerQuest)
        {
            case ProgrammerQuestStage.NotStarted:
                ShowIntroduction();
                break;
            case ProgrammerQuestStage.Ignored:
                ShowSecondChance();
                break;
            case ProgrammerQuestStage.Accepted:
                if (RunState.RescueTargetSaved)
                {
                    RunState.ClearRescue();
                    DialogueUI.Instance.ShowDialogue(
                        "Программист",
                        "Ты вытащил меня из той ямы... я думал, мне конец. Я этого не забуду.",
                        "npc_programmer");
                }
                else
                {
                    DialogueUI.Instance.ShowDialogue(
                        "Программист",
                        "Передатчик должен быть в инженерной зоне. Отвёртка снимет заметную ревизионную панель в хозяйственной части санитарного крыла.",
                        "npc_programmer");
                }
                break;
            case ProgrammerQuestStage.TransmitterAcquired:
                ShowCompletionStart();
                break;
            case ProgrammerQuestStage.Completed:
                DialogueUI.Instance.ShowDialogue(
                    "Программист",
                    "Устройство работает. Перед сбором на эксперимент я смогу сказать, какой имплант предлагают победителю. Полный взлом не вышел, но это уже преимущество.",
                    "npc_programmer");
                break;
            case ProgrammerQuestStage.AnalyzingTransmitter:
                DialogueUI.Instance.ShowDialogue(
                    "Программист",
                    "Я разбираюсь с передатчиком. Защита грубая, но в потоке данных что-то есть. Подойди завтра утром.",
                    "npc_programmer");
                break;
            case ProgrammerQuestStage.DayTwoQuestAvailable:
                ShowDayTwoHook();
                break;
            case ProgrammerQuestStage.DataSourceNeeded:
                DialogueUI.Instance.ShowDialogue(
                    "Программист",
                    "Источник данных должен быть в технологическом крыле блока C. Я смог приоткрыть служебную дверь, но дальше будут патрули. Включай глазной имплант рядом с панелями.",
                    "npc_programmer");
                break;
            case ProgrammerQuestStage.DataSourceAcquired:
                TurnInDataSource();
                break;
            case ProgrammerQuestStage.ComputeAccessNeeded:
                DialogueUI.Instance.ShowDialogue(
                    "Программист",
                    "Теперь нужен модуль доступа из архива данных за блоком C. Там цепь сложнее: ищи скрытые провода рядом с собой.",
                    "npc_programmer");
                break;
            case ProgrammerQuestStage.ComputeAccessAcquired:
                TurnInComputeAccess();
                break;
            case ProgrammerQuestStage.SignalAmplifierNeeded:
                DialogueUI.Instance.ShowDialogue(
                    "Программист",
                    "Остался усилитель сигнала в релейной комнате. Без него я вижу данные слишком поздно, уже перед самым стартом.",
                    "npc_programmer");
                break;
            case ProgrammerQuestStage.SignalAmplifierAcquired:
                CompleteFullRoute();
                break;
            case ProgrammerQuestStage.Rejected:
                DialogueUI.Instance.ShowDialogue(
                    "Программист",
                    "Понял. Не буду тебе мешать.",
                    "npc_programmer");
                break;
        }
    }

    private static void ShowIntroduction()
    {
        DialogueUI.Instance.ShowChoices(
            "Программист",
            "Ты новенький? Здесь людей заставляют участвовать в экспериментах. Я могу объяснить правила... и нам обоим пригодился бы друг.",
            "npc_programmer",
            new DialogueUI.DialogueChoice("Расспросить его о тюрьме", ShowDetails),
            new DialogueUI.DialogueChoice("Спросить о заключённых", ShowInmateRumors),
            new DialogueUI.DialogueChoice("Согласиться помогать друг другу", AcceptQuest),
            new DialogueUI.DialogueChoice("Не разговаривать", Ignore));
    }

    private static void ShowSecondChance()
    {
        DialogueUI.Instance.ShowChoices(
            "Программист",
            "Извини, что снова лезу. Но одному здесь долго не протянуть.",
            "npc_programmer",
            new DialogueUI.DialogueChoice("Теперь выслушать его", ShowDetails),
            new DialogueUI.DialogueChoice("Согласиться помочь", AcceptQuest),
            new DialogueUI.DialogueChoice("Снова уйти", () =>
                DialogueUI.Instance.ShowDialogue(
                    "Программист",
                    "Ладно... Я не буду тебя задерживать.",
                    "npc_programmer")));
    }

    private static void ShowDetails()
    {
        RunState.AddEvidence(EvidenceId.AdaptiveExperimentSystem);
        RunState.AddEvidence(EvidenceId.HiddenSystemsNeedEyeImplant);
        DialogueUI.Instance.ShowChoices(
            "Программист",
            "Система подбирает испытания под заключённых. Если достать передатчик из инженерной зоны, я попробую получать данные заранее. Камеры и скрытые механизмы можно увидеть только с глазным имплантом.",
            "npc_programmer",
            new DialogueUI.DialogueChoice("Договорились. Я помогу", AcceptQuest),
            new DialogueUI.DialogueChoice("Что знаешь о заключённых?", ShowInmateRumors),
            new DialogueUI.DialogueChoice("Это слишком опасно", Reject),
            new DialogueUI.DialogueChoice("Мне нужно подумать", Ignore));
    }

    private static void ShowInmateRumors()
    {
        RunState.StartCompetitorTracking();
        DialogueUI.Instance.ShowChoices(
            "Программист",
            "Есть одна женщина. Держится так, будто правила написаны не для неё. Говорят, у неё особая стратегия выживания: она знает, когда и с кем исчезать из общей зоны. Я бы не стал ей доверять, но проследить за ней полезно.",
            "npc_programmer",
            new DialogueUI.DialogueChoice("Я прослежу за ней", () =>
                DialogueUI.Instance.ShowDialogue(
                    "Программист",
                    "Только не подходи слишком близко. Если она правда ходит в служебную часть, ты узнаешь больше, чем из моих догадок.\n\n<color=#75D99A>Новая задача: проследить за Ракель.</color>",
                    "npc_programmer")),
            new DialogueUI.DialogueChoice("Расскажи лучше о тюрьме", ShowDetails),
            new DialogueUI.DialogueChoice("Теперь о твоей просьбе", AcceptQuest),
            new DialogueUI.DialogueChoice("Хватит разговоров", () =>
                DialogueUI.Instance.ShowDialogue(
                    "Программист",
                    "Как скажешь. Но её маршрут лучше не пропустить утром.",
                    "npc_programmer")));
    }

    private static void AcceptQuest()
    {
        RunState.AcceptProgrammerQuest();
        RunState.AddEvidence(EvidenceId.EngineeringTransmitter);
        DialogueUI.Instance.ShowDialogue(
            "Программист",
            "Возьми эту отвёртку. В хозяйственной части санитарного крыла есть ревизионная панель на четырёх винтах. За ней должен быть старый технический проход.\n\n<color=#75D99A>Отношения улучшились. Получена отвёртка.</color>",
            "npc_programmer");
    }

    private static void Ignore()
    {
        RunState.IgnoreProgrammer();
        DialogueUI.Instance.ShowDialogue(
            "Программист",
            "Ладно... Извини, что помешал.\n\n<color=#E0A070>Отношения немного ухудшились.</color>",
            "npc_programmer");
    }

    private static void Reject()
    {
        RunState.RejectProgrammerQuest();
        DialogueUI.Instance.ShowDialogue(
            "Программист",
            "Понимаю. Тогда забудь, что я это говорил.\n\n<color=#D66D63>Отношения ухудшились.</color>",
            "npc_programmer");
    }

    private static void ShowCompletionStart()
    {
        DialogueUI.Instance.ShowChoices(
            "Программист",
            "Ты вернулся... И передатчик у тебя. Честно говоря, я не был уверен, что снова тебя увижу.",
            "npc_programmer",
            new DialogueUI.DialogueChoice("Передать ему передатчик", CompleteQuest),
            new DialogueUI.DialogueChoice("Ты знал, что дверь заблокируется?", AskAboutLockedDoor),
            new DialogueUI.DialogueChoice("Сначала объясни, что теперь будет", AskAboutReward));
    }

    private static void AskAboutLockedDoor()
    {
        DialogueUI.Instance.ShowChoices(
            "Программист",
            "Я... знал, что инженерный отсек может закрыться. Но думал, что там должен быть аварийный выход. Если бы я сказал всё, ты мог отказаться. Прости.",
            "npc_programmer",
            new DialogueUI.DialogueChoice("Больше ничего от меня не скрывай", CompleteQuest),
            new DialogueUI.DialogueChoice("Ты мной воспользовался", CompleteQuestAngrily),
            new DialogueUI.DialogueChoice("Что ты сделаешь с передатчиком?", AskAboutReward));
    }

    private static void AskAboutReward()
    {
        DialogueUI.Instance.ShowChoices(
            "Программист",
            "Попробую подключиться к системе подбора экспериментов. Точных правил она не выдаст, но мы сможем заранее узнавать тип испытания, участников или главный риск.",
            "npc_programmer",
            new DialogueUI.DialogueChoice("Передать передатчик", CompleteQuest),
            new DialogueUI.DialogueChoice("Вернуться к вопросу о запертой двери", AskAboutLockedDoor));
    }

    private static void CompleteQuest()
    {
        RunState.CompleteProgrammerQuest();
        DialogueUI.Instance.ShowDialogue(
            "Программист",
            "Спасибо. Мне понадобится время, чтобы разобраться с защитой системы. Когда закончу, ты узнаешь первым.\n\n<color=#75D99A>Квест завершён. Отношения улучшились.</color>",
            "npc_programmer");
    }

    private static void CompleteQuestAngrily()
    {
        RunState.CompleteProgrammerQuest();
        RunState.AdjustRelationship(NpcId.Programmer, -RunState.RelationshipNudgeSmall);
        DialogueUI.Instance.ShowDialogue(
            "Программист",
            "Да. Ты прав. Я использовал тебя. Но передатчик всё равно поможет нам обоим. Я постараюсь это исправить.\n\n<color=#D6B06D>Квест завершён. Программист запомнил вашу реакцию.</color>",
            "npc_programmer");
    }

    private static void ShowDayTwoHook()
    {
        RunState.AddEvidence(EvidenceId.AdaptiveExperimentSystem);
        DialogueUI.Instance.ShowChoices(
            "Программист",
            "Я не взломал систему. Но передатчик видит кусок очереди наград: перед следующим экспериментом можно будет понять, какой имплант предлагают победителю. Чтобы получать данные раньше, нужен источник из блока C.\n\n<color=#75D99A>Новая цель второго дня: найти источник данных системы.</color>",
            "npc_programmer",
            new DialogueUI.DialogueChoice("Где искать источник?", BeginDataSourceQuest),
            new DialogueUI.DialogueChoice("Позже", () =>
                DialogueUI.Instance.ShowDialogue(
                    "Программист",
                    "Хорошо. Но если мы не успеем до следующего эксперимента, преимущество пропадёт.",
                    "npc_programmer")));
    }

    private static void BeginDataSourceQuest()
    {
        RunState.BeginProgrammerDataSourceQuest();
        DialogueUI.Instance.ShowDialogue(
            "Программист",
            "Ищи технологическое крыло за защищённым коридором. Я нашёл старый доступ к двери: если ты уже в этой стадии, вход должен поддаться. Внутри будет панель данных и охрана.\n\n<color=#75D99A>Новая цель: добыть источник данных.</color>",
            "npc_programmer");
    }

    private static void TurnInDataSource()
    {
        RunState.TurnInProgrammerDataSource();
        DialogueUI.Instance.ShowDialogue(
            "Программист",
            "Это оно. Теперь поток понятен, но читать его нечем. Нужен модуль доступа из архива данных за блоком C.\n\n<color=#75D99A>Новая цель: добыть модуль доступа.</color>",
            "npc_programmer");
    }

    private static void TurnInComputeAccess()
    {
        RunState.TurnInProgrammerComputeAccess();
        DialogueUI.Instance.ShowDialogue(
            "Программист",
            "Модуль подходит. Теперь устройство понимает данные, но сигнал слишком слабый. В релейной комнате должен быть усилитель.\n\n<color=#75D99A>Новая цель: добыть усилитель сигнала.</color>",
            "npc_programmer");
    }

    private static void CompleteFullRoute()
    {
        RunState.CompleteProgrammerRoute();
        DialogueUI.Instance.ShowDialogue(
            "Программист",
            "Готово. Полностью систему я не вскрыл, но теперь перед сбором на эксперимент смогу увидеть награду-имплант. Это не свобода, но это выбор.\n\n<color=#75D99A>Route программиста завершён. Открыта награда: прогноз импланта следующего эксперимента.</color>",
            "npc_programmer");
    }
}

public sealed class AmbientInmateNPC : NPC
{
    private string speaker = "Заключённый";
    private string line = "Здесь лучше не задерживаться под камерами.";

    public void Configure(string displayName, string dialogueLine, string spriteName = "inmate_c1752")
    {
        speaker = displayName;
        line = dialogueLine;
        SetSpriteResource(spriteName);
    }

    public override void Interact()
    {
        DialogueUI.Instance.ShowDialogue(speaker, line, null);
    }
}

public sealed class CompetitorNPC : NPC
{
    private static readonly Vector2Int CellMirror = BlockCPlayableLayout.CompetitorCell;
    private static readonly Vector2Int CommonWalk = BlockCPlayableLayout.CompetitorCommon;
    private static readonly Vector2Int Toilet = BlockCPlayableLayout.CompetitorSanitaryStop;
    private static readonly Vector2Int StaffRoom = BlockCPlayableLayout.CompetitorStaffRoom;
    private static readonly Vector2Int ExperimentAssembly = BlockCPlayableLayout.ExperimentAssembly;
    private const float RouteStepDelay = 0.12f;

    private float nextRouteStepAt;
    private bool reachedStaffRoomThisRun;

    private void LateUpdate()
    {
        UpdateRoute();
    }

    public override void Interact()
    {
        if (RunState.CompetitorQuest == CompetitorQuestStage.Overheard &&
            RunState.HelpedCompetitorInLastExperiment)
        {
            ShowPostExperimentTrust();
            return;
        }

        if (RunState.CompetitorQuest == CompetitorQuestStage.GardenMeetingScheduled)
        {
            DialogueUI.Instance.ShowDialogue(
                "Ракель",
                "Встретимся у входа в сад в 19:00. Не заставляй меня ждать и не веди за собой хвост.",
                "girl");
            return;
        }

        if (RunState.CompetitorQuest == CompetitorQuestStage.EscapeArchiveFound)
        {
            DialogueUI.Instance.ShowDialogue(
                "Ракель",
                "Нашёл что-то стоящее? Такие вещи не обсуждают в общей зоне. Подойди к саду.",
                "girl");
            return;
        }

        if (RunState.CompetitorQuest == CompetitorQuestStage.SmokeScheduleKnown ||
            RunState.CompetitorQuest == CompetitorQuestStage.GardenAccess ||
            RunState.CompetitorQuest == CompetitorQuestStage.GuardPostLead ||
            RunState.CompetitorQuest == CompetitorQuestStage.ArchiveKeyAcquired)
        {
            DialogueUI.Instance.ShowDialogue(
                "Ракель",
                "Расписание у тебя есть. Сад не про двери, а про момент, когда никто не смотрит. Не перепутай.",
                "girl");
            return;
        }

        if (RunState.CompetitorQuest == CompetitorQuestStage.Unknown)
        {
            DialogueUI.Instance.ShowDialogue(
                "Ракель",
                "Новенький? Не трать моё время. Здесь выживают не те, кто задаёт вопросы, а те, кто понимает, когда молчать.",
                "girl");
            return;
        }

        if (RunState.CompetitorQuest == CompetitorQuestStage.Overheard)
        {
            DialogueUI.Instance.ShowDialogue(
                "Ракель",
                "Ты слишком много смотришь по сторонам. Это может быть полезным качеством. Или последней ошибкой.",
                "girl");
            return;
        }

        DialogueUI.Instance.ShowDialogue(
            "Ракель",
            "Если ты пришёл просить совет, начни с простого: не стой у меня на пути.",
            "girl");
    }

    private static void ShowPostExperimentTrust()
    {
        RunState.ScheduleRaquelGardenMeeting();
        DialogueUI.Instance.ShowDialogueSequence(
            new DialogueUI.DialogueLine(
                "Ракель",
                "В эксперименте ты мог пробежать мимо. Не пробежал.",
                "girl"),
            new DialogueUI.DialogueLine(
                "Ракель",
                "Не называй это дружбой. Если хочешь услышать что-то полезное, встретимся у входа в сад в 19:00.",
                "girl"),
            new DialogueUI.DialogueLine(
                "Ракель",
                "<color=#75D99A>Новая цель: встретиться с Ракель у входа в сад в 19:00.</color>",
                "girl"));
    }

    private void UpdateRoute()
    {
        if (Grid == null || IsMoving || Time.time < nextRouteStepAt) return;

        Vector2Int destination = ScheduledDestination();
        if (GridPosition == destination)
        {
            if (!reachedStaffRoomThisRun && destination == StaffRoom)
            {
                reachedStaffRoomThisRun = true;
                RunState.MarkCompetitorReachedStaffRoom();
                Grid.BeginStaffRoomMeeting();
            }
            return;
        }

        Vector2Int step = FindNextRouteStep(destination);
        if (step != Vector2Int.zero)
        {
            TryMoveToCell(GridPosition + step, allowDoorCells: true);
            nextRouteStepAt = Time.time + RouteStepDelay;
        }
    }

    private Vector2Int ScheduledDestination()
    {
        int minute = RunState.MinuteOfDay;
        if (RunState.DayPhase == DayPhase.ExperimentAssembly ||
            RunState.DayPhase == DayPhase.EscortedToExperiment)
        {
            return ExperimentAssembly;
        }

        if (minute < 9 * 60) return CellMirror;
        if (minute < 10 * 60 + 30) return CommonWalk;
        if (minute < 11 * 60) return Toilet;
        if (minute < DaySchedule.ExperimentAnnouncementMinute) return StaffRoom;
        return ExperimentAssembly;
    }

    private Vector2Int FindNextRouteStep(Vector2Int destination)
    {
        var queue = new Queue<Vector2Int>();
        var previous = new Dictionary<Vector2Int, Vector2Int>();
        queue.Enqueue(GridPosition);
        previous[GridPosition] = GridPosition;

        Vector2Int[] directions =
        {
            Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down
        };

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            if (current == destination) break;

            foreach (Vector2Int direction in directions)
            {
                Vector2Int next = current + direction;
                if (previous.ContainsKey(next) || !IsNpcWalkable(next.x, next.y, allowDoorCells: true)) continue;
                previous[next] = current;
                queue.Enqueue(next);
            }
        }

        if (!previous.ContainsKey(destination)) return Vector2Int.zero;

        Vector2Int stepCell = destination;
        while (previous[stepCell] != GridPosition)
        {
            stepCell = previous[stepCell];
        }

        return stepCell - GridPosition;
    }
}
