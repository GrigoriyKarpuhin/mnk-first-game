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
    public static readonly Vector2Int PlayerStartCell = BlockCPlayableLayout.PlayerStart;

    [Header("Grid Settings")]
    [SerializeField] private int width = 44;
    [SerializeField] private int height = 30;
    // Размерности мира — из общего источника WorldMetrics (не сериализуем).
    private readonly float cellSize = WorldMetrics.CellSize;

    [Header("Sprites (оставь пустым для авто-генерации)")]
    [SerializeField] private Sprite floorSprite;
    [SerializeField] private Sprite wallTopSprite;

    [Header("Tile Colors")]
    [SerializeField] private Color floorColor = new Color(0.36f, 0.39f, 0.43f);
    [SerializeField] private Color wallTopColor = new Color(0.25f, 0.28f, 0.33f);
    [SerializeField] private Color coverColor = new Color(0.38f, 0.28f, 0.18f);

    [Header("References")]
    [SerializeField] private Player player;
    [SerializeField] private NPC npc;

    private TileType[,] grid;
    private GameObject[,] tileObjects;
    private Sprite generatedSquareSprite;
    // Зона каждой клетки-пола (для зональных тилсетов) + кэш загруженных спрайтов.
    private ZoneTiles.Zone[,] zoneGrid;
    private readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
    // Клетки с «телесными» пропами (мебель/сантехника): сетка остаётся Floor
    // (комнаты/зоны/валидация не меняются), но ходить сквозь них нельзя.
    private readonly HashSet<Vector2Int> solidProps = new HashSet<Vector2Int>();

    // Пропы, которые идейно НЕ должны быть проходимы. Настенный/напольный декор
    // (плакаты, трафареты, решётки, трубы, лампы, окна, лейки душа) остаётся проходимым.
    private static readonly HashSet<string> SolidPropSprites = new HashSet<string>
    {
        "bed", "toilet", "sink", "desk", "locker", "stool", "table_canteen",
    };
    private readonly List<PrisonDoor> doors = new List<PrisonDoor>();
    // Завершаемые задачи комнат для карты (головоломки, сканер, папка, замок-шорткат).
    // Пикапы сюда НЕ кладём — они само-уничтожаются, живой Item = несобранная задача.
    private readonly List<IRoomObjective> roomObjectives = new List<IRoomObjective>();
    // Граф комнат: строится один раз при закрытых дверях (см. BuildZoneMap), кэшируется,
    // в течение забега не пересобирается — иначе открытие двери слило бы комнаты и сдвинуло id.
    private RoomGraph roomGraph;
    // Клетки-укрытия (шкафчики/вентиляция): зашёл — становишься невидимым для охраны.
    private readonly HashSet<Vector2Int> hideSpots = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> reactiveCameraCells = new HashSet<Vector2Int>();
    // Ручные закрытые зоны (RestrictedZoneMarker). Если есть хоть одна — заменяют базовые.
    private readonly List<RectInt> extraRestricted = new List<RectInt>();
    private bool hasRestrictedMarkers;
    private PrisonDoor staffRoomDoor;
    private PrisonDoor gardenDoor;
    private PrisonDoor techWingDoor;
    private PrisonDoor kitchenShortcutDoor;
    private PrisonDoor competitorServiceDoor;
    private bool staffRoomMeetingStarted;
    private bool staffRoomMeetingGuardSpawned;

    public int Width => width;
    public int Height => height;
    public float CellSize => cellSize;

    /// <summary>Кэшированный граф комнат (стабильные id за забег). Ленивая сборка безопасна:
    /// GetTileType лениво инициализирует грид, а в рантайме граф уже собран в BuildZoneMap.</summary>
    public RoomGraph RoomGraph => roomGraph ??= RoomGraph.Build(this);

    /// <summary>Все двери карты (для отрисовки состояния/требований на карте).</summary>
    public IReadOnlyList<PrisonDoor> Doors => doors;

    /// <summary>Завершаемые задачи комнат (для статуса «зачищено» на карте).</summary>
    public IReadOnlyList<IRoomObjective> RoomObjectives => roomObjectives;

    private void Awake()
    {
        InitializeGrid();

        // EditMode tests only need the logical grid.
        if (!Application.isPlaying) return;

        ApplyObstacleMarkers(); // ручные препятствия (Cover) до генерации визуала
        CollectRestrictedZones(); // ручные закрытые зоны (RestrictedZoneMarker)
        LoadDefaultSprites();
        BuildZoneMap();
        GenerateVisuals();
        CreateMapContent();
        SpawnPlayer();
        SpawnNPC();
        SpawnProgrammer();
        SpawnCompetitor();
        SpawnMedicMechanic();
        SpawnSecondFloorInmates();
        SpawnGuards();
        SpawnCameras();
        SpawnActiveReactiveCameras();
        ApplyPendingSecurityIncidents(showMessage: false);
        SpawnHideSpots();
        CreateDayDirector();
    }

    /// <summary>
    /// Если спрайты не заданы в инспекторе, берём пиксель-арт из Resources/Sprites.
    /// </summary>
    private void LoadDefaultSprites()
    {
        if (floorSprite == null) floorSprite = LoadArt("floor_concrete");
        if (wallTopSprite == null) wallTopSprite = LoadArt("wall_top");
    }

    private static Sprite LoadArt(string name) => Resources.Load<Sprite>("Sprites/" + name);

    // --- Зональные тилсеты -------------------------------------------------

    /// <summary>Построить карту зон по клеткам-полам из графа комнат (имя комнаты -> зона).</summary>
    private void BuildZoneMap()
    {
        zoneGrid = new ZoneTiles.Zone[width, height]; // дефолт = Common (enum 0)
        // Строим граф при закрытых дверях (до CreateMapContent) и кэшируем — id канонические.
        roomGraph ??= RoomGraph.Build(this);
        RoomGraph graph = roomGraph;
        var nameById = LayoutValidator.NameByComponent(graph);
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int comp = graph.ComponentAt(new Vector2Int(x, y));
                zoneGrid[x, y] = comp >= 0 && nameById.TryGetValue(comp, out string name)
                    ? ZoneTiles.Classify(name)
                    : ZoneTiles.Zone.Common;
            }
        }
    }

    private Sprite CachedArt(string name)
    {
        if (spriteCache.TryGetValue(name, out Sprite cached)) return cached;
        Sprite loaded = LoadArt(name);
        spriteCache[name] = loaded;
        return loaded;
    }

    private Sprite GetFloorSprite(int x, int y)
    {
        ZoneTiles.Zone z = zoneGrid != null ? zoneGrid[x, y] : ZoneTiles.Zone.Common;
        int v = ZoneTiles.PickVariant(x, y, ZoneTiles.VariantCount);
        return CachedArt(ZoneTiles.FloorSpriteName(z, v)) ?? floorSprite;
    }

    private Sprite GetWallSprite(int x, int y)
    {
        ZoneTiles.Zone z = GetWallZone(x, y);
        int v = ZoneTiles.PickVariant(x, y, ZoneTiles.VariantCount);
        return CachedArt(ZoneTiles.WallSpriteName(z, v)) ?? wallTopSprite;
    }

    /// <summary>Зона клетки-стены = зона соседней клетки-пола с макс. приоритетом.</summary>
    private ZoneTiles.Zone GetWallZone(int x, int y)
    {
        if (zoneGrid == null) return ZoneTiles.Zone.Common;
        ZoneTiles.Zone best = ZoneTiles.Zone.Common;
        int bestPri = -1;
        CheckNeighborZone(x + 1, y, ref best, ref bestPri);
        CheckNeighborZone(x - 1, y, ref best, ref bestPri);
        CheckNeighborZone(x, y + 1, ref best, ref bestPri);
        CheckNeighborZone(x, y - 1, ref best, ref bestPri);
        return best;
    }

    private void CheckNeighborZone(int x, int y, ref ZoneTiles.Zone best, ref int bestPri)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return;
        if (grid[x, y] != TileType.Floor && grid[x, y] != TileType.Cover) return;
        ZoneTiles.Zone z = zoneGrid[x, y];
        int pri = ZoneTiles.Priority(z);
        if (pri > bestPri) { bestPri = pri; best = z; }
    }


    private void InitializeGrid()
    {
        width = BlockCPlayableLayout.Width;
        height = BlockCPlayableLayout.Height;
        grid = new TileType[width, height];
        tileObjects = new GameObject[width, height];

        Fill(TileType.Wall);

        foreach (GridArea area in BlockCPlayableLayout.FloorAreas)
        {
            CarveRoom(area.MinX, area.MinY, area.MaxX, area.MaxY);
        }

        foreach (GridArea area in BlockCPlayableLayout.VoidAreas)
        {
            FillArea(area, TileType.Wall);
        }

        foreach (GridWallLine wall in BlockCPlayableLayout.InteriorWalls)
        {
            FillLine(wall, TileType.Wall);
        }

        foreach (Vector2Int door in BlockCPlayableLayout.DoorCells)
        {
            AddDoorTile(door.x, door.y);
        }

        foreach (Vector2Int cover in BlockCPlayableLayout.CoverCells)
        {
            AddCover(cover.x, cover.y);
        }

        foreach (Vector2Int hedge in BlockCPlayableLayout.HedgeCells)
        {
            AddCover(hedge.x, hedge.y);
        }
    }

    private void FillArea(GridArea area, TileType type)
    {
        for (int x = area.MinX; x <= area.MaxX; x++)
        {
            for (int y = area.MinY; y <= area.MaxY; y++) SetTile(x, y, type);
        }
    }

    private void FillLine(GridWallLine line, TileType type)
    {
        int minX = Mathf.Min(line.Start.x, line.End.x);
        int maxX = Mathf.Max(line.Start.x, line.End.x);
        int minY = Mathf.Min(line.Start.y, line.End.y);
        int maxY = Mathf.Max(line.Start.y, line.End.y);
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++) SetTile(x, y, type);
        }
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
        EnsureGridInitialized();
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
                if (!ShouldRenderTile(x, y)) continue;
                GameObject tile = CreateTileVisual(x, y, grid[x, y]);
                tile.transform.SetParent(tilesParent.transform);
                tileObjects[x, y] = tile;
            }
        }
    }

    private bool ShouldRenderTile(int x, int y)
    {
        if (grid[x, y] != TileType.Wall) return true;

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                int neighborX = x + dx;
                int neighborY = y + dy;
                if (neighborX < 0 || neighborX >= width || neighborY < 0 || neighborY >= height) continue;
                if (grid[neighborX, neighborY] != TileType.Wall) return true;
            }
        }

        return false;
    }

    private GameObject CreateTileVisual(int x, int y, TileType type)
    {
        var tile = new GameObject($"Tile_{x}_{y}_{type}");
        Vector3 worldPos = GridToWorld(x, y);
        tile.transform.position = worldPos;

        if (type == TileType.Wall)
        {
            CreateWallVisual(tile, x, y);
            return tile;
        }

        Sprite fs = GetFloorSprite(x, y);
        var renderer = tile.AddComponent<SpriteRenderer>();
        renderer.sprite = fs != null ? fs : CreateSquareSprite();
        renderer.color = fs != null ? Color.white : floorColor;
        renderer.sortingOrder = SortingLayers.Floor;
        tile.transform.localScale = fs != null
            ? Vector3.one * cellSize * WorldMetrics.TileOverlap / GetSpriteSize(fs)
            : Vector3.one * cellSize * WorldMetrics.TileOverlap;

        if (type == TileType.Cover)
        {
            if (BlockCPlayableLayout.IsHedgeCell(x, y))
                CreateHedgeVisual(tile, worldPos);
            else
                CreateBlockVisual(tile, worldPos, coverColor, "Укрытие");
        }

        return tile;
    }

    // Плоская стена top-down: тайл-крышка в клетку (над полом, под сущностями) +
    // короткая боковая «юбка» на южной грани, где стена граничит с полом снизу —
    // стена читается как приподнятая плита (The Escapists), но север не перекрыт.
    private void CreateWallVisual(GameObject parent, int x, int y)
    {
        Sprite ws = GetWallSprite(x, y);
        var renderer = parent.AddComponent<SpriteRenderer>();
        renderer.sprite = ws != null ? ws : CreateSquareSprite();
        renderer.color = ws != null ? Color.white : wallTopColor;
        renderer.sortingOrder = SortingLayers.WallFlat;
        float p = ws != null
            ? cellSize * WorldMetrics.TileOverlap / GetSpriteSize(ws)
            : cellSize * WorldMetrics.TileOverlap;
        parent.transform.localScale = Vector3.one * p;

        // Свет сверху-слева -> в тень уходят ЮЖНАЯ и ВОСТОЧНАЯ грани. Рисуем их
        // на КАЖДОЙ стене, граничащей с полом с этой стороны — поэтому объём есть
        // и у горизонтальных (юг), и у вертикальных (восток) стен, на внешней
        // кромке массива (внутренние клетки за стеной грань не получают).
        // Каждая стена — приподнятый блок при свете СВЕРХУ-СЛЕВА: тёмные грани на
        // ЮГЕ и ВОСТОКЕ, светлые блики на СЕВЕРЕ и ЗАПАДЕ. Грань ставится только
        // там, где стена граничит с полом (внешняя кромка массива) — объём у стен
        // ЛЮБОЙ ориентации, без перекрытия севера.
        ZoneTiles.Zone zone = GetWallZone(x, y);
        if (IsOpen(x, y - 1)) AddWallFace(parent, p, zone, WallSide.South);
        if (IsOpen(x + 1, y)) AddWallFace(parent, p, zone, WallSide.East);
        if (IsOpen(x, y + 1)) AddWallFace(parent, p, zone, WallSide.North);
        if (IsOpen(x - 1, y)) AddWallFace(parent, p, zone, WallSide.West);
    }

    private enum WallSide { South, East, North, West }

    private bool IsOpen(int x, int y)
    {
        TileType t = GetTileType(x, y);
        return t == TileType.Floor || t == TileType.Door || t == TileType.Cover;
    }

    // Грань-«юбка» приподнятой стены у одной из кромок клетки: тонкая полоса,
    // наполовину свисает на соседнюю клетку-пол. Сорт WallFlat+1 (над полом, под
    // сущностями). ЮГ/ВОСТОК — тёмная грань (wall_edge, бизнес-конец -y), СЕВЕР/
    // ЗАПАД — светлый блик (wall_edge_hi, бизнес-конец +y). E/W повёрнуты на +90°,
    // тогда бизнес-конец смотрит наружу. localScale компенсирует масштаб родителя.
    private void AddWallFace(GameObject parent, float parentScale, ZoneTiles.Zone zone, WallSide side)
    {
        bool lit = side == WallSide.North || side == WallSide.West;
        Sprite edge = CachedArt(lit ? "wall_edge_hi" : "wall_edge");
        if (edge == null) return;

        var go = new GameObject("WallFace_" + side);
        go.transform.SetParent(parent.transform, false);

        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = edge;
        r.color = lit ? Color.white : ZoneTiles.EdgeTint(zone);
        r.sortingOrder = SortingLayers.WallFlat + 1;

        Vector2 b = edge.bounds.size;                      // (w,h) спрайта при scale 1
        float along = cellSize * WorldMetrics.TileOverlap; // длина вдоль кромки = клетка
        float thick = cellSize * (side == WallSide.South ? 0.34f
                                : side == WallSide.East ? 0.22f
                                : 0.16f);                  // блики тоньше
        go.transform.localScale = new Vector3(
            along / (b.x * parentScale),
            thick / (b.y * parentScale),
            1f);

        float half = cellSize * 0.5f / parentScale;
        switch (side)
        {
            case WallSide.South:
                go.transform.localRotation = Quaternion.identity;
                go.transform.localPosition = new Vector3(0f, -half, 0f);
                break;
            case WallSide.North:
                go.transform.localRotation = Quaternion.identity;
                go.transform.localPosition = new Vector3(0f, half, 0f);
                break;
            case WallSide.East:
                go.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                go.transform.localPosition = new Vector3(half, 0f, 0f);
                break;
            case WallSide.West:
                go.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                go.transform.localPosition = new Vector3(-half, 0f, 0f);
                break;
        }
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
        renderer.sortingOrder = SortingLayers.Entity(worldPos.y);
    }

    private void CreateHedgeVisual(GameObject parent, Vector3 worldPos)
    {
        var hedge = new GameObject("Живая изгородь");
        hedge.transform.SetParent(parent.transform);
        hedge.transform.localPosition = new Vector3(0f, 0.08f, 0f);
        hedge.transform.localScale = Vector3.one * cellSize * 0.95f / GetSpriteSize(HedgeSprite());

        var renderer = hedge.AddComponent<SpriteRenderer>();
        renderer.sprite = HedgeSprite();
        renderer.color = Color.white;
        renderer.sortingOrder = SortingLayers.Entity(worldPos.y);
    }

    private void CreateMapContent()
    {
        CreateResidentialDoors();

        CreateDoor("Столовая", 13, 20, PrisonItemId.None);
        gardenDoor = CreateDoor("Вход в сад", BlockCPlayableLayout.GardenDoor, PrisonItemId.None);
        CreateDoor("Отправление на эксперименты", 31, 53, PrisonItemId.Unavailable);
        CreateDoor("Санитарно-бытовое крыло", 50, 21, PrisonItemId.None);

        // Ворота санитарной циркуляции: тамбур → коридор → поворот → верхний переход.
        // Вход в кухонный карман идёт через ревизионную панель/вентиляцию, а не напрямую.
        // Без них дверные тайлы остаются непроходимыми (щель в стене, но не пройти).
        CreateDoor("Тамбур: входной коридор", 57, 21, PrisonItemId.None);
        CreateDoor("Коридор: северный поворот", 67, 24, PrisonItemId.None);
        CreateDoor("Поворот: верхний переход", 67, 32, PrisonItemId.None);

        CreateDoor("Умывальники", 66, 28, PrisonItemId.None);
        CreateDoor("Туалеты", 61, 32, PrisonItemId.None);
        CreateDoor("Раздевалка", 62, 20, PrisonItemId.None);
        CreateDoor("Душевые", 66, 16, PrisonItemId.None);
        CreateDoor("Сушка", 75, 16, PrisonItemId.None);
        CreateDoor("Возврат из сушки", 81, 20, PrisonItemId.None);
        CreateDoor("Хозяйственная из сушки", 81, 24, PrisonItemId.None);
        competitorServiceDoor = CreateDoor("Санитарная комната персонала", BlockCPlayableLayout.CompetitorServiceDoor, PrisonItemId.None);
        CreateDoor("Связь комнат персонала", 78, 27, PrisonItemId.None);
        CreateDoor("Хозяйственная часть", 82, 32, PrisonItemId.None);

        CreateDoor("Ревизионная панель", BlockCPlayableLayout.RevisionPanel, PrisonItemId.Screwdriver);
        CreateDoor("Переход в основную кухню", 79, 45, PrisonItemId.None);
        CreateDoor("Угол персонала кухни", 83, 48, PrisonItemId.None);
        CreateDoor("Склад текущей смены", 72, 41, PrisonItemId.None);
        CreateDoor("Странная дверь кухни", 72, 52, PrisonItemId.Unavailable);
        kitchenShortcutDoor = CreateDoor("Служебная дверь кухни", BlockCPlayableLayout.KitchenShortcutDoor, PrisonItemId.None);
        kitchenShortcutDoor.RequireFirstOpenFrom(
            BlockCPlayableLayout.KitchenShortcutServiceSide,
            "Засов и механизм находятся с другой стороны. Эту дверь нужно открыть из служебного коридора.");

        staffRoomDoor = CreateDoor("Комната персонала", BlockCPlayableLayout.StaffRoomDoor, PrisonItemId.None);
        CreateDoor("Склад", BlockCPlayableLayout.StorageDoor, PrisonItemId.KitchenManifest);
        CreateDoor("Выход из склада в защищённый коридор", BlockCPlayableLayout.SecureDoor, PrisonItemId.ServiceBadge);
        CreateDoor("Лаборатория", BlockCPlayableLayout.LaboratoryDoor, PrisonItemId.Unavailable);
        PrisonDoor engineeringEntrance = CreateDoor("Инженерная зона", BlockCPlayableLayout.EngineeringDoor, PrisonItemId.ServiceBadge);
        techWingDoor = CreateDoor("Технологическое крыло блока C", BlockCPlayableLayout.TechWingDoor, PrisonItemId.None);
        CreateDoor("Архив данных", BlockCPlayableLayout.ArchiveDoor, PrisonItemId.ArchiveKey);
        CreateDoor("Релейная комната", BlockCPlayableLayout.RelayDoor, PrisonItemId.None);

        SealCompetitorServiceDoors();
        ConfigureTechWingDoor();

        CreatePickup(PrisonItemId.KitchenManifest, BlockCPlayableLayout.KitchenManifest.x, BlockCPlayableLayout.KitchenManifest.y);
        CreatePickup(PrisonItemId.ServiceBadge, BlockCPlayableLayout.ServiceBadge.x, BlockCPlayableLayout.ServiceBadge.y);
        CreatePickup(PrisonItemId.EyeImplant, BlockCPlayableLayout.EyeImplant.x, BlockCPlayableLayout.EyeImplant.y);
        CreatePickup(PrisonItemId.Transmitter, BlockCPlayableLayout.Transmitter.x, BlockCPlayableLayout.Transmitter.y);
        CreatePickup(PrisonItemId.ExperimentReports, BlockCPlayableLayout.ExperimentReports.x, BlockCPlayableLayout.ExperimentReports.y);

        CreateResourceCaches();
        CreateBed();
        CreateGardenSmokeSpot();
        CreateRaquelGardenMeetingSpot();
        CreateGuardPostScanner();
        CreateEscapeArchiveFolder();
        CreateShortcutLock();
        CreateFloorTransitions();
        CreateObservationCenters();
        SpawnDecor();
        FurnishRoomsProcedurally();
        ConfigureGardenDoor();
        CreateEngineeringPuzzle(engineeringEntrance);
        CreateProgrammerTechPuzzles();
    }

    private void CreateResidentialDoors()
    {
        Vector2Int[] floor1Doors =
        {
            new(17, 8), new(24, 8), new(39, 8), new(46, 8), new(18, 53), new(46, 53),
            new(13, 12), new(13, 29), new(13, 50), new(50, 12), new(50, 29), new(50, 50),
        };
        Vector2Int[] floor2Doors =
        {
            BlockCPlayableLayout.F2(17, 8), BlockCPlayableLayout.F2(24, 8),
            BlockCPlayableLayout.F2(39, 8), BlockCPlayableLayout.F2(46, 8),
            BlockCPlayableLayout.F2(17, 53), BlockCPlayableLayout.F2(24, 53),
            BlockCPlayableLayout.F2(39, 53), BlockCPlayableLayout.F2(46, 53),
            BlockCPlayableLayout.F2(13, 12), BlockCPlayableLayout.F2(13, 29),
            BlockCPlayableLayout.F2(13, 50), BlockCPlayableLayout.F2(50, 12),
            BlockCPlayableLayout.F2(50, 29), BlockCPlayableLayout.F2(50, 50),
        };

        for (int i = 0; i < floor1Doors.Length; i++) CreateDoor($"Камера 1-{i + 1:00}", floor1Doors[i], PrisonItemId.None);
        for (int i = 0; i < floor2Doors.Length; i++) CreateDoor($"Камера 2-{i + 1:00}", floor2Doors[i], PrisonItemId.None);
        CreateDoor("Закрытое западное крыло второго этажа", BlockCPlayableLayout.F2(13, 41), PrisonItemId.Unavailable);
        CreateDoor("Закрытое восточное крыло второго этажа", BlockCPlayableLayout.F2(50, 41), PrisonItemId.Unavailable);
    }

    private PrisonDoor CreateDoor(string displayName, int x, int y, PrisonItemId requirement)
    {
        var go = new GameObject(displayName);
        go.transform.SetParent(transform);
        var door = go.AddComponent<PrisonDoor>();
        Sprite doorSprite = LoadArt("door_metal");
        door.Initialize(this, x, y, displayName, requirement, doorSprite != null ? doorSprite : CreateSquareSprite());
        doors.Add(door);
        return door;
    }

    private PrisonDoor CreateDoor(string displayName, Vector2Int cell, PrisonItemId requirement) =>
        CreateDoor(displayName, cell.x, cell.y, requirement);

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
            PrisonItemId.Transmitter => go.AddComponent<TransmitterItem>(),
            PrisonItemId.ExperimentReports => go.AddComponent<ExperimentReportsItem>(),
            PrisonItemId.DataSource => go.AddComponent<DataSourceItem>(),
            PrisonItemId.ComputeModule => go.AddComponent<ComputeModuleItem>(),
            PrisonItemId.SignalAmplifier => go.AddComponent<SignalAmplifierItem>(),
            _ => null,
        };
        if (item == null) { Destroy(go); return; }

        string spriteName = itemId switch
        {
            PrisonItemId.Screwdriver => "item_screwdriver",
            PrisonItemId.KitchenManifest => "item_manifest",
            PrisonItemId.ServiceBadge => "item_badge",
            PrisonItemId.EyeImplant => "item_implant",
            PrisonItemId.Transmitter => "console",
            PrisonItemId.ExperimentReports => "item_reports",
            PrisonItemId.DataSource => "console",
            PrisonItemId.ComputeModule => "console",
            PrisonItemId.SignalAmplifier => "console",
            _ => null,
        };
        Sprite itemSprite = spriteName != null ? LoadArt(spriteName) : null;
        item.Initialize(this, x, y, itemSprite != null ? itemSprite : CreateSquareSprite(), tintIcon: itemSprite == null);
    }

    private void CreateResourceCaches()
    {
        foreach (ResourceCacheSpec spec in ResourceCacheSpecs())
        {
            CreateResourceCache(spec);
        }
    }

    private readonly struct ResourceCacheSpec
    {
        public readonly string Id;
        public readonly Vector2Int Cell;
        public readonly int MinAmount;
        public readonly int MaxAmount;
        public readonly string SpriteName;
        public readonly Color Tint;

        public ResourceCacheSpec(string id, Vector2Int cell, int minAmount, int maxAmount, string spriteName, Color tint)
        {
            Id = id;
            Cell = cell;
            MinAmount = minAmount;
            MaxAmount = maxAmount;
            SpriteName = spriteName;
            Tint = tint;
        }
    }

    private static ResourceCacheSpec[] ResourceCacheSpecs()
    {
        Color crateTint = new(0.78f, 0.72f, 0.60f);
        Color techTint = new(0.56f, 0.82f, 0.68f);
        return new[]
        {
            R("cache_common_01", new Vector2Int(28, 18), 2, 4, crateTint),
            R("cache_common_02", new Vector2Int(41, 25), 2, 4, crateTint),
            R("cache_common_03", new Vector2Int(23, 44), 2, 4, crateTint),
            R("cache_common_04", new Vector2Int(35, 18), 2, 4, crateTint),
            R("cache_common_05", new Vector2Int(18, 31), 2, 4, crateTint),
            R("cache_common_06", new Vector2Int(45, 44), 2, 4, crateTint),

            R("cache_sanitary_01", new Vector2Int(60, 29), 2, 4, crateTint),
            R("cache_sanitary_02", new Vector2Int(74, 17), 2, 4, crateTint),
            R("cache_sanitary_03", new Vector2Int(84, 30), 2, 4, crateTint),
            R("cache_sanitary_04", new Vector2Int(63, 35), 2, 4, crateTint),
            R("cache_sanitary_05", new Vector2Int(80, 28), 2, 4, crateTint),

            R("cache_kitchen_01", new Vector2Int(76, 46), 2, 4, crateTint),
            R("cache_kitchen_02", new Vector2Int(77, 49), 2, 4, crateTint),
            R("cache_kitchen_03", new Vector2Int(72, 38), 2, 4, crateTint),
            R("cache_kitchen_04", new Vector2Int(84, 52), 2, 4, crateTint),
            R("cache_service_01", new Vector2Int(96, 52), 2, 4, crateTint),
            R("cache_service_02", new Vector2Int(101, 46), 2, 4, crateTint),
            R("cache_storage_01", new Vector2Int(107, 44), 2, 4, crateTint),
            R("cache_storage_02", new Vector2Int(110, 47), 2, 4, crateTint),

            R("cache_lab_01", new Vector2Int(107, 63), 2, 4, techTint),
            R("cache_lab_02", new Vector2Int(109, 67), 2, 4, techTint),
            R("cache_engineering_01", new Vector2Int(119, 66), 2, 4, techTint),
            R("cache_engineering_02", new Vector2Int(123, 67), 2, 4, techTint),
            R("cache_engineering_03", new Vector2Int(117, 68), 2, 4, techTint),

            R("cache_tech_01", BlockCPlayableLayout.F2(134, 56), 2, 4, techTint),
            R("cache_tech_02", BlockCPlayableLayout.F2(137, 58), 2, 4, techTint),
            R("cache_archive_01", BlockCPlayableLayout.F2(148, 57), 2, 4, techTint),
            R("cache_archive_02", BlockCPlayableLayout.F2(150, 55), 2, 4, techTint),
            R("cache_relay_01", BlockCPlayableLayout.F2(148, 40), 2, 4, techTint),
            R("cache_relay_02", BlockCPlayableLayout.F2(149, 38), 2, 4, techTint),

            R("cache_garden_01", new Vector2Int(3, 44), 2, 4, crateTint),
            R("cache_garden_02", new Vector2Int(10, 44), 2, 4, crateTint),
        };
    }

    private static ResourceCacheSpec R(string id, Vector2Int cell, int minAmount, int maxAmount, Color tint) =>
        new(id, cell, minAmount, maxAmount, "crate", tint);

    private void CreateResourceCache(ResourceCacheSpec spec)
    {
        if (!IsWalkable(spec.Cell.x, spec.Cell.y)) return;

        var go = new GameObject($"ResourceCache_{spec.Id}");
        go.transform.SetParent(transform);
        var cache = go.AddComponent<ResourceCacheInteractable>();
        Sprite sprite = LoadArt(spec.SpriteName) ?? CreateSquareSprite();
        cache.Initialize(this, spec.Cell, spec.Id, spec.MinAmount, spec.MaxAmount, sprite, spec.Tint);
    }

    private void CreateBed()
    {
        var go = new GameObject("Кровать игрока");
        go.transform.SetParent(transform);
        var bed = go.AddComponent<BedInteractable>();
        bed.Initialize(this, BlockCPlayableLayout.PlayerBed, LoadArt("bed"), CreateSquareSprite());
    }

    private void CreateGardenSmokeSpot()
    {
        var go = new GameObject("Точка подслушивания в саду");
        go.transform.SetParent(transform);
        var spot = go.AddComponent<GardenSmokeSpot>();
        spot.Initialize(this, BlockCPlayableLayout.GardenSmokeSpot, LoadArt("smoke_spot") ?? CreateSquareSprite());
    }

    private void CreateRaquelGardenMeetingSpot()
    {
        var go = new GameObject("Ракель у входа в сад");
        go.transform.SetParent(transform);
        var spot = go.AddComponent<RaquelGardenMeetingSpot>();
        spot.Initialize(this, BlockCPlayableLayout.RaquelGardenMeeting, LoadArt("girl"));
    }

    private void CreateGuardPostScanner()
    {
        var go = new GameObject("Сканер поста охраны");
        go.transform.SetParent(transform);
        var scanner = go.AddComponent<GuardPostScanner>();
        scanner.Initialize(this, BlockCPlayableLayout.GuardPostScanner, LoadArt("console") ?? CreateSquareSprite());
        roomObjectives.Add(scanner);
    }

    private void CreateEscapeArchiveFolder()
    {
        var go = new GameObject("Папка о сбежавшем заключённом");
        go.transform.SetParent(transform);
        var folder = go.AddComponent<EscapeArchiveFolderInteractable>();
        folder.Initialize(this, BlockCPlayableLayout.EscapeArchiveFolder, LoadArt("item_reports") ?? CreateSquareSprite());
        roomObjectives.Add(folder);
    }

    private void CreateShortcutLock()
    {
        var go = new GameObject("Замок shortcut блока C");
        go.transform.SetParent(transform);
        var shortcut = go.AddComponent<ShortcutLock>();
        shortcut.Initialize(this, BlockCPlayableLayout.BlockCShortcutLock, LoadArt("keypad") ?? CreateSquareSprite());
        roomObjectives.Add(shortcut);
    }

    private void CreateFloorTransitions()
    {
        CreatePortal("Западная лестница: наверх", BlockCPlayableLayout.WestStairFloor1, BlockCPlayableLayout.WestStairFloor2);
        CreatePortal("Восточная лестница: наверх", BlockCPlayableLayout.EastStairFloor1, BlockCPlayableLayout.EastStairFloor2);
        CreatePortal("Западная лестница: вниз", BlockCPlayableLayout.WestStairFloor2, BlockCPlayableLayout.WestStairFloor1);
        CreatePortal("Восточная лестница: вниз", BlockCPlayableLayout.EastStairFloor2, BlockCPlayableLayout.EastStairFloor1);
        CreatePortal("Техническая лестница: наверх", BlockCPlayableLayout.TechStairFloor1, BlockCPlayableLayout.TechStairFloor2);
        CreatePortal("Техническая лестница: вниз", BlockCPlayableLayout.TechStairFloor2, BlockCPlayableLayout.TechStairFloor1);
    }

    private void CreatePortal(string objectName, Vector2Int cell, Vector2Int destination)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(transform);
        var portal = go.AddComponent<GridPortal>();
        portal.Initialize(this, cell, destination, LoadArt("stairs") ?? CreateSquareSprite());
    }

    private void CreateObservationCenters()
    {
        CreateObservationCenter("Подвесной центр наблюдения: вид снизу", new Vector2Int(31, 31), 0.65f);
        CreateObservationCenter("Подвесной центр наблюдения", BlockCPlayableLayout.F2(31, 31), 0.9f);
    }

    private void CreateObservationCenter(string objectName, Vector2Int cell, float alpha)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(transform);
        go.transform.position = GridToWorld(cell.x, cell.y);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = LoadArt("observation_dome") ?? CreateSquareSprite();
        renderer.color = new Color(1f, 1f, 1f, alpha);   // арт не красим, прозрачность сохраняем
        renderer.sortingOrder = SortingLayers.WallFlat + 1;
        float spriteSize = Mathf.Max(renderer.sprite.bounds.size.x, renderer.sprite.bounds.size.y);
        go.transform.localScale = Vector3.one * 7f / Mathf.Max(0.0001f, spriteSize);
    }

    /// <summary>
    /// Расставляет декоративные пропы (мебель + атмосфера) из таблицы
    /// BlockCPlayableLayout.DecorProps. Чисто визуальный слой: не трогает сетку
    /// проходимости. Пропы без спрайта молча пропускаются (без белых квадратов).
    /// </summary>
    private void SpawnDecor()
    {
        foreach (DecorPlacement decor in BlockCPlayableLayout.DecorProps)
        {
            Sprite art = LoadArt(decor.Sprite);
            if (art == null) continue;

            // Настенные пропы (плакат/лампа/окно/камера/замок) — вешаем на грань стены,
            // а не ставим на клетку пола.
            if (!decor.OnFloor && WallMountedProps.Contains(decor.Sprite))
            {
                PlaceWallMounted(decor.Sprite, decor.Cell, decor.Scale);
                continue;
            }

            // Мебель/сантехника — непроходима (декаль на полу и настенный декор — нет).
            if (!decor.OnFloor && SolidPropSprites.Contains(decor.Sprite))
                solidProps.Add(decor.Cell);

            var go = new GameObject($"Decor_{decor.Sprite}");
            go.transform.SetParent(transform);
            Vector3 pos = GridToWorld(decor.Cell.x, decor.Cell.y);
            go.transform.position = pos;
            if (decor.Rotation != 0) go.transform.rotation = Quaternion.Euler(0f, 0f, decor.Rotation);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = art;
            renderer.color = Color.white;
            // Декаль на полу — под персонажами и стенами; объект — Y-sort с сущностями.
            renderer.sortingOrder = decor.OnFloor
                ? SortingLayers.WallFlat - 1
                : SortingLayers.Entity(pos.y) - 1;

            float spriteSize = Mathf.Max(art.bounds.size.x, art.bounds.size.y);
            go.transform.localScale = Vector3.one * CellSize * decor.Scale / Mathf.Max(0.0001f, spriteSize);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Процедурная меблировка комнат. Читает РЕАЛЬНЫЕ проходимые клетки уже
    // построенного грида (поэтому ничего не встаёт «в стену»), плотно заполняет
    // комнаты тематической мебелью: пристенная лента вдоль стен + острова решёткой
    // в глубине, плюс настенный декор и напольные декали. Крупная мебель делается
    // непроходимой (solidProps) — сквозь неё не пройти. BFS-страховка гарантирует,
    // что двери комнаты остаются связаны; если остров всё же режет проход, он
    // снимается из solidProps (остаётся виден, но проходим). Запуск — после
    // SpawnDecor (solidProps уже заполнен статикой) и до спавна персонажей.
    // ─────────────────────────────────────────────────────────────────────────

    private static readonly Vector2Int[] Neigh4 =
        { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

    private struct RoomKit
    {
        public string[] Perimeter;   // напольная мебель вдоль стен (лицом в зал, PlaceWallProp)
        public string[] Interior;    // острова в глубине (выровненная сетка)
        public string[] Mount;       // настенные пропы (висят на стене: панель/плакат/камера/лампа)
        public bool IslandsWide;     // острова шириной 2 тайла (стол/скамья/диван)
        public bool DecorOnly;       // узкие/влажные комнаты — только декор, без мебели

        // Плотность пристенной мебели (0 = дефолт, см. FurnishPerimeter): период
        // кластеров, длина прогона в кластере, мин. длина стены под мебель.
        public int WallPeriod;       // 0 → 5
        public int WallRun;          // 0 → 2
        public int MinSegLen;        // 0 → 4
    }

    // Пропы, у которых есть боковой/задний ракурс (_side/_up) — их разворачиваем к стене.
    private static readonly HashSet<string> DirectionalProps = new HashSet<string>
    {
        "serving_line", "vending_machine", "server_rack", "lab_bench",
        "filing_cabinet", "stove_range", "prep_counter", "kitchen_shelf", "shelving_rack",
    };

    // Пропы, которые ВИСЯТ на стене (плакат/панель/лампа/камера/окно/меню/замок): рисуем
    // на грани стены (сдвиг к стене), они НЕ занимают клетку пола и проходимы.
    private static readonly HashSet<string> WallMountedProps = new HashSet<string>
    {
        "poster_obey", "wall_lamp", "window_barred", "camera", "keypad",
        "control_panel", "menu_board",
    };

    private void FurnishRoomsProcedurally()
    {
        RoomGraph graph = RoomGraph;
        Dictionary<int, string> nameById = LayoutValidator.NameByComponent(graph);
        HashSet<Vector2Int> reserved = CollectReservedFurnishCells();

        foreach (RoomGraph.Room room in graph.Rooms)
        {
            string name = nameById.TryGetValue(room.Id, out string n) ? n : null;
            FurnishOneRoom(room, name, ZoneTiles.Classify(name), reserved);
        }
    }

    // Клетки, которые меблировка НЕ трогает: двери и подходы к ним, укрытия, живая
    // изгородь, спавны/пикапы/старт, точки маршрутов охраны.
    private HashSet<Vector2Int> CollectReservedFurnishCells()
    {
        var r = new HashSet<Vector2Int>();
        foreach (Vector2Int d in BlockCPlayableLayout.DoorCells)
        {
            r.Add(d);
            foreach (Vector2Int n in Neigh4) r.Add(new Vector2Int(d.x + n.x, d.y + n.y));
        }
        foreach (Vector2Int c in BlockCPlayableLayout.CoverCells) r.Add(c);
        foreach (Vector2Int c in BlockCPlayableLayout.HedgeCells) r.Add(c);

        r.Add(BlockCPlayableLayout.PlayerStart);
        r.Add(BlockCPlayableLayout.PlayerBed);
        r.Add(BlockCPlayableLayout.ProgrammerSpawn);
        r.Add(BlockCPlayableLayout.CompetitorSpawn);
        r.Add(BlockCPlayableLayout.ExperimentNpc);
        r.Add(BlockCPlayableLayout.ExperimentReturnSpawn);
        r.Add(BlockCPlayableLayout.KitchenManifest);
        r.Add(BlockCPlayableLayout.ServiceBadge);
        r.Add(BlockCPlayableLayout.EyeImplant);
        r.Add(BlockCPlayableLayout.Transmitter);
        r.Add(BlockCPlayableLayout.ExperimentReports);
        r.Add(BlockCPlayableLayout.GardenSmokeSpot);
        r.Add(BlockCPlayableLayout.RaquelGardenMeeting);
        r.Add(BlockCPlayableLayout.GardenMeetingInterior);
        r.Add(BlockCPlayableLayout.GuardPostScanner);
        r.Add(BlockCPlayableLayout.EscapeArchiveFolder);
        r.Add(BlockCPlayableLayout.BlockCShortcutLock);
        r.Add(BlockCPlayableLayout.TechStairFloor1);
        r.Add(BlockCPlayableLayout.TechStairFloor2);
        r.Add(BlockCPlayableLayout.DataSourceObjective);
        r.Add(BlockCPlayableLayout.ComputeModuleObjective);
        r.Add(BlockCPlayableLayout.SignalAmplifierObjective);

        foreach (ResourceCacheSpec cache in ResourceCacheSpecs())
        {
            AddReserveWithNeighbors(r, cache.Cell);
        }

        foreach (Vector2Int cell in EngineeringPuzzleCells()) AddReserveWithNeighbors(r, cell);
        foreach (Vector2Int cell in ProgrammerPuzzleCells()) AddReserveWithNeighbors(r, cell);
        foreach (Vector2Int cell in BlockCPlayableLayout.EngineeringSecretPassage()) r.Add(cell);
        foreach (Vector2Int cell in BlockCPlayableLayout.BlockCShortcut()) r.Add(cell);

        foreach (DefaultGuard g in PrisonDefaults.Guards())
        {
            if (g.Route == null) continue;
            foreach (PatrolWaypoint wp in g.Route) r.Add(wp.Cell);
        }
        return r;
    }

    private void AddReserveWithNeighbors(HashSet<Vector2Int> reserved, Vector2Int cell)
    {
        reserved.Add(cell);
        foreach (Vector2Int n in Neigh4) reserved.Add(new Vector2Int(cell.x + n.x, cell.y + n.y));
    }

    private IEnumerable<Vector2Int> EngineeringPuzzleCells()
    {
        for (int x = 117; x <= 120; x++)
        {
            for (int y = 62; y <= 65; y++) yield return new Vector2Int(x, y);
        }
    }

    private IEnumerable<Vector2Int> ProgrammerPuzzleCells()
    {
        yield return BlockCPlayableLayout.F2(132, 54);
        yield return BlockCPlayableLayout.F2(133, 54);
        yield return BlockCPlayableLayout.F2(134, 54);
        yield return BlockCPlayableLayout.F2(134, 55);

        yield return BlockCPlayableLayout.F2(144, 53);
        yield return BlockCPlayableLayout.F2(145, 53);
        yield return BlockCPlayableLayout.F2(145, 54);
        yield return BlockCPlayableLayout.F2(146, 54);
        yield return BlockCPlayableLayout.F2(147, 54);
        yield return BlockCPlayableLayout.F2(147, 55);

        yield return BlockCPlayableLayout.F2(144, 37);
        yield return BlockCPlayableLayout.F2(145, 37);
        yield return BlockCPlayableLayout.F2(146, 37);
        yield return BlockCPlayableLayout.F2(146, 38);
        yield return BlockCPlayableLayout.F2(146, 39);
        yield return BlockCPlayableLayout.F2(147, 39);
        yield return BlockCPlayableLayout.F2(148, 39);
    }

    // Тематический набор по имени/зоне: Perimeter — «прогон» вдоль стен (лицом в
    // зал), Interior — острова. IslandsWide — острова в 2 тайла (стол/скамья/диван).
    private static RoomKit KitFor(string name, ZoneTiles.Zone zone)
    {
        switch (name)
        {
            case "Laboratory":
                return new RoomKit { Perimeter = new[] { "lab_bench", "kitchen_shelf" },
                                     Interior = new[] { "lab_bench" }, IslandsWide = true,
                                     MinSegLen = 5,   // приборы только вдоль главных стен
                                     Mount = new[] { "control_panel", "camera", "wall_lamp" } };
            case "Engineering":
                return new RoomKit { DecorOnly = true };
            case "Archive":
                return new RoomKit { Perimeter = new[] { "filing_cabinet", "shelving_rack" },
                                     Interior = new[] { "filing_cabinet" },
                                     WallPeriod = 6, WallRun = 2,   // шкафы редкими группами, ряды дают острова
                                     Mount = new[] { "poster_obey", "camera", "wall_lamp" } };
            case "Relay":
            case "TechWing":
                return new RoomKit { Perimeter = new[] { "server_rack" },
                                     Interior = new[] { "server_rack", "machinery" },
                                     WallPeriod = 4, WallRun = 2,   // стойки плотными группами
                                     Mount = new[] { "control_panel", "camera", "wall_lamp" } };
            case "Storage":
                return new RoomKit { Perimeter = new[] { "shelving_rack" },
                                     Interior = new[] { "crate", "crate_hide" },
                                     Mount = new[] { "poster_obey", "camera", "wall_lamp" } };
            case "StaffRoom":
                return new RoomKit { Perimeter = new[] { "kitchen_shelf", "locker" },
                                     Interior = new[] { "staff_sofa" }, IslandsWide = true,
                                     Mount = new[] { "poster_obey", "wall_lamp" } };
            case "SecureCorridor":
                return new RoomKit { DecorOnly = true };
        }

        return zone switch
        {
            ZoneTiles.Zone.Kitchen => new RoomKit { Perimeter = new[] { "stove_range", "prep_counter", "kitchen_shelf" },
                                                    Interior = new[] { "prep_counter" }, IslandsWide = true,
                                                    MinSegLen = 5,   // техника вдоль главных стен
                                                    Mount = new[] { "wall_lamp", "poster_obey" } },
            ZoneTiles.Zone.Tech    => new RoomKit { Perimeter = new[] { "server_rack", "shelving_rack" },
                                                    Interior = new[] { "machinery", "server_rack" },
                                                    WallPeriod = 4, WallRun = 2,   // стойки группами
                                                    Mount = new[] { "control_panel", "camera", "wall_lamp" } },
            ZoneTiles.Zone.Garden  => new RoomKit { Perimeter = new[] { "planter", "bush_hedge" },
                                                    Interior = new[] { "garden_bench" },
                                                    Mount = new[] { "wall_lamp" } },
            ZoneTiles.Zone.Wet     => new RoomKit { DecorOnly = true },
            _                      => new RoomKit { Perimeter = new[] { "shelving_rack", "locker" },
                                                    Interior = new[] { "crate", "crate_hide" },
                                                    Mount = new[] { "poster_obey", "wall_lamp" } },
        };
    }

    private void FurnishOneRoom(RoomGraph.Room room, string name, ZoneTiles.Zone zone,
        HashSet<Vector2Int> reserved)
    {
        if (zone == ZoneTiles.Zone.Cell) return;                  // камеры уже обставлены (FurnishCell)
        if (name == "A1-Atrium" || name == "F2-Gallery") return;  // хабы — держим открытыми
        if (room.Cells.Count < 6 || room.Cells.Count > 250) return;

        // Столовая — отдельная осмысленная композиция.
        if (zone == ZoneTiles.Zone.Dining) { ComposeDining(room, reserved); return; }

        RoomKit kit = KitFor(name, zone);

        // Узкие комнаты/связки — без напольной мебели, только редкий настенный декор
        // (иначе предмет встаёт поперёк прохода и выглядит мусором).
        int rw = room.Max.x - room.Min.x + 1, rh = room.Max.y - room.Min.y + 1;
        if (Mathf.Min(rw, rh) <= 3) kit.DecorOnly = true;

        var roomCells = new HashSet<Vector2Int>(room.Cells);
        var solids = new List<Vector2Int>();

        // Пристенная мебель — разрежёнными когерентными кластерами (не сплошной лентой),
        // один тип пропа на связный сегмент стены. Пустые участки стен — это норма.
        FurnishPerimeter(room, kit, roomCells, reserved, solids);

        foreach (Vector2Int cell in room.Cells)
        {
            if (!IsWalkable(cell.x, cell.y) || reserved.Contains(cell)) continue;
            bool perimeter = IsPerimeterCell(cell, roomCells, out _);
            int h = Hash(cell);

            if (kit.DecorOnly)
            {
                // Только редкий настенный декор; пол ЧИСТЫЙ (без хеш-декалей).
                if (perimeter && h % 6 == 0)
                    PlaceWallMounted((h / 6) % 2 == 0 ? "wall_lamp" : "poster_obey", cell, 0.6f);
                continue;
            }

            if (perimeter) continue;   // пристенную мебель уже расставил FurnishPerimeter

            // Немного островов в ГЛУБИНЕ — редкой выровненной сеткой, пол вокруг открыт.
            // Никаких хеш-декалей: пустой пол — это норма, а не «недозаполнение».
            if (kit.Interior is { Length: > 0 } && IsIslandAnchor(cell, room.Min, kit.IslandsWide))
            {
                string sp = kit.Interior[0];   // один тип острова на комнату — читаемо, не «как попало»
                if (kit.IslandsWide)
                {
                    var b = new Vector2Int(cell.x + 1, cell.y);   // остров в 2 тайла — визуал = коллизия
                    if (roomCells.Contains(b) && IsWalkable(b.x, b.y) && !reserved.Contains(b))
                    {
                        SpawnFurnitureSprite(sp, cell, 2.0f, 0, false, false, 0.5f, 0f);
                        solidProps.Add(cell); solidProps.Add(b); solids.Add(cell); solids.Add(b);
                    }
                }
                else
                {
                    // Скамьи сада — ряды с чередованием ракурса (спиной/лицом) через ряд.
                    string variant = sp == "garden_bench"
                        ? (((cell.y - room.Min.y) % 2 == 0) ? "garden_bench_up" : "garden_bench")
                        : sp;
                    SpawnFurnitureSprite(variant, cell, 0.95f, 0, false);
                    solidProps.Add(cell); solids.Add(cell);
                }
            }
        }

        // BFS-страховка: если мебель разрезала проход между дверями — снять solidProps
        // (пропы остаются видны, но проходимы), чтобы не сломать маршруты.
        var targets = RoomConnectivityTargets(room, roomCells);
        if (!RoomStillConnected(roomCells, targets))
            foreach (var c in solids) solidProps.Remove(c);
    }

    // Пристенная напольная мебель короткими кластерами с разрывами (НЕ сплошной лентой):
    // клетки у стен группируются в сегменты вдоль одной стены; каждый сегмент получает
    // ОДИН тип пропа (когерентно, без шахматки), а внутри сегмента мебель ставится
    // пачками длины WallRun с периодом WallPeriod. Короткие стены (< MinSegLen) остаются
    // голыми. В разрывах между кластерами изредка ВИСИТ настенный акцент (Mount).
    // Детерминировано (Hash + арифметика по координатам); ракурс к стене и solidProps —
    // как в PlaceWallProp. DecorOnly-комнаты и комнаты без Perimeter пропускаются.
    private void FurnishPerimeter(RoomGraph.Room room, RoomKit kit,
        HashSet<Vector2Int> roomCells, HashSet<Vector2Int> reserved, List<Vector2Int> solids)
    {
        if (kit.DecorOnly || kit.Perimeter is not { Length: > 0 }) return;

        int period = kit.WallPeriod > 0 ? kit.WallPeriod : 5;
        int run = kit.WallRun > 0 ? kit.WallRun : 2;
        int minSeg = kit.MinSegLen > 0 ? kit.MinSegLen : 4;

        // Сегмент = стена одной стороны: горизонтальная (сверху/снизу) → ключ (0, y),
        // ход по x; вертикальная (слева/справа) → ключ (1, x), ход по y. Угловую клетку
        // (обе стороны) детерминированно относим к горизонтали, чтобы не ставить дважды.
        var segments = new Dictionary<Vector2Int, List<Vector2Int>>();
        foreach (Vector2Int cell in room.Cells)
        {
            if (!IsWalkable(cell.x, cell.y) || reserved.Contains(cell)) continue;
            bool wallUD = GetTileType(cell.x, cell.y - 1) == TileType.Wall
                       || GetTileType(cell.x, cell.y + 1) == TileType.Wall;
            bool wallLR = GetTileType(cell.x - 1, cell.y) == TileType.Wall
                       || GetTileType(cell.x + 1, cell.y) == TileType.Wall;
            if (!wallUD && !wallLR) continue;
            Vector2Int key = wallUD ? new Vector2Int(0, cell.y) : new Vector2Int(1, cell.x);
            if (!segments.TryGetValue(key, out List<Vector2Int> list))
            {
                list = new List<Vector2Int>();
                segments[key] = list;
            }
            list.Add(cell);
        }

        foreach (KeyValuePair<Vector2Int, List<Vector2Int>> kv in segments)
        {
            List<Vector2Int> seg = kv.Value;
            if (seg.Count < minSeg) continue;   // короткие стены оставляем голыми
            bool horizontal = kv.Key.x == 0;
            seg.Sort((a, b) => horizontal ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));

            int hs = Hash(kv.Key);
            string sp = kit.Perimeter[hs % kit.Perimeter.Length];   // один тип на сегмент
            int phase = (hs / kit.Perimeter.Length) % period;

            foreach (Vector2Int cell in seg)
            {
                int along = horizontal ? cell.x : cell.y;
                if ((along + phase) % period < run)
                    PlaceWallProp(sp, cell, 0.92f, DirectionalProps.Contains(sp), true, solids);
                else if (kit.Mount is { Length: > 0 } && Hash(cell) % 6 == 0)
                    PlaceWallMounted(kit.Mount[hs % kit.Mount.Length], cell, 0.6f);
            }
        }
    }

    // Якорь острова в выровненной сетке: широкие (2 тайла) — реже, для проходов.
    // Острова РЕДКИЕ (комната не должна быть забита одинаковыми пропами): широкие
    // (2 тайла) — совсем редко, узкие — умеренно, всегда с проходами вокруг.
    private static bool IsIslandAnchor(Vector2Int cell, Vector2Int min, bool wide)
    {
        int cx = cell.x - min.x, cy = cell.y - min.y;
        return wide ? (cx % 6 == 3 && cy % 5 == 2) : (cx % 4 == 2 && cy % 4 == 2);
    }

    // Клетка у стены комнаты (сосед — стена или граница). wallRot=90, если стена
    // слева/справа (длинный проп разворачиваем вертикально), иначе 0.
    private bool IsPerimeterCell(Vector2Int cell, HashSet<Vector2Int> roomCells, out int wallRot)
    {
        bool wallLR = GetTileType(cell.x - 1, cell.y) == TileType.Wall
                   || GetTileType(cell.x + 1, cell.y) == TileType.Wall;
        bool wallUD = GetTileType(cell.x, cell.y - 1) == TileType.Wall
                   || GetTileType(cell.x, cell.y + 1) == TileType.Wall;
        wallRot = wallLR ? 90 : 0;
        // Периметр = клетка у НАСТОЯЩЕЙ стены. Чистую границу комнаты (сосед вне
        // roomCells, но не стена) НЕ считаем — иначе мебель встаёт «рамкой» по краям.
        return wallLR || wallUD;
    }

    private static int Hash(Vector2Int c)
    {
        int h = (c.x * 73856093) ^ (c.y * 19349663);
        return h & 0x7fffffff;
    }

    private List<Vector2Int> RoomConnectivityTargets(RoomGraph.Room room, HashSet<Vector2Int> roomCells)
    {
        var t = new List<Vector2Int>();
        if (roomCells.Contains(room.Centroid)) t.Add(room.Centroid);
        foreach (Vector2Int d in BlockCPlayableLayout.DoorCells)
            foreach (Vector2Int n in Neigh4)
            {
                var nc = new Vector2Int(d.x + n.x, d.y + n.y);
                if (roomCells.Contains(nc)) t.Add(nc);
            }
        return t;
    }

    // Все двери-подходы и центр комнаты по-прежнему взаимно достижимы через
    // проходимые клетки (мебель уже учтена в IsWalkable через solidProps).
    private bool RoomStillConnected(HashSet<Vector2Int> roomCells, List<Vector2Int> targets)
    {
        Vector2Int start = default;
        bool has = false;
        foreach (Vector2Int t in targets)
            if (IsWalkable(t.x, t.y)) { start = t; has = true; break; }
        if (!has) return true;

        var seen = new HashSet<Vector2Int> { start };
        var q = new Queue<Vector2Int>();
        q.Enqueue(start);
        while (q.Count > 0)
        {
            Vector2Int c = q.Dequeue();
            foreach (Vector2Int n in Neigh4)
            {
                var nc = new Vector2Int(c.x + n.x, c.y + n.y);
                if (seen.Contains(nc) || !roomCells.Contains(nc) || !IsWalkable(nc.x, nc.y)) continue;
                seen.Add(nc);
                q.Enqueue(nc);
            }
        }
        foreach (Vector2Int t in targets)
            if (IsWalkable(t.x, t.y) && !seen.Contains(t)) return false;
        return true;
    }

    private void SpawnFurnitureSprite(string sprite, Vector2Int cell, float scale, int rotation,
        bool onFloor, bool flipX = false, float cellDx = 0f, float cellDy = 0f)
    {
        Sprite art = LoadArt(sprite);
        if (art == null) return;

        var go = new GameObject($"Furnish_{sprite}");
        go.transform.SetParent(transform);
        // cellDx/cellDy — дробный сдвиг в долях клетки (напр. +0.5 чтобы поставить
        // проп по центру между двумя клетками — для мебели шириной в 2 тайла).
        Vector3 pos = GridToWorld(cell.x, cell.y) + new Vector3(cellDx * CellSize, cellDy * CellSize, 0f);
        go.transform.position = pos;
        if (rotation != 0) go.transform.rotation = Quaternion.Euler(0f, 0f, rotation);

        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = art;
        r.color = Color.white;
        r.flipX = flipX;
        r.sortingOrder = onFloor ? SortingLayers.WallFlat - 1 : SortingLayers.Entity(pos.y) - 1;

        float sz = Mathf.Max(art.bounds.size.x, art.bounds.size.y);
        go.transform.localScale = Vector3.one * CellSize * scale / Mathf.Max(0.0001f, sz);
    }

    // Пристенный проп с выбором ракурса по стороне стены (лицом в зал):
    // стена сверху→front(база), снизу→back(_up), справа→side, слева→side+flip.
    // Директиональные варианты есть только у некоторых пропов (serving_line,
    // vending_machine); для остальных берётся база.
    private void PlaceWallProp(string baseName, Vector2Int c, float scale, bool directional,
        bool solid, List<Vector2Int> solids)
    {
        string sprite = baseName;
        bool flip = false;
        if (directional)
        {
            if (GetTileType(c.x, c.y + 1) == TileType.Wall) sprite = baseName;               // стена N → лицом на юг → front
            else if (GetTileType(c.x, c.y - 1) == TileType.Wall) sprite = baseName + "_up";   // стена S → лицом на север → back
            else if (GetTileType(c.x + 1, c.y) == TileType.Wall) sprite = baseName + "_side";  // стена E → лицом на запад → side
            else if (GetTileType(c.x - 1, c.y) == TileType.Wall) { sprite = baseName + "_side"; flip = true; } // стена W → лицом на восток
        }
        SpawnFurnitureSprite(sprite, c, scale, 0, false, flip);
        if (solid) { solidProps.Add(c); solids?.Add(c); }
    }

    // Настенный проп: рисуем на грани примыкающей стены (сдвиг на 0.5 клетки к стене),
    // сорт над «юбкой» стены и под сущностями. Клетку пола НЕ занимает (проходимо).
    // Предпочитаем северную стену — её грань обращена к камере.
    private void PlaceWallMounted(string sprite, Vector2Int cell, float scale)
    {
        Sprite art = LoadArt(sprite);
        if (art == null) return;

        Vector2 off;
        if (GetTileType(cell.x, cell.y + 1) == TileType.Wall) off = new Vector2(0f, 0.5f);
        else if (GetTileType(cell.x, cell.y - 1) == TileType.Wall) off = new Vector2(0f, -0.5f);
        else if (GetTileType(cell.x + 1, cell.y) == TileType.Wall) off = new Vector2(0.5f, 0f);
        else if (GetTileType(cell.x - 1, cell.y) == TileType.Wall) off = new Vector2(-0.5f, 0f);
        else off = new Vector2(0f, 0.5f);

        var go = new GameObject($"WallMount_{sprite}");
        go.transform.SetParent(transform);
        go.transform.position = GridToWorld(cell.x, cell.y)
            + new Vector3(off.x * cellSize, off.y * cellSize, 0f);

        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = art;
        r.color = Color.white;
        r.sortingOrder = SortingLayers.WallFlat + 2;   // на грани стены, над юбкой, под сущностями
        float sz = Mathf.Max(art.bounds.size.x, art.bounds.size.y);
        go.transform.localScale = Vector3.one * cellSize * scale / Mathf.Max(0.0001f, sz);
    }

    // Осмысленная композиция столовой (MESS): раздаточная линия вдоль дальней
    // (северной) стены лицом в зал, ряды столов со скамьями с проходами, возврат
    // подносов у входа, автоматы на боковых стенах, меню/диспенсер на торцах
    // раздачи. Вход — с юга (дверь 31,53). Использует directional-ассеты.
    private void ComposeDining(RoomGraph.Room room, HashSet<Vector2Int> reserved)
    {
        int minX = room.Min.x, maxX = room.Max.x, minY = room.Min.y, maxY = room.Max.y;
        var roomCells = new HashSet<Vector2Int>(room.Cells);
        var solids = new List<Vector2Int>();

        // Колонны входных дверей комнаты — держим свободными (проход к раздаче).
        var doorCols = new HashSet<int>();
        foreach (Vector2Int d in BlockCPlayableLayout.DoorCells)
            foreach (Vector2Int n in Neigh4)
            {
                var nc = new Vector2Int(d.x + n.x, d.y + n.y);
                if (roomCells.Contains(nc)) doorCols.Add(nc.x);
            }

        // 1. Раздаточная линия вдоль северной стены (y=maxY), лицом на юг (front).
        //    На торцах — диспенсер воды (запад) и табло-меню (восток).
        for (int x = minX; x <= maxX; x++)
        {
            var c = new Vector2Int(x, maxY);
            if (!IsWalkable(x, maxY) || reserved.Contains(c)) continue;
            if (x == minX) PlaceWallProp("drink_dispenser", c, 0.9f, false, true, solids);
            else PlaceWallProp("serving_line", c, 1.05f, true, true, solids);
        }
        // Табло-меню ВИСИТ на стене над раздачей (не занимает пол).
        var menuCell = new Vector2Int(maxX, maxY);
        if (roomCells.Contains(menuCell)) PlaceWallMounted("menu_board", menuCell, 0.95f);

        // 2. Торговые автоматы на боковых стенах, лицом в зал.
        foreach (var c in new[] { new Vector2Int(maxX, maxY - 2), new Vector2Int(minX, maxY - 2) })
            if (IsWalkable(c.x, c.y) && !reserved.Contains(c))
                PlaceWallProp("vending_machine", c, 0.95f, true, true, solids);

        // 3. Возврат подносов у входа (южные углы).
        foreach (var c in new[] { new Vector2Int(minX, minY), new Vector2Int(maxX, minY) })
            if (IsWalkable(c.x, c.y) && !reserved.Contains(c))
            {
                SpawnFurnitureSprite("tray_return", c, 0.9f, 0, false, false);
                solidProps.Add(c); solids.Add(c);
            }

        // 4. Ряды столов со скамьями: длинный стол = 2 клетки, сетка с проходами
        //    (через колонку и через ряд). Колонны дверей и раздачу не трогаем.
        for (int ty = minY + 1; ty <= maxY - 2; ty += 2)
        {
            for (int tx = minX + 1; tx + 1 <= maxX - 1; tx += 3)
            {
                var a = new Vector2Int(tx, ty);
                var b = new Vector2Int(tx + 1, ty);
                if (doorCols.Contains(tx) || doorCols.Contains(tx + 1)) continue;
                if (!IsWalkable(a.x, a.y) || !IsWalkable(b.x, b.y)) continue;
                if (reserved.Contains(a) || reserved.Contains(b)) continue;
                // Стол шириной 2 тайла: рисуем по центру между a и b (сдвиг +0.5),
                // масштаб 2 клетки — визуал точно совпадает с занятыми клетками a,b.
                SpawnFurnitureSprite("mess_table", a, 2.0f, 0, false, false, 0.5f, 0f);
                solidProps.Add(a); solidProps.Add(b);
                solids.Add(a); solids.Add(b);
            }
        }

        // BFS-страховка: двери столовой остаются связаны. Если нет — снимаем solidProps
        // (пропы остаются видны, но проходимы), чтобы не сломать маршруты.
        var targets = RoomConnectivityTargets(room, roomCells);
        if (!RoomStillConnected(roomCells, targets))
            foreach (var c in solids) solidProps.Remove(c);
    }

    private void SealCompetitorServiceDoors()
    {
        SealCompetitorDoor(competitorServiceDoor);
    }

    private static void SealCompetitorDoor(PrisonDoor door)
    {
        if (door == null) return;
        door.SealClosed();
        door.SetSealedInteraction(_ =>
            DialogueUI.Instance.Show("Доступ только для персонала. Возможно, кто-то с допуском откроет её по расписанию.", 2.2f));
    }

    private void ConfigureTechWingDoor()
    {
        if (techWingDoor == null || RunState.ProgrammerRouteNeedsTechWing) return;

        techWingDoor.SealClosed();
        techWingDoor.SetSealedInteraction(player =>
        {
            if (!RunState.ProgrammerRouteNeedsTechWing)
            {
                DialogueUI.Instance.Show("Технологическое крыло закрыто. Здесь пока нет активной задачи.", 2f);
                return;
            }
            techWingDoor.UnsealAndOpen(player);
        });
    }

    private void ConfigureGardenDoor()
    {
        if (gardenDoor == null) return;

        gardenDoor.SealClosed();
        gardenDoor.SetSealedInteraction(player =>
        {
            if (!RunState.HasEvidence(EvidenceId.StaffSmokeBreakSchedule))
            {
                DialogueUI.Instance.Show("Вход в сад заперт. Нужно узнать безопасное окно у Ракель.", 2.2f);
                return;
            }

            gardenDoor.UnsealAndOpen(player);
            RunState.MarkGardenAccessOpened();
        });
    }

    public void OpenGardenForRaquelMeeting(Player player)
    {
        if (gardenDoor != null) gardenDoor.ForceOpen();
        RunState.CompleteRaquelGardenMeeting();

        if (player != null)
        {
            player.TeleportToCell(BlockCPlayableLayout.GardenMeetingInterior);
            Camera mainCamera = Camera.main;
            CameraFollow follow = mainCamera != null ? mainCamera.GetComponent<CameraFollow>() : null;
            if (follow != null) follow.SnapToTarget();
        }

        DialogueUI.Instance.ShowDialogueSequence(
            new DialogueUI.DialogueLine("Ракель", "Пошли. Медленно. Если кто-то спросит — ты несёшь мои вещи.", "girl"),
            new DialogueUI.DialogueLine("Ракель", "Ты вытащил меня на эксперименте. Это было полезно. Не обязательно умно, но полезно.", "girl"),
            new DialogueUI.DialogueLine("Ракель", "Тут не монастырь. Здесь выживает сильнейший, а сострадание чаще всего просто слабость с красивым названием.", "girl"),
            new DialogueUI.DialogueLine("Ракель", "Я пока не знаю, можно ли тебе доверять. Поэтому дам информацию и посмотрю, как ты ей воспользуешься.", "girl"),
            new DialogueUI.DialogueLine("Ракель", "Повара выходят в сад в 18:00, охрана — в 19:15, учёные — в 20:00. Слушай, но не попадайся.", "girl"),
            new DialogueUI.DialogueLine("Ракель", "И держи имплант. На тридцать секунд будешь выглядеть как охранник. Потом пять минут он бесполезен.", "girl"),
            new DialogueUI.DialogueLine("Мысль", "<color=#75D99A>Получено: расписание персонала и маскировочный имплант. Клавиша T активирует маскировку.</color>", null));
    }

    private void CreateEngineeringPuzzle(PrisonDoor entrance)
    {
        var puzzleObject = new GameObject("Engineering Circuit Puzzle");
        puzzleObject.transform.SetParent(transform);
        var puzzle = puzzleObject.AddComponent<EngineeringCircuitPuzzle>();
        puzzle.Initialize(
            this,
            entrance,
            LoadArt("console"),
            CreateSquareSprite(),
            new Vector2Int(108, 41),
            BlockCPlayableLayout.EngineeringArea,
            BlockCPlayableLayout.EngineeringSecretPassage());
        roomObjectives.Add(puzzle);
    }

    private void CreateProgrammerTechPuzzles()
    {
        Sprite console = LoadArt("console");
        Sprite square = CreateSquareSprite();

        CreateProgrammerPuzzle(
            "Block C Data Source Puzzle",
            PrisonItemId.DataSource,
            "Цепь источника данных замкнута. Получено: источник данных системы.",
            new[]
            {
                new CircuitNodeSpec("Источник питания", BlockCPlayableLayout.F2(132, 54), WireDirection.Right, 0, false, source: true),
                new CircuitNodeSpec("Панель данных 1", BlockCPlayableLayout.F2(133, 54), WireDirection.Left | WireDirection.Right, 1, true),
                new CircuitNodeSpec("Панель данных 2", BlockCPlayableLayout.F2(134, 54), WireDirection.Left | WireDirection.Up, 1, true),
                new CircuitNodeSpec("Источник данных", BlockCPlayableLayout.DataSourceObjective, WireDirection.Down, 0, false, target: true),
            },
            console,
            square);

        CreateProgrammerPuzzle(
            "Archive Compute Access Puzzle",
            PrisonItemId.ComputeModule,
            "Архив открыл вычислительный доступ. Получено: модуль доступа.",
            new[]
            {
                new CircuitNodeSpec("Архивный ввод", BlockCPlayableLayout.F2(144, 53), WireDirection.Right, 0, false, source: true),
                new CircuitNodeSpec("Архивная панель 1", BlockCPlayableLayout.F2(145, 53), WireDirection.Left | WireDirection.Up, 2, true),
                new CircuitNodeSpec("Архивная панель 2", BlockCPlayableLayout.F2(145, 54), WireDirection.Down | WireDirection.Right, 1, true),
                new CircuitNodeSpec("Архивная панель 3", BlockCPlayableLayout.F2(146, 54), WireDirection.Left | WireDirection.Right, 1, true),
                new CircuitNodeSpec("Архивная панель 4", BlockCPlayableLayout.F2(147, 54), WireDirection.Left | WireDirection.Up, 3, true),
                new CircuitNodeSpec("Модуль доступа", BlockCPlayableLayout.ComputeModuleObjective, WireDirection.Down, 0, false, target: true),
            },
            console,
            square);

        CreateProgrammerPuzzle(
            "Signal Amplifier Puzzle",
            PrisonItemId.SignalAmplifier,
            "Релейная цепь стабилизирована. Получено: усилитель сигнала.",
            new[]
            {
                new CircuitNodeSpec("Релейный ввод", BlockCPlayableLayout.F2(144, 37), WireDirection.Right, 0, false, source: true),
                new CircuitNodeSpec("Реле 1", BlockCPlayableLayout.F2(145, 37), WireDirection.Left | WireDirection.Right, 1, true),
                new CircuitNodeSpec("Реле 2", BlockCPlayableLayout.F2(146, 37), WireDirection.Left | WireDirection.Up, 2, true),
                new CircuitNodeSpec("Реле 3", BlockCPlayableLayout.F2(146, 38), WireDirection.Down | WireDirection.Up, 1, true),
                new CircuitNodeSpec("Реле 4", BlockCPlayableLayout.F2(146, 39), WireDirection.Down | WireDirection.Right, 3, true),
                new CircuitNodeSpec("Реле 5", BlockCPlayableLayout.F2(147, 39), WireDirection.Left | WireDirection.Right, 1, true),
                new CircuitNodeSpec("Усилитель сигнала", BlockCPlayableLayout.SignalAmplifierObjective, WireDirection.Left, 0, false, target: true),
            },
            console,
            square);
    }

    private void CreateProgrammerPuzzle(
        string objectName,
        PrisonItemId reward,
        string solvedMessage,
        IEnumerable<CircuitNodeSpec> specs,
        Sprite console,
        Sprite square)
    {
        var puzzleObject = new GameObject(objectName);
        puzzleObject.transform.SetParent(transform);
        var puzzle = puzzleObject.AddComponent<ProgrammerCircuitPuzzle>();
        puzzle.Initialize(this, reward, solvedMessage, specs, console, square);
        roomObjectives.Add(puzzle);
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
        Vector2Int start = RunState.ConsumePrisonReturnSpawn()
            ? BlockCPlayableLayout.ExperimentReturnSpawn
            : PlayerStartCell;
        player.Initialize(this, start.x, start.y);
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

        npc.Initialize(this, BlockCPlayableLayout.ExperimentNpc.x, BlockCPlayableLayout.ExperimentNpc.y);
    }

    private void SpawnProgrammer()
    {
        var programmerObject = new GameObject("Programmer");
        programmerObject.transform.SetParent(transform);
        var programmer = programmerObject.AddComponent<ProgrammerNPC>();
        programmer.SetSpriteResource("npc_programmer");
        programmer.Initialize(this, BlockCPlayableLayout.ProgrammerSpawn.x, BlockCPlayableLayout.ProgrammerSpawn.y);
    }

    private void SpawnCompetitor()
    {
        var competitorObject = new GameObject("Competitor");
        competitorObject.transform.SetParent(transform);
        var competitor = competitorObject.AddComponent<CompetitorNPC>();
        competitor.SetSpriteResource("girl");
        competitor.Initialize(this, BlockCPlayableLayout.CompetitorSpawn.x, BlockCPlayableLayout.CompetitorSpawn.y);
    }

    private void SpawnMedicMechanic()
    {
        var medicObject = new GameObject("MedicMechanic");
        medicObject.transform.SetParent(transform);
        var medic = medicObject.AddComponent<MedicMechanicNPC>();
        medic.SetSpriteResource("inmate_c1752");
        medic.Initialize(this, BlockCPlayableLayout.MedicMechanicSpawn.x, BlockCPlayableLayout.MedicMechanicSpawn.y);
    }

    private void SpawnSecondFloorInmates()
    {
        SpawnAmbientInmate(
            "Заключённый второго этажа",
            "Сверху лучше видно, кого ведут на эксперимент. Но центр наблюдения видит нас ещё лучше.",
            BlockCPlayableLayout.F2(24, 50));
        SpawnAmbientInmate(
            "Молчаливый заключённый",
            "Восточное и западное крылья закрыты уже давно. Никто не говорит, что там было.",
            BlockCPlayableLayout.F2(46, 20));
    }

    private void SpawnAmbientInmate(string displayName, string line, Vector2Int cell)
    {
        var go = new GameObject(displayName);
        go.transform.SetParent(transform);
        var inmate = go.AddComponent<AmbientInmateNPC>();
        inmate.Configure(displayName, line);
        inmate.Initialize(this, cell.x, cell.y);
    }

    private void SpawnGuards()
    {
        // Если в сцене есть маркеры охраны — строим всю охрану из них (ручной левел-дизайн).
        // Один маркер описывает любой тип: патрульного или конвойный пост.
        var guardMarkers = FindObjectsByType<GuardSpawnMarker>(FindObjectsSortMode.None);
        if (guardMarkers.Length > 0)
        {
            foreach (GuardSpawnMarker marker in guardMarkers)
            {
                if (marker.kind == GuardKind.ScheduleEnforcer)
                {
                    CreateScheduleEnforcerGuard(
                        string.IsNullOrEmpty(marker.guardName) ? "Надзиратель" : marker.guardName,
                        WorldToGrid(marker.transform.position));
                }
                else
                {
                    CreateGuardFromMarker(marker);
                }
            }
            return;
        }

        // Иначе — вся охрана по умолчанию из общего источника PrisonDefaults
        // (его же читает editor-«запекатель»).
        foreach (DefaultGuard guard in PrisonDefaults.Guards())
        {
            if (guard.Kind == GuardKind.ScheduleEnforcer)
                CreateScheduleEnforcerGuard(guard.Name, guard.Cell);
            else
                CreateGuard(guard.Name, guard.Route);
        }
    }

    private void CreateGuard(string displayName, PatrolWaypoint[] route)
    {
        var go = new GameObject(displayName);
        go.transform.SetParent(transform);
        var guard = go.AddComponent<GuardPatrol>();
        Sprite guardSprite = LoadArt("guard");
        guard.Initialize(this, route, guardSprite != null ? guardSprite : CreateSquareSprite(), tintSprite: guardSprite == null);
    }

    /// <summary>Создать охранника из маркера сцены: маршрут = дочерние WaypointMarker по порядку.</summary>
    private void CreateGuardFromMarker(GuardSpawnMarker marker)
    {
        var waypoints = marker.CollectWaypoints();
        PatrolWaypoint[] route;
        if (waypoints.Count == 0)
        {
            // Без точек — стационарный пост на клетке самого маркера.
            route = new[] { new PatrolWaypoint(WorldToGrid(marker.transform.position), scan: true) };
        }
        else
        {
            route = new PatrolWaypoint[waypoints.Count];
            for (int i = 0; i < waypoints.Count; i++)
            {
                route[i] = new PatrolWaypoint(WorldToGrid(waypoints[i].transform.position), waypoints[i].scan);
            }
        }

        string displayName = string.IsNullOrEmpty(marker.guardName) ? "Надзиратель" : marker.guardName;
        CreateGuard(displayName, route);
    }

    /// <summary>Ручные препятствия: каждый CoverObstacleMarker превращает свою клетку в Cover.</summary>
    private void ApplyObstacleMarkers()
    {
        foreach (CoverObstacleMarker marker in FindObjectsByType<CoverObstacleMarker>(FindObjectsSortMode.None))
        {
            Vector2Int cell = WorldToGrid(marker.transform.position);
            if (cell.x < 0 || cell.x >= width || cell.y < 0 || cell.y >= height) continue;
            SetTile(cell.x, cell.y, TileType.Cover);
        }
    }

    /// <summary>
    /// Расставляет ящики-укрытия: проп, в который игрок прячется (E) и становится
    /// невидимым для охраны. Один стоит прямо в служебном коридоре с патрулём.
    /// </summary>
    private void SpawnHideSpots()
    {
        // Если в сцене расставлены маркеры ящиков — берём их (ручной левел-дизайн).
        var hideMarkers = FindObjectsByType<HideSpotMarker>(FindObjectsSortMode.None);
        if (hideMarkers.Length > 0)
        {
            foreach (HideSpotMarker marker in hideMarkers)
            {
                Vector2Int cell = WorldToGrid(marker.transform.position);
                AddHideSpot(cell.x, cell.y, string.IsNullOrEmpty(marker.label) ? "Ящик" : marker.label);
            }
            return;
        }

        // Иначе — ящики по умолчанию из общего источника PrisonDefaults.
        foreach (DefaultHideSpot spot in PrisonDefaults.HideSpots())
        {
            AddHideSpot(spot.Cell.x, spot.Cell.y, spot.Label);
        }
    }

    private void AddHideSpot(int x, int y, string displayName)
    {
        // Прячемся только на проходимой клетке — иначе игрок не сможет туда встать.
        if (!IsWalkable(x, y)) return;
        hideSpots.Add(new Vector2Int(x, y));

        var crate = new GameObject(displayName);
        crate.transform.SetParent(transform);
        crate.transform.position = GridToWorld(x, y);

        Sprite sprite = CrateSprite();
        var sr = crate.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = Color.white;
        // Масштаб под клетку (ящик чуть меньше клетки, как у персонажей).
        float spriteUnit = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
        crate.transform.localScale = Vector3.one * cellSize * 0.92f / Mathf.Max(0.0001f, spriteUnit);
        // Сортируем как объект мира (стоит на полу, не «вшит» в него).
        sr.sortingOrder = SortingLayers.Entity(crate.transform.position.y);
        CharacterGroundShadow.Attach(crate);
    }

    /// <summary>Является ли клетка укрытием, где можно спрятаться.</summary>
    public bool IsHideSpot(Vector2Int cell) => hideSpots.Contains(cell);

    private Sprite crateSprite;
    private Sprite hedgeSprite;

    /// <summary>Спрайт ящика-укрытия: AI-арт crate_hide, иначе процедурный fallback.</summary>
    private Sprite CrateSprite()
    {
        if (crateSprite != null) return crateSprite;

        Sprite art = LoadArt("crate_hide");
        if (art != null)
        {
            crateSprite = art;
            return art;
        }

        const int s = 32;
        var tex = new Texture2D(s, s) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[s * s];

        Color wood = new Color(0.55f, 0.38f, 0.20f);
        Color dark = new Color(0.30f, 0.19f, 0.09f);
        Color light = new Color(0.69f, 0.50f, 0.29f);

        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                Color c = wood;
                bool border = x < 2 || x >= s - 2 || y < 2 || y >= s - 2;
                bool brace = Mathf.Abs(x - y) < 2 || Mathf.Abs(x + y - (s - 1)) < 2; // X-распорка
                bool plank = x % 10 == 0 || y % 10 == 0;                              // стыки досок

                if (border) c = dark;
                else if (brace) c = light;
                else if (plank) c = dark;

                px[y * s + x] = c;
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        crateSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        return crateSprite;
    }

    /// <summary>Процедурный спрайт живой изгороди для садового лабиринта Ракель.</summary>
    private Sprite HedgeSprite()
    {
        if (hedgeSprite != null) return hedgeSprite;

        const int s = 32;
        var tex = new Texture2D(s, s) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[s * s];

        Color dark = new Color(0.05f, 0.16f, 0.09f, 1f);
        Color mid = new Color(0.10f, 0.29f, 0.15f, 1f);
        Color light = new Color(0.26f, 0.47f, 0.28f, 1f);
        Color edge = new Color(0.03f, 0.08f, 0.05f, 1f);

        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                bool border = x < 2 || x >= s - 2 || y < 2 || y >= s - 2;
                int noise = (x * 17 + y * 31 + (x / 3) * 11 + (y / 4) * 7) % 13;
                bool leafCluster = noise < 5 || ((x + y) % 9 == 0);
                bool highlight = (x + y * 2) % 17 == 0 || (x > 6 && x < 24 && y > 5 && y < 13 && noise < 3);

                Color c = border ? edge : leafCluster ? mid : dark;
                if (!border && highlight) c = light;
                px[y * s + x] = c;
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        hedgeSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        return hedgeSprite;
    }

    private void CreateScheduleEnforcerGuard(string displayName, Vector2Int startCell)
    {
        var go = new GameObject(displayName);
        go.transform.SetParent(transform);
        var guard = go.AddComponent<ScheduleEnforcerGuard>();
        Sprite guardSprite = LoadArt("guard");
        guard.Initialize(this, startCell, guardSprite != null ? guardSprite : CreateSquareSprite(), tintSprite: guardSprite == null);
    }

    public void BeginStaffRoomMeeting()
    {
        if (staffRoomMeetingStarted) return;

        staffRoomMeetingStarted = true;
        SealCompetitorServiceDoors();
        if (staffRoomDoor != null)
        {
            staffRoomDoor.SealClosed();
            staffRoomDoor.SetSealedInteraction(OverhearStaffRoomMeeting);
        }

        SpawnStaffRoomMeetingGuard();
    }

    private void SpawnStaffRoomMeetingGuard()
    {
        if (staffRoomMeetingGuardSpawned) return;
        staffRoomMeetingGuardSpawned = true;

        var go = new GameObject("Надзиратель комнаты персонала");
        go.transform.SetParent(transform);
        Vector3 worldPos = GridToWorld(96, 52);
        go.transform.position = worldPos;

        var renderer = go.AddComponent<SpriteRenderer>();
        Sprite guardSprite = LoadArt("guard");
        renderer.sprite = guardSprite != null ? SpriteWalkAnimator.FeetAnchored(guardSprite) : CreateSquareSprite();
        renderer.color = guardSprite != null ? Color.white : new Color(0.78f, 0.12f, 0.10f);
        float spriteSize = guardSprite != null ? GetSpriteSize(guardSprite) : 1f;
        go.transform.localScale = Vector3.one * cellSize * WorldMetrics.GuardScale / Mathf.Max(0.0001f, spriteSize);
        renderer.sortingOrder = SortingLayers.Entity(worldPos.y);
        if (guardSprite != null) SpriteWalkAnimator.TryAttach(go, "guard");
        CharacterGroundShadow.Attach(go);
        CharacterScreenFacing.Attach(go);
    }

    private void OverhearStaffRoomMeeting(Player player)
    {
        if (RunState.CompetitorQuest == CompetitorQuestStage.Overheard)
        {
            staffRoomDoor?.UnsealAndOpen(player);
            DialogueUI.Instance.ShowDialogue(
                "За дверью",
                "В комнате персонала уже тихо. Разговор закончился.",
                "girl");
            return;
        }

        RunState.MarkCompetitorConversationOverheard();
        staffRoomDoor?.UnsealAndOpen(player);
        DialogueUI.Instance.ShowDialogueSequence(
            new DialogueUI.DialogueLine(
                "Ракель",
                "Старый вход в сад закрыли слишком рано.",
                "girl"),
            new DialogueUI.DialogueLine(
                "Надзиратель",
                "Вот ключ от нового. Но если тебя увидят повара, я скажу, что ты украла его сама.",
                "guard"),
            new DialogueUI.DialogueLine(
                "Ракель",
                "Ты сегодня не смотри в сторону комнаты персонала. Получишь своё.",
                "girl"),
            new DialogueUI.DialogueLine(
                "За дверью",
                "Слышен короткий поцелуй.",
                null),
            new DialogueUI.DialogueLine(
                "Мысль",
                "<color=#75D99A>Вы подслушали разговор.</color>\nРакель может пользоваться новым входом в сад и связана с охраной.",
                null));
    }

    private void SpawnCameras()
    {
        // Если в сцене есть маркеры камер — берём их (ручной левел-дизайн).
        var cameraMarkers = FindObjectsByType<CameraSpawnMarker>(FindObjectsSortMode.None);
        if (cameraMarkers.Length > 0)
        {
            foreach (CameraSpawnMarker m in cameraMarkers)
            {
                CreateCamera(string.IsNullOrEmpty(m.cameraName) ? "Камера" : m.cameraName,
                    WorldToGrid(m.transform.position), m.FacingVector, m.zone, m.response, m.range);
            }
            return;
        }

        // Иначе — камеры по умолчанию из общего источника PrisonDefaults
        // (его же читает editor-«запекатель»). По умолчанию они только смотрят
        // (CameraResponse.None); смена на SummonGuards/Alarm делает камеру «живой».
        foreach (DefaultCamera cam in PrisonDefaults.Cameras())
        {
            CreateCamera(cam.Name, cam.Cell, cam.Facing, cam.Zone, cam.Response, cam.Range);
        }
    }

    private void CreateCamera(string displayName, Vector2Int cell, Vector2Int facing, string zone, CameraResponse response, int range = 6)
    {
        var go = new GameObject(displayName);
        go.transform.SetParent(transform);
        var camera = go.AddComponent<SurveillanceCamera>();
        Sprite cameraSprite = LoadArt("camera");
        camera.Initialize(this, cell, facing, range, zone, response,
            cameraSprite != null ? cameraSprite : CreateSquareSprite());
    }

    private void SpawnActiveReactiveCameras()
    {
        foreach (ReactiveSecurityCamera camera in RunState.ActiveReactiveSecurityCameras)
        {
            CreateCamera("Камера усиления", camera.Cell, camera.Facing,
                "reactive-security", CameraResponse.SummonGuards, 7);
            reactiveCameraCells.Add(camera.Cell);
        }
    }

    public void ReportRestrictedIncident(Vector2Int incidentCell, string reason)
    {
        if (!IsRestrictedCell(incidentCell)) return;
        RunState.QueueSecurityCameraIncident(incidentCell);
    }

    public void ApplyPendingSecurityIncidents(bool showMessage = true)
    {
        int created = 0;
        foreach (Vector2Int incidentCell in RunState.ConsumePendingSecurityCameraIncidents())
        {
            if (!IsRestrictedCell(incidentCell)) continue;
            if (HasCameraNear(incidentCell, 5)) continue;
            if (!TryFindReactiveCameraMount(incidentCell, out Vector2Int cameraCell, out Vector2Int facing)) continue;

            CreateCamera("Камера усиления", cameraCell, facing,
                "reactive-security", CameraResponse.SummonGuards, 7);
            reactiveCameraCells.Add(cameraCell);
            RunState.RegisterReactiveSecurityCamera(cameraCell, facing);
            created++;
        }

        if (showMessage && created > 0)
        {
            DialogueUI.Instance.Show("После инцидентов охрана усилила закрытые зоны камерами.", 2.2f);
        }
    }

    private bool HasCameraNear(Vector2Int cell, int maxDistance)
    {
        foreach (SurveillanceCamera camera in FindObjectsByType<SurveillanceCamera>(FindObjectsSortMode.None))
        {
            Vector2Int d = camera.GridPosition - cell;
            if (Mathf.Abs(d.x) + Mathf.Abs(d.y) <= maxDistance) return true;
        }
        return false;
    }

    private bool TryFindReactiveCameraMount(Vector2Int incidentCell, out Vector2Int cameraCell, out Vector2Int facing)
    {
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        for (int radius = 1; radius <= 6; radius++)
        {
            foreach (Vector2Int dir in directions)
            {
                Vector2Int candidate = incidentCell - dir * radius;
                Vector2Int behind = candidate - dir;
                if (!IsWalkable(candidate.x, candidate.y)) continue;
                if (reactiveCameraCells.Contains(candidate)) continue;
                if (!BlocksVision(behind.x, behind.y)) continue;
                if (!VisionMath.CanSeeCell(this, candidate, dir, 7, incidentCell)) continue;

                cameraCell = candidate;
                facing = dir;
                return true;
            }
        }

        cameraCell = default;
        facing = default;
        return false;
    }

    private void CreateDayDirector()
    {
        var directorObject = new GameObject("Day Director");
        directorObject.transform.SetParent(transform);
        directorObject.AddComponent<DayDirector>();
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
        EnsureGridInitialized();
        float worldX = (x - width / 2f + 0.5f) * cellSize;
        float worldY = (y - height / 2f + 0.5f) * cellSize;
        return transform.position + new Vector3(worldX, worldY, 0f);
    }

    /// <summary>Мировая точка → клетка грида (обратное к GridToWorld). Для маркеров левел-дизайна.</summary>
    public Vector2Int WorldToGrid(Vector3 world)
    {
        EnsureGridInitialized();
        Vector3 local = world - transform.position;
        int x = Mathf.RoundToInt(local.x / cellSize + width / 2f - 0.5f);
        int y = Mathf.RoundToInt(local.y / cellSize + height / 2f - 0.5f);
        return new Vector2Int(x, y);
    }

    /// <summary>Привязать мировую точку к центру ближайшей клетки (для отрисовки маркеров).</summary>
    public Vector3 SnapToCell(Vector3 world)
    {
        Vector2Int cell = WorldToGrid(world);
        return GridToWorld(cell.x, cell.y);
    }

    public bool IsWalkable(int x, int y)
    {
        EnsureGridInitialized();
        if (x < 0 || x >= width || y < 0 || y >= height) return false;
        if (solidProps.Contains(new Vector2Int(x, y))) return false;  // мебель непроходима
        return grid[x, y] == TileType.Floor;
    }

    public bool BlocksVision(int x, int y)
    {
        TileType type = GetTileType(x, y);
        return type == TileType.Wall || type == TileType.Cover || type == TileType.Door;
    }

    public void UpdateWallCutaway(Vector3 focusWorldPosition)
    {
        // Reserved for visual wall fading/cutaway. The current prototype keeps
        // walls static, but Player calls this after teleports.
    }

    public bool IsRestrictedCell(Vector2Int cell) => IsRestrictedCell(cell.x, cell.y);

    public bool IsPlayerCell(Vector2Int cell)
    {
        return BlockCPlayableLayout.PlayerCell.Contains(cell.x, cell.y);
    }

    public bool IsRestrictedCell(int x, int y)
    {
        EnsureGridInitialized();

        // Если в сцене есть маркеры зон — они полностью задают закрытые области
        // (можно двигать/удалять). Иначе — базовые зоны из BlockCPlayableLayout.
        if (hasRestrictedMarkers)
        {
            foreach (RectInt zone in extraRestricted)
            {
                if (x >= zone.xMin && x < zone.xMax && y >= zone.yMin && y < zone.yMax) return true;
            }
            return false;
        }

        return BlockCPlayableLayout.IsRestricted(x, y);
    }

    /// <summary>Собрать ручные закрытые зоны из маркеров сцены (раз при старте).</summary>
    private void CollectRestrictedZones()
    {
        extraRestricted.Clear();
        var markers = FindObjectsByType<RestrictedZoneMarker>(FindObjectsSortMode.None);
        hasRestrictedMarkers = markers.Length > 0;
        foreach (RestrictedZoneMarker marker in markers)
        {
            extraRestricted.Add(marker.CellRect(this));
        }
    }

    private static bool IsInside(int x, int y, int minX, int minY, int maxX, int maxY)
    {
        return x >= minX && x <= maxX && y >= minY && y <= maxY;
    }

    public TileType GetTileType(int x, int y)
    {
        EnsureGridInitialized();
        if (x < 0 || x >= width || y < 0 || y >= height) return TileType.Wall;
        return grid[x, y];
    }

    /// <summary>Найти дверь по клетке (для охраны, открывающей двери в погоне).</summary>
    public PrisonDoor DoorAt(Vector2Int cell)
    {
        foreach (PrisonDoor door in doors)
        {
            if (door != null && door.GridPosition == cell) return door;
        }
        return null;
    }

    public bool CanNpcTraverseDoor(Vector2Int cell)
    {
        if (cell == BlockCPlayableLayout.RevisionPanel) return true;

        PrisonDoor door = DoorAt(cell);
        return door != null && door.CanNpcTraverse;
    }

    public void OpenDoorForNpc(Vector2Int cell)
    {
        PrisonDoor door = DoorAt(cell);
        if (door == null || !door.CanNpcTraverse) return;

        if (cell == BlockCPlayableLayout.CompetitorServiceDoor)
        {
            competitorServiceDoor?.ForceOpen();
            return;
        }

        door.ForceOpen();
    }

    public void SetDoorOpen(int x, int y, bool isOpen)
    {
        SetTile(x, y, isOpen ? TileType.Floor : TileType.Door);
    }

    public void SetTileAndRefresh(int x, int y, TileType type)
    {
        TrySetTileAndRefresh(x, y, type);
    }

    public bool TrySetTileAndRefresh(int x, int y, TileType type)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
        {
            Debug.LogWarning($"Skip tile refresh outside grid: ({x}, {y}) -> {type}");
            return false;
        }

        SetTile(x, y, type);
        if (tileObjects == null) return true;

        try
        {
            if (tileObjects[x, y] != null) Destroy(tileObjects[x, y]);
            GameObject tile = CreateTileVisual(x, y, type);
            tile.transform.SetParent(transform);
            tileObjects[x, y] = tile;
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to refresh tile ({x}, {y}) as {type}: {ex}");
            return false;
        }
    }

    public void OpenBlockCShortcut()
    {
        foreach (Vector2Int cell in BlockCPlayableLayout.BlockCShortcut())
        {
            SetTileAndRefresh(cell.x, cell.y, TileType.Floor);
        }
    }

    private void EnsureGridInitialized()
    {
        if (grid == null) InitializeGrid();
    }

#if UNITY_EDITOR
    [Header("Editor")]
    [SerializeField] private bool drawMapPreview = true;

    // Схема карты прямо в Scene-вью (без запуска игры): пол/стены/двери/укрытия
    // из BlockCPlayableLayout. Ориентир для ручной расстановки маркеров.
    private void OnDrawGizmos()
    {
        if (!drawMapPreview) return;
        EnsureGridInitialized(); // выставит width/height = размеры карты

        // Пол комнат — полупрозрачная заливка по областям.
        Gizmos.color = new Color(0.40f, 0.46f, 0.52f, 0.22f);
        foreach (GridArea a in BlockCPlayableLayout.FloorAreas) DrawAreaGizmo(a);

        // Глухие провалы (например, шахта над вторым этажом).
        Gizmos.color = new Color(0f, 0f, 0f, 0.30f);
        foreach (GridArea a in BlockCPlayableLayout.VoidAreas) DrawAreaGizmo(a);

        // Закрытые зоны — красная заливка + контур (видно поверх пола).
        // Если в сцене есть маркеры зон, базовые не рисуем — их заменяют маркеры (рисуют себя сами).
        if (FindFirstObjectByType<RestrictedZoneMarker>() == null)
        {
            foreach (GridArea a in BlockCPlayableLayout.RestrictedAreas)
            {
                Gizmos.color = new Color(0.92f, 0.2f, 0.15f, 0.16f);
                DrawAreaGizmo(a);
                Gizmos.color = new Color(0.97f, 0.27f, 0.2f, 0.6f);
                DrawAreaWireGizmo(a);
            }
        }

        // Внутренние стены.
        Gizmos.color = new Color(0.14f, 0.16f, 0.20f, 0.95f);
        foreach (GridWallLine w in BlockCPlayableLayout.InteriorWalls) DrawLineGizmo(w.Start, w.End);

        // Двери.
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.95f);
        foreach (Vector2Int d in BlockCPlayableLayout.DoorCells) DrawCellGizmo(d.x, d.y, 0.8f);

        // Укрытия (cover).
        Gizmos.color = new Color(0.62f, 0.43f, 0.20f, 0.95f);
        foreach (Vector2Int c in BlockCPlayableLayout.CoverCells) DrawCellGizmo(c.x, c.y, 0.85f);

        // Габариты карты.
        Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.8f);
        Gizmos.DrawWireCube(transform.position, new Vector3(width * cellSize, height * cellSize, 0.1f));
    }

    private void DrawAreaGizmo(GridArea a)
    {
        Vector3 min = GridToWorld(a.MinX, a.MinY);
        Vector3 max = GridToWorld(a.MaxX, a.MaxY);
        Vector3 center = (min + max) * 0.5f;
        Vector3 size = new Vector3(Mathf.Abs(max.x - min.x) + cellSize, Mathf.Abs(max.y - min.y) + cellSize, 0.05f);
        Gizmos.DrawCube(center, size);
    }

    private void DrawAreaWireGizmo(GridArea a)
    {
        Vector3 min = GridToWorld(a.MinX, a.MinY);
        Vector3 max = GridToWorld(a.MaxX, a.MaxY);
        Vector3 center = (min + max) * 0.5f;
        Vector3 size = new Vector3(Mathf.Abs(max.x - min.x) + cellSize, Mathf.Abs(max.y - min.y) + cellSize, 0.05f);
        Gizmos.DrawWireCube(center, size);
    }

    private void DrawLineGizmo(Vector2Int start, Vector2Int end)
    {
        int minX = Mathf.Min(start.x, end.x), maxX = Mathf.Max(start.x, end.x);
        int minY = Mathf.Min(start.y, end.y), maxY = Mathf.Max(start.y, end.y);
        Vector3 a = GridToWorld(minX, minY);
        Vector3 b = GridToWorld(maxX, maxY);
        Vector3 center = (a + b) * 0.5f;
        Gizmos.DrawCube(center, new Vector3(Mathf.Abs(b.x - a.x) + cellSize, Mathf.Abs(b.y - a.y) + cellSize, 0.06f));
    }

    private void DrawCellGizmo(int x, int y, float fill)
    {
        Gizmos.DrawCube(GridToWorld(x, y), new Vector3(cellSize * fill, cellSize * fill, 0.07f));
    }
#endif
}
