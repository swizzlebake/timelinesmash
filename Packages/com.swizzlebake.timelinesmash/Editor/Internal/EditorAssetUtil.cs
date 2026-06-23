using System.IO;
using UnityEditor;

namespace TimelineSmash.Editor
{
    /// <summary>Small AssetDatabase helpers shared by the assembler and stage builder.</summary>
    public static class EditorAssetUtil
    {
        /// <summary>Ensure a project-relative folder (e.g. "Assets/Cinematics/Generated") exists,
        /// creating intermediate folders as needed.</summary>
        public static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder))
                return;

            folder = folder.Replace('\\', '/').TrimEnd('/');
            if (folder.Length == 0 || AssetDatabase.IsValidFolder(folder))
                return;

            var parent = Path.GetDirectoryName(folder);
            parent = string.IsNullOrEmpty(parent) ? "Assets" : parent.Replace('\\', '/');
            var leaf = Path.GetFileName(folder);

            EnsureFolder(parent);
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
