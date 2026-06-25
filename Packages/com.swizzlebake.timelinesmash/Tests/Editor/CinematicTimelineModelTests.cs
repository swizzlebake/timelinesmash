using System.Linq;
using NUnit.Framework;
using TimelineSmash.Editor;

namespace TimelineSmash.Tests
{
    public class CinematicTimelineModelTests
    {
        [TearDown]
        public void TearDown() => TestAssets.Cleanup();

        [Test]
        public void Build_ListsDirectSegments_WithOwningSetAndSortedLanes()
        {
            var a = TestAssets.CreateSubTimeline("A");
            var b = TestAssets.CreateSubTimeline("B");
            var alice = TestAssets.CreateContributor("Alice", new[]
            {
                TestAssets.Seg(a, "Characters", 0, 5),
                TestAssets.Seg(b, "Camera", 6, 4),
            });
            var bob = TestAssets.CreateContributor("Bob", new[] { TestAssets.Seg(a, "Characters", 5, 2) });
            var comp = TestAssets.CreateComposition("Cine", null, alice, bob);

            var model = CinematicTimelineModel.Build(comp);

            Assert.AreEqual(3, model.items.Count);
            CollectionAssert.AreEqual(new[] { "Camera", "Characters" }, model.lanes); // sorted ordinal
            Assert.AreEqual(10, model.totalDuration, 1e-6);                            // Camera 6..10

            var cameraItem = model.items.Single(i => i.lane == "Camera");
            Assert.AreSame(alice, cameraItem.set, "Items keep their owning set for editing/Undo.");
            Assert.AreEqual("Alice", cameraItem.owner);
        }

        [Test]
        public void Build_NullComposition_IsEmpty()
        {
            var model = CinematicTimelineModel.Build(null);
            Assert.AreEqual(0, model.items.Count);
            Assert.AreEqual(0, model.lanes.Count);
        }

        [Test]
        public void Snap_RoundsToFrame_AndClampsToZero()
        {
            Assert.AreEqual(31.0 / 30.0, TimelineSnap.Snap(1.04, 30, true), 1e-9); // round(31.2)=31
            Assert.AreEqual(1.04, TimelineSnap.Snap(1.04, 30, false), 1e-9);       // disabled = passthrough
            Assert.AreEqual(0.0, TimelineSnap.Snap(-0.5, 30, true), 1e-9);         // clamped to 0
        }
    }
}
