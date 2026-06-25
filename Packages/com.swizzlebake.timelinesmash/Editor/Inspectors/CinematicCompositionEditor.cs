using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                EditorGUILayout.LabelField("Author", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Add contributor"))
                    {
                        var set = CinematicScaffold.AddContributor(comp, "New Artist");
                        if (set != null)
                        {
                            Selection.activeObject = set;
                            EditorGUIUtility.PingObject(set);
                        }
                    }
                    if (GUILayout.Button("Open visual timeline"))
                        CinematicTimelineWindow.Open(comp);
                }
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Assemble", EditorStyles.boldLabel);

                if (GUILayout.Button("Assemble (master + stage)", GUILayout.Height(26)))
                    Report(CinematicAssembleService.Assemble(comp, true));

                if (GUILayout.Button("Assemble into active scene"))
                    Report(CinematicAssembleService.AssembleIntoActiveScene(comp));

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
            DrawBindings(comp);
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
            var window = TimelineEditor.GetOrCreateWindow();
            window.Focus();
            // Defer: a freshly-opened/!focused Timeline window can drop a SetTimeline issued before its
            // first layout — which is why it only appeared after you selected another timeline. Setting it
            // on the next editor tick (window initialised, selection settled) shows it immediately.
            EditorApplication.delayCall += () =>
            {
                if (master != null)
                    TimelineEditor.GetOrCreateWindow().SetTimeline(master);
            };
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
                        BarLabel(bar, $" {seg.subTimelineName}", ColorFor(seg.owner));
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

        // Draw a label whose text color contrasts the bar it sits on, so it stays readable on any owner
        // color and in either editor theme (the default mini-label washed out on the lighter bars).
        static void BarLabel(Rect bar, string text, Color bg)
        {
            float luminance = 0.299f * bg.r + 0.587f * bg.g + 0.114f * bg.b;
            var style = new GUIStyle(EditorStyles.miniLabel);
            style.normal.textColor = luminance > 0.55f ? Color.black : Color.white;
            GUI.Label(bar, text, style);
        }

        // --- Bindings checklist -------------------------------------------------------------------

        void DrawBindings(CinematicComposition comp)
        {
            var plan = BindingPlan.Build(comp);

            EditorGUILayout.Space();
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(
                    plan.Total == 0 ? "Bindings — no bindable tracks" : $"Bindings — {plan.Bound} of {plan.Total} bound",
                    EditorStyles.boldLabel);

                if (comp.bindingManifest == null)
                {
                    EditorGUILayout.HelpBox(
                        "No binding manifest assigned. Create one to map track names to shared scene actors.",
                        MessageType.Info);
                    if (GUILayout.Button("Create & assign manifest"))
                    {
                        CreateAndAssignManifest(comp);
                        return;
                    }
                }

                var existingKeys = comp.bindingManifest != null
                    ? new HashSet<string>(comp.bindingManifest.entries
                        .Where(e => e != null && !string.IsNullOrEmpty(e.key)).Select(e => e.key))
                    : new HashSet<string>();

                foreach (var r in plan.requirements)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var prev = GUI.color;
                        GUI.color = r.Resolved ? new Color(0.5f, 0.9f, 0.5f) : new Color(1f, 0.55f, 0.55f);
                        GUILayout.Label(r.Resolved ? "✓" : "✗", GUILayout.Width(14));
                        GUI.color = prev;

                        GUILayout.Label($"{r.owner}/{r.lane} · {r.trackName} ({r.TypeLabel})",
                            EditorStyles.miniLabel, GUILayout.MinWidth(120));
                        GUILayout.FlexibleSpace();
                        GUILayout.Label(
                            r.Resolved ? $"→ {TargetName(r.target)}  key '{r.resolvedKey}'"
                                       : $"needs key '{r.suggestedKey}'",
                            EditorStyles.miniLabel);
                    }
                }

                int toAdd = comp.bindingManifest == null ? 0 : plan.requirements
                    .Where(r => !r.Resolved).Select(r => r.suggestedKey).Distinct()
                    .Count(k => !existingKeys.Contains(k));

                if (toAdd > 0 && GUILayout.Button($"Add {toAdd} missing key(s) to manifest"))
                    AddMissingKeys(comp, plan);

                EditorGUILayout.HelpBox(
                    "A track binds to the manifest key matching its name. To reuse one sub-timeline for " +
                    "different actors, set a per-segment Binding Key and add keys like '<key>/<trackName>'. " +
                    "Or name a scene object after a track and use Assemble Into Active Scene to bind without " +
                    "a manifest entry.",
                    MessageType.None);

                if (plan.warnings.Count > 0)
                    EditorGUILayout.HelpBox(string.Join("\n", plan.warnings), MessageType.Warning);
            }
        }

        static string TargetName(Object target)
        {
            if (target == null)
                return "(none)";
            return target is Component c ? $"{c.gameObject.name} ({c.GetType().Name})" : target.name;
        }

        static void CreateAndAssignManifest(CinematicComposition comp)
        {
            var dir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(comp));
            if (string.IsNullOrEmpty(dir))
                dir = "Assets";
            var path = AssetDatabase.GenerateUniqueAssetPath(
                $"{dir.Replace('\\', '/')}/{CinematicAssembleService.SafeName(comp)}_Manifest.asset");

            var manifest = CreateInstance<BindingManifest>();
            AssetDatabase.CreateAsset(manifest, path);
            AssetDatabase.SaveAssets();

            Undo.RecordObject(comp, "Assign binding manifest");
            comp.bindingManifest = manifest;
            EditorUtility.SetDirty(comp);
            Debug.Log($"[TimelineSmash] Created binding manifest '{path}' and assigned it.");
        }

        static void AddMissingKeys(CinematicComposition comp, BindingPlan plan)
        {
            var manifest = comp.bindingManifest;
            if (manifest == null)
                return;

            var existing = new HashSet<string>(manifest.entries
                .Where(e => e != null && !string.IsNullOrEmpty(e.key)).Select(e => e.key));

            Undo.RecordObject(manifest, "Add missing binding keys");
            int added = 0;
            foreach (var r in plan.requirements)
            {
                if (r.Resolved || !existing.Add(r.suggestedKey))
                    continue;
                manifest.entries.Add(new BindingManifest.Entry { key = r.suggestedKey, target = null });
                added++;
            }

            EditorUtility.SetDirty(manifest);
            Debug.Log($"[TimelineSmash] Added {added} binding key(s) to '{manifest.name}'. " +
                      "Assign their targets in the manifest.");
        }
    }
}
