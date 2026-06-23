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

        [Tooltip("Project-relative folder where the regenerable master timeline and stage scene " +
                 "are written. This folder is meant to be gitignored.")]
        public string outputFolder = "Assets/Cinematics/Generated";
    }
}
