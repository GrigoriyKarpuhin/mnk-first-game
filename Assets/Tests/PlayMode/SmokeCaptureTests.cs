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

        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
        rt.Create();

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
            var prev = camera.targetTexture;
            camera.targetTexture = rt;
            camera.Render();
            camera.targetTexture = prev;
        }

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
}
