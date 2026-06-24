using NUnit.Framework;
using UnityEngine;

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
    public void Grid_UsesPlayableSliceDimensionsAndClosedPerimeter()
    {
        Assert.AreEqual(BlockCPlayableLayout.Width, grid.Width);
        Assert.AreEqual(BlockCPlayableLayout.Height, grid.Height);
        Assert.IsFalse(grid.IsWalkable(0, 0));
        Assert.IsFalse(grid.IsWalkable(grid.Width - 1, grid.Height - 1));
    }

    [Test]
    public void AtriumAndPlayerCell_AreConnectedBySingleDoor()
    {
        Assert.IsTrue(grid.IsWalkable(17, 5));
        Assert.AreEqual(TileType.Door, grid.GetTileType(17, 8));
        Assert.IsTrue(grid.IsWalkable(17, 9));
        Assert.AreEqual(TileType.Wall, grid.GetTileType(16, 8));
        Assert.AreEqual(TileType.Wall, grid.GetTileType(18, 8));
    }

    [Test]
    public void RevisionPanel_IsVisibleDoorAndRequiresOpeningBeforeCrawlspace()
    {
        Assert.AreEqual(TileType.Door, grid.GetTileType(83, 36));
        Assert.IsTrue(grid.BlocksVision(83, 36));
        Assert.IsTrue(grid.IsWalkable(83, 35));
        Assert.IsTrue(grid.IsWalkable(83, 37));
        Assert.IsTrue(grid.IsWalkable(83, 40));
        Assert.IsTrue(grid.IsWalkable(83, 41));
    }

    [Test]
    public void SanitaryWing_HasAnglesAndTwoRoomLoops()
    {
        Assert.IsTrue(grid.IsWalkable(58, 21), "Entry corridor should be open");
        Assert.IsTrue(grid.IsWalkable(67, 30), "North turn should be open");
        Assert.IsTrue(grid.IsWalkable(75, 33), "Upper crossing should be open");
        Assert.AreEqual(TileType.Door, grid.GetTileType(66, 16), "Changing room should connect to showers");
        Assert.AreEqual(TileType.Door, grid.GetTileType(75, 16), "Showers should connect to drying");
        Assert.AreEqual(TileType.Door, grid.GetTileType(81, 24), "Drying return should enter housekeeping");
        Assert.AreEqual(TileType.Door, grid.GetTileType(78, 27), "Staff rooms should connect room-to-room");
    }

    [Test]
    public void Kitchen_IsNorthOfSanitaryWingAndHasReservedExpansionWalls()
    {
        Assert.IsTrue(grid.IsWalkable(72, 46), "Main kitchen should be walkable");
        Assert.IsTrue(grid.IsWalkable(83, 44), "Dishwashing room should be walkable");
        Assert.AreEqual(TileType.Door, grid.GetTileType(88, 45), "Service shortcut should be a gate");
        Assert.AreEqual(TileType.Wall, grid.GetTileType(93, 52), "East expansion should remain reserved");
        Assert.AreEqual(TileType.Wall, grid.GetTileType(72, 57), "North expansion should remain reserved");
    }

    [Test]
    public void StaffRoute_PreservesManifestBadgeAndEngineeringOrder()
    {
        Assert.AreEqual(TileType.Door, grid.GetTileType(95, 48));
        Assert.AreEqual(TileType.Door, grid.GetTileType(105, 46));
        Assert.AreEqual(TileType.Door, grid.GetTileType(109, 50));
        Assert.AreEqual(TileType.Door, grid.GetTileType(120, 60));
        Assert.IsTrue(grid.IsWalkable(96, 52));
        Assert.IsTrue(grid.IsWalkable(108, 46));
        Assert.IsTrue(grid.IsWalkable(118, 64));
    }

    [Test]
    public void RestrictedCells_LeavePublicHubAndSanitaryRoomsSafe()
    {
        Assert.IsFalse(grid.IsRestrictedCell(31, 31));
        Assert.IsFalse(grid.IsRestrictedCell(62, 28));
        Assert.IsTrue(grid.IsRestrictedCell(72, 46));
        Assert.IsTrue(grid.IsRestrictedCell(96, 52));
        Assert.IsTrue(grid.IsRestrictedCell(120, 64));
        Assert.IsTrue(grid.IsRestrictedCell(7, 42));
    }

    [Test]
    public void SecondFloor_IsSeparatedAndKeepsContinuousGallery()
    {
        Vector2Int west = BlockCPlayableLayout.WestStairFloor2;
        Vector2Int east = BlockCPlayableLayout.EastStairFloor2;
        Assert.IsTrue(grid.IsWalkable(west.x, west.y));
        Assert.IsTrue(grid.IsWalkable(east.x, east.y));
        Assert.IsTrue(grid.IsWalkable(17, 50 + BlockCPlayableLayout.Floor2OffsetY));
        Assert.AreEqual(TileType.Wall, grid.GetTileType(31, 31 + BlockCPlayableLayout.Floor2OffsetY));
        Assert.AreEqual(TileType.Door, grid.GetTileType(13, 41 + BlockCPlayableLayout.Floor2OffsetY));
    }

    [Test]
    public void GridToWorld_CenterTile_ReturnsCorrectPosition()
    {
        int centerX = grid.Width / 2;
        int centerY = grid.Height / 2;
        Vector3 world = grid.GridToWorld(centerX, centerY);
        Assert.That(world.x, Is.EqualTo(0.5f * grid.CellSize).Within(0.1f));
        Assert.That(world.y, Is.EqualTo(0.5f * grid.CellSize).Within(0.1f));
    }

    [Test]
    public void OutOfBounds_IsAlwaysWall()
    {
        Assert.IsFalse(grid.IsWalkable(-1, 0));
        Assert.IsFalse(grid.IsWalkable(grid.Width, 0));
        Assert.AreEqual(TileType.Wall, grid.GetTileType(0, grid.Height));
    }

    [Test]
    public void ServiceGuardPatrolLine_IsUnblocked()
    {
        for (int x = 90; x <= 103; x++)
        {
            Assert.AreEqual(TileType.Floor, grid.GetTileType(x, 46), $"Service patrol blocked at x={x}");
        }
    }

    [Test]
    public void GuardVision_StillUsesCoverAndForwardCone()
    {
        var guardObject = new GameObject("Test Guard");
        var guard = guardObject.AddComponent<GuardPatrol>();
        guard.Initialize(grid, new[] { new Vector2Int(90, 46), new Vector2Int(103, 46) }, grid.CreateSquareSprite());

        Assert.IsTrue(guard.CanSeeCell(new Vector2Int(93, 47)));
        Assert.IsFalse(guard.CanSeeCell(new Vector2Int(89, 46)));
        Assert.IsFalse(guard.CanSeeCell(new Vector2Int(91, 49)));

        Object.DestroyImmediate(guardObject);
    }

    [Test]
    public void EngineeringCircuit_OpensSecretRouteBackToStorage()
    {
        Assert.AreEqual(TileType.Wall, grid.GetTileType(113, 49));
        Assert.AreEqual(TileType.Wall, grid.GetTileType(115, 67));

        var puzzleObject = new GameObject("Test Engineering Puzzle");
        var puzzle = puzzleObject.AddComponent<EngineeringCircuitPuzzle>();
        puzzle.Initialize(
            grid,
            null,
            grid.CreateSquareSprite(),
            grid.CreateSquareSprite(),
            new Vector2Int(108, 41),
            BlockCPlayableLayout.EngineeringArea,
            BlockCPlayableLayout.EngineeringSecretPassage());

        RotateNode(puzzleObject, new Vector2Int(117, 62), 3);
        RotateNode(puzzleObject, new Vector2Int(117, 63), 3);
        RotateNode(puzzleObject, new Vector2Int(118, 63), 3);
        RotateNode(puzzleObject, new Vector2Int(119, 63), 2);
        RotateNode(puzzleObject, new Vector2Int(119, 64), 3);
        RotateNode(puzzleObject, new Vector2Int(119, 65), 1);
        RotateNode(puzzleObject, new Vector2Int(120, 65), 3);

        Assert.AreEqual(TileType.Floor, grid.GetTileType(113, 49));
        Assert.AreEqual(TileType.Floor, grid.GetTileType(115, 67));
        Object.DestroyImmediate(puzzleObject);
    }

    [Test]
    public void BlockCShortcut_OpensRouteToStorageWithoutOpeningGardenGeometry()
    {
        Assert.AreEqual(TileType.Wall, grid.GetTileType(120, 48));
        grid.OpenBlockCShortcut();

        for (int x = 113; x <= 130; x++)
        {
            Assert.AreEqual(TileType.Floor, grid.GetTileType(x, 48), $"Shortcut blocked at x={x}");
        }
        Assert.AreEqual(TileType.Door, grid.GetTileType(13, 41));
    }

    private static void RotateNode(GameObject puzzleObject, Vector2Int cell, int times)
    {
        CircuitNode node = null;
        foreach (CircuitNode candidate in puzzleObject.GetComponentsInChildren<CircuitNode>())
        {
            if (candidate.Cell == cell)
            {
                node = candidate;
                break;
            }
        }

        Assert.IsNotNull(node, $"Missing circuit node at {cell}");
        for (int i = 0; i < times; i++) node.Interact(null);
    }
}
