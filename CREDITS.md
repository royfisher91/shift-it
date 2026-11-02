# Credits

**SHIFT IT!** — GitHub Game Off 2026 entry by **KT Game Development**.

## Everything in this repository is original

- **Code** — written for this entry.
- **Art** — there is not one imported image in this repository. Every sprite is generated in code
  at runtime from rounded rectangles, circles, rings, triangles and radial glows
  (`Assets/Scripts/Game/Shapes.cs`). The four zone backdrops are assembled from those same shapes.
- **Sound effects** — synthesised in code (`Assets/Scripts/Game/AudioService.cs`): the fuse tick,
  throw whoosh, catch thunk, PERFECT ding, shield clang, dud fizzle, fail buzzer and the three-note
  zone jingle are all generated from oscillators and filtered noise, not sampled.
- **Font** — Stage 1 uses the font that ships with Unity.

## Third party — all CC0, full details in ASSETS_LICENCE.md

Four zone music loops and one explosion, every one released under CC0 1.0. CC0 asks for no
attribution; these are listed because three of the authors said a credit would be welcome.

- *in the robot pub loop* — **Sauer2** — zone 1, the factory floor
- *Summer Park – 8bit tune* — **Scribe (Daniel Stephens)** — zone 2, the rooftop
- *Fast fight / battle music (looped)* — **XCVG**, from the original by **Ville Nousiainen** — zone 3, the scrapyard
- *SpacyLoop* — **ElectronicFreaker** — zone 4, the launchpad
- *100 CC0 SFX* (the explosion) — **rubberduck**

All five from [OpenGameArt](https://opengameart.org).

- **Unity 6000.6.4f1** and the Unity Input System package.

## Not used

No asset-store content, no CC-BY assets and no downloaded fonts. Downloaded audio is CC0 only.

The existing KT sound library is deliberately **not** used: it is licensed for use in a shipped game
but **not** for redistribution, and this entry has to live in a public repository. See
`ASSETS_LICENCE.md` and `Docs/AUDIO_NEEDS.md`.
