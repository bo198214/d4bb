using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using D4BB.Geometry;
using D4BB.Geometry2;

namespace D4BB.Geometry2Tests {

    /// Pins the edge-classification property behind the "two edge colors overlaid" render
    /// symptom: a DRAWN original segment (isOriginal && !isCoplanar) must never overlap a
    /// DRAWN cut segment (!isOriginal && !isCoplanar) on the same line — otherwise the
    /// regular-edge and cut-edge materials render on top of each other. Covers the
    /// L-Assemble2 bake configuration (L piece pre-centered like the bake, XZ sweep, and
    /// tumbling single cubes which must produce no cut edges at all).
    public class EdgeClassOverlayTests {

        static void ShiftComplex(PolyhedralComplex4d complex, double[] center) {
            foreach (var v in complex.vertices)
                for (int c = 0; c < 4; c++) v.x[c] -= center[c];
            complex.InvalidateCaches();
        }

        [Test] public void LPiece_XzSweep_NoDrawnOriginalVsDrawnCutOverlay() {
            var cells = PolycubeFigures.ByName("L3");
            var overlays = new List<string>();
            for (int deg = 0; deg < 360; deg += 10) {
                var complex = IntegerComplex4dBuilder.Boundary(PolycubeFigures.AsIntegerCells(cells));
                ShiftComplex(complex, new[] { 0.0, -0.5, 0.5, 0.25 });   // as in the L-Assemble2 bake
                TestGeom.RotateComplexInPlane(complex, 0, 2, deg * System.Math.PI / 180.0);
                var cam = new Camera4dParallel();
                var processed = RenderPipeline.Process(complex, cam, true, true, true);
                var segs = CellRender3dEdges.ExtractFromPolygonBoundaries(processed, complex, cam);

                var drawnOrig = segs.Where(s => s.isOriginal && !s.isCoplanar).ToList();
                var drawnCut = segs.Where(s => !s.isOriginal && !s.isCoplanar).ToList();
                foreach (var o in drawnOrig)
                    foreach (var c in drawnCut) {
                        double ol = OverlapLength(o, c, 1e-3);
                        if (ol > 1e-2 && overlays.Count < 8)
                            overlays.Add($"deg={deg}: orig {TestGeom.Fmt(o.a)}–{TestGeom.Fmt(o.b)} vs " +
                                         $"cut {TestGeom.Fmt(c.a)}–{TestGeom.Fmt(c.b)} overlap={ol:F3}");
                    }
            }
            Assert.That(overlays, Is.Empty,
                "drawn original and drawn cut segments overlap (renders two edge colors on top of each other):\n  " +
                string.Join("\n  ", overlays));
        }

        [Test] public void SingleCube_RandomTumble_ProducesNoCutEdges() {
            var rng = new System.Random(42);
            for (int trial = 0; trial < 20; trial++) {
                var complex = IntegerComplex4dBuilder.Boundary(new[] { new D4BB.Comb.IntegerCell(new[] { 0, -1, 0, 0 }) });
                ShiftComplex(complex, new[] { 0.5, -0.5, 0.5, 0.5 });
                int i1 = rng.Next(4), j1 = (i1 + 1 + rng.Next(3)) % 4;
                TestGeom.RotateComplexInPlane(complex, System.Math.Min(i1, j1), System.Math.Max(i1, j1),
                    rng.NextDouble() * 2 * System.Math.PI);
                int i2 = rng.Next(4), j2 = (i2 + 1 + rng.Next(3)) % 4;
                TestGeom.RotateComplexInPlane(complex, System.Math.Min(i2, j2), System.Math.Max(i2, j2),
                    rng.NextDouble() * 2 * System.Math.PI);
                var cam = new Camera4dParallel();
                var processed = RenderPipeline.Process(complex, cam, true, true, true);
                var segs = CellRender3dEdges.ExtractFromPolygonBoundaries(processed, complex, cam);
                Assert.That(segs.Count(s => !s.isOriginal), Is.EqualTo(0),
                    $"trial {trial}: a single convex cube must produce no cut edges");
            }
        }

        /// Overlap length of two segments if they are collinear (within eps); 0 otherwise.
        static double OverlapLength(EdgeSegment3d s1, EdgeSegment3d s2, double eps) {
            var d1 = Dir(s1, out double len1);
            if (len1 < eps) return 0;
            if (DistToLine(s1.a, d1, s2.a) > eps || DistToLine(s1.a, d1, s2.b) > eps) return 0;
            double t2a = ProjT(s1.a, d1, s2.a), t2b = ProjT(s1.a, d1, s2.b);
            double lo = System.Math.Max(0, System.Math.Min(t2a, t2b));
            double hi = System.Math.Min(len1, System.Math.Max(t2a, t2b));
            return System.Math.Max(0, hi - lo);
        }

        static double[] Dir(EdgeSegment3d s, out double len) {
            var d = new[] { s.b.x[0] - s.a.x[0], s.b.x[1] - s.a.x[1], s.b.x[2] - s.a.x[2] };
            len = System.Math.Sqrt(d[0] * d[0] + d[1] * d[1] + d[2] * d[2]);
            if (len > 1e-12) { d[0] /= len; d[1] /= len; d[2] /= len; }
            return d;
        }

        static double ProjT(Point origin, double[] dir, Point p) =>
            (p.x[0] - origin.x[0]) * dir[0] + (p.x[1] - origin.x[1]) * dir[1] + (p.x[2] - origin.x[2]) * dir[2];

        static double DistToLine(Point origin, double[] dir, Point p) {
            double t = ProjT(origin, dir, p);
            double dx = p.x[0] - origin.x[0] - t * dir[0];
            double dy = p.x[1] - origin.x[1] - t * dir[1];
            double dz = p.x[2] - origin.x[2] - t * dir[2];
            return System.Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}
