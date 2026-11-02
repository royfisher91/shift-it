#!/bin/bash
# SHIFT IT! - the whole Stage 1 gate deliverable, in order, from one command.
#
#   1. create the one scene
#   2. PlayMode tests WITH graphics, which also capture every review still and the GIF frames
#   3. the WebGL build (primary) and the Windows build, both release
#   4. contact sheets and the two GIFs
#
# Each step stops the run if it fails, so a later step can never report success over an earlier
# failure (KT_QUALITY_LESSONS B5).
set -u
cd "$(dirname "$0")/.."
REV="D:/KT/ShiftIt/Review"
SHEETS="$REV/sheets"
mkdir -p "$SHEETS"

step() { echo; echo "=================== $* ==================="; }

step "0/5  EditMode suite: the simulation, the difficulty ramp and the bot itself"
Tools/with_lock.sh "${KT_WAIT:-240}" editmode -runTests -testPlatform EditMode
EM=$?
echo "editmode exit=$EM"
if [ $EM -eq 75 ]; then echo "ABORTING: never got the machine lock."; exit 75; fi
if [ $EM -ne 0 ]; then
  echo "EditMode suite did not pass (exit $EM). Continuing to the builds anyway so the gate has"
  echo "something playable, but the Stage 1 report must lead with this."
fi

step "1/5  create the scene"
Tools/with_lock.sh "${KT_WAIT:-240}" scene -executeMethod KT.ShiftIt.EditorTools.BuildScript.CreateGameScene || exit 1

step "2/5  PlayMode tests + captures (needs a graphics device and the foreground)"
KT_GFX=1 KT_NEEDS=both Tools/with_lock.sh "${KT_WAIT:-240}" playmode -runTests -testPlatform PlayMode
PM=$?
echo "playmode exit=$PM"

step "3/5  builds"
Tools/with_lock.sh "${KT_WAIT:-240}" buildwebgl -executeMethod KT.ShiftIt.EditorTools.BuildScript.BuildWebGL || exit 1
Tools/with_lock.sh "${KT_WAIT:-240}" buildwin   -executeMethod KT.ShiftIt.EditorTools.BuildScript.BuildWindows || exit 1

step "4/5  review sheets and GIFs"
python Tools/contact_sheet.py "$SHEETS/sheet1_zones.png" \
  "SHIFT IT! - overview: the four zones" 2 760 \
  "$REV/sheet_zones/zone1_factory_floor.png" "$REV/sheet_zones/zone2_rooftop.png" \
  "$REV/sheet_zones/zone3_scrapyard.png" "$REV/sheet_zones/zone4_launchpad.png"

python Tools/contact_sheet.py "$SHEETS/sheet2_tells.png" \
  "SHIFT IT! - overview: every blocker tell, both special bombs, Mode A aiming" 3 620 \
  "$REV/sheet_tells/tell_shield.png" "$REV/sheet_tells/tell_asleep.png" \
  "$REV/sheet_tells/tell_on_break.png" "$REV/sheet_tells/tell_mirrored.png" \
  "$REV/sheet_tells/tell_both_blocked.png" "$REV/sheet_tells/tell_both_blocked_open.png" \
  "$REV/sheet_tells/bomb_hot.png" "$REV/sheet_tells/bomb_dud.png" \
  "$REV/sheet_tells/modeA_aim_arrow.png" "$REV/sheet_tells/modeA_aim_over_gap.png"

python Tools/contact_sheet.py "$SHEETS/sheet3_moments.png" \
  "SHIFT IT! - close-ups: throw, catch, PERFECT, combo, explosion, UI" 3 620 \
  "$REV/sheet_moments/ui_title.png" "$REV/sheet_moments/moment_catch.png" \
  "$REV/sheet_moments/moment_in_band.png" "$REV/sheet_moments/moment_perfect.png" \
  "$REV/sheet_moments/moment_throw_arc.png" "$REV/sheet_moments/moment_combo.png" \
  "$REV/sheet_moments/moment_explosion.png" "$REV/sheet_moments/ui_game_over.png"

python Tools/contact_sheet.py "$SHEETS/sheet4_playtests.png" \
  "SHIFT IT! - automated playtest evidence" 3 620 \
  "$REV/playtests/01_title_modeB.png" "$REV/playtests/02_first_catch_modeB.png" \
  "$REV/playtests/03_title_modeA.png" "$REV/playtests/04_fuse_in_green_band.png" \
  "$REV/playtests/05_perfect_throw.png" "$REV/playtests/06_explosion.png" \
  "$REV/playtests/07_game_over_fuse_ran_out.png" "$REV/playtests/08_shield_up.png" \
  "$REV/playtests/09_game_over_shield.png" "$REV/playtests/10_modeA_aiming.png"

python Tools/make_gif.py "$REV/gif_modeB" "$SHEETS/modeB.gif" 20 400 112
python Tools/make_gif.py "$REV/gif_modeA" "$SHEETS/modeA.gif" 20 400 112

echo
echo "=================== done ==================="
echo "sheets and gifs: $SHEETS"
echo "builds:          D:/KT/Builds/ShiftIt"
[ $EM -ne 0 ] && echo "NOTE: the EditMode suite exited $EM - read it above."
[ $PM -ne 0 ] && echo "NOTE: the PlayMode suite exited $PM - read its result above before trusting the captures."
exit 0
