using NUnit.Framework;
using UnityEngine;

[TestFixture]
public sealed class CarryMathTests
{
    private static bool Open(int x, int y) => true;

    [Test]
    public void TrailCell_ReturnsCellBehindPlayer_WhenWalkable()
    {
        // Игрок в (5,5) смотрит вправо — тело держим позади, в (4,5).
        Vector2Int trail = CarryMath.TrailCell(Open, new Vector2Int(5, 5), Vector2Int.right);
        Assert.AreEqual(new Vector2Int(4, 5), trail);
    }

    [Test]
    public void TrailCell_FallsBackToPlayerCell_WhenBehindBlocked()
    {
        bool IsWalkable(int x, int y) => !(x == 4 && y == 5); // позади стена
        Vector2Int trail = CarryMath.TrailCell(IsWalkable, new Vector2Int(5, 5), Vector2Int.right);
        Assert.AreEqual(new Vector2Int(5, 5), trail);
    }

    [Test]
    public void TrailCell_HandlesVerticalFacing()
    {
        // Смотрим вверх (0,1) из (0,0) — позади (0,-1).
        Vector2Int trail = CarryMath.TrailCell(Open, new Vector2Int(0, 0), new Vector2Int(0, 1));
        Assert.AreEqual(new Vector2Int(0, -1), trail);
    }
}
