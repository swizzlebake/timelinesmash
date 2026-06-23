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
                "track name (or a segment's binding-key override). Keys live here, not in the scene, " +
                "so the working scene stays merge-friendly.",
                MessageType.Info);

            DrawDefaultInspector();

            var manifest = (BindingManifest)target;
            int empties = 0;
            foreach (var e in manifest.entries)
                if (e == null || string.IsNullOrEmpty(e.key) || e.target == null)
                    empties++;

            if (empties > 0)
                EditorGUILayout.HelpBox($"{empties} entry(ies) are missing a key or target.", MessageType.Warning);
        }
    }
}
