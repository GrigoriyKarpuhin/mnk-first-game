using System.Collections.Generic;
using UnityEngine;

public readonly struct GridArea
{
    public readonly int MinX;
    public readonly int MinY;
    public readonly int MaxX;
    public readonly int MaxY;

    public GridArea(int minX, int minY, int maxX, int maxY)
    {
        MinX = minX;
        MinY = minY;
        MaxX = maxX;
        MaxY = maxY;
    }

    public bool Contains(int x, int y) => x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;
}

public readonly struct GridWallLine
{
    public readonly Vector2Int Start;
    public readonly Vector2Int End;

    public GridWallLine(Vector2Int start, Vector2Int end)
    {
        Start = start;
        End = end;
    }
}

/// <summary>
/// Декоративный проп карты: мебель и атмосферные детали (мебель камер, трубы,
/// плакаты, разметка). Чисто визуальный слой — НЕ влияет на сетку проходимости,
/// поэтому граф комнат и валидация уровня им не затрагиваются.
/// </summary>
public readonly struct DecorPlacement
{
    public readonly string Sprite;     // имя в Resources/Sprites без .png
    public readonly Vector2Int Cell;
    public readonly float Scale;       // доля клетки по большей стороне спрайта
    public readonly int Rotation;      // поворот по Z, градусы
    public readonly bool OnFloor;      // true → декаль на полу (под персонажами и стенами)

    public DecorPlacement(string sprite, int x, int y, float scale, int rotation, bool onFloor)
    {
        Sprite = sprite;
        Cell = new Vector2Int(x, y);
        Scale = scale;
        Rotation = rotation;
        OnFloor = onFloor;
    }
}

/// <summary>
/// Placement data for the Block C playable slice. Runtime systems keep their
/// existing contracts and consume these anchors instead of owning coordinates.
/// </summary>
public static class BlockCPlayableLayout
{
    public const int Width = 156;
    public const int Height = 140;
    public const int Floor2OffsetY = 75;

    public static readonly Vector2Int PlayerStart = new(17, 5);
    public static readonly Vector2Int PlayerBed = new(16, 5);
    public static readonly Vector2Int ExperimentNpc = new(31, 51);
    public static readonly Vector2Int ExperimentReturnSpawn = new(31, 50);
    public static readonly Vector2Int ProgrammerSpawn = new(24, 11);
    public static readonly Vector2Int CompetitorSpawn = new(46, 5);
    public static readonly Vector2Int MedicMechanicSpawn = new(41, 18);

    public static readonly Vector2Int CompetitorCell = new(46, 5);
    public static readonly Vector2Int CompetitorCommon = new(38, 20);
    public static readonly Vector2Int CompetitorSanitaryStop = new(62, 28);
    public static readonly Vector2Int CompetitorStaffRoom = new(95, 52);
    public static readonly Vector2Int ExperimentAssembly = new(31, 50);

    public static readonly Vector2Int RevisionPanel = new(83, 36);
    public static readonly Vector2Int KitchenShortcutDoor = new(88, 45);
    public static readonly Vector2Int KitchenShortcutServiceSide = new(89, 45);
    public static readonly Vector2Int CompetitorServiceDoor = new(70, 28);
    public static readonly Vector2Int StaffRoomDoor = new(95, 48);
    public static readonly Vector2Int StorageDoor = new(105, 46);
    public static readonly Vector2Int SecureDoor = new(109, 50);
    public static readonly Vector2Int LaboratoryDoor = new(108, 60);
    public static readonly Vector2Int EngineeringDoor = new(120, 60);
    public static readonly Vector2Int GardenDoor = new(13, 41);
    public static readonly Vector2Int TechWingDoor = F2(129, 57);
    public static readonly Vector2Int ArchiveDoor = F2(141, 55);
    public static readonly Vector2Int RelayDoor = F2(146, 45);

    public static readonly Vector2Int KitchenManifest = new(96, 52);
    public static readonly Vector2Int ServiceBadge = new(108, 46);
    public static readonly Vector2Int EyeImplant = new(118, 64);
    public static readonly Vector2Int Transmitter = new(122, 68);
    public static readonly Vector2Int ExperimentReports = new(106, 65);

    public static readonly Vector2Int GardenSmokeSpot = new(7, 42);
    public static readonly Vector2Int RaquelGardenMeeting = new(14, 41);
    public static readonly Vector2Int GardenMeetingInterior = new(11, 41);
    public static readonly Vector2Int GuardPostScanner = F2(139, 55);
    public static readonly Vector2Int EscapeArchiveFolder = F2(147, 57);
    public static readonly Vector2Int BlockCShortcutLock = F2(131, 51);
    public static readonly Vector2Int DataSourceObjective = F2(134, 55);
    public static readonly Vector2Int ComputeModuleObjective = F2(147, 55);
    public static readonly Vector2Int SignalAmplifierObjective = F2(148, 39);

    public static readonly Vector2Int WestStairFloor1 = new(17, 35);
    public static readonly Vector2Int EastStairFloor1 = new(46, 35);
    public static readonly Vector2Int WestStairFloor2 = F2(17, 35);
    public static readonly Vector2Int EastStairFloor2 = F2(46, 35);
    public static readonly Vector2Int TechStairFloor1 = new(115, 57);
    public static readonly Vector2Int TechStairFloor2 = F2(115, 57);

    public static readonly GridArea PlayerCell = new(15, 3, 19, 7);
    public static readonly GridArea EngineeringArea = new(116, 61, 124, 69);

    public static readonly GridArea[] FloorAreas =
    {
        // First-floor atrium and public rooms.
        A(14, 9, 49, 52),
        A(26, 54, 37, 61),
        A(3, 16, 12, 25),
        // Служебный сад route Ракель: увеличен и разбит живой изгородью так,
        // чтобы у входа был безопасный карман, а риск начинался глубже.
        A(1, 34, 12, 46),
        A(15, 3, 19, 7), A(22, 3, 26, 7), A(37, 3, 41, 7), A(44, 3, 48, 7),
        A(15, 54, 20, 58), A(43, 54, 48, 58),
        A(8, 10, 12, 14), A(8, 27, 12, 31), A(8, 48, 12, 52),
        A(51, 10, 55, 14), A(51, 27, 55, 31), A(51, 48, 55, 52),

        // Angled sanitary wing.
        A(51, 18, 57, 23), A(58, 20, 69, 23), A(66, 24, 69, 35), A(66, 32, 86, 35),
        A(58, 25, 65, 31), A(58, 32, 65, 38), A(58, 12, 65, 19),
        A(66, 10, 74, 19), A(75, 12, 82, 19), A(79, 20, 82, 23),
        A(70, 24, 77, 31), A(79, 24, 86, 31), A(83, 37, 83, 40),

        // Kitchen and service ring.
        A(79, 41, 87, 47), A(66, 41, 78, 51), A(79, 48, 87, 54), A(66, 36, 78, 40),
        A(89, 44, 104, 47), A(92, 49, 99, 55), A(105, 42, 112, 49),
        A(109, 50, 109, 55), A(103, 56, 128, 59), A(103, 61, 111, 69), A(116, 61, 124, 69),

        // Second-floor gallery and cells, physically separated in the grid.
        F2(A(14, 9, 49, 52)),
        F2(A(15, 3, 19, 7)), F2(A(22, 3, 26, 7)), F2(A(37, 3, 41, 7)), F2(A(44, 3, 48, 7)),
        F2(A(15, 54, 19, 58)), F2(A(22, 54, 26, 58)), F2(A(37, 54, 41, 58)), F2(A(44, 54, 48, 58)),
        F2(A(8, 10, 12, 14)), F2(A(8, 27, 12, 31)), F2(A(8, 48, 12, 52)),
        F2(A(51, 10, 55, 14)), F2(A(51, 27, 55, 31)), F2(A(51, 48, 55, 52)),
        F2(A(0, 36, 12, 46)), F2(A(51, 36, 69, 46)),

        // Programmer technology rooms moved to the second floor above the secure corridor.
        // The tech stair links two matching corridor segments instead of entering a room.
        F2(A(103, 56, 128, 59)), F2(A(130, 50, 140, 60)), F2(A(142, 50, 152, 60)),
        F2(A(146, 45, 146, 49)), F2(A(142, 34, 152, 44)),
    };

    public static readonly GridArea[] VoidAreas =
    {
        F2(A(20, 15, 43, 46)),
    };

    // Запретные зоны выровнены ровно по границам комнат (= их FloorAreas).
    // Санитарное крыло (SAN-01..09, включая STAFF SAN и HOUSEKEEPING) — открытая
    // территория для всех заключённых и в restricted НЕ входит.
    public static readonly GridArea[] RestrictedAreas =
    {
        // Служебный сад (закрыт, маршрут заключённой 2).
        A(1, 34, 12, 46),

        // Служебные комнаты санитарного крыла (staff-only).
        A(70, 24, 77, 31),   // STAFF SAN (санитарная персонала)
        A(79, 24, 86, 31),   // HOUSEKEEPING (хозяйственная часть)

        // Кухонный карман — нелегальная зона (по комнатам).
        A(66, 36, 78, 40),   // SHIFT STORAGE (склад смены)
        A(66, 41, 78, 51),   // MAIN KITCHEN (основная кухня)
        A(79, 41, 87, 47),   // DISHWASH (моечная)
        A(79, 48, 87, 54),   // STAFF NOOK (угол персонала)
        A(83, 37, 83, 40),   // вентиляция/ревизионная панель: единственный ранний проход в DISHWASH
        // Служебный коридор начинается только после кухонной двери. Вертикального
        // широкого коридора справа от вентиляции здесь нет: вентиляция остаётся
        // единственным ранним проходом в кухонный карман.
        A(89, 44, 104, 47),  // холодильники / доставка
        A(92, 49, 99, 55),   // StaffRoom (комната персонала)
        A(105, 42, 112, 49), // Storage (склад)

        // Восточное служебно-техническое крыло (маршрут программиста).
        A(109, 50, 109, 55), A(103, 56, 128, 59),  // SecureCorridor
        A(103, 61, 111, 69),                        // Laboratory
        A(116, 61, 124, 69),                        // Engineering
        F2(A(103, 56, 128, 59)), F2(A(130, 50, 140, 60)), // upper secure corridor + TechWing
        F2(A(142, 50, 152, 60)), F2(A(146, 45, 146, 49)), // Archive + connector
        F2(A(142, 34, 152, 44)),                         // Relay

        // Второй этаж: закрытые крылья.
        F2(A(0, 36, 12, 46)), F2(A(51, 36, 69, 46)),
    };

    public static readonly GridWallLine[] InteriorWalls =
    {
        V(66, 25, 31), H(58, 65, 32), H(58, 65, 20), V(66, 10, 19), V(75, 12, 19),
        H(79, 82, 20), H(79, 82, 24), V(70, 24, 31), V(78, 24, 31), V(87, 24, 31), H(79, 86, 32),
        V(79, 41, 47), H(79, 87, 48), H(66, 78, 41), V(105, 42, 49), H(92, 99, 48),

        // Доведённые стены санитарного крыла и кухни: раньше эти комнаты сливались с
        // общим объёмом (не хватало кусочка стены), теперь каждая замкнута и входит
        // только своей дверью — в соответствии с Design/BLOCK_C_BLOCKOUT_F1_V02.svg.
        V(58, 17, 19),  // CHANGING (раздевалка) отделена от входного тамбура
        V(65, 33, 38),  // TOILETS (туалеты) отделены от санитарной циркуляции
        H(67, 69, 19),  // SHOWERS (душевые) отделены от входного коридора
        H(71, 77, 31),  // STAFF SAN. (санитарная персонала) отделена от верхнего перехода
        H(66, 78, 36),  // SHIFT STORAGE (склад смены) отделён от верхнего перехода
        V(79, 49, 52),  // STAFF NOOK (угол персонала) отделён от основной кухни
        V(87, 40, 44),  // DISHWASH (моечная) отделена от служебной связки (лаз 83,41 сохранён)

        // Стены, разбивающие санитарную циркуляцию на отдельные комнаты с дверями
        // (sync с BLOCK_C_BLOCKOUT_F1_V02.svg): тамбур | коридор | поворот | верхний переход.
        V(57, 20, 23),  // VESTIBULE ↔ ENTRY CORRIDOR (дверь 57,21)
        H(66, 69, 24),  // ENTRY CORRIDOR ↔ NORTH TURN (дверь 67,24)
        H(66, 69, 32),  // NORTH TURN ↔ UPPER CROSSING (дверь 67,32)

        // Заполнение углов на стыках комнат: убирают диагональные щели, чтобы стены
        // выглядели сплошными (а не «обрезанными» по диагонали).
        V(65, 19, 19), V(69, 20, 20), V(70, 32, 32),

        // Восточное служебное крыло: на этих углах выпадала диагональная клетка, и луч
        // охраны проходил между двумя стенами наискось. Закрываем угол комнаты сплошным.
        V(86, 31, 31),  // HOUSEKEEPING ↔ закрытая область справа (луч сквозь (87,31)/(86,32))
        V(79, 31, 31),  // HOUSEKEEPING ↔ верхний переход (луч сквозь (78,31)/(79,32))
        V(82, 23, 23),  // санитарный тамбур ↔ HOUSEKEEPING (луч сквозь (83,23)/(82,24))
    };

    public static readonly Vector2Int[] DoorCells =
    {
        // First-floor cells and public gates.
        P(17, 8), P(24, 8), P(39, 8), P(46, 8), P(18, 53), P(46, 53),
        P(13, 12), P(13, 29), P(13, 50), P(50, 12), P(50, 29), P(50, 50),
        P(13, 20), P(13, 41), P(31, 53), P(50, 21),

        // Sanitary room transitions.
        P(66, 28), P(61, 32), P(62, 20), P(66, 16), P(75, 16), P(81, 20),
        P(81, 24), P(70, 28), P(78, 27), P(82, 32), P(83, 36),
        // Ворота санитарной циркуляции (sync с SVG F1): тамбур→коридор→поворот→верхний переход.
        // Прямого прохода из верхнего перехода в склад смены нет: вход в кухонный карман идёт через ревизионную панель.
        P(57, 21), P(67, 24), P(67, 32),

        // Kitchen and service gates.
        P(79, 45), P(83, 48), P(72, 41), P(72, 52), P(88, 45),
        P(95, 48), P(105, 46), P(109, 50), P(108, 60), P(120, 60),
        F2(129, 57), F2(141, 55), F2(146, 45),

        // Second-floor cells and closed wings.
        F2(17, 8), F2(24, 8), F2(39, 8), F2(46, 8),
        F2(17, 53), F2(24, 53), F2(39, 53), F2(46, 53),
        F2(13, 12), F2(13, 29), F2(13, 50), F2(50, 12), F2(50, 29), F2(50, 50),
        F2(13, 41), F2(50, 41),
    };

    public static readonly Vector2Int[] CoverCells =
    {
        P(23, 18), P(40, 20), P(22, 31), P(41, 42),
        P(67, 29), P(80, 33), P(91, 46), P(101, 45), P(106, 58), P(124, 58),
        F2(132, 53), F2(136, 58), F2(145, 53), F2(150, 58), F2(145, 40),

        // Укрытия в запретных зонах (ломают линию взгляда охраны и камер; рядом
        // с ними экспозиция игрока падает). Расставлены так, чтобы не перекрыть
        // единственный проход и двери — проверено walkability-валидатором.
        P(70, 44), P(74, 47), P(69, 49),   // главная кухня
        P(70, 38),                          // склад смены
        P(82, 45), P(82, 51),               // моечная / угол персонала
        P(74, 26), P(82, 26),               // санитарная персонала / хозчасть
        P(108, 45),                         // склад
        P(4, 45),                           // служебный сад: ящик глубже лабиринта
    };

    public static readonly Vector2Int[] HedgeCells =
    {
        // Живая изгородь сада: блокирует движение и обзор, но оставляет
        // безопасный карман у двери (x10-12, y39-43) и проход в глубину через y41.
        P(9, 34), P(9, 35), P(9, 36), P(9, 37), P(9, 38), P(9, 39),
        P(9, 43), P(9, 44), P(9, 45), P(9, 46),

        // Нижний и центральный изгиби лабиринта.
        P(4, 37), P(5, 37), P(6, 37), P(7, 37),
        P(4, 38), P(4, 39), P(4, 40), P(4, 41),

        // Верхняя перегородка: отсекает патрульную часть от входного кармана,
        // но оставляет маршрут к точке подслушивания в центре сада.
        P(5, 44), P(6, 44), P(7, 44), P(8, 44),
    };

    // Атмосферный декор, расставленный вручную (свет, трубы, плакаты, сантехника
    // общих зон, мебель камеры игрока). Мебель остальных камер добавляется
    // параметрически в BuildDecorProps(). Слой визуальный (см. GameGrid.SpawnDecor).
    private static readonly DecorPlacement[] AtmosphereDecor =
    {
        // Камера игрока A(15,3,19,7): кровать (16,5), вход снизу через (17,8).
        D("sink", 15, 3, 0.70f),
        D("wall_lamp", 17, 3, 0.55f),
        D("toilet", 19, 3, 0.80f),
        D("poster_obey", 15, 5, 0.70f),
        D("desk", 19, 6, 0.85f),
        D("stool", 18, 6, 0.45f),
        D("locker", 15, 7, 0.85f),

        // Атриум A(14,9,49,52): столовая, напольная разметка, окна и свет на стенах.
        D("table_canteen", 22, 44, 0.95f),
        D("table_canteen", 27, 44, 0.95f),
        D("table_canteen", 36, 44, 0.95f),
        D("table_canteen", 41, 44, 0.95f),
        D("floor_stencil", 31, 30, 2.60f, 0, true),
        D("window_barred", 24, 9, 0.90f),
        D("window_barred", 38, 9, 0.90f),
        D("wall_lamp", 20, 9, 0.60f),
        D("wall_lamp", 31, 9, 0.60f),
        D("wall_lamp", 43, 9, 0.60f),
        D("poster_obey", 14, 18, 0.75f),
        D("poster_obey", 49, 18, 0.75f),

        // Санитарное крыло: туалеты A(58,32,65,38), умывальники, душевые A(66,10,74,19).
        D("toilet", 59, 37, 0.80f),
        D("toilet", 61, 37, 0.80f),
        D("toilet", 63, 37, 0.80f),
        D("sink", 58, 33, 0.70f),
        D("sink", 58, 35, 0.70f),
        D("drain_grate", 61, 35, 0.90f, 0, true),
        D("drain_grate", 70, 14, 1.00f, 0, true),
        D("pipes", 67, 11, 0.95f),
        D("wall_lamp", 70, 10, 0.60f),
        // Душевые: лейки на стене над сточными решётками.
        D("shower_head", 68, 11, 0.55f),
        D("shower_head", 71, 11, 0.55f),
        D("shower_head", 73, 11, 0.55f),

        // Кухня A(66,41,78,51): стол, шкаф, трубы, свет, сток.
        D("table_canteen", 69, 45, 0.95f),
        D("locker", 77, 42, 0.80f),
        D("pipes", 68, 41, 0.95f),
        D("wall_lamp", 73, 41, 0.60f),
        D("drain_grate", 72, 49, 0.90f, 0, true),

        // Служебные коридоры/связки: трубы и решётки для атмосферы.
        D("pipes", 80, 45, 0.95f),
        D("drain_grate", 66, 33, 0.90f, 0, true),
    };

    // Камеры заключённых (оба этажа). Каждой достаётся стандартный набор мебели
    // (см. FurnishCell). Камера игрока A(15,3,19,7) обставлена вручную выше
    // (там интерактивная кровать), поэтому в этот список НЕ входит.
    private static readonly GridArea[] PrisonCells =
    {
        // Первый этаж.
        A(22, 3, 26, 7), A(37, 3, 41, 7), A(44, 3, 48, 7),
        A(15, 54, 20, 58), A(43, 54, 48, 58),
        A(8, 10, 12, 14), A(8, 27, 12, 31), A(8, 48, 12, 52),
        A(51, 10, 55, 14), A(51, 27, 55, 31), A(51, 48, 55, 52),

        // Второй этаж (галерея с камерами).
        F2(A(15, 3, 19, 7)), F2(A(22, 3, 26, 7)), F2(A(37, 3, 41, 7)), F2(A(44, 3, 48, 7)),
        F2(A(15, 54, 19, 58)), F2(A(22, 54, 26, 58)), F2(A(37, 54, 41, 58)), F2(A(44, 54, 48, 58)),
        F2(A(8, 10, 12, 14)), F2(A(8, 27, 12, 31)), F2(A(8, 48, 12, 52)),
        F2(A(51, 10, 55, 14)), F2(A(51, 27, 55, 31)), F2(A(51, 48, 55, 52)),
    };

    // Итоговая таблица декора = атмосфера (вручную) + мебель каждой камеры.
    public static readonly DecorPlacement[] DecorProps = BuildDecorProps();

    public static IEnumerable<Vector2Int> EngineeringSecretPassage()
    {
        for (int y = 48; y <= 67; y++) yield return P(113, y);
        yield return P(114, 67);
        yield return P(115, 67);
    }

    public static IEnumerable<Vector2Int> BlockCShortcut()
    {
        for (int x = 113; x <= 130; x++) yield return P(x, 48);
        yield return P(130, 49);
    }

    public static bool IsRestricted(int x, int y)
    {
        foreach (GridArea area in RestrictedAreas)
        {
            if (area.Contains(x, y)) return true;
        }
        return false;
    }

    public static bool IsHedgeCell(int x, int y)
    {
        foreach (Vector2Int cell in HedgeCells)
        {
            if (cell.x == x && cell.y == y) return true;
        }
        return false;
    }

    public static Vector2Int F2(int x, int y) => new(x, y + Floor2OffsetY);

    private static GridArea F2(GridArea area) =>
        new(area.MinX, area.MinY + Floor2OffsetY, area.MaxX, area.MaxY + Floor2OffsetY);

    private static GridArea A(int minX, int minY, int maxX, int maxY) => new(minX, minY, maxX, maxY);
    private static Vector2Int P(int x, int y) => new(x, y);
    private static GridWallLine V(int x, int minY, int maxY) => new(P(x, minY), P(x, maxY));
    private static GridWallLine H(int minX, int maxX, int y) => new(P(minX, y), P(maxX, y));

    private static DecorPlacement D(string sprite, int x, int y, float scale,
        int rotation = 0, bool onFloor = false) =>
        new(sprite, x, y, scale, rotation, onFloor);

    private static DecorPlacement[] BuildDecorProps()
    {
        var list = new List<DecorPlacement>(AtmosphereDecor);
        foreach (GridArea cell in PrisonCells)
            list.AddRange(FurnishCell(cell));
        return list.ToArray();
    }

    // Стандартный набор мебели камеры по её прямоугольнику. Дверь камеры лежит в
    // стене (вне bounds), поэтому мебель внутри камеры её не перекрывает; слой
    // визуальный и не влияет на проходимость.
    private static IEnumerable<DecorPlacement> FurnishCell(GridArea c)
    {
        int x0 = c.MinX, y0 = c.MinY, x1 = c.MaxX, y1 = c.MaxY;
        return new[]
        {
            D("bed", x0 + 1, y0 + 2, 1.15f),   // кровать вдоль левой стены
            D("sink", x0, y0, 0.62f),          // умывальник — дальний левый угол
            D("toilet", x1, y0, 0.78f),        // туалет — дальний правый угол
            D("locker", x0, y1, 0.85f),        // шкаф — ближний левый угол
            D("desk", x1, y1, 0.78f),          // стол — ближний правый угол
            D("stool", x1 - 1, y1, 0.45f),     // табурет у стола
        };
    }
}
