using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

namespace TimelineSmash.Editor
{
    /// <summary>
    /// Introspects every contributor sub-timeline in a composition and reports, per track, which binding
    /// key it resolves against and whether the manifest provides a target — turning silent binding failures
    /// into a checklist. Mirrors <see cref="CinematicOverviewModel"/>: a pure model built from the same
    /// flattened leaves and compiled manifest the assembler uses, so it never disagrees with assemble.
    /// </summary>
    public class BindingPlan
    {
        /// <summary>One track of one placed sub-timeline that needs a scene-object binding.</summary>
        public class Requirement
        {
            public string owner;
            public string lane;
            public string subTimelineName;
            public string trackName;
            public System.Type bindingType;  // scene-object type the track binds to; null for control/playable
            public bool isControl;           // ControlTrack: binds a GameObject through an exposed reference
            public string resolvedKey;       // the manifest key that resolved, or null
            public Object target;            // the resolved manifest target, or null
            public string suggestedKey;      // the recommended key to author when unresolved
            public bool resolvedBySceneName; // true when the active scene supplied the target by object name

            public bool Resolved => target != null;

            public string TypeLabel =>
                isControl ? "GameObject (control)" : bindingType != null ? bindingType.Name : "—";
        }

        public readonly List<Requirement> requirements = new List<Requirement>();
        public readonly List<string> warnings = new List<string>();

        public int Total => requirements.Count;
        public int Bound => requirements.Count(r => r.Resolved);
        public int Missing => Total - Bound;
        public int SceneBound => requirements.Count(r => r.resolvedBySceneName);

        /// <summary>Build the plan from a composition: flatten the tree, compile the manifest, and resolve
        /// each output track that needs a binding against the same candidate keys the runtime applier uses.</summary>
        public static BindingPlan Build(CinematicComposition composition)
        {
            return Build(composition, default, false);
        }

        /// <summary>Build the plan and optionally preview name-based binding against a scene. Manifest
        /// entries retain priority, exactly as they do during stage assembly. A scene match counts only
        /// when the named GameObject contains the component type required by the Timeline track.</summary>
        public static BindingPlan Build(CinematicComposition composition, Scene scene,
            bool resolveBySceneName = true)
        {
            var plan = new BindingPlan();
            if (composition == null)
                return plan;

            var leaves = CinematicAssembler.FlattenTree(composition, plan.warnings);
            var compiled = BindingCompiler.Compile(composition.bindingManifest, plan.warnings);

            foreach (var leaf in leaves)
            {
                var sub = leaf.segment != null ? leaf.segment.subTimeline : null;
                if (sub == null)
                    continue;

                foreach (var track in sub.GetOutputTracks())
                {
                    if (track == null)
                        continue;

                    bool isControl = track is ControlTrack;
                    var bindingType = BindingApplier.BindingTypeOf(track);
                    if (!isControl && bindingType == null)
                        continue; // this track needs no binding (e.g. a PlayableTrack)

                    string suggestedKey = null;
                    foreach (var candidate in BindingApplier.CandidateKeys(leaf.segment, track.name))
                    {
                        suggestedKey = candidate;
                        break;
                    }

                    var target = BindingApplier.Resolve(compiled, leaf.segment, track,
                        resolveBySceneName ? scene : default, out string resolvedKey,
                        out bool resolvedBySceneName);

                    plan.requirements.Add(new Requirement
                    {
                        owner = leaf.owner,
                        lane = leaf.lane,
                        subTimelineName = sub.name,
                        trackName = track.name,
                        bindingType = bindingType,
                        isControl = isControl,
                        resolvedKey = resolvedKey,
                        target = target,
                        suggestedKey = suggestedKey ?? track.name,
                        resolvedBySceneName = resolvedBySceneName,
                    });
                }
            }

            return plan;
        }
    }
}
