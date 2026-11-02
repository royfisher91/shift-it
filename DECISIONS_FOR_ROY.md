# DECISIONS_FOR_ROY — SHIFT IT!

Reversible choices made without stopping the run, per the working rules. Each one says what was
decided, why, and **how to change it** if you disagree. Nothing here needed money, a deletion or a
GitHub push.

Game Off 2026 · theme SHIFT · deadline Tue 1 Dec 2026 21:37 UK · submission-ready target Sat 28 Nov.

---

## Change request logged

**D-000 — Roy's harder/faster/more-blockers change request is the active spec (2 Nov 2026).**
The second brief replaces the first brief's numbers wherever they disagree. Specifically: fuse
3.0/−0.06/0.95 (was 3.2/min 1.25), return delay 0.55/−0.01/0.20, PERFECT band 18–42% narrowing to
18–30%, zone shift every **15** passes (was 20), Mode A sweep 140 + 5/pass capped 340, and the full
blocker ladder (shield 5, sleeping 10, on-break 10, hot 12, mirrored 18, dud 25, both-blocked 30,
swap 40). All of it is in `Assets/Resources/shiftit_balance.json`, and `SimTests.cs` asserts each
ramp against the spec so the numbers cannot drift silently.

---

## Engine and project

**D-001 — Unity 6000.6.4f1, built-in render pipeline, not URP.**
It is the only editor installed, and both the WebGL and Windows modules are present. Built-in was
chosen over URP 2D because this game is flat cartoon sprites with no real-time lighting needs: it
gives a smaller, faster WebGL build and fewer moving parts. The lamp flicker, floodlights and
lightning in zones 1–4 are additive sprites and sprite masks, which need no 2D lighting system.
*To change:* adding URP later means re-authoring every material; say so before Stage 2 if you want it.

**D-002 — One scene with a state machine, not three scenes.**
Pattern 01 says Boot/Menu/Game. This game's title screen, play and game-over are one continuous
loop with instant restart, and a scene load mid-loop would fight the "instant restart" requirement.
A single `Game` scene with `Phase.Title → PlayerHolding → … → GameOver` is the right shape here.
*To change:* the phase machine already models it; splitting into scenes is a presentation change only.

**D-003 — The simulation is a separate assembly with `noEngineReferences: true`.**
`KT.ShiftIt.Sim` physically cannot reference UnityEngine, let alone the UI — pattern 01 rule 1, made
a compile error rather than a convention. The payoff is immediate: the whole balance sweep
(thousands of simulated passes) runs in an EditMode test in about a second, with no editor, no scene
and no frames. This is what makes the 20-run numbers below cheap enough to re-measure after every
tuning change.

---

## Rules the brief did not pin down

**D-004 — A DUD bomb never also carries a blocker or a mirror.**
"Hold it" has to be the only correct action on a dud, so the fail reason is never ambiguous. Mixing a
dud with a shield would make two different rules apply at once and the player could not tell which
killed them. Enforced and tested (`DudTurn_NeverCarriesABlockerOrMirror`).
*To change:* delete the early return in `ShiftSim.RollTurn`.

**D-005 — BOTH SIDES BLOCKED is never also MIRRORED.**
Same reason: on a both-blocked turn the player is already waiting for an OPEN tell and then reacting
under a near-expired fuse. Flipping the keys on top of that is not difficulty, it is noise. Tested.

**D-006 — A fizzled dud counts as a pass.**
It advanced the run, so the difficulty ramp, the zone clock and the passes counter all advance with
it. The alternative (duds freeze progress) makes a dud feel like a punishment for playing correctly.
*To change:* remove the `CompletePass()` call in `ShiftSim.Fizzle`.

**D-007 — The PERFECT band's upper edge narrows from 42% to 30% over the first 40 passes.**
You gave the start and end values but not the rate. 40 passes lines up with the sweep-speed cap and
the swap unlock, so the whole difficulty curve tops out in the same place.
*To change:* `bandNarrowOverPasses` in the balance JSON.

**D-008 — Blocker frequency ramps 35% → 85% between pass 5 and pass 40.**
The brief fixed every twist's *unlock* and most twists' *chance*, but not how often an ordinary turn
has a blocker at all. This is the main difficulty dial and the one the balance sweep tunes.
*To change:* `blockerChanceAtUnlock` / `blockerChanceAtEnd` / `blockerRampEndPass`.

**D-009 — An early throw still scores +1; there is no "rushed" penalty, but the lever is built.**
Your spec says normal throw = +1 with no minimum hold, so a cautious player can throw instantly every
time. That is a safe strategy, but it scores badly (no combo, and a combo is worth up to 6x a plain
throw), and the 30+ both-blocked turns force waiting anyway. `rushedBelowElapsedFraction` is in the
config, defaulted to **0 = off**; set it to e.g. 0.25 to make the first quarter of the fuse score
nothing. The balance sweep reports a panic-throw bot against a skilled one so the gap is measured,
not assumed.

**D-010 — Mode A robots are nearly adjacent: 24°-wide catch windows with 4° gaps.**
Five robots across a 140° arc. The brief says "releasing between robots = floor = game over", and at
the capped 340°/s sweep a *wide* gap would make Mode A a reflex test that fails the quality bar's
"if a mechanic needs dexterity to operate, redesign it rather than tune it". With small gaps, most
releases land on *a* robot and the skill is landing on the *right* one — which is the interesting
decision. The floor-hit punishment still exists and is still real.
*To change:* `modeASlotHalfWidthDeg` (12 = a 24° window). Honest note: Mode A asks the player to
satisfy two constraints at once (be on a robot **and** be in the fuse band), and at high sweep speeds
the window is ~70 ms. This is in the Stage 1 report as a genuine playability finding, not a bug.

**D-011 — Mode A has no ON BREAK gaps.**
It already punishes the real gaps between robots; adding an empty pad would be the same failure twice
with two different tells. Mode A's blockers are NOPE dodgers and sleeping robots, as the brief says.

**D-012 — Hints are shown once per player, not once per run.**
"Shown once only" would nag on every retry if it reset, and this game is built around instant
retries. The shown-hint set persists to disk.
*To change:* stop restoring the set at boot.

**D-013 — Mode A's 5-second hold cap is a backstop the fuse already beats.**
With a 3.0 s fuse burning 1.5x while held, the fuse runs out after 2.0 s of holding at pass 0 and
sooner after that — so the 5 s cap is unreachable in normal play. It is kept as belt-and-braces and
tested in isolation. The *requirement* it serves ("no hold may reach 8 seconds") is therefore
satisfied twice over, and is tested as a property rather than as a specific limit.

---

## Where things live

**D-014 — Balance numbers in `Assets/Resources/shiftit_balance.json`, not StreamingAssets.**
Brief rule 6 wants numbers in data. StreamingAssets on WebGL can only be read asynchronously over
UnityWebRequest, which would make boot async for no gain on a jam game. A JSON `TextAsset` in
`Resources` loads synchronously on both targets and keeps every number out of code. A test asserts
the JSON and the C# defaults agree, so neither can drift.
*Trade-off:* it is baked into the build, so numbers are not patchable without a rebuild. For a
two-week jam entry that is the right trade; say the word and it moves to StreamingAssets.

**D-015 — Builds, logs, captures and review sheets all live on D:, never in the repo.**
`D:\KT\Builds\ShiftIt`, `D:\KT\ShiftIt\logs`, `D:\KT\ShiftIt\Review`. `.gitignore` blocks
`Builds/`, `Logs/`, `Review/` and `Screenshots/` so a capture can never be committed by accident,
which also keeps the visual review images in chat where you asked for them.

**D-016 — Tools/unity.sh is the only way this project starts Unity.**
Adapted from `KT_AnimalSims/Docs/run_tests.sh` rather than written fresh (quality lesson 15). It
takes the shared `D:\KT\unity.lock`, refuses to start if another Unity is up, propagates Unity's exit
code, and treats a missing results file as a loud third state. Three bugs found and fixed in it
during this run, all worth recording because each one makes a failure look like a pass:
1. `python unity_lock.py acquire … | tail` made `$?` the exit code of **tail**, so a refused lock
   read as granted. Nothing is piped now.
2. `$$` in Git Bash is the MSYS pid, invisible to `tasklist`. It uses `/proc/$$/winpid`.
3. **`-quit` must not be passed with `-runTests`** — Unity quits before the suite runs and exits 0.
   The first test run reported "unity exit 0" with no results file at all.

**D-017 — Results files are timestamped rather than deleted.**
The KT reference harness deletes a stale results XML so it cannot be read as the current run's
result. This project may not delete files, so instead no two runs ever share a filename, which gives
the same guarantee. Every run's log and results are kept under `D:\KT\ShiftIt\`.

---

## Still to decide / open questions for Roy

- **Which control mode ships.** The Stage 1 gate. Both are built; the report gives measured numbers
  for each plus an honest recommendation.
- **Chunky display font.** Stage 1 grey-box uses the LiberationSans SDF that ships with Unity. A
  real chunky display face is an art gap — I intend to draw an original block alphabet as a sprite
  atlas in Stage 2 rather than download a font, so the licence question never arises. Say if you
  would rather supply one.
- **Music and sounds.** Your library at `D:\Desktop_Moved_20261020\KT_SoundLibrary` has 169 clips and
  per-game pick lists, but no SHIFT IT! list yet. Stage 2 will audit it against the required sound
  set and report exactly what is missing rather than sourcing anything.

---

# Added during the Stage 1 build, 2 Nov 2026

## Findings that changed the code

**D-018 — The balance bot now has to spend TIME reading a turn, and that fixed a false finding.**
The first balance sweep said panic-throwing was unbeatable: the "throw instantly" bot averaged
**1,105 passes** against a skilled player's 47. That looked like a fatal design hole in the fuse.

It was not. It was the measuring device (quality lesson 6, and it cost one run exactly as that
lesson predicts). The bot read every tell — shields, sleepers, mirrors, duds — *instantly and
perfectly*, so throwing early carried no risk at all. No human can do that: the turn is only decided
at the moment you catch the bomb, so the shield, the mirror and the dud have to be **looked at**,
and that takes time you are spending on the fuse.

Each profile now has a `ReadSeconds` (average 0.52 s, skilled 0.30 s). At the moment the bot
commits, it rolls against how long it has actually been looking; if it has not read the turn it
**guesses** — coin-flip in Mode B, one-in-five in Mode A — and a guess walks into shields, throws
duds and ignores mirrors. Panic-throwing is now a gamble, which is what it is for a player.

*Why this matters beyond the bot:* it means the game's real difficulty is a **reading race against
the fuse**, not a reflex test. That is the thing to protect in tuning.

**D-019 — Mode A aim error is release-timing jitter multiplied by sweep speed, not a fixed angle.**
The bot originally carried a constant angular error in degrees, which flattered the design badly: it
made a 340 deg/s sweep exactly as accurate as a 140 deg/s one. A human's error is in *when* they let
go, so the angular error grows with the sweep. Profiles now carry `ReleaseJitterSeconds` (average
0.055 s, skilled 0.025 s) and the angle falls out of it. At the 340 deg/s cap, an average player's
release lands within about +/-19 degrees — against a 24-degree-wide robot. That is the honest
picture of Mode A at speed, and it is in the Stage 1 recommendation.

**D-020 — `blockerRampStartPass` is now separate from `unlockShield`.**
Blocker frequency used to ramp from whatever pass the shield unlocked at. That coupling meant a test
could not force one blocker *kind* without also switching the whole ramp off, and two tests failed
for that reason rather than because the game was wrong. They are now independent.

## Audio, and a licence problem worth knowing about before any push

**D-021 — Stage 1 sound effects are synthesised in code, not taken from the sound library.**
Three reasons, in order of importance:

1. **The library cannot legally go in a public repository.** `Docs/AUDIO_NEEDS.md` has the detail:
   the clips are Sonniss GDC content, cleared for use *in a shipped game* but explicitly **not** for
   redistribution as files. Game Off needs a **public** repo, so committing the WAVs is not an
   option, and a public repo that cannot build without files it does not contain is a poor entry.
2. **The library has no explosion and no music at all**, so the most important sound in the game
   could not have been wired from it today in any case.
3. Generated audio is original, so it proves the wiring is live rather than leaving a silent stub —
   which is exactly the Chippy failure in quality lesson A2 (a complete UI sound set that never
   played once).

Every sound goes through `Audio.Play(Sfx.Perfect)`, so swapping in real clips later changes no call
site. **My recommendation:** keep the generated effects, and ask you for four zone music loops that
are cleared for public redistribution. Flagged in the Stage 1 report.

**D-022 — `LICENSE` is MIT, and this one is genuinely your call before anything is pushed.**
MIT is the normal choice for a jam entry and keeps the repo simple. Since every asset in this game is
generated by the code, licensing the code licenses the art too — so if you would rather keep the art
reserved, say so and I will split it. Nothing has been pushed.

## Tooling

**D-023 — Screenshots render through the camera, not the screen.**
The HUD canvas is `ScreenSpaceCamera` rather than `ScreenSpaceOverlay`. An Overlay canvas draws
straight to the back buffer and does **not** appear when a camera is rendered into a RenderTexture,
so every automated screenshot would have shown the game with no UI on it — a review sheet that looks
complete and proves nothing (quality lesson B4). It renders identically either way.

**D-024 — The run wrapper keeps a written list of its own bugs.**
`Tools/unity.sh` carries a comment naming four bugs found in it during this build, every one of
which made a failure look like a pass: the piped lock check, the MSYS pid, `-quit` cancelling
`-runTests`, and "no results file" being printed on runs that never asked for tests. They are
recorded so the next person does not reintroduce them.
