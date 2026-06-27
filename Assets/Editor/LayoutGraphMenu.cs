using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-инструмент валидации построения карты: строит граф комнат из тех же
/// данных, что и рантайм (<see cref="BlockCPlayableLayout"/> через временный
/// <see cref="GameGrid"/>), прогоняет <see cref="LayoutValidator"/> и рисует граф в
/// Mermaid-файл Design/generated/room_graph.mmd для сверки с нарисованным прототипом.
///
/// Сцена не нужна: грид собирается лениво из layout-данных, как в EditMode-тестах.
/// Полный отчёт о нарушениях пишется в консоль (ошибки — LogError, чтобы бросались
/// в глаза). Те же проверки гейтят CI через RoomGraphTests.
/// </summary>
public static class LayoutGraphMenu
{
    private const string OutputRelativePath = "Design/generated/room_graph.mmd";

    [MenuItem("Game/Layout/Validate + Export Room Graph")]
    public static void ValidateAndExport()
    {
        var temp = new GameObject("~RoomGraphProbe") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var grid = temp.AddComponent<GameGrid>();
            RoomGraph graph = RoomGraph.Build(grid);
            LayoutValidator.Report report = LayoutValidator.Validate(graph);

            string path = WriteMermaid(RoomGraphExporter.ToMermaid(graph));
            LogReport(graph, report, path);
        }
        finally
        {
            Object.DestroyImmediate(temp);
        }
    }

    private static string WriteMermaid(string mermaid)
    {
        string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Design", "generated"));
        Directory.CreateDirectory(dir);
        string full = Path.Combine(dir, "room_graph.mmd");
        File.WriteAllText(full, mermaid);
        AssetDatabase.Refresh();
        return full;
    }

    private static void LogReport(RoomGraph graph, LayoutValidator.Report report, string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[Layout] Комнат: {graph.Rooms.Count}, дверных рёбер: {graph.Edges.Count}, " +
                      $"дверей-сирот: {graph.OrphanDoors.Count}.");
        sb.AppendLine($"[Layout] Граф нарисован в {OutputRelativePath}.");

        AppendSection(sb, "«Комната — это комната»", report.RoomIssues);
        AppendSection(sb, "Совпадение с прототипом", report.GraphIssues);
        AppendSection(sb, "Достижимость", report.ReachIssues);

        if (report.Ok)
        {
            sb.AppendLine("[Layout] ВАЛИДАЦИЯ ПРОЙДЕНА: нарушений нет.");
            Debug.Log(sb.ToString());
        }
        else
        {
            sb.AppendLine("[Layout] ВАЛИДАЦИЯ НЕ ПРОЙДЕНА — см. список выше.");
            Debug.LogError(sb.ToString());
        }
    }

    private static void AppendSection(StringBuilder sb, string title, System.Collections.Generic.List<string> issues)
    {
        if (issues.Count == 0)
        {
            sb.AppendLine($"  ✓ {title}: ок");
            return;
        }
        sb.AppendLine($"  ✗ {title}: {issues.Count} нарушений");
        foreach (string issue in issues) sb.AppendLine($"      - {issue}");
    }
}
