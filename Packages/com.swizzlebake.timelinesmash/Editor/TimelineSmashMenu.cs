using UnityEditor;
using UnityEngine;

namespace TimelineSmash.Editor
{
    /// <summary>Menu entry points mirroring the inspector's Assemble action.</summary>
    public static class TimelineSmashMenu
    {
        const string AssetMenu = "Assets/TimelineSmash/Assemble Composition";

        [MenuItem(AssetMenu, true)]
        static bool ValidateAssembleSelected()
        {
            return Selection.activeObject is CinematicComposition;
        }

        [MenuItem(AssetMenu)]
        static void AssembleSelected()
        {
            var comp = Selection.activeObject as CinematicComposition;
            if (comp == null)
                return;

            var result = CinematicAssembleService.Assemble(comp, true);
            if (result == null)
                return;

            if (result.warnings.Count > 0)
                Debug.LogWarning($"[TimelineSmash] Assembled '{result.masterPath}' with warnings:\n - " +
                                 string.Join("\n - ", result.warnings));
            else
                Debug.Log($"[TimelineSmash] Assembled '{result.masterPath}' " +
                          $"({result.entries.Count} segments).");
        }
    }
}
