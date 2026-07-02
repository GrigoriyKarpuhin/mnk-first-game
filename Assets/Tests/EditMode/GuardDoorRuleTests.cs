using NUnit.Framework;

[TestFixture]
public sealed class GuardDoorRuleTests
{
    [Test]
    public void Chase_BreaksThroughAnyDoor()
    {
        // В погоне охрана выламывает даже запечатанную дверь с пропуском.
        Assert.IsTrue(GuardDoorRule.CanPass(isChase: true, hasDoorObject: true, canNpcTraverse: false, isSealed: true));
    }

    [Test]
    public void Patrol_OpensFreeUnsealedDoor()
    {
        // Обычную свободную дверь на пути охрана открывает и вне погони — её не запереть.
        Assert.IsTrue(GuardDoorRule.CanPass(isChase: false, hasDoorObject: true, canNpcTraverse: true, isSealed: false));
    }

    [Test]
    public void Patrol_CannotOpenDoorRequiringPass()
    {
        Assert.IsFalse(GuardDoorRule.CanPass(isChase: false, hasDoorObject: true, canNpcTraverse: false, isSealed: false));
    }

    [Test]
    public void Patrol_CannotOpenSealedDoor()
    {
        Assert.IsFalse(GuardDoorRule.CanPass(isChase: false, hasDoorObject: true, canNpcTraverse: true, isSealed: true));
    }

    [Test]
    public void Patrol_PassesBareDoorTileWithoutDoorObject()
    {
        Assert.IsTrue(GuardDoorRule.CanPass(isChase: false, hasDoorObject: false, canNpcTraverse: false, isSealed: false));
    }
}
