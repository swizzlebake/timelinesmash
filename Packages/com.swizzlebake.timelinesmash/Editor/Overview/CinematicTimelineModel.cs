using System;
using System.Collections.Generic;

namespace TimelineSmash.Editor
{
    /// <summary>
    /// The editable lane×time view of a composition's <b>direct</b> segments (one row per lane). Unlike
    /// <see cref="CinematicOverviewModel"/>, which flattens nested sub-compositions for display, this keeps
    /// each item paired with its owning <see cref="ContributorSegmentSet"/> and <see cref="SubTimelineSegment"/>
    /// so the visual window can move/resize them (with Undo). Pure data — the window renders it.
    /// </summary>
    public class CinematicTimelineModel
    {
        public class Item
        {
            public ContributorSegmentSet set;   // owning set — record this for Undo before mutating
            public SubTimelineSegment segment;  // the segment to move/resize
            public string owner;
            public string lane;
            public string name;
            public bool isGroup;                // sub-composition (a group) vs a leaf sub-timeline
        }

        public readonly List<string> lanes = new List<string>();
        public readonly List<Item> items = new List<Item>();
        public double totalDuration;

        public static CinematicTimelineModel Build(CinematicComposition composition)
        {
            var model = new CinematicTimelineModel();
            if (composition == null || composition.contributors == null)
                return model;

            foreach (var set in composition.contributors)
            {
                if (set == null || set.segments == null)
                    continue;

                string owner = string.IsNullOrEmpty(set.owner) ? set.name : set.owner;
                foreach (var seg in set.segments)
                {
                    if (seg == null)
                        continue;

                    string lane = string.IsNullOrEmpty(seg.laneName) ? "Main" : seg.laneName;
                    if (!model.lanes.Contains(lane))
                        model.lanes.Add(lane);

                    string name = seg.subTimeline != null ? seg.subTimeline.name
                        : seg.subComposition != null ? seg.subComposition.name : "(empty)";

                    model.items.Add(new Item
                    {
                        set = set,
                        segment = seg,
                        owner = owner,
                        lane = lane,
                        name = name,
                        isGroup = seg.subComposition != null,
                    });

                    model.totalDuration = Math.Max(model.totalDuration,
                        seg.start + (seg.duration > 0 ? seg.duration : 0));
                }
            }

            model.lanes.Sort(StringComparer.Ordinal);
            return model;
        }
    }

    /// <summary>Snap a time to the nearest frame and clamp to ≥ 0 — used while dragging segments.</summary>
    public static class TimelineSnap
    {
        public static double Snap(double time, double frameRate, bool enabled)
        {
            if (!enabled || frameRate <= 0)
                return Math.Max(0, time);
            double snapped = Math.Round(time * frameRate) / frameRate;
            return Math.Max(0, snapped);
        }
    }
}
