using System.Collections.Generic;
using UnityEngine.Timeline;

namespace TimelineSmash.Editor
{
    /// <summary>
    /// A single placed segment after master generation: links the source record to the generated
    /// clip + control asset, and records the exposed-reference name used later to wire its host
    /// director in the stage scene.
    /// </summary>
    public class SegmentEntry
    {
        public SubTimelineSegment segment;
        public string laneName;
        public int laneIndex;       // 0-based lane order (first appearance in the sorted list)
        public int globalIndex;     // index in the flattened, sorted segment list
        public string exposedName;  // exposed-reference name on the master director
        public TimelineClip clip;
        public ControlPlayableAsset control;
    }

    /// <summary>Outcome of assembling a cinematic's master timeline.</summary>
    public class AssembleResult
    {
        public CinematicComposition composition;
        public TimelineAsset master;
        public string masterPath;
        public readonly List<SegmentEntry> entries = new List<SegmentEntry>();
        public readonly List<string> warnings = new List<string>();

        /// <summary>Effective cinematic length: the composition's explicit totalDuration when set,
        /// otherwise the latest segment end.</summary>
        public double totalDuration;
    }
}
