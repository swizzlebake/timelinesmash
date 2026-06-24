using System.Linq;
using NUnit.Framework;
using TimelineSmash.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

namespace TimelineSmash.Tests
{
    /// <summary>
    /// An elaborate exercise of the assemble → compile-bindings → stage pipeline across many timeline
    /// track types (Animation with translation+rotation, Audio, Activation, Signal, and a nested
    /// Control track). Two tiers of verification:
    ///   • structural — assert the generated master wiring and the per-track host bindings;
    ///   • behavioural — evaluate a host director and assert a bound Transform actually moves.
    /// Two tests intentionally pin down current limitations (they pass by asserting the gap).
    /// </summary>
    public class PlaybackEvaluationTests
    {
        const string MasterPath = TestAssets.Generated + "/Cine_Master.playable";
        Scene _scene;

        [SetUp]
        public void SetUp()
        {
            // Create the working scene FIRST. A freshly-created TimelineAsset goes fake-null if a scene
            // operation runs after it is built (its m_Tracks → null, GetOutputTracks → empty), so every
            // sub-timeline these tests build must be created while this scene already exists.
            _scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [TearDown]
        public void TearDown()
        {
            // Replace the built scene (destroying test GameObjects) so cleanup never touches an open scene.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            TestAssets.Cleanup();
        }

        // Manifests that point at *scene* objects must stay in memory — an .asset can't serialize a
        // scene reference. BindingCompiler reads the live in-memory entries.
        static CompiledBindings CompileInMemory(params (string key, Object target)[] entries)
        {
            var manifest = ScriptableObject.CreateInstance<BindingManifest>();
            foreach (var (key, target) in entries)
                manifest.entries.Add(new BindingManifest.Entry { key = key, target = target });
            return BindingCompiler.Compile(manifest);
        }

        GameObject NewActor(string name)
        {
            var go = new GameObject(name);
            if (_scene.IsValid())
                SceneManager.MoveGameObjectToScene(go, _scene);
            return go;
        }

        // --- Tier B: real playback proves the bound transform translates and rotates ---------------

        [Test]
        public void HostEvaluation_MovesBoundTransform()
        {
            var sub = TestAssets.CreateAnimatedSubTimeline("Mover", "Body");

            var actor = NewActor("Actor");
            var animator = actor.AddComponent<Animator>();

            var host = NewActor("Host").AddComponent<PlayableDirector>();
            host.playableAsset = sub;
            host.SetGenericBinding(sub.GetOutputTracks().First(), animator);

            host.RebuildGraph();
            host.time = 0;
            host.Evaluate();
            var p0 = actor.transform.localPosition;
            var r0 = actor.transform.localRotation;

            host.time = 0.9;
            host.Evaluate();
            var p1 = actor.transform.localPosition;
            var r1 = actor.transform.localRotation;

            Assert.Greater((p1 - p0).magnitude, 0.5f, "Bound transform did not translate.");
            Assert.Greater(Quaternion.Angle(r0, r1), 30f, "Bound transform did not rotate.");

            host.Stop();
        }

        // --- Tier A: the generated master wires control tracks and host bindings correctly ----------

        [Test]
        public void Master_StructuralWiring_IsCorrect()
        {
            var hero = TestAssets.CreateAnimatedSubTimeline("Hero", "Body");
            var cam = TestAssets.CreateAnimatedSubTimeline("Cam", "Body");
            var alice = TestAssets.CreateContributor("Alice", new[] { TestAssets.Seg(hero, "Characters", 0, 1) });
            var bob = TestAssets.CreateContributor("Bob", new[] { TestAssets.Seg(cam, "Camera", 0, 1) });
            var comp = TestAssets.CreateComposition("Cine", null, alice, bob);

            var result = CinematicAssembler.BuildMaster(comp, MasterPath);

            // The master is nothing but one ControlTrack per lane.
            var tracks = result.master.GetOutputTracks().ToList();
            Assert.IsTrue(tracks.All(t => t is ControlTrack));
            CollectionAssert.AreEquivalent(new[] { "Camera", "Characters" }, tracks.Select(t => t.name));

            foreach (var e in result.entries)
            {
                Assert.IsTrue(e.control.updateDirector);
                Assert.IsFalse(e.control.active);
                Assert.IsFalse(e.control.searchHierarchy);
                StringAssert.StartsWith("TS_", e.exposedName);
            }

            // Populate master + hosts into the scene that already holds the shared actor.
            var animator = NewActor("Hero_Actor").AddComponent<Animator>();
            var compiled = CompileInMemory(("Body", animator));

            var build = StageSceneBuilder.Populate(_scene, result, compiled);

            Assert.AreEqual(result.entries.Count, build.hosts.Count);
            foreach (var e in result.entries)
            {
                var go = build.masterDir.GetReferenceValue(e.exposedName, out bool valid) as GameObject;
                Assert.IsTrue(valid, $"Exposed name '{e.exposedName}' did not resolve.");
                Assert.IsNotNull(go);
            }

            // Each host's "Body" AnimationTrack is bound to the shared Animator.
            foreach (var host in build.hosts)
            {
                var body = ((TimelineAsset)host.playableAsset).GetOutputTracks().First(t => t.name == "Body");
                Assert.AreSame(animator, host.GetGenericBinding(body));
            }
        }

        // --- Multi-track sub-timeline: each track binds by its own name to a differently-typed actor -

        [Test]
        public void MultiTrack_BindByTrackName_BindsEachTrack()
        {
            var sub = TestAssets.CreateMultiTrackSubTimeline("HeroMulti");
            var alice = TestAssets.CreateContributor("Alice", new[] { TestAssets.Seg(sub, "Characters", 0, 1) });
            var comp = TestAssets.CreateComposition("Cine", null, alice);
            var result = CinematicAssembler.BuildMaster(comp, MasterPath);

            var animator = NewActor("Body_Actor").AddComponent<Animator>();
            var audio = NewActor("Voice_Actor").AddComponent<AudioSource>();
            var prop = NewActor("Prop_Actor");

            var compiled = CompileInMemory(("Body", animator), ("Voice", audio), ("Prop", prop));
            var build = StageSceneBuilder.Populate(_scene, result, compiled);

            var host = build.hosts.Single();
            var hostSub = (TimelineAsset)host.playableAsset;
            var body = hostSub.GetOutputTracks().First(t => t.name == "Body");
            var voice = hostSub.GetOutputTracks().First(t => t.name == "Voice");
            var propTrack = hostSub.GetOutputTracks().First(t => t.name == "Prop");

            Assert.AreSame(animator, host.GetGenericBinding(body));
            Assert.AreSame(audio, host.GetGenericBinding(voice));
            Assert.AreSame(prop, host.GetGenericBinding(propTrack));
            Assert.IsEmpty(result.warnings);
        }

        // --- FIX A: a bindingKey override can retarget each track via "<key>/<trackName>" ------------

        [Test]
        public void BindingKeyOverride_BareKey_BindsWholeSub()
        {
            // Back-compat: a bare bindingKey still maps EVERY track of the sub-timeline to one target.
            var sub = TestAssets.CreateMultiTrackSubTimeline("HeroMulti");
            var alice = TestAssets.CreateContributor("Alice",
                new[] { TestAssets.Seg(sub, "Characters", 0, 1, bindingKey: "hero") });
            var comp = TestAssets.CreateComposition("Cine", null, alice);
            var result = CinematicAssembler.BuildMaster(comp, MasterPath);

            var animator = NewActor("Hero_Actor").AddComponent<Animator>();
            var compiled = CompileInMemory(("hero", animator));
            var build = StageSceneBuilder.Populate(_scene, result, compiled);

            var host = build.hosts.Single();
            var hostSub = (TimelineAsset)host.playableAsset;
            foreach (var name in new[] { "Body", "Voice", "Prop" })
            {
                var track = hostSub.GetOutputTracks().First(t => t.name == name);
                Assert.AreSame(animator, host.GetGenericBinding(track),
                    $"Track '{name}' should fall back to the bare override key 'hero'.");
            }
        }

        [Test]
        public void BindingKeyOverride_NamespacedKey_RetargetsEachTrack()
        {
            // FIX: "<bindingKey>/<trackName>" entries retarget tracks individually — the AudioTrack now
            // reaches its own AudioSource even though the segment uses a bindingKey override.
            var sub = TestAssets.CreateMultiTrackSubTimeline("HeroMulti");
            var alice = TestAssets.CreateContributor("Alice",
                new[] { TestAssets.Seg(sub, "Characters", 0, 1, bindingKey: "hero") });
            var comp = TestAssets.CreateComposition("Cine", null, alice);
            var result = CinematicAssembler.BuildMaster(comp, MasterPath);

            var animator = NewActor("Body_Actor").AddComponent<Animator>();
            var audio = NewActor("Voice_Actor").AddComponent<AudioSource>();
            var prop = NewActor("Prop_Actor");

            var compiled = CompileInMemory(("hero/Body", animator), ("hero/Voice", audio), ("hero/Prop", prop));
            var build = StageSceneBuilder.Populate(_scene, result, compiled);

            var host = build.hosts.Single();
            var hostSub = (TimelineAsset)host.playableAsset;
            Assert.AreSame(animator, host.GetGenericBinding(hostSub.GetOutputTracks().First(t => t.name == "Body")));
            Assert.AreSame(audio, host.GetGenericBinding(hostSub.GetOutputTracks().First(t => t.name == "Voice")));
            Assert.AreSame(prop, host.GetGenericBinding(hostSub.GetOutputTracks().First(t => t.name == "Prop")));
            Assert.IsEmpty(result.warnings);
        }

        // --- FIX B: a ControlTrack nested inside a sub-timeline is wired through the host director ----

        [Test]
        public void NestedControlTrack_SourceIsWired()
        {
            TestAssets.EnsureRoot();
            const string subPath = TestAssets.Root + "/Nested.playable";
            var sub = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(sub, subPath);
            var nested = sub.CreateTrack<ControlTrack>(null, "Nested");
            var clip = nested.CreateClip<ControlPlayableAsset>();
            ((ControlPlayableAsset)clip.asset).sourceGameObject =
                new ExposedReference<GameObject> { exposedName = "INNER_REF" };
            clip.start = 0;
            clip.duration = 1;
            EditorUtility.SetDirty(sub);
            AssetDatabase.SaveAssets();

            var alice = TestAssets.CreateContributor("Alice", new[] { TestAssets.Seg(sub, "Characters", 0, 1) });
            var comp = TestAssets.CreateComposition("Cine", null, alice);
            var result = CinematicAssembler.BuildMaster(comp, MasterPath);

            var target = NewActor("Inner_Target");
            // The manifest maps the nested ControlTrack's name to a GameObject; the applier now resolves
            // the clip's exposed source reference on the host director instead of leaving it null.
            var compiled = CompileInMemory(("Nested", target));
            var build = StageSceneBuilder.Populate(_scene, result, compiled);

            var host = build.hosts.Single();
            var hostSub = (TimelineAsset)host.playableAsset;
            var nestedControl = (ControlPlayableAsset)hostSub.GetOutputTracks()
                .OfType<ControlTrack>().First().GetClips().First().asset;

            var resolved = nestedControl.sourceGameObject.Resolve(host);
            Assert.AreSame(target, resolved, "Nested ControlTrack source should resolve to the manifest target.");
            Assert.IsEmpty(result.warnings);
        }

        // --- Signal track binds to a SignalReceiver by track name -----------------------------------

        [Test]
        public void SignalTrack_BindsToReceiver()
        {
            TestAssets.EnsureRoot();
            // A standalone SignalAsset, created BEFORE the .playable, keeps the emitter's reference
            // persistent without AddObjectToAsset (which would reimport the .playable and fake-null `sub`).
            var signalAsset = ScriptableObject.CreateInstance<SignalAsset>();
            AssetDatabase.CreateAsset(signalAsset, TestAssets.Root + "/Signal.asset");

            const string subPath = TestAssets.Root + "/Signaler.playable";
            var sub = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(sub, subPath);
            var sigTrack = sub.CreateTrack<SignalTrack>(null, "Signals");
            var emitter = sigTrack.CreateMarker<SignalEmitter>(0.5);
            emitter.asset = signalAsset;
            EditorUtility.SetDirty(sub);
            AssetDatabase.SaveAssets();

            var alice = TestAssets.CreateContributor("Alice", new[] { TestAssets.Seg(sub, "Fx", 0, 1) });
            var comp = TestAssets.CreateComposition("Cine", null, alice);
            var result = CinematicAssembler.BuildMaster(comp, MasterPath);

            var receiver = NewActor("Signal_Receiver").AddComponent<SignalReceiver>();
            var compiled = CompileInMemory(("Signals", receiver));
            var build = StageSceneBuilder.Populate(_scene, result, compiled);

            var host = build.hosts.Single();
            var boundTrack = ((TimelineAsset)host.playableAsset).GetOutputTracks().OfType<SignalTrack>().First();
            Assert.AreSame(receiver, host.GetGenericBinding(boundTrack));
            Assert.IsEmpty(result.warnings);
        }

        // --- FIX C: assemble into a live actor scene; resolve unmapped keys by actor name ------------

        [Test]
        public void Populate_ResolvesUnmappedKey_BySceneObjectName()
        {
            var sub = TestAssets.CreateAnimatedSubTimeline("Mover", "Body");
            var alice = TestAssets.CreateContributor("Alice", new[] { TestAssets.Seg(sub, "Characters", 0, 1) });
            var comp = TestAssets.CreateComposition("Cine", null, alice);
            var result = CinematicAssembler.BuildMaster(comp, MasterPath);

            // An actor named after the track lives in the scene; the manifest has no entry for it.
            var animator = NewActor("Body").AddComponent<Animator>();
            var compiled = CompileInMemory(); // empty

            var build = StageSceneBuilder.Populate(_scene, result, compiled, resolveBySceneName: true);

            var host = build.hosts.Single();
            var body = ((TimelineAsset)host.playableAsset).GetOutputTracks().First(t => t.name == "Body");
            Assert.AreSame(animator, host.GetGenericBinding(body),
                "An unmapped key should resolve to the same-named scene actor's bound component.");
            Assert.IsEmpty(result.warnings);
        }

        [Test]
        public void AssembleIntoActiveScene_IsIdempotent_AndBindsByName()
        {
            var sub = TestAssets.CreateAnimatedSubTimeline("Mover", "Body");
            var alice = TestAssets.CreateContributor("Alice", new[] { TestAssets.Seg(sub, "Characters", 0, 1) });
            var comp = TestAssets.CreateComposition("Cine", null, alice); // null manifest — actors found by name
            var animator = NewActor("Body").AddComponent<Animator>();

            CinematicAssembleService.AssembleIntoActiveScene(comp);
            CinematicAssembleService.AssembleIntoActiveScene(comp); // re-run must not stack a second master

            int masters = _scene.GetRootGameObjects().Count(go => go.name == "Cinematic_Master");
            Assert.AreEqual(1, masters, "Re-assembling into the active scene should replace, not duplicate, the master.");

            var host = _scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<PlayableDirector>())
                .Single(d => d.gameObject.name.StartsWith("Host_"));
            var body = ((TimelineAsset)host.playableAsset).GetOutputTracks().First(t => t.name == "Body");
            Assert.AreSame(animator, host.GetGenericBinding(body));
        }
    }
}
