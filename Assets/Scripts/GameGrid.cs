using System.Collections.Generic;
using UnityEngine;

public enum TileType
{
    Floor,
    Wall,
    Cover,
    Door
}

public class GameGrid : MonoBehaviour
{
    [Header("Grid Settings")]
    [SerializeField] private int width = 44;
    [SerializeField] private int height = 30;
    [SerializeField] private float cellSize = 1f;

    [Header("Sprites (оставь пустым для авто-генерации)")]
    [SerializeField] private Sprite floorSprite;
    [SerializeField] private Sprite wallTopSprite;
    [SerializeField] private Sprite wallSideSprite;

    [Header("Tile Colors")]
    [SerializeField] private Color floorColor = new Color(0.36f, 0.39f, 0.43f);
    [SerializeField] private Color wallTopColor = new Color(0.25f, 0.28f, 0.33f);
    [SerializeField] private Color wallSideColor = new Color(0.15f, 0.17f, 0.21f);
    [SerializeField] private Color coverColor = new Color(0.38f, 0.28f, 0.18f);

    [Header("Wall Settings")]
    [SerializeField] private float wallHeight = 0.6f;

    [Header("References")]
    [SerializeField] private Player player;
    [SerializeField] private NPC npc;

    private TileType[,] grid;
    private GameObject[,] tileObjects;
    private Sprite generatedSquareSprite;
    private readonly List<PrisonDoor> doors = new List<PrisonDoor>();

    public int Width => width;
    public int Height => height;
    public float CellSize => cellSize;

    private void Awake()
    {
        InitializeGrid();

        // EditMode tests only need the logical grid.
        if (!Application.isPlaying) return;

        GenerateVisuals();
        CreateMapContent();
        SpawnPlayer();
        SpawnNPC();
        SpawnGuards();
    }

    private void InitializeGrid()
    {
        width = Mathf.Max(width, 44);
        height = Mathf.Max(height, 30);
        grid = new TileType[width, height];
        tileObjects = new GameObject[width, height];

        Fill(TileType.Wall);

        // Public block.
        CarveRoom(1, 1, 2, 12);       // Cells and cell corridor.
        CarveRoom(3, 2, 18, 12);      // Common area.
        CarveRoom(7, 13, 13, 16);     // Experiment entrance.
        CarveRoom(19, 6, 23, 10);     // Toilet.

        // Vent and staff block.
        CarveRoom(22, 11, 23, 15);    // Vent route.
        CarveRoom(15, 15, 32, 17);    // Staff corridor.
        CarveRoom(34, 12, 42, 18);    // Kitchen.
        CarveRoom(20, 19, 24, 23);    // Storage.
        CarveRoom(26, 19, 30, 23);    // Staff room.

        // High-security wing.
        CarveRoom(2, 14, 8, 20);      // Laboratory.
        CarveRoom(10, 15, 14, 17);    // Laboratory approach.
        CarveRoom(10, 17, 12, 22);    // Engineering approach.
        CarveRoom(2, 24, 12, 28);     // Engineering.
        CarveRoom(14, 20, 18, 24);    // Empty room.
        CarveRoom(16, 18, 16, 19);    // Empty-room approach.

        // Guaranteed test and public spawn cells.
        SetTile(1, 1, TileType.Floor);
        SetTile(2, 2, TileType.Floor);
        SetTile(width / 2, height / 2, TileType.Floor);

        AddCover(6, 6);
        AddCover(10, 6);
        AddCover(14, 9);
        AddCover(17, 4);
        AddCover(18, 8);
        AddCover(20, 16);
        AddCover(27, 16);
        AddCover(31, 16);
        AddCover(36, 14);
        AddCover(39, 16);
        AddCover(21, 21);
        AddCover(23, 21);
        AddCover(5, 17);
        AddCover(5, 25);
        AddCover(8, 26);

        AddDoorTile(22, 11);
        AddDoorTile(14, 16);
        AddDoorTile(33, 16);
        AddDoorTile(22, 18);
        AddDoorTile(28, 18);
        AddDoorTile(11, 23);
        AddDoorTile(9, 16);
        AddDoorTile(16, 19);
    }

    private void Fill(TileType type)
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                grid[x, y] = type;
            }
        }
    }

    private void CarveRoom(int minX, int minY, int maxX, int maxY)
    {
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                SetTile(x, y, TileType.Floor);
            }
        }
    }

    private void AddCover(int x, int y) => SetTile(x, y, TileType.Cover);
    private void AddDoorTile(int x, int y) => SetTile(x, y, TileType.Door);

    public void SetTile(int x, int y, TileType type)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return;
        grid[x, y] = type;
    }

    private void GenerateVisuals()
    {
        var tilesParent = new GameObject("Tiles");
        tilesParent.transform.SetParent(transform);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                GameObject tile = CreateTileVisual(x, y, grid[x, y]);
                tile.transform.SetParent(tilesParent.transform);
                tileObjects[x, y] = tile;
            }
        }
    }

    private GameObject CreateTileVisual(int x, int y, TileType type)
    {
        var tile = new GameObject($"Tile_{x}_{y}_{type}");
        Vector3 worldPos = GridToWorld(x, y);
        tile.transform.position = worldPos;

        if (type == TileType.Wall)
        {
            CreateWallVisual(tile, worldPos);
            return tile;
        }

        var renderer = tile.AddComponent<SpriteRenderer>();
        renderer.sprite = floorSprite != null ? floorSprite : CreateSquareSprite();
        renderer.color = floorSprite != null ? Color.white : floorColor;
        renderer.sortingOrder = SortingLayers.Floor;
        tile.transform.localScale = floorSprite != null
            ? Vector3.one * cellSize / GetSpriteSize(floorSprite)
            : Vector3.one * cellSize * 0.98f;

        if (type == TileType.Cover)
        {
            CreateBlockVisual(tile, worldPos, coverColor, "Укрытие");
        }

        return tile;
    }

    private void CreateWallVisual(GameObject parent, Vector3 worldPos)
    {
        var top = new GameObject("Top");
        top.transform.SetParent(parent.transform);
        top.transform.localPosition = new Vector3(0f, wallHeight * 0.5f, 0f);

        var topRenderer = top.AddComponent<SpriteRenderer>();
        topRenderer.sprite = wallTopSprite != null ? wallTopSprite : CreateSquareSprite();
        topRenderer.color = wallTopSprite != null ? Color.white : wallTopColor;
        float baseY = worldPos.y - cellSize * 0.5f;
        topRenderer.sortingOrder = SortingLayers.Wall(baseY);
        top.transform.localScale = wallTopSprite != null
            ? Vector3.one * cellSize / GetSpriteSize(wallTopSprite)
            : Vector3.one * cellSize * 0.98f;

        var side = new GameObject("Side");
        side.transform.SetParent(parent.transform);
        side.transform.localPosition = new Vector3(0f, -cellSize * 0.5f + wallHeight * 0.5f, 0f);

        var sideRenderer = side.AddComponent<SpriteRenderer>();
        sideRenderer.sprite = wallSideSprite != null ? wallSideSprite : CreateSquareSprite();
        sideRenderer.color = wallSideSprite != null ? Color.white : wallSideColor;
        sideRenderer.sortingOrder = SortingLayers.Wall(baseY) - 1;
        side.transform.localScale = wallSideSprite != null
            ? new Vector3(cellSize / wallSideSprite.bounds.size.x, wallHeight / wallSideSprite.bounds.size.y, 1f)
            : new Vector3(cellSize * 0.98f, wallHeight, 1f);
    }

    private void CreateBlockVisual(GameObject parent, Vector3 worldPos, Color color, string objectName)
    {
        var block = new GameObject(objectName);
        block.transform.SetParent(parent.transform);
        block.transform.localPosition = new Vector3(0f, 0.25f, 0f);
        block.transform.localScale = new Vector3(cellSize * 0.82f, cellSize * 0.65f, 1f);

        var renderer = block.AddComponent<SpriteRenderer>();
        renderer.sprite = CreateSquareSprite();
        renderer.color = color;
        renderer.sortingOrder = SortingLayers.Wall(worldPos.y - cellSize * 0.5f);
    }

    private void CreateMapContent()
    {
        CreateDoor("Вентиляционная решётка", 22, 11, PrisonItemId.Screwdriver);
        CreateDoor("Короткий путь в общий блок", 14, 16, PrisonItemId.ServiceBadge);
        CreateDoor("Дверь кухни", 33, 16, PrisonItemId.ServiceBadge);
        CreateDoor("Кодовый замок склада", 22, 18, PrisonItemId.KitchenManifest);
        CreateDoor("Комната персонала", 28, 18, PrisonItemId.None);
        CreateDoor("Инженерная зона", 11, 23, PrisonItemId.ServiceBadge);
        CreateDoor("Лаборатория: доступ высокого уровня", 9, 16, PrisonItemId.Unavailable);
        CreateDoor("Пустая комната", 16, 19, PrisonItemId.EyeImplant);

        CreatePickup("Самодельная отвёртка", PrisonItemId.Screwdriver, 5, 4, new Color(0.7f, 0.75f, 0.8f));
        CreatePickup("Копия листа приёмки кухни", PrisonItemId.KitchenManifest, 29, 22, new Color(0.95f, 0.9f, 0.55f));
        CreatePickup("Служебный пропуск", PrisonItemId.ServiceBadge, 23, 22, new Color(0.35f, 0.8f, 0.95f));
        CreatePickup("Глазной имплант", PrisonItemId.EyeImplant, 4, 27, new Color(0.45f, 0.95f, 1f));
        CreatePickup("Отчёты прошлых экспериментов", PrisonItemId.ExperimentReports, 7, 19, new Color(0.9f, 0.45f, 0.45f));

        CreateLabel("КАМЕРЫ", 1, 7);
        CreateLabel("ОБЩАЯ ЗОНА", 11, 8);
        CreateLabel("ВХОД В ЭКСПЕРИМЕНТЫ", 10, 15);
        CreateLabel("ТУАЛЕТ", 21, 8);
        CreateLabel("СЛУЖЕБНЫЙ КОРИДОР", 25, 16);
        CreateLabel("КУХНЯ", 38, 17);
        CreateLabel("СКЛАД", 22, 20);
        CreateLabel("КОМНАТА ПЕРСОНАЛА", 28, 21);
        CreateLabel("ЛАБОРАТОРИЯ", 5, 19);
        CreateLabel("ИНЖЕНЕРНАЯ ЗОНА", 7, 27);
        CreateLabel("ПУСТАЯ КОМНАТА", 16, 22);
    }

    private void CreateDoor(string displayName, int x, int y, PrisonItemId requirement)
    {
        var go = new GameObject(displayName);
        go.transform.SetParent(transform);
        var door = go.AddComponent<PrisonDoor>();
        door.Initialize(this, x, y, displayName, requirement, CreateSquareSprite());
        doors.Add(door);
    }

    private void CreatePickup(string displayName, PrisonItemId itemId, int x, int y, Color color)
    {
        var go = new GameObject(displayName);
        go.transform.SetParent(transform);
        var pickup = go.AddComponent<PrisonItemPickup>();
        pickup.Initialize(this, x, y, itemId, displayName, color, CreateSquareSprite());
    }

    private void CreateLabel(string text, int x, int y)
    {
        var go = new GameObject($"Label_{text}");
        go.transform.SetParent(transform);
        go.transform.position = GridToWorld(x, y) + new Vector3(0f, 0f, -0.1f);

        var label = go.AddComponent<TextMesh>();
        label.text = text;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 28;
        label.characterSize = 0.055f;
        label.anchor = TextAnchor.MiddleCenter;
        label.color = new Color(0.75f, 0.8f, 0.85f, 0.8f);
        label.GetComponent<MeshRenderer>().sortingOrder = SortingLayers.Floor + 5;
    }

    private float GetSpriteSize(Sprite sprite) => Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);

    public Sprite CreateSquareSprite()
    {
        if (generatedSquareSprite != null) return generatedSquareSprite;

        var texture = new Texture2D(2, 2);
        texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        texture.Apply();
        texture.filterMode = FilterMode.Point;
        generatedSquareSprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2);
        return generatedSquareSprite;
    }

    private void SpawnPlayer()
    {
        if (player == null) return;
        player.Initialize(this, 10, 8);
        SetupCamera();
    }

    private void SpawnNPC()
    {
        if (npc == null)
        {
            var npcObject = new GameObject("Experiment Entrance NPC");
            npcObject.transform.SetParent(transform);
            npc = npcObject.AddComponent<NPC>();
        }

        npc.Initialize(this, 10, 14);
    }

    private void SpawnGuards()
    {
        CreateGuard("Надзиратель служебного коридора", new[]
        {
            new Vector2Int(16, 16), new Vector2Int(21, 16), new Vector2Int(26, 16),
            new Vector2Int(31, 16), new Vector2Int(32, 16), new Vector2Int(31, 16),
            new Vector2Int(26, 16), new Vector2Int(21, 16)
        });

        CreateGuard("Надзиратель инженерного крыла", new[]
        {
            new Vector2Int(10, 17), new Vector2Int(11, 19), new Vector2Int(10, 21),
            new Vector2Int(11, 19)
        });
    }

    private void CreateGuard(string displayName, Vector2Int[] route)
    {
        var go = new GameObject(displayName);
        go.transform.SetParent(transform);
        var guard = go.AddComponent<GuardPatrol>();
        guard.Initialize(this, route, CreateSquareSprite());
    }

    private void SetupCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null) return;

        var cameraFollow = mainCamera.GetComponent<CameraFollow>();
        if (cameraFollow == null)
        {
            cameraFollow = mainCamera.gameObject.AddComponent<CameraFollow>();
        }

        cameraFollow.SetTarget(player.transform);
        cameraFollow.SnapToTarget();
    }

    public Vector3 GridToWorld(int x, int y)
    {
        float worldX = (x - width / 2f + 0.5f) * cellSize;
        float worldY = (y - height / 2f + 0.5f) * cellSize;
        return transform.position + new Vector3(worldX, worldY, 0f);
    }

    public bool IsWalkable(int x, int y)
    {
        EnsureGridInitialized();
        if (x < 0 || x >= width || y < 0 || y >= height) return false;
        return grid[x, y] == TileType.Floor;
    }

    public bool BlocksVision(int x, int y)
    {
        TileType type = GetTileType(x, y);
        return type == TileType.Wall || type == TileType.Cover || type == TileType.Door;
    }

    public TileType GetTileType(int x, int y)
    {
        EnsureGridInitialized();
        if (x < 0 || x >= width || y < 0 || y >= height) return TileType.Wall;
        return grid[x, y];
    }

    public void SetDoorOpen(int x, int y, bool isOpen)
    {
        SetTile(x, y, isOpen ? TileType.Floor : TileType.Door);
    }

    private void EnsureGridInitialized()
    {
        if (grid == null) InitializeGrid();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        int previewWidth = Mathf.Max(width, 44);
        int previewHeight = Mathf.Max(height, 30);
        Gizmos.color = Color.gray;
        Gizmos.DrawWireCube(transform.position, new Vector3(previewWidth * cellSize, previewHeight * cellSize, 0.1f));
    }
#endif
}
