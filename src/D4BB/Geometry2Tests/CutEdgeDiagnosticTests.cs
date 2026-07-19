using System.Collections.Generic;
using System.Linq;
using D4BB.Geometry;
using D4BB.Geometry2;
using NUnit.Framework;

namespace D4BB.Geometry2Tests {

    /// Diagnostic: on a nonconvex complex (the L piece) CutOut clips facets, so cut
    /// edges (isOriginal=false) MUST appear in the extracted edge segments for
    /// self-occluding poses — and the cached-BSP path must classify identically to
    /// the per-frame rebuild path.
    [TestFixture]
    public class CutEdgeDiagnosticTests {

        static (List<CellRender3d> cells, PolyhedralComplex4d complex, Camera4dParallel cam)
                Run(double a1, double a2, bool cached) {
            var complex = PolychoraAssets.Load("L.json");
            var cam = new Camera4dParallel();
            if (cached) {
                var objectSpace = PolychoraAssets.Load("L.json");
                var cache = Bsp4d.Build(objectSpace);
                TestGeom.RotateComplex(complex, a1, a2);
                double c1 = System.Math.Cos(a1), s1 = System.Math.Sin(a1);
                double c2 = System.Math.Cos(a2), s2 = System.Math.Sin(a2);
                var m = new[] { c1, -s1, 0, 0,  s1, c1, 0, 0,  0, 0, c2, -s2,  0, 0, s2, c2 };
                var cells = RenderPipeline.Process(complex, cam,
                    useBsp: true, applyCutOut: true, backfaceCulling: true,
                    cachedBsp: cache, cachedBspCamera: new RotatedCamera(cam, m));
                return (cells, complex, cam);
            } else {
                TestGeom.RotateComplex(complex, a1, a2);
                var cells = RenderPipeline.Process(complex, cam,
                    useBsp: true, applyCutOut: true, backfaceCulling: true);
                return (cells, complex, cam);
            }
        }

        static (int orig, int cut, int cutCoplanar) Census(double a1, double a2, bool cached) {
            var (cells, complex, cam) = Run(a1, a2, cached);
            var segs = CellRender3dEdges.ExtractFromPolygonBoundaries(cells, complex, cam);
            return (segs.Count(s => s.isOriginal),
                    segs.Count(s => !s.isOriginal && !s.isCoplanar),
                    segs.Count(s => !s.isOriginal && s.isCoplanar));
        }

        /// Regression: on the excavated polychora the dent faces are shared between two
        /// front-facing cells and therefore render twice; their identical clip boundaries
        /// must be classified as VISIBLE cut edges, not suppressed as internal seams
        /// (the face-blind coverage rule once hid every dent cut edge — "facet ends with
        /// no drawn line").
        [Test]
        public void ExcavatedPolychora_DentCutEdges_AreVisible() {
            foreach (var (file, a1, a2, minCuts) in new[] {
                ("excavated-ex.json",  0.4, 0.7, 3),
                ("excavated-ex.json",  1.1, 0.35, 3),
                ("excavated-ico.json", 2.0, 1.3, 9),
                ("excavated-ico.json", 2.8, 0.9, 9),
            }) {
                var complex = PolychoraAssets.Load(file);
                TestGeom.RotateComplex(complex, a1, a2);
                var cam = new Camera4dParallel();
                var cells = RenderPipeline.Process(complex, cam,
                    useBsp: true, applyCutOut: true, backfaceCulling: true);
                var segs = CellRender3dEdges.ExtractFromPolygonBoundaries(cells, complex, cam);
                int visibleCuts = segs.Count(s => !s.isOriginal && !s.isCoplanar);
                Assert.That(visibleCuts, Is.GreaterThanOrEqualTo(minCuts),
                    $"{file} a1={a1} a2={a2}: expected ≥{minCuts} visible cut edges, got {visibleCuts}");
            }
        }

        [Test]
        public void L_WeilerAtherton_ProducesCutEdges() {
            foreach (var (a1, a2) in new[] { (0.4, 0.0), (0.4, 0.7), (1.1, 0.35) }) {
                var complex = PolychoraAssets.Load("L.json");
                TestGeom.RotateComplex(complex, a1, a2);
                var cam = new Camera4dParallel();
                var wa = RenderPipeline2.ProcessPairwise(complex, cam,
                    applyCutOut: true, backfaceCulling: true);
                var cells = wa.Select(c => c.ToCellRender3d()).ToList();
                var segs = CellRender3dEdges.ExtractFromPolygonBoundaries(cells, complex, cam);
                TestContext.Out.WriteLine(
                    $"WA a1={a1:F2} a2={a2:F2}  orig={segs.Count(s => s.isOriginal)}" +
                    $" cut={segs.Count(s => !s.isOriginal && !s.isCoplanar)}" +
                    $" cutCopl={segs.Count(s => !s.isOriginal && s.isCoplanar)}");
            }
        }

        [Test]
        public void L_SelfOccludingPoses_ProduceCutEdges_AndCachedMatches() {
            int posesWithCuts = 0;
            foreach (var (a1, a2) in new[] { (0.4, 0.0), (0.4, 0.7), (1.1, 0.35), (2.0, 1.3), (0.9, 2.1) }) {
                var rebuild = Census(a1, a2, cached: false);
                var cachedR = Census(a1, a2, cached: true);
                TestContext.Out.WriteLine(
                    $"a1={a1:F2} a2={a2:F2}  rebuild: orig={rebuild.orig} cut={rebuild.cut} cutCopl={rebuild.cutCoplanar}" +
                    $"  cached: orig={cachedR.orig} cut={cachedR.cut} cutCopl={cachedR.cutCoplanar}");
                Assert.That(cachedR, Is.EqualTo(rebuild), $"cached vs rebuild mismatch at a1={a1}, a2={a2}");
                if (rebuild.cut > 0) posesWithCuts++;
            }
            Assert.That(posesWithCuts, Is.GreaterThan(0),
                "no pose produced any visible cut edge on the self-occluding L piece");
        }
    }
}
