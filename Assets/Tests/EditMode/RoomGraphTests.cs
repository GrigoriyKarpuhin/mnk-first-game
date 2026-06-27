using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Инварианты построения карты Block C поверх графа комнат: каждая комната
/// герметична и имеет дверь, общий граф совпадает с нарисованным прототипом
/// (<see cref="LayoutPrototype"/>), все комнаты достижимы от старта игрока.
/// Граф строится из той же <see cref="GameGrid"/>, что и рантайм — как в
/// <see cref="LayoutIntegrityTests"/>.
/// </summary>
[TestFixture]
public class RoomGraphTests
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
    public void Build_ProducesRoomsAndEdges()
    {
        Assert.Greater(graph.Rooms.Count, 0, "Граф комнат пуст — заливка не нашла пол.");
        Assert.Greater(graph.Edges.Count, 0, "В графе нет ни одной дверной связи.");
    }

    [Test]
    public void EveryRoom_IsARoom()
    {
        var issues = LayoutValidator.ValidateRooms(graph);
        Assert.IsEmpty(issues,
            "Комнаты, не прошедшие проверку «комната — это комната» " +
            "(герметичность, вырожденность, наличие двери, тупиковые/сиротские двери):\n"
            + string.Join("\n", issues));
    }

    [Test]
    public void Graph_MatchesPrototype()
    {
        var issues = LayoutValidator.GraphMatchesPrototype(graph);
        Assert.IsEmpty(issues,
            "Граф комнат разошёлся с нарисованным прототипом LayoutPrototype " +
            "(слитые/потерянные комнаты, недостающие/лишние двери):\n"
            + string.Join("\n", issues));
    }

    [Test]
    public void EveryRoom_ReachableFromStart()
    {
        var issues = LayoutValidator.Reachability(graph);
        Assert.IsEmpty(issues,
            "Недостижимые от старта игрока комнаты (с учётом лестниц между этажами):\n"
            + string.Join("\n", issues));
    }

    [Test]
    public void ToMermaid_ProducesValidGraph()
    {
        string mermaid = RoomGraphExporter.ToMermaid(graph);
        Assert.IsNotEmpty(mermaid);
        StringAssert.StartsWith("graph TD", mermaid);
        StringAssert.Contains("A1_Atrium", mermaid, "В диаграмме нет ключевого узла атриума.");
    }
}
