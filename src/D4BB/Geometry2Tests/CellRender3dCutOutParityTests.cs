using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using D4BB.Geometry;
using D4BB.Geometry2;

namespace D4BB.Geometry2Tests {

    /// Mirrors the existing CutOutTest_Half / _L / CutOutTest tests in
    /// PolyhedronBoundaryComplexTests, but exercises CellRender3d.CutOut instead of
    /// Polyhedron3dBoundaryComplex.CutOut. The two implementations should produce the
    /// same number of surviving polygons on the same geometry — divergence points to a
    /// routing/orientation bug in CellRender3d.
    public class CellRender3dCutOutParityTests {

        /// Builds a CellRender3d for an axis-aligned unit cube at `origin` with side `side`.
        /// 6 face polygons in cyclic order; faceIds = -1 (synthetic).
        static CellRender3d UnitCube(Point origin, double side) {
            double x0 = origin.x[0], y0 = origin.x[1], z0 = origin.x[2];
            double x1 = x0 + side, y1 = y0 + side, z1 = z0 + side;
            Point P(double x, double y, double z) => new Point(x, y, z);
            var faces = new List<List<Point>> {
                // x=lo: outward normal -x; cyclic order so right-hand rule gives outward = -x
                new List<Point> { P(x0, y0, z0), P(x0, y0, z1), P(x0, y1, z1), P(x0, y1, z0) },
                // x=hi: outward +x
                new List<Point> { P(x1, y0, z0), P(x1, y1, z0), P(x1, y1, z1), P(x1, y0, z1) },
                // y=lo: outward -y
                new List<Point> { P(x0, y0, z0), P(x1, y0, z0), P(x1, y0, z1), P(x0, y0, z1) },
                // y=hi: outward +y
                new List<Point> { P(x0, y1, z0), P(x0, y1, z1), P(x1, y1, z1), P(x1, y1, z0) },
                // z=lo: outward -z
                new List<Point> { P(x0, y0, z0), P(x0, y1, z0), P(x1, y1, z0), P(x1, y0, z0) },
                // z=hi: outward +z
                new List<Point> { P(x0, y0, z1), P(x1, y0, z1), P(x1, y1, z1), P(x0, y1, z1) },
            };
            return new CellRender3d {
                sourceCellId = -1,
                faces = faces,
                faceIds = Enumerable.Repeat(-1, 6).ToList(),
            };
        }

        /// Reproduces CutOutTest_Half: source = unit cube at (0,0,0), cutter = unit cube at (-0.5, 0, 0).
        /// The cutter overlaps source on the x ∈ [0, 0.5] slab. Expected:
        ///   - source's x=0 face is entirely inside cutter ⇒ removed.
        ///   - source's x=1 face is entirely outside cutter ⇒ intact.
        ///   - source's 4 lateral faces (y=0, y=1, z=0, z=1) straddle x=0.5 ⇒ each clipped,
        ///     outer fragment (x ∈ [0.5, 1]) survives.
        /// PolyhedronBoundaryComplex says: 5 surviving facets. CellRender3d should agree.
        [Test] public void CutOutTest_Half_Parity() {
            var src = UnitCube(new Point(0, 0, 0), 1.0);
            var cutter = UnitCube(new Point(-0.5, 0, 0), 1.0);
            src.CutOut(cutter.DefiningHalfSpaces());
            Assert.That(src.faces.Count, Is.EqualTo(5));
        }

        /// Reproduces CutOutTest_L: cutter offset in TWO axes ⇒ "L-shaped" remaining region.
        [Test] public void CutOutTest_L_Parity() {
            var src = UnitCube(new Point(0, 0, 0), 1.0);
            var cutter = UnitCube(new Point(-0.5, -0.5, 0), 1.0);
            src.CutOut(cutter.DefiningHalfSpaces());
            for (int i = 0; i < src.faces.Count; i++) {
                var f = src.faces[i];
                var verts = string.Join(" ", f.Select(v => $"({v.x[0]:F2},{v.x[1]:F2},{v.x[2]:F2})"));
                TestContext.WriteLine($"  poly {i} ({f.Count}v): {verts}");
            }
            Assert.That(src.faces.Count, Is.EqualTo(8));
        }

        /// Reproduces CutOutTest: cutter offset in THREE axes (corner cut). Doesn't pin a
        /// specific count (the original test uses a Does.Not.Contains check with specific
        /// polygons). We verify the cube isn't entirely emptied and at least the far-corner
        /// face (x=1) survives unchanged.
        // ── parity with PreSplittedCut3d (Face2dBCTests) ──────────────────────
        // Cube cut by single halfspace at x=0.5: PBC says 5 facets after CutOut.
        [Test] public void PreSplittedCut3d_Parity() {
            var src = UnitCube(new Point(0, 0, 0), 1.0);
            // The x=0.5 plane with normal (1,0,0): "inside" = x < 0.5 half (per HalfSpace ctor).
            var hs = new HalfSpace(0.5, new Point(1, 0, 0).normalize());
            src.CutOut(new[] { hs });
            Assert.That(src.faces.Count, Is.EqualTo(5));
        }

        // ── parity with DiagSplittedCube (Face2dBCTests) ──────────────────────
        // Cube cut by oblique halfspaces. Same geometry, same CutOut, same face counts.
        [Test] public void DiagSplittedCube_Hs1Alone_Parity() {
            var src = UnitCube(new Point(0, 0, 0), 1.0);
            var hs1 = new HalfSpace(new Point(0.6, 0.5, 0), new Point(1, 1, 0).normalize());
            src.CutOut(new[] { hs1 });
            Assert.That(src.faces.Count, Is.EqualTo(4));
        }

        [Test] public void DiagSplittedCube_Hs2Alone_Parity() {
            var src = UnitCube(new Point(0, 0, 0), 1.0);
            var hs2 = new HalfSpace(new Point(0.5, 0.5, 0), new Point(-1, 1, 0).normalize());
            src.CutOut(new[] { hs2 });
            Assert.That(src.faces.Count, Is.EqualTo(4));
        }

        [Test] public void DiagSplittedCube_BothHs_Parity() {
            var src = UnitCube(new Point(0, 0, 0), 1.0);
            var hs1 = new HalfSpace(new Point(0.6, 0.5, 0), new Point(1, 1, 0).normalize());
            var hs2 = new HalfSpace(new Point(0.5, 0.5, 0), new Point(-1, 1, 0).normalize());
            src.CutOut(new[] { hs1, hs2 });
            Assert.That(src.faces.Count, Is.EqualTo(7));
        }

        // ── parity with CutOutGroove (Face2dBCTests) ──────────────────────────
        // Groove-shape cuts produced by V-pair of oblique halfspaces; offset variants change count.
        [Test] public void CutOutGroove_Centered_Parity() {
            var src = UnitCube(new Point(0, 0, 0), 1.0);
            var hs1 = new HalfSpace(new Point(0.5, 0.5, 0), new Point(1, 1, 0).normalize());
            var hs2 = new HalfSpace(new Point(0.5, 0.5, 0), new Point(-1, 1, 0).normalize());
            src.CutOut(new[] { hs1, hs2 });
            Assert.That(src.faces.Count, Is.EqualTo(7));
        }

        [Test] public void CutOutGroove_OffsetPositive_Parity() {
            var src = UnitCube(new Point(0, 0, 0), 1.0);
            var hs1 = new HalfSpace(new Point(0.6, 0.5, 0), new Point(1, 1, 0).normalize());
            var hs2 = new HalfSpace(new Point(0.5, 0.5, 0), new Point(-1, 1, 0).normalize());
            src.CutOut(new[] { hs1, hs2 });
            Assert.That(src.faces.Count, Is.EqualTo(7));
        }

        [Test] public void CutOutGroove_OffsetNegative_Parity() {
            var src = UnitCube(new Point(0, 0, 0), 1.0);
            var hs1 = new HalfSpace(new Point(0.4, 0.5, 0), new Point(1, 1, 0).normalize());
            var hs2 = new HalfSpace(new Point(0.5, 0.5, 0), new Point(-1, 1, 0).normalize());
            src.CutOut(new[] { hs1, hs2 });
            Assert.That(src.faces.Count, Is.EqualTo(8));
        }

        // ── parity with HellyNoCut (Face2dBCTests) ────────────────────────────
        // Helly case: every halfspace individually intersects the cube, but the intersection
        // of all three is empty inside the cube — so nothing should be removed (6 facets).
        [Test] public void HellyNoCut_Parity() {
            var src = UnitCube(new Point(0, 0, 0), 1.0);
            var hs1 = new HalfSpace(new Point(0.6, 0, 0), new Point(-1, 0, 0));
            var hs2 = new HalfSpace(new Point(0, 0, 0.6), new Point(0, 0, -1));
            var hs3 = new HalfSpace(new Point(0.2, 0, 0.2), new Point(1, 0, 1).normalize());
            src.CutOut(new[] { hs1, hs2, hs3 });
            Assert.That(src.faces.Count, Is.EqualTo(6));
        }

        [Test] public void CutOutTest_Corner_Parity() {
            var src = UnitCube(new Point(0, 0, 0), 1.0);
            var cutter = UnitCube(new Point(-0.5, -0.5, -0.5), 1.0);
            src.CutOut(cutter.DefiningHalfSpaces());
            for (int i = 0; i < src.faces.Count; i++) {
                var f = src.faces[i];
                var verts = string.Join(" ", f.Select(v => $"({v.x[0]:F2},{v.x[1]:F2},{v.x[2]:F2})"));
                TestContext.WriteLine($"  poly {i} ({f.Count}v): {verts}");
            }
            // Far corner of x=1 face: at x=1, y∈[0.5,1], z∈[0.5,1]. Since x=1 face is at
            // x=1 (entirely outside cutter's x=+0.5 halfspace), the FaceIntersectsPolyhedron
            // pre-check filters it ⇒ x=1 face stays intact (full unit square at x=1).
            bool xEqOneIntact = src.faces.Any(face =>
                face.Count == 4 &&
                face.All(v => System.Math.Abs(v.x[0] - 1.0) < AOP.ERR));
            Assert.That(xEqOneIntact, Is.True, "x=1 face should survive intact (4-vertex unit square)");
        }
    }
}
