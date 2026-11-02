# Credits

**SHIFT IT!** — GitHub Game Off 2026 entry by **KT Game Development**.

## Everything in this repository is original

- **Code** — written for this entry.
- **Art** — there is not one imported image in this repository. Every sprite is generated in code
  at runtime from rounded rectangles, circles, rings, triangles and radial glows
  (`Assets/Scripts/Game/Shapes.cs`). The four zone backdrops are assembled from those same shapes.
- **Sound effects** — synthesised in code (`Assets/Scripts/Game/AudioService.cs`): the fuse tick,
  throw whoosh, catch thunk, PERFECT ding, shield clang, dud fizzle, fail buzzer, explosion and the
  three-note zone jingle are all generated from oscillators and filtered noise, not sampled.
- **Font** — Stage 1 uses the font that ships with Unity.

## Third party

- **Unity 6000.6.4f1** and the Unity Input System package.

## Not used

No asset-store content, no downloaded audio and no downloaded fonts. This is a deliberate choice
rather than an accident: the entry has to live in a public repository, and generated assets carry no
redistribution question at all. See `Docs/AUDIO_NEEDS.md` for the one place this matters — the
existing KT sound library is licensed for use in a shipped game but **not** for redistribution in a
public repository, so it is not used here.
