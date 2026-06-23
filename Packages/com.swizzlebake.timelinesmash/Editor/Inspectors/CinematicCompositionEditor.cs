using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Timeline;

namespace TimelineSmash.Editor
{
    [CustomEditor(typeof(CinematicComposition))]
    public class CinematicCompositionEditor : UnityEditor.Editor
    {
        static readonly Color[] s_OwnerColors =
        {
            new Color(0.26f, 0.59f, 0.98f), new Color(0.40f, 0.78f, 0.40f),
            new Color(0.95f, 0.61f, 0.27f), new Color(0.80f, 0.45f, 0.90f),
            new Color(0.95f, 0.40f, 0.45f), new Color(0.35f, 0.80f, 0.80f),
        };

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var comp = (CinematicComposition)target;
            EditorGUILayout.Space();

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Assemble", EditorStyles.boldLabel);

                if (GUILayout.Button("Assemble (master + stage)", GUILayout.Height(26)))
                    Report(CinematicAssembleService.Assemble(comp, true));

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Master only"))
                        Report(CinematicAssembleService.Assemble(comp, false));
                    if (GUILayout.Button("Open Master"))
                        OpenMaster(comp);
                    if (GUILayout.Button("Open Stage"))
                        OpenStage(comp);
                }

                if (RecorderBridge.Available)
                {
                    if (GUILayout.Button("Record cinematic"))
                    {
                        RecorderBridge.RecordAction(
                            comp,
                            CinematicAssembleService.MasterPath(comp),
                            CinematicAssembleService.StagePath(comp),
                            comp.settings != null ? comp.settings.totalDuration : 0);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "Install com.unity.recorder to enable cinematic export. In-editor playback " +
                        "works without it — Open Stage and press Play.",
                        MessageType.Info);
                }
            }

            DrawOverview(comp);
        }

        static void Report(AssembleResult result)
        {
            if (result == null)
                return;

            if (result.warnings.Count == 0)
            {
                Debug.Log($"[TimelineSmash] Assembled '{result.masterPath}' " +
                          $"({result.entries.Count} segments, {result.totalDuration:0.###}s).");
                return;
            }

            Debug.LogWarning($"[TimelineSmash] Assembled '{result.masterPath}' with " +
                             $"{result.warnings.Count} warning(s):\n - {string.Join("\n - ", result.warnings)}");
        }

        static void OpenMaster(CinematicComposition comp)
        {
            var path = CinematicAssembleService.MasterPath(comp);
            var master = AssetDatabase.LoadAssetAtPath<TimelineAsset>(path);
            if (master == null)
            {
                Debug.LogWarning($"[TimelineSmash] No master at '{path}'. Assemble first.");
                return;
            }

            Selection.activeObject = master;
            TimelineEditor.GetOrCreateWindow().SetTimeline(master);
        }

        static void OpenStage(CinematicComposition comp)
        {
            var path = CinematicAssembleService.StagePath(comp);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[TimelineSmash] No stage scene at '{path}'. Assemble (master + stage) first.");
                return;
            }

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }

        void DrawOverview(CinematicComposition comp)
        {
            var model = CinematicOverviewModel.Build(comp);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                $"Overview — {model.lanes.Count} lane(s), {model.totalDuration:0.###}s",
                EditorStyles.boldLabel);

            if (model.lanes.Count == 0)
            {
                EditorGUILayout.HelpBox("No segments yet. Add contributors with segments above.", MessageType.None);
                return;
            }

            double total = model.totalDuration > 0 ? model.totalDuration : 1;

            foreach (var lane in model.lanes)
            {
                EditorGUILayout.LabelField(lane.name, EditorStyles.miniBoldLabel);
                Rect row = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(row, new Color(0, 0, 0, 0.15f));

                foreach (var seg in lane.segments)
                {
                    float x = row.x + (float)(seg.start / total) * row.width;
                    float w = Mathf.Max(2f, (float)(seg.Duration / total) * row.width);
                    var bar = new Rect(x, row.y + 1, w, row.height - 2);
                    EditorGUI.DrawRect(bar, ColorFor(seg.owner));
                    if (w > 40)
                        GUI.Label(bar, $" {seg.subTimelineName}", EditorStyles.miniLabel);
                }
            }

            if (model.warnings.Count > 0)
                EditorGUILayout.HelpBox(string.Join("\n", model.warnings), MessageType.Warning);
        }

        static Color ColorFor(string owner)
        {
            int h = owner != null ? owner.GetHashCode() : 0;
            return s_OwnerColors[(h & 0x7fffffff) % s_OwnerColors.Length];
        }
    }
}
