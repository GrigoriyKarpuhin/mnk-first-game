#!/usr/bin/env bash
#
# driver.sh — build, launch, and screenshot the mnk-first-game prison slice
# headlessly, with no Unity GUI.
#
# It runs the PlayMode smoke test `SmokeCaptureTests` (Assets/Tests/PlayMode/),
# which loads SampleScene, lets GameGrid.Awake build the Block C prison and spawn
# the cast, then renders the live camera to a PNG. This is the agent path: one
# command, exit code = pass/fail, screenshot on disk.
#
# Usage:
#   .claude/skills/run-mnk-first-game/driver.sh [--isolated] [OUTPUT_PNG]
#
#   --isolated   run against an rsync'd shadow copy of the project under
#                $TMPDIR, so it does NOT fight a Unity Editor that already has
#                this project open (the editor holds Temp/UnityLockfile and
#                batchmode otherwise aborts with "another Unity instance is
#                running"). The shadow keeps its own Library, so re-runs are
#                incremental. The screenshot is still written to OUTPUT_PNG.
#   OUTPUT_PNG   where to write the frame (default: <project>/Logs/run-smoke-capture.png)
#
# Env overrides (forwarded to the test):
#   MNK_CAPTURE_W / MNK_CAPTURE_H   capture resolution (default 1280x720)
#   MNK_ISOLATED_DIR                shadow copy location (default $TMPDIR/mnk-first-game-run)
#
set -uo pipefail

SKILL_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$(cd "$SKILL_DIR/../../.." && pwd)"

ISOLATED=0
if [[ "${1:-}" == "--isolated" ]]; then ISOLATED=1; shift; fi

VERSION="$(awk '/m_EditorVersion:/{print $2}' "$PROJECT/ProjectSettings/ProjectVersion.txt")"
UNITY="/Applications/Unity/Hub/Editor/$VERSION/Unity.app/Contents/MacOS/Unity"

if [[ ! -x "$UNITY" ]]; then
  echo "ERROR: Unity $VERSION not found at: $UNITY" >&2
  echo "Installed editors:" >&2
  ls /Applications/Unity/Hub/Editor/ 2>/dev/null >&2 || echo "  (none)" >&2
  exit 1
fi

mkdir -p "$PROJECT/Logs"
OUT="${1:-$PROJECT/Logs/run-smoke-capture.png}"
RESULTS="$PROJECT/Logs/playmode-results.xml"
LOG="$PROJECT/Logs/run-driver.log"

export MNK_CAPTURE_PATH="$OUT"
rm -f "$OUT" "$RESULTS"

# Pick which project tree Unity opens. Isolated mode shadows it under $TMPDIR.
RUN_PROJECT="$PROJECT"
if [[ $ISOLATED -eq 1 ]]; then
  RUN_PROJECT="${MNK_ISOLATED_DIR:-${TMPDIR:-/tmp}/mnk-first-game-run}"
  echo ">> Isolated mode: syncing project -> $RUN_PROJECT (first sync copies ~2GB Library)..."
  mkdir -p "$RUN_PROJECT"
  rsync -a --delete \
    --exclude '.git/' --exclude 'Temp/' --exclude 'Logs/' \
    "$PROJECT/" "$RUN_PROJECT/"
  mkdir -p "$RUN_PROJECT/Logs"
  rm -f "$RUN_PROJECT/Temp/UnityLockfile"
fi

echo ">> Unity:    $UNITY"
echo ">> Project:  $RUN_PROJECT"
echo ">> Capture:  $OUT  (${MNK_CAPTURE_W:-1280}x${MNK_CAPTURE_H:-720})"
echo ">> Log:      $LOG"
echo ">> Running PlayMode smoke capture (first run imports assets — can take a few minutes)..."

"$UNITY" -batchmode -runTests \
  -projectPath "$RUN_PROJECT" \
  -testPlatform PlayMode \
  -testFilter "SmokeCaptureTests" \
  -testResults "$RESULTS" \
  -logFile "$LOG"
CODE=$?

echo
if grep -q "It looks like another Unity instance is running" "$LOG" 2>/dev/null; then
  echo "ERROR: another Unity instance has this project open (editor lock)." >&2
  echo "Either close the Unity Editor for this project, or re-run with --isolated." >&2
  exit 1
fi

# Surface the test outcome and our capture log line from the run.
grep -E "\[SmokeCapture\]|Tests? (passed|failed)|^Test execution|Overall result" "$LOG" 2>/dev/null | tail -6

if [[ $CODE -eq 0 && -f "$OUT" ]]; then
  echo
  echo "PASS — screenshot: $OUT"
  command -v sips >/dev/null 2>&1 && sips -g pixelWidth -g pixelHeight "$OUT" 2>/dev/null | grep pixel
  exit 0
else
  echo
  echo "FAIL (exit $CODE). Last 30 log lines:" >&2
  tail -30 "$LOG" >&2
  exit "${CODE:-1}"
fi
