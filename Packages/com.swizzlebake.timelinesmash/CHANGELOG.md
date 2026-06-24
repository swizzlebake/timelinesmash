# Changelog

All notable changes to this package are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
