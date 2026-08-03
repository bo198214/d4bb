using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using D4BB.Comb;
using D4BB.Geometry;
using D4BB.Transforms;

namespace D4BB.Geometry2Tests {

    /// Cross-piece occlusion soundness for Scene4d on the shipping tunnel-2d level scenario:
    /// the 3x3x3x3 tunnel block (piece 0) plus its 3x3x1x1 plug (piece 1) at several
    /// insertion depths — the multi-piece configuration no other suite covers (the parity
    /// harness is single-piece by construction: contact faces between touching pieces
    /// vanish in a Geometry2 union boundary, so a union comparison would be semantically
    /// wrong, not just fragile).
    ///
    /// Both directions of the occlusion theorem (OCCLUSION-PROOF.md) are checked without a
    /// second pipeline, and epsilon-robustly: lattice scenes place contact faces EXACTLY on
    /// occluder-hull boundaries (the systematically degenerate set of the proof), so both
    /// invariants assert only clearly outside a MARGIN band around those boundaries and
    /// deliberately leave the band itself unjudged (its semantics are AOP.ERR convention):
    ///
    ///   Under-cut: no surviving facet sample lies strictly (margin) inside the projected
    ///   hull of a front-facing boundary cell with strictly nearer parent depth.
    ///   Over-cut:  every UNCUT facet sample that is clearly outside every strictly nearer
    ///   hull (margin outward — unambiguously visible) is still covered by the occluded
    ///   scene's visible facets of its piece.
    ///
    /// Reference cells come from raw per-piece IntegerBoundaryComplex runs, independent of
    /// Scene4d's occluder bookkeeping (same rationale as Scene4dOcclusionSoundnessTests).
    /// Depth is the provable parent-tesseract key; equal parent depths never occlude
    /// (proof, Lemma 2), so strictly-nearer uses a tie tolerance.
    public class Scene4dMultiPieceSoundnessTests {

        static readonly (string name, int i, int j)[] Planes = {
            ("XY", 0, 1), ("XZ", 0, 2), ("XW", 0, 3), ("YZ", 1, 2), ("YW", 1, 3), ("ZW", 2, 3),
        };
        // Plug x-offsets: start = the level's initial gap position, half = two cell columns
        // inside the tunnel (contact faces along the tunnel walls) with one sticking out,
        // full = solved (plug flush everywhere, incl. coplanar edge-adjacent mouth fill).
        static readonly (string name, int dx)[] Insertions = { ("start", 4), ("half", 1), ("full", 0) };

        const double Margin = 1e-3;

        static int[][] PlugCells(int dx) {
            var cells = new List<int[]>();
            for (int i = 0; i < 3; i++)
                for (int y = 0; y < 3; y++)
                    cells.Add(new[] { dx + i, y, 1, 1 });
            return cells.ToArray();
        }

        static System.Collections.IEnumerable SweepCases() {
            foreach (var (insertion, _) in Insertions)
                foreach (var (planeName, _, _) in Planes)
                    yield return new TestCaseData(insertion, planeName).SetName($"plug_{insertion}_{planeName}");
        }

        [Test, TestCaseSource(nameof(SweepCases))]
        public void TunnelWithPlug_NoUnderCut_NoOverCut(string insertion, string planeName) {
            var (_, pi, pj) = System.Array.Find(Planes, p => p.name == planeName);
            int dx = System.Array.Find(Insertions, x => x.name == insertion).dx;
            var pieceOrigins = new[] { PolycubeFigures.ByName("tunnel2d"), PlugCells(dx) };
            var violations = new List<string>();

            for (int deg = 0; deg < 360; deg += 30) {
                double angle = deg * System.Math.PI / 180.0;
                var ei = new Point4d(pi == 0 ? 1 : 0, pi == 1 ? 1 : 0, pi == 2 ? 1 : 0, pi == 3 ? 1 : 0);
                var ej = new Point4d(pj == 0 ? 1 : 0, pj == 1 ? 1 : 0, pj == 2 ? 1 : 0, pj == 3 ? 1 : 0);
                var cam = new Camera4dParallel();
                cam.rotate(-angle, ei, ej, null);

                var occluded = new Scene4d(pieceOrigins, cam,
                    showIntraCoplanarEdges: false, cullBackFaces: true,
                    showGridDivisions: false, enable4dOcclusion: true);
                var uncut = new Scene4d(pieceOrigins, cam,
                    showIntraCoplanarEdges: false, cullBackFaces: true,
                    showGridDivisions: false, enable4dOcclusion: false);

                // Scene4d-independent reference occluders: every front-facing boundary cell
                // of every piece, with parent depth + projected hull.
                var fronts = new List<(double depth, HalfSpace[] hs)>();
                foreach (var origins in pieceOrigins)
                    foreach (var c3 in new IntegerBoundaryComplex(origins).cells) {
                        if (!cam.IsFacedBy(new Point(c3.origin), new Point(c3.Normal()))) continue;
                        fronts.Add((ParentDepth(c3, cam), Scene4d.DefiningHalfSpaces(c3, cam)));
                    }

                var visibleByPiece = new List<TestGeom.Poly>[occluded.pieces.Length];
                for (int p = 0; p < occluded.pieces.Length; p++)
                    visibleByPiece[p] = TestGeom.Prepare(occluded.pieces[p].visibleFacets.Select(f => f.points));

                // ── under-cut: surviving samples must not be clearly inside a nearer hull ──
                for (int p = 0; p < occluded.pieces.Length; p++)
                    foreach (var cb in occluded.pieces[p].cells) {
                        double selfDepth = ParentDepth(cb.cell, cam);
                        foreach (var facet in cb.pbc.d2faces) {
                            var prepared = TestGeom.Prepare(new List<List<Point>> { facet.points });
                            if (prepared.Count == 0) continue;
                            foreach (var sample in TestGeom.InteriorSamples(prepared[0])) {
                                foreach (var (depth, hs) in fronts) {
                                    if (depth >= selfDepth - 1e-9) continue;
                                    if (!InsideBeyond(sample, hs, +Margin)) continue;
                                    if (violations.Count < 8)
                                        violations.Add($"deg={deg} {insertion}: UNDER-CUT — surviving sample of piece {p} " +
                                                       $"cell [{string.Join(",", cb.cell.origin)}] at {TestGeom.Fmt(sample)} " +
                                                       $"lies inside a nearer cell's hull");
                                    goto nextFacetU;
                                }
                            }
                            nextFacetU: ;
                        }
                    }

                // ── over-cut: clearly visible uncut samples must survive ──
                for (int p = 0; p < uncut.pieces.Length; p++)
                    foreach (var cb in uncut.pieces[p].cells) {
                        double selfDepth = ParentDepth(cb.cell, cam);
                        foreach (var facet in cb.pbc.d2faces) {
                            var prepared = TestGeom.Prepare(new List<List<Point>> { facet.points });
                            if (prepared.Count == 0) continue;
                            foreach (var sample in TestGeom.InteriorSamples(prepared[0])) {
                                bool occludedOrEpsilon = false;
                                foreach (var (depth, hs) in fronts) {
                                    if (depth >= selfDepth - 1e-9) continue;
                                    if (InsideBeyond(sample, hs, -Margin)) { occludedOrEpsilon = true; break; }
                                }
                                if (occludedOrEpsilon) continue;   // hidden or inside the margin band — not judged
                                if (TestGeom.IsCovered(sample, visibleByPiece[p])) continue;
                                if (violations.Count < 8)
                                    violations.Add($"deg={deg} {insertion}: OVER-CUT — clearly visible sample of piece {p} " +
                                                   $"cell [{string.Join(",", cb.cell.origin)}] at {TestGeom.Fmt(sample)} " +
                                                   $"was cut away");
                                goto nextFacetO;
                            }
                            nextFacetO: ;
                        }
                    }
            }
            Assert.That(violations, Is.Empty,
                $"tunnel2d+plug {insertion} {planeName}: {violations.Count} cross-piece soundness violations\n  " +
                string.Join("\n  ", violations));
        }

        /// View depth of the cell's parent tesseract center — the provable occlusion key
        /// (OCCLUSION-PROOF.md); duplicated from Scene4dOcclusionSoundnessTests by design
        /// (each suite states its own invariant completely).
        static double ParentDepth(OrientedIntegerCell c3, Camera4dParallel cam) {
            var c = c3.Center();
            var n = c3.Normal();
            double depth = 0;
            for (int k = 0; k < 4; k++) depth += cam.viewNormal.x[k] * (c[k] - 0.5 * n[k]);
            return depth;
        }

        /// True iff p lies inside every halfspace by more than `beyond` (positive: clearly
        /// inside the hull; negative: inside up to a tolerance band around the boundary).
        static bool InsideBeyond(Point p, HalfSpace[] hs, double beyond) {
            foreach (var h in hs) {
                double d = -(h.normal.sc(p) - h.normal.sc(h.origin()));   // >0 = inside this halfspace
                if (d < beyond) return false;
            }
            return true;
        }
    }
}
