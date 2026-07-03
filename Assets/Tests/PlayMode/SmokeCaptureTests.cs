using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// PlayMode smoke test that doubles as the headless "run the game" harness.
///
/// It loads the main playable scene (SampleScene), lets <c>GameGrid.Awake</c>
/// build the Block C prison (tiles, player, NPCs, guards), proves the level
/// actually came up, then renders the live camera to a PNG on disk so an agent
/// can look at the running game without a display.
///
/// Driven by <c>.claude/skills/run-mnk-first-game/driver.sh</c>. Output path and
/// resolution come from environment variables so the driver controls them:
///   MNK_CAPTURE_PATH   absolute PNG path (default: &lt;project&gt;/Logs/run-smoke-capture.png)
///   MNK_CAPTURE_W      width  in px (default 1280)
///   MNK_CAPTURE_H      height in px (default 720)
/// </summary>
public class SmokeCaptureTests
{
    const string SceneName = "SampleScene";

    static string OutputPath
    {
        get
        {
            var env = Environment.GetEnvironmentVariable("MNK_CAPTURE_PATH");
            if (!string.IsNullOrEmpty(env)) return env;
            var root = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(root, "Logs", "run-smoke-capture.png");
        }
    }

    static int EnvInt(string name, int fallback)
    {
        var v = Environment.GetEnvironmentVariable(name);
        return int.TryParse(v, out var n) && n > 0 ? n : fallback;
    }

    [UnityTest]
    public IEnumerator SampleScene_builds_and_renders_a_frame()
    {
        // Load the playable scene. In edit mode GameGrid.Awake never fires, so the
        // level is empty — PlayMode is the only way to see the actual game.
        SceneManager.LoadScene(SceneName, LoadSceneMode.Single);

        // Let Awake/Start build the grid and spawn the cast, plus a few render frames.
        for (int i = 0; i < 10; i++) yield return null;

        // Proof the game actually ran, not just that an empty scene opened.
        var player = UnityEngine.Object.FindFirstObjectByType<Player>();
        Assert.IsNotNull(player, "Player was not spawned — GameGrid did not build the level.");

        var camera = Camera.main != null
            ? Camera.main
            : UnityEngine.Object.FindFirstObjectByType<Camera>();
        Assert.IsNotNull(camera, "No camera in the scene to capture from.");

        int w = EnvInt("MNK_CAPTURE_W", 1280);
        int h = EnvInt("MNK_CAPTURE_H", 720);

        // Опциональный осмотр комнат: MNK_CAPTURE_CELLS="x,y;x,y;..." (+ MNK_CAPTURE_ZOOM)
        // снимает по кадру на каждую клетку в Logs/room-<x>-<y>.png; иначе — обычный кадр у игрока.
        var cellsEnv = Environment.GetEnvironmentVariable("MNK_CAPTURE_CELLS");
        if (!string.IsNullOrEmpty(cellsEnv))
        {
            var grid = UnityEngine.Object.FindFirstObjectByType<GameGrid>();
            var follow = camera.GetComponent<CameraFollow>();
            if (follow != null) follow.enabled = false;
            if (float.TryParse(Environment.GetEnvironmentVariable("MNK_CAPTURE_ZOOM"), out float z)
                && z > 0f && camera.orthographic)
                camera.orthographicSize = z;
            var logRoot = Directory.GetParent(Application.dataPath).FullName;
            foreach (var tok in cellsEnv.Split(';'))
            {
                var parts = tok.Split(',');
                if (parts.Length != 2 || !int.TryParse(parts[0], out int cx) || !int.TryParse(parts[1], out int cy))
                    continue;
                if (grid != null)
                {
                    Vector3 p = grid.GridToWorld(cx, cy);
                    camera.transform.position = new Vector3(p.x, p.y, camera.transform.position.z);
                }
                yield return null;
                CaptureCamera(camera, w, h, Path.Combine(logRoot, "Logs", $"room-{cx}-{cy}.png"));
            }
            Debug.Log($"[SmokeCapture] Player at {player.transform.position}; captured rooms {cellsEnv}");
            yield break;
        }

        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
        rt.Create();

        // Bind the camera to the RT so its pixel size matches the capture, then
        // relayout canvases — otherwise ScreenSpaceCamera UI (HUD, markers) lays
        // out for the headless Screen size and renders as a centered sub-region.
        var prevTarget = camera.targetTexture;
        camera.targetTexture = rt;
        Canvas.ForceUpdateCanvases();

        // Render the camera into the RT on demand. WaitForEndOfFrame is never
        // pumped in batchmode, so we can't rely on the automatic frame render;
        // SubmitRenderRequest is the URP-supported way to render synchronously.
        var request = new RenderPipeline.StandardRequest { destination = rt };
        if (RenderPipeline.SupportsRenderRequest(camera, request))
        {
            RenderPipeline.SubmitRenderRequest(camera, request);
        }
        else
        {
            // Built-in pipeline fallback (not used here — project is URP).
            camera.Render();
        }
        camera.targetTexture = prevTarget;

        var prevActive = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = prevActive;

        var path = OutputPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, tex.EncodeToPNG());

        // Guard against a blank/uniform frame (black screen = nothing rendered).
        var pixels = tex.GetPixels32();
        bool varied = false;
        for (int i = 1; i < pixels.Length; i += 97)
        {
            if (!pixels[i].Equals(pixels[0])) { varied = true; break; }
        }

        UnityEngine.Object.Destroy(tex);
        rt.Release();
        UnityEngine.Object.Destroy(rt);

        Debug.Log($"[SmokeCapture] Player spawned at {player.transform.position}; " +
                  $"wrote {w}x{h} frame to {path}");
        Assert.IsTrue(File.Exists(path), $"Screenshot was not written to {path}");
        Assert.Greater(new FileInfo(path).Length, 0, "Screenshot file is empty.");
        Assert.IsTrue(varied, "Captured frame is a single flat color — nothing rendered.");

        yield return null;
    }

    /// <summary>
    /// Открывает каждый модальный экран UI по очереди и снимает его в PNG, чтобы
    /// визуально проверить редизайн (терминальный стиль). Экраны — ScreenSpaceCamera,
    /// поэтому попадают в headless-скриншот. Каждый синглтон уничтожается после
    /// снимка, чтобы не перекрывать следующий.
    /// </summary>
    [UnityTest]
    public IEnumerator Modals_render_and_capture()
    {
        SceneManager.LoadScene(SceneName, LoadSceneMode.Single);
        for (int i = 0; i < 10; i++) yield return null;

        var player = UnityEngine.Object.FindFirstObjectByType<Player>();
        Assert.IsNotNull(player, "Player not spawned.");
        var grid = UnityEngine.Object.FindFirstObjectByType<GameGrid>();
        var camera = Camera.main != null ? Camera.main : UnityEngine.Object.FindFirstObjectByType<Camera>();

        int w = EnvInt("MNK_CAPTURE_W", 1280);
        int h = EnvInt("MNK_CAPTURE_H", 720);
        var root = Directory.GetParent(Application.dataPath).FullName;
        string Dir(string name) => Path.Combine(root, "Logs", $"ui-modal-{name}.png");

        // Журнал.
        QuestJournalUI.Toggle();
        for (int i = 0; i < 5; i++) yield return null;
        CaptureCamera(camera, w, h, Dir("journal"));
        DestroyByType<QuestJournalUI>();
        yield return null;

        // Доска расследования.
        InvestigationBoardUI.Toggle();
        for (int i = 0; i < 5; i++) yield return null;
        CaptureCamera(camera, w, h, Dir("board"));
        DestroyByType<InvestigationBoardUI>();
        yield return null;

        // Карта.
        if (grid != null)
        {
            PrisonMapUI.Open(grid, player);
            for (int i = 0; i < 5; i++) yield return null;
            CaptureCamera(camera, w, h, Dir("map"));
            DestroyByType<PrisonMapUI>();
            yield return null;
        }

        // Диалог.
        DialogueUI.Instance.ShowDialogue("НАДЗИРАТЕЛЬ", "Стой на месте, заключённый. Плановый досмотр блока C.", null);
        for (int i = 0; i < 5; i++) yield return null;
        CaptureCamera(camera, w, h, Dir("dialogue"));
        DestroyByType<DialogueUI>();

        Debug.Log("[UICapture] wrote modal screenshots to Logs/ui-modal-*.png");
        yield return null;
    }

    /// <summary>
    /// Regression for the resource bug: entering an experiment does a single-mode
    /// scene load, destroying the prison camera. Persistent (DontDestroyOnLoad)
    /// ScreenSpaceCamera UI canvases must re-bind to the new camera instead of
    /// spamming per-frame warnings (which pegged CPU/GPU). WorldCanvasCameraBinder
    /// should keep the warning count near zero across the transition.
    /// </summary>
    [UnityTest]
    public IEnumerator Experiment_transition_does_not_spam_logs()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        for (int i = 0; i < 12; i++) yield return null;

        int warnings = 0;
        Application.LogCallback handler = (msg, stack, type) =>
        {
            if (type == LogType.Warning) warnings++;
        };
        Application.logMessageReceived += handler;

        const int frames = 45;
        SceneManager.LoadScene("Experiment01", LoadSceneMode.Single);
        for (int i = 0; i < frames; i++) yield return null;

        Application.logMessageReceived -= handler;

        var cam = Camera.main != null ? Camera.main : UnityEngine.Object.FindFirstObjectByType<Camera>();
        if (cam != null)
        {
            var root = Directory.GetParent(Application.dataPath).FullName;
            CaptureCamera(cam, EnvInt("MNK_CAPTURE_W", 1280), EnvInt("MNK_CAPTURE_H", 720),
                Path.Combine(root, "Logs", "ui-experiment.png"));
        }

        Debug.Log($"[ExpTransition] {warnings} warnings over {frames} frames after prison→experiment load");
        Assert.Less(warnings, frames,
            $"Per-frame warning spam after scene transition ({warnings} in {frames} frames) — a persistent canvas likely has a dead worldCamera.");
    }

    static void DestroyByType<T>() where T : MonoBehaviour
    {
        var c = UnityEngine.Object.FindFirstObjectByType<T>();
        if (c != null) UnityEngine.Object.Destroy(c.gameObject);
    }

    static void CaptureCamera(Camera camera, int w, int h, string path)
    {
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
        rt.Create();
        // Bind the camera to the RT so its pixel dimensions match the capture size,
        // then relayout canvases — otherwise ScreenSpaceCamera UI lays out for the
        // headless Screen size and renders as a centered sub-region of the frame.
        var prevTarget = camera.targetTexture;
        camera.targetTexture = rt;
        Canvas.ForceUpdateCanvases();
        var request = new RenderPipeline.StandardRequest { destination = rt };
        if (RenderPipeline.SupportsRenderRequest(camera, request))
            RenderPipeline.SubmitRenderRequest(camera, request);
        else
            camera.Render();
        camera.targetTexture = prevTarget;

        var prevActive = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = prevActive;

        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.Destroy(tex);
        rt.Release();
        UnityEngine.Object.Destroy(rt);
    }
}
