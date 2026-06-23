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
    private readonly List<PrisonDoor> doors = new List<PrisonDoor>();
    private PrisonDoor staffRoomDoor;
    private PrisonDoor gardenDoor;
    private PrisonDoor techWingDoor;
    private PrisonDoor kitchenShortcutDoor;
    private PrisonDoor competitorServiceDoor;
    private PrisonDoor competitorServiceExitDoor;
    private bool staffRoomMeetingStarted;
    private bool staffRoomMeetingGuardSpawned;

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
        SpawnProgrammer();
        SpawnCompetitor();
        SpawnSecondFloorInmates();
        SpawnGuards();
        SpawnCameras();
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
            CreateWallVisual(tile);
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

    // Плоская стена top-down: один тайл в клетку, фиксированно над полом и под
    // сущностями. Никакой геометрической высоты — ничего не перекрывается.
    private void CreateWallVisual(GameObject parent)
    {
        var renderer = parent.AddComponent<SpriteRenderer>();
        renderer.sprite = wallTopSprite != null ? wallTopSprite : CreateSquareSprite();
        renderer.color = wallTopSprite != null ? Color.white : wallTopColor;
        renderer.sortingOrder = SortingLayers.WallFlat;
        parent.transform.localScale = wallTopSprite != null
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
        renderer.sortingOrder = SortingLayers.Entity(worldPos.y);
    }

    private void CreateMapContent()
    {
        CreateResidentialDoors();

        CreateDoor("Столовая", 13, 20, PrisonItemId.None);
        gardenDoor = CreateDoor("Вход в сад", BlockCPlayableLayout.GardenDoor, PrisonItemId.None);
        CreateDoor("Отправление на эксперименты", 31, 53, PrisonItemId.Unavailable);
        CreateDoor("Санитарно-бытовое крыло", 50, 21, PrisonItemId.None);

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
        competitorServiceExitDoor = CreateDoor("Служебный выход санитарного крыла", 87, 27, PrisonItemId.None);

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
        CreateDoor("Архив данных", BlockCPlayableLayout.ArchiveDoor, PrisonItemId.None);
        CreateDoor("Релейная комната", BlockCPlayableLayout.RelayDoor, PrisonItemId.None);

        SealCompetitorServiceDoors();
        ConfigureTechWingDoor();

        CreatePickup(PrisonItemId.KitchenManifest, BlockCPlayableLayout.KitchenManifest.x, BlockCPlayableLayout.KitchenManifest.y);
        CreatePickup(PrisonItemId.ServiceBadge, BlockCPlayableLayout.ServiceBadge.x, BlockCPlayableLayout.ServiceBadge.y);
        CreatePickup(PrisonItemId.EyeImplant, BlockCPlayableLayout.EyeImplant.x, BlockCPlayableLayout.EyeImplant.y);
        CreatePickup(PrisonItemId.Transmitter, BlockCPlayableLayout.Transmitter.x, BlockCPlayableLayout.Transmitter.y);
        CreatePickup(PrisonItemId.ExperimentReports, BlockCPlayableLayout.ExperimentReports.x, BlockCPlayableLayout.ExperimentReports.y);

        CreateBed();
        CreateGardenSmokeSpot();
        CreateShortcutLock();
        CreateFloorTransitions();
        CreateObservationCenters();
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
        spot.Initialize(this, BlockCPlayableLayout.GardenSmokeSpot, LoadArt("console") ?? CreateSquareSprite());
    }

    private void CreateShortcutLock()
    {
        var go = new GameObject("Замок shortcut блока C");
        go.transform.SetParent(transform);
        var shortcut = go.AddComponent<ShortcutLock>();
        shortcut.Initialize(this, BlockCPlayableLayout.BlockCShortcutLock, LoadArt("console") ?? CreateSquareSprite());
    }

    private void CreateFloorTransitions()
    {
        CreatePortal("Западная лестница: наверх", BlockCPlayableLayout.WestStairFloor1, BlockCPlayableLayout.WestStairFloor2);
        CreatePortal("Восточная лестница: наверх", BlockCPlayableLayout.EastStairFloor1, BlockCPlayableLayout.EastStairFloor2);
        CreatePortal("Западная лестница: вниз", BlockCPlayableLayout.WestStairFloor2, BlockCPlayableLayout.WestStairFloor1);
        CreatePortal("Восточная лестница: вниз", BlockCPlayableLayout.EastStairFloor2, BlockCPlayableLayout.EastStairFloor1);
    }

    private void CreatePortal(string objectName, Vector2Int cell, Vector2Int destination)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(transform);
        var portal = go.AddComponent<GridPortal>();
        portal.Initialize(this, cell, destination, LoadArt("console") ?? CreateSquareSprite());
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
        renderer.sprite = LoadArt("console") ?? CreateSquareSprite();
        renderer.color = new Color(0.32f, 0.55f, 0.58f, alpha);
        renderer.sortingOrder = SortingLayers.WallFlat + 1;
        float spriteSize = Mathf.Max(renderer.sprite.bounds.size.x, renderer.sprite.bounds.size.y);
        go.transform.localScale = new Vector3(8f, 6f, 1f) / Mathf.Max(0.0001f, spriteSize);
    }

    private void SealCompetitorServiceDoors()
    {
        SealCompetitorDoor(competitorServiceDoor);
        SealCompetitorDoor(competitorServiceExitDoor);
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
                DialogueUI.Instance.Show("Вход в сад заперт. Нужно узнать безопасное окно у заключённой.", 2.2f);
                return;
            }

            gardenDoor.UnsealAndOpen(player);
            RunState.MarkGardenAccessOpened();
        });
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
                new CircuitNodeSpec("Источник питания", new Vector2Int(132, 54), WireDirection.Right, 0, false, source: true),
                new CircuitNodeSpec("Панель данных 1", new Vector2Int(133, 54), WireDirection.Left | WireDirection.Right, 1, true),
                new CircuitNodeSpec("Панель данных 2", new Vector2Int(134, 54), WireDirection.Left | WireDirection.Up, 1, true),
                new CircuitNodeSpec("Источник данных", new Vector2Int(134, 55), WireDirection.Down, 0, false, target: true),
            },
            console,
            square);

        CreateProgrammerPuzzle(
            "Archive Compute Access Puzzle",
            PrisonItemId.ComputeModule,
            "Архив открыл вычислительный доступ. Получено: модуль доступа.",
            new[]
            {
                new CircuitNodeSpec("Архивный ввод", new Vector2Int(144, 53), WireDirection.Right, 0, false, source: true),
                new CircuitNodeSpec("Архивная панель 1", new Vector2Int(145, 53), WireDirection.Left | WireDirection.Up, 2, true),
                new CircuitNodeSpec("Архивная панель 2", new Vector2Int(145, 54), WireDirection.Down | WireDirection.Right, 1, true),
                new CircuitNodeSpec("Архивная панель 3", new Vector2Int(146, 54), WireDirection.Left | WireDirection.Right, 1, true),
                new CircuitNodeSpec("Архивная панель 4", new Vector2Int(147, 54), WireDirection.Left | WireDirection.Up, 3, true),
                new CircuitNodeSpec("Модуль доступа", new Vector2Int(147, 55), WireDirection.Down, 0, false, target: true),
            },
            console,
            square);

        CreateProgrammerPuzzle(
            "Signal Amplifier Puzzle",
            PrisonItemId.SignalAmplifier,
            "Релейная цепь стабилизирована. Получено: усилитель сигнала.",
            new[]
            {
                new CircuitNodeSpec("Релейный ввод", new Vector2Int(144, 37), WireDirection.Right, 0, false, source: true),
                new CircuitNodeSpec("Реле 1", new Vector2Int(145, 37), WireDirection.Left | WireDirection.Right, 1, true),
                new CircuitNodeSpec("Реле 2", new Vector2Int(146, 37), WireDirection.Left | WireDirection.Up, 2, true),
                new CircuitNodeSpec("Реле 3", new Vector2Int(146, 38), WireDirection.Down | WireDirection.Up, 1, true),
                new CircuitNodeSpec("Реле 4", new Vector2Int(146, 39), WireDirection.Down | WireDirection.Right, 3, true),
                new CircuitNodeSpec("Реле 5", new Vector2Int(147, 39), WireDirection.Left | WireDirection.Right, 1, true),
                new CircuitNodeSpec("Усилитель сигнала", new Vector2Int(148, 39), WireDirection.Left, 0, false, target: true),
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
        CreateScheduleEnforcerGuard("Надзиратель общей зоны", new Vector2Int(31, 48));
        CreateScheduleEnforcerGuard(
            "Надзиратель галереи второго этажа",
            BlockCPlayableLayout.F2(31, 48));

        CreateGuard("Надзиратель служебного коридора", new[]
        {
            new Vector2Int(90, 46), new Vector2Int(103, 46)
        });

        CreateGuard("Надзиратель защищённого коридора", new[]
        {
            new Vector2Int(104, 58), new Vector2Int(126, 58)
        });

        CreateGuard("Надзиратель блока C", new[]
        {
            new Vector2Int(132, 52), new Vector2Int(137, 58)
        });

        CreateGuard("Надзиратель архива данных", new[]
        {
            new Vector2Int(144, 52), new Vector2Int(150, 58)
        });

        CreateGuard("Надзиратель релейной", new[]
        {
            new Vector2Int(144, 36), new Vector2Int(150, 42)
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
                "Заключённая",
                "Старый вход в сад закрыли слишком рано.",
                "girl"),
            new DialogueUI.DialogueLine(
                "Надзиратель",
                "Вот ключ от нового. Но если тебя увидят повара, я скажу, что ты украла его сама.",
                "guard"),
            new DialogueUI.DialogueLine(
                "Заключённая",
                "Ты сегодня не смотри в сторону комнаты персонала. Получишь своё.",
                "girl"),
            new DialogueUI.DialogueLine(
                "За дверью",
                "Слышен короткий поцелуй.",
                null),
            new DialogueUI.DialogueLine(
                "Мысль",
                "<color=#75D99A>Вы подслушали разговор.</color>\nУ заключённой есть ключ от нового входа в сад и личная связь с охраной.",
                null));
    }

    private void SpawnCameras()
    {
        // Блок наблюдения: по умолчанию камеры только смотрят и нагнетают (CameraResponse.None).
        // Хук готов — чтобы сделать камеру в зоне «живой», достаточно сменить response
        // на SummonGuards (призыв охраны) или Alarm (глобальная тревога).
        // Камеры монтируются НА стенах и смотрят внутрь помещения.
        CreateCamera("Камера: общая зона", new Vector2Int(31, 50), Vector2Int.down, "common-area", CameraResponse.None);
        CreateCamera("Камера: санитарное крыло", new Vector2Int(67, 34), Vector2Int.down, "sanitary", CameraResponse.None);
        CreateCamera("Камера: служебный коридор", new Vector2Int(96, 47), Vector2Int.down, "staff-corridor", CameraResponse.None);
        CreateCamera("Камера: кухня", new Vector2Int(78, 45), Vector2Int.right, "kitchen", CameraResponse.None);
    }

    private void CreateCamera(string displayName, Vector2Int cell, Vector2Int facing, string zone, CameraResponse response)
    {
        var go = new GameObject(displayName);
        go.transform.SetParent(transform);
        var camera = go.AddComponent<SurveillanceCamera>();
        Sprite cameraSprite = LoadArt("camera");
        camera.Initialize(this, cell, facing, 6, zone, response,
            cameraSprite != null ? cameraSprite : CreateSquareSprite());
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
        return BlockCPlayableLayout.IsRestricted(x, y);
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
        PrisonDoor door = DoorAt(cell);
        return door != null && door.CanNpcTraverse;
    }

    public void OpenDoorForNpc(Vector2Int cell)
    {
        PrisonDoor door = DoorAt(cell);
        if (door == null || !door.CanNpcTraverse) return;

        if (cell == BlockCPlayableLayout.CompetitorServiceDoor ||
            cell == new Vector2Int(87, 27))
        {
            competitorServiceDoor?.ForceOpen();
            competitorServiceExitDoor?.ForceOpen();
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
        SetTile(x, y, type);
        if (tileObjects == null || x < 0 || x >= width || y < 0 || y >= height) return;

        if (tileObjects[x, y] != null) Destroy(tileObjects[x, y]);
        GameObject tile = CreateTileVisual(x, y, type);
        tile.transform.SetParent(transform);
        tileObjects[x, y] = tile;
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
    private void OnDrawGizmosSelected()
    {
        int previewWidth = Mathf.Max(width, BlockCPlayableLayout.Width);
        int previewHeight = Mathf.Max(height, BlockCPlayableLayout.Height);
        Gizmos.color = Color.gray;
        Gizmos.DrawWireCube(transform.position, new Vector3(previewWidth * cellSize, previewHeight * cellSize, 0.1f));
    }
#endif
}
