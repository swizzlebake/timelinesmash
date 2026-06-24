using System.Collections.Generic;
using System.Linq;

namespace TimelineSmash.Editor
{
    /// <summary>
    /// Pure checks over a contributor's segments that surface common authoring mistakes — a segment with
    /// both (or neither) of its sources set, or two segments stacked on the same lane. Kept free of GUI so
    /// it can be unit-tested; the segment drawer and contributor inspector render the results.
    /// </summary>
    public static class SegmentDiagnostics
    {
        /// <summary>A segment must reference EITHER a sub-timeline OR a sub-composition — this flags both set.</summary>
        public static bool HasSourceConflict(SubTimelineSegment s) =>
            s != null && s.subTimeline != null && s.subComposition != null;

        /// <summary>...and this flags neither set (the segment would be skipped at assemble time).</summary>
        public static bool HasNoSource(SubTimelineSegment s) =>
            s != null && s.subTimeline == null && s.subComposition == null;

        /// <summary>Segments on the same lane whose times overlap (a likely mistake — one artist stacking
        /// shots on one lane). Returns a human-readable warning per overlapping pair.</summary>
        public static List<string> LaneOverlaps(ContributorSegmentSet set)
        {
            var warnings = new List<string>();
            if (set == null || set.segments == null)
                return warnings;

            var byLane = new Dictionary<string, List<SubTimelineSegment>>();
            foreach (var s in set.segments)
            {
                if (s == null)
                    continue;
                string lane = string.IsNullOrEmpty(s.laneName) ? "Main" : s.laneName;
                if (!byLane.TryGetValue(lane, out var list))
                    byLane[lane] = list = new List<SubTimelineSegment>();
                list.Add(s);
            }

            foreach (var kv in byLane)
            {
                var ordered = kv.Value.OrderBy(s => s.start).ToList();
                for (int i = 1; i < ordered.Count; i++)
                {
                    var prev = ordered[i - 1];
                    var cur = ordered[i];
                    double prevEnd = prev.start + (prev.duration > 0 ? prev.duration : 0);
                    if (cur.start < prevEnd - 1e-6)
                        warnings.Add($"Lane '{kv.Key}': '{Name(prev)}' overlaps '{Name(cur)}'.");
                }
            }

            return warnings;
        }

        static string Name(SubTimelineSegment s) =>
            s.subTimeline != null ? s.subTimeline.name :
            s.subComposition != null ? s.subComposition.name : "(empty)";
    }
}
