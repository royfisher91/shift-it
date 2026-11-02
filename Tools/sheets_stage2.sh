#!/bin/bash
# Build the Stage 2 visual review sheets, grouped the way Roy asked for them:
#   1a/1b/1c. overview per group - zones, HUD, blockers - framed tight and labelled
#   2.        close-up detail - explosion, bombs, fuse ring
#   3.        before / after for everything that changed
# The NEW / FIXED / UNCHANGED checklist lives in the gate report.
#
# Crops are fractions of the 1920x1080 frame, so each panel shows the thing it is about rather than
# four fifths of empty backdrop. Captions after a pipe override the sidecar, which matters when
# several panels are crops of the SAME source image.
set -u
cd "$(dirname "$0")/.."
R=D:/KT/ShiftIt/Review
S=$R/sheets
B=$R/before_stage2
mkdir -p "$S"

ROBOTS='@0.08,0.235,0.84,0.35'
PLAY='@0.04,0.055,0.92,0.89'
BOMB='@0.345,0.385,0.31,0.42'
HUD_L='@0.0,0.0,0.265,0.195'
HUD_C='@0.325,0.0,0.35,0.135'
HUD_R='@0.715,0.0,0.285,0.105'

echo "=== 1a. overview: zones ==="
python Tools/contact_sheet.py "$S/s2_1a_zones.png" \
  "SHIFT IT! Stage 2 - overview: the four zones" 2 820 \
  "$R/sheet_zones/zone1_factory_floor.png$PLAY|ZONE 1 FACTORY FLOOR (0-14) - panels, pipes, 3 flickering lamps, hazard floor, safety sign" \
  "$R/sheet_zones/zone2_rooftop.png$PLAY|ZONE 2 ROOFTOP (15-29) - sunset gradient, low sun, skyline, ledge, spinning AC fans" \
  "$R/sheet_zones/zone3_scrapyard.png$PLAY|ZONE 3 SCRAPYARD (30-44) - storm sky, drift, rain, crane, tyres, puddles" \
  "$R/sheet_zones/zone4_launchpad.png$PLAY|ZONE 4 LAUNCHPAD (45+) - night sky, stars, floodlights, red gantry, KT-1 rocket"

echo "=== 1b. overview: HUD and screens ==="
python Tools/contact_sheet.py "$S/s2_1b_hud.png" \
  "SHIFT IT! Stage 2 - overview: HUD and screens" 3 600 \
  "$R/sheet_moments/moment_combo.png$HUD_L|SCORE and COMBO, top left" \
  "$R/sheet_moments/moment_combo.png$HUD_C|ZONE NAME and PASSES, top centre" \
  "$R/sheet_moments/moment_combo.png$HUD_R|PERSONAL BEST, top right" \
  "$R/sheet_ui/ui_title_modeB_only.png$PLAY|TITLE - states the controls; no mode toggle now Mode B ships" \
  "$R/sheet_ui/ui_quit_confirm.png$PLAY|ESC quit confirm - the game is PAUSED behind it" \
  "$R/sheet_moments/ui_game_over.png$PLAY|GAME OVER - reason line, score, passes, personal best"

echo "=== 1c. overview: every blocker tell ==="
python Tools/contact_sheet.py "$S/s2_1c_blockers.png" \
  "SHIFT IT! Stage 2 - overview: every blocker tell, side by side" 2 860 \
  "$R/sheet_tells/tell_shield.png$ROBOTS|SHIELD (5+) - cyan arc, red X eyes. Bounces back." \
  "$R/sheet_tells/tell_asleep.png$ROBOTS|ASLEEP (10+) - eyes shut, floating Zzz, and the idle bob STOPS." \
  "$R/sheet_tells/tell_on_break.png$ROBOTS|ON BREAK (10+) - robot gone, dashed pad and sign. Hits the floor." \
  "$R/sheet_tells/tell_mirrored.png$ROBOTS|MIRRORED (18+) - the key labels SWAP sides and turn pink." \
  "$R/sheet_tells/tell_both_blocked.png$ROBOTS|BOTH BLOCKED (30+) - two shields up. You have to wait." \
  "$R/sheet_tells/tell_both_blocked_open.png$ROBOTS|...and one OPENS part way through the fuse."

echo "=== 2. close-up detail ==="
python Tools/contact_sheet.py "$S/s2_2_closeups.png" \
  "SHIFT IT! Stage 2 - close-ups: explosion, bombs, fuse ring" 3 620 \
  "$R/sheet_moments/moment_explosion.png$BOMB|EXPLOSION - bright core, fire, smoke, debris" \
  "$R/sheet_moments/moment_catch.png$BOMB|CATCH - fuse ring full, green band on the track" \
  "$R/sheet_moments/moment_in_band.png$BOMB|IN THE GREEN - the ring tip has entered the band" \
  "$R/sheet_moments/moment_perfect.png$BOMB|PERFECT - green burst and the combo pop" \
  "$R/sheet_tells/bomb_hot.png$BOMB|RED HOT (12+) - half the fuse, double points" \
  "$R/sheet_tells/bomb_dud.png$BOMB|GREEN DUD (25+) - no band at all, and it does not tick"

echo "=== 3. before / after ==="
for g in sheet_zones sheet_moments sheet_tells; do
  python Tools/before_after.py "$B/$g" "$R/$g" "$S/s2_3_beforeafter_${g#sheet_}.png" \
    "SHIFT IT! Stage 1 -> Stage 2: $g" 620
done

echo
echo "sheets: $S"
ls -lh "$S"/s2_*.png 2>/dev/null | awk '{print $5, $9}'
