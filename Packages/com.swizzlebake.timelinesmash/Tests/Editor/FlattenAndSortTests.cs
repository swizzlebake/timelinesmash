using System.Collections.Generic;
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
        public void Flatten_SortsByLaneThenStart()
        {
            var alice = TestAssets.CreateContributor("Alice", new[]
            {
                TestAssets.Seg(_c, "Camera", 10, 5),
                TestAssets.Seg(_a, "Camera", 0, 5),
            });
            var bob = TestAssets.CreateContributor("Bob", new[]
            {
                TestAssets.Seg(_b, "Audio", 2, 5),
            });
            var comp = TestAssets.CreateComposition("Cine", null, alice, bob);

            var flat = CinematicAssembler.Flatten(comp);

            // Lanes ordinal-sorted: "Audio" < "Camera"; within Camera, start 0 < 10.
            var order = flat.Select(s => $"{s.segment.laneName}:{s.segment.start}").ToList();
            CollectionAssert.AreEqual(new[] { "Audio:2", "Camera:0", "Camera:10" }, order);
        }

        [Test]
        public void Flatten_IsDeterministicAcrossCalls()
        {
            var alice = TestAssets.CreateContributor("Alice", new[]
            {
                TestAssets.Seg(_a, "Main", 5, 5),
                TestAssets.Seg(_b, "Main", 5, 5), // tie on lane+start -> broken by owner/index
            });
            var comp = TestAssets.CreateComposition("Cine", null, alice);

            var first = CinematicAssembler.Flatten(comp).Select(Key).ToList();
            var second = CinematicAssembler.Flatten(comp).Select(Key).ToList();

            CollectionAssert.AreEqual(first, second);
        }

        [Test]
        public void Flatten_SkipsNullContributorSets()
        {
            // Unity replaces null elements of a [Serializable]-class list with default instances on
            // serialization, so null *segments* can't survive a saved asset; null *contributor sets*
            // are object references and can. This verifies the null-set guard.
            var alice = TestAssets.CreateContributor("Alice", new[] { TestAssets.Seg(_a, "Main", 0, 5) });
            var comp = TestAssets.CreateComposition("Cine", null, alice, null);

            var flat = CinematicAssembler.Flatten(comp);

            Assert.AreEqual(1, flat.Count);
            Assert.AreSame(_a, flat[0].segment.subTimeline);
        }

        [Test]
        public void Flatten_TreatsEmptyLaneAsMain()
        {
            var c = TestAssets.CreateContributor("Alice", new[] { TestAssets.Seg(_a, "", 0, 5) });
            var comp = TestAssets.CreateComposition("Cine", null, c);

            var flat = CinematicAssembler.Flatten(comp);
            Assert.AreEqual("", flat[0].segment.laneName); // record unchanged...
            Assert.AreEqual("Main", CinematicAssembler.LaneOf(flat[0].segment)); // ...but resolves to Main
        }

        static string Key(CinematicAssembler.SegmentRef s) =>
            $"{s.segment.laneName}:{s.segment.start}:{s.owner}:{s.setIndex}:{s.indexInSet}";
    }
}
