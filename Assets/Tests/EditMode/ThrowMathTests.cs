using NUnit.Framework;
using UnityEngine;

[TestFixture]
public sealed class ThrowMathTests
{
    // Открытое поле: всё проходимо.
    private static bool OpenField(int x, int y) => true;

    [Test]
    public void LandingCell_ClearPath_ReachesFullRange()
    {
        Vector2Int landing = ThrowMath.LandingCell(OpenField, new Vector2Int(5, 5), Vector2Int.right, 6);
        Assert.AreEqual(new Vector2Int(11, 5), landing);
    }

    [Test]
    public void LandingCell_StopsAtLastWalkableBeforeWall()
    {
        // Стена на x == 9: последняя проходимая по пути — x == 8.
        bool IsWalkable(int x, int y) => x < 9;
        Vector2Int landing = ThrowMath.LandingCell(IsWalkable, new Vector2Int(5, 5), Vector2Int.right, 6);
        Assert.AreEqual(new Vector2Int(8, 5), landing);
    }

    [Test]
    public void LandingCell_WallImmediatelyInFront_ReturnsFrom()
    {
        bool IsWalkable(int x, int y) => x <= 5; // клетка 6 уже стена
        var from = new Vector2Int(5, 5);
        Vector2Int landing = ThrowMath.LandingCell(IsWalkable, from, Vector2Int.right, 6);
        Assert.AreEqual(from, landing);
    }

    [Test]
    public void LandingCell_NeverReturnsNonWalkableCell()
    {
        bool IsWalkable(int x, int y) => x < 9;
        Vector2Int landing = ThrowMath.LandingCell(IsWalkable, new Vector2Int(5, 5), Vector2Int.right, 20);
        Assert.IsTrue(IsWalkable(landing.x, landing.y));
    }

    [Test]
    public void LandingCell_ZeroFacing_NormalizesToRight()
    {
        Vector2Int landing = ThrowMath.LandingCell(OpenField, new Vector2Int(0, 0), Vector2Int.zero, 3);
        Assert.AreEqual(new Vector2Int(3, 0), landing);
    }

    [Test]
    public void Cardinal_DiagonalCollapsesToDominantAxis()
    {
        Assert.AreEqual(new Vector2Int(0, 1), ThrowMath.Cardinal(new Vector2Int(1, 3)));
        Assert.AreEqual(new Vector2Int(-1, 0), ThrowMath.Cardinal(new Vector2Int(-4, 2)));
    }
}
