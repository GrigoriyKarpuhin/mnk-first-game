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
    private TextMesh hintLabel;
    private Player player;

    private float moveTimer;

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
            npcSprite = Resources.Load<Sprite>("Sprites/npc_programmer");
        }

        if (npcSprite != null)
        {
            spriteRenderer.sprite = npcSprite;
            spriteRenderer.color = Color.white;
            float spriteSize = Mathf.Max(npcSprite.bounds.size.x, npcSprite.bounds.size.y);
            transform.localScale = Vector3.one * grid.CellSize * WorldMetrics.CharacterScale / spriteSize;
            SpriteWalkAnimator.TryAttach(gameObject, "npc_programmer");
        }
        else
        {
            spriteRenderer.sprite = CreateCircleSprite();
            spriteRenderer.color = npcColor;
            transform.localScale = Vector3.one * grid.CellSize * WorldMetrics.CharacterScale;
        }
    }

    private void CreateHint()
    {
        var hintObject = new GameObject("Hint");
        hintObject.transform.SetParent(transform);
        hintObject.transform.localPosition = new Vector3(0f, hintHeight, 0f);

        hintLabel = hintObject.AddComponent<TextMesh>();
        hintLabel.text = hintText;
        hintLabel.fontSize = 44;
        hintLabel.characterSize = 0.06f;
        hintLabel.anchor = TextAnchor.MiddleCenter;
        hintLabel.color = Color.white;
        hintLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        hintLabel.GetComponent<MeshRenderer>().sortingOrder = SortingLayers.Entity(transform.position.y) + 5;

        hintObject.SetActive(false);
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
        if (hintLabel == null) return;

        if (player == null)
        {
            player = FindFirstObjectByType<Player>();
        }

        if (player == null) return;

        float distance = Vector2.Distance(transform.position, player.transform.position);
        bool shouldShow = distance <= hintRange;
        if (hintLabel.gameObject.activeSelf != shouldShow)
        {
            hintLabel.gameObject.SetActive(shouldShow);
        }
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

    private bool TryMove(int dx, int dy)
    {
        int newX = gridX + dx;
        int newY = gridY + dy;

        if (dx != 0) facingDirection = new Vector2(dx, 0);

        if (grid.IsWalkable(newX, newY))
        {
            gridX = newX;
            gridY = newY;
            targetPosition = grid.GridToWorld(gridX, gridY);
            isMoving = true;
            return true;
        }

        return false;
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
        if (hintLabel != null)
        {
            hintLabel.GetComponent<MeshRenderer>().sortingOrder = SortingLayers.Entity(transform.position.y) + 5;
        }
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

    public void Interact()
    {
        // Выбираем эксперимент из пула (если он собран в Resources), иначе —
        // дефолт на полосу препятствий. Сам выбор — в ExperimentSelector.
        var pool = Resources.Load<ExperimentPool>("ExperimentPool");
        if (pool != null)
        {
            var played = new HashSet<string>(RunState.PlayedExperiments);
            ExperimentDefinition def = ExperimentSelector.Select(
                pool.Experiments, RunState.Day, RunState.ParticipantCount, played, new System.Random());
            if (def != null)
            {
                RunState.EnterExperiment(def);
                return;
            }
        }

        RunState.EnterExperiment();
    }
}
