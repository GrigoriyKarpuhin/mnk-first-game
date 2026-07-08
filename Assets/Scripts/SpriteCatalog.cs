using System.Collections.Generic;

/// <summary>
/// Единая точка вычисления ресурсного пути спрайта в Resources/Sprites/*
/// по его имени (без подпапки). Спрайты разложены по подпапкам категорий;
/// это позволяет вызывающему коду по-прежнему оперировать только базовым
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

        foreach (string basis in CharacterBases)
        {
            if (name.StartsWith(basis)) return "Characters";
        }

        return "Props";
    }
}
