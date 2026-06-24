using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

namespace TimelineSmash.Tests
{
    /// <summary>
    /// Helpers that create throwaway TimelineSmash assets under a temp project folder. Tests call
    /// <see cref="EnsureRoot"/> in setup and <see cref="Cleanup"/> in teardown. The folder is also
    /// gitignored, so stray runs never dirty the repo.
    /// </summary>
    public static class TestAssets
    {
        public const string Root = "Assets/__TimelineSmashTests__";
        public const string Generated = Root + "/Generated";

        public static void EnsureRoot()
        {
            if (!AssetDatabase.IsValidFolder(Root))
                AssetDatabase.CreateFolder("Assets", "__TimelineSmashTests__");
        }

        public static void Cleanup()
        {
            if (AssetDatabase.IsValidFolder(Root))
                AssetDatabase.DeleteAsset(Root);
        }

        /// <summary>A sub-timeline asset with one AnimationTrack of the given name.</summary>
        public static TimelineAsset CreateSubTimeline(string assetName, string trackName = "Track")
        {
            EnsureRoot();
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(timeline, $"{Root}/{assetName}.playable");
            timeline.CreateTrack<AnimationTrack>(null, trackName);
            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssets();
            return timeline;
        }

        public static SubTimelineSegment Seg(
            TimelineAsset sub, string lane, double start, double duration,
            string bindingKey = null, double clipIn = 0, double speed = 1)
        {
            return new SubTimelineSegment
            {
                subTimeline = sub,
                laneName = lane,
                start = start,
                duration = duration,
                bindingKey = bindingKey ?? "",
                clipIn = clipIn,
                speed = speed,
            };
        }

        public static ContributorSegmentSet CreateContributor(string owner, IEnumerable<SubTimelineSegment> segments)
        {
            EnsureRoot();
            var set = ScriptableObject.CreateInstance<ContributorSegmentSet>();
            set.owner = owner;
            set.segments.AddRange(segments);
            AssetDatabase.CreateAsset(set, $"{Root}/Contributor_{owner}.asset");
            AssetDatabase.SaveAssets();
            return set;
        }

        public static BindingManifest CreateManifest(params (string key, Object target)[] entries)
        {
            EnsureRoot();
            var manifest = ScriptableObject.CreateInstance<BindingManifest>();
            foreach (var (key, target) in entries)
                manifest.entries.Add(new BindingManifest.Entry { key = key, target = target });
            AssetDatabase.CreateAsset(manifest, $"{Root}/Manifest.asset");
            AssetDatabase.SaveAssets();
            return manifest;
        }

        public static CinematicComposition CreateComposition(
            string name, BindingManifest manifest, params ContributorSegmentSet[] contributors)
        {
            EnsureRoot();
            var comp = ScriptableObject.CreateInstance<CinematicComposition>();
            comp.cinematicName = name;
            comp.bindingManifest = manifest;
            comp.contributors.AddRange(contributors);
            comp.outputFolder = Generated;
            AssetDatabase.CreateAsset(comp, $"{Root}/{name}.asset");
            AssetDatabase.SaveAssets();
            return comp;
        }

        // --- Rich content helpers (keyframed motion + assorted track types) -----------------------
        // The base CreateSubTimeline gives a bare, empty AnimationTrack. These build sub-timelines with
        // actual content so the assemble → bind → evaluate pipeline can be exercised end to end.

        /// <summary>Author an in-memory AnimationClip that slides along +X and spins about +Y over its
        /// duration, and add it to <paramref name="track"/> as a TimelineClip. The clip drives the bound
        /// Animator's own transform (path ""), using Timeline's canonical curve bindings
        /// (<c>m_LocalPosition.*</c> / <c>localEulerAnglesRaw.*</c>).
        /// The clip is intentionally NOT written to disk: any AssetDatabase write to the .playable mid-build
        /// (AddObjectToAsset, or a separate CreateAsset) reimports it and fake-nulls the in-memory
        /// TimelineAsset, leaving GetOutputTracks empty. Tests evaluate the live instance, so persisted
        /// curves aren't needed.</summary>
        public static AnimationClip AddTransformClip(
            AnimationTrack track, double start, double duration, float slideX = 3f, float spinY = 90f)
        {
            float d = (float)duration;
            var clip = new AnimationClip { name = "Move" };

            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.x"),
                AnimationCurve.Linear(0f, 0f, d, slideX));
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.y"),
                AnimationCurve.Constant(0f, d, 0f));
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.z"),
                AnimationCurve.Constant(0f, d, 0f));

            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("", typeof(Transform), "localEulerAnglesRaw.x"),
                AnimationCurve.Constant(0f, d, 0f));
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("", typeof(Transform), "localEulerAnglesRaw.y"),
                AnimationCurve.Linear(0f, 0f, d, spinY));
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("", typeof(Transform), "localEulerAnglesRaw.z"),
                AnimationCurve.Constant(0f, d, 0f));

            var tc = track.CreateClip(clip);
            tc.start = start;
            tc.duration = duration;

            return clip;
        }

        /// <summary>A sub-timeline with one AnimationTrack carrying a real translate+rotate clip.</summary>
        public static TimelineAsset CreateAnimatedSubTimeline(string assetName, string trackName = "Body")
        {
            EnsureRoot();
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(timeline, $"{Root}/{assetName}.playable");
            var track = timeline.CreateTrack<AnimationTrack>(null, trackName);
            AddTransformClip(track, 0, 1);
            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssets();
            return timeline;
        }

        /// <summary>A sub-timeline mixing three differently-bound track types in one asset:
        /// an AnimationTrack (→Animator), an AudioTrack (→AudioSource), and an ActivationTrack (→GameObject).
        /// This is what exposes the all-or-nothing <c>bindingKey</c> override.</summary>
        public static TimelineAsset CreateMultiTrackSubTimeline(
            string assetName, string animName = "Body", string audioName = "Voice", string activationName = "Prop")
        {
            EnsureRoot();
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(timeline, $"{Root}/{assetName}.playable");

            var anim = timeline.CreateTrack<AnimationTrack>(null, animName);
            AddTransformClip(anim, 0, 1);

            var audio = timeline.CreateTrack<AudioTrack>(null, audioName);
            var beep = AudioClip.Create("Beep", 44100, 1, 44100, false);
            var audioClip = audio.CreateClip(beep);
            audioClip.start = 0;
            audioClip.duration = 1;

            var activation = timeline.CreateTrack<ActivationTrack>(null, activationName);
            var actClip = activation.CreateDefaultClip();
            actClip.start = 0;
            actClip.duration = 1;

            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssets();
            return timeline;
        }
    }
}
