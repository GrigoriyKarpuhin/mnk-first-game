using UnityEngine;

/// <summary>
/// Эталонный («нарисованный прототип») граф комнат Block C: какие комнаты есть и
/// какие из них соединены дверями. Это версионируемый источник истины для проверки
/// <see cref="LayoutValidator.GraphMatchesPrototype"/> — диф против фактического
/// графа, построенного из тайлов <see cref="GameGrid"/>.
///
/// Имена взяты из blockout-документа Design/BLOCK_C_BLOCKOUT_V02.md
/// (камеры C-*, санитарное крыло SAN-*, кухня KIT-*, технические комнаты и т.д.).
/// Якорь (<see cref="RoomSpec.Anchor"/>) — заведомо внутренняя клетка комнаты;
/// именно по ней валидатор узнаёт, в какую компоненту заливки попала комната.
///
/// Каждая запись описывает ровно одну дверную компоненту (комнаты, соединённые
/// проёмами без двери, в навигации — одна комната; так SAN-Ring поглощает коридоры
/// и повороты санитарного крыла). Связи через лестницы между этажами заданы
/// отдельно в <see cref="StairLinks"/>, потому что на уровне тайлов этажи не связаны.
///
/// Как обновлять при намеренной правке карты: меню Game/Layout, кнопка
/// «Validate + Export Room Graph», взять актуальный список комнат/связей из лога и
/// привести этот файл в соответствие. См. .claude/skills/validate-layout/SKILL.md.
/// </summary>
public static class LayoutPrototype
{
    public readonly struct RoomSpec
    {
        public readonly string Name;
        public readonly Vector2Int Anchor;
        public readonly string[] Connects;

        public RoomSpec(string name, Vector2Int anchor, string[] connects)
        {
            Name = name;
            Anchor = anchor;
            Connects = connects;
        }
    }

    public static readonly RoomSpec[] Rooms =
    {
        // --- Первый этаж: атриум-хаб и примыкающие камеры/комнаты ---
        new("A1-Atrium", new(32, 30), new[]{"C-4821", "C-E01", "C-E02", "C-E03", "C-N01", "C-N02", "C-S02", "C-S03", "C-S04", "C-W01", "C-W02", "C-W03", "EXP", "GARDEN", "MESS", "SAN-01"}),
        new("C-4821", new(17, 5), new[]{"A1-Atrium"}),
        new("C-S02", new(24, 5), new[]{"A1-Atrium"}),
        new("C-S03", new(39, 5), new[]{"A1-Atrium"}),
        new("C-S04", new(46, 5), new[]{"A1-Atrium"}),
        new("C-N01", new(18, 56), new[]{"A1-Atrium"}),
        new("C-N02", new(46, 56), new[]{"A1-Atrium"}),
        new("C-W01", new(10, 12), new[]{"A1-Atrium"}),
        new("C-W02", new(10, 29), new[]{"A1-Atrium"}),
        new("C-W03", new(10, 50), new[]{"A1-Atrium"}),
        new("C-E01", new(53, 12), new[]{"A1-Atrium"}),
        new("C-E02", new(53, 29), new[]{"A1-Atrium"}),
        new("C-E03", new(53, 50), new[]{"A1-Atrium"}),
        new("MESS", new(7, 20), new[]{"A1-Atrium"}),
        new("GARDEN", new(7, 41), new[]{"A1-Atrium"}),
        new("EXP", new(31, 57), new[]{"A1-Atrium"}),

        // --- Санитарно-бытовое крыло: циркуляция-цепочка с дверями + замкнутые комнаты ---
        // Тамбур → коридор → поворот → верхний переход (SAN-Ring — узел-хаб у верхнего
        // перехода, к нему примыкают служебные комнаты). Двери: см. BLOCK_C_BLOCKOUT_F1_V02.svg.
        new("SAN-01", new(54, 21), new[]{"A1-Atrium", "SAN-02A"}),                 // входной тамбур
        new("SAN-02A", new(63, 21), new[]{"SAN-01", "SAN-02B", "SAN-05"}),          // входной коридор
        new("SAN-02B", new(68, 27), new[]{"SAN-02A", "SAN-03", "SAN-08", "SAN-Ring"}), // северный поворот
        new("SAN-Ring", new(80, 33), new[]{"KIT-01", "SAN-02B", "SAN-09"}), // верхний переход; в кухню только через ревизионную панель
        new("SAN-03", new(61, 28), new[]{"SAN-02B", "SAN-04"}),        // умывальники
        new("SAN-04", new(61, 35), new[]{"SAN-03"}),                   // туалеты (только через умывальники)
        new("SAN-05", new(61, 15), new[]{"SAN-02A", "SAN-06"}),        // раздевалка
        new("SAN-06", new(70, 15), new[]{"SAN-05", "SAN-07"}),         // душевые
        new("SAN-07", new(79, 16), new[]{"SAN-06", "SAN-07B"}),        // сушка
        new("SAN-07B", new(80, 22), new[]{"SAN-07", "SAN-09"}),        // боковой возврат
        new("SAN-08", new(74, 27), new[]{"SAN-02B", "SAN-09"}),        // санитарная персонала
        new("SAN-09", new(83, 28), new[]{"SAN-07B", "SAN-08", "SAN-Ring"}), // хозяйственная часть

        // --- Кухонный карман ---
        new("KIT-Main", new(72, 46), new[]{"KIT-01", "KIT-04"}),       // основная кухня (+ запечатанная KIT-05)
        new("KIT-01", new(83, 44), new[]{"KIT-03", "KIT-Main", "SAN-Ring", "ServiceCorridor"}), // моечная (лаз с панели)
        new("KIT-03", new(83, 52), new[]{"KIT-01"}),                   // угол персонала
        new("KIT-04", new(72, 38), new[]{"KIT-Main"}),                 // склад смены (только через кухню)
        new("ServiceCorridor", new(96, 46), new[]{"KIT-01", "StaffRoom", "Storage"}),
        new("StaffRoom", new(96, 52), new[]{"ServiceCorridor"}),

        // --- Служебно-техническое кольцо первого этажа ---
        new("Storage", new(109, 46), new[]{"SecureCorridor", "ServiceCorridor"}),
        new("SecureCorridor", new(115, 57), new[]{"Engineering", "Laboratory", "Storage"}),
        new("Laboratory", new(107, 65), new[]{"SecureCorridor"}),
        new("Engineering", new(120, 65), new[]{"SecureCorridor"}),

        // --- Второй этаж: галерея-хаб, жилые заготовки и закрытые крылья ---
        new("F2-Gallery", new(15, 100), new[]{"F2-E01", "F2-E02", "F2-E03", "F2-Ewing", "F2-N01", "F2-N02", "F2-N03", "F2-N04", "F2-S01", "F2-S02", "F2-S03", "F2-S04", "F2-W01", "F2-W02", "F2-W03", "F2-Wwing"}),
        new("F2-S01", new(17, 80), new[]{"F2-Gallery"}),
        new("F2-S02", new(24, 80), new[]{"F2-Gallery"}),
        new("F2-S03", new(39, 80), new[]{"F2-Gallery"}),
        new("F2-S04", new(46, 80), new[]{"F2-Gallery"}),
        new("F2-N01", new(17, 131), new[]{"F2-Gallery"}),
        new("F2-N02", new(24, 131), new[]{"F2-Gallery"}),
        new("F2-N03", new(39, 131), new[]{"F2-Gallery"}),
        new("F2-N04", new(46, 131), new[]{"F2-Gallery"}),
        new("F2-W01", new(10, 87), new[]{"F2-Gallery"}),
        new("F2-W02", new(10, 104), new[]{"F2-Gallery"}),
        new("F2-W03", new(10, 125), new[]{"F2-Gallery"}),
        new("F2-E01", new(53, 87), new[]{"F2-Gallery"}),
        new("F2-E02", new(53, 104), new[]{"F2-Gallery"}),
        new("F2-E03", new(53, 125), new[]{"F2-Gallery"}),
        new("F2-Wwing", new(6, 116), new[]{"F2-Gallery"}),
        new("F2-Ewing", new(60, 116), new[]{"F2-Gallery"}),
        new("F2-TechCorridor", new(115, 132), new[]{"TechWing"}),
        new("TechWing", new(135, 130), new[]{"Archive", "F2-TechCorridor"}),
        new("Archive", new(147, 130), new[]{"Relay", "TechWing"}),
        new("Relay", new(147, 114), new[]{"Archive"}),
    };

    /// <summary>
    /// Лестничные связи между этажами: на уровне тайлов этаж 1 и этаж 2 не соединены,
    /// переход только через порталы-лестницы (см. GameGrid.CreateFloorTransitions).
    /// Учитываются в <see cref="LayoutValidator.Reachability"/>, но НЕ являются
    /// дверными рёбрами графа.
    /// </summary>
    public static readonly (Vector2Int From, Vector2Int To)[] StairLinks =
    {
        (new Vector2Int(17, 35), new Vector2Int(17, 110)), // западная лестница: F1 атриум <-> F2 галерея
        (new Vector2Int(46, 35), new Vector2Int(46, 110)), // восточная лестница: F1 атриум <-> F2 галерея
        (new Vector2Int(115, 57), new Vector2Int(115, 132)), // техлестница: F1 служебный коридор <-> F2 служебный коридор
    };
}
