using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TimelineSmash.Editor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace TimelineSmash.Tests
{
    public class BindingApplierTests
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

        GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        PlayableDirector Host(TimelineAsset sub)
        {
            var dir = Spawn("Host").AddComponent<PlayableDirector>();
            dir.playableAsset = sub;
            return dir;
        }

        [Test]
        public void Apply_BindsTrackByName()
        {
            var sub = TestAssets.CreateSubTimeline("Hero", "HeroTrack");
            var animator = Spawn("HeroActor").AddComponent<Animator>();
            var manifest = TestAssets.CreateManifest(("HeroTrack", animator));
            var host = Host(sub);
            var warnings = new List<string>();

            int bound = BindingApplier.Apply(host, TestAssets.Seg(sub, "Main", 0, 5), manifest, warnings);

            Assert.AreEqual(1, bound);
            Assert.AreEqual(0, warnings.Count);
            var track = sub.GetOutputTracks().First();
            Assert.AreSame(animator, host.GetGenericBinding(track));
        }

        [Test]
        public void Apply_UsesBindingKeyOverride()
        {
            var sub = TestAssets.CreateSubTimeline("Hero", "InternalTrackName");
            var animator = Spawn("HeroActor").AddComponent<Animator>();
            var manifest = TestAssets.CreateManifest(("Protagonist", animator));
            var host = Host(sub);
            var warnings = new List<string>();

            var seg = TestAssets.Seg(sub, "Main", 0, 5, bindingKey: "Protagonist");
            int bound = BindingApplier.Apply(host, seg, manifest, warnings);

            Assert.AreEqual(1, bound);
            var track = sub.GetOutputTracks().First();
            Assert.AreSame(animator, host.GetGenericBinding(track));
        }

        [Test]
        public void Apply_UnresolvedKey_WarnsAndBindsNothing()
        {
            var sub = TestAssets.CreateSubTimeline("Hero", "HeroTrack");
            var manifest = TestAssets.CreateManifest(); // empty
            var host = Host(sub);
            var warnings = new List<string>();

            int bound = BindingApplier.Apply(host, TestAssets.Seg(sub, "Main", 0, 5), manifest, warnings);

            Assert.AreEqual(0, bound);
            Assert.AreEqual(1, warnings.Count);
            Assert.IsTrue(warnings[0].Contains("HeroTrack"));
        }

        [Test]
        public void Apply_NullManifest_WarnsPerTrack()
        {
            var sub = TestAssets.CreateSubTimeline("Hero", "HeroTrack");
            var host = Host(sub);
            var warnings = new List<string>();

            int bound = BindingApplier.Apply(host, TestAssets.Seg(sub, "Main", 0, 5), null, warnings);

            Assert.AreEqual(0, bound);
            Assert.AreEqual(1, warnings.Count);
        }
    }
}
