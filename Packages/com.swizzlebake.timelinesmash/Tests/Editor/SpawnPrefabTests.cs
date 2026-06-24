using System.Linq;
using NUnit.Framework;
using TimelineSmash.Editor;
using UnityEngine.Timeline;

namespace TimelineSmash.Tests
{
    public class SpawnPrefabTests
    {
        const string MasterPath = TestAssets.Generated + "/Cine_Master.playable";

        [TearDown]
        public void TearDown() => TestAssets.Cleanup();

        [Test]
        public void BuildMaster_SpawnPrefab_AddsParallelSpawnClip()
        {
            var sub = TestAssets.CreateSubTimeline("Shot");
            var prefab = TestAssets.CreatePrefab("Burst");
            var seg = TestAssets.Seg(sub, "Characters", 0, 5);
            seg.spawnPrefab = prefab;
            var alice = TestAssets.CreateContributor("Alice", new[] { seg });
            var comp = TestAssets.CreateComposition("Cine", null, alice);

            var result = CinematicAssembler.BuildMaster(comp, MasterPath);

            var spawnTrack = result.master.GetOutputTracks().FirstOrDefault(t => t.name.StartsWith("Spawn:"));
            Assert.IsNotNull(spawnTrack, "Expected a parallel spawn control track.");

            var clip = spawnTrack.GetClips().First();
            var control = (ControlPlayableAsset)clip.asset;
            Assert.AreSame(prefab, control.prefabGameObject, "Spawn clip should reference the prefab.");
            Assert.AreEqual(0, clip.start, 1e-6);
            Assert.AreEqual(5, clip.duration, 1e-6);

            // The host-driving clip on the original lane is untouched (still drives the sub-timeline).
            var laneTrack = result.master.GetOutputTracks().First(t => t.name == "Characters");
            var host = (ControlPlayableAsset)laneTrack.GetClips().First().asset;
            Assert.IsNull(host.prefabGameObject);
            Assert.IsTrue(host.updateDirector);
        }

        [Test]
        public void BuildMaster_NoSpawnPrefab_NoSpawnTrack()
        {
            var sub = TestAssets.CreateSubTimeline("Shot");
            var alice = TestAssets.CreateContributor("Alice", new[] { TestAssets.Seg(sub, "Characters", 0, 5) });
            var comp = TestAssets.CreateComposition("Cine", null, alice);

            var result = CinematicAssembler.BuildMaster(comp, MasterPath);

            Assert.IsFalse(result.master.GetOutputTracks().Any(t => t.name.StartsWith("Spawn:")));
        }
    }
}
