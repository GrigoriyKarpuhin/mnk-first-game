using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Общий экран итогов эксперимента (OnGUI). Любой эксперимент в терминальной фазе вызывает
/// <see cref="Draw"/> вместо своего простого диалога: панель собирается из
/// <see cref="RunState.LastResult"/> и текущих отношений, поэтому одинаково работает для всех
/// экспериментов и не дублирует логику. Управление (E/R) остаётся в самом эксперименте.
/// </summary>
public static class ExperimentSummaryView
{
    private static GUIStyle titleStyle;
    private static GUIStyle bodyStyle;
    private static GUIStyle hintStyle;

    /// <param name="hint">Подсказка по управлению (специфична для эксперимента).</param>
    /// <param name="detail">Необязательная строка-исход от эксперимента (флейвор-текст).</param>
    public static void Draw(string hint, string detail = null)
    {
        EnsureStyles();

        ExperimentResult result = RunState.LastResult;
        if (result == null)
        {
            DrawFallback(hint);
            return;
        }

        string outcome = !result.PlayerSurvived ? "Вы погибли"
            : result.PlayerWon ? "Вы выжили · победа"
            : "Вы выжили";

        string implantLine = null;
        if (result.OfferedImplant.HasValue)
            implantLine = result.ImplantAccepted
                ? $"Получен имплант: {ImplantName(result.OfferedImplant.Value)}"
                : "Имплант отклонён";

        // Строки по участникам: показываем тех, кто реально участвовал в этом эксперименте.
        var npcLines = new List<string>();
        foreach (NpcId npc in RunState.SocialNpcs)
        {
            bool involved = result.NpcSurvived.ContainsKey(npc) ||
                            result.Actions.ContainsKey(npc) ||
                            result.RelationshipDeltas.ContainsKey(npc);
            if (!involved) continue;
            npcLines.Add(NpcLine(result, npc));
        }

        // Высота панели под фактическое число строк.
        float detailH = string.IsNullOrEmpty(detail) ? 0f : 52f;
        float boxW = 660f;
        int rows = 1 /*outcome*/ + (implantLine != null ? 1 : 0) + 1 /*заголовок отношений*/ +
                   Mathf.Max(1, npcLines.Count);
        float boxH = 96f + rows * 30f + 56f + detailH;
        var box = new Rect((Screen.width - boxW) / 2f, (Screen.height - boxH) / 2f, boxW, boxH);
        GUI.Box(box, "");

        float x = box.x + 28f;
        float w = box.width - 56f;
        float y = box.y + 22f;

        GUI.Label(new Rect(x, y, w, 36f), "Итоги эксперимента", titleStyle);
        y += 48f;

        GUI.Label(new Rect(x, y, w, 28f), outcome, bodyStyle);
        y += 30f;
        if (detailH > 0f)
        {
            GUI.Label(new Rect(x, y, w, detailH), detail, bodyStyle);
            y += detailH + 2f;
        }
        if (implantLine != null)
        {
            GUI.Label(new Rect(x, y, w, 28f), implantLine, bodyStyle);
            y += 30f;
        }

        GUI.Label(new Rect(x, y, w, 28f), "Отношения с участниками:", bodyStyle);
        y += 30f;
        if (npcLines.Count == 0)
        {
            GUI.Label(new Rect(x + 16f, y, w - 16f, 28f), "— изменений нет.", bodyStyle);
            y += 30f;
        }
        else
        {
            foreach (string line in npcLines)
            {
                GUI.Label(new Rect(x + 16f, y, w - 16f, 28f), line, bodyStyle);
                y += 30f;
            }
        }

        GUI.Label(new Rect(x, box.yMax - 40f, w, 28f), hint, hintStyle);
    }

    private static string NpcLine(ExperimentResult result, NpcId npc)
    {
        string name = RunState.NpcDisplayName(npc);

        string fate = result.NpcSurvived.TryGetValue(npc, out bool alive)
            ? (alive ? "выжил" : "погиб")
            : null;

        string action = result.Actions.TryGetValue(npc, out NpcAction a) ? ActionLabel(a) : null;

        int after = RunState.RelationshipTo(npc);
        int scaled = result.RelationshipDeltas.TryGetValue(npc, out int d) ? d * RunState.RelationshipDeltaScale : 0;
        int before = Mathf.Clamp(after - scaled, RelationshipLevels.Min, RelationshipLevels.Max);
        string sign = scaled > 0 ? "+" : "";
        string deltaStr = scaled != 0 ? $" ({sign}{scaled})" : "";
        string rel = before == after
            ? $"{RelationshipLevels.Label(after)} {after}"
            : $"{RelationshipLevels.Label(before)} {before} → {RelationshipLevels.Label(after)} {after}{deltaStr}";

        // Имя · судьба/действие · отношения.
        var tags = new List<string>();
        if (fate != null) tags.Add(fate);
        if (!string.IsNullOrEmpty(action)) tags.Add(action);
        string prefix = tags.Count > 0 ? $"{name} ({string.Join(", ", tags)}): " : $"{name}: ";
        return prefix + rel;
    }

    private static string ActionLabel(NpcAction action) => action switch
    {
        NpcAction.Helped => "помог",
        NpcAction.Harmed => "навредил",
        NpcAction.Betrayed => "предал",
        NpcAction.Ignored => "проигнорировал",
        _ => "",
    };

    private static string ImplantName(ImplantId implant) => implant switch
    {
        ImplantId.ReactiveFeet => "реактивные стопы",
        ImplantId.EyeImplant => "глазной имплант",
        ImplantId.MaskingImplant => "маскировочный имплант",
        _ => implant.ToString(),
    };

    private static void DrawFallback(string hint)
    {
        float boxW = 520f, boxH = 160f;
        var box = new Rect((Screen.width - boxW) / 2f, (Screen.height - boxH) / 2f, boxW, boxH);
        GUI.Box(box, "");
        GUI.Label(new Rect(box.x + 28f, box.y + 24f, box.width - 56f, 36f), "Итоги эксперимента", titleStyle);
        GUI.Label(new Rect(box.x + 28f, box.y + 70f, box.width - 56f, 28f), "Результат недоступен.", bodyStyle);
        GUI.Label(new Rect(box.x + 28f, box.yMax - 40f, box.width - 56f, 28f), hint, hintStyle);
    }

    private static void EnsureStyles()
    {
        titleStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white },
        };
        bodyStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            wordWrap = true,
            normal = { textColor = Color.white },
        };
        hintStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Italic,
            normal = { textColor = new Color(0.78f, 0.86f, 0.80f) },
        };
    }
}
