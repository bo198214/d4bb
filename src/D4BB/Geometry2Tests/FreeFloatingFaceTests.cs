using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using D4BB.Geometry;
using D4BB.Geometry2;
using Edge = D4BB.Geometry2.Edge;

namespace D4BB.Geometry2Tests {

    /// Tests for handling 2-faces that aren't part of any 3-cell. Uses a
    /// PolyhedralComplex4d with cells.Count == 0 (pure 2-face complex) and verifies:
    ///   • the bookkeeping (CellsPerFace returns empty for them, IsCoplanarFace=false, etc.)
    ///   • the CutOut routing's drop-on-ambiguous-orientation path (centroid on cutter plane)
    public class FreeFloatingFaceTests {

        /// Build a single triangular 2-face in 4D embedded at w=0.
        static PolyhedralComplex4d TriangleAtW(double w) {
            var c = new PolyhedralComplex4d();
            c.vertices.Add(new Point(0, 0, 0, w));
            c.vertices.Add(new Point(1, 0, 0, w));
            c.vertices.Add(new Point(0, 1, 0, w));
            c.edges.Add(new Edge(0, 1));
            c.edges.Add(new Edge(1, 2));
            c.edges.Add(new Edge(2, 0));
            c.faces.Add(new Face(new[] { 0, 1, 2 }));
            return c;
        }

        [Test] public void FreeFloatingFace_HasNoCellParent() {
            var c = TriangleAtW(0);
            Assert.That(c.cells.Count, Is.EqualTo(0));
            Assert.That(c.faces.Count, Is.EqualTo(1));
            Assert.That(c.CellsPerFace(0), Is.Empty);
        }

        [Test] public void FreeFloatingFace_IsNotCoplanar() {
            // IsCoplanarFace requires exactly 2 cell parents in same hyperplane.
            // A free-floating face has 0 parents → not coplanar.
            var c = TriangleAtW(0);
            Assert.That(c.IsCoplanarFace(0), Is.False);
        }

        [Test] public void FreeFloatingFace_AllEdgesAreVisible() {
            var c = TriangleAtW(0);
            var visible = new HashSet<int>(c.VisibleNonCoplanarEdgeIds(new HashSet<int>()));
            // No incident cells anywhere ⇒ all 3 edges are kept by the (a) clause
            // ("free-floating: not contained in any 3-cell").
            Assert.That(visible, Is.EquivalentTo(new[] { 0, 1, 2 }));
        }

        [Test] public void CellRender3d_CutOut_DropsFreeFloatingFaceCoincidentWithCutter() {
            // A free-floating triangle in 3D, lying exactly on the same plane as one face of
            // a unit cube. CellRender3d.CutOut against the cube's halfspaces should DROP the
            // triangle (orientation ambiguous: triangle's "cellCentroid" is in the triangle's
            // own plane, hence on the cutter's halfspace plane).
            var triangle = new CellRender3d {
                sourceCellId = -1,
                faces = new List<List<Point>> {
                    new List<Point> {
                        new Point(0.2, 0.2, 0),
                        new Point(0.8, 0.2, 0),
                        new Point(0.5, 0.8, 0),
                    }
                },
                faceIds = new List<int> { -1 },
            };
            // Unit cube at origin (cube's z=0 face is at z=0, coplanar with the triangle).
            var cubeCell = Cube4dBuilder.UnitCubeAtW(0);
            var cube3d = CellRender3d.FromFragment(CellFragment.FromCell(cubeCell, 0), new Camera4dParallel());
            var cubeHs = cube3d.DefiningHalfSpaces();

            triangle.CutOut(cubeHs);
            Assert.That(triangle.faces.Count, Is.EqualTo(0),
                "free-floating triangle on cutter plane should be dropped (orientation undefined)");
        }

        [Test] public void CellRender3d_CutOut_KeepsFreeFloatingFaceAwayFromCutter() {
            // A free-floating triangle far away from the cube: should survive intact.
            var triangle = new CellRender3d {
                sourceCellId = -1,
                faces = new List<List<Point>> {
                    new List<Point> {
                        new Point(5, 0, 0),
                        new Point(6, 0, 0),
                        new Point(5.5, 1, 0),
                    }
                },
                faceIds = new List<int> { -1 },
            };
            var cubeCell = Cube4dBuilder.UnitCubeAtW(0);
            var cube3d = CellRender3d.FromFragment(CellFragment.FromCell(cubeCell, 0), new Camera4dParallel());
            triangle.CutOut(cube3d.DefiningHalfSpaces());
            Assert.That(triangle.faces.Count, Is.EqualTo(1));
            Assert.That(triangle.faces[0].Count, Is.EqualTo(3));
        }

        [Test] public void CellRender3d_CutOut_DropsFreeFloatingFaceInsideCutter() {
            // Triangle entirely inside the cube ⇒ Sutherland-Hodgman drops all outer fragments,
            // inner-discarded at end. Drop matches "occluded by cube" semantics.
            var triangle = new CellRender3d {
                sourceCellId = -1,
                faces = new List<List<Point>> {
                    new List<Point> {
                        new Point(0.2, 0.2, 0.5),
                        new Point(0.8, 0.2, 0.5),
                        new Point(0.5, 0.8, 0.5),
                    }
                },
                faceIds = new List<int> { -1 },
            };
            var cubeCell = Cube4dBuilder.UnitCubeAtW(0);
            var cube3d = CellRender3d.FromFragment(CellFragment.FromCell(cubeCell, 0), new Camera4dParallel());
            triangle.CutOut(cube3d.DefiningHalfSpaces());
            Assert.That(triangle.faces.Count, Is.EqualTo(0));
        }
    }
}
