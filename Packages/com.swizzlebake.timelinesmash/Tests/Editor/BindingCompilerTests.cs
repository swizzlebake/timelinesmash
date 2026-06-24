using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TimelineSmash.Editor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TimelineSmash.Tests
{
    public class BindingCompilerTests
    {
        readonly List<Object> _tmp = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _tmp)
                if (o != null)
                    Object.DestroyImmediate(o);
            _tmp.Clear();
            TestAssets.Cleanup();
        }

        T New<T>() where T : ScriptableObject { var o = ScriptableObject.CreateInstance<T>(); _tmp.Add(o); return o; }
        GameObject Go(string n) { var g = new GameObject(n); _tmp.Add(g); return g; }

        BindingManifest Manifest(string name, params (string key, Object target)[] entries)
        {
            var m = New<BindingManifest>();
            m.name = name;
            foreach (var (key, target) in entries)
                m.entries.Add(new BindingManifest.Entry { key = key, target = target });
            return m;
        }

        [Test]
        public void Compile_FlattensIncludes()
        {
            var a = Go("A"); var b = Go("B");
            var child = Manifest("child", ("HeroCam", a));
            var root = Manifest("root", ("Hero", b));
            root.includes.Add(child);

            var c = BindingCompiler.Compile(root);

            Assert.AreEqual(2, c.Count);
            Assert.AreSame(b, c.Resolve("Hero"));
            Assert.AreSame(a, c.Resolve("HeroCam"));
            Assert.AreEqual(0, c.warnings.Count);
        }

        [Test]
        public void Compile_FirstWins_LocalEntriesBeforeIncludes()
        {
            var local = Go("local"); var inc = Go("inc");
            var child = Manifest("child", ("Hero", inc));
            var root = Manifest("root", ("Hero", local));
            root.includes.Add(child);

            var c = BindingCompiler.Compile(root);

            Assert.AreSame(local, c.Resolve("Hero")); // the root's own entry was seen first
            Assert.AreEqual(1, c.warnings.Count);
            Assert.IsTrue(c.warnings[0].Contains("Hero"));
        }

        [Test]
        public void Compile_IncludeOrder_FirstIncludeWins()
        {
            var first = Go("first"); var second = Go("second");
            var i0 = Manifest("i0", ("Hero", first));
            var i1 = Manifest("i1", ("Hero", second));
            var root = Manifest("root");
            root.includes.Add(i0);
            root.includes.Add(i1);

            var c = BindingCompiler.Compile(root);

            Assert.AreSame(first, c.Resolve("Hero"));
            Assert.AreEqual(1, c.warnings.Count);
        }

        [Test]
        public void Compile_DiamondInclude_CompiledOnce_NoWarning()
        {
            var x = Go("X");
            var shared = Manifest("shared", ("Key", x));
            var b = Manifest("b"); b.includes.Add(shared);
            var d = Manifest("d"); d.includes.Add(shared);
            var root = Manifest("root"); root.includes.Add(b); root.includes.Add(d);

            var c = BindingCompiler.Compile(root);

            Assert.AreEqual(1, c.Count);
            Assert.AreSame(x, c.Resolve("Key"));
            Assert.AreEqual(0, c.warnings.Count, "a manifest reached via two include paths must not warn");
        }

        [Test]
        public void Compile_Cycle_IsDetectedAndGuarded()
        {
            var x = Go("X");
            var root = Manifest("root", ("Key", x));
            root.includes.Add(root); // self-include

            var c = BindingCompiler.Compile(root);

            Assert.AreEqual(1, c.Count);
            Assert.IsTrue(c.warnings.Any(w => w.Contains("Cycle")));
        }

        [Test]
        public void WriteAsset_ProducesFlatManifestWithMergedEntries()
        {
            // Use project-asset targets so the compiled asset serializes cleanly.
            var a = TestAssets.CreateSubTimeline("ActorA");
            var b = TestAssets.CreateSubTimeline("ActorB");
            var child = Manifest("child", ("HeroCam", a));
            var root = Manifest("root", ("Hero", b));
            root.includes.Add(child);

            var compiled = BindingCompiler.Compile(root);
            var flat = BindingCompiler.WriteAsset(compiled, TestAssets.Root + "/Compiled.asset");

            Assert.IsNotNull(flat);
            Assert.AreEqual(0, flat.includes.Count, "compiled asset is flat (no includes)");
            Assert.AreEqual(2, flat.entries.Count);
            Assert.AreSame(b, flat.Resolve("Hero"));
            Assert.AreSame(a, flat.Resolve("HeroCam"));
        }
    }
}
