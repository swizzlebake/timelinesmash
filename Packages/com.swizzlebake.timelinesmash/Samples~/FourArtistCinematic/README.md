# Four-Artist Cinematic — Orbital Relay

This is the production-scale companion to the minimal Two-Artist recipe. It is a prebuilt, 45-second
TimelineSmash composition with **four independently owned contributor trees**, 16 flattened segments and
seven final lanes. It is deliberately made from simple primitives so the collaboration architecture stays
visible and no third-party art package is required.

## Run it

1. Import **Four-Artist Cinematic** from TimelineSmash's Package Manager **Samples** tab.
2. Select `Content/OrbitalRelay.asset`.
3. Choose **Tools ▸ TimelineSmash ▸ Samples ▸ Assemble Four-Artist Cinematic** (or click
   **Assemble (master + stage)** in the inspector).
4. Open `Assets/Cinematics/Generated/OrbitalRelay_Stage.unity` and press Play.

The source scene and actor prefab are copied into the generated stage. The master, compiled bindings and
stage are disposable; use **Reset Four-Artist Generated Artifacts** to remove exactly those three files.
To create a fresh working copy under `Assets/Cinematics/FourArtistRelay`, use **Create Editable Four-Artist
Cinematic**.

## Ownership map

| Artist | Owns | Timeline responsibility |
| --- | --- | --- |
| Alice — Character | `Content/Artists/Alice/` | Four character beats; Animation + Audio + Activation tracks |
| Bob — Camera & Edit | `Content/Artists/Bob/` | Four sequential camera passes, clip-in and speed variation |
| Cleo — Props & FX | `Content/Artists/Cleo/` | Orb motion, signals, activation and two spawned pulse prefabs |
| Dev — Lighting & Audio | `Content/Artists/Dev/` | Prelude lighting plus a nested, namespaced finale composition |

The team lead pre-seeds `OrbitalRelay.asset` and `Shared/Bindings/OrbitalRelay_Bindings.asset` before the
four branches split. During normal production, artists change only their own folder and child manifest.

## Features exercised

- 45 seconds, 16 flattened leaves, seven lanes, both overlapping work and sequential hand-offs.
- A root binding manifest including four artist-owned child manifests.
- Per-track retargeting (`hero/Body`, `hero/Voice`, `hero/Prop`, and equivalent namespaces).
- Prefab-asset binding targets remapped to the live stage-prefab instance.
- A nested Dev finale that flattens to `Finale/Satellite`, `Finale/Lighting`, and `Finale/Audio`.
- Real generated WAV audio, a bound SignalTrack, ActivationTracks, nested ControlTrack wiring, prefab
  spawning, clip-in and playback-speed changes.
- Deterministic master regeneration: generated output is not source and must not be merged.

See [COLLABORATION.md](COLLABORATION.md) for the four-branch rehearsal and file-ownership rules.
