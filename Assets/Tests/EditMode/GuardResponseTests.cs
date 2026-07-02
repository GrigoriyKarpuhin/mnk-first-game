using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public sealed class GuardResponseTests
{
    private static readonly Vector2Int Target = new Vector2Int(0, 0);

    [Test]
    public void SelectNearest_PicksTheClosestEligible()
    {
        var cells = new List<Vector2Int> { new(10, 0), new(3, 0), new(6, 0) };
        var eligible = new List<bool> { true, true, true };

        List<int> chosen = GuardResponse.SelectNearest(cells, eligible, Target, 1, 100);

        Assert.AreEqual(new[] { 1 }, chosen.ToArray()); // (3,0) — ближайший
    }

    [Test]
    public void SelectNearest_SkipsIneligibleEvenIfClosest()
    {
        var cells = new List<Vector2Int> { new(1, 0), new(5, 0) };
        var eligible = new List<bool> { false, true }; // ближайший (1,0) недоступен

        List<int> chosen = GuardResponse.SelectNearest(cells, eligible, Target, 1, 100);

        Assert.AreEqual(new[] { 1 }, chosen.ToArray());
    }

    [Test]
    public void SelectNearest_RespectsCount()
    {
        var cells = new List<Vector2Int> { new(2, 0), new(4, 0), new(6, 0) };
        var eligible = new List<bool> { true, true, true };

        List<int> chosen = GuardResponse.SelectNearest(cells, eligible, Target, 2, 100);

        Assert.AreEqual(2, chosen.Count);
        Assert.AreEqual(new[] { 0, 1 }, chosen.ToArray()); // два ближайших по порядку
    }

    [Test]
    public void SelectNearest_ExcludesBeyondRadius()
    {
        var cells = new List<Vector2Int> { new(20, 0), new(5, 0) };
        var eligible = new List<bool> { true, true };

        List<int> chosen = GuardResponse.SelectNearest(cells, eligible, Target, 5, 12);

        Assert.AreEqual(new[] { 1 }, chosen.ToArray()); // (20,0) вне радиуса 12
    }

    [Test]
    public void SelectNearest_EmptyWhenNoneInRange()
    {
        var cells = new List<Vector2Int> { new(20, 0), new(30, 0) };
        var eligible = new List<bool> { true, true };

        List<int> chosen = GuardResponse.SelectNearest(cells, eligible, Target, 3, 12);

        Assert.IsEmpty(chosen);
    }
}
