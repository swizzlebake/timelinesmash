using System;
using UnityEngine;
using UnityEngine.Timeline;

namespace TimelineSmash
{
    /// <summary>
    /// One placement of a sub-timeline inside a cinematic: which sub-timeline, on which lane,
    /// when, and for how long. Authored by a single contributor inside their own
    /// <see cref="ContributorSegmentSet"/> so that two artists never edit the same file.
    /// </summary>
    [Serializable]
    public class SubTimelineSegment
    {
        [Tooltip("The sub-timeline asset (.playable) this segment plays. Owned by one artist. " +
                 "Set EITHER this OR Sub Composition.")]
        public TimelineAsset subTimeline;

        [Tooltip("A nested cinematic (group) to place here instead of a leaf sub-timeline. The group " +
                 "is flattened into the master at assemble time (arbitrary nesting depth, no runtime " +
                 "desync). Set EITHER this OR Sub Timeline.")]
        public CinematicComposition subComposition;

        [Tooltip("Lane to place this segment on. Segments sharing a lane become one master Control Track. " +
                 "For a sub-composition: empty = merge the group's lanes into the parent; named = " +
                 "namespace them under this lane (e.g. 'Group/Camera').")]
        public string laneName = "Main";

        [Tooltip("Optional binding-manifest key override. When empty, each sub-timeline track is " +
                 "bound by its own track name.")]
        public string bindingKey = "";

        [Tooltip("Start time on the master timeline, in seconds.")]
        public double start = 0;

        [Tooltip("Duration on the master timeline, in seconds.")]
        public double duration = 5;

        [Tooltip("Trim offset into the sub-timeline (clip-in), in seconds.")]
        public double clipIn = 0;

        [Tooltip("Playback speed multiplier for this segment.")]
        public double speed = 1;

        [Tooltip("Optional prefab to spawn for this segment's duration. The master gets a parallel control " +
                 "clip that instantiates the prefab when the segment starts and destroys it when it ends. " +
                 "Use a self-animating prefab (its own PlayableDirector / Animator / particles).")]
        public GameObject spawnPrefab;
    }
}
