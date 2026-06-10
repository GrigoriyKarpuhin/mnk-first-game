using UnityEngine;

/// <summary>
/// Типы тайлов на карте
/// </summary>
public enum TileType
{
    Floor,  // Пол - можно ходить
    Wall    // Стена - нельзя ходить
}

/// <summary>
/// Управляет гридом карты и генерирует визуальное представление
/// </summary>
public class GameGrid : MonoBehaviour
{
    [Header("Grid Settings")]
    [SerializeField] private int width = 10;
    [SerializeField] private int height = 10;
    [SerializeField] private float cellSize = 1f;

    [Header("Sprites (оставь пустым для авто-генерации)")]
    [SerializeField] private Sprite floorSprite;      // Спрайт пола
    [SerializeField] private Sprite wallTopSprite;    // Спрайт верха стены
    [SerializeField] private Sprite wallSideSprite;   // Спрайт боковой части стены

    [Header("Tile Colors (если спрайты не заданы)")]
    [SerializeField] private Color floorColor = new Color(0.6f, 0.5f, 0.4f);
    [SerializeField] private Color wallTopColor = new Color(0.5f, 0.35f, 0.2f);
    [SerializeField] private Color wallSideColor = new Color(0.35f, 0.25f, 0.15f);
    
    [Header("Wall Settings")]
    [SerializeField] private float wallHeight = 0.6f;

    [Header("References")]
    [SerializeField] private Player player;
    [SerializeField] private NPC npc;

    // Данные карты
    private TileType[,] grid;
    private GameObject[,] tileObjects;

    public int Width => width;
    public int Height => height;
    public float CellSize => cellSize;

    private void Awake()
    {
        InitializeGrid();
        GenerateVisuals();
        SpawnPlayer();
        SpawnNPC();
    }

    /// <summary>
    /// Инициализирует грид с простой картой
    /// </summary>
    private void InitializeGrid()
    {
        grid = new TileType[width, height];
        tileObjects = new GameObject[width, height];

        // Заполняем всё полом
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                grid[x, y] = TileType.Floor;
            }
        }

        // Создаём стены по периметру
        for (int x = 0; x < width; x++)
        {
            grid[x, 0] = TileType.Wall;           // Нижняя стена
            grid[x, height - 1] = TileType.Wall;  // Верхняя стена
        }
        for (int y = 0; y < height; y++)
        {
            grid[0, y] = TileType.Wall;           // Левая стена
            grid[width - 1, y] = TileType.Wall;   // Правая стена
        }

        // Добавляем несколько внутренних стен для интереса
        grid[3, 3] = TileType.Wall;
        grid[3, 4] = TileType.Wall;
        grid[3, 5] = TileType.Wall;
        grid[6, 5] = TileType.Wall;
        grid[6, 6] = TileType.Wall;
        grid[6, 7] = TileType.Wall;
    }

    /// <summary>
    /// Создаёт визуальные объекты для каждого тайла
    /// </summary>
    private void GenerateVisuals()
    {
        // Создаём родительский объект для тайлов
        var tilesParent = new GameObject("Tiles");
        tilesParent.transform.SetParent(transform);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var tile = CreateTileVisual(x, y, grid[x, y]);
                tile.transform.SetParent(tilesParent.transform);
                tileObjects[x, y] = tile;
            }
        }
    }

    /// <summary>
    /// Создаёт визуальный объект для одного тайла
    /// </summary>
    private GameObject CreateTileVisual(int x, int y, TileType type)
    {
        var tile = new GameObject($"Tile_{x}_{y}");
        var worldPos = GridToWorld(x, y);
        tile.transform.position = worldPos;

        if (type == TileType.Floor)
        {
            // Пол - ВСЕГДА под всем
            var renderer = tile.AddComponent<SpriteRenderer>();
            renderer.sprite = floorSprite != null ? floorSprite : CreateSquareSprite();
            renderer.color = floorSprite != null ? Color.white : floorColor;
            renderer.sortingOrder = SortingLayers.Floor;
            tile.transform.localScale = floorSprite != null 
                ? Vector3.one * cellSize / GetSpriteSize(floorSprite) 
                : Vector3.one * cellSize * 0.98f;
        }
        else
        {
            // Стена - верхняя часть + боковая грань
            CreateWallVisual(tile, worldPos, x, y);
        }

        return tile;
    }

    /// <summary>
    /// Создаёт визуал стены с объёмом (верх + бок)
    /// </summary>
    private void CreateWallVisual(GameObject parent, Vector3 worldPos, int x, int y)
    {
        // Верхняя часть стены
        var top = new GameObject("Top");
        top.transform.SetParent(parent.transform);
        top.transform.localPosition = new Vector3(0, wallHeight * 0.5f, 0);
        
        var topRenderer = top.AddComponent<SpriteRenderer>();
        topRenderer.sprite = wallTopSprite != null ? wallTopSprite : CreateSquareSprite();
        topRenderer.color = wallTopSprite != null ? Color.white : wallTopColor;
        // Сортировка по основанию стены (нижняя точка тайла)
        float baseY = worldPos.y - cellSize * 0.5f;
        topRenderer.sortingOrder = SortingLayers.Wall(baseY);
        top.transform.localScale = wallTopSprite != null 
            ? Vector3.one * cellSize / GetSpriteSize(wallTopSprite)
            : Vector3.one * cellSize * 0.98f;

        // Боковая (передняя) грань стены - рисуем всегда
        var side = new GameObject("Side");
        side.transform.SetParent(parent.transform);
        side.transform.localPosition = new Vector3(0, -cellSize * 0.5f + wallHeight * 0.5f, 0);
        
        var sideRenderer = side.AddComponent<SpriteRenderer>();
        sideRenderer.sprite = wallSideSprite != null ? wallSideSprite : CreateSquareSprite();
        sideRenderer.color = wallSideSprite != null ? Color.white : wallSideColor;
        // Боковая грань сортируется чуть ниже верха (чтобы верх был поверх)
        sideRenderer.sortingOrder = SortingLayers.Wall(baseY) - 1;
        
        if (wallSideSprite != null)
        {
            float spriteAspect = wallSideSprite.bounds.size.x / wallSideSprite.bounds.size.y;
            side.transform.localScale = new Vector3(cellSize / wallSideSprite.bounds.size.x, wallHeight / wallSideSprite.bounds.size.y, 1);
        }
        else
        {
            side.transform.localScale = new Vector3(cellSize * 0.98f, wallHeight, 1);
        }
    }

    /// <summary>
    /// Возвращает размер спрайта в юнитах
    /// </summary>
    private float GetSpriteSize(Sprite sprite)
    {
        return Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
    }

    /// <summary>
    /// Создаёт простой белый квадратный спрайт
    /// </summary>
    private Sprite CreateSquareSprite()
    {
        // Создаём текстуру 32x32 белого цвета
        var texture = new Texture2D(32, 32);
        var pixels = new Color[32 * 32];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white;
        }
        texture.SetPixels(pixels);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32);
    }

    /// <summary>
    /// Спавнит игрока в центре карты
    /// </summary>
    private void SpawnPlayer()
    {
        if (player != null)
        {
            int startX = width / 2;
            int startY = height / 2;
            player.Initialize(this, startX, startY);

            // Настраиваем камеру на игрока
            SetupCamera();
        }
    }

    /// <summary>
    /// Спавнит NPC рядом с игроком
    /// </summary>
    private void SpawnNPC()
    {
        if (npc == null)
        {
            var npcObject = new GameObject("NPC");
            npcObject.transform.SetParent(transform);
            npc = npcObject.AddComponent<NPC>();
        }

        int preferredX = width / 2 - 2;
        int preferredY = height / 2;

        if (!TryFindWalkable(preferredX, preferredY, out int startX, out int startY))
        {
            return;
        }

        npc.Initialize(this, startX, startY);
    }

    /// <summary>
    /// Настраивает камеру следовать за игроком
    /// </summary>
    private void SetupCamera()
    {
        var mainCamera = Camera.main;
        if (mainCamera == null) return;

        // Добавляем CameraFollow если его нет
        var cameraFollow = mainCamera.GetComponent<CameraFollow>();
        if (cameraFollow == null)
        {
            cameraFollow = mainCamera.gameObject.AddComponent<CameraFollow>();
        }

        // Устанавливаем цель и мгновенно перемещаем к ней
        cameraFollow.SetTarget(player.transform);
        cameraFollow.SnapToTarget();
    }

    /// <summary>
    /// Преобразует координаты грида в мировые координаты
    /// </summary>
    public Vector3 GridToWorld(int x, int y)
    {
        // Центрируем грид относительно начала координат
        float worldX = (x - width / 2f + 0.5f) * cellSize;
        float worldY = (y - height / 2f + 0.5f) * cellSize;
        return new Vector3(worldX, worldY, 0);
    }

    /// <summary>
    /// Проверяет, можно ли пройти на указанную клетку
    /// </summary>
    public bool IsWalkable(int x, int y)
    {
        EnsureGridInitialized();

        // Проверяем границы
        if (x < 0 || x >= width || y < 0 || y >= height)
            return false;

        return grid[x, y] == TileType.Floor;
    }

    /// <summary>
    /// Возвращает тип тайла на указанных координатах
    /// </summary>
    public TileType GetTileType(int x, int y)
    {
        EnsureGridInitialized();

        if (x < 0 || x >= width || y < 0 || y >= height)
            return TileType.Wall;

        return grid[x, y];
    }

    private void EnsureGridInitialized()
    {
        if (grid == null)
        {
            InitializeGrid();
        }
    }

    /// <summary>
    /// Ищет ближайшую проходимую клетку
    /// </summary>
    private bool TryFindWalkable(int preferredX, int preferredY, out int x, out int y)
    {
        if (IsWalkable(preferredX, preferredY))
        {
            x = preferredX;
            y = preferredY;
            return true;
        }

        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
            {
                if (grid[i, j] == TileType.Floor)
                {
                    x = i;
                    y = j;
                    return true;
                }
            }
        }

        x = 0;
        y = 0;
        return false;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Рисует превью грида в редакторе
    /// </summary>
    private void OnDrawGizmos()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 pos = GridToWorldEditor(x, y);
                
                // Определяем тип тайла (упрощённая логика как в InitializeGrid)
                bool isWall = x == 0 || x == width - 1 || y == 0 || y == height - 1;
                
                Gizmos.color = isWall ? wallTopColor : floorColor;
                Gizmos.DrawCube(pos, Vector3.one * cellSize * 0.9f);
                
                // Рамка
                Gizmos.color = Color.black;
                Gizmos.DrawWireCube(pos, Vector3.one * cellSize);
            }
        }
        
        // Показываем где будет игрок
        if (player != null)
        {
            Vector3 playerPos = GridToWorldEditor(width / 2, height / 2);
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(playerPos, cellSize * 0.3f);
        }
    }

    /// <summary>
    /// GridToWorld для редактора (без зависимости от runtime данных)
    /// </summary>
    private Vector3 GridToWorldEditor(int x, int y)
    {
        float worldX = (x - width / 2f + 0.5f) * cellSize;
        float worldY = (y - height / 2f + 0.5f) * cellSize;
        return transform.position + new Vector3(worldX, worldY, 0);
    }
#endif
}
