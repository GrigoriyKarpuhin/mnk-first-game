---
name: run-mnk-first-game
description: >
  Build, launch, run, play, or screenshot the mnk-first-game prison stealth game
  headlessly (no Unity GUI). Use when asked to run the game, smoke-test it, take a
  screenshot of the Block C prison level / SampleScene, or confirm a runtime change
  actually works in the running game rather than just in unit tests.
version: 1.0.0
---

# Run mnk-first-game (Unity 6 prison stealth prototype)

This is a Unity 6 (`6000.4.10f1`) 2D top-down stealth game. The main playable scene
is `Assets/Scenes/SampleScene.unity`: its `GameGrid.Awake()` procedurally builds the
"Block C" prison (tiles, doors, hide spots), then spawns the player, NPCs and guards.

You cannot click a Unity window from here, so the game is driven **headlessly** by a
PlayMode smoke test that loads the scene, lets the level build, proves the player
spawned, and renders the live camera to a PNG. The driver wraps it:

- **Driver:** [.claude/skills/run-mnk-first-game/driver.sh](driver.sh) — runs Unity in batchmode.
- **Test (the thing it runs):** [Assets/Tests/PlayMode/SmokeCaptureTests.cs](../../../Assets/Tests/PlayMode/SmokeCaptureTests.cs) — committed in the project's test suite.

> Paths below are relative to the project root (`<repo>/`), not this skill folder.

## Prerequisites

- **macOS** with Unity Editor `6000.4.10f1` installed via Unity Hub. The driver finds
  the binary from `ProjectSettings/ProjectVersion.txt` at:
  `/Applications/Unity/Hub/Editor/6000.4.10f1/Unity.app/Contents/MacOS/Unity`
- `rsync` and `sips` (both ship with macOS) — used by the `--isolated` path.

No `apt-get` / extra packages: Unity restores all UPM packages (URP, Input System,
Test Framework) from `Packages/manifest.json` on first launch.

## Run — agent path (headless screenshot)

```bash
.claude/skills/run-mnk-first-game/driver.sh --isolated
```

What it does, and what you get:

- Renders the running game to `Logs/run-smoke-capture.png` (1280×720). **Open that PNG
  and look at it** — it should show the stone-tiled prison with the player sprite, not a
  flat color. The test fails if the frame is a single flat color.
- Prints `PASS — screenshot: <path>` and the player's spawn position on success;
  exit code 0 = scene built + player spawned + frame rendered.
- Full Unity log: `Logs/run-driver.log`. NUnit result XML: `Logs/playmode-results.xml`.

Verified output from this command:

```
[SmokeCapture] Player spawned at (-60.50, -64.50, 0.00); wrote 1280x720 frame to .../Logs/run-smoke-capture.png
PASS — screenshot: .../Logs/run-smoke-capture.png
  pixelWidth: 1280
  pixelHeight: 720
```

**Why `--isolated`:** batchmode needs exclusive access to the project, but this machine
normally has the project **open in the Unity Editor** (Hub holds `Temp/UnityLockfile`).
`--isolated` runs against an `rsync`'d shadow copy under `$TMPDIR/mnk-first-game-run`
(its own `Library`, so re-runs are incremental — first sync copies ~2 GB), leaving the
editor untouched. The screenshot is still written back to `Logs/` in the real project.

If the Unity Editor is **closed**, you can drop `--isolated` to skip the copy and run
directly against the working tree (faster, mutates the real `Library/`):

```bash
.claude/skills/run-mnk-first-game/driver.sh
```

Custom output path / resolution:

```bash
MNK_CAPTURE_W=1920 MNK_CAPTURE_H=1080 \
  .claude/skills/run-mnk-first-game/driver.sh --isolated /tmp/prison.png
```

## Logic checks without rendering (EditMode tests)

For pure C# / level-geometry changes you don't need the screenshot. The EditMode test
suite (grid, layout integrity, room graph, schedule, run state) runs the same way but
faster. The room-graph / Block C layout validation has its own skill — see
`/validate-layout`. General form (same batchmode runner, `-testPlatform EditMode`,
editor must be closed or use a shadow copy as above).

## Run — human path (Unity GUI)

Open the project in Unity Hub → open `Assets/Scenes/SampleScene.unity` → press Play.
This is the only way to actually *control* the character (WASD / Input System). It is
useless headless and not scriptable from here — use the driver for automated checks.

## Gotchas (battle scars from building this)

- **Editor lock is the #1 trap.** With the project open in the Editor, plain batchmode
  aborts: `It looks like another Unity instance is running with this project open.`
  Use `--isolated`, or close the Editor. Don't kill the user's Editor process.
- **`WaitForEndOfFrame` is never pumped in batchmode** — a `[UnityTest]` that yields it
  throws `UnityTest yielded WaitForEndOfFrame, which is not evoked in batchmode.` The
  capture renders the camera synchronously via `RenderPipeline.SubmitRenderRequest`
  (the URP-supported on-demand render) instead of waiting for a frame.
- **The level only exists in PlayMode.** `GameGrid.Awake()` builds everything, and Awake
  does not run when a scene is merely opened in edit mode — so an EditMode/`-executeMethod`
  screenshot would capture an empty scene. The capture must be a PlayMode test.
- **Don't pass `-nographics`.** Rendering needs the Metal device; `-nographics` would
  give a black/empty frame. The driver omits it deliberately.
- **Don't pass `-quit` with `-runTests`.** The test runner exits on its own; exit code 0
  means all tests green.
- **Benign log noise, not failures:** an early `[Licensing] HandshakeResponse ... 505
  Unsupported protocol version` that immediately recovers (`Successfully updated license
  / Unity Personal`), and `No .NET SDKs were found` from an optional out-of-process
  build server. The run still passes. Judge success by the `PASS` line + exit 0, not by
  absence of the word "Error" in the log.
- **First isolated run is slow** (~2 GB `Library` copy + asset import). Subsequent runs
  reuse `$TMPDIR/mnk-first-game-run` and are much faster. Delete that dir to force clean.

## Troubleshooting

| Symptom (in `Logs/run-driver.log`) | Fix |
|---|---|
| `another Unity instance is running with this project open` | Add `--isolated`, or close the Unity Editor. |
| `UnityTest yielded WaitForEndOfFrame ... not evoked in batchmode` | You reintroduced a frame-wait yield; render via `RenderPipeline.SubmitRenderRequest`. |
| `Player was not spawned — GameGrid did not build the level` | Scene/bootstrap regression: `SampleScene` lost its `GameGrid`, or `Awake` threw — read the log above the assertion. |
| `Captured frame is a single flat color` | Camera rendered nothing: check `Camera.main`/MainCamera tag, URP active, no `-nographics`. |
| `Unity 6000.4.10f1 not found at ...` | Install that exact editor via Unity Hub (version comes from `ProjectSettings/ProjectVersion.txt`). |
