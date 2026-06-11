using System.Collections.Generic;
using UnityEngine;

public enum TileType
{
    Floor,
    Wall,
    Cover,
    Door
}

public class PrisonMinimap : MonoBehaviour
{
    [SerializeField] private float mapWidth = 220f;
    [SerializeField] private float margin = 16f;

    private static readonly Color BackgroundColor = new Color(0.025f, 0.04f, 0.055f, 0.92f);
    private static readonly Color WallColor = new Color(0.12f, 0.16f, 0.2f, 1f);
    private static readonly Color FloorColor = new Color(0.3f, 0.36f, 0.42f, 1f);
    private static readonly Color CoverColor = new Color(0.42f, 0.3f, 0.18f, 1f);
    private static readonly Color DoorColor = new Color(0.55f, 0.24f, 0.2f, 1f);
    private static readonly Color VisionColor = new Color(1f, 0.92f, 0.35f, 0.28f);
    private static readonly Color BorderColor = new Color(0.25f, 0.9f, 0.95f, 1f);
    private static readonly Color PlayerColor = new Color(0.2f, 0.85f, 1f, 1f);

    private GameGrid grid;
    private Player player;
    private Texture2D circleTexture;

    public void Initialize(GameGrid gameGrid, Player trackedPlayer)
    {
        grid = gameGrid;
        player = trackedPlayer;
        circleTexture = CreateCircleTexture();
    }

    private void OnGUI()
    {
        if (grid == null || player == null) return;

        float cellSize = mapWidth / grid.Width;
        float mapHeight = cellSize * grid.Height;
        Rect mapRect = new Rect(Screen.width - mapWidth - margin, margin, mapWidth, mapHeight);

        DrawRect(new Rect(mapRect.x - 4f, mapRect.y - 4f, mapRect.width + 8f, mapRect.height + 8f), BorderColor);
        DrawRect(mapRect, BackgroundColor);
        DrawTiles(mapRect, cellSize);

        GuardPatrol[] guards = FindObjectsByType<GuardPatrol>(FindObjectsSortMode.None);
        foreach (GuardPatrol guard in guards)
        {
            DrawGuardVision(mapRect, cellSize, guard);
        }

        DrawDot(mapRect, cellSize, player.GridPosition, PlayerColor, 0.86f);
        foreach (GuardPatrol guard in guards)
        {
            Color guardColor = guard.State == GuardState.Chase ? Color.red : Color.white;
            DrawDot(mapRect, cellSize, guard.GridPosition, guardColor, 0.82f);
        }
    }

    private void DrawTiles(Rect mapRect, float cellSize)
    {
        for (int x = 0; x < grid.Width; x++)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                TileType type = grid.GetTileType(x, y);
                Color color = type switch
                {
                    TileType.Floor => FloorColor,
                    TileType.Cover => CoverColor,
                    TileType.Door => DoorColor,
                    _ => WallColor
                };

                DrawRect(CellRect(mapRect, cellSize, new Vector2Int(x, y)), color);
            }
        }
    }

    private void DrawGuardVision(Rect mapRect, float cellSize, GuardPatrol guard)
    {
        if (guard.State == GuardState.Disabled) return;

        Vector2Int origin = guard.GridPosition;
        int range = guard.VisionRange;
        for (int x = origin.x - range; x <= origin.x + range; x++)
        {
            for (int y = origin.y - range; y <= origin.y + range; y++)
            {
                var cell = new Vector2Int(x, y);
                if (guard.CanSeeCell(cell))
                {
                    DrawRect(CellRect(mapRect, cellSize, cell), VisionColor);
                }
            }
        }
    }

    private void DrawDot(Rect mapRect, float cellSize, Vector2Int cell, Color color, float scale)
    {
        Rect cellRect = CellRect(mapRect, cellSize, cell);
        float size = cellSize * scale;
        var dotRect = new Rect(
            cellRect.center.x - size * 0.5f,
            cellRect.center.y - size * 0.5f,
            size,
            size
        );

        Color previousColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(dotRect, circleTexture);
        GUI.color = previousColor;
    }

    private Rect CellRect(Rect mapRect, float cellSize, Vector2Int cell)
    {
        return new Rect(
            mapRect.x + cell.x * cellSize,
            mapRect.y + (grid.Height - cell.y - 1) * cellSize,
            cellSize + 0.25f,
            cellSize + 0.25f
        );
    }

    private static void DrawRect(Rect rect, Color color)
    {
        Color previousColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previousColor;
    }

    private static Texture2D CreateCircleTexture()
    {
        const int size = 32;
        var texture = new Texture2D(size, size);
        var pixels = new Color[size * size];
        Vector2 center = Vector2.one * (size - 1) * 0.5f;
        float radius = size * 0.46f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                pixels[y * size + x] = Vector2.Distance(new Vector2(x, y), center) <= radius
                    ? Color.white
                    : Color.clear;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        texture.filterMode = FilterMode.Bilinear;
        return texture;
    }
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
        CreateMinimap();
    }

    private void InitializeGrid()
    {
        width = Mathf.Max(width, 44);
        height = Mathf.Max(height, 30);
        grid = new TileType[width, height];
        tileObjects = new GameObject[width, height];

        Fill(TileType.Wall);

        // Rooms are carved only inside their wall outlines. Connections are added explicitly below.
        CarveRoom(10, 2, 26, 12);     // Common area.
        CarveRoom(28, 6, 33, 10);     // Toilet.
        CarveRoom(3, 2, 6, 3);        // Separate solitary cells.
        CarveRoom(3, 5, 6, 6);
        CarveRoom(3, 8, 6, 9);
        CarveRoom(3, 11, 6, 12);

        CarveRoom(35, 10, 36, 14);    // Ventilation to staff corridor.
        CarveRoom(21, 16, 36, 18);    // Staff corridor.
        CarveRoom(38, 14, 42, 19);    // Kitchen.
        CarveRoom(27, 20, 31, 24);    // Staff room.
        CarveRoom(14, 16, 19, 22);    // Storage: mandatory transition.
        CarveRoom(2, 16, 12, 18);     // Secure corridor.
        CarveRoom(2, 20, 6, 26);      // Laboratory.
        CarveRoom(8, 20, 12, 26);     // Engineering.

        // Covers create observation points without opening extra routes.
        AddCover(15, 5);
        AddCover(20, 5);
        AddCover(25, 9);
        AddCover(15, 11);
        AddCover(32, 8);
        AddCover(39, 16);
        AddCover(40, 18);
        AddCover(34, 16);             // Safe observation point left of the ventilation exit.
        AddCover(25, 16);
        AddCover(30, 18);
        AddCover(17, 19);
        AddCover(17, 20);
        AddCover(5, 18);
        AddCover(5, 24);
        AddCover(10, 24);

        // Explicit connections from the marked openings in the reference map.
        CarvePassage(7, 2, 9, 2);
        CarvePassage(7, 5, 9, 5);
        CarvePassage(7, 8, 9, 8);
        CarvePassage(7, 11, 9, 11);
        AddDoorTile(7, 2);             // Cell doors into common area.
        AddDoorTile(7, 5);
        AddDoorTile(7, 8);
        AddDoorTile(7, 11);

        AddDoorTile(27, 8);            // Common area to toilet.
        AddDoorTile(34, 10);           // Toilet to ventilation.
        AddDoorTile(35, 15);           // Ventilation to staff corridor, no kitchen connection.

        // Staff and secure wing doors.
        AddDoorTile(37, 17);           // Kitchen to staff corridor: the only opening.
        AddDoorTile(29, 19);           // Staff room.
        AddDoorTile(20, 17);           // Staff corridor to storage.
        AddDoorTile(13, 17);           // Storage to secure corridor.
        AddDoorTile(4, 19);            // Laboratory, on its outer wall.
        AddDoorTile(10, 19);           // Engineering, on its outer wall.
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

    private void CarvePassage(int startX, int startY, int endX, int endY)
    {
        int minX = Mathf.Min(startX, endX);
        int maxX = Mathf.Max(startX, endX);
        int minY = Mathf.Min(startY, endY);
        int maxY = Mathf.Max(startY, endY);
        CarveRoom(minX, minY, maxX, maxY);
    }

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
        CreateDoor("Камера 1", 7, 2, PrisonItemId.None);
        CreateDoor("Камера 2", 7, 5, PrisonItemId.None);
        CreateDoor("Камера 3", 7, 8, PrisonItemId.None);
        CreateDoor("Камера 4", 7, 11, PrisonItemId.None);
        CreateDoor("Туалет", 27, 8, PrisonItemId.None);
        CreateDoor("Вентиляционная решётка", 34, 10, PrisonItemId.Screwdriver);
        CreateDoor("Выход вентиляции", 35, 15, PrisonItemId.None);
        CreateDoor("Дверь кухни", 37, 17, PrisonItemId.None);
        CreateDoor("Комната персонала", 29, 19, PrisonItemId.None);
        CreateDoor("Склад", 20, 17, PrisonItemId.KitchenManifest);
        CreateDoor("Выход из склада в защищённый коридор", 13, 17, PrisonItemId.ServiceBadge);
        CreateDoor("Лаборатория", 4, 19, PrisonItemId.Unavailable);
        CreateDoor("Инженерная зона", 10, 19, PrisonItemId.ServiceBadge);

        CreatePickup("Самодельная отвёртка", PrisonItemId.Screwdriver, 5, 3, new Color(0.7f, 0.75f, 0.8f));
        CreatePickup("Копия листа приёмки кухни", PrisonItemId.KitchenManifest, 29, 22, new Color(0.95f, 0.9f, 0.55f));
        CreatePickup("Служебный пропуск", PrisonItemId.ServiceBadge, 17, 20, new Color(0.35f, 0.8f, 0.95f));
        CreatePickup("Глазной имплант", PrisonItemId.EyeImplant, 10, 24, new Color(0.45f, 0.95f, 1f));
        CreatePickup("Отчёты прошлых экспериментов", PrisonItemId.ExperimentReports, 4, 24, new Color(0.9f, 0.45f, 0.45f));
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
        player.Initialize(this, 5, 2);
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

        npc.Initialize(this, 19, 12);
    }

    private void SpawnGuards()
    {
        CreateGuard("Надзиратель служебного коридора", new[]
        {
            new Vector2Int(22, 17), new Vector2Int(34, 17)
        });

        CreateGuard("Надзиратель защищённого коридора", new[]
        {
            new Vector2Int(3, 17), new Vector2Int(11, 17)
        });
    }

    private void CreateGuard(string displayName, Vector2Int[] route)
    {
        var go = new GameObject(displayName);
        go.transform.SetParent(transform);
        var guard = go.AddComponent<GuardPatrol>();
        guard.Initialize(this, route, CreateSquareSprite());
    }

    private void CreateMinimap()
    {
        var minimapObject = new GameObject("Prison Minimap");
        minimapObject.transform.SetParent(transform);
        minimapObject.AddComponent<PrisonMinimap>().Initialize(this, player);
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
