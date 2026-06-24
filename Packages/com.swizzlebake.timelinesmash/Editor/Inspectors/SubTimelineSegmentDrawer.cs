using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

namespace TimelineSmash.Editor
{
    /// <summary>
    /// Per-segment editing aid: a collapsible summary, a warning when the sub-timeline / sub-composition
    /// either/or is violated, a lane dropdown seeded from lanes already in use, and a live preview of the
    /// manifest keys this segment binds (so the artist sees exactly what the Bindings checklist will want).
    /// </summary>
    [CustomPropertyDrawer(typeof(SubTimelineSegment))]
    public class SubTimelineSegmentDrawer : PropertyDrawer
    {
        const float Pad = 2f;
        static float LineH => EditorGUIUtility.singleLineHeight;
        static float Step => LineH + Pad;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return property.isExpanded ? Step * (1 + FieldLines(property)) : Step;
        }

        static int FieldLines(SerializedProperty property)
        {
            var subTl = property.FindPropertyRelative("subTimeline").objectReferenceValue;
            var subComp = property.FindPropertyRelative("subComposition").objectReferenceValue;
            bool note = (subTl != null) == (subComp != null); // both set, or neither
            // subTimeline, subComposition, lane, bindingKey, start, duration, clipIn, speed
            return 8 + (note ? 1 : 0) + (subTl != null ? 1 : 0);
        }

        public override void OnGUI(Rect pos, SerializedProperty property, GUIContent label)
        {
            var subTlProp = property.FindPropertyRelative("subTimeline");
            var subCompProp = property.FindPropertyRelative("subComposition");
            var laneProp = property.FindPropertyRelative("laneName");
            var keyProp = property.FindPropertyRelative("bindingKey");
            var startProp = property.FindPropertyRelative("start");
            var durProp = property.FindPropertyRelative("duration");

            var subTl = subTlProp.objectReferenceValue as TimelineAsset;
            var subComp = subCompProp.objectReferenceValue;

            var r = new Rect(pos.x, pos.y, pos.width, LineH);

            string name = subTl != null ? subTl.name : subComp != null ? subComp.name : "(empty)";
            string lane = string.IsNullOrEmpty(laneProp.stringValue) ? "Main" : laneProp.stringValue;
            double start = startProp.doubleValue, dur = durProp.doubleValue;
            property.isExpanded = EditorGUI.Foldout(
                r, property.isExpanded, $"{name} · {lane} · {start:0.#}-{start + dur:0.#}s", true);
            if (!property.isExpanded)
                return;

            r.y += Step; EditorGUI.PropertyField(r, subTlProp);
            r.y += Step; EditorGUI.PropertyField(r, subCompProp);

            bool conflict = subTl != null && subComp != null;
            bool missing = subTl == null && subComp == null;
            if (conflict || missing)
            {
                r.y += Step;
                var prevC = GUI.color;
                GUI.color = conflict ? new Color(1f, 0.6f, 0.6f) : new Color(0.85f, 0.85f, 0.6f);
                EditorGUI.LabelField(r, conflict
                    ? "Set EITHER Sub Timeline OR Sub Composition, not both."
                    : "Assign a Sub Timeline (or a Sub Composition to nest a group).",
                    EditorStyles.miniLabel);
                GUI.color = prevC;
            }

            r.y += Step; DrawLane(r, laneProp, property);
            r.y += Step; EditorGUI.PropertyField(r, keyProp);
            r.y += Step; EditorGUI.PropertyField(r, startProp);
            r.y += Step; EditorGUI.PropertyField(r, durProp);
            r.y += Step; EditorGUI.PropertyField(r, property.FindPropertyRelative("clipIn"));
            r.y += Step; EditorGUI.PropertyField(r, property.FindPropertyRelative("speed"));

            if (subTl != null)
            {
                r.y += Step;
                DrawBindingHint(r, subTl, keyProp.stringValue);
            }
        }

        static void DrawLane(Rect r, SerializedProperty laneProp, SerializedProperty property)
        {
            const float btn = 20f;
            EditorGUI.PropertyField(new Rect(r.x, r.y, r.width - btn, r.height), laneProp);

            var btnRect = new Rect(r.x + r.width - btn, r.y, btn, r.height);
            if (!GUI.Button(btnRect, "▾", EditorStyles.miniButton))
                return;

            var target = property.serializedObject.targetObject;
            string path = property.propertyPath;
            var menu = new GenericMenu();
            foreach (var laneName in LanesInSet(target))
            {
                string captured = laneName;
                menu.AddItem(new GUIContent(laneName), laneProp.stringValue == laneName, () =>
                {
                    // Re-open a SerializedObject in the deferred callback; the original may be stale.
                    var so = new SerializedObject(target);
                    so.FindProperty(path + ".laneName").stringValue = captured;
                    so.ApplyModifiedProperties();
                });
            }
            if (menu.GetItemCount() == 0)
                menu.AddDisabledItem(new GUIContent("No lanes yet"));
            menu.DropDown(btnRect);
        }

        static IEnumerable<string> LanesInSet(Object target)
        {
            var lanes = new SortedSet<string> { "Main" };
            if (target is ContributorSegmentSet set && set.segments != null)
                foreach (var s in set.segments)
                    if (s != null && !string.IsNullOrEmpty(s.laneName))
                        lanes.Add(s.laneName);
            return lanes;
        }

        static void DrawBindingHint(Rect r, TimelineAsset sub, string bindingKey)
        {
            var temp = new SubTimelineSegment { bindingKey = bindingKey };
            var keys = sub.GetOutputTracks()
                .Where(t => t != null && (t is ControlTrack || BindingApplier.BindingTypeOf(t) != null))
                .Select(t => BindingApplier.CandidateKeys(temp, t.name).First())
                .ToList();

            var prevC = GUI.color;
            GUI.color = new Color(0.7f, 0.85f, 1f);
            EditorGUI.LabelField(r,
                "Binds keys: " + (keys.Count == 0 ? "no bindable tracks" : string.Join(", ", keys)),
                EditorStyles.miniLabel);
            GUI.color = prevC;
        }
    }
}
