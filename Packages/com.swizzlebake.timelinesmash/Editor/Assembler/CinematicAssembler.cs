using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

namespace TimelineSmash.Editor
{
    /// <summary>
    /// Generates the native master <see cref="TimelineAsset"/> for a cinematic from its
    /// contributors' segments. The output is deterministic — identical inputs always yield the same
    /// structure and exposed-reference names — so the generated master is a regenerable build
    /// artifact that replaces hand-merging.
    /// </summary>
    public static class CinematicAssembler
    {
        /// <summary>A flattened reference to one segment, carrying the data needed for a stable sort.</summary>
        public struct SegmentRef
        {
            public SubTimelineSegment segment;
            public string owner;
            public int setIndex;
            public int indexInSet;
        }

        /// <summary>Flatten every contributor's segments into one deterministically-ordered list.
        /// Null contributor sets and null segments are skipped.</summary>
        public static List<SegmentRef> Flatten(CinematicComposition composition)
        {
            var list = new List<SegmentRef>();
            if (composition == null || composition.contributors == null)
                return list;

            for (int c = 0; c < composition.contributors.Count; c++)
            {
                var set = composition.contributors[c];
                if (set == null || set.segments == null)
                    continue;

                string owner = string.IsNullOrEmpty(set.owner) ? set.name : set.owner;
                for (int i = 0; i < set.segments.Count; i++)
                {
                    var seg = set.segments[i];
                    if (seg == null)
                        continue;
                    list.Add(new SegmentRef { segment = seg, owner = owner, setIndex = c, indexInSet = i });
                }
            }

            list.Sort(CompareSegments);
            return list;
        }

        static int CompareSegments(SegmentRef a, SegmentRef b)
        {
            int byLane = string.CompareOrdinal(LaneOf(a.segment), LaneOf(b.segment));
            if (byLane != 0) return byLane;

            int byStart = a.segment.start.CompareTo(b.segment.start);
            if (byStart != 0) return byStart;

            int byOwner = string.CompareOrdinal(a.owner ?? "", b.owner ?? "");
            if (byOwner != 0) return byOwner;

            int bySet = a.setIndex.CompareTo(b.setIndex);
            if (bySet != 0) return bySet;

            return a.indexInSet.CompareTo(b.indexInSet);
        }

        public static string LaneOf(SubTimelineSegment seg)
        {
            return string.IsNullOrEmpty(seg.laneName) ? "Main" : seg.laneName;
        }

        /// <summary>Generate (or regenerate) the master timeline at <paramref name="masterPath"/>.
        /// The asset is deleted and recreated from scratch — Timeline tracks/clips are sub-assets of
        /// the .playable, so wholesale recreation avoids orphaned sub-assets.</summary>
        public static AssembleResult BuildMaster(CinematicComposition composition, string masterPath)
        {
            if (string.IsNullOrEmpty(masterPath))
                throw new ArgumentException("masterPath is required", nameof(masterPath));

            var result = new AssembleResult { composition = composition, masterPath = masterPath };

            if (AssetDatabase.LoadAssetAtPath<TimelineAsset>(masterPath) != null)
                AssetDatabase.DeleteAsset(masterPath);

            EditorAssetUtil.EnsureFolder(Path.GetDirectoryName(masterPath));

            var master = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(master, masterPath);
            result.master = master;

            double fps = composition != null && composition.settings != null && composition.settings.frameRate > 0
                ? composition.settings.frameRate
                : 30.0;

            var flat = Flatten(composition);

            // Lane order = first appearance in the sorted list, so it is deterministic.
            var laneTracks = new Dictionary<string, ControlTrack>();
            var laneOrder = new List<string>();
            double maxEnd = 0;

            for (int gi = 0; gi < flat.Count; gi++)
            {
                var sref = flat[gi];
                var seg = sref.segment;

                if (seg.subTimeline == null)
                {
                    result.warnings.Add(
                        $"Segment {gi} from '{sref.owner}' has no sub-timeline assigned; skipped.");
                    continue;
                }

                string lane = LaneOf(seg);
                if (!laneTracks.TryGetValue(lane, out var track))
                {
                    track = master.CreateTrack<ControlTrack>(null, lane);
                    laneTracks[lane] = track;
                    laneOrder.Add(lane);
                }

                var clip = track.CreateClip<ControlPlayableAsset>();
                var control = (ControlPlayableAsset)clip.asset;

                // Drive only the nested director — no GameObject activation, particles, or ITimeControl.
                control.updateDirector = true;
                control.updateParticle = false;
                control.updateITimeControl = false;
                control.searchHierarchy = false;
                control.active = false;

                string exposedName = $"TS_{gi:D4}";
                control.sourceGameObject = new ExposedReference<GameObject> { exposedName = exposedName };

                clip.start = seg.start;
                clip.duration = seg.duration > 0 ? seg.duration : 0.0001;
                clip.clipIn = seg.clipIn > 0 ? seg.clipIn : 0;
                clip.timeScale = seg.speed > 0 ? seg.speed : 1;
                clip.displayName = $"{seg.subTimeline.name} ({sref.owner})";

                maxEnd = Math.Max(maxEnd, seg.start + clip.duration);

                result.entries.Add(new SegmentEntry
                {
                    segment = seg,
                    laneName = lane,
                    laneIndex = laneOrder.IndexOf(lane),
                    globalIndex = gi,
                    exposedName = exposedName,
                    clip = clip,
                    control = control,
                });
            }

            bool hasExplicit = composition != null && composition.settings != null && composition.settings.totalDuration > 0;
            result.totalDuration = hasExplicit ? composition.settings.totalDuration : maxEnd;

            master.editorSettings.frameRate = fps;

            EditorUtility.SetDirty(master);
            AssetDatabase.SaveAssets();

            return result;
        }
    }
}
