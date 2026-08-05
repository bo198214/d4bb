using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using D4BB.Geometry;
using D4BB.Transforms;

namespace D4BB.Geometry2Tests {

    /// The decoupling guarantee for the formerly forbidden mixed mode (OCCLUSION-PROOF.md
    /// "Backfaces", Scene4d-pipeline.md "cullBackFaces × enable4dOcclusion"):
    ///
    ///   With 4D occlusion ON, cullBackFaces OFF must render the SAME visible geometry as
    ///   cullBackFaces ON.
    ///
    /// Every camera-averted cell is hidden surface — it is fully cut away either by a strictly
    /// nearer tesseract's front cells (the ordinary painter cut) or by its OWN parent tesseract's
    /// front cells (the same-parent front-over-back rule in OccludePieceCells, enabled by the
    /// averted-first equal-depth tiebreak in SortFarToNear). The front cells' own cut results must
    /// stay untouched by the extra averted occludees. This is what re-decoupled the two GameMenu
    /// toggles (the "occlusion ⇒ culling" invariant in Game.SetOcclusion4d is gone).
    ///
    /// Compared by mutual sampling coverage (like Scene4dParityTests), not polygon lists —
    /// fragmentation may legitimately differ between the two runs. "missing" = front geometry the
    /// mixed mode lost (over-cut); "extra" = backface remnants that survived (under-cut).
    public class Scene4dBackfaceRemovalTests {

        static List<List<Point>> VisiblePolygons(int[][][] pieceOrigins, ICamera4d cam, bool cull) {
            var scene = new Scene4d(pieceOrigins, cam,
                showIntraCoplanarEdges: false, cullBackFaces: cull,
                showGridDivisions: false, enable4dOcclusion: true);
            return scene.pieces.SelectMany(pc => pc.visibleFacets)
                .Select(f => f.points).ToList();
        }

        static readonly (string name, int i, int j)[] Planes = {
            ("XY", 0, 1), ("XZ", 0, 2), ("XW", 0, 3), ("YZ", 1, 2), ("YW", 1, 3), ("ZW", 2, 3),
        };
        // single: the pure same-parent case (every cut in the mixed mode comes from the new rule).
        // The rest: sibling tesseracts (bar2/L3/T4), concavities (T4), a hidden cavity (box3d) and
        // the genuinely-4D tunnel cavities.
        static readonly string[] Figures = { "single", "bar2", "L3", "T4", "box3d", "tunnel1d", "tunnel2d" };

        static System.Collections.IEnumerable SweepCases() {
            foreach (var figure in Figures) {
                int step = PolycubeFigures.IsLarge(figure) ? 30 : 10;
                foreach (var (planeName, _, _) in Planes)
                    for (int deg = 0; deg < 360; deg += step)
                        yield return new TestCaseData(figure, planeName, deg)
                            .SetName($"{figure}_{planeName}_deg={deg:D3}");
            }
        }

        [Test, TestCaseSource(nameof(SweepCases))]
        public void CullingOff_SameVisibleGeometry_AsCullingOn(string figureName, string planeName, int deg) {
            var (_, i, j) = System.Array.Find(Planes, p => p.name == planeName);
            double angle = deg * System.Math.PI / 180.0;
            var ei = new Point4d(i == 0 ? 1 : 0, i == 1 ? 1 : 0, i == 2 ? 1 : 0, i == 3 ? 1 : 0);
            var ej = new Point4d(j == 0 ? 1 : 0, j == 1 ? 1 : 0, j == 2 ? 1 : 0, j == 3 ? 1 : 0);
            var cam = new Camera4dParallel();
            cam.rotate(-angle, ei, ej, null);

            AssertSameCoverage(figureName, $"{planeName} deg={deg}",
                new[] { PolycubeFigures.ByName(figureName) }, cam);
        }

        // Cross-piece backface removal: two separate pieces stacked so that at generic angles one
        // piece's front cells must also carve the other piece's backfaces (different parent depths
        // — the ordinary painter cut, but now with averted occludees enqueued).
        [Test] public void TwoPieces_Stacked_CullingOffMatches() {
            var pieces = new int[][][] {
                new[] { new int[] { 0, 0, 0, 0 }, new int[] { 1, 0, 0, 0 } },
                new[] { new int[] { 0, 0, 1, 0 } },
            };
            foreach (var deg in new[] { 20, 110, 200, 290 }) {
                var cam = new Camera4dParallel();
                cam.rotate(-deg * System.Math.PI / 180.0, new Point4d(1, 0, 0, 0), new Point4d(0, 0, 1, 0), null);
                AssertSameCoverage("twoPieces", $"XZ deg={deg}", pieces, cam);
            }
        }

        static void AssertSameCoverage(string figureName, string caseDesc, int[][][] pieceOrigins, ICamera4d cam) {
            var culled = TestGeom.Prepare(VisiblePolygons(pieceOrigins, cam, cull: true));    // ground truth
            var mixed = TestGeom.Prepare(VisiblePolygons(pieceOrigins, cam, cull: false));

            Assert.That(culled.Count, Is.GreaterThan(0), "culling-on scene produced no visible faces — harness problem");
            Assert.That(mixed.Count, Is.GreaterThan(0), "mixed-mode scene produced no visible faces");

            var missing = TestGeom.CoverageMismatches(culled, mixed);
            var extra = TestGeom.CoverageMismatches(mixed, culled);

            Assert.That(missing.Count + extra.Count, Is.EqualTo(0),
                $"{figureName} {caseDesc}: " +
                $"{culled.Count} culling-on polys vs {mixed.Count} culling-off polys\n" +
                Describe("MISSING with culling off (front geometry over-cut)", missing) +
                Describe("EXTRA with culling off (backface remnant survived)", extra));
        }

        static string Describe(string label, List<string> mismatches) {
            if (mismatches.Count == 0) return "";
            const int cap = 8;
            var shown = mismatches.Take(cap);
            var more = mismatches.Count > cap ? $"\n  … and {mismatches.Count - cap} more" : "";
            return $"{label} ({mismatches.Count}):\n  " + string.Join("\n  ", shown) + more + "\n";
        }
    }
}
