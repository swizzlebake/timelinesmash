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
        /// <summary>Create a fresh single scene, populate it, and save it to <paramref name="scenePath"/>.</summary>
        public static StageBuildResult BuildStage(AssembleResult result, CompiledBindings bindings, string scenePath)
        {
            EditorAssetUtil.EnsureFolder(Path.GetDirectoryName(scenePath));

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var build = Populate(scene, result, bindings);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenePath);
            return build;
        }

        /// <summary>Populate an existing scene with the master + host directors (no file I/O).
        /// Used by <see cref="BuildStage"/> and directly by tests against an additive scene.</summary>
        public static StageBuildResult Populate(Scene scene, AssembleResult result, CompiledBindings bindings)
        {
            var build = new StageBuildResult();

            var masterGO = new GameObject("Cinematic_Master");
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
                BindingApplier.Apply(hostDir, entry.segment, bindings, result.warnings);
            }

            return build;
        }
    }
}
