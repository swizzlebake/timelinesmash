using System;
using System.Collections.Generic;

namespace TimelineSmash.Editor
{
    /// <summary>
    /// Read-only summary of a composition for inspector display and validation: lanes, placed
    /// segments, total duration, and overlap/gap diagnostics. Pure (no asset side effects).
    /// </summary>
    public class CinematicOverviewModel
    {
        public class Placed
        {
            public string owner;
            public string subTimelineName;
            public double start;
            public double end;
            public double Duration => end - start;
        }

        public class Gap
        {
            public double from;
            public double to;
            public double Length => to - from;
        }

        public class Lane
        {
            public string name;
            public readonly List<Placed> segments = new List<Placed>();
            public readonly List<string> overlaps = new List<string>();
            public readonly List<Gap> gaps = new List<Gap>();
        }

        public readonly List<Lane> lanes = new List<Lane>();
        public readonly List<string> warnings = new List<string>();
        public double totalDuration;

        public static CinematicOverviewModel Build(CinematicComposition composition)
        {
            var model = new CinematicOverviewModel();
            if (composition == null)
                return model;

            var flat = CinematicAssembler.Flatten(composition);
            var laneByName = new Dictionary<string, Lane>();

            foreach (var sref in flat)
            {
                var seg = sref.segment;
                string laneName = CinematicAssembler.LaneOf(seg);

                if (!laneByName.TryGetValue(laneName, out var lane))
                {
                    lane = new Lane { name = laneName };
                    laneByName[laneName] = lane;
                    model.lanes.Add(lane);
                }

                double dur = seg.duration > 0 ? seg.duration : 0;
                var placed = new Placed
                {
                    owner = sref.owner,
                    subTimelineName = seg.subTimeline != null ? seg.subTimeline.name : "<missing>",
                    start = seg.start,
                    end = seg.start + dur,
                };

                // Overlap: any existing segment on this lane whose interval intersects.
                foreach (var other in lane.segments)
                {
                    if (placed.start < other.end && other.start < placed.end)
                    {
                        lane.overlaps.Add(
                            $"'{placed.subTimelineName}' overlaps '{other.subTimelineName}' on lane '{laneName}'");
                    }
                }

                lane.segments.Add(placed);

                if (seg.subTimeline == null)
                    model.warnings.Add($"Segment from '{sref.owner}' on lane '{laneName}' has no sub-timeline.");

                model.totalDuration = Math.Max(model.totalDuration, placed.end);
            }

            // Gaps: per lane, between consecutive segments ordered by start.
            foreach (var lane in model.lanes)
            {
                lane.segments.Sort((a, b) => a.start.CompareTo(b.start));
                double cursor = 0;
                foreach (var p in lane.segments)
                {
                    if (p.start > cursor + 1e-6)
                        lane.gaps.Add(new Gap { from = cursor, to = p.start });
                    cursor = Math.Max(cursor, p.end);
                }
            }

            foreach (var lane in model.lanes)
                foreach (var o in lane.overlaps)
                    model.warnings.Add(o);

            return model;
        }
    }
}
