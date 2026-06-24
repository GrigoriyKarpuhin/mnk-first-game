using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editor-инструмент: «запекает» дефолтную (код-) расстановку — охрану, камеры, ящики —
/// в реальные объекты-маркеры на сцене, которые потом двигаешь руками. После запекания
/// <see cref="GameGrid"/> видит маркеры и больше не спавнит соответствующие объекты кодом.
/// Источник координат — общий <see cref="PrisonDefaults"/> (как и у рантайма).
/// </summary>
public static class LevelMarkerBaker
{
    [MenuItem("Game/Level Markers/Bake Default Guards Into Scene")]
    public static void BakeGuards()
    {
        if (!TryBegin<GuardSpawnMarker>("охраны", out GameGrid grid)) return;

        Transform parent = MakeParent("Guard Markers");
        int count = 0;
        foreach (DefaultGuard guard in PrisonDefaults.Guards())
        {
            var go = Create(guard.Name, parent);
            go.transform.position = grid.GridToWorld(guard.Cell.x, guard.Cell.y);

            var marker = go.AddComponent<GuardSpawnMarker>();
            marker.guardName = guard.Name;
            marker.kind = guard.Kind;

            // Конвойный пост — без точек (стоит на месте маркера). Патруль — точки маршрута.
            if (guard.Kind == GuardKind.Patrol)
            {
                for (int i = 0; i < guard.Route.Length; i++)
                {
                    PatrolWaypoint wp = guard.Route[i];
                    var wpGo = Create($"Точка {i + 1}", go.transform);
                    wpGo.transform.position = grid.GridToWorld(wp.Cell.x, wp.Cell.y);
                    wpGo.AddComponent<WaypointMarker>().scan = wp.Scan;
                }
            }
            count++;
        }
        Finish(parent, $"Запечено охранников: {count} (патрульные + конвойные).");
    }

    [MenuItem("Game/Level Markers/Bake Default Cameras Into Scene")]
    public static void BakeCameras()
    {
        if (!TryBegin<CameraSpawnMarker>("камер", out GameGrid grid)) return;

        Transform parent = MakeParent("Camera Markers");
        int count = 0;
        foreach (DefaultCamera cam in PrisonDefaults.Cameras())
        {
            var go = Create(cam.Name, parent);
            go.transform.position = grid.GridToWorld(cam.Cell.x, cam.Cell.y);
            var marker = go.AddComponent<CameraSpawnMarker>();
            marker.cameraName = cam.Name;
            marker.facing = ToFacingDir(cam.Facing);
            marker.range = cam.Range;
            marker.zone = cam.Zone;
            marker.response = cam.Response;
            count++;
        }
        Finish(parent, $"Запечено камер: {count}.");
    }

    [MenuItem("Game/Level Markers/Bake Default Hide Spots Into Scene")]
    public static void BakeHideSpots()
    {
        if (!TryBegin<HideSpotMarker>("ящиков", out GameGrid grid)) return;

        Transform parent = MakeParent("Hide Spot Markers");
        int count = 0;
        foreach (DefaultHideSpot spot in PrisonDefaults.HideSpots())
        {
            var go = Create(spot.Label, parent);
            go.transform.position = grid.GridToWorld(spot.Cell.x, spot.Cell.y);
            go.AddComponent<HideSpotMarker>().label = spot.Label;
            count++;
        }
        Finish(parent, $"Запечено ящиков: {count}.");
    }

    [MenuItem("Game/Level Markers/Bake Default Restricted Zones Into Scene")]
    public static void BakeRestrictedZones()
    {
        if (!TryBegin<RestrictedZoneMarker>("закрытых зон", out GameGrid grid)) return;

        Transform parent = MakeParent("Restricted Zone Markers");
        int count = 0;
        foreach (RectInt zone in PrisonDefaults.RestrictedZones())
        {
            var go = Create($"Зона {count + 1} ({zone.width}x{zone.height})", parent);
            go.transform.position = grid.GridToWorld(zone.xMin, zone.yMin);
            var marker = go.AddComponent<RestrictedZoneMarker>();
            marker.widthCells = zone.width;
            marker.heightCells = zone.height;
            count++;
        }
        Finish(parent, $"Запечено закрытых зон: {count}.");
    }

    [MenuItem("Game/Level Markers/Bake ALL Defaults Into Scene")]
    public static void BakeAll()
    {
        BakeGuards();
        BakeCameras();
        BakeHideSpots();
        BakeRestrictedZones();
    }

    // ---- Общие хелперы ----

    /// <summary>Найти GameGrid и подтвердить добавление, если маркеры этого типа уже есть.</summary>
    private static bool TryBegin<T>(string what, out GameGrid grid) where T : Object
    {
        grid = Object.FindFirstObjectByType<GameGrid>();
        if (grid == null)
        {
            EditorUtility.DisplayDialog("Нет GameGrid",
                "Открой сцену с объектом GameGrid (SampleScene) и повтори.", "OK");
            return false;
        }

        int existing = Object.FindObjectsByType<T>(FindObjectsSortMode.None).Length;
        if (existing > 0 &&
            !EditorUtility.DisplayDialog("Маркеры уже есть",
                $"В сцене уже {existing} маркер(ов) {what}. Всё равно добавить дефолтных?",
                "Добавить", "Отмена"))
        {
            return false;
        }
        return true;
    }

    private static Transform MakeParent(string name)
    {
        var parent = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(parent, "Bake Level Markers");
        return parent.transform;
    }

    private static GameObject Create(string name, Transform parent)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Bake Level Markers");
        go.transform.SetParent(parent);
        return go;
    }

    private static void Finish(Transform parent, string message)
    {
        Selection.activeGameObject = parent.gameObject;
        EditorSceneManager.MarkSceneDirty(parent.gameObject.scene);
        Debug.Log(message + " Дефолтный код-спавн этого типа теперь отключён — используются маркеры.");
    }

    private static FacingDir ToFacingDir(Vector2Int f)
    {
        if (f == Vector2Int.up) return FacingDir.Up;
        if (f == Vector2Int.left) return FacingDir.Left;
        if (f == Vector2Int.right) return FacingDir.Right;
        return FacingDir.Down;
    }
}
