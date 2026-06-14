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
    [SerializeField] private float mapWidth = 200f;
    [SerializeField] private float margin = 12f;

    // Палитра в стиле игры: зелёный фосфор охранного CRT-монитора.
    // Тона взяты из sprite_lib (GREEN / CONCRETE) и console.png / door_metal.png.
    private static readonly Color ScreenColor = new Color(0.055f, 0.118f, 0.078f, 0.94f);  // GREEN d2 — стекло экрана
    private static readonly Color WallColor = new Color(0.086f, 0.165f, 0.118f, 1f);        // тёмный бетон с зеленью
    private static readonly Color FloorColor = new Color(0.172f, 0.376f, 0.243f, 1f);       // фосфорный пол
    private static readonly Color CoverColor = new Color(0.36f, 0.39f, 0.2f, 1f);           // ящик/укрытие — оливковый
    private static readonly Color DoorColor = new Color(0.345f, 0.769f, 0.439f, 1f);        // GREEN hi — дверь-узел
    private static readonly Color VisionColor = new Color(1f, 0.62f, 0.18f, 0.22f);         // янтарный конус обзора
    private static readonly Color ScanlineColor = new Color(0f, 0.02f, 0.01f, 0.28f);       // CRT-развёртка
    private static readonly Color GlowColor = new Color(0.2f, 0.502f, 0.29f, 0.5f);         // GREEN m — свечение рамки
    private static readonly Color BezelColor = new Color(0.118f, 0.133f, 0.125f, 1f);       // корпус консоли
    private static readonly Color BorderColor = new Color(0.345f, 0.769f, 0.439f, 1f);      // фосфорная рамка
    private static readonly Color PlayerColor = new Color(0.59f, 1f, 0.71f, 1f);            // яркий блик «ты»
    private static readonly Color GuardColor = new Color(0.86f, 0.71f, 0.27f, 1f);          // янтарь — патруль
    private static readonly Color ChaseColor = new Color(0.9f, 0.27f, 0.2f, 1f);            // красный — тревога

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

        DrawFrame(mapRect);
        DrawRect(mapRect, ScreenColor);
        DrawTiles(mapRect, cellSize);

        GuardPatrol[] guards = FindObjectsByType<GuardPatrol>(FindObjectsSortMode.None);
        foreach (GuardPatrol guard in guards)
        {
            DrawGuardVision(mapRect, cellSize, guard);
        }

        DrawScanlines(mapRect);

        DrawDot(mapRect, cellSize, player.GridPosition, PlayerColor, 0.86f);
        foreach (GuardPatrol guard in guards)
        {
            Color guardColor = guard.State == GuardState.Chase ? ChaseColor : GuardColor;
            DrawDot(mapRect, cellSize, guard.GridPosition, guardColor, 0.82f);
        }
    }

    // Рамка-безель CRT-монитора: внешнее свечение, тёмный корпус,
    // фосфорный кант и угловые тактические скобки.
    private void DrawFrame(Rect mapRect)
    {
        for (int i = 4; i >= 1; i--)
        {
            Color halo = GlowColor;
            halo.a = GlowColor.a * (0.16f / i);
            DrawRect(new Rect(mapRect.x - i, mapRect.y - i,
                mapRect.width + i * 2, mapRect.height + i * 2), halo);
        }
        DrawRect(new Rect(mapRect.x - 3f, mapRect.y - 3f, mapRect.width + 6f, mapRect.height + 6f), BezelColor);
        DrawRect(new Rect(mapRect.x - 1f, mapRect.y - 1f, mapRect.width + 2f, mapRect.height + 2f), BorderColor);
        DrawCornerBrackets(mapRect);
    }

    private void DrawCornerBrackets(Rect mapRect)
    {
        const float len = 7f;
        const float thick = 2f;
        float x0 = mapRect.x - 3f, y0 = mapRect.y - 3f;
        float x1 = mapRect.xMax + 3f, y1 = mapRect.yMax + 3f;

        DrawRect(new Rect(x0, y0, len, thick), BorderColor);            // верх-лево
        DrawRect(new Rect(x0, y0, thick, len), BorderColor);
        DrawRect(new Rect(x1 - len, y0, len, thick), BorderColor);      // верх-право
        DrawRect(new Rect(x1 - thick, y0, thick, len), BorderColor);
        DrawRect(new Rect(x0, y1 - thick, len, thick), BorderColor);    // низ-лево
        DrawRect(new Rect(x0, y1 - len, thick, len), BorderColor);
        DrawRect(new Rect(x1 - len, y1 - thick, len, thick), BorderColor);  // низ-право
        DrawRect(new Rect(x1 - thick, y1 - len, thick, len), BorderColor);
    }

    private void DrawScanlines(Rect mapRect)
    {
        for (float y = mapRect.y; y < mapRect.yMax; y += 2f)
        {
            DrawRect(new Rect(mapRect.x, y, mapRect.width, 1f), ScanlineColor);
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
    // Размерности мира — из общего источника WorldMetrics (не сериализуем).
    private readonly float cellSize = WorldMetrics.CellSize;

    [Header("Sprites (оставь пустым для авто-генерации)")]
    [SerializeField] private Sprite floorSprite;
    [SerializeField] private Sprite wallTopSprite;
    [SerializeField] private Sprite wallSideSprite;

    [Header("Tile Colors")]
    [SerializeField] private Color floorColor = new Color(0.36f, 0.39f, 0.43f);
    [SerializeField] private Color wallTopColor = new Color(0.25f, 0.28f, 0.33f);
    [SerializeField] private Color wallSideColor = new Color(0.15f, 0.17f, 0.21f);
    [SerializeField] private Color coverColor = new Color(0.38f, 0.28f, 0.18f);

    private readonly float wallHeight = WorldMetrics.WallHeight;

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

        LoadDefaultSprites();
        GenerateVisuals();
        CreateMapContent();
        SpawnPlayer();
        SpawnNPC();
        SpawnGuards();
        CreateMinimap();
    }

    /// <summary>
    /// Если спрайты не заданы в инспекторе, берём пиксель-арт из Resources/Sprites.
    /// </summary>
    private void LoadDefaultSprites()
    {
        if (floorSprite == null) floorSprite = LoadArt("floor_concrete");
        if (wallTopSprite == null) wallTopSprite = LoadArt("wall_top");
        if (wallSideSprite == null) wallSideSprite = LoadArt("wall_side");
    }

    private static Sprite LoadArt(string name) => Resources.Load<Sprite>("Sprites/" + name);

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
            // Пол под стеной отдельным ребёнком — чтобы при cutaway (стена
            // становится прозрачной) под ней был пол, а не пустота фона.
            AddFloorUnder(tile);
            CreateWallVisual(tile, worldPos);
            return tile;
        }

        var renderer = tile.AddComponent<SpriteRenderer>();
        renderer.sprite = floorSprite != null ? floorSprite : CreateSquareSprite();
        renderer.color = floorSprite != null ? Color.white : floorColor;
        renderer.sortingOrder = SortingLayers.Floor;
        tile.transform.localScale = floorSprite != null
            ? Vector3.one * cellSize * WorldMetrics.TileOverlap / GetSpriteSize(floorSprite)
            : Vector3.one * cellSize * WorldMetrics.TileOverlap;

        if (type == TileType.Cover)
        {
            CreateBlockVisual(tile, worldPos, coverColor, "Укрытие");
        }

        return tile;
    }

    // Пол-ребёнок под клеткой (для стен): рисуется на слое Floor, cutaway его
    // не трогает (SetWallAlpha пропускает рендеры на слое Floor).
    private void AddFloorUnder(GameObject parent)
    {
        var floor = new GameObject("Floor");
        floor.transform.SetParent(parent.transform);
        floor.transform.localPosition = Vector3.zero;
        var r = floor.AddComponent<SpriteRenderer>();
        r.sprite = floorSprite != null ? floorSprite : CreateSquareSprite();
        r.color = floorSprite != null ? Color.white : floorColor;
        r.sortingOrder = SortingLayers.Floor;
        floor.transform.localScale = floorSprite != null
            ? Vector3.one * cellSize * WorldMetrics.TileOverlap / GetSpriteSize(floorSprite)
            : Vector3.one * cellSize * WorldMetrics.TileOverlap;
    }

    private void CreateWallVisual(GameObject parent, Vector3 worldPos)
    {
        float baseY = worldPos.y - cellSize * 0.5f;          // низ клетки = пол
        BuildWallColumn(parent.transform, worldPos, baseY, baseY + wallHeight);
    }

    // Строит вертикальную бетонную «колонну» стены (лицевая грань + крышка)
    // между двумя мировыми Y. Используется и для обычных стен (вся высота),
    // и для перемычки над дверью (от верха створки до верха стены).
    private void BuildWallColumn(Transform parent, Vector3 columnWorldPos, float bottomWorldY, float topWorldY)
    {
        float height = topWorldY - bottomWorldY;
        if (height <= 0.001f) return;
        float baseY = columnWorldPos.y - cellSize * 0.5f;    // глубина сортировки = низ клетки

        // Лицевая грань: тайлится по вертикали стопкой сегментов (без растяжения).
        if (wallSideSprite != null)
        {
            float bx = wallSideSprite.bounds.size.x;
            float by = wallSideSprite.bounds.size.y;
            float nativeSegH = cellSize * (by / bx);                 // высота сегмента в нативной пропорции
            int segments = Mathf.Max(1, Mathf.RoundToInt(height / nativeSegH));
            float segH = height / segments;                          // ровно укладываем по высоте
            for (int i = 0; i < segments; i++)
            {
                var seg = new GameObject($"Side_{i}");
                seg.transform.SetParent(parent);
                seg.transform.position = new Vector3(columnWorldPos.x, bottomWorldY + segH * (i + 0.5f), 0f);
                var r = seg.AddComponent<SpriteRenderer>();
                r.sprite = wallSideSprite;
                r.color = Color.white;
                r.sortingOrder = SortingLayers.Wall(baseY) - 1;
                seg.transform.localScale = new Vector3(
                    cellSize * WorldMetrics.TileOverlap / bx, segH / by, 1f);
            }
        }
        else
        {
            var side = new GameObject("Side");
            side.transform.SetParent(parent);
            side.transform.position = new Vector3(columnWorldPos.x, bottomWorldY + height * 0.5f, 0f);
            var r = side.AddComponent<SpriteRenderer>();
            r.sprite = CreateSquareSprite();
            r.color = wallSideColor;
            r.sortingOrder = SortingLayers.Wall(baseY) - 1;
            side.transform.localScale = new Vector3(cellSize * WorldMetrics.TileOverlap, height, 1f);
        }

        // Верхняя «крышка» садится НА верх лицевой грани.
        var top = new GameObject("Top");
        top.transform.SetParent(parent);
        top.transform.position = new Vector3(columnWorldPos.x, topWorldY, 0f);

        var topRenderer = top.AddComponent<SpriteRenderer>();
        topRenderer.sprite = wallTopSprite != null ? wallTopSprite : CreateSquareSprite();
        topRenderer.color = wallTopSprite != null ? Color.white : wallTopColor;
        topRenderer.sortingOrder = SortingLayers.Wall(baseY);
        top.transform.localScale = wallTopSprite != null
            ? Vector3.one * cellSize * WorldMetrics.TileOverlap / GetSpriteSize(wallTopSprite)
            : Vector3.one * cellSize * WorldMetrics.TileOverlap;
    }

    private void CreateBlockVisual(GameObject parent, Vector3 worldPos, Color color, string objectName)
    {
        var block = new GameObject(objectName);
        block.transform.SetParent(parent.transform);

        Sprite crateSprite = LoadArt("crate");
        var renderer = block.AddComponent<SpriteRenderer>();
        if (crateSprite != null)
        {
            block.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            block.transform.localScale = Vector3.one * cellSize * 0.85f / GetSpriteSize(crateSprite);
            renderer.sprite = crateSprite;
            renderer.color = Color.white;
        }
        else
        {
            block.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            block.transform.localScale = new Vector3(cellSize * 0.82f, cellSize * 0.65f, 1f);
            renderer.sprite = CreateSquareSprite();
            renderer.color = color;
        }
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

        CreatePickup(PrisonItemId.Screwdriver, 5, 3);
        CreatePickup(PrisonItemId.KitchenManifest, 29, 22);
        CreatePickup(PrisonItemId.ServiceBadge, 17, 20);
        CreatePickup(PrisonItemId.EyeImplant, 10, 24);
        CreatePickup(PrisonItemId.ExperimentReports, 4, 24);
    }

    private void CreateDoor(string displayName, int x, int y, PrisonItemId requirement)
    {
        var go = new GameObject(displayName);
        go.transform.SetParent(transform);
        var door = go.AddComponent<PrisonDoor>();
        Sprite doorSprite = LoadArt("door_metal");
        door.Initialize(this, x, y, displayName, requirement, doorSprite != null ? doorSprite : CreateSquareSprite());
        doors.Add(door);

        // Бетонная перемычка над дверью: закрывает проём от верха створки до
        // верха стены — иначе дверь пришлось бы растягивать на всю высоту.
        CreateLintel(x, y, door.TopWorldY);
    }

    private void CreateLintel(int x, int y, float doorTopWorldY)
    {
        Vector3 worldPos = GridToWorld(x, y);
        float wallTopY = worldPos.y - cellSize * 0.5f + wallHeight;
        if (wallTopY - doorTopWorldY <= 0.01f) return;

        var lintel = new GameObject($"Lintel_{x}_{y}");
        lintel.transform.SetParent(transform);
        lintel.transform.position = worldPos;
        BuildWallColumn(lintel.transform, worldPos, doorTopWorldY, wallTopY);
    }

    private void CreatePickup(PrisonItemId itemId, int x, int y)
    {
        var go = new GameObject($"Item_{itemId}");
        go.transform.SetParent(transform);
        Item item = itemId switch
        {
            PrisonItemId.Screwdriver => go.AddComponent<ScrewdriverItem>(),
            PrisonItemId.KitchenManifest => go.AddComponent<KitchenManifestItem>(),
            PrisonItemId.ServiceBadge => go.AddComponent<ServiceBadgeItem>(),
            PrisonItemId.EyeImplant => go.AddComponent<EyeImplantItem>(),
            PrisonItemId.ExperimentReports => go.AddComponent<ExperimentReportsItem>(),
            _ => null,
        };
        if (item == null) { Destroy(go); return; }

        string spriteName = itemId switch
        {
            PrisonItemId.Screwdriver => "item_screwdriver",
            PrisonItemId.KitchenManifest => "item_manifest",
            PrisonItemId.ServiceBadge => "item_badge",
            PrisonItemId.EyeImplant => "item_implant",
            PrisonItemId.ExperimentReports => "item_reports",
            _ => null,
        };
        Sprite itemSprite = spriteName != null ? LoadArt(spriteName) : null;
        item.Initialize(this, x, y, itemSprite != null ? itemSprite : CreateSquareSprite(), tintIcon: itemSprite == null);
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
        Sprite guardSprite = LoadArt("guard");
        guard.Initialize(this, route, guardSprite != null ? guardSprite : CreateSquareSprite(), tintSprite: guardSprite == null);
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
        cameraFollow.SetFramePerspective(true);
        cameraFollow.SnapToTarget();
    }

    public Vector3 GridToWorld(int x, int y)
    {
        float worldX = (x - width / 2f + 0.5f) * cellSize;
        float worldY = (y - height / 2f + 0.5f) * cellSize;
        return transform.position + new Vector3(worldX, worldY, 0f);
    }

    // --- Cutaway: затухание стен КОНУСОМ ВНИЗ под персонажем ----------------
    private const float WallFadeAlpha = 0.22f;   // самая прозрачная (у героя)
    private const float CutawayInner = 1.3f;     // радиус полного затухания (клетки)
    private const float CutawayOuter = 3.2f;     // дальше — стены непрозрачны
    private const float CutawayConeCos = 0.6f;   // конус вниз: cos полу-угла (~53°)
    private readonly List<GameObject> fadedWalls = new List<GameObject>();

    /// <summary>
    /// Делает полупрозрачными ТОЛЬКО стены ПОД персонажем (южнее, в конусе вниз)
    /// — именно их высокие грани перекрывают героя. Стены сбоку и сверху не
    /// трогаются. Вблизи — почти прозрачные (WallFadeAlpha), к CutawayOuter
    /// плавно возвращаются к непрозрачности. center — мировая позиция героя.
    /// </summary>
    public void UpdateWallCutaway(Vector3 center)
    {
        if (tileObjects == null) return;
        for (int i = 0; i < fadedWalls.Count; i++)
        {
            if (fadedWalls[i] != null) SetWallAlpha(fadedWalls[i], 1f);
        }
        fadedWalls.Clear();

        int px = Mathf.RoundToInt((center.x - transform.position.x) / cellSize + width / 2f - 0.5f);
        int py = Mathf.RoundToInt((center.y - transform.position.y) / cellSize + height / 2f - 0.5f);
        int r = Mathf.CeilToInt(CutawayOuter) + 1;
        var c2 = new Vector2(center.x, center.y);

        for (int dx = -r; dx <= r; dx++)
        for (int dy = -r; dy <= r; dy++)
        {
            int wx = px + dx, wy = py + dy;
            if (wx < 0 || wx >= width || wy < 0 || wy >= height) continue;
            if (grid[wx, wy] != TileType.Wall) continue;
            GameObject wall = tileObjects[wx, wy];
            if (wall == null) continue;

            Vector3 wc = GridToWorld(wx, wy);
            Vector2 dir = new Vector2(wc.x, wc.y) - c2;
            float d = dir.magnitude;
            if (d >= CutawayOuter) continue;
            // только конус вниз (юг): -dir.y/d = «насколько вниз» (1 = ровно вниз)
            if (d > 0.01f && (-dir.y / d) < CutawayConeCos) continue;
            float t = Mathf.Clamp01((d - CutawayInner) / (CutawayOuter - CutawayInner));
            float a = Mathf.Lerp(WallFadeAlpha, 1f, t);
            if (a >= 0.999f) continue;
            SetWallAlpha(wall, a);
            fadedWalls.Add(wall);
        }
    }

    private static void SetWallAlpha(GameObject wall, float a)
    {
        var renderers = wall.GetComponentsInChildren<SpriteRenderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].sortingOrder == SortingLayers.Floor) continue; // пол под стеной не трогаем
            Color c = renderers[i].color;
            c.a = a;
            renderers[i].color = c;
        }
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
