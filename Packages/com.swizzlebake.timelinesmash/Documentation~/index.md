# TimelineSmash Documentation

## Architecture

```
Sub-timelines (.playable)        ── one file per artist ──┐
Contributor Segment Sets (.asset) ── one file per artist ─┤
Binding Manifest (.asset)         ── shared, rare edits ──┤── deterministic ──▶  <Name>_Master.playable   (generated)
Cinematic Composition (.asset)    ── shared, rare edits ──┘    assembler          <Name>_Stage.unity        (generated)
```

The **source of truth** (top four) is committed and split so each file has a single owner. The
**generated** master + stage are regenerable artifacts — gitignored, never hand-merged.

### Assemble pipeline

1. **Flatten** all contributor segments into one list, sorted by a stable key
   `(laneName, start, owner, setIndex, indexInSet)` — determinism is what makes the master a
   regenerable artifact instead of a merge surface.
2. **Build master** (`CinematicAssembler.BuildMaster`): recreate the `.playable` from scratch, one
   `ControlTrack` per lane, one `ControlPlayableAsset` clip per segment. Clip timing comes straight
   from the segment record (`start`, `duration`, `clipIn`, `timeScale`). Each clip gets a
   deterministic exposed-reference name (`TS_0000`, `TS_0001`, …).
3. **Build stage** (`StageSceneBuilder`): a master `PlayableDirector` bound to the master timeline,
   plus one **host** `PlayableDirector` per segment (its `playableAsset` is the sub-timeline). The
   master's Control clips resolve their exposed reference to the matching host.
4. **Apply bindings** (`BindingApplier`): for each output track of a segment's sub-timeline, resolve
   `segment.bindingKey ?? track.name` through the manifest and `SetGenericBinding` it on the **host**
   director (where nested Animation/Audio track bindings resolve).

### Why a host director per segment

`DirectorControlPlayable` drives a nested timeline through a live `PlayableDirector`, and a nested
`AnimationTrack`'s binding resolves against **that** director (`director.GetGenericBinding(track)`).
Recording also needs those directors live. Host directors live only in the generated stage scene,
so the hand-edited working scene never carries merge-prone director/binding wiring.

## Bindings

Keys default to **track name**: a sub-timeline track named `HeroCam` binds to the manifest entry
`HeroCam`. Override per segment with `bindingKey` (applies to all of that segment's tracks — handy
for single-track shots). Unresolved keys produce warnings, not errors.

## Recording (optional)

Install `com.unity.recorder` to light up the **Record** button. The integration lives in a separate,
define-constrained assembly, so the package compiles fine without Recorder. In-editor playback works
without it — open the stage scene and press Play.

## Determinism & merge safety guarantees

- Same inputs → same master structure and exposed names.
- The master `.playable` is deleted and recreated wholesale each assemble (never surgically edited),
  avoiding orphaned Timeline sub-assets.
- Bindings are derived from the manifest and rewritten every assemble — regeneration *restores*
  bindings rather than losing them. Do not hand-edit the stage scene.

## Testing

EditMode tests under `Tests/Editor` cover flatten/sort determinism, master generation (lanes,
clips, timing, edge cases), the overview model (overlaps/gaps), stage building (host count and
exposed-reference resolution), and binding resolution. Run them from the Test Runner or headlessly
with `-runTests -testPlatform EditMode`.
