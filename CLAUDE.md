# CLAUDE.md

Guidance for working in this repository.

## What this repo is

A Unity **6000.3.10f1** (URP 17.3.0) project that develops the **TimelineSmash** package, embedded at
`Packages/com.swizzlebake.timelinesmash` — the deliverable. Consumers install it via git URL with
`?path=Packages/com.swizzlebake.timelinesmash`. TimelineSmash assembles collaborative cinematics from
per-artist sub-timelines without merge conflicts. Dependencies: `com.unity.timeline` 1.8.12 (required),
`com.unity.recorder` 5.1.2 (optional, enables export).

## Core idea — merge-safety

Source of truth is split into **one-owner-per-file** assets; the playable master is **generated
deterministically** and regenerated, never hand-merged:

- Committed: sub-timelines (`*.playable`), per-artist `ContributorSegmentSet`, `BindingManifest`,
  `CinematicComposition`.
- Generated + gitignored (`Assets/Cinematics/Generated/`): `<Name>_Master.playable`,
  `<Name>_Stage.unity`, `<Name>_Bindings.asset`.

A "conflict" on generated output is resolved by re-assembling, not by hand-merging.

## Package layout

- `Runtime/` (`TimelineSmash.Runtime` asmdef → `Unity.Timeline`) — data ScriptableObjects in `Data/`
  (`SubTimelineSegment`, `ContributorSegmentSet`, `BindingManifest`, `AssembleSettings`,
  `CinematicComposition`).
- `Editor/` (`TimelineSmash.Editor` asmdef → Runtime + `Unity.Timeline` + `Unity.Timeline.Editor`):
  - `Assembler/CinematicAssembler.cs` — `FlattenTree` (recursively flattens nested compositions into
    absolute-time leaves) + `BuildMaster` (one `ControlTrack` per lane, one `ControlPlayableAsset` clip
    per leaf).
  - `Assembler/StageSceneBuilder.cs` (master + host directors), `BindingApplier.cs` (per-leaf bindings),
    `BindingCompiler.cs` (compiles a manifest tree → `CompiledBindings` master lookup).
  - `CinematicAssembleService.cs` (paths + orchestration), `GroupingService.cs`, `TimelineSmashMenu.cs`,
    `Inspectors/`, `Overview/CinematicOverviewModel.cs`, `Internal/EditorAssetUtil.cs`,
    `RecorderBridge.cs`.
  - `Recording/` — optional Recorder export in its own `TimelineSmash.Recorder.Editor` asmdef,
    define-constrained on `TIMELINESMASH_RECORDER` (compiles only when `com.unity.recorder` is present),
    registering itself through `RecorderBridge`.
- `Tests/Editor/` (`TimelineSmash.Editor.Tests`) — EditMode tests; shared fixtures in `TestAssets.cs`.

## Build / test

The Unity editor is often open on this project (holds `Temp/UnityLockfile`), so `-batchmode` against the
repo fails with a lock error. Two ways to run tests:

- **In-editor:** Window ▸ General ▸ Test Runner ▸ EditMode.
- **Headless, isolated (also validates the package as a consumer install):** create a throwaway project
  that embeds the package (copy it under `Packages/`, or reference it via `file:`) and lists it in the
  project manifest's `testables`, then run with your Unity 6000.3.10f1 editor:

  ```
  <UnityEditor> -batchmode -nographics -projectPath <proj> \
    -runTests -testPlatform EditMode -testResults r.xml -logFile u.log -accept-apiupdate
  ```

  After a run, copy any newly generated `.meta` files back into the package so GUIDs are committed.

Pure-logic tests (flatten, binding compile, grouping) build assets in memory with
`ScriptableObject.CreateInstance` and need no AssetDatabase; only asset-writing paths touch disk.

## Conventions & gotchas

- **Force Text serialization is required** (mergeable `.playable`/`.asset`/`.unity`) — `EditorSettings`
  `m_SerializationMode: 2`. Keep it.
- **Commit `.meta` files** for everything in the package (asmdef GUIDs etc.).
- **Freshly-created `TimelineAsset` instances go fake-null** after `AssetDatabase.ImportAsset` or a scene
  save — the managed instance is destroyed. Reload via `AssetDatabase.LoadAssetAtPath`, never trust a
  held C# reference. (`BuildMaster` avoids `ImportAsset`; `StageSceneBuilder` reloads the master by path.)
  A plain `BindingManifest` is *not* affected (no sub-assets).
- **Assets cannot serialize scene-object references** — the generated `_Bindings.asset` is a best-effort
  inspection snapshot (keys + project-asset targets); live binding application uses the in-memory
  `CompiledBindings`.
- **Determinism matters** — `FlattenTree` and `BindingCompiler` sort/iterate deterministically so
  generated artifacts are regenerable, not merge surfaces. Don't introduce nondeterministic ordering.
- Timeline tracks/clips are **sub-assets** of the `.playable`; on regenerate, delete + recreate the whole
  asset — never surgically remove sub-assets.

## Releasing

Bump `Packages/com.swizzlebake.timelinesmash/package.json` `version`, add a `CHANGELOG.md` entry, commit,
then `git tag -a X.Y.Z -m "…"` and push the tag. Consumers pin with `#X.Y.Z` on the git URL. Current
latest: **0.13.0**. Minimum editor: **Unity 2021.3 LTS** (`com.unity.timeline` ≥ 1.6.1; optional
Recorder export needs `com.unity.recorder` ≥ 4.0.0).

## Git

Default branch `main`. End commit messages with:

```
Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
```
