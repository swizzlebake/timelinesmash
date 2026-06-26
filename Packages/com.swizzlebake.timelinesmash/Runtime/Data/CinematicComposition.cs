using System.Collections.Generic;
using UnityEngine;

namespace TimelineSmash
{
    /// <summary>
    /// The master cinematic: which contributors take part, which binding manifest resolves shared
    /// actors, and where the regenerable artifacts are written. Edited rarely — adding a contributor
    /// is an append, so even this shared file stays merge-friendly.
    /// </summary>
    [CreateAssetMenu(
        fileName = "Cinematic",
        menuName = "TimelineSmash/Cinematic Composition",
        order = 101)]
    public class CinematicComposition : ScriptableObject
    {
        [Tooltip("Used to name the generated master timeline and stage scene.")]
        public string cinematicName = "Cinematic";

        [Tooltip("Each artist's contributions. Order does not affect the deterministic result.")]
        public List<ContributorSegmentSet> contributors = new List<ContributorSegmentSet>();

        [Tooltip("Resolves logical binding keys to shared scene actors at assemble time.")]
        public BindingManifest bindingManifest;

        public AssembleSettings settings = new AssembleSettings();

        [Tooltip("High-resolution capture settings used by Record (image sequence).")]
        public CaptureSettings capture = new CaptureSettings();

        [Tooltip("Project-relative folder where the regenerable master timeline and stage scene " +
                 "are written. This folder is meant to be gitignored.")]
        public string outputFolder = "Assets/Cinematics/Generated";

        // --- Stage population --------------------------------------------------------------------
        // By default the generated stage scene holds only the master + host directors, so it records
        // nothing unless every segment spawns a prefab. Pointing it at a source scene and/or an actor
        // prefab makes the stage self-contained: it ships the real, animatable actors (and, for a scene,
        // its lighting and camera) so it can be played and recorded on its own. Bindings then resolve
        // against those actors by name (a manifest entry still wins when present). Set via the inspector's
        // "Stage source" fields. Both are optional and combine; leave empty for a bare director-only stage.

        [Tooltip("Optional prefab instantiated at the root of the generated stage scene so it carries real, " +
                 "animatable actors. Bindings resolve against it by name. Combines with Stage Source Scene.")]
        [HideInInspector]
        public GameObject stageActorPrefab;

        [Tooltip("GUID of a scene cloned as the base of the generated stage scene, shipping its actors, " +
                 "lighting and camera. Stored as a GUID (survives asset moves); set via the inspector.")]
        [HideInInspector]
        public string stageSourceSceneGuid = "";
    }
}
