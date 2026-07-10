using System.Collections.Generic;

/// <summary>
/// Единая точка вычисления ресурсного пути спрайта в Resources/Sprites/*
/// по его имени (без подпапки). Спрайты разложены по подпапкам категорий;
/// персонажи дополнительно разложены как Characters/&lt;character&gt;/&lt;action&gt;.
/// Это позволяет вызывающему коду по-прежнему оперировать только базовым
/// именем (как раньше), а раскладку по папкам знает только этот класс.
/// </summary>
public static class SpriteCatalog
{
    private static readonly HashSet<string> RaceTrack = new HashSet<string>
    {
        "finish_line", "start_line", "race_dirt", "pit", "rock",
    };

    private static readonly HashSet<string> PropExceptions = new HashSet<string>
    {
        "wall_lamp", // настенный декор, а не тайл зоны
    };

    private static readonly string[] CharacterBases =
    {
        "player", "guard", "girl", "npc_programmer", "prisoner_generic", "inmate_c1752",
    };

    public static string Resolve(string name)
    {
        return "Sprites/" + CategoryOf(name) + "/" + name;
    }

    private static string CategoryOf(string name)
    {
        if (RaceTrack.Contains(name)) return "RaceTrack";
        if (PropExceptions.Contains(name)) return "Props";
        if (name.StartsWith("floor_")) return "Environment/Floors";
        if (name.StartsWith("wall_")) return "Environment/Walls";
        if (name.StartsWith("item_")) return "Items";
        if (name.StartsWith("hud_") || name == "ui_scanline") return "UI";
        if (name.StartsWith("fight_punch_") || name.StartsWith("fight_puch_")) return "Characters/player/fight";
        if (name.EndsWith("hoke_assets")) return "Characters/player/choke";

        foreach (string basis in CharacterBases)
        {
            if (name == basis || name.StartsWith(basis + "_")) return CharacterPathOf(basis, name);
        }

        return "Props";
    }

    private static string CharacterPathOf(string basis, string name)
    {
        string rest = name.Length == basis.Length ? "" : name.Substring(basis.Length + 1);
        return "Characters/" + basis + "/" + CharacterActionOf(rest);
    }

    private static string CharacterActionOf(string suffix)
    {
        if (string.IsNullOrEmpty(suffix) || suffix == "side" || suffix == "up") return "idle";

        if (suffix == "walk_1" || suffix == "walk_2" ||
            suffix.StartsWith("side_walk_") || suffix.StartsWith("up_walk_"))
        {
            return "walk";
        }

        if (suffix.Contains("fight_stand")) return "fight_stand";
        if (suffix.Contains("fight")) return "fight";
        if (suffix.Contains("pickup")) return "pickup";
        if (suffix.Contains("choke")) return "choke";
        if (suffix.Contains("throw")) return "throw";

        return "idle";
    }
}
