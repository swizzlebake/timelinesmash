# Changelog

All notable changes to this package are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.7.0] - 2026-06-24

### Added
- **One-click scaffolding.** A new `CinematicScaffold` service plus entry points remove the manual
  asset-creation dance:
  - **Assets ▸ TimelineSmash ▸ New Cinematic** creates a `BindingManifest` + `CinematicComposition` wired
    together and selects the composition.
  - **Add contributor** (composition inspector) creates a `ContributorSegmentSet` and adds it to the
    composition in one click.
  - **New sub-timeline + segment** (contributor inspector) creates a fresh `.playable` with a named
    AnimationTrack, appends a segment for it (placed after the latest), and opens it in the Timeline window.
- Tests: `CinematicScaffoldTests` cover create-and-wire, add-contributor, and new-sub-timeline placement.

## [0.6.0] - 2026-06-24

### Added
- **Bindings checklist on the Cinematic Composition inspector.** A new `BindingPlan` introspects every track
  across all contributors and shows, per track, whether it resolves (✓, to which actor and key) or is
  unresolved (✗, with the exact key to author) — turning previously-silent binding failures into a visible
  to-do list. Buttons: **Create & assign manifest** (when none is set) and **Add N missing key(s)** (seeds the
  manifest with the suggested keys so the artist just drags actors onto the targets). The plan resolves
  against the same candidate keys the runtime applier uses, so it never disagrees with assemble.
- **Assemble into active scene** is now a button on the composition inspector (previously only a right-click
  menu item), alongside a tip surfacing per-track override keys and bind-by-name.

### Changed
- `BindingApplier` key resolution is factored into a shared `CandidateKeys` helper (and `BindingTypeOf` is
  now internal) so the inspector and the runtime stay in lockstep. Behaviour is unchanged.

## [0.5.0] - 2026-06-24

### Added
- **Per-track binding overrides.** A segment's `bindingKey` override now resolves **`"<bindingKey>/<trackName>"`
  first**, then falls back to the bare `"<bindingKey>"`. A multi-track sub-timeline reused by two segments
  can finally retarget its tracks individually (`alice/Body`, `alice/Voice`, `bob/Body`, …); a bare key still
  binds the whole sub-timeline, so existing manifests are unaffected.
- **Nested ControlTrack wiring.** A `ControlTrack` placed *inside* a sub-timeline is no longer dead: for each
  control clip `BindingApplier` resolves its manifest target to a GameObject and sets the clip's exposed
  source reference on the host director (previously only the master's control tracks were wired).
- **Assemble into the active scene.** `CinematicAssembleService.AssembleIntoActiveScene` (menu: **Assets ▸
  TimelineSmash ▸ Assemble Into Active Scene**) wires the master + host directors into the currently open
  scene instead of regenerating an empty stage, so bindings resolve against the actors already in the scene.
  It is idempotent (re-running replaces the master it added) and never destroys the user's actors.
- **Bind by scene-object name.** When assembling into a live scene, a binding key the manifest does not
  resolve falls back to a scene GameObject of that name, selecting the component the track binds to — so a
  committed manifest can target live actors without serializing scene references. Opt-in via
  `StageSceneBuilder.Populate(..., resolveBySceneName: true)`.
- Tests: per-track vs whole-sub override, nested-control wiring, idempotent active-scene assembly,
  bind-by-name resolution, plus an elaborate multi-track playback-evaluation suite (Animation translate +
  rotate proven by host evaluation, Audio/Activation/Signal/Control track types).

### Changed
- `BindingApplier.Apply` and `StageSceneBuilder.Populate` take an optional `resolveBySceneName` flag
  (default `false`, preserving prior behaviour).

## [0.4.0] - 2026-06-24

### Changed
- **High-resolution recording.** The optional Recorder export now writes a **PNG/EXR image sequence** from
  a chosen camera via `CameraInputSettings` at an **arbitrary resolution** (`CaptureSettings.width/height`,
  e.g. 4K/8K), instead of an H.264 movie — removing the built-in encoder's ~4K ceiling. PNG captures
  tonemapped sRGB (ready to encode); EXR captures linear HDR (for grading). Driven over a fixed frame
  range (`SetRecordModeToFrameInterval` + `CapFrameRate`). ProRes is macOS/Windows only, so the cross-
  platform high-res path is image sequences.

### Added
- `CaptureSettings` (width, height, format, supersample, camera tag) on `CinematicComposition.capture`.

### Notes
- Recorder capture is interactive (enter Play Mode to capture; it auto-stops at the last frame) and
  requires the capture camera to be present in the open scene(s). A custom, dependency-free capture
  pipeline (play-mode frame-stepping → image sequence → ffmpeg video) is planned to make this fully
  turnkey.

## [0.3.0] - 2026-06-24

### Added
- **Composable binding manifests.** A `BindingManifest` can now `include` any number of child lookup
  manifests, so teams split bindings across files. At assemble time `BindingCompiler.Compile` flattens
  the whole tree into one master lookup — **first definition of a key wins** (a manifest's own entries
  before its includes, includes in order); duplicates are ignored and warned (naming both sources).
  Diamond includes compile once; cycles are detected and skipped.
- The compiled master lookup drives binding application, and a flat, regenerable `<Name>_Bindings.asset`
  is written next to the master timeline + stage scene (gitignored output folder) for inspection. Note:
  a saved asset cannot serialize *scene-object* references, so on-disk targets are a best-effort snapshot;
  live binding application uses the in-memory compiled lookup.
- `BindingManifest` inspector: a **Compile preview** button reports the compiled key count and any
  duplicate/cycle warnings.
- Tests: include flattening, first-wins precedence (local + include order), diamond, cycle guard, and
  compiled-asset round-trip.

### Changed
- `BindingApplier.Apply` and `StageSceneBuilder.BuildStage`/`Populate` take a compiled lookup
  (`CompiledBindings`) instead of a raw `BindingManifest`. `BindingManifest.Resolve` remains a
  single-level (own-entries-only) lookup.

## [0.2.0] - 2026-06-24

### Added
- **Nested compositions (arbitrary depth).** A segment can now reference a sub-composition
  (`SubTimelineSegment.subComposition`) as well as a leaf sub-timeline. The assembler **flattens** the
  whole tree into one single-level native master at assemble time (`CinematicAssembler.FlattenTree`),
  so the runtime never nests Control Tracks and there is no playback desync — nesting depth is
  effectively unlimited.
- **Grouping.** `GroupingService.GroupSegments` extracts a set of segments into a reusable
  sub-composition asset and replaces them with a single reference, rebasing children so the flattened
  result is unchanged (grouping is transparent). Exposed via a "Group all segments into a
  sub-composition" button on the contributor inspector.
- Flatten accumulates start offset + speed scale down each path; nested lanes merge (empty group lane)
  or namespace (named group lane, e.g. `Group/Camera`). Cycles are detected and skipped with a warning.
- Tests: nesting depth, timing accumulation, grouping transparency, lane namespacing, cycle guard, and
  grouping round-trip.

### Changed
- `CinematicAssembler.Flatten`/`SegmentRef` replaced by `FlattenTree`/`LeafRef` (recursive, absolute
  timing). The overview model and binding/stage pipeline consume the flattened leaves unchanged.

## [0.1.0] - 2026-06-23

### Added
- Runtime data model: `SubTimelineSegment`, `ContributorSegmentSet`, `BindingManifest`,
  `AssembleSettings`, `CinematicComposition` (all with `Create ▸ TimelineSmash` menu entries).
- `CinematicAssembler` — deterministic master `TimelineAsset` generation (one Control Track per
  lane, one clip per segment, with start / duration / clip-in / speed).
- `StageSceneBuilder` — regenerable stage scene with a master director plus one host director per
  segment, kept out of the hand-edited working scene.
- `BindingApplier` — resolves each sub-timeline track's binding from the manifest onto its host
  director at assemble time.
- `CinematicOverviewModel` — lane × time overview with overlap and gap diagnostics.
- Custom inspectors and menu items: **Assemble**, **Open Master**, **Open Stage**, **Record**.
- Optional `com.unity.recorder` integration (define-constrained; export is enabled only when the
  Recorder package is installed).
- EditMode test suite covering the data model, flatten/sort determinism, master generation,
  the overview model, stage building, and binding resolution.
