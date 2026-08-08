# Four-branch collaboration rehearsal

This rehearsal proves the workflow, not merely the final playback. Start from a commit containing the
shared `OrbitalRelay.asset`, root manifest and all four contributor references. Do not add contributors to
the shared composition after branches split.

## Branch ownership

| Branch | May edit | Must not edit |
| --- | --- | --- |
| `cinematic/alice-character` | `Content/Artists/Alice/`, `Alice_Bindings.asset` | Other artists, root composition, generated output |
| `cinematic/bob-camera` | `Content/Artists/Bob/`, `Bob_Bindings.asset` | Other artists, root composition, generated output |
| `cinematic/cleo-fx` | `Content/Artists/Cleo/`, `Cleo_Bindings.asset` | Other artists, root composition, generated output |
| `cinematic/dev-finale` | `Content/Artists/Dev/`, `Dev_Bindings.asset` | Other artists, root composition, generated output |

## Rehearsal

1. Import the sample and commit it as the collaboration baseline.
2. Create the four branches above from that same commit.
3. On each branch, move or resize one segment and change one owned Timeline clip. Commit only that branch's
   owned files and their `.meta` files.
4. Merge the four branches into an integration branch. The source assets should merge without a Unity YAML
   conflict because no file had multiple owners.
5. Open `OrbitalRelay.asset`, inspect the visual timeline and confirm all four changes are present.
6. Assemble twice. Both runs must produce 16 segment hosts, zero assembly/binding warnings and the same
   lane/timing structure.
7. Open the generated stage and play through 45 seconds. Confirm character and camera motion, orb pulses,
   the audible relay tone, and the namespaced satellite finale.
8. Ensure `Assets/Cinematics/Generated/` is ignored. Never resolve a generated master or stage conflict by
   hand; delete the artifacts and assemble again.

## Shared-file changes

Changing the contributor roster, root include order, stage source, actor prefab or overall duration is a
coordinator task. Make that change on the integration branch, let all artist branches rebase, and only then
resume parallel authoring. This preserves the one-owner-per-file contract.
