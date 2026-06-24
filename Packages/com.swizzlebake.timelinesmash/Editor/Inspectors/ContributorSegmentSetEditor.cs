using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

namespace TimelineSmash.Editor
{
    [CustomEditor(typeof(ContributorSegmentSet))]
    public class ContributorSegmentSetEditor : UnityEditor.Editor
    {
        TimelineAsset _toAdd;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var set = (ContributorSegmentSet)target;
            EditorGUILayout.Space();

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Quick add segment", EditorStyles.boldLabel);
                _toAdd = (TimelineAsset)EditorGUILayout.ObjectField(
                    "Sub-timeline", _toAdd, typeof(TimelineAsset), false);

                using (new EditorGUI.DisabledScope(_toAdd == null))
                {
                    if (GUILayout.Button("Add as new segment"))
                    {
                        Undo.RecordObject(set, "Add segment");
                        double start = NextStart(set);
                        set.segments.Add(new SubTimelineSegment
                        {
                            subTimeline = _toAdd,
                            laneName = "Main",
                            start = start,
                            duration = _toAdd.duration > 0 ? _toAdd.duration : 5,
                        });
                        EditorUtility.SetDirty(set);
                        _toAdd = null;
                    }
                }

                EditorGUILayout.LabelField($"{set.segments.Count} segment(s)", EditorStyles.miniLabel);
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Grouping", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "Combine these segments into one reusable sub-composition (a movable group). " +
                    "Flattening is unchanged — only the structure becomes portable.",
                    EditorStyles.wordWrappedMiniLabel);

                using (new EditorGUI.DisabledScope(set.segments.Count < 1))
                {
                    if (GUILayout.Button("Group all segments into a sub-composition"))
                        GroupAll(set);
                }
            }
        }

        static void GroupAll(ContributorSegmentSet set)
        {
            var path = AssetDatabase.GetAssetPath(set);
            var folder = string.IsNullOrEmpty(path) ? "Assets" : System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            var indices = new List<int>();
            for (int i = 0; i < set.segments.Count; i++)
                indices.Add(i);

            Undo.RecordObject(set, "Group segments");
            var group = GroupingService.GroupSegments(set, indices, set.name + "_Group", folder);
            if (group != null)
                Selection.activeObject = group;
        }

        // Append after the latest existing segment end so quick-adds don't stack at 0.
        static double NextStart(ContributorSegmentSet set)
        {
            double end = 0;
            foreach (var seg in set.segments)
                if (seg != null)
                    end = System.Math.Max(end, seg.start + (seg.duration > 0 ? seg.duration : 0));
            return end;
        }
    }
}
