using UnityEngine;
using UnityEngine.SceneManagement;

public static class RunState
{
    public const string PrisonScene = "SampleScene";
    public const string ExperimentScene = "Experiment01";

    private const string ReactiveFeetKey = "run.reactive-feet";

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
}
