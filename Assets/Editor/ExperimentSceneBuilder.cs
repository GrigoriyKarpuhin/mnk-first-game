using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ExperimentSceneBuilder
{
    private const string PoolResourcePath = "Assets/Resources/ExperimentPool.asset";
    private const string DefinitionsFolder = "Assets/Resources/Experiments";

    [MenuItem("Game/Build Experiment 01 Scene")]
    public static void Build()
    {
        BuildExperimentScene<ExperimentPrototype>(
            "Experiment 01 Prototype", "Assets/Scenes/Experiment01.unity");
    }

    [MenuItem("Game/Build Experiment 02 Scene")]
    public static void BuildExperiment02()
    {
        BuildExperimentScene<MemoryExperiment>(
            "Experiment 02 Memory", "Assets/Scenes/Experiment02.unity");
    }

    /// <summary>
    /// Создаёт ассеты определений и пул в Resources, чтобы диспетчер (NPC.Interact)
    /// мог загрузить пул в рантайме через Resources.Load без ссылки в сцене.
    /// </summary>
    [MenuItem("Game/Build Experiment Pool Asset")]
    public static void BuildPool()
    {
        EnsureFolder("Assets/Resources");
        EnsureFolder(DefinitionsFolder);

        ExperimentDefinition obstacle = CreateDefinition(
            "obstacle-course",
            id: "experiment.obstacle-course",
            displayName: "Полоса препятствий",
            category: ExperimentCategory.FreeForAll,
            sceneName: "Experiment01",
            minParticipants: 2,
            maxParticipants: 4);

        ExperimentDefinition memory = CreateDefinition(
            "memory-protocol",
            id: "experiment.memory-protocol",
            displayName: "Протокол памяти",
            category: ExperimentCategory.Solo,
            sceneName: "Experiment02",
            minParticipants: 1,
            maxParticipants: 4);

        ExperimentPool pool = AssetDatabase.LoadAssetAtPath<ExperimentPool>(PoolResourcePath);
        if (pool == null)
        {
            pool = ScriptableObject.CreateInstance<ExperimentPool>();
            AssetDatabase.CreateAsset(pool, PoolResourcePath);
        }

        var poolSo = new SerializedObject(pool);
        SerializedProperty experiments = poolSo.FindProperty("experiments");
        experiments.arraySize = 2;
        experiments.GetArrayElementAtIndex(0).objectReferenceValue = obstacle;
        experiments.GetArrayElementAtIndex(1).objectReferenceValue = memory;
        poolSo.ApplyModifiedProperties();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Пул экспериментов собран: {PoolResourcePath}");
    }

    private static ExperimentDefinition CreateDefinition(
        string assetName, string id, string displayName, ExperimentCategory category,
        string sceneName, int minParticipants, int maxParticipants)
    {
        string path = $"{DefinitionsFolder}/{assetName}.asset";
        ExperimentDefinition def = AssetDatabase.LoadAssetAtPath<ExperimentDefinition>(path);
        if (def == null)
        {
            def = ScriptableObject.CreateInstance<ExperimentDefinition>();
            AssetDatabase.CreateAsset(def, path);
        }

        var so = new SerializedObject(def);
        so.FindProperty("id").stringValue = id;
        so.FindProperty("displayName").stringValue = displayName;
        so.FindProperty("category").enumValueIndex = (int)category;
        so.FindProperty("sceneName").stringValue = sceneName;
        so.FindProperty("minDay").intValue = 1;
        so.FindProperty("maxDay").intValue = 0;
        so.FindProperty("minParticipants").intValue = minParticipants;
        so.FindProperty("maxParticipants").intValue = maxParticipants;
        so.FindProperty("implemented").boolValue = true;
        so.ApplyModifiedProperties();

        return def;
    }

    private static void BuildExperimentScene<T>(string objectName, string scenePath)
        where T : MonoBehaviour
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 8f;
        cameraObject.transform.position = new Vector3(0f, 6f, -10f);

        var prototypeObject = new GameObject(objectName);
        prototypeObject.AddComponent<T>();

        EditorSceneManager.SaveScene(scene, scenePath);
        AddSceneToBuildSettings(scenePath);
    }

    private static void AddSceneToBuildSettings(string scenePath)
    {
        var scenes = EditorBuildSettings.scenes;
        foreach (var buildScene in scenes)
        {
            if (buildScene.path == scenePath) return;
        }

        var updated = new EditorBuildSettingsScene[scenes.Length + 1];
        scenes.CopyTo(updated, 0);
        updated[^1] = new EditorBuildSettingsScene(scenePath, true);
        EditorBuildSettings.scenes = updated;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
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
