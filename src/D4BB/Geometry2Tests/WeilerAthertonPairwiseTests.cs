using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using D4BB.Geometry;
using D4BB.Geometry2;

namespace D4BB.Geometry2Tests {

    /// Compares the BSP-free pairwise Weiler-Atherton pipeline
    /// (RenderPipeline2.ProcessPairwise) against the BSP+CutOut reference
    /// (RenderPipeline.Process, Sutherland-Hodgman) on the typical polytesseracts of
    /// PolycubeFigures, sweeping all 6 coordinate rotation planes.
    ///
    /// Comparison metric: visible area per source cell. Both pipelines compute the same
    /// exact hidden-surface removal (visible region = cell surface minus the union of
    /// nearer cells' projected volumes), so the areas must agree regardless of how each
    /// pipeline fragments the polygons. The BSP+WA mode (RenderPipeline2.Process) is
    /// asserted against the same reference in the same sweep.
    public class WeilerAthertonPairwiseTests {

        static readonly (string name, int i, int j)[] Planes = {
            ("XY", 0, 1), ("XZ", 0, 2), ("XW", 0, 3), ("YZ", 1, 2), ("YW", 1, 3), ("ZW", 2, 3),
        };
        // All non-convex figures; convex ones are covered by ConvexFigures_FixedAngles.
        static readonly string[] Figures = { "L3", "Lw3", "T4", "S4", "rnd5", "rnd7", "box3d" };

        static System.Collections.IEnumerable SweepCases() {
            foreach (var figure in Figures)
                foreach (var (planeName, _, _) in Planes)
                    yield return new TestCaseData(figure, planeName).SetName($"{figure}_{planeName}");
        }

        [Test, TestCaseSource(nameof(SweepCases))]
        public void PairwiseWA_MatchesBspReference(string figureName, string planeName) {
            var (_, pi, pj) = System.Array.Find(Planes, p => p.name == planeName);
            var cells = PolycubeFigures.ByName(figureName);
            var failures = new List<string>();
            for (int deg = 0; deg < 360; deg += 30) {
                double angle = deg * System.Math.PI / 180.0;
                var complex = IntegerComplex4dBuilder.Boundary(PolycubeFigures.AsIntegerCells(cells));
                TestGeom.RotateComplexInPlane(complex, pi, pj, angle);
                var cam = new Camera4dParallel();

                var reference = AreasSH(RenderPipeline.Process(complex, cam,
                    useBsp: true, applyCutOut: true, backfaceCulling: true));
                var pairwise = AreasWA(RenderPipeline2.ProcessPairwise(complex, cam,
                    applyCutOut: true, backfaceCulling: true));
                var bspWA = AreasWA(RenderPipeline2.Process(complex, cam,
                    useBsp: true, applyCutOut: true, backfaceCulling: true));

                Compare(reference, pairwise, $"deg={deg} pairwise-WA", failures);
                Compare(reference, bspWA, $"deg={deg} bsp-WA", failures);
            }
            Assert.That(failures, Is.Empty,
                $"{figureName} {planeName}: {failures.Count} area mismatches\n  " +
                string.Join("\n  ", failures.Take(12)));
        }

        /// Convex sanity figures at a generic double rotation (every cell pair of a convex
        /// body is ordered by either supporting hyperplane, and front cells never overlap —
        /// pairwise cutting must be a near-no-op that still matches the BSP reference).
        [Test] public void ConvexFigures_FixedAngles() {
            foreach (var figureName in new[] { "single", "bar2", "block221" }) {
                var cells = PolycubeFigures.ByName(figureName);
                var complex = IntegerComplex4dBuilder.Boundary(PolycubeFigures.AsIntegerCells(cells));
                TestGeom.RotateComplexInPlane(complex, 0, 3, 0.35);
                TestGeom.RotateComplexInPlane(complex, 1, 2, 0.75);
                var cam = new Camera4dParallel();
                var reference = AreasSH(RenderPipeline.Process(complex, cam,
                    useBsp: true, applyCutOut: true, backfaceCulling: true));
                var pairwise = AreasWA(RenderPipeline2.ProcessPairwise(complex, cam,
                    applyCutOut: true, backfaceCulling: true));
                var failures = new List<string>();
                Compare(reference, pairwise, figureName, failures);
                Assert.That(failures, Is.Empty, string.Join("\n", failures));
            }
        }

        // ── helpers ────────────────────────────────────────────────────────────

        const double AreaTolerance = 2e-3;

        static void Compare(Dictionary<int, double> reference, Dictionary<int, double> actual,
                            string label, List<string> failures) {
            foreach (var key in reference.Keys.Union(actual.Keys)) {
                double expected = reference.GetValueOrDefault(key);
                double got = actual.GetValueOrDefault(key);
                if (System.Math.Abs(expected - got) > AreaTolerance)
                    failures.Add($"{label}: cell {key} area {got:F6} (reference {expected:F6})");
            }
        }

        /// Visible area per source cell for Sutherland-Hodgman output (convex disjoint
        /// fragments: unsigned polygon areas).
        static Dictionary<int, double> AreasSH(List<CellRender3d> cellsOut) {
            var areas = new Dictionary<int, double>();
            foreach (var c in cellsOut) {
                double a = 0;
                foreach (var f in c.faces) a += WeilerAtherton.NewellNormal(f).len() / 2;
                areas[c.sourceCellId] = areas.GetValueOrDefault(c.sourceCellId) + a;
            }
            return areas;
        }

        /// Visible area per source cell for Weiler-Atherton output (signed ring areas:
        /// outer positive, holes negative).
        static Dictionary<int, double> AreasWA(List<CellRenderWA3d> cellsOut) {
            var areas = new Dictionary<int, double>();
            foreach (var c in cellsOut) {
                double a = 0;
                foreach (var f in c.faces) {
                    a += WeilerAtherton.SignedArea(f.outer, f.planeNormal);
                    foreach (var h in f.holes) a += WeilerAtherton.SignedArea(h, f.planeNormal);
                }
                areas[c.sourceCellId] = areas.GetValueOrDefault(c.sourceCellId) + a;
            }
            return areas;
        }
    }
}
