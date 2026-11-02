# SHIFT IT!

**Pass the bomb. Don't be holding it.**

A one-key arcade game for [GitHub Game Off 2026](https://itch.io/jam/game-off-2026).
Theme: **SHIFT**.

You are a small yellow robot in a bomb-testing bay, and somebody has just handed you a lit bomb.
Shift it onto somebody else before the fuse runs out. Then do it again, faster. The bomb comes
back, the fuse gets shorter, the other robots start putting their shields up, falling asleep and
going on break — and every fifteen passes the entire scene shifts somewhere new.

**The Shift keys run the whole game.** Starting, throwing, retrying. No mouse, no arrows, no WASD,
no Space. On a phone, tap the left or right half of the screen.

---

## The theme, three ways

- **SHIFT** — the only keys you ever press.
- **SHIFT the blame** — the entire game is getting rid of a bomb by making it someone else's problem.
- **The scene SHIFTS** — factory floor, rooftop, scrapyard, launchpad.

---

## How it plays

A fuse ring sits around the bomb, with a green band on it. Throw while the ring's tip is inside the
green and you get a **PERFECT** and a combo worth up to six points; throw early and it is worth one.
So the game is a dare: hold it longer than feels sensible.

Then it starts arguing with you.

| From | What goes wrong |
|---|---|
| 5 passes | A robot raises an energy **SHIELD**. Throw at it and the bomb bounces back. |
| 10 | One is **ASLEEP** (eyes shut, floating Zzz, and it stops bobbing). |
| 10 | One is **ON BREAK** — the spot is just empty, with a dashed outline and a sign. |
| 12 | **RED HOT** bombs: half the fuse, double points. |
| 18 | **MIRRORED** turns: Left Shift throws right. The key labels swap and turn pink. |
| 25 | Green **DUD** bombs. Do *not* throw them. Hold on and let it fizzle for +2. |
| 30 | **BOTH SIDES BLOCKED** — one opens part way through the fuse. You have to wait. |
| 40 | The blocked side can **SWAP** while you are still holding. |

Every one of those gets one short hint line the first time you meet it, and never again.

---

## Two control modes

Both are built, and the title screen switches between them with **Tab** or the mouse.

- **MODE B — Left / Right.** `LEFT SHIFT` throws left, `RIGHT SHIFT` throws right. Two robots.
- **MODE A — Hold & Release.** Hold Shift and an aim arrow sweeps across five robots in an arc;
  release to throw. Let go between two robots and the bomb hits the floor. The fuse burns half
  again as fast while you hold.

---

## Running it

Built with **Unity 6000.6.4f1**. Targets WebGL (the main one, for itch.io) and Windows x64.

```bash
# everything goes through one wrapper, which takes the shared machine lock
Tools/unity.sh editmode  -runTests -testPlatform EditMode     # simulation + balance sweep
KT_GFX=1 Tools/unity.sh playmode -runTests -testPlatform PlayMode   # plays the real game, shoots screenshots
Tools/unity.sh build -executeMethod KT.ShiftIt.EditorTools.BuildScript.BuildAll
```

---

## How it is put together

The thing worth knowing: **the game simulation is a separate assembly that cannot see Unity at
all.** `KT.ShiftIt.Sim` is built with `noEngineReferences`, so a reference to `UnityEngine` — let
alone to the UI — is a compile error rather than a code-review argument.

That one constraint pays for itself immediately: the whole game is a deterministic
`Tick(dt)` + `SetKey(side, down)` state machine, so the balance sweep plays **thousands of
simulated games in about a second** inside a plain unit test, with no editor, no scene and no
frames. Re-tuning a number and re-measuring the difficulty curve is a one-second loop.

```
Assets/Scripts/Sim/      KT.ShiftIt.Sim      pure C#, no UnityEngine. The whole game.
  ShiftSim.cs                                the state machine
  SimConfig.cs                               every balance number
  SimBot.cs                                  the simulated player, with skill profiles
Assets/Scripts/Game/     KT.ShiftIt.Game     presentation only. Reads the sim; never writes to it.
  GameRoot.cs                                the one bootstrap, explicit construction order
  ShiftInput.cs                              the only thing that touches a keyboard
  Shapes.cs                                  every sprite in the game, generated in code
Assets/Tests/            EditMode + PlayMode
Assets/Resources/shiftit_balance.json        the numbers, outside the code
```

**All art is generated at runtime from code** — rounded rectangles, rings, triangles, glows — so
there is not a single imported image in the repository and the art is original by construction.
The Stage 1 sound effects are synthesised in code for the same reason.

The simulation has one input: two booleans. So the automated tests and the balance bots drive the
game through exactly the path a player's key press takes, and *cannot* reach past it to apply a
throw directly.

---

## Accessibility, and the Windows Shift-key problem

A game played entirely with Shift runs straight into two Windows accessibility shortcuts, and both
are handled rather than ignored:

- **Sticky Keys** (five Shift presses) can latch the key down. Every throw is edge-detected, so a
  latched or held Shift is **one** throw, never a stream. There is a test for exactly this.
- **Filter Keys** (holding Right Shift for eight seconds) — nothing in this game can hold Shift
  that long. Mode A caps a hold at 5 s, and the fuse itself runs out well before that. Tested as a
  property, not as a limit.
- The Windows build switches off the two *activation shortcuts* while it runs and restores the
  player's exact original settings on quit — and deliberately does not touch the features
  themselves if the player has them turned on.
- The browser build cannot change Windows settings, so it says so on the title screen instead.

---

## Licence

Code and generated assets: see `LICENSE`.
