using System;
using System.Collections.Generic;
using NUnit.Framework;
using D4BB.Geometry;
using D4BB.Geometry3d;

namespace D4BB.Geometry3dTests {

    /// Edge-class invariants of FaceRender2dEdges — the 3D sibling of
    /// Geometry2Tests.EdgeClassOverlayTests, plus the docked-L seam/clipping behaviour
    /// the LAssembleCubesBeat relies on.
    public class EdgeClassOverlay3dTests {

        // A freely tumbling single cube is never occluded by anything: every extracted
        // segment is a (whole) original edge, no cut edges appear.
        [Test] public void TumblingSingleCube_NoCutEdges() {
            var cube = IntegerComplex3dBuilder.Boundary(Polycube3dFigures.ByName("single"));
            var posed = PosedComplexes.Single(cube);
            posed.BakeOriginalTransform(0, 1.0, new double[] { -0.5, -0.5, -0.5 });
            var cam = new Camera3dParallel();
            var rng = new Random(99);
            for (int trial = 0; trial < 6; trial++) {
                posed.SetPose(0, PosedComplexes.AxisAngleRot(RandomUnitAxis(rng),
                              rng.NextDouble() * 2 * Math.PI), null, null);
                var faces = RenderPipeline3d.ProcessPairwise(posed.complex, cam, true, true);
                var edges = FaceRender2dEdges.ExtractFromPolygonBoundaries(faces, posed.complex, cam);
                Assert.That(edges.Count, Is.GreaterThan(0));
                foreach (var e in edges)
                    Assert.That(e.isOriginal, Is.True, $"trial {trial}: uncut cube has only original edges");
            }
        }

        // The docked L, front view: the two seams inside the flat z=0 face are
        // structurally coplanar and must be HIDDEN; ridges and silhouette stay drawn.
        [Test] public void DockedL_FrontView_SeamsHidden() {
            var complex = IntegerComplex3dBuilder.Boundary(Polycube3dFigures.ByName("L3"));
            var cam = new Camera3dParallel();
            var faces = RenderPipeline3d.ProcessPairwise(complex, cam, true, true);
            var edges = FaceRender2dEdges.ExtractFromPolygonBoundaries(faces, complex, cam);

            // The projected seam segments on the front face z=0: (0,-1,0)-(0,0,0) and
            // (0,0,0)-(1,0,0) project to themselves under the cabinet camera (z = 0).
            var seams = new[] {
                (new Point(0, -1, 0), new Point(0, 0, 0)),
                (new Point(0, 0, 0), new Point(1, 0, 0)),
            };
            foreach (var e in edges) {
                if (e.isCoplanar) continue;   // hidden by the consumer — irrelevant
                foreach (var (sa, sb) in seams)
                    Assert.That(TestGeom3d.CollinearOverlap(e.a, e.b, sa, sb, 1e-6, 0.01),
                        Is.False, "a drawn segment must not lie on a flat-surface seam");
            }
            // …and the seams DO exist as hidden coplanar originals.
            int hiddenSeams = 0;
            foreach (var e in edges)
                if (e.isOriginal && e.isCoplanar) hiddenSeams++;
            Assert.That(hiddenSeams, Is.GreaterThanOrEqualTo(2),
                "the flat-surface seams are extracted as coplanar (hidden) originals");
        }

        // Sweep the docked L about +Y (the beat's rotation): drawn originals never
        // collinearly overlap drawn cut edges (the two would z-fight under different
        // materials), at every angle.
        [Test] public void DockedL_YSweep_NoDrawnOriginalCutOverlap() {
            var complex = IntegerComplex3dBuilder.Boundary(Polycube3dFigures.ByName("L3"));
            var posed = PosedComplexes.Single(complex);
            var cam = new Camera3dParallel();
            for (int deg = 0; deg < 360; deg += 10) {
                posed.SetPose(0, PosedComplexes.AxisAngleRot(new double[] { 0, 1, 0 },
                              deg * Math.PI / 180.0), null, null);
                var faces = RenderPipeline3d.ProcessPairwise(posed.complex, cam, true, true);
                var edges = FaceRender2dEdges.ExtractFromPolygonBoundaries(faces, posed.complex, cam);

                var drawnOriginals = new List<EdgeSegment2d>();
                var drawnCuts = new List<EdgeSegment2d>();
                foreach (var e in edges) {
                    if (e.isCoplanar) continue;
                    if (e.isOriginal) drawnOriginals.Add(e);
                    else drawnCuts.Add(e);
                }
                foreach (var o in drawnOriginals)
                    foreach (var c in drawnCuts)
                        Assert.That(TestGeom3d.CollinearOverlap(o.a, o.b, c.a, c.b, 1e-4, 0.01),
                            Is.False, $"{deg}°: drawn original overlays a drawn cut edge");
            }
        }

        // The concave L self-occludes at some angles of the +Y turn: there, original
        // edges must be CLIPPED to their visible portions — the total drawn-original
        // length drops below the uncut reference.
        [Test] public void DockedL_YSweep_SelfOcclusionClipsOriginals() {
            var complex = IntegerComplex3dBuilder.Boundary(Polycube3dFigures.ByName("L3"));
            var posed = PosedComplexes.Single(complex);
            var cam = new Camera3dParallel();
            bool anyClipped = false;
            for (int deg = 0; deg < 360 && !anyClipped; deg += 10) {
                posed.SetPose(0, PosedComplexes.AxisAngleRot(new double[] { 0, 1, 0 },
                              deg * Math.PI / 180.0), null, null);
                double cutLen = DrawnOriginalLength(posed.complex, cam, applyCutOut: true);
                double rawLen = DrawnOriginalLength(posed.complex, cam, applyCutOut: false);
                if (cutLen < rawLen - 0.05) anyClipped = true;
            }
            Assert.That(anyClipped, Is.True,
                "somewhere in the 360° turn the concave L must partially occlude its own edges");
        }

        static double DrawnOriginalLength(PolyhedralComplex3d complex, ICamera3d cam, bool applyCutOut) {
            var faces = RenderPipeline3d.ProcessPairwise(complex, cam, applyCutOut, backfaceCulling: true);
            var edges = FaceRender2dEdges.ExtractFromPolygonBoundaries(faces, complex, cam);
            double len = 0;
            foreach (var e in edges)
                if (e.isOriginal && !e.isCoplanar)
                    len += e.b.clone().subtract(e.a).len();
            return len;
        }

        static double[] RandomUnitAxis(Random rng) {
            while (true) {
                var v = new[] {
                    rng.NextDouble() * 2 - 1, rng.NextDouble() * 2 - 1, rng.NextDouble() * 2 - 1 };
                double m = Math.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]);
                if (m > 0.1 && m <= 1) return new[] { v[0] / m, v[1] / m, v[2] / m };
            }
        }
    }
}
