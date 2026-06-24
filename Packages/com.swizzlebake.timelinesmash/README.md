# TimelineSmash

Collaborative cinematic assembly for Unity Timeline — build long, intricate cinematics as a
team **without merge conflicts**.

Each artist authors **isolated sub-timelines** (`.playable`) in their own files and declares
where their shots go in their own **contributor file**. A deterministic **assembler** bakes all
contributors into a native **master Timeline** plus a regenerable **stage scene**, with bindings
to shared scene actors resolved from a **manifest** at assemble time. Because the master is
generated (never hand-edited) and every artist owns separate files, two people never touch the
same asset — a "conflict" is resolved by re-assembling, not by hand-merging.

## Why

Unity Timeline already nests timelines (Control Track), but team cinematics still hit three walls:

1. A single `.playable` is one file — two artists editing it conflict.
2. Track **bindings live on the scene's `PlayableDirector`**, so the scene file is the merge battleground.
3. There's no first-class "compose many shots, owned by many people, by lane and by time" model.

TimelineSmash addresses all three: split ownership into per-artist files, move bindings into a
manifest, and generate the master deterministically.

## Requirements

- Unity **6000.3** or newer
- `com.unity.timeline` **1.8.12+** (declared as a dependency)
- *(optional)* `com.unity.recorder` — enables the **Record** button for video / image-sequence export

## Install (git URL)

Package Manager → **Add package from git URL…**:

```
https://github.com/swizzlebake/timelinesmash.git?path=Packages/com.swizzlebake.timelinesmash
```

Or add to `Packages/manifest.json`:

```json
"com.swizzlebake.timelinesmash": "https://github.com/swizzlebake/timelinesmash.git?path=Packages/com.swizzlebake.timelinesmash"
```

To pin a version, append `#0.1.0` (a git tag) to the URL.

## Concepts

| Asset | Owner | Role |
| --- | --- | --- |
| **Sub-timeline** (`.playable`) | one artist | a shot / element, authored in the normal Timeline window |
| **Contributor Segment Set** | one artist | where that artist's segments go: lane, start, duration, clip-in, speed |
| **Binding Manifest** | shared (rare edits) | logical name → shared scene actor (camera, character, light) |
| **Cinematic Composition** | shared (rare edits) | references the contributor sets + manifest + assemble settings |
| `<Name>_Master.playable` | **generated** | native master Timeline (one Control Track per lane) — gitignored |
| `<Name>_Stage.unity` | **generated** | master director + one host director per segment, bindings applied — gitignored |

Create assets via **Assets ▸ Create ▸ TimelineSmash ▸ …**, or scaffold a whole cinematic in one step with
**Assets ▸ TimelineSmash ▸ New Cinematic** (creates a wired composition + manifest). From there, **Add
contributor** (composition inspector) and **New sub-timeline + segment** (contributor inspector) create and
wire the remaining assets for you.

## Workflow

1. Add your shared actors (cameras, characters, lights) to a scene.
2. Create a **Binding Manifest** mapping logical names to those actors.
3. Each artist creates sub-timelines and a **Contributor Segment Set** placing them by lane + time.
   - A sub-timeline's track named `Hero` binds to the manifest key `Hero` (or set an explicit key on the segment).
4. Create a **Cinematic Composition** referencing the contributor sets + the manifest. Its inspector shows
   a **Bindings checklist** — every track that needs an actor and whether it's bound — with an **Add missing
   keys** button that seeds the manifest for you (then just drag actors onto the targets).
5. Select the composition and press **Assemble (master + stage)** — or **Assemble into active scene** to wire
   the cinematic into the scene you already have open (see below).
6. **Open Master** to preview/scrub in the native Timeline window, or open the stage scene and press Play.
7. *(with Recorder installed)* press **Record** to export.

### Merge-conflict-free, by construction

Artists only ever edit **their own** sub-timelines and contributor file. The master + stage are
regenerable artifacts (gitignored). Two artists working in parallel merge cleanly because they
changed different files; re-assembling produces the combined cinematic.

### Nesting & grouping (0.2.0+)

A segment can reference a **sub-composition** instead of a leaf sub-timeline, so cinematics nest to
any depth. The assembler **flattens** the tree into one single-level master at assemble time, so the
runtime never nests Control Tracks — there's no playback desync regardless of depth. A nested group's
lanes **merge** into the parent when its lane is empty, or **namespace** (e.g. `Group/Camera`) when
named.

**Group → reusable unit:** the "Group all segments into a sub-composition" button (contributor
inspector) extracts segments into a standalone group asset and references it in one place. Children
are rebased so the result is identical — the group is just portable now, and can be referenced from
other cinematics.

### Composable bindings (0.3.0+)

Bindings are merge-safe too. A `BindingManifest` can **include** any number of child lookup manifests,
so teams split bindings across files. At assemble time the whole tree compiles into **one master
lookup** — the **first definition of a key wins** (a manifest's own entries before its includes,
includes in order); duplicates are ignored and reported. The compiled lookup is also written to a flat
`<Name>_Bindings.asset` (gitignored) for inspection. Use the **Compile preview** button on a manifest
to check the key count and any conflicts.

### High-resolution recording (0.4.0+)

With `com.unity.recorder` installed, **Record** writes a **PNG/EXR image sequence** from a chosen camera
at any resolution (`CaptureSettings.width/height` on the composition — 4K, 8K, …), not the Game-View
size. This sidesteps the built-in H.264 encoder's ~4K ceiling (the limit was the *encoder*, not the
capture). PNG = tonemapped sRGB (ready to encode); EXR = linear HDR (for grading). Enter Play Mode to
capture; it auto-stops at the last frame. Ensure the capture camera (default tag `MainCamera`) is present
in the open scene. *(ProRes is macOS/Windows only — image sequences are the cross-platform high-res path.)*

### Easier binding & live-scene assembly (0.5.0+)

Binding is the step most likely to trip an artist, so several conveniences make it visible and forgiving:

- **Bindings checklist** *(0.6.0)* — the Cinematic Composition inspector lists every track across all
  contributors that needs an actor, shows whether it resolves (and to what) or is unresolved with the exact
  key to author, and offers **Create & assign manifest** / **Add N missing key(s)**. No more hunting through
  `.playable`s by hand or debugging silent failures after assemble.
- **Per-track retargeting** *(0.5.0)* — reuse one multi-track sub-timeline for different actors by setting a
  per-segment **Binding Key** and authoring keys like `hero/Body`, `hero/Voice`. A bare key still binds the
  whole sub-timeline (backwards compatible).
- **Assemble into active scene + bind by name** *(0.5.0)* — **Assemble into active scene** wires the master +
  host directors into the scene you already have open (idempotent; your actors are never destroyed). Any key
  the manifest doesn't resolve falls back to a scene GameObject of that **name** — so naming an actor after a
  track is enough, no manifest entry required.

## Development

This repository is itself a Unity project that embeds the package under
`Packages/com.swizzlebake.timelinesmash`, so you can develop and run the tests in-place.

Run the EditMode tests via **Window ▸ General ▸ Test Runner ▸ EditMode**, or headless:

```
"<UnityEditor>" -batchmode -projectPath . -runTests -testPlatform EditMode \
  -testResults ./TestResults.xml -quit
```

## License

MIT — see [LICENSE.md](LICENSE.md).
