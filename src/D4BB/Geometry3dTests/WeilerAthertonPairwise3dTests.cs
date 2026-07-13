using System.Collections.Generic;
using NUnit.Framework;
using D4BB.Geometry;
using D4BB.Geometry3d;

namespace D4BB.Geometry3dTests {

    /// Unit tests of the pairwise ordering and the exact Weiler-Atherton cut — the 3D
    /// sibling of Geometry2Tests.WeilerAthertonPairwiseTests. There is no BSP reference
    /// in 3D, so the assertions pin exact per-face visible AREAS instead.
    public class WeilerAthertonPairwise3dTests {

        // Two separate cubes stacked along the depth axis, viewed dead-on (orthographic,
        // zDir = 0): the near cube's front face projects exactly onto the far cube's, so
        // the far face vanishes and only the near unit square remains. Replays the
        // Scene3dOcclusionTests fixture through the new pipeline.
        [Test] public void TwoStackedCubes_DeadOn_FarFaceVanishes() {
            var cube = IntegerComplex3dBuilder.Boundary(Polycube3dFigures.ByName("single"));
            var posed = PosedComplexes.Merge(new List<PolyhedralComplex3d> { cube, cube });
            posed.SetPose(1, PosedComplexes.IdentityRot, null, new double[] { 0, 0, 2 });
            var cam = new Camera3dParallel(new Point(0, 0));

            var faces = RenderPipeline3d.ProcessPairwise(posed.complex, cam, applyCutOut: true, backfaceCulling: true);
            Assert.That(TestGeom3d.TotalVisibleArea(faces), Is.EqualTo(1.0).Within(1e-6),
                "only the near cube's front face survives (side faces are edge-on)");

            var off = RenderPipeline3d.ProcessPairwise(posed.complex, cam, applyCutOut: false, backfaceCulling: true);
            Assert.That(TestGeom3d.TotalVisibleArea(off), Is.EqualTo(2.0).Within(1e-6),
                "without CutOut both front faces stay");
        }

        // Two cubes side by side: their front faces are COINCIDENT (same supporting
        // plane) — the ordering must skip the pair, nothing is cut.
        [Test] public void CoincidentFrontFaces_NotCut() {
            var cube = IntegerComplex3dBuilder.Boundary(Polycube3dFigures.ByName("single"));
            var posed = PosedComplexes.Merge(new List<PolyhedralComplex3d> { cube, cube });
            posed.SetPose(1, PosedComplexes.IdentityRot, null, new double[] { 2, 0, 0 });
            var cam = new Camera3dParallel(new Point(0, 0));

            var faces = RenderPipeline3d.ProcessPairwise(posed.complex, cam, applyCutOut: true, backfaceCulling: true);
            Assert.That(faces.Count, Is.EqualTo(2));
            foreach (var f in faces)
                Assert.That(f.VisibleArea(), Is.EqualTo(1.0).Within(1e-6),
                    "coplanar faces cannot overlap in projection — no cutting");
        }

        // A single freely rotated cube: the front faces of a convex body never overlap in
        // projection, so CutOut must not remove anything (every face keeps its full
        // projected area). Exercises the exact non-straddle plane-side ordering under
        // arbitrary rigid poses.
        [Test] public void RotatedConvexCube_FrontFacesNeverCut() {
            var cube = IntegerComplex3dBuilder.Boundary(Polycube3dFigures.ByName("single"));
            var posed = PosedComplexes.Single(cube);
            posed.BakeOriginalTransform(0, 1.0, new double[] { -0.5, -0.5, -0.5 });
            var cam = new Camera3dParallel();   // cabinet
            var rng = new System.Random(4711);

            for (int trial = 0; trial < 12; trial++) {
                var axis = RandomUnitAxis(rng);
                double angle = rng.NextDouble() * 2 * System.Math.PI;
                posed.SetPose(0, PosedComplexes.AxisAngleRot(axis, angle), null, null);

                var cut = RenderPipeline3d.ProcessPairwise(posed.complex, cam, applyCutOut: true, backfaceCulling: true);
                var raw = RenderPipeline3d.ProcessPairwise(posed.complex, cam, applyCutOut: false, backfaceCulling: true);
                Assert.That(TestGeom3d.TotalVisibleArea(cut),
                    Is.EqualTo(TestGeom3d.TotalVisibleArea(raw)).Within(1e-6),
                    $"trial {trial}: convex front faces must survive uncut");
            }
        }

        // Mutual straddle with overlapping projections but DISJOINT faces (each face
        // pokes through the other's infinite plane far away from the overlap — the case
        // that throws in the 4D pipeline). The depth-probe fallback must order the pair
        // exactly: the farther face loses exactly the projected overlap, the nearer face
        // stays whole.
        [Test] public void MutualStraddle_DisjointFaces_ExactCut() {
            // A in plane z = y (x ∈ [0,1]), B in plane z = 2−y (x ∈ [2,3]); both span
            // y ∈ [0,2], so each crosses the other's plane, but their x-ranges are
            // disjoint (the faces never touch). A strong x-shear makes the projections overlap.
            var c = TestGeom3d.RectComplex(
                new double[] { 0, 0, 0 }, new double[] { 1, 0, 0 },
                new double[] { 1, 2, 2 }, new double[] { 0, 2, 2 });
            TestGeom3d.AddRect(c, new[] {
                new double[] { 2, 0, 2 }, new double[] { 3, 0, 2 },
                new double[] { 3, 2, 0 }, new double[] { 2, 2, 0 } });
            var cam = new Camera3dParallel(new Point(2, 0));

            // Sanity: the pair mutually straddles (each face has vertices on both sides
            // of the other's supporting plane).
            var planeA = c.FacePlane(0);
            var planeB = c.FacePlane(1);
            Assert.That(Straddles(c, 1, planeA), Is.True, "B straddles A's plane");
            Assert.That(Straddles(c, 0, planeB), Is.True, "A straddles B's plane");

            var faces = RenderPipeline3d.ProcessPairwise(c, cam, applyCutOut: true, backfaceCulling: false);
            Assert.That(faces.Count, Is.EqualTo(2));

            double fullA = TestGeom3d.Area(faces[0].ring);
            double fullB = TestGeom3d.Area(faces[1].ring);
            double overlap = TestGeom3d.Area(
                TestGeom3d.ClipConvex(faces[1].ring, faces[0].DefiningHalfSpaces2d()));
            Assert.That(overlap, Is.GreaterThan(0.1), "the projections genuinely overlap");

            double visA = faces[0].VisibleArea();
            double visB = faces[1].VisibleArea();
            // One face stays whole, the other loses exactly the overlap.
            bool aNearer = System.Math.Abs(visA - fullA) < 1e-6;
            if (aNearer) {
                Assert.That(visB, Is.EqualTo(fullB - overlap).Within(1e-5));
            } else {
                Assert.That(visB, Is.EqualTo(fullB).Within(1e-6));
                Assert.That(visA, Is.EqualTo(fullA - overlap).Within(1e-5));
            }
        }

        // Two faces CROSSING inside their projected overlap cannot be pairwise-ordered —
        // the pipeline must fail fast instead of cutting wrongly.
        [Test] public void CrossingFaces_Throw() {
            var c = TestGeom3d.RectComplex(
                new double[] { 0, 0, 0 }, new double[] { 4, 0, 0 },
                new double[] { 4, 2, 2 }, new double[] { 0, 2, 2 });
            TestGeom3d.AddRect(c, new[] {
                new double[] { 0, 0, 2 }, new double[] { 4, 0, 2 },
                new double[] { 4, 2, 0 }, new double[] { 0, 2, 0 } });
            var cam = new Camera3dParallel();   // cabinet

            Assert.Throws<System.Exception>(() =>
                RenderPipeline3d.ProcessPairwise(c, cam, applyCutOut: true, backfaceCulling: false));
        }

        // DefiningHalfSpaces2d sanity (mirror of Scene3dOcclusionTests): the halfplanes
        // bound the projected ring — centroid inside all, a far point outside one.
        [Test] public void DefiningHalfSpaces2d_BoundsProjectedRing() {
            var cube = IntegerComplex3dBuilder.Boundary(Polycube3dFigures.ByName("single"));
            var cam = new Camera3dParallel();
            var faces = RenderPipeline3d.ProcessPairwise(cube, cam, applyCutOut: false, backfaceCulling: true);
            Assert.That(faces.Count, Is.GreaterThan(0));
            foreach (var f in faces) {
                var hull = f.DefiningHalfSpaces2d();
                Assert.That(hull.Length, Is.EqualTo(4));
                var centroid = TestGeom3d.Centroid(f.ring);
                foreach (var hs in hull)
                    Assert.That(hs.side(centroid), Is.EqualTo(HalfSpace.INSIDE));
                var farPoint = new Point(100, 100, 0);
                bool outside = false;
                foreach (var hs in hull)
                    if (hs.side(farPoint) == HalfSpace.OUTSIDE) { outside = true; break; }
                Assert.That(outside, Is.True);
            }
        }

        static bool Straddles(PolyhedralComplex3d c, int faceId, HalfSpace plane) {
            bool pos = false, neg = false;
            foreach (var v in c.FaceVertices(faceId)) {
                int s = plane.side(v);
                if (s > 0) pos = true;
                if (s < 0) neg = true;
            }
            return pos && neg;
        }

        static double[] RandomUnitAxis(System.Random rng) {
            while (true) {
                var v = new[] {
                    rng.NextDouble() * 2 - 1, rng.NextDouble() * 2 - 1, rng.NextDouble() * 2 - 1 };
                double m = System.Math.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]);
                if (m > 0.1 && m <= 1) return new[] { v[0] / m, v[1] / m, v[2] / m };
            }
        }
    }
}
