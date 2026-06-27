using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Валидатор построения карты Block C поверх <see cref="RoomGraph"/>. Три инструмента:
///   1) <see cref="GraphMatchesPrototype"/> — граф комнат совпадает с нарисованным
///      прототипом (<see cref="LayoutPrototype"/>): нет слитых/потерянных комнат и
///      незапланированных/недостающих дверных связей.
///   2) <see cref="Reachability"/> — все комнаты достижимы от старта игрока (с учётом
///      лестниц между этажами).
///   3) <see cref="ValidateRooms"/> — каждая отдельная комната «является комнатой»:
///      не вырождена, имеет дверь, без тупиковых/сиротских проёмов.
/// Все методы возвращают список нарушений в стиле offenders из LayoutIntegrityTests
/// (пустой список = всё хорошо).
///
/// О герметичности: за гридом и в неигровом пространстве всё — Wall (GetTileType
/// возвращает Wall за границей), поэтому комната всегда замкнута стеной по
/// построению. Реальные бреши проявляются как нарушения: дверь-сирота/тупик в
/// <see cref="ValidateRooms"/> (дыра в стене) либо слияние комнат / лишнее ребро в
/// <see cref="GraphMatchesPrototype"/> (забыта перегородка). Отдельной «рамочной»
/// проверки нет намеренно — она давала бы ложные срабатывания на комнатах, штатно
/// прижатых к краю карты (закрытое западное крыло на x=0).
/// </summary>
public static class LayoutValidator
{
    /// <summary>Минимальный размер комнаты — отлов случайных «комнат» в одну клетку.</summary>
    public const int MinRoomCells = 2;

    /// <summary>
    /// Намеренно запечатанные двери-«обещания»: пол только с одной стороны (sealed
    /// promise). Совпадает с whitelist из LayoutIntegrityTests — KIT-05 «странная
    /// дверь» (BLOCK_C_BLOCKOUT_V02.md). Тупиковые двери вне списка — ошибка.
    /// </summary>
    public static readonly HashSet<Vector2Int> SealedPromiseDoors = new()
    {
        new Vector2Int(72, 52),
    };

    public sealed class Report
    {
        public readonly List<string> RoomIssues = new();   // «комната — это комната»
        public readonly List<string> GraphIssues = new();  // совпадение с прототипом
        public readonly List<string> ReachIssues = new();  // достижимость
        public bool Ok => RoomIssues.Count == 0 && GraphIssues.Count == 0 && ReachIssues.Count == 0;
    }

    /// <summary>Полный прогон всех трёх проверок.</summary>
    public static Report Validate(RoomGraph graph)
    {
        var report = new Report();
        report.RoomIssues.AddRange(ValidateRooms(graph));
        report.GraphIssues.AddRange(GraphMatchesPrototype(graph));
        report.ReachIssues.AddRange(Reachability(graph));
        return report;
    }

    // --- Инструмент №3: «комната — это комната» ---------------------------------

    public static List<string> ValidateRooms(RoomGraph graph)
    {
        var issues = new List<string>();

        foreach (RoomGraph.Room room in graph.Rooms)
        {
            string at = $"r{room.Id} @({room.Centroid.x},{room.Centroid.y})";

            if (room.Cells.Count < MinRoomCells)
                issues.Add($"{at}: вырожденная комната — всего {room.Cells.Count} клет.");

            // Наличие двери: комната без дверных связей изолирована/недостижима.
            if (room.Neighbors.Count == 0)
                issues.Add($"{at}: нет двери — комната изолирована");

            // Тупиковые двери вне whitelist — «дверь в никуда» (пол с одной стороны
            // или кривой угловой проём); это и есть нарушение герметичности стены.
            foreach (Vector2Int door in room.DeadEndDoors)
            {
                if (!SealedPromiseDoors.Contains(door))
                    issues.Add($"{at}: тупиковая дверь ({door.x},{door.y}) — проём в стене без второй комнаты");
            }
        }

        // Дверь, пробитая в сплошной стене (пола нет ни с одной стороны).
        foreach (Vector2Int door in graph.OrphanDoors)
            issues.Add($"дверь-сирота ({door.x},{door.y}) — нет пола ни с одной стороны");

        return issues;
    }

    // --- Инструмент №1: граф совпадает с нарисованным прототипом -----------------

    public static List<string> GraphMatchesPrototype(RoomGraph graph)
    {
        var issues = new List<string>();

        // Привязка имён прототипа к компонентам по якорным клеткам.
        var nameToComp = new Dictionary<string, int>();
        var compToName = new Dictionary<int, string>();
        foreach (LayoutPrototype.RoomSpec spec in LayoutPrototype.Rooms)
        {
            int comp = graph.ComponentAt(spec.Anchor);
            if (comp < 0)
            {
                issues.Add($"прототип: якорь комнаты «{spec.Name}» ({spec.Anchor.x},{spec.Anchor.y}) не внутри комнаты (стена/пустота?)");
                continue;
            }
            if (compToName.TryGetValue(comp, out string other))
            {
                issues.Add($"прототип: комнаты «{other}» и «{spec.Name}» оказались одной комнатой — забыта стена между ними?");
                continue;
            }
            nameToComp[spec.Name] = comp;
            compToName[comp] = spec.Name;
        }

        // Компоненты без имени в прототипе — неописанная/новая комната (возможна дыра).
        foreach (RoomGraph.Room room in graph.Rooms)
        {
            if (!compToName.ContainsKey(room.Id))
                issues.Add($"комната r{room.Id} @({room.Centroid.x},{room.Centroid.y}) не описана в прототипе");
        }

        // Сравнение множеств дверных связей (только между именованными комнатами).
        var expected = new HashSet<(string, string)>();
        foreach (LayoutPrototype.RoomSpec spec in LayoutPrototype.Rooms)
            foreach (string other in spec.Connects)
                expected.Add(OrderedPair(spec.Name, other));

        var actual = new HashSet<(string, string)>();
        foreach (RoomGraph.Edge edge in graph.Edges)
        {
            if (compToName.TryGetValue(edge.A, out string a) && compToName.TryGetValue(edge.B, out string b))
                actual.Add(OrderedPair(a, b));
        }

        foreach ((string a, string b) in expected)
        {
            if (!actual.Contains((a, b)))
                issues.Add($"недостающая связь «{a}»–«{b}» — ожидалась дверь, но прохода нет");
        }
        foreach ((string a, string b) in actual)
        {
            if (!expected.Contains((a, b)))
                issues.Add($"лишняя связь «{a}»–«{b}» — незапланированный проём (дыра в стене?)");
        }

        return issues;
    }

    // --- Инструмент №2 (часть): достижимость от старта игрока --------------------

    public static List<string> Reachability(RoomGraph graph)
    {
        var issues = new List<string>();

        int start = graph.ComponentAt(BlockCPlayableLayout.PlayerStart);
        if (start < 0)
        {
            issues.Add($"старт игрока ({BlockCPlayableLayout.PlayerStart.x},{BlockCPlayableLayout.PlayerStart.y}) не внутри комнаты");
            return issues;
        }

        // Список смежности: дверные рёбра + лестничные связи между этажами.
        var adjacency = new Dictionary<int, HashSet<int>>();
        void AddDir(int a, int b)
        {
            if (!adjacency.TryGetValue(a, out HashSet<int> set))
            {
                set = new HashSet<int>();
                adjacency[a] = set;
            }
            set.Add(b);
        }
        void Connect(int a, int b)
        {
            if (a < 0 || b < 0 || a == b) return;
            AddDir(a, b);
            AddDir(b, a);
        }

        foreach (RoomGraph.Edge edge in graph.Edges) Connect(edge.A, edge.B);
        foreach ((Vector2Int from, Vector2Int to) in LayoutPrototype.StairLinks)
            Connect(graph.ComponentAt(from), graph.ComponentAt(to));

        var seen = new HashSet<int> { start };
        var queue = new Queue<int>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            int cur = queue.Dequeue();
            if (!adjacency.TryGetValue(cur, out var neighbors)) continue;
            foreach (int n in neighbors)
                if (seen.Add(n)) queue.Enqueue(n);
        }

        var compToName = NameByComponent(graph);
        foreach (RoomGraph.Room room in graph.Rooms)
        {
            if (seen.Contains(room.Id)) continue;
            string name = compToName.TryGetValue(room.Id, out string n) ? $"«{n}»" : "(без имени)";
            issues.Add($"недостижимо от старта: r{room.Id} {name} @({room.Centroid.x},{room.Centroid.y})");
        }

        return issues;
    }

    /// <summary>Карта component id -> имя из прототипа (для отчётов и экспортёра).</summary>
    public static Dictionary<int, string> NameByComponent(RoomGraph graph)
    {
        var map = new Dictionary<int, string>();
        foreach (LayoutPrototype.RoomSpec spec in LayoutPrototype.Rooms)
        {
            int comp = graph.ComponentAt(spec.Anchor);
            if (comp >= 0 && !map.ContainsKey(comp)) map[comp] = spec.Name;
        }
        return map;
    }

    private static (string, string) OrderedPair(string a, string b) =>
        string.CompareOrdinal(a, b) <= 0 ? (a, b) : (b, a);
}
