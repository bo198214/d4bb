using NUnit.Framework;
using D4BB.Comb;

namespace D4BB.Game
{
[TestFixture]
public class RotationSweepTests
{
    // Footprint2d works in twice-coordinates relative to the pivot: a cell origin o with
    // pivot twice-coordinates p enters as 2·o − p per plane axis. All expected sets below
    // were derived by hand from the sweep geometry (see the RotationSweep class doc).

    [Test]
    public void Footprint_InPlaceRotation_IsSelfPlusSideNeighbors()
    {
        // Square centered on the pivot (origin twice-offset (−1,−1)): during the turn the
        // corners (radius √2/2) bulge across all four edges into the side neighbors, while
        // the diagonal neighbors are only grazed (their nearest corner sits at exactly √2/2).
        var fp = RotationSweep.Footprint2d(-1, -1);
        var expected = new[] {
            (-1, -1),          // itself (start == end pose)
            (1, -1), (-3, -1), // right, left
            (-1, 1), (-1, -3), // top, bottom
        };
        Assert.That(fp, Is.EquivalentTo(expected));
    }

    [Test]
    public void Footprint_DiagonalCell_TrailingNeighborsFree_LeadingBlocked()
    {
        // Pivot = center of the cell diagonally below-left of the rotating square
        // (origin twice-offset (1,1), i.e. the square is [1/2,3/2]² relative to the pivot),
        // rotating CCW. Hand-derived footprint:
        //  - (1,1)  itself, (−3,1) end pose,
        //  - (1,3)  top neighbor (leading side: the outer corner at radius √4.5 crosses y=3/2),
        //  - (−1,1) the inner corner (radius √2/2) crosses x=1/2 on its way,
        //  - (−1,3), (−3,3) the outer corner's arc passes through them,
        //  - NOT (3,1) right / (1,−1) bottom neighbor: trailing side — every point moves
        //    away from the shared edge for the whole turn (contact only at θ=0),
        //  - NOT (3,3): the far diagonal's nearest corner sits at exactly the outer corner
        //    radius √4.5 — grazing, no interior overlap,
        //  - NOT (−3,−1): the end pose's trailing neighbor (contact only at θ=90°).
        var fp = RotationSweep.Footprint2d(1, 1);
        var expected = new[] {
            (1, 1), (-3, 1),
            (1, 3), (-1, 1), (-1, 3), (-3, 3),
        };
        Assert.That(fp, Is.EquivalentTo(expected));
    }

    [Test]
    public void GameLevel_SweptCollision_Blocks_QuantumRotationAllows()
    {
        // Piece 0: single cube at the origin; piece 1 sits on its +y neighbor cell — in the
        // (x,y) rotation plane exactly a side neighbor of the in-place quarter turn. The end
        // pose is free (identical to the start pose), so only the sweep can object.
        Objective MakeObj() => new Objective("t",
            new int[][] { new int[] { 5, 5, 0, 0 } },
            new int[][][] {
                new int[][] { new int[] { 0, 0, 0, 0 } },
                new int[][] { new int[] { 0, 1, 0, 0 } },
            },
            new int[][] { new int[] { -5, -5, -5, -5 }, new int[] { 5, 5, 5, 5 } });

        var blockedObj = MakeObj(); // quantumRotation defaults to false
        var level = new GameLevel(blockedObj);
        level.SelectPiece(0);
        Assert.That(level.RotateSelected(0, 1, new int[] { 0, 0, 0, 0 }), Is.False);
        Assert.That(level.LastBlockReason, Is.EqualTo(MoveBlockReason.Overlap));
        Assert.That(IntegerOps.SetEqual(level.pieces[0].origins,
            new int[][] { new int[] { 0, 0, 0, 0 } }), Is.True, "blocked rotation must revert");

        var quantumObj = MakeObj();
        quantumObj.quantumRotation = true;
        var level2 = new GameLevel(quantumObj);
        level2.SelectPiece(0);
        Assert.That(level2.RotateSelected(0, 1, new int[] { 0, 0, 0, 0 }), Is.True);
    }

    [Test]
    public void GameLevel_FixedPlaneNeighbor_DoesNotBlock()
    {
        // The obstacle differs only on axis 2 (z), which the (x,y) rotation leaves fixed:
        // it slides face-to-face along the rotating cube for the whole turn — legal contact,
        // never an interior overlap (the fiber filter must discard it).
        var obj = new Objective("t",
            new int[][] { new int[] { 5, 5, 0, 0 } },
            new int[][][] {
                new int[][] { new int[] { 0, 0, 0, 0 } },
                new int[][] { new int[] { 0, 0, 1, 0 } },
            },
            new int[][] { new int[] { -5, -5, -5, -5 }, new int[] { 5, 5, 5, 5 } });
        var level = new GameLevel(obj);
        level.SelectPiece(0);
        Assert.That(level.RotateSelected(0, 1, new int[] { 0, 0, 0, 0 }), Is.True);
    }

    [Test]
    public void GameLevel_TrailingNeighbor_DoesNotBlock()
    {
        // Trailing-side configuration from the class doc: pivot = center of the cell at
        // (−1,−1), piece rotates CCW in (x,y); its +x face neighbor is only touched in the
        // start instant (the whole square moves monotonically away from the shared face).
        var obj = new Objective("t",
            new int[][] { new int[] { 5, 5, 0, 0 } },
            new int[][][] {
                new int[][] { new int[] { 0, 0, 0, 0 } },
                new int[][] { new int[] { 1, 0, 0, 0 } },
            },
            new int[][] { new int[] { -5, -5, -5, -5 }, new int[] { 5, 5, 5, 5 } });
        var level = new GameLevel(obj);
        level.SelectPiece(0);
        Assert.That(level.RotateSelected(0, 1, new int[] { -1, -1, 0, 0 }), Is.True,
            "CCW away from the +x neighbor must not collide");
    }

    [Test]
    public void GameLevel_LeadingNeighbor_Blocks()
    {
        // Same geometry, opposite sense: rotating CW ((v,w) swapped) turns the +x neighbor
        // into the leading side — the outer corner crosses the shared face plane mid-turn.
        var obj = new Objective("t",
            new int[][] { new int[] { 5, 5, 0, 0 } },
            new int[][][] {
                new int[][] { new int[] { 0, 0, 0, 0 } },
                new int[][] { new int[] { 1, 0, 0, 0 } },
            },
            new int[][] { new int[] { -5, -5, -5, -5 }, new int[] { 5, 5, 5, 5 } });
        var level = new GameLevel(obj);
        level.SelectPiece(0);
        Assert.That(level.RotateSelected(1, 0, new int[] { -1, -1, 0, 0 }), Is.False);
        Assert.That(level.LastBlockReason, Is.EqualTo(MoveBlockReason.Overlap));
    }

    [Test]
    public void GameLevel_SweepOutOfBoundary_Blocks()
    {
        // Boundary exactly one cell: the end pose of the in-place turn is inside, but the
        // corners sweep across all four faces of the envelope mid-turn.
        var obj = new Objective("t",
            new int[][] { new int[] { 0, 0, 0, 0 } },
            new int[][][] { new int[][] { new int[] { 0, 0, 0, 0 } } },
            new int[][] { new int[] { 0, 0, 0, 0 }, new int[] { 1, 1, 1, 1 } });
        var level = new GameLevel(obj);
        level.SelectPiece(0);
        Assert.That(level.RotateSelected(0, 1, new int[] { 0, 0, 0, 0 }), Is.False);
        Assert.That(level.LastBlockReason, Is.EqualTo(MoveBlockReason.OutOfBoundary));

        obj.quantumRotation = true;
        var level2 = new GameLevel(obj);
        level2.SelectPiece(0);
        Assert.That(level2.RotateSelected(0, 1, new int[] { 0, 0, 0, 0 }), Is.True);
    }

    [Test]
    public void Objective_QuantumRotation_JsonRoundTrip()
    {
        var obj = new Objective("t",
            new int[][] { new int[] { 0, 0, 0, 0 } },
            new int[][][] { new int[][] { new int[] { 1, 0, 0, 0 } } });

        // Default false: not emitted, absent reads back as false.
        var json = obj.ToJson();
        Assert.That(json, Does.Not.Contain("quantum_rotation"));
        Assert.That(Objective.FromJson(json).quantumRotation, Is.False);

        obj.quantumRotation = true;
        json = obj.ToJson();
        Assert.That(json, Does.Contain("\"quantum_rotation\": true"));
        Assert.That(Objective.FromJson(json).quantumRotation, Is.True);
    }
}
}
