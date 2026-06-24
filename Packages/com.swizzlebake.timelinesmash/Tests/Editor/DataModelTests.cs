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
                Assert.IsNotNull(comp.capture);
                Assert.AreEqual(30, comp.settings.frameRate);
                Assert.AreEqual("Assets/Cinematics/Generated", comp.outputFolder);
            }
            finally
            {
                Object.DestroyImmediate(comp);
            }
        }

        [Test]
        public void CaptureSettings_Defaults()
        {
            var c = new CaptureSettings();
            Assert.AreEqual(3840, c.width);
            Assert.AreEqual(2160, c.height);
            Assert.AreEqual(CaptureImageFormat.PNG, c.format);
            Assert.AreEqual(1, c.supersample);
            Assert.AreEqual("MainCamera", c.cameraTag);
        }
    }
}
