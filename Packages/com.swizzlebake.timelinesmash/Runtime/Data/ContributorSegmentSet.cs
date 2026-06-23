using System.Collections.Generic;
using UnityEngine;

namespace TimelineSmash
{
    /// <summary>
    /// One artist's contributions to a cinematic: the list of segments they own. This is the unit
    /// of isolation — each artist edits only their own asset, so contributions merge cleanly.
    /// </summary>
    [CreateAssetMenu(
        fileName = "Contributor",
        menuName = "TimelineSmash/Contributor Segment Set",
        order = 102)]
    public class ContributorSegmentSet : ScriptableObject
    {
        [Tooltip("Display name of the contributor (used for labels and deterministic ordering).")]
        public string owner = "";

        public List<SubTimelineSegment> segments = new List<SubTimelineSegment>();
    }
}
