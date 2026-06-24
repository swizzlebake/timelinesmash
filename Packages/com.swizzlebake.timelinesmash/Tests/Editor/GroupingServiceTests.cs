using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TimelineSmash.Editor;
using UnityEngine;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

namespace TimelineSmash.Tests
{
    public class GroupingServiceTests
    {
        readonly List<Object> _tmp = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _tmp)
                if (o != null)
                    Object.DestroyImmediate(o);
            _tmp.Clear();
            TestAssets.Cleanup();
        }

        T New<T>() where T : ScriptableObject
        {
            var o = ScriptableObject.CreateInstance<T>();
            _tmp.Add(o);
            return o;
        }

        TimelineAsset Tl(string n) { var t = New<TimelineAsset>(); t.name = n; return t; }

        SubTimelineSegment Leaf(TimelineAsset tl, string lane, double start, double dur)
            => new SubTimelineSegment { subTimeline = tl, laneName = lane, start = start, duration = dur };

        [Test]
        public void GroupSegments_IsTransparent_WithRebasing()
        {
            TestAssets.EnsureRoot();
            var a = Tl("A"); var b = Tl("B"); var c = Tl("C");
            var set = New<ContributorSegmentSet>();
            set.owner = "Alice";
            set.segments.Add(Leaf(a, "X", 0, 4));
            set.segments.Add(Leaf(b, "Y", 5, 3));
            set.segments.Add(Leaf(c, "X", 10, 2));
            var parent = New<CinematicComposition>();
            parent.contributors.Add(set);

            var before = CinematicAssembler.FlattenTree(parent);

            var group = GroupingService.GroupSegments(set, new[] { 1, 2 }, "Grp", TestAssets.Root);

            Assert.IsNotNull(group);
            Assert.AreEqual(2, set.segments.Count); // A + the group reference

            var after = CinematicAssembler.FlattenTree(parent);
            AssertSame(before, after);
        }

        [Test]
        public void GroupSegments_CreatesGroupWithRebasedChildren()
        {
            TestAssets.EnsureRoot();
            var a = Tl("A"); var b = Tl("B");
            var set = New<ContributorSegmentSet>();
            set.owner = "Alice";
            set.segments.Add(Leaf(a, "X", 2, 4));
            set.segments.Add(Leaf(b, "X", 8, 2));

            var group = GroupingService.GroupSegments(set, new[] { 0, 1 }, "Grp", TestAssets.Root);

            Assert.IsNotNull(group);
            Assert.AreEqual(1, set.segments.Count);
            Assert.AreSame(group, set.segments[0].subComposition);
            Assert.AreEqual(2, set.segments[0].start, 1e-6); // group placed at minStart

            var childSet = group.contributors.Single();
            Assert.AreEqual(2, childSet.segments.Count);
            var byName = childSet.segments.ToDictionary(s => s.subTimeline.name, s => s.start);
            Assert.AreEqual(0, byName["A"], 1e-6); // 2 - 2
            Assert.AreEqual(6, byName["B"], 1e-6); // 8 - 2
        }

        static void AssertSame(List<CinematicAssembler.LeafRef> a, List<CinematicAssembler.LeafRef> b)
        {
            Assert.AreEqual(a.Count, b.Count, "leaf count");
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreSame(a[i].segment.subTimeline, b[i].segment.subTimeline, $"leaf[{i}]");
                Assert.AreEqual(a[i].lane, b[i].lane, $"lane[{i}]");
                Assert.AreEqual(a[i].start, b[i].start, 1e-6, $"start[{i}]");
                Assert.AreEqual(a[i].duration, b[i].duration, 1e-6, $"dur[{i}]");
                Assert.AreEqual(a[i].speed, b[i].speed, 1e-6, $"speed[{i}]");
            }
        }
    }
}
