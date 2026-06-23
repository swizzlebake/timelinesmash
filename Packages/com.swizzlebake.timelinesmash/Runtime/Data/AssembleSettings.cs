using System;
using UnityEngine;

namespace TimelineSmash
{
    /// <summary>Settings that control how a cinematic is baked and recorded.</summary>
    [Serializable]
    public class AssembleSettings
    {
        [Tooltip("Frame rate for the generated master timeline.")]
        public double frameRate = 30;

        [Tooltip("Total cinematic duration in seconds used for recording. " +
                 "0 = auto from the latest segment's end.")]
        public double totalDuration = 0;
    }
}
