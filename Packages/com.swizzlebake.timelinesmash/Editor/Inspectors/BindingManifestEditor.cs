using UnityEditor;
using UnityEngine;

namespace TimelineSmash.Editor
{
    [CustomEditor(typeof(BindingManifest))]
    public class BindingManifestEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "Each key maps a logical name to a shared scene actor. Sub-timeline tracks bind by " +
                "track name (or a segment's binding-key override). Add Includes to split lookups across " +
                "files; at assemble time the whole tree compiles into one master lookup (first definition " +
                "of a key wins). Keys live here, not in the scene, so the working scene stays merge-friendly.",
                MessageType.Info);

            DrawDefaultInspector();

            var manifest = (BindingManifest)target;
            int empties = 0;
            foreach (var e in manifest.entries)
                if (e == null || string.IsNullOrEmpty(e.key) || e.target == null)
                    empties++;

            if (empties > 0)
                EditorGUILayout.HelpBox($"{empties} entry(ies) are missing a key or target.", MessageType.Warning);

            EditorGUILayout.Space();
            if (GUILayout.Button("Compile preview"))
            {
                var compiled = BindingCompiler.Compile(manifest);
                if (compiled.warnings.Count == 0)
                    Debug.Log($"[TimelineSmash] Compiled '{manifest.name}': {compiled.Count} key(s), no conflicts.");
                else
                    Debug.LogWarning($"[TimelineSmash] Compiled '{manifest.name}': {compiled.Count} key(s), " +
                                     $"{compiled.warnings.Count} warning(s):\n - " + string.Join("\n - ", compiled.warnings));
            }
        }
    }
}
