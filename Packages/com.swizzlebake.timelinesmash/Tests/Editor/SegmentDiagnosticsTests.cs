using System.Collections.Generic;
using NUnit.Framework;
using TimelineSmash.Editor;
using UnityEngine;
using UnityEngine.Timeline;

namespace TimelineSmash.Tests
{
    public class SegmentDiagnosticsTests
    {
        readonly List<Object> _temp = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _temp)
                if (o != null)
                    Object.DestroyImmediate(o);
            _temp.Clear();
        }

        T Make<T>() where T : ScriptableObject
        {
            var o = ScriptableObject.CreateInstance<T>();
            _temp.Add(o);
            return o;
        }

        static ContributorSegmentSet SetWith(params SubTimelineSegment[] segments)
        {
            var set = ScriptableObject.CreateInstance<ContributorSegmentSet>();
            set.segments.AddRange(segments);
            return set;
        }

        [Test]
        public void HasSourceConflict_BothSet_IsTrue()
        {
            var seg = new SubTimelineSegment { subTimeline = Make<TimelineAsset>(), subComposition = Make<CinematicComposition>() };
            Assert.IsTrue(SegmentDiagnostics.HasSourceConflict(seg));
            Assert.IsFalse(SegmentDiagnostics.HasNoSource(seg));
        }

        [Test]
        public void HasNoSource_NeitherSet_IsTrue()
        {
            var seg = new SubTimelineSegment();
            Assert.IsTrue(SegmentDiagnostics.HasNoSource(seg));
            Assert.IsFalse(SegmentDiagnostics.HasSourceConflict(seg));
        }

        [Test]
        public void LaneOverlaps_SameLaneOverlapping_Warns()
        {
            var set = SetWith(
                new SubTimelineSegment { laneName = "Main", start = 0, duration = 5 },
                new SubTimelineSegment { laneName = "Main", start = 3, duration = 5 });

            Assert.AreEqual(1, SegmentDiagnostics.LaneOverlaps(set).Count);
            Object.DestroyImmediate(set);
        }

        [Test]
        public void LaneOverlaps_Adjacent_DoesNotWarn()
        {
            var set = SetWith(
                new SubTimelineSegment { laneName = "Main", start = 0, duration = 5 },
                new SubTimelineSegment { laneName = "Main", start = 5, duration = 5 });

            Assert.AreEqual(0, SegmentDiagnostics.LaneOverlaps(set).Count);
            Object.DestroyImmediate(set);
        }

        [Test]
        public void LaneOverlaps_DifferentLanes_DoesNotWarn()
        {
            var set = SetWith(
                new SubTimelineSegment { laneName = "Main", start = 0, duration = 5 },
                new SubTimelineSegment { laneName = "Camera", start = 3, duration = 5 });

            Assert.AreEqual(0, SegmentDiagnostics.LaneOverlaps(set).Count);
            Object.DestroyImmediate(set);
        }
    }
}
