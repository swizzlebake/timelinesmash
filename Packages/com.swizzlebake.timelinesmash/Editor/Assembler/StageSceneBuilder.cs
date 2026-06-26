using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

namespace TimelineSmash.Editor
{
    /// <summary>Result of populating a stage scene — the live directors, for inspection/testing.</summary>
    public class StageBuildResult
    {
        public PlayableDirector masterDir;
        public readonly List<PlayableDirector> hosts = new List<PlayableDirector>();
    }

    /// <summary>
    /// Builds the regenerable stage scene for a cinematic: a master <see cref="PlayableDirector"/>
    /// bound to the generated master timeline, plus one host director per segment. Host directors and
    /// bindings live only here, keeping the hand-edited working scene free of merge-prone wiring.
    /// </summary>
    public static class StageSceneBuilder
    {
        /// <summary>Create the stage scene, populate it with the master + host directors, and save it to
        /// <paramref name="scenePath"/>. By default the scene starts empty (directors only). Pass
        /// <paramref name="sourceScenePath"/> to clone an existing scene as the stage's base (shipping its
        /// actors, lighting and camera) and/or <paramref name="actorPrefab"/> to instantiate a prefab at the
        /// stage root — either makes the stage self-contained and recordable. When actors are brought in this
        /// way, bindings resolve against them by name (a manifest entry still wins; see
        /// <see cref="BindingApplier"/>).</summary>
        public static StageBuildResult BuildStage(AssembleResult result, CompiledBindings bindings, string scenePath,
            string sourceScenePath = null, GameObject actorPrefab = null)
        {
            EditorAssetUtil.EnsureFolder(Path.GetDirectoryName(scenePath));

            // Use a source scene as the stage's base when one is set (and isn't the stage itself). We open the
            // source and later save it *to the stage path* (a Save As) — the source file on disk is never
            // touched, and the generated stage stays fully regenerable.
            bool cloneScene = !string.IsNullOrEmpty(sourceScenePath)
                              && sourceScenePath != scenePath
                              && File.Exists(sourceScenePath);

            Scene scene = cloneScene
                ? EditorSceneManager.OpenScene(sourceScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            if (actorPrefab != null)
                PrefabUtility.InstantiatePrefab(actorPrefab, scene);

            // With real actors present, let unmatched binding keys fall back to scene GameObjects by name.
            bool haveActors = cloneScene || actorPrefab != null;
            var build = Populate(scene, result, bindings, resolveBySceneName: haveActors);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenePath); // Save As to the stage path; source left untouched
            return build;
        }

        const string MasterRootName = "Cinematic_Master";

        /// <summary>Populate an existing scene with the master + host directors (no file I/O).
        /// Used by <see cref="BuildStage"/>, and directly when assembling into a live, actor-populated
        /// scene. Any previous master root is removed first, so re-assembling never stacks duplicates.
        /// When <paramref name="resolveBySceneName"/> is set, binding keys the manifest does not resolve
        /// fall back to scene GameObjects of that name (see <see cref="BindingApplier"/>).</summary>
        public static StageBuildResult Populate(Scene scene, AssembleResult result, CompiledBindings bindings,
            bool resolveBySceneName = false)
        {
            // Idempotency: drop a master left by a previous assemble into this same scene.
            if (scene.IsValid())
            {
                foreach (var root in scene.GetRootGameObjects())
                    if (root.name == MasterRootName)
                        Object.DestroyImmediate(root);
            }

            var build = new StageBuildResult();

            var masterGO = new GameObject(MasterRootName);
            if (scene.IsValid())
                SceneManager.MoveGameObjectToScene(masterGO, scene);

            var masterDir = masterGO.AddComponent<PlayableDirector>();
            // Reload the master by path: the caller's C# reference can go stale (fake-null) after the
            // scene operations here trigger an asset reload. A path lookup always yields the live asset.
            var master = AssetDatabase.LoadAssetAtPath<TimelineAsset>(result.masterPath) ?? result.master;
            masterDir.playableAsset = master;
            masterDir.playOnAwake = true;
            build.masterDir = masterDir;

            foreach (var entry in result.entries)
            {
                var hostGO = new GameObject($"Host_{entry.laneName}_{entry.globalIndex:D4}");
                hostGO.transform.SetParent(masterGO.transform);

                var hostDir = hostGO.AddComponent<PlayableDirector>();
                hostDir.playableAsset = entry.segment.subTimeline;
                hostDir.playOnAwake = false;
                build.hosts.Add(hostDir);

                // Wire the master Control clip's exposed reference to this host.
                masterDir.SetReferenceValue(entry.exposedName, hostGO);

                // Bindings resolve on the host director (where the nested timeline plays).
                BindingApplier.Apply(hostDir, entry.segment, bindings, result.warnings, resolveBySceneName);
            }

            return build;
        }
    }
}
