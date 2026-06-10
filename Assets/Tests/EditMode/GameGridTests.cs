using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Тесты для GameGrid
/// </summary>
[TestFixture]
public class GameGridTests
{
    private GameObject gridObject;
    private GameGrid grid;

    [SetUp]
    public void SetUp()
    {
        gridObject = new GameObject("TestGrid");
        grid = gridObject.AddComponent<GameGrid>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(gridObject);
    }

    [Test]
    public void Grid_WallsAtPerimeter_AreNotWalkable()
    {
        // Arrange - грид инициализируется в Awake, но нам нужно вызвать его вручную
        // В реальном тесте нужно будет рефакторить для тестируемости

        // Углы и периметр должны быть стенами
        Assert.IsFalse(grid.IsWalkable(0, 0), "Bottom-left corner should be wall");
        Assert.IsFalse(grid.IsWalkable(0, grid.Height - 1), "Top-left corner should be wall");
        Assert.IsFalse(grid.IsWalkable(grid.Width - 1, 0), "Bottom-right corner should be wall");
        Assert.IsFalse(grid.IsWalkable(grid.Width - 1, grid.Height - 1), "Top-right corner should be wall");
    }

    [Test]
    public void Grid_CenterTiles_AreWalkable()
    {
        // Центр карты должен быть полом
        int centerX = grid.Width / 2;
        int centerY = grid.Height / 2;

        Assert.IsTrue(grid.IsWalkable(centerX, centerY), "Center should be walkable floor");
    }

    [Test]
    public void Grid_OutOfBounds_IsNotWalkable()
    {
        // За пределами карты - нельзя ходить
        Assert.IsFalse(grid.IsWalkable(-1, 0), "Negative X should not be walkable");
        Assert.IsFalse(grid.IsWalkable(0, -1), "Negative Y should not be walkable");
        Assert.IsFalse(grid.IsWalkable(grid.Width, 0), "Beyond width should not be walkable");
        Assert.IsFalse(grid.IsWalkable(0, grid.Height), "Beyond height should not be walkable");
    }

    [Test]
    public void GridToWorld_CenterTile_ReturnsCorrectPosition()
    {
        // Позиция центральной клетки
        int centerX = grid.Width / 2;
        int centerY = grid.Height / 2;

        var worldPos = grid.GridToWorld(centerX, centerY);

        // Центр должен быть близко к (0, 0, 0)
        Assert.That(worldPos.x, Is.EqualTo(0.5f * grid.CellSize).Within(0.1f));
        Assert.That(worldPos.y, Is.EqualTo(0.5f * grid.CellSize).Within(0.1f));
        Assert.AreEqual(0, worldPos.z);
    }

    [Test]
    public void GetTileType_OutOfBounds_ReturnsWall()
    {
        // За пределами карты считается стеной
        Assert.AreEqual(TileType.Wall, grid.GetTileType(-1, 0));
        Assert.AreEqual(TileType.Wall, grid.GetTileType(grid.Width + 5, 0));
    }

    [Test]
    public void GetTileType_Perimeter_ReturnsWall()
    {
        // Периметр - стены
        Assert.AreEqual(TileType.Wall, grid.GetTileType(0, 0));
        Assert.AreEqual(TileType.Wall, grid.GetTileType(grid.Width - 1, grid.Height - 1));
    }

    [Test]
    public void GetTileType_Interior_ReturnsFloor()
    {
        // Внутренние клетки (не на внутренних стенах) - пол
        Assert.AreEqual(TileType.Floor, grid.GetTileType(1, 1));
        Assert.AreEqual(TileType.Floor, grid.GetTileType(2, 2));
    }

    [Test]
    public void ClosedDoor_IsNotWalkableAndBlocksVision()
    {
        Assert.AreEqual(TileType.Door, grid.GetTileType(22, 11));
        Assert.IsFalse(grid.IsWalkable(22, 11));
        Assert.IsTrue(grid.BlocksVision(22, 11));
    }

    [Test]
    public void Cover_IsNotWalkableAndBlocksVision()
    {
        Assert.AreEqual(TileType.Cover, grid.GetTileType(20, 16));
        Assert.IsFalse(grid.IsWalkable(20, 16));
        Assert.IsTrue(grid.BlocksVision(20, 16));
    }
}
