# itch.io page copy — SHIFT IT!

**Page: https://kadence-tech-games.itch.io/shift-it** (draft)

Draft copy for the store page. Screenshots, cover art and the GIF are produced by the capture
harness and live outside the repository at `D:\KT\ShiftIt\Review\sheets`.

---

## Title

**SHIFT IT!**

## Tagline

Pass the bomb. Don't be holding it.

## Short description (itch's "short description" field, ~140 characters)

> A one-key arcade game about getting rid of a lit bomb. Left Shift throws left, right Shift throws
> right, and that is the whole control scheme.

## Classification

- **Kind:** HTML (playable in browser), with a Windows download
- **Genre:** Action / Arcade
- **Input:** Keyboard (Shift keys only), touch
- **Made for:** GitHub Game Off 2026 — theme **SHIFT**

---

## Page body

You are a small yellow robot in a bomb-testing bay, and somebody has just handed you a lit bomb.

Shift it onto somebody else before the fuse runs out. Then do it again, faster. The bomb comes back,
the fuse gets shorter, and the other robots start putting their shields up, falling asleep and going
on break. Every fifteen passes the whole scene shifts somewhere new.

A fuse ring sits around the bomb with a green band on it. Throw while the ring's tip is inside the
green and you get a **PERFECT** and a combo worth up to six points; throw early and it's worth one.
So the game is a dare: hold it longer than feels sensible.

### The theme, three ways

- **SHIFT** — the only keys you ever press.
- **SHIFT the blame** — the whole game is making a bomb somebody else's problem.
- **The scene SHIFTS** — factory floor, rooftop, scrapyard, launchpad.

### What goes wrong, and when

| From | What |
|---|---|
| 5 passes | A robot raises an energy **SHIELD**. Throw at it and the bomb bounces back. |
| 10 | One is **ASLEEP** — eyes shut, floating Zzz, and it stops bobbing. |
| 10 | One is **ON BREAK** — the spot is just empty, with a dashed outline and a sign. |
| 12 | **RED HOT** bombs: half the fuse, double points. |
| 18 | **MIRRORED** turns: left Shift throws right. The key labels swap and turn pink. |
| 25 | Green **DUD** bombs. Do *not* throw them. Hold on and let it fizzle for +2. |
| 30 | **BOTH SIDES BLOCKED** — one opens part way through the fuse. You have to wait. |
| 40 | The blocked side can **SWAP** while you are still holding. |

A typical run lasts about a minute.

---

## Controls

| | |
|---|---|
| **LEFT SHIFT** | throw left |
| **RIGHT SHIFT** | throw right |
| **SHIFT** | start, and retry |
| **ESC** | quit (desktop build) |
| **TAB** | settings (music, sound, fullscreen) |
| **Touch** | tap the left or right half of the screen |

No mouse, no arrows, no WASD, no Space **in the game**. The settings screen takes the mouse — it is
the only sensible shape for a volume slider in a two-key game.

---

## Notes

**If Windows beeps at you:** tapping Shift five times in a row triggers Windows' own **Sticky Keys**
shortcut, which plays a beep. That is Windows, not the game. Turn the shortcut off in
**Settings → Accessibility → Keyboard → Sticky keys**, and it will stop.

The **Windows download handles this for you** — it switches that shortcut off while the game is
running and puts your exact original settings back when you quit. The browser version cannot change
Windows settings, so it shows the same tip on the start screen instead, once, with a GOT IT button.

The game also never *holds* Shift down: every throw is a single tap, edge-detected, so a Shift key
latched by Sticky Keys is one throw rather than a stream of them. Nothing in the game can hold Shift
for the eight seconds that would trigger Filter Keys either — the fuse runs out long before.

---

## Credits and licence

All art is generated in code — there is not one imported image in the project, and the chunky
display typeface is drawn the same way, a 5x7 grid of rounded cells per character. The sound effects
are synthesised in code too. The music and the explosion are CC0 from
[OpenGameArt](https://opengameart.org):

- *in the robot pub loop* — Sauer2
- *Summer Park – 8bit tune* — Scribe (Daniel Stephens)
- *Fast fight / battle music (looped)* — XCVG, from the original by Ville Nousiainen
- *SpacyLoop* — ElectronicFreaker
- *100 CC0 SFX* — rubberduck

CC0 asks for no attribution; these are credited because three of the authors said a mention would be
welcome.

**Code: MIT. All art and audio: CC0 1.0.** Full details in `ASSETS_LICENCE.md` in the repository.

Built with Unity 6000.6.4f1 by **KT Game Development**.

---

## Still to do before submission

- [x] Cover image (630×500)
- [x] 5 screenshots
- [x] Animated GIFs for the page (early game, late game with blockers)
- [ ] Final WebGL upload with "click to play" disabled so Shift reaches the game immediately
- [ ] Set the itch embed to 1280×720 and tick "fullscreen button"

### Upload settings that matter for this game

The game is driven entirely by the Shift keys, so **the iframe must have keyboard focus before the
player presses anything**. On the itch page set the embed to click-to-launch rather than
auto-start, so the player's first click gives the frame focus; the start screen then responds to
Shift immediately. Without that first click the browser sends the key events to the page, not the
game, and it looks broken.
