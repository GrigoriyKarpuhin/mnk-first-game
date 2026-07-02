using NUnit.Framework;

[TestFixture]
public sealed class CautionStateTests
{
    [Test]
    public void GainMultiplier_BoostsWhileTimerPositive()
    {
        Assert.AreEqual(1.6f, CautionState.GainMultiplier(2f, 1.6f));
    }

    [Test]
    public void GainMultiplier_IsOneWhenTimerElapsed()
    {
        Assert.AreEqual(1f, CautionState.GainMultiplier(0f, 1.6f));
        Assert.AreEqual(1f, CautionState.GainMultiplier(-3f, 1.6f));
    }

    [Test]
    public void GainMultiplier_NeverBelowOne()
    {
        // Даже если множитель настроен меньше 1, набор тревоги не должен замедляться.
        Assert.AreEqual(1f, CautionState.GainMultiplier(2f, 0.5f));
    }
}
