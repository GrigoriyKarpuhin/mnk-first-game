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
    public static readonly Vector2Int TechWingDoor = new(129, 57);
    public static readonly Vector2Int ArchiveDoor = new(141, 55);
    public static readonly Vector2Int RelayDoor = new(146, 45);

    public static readonly Vector2Int KitchenManifest = new(96, 52);
    public static readonly Vector2Int ServiceBadge = new(108, 46);
    public static readonly Vector2Int EyeImplant = new(118, 64);
    public static readonly Vector2Int Transmitter = new(122, 68);
    public static readonly Vector2Int ExperimentReports = new(106, 65);

    public static readonly Vector2Int GardenSmokeSpot = new(7, 42);
    public static readonly Vector2Int RaquelGardenMeeting = new(14, 41);
    public static readonly Vector2Int GardenMeetingInterior = new(11, 41);
    public static readonly Vector2Int GuardPostScanner = new(139, 55);
    public static readonly Vector2Int EscapeArchiveFolder = new(147, 57);
    public static readonly Vector2Int BlockCShortcutLock = new(131, 51);

    public static readonly Vector2Int WestStairFloor1 = new(17, 35);
    public static readonly Vector2Int EastStairFloor1 = new(46, 35);
    public static readonly Vector2Int WestStairFloor2 = F2(17, 35);
    public static readonly Vector2Int EastStairFloor2 = F2(46, 35);

    public static readonly GridArea PlayerCell = new(15, 3, 19, 7);
    public static readonly GridArea EngineeringArea = new(116, 61, 124, 69);

    public static readonly GridArea[] FloorAreas =
    {
        // First-floor atrium and public rooms.
        A(14, 9, 49, 52),
        A(26, 54, 37, 61),
        A(3, 16, 12, 25),
        A(3, 36, 12, 46),
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
        A(89, 44, 104, 47), A(87, 27, 90, 43), A(92, 49, 99, 55), A(105, 42, 112, 49),
        A(109, 50, 109, 55), A(103, 56, 128, 59), A(103, 61, 111, 69), A(116, 61, 124, 69),

        // Programmer prototype technology rooms.
        A(128, 55, 128, 55), A(130, 50, 140, 60), A(142, 50, 152, 60),
        A(146, 45, 146, 49), A(142, 34, 152, 44),

        // Second-floor gallery and cells, physically separated in the grid.
        F2(A(14, 9, 49, 52)),
        F2(A(15, 3, 19, 7)), F2(A(22, 3, 26, 7)), F2(A(37, 3, 41, 7)), F2(A(44, 3, 48, 7)),
        F2(A(15, 54, 19, 58)), F2(A(22, 54, 26, 58)), F2(A(37, 54, 41, 58)), F2(A(44, 54, 48, 58)),
        F2(A(8, 10, 12, 14)), F2(A(8, 27, 12, 31)), F2(A(8, 48, 12, 52)),
        F2(A(51, 10, 55, 14)), F2(A(51, 27, 55, 31)), F2(A(51, 48, 55, 52)),
        F2(A(0, 36, 12, 46)), F2(A(51, 36, 69, 46)),
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
        A(3, 36, 12, 46),

        // Служебные комнаты санитарного крыла (staff-only).
        A(70, 24, 77, 31),   // STAFF SAN (санитарная персонала)
        A(79, 24, 86, 31),   // HOUSEKEEPING (хозяйственная часть)

        // Кухонный карман — нелегальная зона (по комнатам).
        A(66, 36, 78, 40),   // SHIFT STORAGE (склад смены)
        A(66, 41, 78, 51),   // MAIN KITCHEN (основная кухня)
        A(79, 41, 87, 47),   // DISHWASH (моечная)
        A(79, 48, 87, 54),   // STAFF NOOK (угол персонала)
        A(83, 37, 83, 40),   // лаз с ревизионной панели в кухню

        // Служебная связка и восточный кухонный резерв.
        A(87, 27, 90, 43),   // вертикальная служебная связка
        A(89, 44, 104, 47),  // холодильники / доставка
        A(92, 49, 99, 55),   // StaffRoom (комната персонала)
        A(105, 42, 112, 49), // Storage (склад)

        // Восточное служебно-техническое крыло (маршрут программиста).
        A(109, 50, 109, 55), A(103, 56, 128, 59),  // SecureCorridor
        A(103, 61, 111, 69),                        // Laboratory
        A(116, 61, 124, 69),                        // Engineering
        A(128, 55, 128, 55), A(130, 50, 140, 60),   // TechWing + связка
        A(142, 50, 152, 60), A(146, 45, 146, 49),   // Archive + связка
        A(142, 34, 152, 44),                        // Relay

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
        // Ворота санитарной циркуляции (sync с SVG F1): тамбур→коридор→поворот→верхний переход→склад.
        P(57, 21), P(67, 24), P(67, 32), P(72, 36),

        // Kitchen and service gates.
        P(79, 45), P(83, 48), P(72, 41), P(72, 52), P(88, 45), P(87, 27),
        P(95, 48), P(105, 46), P(109, 50), P(108, 60), P(120, 60),
        P(129, 57), P(141, 55), P(146, 45),

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
        P(132, 53), P(136, 58), P(145, 53), P(150, 58), P(145, 40),
    };

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

    public static Vector2Int F2(int x, int y) => new(x, y + Floor2OffsetY);

    private static GridArea F2(GridArea area) =>
        new(area.MinX, area.MinY + Floor2OffsetY, area.MaxX, area.MaxY + Floor2OffsetY);

    private static GridArea A(int minX, int minY, int maxX, int maxY) => new(minX, minY, maxX, maxY);
    private static Vector2Int P(int x, int y) => new(x, y);
    private static GridWallLine V(int x, int minY, int maxY) => new(P(x, minY), P(x, maxY));
    private static GridWallLine H(int minX, int maxX, int y) => new(P(minX, y), P(maxX, y));
}
