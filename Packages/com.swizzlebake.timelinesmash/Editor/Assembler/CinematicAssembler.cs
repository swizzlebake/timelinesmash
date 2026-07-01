using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

namespace TimelineSmash.Editor
{
    /// <summary>
    /// Generates the native master <see cref="TimelineAsset"/> for a cinematic. A composition may nest
    /// sub-compositions to arbitrary depth; the assembler <b>flattens</b> the whole tree into one
    /// single-level master at assemble time, so the runtime never nests Control Tracks (no desync).
    /// Output is deterministic — identical inputs yield the same structure and exposed-reference names —
    /// so the generated master is a regenerable build artifact that replaces hand-merging.
    /// </summary>
    public static class CinematicAssembler
    {
        /// <summary>A leaf sub-timeline resolved to absolute master time after flattening the tree.</summary>
        public struct LeafRef
        {
            public SubTimelineSegment segment; // the leaf (has subTimeline + bindingKey)
            public string owner;
            public string lane;     // absolute lane (group-prefixed where applicable)
            public double start;    // absolute master time
            public double duration; // absolute
            public double clipIn;
            public double speed;    // absolute
            public int emitOrder;   // stable deterministic tiebreak
        }

        public static string LaneOf(SubTimelineSegment seg) =>
            string.IsNullOrEmpty(seg.laneName) ? "Main" : seg.laneName;

        /// <summary>Combine a parent lane prefix with a local lane. Empty prefix → pass-through (merge);
        /// non-empty prefix → namespaced ("Group/Lane").</summary>
        public static string Combine(string prefix, string lane)
        {
            if (string.IsNullOrEmpty(prefix)) return string.IsNullOrEmpty(lane) ? "Main" : lane;
            if (string.IsNullOrEmpty(lane)) return prefix;
            return prefix + "/" + lane;
        }

        /// <summary>Recursively flatten a composition (which may nest sub-compositions) into
        /// absolute-time leaf placements, accumulating start offset and speed scale down each path.
        /// Cycles are detected and skipped with a warning.</summary>
        public static List<LeafRef> FlattenTree(CinematicComposition root, List<string> warnings = null)
        {
            var output = new List<LeafRef>();
            int emit = 0;
            Recurse(root, 0.0, 1.0, "", new HashSet<CinematicComposition>(), output, warnings, ref emit);

            output.Sort((a, b) =>
            {
                int byLane = string.CompareOrdinal(a.lane, b.lane);
                if (byLane != 0) return byLane;
                int byStart = a.start.CompareTo(b.start);
                if (byStart != 0) return byStart;
                int byOwner = string.CompareOrdinal(a.owner ?? "", b.owner ?? "");
                if (byOwner != 0) return byOwner;
                return a.emitOrder.CompareTo(b.emitOrder);
            });
            return output;
        }

        static void Recurse(CinematicComposition comp, double baseStart, double scale, string lanePrefix,
            HashSet<CinematicComposition> path, List<LeafRef> output, List<string> warnings, ref int emit)
        {
            if (comp == null)
                return;
            if (!path.Add(comp))
            {
                warnings?.Add($"Cycle detected: composition '{comp.name}' nests itself; skipped.");
                return;
            }
            if (scale <= 0)
                scale = 1;

            if (comp.contributors != null)
            {
                foreach (var set in comp.contributors)
                {
                    if (set == null || set.segments == null)
                        continue;

                    string owner = string.IsNullOrEmpty(set.owner) ? set.name : set.owner;

                    foreach (var seg in set.segments)
                    {
                        if (seg == null)
                            continue;

                        double absStart = baseStart + seg.start / scale;
                        string lane = Combine(lanePrefix, LaneOf(seg));

                        if (seg.subComposition != null)
                        {
                            double childSpeed = seg.speed > 0 ? seg.speed : 1;
                            // Named lane → namespace the group's lanes under it; empty → pass-through.
                            string childPrefix = string.IsNullOrEmpty(seg.laneName) ? lanePrefix : lane;
                            Recurse(seg.subComposition, absStart, scale * childSpeed, childPrefix, path, output, warnings, ref emit);
                        }
                        else if (seg.subTimeline != null)
                        {
                            // A segment with no explicit duration (<= 0) plays its whole sub-timeline:
                            // fall back to the sub-timeline's own length so artists don't have to retype
                            // each shot's duration by hand. (An empty sub-timeline reports 0 → still 0.)
                            double srcDuration = seg.duration > 0
                                ? seg.duration
                                : (seg.subTimeline.duration > 0 ? seg.subTimeline.duration : 0);

                            output.Add(new LeafRef
                            {
                                segment = seg,
                                owner = owner,
                                lane = lane,
                                start = absStart,
                                duration = srcDuration / scale,
                                clipIn = seg.clipIn > 0 ? seg.clipIn : 0,
                                speed = (seg.speed > 0 ? seg.speed : 1) * scale,
                                emitOrder = emit++,
                            });
                        }
                        else
                        {
                            warnings?.Add($"Segment from '{owner}' has neither a sub-timeline nor a sub-composition; skipped.");
                        }
                    }
                }
            }

            path.Remove(comp);
        }

        /// <summary>Same-lane time overlaps in a flattened leaf list — the cross-contributor "logical
        /// conflict" TimelineSmash exists to catch (two artists stacking shots on one lane). Returns one
        /// human-readable warning per overlapping pair. Segments that merely touch (end == next start)
        /// do not count. Fed into the assemble result so the conflict is reported at assemble time, not
        /// only in the inspector overview.</summary>
        public static List<string> OverlapWarnings(List<LeafRef> leaves)
        {
            var warnings = new List<string>();
            if (leaves == null)
                return warnings;

            var byLane = new Dictionary<string, List<LeafRef>>();
            foreach (var leaf in leaves)
            {
                string lane = string.IsNullOrEmpty(leaf.lane) ? "Main" : leaf.lane;
                if (!byLane.TryGetValue(lane, out var list))
                    byLane[lane] = list = new List<LeafRef>();
                list.Add(leaf);
            }

            foreach (var kv in byLane)
            {
                var list = kv.Value;
                for (int i = 0; i < list.Count; i++)
                    for (int j = i + 1; j < list.Count; j++)
                    {
                        var a = list[i];
                        var b = list[j];
                        double aEnd = a.start + (a.duration > 0 ? a.duration : 0);
                        double bEnd = b.start + (b.duration > 0 ? b.duration : 0);
                        if (a.start < bEnd && b.start < aEnd)
                            warnings.Add($"Lane '{kv.Key}': '{NameOf(a)}' overlaps '{NameOf(b)}'.");
                    }
            }

            return warnings;
        }

        static string NameOf(LeafRef leaf)
        {
            string shot = leaf.segment != null && leaf.segment.subTimeline != null
                ? leaf.segment.subTimeline.name
                : "<missing>";
            return string.IsNullOrEmpty(leaf.owner) ? shot : $"{shot} ({leaf.owner})";
        }

        /// <summary>Generate (or regenerate) the master timeline at <paramref name="masterPath"/> by
        /// flattening the composition tree. The asset is deleted and recreated from scratch — Timeline
        /// tracks/clips are sub-assets of the .playable, so wholesale recreation avoids orphans.</summary>
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

            var leaves = FlattenTree(composition, result.warnings);
            result.warnings.AddRange(OverlapWarnings(leaves));

            var laneTracks = new Dictionary<string, ControlTrack>();
            double maxEnd = 0;

            for (int gi = 0; gi < leaves.Count; gi++)
            {
                var leaf = leaves[gi];
                string lane = string.IsNullOrEmpty(leaf.lane) ? "Main" : leaf.lane;

                if (!laneTracks.TryGetValue(lane, out var track))
                {
                    track = master.CreateTrack<ControlTrack>(null, lane);
                    laneTracks[lane] = track;
                }

                var clip = track.CreateClip<ControlPlayableAsset>();
                var control = (ControlPlayableAsset)clip.asset;

                control.updateDirector = true;
                control.updateParticle = false;
                control.updateITimeControl = false;
                control.searchHierarchy = false;
                control.active = false;

                string exposedName = $"TS_{gi:D4}";
                control.sourceGameObject = new ExposedReference<GameObject> { exposedName = exposedName };

                clip.start = leaf.start;
                clip.duration = leaf.duration > 0 ? leaf.duration : 0.0001;
                clip.clipIn = leaf.clipIn > 0 ? leaf.clipIn : 0;
                clip.timeScale = leaf.speed > 0 ? leaf.speed : 1;
                clip.displayName = $"{leaf.segment.subTimeline.name} ({leaf.owner})";

                maxEnd = Math.Max(maxEnd, leaf.start + clip.duration);

                result.entries.Add(new SegmentEntry
                {
                    segment = leaf.segment,
                    laneName = lane,
                    laneIndex = 0,
                    globalIndex = gi,
                    exposedName = exposedName,
                    clip = clip,
                    control = control,
                });

                // Optional per-segment prefab spawn: a parallel control clip (on a per-lane spawn track so it
                // never collides with the host-driving clip) that instantiates the prefab for the segment's
                // duration. The prefab reference is a project asset, so it serializes into the master.
                if (leaf.segment.spawnPrefab != null)
                {
                    string spawnLane = "Spawn:" + lane;
                    if (!laneTracks.TryGetValue(spawnLane, out var spawnTrack))
                    {
                        spawnTrack = master.CreateTrack<ControlTrack>(null, spawnLane);
                        laneTracks[spawnLane] = spawnTrack;
                    }

                    var spawnClip = spawnTrack.CreateClip<ControlPlayableAsset>();
                    var spawnControl = (ControlPlayableAsset)spawnClip.asset;
                    spawnControl.prefabGameObject = leaf.segment.spawnPrefab;
                    spawnControl.updateDirector = true;     // drive a self-animating prefab's own director
                    spawnControl.updateParticle = true;
                    spawnControl.updateITimeControl = true;
                    spawnControl.searchHierarchy = true;
                    spawnControl.active = true;

                    spawnClip.start = leaf.start;
                    spawnClip.duration = clip.duration;
                    spawnClip.timeScale = clip.timeScale;
                    spawnClip.displayName = $"Spawn {leaf.segment.spawnPrefab.name}";
                }
            }

            bool hasExplicit = composition != null && composition.settings != null && composition.settings.totalDuration > 0;
            result.totalDuration = hasExplicit ? composition.settings.totalDuration : maxEnd;

            master.editorSettings.frameRate = fps;

            // Persist via SaveAssets only. Do NOT ImportAsset here: a reimport destroys this in-memory
            // TimelineAsset instance (and its sub-asset clips), leaving references the caller still needs.
            EditorUtility.SetDirty(master);
            AssetDatabase.SaveAssets();

            return result;
        }
    }
}
