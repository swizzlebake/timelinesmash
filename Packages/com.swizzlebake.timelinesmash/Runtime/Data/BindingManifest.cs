using System.Collections.Generic;
using UnityEngine;

namespace TimelineSmash
{
    /// <summary>
    /// Maps logical names to shared scene actors (cameras, characters, lights). Keeping bindings
    /// here — instead of on a scene's PlayableDirector — is what keeps the merge-prone scene file
    /// out of the collaboration loop. Resolved onto host directors at assemble time.
    /// </summary>
    [CreateAssetMenu(
        fileName = "BindingManifest",
        menuName = "TimelineSmash/Binding Manifest",
        order = 103)]
    public class BindingManifest : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            [Tooltip("Logical name referenced by a sub-timeline track name (or a segment's binding key).")]
            public string key;

            [Tooltip("The shared scene actor this key resolves to (Animator, GameObject, etc.).")]
            public Object target;
        }

        public List<Entry> entries = new List<Entry>();

        /// <summary>Resolve a logical key to its scene actor, or null when unmapped/empty.</summary>
        public Object Resolve(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e != null && e.key == key)
                    return e.target;
            }
            return null;
        }
    }
}
