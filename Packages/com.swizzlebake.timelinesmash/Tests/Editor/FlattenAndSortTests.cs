using System.Linq;
using NUnit.Framework;
using TimelineSmash.Editor;
using UnityEngine.Timeline;

namespace TimelineSmash.Tests
{
    public class FlattenAndSortTests
    {
        TimelineAsset _a, _b, _c;

        [SetUp]
        public void SetUp()
        {
            _a = TestAssets.CreateSubTimeline("A");
            _b = TestAssets.CreateSubTimeline("B");
            _c = TestAssets.CreateSubTimeline("C");
        }

        [TearDown]
        public void TearDown() => TestAssets.Cleanup();

        [Test]
        public void FlattenTree_SortsByLaneThenStart()
        {
            var alice = TestAssets.CreateContributor("Alice", new[]
            {
                TestAssets.Seg(_c, "Camera", 10, 5),
                TestAssets.Seg(_a, "Camera", 0, 5),
            });
            var bob = TestAssets.CreateContributor("Bob", new[] { TestAssets.Seg(_b, "Audio", 2, 5) });
            var comp = TestAssets.CreateComposition("Cine", null, alice, bob);

            var order = CinematicAssembler.FlattenTree(comp).Select(l => $"{l.lane}:{l.start}").ToList();
            CollectionAssert.AreEqual(new[] { "Audio:2", "Camera:0", "Camera:10" }, order);
        }

        [Test]
        public void FlattenTree_IsDeterministicAcrossCalls()
        {
            var alice = TestAssets.CreateContributor("Alice", new[]
            {
                TestAssets.Seg(_a, "Main", 5, 5),
                TestAssets.Seg(_b, "Main", 5, 5), // tie on lane+start -> broken deterministically
            });
            var comp = TestAssets.CreateComposition("Cine", null, alice);

            var first = CinematicAssembler.FlattenTree(comp).Select(Key).ToList();
            var second = CinematicAssembler.FlattenTree(comp).Select(Key).ToList();
            CollectionAssert.AreEqual(first, second);
        }

        [Test]
        public void FlattenTree_SkipsNullContributorSets()
        {
            var alice = TestAssets.CreateContributor("Alice", new[] { TestAssets.Seg(_a, "Main", 0, 5) });
            var comp = TestAssets.CreateComposition("Cine", null, alice, null);

            Assert.AreEqual(1, CinematicAssembler.FlattenTree(comp).Count);
        }

        [Test]
        public void FlattenTree_TreatsEmptyLaneAsMain()
        {
            var c = TestAssets.CreateContributor("Alice", new[] { TestAssets.Seg(_a, "", 0, 5) });
            var comp = TestAssets.CreateComposition("Cine", null, c);

            Assert.AreEqual("Main", CinematicAssembler.FlattenTree(comp)[0].lane);
        }

        static string Key(CinematicAssembler.LeafRef l) =>
            $"{l.lane}:{l.start}:{l.owner}:{l.segment.subTimeline.name}";
    }
}
