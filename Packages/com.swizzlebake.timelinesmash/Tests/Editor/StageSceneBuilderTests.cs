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
    }
}
