# TimelineSmash — elaborate cinematic findings

A "moderately expansive" cinematic was assembled to see whether TimelineSmash carries a real mix of
timeline track types end to end. **Verdict: yes, the core pipeline holds up** — keyframed translation +
rotation, audio, activation, and signal tracks all flow through `assemble → compile-bindings → stage` and
the bound transforms actually move when evaluated. Three limitations surfaced — **all three are now fixed**
(see "Gaps found — all three now fixed" below).

> Verified: the new `PlaybackEvaluationTests` pass headlessly alongside the existing suite (57/57) on
> Unity 6000.3.10f1. `HostEvaluation_MovesBoundTransform` proves real motion (Transform translates > 0.5u
> and rotates > 30° when the host director is evaluated); the fix tests prove per-track override retargeting,
> nested-control wiring, idempotent active-scene assembly, and bind-by-name resolution.
>
> One sharp authoring gotcha this surfaced: a freshly-created `TimelineAsset` goes **fake-null** the moment
> any scene operation (or an `AddObjectToAsset`/`CreateAsset` write that reimports the `.playable`) runs
> after it is built — its `m_Tracks` becomes null and `GetOutputTracks()` returns empty. Build sub-timelines
> *after* the scene exists, author `AnimationClip`s as standalone `.anim` assets (not sub-assets of the
> `.playable`), and never trust a held reference across a scene/import op. This is exactly the hazard the
> repo's CLAUDE.md warns about, now demonstrated end to end.

## What it takes to author an expansive cinematic

The mental model that matters: **the generated master is *only* ControlTracks** (one per lane, one control
clip per segment). All real content lives in the per-artist sub-timeline `.playable` assets, and bindings
are applied to **host PlayableDirectors**, one per segment, at stage-build time.

To author one, you need, per artist:
1. A sub-timeline `.playable` with the actual tracks (Animation / Audio / Activation / Signal …). **Track
   names are the binding keys** — name them after the logical actor (`Body`, `Voice`, `Prop`, `CameraRig`).
   - Animation curves drive the bound Animator's own transform: path `""`, `m_LocalPosition.*` for
     translation and **`localEulerAnglesRaw.*`** for rotation (`localEulerAngles` silently does nothing).
   - Keyframed `AnimationClip`s must be added as sub-assets of the `.playable` (`AddObjectToAsset`) or the
     curves are lost on reload.
2. A `ContributorSegmentSet` placing that sub-timeline on a lane at a start/duration.
3. A `BindingManifest` mapping each track-name key to a shared scene actor (Animator / AudioSource /
   GameObject / SignalReceiver).
4. A `CinematicComposition` tying the contributors + manifest together, then **Assemble**.

Binding target types confirmed working via `SetGenericBinding`: AnimationTrack→`Animator`,
AudioTrack→`AudioSource`, ActivationTrack→`GameObject`, SignalTrack→`SignalReceiver`.

## What was exercised

- **Master ControlTracks** — one per lane, correct exposed-reference wiring to host directors.
- **AnimationTracks with translation + rotation** — proven by evaluating a host director and asserting the
  bound transform translated > 0.5u and rotated > 30°.
- **Multi-track sub-timeline** (Animation + Audio + Activation in one `.playable`) — each track binds by its
  own name to a differently-typed actor.
- **SignalTrack** — emitter marker + `SignalAsset`, bound to a `SignalReceiver` by track name.
- **Nested ControlTrack** inside a sub-timeline — included specifically to probe gap B.

See `Packages/com.swizzlebake.timelinesmash/Tests/Editor/PlaybackEvaluationTests.cs`.

## Gaps found — all three now fixed

### A. `bindingKey` override is all-or-nothing → **fixed: namespaced per-track keys**
Previously `BindingApplier.Apply` mapped *every* track of a sub-timeline to the one override key, so a
multi-track sub-timeline reused by two segments couldn't retarget its tracks individually. Now, when a
`bindingKey` override is set, each track resolves **`"<bindingKey>/<trackName>"` first**, then falls back to
the bare `"<bindingKey>"`. So a manifest can carry `alice/Body`, `alice/Voice`, `bob/Body`, … to give each
track its own actor, while a bare `hero` still binds the whole sub-timeline (backwards compatible).
Tests: `BindingKeyOverride_NamespacedKey_RetargetsEachTrack`, `BindingKeyOverride_BareKey_BindsWholeSub`.

### B. Nested ControlTrack left unwired → **fixed: exposed refs resolved on the host**
`BindingApplier` now special-cases `ControlTrack`: for each control clip it resolves the manifest target
(by track name / override) to a GameObject and calls `hostDir.SetReferenceValue(exposedName, go)`, so a
control track nested inside a sub-timeline drives its source instead of keeping a null reference.
Test: `NestedControlTrack_SourceIsWired`.

### C. Stage scene regenerated empty → **fixed: assemble into the active scene + bind by name**
New `CinematicAssembleService.AssembleIntoActiveScene(comp)` (menu: **Assets ▸ TimelineSmash ▸ Assemble Into
Active Scene**) wires the master + host directors into the **currently open scene** — the one that already
holds your actors — instead of regenerating an empty stage. It is **idempotent** (re-running replaces the
master it added rather than stacking a duplicate) and leaves your actors untouched. Because a committed
manifest `.asset` can't reference scene objects, binding keys the manifest doesn't resolve **fall back to a
scene GameObject of that name**, picking the component the track binds to — so naming an actor after its
track (or binding key) is enough. `StageSceneBuilder.Populate(scene, result, bindings, resolveBySceneName:true)`
exposes the same behaviour. Tests: `AssembleIntoActiveScene_IsIdempotent_AndBindsByName`,
`Populate_ResolvesUnmappedKey_BySceneObjectName`.

## How to reproduce

- **Tests:** Window ▸ General ▸ Test Runner ▸ EditMode → run `PlaybackEvaluationTests`.
- **Demo:** Tools ▸ TimelineSmash ▸ Build Elaborate Demo Scene → open `Demo_Stage.unity` → press Play.
  (`Demo_Stage.unity`, `SubTimelines/`, and `Generated/` here are regenerable — rebuild via the menu.)
