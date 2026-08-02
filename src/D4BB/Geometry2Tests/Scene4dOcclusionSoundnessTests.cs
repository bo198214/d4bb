using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using D4BB.Comb;
using D4BB.Geometry;
using D4BB.Transforms;

namespace D4BB.Geometry2Tests {

    /// The OcclusionSoundnessTests under-cut invariant applied to the OLD pipeline
    /// (Scene4d, the shipping game's renderer) instead of Geometry2:
    ///
    ///   No surviving visible-facet sample may lie strictly inside the projected
    ///   volume of a strictly NEARER front-facing 3-cell — such a sample is occluded
    ///   and should have been cut away.
    ///
    /// Together with OcclusionSoundnessTests this resolves parity mismatches to a
    /// culprit: a "MISSING in Geometry2" parity failure is Scene4d's fault exactly
    /// when the missing area violates this invariant (Scene4d under-cut) and
    /// Geometry2's own soundness stays green.
    ///
    /// Motivation (2026-08): the tunnel-1d/tunnel-2d levels show visible occlusion
    /// errors in the game; the tunnel figures fail parity with surviving inner-tunnel-
    /// wall fragments on the Scene4d side.
    public class Scene4dOcclusionSoundnessTests {

        static readonly (string name, int i, int j)[] Planes = {
            ("XY", 0, 1), ("XZ", 0, 2), ("XW", 0, 3), ("YZ", 1, 2), ("YW", 1, 3), ("ZW", 2, 3),
        };
        // box3d/L3 double as controls: figures the parity suite has always accepted.
        static readonly string[] Figures = { "L3", "box3d", "tunnel1d", "tunnel2d" };

        static System.Collections.IEnumerable SweepCases() {
            foreach (var figure in Figures)
                foreach (var (planeName, _, _) in Planes)
                    yield return new TestCaseData(figure, planeName).SetName($"{figure}_{planeName}");
        }

        [Test, TestCaseSource(nameof(SweepCases))]
        public void SurvivingFacets_NeverInsideNearerCell(string figureName, string planeName) {
            var (_, pi, pj) = System.Array.Find(Planes, p => p.name == planeName);
            var cells = PolycubeFigures.ByName(figureName);
            var violations = new List<string>();

            int step = PolycubeFigures.IsLarge(figureName) ? 30 : 10;   // thin coverage for large figures
            for (int deg = 0; deg < 360; deg += step) {
                double angle = deg * System.Math.PI / 180.0;
                // Pose exactly like the parity plane sweep: identity figure viewed through
                // the inversely rotated camera basis.
                var ei = new Point4d(pi == 0 ? 1 : 0, pi == 1 ? 1 : 0, pi == 2 ? 1 : 0, pi == 3 ? 1 : 0);
                var ej = new Point4d(pj == 0 ? 1 : 0, pj == 1 ? 1 : 0, pj == 2 ? 1 : 0, pj == 3 ? 1 : 0);
                var cam = new Camera4dParallel();
                cam.rotate(-angle, ei, ej, null);

                var scene = new Scene4d(new[] { cells }, cam,
                    showIntraCoplanarEdges: false, cullBackFaces: true,
                    showGridDivisions: false, enable4dOcclusion: true);

                // Pre-cut front-facing 3-cells with scalar depth + projected halfspace hulls —
                // the same occluder MODEL Scene4d sorts and cuts with, but built INDEPENDENTLY
                // from the raw IntegerBoundaryComplex, not from scene.pieces[0].cells: if
                // Scene4d's occluder set silently drops a boundary cell, using its own cell
                // list here would drop the same cell and mask exactly that bug.
                var ibc = new IntegerBoundaryComplex(cells);
                var fronts = new List<(OrientedIntegerCell cell, double depth, HalfSpace[] hs)>();
                foreach (var c3 in ibc.cells) {
                    if (!cam.IsFacedBy(new Point(c3.origin), new Point(c3.Normal()))) continue;
                    fronts.Add((c3, ParentDepth(c3, cam), Scene4d.DefiningHalfSpaces(c3, cam)));
                }

                const double margin = 1e-3;
                foreach (var cb in scene.pieces[0].cells) {
                    var c3 = cb.cell;
                    double selfDepth = ParentDepth(c3, cam);
                    foreach (var facet in cb.pbc.d2faces) {
                        var prepared = TestGeom.Prepare(new List<List<Point>> { facet.points });
                        if (prepared.Count == 0) continue;
                        foreach (var sample in TestGeom.InteriorSamples(prepared[0])) {
                            foreach (var (otherCell, otherDepth, hs) in fronts) {
                                // No self-skip needed: the cell's own IBC twin has identical
                                // depth and is skipped by the strictly-nearer guard.
                                if (otherDepth >= selfDepth - 1e-9) continue;   // only strictly nearer cells occlude
                                if (!StrictlyInside(sample, hs, margin)) continue;
                                if (violations.Count < 8)
                                    violations.Add($"deg={deg}: surviving facet sample of cell " +
                                                   $"[{string.Join(",", c3.origin)}|span {string.Join(",", c3.span)}] " +
                                                   $"at {TestGeom.Fmt(sample)} lies inside nearer cell " +
                                                   $"[{string.Join(",", otherCell.origin)}|span {string.Join(",", otherCell.span)}] — under-cut");
                                goto nextFacet;
                            }
                        }
                        nextFacet: ;
                    }
                }
            }
            Assert.That(violations, Is.Empty,
                $"{figureName} {planeName}: {violations.Count} Scene4d occlusion-soundness violations\n  " +
                string.Join("\n  ", violations));
        }

        /// The provable depth key (see OCCLUSION-PROOF.md): view depth of the cell's parent
        /// tesseract center (cell center − ½·outward normal). The occlusion theorem orders
        /// parent tesseracts, not the flat 3-cells, so the invariant must compare exactly this.
        static double ParentDepth(OrientedIntegerCell c3, Camera4dParallel cam) {
            var c = c3.Center();
            var n = c3.Normal();
            double depth = 0;
            for (int k = 0; k < 4; k++) depth += cam.viewNormal.x[k] * (c[k] - 0.5 * n[k]);
            return depth;
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
