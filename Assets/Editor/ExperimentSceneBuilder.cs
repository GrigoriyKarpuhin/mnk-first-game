using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ExperimentSceneBuilder
{
    [MenuItem("Game/Build Experiment 01 Scene")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 8f;
        cameraObject.transform.position = new Vector3(0f, 6f, -10f);

        var prototypeObject = new GameObject("Experiment 01 Prototype");
        prototypeObject.AddComponent<ExperimentPrototype>();

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Experiment01.unity");

        var scenes = EditorBuildSettings.scenes;
        bool alreadyAdded = false;
        foreach (var buildScene in scenes)
        {
            if (buildScene.path == "Assets/Scenes/Experiment01.unity")
            {
                alreadyAdded = true;
                break;
            }
        }

        if (!alreadyAdded)
        {
            var updated = new EditorBuildSettingsScene[scenes.Length + 1];
            scenes.CopyTo(updated, 0);
            updated[^1] = new EditorBuildSettingsScene("Assets/Scenes/Experiment01.unity", true);
            EditorBuildSettings.scenes = updated;
        }
    }

    public static void BuildMacPlayer()
    {
        BuildPipeline.BuildPlayer(
            EditorBuildSettings.scenes,
            "/tmp/MNKExperiment.app",
            BuildTarget.StandaloneOSX,
            BuildOptions.Development
        );
    }
}
