# SHIFT IT! audio needs vs KT_SoundLibrary

Library root: `D:\Desktop_Moved_20261020\KT_SoundLibrary` (abbreviated `LIB` in the table; every path is `LIB\...`).
Audited by catalogue/description only. **No clip has been auditioned**, so every YES means "matches on description and length", not "heard and approved".

Important: `CATALOGUE.csv` lists 347 clips, but only 89 distinct clips exist as WAV files on disk (the 169 files in the per-game folders are copies of those). The rest point into the Sonniss ZIPs in `C:\Users\roy-f\Downloads`, which are no longer there. Only clips that exist on disk are offered as candidates.

Licence for every candidate: Sonniss GameAudioGDC royalty-free (the only licence in the catalogue; all 347 rows are identical).

| id | candidate file (full path) | licence | fits? | note |
|---|---|---|---|---|
| tick | `D:\Desktop_Moved_20261020\KT_SoundLibrary\GoldGemMining\AUD-020\01_KT-SFX-0150.wav` (UI Button Analog Vintage Double Click, 0.17 s) | Sonniss | MAYBE | Short dry click, usable as a single tick re-pitched per beat. It is a double click, so trim to one transient. Not a real fuse tick. Alt: `D:\Desktop_Moved_20261020\KT_SoundLibrary\PrisonWarden\PW-AUD-005\03_KT-SFX-0232.wav` (thermometer beep, 0.24 s), tonal and less tick-like. The catalogued clock ticks (KT-SFX-0012/0013, 60 s fixed tempo) are not on disk and are the wrong shape anyway. |
| throw | `D:\Desktop_Moved_20261020\KT_SoundLibrary\GoldGemMining\AUD-014\01_KT-SFX-0106.wav` (Woosh Debris, 2.4 s) | Sonniss | MAYBE | Whoosh, but 2.4 s with debris character; needs trimming to about 0.3-0.5 s and may sound gritty. |
| catch | `D:\Desktop_Moved_20261020\KT_SoundLibrary\Mole\MO-AUD-011\02_KT-SFX-0170.wav` (Click Deep Mechanism Latch Button Thunk, 0.38 s) | Sonniss | MAYBE | "Thunk" per its title, right length. Mechanical latch rather than a soft hand catch; may be too clicky. |
| perfect | `D:\Desktop_Moved_20261020\KT_SoundLibrary\PubBreweryEmpire\A-AUD-007\03_KT-SFX-0112.wav` (Interface Pop High Short, 0.65 s) | Sonniss | YES | Bright pop; pitch-shift for the combo ladder. Variations on disk: `D:\Desktop_Moved_20261020\KT_SoundLibrary\GoldGemMining\AUD-020\02_KT-SFX-0110.wav` (Glassy Snap), `D:\Desktop_Moved_20261020\KT_SoundLibrary\GoldGemMining\AUD-023\02_KT-SFX-0124.wav` (Ting Coins), `D:\Desktop_Moved_20261020\KT_SoundLibrary\GoldGemMining\AUD-023\03_KT-SFX-0093.wav` (Kalimba Up). That is 4 variants plus pitch headroom. |
| shield_clang | `D:\Desktop_Moved_20261020\KT_SoundLibrary\Mole\MO-AUD-011\03_KT-SFX-0167.wav` (Metal Old File Impact Tap on Tire Iron, 0.47 s) | Sonniss | MAYBE | Real metal hit, short. A small tap rather than a big energy-shield clang; may need pitch-down plus reverb or layering. |
| dud_fizzle | `D:\Desktop_Moved_20261020\KT_SoundLibrary\PubBreweryEmpire\A-AUD-003\02_KT-SFX-0259.wav` (Putting Out Fire, Water from Bottle, 30.9 s) | Sonniss | MAYBE | Contains a hiss/extinguish but is a 31 s field recording; needs a 0.5-1 s cut and may be too watery. |
| fail_buzz | `D:\Desktop_Moved_20261020\KT_SoundLibrary\GoldGemMining\AUD-020\03_KT-SFX-0120.wav` (Interface Deny Low Fat Dark, 0.74 s) | Sonniss | YES | Short harsh deny sound. |
| explosion | none on disk | n/a | NO CANDIDATE | KT-SFX-0145 "Explosion Small Blast ... Cartoon Noisy Crash" (1.5 s) is in the catalogue and would fit, but its source ZIP is missing and it was never copied out. Fireworks (KT-SFX-0263/0264) are also ZIP-only and not cartoon. |
| zone_jingle | `D:\Desktop_Moved_20261020\KT_SoundLibrary\GoldGemMining\AUD-021\01_KT-SFX-0119.wav` (Interface Arp Reveal Down Long, 1.17 s) | Sonniss | MAYBE | Short arpeggio reveal; not confirmed to be 3 notes and it descends. Alt: `D:\Desktop_Moved_20261020\KT_SoundLibrary\GoldGemMining\AUD-023\03_KT-SFX-0093.wav` (Kalimba Up, 0.55 s). Probably needs editing or a new stinger. |
| swap_clang | `D:\Desktop_Moved_20261020\KT_SoundLibrary\Mole\MO-AUD-011\03_KT-SFX-0167.wav` (same as shield_clang) | Sonniss | MAYBE | Shared with shield_clang; pitch it up or down so the two are distinguishable. |
| open_pop | `D:\Desktop_Moved_20261020\KT_SoundLibrary\PubBreweryEmpire\A-AUD-007\03_KT-SFX-0112.wav` (Interface Pop High Short) | Sonniss | YES | Same pop as perfect; pitch it down or give perfect the other variants so they do not clash. |
| ui_hover | `D:\Desktop_Moved_20261020\KT_SoundLibrary\GoldGemMining\AUD-022\02_KT-SFX-0149.wav` (Game Play Cards Dry Flip Toss, 0.17 s) | Sonniss | MAYBE | Tiny dry tick, right length and quiet; not a true UI hover but works at low volume. |
| ui_press | `D:\Desktop_Moved_20261020\KT_SoundLibrary\GoldGemMining\AUD-020\02_KT-SFX-0110.wav` (Interface Accept Glassy Snap, 0.44 s) | Sonniss | YES | Clean accept/press. Also listed as a perfect variant; drop it from perfect if you want them distinct. |
| music_zone1 | none | n/a | NO CANDIDATE | The library has no music. `D:\Desktop_Moved_20261020\KT_SoundLibrary\PrisonWarden\PW-AUD-004\03_KT-SFX-0195.wav` (factory room-tone loop, 59 s) could be an ambient layer under music for the factory floor, but it is not music. |
| music_zone2 | none | n/a | NO CANDIDATE | No music in library (GAPS.md shows music missing for other games too). |
| music_zone3 | none | n/a | NO CANDIDATE | No music. Storm/rain beds exist (e.g. `D:\Desktop_Moved_20261020\KT_SoundLibrary\GoldGemMining\AUD-011\01_KT-SFX-0216.wav`) as ambience only. |
| music_zone4 | none | n/a | NO CANDIDATE | No music. |

## MISSING - needed from Roy

- `explosion`: big cartoon explosion, one-shot, 1-2 s, punchy boom with debris tail. (KT-SFX-0145 would do if its Sonniss ZIP can be restored.)
- `music_zone1`: factory floor, loopable, 30-60 s, driving industrial/mechanical arcade beat.
- `music_zone2`: rooftop sunset, loopable, 30-60 s, warmer and mellower, same tempo family.
- `music_zone3`: scrapyard storm, loopable, 30-60 s, tense, heavier, faster.
- `music_zone4`: night launchpad, loopable, 30-60 s, most urgent, building and space-flavoured.
- Soft gaps (MAYBE items that will probably want real replacements): a proper single fuse `tick` (clean 0.05-0.1 s transient, one-shot), a 3-note `zone_jingle` (about 1 s, one-shot), and a big `shield_clang` (metallic ring, about 0.5 s, one-shot).

## Licence note

All recommended clips are from the Sonniss GDC 2026 bundle. `LICENCE.md` says:

> "Commercial game use | YES. Worldwide, non-exclusive, royalty-free use on unlimited personal/commercial projects for the licensee's lifetime; games are expressly included in the synchronization right."
> "Sell a finished game using the sounds | YES as incorporated into the licensee's project."
> "Attribution | Not required."

So shipping them inside SHIFT IT! on itch.io (free or paid) is cleared, and editing, pitching and layering is allowed. Concerns:

- **Not CC0.** Do not put the raw WAVs in a public GitHub repo or offer them as a downloadable asset pack. `LICENCE.md` says: "Keep this KT catalogue and candidate copies private to the licensee's projects/team. Do not publish this folder as a downloadable library or bundle", and standalone redistribution is NO. If the Unity repo is public, gitignore the audio or add it only in the built game.
- **Licence version unclear.** The ZIP's embedded PDF is the older wording; Sonniss's online v2.0 (effective 27 Aug 2026) is stricter on standalone packs, and the actual download date was never established. Game use and no-attribution are the same in both, so game shipping is unaffected.
- **No AI training** on these clips.
- Original recording authors retain copyright; the individual recordist is "not separately established". This does not change the Sonniss grant.
- Nothing was auditioned, and the original ZIPs are missing, so the on-disk WAVs are the only copies.

## Counts (17 required ids)

- YES: 4 (perfect, fail_buzz, open_pop, ui_press)
- MAYBE: 8 (tick, throw, catch, shield_clang, dud_fizzle, zone_jingle, swap_clang, ui_hover)
- NO CANDIDATE: 5 (explosion, music_zone1, music_zone2, music_zone3, music_zone4)
