using NUnit.Framework;
using UnityEngine;

[TestFixture]
public sealed class GuardCombatTests
{
    private GameObject guardObject;
    private GuardPatrol guard;

    [SetUp]
    public void SetUp()
    {
        guardObject = new GameObject("Guard combat test");
        guard = guardObject.AddComponent<GuardPatrol>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(guardObject);
    }

    [Test]
    public void TakeDamage_ReducesHealthAndDisablesGuardAtZero()
    {
        Assert.AreEqual(7, guard.CurrentHealth);

        Assert.IsTrue(guard.TakeDamage(1));
        Assert.AreEqual(6, guard.CurrentHealth);
        Assert.AreEqual(6f / 7f, guard.HealthFraction, 0.0001f);
        Assert.AreEqual(GuardState.Patrol, guard.State);

        Assert.IsTrue(guard.TakeDamage(6));
        Assert.AreEqual(0, guard.CurrentHealth);
        Assert.AreEqual(GuardState.Disabled, guard.State);
        Assert.IsTrue(guard.CanBePickedUp);
    }

    [Test]
    public void TakeDamage_IgnoresDisabledGuardAndInvalidDamage()
    {
        Assert.IsFalse(guard.TakeDamage(0));
        Assert.AreEqual(guard.MaxHealth, guard.CurrentHealth);

        guard.SilentTakedown();

        Assert.IsFalse(guard.TakeDamage(1));
        Assert.AreEqual(0, guard.CurrentHealth);
    }

    [Test]
    public void RespawnAtRouteStart_RestoresGuardHealth()
    {
        guard.TakeDamage(2);

        guard.RespawnAtRouteStart();

        Assert.AreEqual(guard.MaxHealth, guard.CurrentHealth);
    }
}
