using UnityEngine;

/// <summary>Описание охранника по умолчанию: имя, тип и маршрут (для конвойного — одна точка-пост).</summary>
public readonly struct DefaultGuard
{
    public readonly string Name;
    public readonly GuardKind Kind;
    public readonly PatrolWaypoint[] Route;

    public DefaultGuard(string name, GuardKind kind, PatrolWaypoint[] route)
    {
        Name = name;
        Kind = kind;
        Route = route;
    }

    /// <summary>Стартовая клетка (для конвойного поста — единственная точка маршрута).</summary>
    public Vector2Int Cell => Route != null && Route.Length > 0 ? Route[0].Cell : Vector2Int.zero;
}

/// <summary>Описание камеры наблюдения по умолчанию.</summary>
public readonly struct DefaultCamera
{
    public readonly string Name;
    public readonly Vector2Int Cell;
    public readonly Vector2Int Facing;
    public readonly string Zone;
    public readonly CameraResponse Response;
    public readonly int Range;

    public DefaultCamera(string name, Vector2Int cell, Vector2Int facing, string zone,
        CameraResponse response, int range = 6)
    {
        Name = name;
        Cell = cell;
        Facing = facing;
        Zone = zone;
        Response = response;
        Range = range;
    }
}

/// <summary>Описание ящика-укрытия по умолчанию.</summary>
public readonly struct DefaultHideSpot
{
    public readonly Vector2Int Cell;
    public readonly string Label;

    public DefaultHideSpot(Vector2Int cell, string label)
    {
        Cell = cell;
        Label = label;
    }
}

/// <summary>
/// Единый источник дефолтной расстановки тюрьмы. Им пользуется и рантайм
/// (<see cref="GameGrid"/> при отсутствии маркеров), и editor-«запекатель»,
/// который превращает эти данные в редактируемые объекты-маркеры на сцене.
/// </summary>
public static class PrisonDefaults
{
    /// <summary>Все охранники по умолчанию: конвойные посты + патрули (карта Block C).</summary>
    public static DefaultGuard[] Guards() => new[]
    {
        // Конвойные посты (ScheduleEnforcer): стоят, ловят при нарушении расписания.
        new DefaultGuard("Надзиратель общей зоны", GuardKind.ScheduleEnforcer, new[]
        {
            new PatrolWaypoint(new Vector2Int(31, 48))
        }),
        new DefaultGuard("Надзиратель галереи второго этажа", GuardKind.ScheduleEnforcer, new[]
        {
            new PatrolWaypoint(BlockCPlayableLayout.F2(31, 48))
        }),

        // Патрульные (GuardPatrol): на концах маршрута — осмотр взглядом (Scan).
        new DefaultGuard("Надзиратель служебного коридора", GuardKind.Patrol, new[]
        {
            new PatrolWaypoint(new Vector2Int(90, 46), scan: true),
            new PatrolWaypoint(new Vector2Int(103, 46), scan: true)
        }),
        new DefaultGuard("Надзиратель защищённого коридора", GuardKind.Patrol, new[]
        {
            new PatrolWaypoint(new Vector2Int(104, 58), scan: true),
            new PatrolWaypoint(new Vector2Int(126, 58), scan: true)
        }),
        new DefaultGuard("Надзиратель блока C", GuardKind.Patrol, new[]
        {
            new PatrolWaypoint(new Vector2Int(132, 52), scan: true),
            new PatrolWaypoint(new Vector2Int(137, 58), scan: true)
        }),
        new DefaultGuard("Надзиратель архива данных", GuardKind.Patrol, new[]
        {
            new PatrolWaypoint(new Vector2Int(144, 52), scan: true),
            // (150,58) — клетка-укрытие (Cover), туда патруль дойти не может и
            // застревает; сдвинуто на (150,57) рядом с укрытием.
            new PatrolWaypoint(new Vector2Int(150, 57), scan: true)
        }),
        new DefaultGuard("Надзиратель релейной", GuardKind.Patrol, new[]
        {
            new PatrolWaypoint(new Vector2Int(144, 36), scan: true),
            new PatrolWaypoint(new Vector2Int(150, 42), scan: true)
        }),

        // Патрули в ранее «слепых» запретных зонах (маршруты держатся внутри одной
        // комнаты — в патруле охрана не ходит через закрытые двери).
        new DefaultGuard("Надзиратель кухни", GuardKind.Patrol, new[]
        {
            new PatrolWaypoint(new Vector2Int(68, 49), scan: true),
            new PatrolWaypoint(new Vector2Int(76, 43), scan: true)
        }),
        new DefaultGuard("Надзиратель моечной", GuardKind.Patrol, new[]
        {
            new PatrolWaypoint(new Vector2Int(81, 44), scan: true),
            new PatrolWaypoint(new Vector2Int(85, 45), scan: true)
        }),
        new DefaultGuard("Надзиратель санитарной персонала", GuardKind.Patrol, new[]
        {
            new PatrolWaypoint(new Vector2Int(81, 27), scan: true),
            new PatrolWaypoint(new Vector2Int(85, 27), scan: true)
        }),
        new DefaultGuard("Надзиратель служебного сада", GuardKind.Patrol, new[]
        {
            new PatrolWaypoint(new Vector2Int(5, 41), scan: true),
            new PatrolWaypoint(new Vector2Int(10, 44), scan: true)
        }),
    };

    /// <summary>Камеры наблюдения по умолчанию (монтируются на стенах, смотрят в комнату).</summary>
    public static DefaultCamera[] Cameras() => new[]
    {
        // Камеры в ЛЕГАЛЬНЫХ зонах: только «ужас», без последствий (там игрок имеет право быть).
        new DefaultCamera("Камера: общая зона", new Vector2Int(31, 50), Vector2Int.down, "common-area", CameraResponse.None),
        new DefaultCamera("Камера: санитарное крыло", new Vector2Int(67, 34), Vector2Int.down, "sanitary", CameraResponse.None),

        // Камеры в ЗАПРЕТНЫХ зонах: засекли игрока — поднимают охрану на его клетку.
        new DefaultCamera("Камера: служебный коридор", new Vector2Int(96, 47), Vector2Int.down, "staff-corridor", CameraResponse.SummonGuards),
        new DefaultCamera("Камера: кухня", new Vector2Int(78, 45), Vector2Int.left, "kitchen", CameraResponse.SummonGuards),
        new DefaultCamera("Камера: склад смены", new Vector2Int(78, 38), Vector2Int.left, "shift-storage", CameraResponse.SummonGuards),
        new DefaultCamera("Камера: санитарная персонала", new Vector2Int(73, 30), Vector2Int.down, "staff-san", CameraResponse.SummonGuards),
        new DefaultCamera("Камера: склад", new Vector2Int(111, 48), Vector2Int.left, "storage", CameraResponse.SummonGuards),
        new DefaultCamera("Камера: служебный сад", new Vector2Int(12, 45), Vector2Int.left, "service-garden", CameraResponse.SummonGuards),
    };

    /// <summary>Ящики-укрытия по умолчанию.</summary>
    public static DefaultHideSpot[] HideSpots() => new[]
    {
        new DefaultHideSpot(new Vector2Int(96, 46), "Ящик: служебный коридор"),
        new DefaultHideSpot(new Vector2Int(30, 20), "Ящик: атриум"),
        new DefaultHideSpot(new Vector2Int(94, 53), "Ящик: комната персонала"),
        // Контригра в запретных зонах: переждать погоню/сбросить преследование.
        new DefaultHideSpot(new Vector2Int(67, 50), "Ящик: кухня"),
        new DefaultHideSpot(new Vector2Int(85, 53), "Ящик: угол персонала"),
        new DefaultHideSpot(new Vector2Int(109, 44), "Ящик: склад"),
        new DefaultHideSpot(new Vector2Int(4, 45), "Ящик: служебный сад"),
    };

    /// <summary>
    /// Закрытые зоны по умолчанию — в виде прямоугольников клеток (origin = левый-нижний угол).
    /// Берутся из геометрии комнат BlockCPlayableLayout; их же читает запекатель зон.
    /// </summary>
    public static RectInt[] RestrictedZones()
    {
        GridArea[] areas = BlockCPlayableLayout.RestrictedAreas;
        var zones = new RectInt[areas.Length];
        for (int i = 0; i < areas.Length; i++)
        {
            GridArea a = areas[i];
            zones[i] = new RectInt(a.MinX, a.MinY, a.MaxX - a.MinX + 1, a.MaxY - a.MinY + 1);
        }
        return zones;
    }
}
