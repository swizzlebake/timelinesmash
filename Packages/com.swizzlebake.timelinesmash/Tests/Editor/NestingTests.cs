using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TimelineSmash.Editor;
using UnityEngine;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

namespace TimelineSmash.Tests
{
    // Deep nesting + grouping at the data/flatten level. Pure in-memory (no AssetDatabase) since
    // FlattenTree only walks references.
    public class NestingTests
    {
        readonly List<Object> _tmp = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _tmp)
                if (o != null)
                    Object.DestroyImmediate(o);
            _tmp.Clear();
        }

        T New<T>() where T : ScriptableObject
        {
            var o = ScriptableObject.CreateInstance<T>();
            _tmp.Add(o);
            return o;
        }

        TimelineAsset Tl(string name) { var t = New<TimelineAsset>(); t.name = name; return t; }

        ContributorSegmentSet Set(string owner, params SubTimelineSegment[] segs)
        {
            var s = New<ContributorSegmentSet>();
            s.owner = owner;
            s.segments.AddRange(segs);
            return s;
        }

        CinematicComposition Comp(params ContributorSegmentSet[] sets)
        {
            var c = New<CinematicComposition>();
            c.contributors.AddRange(sets);
            return c;
        }

        static SubTimelineSegment LeafSeg(TimelineAsset tl, string lane, double start, double dur, double speed = 1, double clipIn = 0)
            => new SubTimelineSegment { subTimeline = tl, laneName = lane, start = start, duration = dur, speed = speed, clipIn = clipIn };

        static SubTimelineSegment GrpSeg(CinematicComposition child, string lane, double start, double speed = 1)
            => new SubTimelineSegment { subComposition = child, laneName = lane, start = start, speed = speed };

        [Test]
        public void DeepNesting_FlattensToLeafCount()
        {
            var L = Tl("L");
            var inner = Comp(Set("a", LeafSeg(L, "X", 0, 2)));
            var mid = Comp(Set("a", GrpSeg(inner, "", 0)));
            var root = Comp(Set("a", GrpSeg(mid, "", 0))); // 3 levels of groups

            var leaves = CinematicAssembler.FlattenTree(root);
            Assert.AreEqual(1, leaves.Count);
            Assert.AreSame(L, leaves[0].segment.subTimeline);
        }

        [Test]
        public void OffsetAndSpeed_AccumulateThroughNesting()
        {
            var L = Tl("L");
            var inner = Comp(Set("a", LeafSeg(L, "", 4, 6)));
            var root = Comp(Set("a", GrpSeg(inner, "", 10, speed: 2)));

            var p = CinematicAssembler.FlattenTree(root).Single();
            Assert.AreEqual(12, p.start, 1e-6);    // 10 + 4/2
            Assert.AreEqual(3, p.duration, 1e-6);  // 6/2
            Assert.AreEqual(2, p.speed, 1e-6);     // 1 * 2
        }

        [Test]
        public void Grouping_IsTransparentAtIdentity()
        {
            var A = Tl("A"); var B = Tl("B"); var C = Tl("C");
            var inline = Comp(Set("a", LeafSeg(A, "X", 0, 4), LeafSeg(B, "Y", 5, 3), LeafSeg(C, "X", 10, 2)));
            var group = Comp(Set("a", LeafSeg(B, "Y", 5, 3), LeafSeg(C, "X", 10, 2)));
            var grouped = Comp(Set("a", LeafSeg(A, "X", 0, 4), GrpSeg(group, "", 0)));

            AssertSame(CinematicAssembler.FlattenTree(inline), CinematicAssembler.FlattenTree(grouped));
        }

        [Test]
        public void NamedGroupLane_NamespacesChildren()
        {
            var B = Tl("B"); var C = Tl("C");
            var group = Comp(Set("a", LeafSeg(B, "Y", 5, 3), LeafSeg(C, "X", 10, 2)));
            var root = Comp(Set("a", GrpSeg(group, "Grp", 0)));

            var byName = CinematicAssembler.FlattenTree(root).ToDictionary(l => l.segment.subTimeline.name, l => l.lane);
            Assert.AreEqual("Grp/Y", byName["B"]);
            Assert.AreEqual("Grp/X", byName["C"]);
        }

        [Test]
        public void Cycle_IsDetectedAndSkipped()
        {
            var L = Tl("L");
            var comp = Comp();
            var set = Set("a", LeafSeg(L, "X", 0, 5));
            comp.contributors.Add(set);
            set.segments.Add(GrpSeg(comp, "", 0)); // self-reference

            var warnings = new List<string>();
            var leaves = CinematicAssembler.FlattenTree(comp, warnings);

            Assert.AreEqual(1, leaves.Count);
            Assert.IsTrue(warnings.Any(w => w.Contains("Cycle")));
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
