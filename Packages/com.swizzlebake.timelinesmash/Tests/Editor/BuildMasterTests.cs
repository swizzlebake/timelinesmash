using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TimelineSmash.Editor;
using UnityEditor;
using UnityEngine.Timeline;

namespace TimelineSmash.Tests
{
    public class BuildMasterTests
    {
        const string MasterPath = TestAssets.Generated + "/Cine_Master.playable";
        TimelineAsset _a, _b;

        [SetUp]
        public void SetUp()
        {
            _a = TestAssets.CreateSubTimeline("ShotA");
            _b = TestAssets.CreateSubTimeline("ShotB");
        }

        [TearDown]
        public void TearDown() => TestAssets.Cleanup();

        CinematicComposition TwoLaneComposition()
        {
            var alice = TestAssets.CreateContributor("Alice", new[]
            {
                TestAssets.Seg(_a, "Characters", 0, 5),
            });
            var bob = TestAssets.CreateContributor("Bob", new[]
            {
                TestAssets.Seg(_b, "Camera", 0, 5),
                TestAssets.Seg(_a, "Camera", 6, 4),
            });
            return TestAssets.CreateComposition("Cine", null, alice, bob);
        }

        [Test]
        public void BuildMaster_CreatesOneTrackPerLane()
        {
            var result = CinematicAssembler.BuildMaster(TwoLaneComposition(), MasterPath);

            var tracks = result.master.GetOutputTracks().ToList();
            var names = tracks.Select(t => t.name).OrderBy(n => n).ToList();

            CollectionAssert.AreEqual(new[] { "Camera", "Characters" }, names);
            Assert.IsTrue(tracks.All(t => t is ControlTrack));
        }

        [Test]
        public void BuildMaster_CreatesOneClipPerSegment()
        {
            var result = CinematicAssembler.BuildMaster(TwoLaneComposition(), MasterPath);
            Assert.AreEqual(3, result.entries.Count);

            int clipCount = result.master.GetOutputTracks().Sum(t => t.GetClips().Count());
            Assert.AreEqual(3, clipCount);
        }

        [Test]
        public void BuildMaster_MapsClipTiming()
        {
            var alice = TestAssets.CreateContributor("Alice", new[]
            {
                TestAssets.Seg(_a, "Main", 3, 7, clipIn: 1.5, speed: 2),
            });
            var comp = TestAssets.CreateComposition("Cine", null, alice);

            var result = CinematicAssembler.BuildMaster(comp, MasterPath);
            var clip = result.entries[0].clip;

            Assert.AreEqual(3, clip.start, 1e-6);
            Assert.AreEqual(7, clip.duration, 1e-6);
            Assert.AreEqual(1.5, clip.clipIn, 1e-6);
            Assert.AreEqual(2, clip.timeScale, 1e-6);
        }

        [Test]
        public void BuildMaster_ConfiguresControlAssetForNestedDirector()
        {
            var result = CinematicAssembler.BuildMaster(TwoLaneComposition(), MasterPath);

            foreach (var entry in result.entries)
            {
                Assert.IsNotNull(entry.control);
                Assert.IsTrue(entry.control.updateDirector);
                Assert.IsFalse(entry.control.active);
                Assert.IsFalse(entry.control.searchHierarchy);
                Assert.IsFalse(string.IsNullOrEmpty(entry.exposedName));
            }

            // Exposed names are unique.
            var names = result.entries.Select(e => e.exposedName).ToList();
            CollectionAssert.AllItemsAreUnique(names);
        }

        [Test]
        public void BuildMaster_SetsFrameRate()
        {
            var comp = TwoLaneComposition();
            comp.settings.frameRate = 24;
            EditorUtility.SetDirty(comp);
            AssetDatabase.SaveAssets(); // persist so an asset reload can't revert the in-memory mutation

            var result = CinematicAssembler.BuildMaster(comp, MasterPath);
            Assert.AreEqual(24, result.master.editorSettings.frameRate, 1e-6);
        }

        [Test]
        public void BuildMaster_TotalDuration_AutoFromLatestEnd()
        {
            var result = CinematicAssembler.BuildMaster(TwoLaneComposition(), MasterPath);
            // Latest segment: Camera 6..10
            Assert.AreEqual(10, result.totalDuration, 1e-6);
        }

        [Test]
        public void BuildMaster_TotalDuration_RespectsExplicitOverride()
        {
            var comp = TwoLaneComposition();
            comp.settings.totalDuration = 99;
            EditorUtility.SetDirty(comp);
            AssetDatabase.SaveAssets(); // persist so an asset reload can't revert the in-memory mutation

            var result = CinematicAssembler.BuildMaster(comp, MasterPath);
            Assert.AreEqual(99, result.totalDuration, 1e-6);
        }

        [Test]
        public void BuildMaster_EmptyComposition_ProducesNoTracks()
        {
            var comp = TestAssets.CreateComposition("Cine", null);

            var result = CinematicAssembler.BuildMaster(comp, MasterPath);

            Assert.IsNotNull(result.master);
            Assert.AreEqual(0, result.entries.Count);
            Assert.AreEqual(0, result.master.GetOutputTracks().Count());
        }

        [Test]
        public void BuildMaster_NullSubTimeline_WarnsAndSkips()
        {
            var alice = TestAssets.CreateContributor("Alice", new[]
            {
                TestAssets.Seg(_a, "Main", 0, 5),
                TestAssets.Seg(null, "Main", 6, 5),
            });
            var comp = TestAssets.CreateComposition("Cine", null, alice);

            var result = CinematicAssembler.BuildMaster(comp, MasterPath);

            Assert.AreEqual(1, result.entries.Count);
            Assert.AreEqual(1, result.warnings.Count);
        }

        [Test]
        public void BuildMaster_OverlappingSegmentsOnSameLane_Warns()
        {
            var alice = TestAssets.CreateContributor("Alice", new[]
            {
                TestAssets.Seg(_a, "Main", 0, 5),
            });
            var bob = TestAssets.CreateContributor("Bob", new[]
            {
                TestAssets.Seg(_b, "Main", 3, 5), // starts before Alice's ends → overlap on lane "Main"
            });
            var comp = TestAssets.CreateComposition("Cine", null, alice, bob);

            var result = CinematicAssembler.BuildMaster(comp, MasterPath);

            Assert.AreEqual(2, result.entries.Count);
            Assert.IsTrue(result.warnings.Any(w => w.Contains("overlaps")),
                "Expected an overlap warning; got: " + string.Join(" | ", result.warnings));
        }

        [Test]
        public void BuildMaster_TouchingSegmentsOnSameLane_DoNotWarn()
        {
            var alice = TestAssets.CreateContributor("Alice", new[]
            {
                TestAssets.Seg(_a, "Main", 0, 5),
                TestAssets.Seg(_b, "Main", 5, 5), // starts exactly where the previous ends → no overlap
            });
            var comp = TestAssets.CreateComposition("Cine", null, alice);

            var result = CinematicAssembler.BuildMaster(comp, MasterPath);

            Assert.IsFalse(result.warnings.Any(w => w.Contains("overlaps")),
                "Touching (end == next start) must not count as an overlap.");
        }

        [Test]
        public void BuildMaster_DifferentLanes_DoNotWarnOnOverlap()
        {
            // Same times, different lanes → no conflict.
            var result = CinematicAssembler.BuildMaster(TwoLaneComposition(), MasterPath);
            Assert.IsFalse(result.warnings.Any(w => w.Contains("overlaps")));
        }

        [Test]
        public void BuildMaster_UnsetDuration_AutoFillsFromSubTimelineLength()
        {
            var animated = TestAssets.CreateAnimatedSubTimeline("Shot"); // one 0..1 clip → duration ~1
            double expected = animated.duration; // capture before BuildMaster (SaveAssets can fake-null it)
            Assume.That(expected, Is.GreaterThan(0));

            var alice = TestAssets.CreateContributor("Alice", new[]
            {
                TestAssets.Seg(animated, "Main", 0, 0), // duration 0 → "play the whole shot"
            });
            var comp = TestAssets.CreateComposition("Cine", null, alice);

            var result = CinematicAssembler.BuildMaster(comp, MasterPath);

            Assert.AreEqual(1, result.entries.Count);
            Assert.AreEqual(expected, result.entries[0].clip.duration, 1e-6);
        }

        [Test]
        public void BuildMaster_ExplicitDuration_OverridesSubTimelineLength()
        {
            var animated = TestAssets.CreateAnimatedSubTimeline("Shot"); // natural length ~1
            var alice = TestAssets.CreateContributor("Alice", new[]
            {
                TestAssets.Seg(animated, "Main", 0, 4), // explicit 4 wins over the auto length
            });
            var comp = TestAssets.CreateComposition("Cine", null, alice);

            var result = CinematicAssembler.BuildMaster(comp, MasterPath);

            Assert.AreEqual(4, result.entries[0].clip.duration, 1e-6);
        }

        [Test]
        public void BuildMaster_IsDeterministic()
        {
            var comp = TwoLaneComposition();

            var snapshot1 = Snapshot(CinematicAssembler.BuildMaster(comp, MasterPath));
            var snapshot2 = Snapshot(CinematicAssembler.BuildMaster(comp, MasterPath));

            CollectionAssert.AreEqual(snapshot1, snapshot2);
        }

        [Test]
        public void BuildMaster_WritesAssetToDisk()
        {
            CinematicAssembler.BuildMaster(TwoLaneComposition(), MasterPath);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<TimelineAsset>(MasterPath));
        }

        // Order-independent structural fingerprint for determinism comparison.
        static List<string> Snapshot(AssembleResult r)
        {
            return r.entries
                .Select(e => $"{e.laneName}|{e.clip.start}|{e.clip.duration}|{e.clip.clipIn}|" +
                             $"{e.clip.timeScale}|{e.exposedName}|{e.clip.displayName}")
                .ToList();
        }
    }
}
