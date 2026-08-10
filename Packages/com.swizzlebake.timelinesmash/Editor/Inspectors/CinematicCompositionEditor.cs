using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.SceneManagement;
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
                    if (GUILayout.Button(new GUIContent("Add contributor",
                        "Create a ContributorSegmentSet asset next to this composition, append it to " +
                        "Contributors and select it. One set per artist keeps files one-owner and merge-safe.")))
                    {
                        var set = CinematicScaffold.AddContributor(comp, "New Artist");
                        if (set != null)
                        {
                            Selection.activeObject = set;
                            EditorGUIUtility.PingObject(set);
                        }
                    }
                    if (GUILayout.Button(new GUIContent("Open visual timeline",
                        "Open the Cinematic Timeline window: a lane-per-contributor view of this " +
                        "composition where segments can be inspected and rearranged.")))
                        CinematicTimelineWindow.Open(comp);
                }
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Assemble", EditorStyles.boldLabel);

                DrawStageSource(comp);

                if (GUILayout.Button(new GUIContent("Assemble (master + stage)",
                        "Regenerate the master timeline and the stage scene in the output folder. " +
                        "The stage hosts the master plus per-segment directors, ready to play or record."),
                        GUILayout.Height(26)))
                    Report(CinematicAssembleService.Assemble(comp, true));

                if (GUILayout.Button(new GUIContent("Assemble into active scene",
                        "Regenerate the master timeline and host it in the currently open scene instead " +
                        "of a generated stage, binding tracks to this scene's actors by name.")))
                    Report(CinematicAssembleService.AssembleIntoActiveScene(comp));

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("Master only",
                        "Regenerate just the master timeline asset, leaving any existing stage scene untouched.")))
                        Report(CinematicAssembleService.Assemble(comp, false));
                    if (GUILayout.Button(new GUIContent("Open Master",
                        "Show the generated master timeline in the Timeline window. Assemble first if it " +
                        "does not exist yet.")))
                        OpenMaster(comp);
                    if (GUILayout.Button(new GUIContent("Open Stage",
                        "Open the generated stage scene (prompting to save current changes). Assemble " +
                        "(master + stage) first if it does not exist yet.")))
                        OpenStage(comp);
                }

                if (RecorderBridge.Available)
                {
                    if (GUILayout.Button(new GUIContent("Record cinematic",
                        "Open the generated stage scene, enter Play Mode and capture the cinematic using " +
                        "the Capture settings above (image sequence and/or video via Unity Recorder). " +
                        "Requires Assemble (master + stage) first.")))
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

        // Stage source: a scene and/or prefab the generated stage is populated from, so it ships real
        // actors and can be played/recorded on its own (otherwise it holds only directors). The scene
        // reference is stored as a GUID on the composition; the prefab is a direct GameObject reference.
        // Both composition fields are [HideInInspector] so they're drawn here as proper object pickers.
        void DrawStageSource(CinematicComposition comp)
        {
            var scenePath = AssetDatabase.GUIDToAssetPath(comp.stageSourceSceneGuid ?? "");
            var sceneAsset = string.IsNullOrEmpty(scenePath)
                ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);

            EditorGUI.BeginChangeCheck();
            var newScene = (SceneAsset)EditorGUILayout.ObjectField(
                new GUIContent("Stage source scene",
                    "Optional. Cloned as the base of the generated stage so it ships real actors, lighting " +
                    "and camera. Bindings resolve against its objects by name."),
                sceneAsset, typeof(SceneAsset), false);
            var newPrefab = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent("Stage actor prefab",
                    "Optional. Instantiated at the stage root. Bindings resolve against it by name. " +
                    "Combines with the source scene if both are set."),
                comp.stageActorPrefab, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(comp, "Set stage source");
                comp.stageSourceSceneGuid = newScene != null
                    ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(newScene)) : "";
                comp.stageActorPrefab = newPrefab;
                EditorUtility.SetDirty(comp);
            }

            if (sceneAsset == null && comp.stageActorPrefab == null)
                EditorGUILayout.HelpBox(
                    "Set a source scene or actor prefab to populate the generated stage with real actors so " +
                    "it can be played and recorded. Without one, the stage holds only directors — it records " +
                    "nothing unless every segment spawns a prefab.",
                    MessageType.None);
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
            var activeScene = SceneManager.GetActiveScene();
            var plan = BindingPlan.Build(comp, activeScene);
            string activeSceneName = activeScene.IsValid() && !string.IsNullOrEmpty(activeScene.name)
                ? activeScene.name
                : "Untitled";

            EditorGUILayout.Space();
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(
                    plan.Total == 0
                        ? "Bindings — no bindable tracks"
                        : $"Bindings — {plan.Bound} of {plan.Total} resolvable",
                    EditorStyles.boldLabel);

                if (plan.SceneBound > 0)
                    EditorGUILayout.LabelField(
                        $"{plan.SceneBound} resolved by name in active scene '{activeSceneName}'",
                        EditorStyles.miniLabel);

                if (comp.bindingManifest == null)
                {
                    EditorGUILayout.HelpBox(
                        plan.SceneBound > 0
                            ? "No binding manifest assigned. Green scene-name matches work when assembling " +
                              "into this scene; create a manifest if the bindings must travel with another stage."
                            : "No binding manifest assigned. Create one to map track names to shared scene actors.",
                        MessageType.Info);
                    if (GUILayout.Button(new GUIContent("Create & assign manifest",
                        "Create a BindingManifest asset next to this composition and assign it to the " +
                        "Binding Manifest field, ready to map track keys to shared scene actors.")))
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
                            r.Resolved ? r.resolvedBySceneName
                                           ? $"→ {TargetName(r.target)}  active scene name '{r.resolvedKey}'"
                                           : $"→ {TargetName(r.target)}  manifest key '{r.resolvedKey}'"
                                       : $"needs key '{r.suggestedKey}'",
                            EditorStyles.miniLabel);
                    }
                }

                int toAdd = comp.bindingManifest == null ? 0 : plan.requirements
                    .Where(r => !r.Resolved).Select(r => r.suggestedKey).Distinct()
                    .Count(k => !existingKeys.Contains(k));

                if (toAdd > 0 && GUILayout.Button(new GUIContent($"Add {toAdd} missing key(s) to manifest",
                        "Append the unresolved keys listed above to the binding manifest with empty " +
                        "targets, so they only need their scene actors assigned.")))
                    AddMissingKeys(comp, plan);

                EditorGUILayout.HelpBox(
                    "Green checks identify either a manifest target or a compatible object found by name in " +
                    $"the active scene '{activeSceneName}'. Active-scene matches apply when using Assemble " +
                    "Into Active Scene, or when the generated stage contains the same named actors. To reuse " +
                    "one sub-timeline for different actors, set a per-segment Binding Key and add manifest " +
                    "keys like '<key>/<trackName>'.",
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
