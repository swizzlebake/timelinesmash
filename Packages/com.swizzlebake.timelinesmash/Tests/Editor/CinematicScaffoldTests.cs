using System.Linq;
using NUnit.Framework;
using TimelineSmash.Editor;
using UnityEditor;
using UnityEngine.Timeline;

namespace TimelineSmash.Tests
{
    public class CinematicScaffoldTests
    {
        [TearDown]
        public void TearDown() => TestAssets.Cleanup();

        [Test]
        public void CreateCinematic_WiresManifestToComposition()
        {
            var comp = CinematicScaffold.CreateCinematic(TestAssets.Root, "MyCine");

            Assert.IsNotNull(comp);
            Assert.AreEqual("MyCine", comp.cinematicName);
            Assert.IsNotNull(comp.bindingManifest, "Composition should be wired to a manifest.");
            Assert.IsTrue(AssetDatabase.Contains(comp));
            Assert.IsTrue(AssetDatabase.Contains(comp.bindingManifest));
        }

        [Test]
        public void AddContributor_CreatesAndAddsToComposition()
        {
            var comp = CinematicScaffold.CreateCinematic(TestAssets.Root, "MyCine");

            var set = CinematicScaffold.AddContributor(comp, "Alice");

            Assert.IsNotNull(set);
            Assert.AreEqual("Alice", set.owner);
            Assert.IsTrue(AssetDatabase.Contains(set));
            CollectionAssert.Contains(comp.contributors, set);
        }

        [Test]
        public void AddSubTimeline_CreatesPlayableWithNamedTrackAndSegment()
        {
            var comp = CinematicScaffold.CreateCinematic(TestAssets.Root, "MyCine");
            var set = CinematicScaffold.AddContributor(comp, "Alice");

            var tl = CinematicScaffold.AddSubTimeline(set, "Alice_Shot", "Characters", "Alice");

            Assert.IsNotNull(tl);
            Assert.IsTrue(AssetDatabase.Contains(tl));

            var tracks = tl.GetOutputTracks().ToList();
            Assert.AreEqual(1, tracks.Count);
            Assert.IsInstanceOf<AnimationTrack>(tracks[0]);
            Assert.AreEqual("Alice", tracks[0].name);

            Assert.AreEqual(1, set.segments.Count);
            Assert.AreSame(tl, set.segments[0].subTimeline);
            Assert.AreEqual("Characters", set.segments[0].laneName);
        }

        [Test]
        public void AddSubTimeline_AppendsAfterLatestSegment()
        {
            var comp = CinematicScaffold.CreateCinematic(TestAssets.Root, "MyCine");
            var set = CinematicScaffold.AddContributor(comp, "Alice");

            CinematicScaffold.AddSubTimeline(set, "A", "Main", "A");
            CinematicScaffold.AddSubTimeline(set, "B", "Main", "B");

            Assert.AreEqual(2, set.segments.Count);
            Assert.AreEqual(0, set.segments[0].start, 1e-6);
            Assert.Greater(set.segments[1].start, set.segments[0].start,
                "A second sub-timeline should be placed after the first, not stacked at 0.");
        }
    }
}
