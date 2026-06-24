using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

namespace TimelineSmash.Editor
{
    /// <summary>
    /// One-click asset scaffolding for artists: create a wired cinematic, add a contributor, or create a
    /// fresh sub-timeline and place it. These are pure asset operations (no scene work), so they are
    /// headless-testable; the menu items and inspector buttons are thin wrappers that supply names + folders.
    /// </summary>
    public static class CinematicScaffold
    {
        /// <summary>Create a <see cref="BindingManifest"/> + <see cref="CinematicComposition"/> (wired
        /// together) under <paramref name="folder"/>. Returns the composition.</summary>
        public static CinematicComposition CreateCinematic(string folder, string name)
        {
            folder = NormalizeFolder(folder);
            EditorAssetUtil.EnsureFolder(folder);

            string display = string.IsNullOrEmpty(name) ? "Cinematic" : name;
            string safe = Sanitize(display);

            var manifest = ScriptableObject.CreateInstance<BindingManifest>();
            AssetDatabase.CreateAsset(manifest,
                AssetDatabase.GenerateUniqueAssetPath($"{folder}/{safe}_Manifest.asset"));

            var comp = ScriptableObject.CreateInstance<CinematicComposition>();
            comp.cinematicName = display;
            comp.bindingManifest = manifest;
            AssetDatabase.CreateAsset(comp,
                AssetDatabase.GenerateUniqueAssetPath($"{folder}/{safe}.asset"));

            AssetDatabase.SaveAssets();
            return comp;
        }

        /// <summary>Create a <see cref="ContributorSegmentSet"/> beside <paramref name="comp"/> and add it
        /// to the composition. Returns the new set.</summary>
        public static ContributorSegmentSet AddContributor(CinematicComposition comp, string owner)
        {
            if (comp == null)
                return null;

            string folder = FolderOf(comp);
            EditorAssetUtil.EnsureFolder(folder);

            var set = ScriptableObject.CreateInstance<ContributorSegmentSet>();
            set.owner = string.IsNullOrEmpty(owner) ? "Artist" : owner;
            AssetDatabase.CreateAsset(set,
                AssetDatabase.GenerateUniqueAssetPath($"{folder}/Contributor_{Sanitize(set.owner)}.asset"));

            Undo.RecordObject(comp, "Add contributor");
            comp.contributors.Add(set);
            EditorUtility.SetDirty(comp);
            AssetDatabase.SaveAssets();
            return set;
        }

        /// <summary>Create a new sub-timeline (.playable) with one named <see cref="AnimationTrack"/> and
        /// append a segment referencing it to <paramref name="set"/> (placed after the latest segment).
        /// Returns the sub-timeline.</summary>
        public static TimelineAsset AddSubTimeline(ContributorSegmentSet set, string subName, string lane, string trackName)
        {
            if (set == null)
                return null;

            string folder = FolderOf(set);
            EditorAssetUtil.EnsureFolder(folder);

            var tl = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(tl,
                AssetDatabase.GenerateUniqueAssetPath($"{folder}/{Sanitize(string.IsNullOrEmpty(subName) ? "Shot" : subName)}.playable"));
            tl.CreateTrack<AnimationTrack>(null, string.IsNullOrEmpty(trackName) ? "Track" : trackName);
            EditorUtility.SetDirty(tl);
            AssetDatabase.SaveAssets();

            Undo.RecordObject(set, "Add sub-timeline segment");
            set.segments.Add(new SubTimelineSegment
            {
                subTimeline = tl,
                laneName = string.IsNullOrEmpty(lane) ? "Main" : lane,
                start = NextStart(set),
                duration = tl.duration > 0 ? tl.duration : 5,
            });
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            return tl;
        }

        // Append after the latest existing segment end so adds don't stack at 0.
        static double NextStart(ContributorSegmentSet set)
        {
            double end = 0;
            foreach (var seg in set.segments)
                if (seg != null)
                    end = System.Math.Max(end, seg.start + (seg.duration > 0 ? seg.duration : 0));
            return end;
        }

        static string FolderOf(Object asset)
        {
            var path = AssetDatabase.GetAssetPath(asset);
            return string.IsNullOrEmpty(path) ? "Assets" : System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        }

        static string NormalizeFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder))
                return "Assets";
            folder = folder.Replace('\\', '/').TrimEnd('/');
            return folder.Length == 0 ? "Assets" : folder;
        }

        static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "Unnamed";
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s)
                sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            return sb.ToString();
        }
    }
}
