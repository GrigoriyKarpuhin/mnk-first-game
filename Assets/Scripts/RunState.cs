using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public static class RunState
{
    public const string PrisonScene = "SampleScene";
    public const string ExperimentScene = "Experiment01";

    private const string ReactiveFeetKey = "run.reactive-feet";
    private static readonly HashSet<PrisonItemId> PrisonItems = new HashSet<PrisonItemId>();

    public static bool HasReactiveFeet
    {
        get => PlayerPrefs.GetInt(ReactiveFeetKey, 0) == 1;
        set
        {
            PlayerPrefs.SetInt(ReactiveFeetKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static void EnterExperiment()
    {
        SceneManager.LoadScene(ExperimentScene);
    }

    public static void ReturnToPrison()
    {
        SceneManager.LoadScene(PrisonScene);
    }

    public static int PrisonItemCount => PrisonItems.Count;

    public static bool HasPrisonItem(PrisonItemId itemId)
    {
        return PrisonItems.Contains(itemId);
    }

    public static void AddPrisonItem(PrisonItemId itemId)
    {
        PrisonItems.Add(itemId);
    }
}
