#!/bin/bash
# Run a command while holding the shared machine lock's FOREGROUND resource.
#
# The browser check and the Sticky Keys check drive real windows and send real keystrokes, so
# they need the screen to themselves exactly as Spider's store shots do. They are not Unity runs,
# so Tools/unity.sh is the wrong wrapper - but the lock still applies, because a capture that
# loses focus dies and a keystroke sent to the wrong window is worse than a failed check.
#
# usage: Tools/with_foreground.sh <minutes> <note> <command...>
set -u
MINS="${1:-30}"; NOTE="${2:-foreground}"; shift 2
INTERVAL="${KT_RETRY_SECS:-60}"
DEADLINE=$(( $(date +%s) + MINS * 60 ))
WINPID=$(cat /proc/$$/winpid 2>/dev/null || echo $$)

while :; do
  python "D:/KT/unity_lock.py" acquire ShiftIt "$WINPID" foreground "$NOTE"
  if [ $? -eq 0 ]; then break; fi
  NOW=$(date +%s)
  if [ $NOW -ge $DEADLINE ]; then
    echo "GAVE UP waiting for the foreground: the check did not run."
    echo "  NOT a pass and NOT a failure."
    exit 75
  fi
  echo "  foreground busy, $(( (DEADLINE - NOW) / 60 )) min left; retrying in ${INTERVAL}s"
  python -c "import time;time.sleep($INTERVAL)"
done
trap 'python "D:/KT/unity_lock.py" release ShiftIt >/dev/null 2>&1' EXIT

"$@"
exit $?
