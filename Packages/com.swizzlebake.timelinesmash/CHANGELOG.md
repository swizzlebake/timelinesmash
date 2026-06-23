# Changelog

All notable changes to this package are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
