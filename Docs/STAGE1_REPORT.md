# SHIFT IT! — Stage 1 prototype gate

**Date:** 2 November 2026 · **Unity:** 6000.6.4f1 · **Targets:** WebGL (primary, itch.io) + Windows x64
**Jam:** GitHub Game Off 2026, theme SHIFT. Deadline Tue 1 Dec 21:37 UK; submission-ready target Sat 28 Nov.

> This is the Stage 1 gate. Both control modes are built and measured. **The one decision I need
> from you is which mode ships.** My recommendation is at the end, with the numbers behind it.

---

## 1. The recommendation first

**Ship MODE B (Left Shift / Right Shift). I'd cut Mode A rather than keep both.**

Not a preference — the measurements say so, and they say it twice.

| | target | **MODE B** | **MODE A** |
|---|---|---|---|
| New player usually dies between | 15–35 passes | **median 26–30** ✅ | **median 7** ❌ |
| Skilled player can reach | 60+ | **mean 61, best 299** ✅ | mean 35–37, best 66 ❌ |
| Run length | 30 s – 3 min | **64 s mean** ✅ | 25 s mean ❌ |
| What kills a new player | should be varied | duds 33, on-break 53, shield 52, asleep 47, fuse 15 — **spread across every twist** | **ReleasedBetween 53 of 60 (88%)** — one failure, over and over |

(200 runs per profile for the target checks, 20 per profile for the headline sweep, as you asked.)

### Why Mode A fails, and why it is not a tuning problem

88% of an average player's deaths in Mode A are *letting go between two robots*. That is not the
game being hard, it is one mechanical skill acting as a gate on everything else.

The reason is geometry against human hands. Five robots span a 140° arc, so each owns 28° and I gave
each a 24° catch window with 4° gaps. A player's error is in **when** they release, so the angular
error is their timing jitter multiplied by the sweep speed — and your ramp takes the sweep from
140°/s to 340°/s. An average release jitter of ~55 ms is ±7.7° at the start and **±18.7° at the
cap**, against a window of ±12°. At speed, missing is the default outcome.

That is your own quality bar talking: *"If a mechanic needs dexterity to operate, redesign it rather
than tune it."*

### I tested the obvious fix and it does not work

My hypothesis was that Mode A asks for two things at once — arrow on a robot **and** ring tip in the
green — and that those probabilities multiply. So I built the alternative (PERFECT judged on aim
accuracy instead of fuse timing) behind a config flag and measured it.

**The hypothesis was wrong.** Mode A's band-hit rate is already ~89% for an average bot, so the band
was never the problem. And the aim-judged variant changes only scoring, not survival: identical
passes (9.1 and 35.0), while *lowering* scores from 36.8 to 19.6 and 179.6 to 145.2. It is strictly
worse. Recorded because a measurement that kills your own idea is worth more than one that confirms it.

The lever that *would* move Mode A is geometry — widening the robots until the gaps vanish — and I
have measured that too (section 4). But it works by deleting your "release between robots = the bomb
hits the floor" rule, which is one of Mode A's two real ideas.

### The positive case for Mode B

- **The skill is reading, not reflexes.** Mode B's difficulty is identifying which side is throwable
  before the fuse runs out. That scales indefinitely — every twist you added makes a binary choice
  richer without asking for more dexterity.
- **Seven of the eight twists in your change request are Mode-B-shaped.** Mirrored keys, both sides
  blocked, the L-SHIFT/R-SHIFT labels, on-break — all of them need two named sides.
- **The control *is* the theme.** Two Shift keys, left and right, mapping to left and right.
- **It works on touch** unchanged: tap the left or right half of the screen.
- **The deaths are varied**, so the game reads as different problems rather than one wall.

---

## 2. The difficulty curve is doing what it should

`panic-throw: mean passes 10.8, mean score 11.1` against `skilled: mean passes 61.0, mean score 314.0`

That line is the whole design working. Throwing instantly is safe from the fuse but you have not
read the turn, so you guess which side — and guessing dies at ~11 passes with a score of 11. Playing
the band properly reaches 61 passes and 314 points. **Holding your nerve is worth about 28× as much
as panicking**, which is exactly the dare the game is built on.

Every twist was verified to actually reach the player across a long run — not merely configured:

```
twists reached: bombs=Normal/Hot/Dud  blockers=Shield/OnBreak/Asleep
                mirrored=True  bothBlocked=True  opened=True  swap=True  deepest pass=179
```

---

## 3. An important correction: my first balance run was wrong, and the bot was the bug

The first sweep reported panic-throwing as **unbeatable** — 1,105 passes against a skilled player's
47. It looked like a fatal hole in the fuse mechanic.

It was the measuring device. The bot read every shield, sleeper, mirror and dud *instantly and
perfectly*, so throwing early carried no risk whatsoever. No human can do that: the turn is only
decided at the moment you catch the bomb, so the tells have to be **looked at**, and that costs you
fuse.

Each bot profile now has a `ReadSeconds` (average 0.52 s, skilled 0.30 s). At the moment it commits
it rolls against how long it has actually been looking, and if it has not read the turn it
**guesses**. The same error was in Mode A, where aim error was a fixed angle — which made a 340°/s
sweep exactly as accurate as a 140°/s one. It is now release jitter × sweep speed.

This is `KT_QUALITY_LESSONS` lesson 6 collecting its due: when the numbers look dramatic, suspect the
instrument first. It cost one run, and the lesson predicted it.

---

## 4. Scope checklist

*(filled in from the pipeline run — see sections 5–8 for evidence)*

---

## 5. Sticky Keys / Filter Keys results

*(pending)*

---

## 6. Known bugs and honest gaps

*(pending)*

---

## 7. Sounds and music still needed from you

Full audit in `Docs/AUDIO_NEEDS.md`. Coverage against the 18 sounds this game needs:
**4 usable as-is, 8 usable with editing, 5 with no candidate at all.**

### There is a licence problem with the public repo, and you should see it before we push

Your library is Sonniss GDC content. `LICENCE.md` is clear that it is cleared for use *in a shipped
game* ("Commercial game use | YES") but **not** for redistribution as files: *"Do not publish this
folder as a downloadable library or bundle."*

Game Off requires a **public** GitHub repository. So the WAVs cannot be committed, and a public repo
that cannot build without files it does not contain is a weak entry.

**What I did about it:** Stage 1 synthesises every sound effect in code — the fuse tick, throw
whoosh, catch thunk, PERFECT ding, shield clang, dud fizzle, fail buzzer, explosion and the
three-note zone jingle are all oscillators and filtered noise. That is original, ships cleanly in a
public repo, and — more importantly — it means the audio is *actually wired and audible* rather than
a silent stub that looks fine (the Chippy failure in lesson A2). Everything goes through
`Audio.Play(Sfx.Perfect)`, so dropping real clips in later changes no call site.

### What I need from you

- **4 × zone music loops**, 30–60 s, loopable, **cleared for public redistribution**: factory floor
  (driving/mechanical), rooftop sunset (warmer, same tempo family), scrapyard storm (tense, heavier),
  night launchpad (most urgent). The library has **no music at all**.
- **A cartoon explosion**, 1–2 s. There is no explosion on disk — the catalogued one
  (`KT-SFX-0145`) points into a Sonniss ZIP that is no longer in Downloads.
- Nice-to-have, since the generated versions are serviceable: a clean single fuse `tick`
  (0.05–0.1 s transient), a real 3-note `zone_jingle`, and a big metallic `shield_clang`.

Also worth knowing: `CATALOGUE.csv` lists 347 clips but only **89 exist on disk** — the rest point
into those missing ZIPs.

---

## 8. Ready for the GitHub push, waiting on your approval

Nothing has been pushed. Nothing has been created on GitHub.

The repository is **597 KB of pure text with zero binary assets** — because every sprite and every
sound is generated in code, there is not one image or audio file in it. That is an unusually clean
public repo and it sidesteps the licence question entirely.

Prepared and committed locally: `README.md`, `LICENSE` (MIT), `CREDITS.md`, `.gitignore` (blocks
builds, logs, review captures), `DECISIONS_FOR_ROY.md`, `Docs/AUDIO_NEEDS.md`.

**Two things are your call before anything goes public:**

1. **The licence.** `LICENSE` is MIT. Since every asset in this game is produced by the code,
   licensing the code licenses the art too. Say the word if you would rather keep the art reserved
   and I will split it.
2. **Repo name and visibility.** Game Off needs public. I suggest `KT_ShiftIt` or `shift-it`.
