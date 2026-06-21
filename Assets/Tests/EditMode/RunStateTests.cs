using NUnit.Framework;

[TestFixture]
public sealed class RunStateTests
{
    [SetUp]
    public void SetUp()
    {
        RunState.ResetRun();
    }

    [TearDown]
    public void TearDown()
    {
        RunState.ResetRun();
    }

    [Test]
    public void ResetRun_RestoresDayScheduleAndQuestState()
    {
        RunState.AcceptProgrammerQuest();
        RunState.StartCompetitorTracking();
        RunState.MarkCompetitorReachedStaffRoom();
        RunState.AddEvidence(EvidenceId.AdaptiveExperimentSystem);
        RunState.AddEvidence(EvidenceId.EngineeringTransmitter);
        RunState.TryConnectEvidence(EvidenceId.AdaptiveExperimentSystem, EvidenceId.EngineeringTransmitter);
        RunState.AddPrisonItem(PrisonItemId.Transmitter);
        RunState.AddImplant(ImplantId.ReactiveFeet);
        RunState.SetTime(DaySchedule.ExperimentDeadlineMinute);
        RunState.BeginForcedExperimentEscort();
        RunState.StartNewDay();

        RunState.ResetRun();

        Assert.AreEqual(1, RunState.Day);
        Assert.AreEqual(DaySchedule.WakeUpMinute, RunState.MinuteOfDay);
        Assert.AreEqual(DayPhase.MorningFreeTime, RunState.DayPhase);
        Assert.AreEqual(ProgrammerQuestStage.NotStarted, RunState.ProgrammerQuest);
        Assert.AreEqual(CompetitorQuestStage.Unknown, RunState.CompetitorQuest);
        Assert.IsFalse(RunState.HasEvidence(EvidenceId.AdaptiveExperimentSystem));
        Assert.IsFalse(RunState.HasDeduction(DeductionId.PredictExperimentData));
        Assert.IsFalse(RunState.HasReactiveFeet);
        Assert.IsFalse(RunState.HasPrisonItem(PrisonItemId.Transmitter));
        Assert.IsFalse(RunState.HasPrisonItem(PrisonItemId.Screwdriver));
    }

    [Test]
    public void TryConnectEvidence_CreatesMatchingDeduction()
    {
        RunState.AddEvidence(EvidenceId.CompetitorVentRoute);
        RunState.AddEvidence(EvidenceId.GardenKey);

        DeductionId? deduction = RunState.TryConnectEvidence(
            EvidenceId.CompetitorVentRoute,
            EvidenceId.GardenKey);

        Assert.AreEqual(DeductionId.UnofficialStaffRoutes, deduction);
        Assert.IsTrue(RunState.HasDeduction(DeductionId.UnofficialStaffRoutes));
    }

    [Test]
    public void StartNewDay_AdvancesProgrammerQuestAfterTransmitterAnalysis()
    {
        RunState.AcceptProgrammerQuest();
        RunState.AddPrisonItem(PrisonItemId.Transmitter);
        Assert.IsTrue(RunState.CompleteProgrammerQuest());

        RunState.StartNewDay();

        Assert.AreEqual(2, RunState.Day);
        Assert.AreEqual(DaySchedule.WakeUpMinute, RunState.MinuteOfDay);
        Assert.AreEqual(DayPhase.MorningFreeTime, RunState.DayPhase);
        Assert.AreEqual(ProgrammerQuestStage.DayTwoQuestAvailable, RunState.ProgrammerQuest);
    }

    [Test]
    public void ProgrammerRoute_CollectsTechnologyAndUnlocksExperimentPreview()
    {
        RunState.AcceptProgrammerQuest();
        RunState.AddPrisonItem(PrisonItemId.Transmitter);
        Assert.IsTrue(RunState.CompleteProgrammerQuest());
        RunState.StartNewDay();

        Assert.IsTrue(RunState.BeginProgrammerDataSourceQuest());
        Assert.AreEqual(ProgrammerQuestStage.DataSourceNeeded, RunState.ProgrammerQuest);

        RunState.AddPrisonItem(PrisonItemId.DataSource);
        Assert.AreEqual(ProgrammerQuestStage.DataSourceAcquired, RunState.ProgrammerQuest);
        Assert.IsTrue(RunState.TurnInProgrammerDataSource());
        Assert.AreEqual(ProgrammerQuestStage.ComputeAccessNeeded, RunState.ProgrammerQuest);

        RunState.AddPrisonItem(PrisonItemId.ComputeModule);
        Assert.AreEqual(ProgrammerQuestStage.ComputeAccessAcquired, RunState.ProgrammerQuest);
        Assert.IsTrue(RunState.TurnInProgrammerComputeAccess());
        Assert.AreEqual(ProgrammerQuestStage.SignalAmplifierNeeded, RunState.ProgrammerQuest);

        RunState.AddPrisonItem(PrisonItemId.SignalAmplifier);
        Assert.AreEqual(ProgrammerQuestStage.SignalAmplifierAcquired, RunState.ProgrammerQuest);
        Assert.IsTrue(RunState.CompleteProgrammerRoute());

        Assert.AreEqual(ProgrammerQuestStage.Completed, RunState.ProgrammerQuest);
        Assert.IsTrue(RunState.ProgrammerPredictionUnlocked);
        Assert.IsTrue(RunState.EnsureExperimentPreview());
        Assert.IsTrue(RunState.HasQueuedExperimentPreview);
        Assert.IsFalse(string.IsNullOrEmpty(RunState.QueuedExperimentDisplayName));
    }

    [Test]
    public void StartNewDay_DoesNotResetCompletedProgrammerRoute()
    {
        RunState.AcceptProgrammerQuest();
        RunState.AddPrisonItem(PrisonItemId.Transmitter);
        Assert.IsTrue(RunState.CompleteProgrammerQuest());
        RunState.StartNewDay();
        Assert.IsTrue(RunState.BeginProgrammerDataSourceQuest());
        RunState.AddPrisonItem(PrisonItemId.DataSource);
        Assert.IsTrue(RunState.TurnInProgrammerDataSource());
        RunState.AddPrisonItem(PrisonItemId.ComputeModule);
        Assert.IsTrue(RunState.TurnInProgrammerComputeAccess());
        RunState.AddPrisonItem(PrisonItemId.SignalAmplifier);
        Assert.IsTrue(RunState.CompleteProgrammerRoute());

        RunState.MarkProgrammerAnalyzingTransmitter();
        RunState.StartNewDay();

        Assert.AreEqual(ProgrammerQuestStage.Completed, RunState.ProgrammerQuest);
        Assert.IsTrue(RunState.ProgrammerPredictionUnlocked);
    }

    [Test]
    public void EyeImplant_IsOnlyActiveAfterInstallAndToggle()
    {
        Assert.IsFalse(RunState.ToggleEyeImplant());
        Assert.IsFalse(RunState.IsImplantActive(ImplantId.EyeImplant));

        RunState.AddImplant(ImplantId.EyeImplant);
        Assert.IsTrue(RunState.ToggleEyeImplant());

        Assert.IsTrue(RunState.IsImplantActive(ImplantId.EyeImplant));
    }

    [Test]
    public void BedResting_IsDisabledDuringForcedStatesAndResetByNewDay()
    {
        RunState.BeginRestingInBed();
        Assert.IsTrue(RunState.IsRestingInBed);

        RunState.BeginForcedLightsOutEscort();
        Assert.IsFalse(RunState.IsRestingInBed);
        Assert.AreEqual(DayPhase.EscortedToCell, RunState.DayPhase);

        RunState.StartNewDay();

        Assert.IsFalse(RunState.IsRestingInBed);
        Assert.AreEqual(DayPhase.MorningFreeTime, RunState.DayPhase);
    }
}
