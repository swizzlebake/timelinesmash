using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TimelineSmash.Tests
{
    public class DataModelTests
    {
        [TearDown]
        public void TearDown() => TestAssets.Cleanup();

        [Test]
        public void SubTimelineSegment_Defaults()
        {
            var seg = new SubTimelineSegment();
            Assert.AreEqual("Main", seg.laneName);
            Assert.AreEqual(5, seg.duration);
            Assert.AreEqual(1, seg.speed);
            Assert.AreEqual(0, seg.start);
            Assert.AreEqual(0, seg.clipIn);
            Assert.IsNull(seg.subTimeline);
        }

        [Test]
        public void BindingManifest_Resolve_ReturnsMappedTarget()
        {
            var go = new GameObject("Actor");
            try
            {
                var manifest = TestAssets.CreateManifest(("Hero", go));
                Assert.AreSame(go, manifest.Resolve("Hero"));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void BindingManifest_Resolve_ReturnsNullForMissingKey()
        {
            var manifest = TestAssets.CreateManifest();
            Assert.IsNull(manifest.Resolve("Nope"));
        }

        [Test]
        public void BindingManifest_Resolve_ReturnsNullForEmptyKey()
        {
            var go = new GameObject("Actor");
            try
            {
                var manifest = TestAssets.CreateManifest(("Hero", go));
                Assert.IsNull(manifest.Resolve(""));
                Assert.IsNull(manifest.Resolve(null));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CinematicComposition_Defaults()
        {
            var comp = ScriptableObject.CreateInstance<CinematicComposition>();
            try
            {
                Assert.IsNotNull(comp.contributors);
                Assert.IsNotNull(comp.settings);
                Assert.AreEqual(30, comp.settings.frameRate);
                Assert.AreEqual("Assets/Cinematics/Generated", comp.outputFolder);
            }
            finally
            {
                Object.DestroyImmediate(comp);
            }
        }
    }
}
