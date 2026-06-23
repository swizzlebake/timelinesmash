# Two-Artist Demo

A minimal, hand-built walkthrough of the TimelineSmash workflow. Because Timeline assets are most
easily authored in the editor, this sample is a recipe rather than prebuilt binaries — follow it
once to see an end-to-end cinematic assembled from two contributors.

## Steps

1. **Shared actors.** In a scene, add:
   - `MainCam` — a Camera with an `Animator`
   - `Hero` — a GameObject with an `Animator`
2. **Binding manifest.** `Create ▸ TimelineSmash ▸ Binding Manifest`. Add:
   - `MainCam` → the MainCam Animator
   - `Hero` → the Hero Animator
3. **Artist A (camera).** Create a Timeline (`.playable`) named `Camera` with an Animation Track
   **named `MainCam`**, animating the camera. Then `Create ▸ TimelineSmash ▸ Contributor Segment Set`,
   name the owner `Bob`, and add a segment: sub-timeline `Camera`, lane `Camera`, start `0`, duration `5`.
4. **Artist B (character).** Create a Timeline named `Hero` with an Animation Track **named `Hero`**,
   animating the character. Create another Contributor Segment Set, owner `Alice`, with a segment:
   sub-timeline `Hero`, lane `Characters`, start `0`, duration `5`.
5. **Composition.** `Create ▸ TimelineSmash ▸ Cinematic Composition`. Assign both contributor sets and
   the binding manifest. Press **Assemble (master + stage)**.
6. **Preview.** Press **Open Stage**, then Play — both sub-timelines drive the shared actors. Or
   **Open Master** to scrub the combined timeline.
7. *(optional)* With `com.unity.recorder` installed, press **Record cinematic**.

## The point

Alice and Bob only ever edited *their own* files (`Hero.playable` + Alice's set; `Camera.playable` +
Bob's set). Working in parallel and merging produces no conflicts; re-assembling combines their work.
The generated `*_Master.playable` and `*_Stage.unity` are disposable — delete them and Assemble again.
