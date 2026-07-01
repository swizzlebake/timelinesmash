# Changelog

All notable changes to this package are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- **Assemble now reports same-lane overlaps.** When two segments on the same lane overlap in time —
  the cross-contributor "logical conflict" TimelineSmash exists to catch — `BuildMaster` adds a warning
  per overlapping pair to the assemble result, so the conflict surfaces in the post-Assemble summary
  instead of only in the inspector overview. Segments that merely touch (one ends where the next begins)
  are not flagged.

### Changed
- **A segment with no explicit duration plays its whole sub-timeline.** When a segment's `duration` is
  left at 0 (or negative), the assembler now fills it from the sub-timeline's own length instead of
  collapsing the clip to a near-zero sliver. An explicit duration still wins; an empty sub-timeline
  (length 0) is unchanged.

### Changed
- **Lowered the minimum Unity to 2021.3 LTS** (from 6000.3). The package only ever needed a handful of
  modern APIs; the rest is long-standing Timeline/IMGUI/AssetDatabase surface available since 2018–2019.
  The required Timeline dependency floor drops accordingly to **`com.unity.timeline` 1.6.1** — the oldest
  Timeline that has both `TimelineAsset.editorSettings.frameRate` (1.6.1) and
  `TimelineEditor.GetOrCreateWindow()` / `TimelineEditorWindow.SetTimeline()` (1.5.2), the two newest
  APIs the assembler and inspectors use. Package resolution still picks the editor's bundled Timeline
  (1.6.x on 2021.3, 1.8.x on Unity 6), so nothing changes on newer editors.

### Fixed
- **Optional Recorder export no longer breaks compilation on older Recorder.** The export assembly's
  `versionDefines` expression was empty, so `TIMELINESMASH_RECORDER` switched on for *any* installed
  Recorder — but the export code uses the scripted ProRes `Encoder` API (`ProResEncoderSettings`) that
  only exists in **Recorder 4.0.0+**. The expression is now `[4.0.0,)`, so on Recorder 3.x the export
  path is cleanly absent (as an optional feature should be) instead of a hard compile error.

## [0.12.0] - 2026-06-26

### Added
- **Record encodes a video on macOS/Windows.** A new `CaptureSettings.output` option
  (`ImageSequence` / `Video` / `ImageSequenceAndVideo`, **default `Video`**) makes Record emit a
  **ProRes 422 HQ** `.mov` (with stereo audio) as part of the record pass. ProRes — not H.264, which Unity
  Recorder caps at 4K — so the video matches the 4K/6K/8K master resolution. Linux has no platform ProRes
  encoder, so a video request there falls back to the high-res PNG/EXR image sequence (encode it yourself
  with ffmpeg); choose `ImageSequenceAndVideo` to get both on macOS/Windows.

### Changed
- **Record length auto-fills.** When a composition's `Total Duration` is left at 0, Record now derives the
  frame range from the assembled master timeline's own duration (the latest segment's end) instead of
  silently dropping into manual mode — so a single Record captures the whole cinematic without typing the
  length. An empty/absent master still falls back to manual mode.

## [0.11.0] - 2026-06-26

### Added
- **Self-contained, recordable stage scenes.** A composition can now name a **Stage source scene** and/or a
  **Stage actor prefab** (new fields on the composition inspector's Assemble section). On Assemble, the
  generated `<Name>_Stage.unity` is built from them — the source scene is cloned as the stage's base
  (shipping its actors, lighting and camera) and/or the prefab is instantiated at the stage root. Bindings
  resolve against those actors **by name** (a manifest entry still wins). Previously the generated stage held
  only the master + host directors, so Record captured an empty scene unless every segment spawned a prefab;
  now the stage carries the real, animatable actors and plays/records on its own. With neither field set the
  behaviour is unchanged (director-only stage). The source scene is cloned to a copy, never edited in place,
  so the stage stays fully regenerable.

## [0.10.1] - 2026-06-25

### Fixed
- **Recording play-mode flow.** Record threw "you can only call the PrepareRecording method in PlayMode"
  (it prepared/started the `RecorderController` in Edit Mode) and, if clicked during Play, "this cannot be
  used during play mode" (it opened the stage scene while playing). Record now **arms** in Edit Mode — opens
  the stage scene and remembers the composition — then enters Play Mode; the controller is prepared and
  started in the `EnteredPlayMode` callback, auto-stops at the last frame and leaves Play Mode (a manual
  capture stops when you exit Play Mode). Clicking Record while already in Play Mode now warns instead of
  throwing.

## [0.10.0] - 2026-06-25

### Added
- **Visual timeline window** (`Window ▸ TimelineSmash ▸ Cinematic Timeline`, or "Open visual timeline" on
  the composition inspector). An interactive lane×time view of a composition's direct segments: drag a bar
  to move its start, drag its right edge to resize the duration — both snap to frames and are undoable.
  Toolbar has zoom / fit / snap toggle and a one-click Assemble; double-click a bar pings its contributor
  set. Lane reassignment and other fields remain on the contributor inspector (which has the lane dropdown).
  The edit model (`CinematicTimelineModel`, pairing each bar with its owning set + segment) and frame
  snapping (`TimelineSnap`) are unit-tested; the window renders them.

## [0.9.0] - 2026-06-24

### Added
- **Per-segment prefab spawning.** `SubTimelineSegment.spawnPrefab` — when set, the assembler adds a
  parallel control clip (on a per-lane `Spawn:<lane>` track, so it never collides with the host-driving
  clip) that instantiates the prefab when the segment starts and destroys it when it ends. Use a
  self-animating prefab (its own PlayableDirector / Animator / particles). The segment drawer exposes it.

### Fixed
- **Overview readability.** Overview bar labels now use a black/white text color chosen for contrast against
  each bar (the theme's grey mini-label washed out on the lighter owner colors); the segment drawer's
  warning/hint text is no longer drawn in a hard-to-read pale tint.
- **Open Master** reliably shows the master in the Timeline window — `SetTimeline` is deferred past the
  freshly-opened window's first layout, instead of needing you to select another timeline first.

## [0.8.0] - 2026-06-24

### Added
- **Segment editing aids.** A `CustomPropertyDrawer` for `SubTimelineSegment` gives each segment a
  collapsible one-line summary (name · lane · time range), a warning when the **sub-timeline / sub-composition
  either-or** is violated (both set, or neither), a **lane dropdown** seeded from lanes already in use, and a
  live **"Binds keys: …"** preview of the manifest keys the segment needs (reusing the same `CandidateKeys`
  as the runtime and the Bindings checklist).
- **Same-lane overlap warning.** The contributor inspector flags segments stacked on one lane via a new
  pure, tested `SegmentDiagnostics` helper.
- Tests: `SegmentDiagnosticsTests` cover source conflict/missing detection and lane-overlap cases.

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
