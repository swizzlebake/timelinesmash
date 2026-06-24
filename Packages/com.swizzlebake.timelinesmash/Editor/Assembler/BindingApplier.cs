using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
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
        /// Key resolution per track:
        /// <list type="bullet">
        /// <item>with a <c>segment.bindingKey</c> override: <c>"&lt;bindingKey&gt;/&lt;trackName&gt;"</c>
        /// first (so one sub-timeline can retarget its tracks individually), then the bare
        /// <c>"&lt;bindingKey&gt;"</c> (a whole-sub override, backwards compatible);</item>
        /// <item>otherwise the track's own name.</item>
        /// </list>
        /// A nested <see cref="ControlTrack"/> has no generic binding; its clips are wired through the host
        /// director's exposed references instead. When <paramref name="resolveBySceneName"/> is set, a key
        /// that the manifest does not resolve falls back to a GameObject of that name in the host's scene
        /// (picking the component the track binds to) — this lets a committed manifest target live actors
        /// by name when assembling into a populated scene. Returns the number of tracks bound; unresolved
        /// keys are appended to <paramref name="warnings"/>.</summary>
        public static int Apply(PlayableDirector hostDir, SubTimelineSegment segment, CompiledBindings bindings,
            List<string> warnings, bool resolveBySceneName = false)
        {
            if (hostDir == null || segment == null)
                return 0;

            var sub = segment.subTimeline;
            if (sub == null)
                return 0;

            int bound = 0;
            Scene scene = resolveBySceneName ? hostDir.gameObject.scene : default;

            foreach (var track in sub.GetOutputTracks())
            {
                if (track == null)
                    continue;

                Object target = Resolve(bindings, segment, track, scene, out string key);

                if (track is ControlTrack control)
                {
                    // A ControlTrack carries no generic binding: each clip drives a GameObject through an
                    // ExposedReference resolved on the host director. Wire those so a control track nested
                    // inside a sub-timeline isn't silently dead.
                    int wired = WireControlTrack(hostDir, control, target);
                    if (wired > 0)
                        bound += wired;
                    else if (target == null)
                        warnings?.Add(Unresolved(key, track, sub));
                    continue;
                }

                if (target != null)
                {
                    hostDir.SetGenericBinding(track, target);
                    bound++;
                }
                else
                {
                    warnings?.Add(Unresolved(key, track, sub));
                }
            }

            return bound;
        }

        static string Unresolved(string key, TrackAsset track, TimelineAsset sub) =>
            $"Unresolved binding key '{key}' for track '{track.name}' in sub-timeline '{sub.name}'.";

        /// <summary>The manifest keys a track is resolved against, in priority order. With a
        /// <c>segment.bindingKey</c> override: <c>"&lt;key&gt;/&lt;track&gt;"</c> (per-track) then the bare
        /// <c>"&lt;key&gt;"</c> (whole-sub, back-compat); otherwise just the track name. The FIRST entry is
        /// the recommended key to author. Shared by the runtime applier and the binding-plan inspector so
        /// the two never diverge.</summary>
        internal static IEnumerable<string> CandidateKeys(SubTimelineSegment segment, string trackName)
        {
            if (segment != null && !string.IsNullOrEmpty(segment.bindingKey))
            {
                yield return segment.bindingKey + "/" + trackName;
                yield return segment.bindingKey;
            }
            else
            {
                yield return trackName;
            }
        }

        /// <summary>Resolve a track's binding target. <paramref name="key"/> returns the key that resolved,
        /// or — when nothing resolves — the recommended key to author.</summary>
        static Object Resolve(CompiledBindings bindings, SubTimelineSegment segment, TrackAsset track,
            Scene scene, out string key)
        {
            string first = null;
            foreach (var candidate in CandidateKeys(segment, track.name))
            {
                first ??= candidate;
                var t = bindings != null ? bindings.Resolve(candidate) : null;
                if (t != null)
                {
                    key = candidate;
                    return t;
                }
            }

            key = first ?? track.name; // recommended key for the warning / suggestion

            if (scene.IsValid())
            {
                bool hasOverride = segment != null && !string.IsNullOrEmpty(segment.bindingKey);
                return FindInScene(scene, hasOverride ? segment.bindingKey : track.name, track);
            }
            return null;
        }

        /// <summary>Find a GameObject named <paramref name="name"/> in <paramref name="scene"/> and return
        /// the component the track binds to (or the GameObject itself when the track binds to a GameObject).</summary>
        static Object FindInScene(Scene scene, string name, TrackAsset track)
        {
            if (string.IsNullOrEmpty(name) || !scene.IsValid())
                return null;

            GameObject match = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                match = root.name == name ? root : FindChild(root.transform, name);
                if (match != null)
                    break;
            }
            if (match == null)
                return null;

            var bindingType = BindingTypeOf(track);
            if (bindingType == null || bindingType == typeof(GameObject))
                return match;
            return match.GetComponent(bindingType);
        }

        static GameObject FindChild(Transform t, string name)
        {
            for (int i = 0; i < t.childCount; i++)
            {
                var c = t.GetChild(i);
                if (c.name == name)
                    return c.gameObject;
                var deeper = FindChild(c, name);
                if (deeper != null)
                    return deeper;
            }
            return null;
        }

        /// <summary>The scene-object type a track binds to (its <c>[TrackBindingType]</c>), or null when the
        /// track has none (e.g. ControlTrack/PlayableTrack). Shared with the binding-plan inspector.</summary>
        internal static System.Type BindingTypeOf(TrackAsset track)
        {
            var attr = (TrackBindingTypeAttribute)System.Attribute.GetCustomAttribute(
                track.GetType(), typeof(TrackBindingTypeAttribute));
            return attr?.type;
        }

        /// <summary>Wire a nested ControlTrack's clips: point each clip's exposed source-GameObject
        /// reference at <paramref name="target"/> (resolved to a GameObject) on the host director.</summary>
        static int WireControlTrack(PlayableDirector hostDir, ControlTrack track, Object target)
        {
            var go = target as GameObject ?? (target as Component)?.gameObject;
            if (go == null)
                return 0;

            int n = 0;
            foreach (var clip in track.GetClips())
            {
                if (clip.asset is ControlPlayableAsset cpa &&
                    !PropertyName.IsNullOrEmpty(cpa.sourceGameObject.exposedName))
                {
                    hostDir.SetReferenceValue(cpa.sourceGameObject.exposedName, go);
                    n++;
                }
            }
            return n;
        }
    }
}
