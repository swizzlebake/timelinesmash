using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TimelineSmash.Editor
{
    /// <summary>The flattened master lookup produced from a manifest tree at assemble time.</summary>
    public class CompiledBindings
    {
        public readonly Dictionary<string, Object> map = new Dictionary<string, Object>();
        public readonly List<BindingManifest.Entry> orderedEntries = new List<BindingManifest.Entry>();
        public readonly List<string> warnings = new List<string>();

        public int Count => map.Count;

        public Object Resolve(string key) =>
            !string.IsNullOrEmpty(key) && map.TryGetValue(key, out var v) ? v : null;
    }

    /// <summary>
    /// Compiles a <see cref="BindingManifest"/> and all of its <c>includes</c> into one master lookup.
    /// Traversal is depth-first, a manifest's own entries before its includes. <b>First definition of a
    /// key wins</b>; later duplicates are ignored and warned (naming both sources). A manifest reached
    /// via multiple include paths (diamond) is compiled once; a manifest that includes itself (cycle)
    /// is skipped with a warning.
    /// </summary>
    public static class BindingCompiler
    {
        public static CompiledBindings Compile(BindingManifest root, List<string> warnings = null)
        {
            var compiled = new CompiledBindings();
            var firstSource = new Dictionary<string, string>();
            Recurse(root, new HashSet<BindingManifest>(), new HashSet<BindingManifest>(), compiled, firstSource);

            if (warnings != null)
                warnings.AddRange(compiled.warnings);
            return compiled;
        }

        static void Recurse(BindingManifest m, HashSet<BindingManifest> path, HashSet<BindingManifest> visited,
            CompiledBindings compiled, Dictionary<string, string> firstSource)
        {
            if (m == null)
                return;
            if (path.Contains(m))
            {
                compiled.warnings.Add($"Cycle detected: binding manifest '{m.name}' includes itself; skipped.");
                return;
            }
            if (!visited.Add(m))
                return; // reached via another include path already; compile once (no warning)

            path.Add(m);

            if (m.entries != null)
            {
                foreach (var e in m.entries)
                {
                    if (e == null || string.IsNullOrEmpty(e.key))
                        continue;

                    if (compiled.map.ContainsKey(e.key))
                    {
                        compiled.warnings.Add(
                            $"Duplicate binding key '{e.key}': keeping '{firstSource[e.key]}', ignoring '{m.name}'.");
                        continue;
                    }

                    compiled.map[e.key] = e.target;
                    compiled.orderedEntries.Add(new BindingManifest.Entry { key = e.key, target = e.target });
                    firstSource[e.key] = m.name;
                }
            }

            if (m.includes != null)
            {
                foreach (var child in m.includes)
                    Recurse(child, path, visited, compiled, firstSource);
            }

            path.Remove(m);
        }

        /// <summary>Write the compiled lookup as a flat, regenerable <see cref="BindingManifest"/> asset
        /// (entries only, no includes) for inspection. Reloads by path after save.</summary>
        public static BindingManifest WriteAsset(CompiledBindings compiled, string path)
        {
            if (compiled == null || string.IsNullOrEmpty(path))
                return null;

            if (AssetDatabase.LoadAssetAtPath<BindingManifest>(path) != null)
                AssetDatabase.DeleteAsset(path);
            EditorAssetUtil.EnsureFolder(Path.GetDirectoryName(path));

            var flat = ScriptableObject.CreateInstance<BindingManifest>();
            foreach (var e in compiled.orderedEntries)
                flat.entries.Add(new BindingManifest.Entry { key = e.key, target = e.target });

            AssetDatabase.CreateAsset(flat, path);
            EditorUtility.SetDirty(flat);
            AssetDatabase.SaveAssets();
            // Return the in-memory instance: it keeps live references. A saved asset cannot serialize
            // references to *scene* objects, so the on-disk compiled asset is a best-effort inspection
            // snapshot (keys + project-asset targets); live binding application uses this returned lookup.
            return flat;
        }
    }
}
