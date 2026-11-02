#!/bin/bash
# SHIFT IT! - the one way this project is allowed to start Unity.
# Adapted from KT_AnimalSims/Docs/run_tests.sh (quality lesson 15: reuse KT's tools).
#
# usage: Tools/unity.sh <tag> [unity args...]
#   Tools/unity.sh compile
#   Tools/unity.sh editmode -runTests -testPlatform EditMode
#   KT_GFX=1 Tools/unity.sh playmode -runTests -testPlatform PlayMode
#   Tools/unity.sh build -executeMethod KT.ShiftIt.EditorTools.BuildScript.BuildWebGL
#
# env:
#   KT_GFX=1    drop -nographics, so the run can render and capture screenshots
#   KT_NEEDS=   what the run needs from the shared machine lock (editor|foreground|both)
#
# Four bugs were found in this wrapper on 2 Nov 2026, each of which made A FAILURE LOOK LIKE A PASS.
# They are recorded here so nobody reintroduces them:
#   1. piping the lock acquire into tail made $? the exit code of TAIL, so a refused lock read as
#      granted. Nothing is piped now and the code is checked directly.
#   2. $$ in Git Bash is the MSYS pid, invisible to Windows tasklist. Uses /proc/$$/winpid.
#   3. -quit must NOT be passed with -runTests: Unity quits before the suite runs and exits 0.
#   4. "NO RESULTS FILE" was printed for non-test runs too, which trains the reader to ignore it.
set -u

U="/c/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe"
ROOT="C:/c/Projects/KT_ShiftIt"
OUT="D:/KT/ShiftIt"

TAG="${1:-run}"; shift || true
STAMP=$(date +%Y%m%d_%H%M%S)
LOG="$OUT/logs/${TAG}_${STAMP}.log"
XML="$OUT/Review/${TAG}_${STAMP}_results.xml"
mkdir -p "$OUT/logs" "$OUT/Review"

# Results go to a STAMPED path every run. The KT reference harness deleted a stale results file so
# it could never be read as this run's result; this project may not delete files, so instead no two
# runs ever share a filename - same guarantee, nothing removed.

WINPID=$(cat /proc/$$/winpid 2>/dev/null || echo $$)
NEEDS="${KT_NEEDS:-editor}"

if tasklist //FI "IMAGENAME eq Unity.exe" 2>/dev/null | grep -qi "Unity.exe"; then
  echo "UNITY BUSY: another Unity.exe is already running. Not starting."
  exit 75
fi

python "D:/KT/unity_lock.py" acquire ShiftIt "$WINPID" "$NEEDS" "$TAG"
ACQ=$?
if [ $ACQ -ne 0 ]; then
  echo "LOCK NOT GRANTED (exit $ACQ). Do non-Unity work and retry."
  exit 75
fi
trap 'python "D:/KT/unity_lock.py" release ShiftIt >/dev/null 2>&1' EXIT

ARGS=(-batchmode -projectPath "$ROOT" -logFile "$LOG")
[ "${KT_GFX:-0}" = "1" ] || ARGS+=(-nographics)

IS_TEST=0
case " $* " in
  *" -runTests "*) IS_TEST=1 ;;
esac
if [ $IS_TEST -eq 1 ]; then
  ARGS+=(-testResults "$XML")
else
  ARGS+=(-quit)
fi

"$U" "${ARGS[@]}" "$@"
RC=$?
echo "unity exit=$RC   log=$LOG"

if grep -qE "error CS[0-9]+" "$LOG" 2>/dev/null; then
  echo "--- COMPILE ERRORS ---"
  grep -oE "[^ ]+\.cs\([0-9]+,[0-9]+\): error CS[0-9]+: .*" "$LOG" | sort -u | head -40
fi

grep -oE "\[SHIFTIT\].*" "$LOG" 2>/dev/null | tr -d '\r' | head -120

if [ $IS_TEST -eq 1 ]; then
  if [ -f "$XML" ]; then
    python "$ROOT/Tools/test_summary.py" "$XML"
  else
    echo "RESULT NO RESULTS FILE - THE SUITE DID NOT RUN."
    echo "  This is NOT a pass and NOT a test failure: it is a loud third state."
  fi
  echo "results=$XML"
fi

case $RC in
  0) ;;
  2) echo "TESTS FAILED (unity exit 2)." ;;
  3) echo "RUN CANCELLED (unity exit 3)." ;;
  4) echo "UNITY COULD NOT RUN (exit 4) - the filter matched nothing, or the editor failed to start." ;;
  *) echo "RUN DID NOT COMPLETE (unity exit $RC)." ;;
esac
exit $RC
