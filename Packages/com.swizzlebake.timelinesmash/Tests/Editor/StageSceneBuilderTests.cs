using System.Linq;
using NUnit.Framework;
using TimelineSmash.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Timeline;

namespace TimelineSmash.Tests
{
    public class StageSceneBuilderTests
    {
        const string MasterPath = TestAssets.Generated + "/Cine_Master.playable";
        const string StagePath = TestAssets.Generated + "/Cine_Stage.unity";
        TimelineAsset _a, _b;

        [SetUp]
        public void SetUp()
        {
            _a = TestAssets.CreateSubTimeline("ShotA");
            _b = TestAssets.CreateSubTimeline("ShotB");
        }

        [TearDown]
        public void TearDown()
        {
            // Detach from the saved stage scene so cleanup can delete it without touching an open scene.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            TestAssets.Cleanup();
        }

        AssembleResult AssembleTwoSegments()
        {
            var alice = TestAssets.CreateContributor("Alice", new[] { TestAssets.Seg(_a, "Characters", 0, 5) });
            var bob = TestAssets.CreateContributor("Bob", new[] { TestAssets.Seg(_b, "Camera", 0, 5) });
            var comp = TestAssets.CreateComposition("Cine", null, alice, bob);
            return CinematicAssembler.BuildMaster(comp, MasterPath);
        }

        [Test]
        public void BuildStage_CreatesOneHostPerSegment()
        {
            var result = AssembleTwoSegments();
            var build = StageSceneBuilder.BuildStage(result, null, StagePath);

            Assert.AreEqual(result.entries.Count, build.hosts.Count);
            Assert.IsNotNull(build.masterDir);
            Assert.IsNotNull(build.masterDir.playableAsset);
            Assert.AreEqual(MasterPath, AssetDatabase.GetAssetPath(build.masterDir.playableAsset));
        }

        [Test]
        public void BuildStage_HostsReferenceTheirSubTimelines()
        {
            var result = AssembleTwoSegments();
            var build = StageSceneBuilder.BuildStage(result, null, StagePath);

            var hostAssets = build.hosts.Select(h => h.playableAsset).ToList();
            CollectionAssert.Contains(hostAssets, _a);
            CollectionAssert.Contains(hostAssets, _b);
        }

        [Test]
        public void BuildStage_ResolvesExposedReferencesToHosts()
        {
            var result = AssembleTwoSegments();
            var build = StageSceneBuilder.BuildStage(result, null, StagePath);

            foreach (var entry in result.entries)
            {
                var referenced = build.masterDir.GetReferenceValue(entry.exposedName, out bool valid);
                Assert.IsTrue(valid, $"Exposed name '{entry.exposedName}' did not resolve.");
                Assert.IsNotNull(referenced);
                Assert.IsInstanceOf<GameObject>(referenced);
            }
        }

        [Test]
        public void BuildStage_WritesSceneToDisk()
        {
            var result = AssembleTwoSegments();
            StageSceneBuilder.BuildStage(result, null, StagePath);
            Assert.IsTrue(System.IO.File.Exists(StagePath));
        }

        // One contributor whose sub-timeline has a single AnimationTrack named after the actor, so the
        // track's binding key is the actor name and resolves by-name against a populated stage.
        AssembleResult AssembleNamedActor(string actorName)
        {
            var sub = TestAssets.CreateSubTimeline("Shot", actorName);
            var owner = TestAssets.CreateContributor("Alice", new[] { TestAssets.Seg(sub, "Characters", 0, 5) });
            var comp = TestAssets.CreateComposition("Cine", null, owner);
            return CinematicAssembler.BuildMaster(comp, MasterPath);
        }

        [Test]
        public void BuildStage_WithActorPrefab_InstantiatesActorAndBindsByName()
        {
            var result = AssembleNamedActor("Alice");
            var actor = TestAssets.CreatePrefab("Alice", withAnimator: true);

            var build = StageSceneBuilder.BuildStage(result, null, StagePath, null, actor);

            // The prefab was instantiated into the generated stage scene…
            var names = build.masterDir.gameObject.scene.GetRootGameObjects().Select(g => g.name).ToList();
            CollectionAssert.Contains(names, "Alice");

            // …and the segment's track bound to its Animator by name, with no manifest.
            var liveSub = build.hosts[0].playableAsset as TimelineAsset;
            Assert.IsNotNull(liveSub);
            var track = liveSub.GetOutputTracks().First();
            var bound = build.hosts[0].GetGenericBinding(track);
            Assert.IsInstanceOf<Animator>(bound);
            Assert.AreEqual("Alice", ((Animator)bound).gameObject.name);
        }

        [Test]
        public void BuildStage_WithActorPrefab_RemapManifestPrefabTargetToSceneInstance()
        {
            // The track key deliberately differs from the actor name, so only the manifest can resolve it.
            var result = AssembleNamedActor("hero/Body");
            var actor = TestAssets.CreatePrefab("Alice", withAnimator: true);
            var prefabAnimator = actor.GetComponent<Animator>();
            var manifest = ScriptableObject.CreateInstance<BindingManifest>();
            manifest.entries.Add(new BindingManifest.Entry { key = "hero/Body", target = prefabAnimator });

            var build = StageSceneBuilder.BuildStage(result, BindingCompiler.Compile(manifest), StagePath, null, actor);

            var liveSub = build.hosts[0].playableAsset as TimelineAsset;
            Assert.IsNotNull(liveSub);
            var track = liveSub.GetOutputTracks().First();
            var bound = build.hosts[0].GetGenericBinding(track) as Animator;

            Assert.IsNotNull(bound);
            Assert.AreNotSame(prefabAnimator, bound, "The host must not bind to the prefab asset component.");
            Assert.AreEqual("Alice", bound.gameObject.name);
            Assert.AreEqual(StagePath, bound.gameObject.scene.path);

            Object.DestroyImmediate(manifest);
        }

        [Test]
        public void BuildStage_WithSourceScene_ClonesActorsIntoStageAndBindsByName()
        {
            // A source scene containing a named actor with an Animator, saved under the test root.
            var src = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Alice").AddComponent<Animator>();
            var srcPath = TestAssets.Root + "/Source.unity";
            EditorSceneManager.SaveScene(src, srcPath);

            var result = AssembleNamedActor("Alice");

            var build = StageSceneBuilder.BuildStage(result, null, StagePath, srcPath, null);

            // Saved to the stage path (the source is left untouched) and carries the cloned actor.
            Assert.AreEqual(StagePath, build.masterDir.gameObject.scene.path);
            var names = build.masterDir.gameObject.scene.GetRootGameObjects().Select(g => g.name).ToList();
            CollectionAssert.Contains(names, "Alice");
            CollectionAssert.Contains(names, "Cinematic_Master");

            var liveSub = build.hosts[0].playableAsset as TimelineAsset;
            Assert.IsNotNull(liveSub);
            var track = liveSub.GetOutputTracks().First();
            Assert.IsInstanceOf<Animator>(build.hosts[0].GetGenericBinding(track));
        }

        [Test]
        public void BuildStage_NoSource_LeavesStageActorsAbsent()
        {
            var result = AssembleNamedActor("Alice");

            var build = StageSceneBuilder.BuildStage(result, null, StagePath);

            // Director-only stage: just the master root, no actor named "Alice".
            var names = build.masterDir.gameObject.scene.GetRootGameObjects().Select(g => g.name).ToList();
            CollectionAssert.DoesNotContain(names, "Alice");
        }
    }
}
