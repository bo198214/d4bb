using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using D4BB.Geometry;
using D4BB.Geometry2;
using D4BB.Transforms;

namespace D4BB.Geometry2Tests {

    /// Sensitivity guards for the parity harness in Scene4dParityTests: a coverage-based
    /// comparison that silently passed on everything would make the parity suite worthless,
    /// so these tests prove it flags known discrepancies. Plus one structural pin about
    /// polycube BSPs that the parity suite's cap-face exclusion relies on.
    public class ParityHarnessSelfTests {

        static Camera4dParallel Cam(double a1, double a2) {
            var cam = new Camera4dParallel();
            cam.RotateBasisXY(a1);
            cam.RotateBasisZW(a2);
            return cam;
        }

        static List<TestGeom.Poly> Scene4dSide(int[][] cells, ICamera4d cam) {
            var scene = new Scene4d(new[] { cells }, cam,
                showIntraCoplanarEdges: false, cullBackFaces: true,
                showGridDivisions: false, enable4dOcclusion: true);
            return TestGeom.Prepare(scene.pieces[0].visibleFacets.Select(f => f.points));
        }

        static List<TestGeom.Poly> Geometry2Side(int[][] cells, ICamera4d cam, bool applyCutOut) {
            var complex = IntegerComplex4dBuilder.Boundary(PolycubeFigures.AsIntegerCells(cells));
            var processed = RenderPipeline.Process(complex, cam,
                useBsp: true, applyCutOut: applyCutOut, backfaceCulling: true);
            var polys = new List<List<Point>>();
            foreach (var cell in processed)
                for (int i = 0; i < cell.faces.Count; i++) {
                    int id = cell.faceIds[i];
                    if (id < 0 || complex.IsCoplanarFace(id)) continue;
                    polys.Add(cell.faces[i]);
                }
            return TestGeom.Prepare(polys);
        }

        // Disabling CutOut leaves occluded geometry in place; the harness must report it as
        // EXTRA. Angle chosen so CutOut actually engages for L3 (verified by measurement).
        [Test] public void HarnessFlagsUncutGeometryAsExtra() {
            var cells = PolycubeFigures.ByName("L3");
            var cam = Cam(0.67, 1.21);
            var expected = Scene4dSide(cells, cam);
            var uncut = Geometry2Side(cells, cam, applyCutOut: false);
            Assert.That(TestGeom.CoverageMismatches(uncut, expected), Is.Not.Empty,
                "harness failed to flag uncut occluded geometry (applyCutOut=false)");
            // …and with CutOut on, the same configuration is clean (the parity baseline).
            var cut = Geometry2Side(cells, cam, applyCutOut: true);
            Assert.That(TestGeom.CoverageMismatches(cut, expected), Is.Empty);
        }

        // Displaced geometry must be flagged in BOTH directions (missing and extra).
        [Test] public void HarnessFlagsShiftedGeometryBothWays() {
            var cells = PolycubeFigures.ByName("L3");
            var cam = Cam(0.67, 1.21);
            var expected = Scene4dSide(cells, cam);
            var shifted = TestGeom.Prepare(expected.Select(p =>
                p.points.Select(v => new Point(v.x[0] + 0.05, v.x[1], v.x[2])).ToList()));
            Assert.That(TestGeom.CoverageMismatches(expected, shifted), Is.Not.Empty, "missing not flagged");
            Assert.That(TestGeom.CoverageMismatches(shifted, expected), Is.Not.Empty, "extra not flagged");
        }

        // Structural pin: a lattice cube's supporting hyperplane (xᵢ = k) can never strictly
        // separate the vertices of another lattice cube, so Bsp4d over a polycube figure
        // NEVER splits a cell — no synthetic cap faces exist, in any pose (STRADDLE is
        // intrinsic geometry, invariant under rotating the whole complex). This is why the
        // parity suite's cap-face exclusion cannot mask a cap-related bug for grid figures,
        // and why cap-face rendering can only matter for non-grid polychora.
        [Test] public void PolycubeBsp_NeverSplits() {
            foreach (var (name, cells) in PolycubeFigures.All) {
                foreach (var (a1, a2) in new[] { (0.0, 0.0), (0.67, 1.21), (2.13, 0.42) }) {
                    var complex = IntegerComplex4dBuilder.Boundary(PolycubeFigures.AsIntegerCells(cells));
                    if (a1 != 0 || a2 != 0) TestGeom.RotateComplex(complex, a1, a2);
                    var bsp = Bsp4d.Build(complex);
                    int fragments = 0;
                    void Walk(BspNode n) {
                        if (n == null) return;
                        fragments += n.coincident.Count;
                        Walk(n.positive); Walk(n.negative);
                    }
                    Walk(bsp.root);
                    Assert.That(fragments, Is.EqualTo(complex.cells.Count),
                        $"{name} (a1={a1:F2}, a2={a2:F2}): BSP split a lattice cube");
                }
            }
        }
    }
}
