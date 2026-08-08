using System.Linq;
using NUnit.Framework;
using TimelineSmash.Editor;
using TimelineSmash.Editor.Samples;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace TimelineSmash.Tests
{
    public class FourArtistSampleTests
    {
        const string SampleRoot = TestAssets.Root + "/FourArtist";
        const string MasterPath = TestAssets.Generated + "/OrbitalRelay_Master.playable";
        const string StagePath = TestAssets.Generated + "/OrbitalRelay_Stage.unity";

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            TestAssets.Cleanup();
        }

        [Test]
        public void BuildAssembleAndPlay_ProvesFourArtistWorkflowEndToEnd()
        {
            var composition = FourArtistCinematicBuilder.BuildAt(SampleRoot, overwrite: true);
            Assert.IsNotNull(composition);
            Assert.AreEqual(4, composition.contributors.Count);
            CollectionAssert.AreEquivalent(
                new[] { "Alice — Character", "Bob — Camera & Edit", "Cleo — Props & FX", "Dev — Lighting & Audio" },
                composition.contributors.Select(c => c.owner));

            var leaves = CinematicAssembler.FlattenTree(composition);
            Assert.AreEqual(16, leaves.Count, "The production sample should flatten to four leaves per artist.");
            CollectionAssert.AreEquivalent(
                new[] { "Camera", "Character", "FX", "Lighting", "Finale/Audio", "Finale/Lighting", "Finale/Satellite" },
                leaves.Select(l => l.lane).Distinct());
            Assert.AreEqual(45, leaves.Max(l => l.start + l.duration), 1e-6);
            Assert.AreEqual(2, leaves.Count(l => l.segment.spawnPrefab != null));
            Assert.IsTrue(leaves.Any(l => l.clipIn > 0));
            Assert.IsTrue(leaves.Any(l => System.Math.Abs(l.speed - 1) > 1e-6));

            var compiled = BindingCompiler.Compile(composition.bindingManifest);
            Assert.AreEqual(10, compiled.Count);
            Assert.IsEmpty(compiled.warnings);

            var first = CinematicAssembler.BuildMaster(composition, MasterPath);
            string signature1 = Signature(first);
            var second = CinematicAssembler.BuildMaster(composition, MasterPath);
            string signature2 = Signature(second);
            Assert.AreEqual(signature1, signature2, "Identical source must deterministically rebuild the same master.");
            Assert.AreEqual(16, second.entries.Count);
            Assert.AreEqual(45, second.totalDuration, 1e-6);

            var stage = StageSceneBuilder.BuildStage(second, compiled, StagePath,
                CinematicAssembleService.StageSourceScenePath(composition), composition.stageActorPrefab);
            Assert.AreEqual(16, stage.hosts.Count);
            Assert.IsEmpty(second.warnings, string.Join("\n", second.warnings));

            var arrivalHost = stage.hosts.Single(h => h.playableAsset.name == "Alice_Beat01_Arrival");
            var arrival = (TimelineAsset)arrivalHost.playableAsset;
            var body = arrival.GetOutputTracks().Single(t => t.name == "Body");
            var hero = arrivalHost.GetGenericBinding(body) as Animator;
            Assert.IsNotNull(hero);
            Assert.AreEqual(StagePath, hero.gameObject.scene.path,
                "Prefab manifest targets must remap to the generated stage instance.");

            arrivalHost.RebuildGraph();
            arrivalHost.time = 0;
            arrivalHost.Evaluate();
            Vector3 start = hero.transform.localPosition;
            arrivalHost.time = 7;
            arrivalHost.Evaluate();
            Vector3 end = hero.transform.localPosition;
            Assert.Greater((end - start).magnitude, 0.5f, "The imported sample's hero performance must really play.");
            arrivalHost.Stop();

            var fxHost = stage.hosts.First(h => h.playableAsset.name.StartsWith("Cleo_FX"));
            var fx = (TimelineAsset)fxHost.playableAsset;
            var signalTrack = fx.GetOutputTracks().OfType<SignalTrack>().Single();
            Assert.IsInstanceOf<SignalReceiver>(fxHost.GetGenericBinding(signalTrack));

            var audioHost = stage.hosts.Single(h => h.playableAsset.name == "Dev_FinaleAudio");
            var audioTrack = ((TimelineAsset)audioHost.playableAsset).GetOutputTracks().OfType<AudioTrack>().Single();
            Assert.IsNotNull(audioTrack.GetClips().Single().asset);
        }

        [Test]
        public void SerializedPackageSample_ImportsWithAllReferencesAndAssembles()
        {
            TestAssets.EnsureRoot();
            const string packaged = "Packages/com.swizzlebake.timelinesmash/Samples~/FourArtistCinematic/Content";
            string imported = SampleRoot + "/ImportedContent";
            if (!AssetDatabase.IsValidFolder(SampleRoot))
                AssetDatabase.CreateFolder(TestAssets.Root, "FourArtist");
            FileUtil.CopyFileOrDirectory(packaged, imported);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var composition = AssetDatabase.LoadAssetAtPath<CinematicComposition>(imported + "/OrbitalRelay.asset");
            Assert.IsNotNull(composition, "The Package Manager sample payload should import as a real composition.");
            Assert.AreEqual(4, composition.contributors.Count);
            Assert.IsNotNull(composition.bindingManifest);
            Assert.IsNotNull(composition.stageActorPrefab);
            Assert.IsNotEmpty(CinematicAssembleService.StageSourceScenePath(composition));
            Assert.AreEqual(16, CinematicAssembler.FlattenTree(composition).Count);

            var result = CinematicAssembler.BuildMaster(composition, MasterPath);
            var compiled = BindingCompiler.Compile(composition.bindingManifest);
            var stage = StageSceneBuilder.BuildStage(result, compiled, StagePath,
                CinematicAssembleService.StageSourceScenePath(composition), composition.stageActorPrefab);

            Assert.AreEqual(16, stage.hosts.Count);
            Assert.IsEmpty(result.warnings, string.Join("\n", result.warnings));
        }

        static string Signature(AssembleResult result) => string.Join("|", result.entries.Select(e =>
            $"{e.globalIndex}:{e.laneName}:{e.exposedName}:{e.clip.start:F3}:{e.clip.duration:F3}:" +
            $"{e.clip.clipIn:F3}:{e.clip.timeScale:F3}:{e.subTimelinePath}"));
    }
}
