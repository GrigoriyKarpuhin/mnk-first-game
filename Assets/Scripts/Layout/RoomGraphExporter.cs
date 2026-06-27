using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Рисование графа комнат в текст Mermaid (диаграмма для сверки с нарисованным
/// прототипом BLOCK_C_BLOCKOUT_F1/F2_V02.svg). Чистая функция — без файлового
/// ввода-вывода и без UnityEditor, поэтому покрывается EditMode-тестом, а запись в
/// Design/generated/ делает редакторное меню (LayoutGraphMenu).
///
/// Узлы — комнаты (имя из прототипа или r{id}, в подписи центроид). Сплошные рёбра —
/// двери; пунктир «stairs» — лестницы между этажами; пунктир «sealed» — тупиковые
/// двери-обещания. Комнаты с нарушениями подсвечиваются красным (classDef bad).
/// </summary>
public static class RoomGraphExporter
{
    public static string ToMermaid(RoomGraph graph)
    {
        Dictionary<int, string> nameByComp = LayoutValidator.NameByComponent(graph);
        LayoutValidator.Report report = LayoutValidator.Validate(graph);

        // Комнаты, упомянутые в любом нарушении (по подстроке "r{id} "), — красим.
        var badRooms = new HashSet<int>();
        foreach (string issue in Concat(report.RoomIssues, report.GraphIssues, report.ReachIssues))
        {
            foreach (RoomGraph.Room room in graph.Rooms)
            {
                if (issue.Contains($"r{room.Id} ") || issue.Contains($"r{room.Id}:") || issue.Contains($"r{room.Id}@"))
                    badRooms.Add(room.Id);
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine("graph TD");
        sb.AppendLine("  %% Сгенерировано LayoutGraphMenu из тайлов GameGrid. Не редактировать вручную.");

        for (int floor = 1; floor <= 2; floor++)
        {
            sb.AppendLine($"  %% --- Этаж {floor} ---");
            foreach (RoomGraph.Room room in graph.Rooms)
            {
                if (room.Floor != floor) continue;
                string id = NodeId(room, nameByComp);
                string label = (nameByComp.TryGetValue(room.Id, out string n) ? n : $"r{room.Id}")
                               + $"\\n({room.Centroid.x},{room.Centroid.y})";
                sb.AppendLine($"  {id}[\"{label}\"]");
            }
        }

        sb.AppendLine("  %% --- Двери ---");
        foreach (RoomGraph.Edge edge in graph.Edges)
        {
            string a = NodeId(graph.Rooms[edge.A], nameByComp);
            string b = NodeId(graph.Rooms[edge.B], nameByComp);
            sb.AppendLine($"  {a} --- {b}");
        }

        sb.AppendLine("  %% --- Лестницы ---");
        foreach ((Vector2Int from, Vector2Int to) in LayoutPrototype.StairLinks)
        {
            int ca = graph.ComponentAt(from), cb = graph.ComponentAt(to);
            if (ca < 0 || cb < 0) continue;
            sb.AppendLine($"  {NodeId(graph.Rooms[ca], nameByComp)} -. stairs .- {NodeId(graph.Rooms[cb], nameByComp)}");
        }

        bool anySealed = false;
        foreach (RoomGraph.Room room in graph.Rooms)
        {
            foreach (Vector2Int door in room.DeadEndDoors)
            {
                if (!anySealed) { sb.AppendLine("  %% --- Тупиковые двери (sealed promise) ---"); anySealed = true; }
                string deadId = $"sealed_{door.x}_{door.y}";
                sb.AppendLine($"  {deadId}([\"sealed ({door.x},{door.y})\"])");
                sb.AppendLine($"  {NodeId(room, nameByComp)} -. sealed .- {deadId}");
            }
        }

        if (badRooms.Count > 0)
        {
            sb.AppendLine("  classDef bad fill:#f99,stroke:#900,color:#000;");
            var ids = new List<string>();
            foreach (int rid in badRooms) ids.Add(NodeId(graph.Rooms[rid], nameByComp));
            ids.Sort(string.CompareOrdinal);
            sb.AppendLine($"  class {string.Join(",", ids)} bad;");
        }

        return sb.ToString();
    }

    private static string NodeId(RoomGraph.Room room, Dictionary<int, string> nameByComp)
    {
        string raw = nameByComp.TryGetValue(room.Id, out string n) ? n : $"r{room.Id}";
        var sb = new StringBuilder(raw.Length);
        foreach (char c in raw)
            sb.Append(char.IsLetterOrDigit(c) ? c : '_');
        return sb.ToString();
    }

    private static IEnumerable<string> Concat(params List<string>[] lists)
    {
        foreach (List<string> list in lists)
            foreach (string s in list)
                yield return s;
    }
}
