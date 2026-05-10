using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using D4BB.Geometry;
using D4BB.Geometry2;

namespace D4BB.Geometry2Tests {

    /// Property: for a CONVEX polychoron, after removing back-facing cells via 4D backface
    /// culling, the 3D parallel-projections of the remaining (front-facing) cells must have
    /// pairwise-disjoint interiors. They tile the visible 3D shadow without overlap.
    ///
    /// This is what the painter in RotatingComplex relies on when useBsp=false: with
    /// non-overlapping projections, draw order doesn't matter for translucent materials and
    /// no BSP / CutOut is needed. If two front-facing cell projections overlap, that
    /// assumption is broken — the polychoron is either not convex, or its cell normals /
    /// face windings in the JSON are inconsistent.
    public class FrontCellsNonOverlappingTests {

        // ── helpers ────────────────────────────────────────────────────────────

        static PolyhedralComplex4d LoadPolychoron(string fileName) {
            var assemblyDir = Path.GetDirectoryName(typeof(FrontCellsNonOverlappingTests).Assembly.Location);
            var d = new DirectoryInfo(assemblyDir);
            while (d != null) {
                var p = Path.Combine(d.FullName, "Assets", "tesserian", "Resources", "polychora", fileName);
                if (File.Exists(p)) {
                    try { return PolyhedralComplex4dJson.FromJson(File.ReadAllText(p)); }
                    catch (System.FormatException ex) {
                        Assert.Ignore($"{fileName}: malformed JSON ({ex.Message}) — skipping");
                        return null;
                    }
                }
                d = d.Parent;
            }
            return null;
        }

        /// Project each front-facing cell of `complex` to 3D using `cam`.
        /// A cell is front-facing iff `cam.IsFacedBy(anyVertex, cell.normal)` is true; cells
        /// without a stored normal are always considered front-facing. (For Camera4dParallel
        /// the origin argument is ignored — the test only depends on the normal sign.)
        static List<CellRender3d> FrontFacingProjections(PolyhedralComplex4d complex, ICamera4d cam) {
            var result = new List<CellRender3d>();
            for (int i = 0; i < complex.cells.Count; i++) {
                var cell = complex.cells[i];
                if (cell.normal != null) {
                    var probe = complex.vertices[complex.CellVertexIds(i).First()];
                    if (!cam.IsFacedBy(probe, cell.normal)) continue;
                }
                var fragment = CellFragment.FromCell(complex, i);
                result.Add(CellRender3d.FromFragment(fragment, cam));
            }
            return result;
        }

        /// Separating-axis test for two convex 3-polytopes. Returns true iff their interiors
        /// overlap (touching at a shared face is NOT considered an overlap).
        ///
        /// For convex polytopes the SAT axes can be restricted to face-supporting halfspace
        /// normals — if any defining halfspace of A has all of B's vertices on its OUTSIDE
        /// (or boundary), then that halfspace separates them. Symmetric for B.
        static bool ProjectionsOverlap(CellRender3d a, CellRender3d b) {
            var verticesA = a.faces.SelectMany(f => f).ToList();
            var verticesB = b.faces.SelectMany(f => f).ToList();
            foreach (var h in a.DefiningHalfSpaces())
                if (verticesB.All(v => h.side(v) >= 0)) return false;
            foreach (var h in b.DefiningHalfSpaces())
                if (verticesA.All(v => h.side(v) >= 0)) return false;
            return true;
        }

        /// Returns the list of (sourceCellId_i, sourceCellId_j) pairs whose projections
        /// have overlapping interiors. Empty list ⇔ tile property holds.
        static List<(int, int)> OverlappingPairs(PolyhedralComplex4d complex, ICamera4d cam) {
            var cells = FrontFacingProjections(complex, cam);
            var pairs = new List<(int, int)>();
            for (int i = 0; i < cells.Count; i++)
                for (int j = i + 1; j < cells.Count; j++)
                    if (ProjectionsOverlap(cells[i], cells[j]))
                        pairs.Add((cells[i].sourceCellId, cells[j].sourceCellId));
            return pairs;
        }

        /// Apply the same per-frame XY/ZW double-rotation that ComplexFrame uses, so the
        /// test matches what RotatingComplex actually shows at runtime. Mutates `complex`
        /// vertices and cell normals in place; calls InvalidateCaches.
        static void Rotate(PolyhedralComplex4d complex, double angle1, double angle2) {
            double c1 = System.Math.Cos(angle1), s1 = System.Math.Sin(angle1);
            double c2 = System.Math.Cos(angle2), s2 = System.Math.Sin(angle2);
            void Rot(Point p) {
                double x = p.x[0], y = p.x[1], z = p.x[2], w = p.x[3];
                p.x[0] = c1 * x - s1 * y;
                p.x[1] = s1 * x + c1 * y;
                p.x[2] = c2 * z - s2 * w;
                p.x[3] = s2 * z + c2 * w;
            }
            foreach (var v in complex.vertices) Rot(v);
            foreach (var cell in complex.cells) if (cell.normal != null) Rot(cell.normal);
            complex.InvalidateCaches();
        }

        static void AssertFrontProjectionsTile(string fileName, double angle1 = 0, double angle2 = 0) {
            var c = LoadPolychoron(fileName);
            if (c == null) { Assert.Ignore($"{fileName} not found"); return; }
            if (angle1 != 0 || angle2 != 0) Rotate(c, angle1, angle2);
            var cam = new Camera4dParallel(new Point4d(0, 0, 0, -5));
            var fronts = FrontFacingProjections(c, cam);
            Assert.That(fronts.Count, Is.GreaterThan(0),
                $"{fileName}: no front-facing cells — camera/normals problem?");
            var overlaps = OverlappingPairs(c, cam);
            Assert.That(overlaps, Is.Empty,
                $"{fileName} (angle1={angle1:F2}, angle2={angle2:F2}): " +
                $"{fronts.Count} front cells, overlapping pairs: " +
                string.Join(", ", overlaps.Select(p => $"({p.Item1},{p.Item2})")));
        }

        // ── unrotated baseline ────────────────────────────────────────────────

        [Test] public void Pen_FrontCellsTileWithoutOverlap()  { AssertFrontProjectionsTile("pen.json"); }
        [Test] public void Tes_FrontCellsTileWithoutOverlap()  { AssertFrontProjectionsTile("tes.json"); }
        [Test] public void Hex_FrontCellsTileWithoutOverlap()  { AssertFrontProjectionsTile("hex.json"); }
        [Test] public void Ico_FrontCellsTileWithoutOverlap()  { AssertFrontProjectionsTile("ico.json"); }
        [Test] public void Rap_FrontCellsTileWithoutOverlap()  { AssertFrontProjectionsTile("rap.json"); }

        // ── rotation sweep: matches the runtime scenario ──────────────────────
        // RotatingComplex applies an XY rotation by angle1 and a ZW rotation by angle2
        // every frame. The non-overlap property must hold at every angle for a convex
        // polychoron. Sample a coarse grid of (angle1, angle2) and assert the property
        // at every sample.
        static System.Collections.IEnumerable RotationGrid() {
            double[] a = { 0.0, 0.37, 0.91, 1.57, 2.20, 2.83, 3.50 };
            for (int i = 0; i < a.Length; i++)
                for (int j = 0; j < a.Length; j++)
                    yield return new TestCaseData(a[i], a[j]).SetName($"a1={a[i]:F2}_a2={a[j]:F2}");
        }

        [Test, TestCaseSource(nameof(RotationGrid))]
        public void Pen_FrontCellsTileUnderRotation(double a1, double a2) { AssertFrontProjectionsTile("pen.json", a1, a2); }

        [Test, TestCaseSource(nameof(RotationGrid))]
        public void Hex_FrontCellsTileUnderRotation(double a1, double a2) { AssertFrontProjectionsTile("hex.json", a1, a2); }

        [Test, TestCaseSource(nameof(RotationGrid))]
        public void Ico_FrontCellsTileUnderRotation(double a1, double a2) { AssertFrontProjectionsTile("ico.json", a1, a2); }

        [Test, TestCaseSource(nameof(RotationGrid))]
        public void Rap_FrontCellsTileUnderRotation(double a1, double a2) { AssertFrontProjectionsTile("rap.json", a1, a2); }

        // ── edge-visibility filter ────────────────────────────────────────────
        // VisibleNonCoplanarEdgeIds drops edges whose every incident cell is back-facing.
        // For a polychoron with at least one back-facing cell, the filter must drop a
        // non-empty set of edges; otherwise back-only edges would render INSIDE the
        // front-cell shadows (visible criss-cross through the translucent material).

        static HashSet<int> FrontFacingCellIds(PolyhedralComplex4d complex, ICamera4d cam) {
            var ids = new HashSet<int>();
            for (int i = 0; i < complex.cells.Count; i++) {
                var cell = complex.cells[i];
                if (cell.normal != null) {
                    var probe = complex.vertices[complex.CellVertexIds(i).First()];
                    if (!cam.IsFacedBy(probe, cell.normal)) continue;
                }
                ids.Add(i);
            }
            return ids;
        }

        /// Reference implementation: the *definition* of which non-coplanar edges should
        /// be visible. The filter under test must match this exactly.
        static HashSet<int> ExpectedVisibleEdges(PolyhedralComplex4d c, HashSet<int> visibleCellIds) {
            var result = new HashSet<int>();
            foreach (int eId in c.NonCoplanarEdgeIds()) {
                bool hasFront = false;
                bool hasAnyCell = false;
                foreach (int fId in c.FacesPerEdge(eId))
                    foreach (int cId in c.CellsPerFace(fId)) {
                        hasAnyCell = true;
                        if (visibleCellIds.Contains(cId)) hasFront = true;
                    }
                if (!hasAnyCell || hasFront) result.Add(eId);
            }
            return result;
        }

        static void AssertFilterMatchesDefinition(string fileName) {
            var c = LoadPolychoron(fileName);
            if (c == null) { Assert.Ignore($"{fileName} not found"); return; }
            var cam = new Camera4dParallel(new Point4d(0, 0, 0, -5));
            var visibleCellIds = FrontFacingCellIds(c, cam);
            var actual = new HashSet<int>(c.VisibleNonCoplanarEdgeIds(visibleCellIds));
            var expected = ExpectedVisibleEdges(c, visibleCellIds);
            Assert.That(actual, Is.EquivalentTo(expected),
                $"{fileName}: VisibleNonCoplanarEdgeIds output disagrees with reference definition");
        }

        [Test] public void Pen_EdgeFilter_MatchesDefinition() { AssertFilterMatchesDefinition("pen.json"); }
        [Test] public void Hex_EdgeFilter_MatchesDefinition() { AssertFilterMatchesDefinition("hex.json"); }
        [Test] public void Ico_EdgeFilter_MatchesDefinition() { AssertFilterMatchesDefinition("ico.json"); }
        [Test] public void Rap_EdgeFilter_MatchesDefinition() { AssertFilterMatchesDefinition("rap.json"); }

        // Sanity: for polychora large enough that some non-coplanar edges live on the back
        // hemisphere only, the filter must actually drop those — that's its purpose. (Pen is
        // too small: every non-coplanar edge of the 5-cell shares at least one cell with the
        // front-facing set.)
        static void AssertFilterDropsSomething(string fileName) {
            var c = LoadPolychoron(fileName);
            if (c == null) { Assert.Ignore($"{fileName} not found"); return; }
            var cam = new Camera4dParallel(new Point4d(0, 0, 0, -5));
            var visibleCellIds = FrontFacingCellIds(c, cam);
            int total = c.NonCoplanarEdgeIds().Count();
            int visible = c.VisibleNonCoplanarEdgeIds(visibleCellIds).Count();
            Assert.That(visible, Is.LessThan(total),
                $"{fileName}: filter dropped 0 of {total} non-coplanar edges " +
                $"({visibleCellIds.Count}/{c.cells.Count} cells front-facing)");
        }

        [Test] public void Hex_EdgeFilterActuallyDropsBackOnlyEdges()  { AssertFilterDropsSomething("hex.json"); }
        [Test] public void Ico_EdgeFilterActuallyDropsBackOnlyEdges()  { AssertFilterDropsSomething("ico.json"); }
        [Test] public void Rap_EdgeFilterActuallyDropsBackOnlyEdges()  { AssertFilterDropsSomething("rap.json"); }
    }
}
