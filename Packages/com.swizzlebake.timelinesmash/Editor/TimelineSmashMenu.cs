using UnityEditor;
using UnityEngine;

namespace TimelineSmash.Editor
{
    /// <summary>Menu entry points mirroring the inspector's Assemble action.</summary>
    public static class TimelineSmashMenu
    {
        const string AssetMenu = "Assets/TimelineSmash/Assemble Composition";
        const string AssetMenuActiveScene = "Assets/TimelineSmash/Assemble Into Active Scene";
        const string NewCinematicMenu = "Assets/TimelineSmash/New Cinematic";

        [MenuItem(NewCinematicMenu, false, 0)]
        static void NewCinematic()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "New TimelineSmash Cinematic", "Cinematic", "asset",
                "Creates a Cinematic Composition + Binding Manifest, wired together.");
            if (string.IsNullOrEmpty(path))
                return;

            string folder = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string name = System.IO.Path.GetFileNameWithoutExtension(path);

            var comp = CinematicScaffold.CreateCinematic(folder, name);
            Selection.activeObject = comp;
            EditorGUIUtility.PingObject(comp);
            Debug.Log($"[TimelineSmash] Created cinematic '{comp.cinematicName}' + manifest. " +
                      "Add contributors from its inspector.");
        }

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

        [MenuItem(AssetMenuActiveScene, true)]
        static bool ValidateAssembleActiveScene()
        {
            return Selection.activeObject is CinematicComposition;
        }

        [MenuItem(AssetMenuActiveScene)]
        static void AssembleIntoActiveScene()
        {
            var comp = Selection.activeObject as CinematicComposition;
            if (comp == null)
                return;

            var result = CinematicAssembleService.AssembleIntoActiveScene(comp);
            if (result == null)
                return;

            if (result.warnings.Count > 0)
                Debug.LogWarning($"[TimelineSmash] Assembled '{result.masterPath}' into the active scene " +
                                 $"with warnings:\n - " + string.Join("\n - ", result.warnings));
            else
                Debug.Log($"[TimelineSmash] Assembled '{result.masterPath}' into the active scene " +
                          $"({result.entries.Count} segments). Save the scene to keep it.");
        }
    }
}
