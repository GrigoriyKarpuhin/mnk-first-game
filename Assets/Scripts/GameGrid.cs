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
    public static readonly Vector2Int PlayerStartCell = new Vector2Int(5, 2);

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
        width = Mathf.Max(width, 78);
        height = Mathf.Max(height, 34);
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
        CarveRoom(44, 15, 51, 23);    // Garden: future hub between wings.
        CarveRoom(53, 14, 60, 24);    // Block C prototype wing.
        CarvePassage(61, 20, 62, 20);  // Block C to data archive.
        CarveRoom(63, 17, 72, 25);    // Data archive: programmer route room.
        CarvePassage(67, 13, 67, 16); // Archive to relay room.
        CarveRoom(63, 4, 72, 12);     // Signal relay: programmer route room.

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
        AddCover(8, 25);
        AddCover(10, 25);
        AddCover(46, 18);
        AddCover(49, 21);
        AddCover(55, 17);
        AddCover(57, 22);
        AddCover(55, 20);
        AddCover(59, 16);
        AddCover(63, 22);
        AddCover(66, 23);
        AddCover(70, 21);
        AddCover(65, 11);
        AddCover(68, 10);
        AddCover(71, 6);

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
        AddDoorTile(43, 17);           // Kitchen/service wing to garden.
        AddDoorTile(52, 18);           // Garden to Block C.
        AddDoorTile(62, 20);           // Block C to data archive.
        AddDoorTile(67, 16);           // Data archive to relay corridor.
        AddDoorTile(67, 13);           // Relay corridor to signal room.
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
        CreateDoor("Камера 1", 7, 2, PrisonItemId.None);
        CreateDoor("Камера 2", 7, 5, PrisonItemId.None);
        CreateDoor("Камера 3", 7, 8, PrisonItemId.None);
        CreateDoor("Камера 4", 7, 11, PrisonItemId.None);
        CreateDoor("Туалет", 27, 8, PrisonItemId.None);
        CreateDoor("Вентиляционная решётка", 34, 10, PrisonItemId.Screwdriver);
        CreateDoor("Выход вентиляции", 35, 15, PrisonItemId.None);
        CreateDoor("Дверь кухни", 37, 17, PrisonItemId.None);
        staffRoomDoor = CreateDoor("Комната персонала", 29, 19, PrisonItemId.None);
        CreateDoor("Склад", 20, 17, PrisonItemId.KitchenManifest);
        CreateDoor("Выход из склада в защищённый коридор", 13, 17, PrisonItemId.ServiceBadge);
        CreateDoor("Лаборатория", 4, 19, PrisonItemId.Unavailable);
        PrisonDoor engineeringEntrance = CreateDoor("Инженерная зона", 10, 19, PrisonItemId.ServiceBadge);
        gardenDoor = CreateDoor("Вход в сад", 43, 17, PrisonItemId.None);
        CreateDoor("Блок C", 52, 18, PrisonItemId.None);
        CreateDoor("Архив данных", 62, 20, PrisonItemId.None);
        CreateDoor("Коридор релейной", 67, 16, PrisonItemId.None);
        CreateDoor("Релейная комната", 67, 13, PrisonItemId.None);

        CreatePickup(PrisonItemId.KitchenManifest, 29, 22);
        CreatePickup(PrisonItemId.ServiceBadge, 17, 20);
        CreatePickup(PrisonItemId.EyeImplant, 8, 21);
        CreatePickup(PrisonItemId.Transmitter, 12, 26);
        CreatePickup(PrisonItemId.ExperimentReports, 4, 24);

        CreateBed();
        CreateGardenSmokeSpot();
        CreateShortcutLock();
        ConfigureGardenDoor();
        CreateEngineeringPuzzle(engineeringEntrance);
        CreateProgrammerTechPuzzles();
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
        bed.Initialize(this, new Vector2Int(4, 2), LoadArt("bed"), CreateSquareSprite());
    }

    private void CreateGardenSmokeSpot()
    {
        var go = new GameObject("Точка подслушивания в саду");
        go.transform.SetParent(transform);
        var spot = go.AddComponent<GardenSmokeSpot>();
        spot.Initialize(this, new Vector2Int(47, 20), LoadArt("console") ?? CreateSquareSprite());
    }

    private void CreateShortcutLock()
    {
        var go = new GameObject("Замок shortcut блока C");
        go.transform.SetParent(transform);
        var shortcut = go.AddComponent<ShortcutLock>();
        shortcut.Initialize(this, new Vector2Int(54, 14), LoadArt("console") ?? CreateSquareSprite());
    }

    private void ConfigureGardenDoor()
    {
        if (gardenDoor == null) return;
        if (RunState.HasEvidence(EvidenceId.StaffSmokeBreakSchedule) ||
            RunState.ProgrammerRouteNeedsTechWing)
        {
            return;
        }

        gardenDoor.SealClosed();
        gardenDoor.SetSealedInteraction(player =>
        {
            if (!RunState.HasEvidence(EvidenceId.StaffSmokeBreakSchedule) &&
                !RunState.ProgrammerRouteNeedsTechWing)
            {
                DialogueUI.Instance.Show("Вход в сад заперт. Нужны расписание или технический доступ программиста.", 2.2f);
                return;
            }

            gardenDoor.UnsealAndOpen(player);
        });
    }

    private void CreateEngineeringPuzzle(PrisonDoor entrance)
    {
        var puzzleObject = new GameObject("Engineering Circuit Puzzle");
        puzzleObject.transform.SetParent(transform);
        var puzzle = puzzleObject.AddComponent<EngineeringCircuitPuzzle>();
        puzzle.Initialize(this, entrance, LoadArt("console"), CreateSquareSprite());
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
                new CircuitNodeSpec("Источник питания", new Vector2Int(56, 18), WireDirection.Right, 0, false, source: true),
                new CircuitNodeSpec("Панель данных 1", new Vector2Int(57, 18), WireDirection.Left | WireDirection.Right, 1, true),
                new CircuitNodeSpec("Панель данных 2", new Vector2Int(58, 18), WireDirection.Left | WireDirection.Up, 1, true),
                new CircuitNodeSpec("Источник данных", new Vector2Int(58, 19), WireDirection.Down, 0, false, target: true),
            },
            console,
            square);

        CreateProgrammerPuzzle(
            "Archive Compute Access Puzzle",
            PrisonItemId.ComputeModule,
            "Архив открыл вычислительный доступ. Получено: модуль доступа.",
            new[]
            {
                new CircuitNodeSpec("Архивный ввод", new Vector2Int(64, 19), WireDirection.Right, 0, false, source: true),
                new CircuitNodeSpec("Архивная панель 1", new Vector2Int(65, 19), WireDirection.Left | WireDirection.Up, 2, true),
                new CircuitNodeSpec("Архивная панель 2", new Vector2Int(65, 20), WireDirection.Down | WireDirection.Right, 1, true),
                new CircuitNodeSpec("Архивная панель 3", new Vector2Int(66, 20), WireDirection.Left | WireDirection.Right, 1, true),
                new CircuitNodeSpec("Архивная панель 4", new Vector2Int(67, 20), WireDirection.Left | WireDirection.Up, 3, true),
                new CircuitNodeSpec("Модуль доступа", new Vector2Int(67, 21), WireDirection.Down, 0, false, target: true),
            },
            console,
            square);

        CreateProgrammerPuzzle(
            "Signal Amplifier Puzzle",
            PrisonItemId.SignalAmplifier,
            "Релейная цепь стабилизирована. Получено: усилитель сигнала.",
            new[]
            {
                new CircuitNodeSpec("Релейный ввод", new Vector2Int(64, 6), WireDirection.Right, 0, false, source: true),
                new CircuitNodeSpec("Реле 1", new Vector2Int(65, 6), WireDirection.Left | WireDirection.Right, 1, true),
                new CircuitNodeSpec("Реле 2", new Vector2Int(66, 6), WireDirection.Left | WireDirection.Up, 2, true),
                new CircuitNodeSpec("Реле 3", new Vector2Int(66, 7), WireDirection.Down | WireDirection.Up, 1, true),
                new CircuitNodeSpec("Реле 4", new Vector2Int(66, 8), WireDirection.Down | WireDirection.Right, 3, true),
                new CircuitNodeSpec("Реле 5", new Vector2Int(67, 8), WireDirection.Left | WireDirection.Right, 1, true),
                new CircuitNodeSpec("Усилитель сигнала", new Vector2Int(68, 8), WireDirection.Left, 0, false, target: true),
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
        player.Initialize(this, PlayerStartCell.x, PlayerStartCell.y);
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

    private void SpawnProgrammer()
    {
        var programmerObject = new GameObject("Programmer");
        programmerObject.transform.SetParent(transform);
        var programmer = programmerObject.AddComponent<ProgrammerNPC>();
        programmer.SetSpriteResource("npc_programmer");
        programmer.Initialize(this, 11, 3);
    }

    private void SpawnCompetitor()
    {
        var competitorObject = new GameObject("Competitor");
        competitorObject.transform.SetParent(transform);
        var competitor = competitorObject.AddComponent<CompetitorNPC>();
        competitor.SetSpriteResource("girl");
        competitor.Initialize(this, 5, 11);
    }

    private void SpawnGuards()
    {
        CreateScheduleEnforcerGuard("Надзиратель общей зоны", new Vector2Int(24, 12));

        CreateGuard("Надзиратель служебного коридора", new[]
        {
            new Vector2Int(22, 17), new Vector2Int(34, 17)
        });

        CreateGuard("Надзиратель защищённого коридора", new[]
        {
            new Vector2Int(3, 17), new Vector2Int(11, 17)
        });

        CreateGuard("Надзиратель блока C", new[]
        {
            new Vector2Int(54, 16), new Vector2Int(59, 22)
        });

        CreateGuard("Надзиратель архива данных", new[]
        {
            new Vector2Int(64, 20), new Vector2Int(71, 20)
        });

        CreateGuard("Надзиратель релейной", new[]
        {
            new Vector2Int(64, 7), new Vector2Int(71, 10)
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
        Vector3 worldPos = GridToWorld(28, 21);
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
            DialogueUI.Instance.ShowDialogue(
                "За дверью",
                "В комнате персонала уже тихо. Разговор закончился.",
                "girl");
            return;
        }

        RunState.MarkCompetitorConversationOverheard();
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
        CreateCamera("Камера: общая зона", new Vector2Int(18, 13), Vector2Int.down, "common-area", CameraResponse.None);
        CreateCamera("Камера: служебный коридор", new Vector2Int(28, 19), Vector2Int.down, "staff-corridor", CameraResponse.None);
        CreateCamera("Камера: кухня", new Vector2Int(37, 16), Vector2Int.right, "kitchen", CameraResponse.None);
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
        return IsInside(cell.x, cell.y, 3, 2, 6, 3);
    }

    public bool IsRestrictedCell(int x, int y)
    {
        EnsureGridInitialized();
        return IsInside(x, y, 35, 10, 36, 14) || // ventilation
               IsInside(x, y, 21, 16, 36, 18) || // staff corridor
               IsInside(x, y, 38, 14, 42, 19) || // kitchen
               IsInside(x, y, 27, 20, 31, 24) || // staff room
               IsInside(x, y, 14, 16, 19, 22) || // storage
               IsInside(x, y, 2, 16, 12, 18) ||  // secure corridor
               IsInside(x, y, 2, 20, 6, 26) ||   // laboratory
               IsInside(x, y, 8, 20, 12, 26) ||   // engineering
               IsInside(x, y, 44, 15, 51, 23) ||  // garden
               IsInside(x, y, 53, 14, 60, 24) ||  // block C
               IsInside(x, y, 61, 20, 62, 20) ||  // archive connector
               IsInside(x, y, 63, 17, 72, 25) ||  // data archive
               IsInside(x, y, 67, 13, 67, 16) ||  // relay connector
               IsInside(x, y, 63, 4, 72, 12);     // signal relay
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
        for (int x = 37; x <= 52; x++)
        {
            SetTileAndRefresh(x, 12, TileType.Floor);
        }

        SetTileAndRefresh(37, 13, TileType.Floor);
        SetTileAndRefresh(37, 14, TileType.Floor);
        SetTileAndRefresh(52, 13, TileType.Floor);
        SetTileAndRefresh(52, 14, TileType.Floor);
        RunState.MarkGardenAccessOpened();
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
