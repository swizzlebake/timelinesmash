# TimelineSmash

> Collaborative cinematic assembly for Unity Timeline — build long, intricate cinematics as a team
> **without merge conflicts**.

Each artist authors **isolated sub-timelines** (`.playable`) in their own files and declares where their
shots go in their own **contributor file**. A deterministic **assembler** bakes every contributor into a
native **master Timeline** plus a regenerable **stage scene**, with bindings to shared scene actors
resolved from a **manifest** at assemble time. Because the master is generated (never hand-edited) and
every artist owns separate files, two people never touch the same asset — a "conflict" is resolved by
re-assembling, not by hand-merging.

This repository is a Unity 6 project that **develops and hosts** the package, embedded at
[`Packages/com.swizzlebake.timelinesmash`](Packages/com.swizzlebake.timelinesmash). The package is the
deliverable; the project around it is the development + test harness.

## Why

Unity Timeline already nests timelines (Control Track), but team cinematics still hit three walls:

1. A single `.playable` is one file — two artists editing it conflict.
2. Track **bindings live on the scene's `PlayableDirector`**, so the scene file becomes the merge battleground.
3. There's no first-class "compose many shots, owned by many people, by lane and by time" model.

TimelineSmash addresses all three: split ownership into per-artist files, move bindings into a manifest,
and generate the master deterministically.

## Install

Package Manager → **Add package from git URL…**:

```
https://github.com/swizzlebake/timelinesmash.git?path=Packages/com.swizzlebake.timelinesmash
```

Or add to `Packages/manifest.json`:

```json
"com.swizzlebake.timelinesmash": "https://github.com/swizzlebake/timelinesmash.git?path=Packages/com.swizzlebake.timelinesmash"
```

Pin a version by appending a git tag, e.g. `#0.12.0`. Requires Unity **6000.3+** and
`com.unity.timeline` **1.8.12+**; `com.unity.recorder` is optional and enables video / image-sequence
export.

## Concepts

| Asset | Owner | Role |
| --- | --- | --- |
| **Sub-timeline** (`.playable`) | one artist | a shot / element, authored in the normal Timeline window |
| **Contributor Segment Set** | one artist | where that artist's segments go: lane, start, duration, clip-in, speed, optional spawn-prefab |
| **Binding Manifest** | shared (rare edits) | logical name → shared scene actor (camera, character, light) |
| **Cinematic Composition** | shared (rare edits) | references the contributor sets + manifest + assemble settings |
| `<Name>_Master.playable` | **generated** | native master Timeline, one Control Track per lane — gitignored |
| `<Name>_Stage.unity` | **generated** | master director + one host director per segment, bindings applied — gitignored |

Create assets via **Assets ▸ Create ▸ TimelineSmash ▸ …**, or scaffold a whole cinematic in one step with
**Assets ▸ TimelineSmash ▸ New Cinematic** (creates a wired composition + manifest). From there **Add
contributor** (composition inspector) and **New sub-timeline + segment** (contributor inspector) create and
wire the remaining assets for you.

## Workflow

1. Add your shared actors (cameras, characters, lights) to a scene.
2. Create a **Binding Manifest** mapping logical names to those actors.
3. Each artist creates sub-timelines and a **Contributor Segment Set** placing them by lane + time.
   A sub-timeline track named `Hero` binds to the manifest key `Hero` (or set an explicit key on the segment).
4. Create a **Cinematic Composition** referencing the contributor sets + the manifest. Its inspector shows a
   **Bindings checklist** — every track that needs an actor and whether it's bound — with **Add missing keys**
   to seed the manifest for you (then drag actors onto the targets).
5. Select the composition and press **Assemble (master + stage)** — or **Assemble into active scene** to wire
   the cinematic into the scene you already have open.
6. **Open Master** to preview/scrub in the native Timeline window, or open the stage scene and press Play.
7. *(with Recorder installed)* press **Record** to export.

Artists only ever edit **their own** sub-timelines and contributor file; the master + stage are regenerable
artifacts (gitignored). Two artists working in parallel merge cleanly because they changed different files —
re-assembling produces the combined cinematic.

## Features

- **Merge-safe by construction** — one-owner-per-file source assets; the playable master is generated
  deterministically and regenerated, never hand-merged.
- **Nesting to any depth** — a segment can reference a **sub-composition** instead of a leaf sub-timeline.
  The assembler **flattens** the tree into one single-level master at assemble time, so the runtime never
  nests Control Tracks and there's no playback desync regardless of depth. A nested group's lanes **merge**
  into the parent when its lane is empty, or **namespace** (e.g. `Group/Camera`) when named.
- **Grouping into reusable units** — extract a contributor's segments into a standalone, portable group asset
  (children rebased so the result is identical) and reference it from other cinematics.
- **Composable bindings** — a `BindingManifest` can **include** child manifests, so teams split bindings
  across files. At assemble time the whole tree compiles into **one master lookup**, first-definition-of-a-key
  wins; duplicates are ignored and reported. **Compile preview** shows the key count and any conflicts.
- **Per-track retargeting** — reuse one multi-track sub-timeline for different actors with per-segment binding
  keys like `hero/Body`, `hero/Voice`. A bare key still binds the whole sub-timeline.
- **Bind by name / live-scene assembly** — **Assemble into active scene** wires the master + host directors
  into your open scene (idempotent; your actors are never destroyed). Any key the manifest doesn't resolve
  falls back to a scene GameObject of that **name**, so naming an actor after a track is enough.
- **Visual timeline** — `Window ▸ TimelineSmash ▸ Cinematic Timeline` opens an interactive lane×time view of
  the selected composition. Drag a bar to move its start or drag its right edge to resize — both snap to
  frames and are undoable. Zoom / Fit / Snap and one-click Assemble are in the toolbar; double-click a bar to
  ping its contributor.
- **High-resolution recording** *(needs `com.unity.recorder`)* — **Record** writes a 4K/6K/8K **PNG/EXR image
  sequence** at any resolution (not the Game-View size), and on macOS/Windows encodes a **ProRes 422 HQ**
  `.mov` (with audio) that matches the master resolution — sidestepping Unity Recorder's ~4K H.264 ceiling.
  PNG = tonemapped sRGB, EXR = linear HDR for grading. Record length auto-fills from the assembled master's
  duration.

## How it assembles

```
Sub-timelines (.playable)         ── one file per artist ──┐
Contributor Segment Sets (.asset) ── one file per artist ──┤── deterministic ──▶  <Name>_Master.playable  (generated)
Binding Manifest (.asset)         ── shared, rare edits ───┤    assembler          <Name>_Stage.unity       (generated)
Cinematic Composition (.asset)    ── shared, rare edits ───┘
```

1. **Flatten** all contributor segments into one list, sorted by a stable key — determinism is what makes the
   master a regenerable artifact instead of a merge surface.
2. **Build master** — recreate the `.playable` from scratch: one `ControlTrack` per lane, one
   `ControlPlayableAsset` clip per segment, timing taken straight from the segment record.
3. **Build stage** — a master `PlayableDirector` bound to the master timeline, plus one **host** director per
   segment (its `playableAsset` is the sub-timeline). Host directors live only in the generated stage scene,
   so your working scene never carries merge-prone director/binding wiring.
4. **Apply bindings** — for each output track, resolve `segment.bindingKey ?? track.name` through the manifest
   and bind it on the host director.

## Develop / test

Open this project in Unity **6000.3.10f1**. Run the EditMode tests via **Window ▸ General ▸ Test Runner ▸
EditMode**, or headless against a project that embeds the package:

```
<UnityEditor> -batchmode -nographics -projectPath <project> -runTests -testPlatform EditMode \
  -testResults ./TestResults.xml -accept-apiupdate
```

See [CLAUDE.md](CLAUDE.md) for development conventions and gotchas, and the
[package docs](Packages/com.swizzlebake.timelinesmash/Documentation~/index.md) for the architecture deep-dive.

## License

MIT — see [LICENSE](LICENSE).
