using System;
using System.Collections.Generic;

namespace TimelineSmash.Editor
{
    /// <summary>
    /// Read-only summary of a composition for inspector display and validation: lanes, placed leaves
    /// (after flattening any nested sub-compositions), total duration, and overlap/gap diagnostics.
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

            var leaves = CinematicAssembler.FlattenTree(composition, model.warnings);
            var laneByName = new Dictionary<string, Lane>();

            foreach (var leaf in leaves)
            {
                string laneName = string.IsNullOrEmpty(leaf.lane) ? "Main" : leaf.lane;
                if (!laneByName.TryGetValue(laneName, out var lane))
                {
                    lane = new Lane { name = laneName };
                    laneByName[laneName] = lane;
                    model.lanes.Add(lane);
                }

                var placed = new Placed
                {
                    owner = leaf.owner,
                    subTimelineName = leaf.segment.subTimeline != null ? leaf.segment.subTimeline.name : "<missing>",
                    start = leaf.start,
                    end = leaf.start + (leaf.duration > 0 ? leaf.duration : 0),
                };

                foreach (var other in lane.segments)
                {
                    if (placed.start < other.end && other.start < placed.end)
                        lane.overlaps.Add(
                            $"'{placed.subTimelineName}' overlaps '{other.subTimelineName}' on lane '{laneName}'");
                }

                lane.segments.Add(placed);
                model.totalDuration = Math.Max(model.totalDuration, placed.end);
            }

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
