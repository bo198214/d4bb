using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using D4BB.Geometry;
using D4BB.Geometry2;

namespace D4BB.Geometry2Tests {

    /// Tests for the FromPolygonBoundaries edge-extraction strategy.
    public class CellRender3dEdgesTests {

        [Test] public void FromPolygonBoundaries_UnitCubeUnclipped_AllOriginal() {
            // A single uncut cube: 12 unique edges. Each cube edge is shared between 2 faces,
            // so the per-face boundary scan emits each edge twice; MergeOriginalSegments
            // dedupes via per-edge interval union.
            var cube = Cube4dBuilder.UnitCubeAtW(0, cellNormal: new Point(0, 0, 0, 1));
            var cell3d = CellRender3d.FromFragment(CellFragment.FromCell(cube, 0), new Camera4dParallel());
            var processed = new List<CellRender3d> { cell3d };
            var edges = CellRender3dEdges.ExtractFromPolygonBoundaries(processed, cube, new Camera4dParallel());
            Assert.That(edges.All(e => e.isOriginal), Is.True);
            Assert.That(edges.Count, Is.EqualTo(12),
                "12 unique cube edges expected after dedup (would be 24 without)");
        }

        [Test] public void FromPolygonBoundaries_AfterClip_HasCutEdges() {
            // Clip the unit cube against a halfspace at x=0.5. The remaining outer fragment(s)
            // have new cut edges along the clip plane. These edges aren't on any original
            // complex edge of the cube ⇒ classified as cut (isOriginal=false).
            var cube = Cube4dBuilder.UnitCubeAtW(0, cellNormal: new Point(0, 0, 0, 1));
            var cell3d = CellRender3d.FromFragment(CellFragment.FromCell(cube, 0), new Camera4dParallel());
            var hs = new HalfSpace(0.5, new Point(1, 0, 0).normalize());
            cell3d.CutOut(new[] { hs });
            var processed = new List<CellRender3d> { cell3d };
            var edges = CellRender3dEdges.ExtractFromPolygonBoundaries(processed, cube, new Camera4dParallel());
            int original = edges.Count(e => e.isOriginal);
            int cut = edges.Count(e => !e.isOriginal);
            Assert.That(cut, Is.GreaterThan(0), "expected some cut edges along the clip plane");
            Assert.That(original, Is.GreaterThan(0), "expected the cube's surviving original edges to be classified original");
        }

        // ── cut-edge sharing under partial overlap ────────────────────────────

        /// Cube clipped by a smaller cube offset in two axes (the L-shaped CutOut). Each
        /// originally-square face that gets clipped produces multiple surviving fragments.
        /// The cut edges between adjacent surviving fragments overlap only over part of
        /// their length: that overlap is shared (coplanar-embedded), the rest is unique.
        ///
        /// Concretely the z=0 face produces two surviving fragments; their cut edges along
        /// x=0.5 overlap over y ∈ [0.5, 1] (= shared) and the rest of the longer fragment's
        /// edge spans y ∈ [0, 0.5] (= unique boundary). Both regions must appear in the
        /// output, the first as coplanar, the second as not coplanar.
        [Test] public void FromPolygonBoundaries_PartialCutOverlap_ExactSubdivision() {
            // Single cube clipped by another cube at (-0.5, -0.5, 0) — L-cut. The z=0 face
            // produces two surviving fragments: poly3 (x∈[0.5,1], full y) and poly6
            // (x∈[0,0.5], y∈[0.5,1]). Their cut edges along the x=0.5 line overlap in
            // y∈[0.5,1] (= shared, coplanar) and only poly3 covers y∈[0,0.5] (= visible,
            // non-coplanar). Verify the algorithm yields exactly these two sub-segments.
            var cube = Cube4dBuilder.UnitCubeAtW(0, cellNormal: new Point(0, 0, 0, 1));
            var cell3d = CellRender3d.FromFragment(CellFragment.FromCell(cube, 0), new Camera4dParallel());
            var cutter = Cube4dBuilder.UnitCubeAt(-0.5, -0.5, 0, 0);
            var cutter3d = CellRender3d.FromFragment(CellFragment.FromCell(cutter, 0), new Camera4dParallel());
            cell3d.CutOut(cutter3d.DefiningHalfSpaces());
            var processed = new List<CellRender3d> { cell3d };
            var edges = CellRender3dEdges.ExtractFromPolygonBoundaries(processed, cube, new Camera4dParallel());
            // Filter to cut edges on the x=0.5,z=0 line (= direction (0,1,0)).
            const double eps = 1e-3;
            var xLineCuts = edges.Where(e => !e.isOriginal
                && System.Math.Abs(e.a.x[0] - 0.5) < eps && System.Math.Abs(e.a.x[2] - 0.0) < eps
                && System.Math.Abs(e.b.x[0] - 0.5) < eps && System.Math.Abs(e.b.x[2] - 0.0) < eps).ToList();
            // Expect exactly 2 sub-segments: y∈[0,0.5] visible, y∈[0.5,1] shared.
            Assert.That(xLineCuts.Count, Is.EqualTo(2),
                $"expected 2 sub-segments along x=0.5, got {xLineCuts.Count}");
            var visible = xLineCuts.Where(e => !e.isCoplanar).ToList();
            var shared  = xLineCuts.Where(e =>  e.isCoplanar).ToList();
            Assert.That(visible.Count, Is.EqualTo(1));
            Assert.That(shared.Count,  Is.EqualTo(1));
            // Verify y-ranges.
            double Lo(EdgeSegment3d e) => System.Math.Min(e.a.x[1], e.b.x[1]);
            double Hi(EdgeSegment3d e) => System.Math.Max(e.a.x[1], e.b.x[1]);
            Assert.That(Lo(visible[0]), Is.EqualTo(0.0).Within(eps));
            Assert.That(Hi(visible[0]), Is.EqualTo(0.5).Within(eps));
            Assert.That(Lo(shared[0]),  Is.EqualTo(0.5).Within(eps));
            Assert.That(Hi(shared[0]),  Is.EqualTo(1.0).Within(eps));
        }

        [Test] public void FromPolygonBoundaries_PartialCutOverlap_SubdividedCorrectly() {
            var cube = Cube4dBuilder.UnitCubeAtW(0, cellNormal: new Point(0, 0, 0, 1));
            var cell3d = CellRender3d.FromFragment(CellFragment.FromCell(cube, 0), new Camera4dParallel());
            // Clip by another cube at (-0.5, -0.5, 0) — produces an L-cut.
            var cutter = Cube4dBuilder.UnitCubeAt(-0.5, -0.5, 0, 0);
            var cutter3d = CellRender3d.FromFragment(CellFragment.FromCell(cutter, 0), new Camera4dParallel());
            cell3d.CutOut(cutter3d.DefiningHalfSpaces());
            var processed = new List<CellRender3d> { cell3d };
            var edges = CellRender3dEdges.ExtractFromPolygonBoundaries(processed, cube, new Camera4dParallel());
            var cuts = edges.Where(e => !e.isOriginal).ToList();
            int sharedCuts = cuts.Count(e => e.isCoplanar);
            int uniqueCuts = cuts.Count(e => !e.isCoplanar);
            Assert.That(sharedCuts, Is.GreaterThan(0),
                "expected some cut edges between two surviving fragments to be marked shared");
            Assert.That(uniqueCuts, Is.GreaterThan(0),
                "expected some cut edges to be unique boundaries (only one surviving fragment on this side)");
        }

        [Test] public void FromPolygonBoundaries_FlagsCoplanarOriginalEdges() {
            var c = IntegerComplex4dBuilder.Boundary(new[] {
                new D4BB.Comb.IntegerCell(new[] { 0, 0, 0, 0 }),
                new D4BB.Comb.IntegerCell(new[] { 1, 0, 0, 0 }),
            });
            var cam = new Camera4dParallel();
            var processed = new List<CellRender3d>();
            for (int i = 0; i < c.cells.Count; i++)
                processed.Add(CellRender3d.FromFragment(CellFragment.FromCell(c, i), cam));
            var edges = CellRender3dEdges.ExtractFromPolygonBoundaries(processed, c, cam);
            int origCoplanar    = edges.Count(e => e.isOriginal && e.isCoplanar);
            int origNonCoplanar = edges.Count(e => e.isOriginal && !e.isCoplanar);
            Assert.That(origCoplanar,    Is.GreaterThan(0));
            Assert.That(origNonCoplanar, Is.GreaterThan(0));
        }

        [Test] public void FromPolygonBoundaries_KleinBottle_AllEdgesViaFastPath() {
            // klein-bottle.json is a pure 2-complex (1152 faces, 1728 edges, 0 cells).
            // Free-floating face polygons go straight through the edge pipeline; every
            // polygon-boundary segment must be matched against a complex edge via the
            // O(1) vertex-hash fast path (no CutOut means no sub-segments). Validates
            // both correctness and that the fast path is actually exercised — if the
            // Quantize hash drifts vs. ProjectComplexEdges, this test will produce cuts
            // and fail the Count assertion.
            var assemblyDir = Path.GetDirectoryName(typeof(CellRender3dEdgesTests).Assembly.Location);
            var d = new System.IO.DirectoryInfo(assemblyDir);
            string jsonPath = null;
            while (d != null) {
                var p = Path.Combine(d.FullName, "Assets", "tesserian", "Resources", "polychora", "klein-bottle.json");
                if (File.Exists(p)) { jsonPath = p; break; }
                d = d.Parent;
            }
            if (jsonPath == null) { Assert.Ignore("klein-bottle.json not found in containing repo"); return; }

            var c = PolyhedralComplex4dJson.FromJson(File.ReadAllText(jsonPath));
            Assert.That(c.cells.Count, Is.EqualTo(0), "klein-bottle is a pure 2-complex");
            var cam = new Camera4dParallel();
            // Mirror ComplexFrame's free-floating-face path: wrap each face as a single-face CellRender3d.
            var processed = new List<CellRender3d>();
            for (int fId = 0; fId < c.faces.Count; fId++) {
                var poly3d = new List<Point>();
                foreach (var v in c.FaceVertices(fId)) poly3d.Add(cam.Proj3d(v));
                processed.Add(new CellRender3d {
                    sourceCellId = -1,
                    faces = new List<List<Point>> { poly3d },
                    faceIds = new List<int> { fId },
                });
            }
            var edges = CellRender3dEdges.ExtractFromPolygonBoundaries(processed, c, cam);
            int original = edges.Count(e => e.isOriginal);
            int cut      = edges.Count(e => !e.isOriginal);
            Assert.That(cut, Is.EqualTo(0), "no clipping happened ⇒ no cut edges expected");
            Assert.That(original, Is.EqualTo(c.edges.Count),
                $"every complex edge should produce one merged segment (got {original} originals for {c.edges.Count} edges)");
        }
    }
}
