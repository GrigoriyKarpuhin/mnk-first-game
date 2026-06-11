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
        Assert.IsTrue(grid.IsWalkable(18, 8), "Common area should be walkable");
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
        Assert.AreEqual(TileType.Floor, grid.GetTileType(18, 8));
        Assert.AreEqual(TileType.Floor, grid.GetTileType(25, 17));
    }

    [Test]
    public void ClosedDoor_IsNotWalkableAndBlocksVision()
    {
        Assert.AreEqual(TileType.Door, grid.GetTileType(34, 10));
        Assert.IsFalse(grid.IsWalkable(34, 10));
        Assert.IsTrue(grid.BlocksVision(34, 10));
    }

    [Test]
    public void Cover_IsNotWalkableAndBlocksVision()
    {
        Assert.AreEqual(TileType.Cover, grid.GetTileType(17, 19));
        Assert.IsFalse(grid.IsWalkable(17, 19));
        Assert.IsTrue(grid.BlocksVision(17, 19));
    }

    [Test]
    public void SolitaryCell_HasWallsAndSingleDoor()
    {
        Assert.AreEqual(TileType.Door, grid.GetTileType(7, 2));
        Assert.AreEqual(TileType.Wall, grid.GetTileType(7, 3));
        Assert.AreEqual(TileType.Wall, grid.GetTileType(7, 4));
        Assert.AreEqual(TileType.Door, grid.GetTileType(7, 5));
    }

    [Test]
    public void PublicAndStaffWings_AreSeparated()
    {
        for (int x = 10; x <= 33; x++)
        {
            Assert.AreEqual(TileType.Wall, grid.GetTileType(x, 13), $"Unexpected public/staff opening at x={x}");
        }
    }

    [Test]
    public void ToiletHasDoorAndVentHasNoSidePassage()
    {
        Assert.AreEqual(TileType.Door, grid.GetTileType(27, 8));
        Assert.AreEqual(TileType.Door, grid.GetTileType(34, 10));
        Assert.AreEqual(TileType.Door, grid.GetTileType(35, 15));
        Assert.AreEqual(TileType.Wall, grid.GetTileType(36, 15));
    }

    [Test]
    public void StorageSeparatesStaffAndSecureCorridors()
    {
        Assert.AreEqual(TileType.Door, grid.GetTileType(20, 17));
        Assert.AreEqual(TileType.Door, grid.GetTileType(13, 17));
        Assert.AreEqual(TileType.Floor, grid.GetTileType(17, 17));
    }

    [Test]
    public void LaboratoryDoorHasFloorOnBothSides()
    {
        Assert.AreEqual(TileType.Door, grid.GetTileType(4, 19));
        Assert.AreEqual(TileType.Floor, grid.GetTileType(4, 18));
        Assert.AreEqual(TileType.Floor, grid.GetTileType(4, 20));
        Assert.AreEqual(TileType.Wall, grid.GetTileType(5, 19));
    }

    [Test]
    public void EngineeringDoorIsOnlyOpeningInItsWall()
    {
        Assert.AreEqual(TileType.Door, grid.GetTileType(10, 19));
        Assert.AreEqual(TileType.Floor, grid.GetTileType(10, 18));
        Assert.AreEqual(TileType.Floor, grid.GetTileType(10, 20));
        Assert.AreEqual(TileType.Wall, grid.GetTileType(9, 19));
        Assert.AreEqual(TileType.Wall, grid.GetTileType(11, 19));
    }

    [Test]
    public void KitchenDoorIsOnlyOpeningToStaffCorridor()
    {
        Assert.AreEqual(TileType.Door, grid.GetTileType(37, 17));
        Assert.AreEqual(TileType.Floor, grid.GetTileType(36, 17));
        Assert.AreEqual(TileType.Floor, grid.GetTileType(38, 17));
        Assert.AreEqual(TileType.Wall, grid.GetTileType(37, 16));
        Assert.AreEqual(TileType.Wall, grid.GetTileType(37, 18));
    }

    [Test]
    public void VentExitHasObservationCoverOnItsLeft()
    {
        Assert.AreEqual(TileType.Door, grid.GetTileType(35, 15));
        Assert.AreEqual(TileType.Cover, grid.GetTileType(34, 16));
        Assert.IsTrue(grid.BlocksVision(34, 16));
    }

    [Test]
    public void ServiceGuardHasStraightUnblockedPatrolLine()
    {
        for (int x = 22; x <= 34; x++)
        {
            Assert.AreEqual(TileType.Floor, grid.GetTileType(x, 17), $"Patrol line blocked at x={x}");
        }
    }

    [Test]
    public void GuardVision_UsesForwardCone()
    {
        var guardObject = new GameObject("Test Guard");
        var guard = guardObject.AddComponent<GuardPatrol>();
        guard.Initialize(grid, new[] { new Vector2Int(22, 17), new Vector2Int(34, 17) }, grid.CreateSquareSprite());

        Assert.IsTrue(guard.CanSeeCell(new Vector2Int(25, 18)), "Cell inside the forward cone should be visible");
        Assert.IsFalse(guard.CanSeeCell(new Vector2Int(21, 17)), "Cell behind the guard should not be visible");
        Assert.IsFalse(guard.CanSeeCell(new Vector2Int(23, 19)), "Cell outside the cone should not be visible");

        Object.DestroyImmediate(guardObject);
    }

    [Test]
    public void GuardVision_IsBlockedByCover()
    {
        var guardObject = new GameObject("Test Guard");
        var guard = guardObject.AddComponent<GuardPatrol>();
        guard.Initialize(grid, new[] { new Vector2Int(17, 18), new Vector2Int(17, 22) }, grid.CreateSquareSprite());

        Assert.IsFalse(guard.CanSeeCell(new Vector2Int(17, 21)), "Cover should block cells behind it");

        Object.DestroyImmediate(guardObject);
    }
}
