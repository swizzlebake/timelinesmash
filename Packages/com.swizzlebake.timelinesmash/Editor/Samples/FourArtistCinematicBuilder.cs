using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace TimelineSmash.Editor.Samples
{
    /// <summary>
    /// Rebuilds the complete four-artist sample as editable source assets. The package ships a prebuilt
    /// copy, but this command is intentionally deterministic so teams can inspect how every source file is
    /// authored and can create a clean rehearsal copy outside the read-only package cache.
    /// </summary>
    public static class FourArtistCinematicBuilder
    {
        public const string EditableRoot = "Assets/Cinematics/FourArtistRelay";
        public const string ExportRoot = "Assets/__TimelineSmashFourArtistExport__";
        const string GeneratedRoot = "Assets/Cinematics/Generated";
        const string CompositionName = "OrbitalRelay";
        const double Duration = 45.0;

        struct SegmentSpec
        {
            public string sourcePath;
            public bool isComposition;
            public string lane;
            public string bindingKey;
            public double start;
            public double duration;
            public double clipIn;
            public double speed;
            public string spawnPrefabPath;
        }

        [MenuItem("Tools/TimelineSmash/Samples/Create Editable Four-Artist Cinematic")]
        public static void CreateEditableCopy()
        {
            bool exists = AssetDatabase.IsValidFolder(EditableRoot);
            if (exists && !EditorUtility.DisplayDialog(
                    "Rebuild Four-Artist Cinematic?",
                    $"This replaces only '{EditableRoot}'. Generated master/stage artifacts are separate.",
                    "Rebuild", "Cancel"))
                return;

            var composition = BuildAt(EditableRoot, overwrite: true);
            Selection.activeObject = composition;
            EditorGUIUtility.PingObject(composition);
            Debug.Log($"[TimelineSmash Sample] Created editable four-artist source at '{EditableRoot}'. " +
                      "Select OrbitalRelay and choose Assemble (master + stage).");
        }

        [MenuItem("Tools/TimelineSmash/Samples/Assemble Four-Artist Cinematic")]
        public static void AssembleSample()
        {
            var composition = FindComposition();
            if (composition == null)
            {
                EditorUtility.DisplayDialog("Four-Artist Cinematic",
                    "Import the Four-Artist Cinematic sample or create an editable copy first.", "OK");
                return;
            }

            Selection.activeObject = composition;
            var result = CinematicAssembleService.Assemble(composition, buildStage: true);
            if (result == null)
                return;

            string summary = result.warnings.Count == 0
                ? "with no warnings"
                : $"with {result.warnings.Count} warning(s)";
            Debug.Log($"[TimelineSmash Sample] Assembled {result.entries.Count} segments {summary}. " +
                      $"Stage: {CinematicAssembleService.StagePath(composition)}");
        }

        [MenuItem("Tools/TimelineSmash/Samples/Reset Four-Artist Generated Artifacts")]
        public static void ResetGeneratedArtifacts()
        {
            var composition = FindComposition();
            if (composition == null)
                return;

            foreach (string path in new[]
                     {
                         CinematicAssembleService.MasterPath(composition),
                         CinematicAssembleService.StagePath(composition),
                         CinematicAssembleService.BindingsPath(composition),
                     })
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                    AssetDatabase.DeleteAsset(path);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[TimelineSmash Sample] Removed only the three regenerable OrbitalRelay artifacts.");
        }

        /// <summary>Used by package maintainers to regenerate the serialized sample before release.</summary>
        public static void ExportForPackage() => BuildAt(ExportRoot, overwrite: true);

        public static CinematicComposition BuildAt(string root, bool overwrite)
        {
            root = root.Replace('\\', '/').TrimEnd('/');
            if (overwrite && AssetDatabase.IsValidFolder(root))
                AssetDatabase.DeleteAsset(root);

            EnsureFolder(root);
            foreach (string folder in new[]
                     {
                         "Shared/Audio", "Shared/Prefabs", "Shared/Signals", "Shared/Scenes",
                         "Shared/Bindings", "Artists/Alice", "Artists/Bob", "Artists/Cleo",
                         "Artists/Dev", "Artists/Dev/Finale",
                     })
                EnsureFolder($"{root}/{folder}");

            // Scene creation comes first: Unity can invalidate held TimelineAsset references when scene
            // operations occur. From this point onward the builder carries asset paths, not held timelines.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            string tonePath = $"{root}/Shared/Audio/RelayTone.wav";
            WriteToneWav(tonePath, seconds: 2.0f, frequency: 523.25f);
            AssetDatabase.ImportAsset(tonePath, ImportAssetOptions.ForceSynchronousImport);

            string signalPath = $"{root}/Shared/Signals/RelayPulse.asset";
            var signal = ScriptableObject.CreateInstance<SignalAsset>();
            AssetDatabase.CreateAsset(signal, signalPath);

            // Alice — character performance. Shot 1 deliberately mixes three track types under a
            // namespaced binding override; the other beats continue the Body performance.
            string alice1 = BuildPerformance(root, "Artists/Alice", "Alice_Beat01_Arrival", 8,
                new Vector3(-4, 0, 0), new Vector3(-1, 0, 0), 35, tonePath, includeProp: true);
            string alice2 = BuildPerformance(root, "Artists/Alice", "Alice_Beat02_Discovery", 10,
                new Vector3(-1, 0, 0), new Vector3(1, 0.4f, 0), 100);
            string alice3 = BuildPerformance(root, "Artists/Alice", "Alice_Beat03_Response", 11,
                new Vector3(1, 0.4f, 0), new Vector3(2.5f, 0, 0), 220);
            string alice4 = BuildPerformance(root, "Artists/Alice", "Alice_Beat04_Departure", 12,
                new Vector3(2.5f, 0, 0), new Vector3(7, 0, 0), 360);

            // Bob — four sequential editorial/camera passes.
            string bob1 = BuildMotion(root, "Artists/Bob", "Bob_Shot01_Establish", "Rig", 10,
                new Vector3(-10, 4, -12), new Vector3(-7, 3, -10), 8);
            string bob2 = BuildMotion(root, "Artists/Bob", "Bob_Shot02_Dolly", "Rig", 12,
                new Vector3(-7, 3, -10), new Vector3(-2, 2, -8), 20);
            string bob3 = BuildMotion(root, "Artists/Bob", "Bob_Shot03_Orbit", "Rig", 14,
                new Vector3(-2, 2, -8), new Vector3(5, 3, -9), 55);
            string bob4 = BuildMotion(root, "Artists/Bob", "Bob_Shot04_Wide", "Rig", 12,
                new Vector3(5, 3, -9), new Vector3(10, 5, -14), 85);

            // Cleo — effects beats combine motion, activation and signal markers. Two placements spawn a
            // self-contained pulse prefab for their exact segment duration.
            string cleo1 = BuildEffects(root, "Cleo_FX01_Wakeup", 6, signalPath, includeProp: true);
            string cleo2 = BuildEffects(root, "Cleo_FX02_Scan", 6, signalPath);
            string cleo3 = BuildEffects(root, "Cleo_FX03_Transfer", 7, signalPath, includeProp: true);
            string cleo4 = BuildEffects(root, "Cleo_FX04_Burst", 6, signalPath);

            // Dev — a prelude plus a nested finale composition. The group is placed on lane "Finale",
            // namespacing its Satellite/Lighting/Audio child lanes in the flattened master.
            string devPrelude = BuildMotion(root, "Artists/Dev", "Dev_PreludeLight", "KeyLight", 12,
                new Vector3(0, 5, 0), new Vector3(2, 6, 0), 45);
            string satelliteInner = BuildMotion(root, "Artists/Dev/Finale", "Dev_SatelliteInner", "Satellite", 15,
                new Vector3(-5, 3, 1), new Vector3(5, 4, -1), 720);
            string satelliteControl = BuildNestedControl(root, satelliteInner);
            string devFinalLight = BuildMotion(root, "Artists/Dev/Finale", "Dev_FinaleLight", "KeyLight", 13,
                new Vector3(2, 6, 0), new Vector3(-2, 4, 0), 160);
            string devAudio = BuildAudio(root, "Artists/Dev/Finale", "Dev_FinaleAudio", "Beacon", 15, tonePath);

            string pulsePrefabPath = BuildPulsePrefab(root);
            string actorsPrefabPath = BuildActorsPrefab(root, satelliteInner, signalPath);
            string stageScenePath = BuildStageSource(root);

            string aliceContributor = CreateContributor(root, "Artists/Alice/Alice_Contributor.asset", "Alice — Character",
                Leaf(alice1, "Character", "hero", 0, 8),
                Leaf(alice2, "Character", "hero", 10, 9, clipIn: 0.5, speed: 1.1),
                Leaf(alice3, "Character", "hero", 21, 9),
                Leaf(alice4, "Character", "hero", 33, 12));

            string bobContributor = CreateContributor(root, "Artists/Bob/Bob_Contributor.asset", "Bob — Camera & Edit",
                Leaf(bob1, "Camera", "camera", 0, 10),
                Leaf(bob2, "Camera", "camera", 10, 11, clipIn: 0.5),
                Leaf(bob3, "Camera", "camera", 21, 12, clipIn: 1, speed: 0.9),
                Leaf(bob4, "Camera", "camera", 33, 12));

            string cleoContributor = CreateContributor(root, "Artists/Cleo/Cleo_Contributor.asset", "Cleo — Props & FX",
                Leaf(cleo1, "FX", "fx", 4, 6),
                Leaf(cleo2, "FX", "fx", 14, 5, speed: 1.2, spawnPrefabPath: pulsePrefabPath),
                Leaf(cleo3, "FX", "fx", 25, 6),
                Leaf(cleo4, "FX", "fx", 37, 6, spawnPrefabPath: pulsePrefabPath));

            string finaleContributor = CreateContributor(root,
                "Artists/Dev/Finale/DevFinale_Contributor.asset", "Dev — Finale Group",
                Leaf(satelliteControl, "Satellite", "satellite", 0, 15, speed: 1.2),
                Leaf(devFinalLight, "Lighting", "dev", 2, 13),
                Leaf(devAudio, "Audio", "dev", 0, 15));
            string finaleComposition = CreateComposition(root, "Artists/Dev/Finale/DevFinale.asset", "DevFinale",
                null, null, null, 15, finaleContributor);
            string devContributor = CreateContributor(root, "Artists/Dev/Dev_Contributor.asset", "Dev — Lighting & Audio",
                Leaf(devPrelude, "Lighting", "dev", 0, 12),
                Group(finaleComposition, "Finale", 28, 1));

            string rootManifest = BuildManifests(root, actorsPrefabPath);
            string compositionPath = CreateComposition(root, "OrbitalRelay.asset", CompositionName,
                rootManifest, actorsPrefabPath, stageScenePath, Duration,
                aliceContributor, bobContributor, cleoContributor, devContributor);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return AssetDatabase.LoadAssetAtPath<CinematicComposition>(compositionPath);
        }

        static SegmentSpec Leaf(string path, string lane, string key, double start, double duration,
            double clipIn = 0, double speed = 1, string spawnPrefabPath = null) => new SegmentSpec
        {
            sourcePath = path, lane = lane, bindingKey = key, start = start, duration = duration,
            clipIn = clipIn, speed = speed, spawnPrefabPath = spawnPrefabPath,
        };

        static SegmentSpec Group(string path, string lane, double start, double speed) => new SegmentSpec
        {
            sourcePath = path, isComposition = true, lane = lane, start = start, speed = speed,
        };

        static string BuildPerformance(string root, string folder, string name, double duration,
            Vector3 from, Vector3 to, float spin, string audioPath = null, bool includeProp = false)
        {
            string timelinePath = BuildMotion(root, folder, name, "Body", duration, from, to, spin);
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelinePath);
            if (!string.IsNullOrEmpty(audioPath))
            {
                var audio = timeline.CreateTrack<AudioTrack>(null, "Voice");
                var source = AssetDatabase.LoadAssetAtPath<AudioClip>(audioPath);
                var clip = audio.CreateClip(source);
                clip.start = 0.75;
                clip.duration = Math.Min(duration - 0.75, source.length);
            }
            if (includeProp)
            {
                var prop = timeline.CreateTrack<ActivationTrack>(null, "Prop");
                var clip = prop.CreateDefaultClip();
                clip.start = 2;
                clip.duration = Math.Max(1, duration - 4);
            }
            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssets();
            return timelinePath;
        }

        static string BuildMotion(string root, string folder, string name, string trackName, double duration,
            Vector3 from, Vector3 to, float spin)
        {
            string animPath = $"{root}/{folder}/{name}.anim";
            var anim = new AnimationClip { name = name + "_Motion" };
            float d = (float)duration;
            SetCurve(anim, "m_LocalPosition.x", AnimationCurve.Linear(0, from.x, d, to.x));
            SetCurve(anim, "m_LocalPosition.y", AnimationCurve.Linear(0, from.y, d, to.y));
            SetCurve(anim, "m_LocalPosition.z", AnimationCurve.Linear(0, from.z, d, to.z));
            SetCurve(anim, "localEulerAnglesRaw.x", AnimationCurve.Constant(0, d, 0));
            SetCurve(anim, "localEulerAnglesRaw.y", AnimationCurve.Linear(0, 0, d, spin));
            SetCurve(anim, "localEulerAnglesRaw.z", AnimationCurve.Constant(0, d, 0));
            AssetDatabase.CreateAsset(anim, animPath);

            string timelinePath = $"{root}/{folder}/{name}.playable";
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(timeline, timelinePath);
            var track = timeline.CreateTrack<AnimationTrack>(null, trackName);
            var clip = track.CreateClip(anim);
            clip.start = 0;
            clip.duration = duration;
            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssets();
            return timelinePath;
        }

        static string BuildEffects(string root, string name, double duration, string signalPath, bool includeProp = false)
        {
            string path = BuildMotion(root, "Artists/Cleo", name, "Orb", duration,
                new Vector3(-3, 1, 2), new Vector3(3, 2, -1), 240);
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(path);
            var signals = timeline.CreateTrack<SignalTrack>(null, "Signals");
            var emitter = signals.CreateMarker<SignalEmitter>(duration * 0.5);
            emitter.asset = AssetDatabase.LoadAssetAtPath<SignalAsset>(signalPath);
            if (includeProp)
            {
                var prop = timeline.CreateTrack<ActivationTrack>(null, "Prop");
                var clip = prop.CreateDefaultClip();
                clip.start = 1;
                clip.duration = Math.Max(1, duration - 2);
            }
            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssets();
            return path;
        }

        static string BuildAudio(string root, string folder, string name, string trackName, double duration, string audioPath)
        {
            string path = $"{root}/{folder}/{name}.playable";
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(timeline, path);
            var track = timeline.CreateTrack<AudioTrack>(null, trackName);
            var clip = track.CreateClip(AssetDatabase.LoadAssetAtPath<AudioClip>(audioPath));
            clip.start = 0;
            clip.duration = duration;
            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssets();
            return path;
        }

        static string BuildNestedControl(string root, string innerPath)
        {
            string path = $"{root}/Artists/Dev/Finale/Dev_SatelliteControl.playable";
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(timeline, path);
            var track = timeline.CreateTrack<ControlTrack>(null, "SatelliteRig");
            var clip = track.CreateClip<ControlPlayableAsset>();
            var control = (ControlPlayableAsset)clip.asset;
            control.sourceGameObject = new ExposedReference<GameObject> { exposedName = "ORBITAL_RELAY_RIG" };
            control.updateDirector = true;
            control.updateParticle = false;
            control.updateITimeControl = false;
            control.searchHierarchy = false;
            control.active = false;
            clip.start = 0;
            clip.duration = 15;
            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssets();
            return path;
        }

        static string BuildPulsePrefab(string root)
        {
            var pulse = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pulse.name = "RelayPulse";
            pulse.transform.localScale = Vector3.one * 0.35f;
            var particles = pulse.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.duration = 1.5f;
            main.loop = true;
            main.startLifetime = 0.8f;
            main.startSpeed = 1.5f;
            string path = $"{root}/Shared/Prefabs/RelayPulse.prefab";
            PrefabUtility.SaveAsPrefabAsset(pulse, path);
            UnityEngine.Object.DestroyImmediate(pulse);
            return path;
        }

        static string BuildActorsPrefab(string root, string satelliteInnerPath, string signalPath)
        {
            var actors = new GameObject("OrbitalRelayActors");
            var hero = Primitive(PrimitiveType.Cube, "Hero", actors.transform, new Vector3(-4, 0, 0));
            hero.AddComponent<Animator>();
            new GameObject("Voice", typeof(AudioSource)).transform.SetParent(actors.transform);
            Primitive(PrimitiveType.Sphere, "Prop", actors.transform, new Vector3(0, 2, 0));
            var orb = Primitive(PrimitiveType.Capsule, "Orb", actors.transform, new Vector3(-3, 1, 2));
            orb.AddComponent<Animator>();

            var cameraObject = new GameObject("MainCamera", typeof(Camera), typeof(Animator));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(actors.transform);
            cameraObject.transform.position = new Vector3(-10, 4, -12);

            var satellite = Primitive(PrimitiveType.Cube, "Satellite", actors.transform, new Vector3(-5, 3, 1));
            satellite.transform.localScale = Vector3.one * 0.4f;
            var satelliteAnimator = satellite.AddComponent<Animator>();

            var rig = new GameObject("SatelliteRig", typeof(PlayableDirector));
            rig.transform.SetParent(actors.transform);
            var rigDirector = rig.GetComponent<PlayableDirector>();
            var inner = AssetDatabase.LoadAssetAtPath<TimelineAsset>(satelliteInnerPath);
            rigDirector.playableAsset = inner;
            rigDirector.playOnAwake = false;
            rigDirector.SetGenericBinding(inner.GetOutputTracks().First(), satelliteAnimator);

            var keyLight = new GameObject("KeyLight", typeof(Light), typeof(Animator));
            keyLight.transform.SetParent(actors.transform);
            keyLight.transform.position = new Vector3(0, 5, 0);
            keyLight.GetComponent<Light>().type = LightType.Point;
            keyLight.GetComponent<Light>().intensity = 6;

            new GameObject("Beacon", typeof(AudioSource)).transform.SetParent(actors.transform);
            var signals = new GameObject("Signals", typeof(SignalReceiver));
            signals.transform.SetParent(actors.transform);
            signals.GetComponent<SignalReceiver>().AddReaction(
                AssetDatabase.LoadAssetAtPath<SignalAsset>(signalPath), new UnityEngine.Events.UnityEvent());

            string path = $"{root}/Shared/Prefabs/OrbitalRelayActors.prefab";
            PrefabUtility.SaveAsPrefabAsset(actors, path);
            UnityEngine.Object.DestroyImmediate(actors);
            return path;
        }

        static string BuildStageSource(string root)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var floor = Primitive(PrimitiveType.Plane, "DockFloor", null, Vector3.zero);
            floor.transform.localScale = new Vector3(2.5f, 1, 2.5f);
            var back = Primitive(PrimitiveType.Cube, "DockBackdrop", null, new Vector3(0, 4, 6));
            back.transform.localScale = new Vector3(14, 8, 0.4f);
            var light = new GameObject("DockSun", typeof(Light));
            light.GetComponent<Light>().type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);
            string path = $"{root}/Shared/Scenes/OrbitalDock.unity";
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        static string BuildManifests(string root, string actorsPrefabPath)
        {
            var actors = AssetDatabase.LoadAssetAtPath<GameObject>(actorsPrefabPath);
            Transform hero = Find(actors.transform, "Hero");
            Transform camera = Find(actors.transform, "MainCamera");
            Transform voice = Find(actors.transform, "Voice");
            Transform prop = Find(actors.transform, "Prop");
            Transform orb = Find(actors.transform, "Orb");
            Transform rig = Find(actors.transform, "SatelliteRig");
            Transform keyLight = Find(actors.transform, "KeyLight");
            Transform beacon = Find(actors.transform, "Beacon");
            Transform signals = Find(actors.transform, "Signals");

            string alice = CreateManifest(root, "Shared/Bindings/Alice_Bindings.asset",
                ("hero/Body", (UnityEngine.Object)hero.GetComponent<Animator>()),
                ("hero/Voice", voice.GetComponent<AudioSource>()),
                ("hero/Prop", prop.gameObject));
            string bob = CreateManifest(root, "Shared/Bindings/Bob_Bindings.asset",
                ("camera/Rig", (UnityEngine.Object)camera.GetComponent<Animator>()));
            string cleo = CreateManifest(root, "Shared/Bindings/Cleo_Bindings.asset",
                ("fx/Orb", (UnityEngine.Object)orb.GetComponent<Animator>()),
                ("fx/Prop", prop.gameObject),
                ("fx/Signals", signals.GetComponent<SignalReceiver>()));
            string dev = CreateManifest(root, "Shared/Bindings/Dev_Bindings.asset",
                ("dev/KeyLight", (UnityEngine.Object)keyLight.GetComponent<Animator>()),
                ("dev/Beacon", beacon.GetComponent<AudioSource>()),
                ("satellite/SatelliteRig", rig.gameObject));

            var rootManifest = ScriptableObject.CreateInstance<BindingManifest>();
            foreach (string path in new[] { alice, bob, cleo, dev })
                rootManifest.includes.Add(AssetDatabase.LoadAssetAtPath<BindingManifest>(path));
            string rootPath = $"{root}/Shared/Bindings/OrbitalRelay_Bindings.asset";
            AssetDatabase.CreateAsset(rootManifest, rootPath);
            return rootPath;
        }

        static string CreateManifest(string root, string relativePath,
            params (string key, UnityEngine.Object target)[] entries)
        {
            var manifest = ScriptableObject.CreateInstance<BindingManifest>();
            foreach (var entry in entries)
                manifest.entries.Add(new BindingManifest.Entry { key = entry.key, target = entry.target });
            string path = $"{root}/{relativePath}";
            AssetDatabase.CreateAsset(manifest, path);
            return path;
        }

        static string CreateContributor(string root, string relativePath, string owner, params SegmentSpec[] specs)
        {
            var contributor = ScriptableObject.CreateInstance<ContributorSegmentSet>();
            contributor.owner = owner;
            foreach (var spec in specs)
            {
                contributor.segments.Add(new SubTimelineSegment
                {
                    subTimeline = spec.isComposition
                        ? null
                        : AssetDatabase.LoadAssetAtPath<TimelineAsset>(spec.sourcePath),
                    subComposition = spec.isComposition
                        ? AssetDatabase.LoadAssetAtPath<CinematicComposition>(spec.sourcePath)
                        : null,
                    laneName = spec.lane,
                    bindingKey = spec.bindingKey ?? "",
                    start = spec.start,
                    duration = spec.duration,
                    clipIn = spec.clipIn,
                    speed = spec.speed > 0 ? spec.speed : 1,
                    spawnPrefab = string.IsNullOrEmpty(spec.spawnPrefabPath)
                        ? null
                        : AssetDatabase.LoadAssetAtPath<GameObject>(spec.spawnPrefabPath),
                });
            }
            string path = $"{root}/{relativePath}";
            AssetDatabase.CreateAsset(contributor, path);
            AssetDatabase.SaveAssets();
            return path;
        }

        static string CreateComposition(string root, string relativePath, string name, string manifestPath,
            string actorPrefabPath, string scenePath, double duration, params string[] contributorPaths)
        {
            var composition = ScriptableObject.CreateInstance<CinematicComposition>();
            composition.cinematicName = name;
            composition.settings.frameRate = 30;
            composition.settings.totalDuration = duration;
            composition.outputFolder = GeneratedRoot;
            composition.bindingManifest = string.IsNullOrEmpty(manifestPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<BindingManifest>(manifestPath);
            composition.stageActorPrefab = string.IsNullOrEmpty(actorPrefabPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<GameObject>(actorPrefabPath);
            composition.stageSourceSceneGuid = string.IsNullOrEmpty(scenePath)
                ? ""
                : AssetDatabase.AssetPathToGUID(scenePath);
            foreach (string contributorPath in contributorPaths)
                composition.contributors.Add(AssetDatabase.LoadAssetAtPath<ContributorSegmentSet>(contributorPath));
            string path = $"{root}/{relativePath}";
            AssetDatabase.CreateAsset(composition, path);
            AssetDatabase.SaveAssets();
            return path;
        }

        static CinematicComposition FindComposition()
        {
            if (Selection.activeObject is CinematicComposition selected && selected.cinematicName == CompositionName)
                return selected;

            foreach (string guid in AssetDatabase.FindAssets("t:CinematicComposition OrbitalRelay"))
            {
                var composition = AssetDatabase.LoadAssetAtPath<CinematicComposition>(AssetDatabase.GUIDToAssetPath(guid));
                if (composition != null && composition.cinematicName == CompositionName)
                    return composition;
            }
            return null;
        }

        static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 position)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (parent != null)
                go.transform.SetParent(parent);
            go.transform.position = position;
            return go;
        }

        static Transform Find(Transform root, string name)
        {
            if (root.name == name)
                return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = Find(root.GetChild(i), name);
                if (found != null)
                    return found;
            }
            return null;
        }

        static void SetCurve(AnimationClip clip, string property, AnimationCurve curve) =>
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("", typeof(Transform), property), curve);

        static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        static void WriteToneWav(string assetPath, float seconds, float frequency)
        {
            const int sampleRate = 44100;
            int sampleCount = Mathf.CeilToInt(sampleRate * seconds);
            string fullPath = Path.GetFullPath(assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            using var stream = File.Create(fullPath);
            using var writer = new BinaryWriter(stream);
            int dataBytes = sampleCount * sizeof(short);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataBytes);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(sampleRate);
            writer.Write(sampleRate * sizeof(short));
            writer.Write((short)sizeof(short));
            writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(dataBytes);
            for (int i = 0; i < sampleCount; i++)
            {
                float envelope = Mathf.Min(1, i / (sampleRate * 0.05f)) *
                                 Mathf.Min(1, (sampleCount - i) / (sampleRate * 0.08f));
                float sample = Mathf.Sin(2 * Mathf.PI * frequency * i / sampleRate) * 0.18f * envelope;
                writer.Write((short)(sample * short.MaxValue));
            }
        }
    }
}
