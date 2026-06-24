using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TimelineSmash.Editor
{
    /// <summary>
    /// Turns a set of segments into a reusable sub-composition ("group") that can be referenced from
    /// anywhere. Extraction rebases the children's start times so the flattened result is unchanged —
    /// grouping is semantically transparent (proven by the spike): the only difference is that the
    /// segments now live in one movable asset.
    /// </summary>
    public static class GroupingService
    {
        /// <summary>Extract <paramref name="indices"/> from <paramref name="set"/> into a new sub-composition
        /// asset, and replace them in the set with one segment referencing it. Returns the new group, or
        /// null on invalid input.</summary>
        public static CinematicComposition GroupSegments(
            ContributorSegmentSet set, IList<int> indices, string groupName, string folder)
        {
            if (set == null || indices == null || indices.Count == 0)
                return null;

            var ordered = indices.Distinct().Where(i => i >= 0 && i < set.segments.Count).OrderBy(i => i).ToList();
            var selected = ordered.Select(i => set.segments[i]).Where(s => s != null).ToList();
            if (selected.Count == 0)
                return null;

            double minStart = selected.Min(s => s.start);
            EditorAssetUtil.EnsureFolder(folder);
            string baseName = SanitizeFileName(groupName);

            // Create the group composition asset first, then add its contributor set as a sub-asset.
            var group = ScriptableObject.CreateInstance<CinematicComposition>();
            group.cinematicName = groupName;
            AssetDatabase.CreateAsset(group, $"{folder}/{baseName}.asset");

            var groupSet = ScriptableObject.CreateInstance<ContributorSegmentSet>();
            groupSet.name = baseName + "_Segments";
            groupSet.owner = string.IsNullOrEmpty(set.owner) ? "Group" : set.owner;
            foreach (var s in selected)
            {
                groupSet.segments.Add(new SubTimelineSegment
                {
                    subTimeline = s.subTimeline,
                    subComposition = s.subComposition,
                    laneName = s.laneName,
                    bindingKey = s.bindingKey,
                    start = s.start - minStart, // rebase so flattening stays transparent
                    duration = s.duration,
                    clipIn = s.clipIn,
                    speed = s.speed,
                });
            }
            AssetDatabase.AddObjectToAsset(groupSet, group);
            group.contributors.Add(groupSet);

            // Replace the selected segments in the original set with one reference to the group.
            foreach (var i in ordered.AsEnumerable().Reverse())
                set.segments.RemoveAt(i);

            set.segments.Insert(ordered[0], new SubTimelineSegment
            {
                subComposition = group,
                laneName = "",   // empty lane → the group's lanes merge into the parent (transparent)
                start = minStart,
                duration = 0,
                clipIn = 0,
                speed = 1,
            });

            EditorUtility.SetDirty(group);
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            return group;
        }

        static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "Group";
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Replace(' ', '_');
        }
    }
}
