using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
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
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float playerScale = 0.8f;

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
    private InputAction takedownAction;
    private readonly HashSet<PrisonItemId> inventory = new HashSet<PrisonItemId>();

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

        takedownAction = inputMap.AddAction("Silent Takedown", InputActionType.Button);
        takedownAction.AddBinding("<Keyboard>/f");

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
            transform.localScale = Vector3.one * playerScale;
            return;
        }

        // Если нет Animator - создаём простой спрайт
        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }

        if (playerSprite != null)
        {
            spriteRenderer.sprite = playerSprite;
            spriteRenderer.color = Color.white;
            float spriteSize = Mathf.Max(playerSprite.bounds.size.x, playerSprite.bounds.size.y);
            transform.localScale = Vector3.one * grid.CellSize * playerScale / spriteSize;
        }
        else
        {
            spriteRenderer.sprite = CreateCircleSprite();
            spriteRenderer.color = playerColor;
            transform.localScale = Vector3.one * grid.CellSize * playerScale;
        }
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
        HandleInput();
        HandleInteract();
        HandleSilentTakedown();
        HandleImplant();
        UpdateMovement();
        UpdateAnimation();
    }

    private void HandleImplant()
    {
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

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            moveSpeed * Time.deltaTime
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
            transform.localScale = new Vector3(-Mathf.Abs(playerScale), playerScale, 1);
        }
        else if (facingDirection.x < 0)
        {
            // Смотрим влево (нормально)
            transform.localScale = new Vector3(Mathf.Abs(playerScale), playerScale, 1);
        }
    }

    /// <summary>
    /// Текущая позиция на гриде
    /// </summary>
    public Vector2Int GridPosition => new Vector2Int(gridX, gridY);
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    public void TakeDamage(int amount)
    {
        if (Time.time < invulnerableUntil || amount <= 0) return;

        currentHealth = Mathf.Max(0, currentHealth - amount);
        invulnerableUntil = Time.time + 0.45f;
        DialogueUI.Instance.Show($"Надзиратель ударил вас. Здоровье: {currentHealth}/{maxHealth}", 1f);

        if (currentHealth == 0)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
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

    public static string GetItemName(PrisonItemId itemId)
    {
        switch (itemId)
        {
            case PrisonItemId.Screwdriver: return "самодельная отвёртка";
            case PrisonItemId.KitchenManifest: return "подсказка к коду склада";
            case PrisonItemId.ServiceBadge: return "служебный пропуск";
            case PrisonItemId.EyeImplant: return "глазной имплант";
            case PrisonItemId.ExperimentReports: return "отчёты об экспериментах";
            default: return "неизвестный доступ";
        }
    }

    private GUIStyle hudStyle;

    private void OnGUI()
    {
        hudStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = Color.white } };

        // Подсказки — одна компактная строка.
        string controls = $"WASD ходить · E действие · F со спины · Стопы {(RunState.HasReactiveFeet ? "Q" : "—")} · Предметы {RunState.PrisonItemCount}";
        GUI.Box(new Rect(6, 6, 430, 18), "");
        GUI.Label(new Rect(12, 7, 424, 16), controls, hudStyle);

        // Здоровье — узкая полоса.
        var hp = new Rect(6, 28, 116, 14);
        GUI.Box(hp, "");
        GUI.color = new Color(0.75f, 0.12f, 0.12f);
        GUI.DrawTexture(new Rect(hp.x + 2, hp.y + 2, (hp.width - 4) * currentHealth / maxHealth, hp.height - 4), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(hp.x + 6, hp.y, hp.width - 8, hp.height), $"HP {currentHealth}/{maxHealth}", hudStyle);
    }
}
