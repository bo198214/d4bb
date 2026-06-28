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
            reference.pieces[1].Translate(axis); reference.UpdateCamera();
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
            reference.pieces[1].Translate(axis); reference.UpdateCamera();
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
            reference.pieces[1].Translate(axis); reference.UpdateCamera();
            AssertSameGeom(inc, reference, $"middle step {step} axis {axis.Human()}");
            step++;
        }
    }

    // ── rotate equivalence ──────────────────────────────────────────────────────

    // A two-cube piece (so a 90° rotation actually moves cells) behind a single cube. Rotating it in
    // several planes must match the full rebuild. The reference mirrors with
    // pieces[i].Rotate + UpdateCamera, so no hand-computed rotated origins are needed.
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
            reference.pieces[0].Rotate(v, w, center); reference.UpdateCamera();
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

    // ── bound scene (shared pieces) ──────────────────────────────────────────────

    // A Scene4d bound to an externally-owned piece list shares the SAME Piece objects, so a single move
    // applied by the owner (piece.Translate, as GameLevel.TranslateSelected does) is exactly what the
    // scene renders — there is no second, scene-side mutation (the former double-update). The bound
    // scene must match a standalone scene built from the same origins, before and after such a move.
    [Test] public void BoundScene_SharesPieces_SingleMoveSuffices() {
        var origins = new int[][][] {
            new int[][] { new int[] {0,0,0,0} },
            new int[][] { new int[] {-1,-1,0,2} },
        };
        var shared = new List<Piece> { new Piece(origins[0]), new Piece(origins[1]) };
        var bound = new Scene4d(shared, new Camera4dParallel());

        // The bound scene's working array IS the shared pieces (same references).
        Assert.That(bound.pieces[0], Is.SameAs(shared[0]));
        Assert.That(bound.pieces[1], Is.SameAs(shared[1]));

        var standalone = new Scene4d(origins, new Camera4dParallel());
        AssertSameGeom(bound, standalone, "bound vs standalone (initial)");

        // Move the shared piece ONCE via the piece itself (the owner's move), then only re-occlude.
        shared[1].Translate(IntegerSignedAxis.PD1);
        bound.UpdateCamera();

        var reference = new Scene4d(origins, new Camera4dParallel());
        reference.pieces[1].Translate(IntegerSignedAxis.PD1); reference.UpdateCamera();
        AssertSameGeom(bound, reference, "bound after single shared move");
    }

    // ── per-piece mesh ownership + dirty signal ─────────────────────────────────

    // Scene4d builds each piece's FacetsGenericMesh/EdgesGenericMesh and rebuilds them only for the
    // affected pieces on an incremental move. An untouched piece must keep the SAME mesh objects
    // (reference identity is the renderer's dirty signal); an affected piece gets fresh objects. And
    // the set returned by the move method must be exactly the set of pieces whose meshes changed.
    [Test] public void IncrementalMove_OnlyAffectedPieces_GetNewMeshObjects() {
        // Three pieces spread far apart along w so none of their projections overlap.
        var origins = new int[][][] {
            new int[][] { new int[] {0,0,0,0} },
            new int[][] { new int[] {0,0,0,10} },
            new int[][] { new int[] {0,0,0,20} },
        };
        var scene = new Scene4d(origins, new Camera4dParallel());
        for (int i = 0; i < scene.pieces.Length; i++) {
            Assert.That(scene.pieces[i].facetsMesh, Is.Not.Null, $"piece {i} facetsMesh built");
            Assert.That(scene.pieces[i].edgesMesh, Is.Not.Null, $"piece {i} edgesMesh built");
        }

        var beforeF = scene.pieces.Select(p => p.facetsMesh).ToArray();
        var beforeE = scene.pieces.Select(p => p.edgesMesh).ToArray();

        // Move piece 0 along x; it overlaps nobody, so only piece 0 is affected.
        var affected = scene.Translate(0, IntegerSignedAxis.PD1);
        Assert.That(affected, Is.EquivalentTo(new[] { 0 }), "non-overlapping move affects only the moved piece");

        // Exactly the affected pieces get new mesh objects; the rest keep their references.
        for (int i = 0; i < scene.pieces.Length; i++) {
            bool changed = affected.Contains(i);
            Assert.That(!ReferenceEquals(scene.pieces[i].facetsMesh, beforeF[i]), Is.EqualTo(changed),
                $"piece {i} facetsMesh identity (changed={changed})");
            Assert.That(!ReferenceEquals(scene.pieces[i].edgesMesh, beforeE[i]), Is.EqualTo(changed),
                $"piece {i} edgesMesh identity (changed={changed})");
        }
    }

    // When the move makes pieces overlap, the affected set (and thus the rebuilt-mesh set) must include
    // the overlapping neighbour, not just the moved piece.
    [Test] public void IncrementalMove_OverlappingNeighbour_IsRebuilt() {
        var origins = new int[][][] {
            new int[][] { new int[] {0,0,0,0} },
            new int[][] { new int[] {-1,-1,0,2} }, // overlaps piece 0 in projection (piece 1 farther)
        };
        var scene = new Scene4d(origins, new Camera4dParallel());
        var beforeF = scene.pieces.Select(p => p.facetsMesh).ToArray();

        var affected = scene.Translate(1, IntegerSignedAxis.PD1);
        Assert.That(affected, Does.Contain(1), "moved piece is affected");
        Assert.That(affected, Does.Contain(0), "overlapping neighbour is affected");

        foreach (var i in affected)
            Assert.That(scene.pieces[i].facetsMesh, Is.Not.SameAs(beforeF[i]), $"piece {i} mesh rebuilt");
    }

    // The wired drag pattern: the owner moves the piece in place (piece.Translate, possibly several
    // steps in one snap), then a single ReoccludePiece does the incremental re-occlusion. Must equal a
    // full UpdateCamera for the same end state, for single- and multi-step batches.
    [Test] public void ReoccludePiece_AfterInPlaceMove_MatchesUpdateCamera() {
        var origins = new int[][][] {
            new int[][] { new int[] {0,0,0,0} },
            new int[][] { new int[] {-1,-1,0,2} },
        };
        var inc = new Scene4d(origins, new Camera4dParallel());
        var reference = new Scene4d(origins, new Camera4dParallel());

        // (steps-per-snap, axis) — single steps and a 2-step batch (a snap dragging two fields at once).
        var snaps = new[] {
            (1, IntegerSignedAxis.PD1),
            (2, IntegerSignedAxis.PD1),   // batch: two in-place steps, then ONE ReoccludePiece
            (1, IntegerSignedAxis.PD2),
            (3, IntegerSignedAxis.MD1),
        };
        int step = 0;
        foreach (var (count, axis) in snaps) {
            for (int s = 0; s < count; s++) {
                inc.pieces[1].Translate(axis);          // in-place topology shift only (no occlusion)
                reference.pieces[1].Translate(axis);
            }
            inc.ReoccludePiece(1);                       // one incremental re-occlusion after the batch
            reference.UpdateCamera();                    // full re-occlusion
            AssertSameGeom(inc, reference, $"reocclude snap {step} (x{count} {axis.Human()})");
            step++;
        }
    }
}
}
