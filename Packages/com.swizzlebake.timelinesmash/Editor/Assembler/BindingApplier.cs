using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace TimelineSmash.Editor
{
    /// <summary>
    /// Resolves a segment's sub-timeline track bindings from the manifest and applies them onto the
    /// segment's host director. Nested Animation/Audio track bindings resolve against the host
    /// director that plays the sub-timeline, which is why bindings are set here and not on the master.
    /// </summary>
    public static class BindingApplier
    {
        /// <summary>Bind every output track of the segment's sub-timeline on <paramref name="hostDir"/>.
        /// The key is <c>segment.bindingKey</c> when set, otherwise the track's own name. Returns the
        /// number of tracks successfully bound; unresolved keys are appended to <paramref name="warnings"/>.</summary>
        public static int Apply(PlayableDirector hostDir, SubTimelineSegment segment, BindingManifest manifest, List<string> warnings)
        {
            if (hostDir == null || segment == null)
                return 0;

            var sub = segment.subTimeline;
            if (sub == null)
                return 0;

            int bound = 0;
            bool overrideKey = !string.IsNullOrEmpty(segment.bindingKey);

            foreach (var track in sub.GetOutputTracks())
            {
                if (track == null)
                    continue;

                string key = overrideKey ? segment.bindingKey : track.name;
                Object target = manifest != null ? manifest.Resolve(key) : null;

                if (target != null)
                {
                    hostDir.SetGenericBinding(track, target);
                    bound++;
                }
                else
                {
                    warnings?.Add(
                        $"Unresolved binding key '{key}' for track '{track.name}' in sub-timeline '{sub.name}'.");
                }
            }

            return bound;
        }
    }
}
