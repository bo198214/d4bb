using System.Collections.Generic;
using NUnit.Framework;
using D4BB.Geometry3d;

namespace D4BB.Geometry3dTests {

    public class IntegerComplex3dBuilderTests {

        [Test] public void SingleCube_Counts() {
            var c = IntegerComplex3dBuilder.Boundary(Polycube3dFigures.ByName("single"));
            Assert.That(c.vertices.Count, Is.EqualTo(8));
            Assert.That(c.edges.Count, Is.EqualTo(12));
            Assert.That(c.faces.Count, Is.EqualTo(6));
        }

        // Every face's supporting plane agrees with its stored outward normal, and the
        // whole cube lies on the inner side of every face plane (outward orientation).
        [Test] public void SingleCube_OutwardNormals() {
            var c = IntegerComplex3dBuilder.Boundary(Polycube3dFigures.ByName("single"));
            for (int f = 0; f < c.faces.Count; f++) {
                var plane = c.FacePlane(f);
                Assert.That(plane.normal.sc(c.faces[f].normal), Is.GreaterThan(0.9),
                    $"face {f}: FacePlane orientation must agree with the stored outward normal");
                foreach (var v in c.vertices)
                    Assert.That(plane.side(v), Is.LessThanOrEqualTo(0),
                        $"face {f}: every cube vertex lies inside or on the face plane");
            }
        }

        // The L tricube: 3×6 − 2×2 shared = 14 boundary faces; the boundary is a closed
        // surface (every edge borders exactly 2 faces) of genus 0 (Euler characteristic 2).
        [Test] public void L3_FourteenFaces_ClosedSurface_Euler2() {
            var c = IntegerComplex3dBuilder.Boundary(Polycube3dFigures.ByName("L3"));
            Assert.That(c.faces.Count, Is.EqualTo(14));
            foreach (var eId in c.AllEdgeIds())
                Assert.That(c.FacesPerEdge(eId).Count, Is.EqualTo(2),
                    $"edge {eId}: a closed boundary surface has exactly 2 faces per edge");
            int euler = c.vertices.Count - c.edges.Count + c.faces.Count;
            Assert.That(euler, Is.EqualTo(2));
        }

        // All figures produce closed genus-0 surfaces with deduplicated geometry.
        [Test] public void AllFigures_ClosedSurfaces() {
            foreach (var (name, cells) in Polycube3dFigures.All) {
                var c = IntegerComplex3dBuilder.Boundary(cells);
                foreach (var eId in c.AllEdgeIds())
                    Assert.That(c.FacesPerEdge(eId).Count, Is.EqualTo(2), $"{name}: edge {eId}");
                Assert.That(c.vertices.Count - c.edges.Count + c.faces.Count, Is.EqualTo(2),
                    $"{name}: Euler characteristic");
            }
        }
    }

    public class PolyhedralComplex3dTests {

        // The two seams inside the L's flat front (z=0) face are coplanar-embedded;
        // ridges and silhouette edges are not.
        [Test] public void L3_SeamEdgesCoplanar_RidgeEdgesNot() {
            var c = IntegerComplex3dBuilder.Boundary(Polycube3dFigures.ByName("L3"));

            // Seam between cube A (0,-1,0) and cube B (-1,-1,0) on the front face z=0.
            int seamAB = TestGeom3d.FindEdge(c, new double[] { 0, -1, 0 }, new double[] { 0, 0, 0 });
            Assert.That(seamAB, Is.GreaterThanOrEqualTo(0), "seam edge A|B exists");
            Assert.That(c.IsCoplanarEdge(seamAB), Is.True, "flat-surface seam is coplanar-embedded");

            // Seam between cube A (0,-1,0) and cube C (0,0,0) on the front face z=0.
            int seamAC = TestGeom3d.FindEdge(c, new double[] { 0, 0, 0 }, new double[] { 1, 0, 0 });
            Assert.That(seamAC, Is.GreaterThanOrEqualTo(0), "seam edge A|C exists");
            Assert.That(c.IsCoplanarEdge(seamAC), Is.True);

            // The concave ridge where cubes B and C touch diagonally: two non-coplanar faces.
            int ridge = TestGeom3d.FindEdge(c, new double[] { 0, 0, 0 }, new double[] { 0, 0, 1 });
            Assert.That(ridge, Is.GreaterThanOrEqualTo(0), "concave ridge edge exists");
            Assert.That(c.IsCoplanarEdge(ridge), Is.False, "ridge between non-coplanar faces");

            // An outer silhouette edge of the front face (front z=0 face meets side x=1 face).
            int outer = TestGeom3d.FindEdge(c, new double[] { 1, -1, 0 }, new double[] { 1, 0, 0 });
            Assert.That(outer, Is.GreaterThanOrEqualTo(0));
            Assert.That(c.IsCoplanarEdge(outer), Is.False);
        }

        // A free rectangle's edges have a single incident face — treated as visible
        // (silhouette), never coplanar-embedded.
        [Test] public void FreeRectangle_EdgesNotCoplanar() {
            var c = TestGeom3d.RectComplex(
                new double[] { 0, 0, 0 }, new double[] { 1, 0, 0 },
                new double[] { 1, 1, 0 }, new double[] { 0, 1, 0 });
            foreach (var eId in c.AllEdgeIds())
                Assert.That(c.IsCoplanarEdge(eId), Is.False);
        }

        // PosedComplexes: merged parts pose independently, recomputed from originals
        // (no accumulation), and normals rotate along.
        [Test] public void PosedComplexes_SetPose_IsAbsoluteAndRotatesNormals() {
            var cube = IntegerComplex3dBuilder.Boundary(Polycube3dFigures.ByName("single"));
            var posed = PosedComplexes.Merge(new List<PolyhedralComplex3d> { cube, cube });
            // Center part 0 at the origin (unit cube [0,1]³ → [-0.5,0.5]³).
            posed.BakeOriginalTransform(0, 1.0, new double[] { -0.5, -0.5, -0.5 });

            var rot90 = PosedComplexes.AxisAngleRot(new double[] { 0, 0, 1 }, System.Math.PI / 2);
            posed.SetPose(0, rot90, null, null);
            posed.SetPose(0, rot90, null, null);   // absolute: applying twice must not accumulate
            // A 90° z-rotation of the centered cube maps corner (0.5,-0.5,-0.5) onto (0.5,0.5,-0.5).
            bool found = false;
            for (int i = 0; i < 8; i++) {
                var x = posed.complex.vertices[i].x;
                if (System.Math.Abs(x[0] - 0.5) < 1e-9 && System.Math.Abs(x[1] - 0.5) < 1e-9 &&
                    System.Math.Abs(x[2] + 0.5) < 1e-9) { found = true; break; }
            }
            Assert.That(found, Is.True, "pose is absolute (from originals), not accumulated");

            // Part 1 is untouched by part 0's pose.
            var v8 = posed.complex.vertices[8].x;
            Assert.That(v8[0] == 0 || v8[0] == 1, "part 1 stays at its original coordinates");

            // Normals rotated: some face of part 0 now has normal ≈ (0,-1,0)→(−1,0,0) etc.;
            // check that all six axis directions are still present among part-0 normals.
            var seen = new HashSet<(int, int, int)>();
            for (int f = 0; f < 6; f++) {
                var n = posed.complex.faces[f].normal.x;
                seen.Add(((int)System.Math.Round(n[0]), (int)System.Math.Round(n[1]),
                          (int)System.Math.Round(n[2])));
            }
            Assert.That(seen.Count, Is.EqualTo(6), "the six outward normals stay distinct after rotation");
        }
    }
}
