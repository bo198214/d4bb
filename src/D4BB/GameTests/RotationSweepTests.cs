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
    public void Footprint_InPlaceRotation_IsJustTheCellItself()
    {
        // Square centered on the pivot (origin twice-offset (−1,−1)): the inscribed disk
        // spins in place — its distance to all four side neighbors is exactly ½ (legal open
        // contact) for the whole turn. The corner lenses that bulge √2/2 − 1/2 into the side
        // neighbors are forgiven by design; under the retired full-square sweep this
        // footprint additionally contained all four side neighbors, making any face contact
        // in the rotation plane block even an in-place turn.
        var fp = RotationSweep.Footprint2d(-1, -1);
        Assert.That(fp, Is.EquivalentTo(new[] { (-1, -1) }));
    }

    [Test]
    public void Footprint_DiagonalCell_TrailingNeighborsFree_LeadingBlocked()
    {
        // Pivot = center of the cell diagonally below-left of the rotating square
        // (origin twice-offset (1,1), i.e. the square is [1/2,3/2]² relative to the pivot),
        // rotating CCW: the center orbits at radius √2 from 45° to 135°, carrying the
        // ½-disk. Hand-derived footprint:
        //  - (1,1)  itself, (−3,1) end pose,
        //  - (−1,1) the center passes straight through it (at 90° it sits at (0, √2)),
        //  - (1,3)  its near corner (1/2,3/2) lies at radius √2.5, only √2.5 − √2 ≈ 0.17
        //    from the arc — the disk dips in (leading side),
        //  - (−1,3) at 90° the center is only 3/2 − √2 ≈ 0.086 below its bottom edge,
        //  - (−3,3) mirror image of (1,3) around the 90° mid-turn,
        //  - NOT (3,1) right / (1,−1) bottom neighbor: trailing side — distance exactly ½
        //    at θ=0 and growing (contact only in the start instant),
        //  - NOT (−3,−1): the end pose's trailing neighbor (tangent only at θ=90°),
        //  - NOT (3,3): its nearest corner sits at radius √4.5, a full √4.5 − √2 ≈ 0.71
        //    from the arc,
        //  - NOT (−1,−1) pivot cell: its farthest corner sits at radius √2/2, again
        //    √2 − √2/2 ≈ 0.71 from the arc.
        // (Same set as the retired full-square sweep produced here: each member is a
        // genuine pass, and every graze was already excluded by exact-radius contact.)
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
        // Piece 0: single cube at (1,0), rotating CCW in (x,y) about the center of the
        // origin cell — it swings through the diagonal cell (1,1), where piece 1 sits, into
        // the free end pose (0,1). Start and end poses are legal, so only the sweep can
        // object; the cube's CENTER passes through the obstacle cell (at 45° it sits at
        // (1.21, 1.21)), a genuine pass-through that no corner-lens tolerance forgives.
        Objective MakeObj() => new Objective("t",
            new int[][] { new int[] { 5, 5, 0, 0 } },
            new int[][][] {
                new int[][] { new int[] { 1, 0, 0, 0 } },
                new int[][] { new int[] { 1, 1, 0, 0 } },
            },
            new int[][] { new int[] { -5, -5, -5, -5 }, new int[] { 5, 5, 5, 5 } });

        var blockedObj = MakeObj(); // quantumRotation defaults to false
        var level = new GameLevel(blockedObj);
        level.SelectPiece(0);
        Assert.That(level.RotateSelected(0, 1, new int[] { 0, 0, 0, 0 }), Is.False);
        Assert.That(level.LastBlockReason, Is.EqualTo(MoveBlockReason.Overlap));
        Assert.That(IntegerOps.SetEqual(level.pieces[0].origins,
            new int[][] { new int[] { 1, 0, 0, 0 } }), Is.True, "blocked rotation must revert");

        var quantumObj = MakeObj();
        quantumObj.quantumRotation = true;
        var level2 = new GameLevel(quantumObj);
        level2.SelectPiece(0);
        Assert.That(level2.RotateSelected(0, 1, new int[] { 0, 0, 0, 0 }), Is.True);
    }

    [Test]
    public void GameLevel_InPlaceTurnAgainstFaceNeighbor_IsAllowed()
    {
        // Piece 1 sits flush on piece 0's +y face in the (x,y) rotation plane. The in-place
        // quarter turn only pushes the corner lenses (≤ √2/2 − 1/2 ≈ 0.207 deep) across the
        // shared face — exactly the face-contact tolerance the inscribed-disk semantics
        // grants. Under the retired full-square sweep this was blocked, which froze every
        // piece that touched anything in the rotation plane.
        var obj = new Objective("t",
            new int[][] { new int[] { 5, 5, 0, 0 } },
            new int[][][] {
                new int[][] { new int[] { 0, 0, 0, 0 } },
                new int[][] { new int[] { 0, 1, 0, 0 } },
            },
            new int[][] { new int[] { -5, -5, -5, -5 }, new int[] { 5, 5, 5, 5 } });
        var level = new GameLevel(obj);
        level.SelectPiece(0);
        Assert.That(level.RotateSelected(0, 1, new int[] { 0, 0, 0, 0 }), Is.True);
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
        // into the leading side — mid-turn the cube's center comes within √2.5 − √2 ≈ 0.17
        // of the neighbor's near corner, so even the inscribed disk dips in: not a face
        // graze but a genuine swing-through.
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
    public void GameLevel_InPlaceTurnInTightBoundary_IsAllowed()
    {
        // Boundary exactly one cell: the inscribed disk spins flush inside the envelope
        // (distance to every wall exactly ½ throughout — legal open contact). Only the
        // forgiven corner lenses cross the walls; under the retired full-square sweep this
        // was blocked, so a piece could never turn inside a snug envelope.
        var obj = new Objective("t",
            new int[][] { new int[] { 0, 0, 0, 0 } },
            new int[][][] { new int[][] { new int[] { 0, 0, 0, 0 } } },
            new int[][] { new int[] { 0, 0, 0, 0 }, new int[] { 1, 1, 1, 1 } });
        var level = new GameLevel(obj);
        level.SelectPiece(0);
        Assert.That(level.RotateSelected(0, 1, new int[] { 0, 0, 0, 0 }), Is.True);
    }

    [Test]
    public void GameLevel_SweepOutOfBoundary_Blocks()
    {
        // Single cube at (1,−1) swings CCW in (x,y) about the center of the origin cell up
        // to (1,1). Both end poses keep x ≤ 2, but mid-turn (at 0°) the center passes
        // (1/2 + √2, 1/2) and the disk reaches x = √2 + 1 ≈ 2.41 — through the x-wall at 2.
        // A genuine swing beyond the envelope, not a flush graze.
        Objective MakeObj() => new Objective("t",
            new int[][] { new int[] { 0, 0, 0, 0 } },
            new int[][][] { new int[][] { new int[] { 1, -1, 0, 0 } } },
            new int[][] { new int[] { 0, -1, 0, 0 }, new int[] { 2, 2, 1, 1 } });

        var level = new GameLevel(MakeObj());
        level.SelectPiece(0);
        Assert.That(level.RotateSelected(0, 1, new int[] { 0, 0, 0, 0 }), Is.False);
        Assert.That(level.LastBlockReason, Is.EqualTo(MoveBlockReason.OutOfBoundary));

        var quantumObj = MakeObj();
        quantumObj.quantumRotation = true;
        var level2 = new GameLevel(quantumObj);
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
