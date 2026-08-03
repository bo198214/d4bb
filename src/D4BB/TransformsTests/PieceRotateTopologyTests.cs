using System.Collections.Generic;
using System.Linq;
using D4BB.Comb;
using D4BB.Geometry;
using NUnit.Framework;

namespace D4BB.Transforms {
// True oracle for the in-place Piece.Rotate topology mutation: after a rotation, the piece's cached
// boundary topology (boundaryCells + coplanarBoundaryFaces, INCLUDING each cell's inverted/parity
// orientation) must equal a FRESH IntegerBoundaryComplex build from the rotated origins.
//
// Why this file exists: Scene4dIncrementalTests.TwoCubePiece_Rotate_MatchesFullRebuild compares the
// incremental path against a reference that also mirrors the move with pieces[0].Rotate — both sides
// share the same in-place mutation, so an orientation bug in Piece.Rotate passes that test unseen.
// (Historically it was masked in the game by the EndDrag full Update, whose FillTopology recomputed
// the topology from origins; removing that rebuild exposed stale orientations as backface/winding
// artifacts after drag rotations.)
public class PieceRotateTopologyTests {

    // Orientation-carrying cell key. ToString() encodes origin, span and inverted ([o]±[span]);
    // parity is appended explicitly since ToString omits it.
    static string Key(OrientedIntegerCell c) => c.ToString() + (c.parity ? "|p1" : "|p0");

    static List<string> BoundaryKeys(Piece p) =>
        p.boundaryCells.Select(Key).OrderBy(x => x, System.StringComparer.Ordinal).ToList();

    // Pair key: the f2's orientation is relative to its generating parent c3, which for the
    // coplanar-boundary pair list is exactly the stored c3 — compare them as a unit.
    static List<string> PairKeys(Piece p) =>
        p.coplanarBoundaryFaces.Select(t => Key(t.c3) + "&" + Key(t.f2))
            .OrderBy(x => x, System.StringComparer.Ordinal).ToList();

    // Interior grid-division faces: owner choice and f2 orientation depend on HashSet iteration
    // order in FillTopology (the two coplanar parents share a span but sit on opposite sides), so
    // only the unoriented face set is comparable across independently built topologies.
    static List<string> InteriorFaceKeys(Piece p) =>
        (p.interiorDivisionFaces ?? System.Array.Empty<(OrientedIntegerCell c3, OrientedIntegerCell f2)>())
            .Select(t => {
                var span = t.f2.span.ToArray(); System.Array.Sort(span);
                return IntegerOps.ToString(t.f2.origin) + ":" + IntegerOps.ToString(span);
            })
            .OrderBy(x => x, System.StringComparer.Ordinal).ToList();

    static void AssertTopologyMatchesFresh(Piece rotated, string ctx) {
        // Fresh scene from the piece's (already rotated) origins — the independent oracle.
        var fresh = new Scene4d(new int[][][] { rotated.origins }, new Camera4dParallel());
        var freshPiece = fresh.pieces[0];
        Assert.That(BoundaryKeys(rotated), Is.EqualTo(BoundaryKeys(freshPiece)),
            $"{ctx}: boundaryCells (orientation-sensitive) differ from fresh build");
        Assert.That(PairKeys(rotated), Is.EqualTo(PairKeys(freshPiece)),
            $"{ctx}: coplanarBoundaryFaces (orientation-sensitive) differ from fresh build");
        Assert.That(InteriorFaceKeys(rotated), Is.EqualTo(InteriorFaceKeys(freshPiece)),
            $"{ctx}: interiorDivisionFaces (unoriented) differ from fresh build");
    }

    static readonly (string name, int[][] origins)[] Figures = {
        ("single", new int[][] { new int[] {0,0,0,0} }),
        ("L-tricube", new int[][] { new int[] {0,0,0,0}, new int[] {1,0,0,0}, new int[] {1,1,0,0} }),
        ("bar2", new int[][] { new int[] {0,0,0,0}, new int[] {1,0,0,0} }),
        // 2x2x2 block in xyz: has faces in every span combination, incl. w-normal cells everywhere
        ("block222", new int[][] {
            new int[] {0,0,0,0}, new int[] {1,0,0,0}, new int[] {0,1,0,0}, new int[] {1,1,0,0},
            new int[] {0,0,1,0}, new int[] {1,0,1,0}, new int[] {0,1,1,0}, new int[] {1,1,1,0},
        }),
    };

    static IEnumerable<(int v, int w)> AllPlanes() {
        for (int v = 0; v < 4; v++)
            for (int w = 0; w < 4; w++)
                if (v != w) yield return (v, w);
    }

    // Every figure × every ordered rotation plane: one 90° in-place rotation must reproduce the
    // freshly built topology of the rotated origins, orientation flags included.
    [Test] public void SingleRotation_MatchesFreshTopology() {
        foreach (var (name, origins) in Figures) {
            foreach (var (v, w) in AllPlanes()) {
                var scene = new Scene4d(new int[][][] { origins }, new Camera4dParallel());
                var piece = scene.pieces[0];
                var pivot = new IntegerCenter(piece.origins, asCubes: true);
                piece.Rotate(v, w, pivot);
                AssertTopologyMatchesFresh(piece, $"{name} rotate ({v},{w})");
            }
        }
    }

    // Two successive rotations in different planes (the drag can chain them) must still match.
    [Test] public void ChainedRotations_MatchFreshTopology() {
        foreach (var (name, origins) in Figures) {
            var scene = new Scene4d(new int[][][] { origins }, new Camera4dParallel());
            var piece = scene.pieces[0];
            var pivot = new IntegerCenter(piece.origins, asCubes: true);
            var chain = new[] { (0, 3), (1, 2), (3, 1), (2, 0) };
            int step = 0;
            foreach (var (v, w) in chain) {
                piece.Rotate(v, w, pivot);
                AssertTopologyMatchesFresh(piece, $"{name} chain step {step} plane ({v},{w})");
                step++;
            }
        }
    }

    // Four 90° rotations in the same plane are the identity — the cached topology must return to the
    // exact starting state (flags included). Guards against symmetric errors that cancel pairwise
    // (note: a pure inverted-flip bug DOES cancel over 4 steps, which is why the fresh-oracle tests
    // above are the primary guard and this one is only the cheap regression net).
    [Test] public void FourQuarterTurns_AreIdentity() {
        foreach (var (name, origins) in Figures) {
            var scene = new Scene4d(new int[][][] { origins }, new Camera4dParallel());
            var piece = scene.pieces[0];
            var pivot = new IntegerCenter(piece.origins, asCubes: true);
            var before = (BoundaryKeys(piece), PairKeys(piece), InteriorFaceKeys(piece));
            for (int i = 0; i < 4; i++) piece.Rotate(1, 3, pivot);
            Assert.That(BoundaryKeys(piece), Is.EqualTo(before.Item1), $"{name}: boundaryCells round trip");
            Assert.That(PairKeys(piece), Is.EqualTo(before.Item2), $"{name}: pairs round trip");
            Assert.That(InteriorFaceKeys(piece), Is.EqualTo(before.Item3), $"{name}: interior faces round trip");
        }
    }
}
}
