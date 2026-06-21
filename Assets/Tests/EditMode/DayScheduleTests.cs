using NUnit.Framework;

[TestFixture]
public sealed class DayScheduleTests
{
    [Test]
    public void ExperimentCheckInWindow_IsOnlyFromNoonToDeadline()
    {
        Assert.IsFalse(DaySchedule.IsExperimentCheckInWindow(11 * 60 + 59));
        Assert.IsTrue(DaySchedule.IsExperimentCheckInWindow(12 * 60));
        Assert.IsTrue(DaySchedule.IsExperimentCheckInWindow(12 * 60 + 14));
        Assert.IsFalse(DaySchedule.IsExperimentCheckInWindow(12 * 60 + 15));
    }

    [Test]
    public void PhaseForMinute_ReturnsExpectedDayPhases()
    {
        Assert.AreEqual(DayPhase.MorningFreeTime, DaySchedule.PhaseForMinute(8 * 60));
        Assert.AreEqual(DayPhase.ExperimentAssembly, DaySchedule.PhaseForMinute(12 * 60));
        Assert.AreEqual(DayPhase.MorningFreeTime, DaySchedule.PhaseForMinute(12 * 60 + 15));
        Assert.AreEqual(DayPhase.AfternoonFreeTime, DaySchedule.PhaseForMinute(13 * 60));
        Assert.AreEqual(DayPhase.LightsOut, DaySchedule.PhaseForMinute(21 * 60));
    }

    [Test]
    public void FormatTime_UsesTwoDigitClock()
    {
        Assert.AreEqual("08:00", DaySchedule.FormatTime(8 * 60));
        Assert.AreEqual("12:05", DaySchedule.FormatTime(12 * 60 + 5));
        Assert.AreEqual("21:00", DaySchedule.FormatTime(21 * 60));
    }
}
