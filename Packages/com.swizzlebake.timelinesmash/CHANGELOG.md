# Changelog

All notable changes to this package are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
