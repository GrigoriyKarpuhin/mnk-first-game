using NUnit.Framework;

[TestFixture]
public sealed class AwarenessMeterTests
{
    [Test]
    public void Tick_RisesFasterWhenPlayerIsNear()
    {
        var near = new AwarenessMeter();
        var far = new AwarenessMeter();

        near.Tick(visible: true, normalizedDistance: 0f, dt: 0.1f,
            gainNear: 2.5f, gainFar: 0.8f, decay: 0.5f);
        far.Tick(visible: true, normalizedDistance: 1f, dt: 0.1f,
            gainNear: 2.5f, gainFar: 0.8f, decay: 0.5f);

        Assert.Greater(near.Level, far.Level);
    }

    [Test]
    public void Tick_DecaysWhenPlayerNotVisible()
    {
        var meter = new AwarenessMeter();
        meter.Tick(true, 0f, 0.2f, 2.5f, 0.8f, 0.5f); // поднять уровень
        float raised = meter.Level;

        meter.Tick(false, 0f, 0.2f, 2.5f, 0.8f, 0.5f);

        Assert.Less(meter.Level, raised);
    }

    [Test]
    public void Level_IsClampedBetweenZeroAndOne()
    {
        var meter = new AwarenessMeter();

        // Большой шаг при видимом игроке не должен превысить 1.
        meter.Tick(true, 0f, 100f, 2.5f, 0.8f, 0.5f);
        Assert.LessOrEqual(meter.Level, 1f);

        // Большой шаг спада не должен уйти ниже 0.
        meter.Tick(false, 0f, 100f, 2.5f, 0.8f, 0.5f);
        Assert.GreaterOrEqual(meter.Level, 0f);
    }

    [Test]
    public void Tick_CrossesSuspicionThreshold()
    {
        const float threshold = 0.4f;
        var meter = new AwarenessMeter();

        Assert.Less(meter.Level, threshold);
        for (int i = 0; i < 5; i++)
        {
            meter.Tick(true, 0f, 0.1f, 2.5f, 0.8f, 0.5f);
        }
        Assert.GreaterOrEqual(meter.Level, threshold);
    }

    [Test]
    public void SetMaxAndReset_SetExtremes()
    {
        var meter = new AwarenessMeter();

        meter.SetMax();
        Assert.AreEqual(1f, meter.Level);

        meter.Reset();
        Assert.AreEqual(0f, meter.Level);
    }
}
