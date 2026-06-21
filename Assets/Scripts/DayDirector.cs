using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Режиссёр одного тюремного дня: двигает часы, объявляет обязательные события
/// и связывает расписание с входом в эксперименты.
/// </summary>
public sealed class DayDirector : MonoBehaviour
{
    [SerializeField] private float secondsPerGameMinute = 1f;

    private float minuteAccumulator;
    private bool announcedExperiment;
    private bool announcedLightsOut;
    private bool startedLightsOutSearch;
    private GUIStyle hudStyle;

    private void Start()
    {
        if (RunState.HasPendingExperimentSummary)
        {
            DialogueUI.Instance.Show(BuildExperimentSummary(), 4.5f);
            RunState.MarkExperimentSummaryShown();
        }
    }

    private void Update()
    {
        if (DialogueUI.IsModalOpen) return;

        UpdateClock();
        UpdateExperimentAnnouncement();
        UpdateMissedExperiment();
        UpdateLightsOut();
    }

    private void UpdateClock()
    {
        if (RunState.DayPhase == DayPhase.Experiment ||
            RunState.DayPhase == DayPhase.EscortedToExperiment ||
            RunState.DayPhase == DayPhase.EscortedToCell ||
            RunState.DayPhase == DayPhase.LightsOut)
        {
            return;
        }

        float multiplier = RunState.IsRestingInBed ? 4f : 1f;
        minuteAccumulator += Time.deltaTime * multiplier;
        if (minuteAccumulator < secondsPerGameMinute) return;

        int minutes = Mathf.FloorToInt(minuteAccumulator / secondsPerGameMinute);
        minuteAccumulator -= minutes * secondsPerGameMinute;
        RunState.AdvanceTime(minutes);
    }

    private void UpdateExperimentAnnouncement()
    {
        if (announcedExperiment) return;
        if (RunState.MinuteOfDay < DaySchedule.ExperimentAnnouncementMinute) return;
        if (RunState.MinuteOfDay >= DaySchedule.ExperimentDeadlineMinute) return;

        announcedExperiment = true;
        DialogueUI.Instance.Show(
            "12:00. Объявлен сбор на эксперимент. Доберитесь до точки начала до 12:15.",
            4f);
    }

    private void UpdateMissedExperiment()
    {
        if (RunState.DayPhase == DayPhase.EscortedToExperiment ||
            RunState.DayPhase == DayPhase.EscortedToCell)
        {
            return;
        }

        bool missedWindow = RunState.MinuteOfDay >= DaySchedule.ExperimentDeadlineMinute &&
                            RunState.MinuteOfDay < DaySchedule.AfternoonStartMinute;
        if (!missedWindow) return;

        RunState.BeginForcedExperimentEscort();
        NotifyGuardsAboutScheduleViolation();
        DialogueUI.Instance.Show(
            "12:15. Вы не явились на эксперимент. Надзиратели начали розыск.",
            3f);
    }

    private static void NotifyGuardsAboutScheduleViolation()
    {
        foreach (ScheduleEnforcerGuard guard in FindObjectsByType<ScheduleEnforcerGuard>(FindObjectsSortMode.None))
        {
            guard.StartScheduleSearch();
        }

        foreach (GuardPatrol guard in FindObjectsByType<GuardPatrol>(FindObjectsSortMode.None))
        {
            guard.StartScheduleSearch();
        }
    }

    private void UpdateLightsOut()
    {
        if (RunState.DayPhase != DayPhase.LightsOut) return;

        if (!announcedLightsOut)
        {
            announcedLightsOut = true;
            DialogueUI.Instance.Show("21:00. Отбой. Вернитесь в камеру и используйте кровать.", 3f);
        }

        if (startedLightsOutSearch) return;

        Player player = FindFirstObjectByType<Player>();
        GameGrid grid = FindFirstObjectByType<GameGrid>();
        if (player == null || grid == null || grid.IsPlayerCell(player.GridPosition)) return;

        startedLightsOutSearch = true;
        RunState.BeginForcedLightsOutEscort();
        NotifyGuardsAboutScheduleViolation();
        DialogueUI.Instance.Show(
            "Вы не в камере после отбоя. Надзиратели начали розыск.",
            3f);
    }

    public void ResetForNewDay()
    {
        announcedExperiment = false;
        announcedLightsOut = false;
        startedLightsOutSearch = false;
        minuteAccumulator = 0f;
    }

    private static string BuildExperimentSummary()
    {
        ExperimentResult result = RunState.LastResult;
        if (result == null) return "";

        string outcome = result.PlayerSurvived ? "Вы выжили." : "Вы погибли.";
        string reward = result.ImplantAccepted && result.OfferedImplant.HasValue
            ? $" Получен имплант: {ImplantName(result.OfferedImplant.Value)}."
            : "";

        string relationships = "";
        foreach (KeyValuePair<NpcId, int> delta in result.RelationshipDeltas)
        {
            string sign = delta.Value > 0 ? "+" : "";
            relationships += $" {NpcName(delta.Key)}: {sign}{delta.Value}.";
        }

        return $"Итоги эксперимента. {outcome}{reward}{relationships}";
    }

    private static string ImplantName(ImplantId implant)
    {
        return implant switch
        {
            ImplantId.ReactiveFeet => "реактивные стопы",
            ImplantId.EyeImplant => "глазной имплант",
            _ => implant.ToString(),
        };
    }

    private static string NpcName(NpcId npc)
    {
        return npc switch
        {
            NpcId.Programmer => "Программист",
            NpcId.Competitor => "Заключённая",
            _ => npc.ToString(),
        };
    }

    private void OnGUI()
    {
        if (QuestJournalUI.IsOpen || InvestigationBoardUI.IsOpen) return;

        hudStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white },
        };

        GUI.Box(new Rect(6, 68, 254, 30), "");
        GUI.Label(
            new Rect(14, 74, 238, 18),
            $"День {RunState.Day} · {DaySchedule.FormatTime(RunState.MinuteOfDay)} · {RunState.ScheduleLabel}",
            hudStyle);
    }
}
