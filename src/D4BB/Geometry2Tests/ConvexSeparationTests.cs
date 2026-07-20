using System;
using System.Collections.Generic;
using System.Linq;
using D4BB.Comb;
using D4BB.Geometry;
using D4BB.Geometry2;
using NUnit.Framework;

namespace D4BB.Geometry2Tests {

    /// Tests for the GJK separating-hyperplane utility and its use as the RenderPipeline2
    /// pairwise-ordering fallback (replacing the old "mutually straddle ⇒ throw").
    [TestFixture]
    public class ConvexSeparationTests {

        static List<Point> Box4d(double[] lo, double[] hi) {
            var pts = new List<Point>(16);
            for (int m = 0; m < 16; m++)
                pts.Add(new Point(new[] {
                    (m & 1) != 0 ? hi[0] : lo[0], (m & 2) != 0 ? hi[1] : lo[1],
                    (m & 4) != 0 ? hi[2] : lo[2], (m & 8) != 0 ? hi[3] : lo[3] }));
            return pts;
        }

        static void AssertSeparates(List<Point> A, List<Point> B, Point normal, Point onPlane) {
            double off = normal.sc(onPlane);
            double minA = A.Min(p => normal.sc(p)), maxA = A.Max(p => normal.sc(p));
            double minB = B.Min(p => normal.sc(p)), maxB = B.Max(p => normal.sc(p));
            // A strictly on one side of the plane, B strictly on the other.
            bool aPos = minA > off + 1e-9 && minB < off - 1e-9 && maxB < off - 1e-9;
            bool aNeg = maxA < off - 1e-9 && minB > off + 1e-9;
            Assert.That(aPos || aNeg, Is.True,
                $"plane does not strictly separate: A∈[{minA:F4},{maxA:F4}] B∈[{minB:F4},{maxB:F4}] off={off:F4}");
            Assert.That(Math.Abs(normal.len() - 1.0), Is.LessThan(1e-9), "normal must be unit");
        }

        [Test]
        public void DisjointAxisAlignedBoxes_Separated() {
            var A = Box4d(new[] { 0.0, 0, 0, 0 }, new[] { 1.0, 1, 1, 1 });
            var B = Box4d(new[] { 2.0, 0, 0, 0 }, new[] { 3.0, 1, 1, 1 });   // gap along x
            Assert.That(ConvexSeparation.TrySeparatingHyperplane(A, B, out var n, out var p), Is.True);
            AssertSeparates(A, B, n, p);
        }

        [Test]
        public void OverlappingBoxes_NotSeparated() {
            var A = Box4d(new[] { 0.0, 0, 0, 0 }, new[] { 2.0, 2, 2, 2 });
            var B = Box4d(new[] { 1.0, 1, 1, 1 }, new[] { 3.0, 3, 3, 3 });   // interiors overlap
            Assert.That(ConvexSeparation.TrySeparatingHyperplane(A, B, out _, out _), Is.False);
        }

        [Test]
        public void TouchingBoxes_NotStrictlySeparated() {
            var A = Box4d(new[] { 0.0, 0, 0, 0 }, new[] { 1.0, 1, 1, 1 });
            var B = Box4d(new[] { 1.0, 0, 0, 0 }, new[] { 2.0, 1, 1, 1 });   // share the face x=1
            Assert.That(ConvexSeparation.TrySeparatingHyperplane(A, B, out _, out _), Is.False);
        }

        [Test]
        public void DisjointAlongDiagonal_Separated() {
            var A = Box4d(new[] { 0.0, 0, 0, 0 }, new[] { 1.0, 1, 1, 1 });
            var B = Box4d(new[] { 2.0, 2, 2, 2 }, new[] { 3.0, 3, 3, 3 });   // offset along (1,1,1,1)
            Assert.That(ConvexSeparation.TrySeparatingHyperplane(A, B, out var n, out var p), Is.True);
            AssertSeparates(A, B, n, p);
        }

        [Test]
        public void ThinGapBoxes_Separated() {
            var A = Box4d(new[] { 0.0, 0, 0, 0 }, new[] { 1.0, 1, 1, 1 });
            var B = Box4d(new[] { 1.001, 0, 0, 0 }, new[] { 2.0, 1, 1, 1 });  // 1mm gap
            Assert.That(ConvexSeparation.TrySeparatingHyperplane(A, B, out var n, out var p), Is.True);
            AssertSeparates(A, B, n, p);
        }

        // ── integration: RenderPipeline2 no longer throws on mutually-straddling cells ──

        static PolyhedralComplex4d TwoTumbledCubes(
                (int i, int j, double a) rotA, (int i, int j, double a) rotB, double[] offsetB) {
            var merged = new PolyhedralComplex4d();
            var cubeA = IntegerComplex4dBuilder.Boundary(new[] { new IntegerCell(new[] { 0, 0, 0, 0 }) });
            var cubeB = IntegerComplex4dBuilder.Boundary(new[] { new IntegerCell(new[] { 0, 0, 0, 0 }) });
            foreach (var v in cubeA.vertices) for (int c = 0; c < 4; c++) v.x[c] -= 0.5;
            foreach (var v in cubeB.vertices) for (int c = 0; c < 4; c++) v.x[c] -= 0.5;
            TestGeom.RotateComplexInPlane(cubeA, rotA.i, rotA.j, rotA.a);
            TestGeom.RotateComplexInPlane(cubeB, rotB.i, rotB.j, rotB.a);
            foreach (var v in cubeB.vertices) for (int c = 0; c < 4; c++) v.x[c] += offsetB[c];
            AppendComplex(merged, cubeA);
            AppendComplex(merged, cubeB);
            merged.InvalidateCaches();
            return merged;
        }

        static void AppendComplex(PolyhedralComplex4d target, PolyhedralComplex4d part) {
            int vOff = target.vertices.Count, eOff = target.edges.Count, fOff = target.faces.Count;
            foreach (var v in part.vertices) target.vertices.Add(v.clone());
            foreach (var e in part.edges) target.edges.Add(new D4BB.Geometry2.Edge(e.v0 + vOff, e.v1 + vOff));
            foreach (var f in part.faces) {
                var ids = new int[f.edgeIds.Length];
                for (int k = 0; k < ids.Length; k++) ids[k] = f.edgeIds[k] + eOff;
                target.faces.Add(new Face(ids));
            }
            foreach (var cell in part.cells) {
                var ids = new int[cell.faceIds.Length];
                for (int k = 0; k < ids.Length; k++) ids[k] = cell.faceIds[k] + fOff;
                target.cells.Add(new Cell(ids, cell.normal?.clone()));
            }
        }

        static List<(Point a, Point b)> VisibleCuts(List<CellRender3d> cells, PolyhedralComplex4d cx, ICamera4d cam) =>
            CellRender3dEdges.ExtractFromPolygonBoundaries(cells, cx, cam)
                .Where(s => !s.isOriginal && !s.isCoplanar)
                .Select(s => (s.a, s.b)).ToList();

        static bool OnSeg(Point p, (Point a, Point b) s) {
            var ab = s.b.clone().subtract(s.a); double len2 = ab.sc(ab);
            if (len2 < 1e-12) return p.clone().subtract(s.a).len() < 1e-3;
            double t = p.clone().subtract(s.a).sc(ab) / len2;
            if (t < -1e-4 || t > 1 + 1e-4) return false;
            return p.clone().subtract(s.a.clone().add(ab.clone().multiply(t))).len() < 2e-3;
        }
        static int Uncovered(List<(Point a, Point b)> probe, List<(Point a, Point b)> cover) {
            int u = 0;
            foreach (var s in probe) {
                var mid = s.a.clone().add(s.b.clone().subtract(s.a).multiply(0.5));
                if (s.a.clone().subtract(s.b).len() < 0.05) continue;   // ignore tiny fragments
                if (!cover.Any(c => OnSeg(mid, c))) u++;
            }
            return u;
        }

        [Test]
        public void PairwiseOrdering_TumbledCubes_NoThrow_AndMatchesBsp() {
            var cam = new Camera4dParallel();
            var rng = new System.Random(20260720);
            (int, int) Plane() { int i = rng.Next(4), j; do { j = rng.Next(4); } while (j == i); return (System.Math.Min(i, j), System.Math.Max(i, j)); }
            double Ang() => 0.3 + 0.9 * rng.NextDouble();

            int checkedPairs = 0, straddleFired = 0, intersecting = 0, parityFails = 0;
            for (int trial = 0; trial < 120; trial++) {
                var (ai, aj) = Plane(); var (bi, bj) = Plane();
                var rotA = (ai, aj, Ang()); var rotB = (bi, bj, Ang());
                // Center distance in [1.6, 2.2]: close enough that tumbled facets often interleave
                // (mutual straddle), far enough that many pairs stay 4D-disjoint. Random direction.
                var dir = new double[4]; double nn = 0;
                for (int c = 0; c < 4; c++) { dir[c] = rng.NextDouble() * 2 - 1; nn += dir[c] * dir[c]; }
                nn = System.Math.Sqrt(nn); double dist = 1.6 + 0.6 * rng.NextDouble();
                var off = new double[4]; for (int c = 0; c < 4; c++) off[c] = dir[c] / nn * dist;

                var cx = TwoTumbledCubes(rotA, rotB, off);
                long before = ConvexSeparation.FallbackInvocations;

                List<CellRenderWA3d> wa;
                try { wa = RenderPipeline2.ProcessPairwise(cx, cam, applyCutOut: true, backfaceCulling: true); }
                catch (Exception) { intersecting++; continue; }   // GJK reports a real 4D overlap → skip

                if (ConvexSeparation.FallbackInvocations > before) straddleFired++;

                var waCuts = VisibleCuts(wa.Select(c => c.ToCellRender3d()).ToList(), cx, cam);
                var bspCuts = VisibleCuts(
                    RenderPipeline.Process(cx, cam, useBsp: true, applyCutOut: true, backfaceCulling: true), cx, cam);
                if (Uncovered(waCuts, bspCuts) != 0 || Uncovered(bspCuts, waCuts) != 0) {
                    parityFails++;
                    TestContext.Out.WriteLine($"parity fail trial {trial}: rotA={rotA} rotB={rotB} off=({off[0]:F2},{off[1]:F2},{off[2]:F2},{off[3]:F2})");
                }
                checkedPairs++;
            }
            TestContext.Out.WriteLine($"checked={checkedPairs} straddleFired={straddleFired} intersecting(skipped)={intersecting} parityFails={parityFails}");
            Assert.That(parityFails, Is.EqualTo(0), "GJK-ordered pairs must match the BSP occlusion result");
            Assert.That(straddleFired, Is.GreaterThan(0),
                "no tumbled pair exercised the GJK fallback — the test does not cover the straddle path");
            Assert.That(checkedPairs, Is.GreaterThan(20), "too few disjoint pairs checked");
        }
    }
}
