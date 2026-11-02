#!/bin/bash
# Retry a Tools/unity.sh invocation until the shared machine lock frees.
#
# Exit 75 from unity.sh means "another run holds a resource this needs". The KT rule is to do
# non-Unity work and try again rather than idling or asking Roy, and ShiftIt is the lowest
# priority holder on this machine (Mole, Spider and PrisonWarden come first), so a wait of
# several minutes is normal and not an error.
#
# usage: Tools/with_lock.sh <minutes> <tag> [unity args...]
set -u
MINS="${1:-30}"; shift
DEADLINE=$(( $(date +%s) + MINS * 60 ))
ATTEMPT=0
while :; do
  ATTEMPT=$((ATTEMPT+1))
  "$(dirname "$0")/unity.sh" "$@"
  RC=$?
  if [ $RC -ne 75 ]; then exit $RC; fi
  NOW=$(date +%s)
  if [ $NOW -ge $DEADLINE ]; then
    echo "GAVE UP after ${MINS} min and ${ATTEMPT} attempts: the lock never freed."
    echo "  This is NOT a pass and NOT a failure - the run did not happen."
    exit 75
  fi
  echo "  [$ATTEMPT] lock busy, $(( (DEADLINE - NOW) / 60 )) min left; retrying in 60s"
  python -c "import time;time.sleep(60)"
done
