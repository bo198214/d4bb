using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using D4BB.Geometry;
using D4BB.Geometry2;

namespace D4BB.Geometry2Tests {

    /// Scene4d-INDEPENDENT occlusion invariant for the Geometry2 BSP+CutOut pipeline.
    /// The parity suite (Scene4dParityTests) proves both pipelines agree; this suite proves
    /// the agreed-on result is actually sound, so the two cannot share a mistake unnoticed:
    ///
    ///   Under-cut check: no surviving face sample may lie strictly inside the pre-cut
    ///   projected volume of a strictly NEARER front-facing cell — such a sample is
    ///   occluded and should have been cut away.
    ///
    /// Depth is the scalar viewNormal·(parent-tesseract center) order — cell centroid minus
    /// ½·outward normal. The occlusion theorem (OCCLUSION-PROOF.md next to Scene4d) orders the
    /// solid unit tesseracts (interior-disjoint integer translates of one convex body), not the
    /// flat 3-cells: wherever two tesseracts' shadows overlap, their center order IS the
    /// pointwise occlusion order, and equal parent depth implies disjoint shadows.
    ///
    /// Sweeps all 6 coordinate rotation planes in 10° steps (including the XZ family the
    /// XY×ZW grid can't reach — the L-Sequence view where the L visibly self-occludes at
    /// 18 of 36 angles).
    public class OcclusionSoundnessTests {

        static readonly (string name, int i, int j)[] Planes = {
            ("XY", 0, 1), ("XZ", 0, 2), ("XW", 0, 3), ("YZ", 1, 2), ("YW", 1, 3), ("ZW", 2, 3),
        };
        static readonly string[] Figures = { "L3", "Lw3", "T4", "rnd7", "box3d", "tunnel1d", "tunnel2d" };

        static System.Collections.IEnumerable SweepCases() {
            foreach (var figure in Figures)
                foreach (var (planeName, _, _) in Planes)
                    yield return new TestCaseData(figure, planeName).SetName($"{figure}_{planeName}");
        }

        [Test, TestCaseSource(nameof(SweepCases))]
        public void SurvivingFaces_NeverInsideNearerCell(string figureName, string planeName) {
            var (_, pi, pj) = System.Array.Find(Planes, p => p.name == planeName);
            var cells = PolycubeFigures.ByName(figureName);
            var violations = new List<string>();

            int step = PolycubeFigures.IsLarge(figureName) ? 30 : 10;   // thin coverage for large figures
            for (int deg = 0; deg < 360; deg += step) {
                double angle = deg * System.Math.PI / 180.0;
                var complex = IntegerComplex4dBuilder.Boundary(PolycubeFigures.AsIntegerCells(cells));
                TestGeom.RotateComplexInPlane(complex, pi, pj, angle);
                var cam = new Camera4dParallel();

                // Pre-cut front-facing cells with their scalar depth and halfspace hulls.
                var fronts = new Dictionary<int, (double depth, HalfSpace[] hs)>();
                for (int i = 0; i < complex.cells.Count; i++) {
                    var cell = complex.cells[i];
                    if (cell.normal != null) {
                        var probe = complex.vertices[complex.CellVertexIds(i).First()];
                        if (!cam.IsFacedBy(probe, cell.normal)) continue;
                    }
                    var r = CellRender3d.FromFragment(CellFragment.FromCell(complex, i), cam);
                    double depth = 0; int n = 0;
                    foreach (var vid in complex.CellVertexIds(i)) { depth += cam.viewNormal.sc(complex.vertices[vid]); n++; }
                    depth /= n;
                    // parent-tesseract center = cell centroid − ½·outward normal (unit; rotated
                    // along with the vertices) — the provable depth key, see the class doc.
                    if (cell.normal != null) depth -= 0.5 * cam.viewNormal.sc(cell.normal);
                    fronts[i] = (depth, r.DefiningHalfSpaces());
                }

                var processed = RenderPipeline.Process(complex, cam, useBsp: true, applyCutOut: true, backfaceCulling: true);
                const double margin = 1e-3;   // clearly inside, beyond any epsilon snapping
                foreach (var cell in processed) {
                    if (!fronts.TryGetValue(cell.sourceCellId, out var self)) continue;
                    for (int k = 0; k < cell.faces.Count; k++) {
                        if (cell.faceIds[k] < 0) continue;
                        var prepared = TestGeom.Prepare(new List<List<Point>> { cell.faces[k] });
                        if (prepared.Count == 0) continue;
                        foreach (var sample in TestGeom.InteriorSamples(prepared[0])) {
                            foreach (var (otherId, other) in fronts) {
                                if (otherId == cell.sourceCellId) continue;
                                if (other.depth >= self.depth - 1e-9) continue;   // only strictly nearer cells occlude
                                if (!StrictlyInside(sample, other.hs, margin)) continue;
                                if (violations.Count < 8)
                                    violations.Add($"deg={deg}: surviving sample of cell {cell.sourceCellId} " +
                                                   $"face {cell.faceIds[k]} at {TestGeom.Fmt(sample)} lies inside " +
                                                   $"nearer cell {otherId} — under-cut");
                                goto nextFace;
                            }
                        }
                        nextFace: ;
                    }
                }
            }
            Assert.That(violations, Is.Empty,
                $"{figureName} {planeName}: {violations.Count} occlusion-soundness violations\n  " +
                string.Join("\n  ", violations));
        }

        static bool StrictlyInside(Point p, HalfSpace[] hs, double margin) {
            foreach (var h in hs) {
                double d = -(h.normal.sc(p) - h.normal.sc(h.origin()));   // >0 = inside this halfspace
                if (d < margin) return false;
            }
            return true;
        }
    }
}
