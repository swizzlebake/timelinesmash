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
