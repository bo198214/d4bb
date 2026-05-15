using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using D4BB.Geometry;
using D4BB.Geometry2;

namespace D4BB.Geometry2Tests {
    /// Regression tests for BSP+CutOut on a 4-simplex (pentachoron). For a single 4-simplex
    /// no BSP cell-splits should ever happen during Build (every cell pair shares a 2-face →
    /// 3 vertices on the splitter plane, 1 off → POSITIVE/NEGATIVE, never STRADDLE).
    ///
    /// Note: CutOut DOES legitimately produce 2D-face splits, because in the 3D projection
    /// of a 4-simplex one vertex projects inside the convex hull of the others, making the
    /// "outer" cell contain the other 4 "inner" sub-tets. CutOut clipping the outer cell's
    /// faces against the inner cells' halfspaces is correct (and necessary for opaque-material
    /// HSR). For translucent materials, this is unnecessary work and visual fragmentation;
    /// the rendering controller can disable CutOut via applyCutOut=false.
    public class SimplexBspCutOutTests {

        static PolyhedralComplex4d LoadPenJson() {
            var assemblyDir = Path.GetDirectoryName(typeof(SimplexBspCutOutTests).Assembly.Location);
            var d = new DirectoryInfo(assemblyDir);
            while (d != null) {
                var p = Path.Combine(d.FullName, "Assets", "tesserian", "Resources", "polychora", "pen.json");
                if (File.Exists(p)) return PolyhedralComplex4dJson.FromJson(File.ReadAllText(p));
                d = d.Parent;
            }
            return null;
        }

        [Test] public void Simplex_BspBuild_NoStraddleSplits() {
            var c = LoadPenJson();
            if (c == null) { Assert.Ignore("pen.json not found"); return; }
            var bsp = Bsp4d.Build(c);
            // Walk the tree and count fragments — should equal the 5 source cells (no fragmentation).
            int totalFragments = 0;
            void Walk(BspNode n) {
                if (n == null) return;
                totalFragments += n.coincident.Count;
                Walk(n.positive);
                Walk(n.negative);
            }
            Walk(bsp.root);
            Assert.That(totalFragments, Is.EqualTo(c.cells.Count),
                "4-simplex BSP should not produce any cell-splits");
        }

        [Test] public void Simplex_BspBackToFront_YieldsExpectedNumberOfCells() {
            // For a 4-simplex viewed by Camera4dParallel cavalier from outside, exactly one
            // boundary cell is back-facing (its outward 4D-normal aligns with viewNormal);
            // BSP back-to-front yields the other 4 (or all 5 without backface culling).
            var c = LoadPenJson();
            if (c == null) { Assert.Ignore("pen.json not found"); return; }
            var bsp = Bsp4d.Build(c);
            var cam = new Camera4dParallel();
            int withCull = bsp.BackToFront(cam, cullBackfaces: true).Count();
            int withoutCull = bsp.BackToFront(cam, cullBackfaces: false).Count();
            Assert.That(withoutCull, Is.EqualTo(5), "all 5 simplex cells without culling");
            Assert.That(withCull, Is.GreaterThanOrEqualTo(3).And.LessThanOrEqualTo(4),
                "3-4 front-facing cells with culling");
        }

        [Test] public void Simplex_CutOut_AddsFragmentsForOuterCellOnly() {
            // The "outer" cell (whose excluded vertex projects inside the convex hull of the
            // remaining 4) gets its faces clipped by the inner cells. The inner cells, being
            // smaller and processed later, do not get their faces split by farther outer cells
            // (CutOut runs farther-against-nearer-halfspaces, so inner cells process LATER ones).
            //
            // Documents the actual behavior so future CutOut changes don't silently regress.
            var c = LoadPenJson();
            if (c == null) { Assert.Ignore("pen.json not found"); return; }
            var bsp = Bsp4d.Build(c);
            var cam = new Camera4dParallel();
            var processed = new List<CellRender3d>();
            var faceCountsBefore = new List<int>();
            foreach (var fragment in bsp.BackToFront(cam, cullBackfaces: true)) {
                var cell3d = CellRender3d.FromFragment(fragment, cam);
                faceCountsBefore.Add(cell3d.faces.Count);
                var halfSpaces = cell3d.DefiningHalfSpaces();
                foreach (var farCell in processed)
                    farCell.CutOut(halfSpaces);
                processed.Add(cell3d);
            }
            // Each cell should still have at least 1 face after CutOut (not entirely culled).
            for (int i = 0; i < processed.Count; i++) {
                Assert.That(processed[i].faces.Count, Is.GreaterThan(0),
                    $"cell {i} (sourceCellId={processed[i].sourceCellId}) was entirely removed by CutOut");
            }
            // Last-yielded (nearest) cell can never have been CutOut'd (no later cell to clip it).
            int lastIdx = processed.Count - 1;
            Assert.That(processed[lastIdx].faces.Count, Is.EqualTo(faceCountsBefore[lastIdx]),
                "nearest cell should be unmodified");
        }
    }
}
