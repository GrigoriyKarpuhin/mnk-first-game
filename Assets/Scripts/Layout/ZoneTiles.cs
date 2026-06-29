using UnityEngine;

/// <summary>
/// Классификация комнат Block C по ЗОНАМ для подбора тилсета (пол/стена) и тинта
/// 3D-юбки. Зона выводится из ИМЕНИ комнаты (LayoutPrototype.Rooms через
/// RoomGraph + LayoutValidator.NameByComponent). Спрайты грузятся по имени из
/// Resources/Sprites; если ассета зоны нет — вызывающий (GameGrid) падает на
/// дефолт (wall_top/floor_concrete), поэтому частичный набор не ломает игру.
///
/// Внутри зоны — 2 варианта текстуры, выбор поклеточно детерминированным хэшем
/// (PickVariant), чтобы не было одноклеточного повтора («визуальной каши»).
/// </summary>
public static class ZoneTiles
{
    public enum Zone { Common, Cell, Wet, Kitchen, Tech, Dining, Garden }

    public const int VariantCount = 2;

    /// <summary>Имя комнаты прототипа -> зона. Неизвестное/хаб -> Common.</summary>
    public static Zone Classify(string roomName)
    {
        if (string.IsNullOrEmpty(roomName)) return Zone.Common;

        // Закрытые крылья 2-го этажа — не жилые камеры, это режимные зоны.
        if (roomName.StartsWith("F2-Wwing") || roomName.StartsWith("F2-Ewing")) return Zone.Tech;

        // Жилые камеры: C-4821, C-S02…, и F2-S/N/W/E + номер.
        if (roomName.StartsWith("C-")) return Zone.Cell;
        if (roomName.StartsWith("F2-S") || roomName.StartsWith("F2-N")
            || roomName.StartsWith("F2-W") || roomName.StartsWith("F2-E")) return Zone.Cell;

        if (roomName.StartsWith("SAN-")) return Zone.Wet;
        if (roomName.StartsWith("KIT-") || roomName == "StaffRoom") return Zone.Kitchen;
        if (roomName == "MESS") return Zone.Dining;
        if (roomName == "GARDEN") return Zone.Garden;

        switch (roomName)
        {
            case "Laboratory":
            case "Engineering":
            case "TechWing":
            case "Archive":
            case "Relay":
            case "Storage":
            case "SecureCorridor":
                return Zone.Tech;
        }

        return Zone.Common; // A1-Atrium, F2-Gallery, EXP и прочее
    }

    /// <summary>Имя спрайта пола для зоны/варианта. Common-a = текущий floor_concrete.</summary>
    public static string FloorSpriteName(Zone z, int variant)
    {
        if (z == Zone.Common) return variant == 0 ? "floor_concrete" : "floor_common_b";
        return Base(z, "floor") + (variant == 0 ? "_a" : "_b");
    }

    /// <summary>Имя спрайта стены для зоны/варианта. Common-a = текущий wall_top.</summary>
    public static string WallSpriteName(Zone z, int variant)
    {
        if (z == Zone.Common) return variant == 0 ? "wall_top" : "wall_common_b";
        return Base(z, "wall") + (variant == 0 ? "_a" : "_b");
    }

    private static string Base(Zone z, string surface) => surface + "_" + z switch
    {
        Zone.Cell => "cell",
        Zone.Wet => "wet",
        Zone.Kitchen => "kitchen",
        Zone.Tech => "tech",
        Zone.Dining => "dining",
        Zone.Garden => "garden",
        _ => "common",
    };

    /// <summary>Детерминированный выбор варианта по клетке (без Random, стабилен между кадрами).</summary>
    public static int PickVariant(int x, int y, int count)
    {
        if (count <= 1) return 0;
        int h = (x * 73856093) ^ (y * 19349663);
        return (h & 0x7fffffff) % count;
    }

    /// <summary>Приоритет зоны для клетки-стены, граничащей с разными комнатами:
    /// спец-комнаты «держат лицо» (стена душевой остаётся стеной душевой).</summary>
    public static int Priority(Zone z) => z switch
    {
        Zone.Wet => 6,
        Zone.Cell => 5,
        Zone.Kitchen => 4,
        Zone.Tech => 3,
        Zone.Dining => 2,
        Zone.Garden => 1,
        _ => 0, // Common
    };

    /// <summary>Тинт боковой «юбки» стены по зоне (чтобы лип не выглядел инородно).</summary>
    public static Color EdgeTint(Zone z) => z switch
    {
        Zone.Wet => new Color(0.86f, 0.90f, 0.92f),
        Zone.Garden => new Color(0.82f, 0.84f, 0.78f),
        Zone.Tech => new Color(0.82f, 0.84f, 0.88f),
        _ => Color.white,
    };
}
