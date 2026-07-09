using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Инварианты карты в стиле Resident Evil (исследовано/зачищено):
///  • id комнат стабильны между сборками графа — иначе отметки «посещено» в RunState
///    (ключ = id) сползут при перезагрузке сцены;
///  • каждая клетка задачи комнаты (IRoomObjective.Cell) и каждый размещённый пикап
///    лежит ВНУТРИ комнаты (ComponentAt ≥ 0) — иначе задача не попадёт в комнату и
///    комната останется «незачищаемой» либо задача потеряется.
/// Граф строится из той же <see cref="GameGrid"/>, что и рантайм.
/// </summary>
[TestFixture]
public class MapExplorationTests
{
    private GameObject gridObject;
    private GameGrid grid;
    private RoomGraph graph;

    [SetUp]
    public void SetUp()
    {
        gridObject = new GameObject("TestGrid");
        grid = gridObject.AddComponent<GameGrid>();
        graph = RoomGraph.Build(grid);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(gridObject);
    }

    [Test]
    public void RoomIds_AreStableAcrossRebuilds()
    {
        var secondObject = new GameObject("TestGrid2");
        try
        {
            var secondGrid = secondObject.AddComponent<GameGrid>();
            RoomGraph other = RoomGraph.Build(secondGrid);

            Assert.AreEqual(graph.Rooms.Count, other.Rooms.Count,
                "Разное число комнат в двух сборках — заливка недетерминирована.");

            for (int id = 0; id < graph.Rooms.Count; id++)
            {
                Assert.AreEqual(graph.Rooms[id].Centroid, other.Rooms[id].Centroid,
                    $"id {id} указывает на разные комнаты в двух сборках " +
                    "(центроиды разошлись) — id нельзя использовать как ключ посещения.");
            }
        }
        finally
        {
            Object.DestroyImmediate(secondObject);
        }
    }

    [Test]
    public void ObjectiveAndPickupCells_ResolveToRooms()
    {
        // Клетки задач комнат (совпадают с IRoomObjective.Cell соответствующих объектов)
        // и клетки размещённых пикапов. Все должны попадать во внутренность комнаты.
        var cells = new Dictionary<string, Vector2Int>
        {
            // Одноразовые точки
            { "Сканер поста", BlockCPlayableLayout.GuardPostScanner },
            { "Папка архива", BlockCPlayableLayout.EscapeArchiveFolder },
            // Центр инженерной зоны (EngineeringCircuitPuzzle.Cell)
            { "Инженерная головоломка", EngineeringAreaCenter() },
            // Целевые узлы головоломок программиста (ProgrammerCircuitPuzzle.Cell)
            { "Источник данных", BlockCPlayableLayout.DataSourceObjective },
            { "Модуль доступа", BlockCPlayableLayout.ComputeModuleObjective },
            { "Усилитель сигнала", BlockCPlayableLayout.SignalAmplifierObjective },
            // Размещённые пикапы
            { "Лист приёмки кухни", BlockCPlayableLayout.KitchenManifest },
            { "Служебный пропуск", BlockCPlayableLayout.ServiceBadge },
            { "Глазной имплант", BlockCPlayableLayout.EyeImplant },
            { "Передатчик", BlockCPlayableLayout.Transmitter },
            { "Отчёты экспериментов", BlockCPlayableLayout.ExperimentReports },
            { "Ключ технологического крыла", BlockCPlayableLayout.TechWingKey },
        };

        var offenders = new List<string>();
        foreach (KeyValuePair<string, Vector2Int> entry in cells)
        {
            if (!grid.IsWalkable(entry.Value.x, entry.Value.y) || graph.ComponentAt(entry.Value) < 0)
            {
                offenders.Add($"{entry.Key} @ ({entry.Value.x},{entry.Value.y})");
            }
        }

        Assert.IsEmpty(offenders,
            "Задачи/пикапы вне комнаты (на стене/двери) — комната не зачистится:\n"
            + string.Join("\n", offenders));
    }

    private static Vector2Int EngineeringAreaCenter()
    {
        GridArea a = BlockCPlayableLayout.EngineeringArea;
        return new Vector2Int((a.MinX + a.MaxX) / 2, (a.MinY + a.MaxY) / 2);
    }
}
