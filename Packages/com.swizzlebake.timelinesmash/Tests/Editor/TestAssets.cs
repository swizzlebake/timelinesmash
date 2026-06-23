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
    }
}
