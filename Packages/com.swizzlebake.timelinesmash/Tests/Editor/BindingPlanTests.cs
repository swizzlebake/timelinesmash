using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TimelineSmash.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

namespace TimelineSmash.Tests
{
    public class BindingPlanTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
                if (go != null)
                    Object.DestroyImmediate(go);
            _spawned.Clear();
            TestAssets.Cleanup();
        }

        GameObject Actor(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        // An in-memory manifest avoids serializing references into an .asset.
        static BindingManifest Manifest(params (string key, Object target)[] entries)
        {
            var m = ScriptableObject.CreateInstance<BindingManifest>();
            foreach (var (key, target) in entries)
                m.entries.Add(new BindingManifest.Entry { key = key, target = target });
            return m;
        }

        CinematicComposition MultiTrackComp(BindingManifest manifest, string bindingKey = null)
        {
            var sub = TestAssets.CreateMultiTrackSubTimeline("HeroMulti"); // tracks: Body / Voice / Prop
            var alice = TestAssets.CreateContributor("Alice",
                new[] { TestAssets.Seg(sub, "Characters", 0, 1, bindingKey: bindingKey) });
            return TestAssets.CreateComposition("Cine", manifest, alice);
        }

        [Test]
        public void Build_AllKeysPresent_EveryTrackResolved()
        {
            var comp = MultiTrackComp(Manifest(
                ("Body", Actor("hero")), ("Voice", Actor("speaker")), ("Prop", Actor("prop"))));

            var plan = BindingPlan.Build(comp);

            Assert.AreEqual(3, plan.Total);
            Assert.AreEqual(3, plan.Bound);
            Assert.AreEqual(0, plan.Missing);
            foreach (var r in plan.requirements)
            {
                Assert.IsTrue(r.Resolved, $"'{r.trackName}' should resolve.");
                Assert.AreEqual(r.trackName, r.resolvedKey);
            }
        }

        [Test]
        public void Build_MissingKey_IsUnresolvedWithSuggestion()
        {
            var comp = MultiTrackComp(Manifest(("Body", Actor("hero")), ("Prop", Actor("prop"))));

            var plan = BindingPlan.Build(comp);

            Assert.AreEqual(3, plan.Total);
            Assert.AreEqual(2, plan.Bound);
            var voice = plan.requirements.Single(r => r.trackName == "Voice");
            Assert.IsFalse(voice.Resolved);
            Assert.AreEqual("Voice", voice.suggestedKey);
        }

        [Test]
        public void Build_Override_NamespacedKeyRetargetsAndSuggestsPerTrack()
        {
            var comp = MultiTrackComp(Manifest(("hero/Body", Actor("hero"))), bindingKey: "hero");

            var plan = BindingPlan.Build(comp);

            var body = plan.requirements.Single(r => r.trackName == "Body");
            Assert.IsTrue(body.Resolved);
            Assert.AreEqual("hero/Body", body.resolvedKey);

            var voice = plan.requirements.Single(r => r.trackName == "Voice");
            Assert.IsFalse(voice.Resolved);
            Assert.AreEqual("hero/Voice", voice.suggestedKey, "Unresolved override should suggest the per-track key.");
        }

        [Test]
        public void Build_Override_BareKeyResolvesEveryTrack()
        {
            var comp = MultiTrackComp(Manifest(("hero", Actor("hero"))), bindingKey: "hero");

            var plan = BindingPlan.Build(comp);

            Assert.AreEqual(3, plan.Bound);
            var body = plan.requirements.Single(r => r.trackName == "Body");
            Assert.AreEqual("hero", body.resolvedKey, "Should resolve via the bare override fallback.");
        }

        [Test]
        public void Build_NestedControlTrack_IsCountedAndResolvable()
        {
            TestAssets.EnsureRoot();
            var sub = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(sub, TestAssets.Root + "/Ctrl.playable");
            var ctrl = sub.CreateTrack<ControlTrack>(null, "Rig");
            var clip = ctrl.CreateClip<ControlPlayableAsset>();
            ((ControlPlayableAsset)clip.asset).sourceGameObject =
                new ExposedReference<GameObject> { exposedName = "R" };
            clip.start = 0;
            clip.duration = 1;
            EditorUtility.SetDirty(sub);
            AssetDatabase.SaveAssets();

            var alice = TestAssets.CreateContributor("Alice", new[] { TestAssets.Seg(sub, "Fx", 0, 1) });
            var comp = TestAssets.CreateComposition("Cine", Manifest(("Rig", Actor("rig"))), alice);

            var plan = BindingPlan.Build(comp);

            var req = plan.requirements.Single();
            Assert.IsTrue(req.isControl);
            Assert.IsTrue(req.Resolved);
            Assert.AreEqual("Rig", req.resolvedKey);
        }

        [Test]
        public void Build_NoManifest_EverythingUnresolved()
        {
            var comp = MultiTrackComp(null);

            var plan = BindingPlan.Build(comp);

            Assert.AreEqual(3, plan.Total);
            Assert.AreEqual(0, plan.Bound);
            Assert.AreEqual(3, plan.Missing);
        }
    }
}
