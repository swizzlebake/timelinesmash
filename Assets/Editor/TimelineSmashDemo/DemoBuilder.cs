using System.Text;
using TimelineSmash;
using TimelineSmash.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Timeline;

namespace TimelineSmashDemo
{
    /// <summary>
    /// Builds a self-contained, playable demo cinematic that exercises the full breadth of timeline
    /// track types through TimelineSmash: ControlTracks in the generated master, plus AnimationTracks
    /// (translation + rotation), an AudioTrack and an ActivationTrack inside the per-artist sub-timelines.
    ///
    /// The key trick is the actor/stage-scene friction work-around: <see cref="StageSceneBuilder.BuildStage"/>
    /// regenerates an EMPTY scene, but binding targets must be live scene objects. So this builder creates
    /// the scene, instantiates the actors first, then populates the master + host directors into that SAME
    /// scene via the public <see cref="StageSceneBuilder.Populate"/> — so every binding resolves and the
    /// saved scene plays on enter-play.
    /// </summary>
    public static class DemoBuilder
    {
        const string DemoRoot = "Assets/TimelineSmashDemo";
        const string SubDir = DemoRoot + "/SubTimelines";
        const string GenDir = DemoRoot + "/Generated";
        const string ScenePath = DemoRoot + "/Demo_Stage.unity";
        const double Length = 3.0;

        [MenuItem("Tools/TimelineSmash/Build Elaborate Demo Scene")]
        public static void BuildDemo()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return; // user cancelled — don't blow away their open scene

            EditorAssetUtil.EnsureFolder(SubDir);
            EditorAssetUtil.EnsureFolder(GenDir);

            // 1. Create the scene FIRST. A freshly-built TimelineAsset goes fake-null if a scene operation
            //    runs after it is created, so the sub-timelines must be authored while this scene exists.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 2. Author the per-artist sub-timelines with real, keyframed content.
            var heroSub = BuildHeroSub();
            var camSub = BuildCameraSub();

            // 3. Contributors + composition (the committed, merge-friendly source of truth).
            var alice = CreateContributor("Alice (character)", "Characters", heroSub);
            var bob = CreateContributor("Bob (camera)", "Camera", camSub);
            var comp = CreateComposition("ElaborateDemo", alice, bob);

            // 4. The actors the bindings point at.
            var hero = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hero.name = "Hero";
            var heroAnimator = hero.AddComponent<Animator>();
            var speaker = hero.AddComponent<AudioSource>();

            var prop = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            prop.name = "Prop";
            prop.transform.position = new Vector3(0f, 2f, 0f);

            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            camGO.AddComponent<Camera>();
            var camAnimator = camGO.AddComponent<Animator>();

            var lightGO = new GameObject("Directional Light");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // 5. The manifest maps logical keys to the LIVE scene actors. It stays in memory — an .asset
            //    cannot serialize a scene reference (this is finding C). BindingCompiler reads it directly.
            var manifest = ScriptableObject.CreateInstance<BindingManifest>();
            manifest.entries.Add(new BindingManifest.Entry { key = "Body", target = heroAnimator });
            manifest.entries.Add(new BindingManifest.Entry { key = "Voice", target = speaker });
            manifest.entries.Add(new BindingManifest.Entry { key = "Prop", target = prop });
            manifest.entries.Add(new BindingManifest.Entry { key = "CameraRig", target = camAnimator });
            var compiled = BindingCompiler.Compile(manifest);

            // 6. Assemble the master, then drop master + host directors into the actor scene.
            var result = CinematicAssembler.BuildMaster(comp, GenDir + "/Demo_Master.playable");
            var build = StageSceneBuilder.Populate(scene, result, compiled);

            // 7. Save the playable scene.
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            if (result.warnings.Count > 0)
                Debug.LogWarning("[TimelineSmash Demo] Built with warnings:\n - " +
                                 string.Join("\n - ", result.warnings));
            Debug.Log($"[TimelineSmash Demo] Built '{ScenePath}' " +
                      $"({result.entries.Count} segments, {build.hosts.Count} host directors). " +
                      "Open the scene and press Play — Hero slides + spins, Prop blinks on, the camera pans.");
            EditorUtility.RevealInFinder(ScenePath);
        }

        // --- Sub-timeline authoring ---------------------------------------------------------------

        static TimelineAsset BuildHeroSub()
        {
            // Author the motion clip as a standalone .anim FIRST, before any timeline is in flight: creating
            // an asset reimports it, and doing that mid-timeline-build would fake-null the timeline.
            var bodyClip = MakeTransformClip("Hero_Body",
                new Vector3(-4f, 0f, 0f), new Vector3(4f, 0f, 0f), 360f);

            var tl = CreateTimeline(SubDir + "/Hero.playable");

            var body = tl.CreateTrack<AnimationTrack>(null, "Body");
            var bodyTc = body.CreateClip(bodyClip);
            bodyTc.start = 0;
            bodyTc.duration = Length;

            // An AudioTrack to demonstrate a differently-bound track type. Bound to an AudioSource but
            // silent here: audible playback needs a COMMITTED AudioClip asset (see FINDINGS.md).
            tl.CreateTrack<AudioTrack>(null, "Voice");

            var propTrack = tl.CreateTrack<ActivationTrack>(null, "Prop");
            var actClip = propTrack.CreateDefaultClip();
            actClip.start = 0.75;
            actClip.duration = 1.5;

            EditorUtility.SetDirty(tl);
            AssetDatabase.SaveAssets();
            return tl;
        }

        static TimelineAsset BuildCameraSub()
        {
            var rigClip = MakeTransformClip("Camera_Rig",
                new Vector3(-6f, 2f, -10f), new Vector3(6f, 2f, -10f), 0f);

            var tl = CreateTimeline(SubDir + "/Camera.playable");
            var rig = tl.CreateTrack<AnimationTrack>(null, "CameraRig");
            var tc = rig.CreateClip(rigClip);
            tc.start = 0;
            tc.duration = Length;

            EditorUtility.SetDirty(tl);
            AssetDatabase.SaveAssets();
            return tl;
        }

        /// <summary>Author a standalone .anim asset that translates (from→to) and spins about +Y over the
        /// cinematic length, on the bound transform. Created on its own so persisting it can't reimport
        /// (and fake-null) a timeline under construction; the timeline references it by GUID.</summary>
        static AnimationClip MakeTransformClip(string animName, Vector3 from, Vector3 to, float spinY)
        {
            float d = (float)Length;
            var clip = new AnimationClip { name = animName };

            SetCurve(clip, "m_LocalPosition.x", AnimationCurve.Linear(0f, from.x, d, to.x));
            SetCurve(clip, "m_LocalPosition.y", AnimationCurve.Linear(0f, from.y, d, to.y));
            SetCurve(clip, "m_LocalPosition.z", AnimationCurve.Linear(0f, from.z, d, to.z));
            SetCurve(clip, "localEulerAnglesRaw.x", AnimationCurve.Constant(0f, d, 0f));
            SetCurve(clip, "localEulerAnglesRaw.y", AnimationCurve.Linear(0f, 0f, d, spinY));
            SetCurve(clip, "localEulerAnglesRaw.z", AnimationCurve.Constant(0f, d, 0f));

            AssetDatabase.CreateAsset(clip, $"{SubDir}/{animName}.anim");
            return clip;
        }

        static void SetCurve(AnimationClip clip, string property, AnimationCurve curve) =>
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("", typeof(Transform), property), curve);

        // --- Data-asset authoring -----------------------------------------------------------------

        static TimelineAsset CreateTimeline(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<TimelineAsset>(path) != null)
                AssetDatabase.DeleteAsset(path);
            var tl = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(tl, path);
            return tl;
        }

        static ContributorSegmentSet CreateContributor(string owner, string lane, TimelineAsset sub)
        {
            var set = ScriptableObject.CreateInstance<ContributorSegmentSet>();
            set.owner = owner;
            set.segments.Add(new SubTimelineSegment
            {
                subTimeline = sub,
                laneName = lane,
                start = 0,
                duration = Length,
            });
            string path = DemoRoot + "/Contributor_" + Sanitize(owner) + ".asset";
            if (AssetDatabase.LoadAssetAtPath<ContributorSegmentSet>(path) != null)
                AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(set, path);
            return set;
        }

        static CinematicComposition CreateComposition(string name, params ContributorSegmentSet[] contributors)
        {
            var comp = ScriptableObject.CreateInstance<CinematicComposition>();
            comp.cinematicName = name;
            comp.contributors.AddRange(contributors);
            comp.outputFolder = GenDir;
            comp.settings.frameRate = 30;
            comp.settings.totalDuration = Length;
            // bindingManifest is left null on purpose: the demo's manifest targets scene actors and is
            // applied in memory (see step 4). A saved manifest can't carry scene references anyway.
            string path = DemoRoot + "/" + name + ".asset";
            if (AssetDatabase.LoadAssetAtPath<CinematicComposition>(path) != null)
                AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(comp, path);
            AssetDatabase.SaveAssets();
            return comp;
        }

        static string Sanitize(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
                sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            return sb.ToString();
        }
    }
}
