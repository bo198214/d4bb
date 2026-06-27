using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using D4BB.Comb;
using D4BB.Geometry;
using D4BB.Transforms;
using NUnit.Framework;

namespace D4BB.Transforms {
// Equivalence oracle for the incremental Scene4d.Translate / Scene4d.Rotate path
// (ReoccludeAfterPieceChange). The incremental path reprojects only the moved piece and re-occludes
// only the pieces overlapping it; this must produce byte-for-byte the same visible geometry as the
// full RebuildAllPieces (UpdateCamera) after the same topology mutation.
//
// Why a strict per-vertex comparison is valid (not just an integerCell set): both the incremental
// scene and the reference scene start from identical origins, so their cached topology has identical
// iteration order; ComputeOccluders + SortFarToNear therefore yield the same occluder order, and
// OccludePieceCells applies CutOut in that same order — so the resulting fragments are bit-identical,
// not merely set-equivalent. (Across two scenes built from *different* origin arrays the occluder
// order can differ by HashSet iteration, so for the fresh-scene cross-check we compare the
// integerCell set instead.)
public class Scene4dIncrementalTests {

    // ── oracles ───────────────────────────────────────────────────────────────

    // Strict per-face geometry key: integerCell + its (orientation-independent, sorted) projected
    // vertices. Catches both a vanished/extra face and a mis-cut fragment.
    static List<string> VisibleGeom(Scene4d s, int piece) =>
        s.pieces[piece].visibleFacets.Select(f => {
            var ic = ((Face2dWithIntegerCellAttribute)f).integerCell.ToString();
            var pts = f.points
                .Select(p => string.Format(CultureInfo.InvariantCulture, "({0:F4},{1:F4},{2:F4})", p.x[0], p.x[1], p.x[2]))
                .OrderBy(x => x, System.StringComparer.Ordinal);
            return ic + "|" + string.Join(",", pts);
        }).OrderBy(x => x, System.StringComparer.Ordinal).ToList();

    // Looser key for the fresh-scene cross-check: the set of visible integerCells per piece.
    static HashSet<string> VisibleCellSet(Scene4d s, int piece) =>
        s.pieces[piece].visibleFacets.Select(f => ((Face2dWithIntegerCellAttribute)f).integerCell.ToString()).ToHashSet();

    static void AssertSameGeom(Scene4d inc, Scene4d reference, string ctx) {
        Assert.That(inc.pieces.Length, Is.EqualTo(reference.pieces.Length), $"{ctx}: piece count");
        for (int i = 0; i < inc.pieces.Length; i++)
            Assert.That(VisibleGeom(inc, i), Is.EqualTo(VisibleGeom(reference, i)),
                $"{ctx}: piece {i} visible geometry differs (incremental vs full)");
    }

    static void AssertSameCellSet(Scene4d inc, Scene4d reference, string ctx) {
        Assert.That(inc.pieces.Length, Is.EqualTo(reference.pieces.Length), $"{ctx}: piece count");
        for (int i = 0; i < inc.pieces.Length; i++)
            Assert.That(VisibleCellSet(inc, i), Is.EquivalentTo(VisibleCellSet(reference, i)),
                $"{ctx}: piece {i} visible cell set differs");
    }

    static int[][][] Clone(int[][][] o) =>
        o.Select(piece => piece.Select(cell => (int[])cell.Clone()).ToArray()).ToArray();

    static int[][][] WithTranslate(int[][][] origins, int piece, IntegerSignedAxis axis) {
        var res = Clone(origins);
        var uv = axis.UnitVector(4);
        foreach (var cell in res[piece])
            for (int k = 0; k < cell.Length; k++) cell[k] += uv[k];
        return res;
    }

    // ── translate equivalence ───────────────────────────────────────────────────

    // Two single-hypercube pieces whose 3D projections overlap (piece 1 farther). Sliding piece 1
    // across piece 0 exercises the full occluder/occludee dance. Compared step-by-step against a
    // reference scene that mirrors each move with the full UpdateCamera path (strict geometry), and
    // against a freshly built scene at the same final origins (cell set).
    [Test] public void TwoTesseracts_TranslateSlide_MatchesFullRebuild() {
        var origins = new int[][][] {
            new int[][] { new int[] {0,0,0,0} },
            new int[][] { new int[] {-1,-1,0,2} },
        };
        var inc = new Scene4d(origins, new Camera4dParallel());
        var reference = new Scene4d(origins, new Camera4dParallel());
        var expectedOrigins = Clone(origins);

        var axes = new[] { IntegerSignedAxis.PD1, IntegerSignedAxis.PD1, IntegerSignedAxis.PD2,
                           IntegerSignedAxis.MD1, IntegerSignedAxis.PD3 };
        int step = 0;
        foreach (var axis in axes) {
            inc.Translate(1, axis);
            reference.pieces[1].topology.Translate(axis); reference.UpdateCamera();
            expectedOrigins = WithTranslate(expectedOrigins, 1, axis);
            var fresh = new Scene4d(expectedOrigins, new Camera4dParallel());

            AssertSameGeom(inc, reference, $"slide step {step} axis {axis.Human()}");
            AssertSameCellSet(inc, fresh, $"slide step {step} (vs fresh)");
            step++;
        }
    }

    // Two pieces sharing a face: moving piece 1 away until it no longer overlaps piece 0 exercises the
    // prevBounds-restore branch — piece 0's faces that piece 1 used to occlude must reappear.
    [Test] public void SharedFace_TranslateApartAndBack_RestoresOccludedFaces() {
        var origins = new int[][][] {
            new int[][] { new int[] {0,0,0,0} },
            new int[][] { new int[] {1,0,0,0} },
        };
        var inc = new Scene4d(origins, new Camera4dParallel());
        var reference = new Scene4d(origins, new Camera4dParallel());

        // Move apart along +x, then back along -x to the original overlap.
        var axes = new[] { IntegerSignedAxis.PD1, IntegerSignedAxis.PD1, IntegerSignedAxis.PD1,
                           IntegerSignedAxis.MD1, IntegerSignedAxis.MD1, IntegerSignedAxis.MD1 };
        int step = 0;
        foreach (var axis in axes) {
            inc.Translate(1, axis);
            reference.pieces[1].topology.Translate(axis); reference.UpdateCamera();
            AssertSameGeom(inc, reference, $"apart/back step {step} axis {axis.Human()}");
            step++;
        }
    }

    // Three pieces in a row: moving the middle one must correctly update its relationships with both
    // neighbours while leaving the untouched far piece's cells intact.
    [Test] public void ThreePieces_TranslateMiddle_MatchesFullRebuild() {
        var origins = new int[][][] {
            new int[][] { new int[] {0,0,0,0} },
            new int[][] { new int[] {0,0,0,2} },
            new int[][] { new int[] {0,0,0,4} },
        };
        var inc = new Scene4d(origins, new Camera4dParallel());
        var reference = new Scene4d(origins, new Camera4dParallel());

        var axes = new[] { IntegerSignedAxis.PD1, IntegerSignedAxis.PD2, IntegerSignedAxis.PD1, IntegerSignedAxis.MD2 };
        int step = 0;
        foreach (var axis in axes) {
            inc.Translate(1, axis);
            reference.pieces[1].topology.Translate(axis); reference.UpdateCamera();
            AssertSameGeom(inc, reference, $"middle step {step} axis {axis.Human()}");
            step++;
        }
    }

    // ── rotate equivalence ──────────────────────────────────────────────────────

    // A two-cube piece (so a 90° rotation actually moves cells) behind a single cube. Rotating it in
    // several planes must match the full rebuild. The reference mirrors with
    // pieces[i].topology.Rotate + UpdateCamera, so no hand-computed rotated origins are needed.
    [Test] public void TwoCubePiece_Rotate_MatchesFullRebuild() {
        var origins = new int[][][] {
            new int[][] { new int[] {0,0,0,0}, new int[] {1,0,0,0} }, // piece 0: 2 cubes along x
            new int[][] { new int[] {0,0,0,2} },                       // piece 1: behind
        };
        var inc = new Scene4d(origins, new Camera4dParallel());
        var reference = new Scene4d(origins, new Camera4dParallel());
        var center = new IntegerCenter(new int[] {0,0,0,0});

        // (axis v, axis w) rotation planes; rotate piece 0.
        var planes = new[] { (0, 3), (0, 1), (1, 3), (2, 3) };
        int step = 0;
        foreach (var (v, w) in planes) {
            inc.Rotate(0, v, w, center);
            reference.pieces[0].topology.Rotate(v, w, center); reference.UpdateCamera();
            AssertSameGeom(inc, reference, $"rotate step {step} plane ({v},{w})");
            step++;
        }
    }

    // ── idempotence / round trip ────────────────────────────────────────────────

    // Translating one step out and back must return exactly the starting visible geometry — a guard
    // against the cumulative-erosion class of bug (each occlusion must start from a clean projection).
    [Test] public void TwoTesseracts_TranslateRoundTrip_IsIdentity() {
        var origins = new int[][][] {
            new int[][] { new int[] {0,0,0,0} },
            new int[][] { new int[] {-1,-1,0,2} },
        };
        var scene = new Scene4d(origins, new Camera4dParallel());
        var before = new List<List<string>>();
        for (int i = 0; i < scene.pieces.Length; i++) before.Add(VisibleGeom(scene, i));

        for (int rep = 0; rep < 5; rep++) {
            scene.Translate(1, IntegerSignedAxis.PD1);
            scene.Translate(1, IntegerSignedAxis.MD1);
        }
        for (int i = 0; i < scene.pieces.Length; i++)
            Assert.That(VisibleGeom(scene, i), Is.EqualTo(before[i]), $"round trip changed piece {i}");
    }
}
}
