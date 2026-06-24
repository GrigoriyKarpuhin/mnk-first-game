using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// Управление игроком - движение по гриду с анимациями
/// </summary>
public class Player : MonoBehaviour
{
    [Header("Sprite (оставь пустым если используешь Animator)")]
    [SerializeField] private Sprite playerSprite;
    
    [Header("Visual Settings")]
    [SerializeField] private Color playerColor = new Color(0.2f, 0.6f, 1f);
    [SerializeField] private float moveSpeed = 6.7f;

    [Header("Interaction")]
    [SerializeField] private float interactRange = 1.2f;

    [Header("Health")]
    [SerializeField] private int maxHealth = 100;

    // Текущая позиция на гриде
    private int gridX;
    private int gridY;
    private int currentHealth;
    private float invulnerableUntil;

    // Целевая позиция для плавного движения
    private Vector3 targetPosition;
    private bool isMoving;

    // Направление движения (для анимаций)
    private Vector2 lastMoveDirection;
    private Vector2 facingDirection = Vector2.right;

    // Ссылка на грид
    private GameGrid grid;

    // Компоненты
    private SpriteRenderer spriteRenderer;
    private Animator animator;

    // Input System
    private InputActionMap inputMap;
    private InputAction moveAction;
    private InputAction interactAction;
    private InputAction useImplantAction;
    private InputAction useSecondImplantAction;
    private InputAction takedownAction;
    private InputAction journalAction;
    private InputAction investigationBoardAction;
    private InputAction mapAction;
    private InputAction crouchAction;
    private InputAction noiseAction;
    private readonly HashSet<PrisonItemId> inventory = new HashSet<PrisonItemId>();

    // Стелс-состояние игрока (контр-механики обнаружения).
    private bool isCrouching;
    private bool isHidden;
    private bool inCover;
    private float nextNoiseTime;

    [SerializeField] private float crouchSpeedMultiplier = 0.55f;
    [SerializeField] private float noiseCooldown = 1.5f;
    [SerializeField] private int noiseHearRange = 9;

    /// <summary>
    /// Инициализация игрока
    /// </summary>
    public void Initialize(GameGrid gameGrid, int startX, int startY)
    {
        grid = gameGrid;
        gridX = startX;
        gridY = startY;
        currentHealth = maxHealth;

        // Создаём визуал игрока
        CreateVisual();

        // Настраиваем Input System
        SetupInput();

        // Устанавливаем начальную позицию
        targetPosition = grid.GridToWorld(gridX, gridY);
        transform.position = targetPosition;
        
        // Устанавливаем начальный sorting order
        UpdateSortingOrder();
    }

    /// <summary>
    /// Настройка Input System
    /// </summary>
    private void SetupInput()
    {
        // Создаём простой InputActionMap для движения
        inputMap = new InputActionMap("Player");
        
        moveAction = inputMap.AddAction("Move", InputActionType.Value);
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/rightArrow");

        interactAction = inputMap.AddAction("Interact", InputActionType.Button);
        interactAction.AddBinding("<Keyboard>/e");

        useImplantAction = inputMap.AddAction("Use Implant", InputActionType.Button);
        useImplantAction.AddBinding("<Keyboard>/q");

        useSecondImplantAction = inputMap.AddAction("Use Second Implant", InputActionType.Button);
        useSecondImplantAction.AddBinding("<Keyboard>/r");

        takedownAction = inputMap.AddAction("Silent Takedown", InputActionType.Button);
        takedownAction.AddBinding("<Keyboard>/f");

        journalAction = inputMap.AddAction("Quest Journal", InputActionType.Button);
        journalAction.AddBinding("<Keyboard>/j");

        investigationBoardAction = inputMap.AddAction("Investigation Board", InputActionType.Button);
        investigationBoardAction.AddBinding("<Keyboard>/b");

        mapAction = inputMap.AddAction("Prison Map", InputActionType.Button);
        mapAction.AddBinding("<Keyboard>/m");

        crouchAction = inputMap.AddAction("Crouch", InputActionType.Button);
        crouchAction.AddBinding("<Keyboard>/leftShift");
        crouchAction.AddBinding("<Keyboard>/rightShift");

        noiseAction = inputMap.AddAction("Make Noise", InputActionType.Button);
        noiseAction.AddBinding("<Keyboard>/g");

        inputMap.Enable();
    }

    private void OnDestroy()
    {
        inputMap?.Disable();
    }

    /// <summary>
    /// Создаёт визуальное представление игрока
    /// </summary>
    private void CreateVisual()
    {
        // Получаем компоненты
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();

        // Если есть Animator - используем анимации
        if (animator != null)
        {
            // SpriteRenderer должен быть настроен на объекте с Animator
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }
            // Масштаб для анимированного персонажа
            transform.localScale = Vector3.one * WorldMetrics.CharacterScale;
            return;
        }

        // Если нет Animator - создаём простой спрайт
        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }

        // Пиксель-арт по умолчанию из Resources/Sprites, если не задан в инспекторе.
        if (playerSprite == null)
        {
            playerSprite = SpriteWalkAnimator.FeetAnchored(Resources.Load<Sprite>("Sprites/player"));
        }

        if (playerSprite != null)
        {
            spriteRenderer.sprite = playerSprite;
            spriteRenderer.color = Color.white;
            float spriteSize = Mathf.Max(playerSprite.bounds.size.x, playerSprite.bounds.size.y);
            transform.localScale = Vector3.one * grid.CellSize * WorldMetrics.CharacterScale / spriteSize;
            SpriteWalkAnimator.TryAttach(gameObject, "player");
        }
        else
        {
            spriteRenderer.sprite = CreateCircleSprite();
            spriteRenderer.color = playerColor;
            transform.localScale = Vector3.one * grid.CellSize * WorldMetrics.CharacterScale;
        }

        CharacterGroundShadow.Attach(gameObject);
    }

    /// <summary>
    /// Создаёт круглый спрайт
    /// </summary>
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
        HandleJournal();
        HandleInvestigationBoard();
        HandleMap();

        if (DialogueUI.IsModalOpen)
        {
            isMoveInputHeld = false;
            UpdateAnimation();
            return;
        }

        HandleCrouch();
        HandleNoise();
        HandleInput();
        HandleInteract();
        HandleSilentTakedown();
        HandleImplant();
        UpdateMovement();
        UpdateStealthState();
        UpdateAnimation();
    }

    private void HandleCrouch()
    {
        isCrouching = crouchAction != null && crouchAction.IsPressed();
    }

    /// <summary>Отвлечение: стук по стене (G) уводит ближнюю охрану на шум.</summary>
    private void HandleNoise()
    {
        if (noiseAction == null || !noiseAction.WasPressedThisFrame()) return;
        if (Time.time < nextNoiseTime || grid == null) return;
        nextNoiseTime = Time.time + noiseCooldown;

        Vector2Int from = GridPosition;
        int alerted = 0;
        foreach (GuardPatrol guard in FindObjectsByType<GuardPatrol>(FindObjectsSortMode.None))
        {
            Vector2Int d = guard.GridPosition - from;
            if (Mathf.Abs(d.x) + Mathf.Abs(d.y) > noiseHearRange) continue;
            if (guard.HearNoise(from)) alerted++;
        }

        DialogueUI.Instance.Show(alerted > 0
            ? "Вы стукнули по стене — надзиратель пошёл на шум."
            : "Вы стукнули по стене. Тишина в ответ.", 1.4f);
    }

    /// <summary>Обновляет «в укрытии» и невидимость, тонирует спрайт по стелс-состоянию.</summary>
    private void UpdateStealthState()
    {
        inCover = !isMoving && !isHidden && IsAdjacentToCover();

        if (spriteRenderer == null) return;
        Color c = spriteRenderer.color;
        if (isHidden) c.a = 0.35f;
        else c.a = isCrouching ? 0.8f : 1f;
        spriteRenderer.color = c;
    }

    private bool IsAdjacentToCover()
    {
        if (grid == null) return false;
        if (grid.GetTileType(gridX, gridY) == TileType.Cover) return true;
        return grid.GetTileType(gridX + 1, gridY) == TileType.Cover
            || grid.GetTileType(gridX - 1, gridY) == TileType.Cover
            || grid.GetTileType(gridX, gridY + 1) == TileType.Cover
            || grid.GetTileType(gridX, gridY - 1) == TileType.Cover;
    }

    private void HandleJournal()
    {
        if (!DialogueUI.IsDialogueOpen &&
            !InvestigationBoardUI.IsOpen &&
            journalAction != null &&
            journalAction.WasPressedThisFrame())
        {
            QuestJournalUI.Toggle();
        }
    }

    private void HandleInvestigationBoard()
    {
        if (!DialogueUI.IsDialogueOpen &&
            !QuestJournalUI.IsOpen &&
            investigationBoardAction != null &&
            investigationBoardAction.WasPressedThisFrame())
        {
            InvestigationBoardUI.Toggle();
        }
    }

    private void HandleMap()
    {
        if (mapAction == null || !mapAction.WasPressedThisFrame()) return;
        if (DialogueUI.IsDialogueOpen || QuestJournalUI.IsOpen || InvestigationBoardUI.IsOpen || PrisonMapUI.IsOpen) return;

        PrisonMapUI.Open(grid, this);
    }

    private void HandleImplant()
    {
        if (useSecondImplantAction != null && useSecondImplantAction.WasPressedThisFrame())
        {
            if (!RunState.HasImplant(ImplantId.EyeImplant))
            {
                DialogueUI.Instance.Show("Глазной имплант не установлен.", 1.2f);
            }
            else if (RunState.ToggleEyeImplant())
            {
                string state = RunState.EyeImplantActive ? "активен" : "выключен";
                DialogueUI.Instance.Show($"Глазной имплант {state}.", 1.2f);
            }
        }

        if (!RunState.HasReactiveFeet || useImplantAction == null) return;
        if (!useImplantAction.WasPressedThisFrame() || isMoving || grid == null) return;

        int dx = Mathf.RoundToInt(facingDirection.x);
        int dy = Mathf.RoundToInt(facingDirection.y);
        if (dx == 0 && dy == 0) dx = 1;

        int distance = grid.IsWalkable(gridX + dx * 2, gridY + dy * 2) ? 2 : 1;
        if (!grid.IsWalkable(gridX + dx * distance, gridY + dy * distance)) return;

        gridX += dx * distance;
        gridY += dy * distance;
        targetPosition = grid.GridToWorld(gridX, gridY);
        isMoving = true;
    }

    // Убрал stepDelay - теперь движение непрерывное
    // Флаг: клавиша движения зажата
    private bool isMoveInputHeld;

    /// <summary>
    /// Обрабатывает ввод с клавиатуры (плавное непрерывное движение)
    /// </summary>
    private void HandleInput()
    {
        if (moveAction == null) return;

        var input = moveAction.ReadValue<Vector2>();
        isMoveInputHeld = input.sqrMagnitude > 0.5f;
        if (isMoveInputHeld && RunState.IsRestingInBed)
        {
            RunState.StopRestingInBed();
            DialogueUI.Instance.Show("Вы встали с кровати.", 1.2f);
        }

        // В укрытии стоим на месте; движение выводит из него.
        if (isHidden)
        {
            if (!isMoveInputHeld) return;
            isHidden = false;
        }

        // Не принимаем новый ввод пока двигаемся
        if (isMoving) return;

        if (!isMoveInputHeld) return;

        int dx = 0;
        int dy = 0;

        // Определяем направление (приоритет по большей оси)
        if (Mathf.Abs(input.y) >= Mathf.Abs(input.x))
        {
            dy = input.y > 0 ? 1 : -1;
        }
        else
        {
            dx = input.x > 0 ? 1 : -1;
        }

        // Пробуем переместиться
        if (dx != 0 || dy != 0)
        {
            TryMove(dx, dy);
        }
    }

    /// <summary>
    /// Взаимодействие с ближайшим объектом или NPC по кнопке E
    /// </summary>
    private void HandleInteract()
    {
        if (interactAction == null) return;
        if (!interactAction.WasPressedThisFrame()) return;

        // В укрытии E — выйти из него.
        if (isHidden)
        {
            isHidden = false;
            DialogueUI.Instance.Show("Вы вышли из укрытия.", 1f);
            return;
        }

        IGridInteractable nearestInteractable = null;
        NPC nearestNpc = null;
        float nearestDistance = float.MaxValue;

        foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (!(behaviour is IGridInteractable interactable)) continue;

            float dist = Vector2.Distance(transform.position, interactable.InteractionPosition);
            if (dist <= interactRange && dist < nearestDistance)
            {
                nearestDistance = dist;
                nearestInteractable = interactable;
                nearestNpc = null;
            }
        }

        foreach (var npc in FindObjectsByType<NPC>(FindObjectsSortMode.None))
        {
            float dist = Vector2.Distance(transform.position, npc.transform.position);
            if (dist <= interactRange && dist < nearestDistance)
            {
                nearestDistance = dist;
                nearestNpc = npc;
                nearestInteractable = null;
            }
        }

        if (nearestInteractable != null)
        {
            nearestInteractable.Interact(this);
        }
        else if (nearestNpc != null)
        {
            nearestNpc.Interact();
        }
        else if (grid != null && grid.IsHideSpot(GridPosition))
        {
            isHidden = true;
            isMoving = false;
            DialogueUI.Instance.Show("Вы спрятались. Надзиратели вас не видят.", 1.6f);
        }
    }

    private void HandleSilentTakedown()
    {
        if (takedownAction == null || !takedownAction.WasPressedThisFrame()) return;

        GuardPatrol nearestGuard = null;
        float nearestDistance = float.MaxValue;
        foreach (GuardPatrol guard in FindObjectsByType<GuardPatrol>(FindObjectsSortMode.None))
        {
            float distance = Vector2.Distance(transform.position, guard.transform.position);
            if (distance <= grid.CellSize * 1.35f && distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestGuard = guard;
            }
        }

        if (nearestGuard == null)
        {
            DialogueUI.Instance.Show("Рядом нет надзирателя.", 1f);
            return;
        }

        if (!nearestGuard.CanBeSilentlyTakedownBy(this))
        {
            DialogueUI.Instance.Show("Для тихого устранения нужно подойти сзади.", 1.4f);
            return;
        }

        nearestGuard.SilentTakedown();
    }

    /// <summary>
    /// Пытается переместить игрока
    /// </summary>
    private void TryMove(int dx, int dy)
    {
        int newX = gridX + dx;
        int newY = gridY + dy;

        // Сохраняем направление для анимаций
        lastMoveDirection = new Vector2(dx, dy);
        facingDirection = new Vector2(dx, dy);

        // Проверяем, можно ли пройти
        if (grid.IsWalkable(newX, newY))
        {
            gridX = newX;
            gridY = newY;
            targetPosition = grid.GridToWorld(gridX, gridY);
            isMoving = true;
        }
    }

    /// <summary>
    /// Плавное движение к целевой позиции
    /// </summary>
    private void UpdateMovement()
    {
        if (!isMoving) return;

        float speed = isCrouching ? moveSpeed * crouchSpeedMultiplier : moveSpeed;
        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            speed * Time.deltaTime
        );

        // Обновляем sorting order по Y (чем ниже - тем поверх)
        UpdateSortingOrder();

        // Проверяем, достигли ли цели
        if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
        {
            transform.position = targetPosition;
            isMoving = false;
        }
    }

    /// <summary>
    /// Обновляет sorting order для правильной глубины
    /// </summary>
    private void UpdateSortingOrder()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = SortingLayers.Entity(transform.position.y);
        }
    }

    /// <summary>
    /// Обновляет анимации и направление спрайта
    /// </summary>
    private void UpdateAnimation()
    {
        if (animator == null) return;

        // Анимация Run пока двигаемся ИЛИ пока клавиша зажата (для плавности)
        bool shouldRun = isMoving || isMoveInputHeld;
        animator.SetInteger("AnimState", shouldRun ? 2 : 0);

        // Flip спрайта влево/вправо
        if (facingDirection.x > 0)
        {
            // Смотрим вправо (flip по X)
            transform.localScale = new Vector3(-Mathf.Abs(WorldMetrics.CharacterScale), WorldMetrics.CharacterScale, 1);
        }
        else if (facingDirection.x < 0)
        {
            // Смотрим влево (нормально)
            transform.localScale = new Vector3(Mathf.Abs(WorldMetrics.CharacterScale), WorldMetrics.CharacterScale, 1);
        }
    }

    /// <summary>
    /// Текущая позиция на гриде
    /// </summary>
    public Vector2Int GridPosition => new Vector2Int(gridX, gridY);
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    public bool IsHidden => isHidden;
    public bool IsCrouching => isCrouching;
    public bool IsInCover => inCover;

    /// <summary>
    /// «Заметность» игрока: 0 — невидим (укрытие), ~1 — открыто идёт. Охрана
    /// умножает скорость роста тревоги на это значение. Движение выдаёт сильнее,
    /// приседание и прятки за cover резко снижают заметность.
    /// </summary>
    public float StealthExposure
    {
        get
        {
            if (isHidden) return 0f;
            float exposure = isMoving ? 1f : 0.5f; // движение выдаёт; стоять — тише
            if (isCrouching) exposure *= 0.4f;
            if (inCover) exposure *= 0.12f;         // вплотную к укрытию — почти не палят
            return exposure;
        }
    }

    public void TeleportToCell(Vector2Int cell)
    {
        gridX = cell.x;
        gridY = cell.y;
        targetPosition = grid.GridToWorld(gridX, gridY);
        transform.position = targetPosition;
        isMoving = false;
        UpdateSortingOrder();
        grid.UpdateWallCutaway(transform.position);
    }

    public void TakeDamage(int amount)
    {
        if (Time.time < invulnerableUntil || amount <= 0) return;

        currentHealth = Mathf.Max(0, currentHealth - amount);
        invulnerableUntil = Time.time + 0.45f;
        DialogueUI.Instance.Show($"Надзиратель ударил вас. Здоровье: {currentHealth}/{maxHealth}", 1f);

        if (currentHealth == 0)
        {
            KillAndResetRun("Вы погибли.");
        }
    }

    public void KillAndResetRun(string message)
    {
        if (!string.IsNullOrEmpty(message))
        {
            DialogueUI.Instance.Show(message, 1f);
        }

        RunState.RestartRunInPrison();
    }

    public bool HasItem(PrisonItemId itemId)
    {
        return inventory.Contains(itemId) || RunState.HasPrisonItem(itemId);
    }

    public void AddItem(PrisonItemId itemId)
    {
        if (itemId == PrisonItemId.None || itemId == PrisonItemId.Unavailable) return;
        inventory.Add(itemId);
        RunState.AddPrisonItem(itemId);
    }

    /// <summary>
    /// Проигрывает анимацию подбора предмета (присел — поднял — встал).
    /// Работает на покадровом SpriteWalkAnimator; при использовании Unity
    /// Animator ничего не делает.
    /// </summary>
    public void PlayPickupAnimation()
    {
        var walkAnimator = GetComponent<SpriteWalkAnimator>();
        if (walkAnimator != null) walkAnimator.PlayPickup();
    }

    public static string GetItemName(PrisonItemId itemId)
    {
        switch (itemId)
        {
            case PrisonItemId.Screwdriver: return "самодельная отвёртка";
            case PrisonItemId.KitchenManifest: return "подсказка к коду склада";
            case PrisonItemId.ServiceBadge: return "служебный пропуск";
            case PrisonItemId.EyeImplant: return "глазной имплант";
            case PrisonItemId.Transmitter: return "передатчик";
            case PrisonItemId.ExperimentReports: return "отчёты об экспериментах";
            case PrisonItemId.DataSource: return "источник данных";
            case PrisonItemId.ComputeModule: return "модуль доступа";
            case PrisonItemId.SignalAmplifier: return "усилитель сигнала";
            default: return "неизвестный доступ";
        }
    }

    private GUIStyle hudStyle;
    private Texture2D hpPanelTex;
    private Texture2D hpCellTex;
    private bool hpPanelMissing;

    // HUD здоровья: CRT-панель C-4821. 10 делений по 10 HP.
    // Экран в ассете пустой; горящие ячейки рисуем кодом (hud_hp_cell) поверх —
    // так зелёное жёстко держится в границах ячейки, без расплывающегося glow.
    private const int HpSegmentCount = 10;
    private const float HpPanelWidth = 170f;

    // Прямоугольники ячеек, нормализованные к размеру панели (0..1, начало
    // отсчёта — левый верх). Сгенерированы из ассета hud_hp_panel.png.
    private static readonly Rect[] HpSegmentRects =
    {
        new Rect(0.28099f, 0.48435f, 0.04927f, 0.12155f),
        new Rect(0.33949f, 0.48435f, 0.05004f, 0.12155f),
        new Rect(0.39954f, 0.48435f, 0.04927f, 0.12155f),
        new Rect(0.45881f, 0.48435f, 0.05004f, 0.12155f),
        new Rect(0.51809f, 0.48435f, 0.05004f, 0.12155f),
        new Rect(0.57814f, 0.48435f, 0.05004f, 0.12155f),
        new Rect(0.63818f, 0.48435f, 0.05004f, 0.12155f),
        new Rect(0.69823f, 0.48435f, 0.05004f, 0.12155f),
        new Rect(0.75751f, 0.48435f, 0.05004f, 0.12155f),
        new Rect(0.81678f, 0.48435f, 0.05004f, 0.12155f),
    };

    private void OnGUI()
    {
        if (QuestJournalUI.IsOpen || InvestigationBoardUI.IsOpen || PrisonMapUI.IsOpen) return;

        hudStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = Color.white } };

        // Подсказки — одна компактная строка.
        string eye = RunState.HasImplant(ImplantId.EyeImplant)
            ? (RunState.EyeImplantActive ? "R выкл. глаз" : "R глаз")
            : "R —";
        string feet = RunState.HasReactiveFeet ? "Q стопы" : "Q —";
        string rest = RunState.IsRestingInBed ? " · отдых x4" : "";
        string controls = $"WASD ходить · Shift красться · G шум · E действие · M карта · J журнал · B доска · F со спины · {feet} · {eye} · Предметы {RunState.PrisonItemCount}{rest}";
        GUI.Box(new Rect(6, 6, 790, 18), "");
        GUI.Label(new Rect(12, 7, 782, 16), controls, hudStyle);

        DrawHealthPanel();
        DrawStealthStatus();

        if (!string.IsNullOrEmpty(RunState.ActiveObjective))
        {
            GUI.Box(new Rect(6, 102, 430, 34), "");
            GUI.Label(new Rect(12, 108, 418, 22), RunState.ActiveObjective, hudStyle);
        }
    }

    private void DrawStealthStatus()
    {
        string label = null;
        Color color = Color.white;
        if (isHidden) { label = "СКРЫТ"; color = new Color(0.4f, 0.8f, 1f); }
        else if (inCover) { label = "В УКРЫТИИ"; color = new Color(0.4f, 0.9f, 0.6f); }
        else if (isCrouching) { label = "КРАДЁТСЯ"; color = new Color(0.85f, 0.85f, 0.5f); }
        if (label == null) return;

        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = color }
        };
        GUI.Box(new Rect(Screen.width * 0.5f - 70f, 6, 140, 22), "");
        GUI.Label(new Rect(Screen.width * 0.5f - 70f, 7, 140, 20), label, style);
    }

    private void DrawHealthPanel()
    {
        if (hpPanelTex == null && !hpPanelMissing)
        {
            hpPanelTex = Resources.Load<Texture2D>("Sprites/hud_hp_panel");
            hpCellTex = Resources.Load<Texture2D>("Sprites/hud_hp_cell");
            hpPanelMissing = hpPanelTex == null;
        }

        // Фолбэк, если ассет не нашёлся — узкая полоса как раньше.
        if (hpPanelTex == null)
        {
            var bar = new Rect(6, 28, 116, 14);
            GUI.Box(bar, "");
            GUI.color = new Color(0.75f, 0.12f, 0.12f);
            GUI.DrawTexture(new Rect(bar.x + 2, bar.y + 2, (bar.width - 4) * currentHealth / maxHealth, bar.height - 4), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(bar.x + 6, bar.y, bar.width - 8, bar.height), $"HP {currentHealth}/{maxHealth}", hudStyle);
            return;
        }

        float w = HpPanelWidth;
        float h = w * hpPanelTex.height / hpPanelTex.width;
        var panel = new Rect(6, 28, w, h);
        GUI.DrawTexture(panel, hpPanelTex, ScaleMode.StretchToFill, true);

        // Сколько ячеек горит: каждая = 10 HP, частичное значение считаем горящим.
        int hpPerSegment = Mathf.Max(1, maxHealth / HpSegmentCount);
        int lit = Mathf.CeilToInt(currentHealth / (float)hpPerSegment);
        lit = Mathf.Clamp(lit, 0, HpSegmentCount);

        // Горящие ячейки штампуем поверх пустого экрана. Погашенные не рисуем —
        // тёмный экран сам читается как пустой слот.
        if (hpCellTex != null)
        {
            for (int i = 0; i < lit; i++)
            {
                var r = HpSegmentRects[i];
                var cell = new Rect(panel.x + r.x * panel.width, panel.y + r.y * panel.height,
                                    r.width * panel.width, r.height * panel.height);
                GUI.DrawTexture(cell, hpCellTex, ScaleMode.StretchToFill, true);
            }
        }
    }
}
