using System.Linq;
using NUnit.Framework;
using TimelineSmash.Editor;
using UnityEngine.Timeline;

namespace TimelineSmash.Tests
{
    public class OverviewModelTests
    {
        TimelineAsset _a, _b;

        [SetUp]
        public void SetUp()
        {
            _a = TestAssets.CreateSubTimeline("A");
            _b = TestAssets.CreateSubTimeline("B");
        }

        [TearDown]
        public void TearDown() => TestAssets.Cleanup();

        [Test]
        public void Build_GroupsSegmentsIntoLanes()
        {
            var c = TestAssets.CreateContributor("Alice", new[]
            {
                TestAssets.Seg(_a, "Camera", 0, 5),
                TestAssets.Seg(_b, "Audio", 0, 5),
            });
            var model = CinematicOverviewModel.Build(TestAssets.CreateComposition("Cine", null, c));

            Assert.AreEqual(2, model.lanes.Count);
            CollectionAssert.AreEquivalent(new[] { "Camera", "Audio" }, model.lanes.Select(l => l.name));
        }

        [Test]
        public void Build_DetectsOverlapWithinLane()
        {
            var c = TestAssets.CreateContributor("Alice", new[]
            {
                TestAssets.Seg(_a, "Camera", 0, 5),
                TestAssets.Seg(_b, "Camera", 3, 5), // overlaps 0..5
            });
            var model = CinematicOverviewModel.Build(TestAssets.CreateComposition("Cine", null, c));

            var camera = model.lanes.Single(l => l.name == "Camera");
            Assert.AreEqual(1, camera.overlaps.Count);
            Assert.IsTrue(model.warnings.Any(w => w.Contains("overlaps")));
        }

        [Test]
        public void Build_NoOverlapAcrossDifferentLanes()
        {
            var c = TestAssets.CreateContributor("Alice", new[]
            {
                TestAssets.Seg(_a, "Camera", 0, 5),
                TestAssets.Seg(_b, "Audio", 0, 5), // same time, different lane
            });
            var model = CinematicOverviewModel.Build(TestAssets.CreateComposition("Cine", null, c));

            Assert.IsTrue(model.lanes.All(l => l.overlaps.Count == 0));
        }

        [Test]
        public void Build_DetectsGapBetweenSegments()
        {
            var c = TestAssets.CreateContributor("Alice", new[]
            {
                TestAssets.Seg(_a, "Camera", 0, 5),
                TestAssets.Seg(_b, "Camera", 8, 2), // gap 5..8
            });
            var model = CinematicOverviewModel.Build(TestAssets.CreateComposition("Cine", null, c));

            var camera = model.lanes.Single(l => l.name == "Camera");
            Assert.AreEqual(1, camera.gaps.Count);
            Assert.AreEqual(5, camera.gaps[0].from, 1e-6);
            Assert.AreEqual(8, camera.gaps[0].to, 1e-6);
        }

        [Test]
        public void Build_TotalDurationIsLatestEnd()
        {
            var c = TestAssets.CreateContributor("Alice", new[]
            {
                TestAssets.Seg(_a, "Camera", 0, 5),
                TestAssets.Seg(_b, "Audio", 10, 7), // ends at 17
            });
            var model = CinematicOverviewModel.Build(TestAssets.CreateComposition("Cine", null, c));

            Assert.AreEqual(17, model.totalDuration, 1e-6);
        }

        [Test]
        public void Build_NullComposition_IsEmpty()
        {
            var model = CinematicOverviewModel.Build(null);
            Assert.AreEqual(0, model.lanes.Count);
            Assert.AreEqual(0, model.totalDuration, 1e-6);
        }
    }
}
