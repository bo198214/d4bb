using System;
using NUnit.Framework;
using D4BB.Comb;
using D4BB.Transforms;

namespace D4BB.Game
{
[TestFixture]
public class GameTests
{
    [Test]
    public void Compound_TranslateRoundTrip()
    {
        var origins = new int[][] { new int[] { 0,0,0,0 }, new int[] { 1,0,0,0 } };
        var c = new Piece(origins);
        var before = IntegerOps.Clone(c.origins);
        c.Translate(IntegerSignedAxis.PD1);
        c.Translate(IntegerSignedAxis.MD1);
        Assert.That(IntegerOps.SetEqual(c.origins, before), Is.True);
    }

    [Test]
    public void Compound_RotateRoundTrip()
    {
        var origins = new int[][] {
            new int[] { 0,0,0,0 }, new int[] { 1,0,0,0 },
            new int[] { 0,1,0,0 }, new int[] { 1,1,0,0 }
        };
        var c = new Piece(origins);
        var before = IntegerOps.Clone(c.origins);
        var pivot = new IntegerCenter(c.origins, asCubes: true);
        c.Rotate(0, 1, pivot); // rotate XY
        c.Rotate(1, 0, pivot); // counter-rotate XY
        Assert.That(IntegerOps.SetEqual(c.origins, before), Is.True);
    }

    [Test]
    public void Compound_Combine()
    {
        var c0 = new Piece(new int[][] { new int[] { 0,0,0,0 } });
        var c1 = new Piece(new int[][] { new int[] { 1,0,0,0 } });
        c0.Combine(new[] { c1 });
        Assert.That(c0.origins.Length, Is.EqualTo(2));
        Assert.That(IntegerOps.SetEqual(c0.origins,
            new int[][] { new int[] { 0,0,0,0 }, new int[] { 1,0,0,0 } }), Is.True);
    }

    [Test]
    public void GameLevel_CollisionBlocksMove()
    {
        // Two pieces side by side: piece0 at (0,0,0,0), piece1 at (1,0,0,0)
        // Moving piece0 +X should be blocked
        var obj = new Objective("test",
            new int[][] { new int[] { 0,0,0,0 } },
            new int[][][] {
                new int[][] { new int[] { 0,0,0,0 } },
                new int[][] { new int[] { 1,0,0,0 } },
            });
        var level = new GameLevel(obj);
        level.SelectPiece(0);
        bool moved = level.TranslateSelected(IntegerSignedAxis.PD1);
        Assert.That(moved, Is.False);
        Assert.That(IntegerOps.SetEqual(level.pieces[0].origins,
            new int[][] { new int[] { 0,0,0,0 } }), Is.True);
    }

    [Test]
    public void GameLevel_Bar_Reached()
    {
        // Bar level: goal is 3 cells in a row [0,1,2 at x-axis]
        // piece0: two cells at (-1,0,0,0) and (-1,1,0,0) → need to move to connect with piece1
        // piece1: one cell at (1,0,0,0)
        // Simplest test: single compound already matches goal → Reached immediately
        var goal = new int[][] {
            new int[] { 0,0,0,0 }, new int[] { 1,0,0,0 }, new int[] { 2,0,0,0 }
        };
        var obj = new Objective("Bar",
            goal,
            new int[][][] {
                new int[][] {
                    new int[] { 0,0,0,0 }, new int[] { 1,0,0,0 }, new int[] { 2,0,0,0 }
                }
            });
        var level = new GameLevel(obj);
        Assert.That(level.status, Is.EqualTo(GameStatus.Reached));
    }

    [Test]
    public void GameLevel_Absolute_TranslatedShapeIsNotReached()
    {
        // Goal lives at x=0..1; the single compound is the same shape but shifted to
        // x=2..3. Absolute mode must reject it — congruence required. It stays Pending:
        // absolute mode never reports Missed, the player can always translate back.
        var goal = new int[][] { new int[] { 0,0,0,0 }, new int[] { 1,0,0,0 } };
        var obj = new Objective("shifted", goal,
            new int[][][] {
                new int[][] { new int[] { 2,0,0,0 }, new int[] { 3,0,0,0 } },
            }) { mode = GoalMode.Absolute };
        Assert.That(obj.mode, Is.EqualTo(GoalMode.Absolute));
        var level = new GameLevel(obj);
        Assert.That(level.status, Is.EqualTo(GameStatus.Pending));
    }

    [Test]
    public void GameLevel_Absolute_SeparatePiecesFillingGoalAreReached()
    {
        // Two uncombined pieces that together cover exactly the goal cells.
        var goal = new int[][] { new int[] { 0,0,0,0 }, new int[] { 1,0,0,0 } };
        var obj = new Objective("split", goal,
            new int[][][] {
                new int[][] { new int[] { 0,0,0,0 } },
                new int[][] { new int[] { 1,0,0,0 } },
            }) { mode = GoalMode.Absolute };
        var level = new GameLevel(obj);
        Assert.That(level.pieces.Count, Is.EqualTo(2));
        Assert.That(level.status, Is.EqualTo(GameStatus.Reached));
    }

    [Test]
    public void GameLevel_Absolute_PartiallyFilledGoalIsPending()
    {
        var goal = new int[][] { new int[] { 0,0,0,0 }, new int[] { 1,0,0,0 } };
        var obj = new Objective("partial", goal,
            new int[][][] {
                new int[][] { new int[] { 0,0,0,0 } },
            }) { mode = GoalMode.Absolute };
        var level = new GameLevel(obj);
        Assert.That(level.status, Is.EqualTo(GameStatus.Pending));
    }

    [Test]
    public void GameLevel_Shape_TranslatedShapeIsReached()
    {
        // Same translated shape, but in Shape mode equality-modulo-motion accepts it.
        var goal = new int[][] { new int[] { 0,0,0,0 }, new int[] { 1,0,0,0 } };
        var obj = new Objective("shifted", goal,
            new int[][][] {
                new int[][] { new int[] { 2,0,0,0 }, new int[] { 3,0,0,0 } },
            });
        obj.mode = GoalMode.Shape;
        var level = new GameLevel(obj);
        Assert.That(level.status, Is.EqualTo(GameStatus.Reached));
    }

    [Test]
    public void Objective_ModeJsonRoundTrip()
    {
        var goal = new int[][] { new int[] { 0,0,0,0 }, new int[] { 1,0,0,0 } };
        var pieces = new int[][][] { new int[][] { new int[] { 0,0,0,0 } } };

        // Default (Shape) omits "mode" and round-trips back to Shape.
        var def = new Objective("d", goal, pieces);
        Assert.That(def.ToJson(), Does.Not.Contain("mode"));
        Assert.That(Objective.FromJson(def.ToJson()).mode, Is.EqualTo(GoalMode.Shape));

        // Absolute is emitted and parsed back.
        var abs = new Objective("a", goal, pieces) { mode = GoalMode.Absolute };
        Assert.That(abs.ToJson(), Does.Contain("absolute"));
        Assert.That(Objective.FromJson(abs.ToJson()).mode, Is.EqualTo(GoalMode.Absolute));
    }

    [Test]
    public void Objective_ScalarPaddingKeepsTotalSlackPerAxis()
    {
        // One cell at the origin: bounding box [0,1) on every axis. The code-constructed form
        // uses scale 1 and DefaultDist, so z gets two near cells (see the dist tests); every axis
        // still carries 2·padding cells of slack in total.
        var goal = new int[][] { new int[] { 0,0,0,0 } };
        var pieces = new int[][][] { new int[][] { new int[] { 0,0,0,0 } } };
        var p3 = new Objective("p3", goal, pieces, 3);
        Assert.That(p3.boundary_min_max[0], Is.EqualTo(new int[] { -3, -3, -2, -1 }));
        Assert.That(p3.boundary_min_max[1], Is.EqualTo(new int[] {  4,  4,  5,  6 }));
        for (int k = 0; k < 4; k++)
            Assert.That(p3.PaddingsLowerUpper()[0][k] + p3.PaddingsLowerUpper()[1][k], Is.EqualTo(6));
    }

    [Test]
    public void Objective_EnvelopeIsWrittenAsPaddings()
    {
        var goal = new int[][] { new int[] { 0,0,0,0 }, new int[] { 1,0,0,0 } };
        var pieces = new int[][][] { new int[][] { new int[] { 0,0,0,0 } } };

        // The envelope is exported as paddings_lower_upper, never as boundary_min_max...
        var padded = new Objective("p", goal, pieces, 2);
        Assert.That(padded.ToJson(), Does.Contain("paddings_lower_upper"));
        Assert.That(padded.ToJson(), Does.Not.Contain("boundary_min_max"));
        Assert.That(Objective.FromJson(padded.ToJson()).boundary_min_max,
                    Is.EqualTo(padded.boundary_min_max));

        // ...also when it was authored as an explicit boundary_min_max that no padding
        // combination of the scalar form could produce (asymmetric, and cutting into the
        // bounding box on +x, i.e. a negative padding).
        var bmm = new int[][] { new int[] { -3,-1,0,0 }, new int[] { 1,5,2,4 } };
        var explicitBox = new Objective("b", goal, pieces, bmm);
        Assert.That(explicitBox.ToJson(), Does.Not.Contain("boundary_min_max"));
        Assert.That(Objective.FromJson(explicitBox.ToJson()).boundary_min_max, Is.EqualTo(bmm));
    }

    [Test]
    public void Objective_MetadataJsonRoundTrip()
    {
        var goal = new int[][] { new int[] { 0,0,0,0 }, new int[] { 1,0,0,0 } };
        var pieces = new int[][][] { new int[][] { new int[] { 0,0,0,0 } } };

        // Absent metadata stays absent on round-trip (no empty "description"/"author" noise).
        var bare = new Objective("bare", goal, pieces);
        Assert.That(bare.ToJson(), Does.Not.Contain("description"));
        Assert.That(bare.ToJson(), Does.Not.Contain("author"));
        Assert.That(Objective.FromJson(bare.ToJson()).description, Is.Null);
        Assert.That(Objective.FromJson(bare.ToJson()).author, Is.Null);

        // Present metadata is emitted and parsed back.
        var meta = new Objective("meta", goal, pieces)
        {
            description = "Slide the <b>bar</b> home.",
            author = "bo",
        };
        var round = Objective.FromJson(meta.ToJson());
        Assert.That(round.description, Is.EqualTo("Slide the <b>bar</b> home."));
        Assert.That(round.author, Is.EqualTo("bo"));
    }

    [Test]
    public void Objective_PointsJsonRoundTrip()
    {
        var goal = new int[][] { new int[] { 0,0,0,0 }, new int[] { 1,0,0,0 } };
        var pieces = new int[][][] { new int[][] { new int[] { 0,0,0,0 } } };

        // The default weight 1 stays absent on round-trip (same policy as "mode"): unrated level
        // files carry no "points" field.
        var unrated = new Objective("u", goal, pieces);
        Assert.That(unrated.points, Is.EqualTo(1));
        Assert.That(unrated.ToJson(), Does.Not.Contain("points"));
        Assert.That(Objective.FromJson(unrated.ToJson()).points, Is.EqualTo(1));

        // A non-default weight is emitted and parsed back.
        var weighted = new Objective("w", goal, pieces) { points = 3 };
        Assert.That(weighted.ToJson(), Does.Contain("\"points\": 3"));
        Assert.That(Objective.FromJson(weighted.ToJson()).points, Is.EqualTo(3));

        // A zero/negative weight would corrupt the point-based progression — fail fast on parse.
        Assert.Throws<ArgumentException>(() =>
            Objective.FromJson(weighted.ToJson().Replace("\"points\": 3", "\"points\": 0")));
    }

    [Test]
    public void Objective_DistJsonRoundTrip()
    {
        var goal = new int[][] { new int[] { 0,0,0,0 }, new int[] { 1,0,0,0 } };
        var pieces = new int[][][] { new int[][] { new int[] { 0,0,0,0 } } };

        // Unset stays unset on round-trip (Dist reads DefaultDist), and level files without the
        // field stay free of it.
        var inherit = new Objective("i", goal, pieces);
        Assert.That(inherit.dist, Is.Null);
        Assert.That(inherit.Dist, Is.EqualTo(Objective.DefaultDist));
        Assert.That(inherit.ToJson(), Does.Not.Contain("dist"));
        Assert.That(Objective.FromJson(inherit.ToJson()).dist, Is.Null);

        // An explicit facade distance is emitted and parsed back.
        var far = new Objective("f", goal, pieces) { dist = 4.5 };
        Assert.That(far.ToJson(), Does.Contain("\"dist\": 4.5"));
        Assert.That(Objective.FromJson(far.ToJson()).dist, Is.EqualTo(4.5));

        // Zero/negative is meaningless — fail fast.
        Assert.Throws<ArgumentException>(() =>
            Objective.FromJson(far.ToJson().Replace("\"dist\": 4.5", "\"dist\": 0")));
    }

    static string ScalarLevel(int padding, string extra = "") =>
        "{ \"name\": \"s\", \"goal\": [[0,0,0,0]], \"pieces\": [[[0,0,0,0]]], \"padding\": " + padding + extra + " }";

    [Test]
    public void Objective_DistDerivesNearZPaddingUpToTheCap()
    {
        // Default dist 3.5 at scale 1: the gap to the 0.3 m front line is 3.2 m; the near w cell
        // takes 0.5 of it, so two whole z cells fit — up to the cap (padding 2 → 2, padding 3 → 2
        // as well, since only two fit). The far side takes the rest of the 2·padding slack.
        var p2 = Objective.FromJson(ScalarLevel(2));
        Assert.That(p2.PaddingsLowerUpper()[0], Is.EqualTo(new int[] { 2, 2, 2, 1 }));
        Assert.That(p2.PaddingsLowerUpper()[1], Is.EqualTo(new int[] { 2, 2, 2, 3 }));
        Assert.That(p2.EnvelopeFrontDistance, Is.EqualTo(1.0).Within(1e-9));
        var p3 = Objective.FromJson(ScalarLevel(3));
        Assert.That(p3.PaddingsLowerUpper()[0], Is.EqualTo(new int[] { 3, 3, 2, 1 }));
        Assert.That(p3.PaddingsLowerUpper()[1], Is.EqualTo(new int[] { 3, 3, 4, 5 }));

        // A smaller dist fills fewer cells: 2.5 m leaves exactly one z cell.
        var near = Objective.FromJson(ScalarLevel(3, ", \"dist\": 2.5"));
        Assert.That(near.PaddingsLowerUpper()[0], Is.EqualTo(new int[] { 3, 3, 1, 1 }));
        Assert.That(near.EnvelopeFrontDistance, Is.EqualTo(1.0).Within(1e-9));

        // Beyond the cap the remainder is pure distance: padding 2 at 5 m → two cells, front at 2.5 m.
        var far = Objective.FromJson(ScalarLevel(2, ", \"dist\": 5"));
        Assert.That(far.PaddingsLowerUpper()[0], Is.EqualTo(new int[] { 2, 2, 2, 1 }));
        Assert.That(far.EnvelopeFrontDistance, Is.EqualTo(2.5).Within(1e-9));

        // A cell fraction goes into distance, never into a partial cell (3.2 m: 2.9 m of room
        // minus the w cell's 0.5 holds two whole cells, the 0.4 m remainder is distance → front 0.7 m).
        var frac = Objective.FromJson(ScalarLevel(2, ", \"dist\": 3.2"));
        Assert.That(frac.PaddingsLowerUpper()[0][2], Is.EqualTo(2));
        Assert.That(frac.EnvelopeFrontDistance, Is.EqualTo(0.7).Within(1e-9));

        // Scale counts: at 0.5 the same 3.5 m holds four z cells, capped at padding 3.
        var small = Objective.FromJson(ScalarLevel(3, ", \"scale\": 0.5"));
        Assert.That(small.PaddingsLowerUpper()[0][2], Is.EqualTo(3));

        // padding 1 keeps its single workbench cell; padding 0 adds nothing (no negative far side).
        Assert.That(Objective.FromJson(ScalarLevel(1)).PaddingsLowerUpper()[0], Is.EqualTo(new int[] { 1, 1, 1, 1 }));
        Assert.That(Objective.FromJson(ScalarLevel(1)).PaddingsLowerUpper()[1], Is.EqualTo(new int[] { 1, 1, 1, 1 }));
        Assert.That(Objective.FromJson(ScalarLevel(0)).PaddingsLowerUpper()[0], Is.EqualTo(new int[] { 0, 0, 0, 0 }));
        Assert.That(Objective.FromJson(ScalarLevel(0)).PaddingsLowerUpper()[1], Is.EqualTo(new int[] { 0, 0, 0, 0 }));
    }

    [Test]
    public void Objective_EnvelopeFrontMustStayBehindTheComfortLine()
    {
        // Scalar form: a dist too small for even the fixed near cells fails.
        Assert.Throws<ArgumentException>(() => Objective.FromJson(ScalarLevel(1, ", \"dist\": 1.2")));
        // Explicit form is literal: dist only places the facade, so a large near margin with a
        // small dist would pull the envelope front through the player — fail fast.
        const string explicitNear = "{ \"name\": \"e\", \"goal\": [[0,0,0,0]], \"pieces\": [[[0,0,0,0]]], " +
                                    "\"paddings_lower_upper\": [[1,1,3,1],[1,1,1,1]], \"dist\": 3 }";
        Assert.Throws<ArgumentException>(() => Objective.FromJson(explicitNear));
        Assert.That(Objective.FromJson(explicitNear.Replace("\"dist\": 3", "\"dist\": 4.5")).EnvelopeFrontDistance,
                    Is.EqualTo(1.0).Within(1e-9));
    }

    [Test]
    public void GameLevel_CombineAndReach()
    {
        // Two single cells, goal is their union
        var goal = new int[][] { new int[] { 0,0,0,0 }, new int[] { 1,0,0,0 } };
        var obj = new Objective("two",
            goal,
            new int[][][] {
                new int[][] { new int[] { 0,0,0,0 } },
                new int[][] { new int[] { 1,0,0,0 } },
            });
        var level = new GameLevel(obj);
        Assert.That(level.status, Is.EqualTo(GameStatus.Pending));
        level.SelectPiece(0);
        level.CombineSelected();
        Assert.That(level.pieces.Count, Is.EqualTo(1));
        Assert.That(level.status, Is.EqualTo(GameStatus.Reached));
    }

    [Test]
    public void GameLevel_Combine_IsTransitive_SingleEvent()
    {
        // Three unit pieces in a row: piece 2 is NOT adjacent to the selected piece 0, only to
        // piece 1 — it must still be absorbed (a combine merges everything connected to the
        // selected piece through a chain of adjacencies), and the whole cascade must surface as
        // ONE OnCombine event.
        var cells = new int[][] { new int[] { 0,0,0,0 }, new int[] { 1,0,0,0 }, new int[] { 2,0,0,0 } };
        var obj = new Objective("row", cells,
            new int[][][] {
                new int[][] { cells[0] },
                new int[][] { cells[1] },
                new int[][] { cells[2] },
            });
        var level = new GameLevel(obj);
        int combineEvents = 0;
        level.OnCombine += (idx) => combineEvents++;

        level.SelectPiece(0);
        level.CombineSelected();

        Assert.That(level.pieces.Count, Is.EqualTo(1));
        Assert.That(level.pieces[0].origins.Length, Is.EqualTo(3));
        Assert.That(combineEvents, Is.EqualTo(1));
        Assert.That(level.selectedIndex, Is.EqualTo(0));
        Assert.That(level.status, Is.EqualTo(GameStatus.Reached));
    }

    [Test]
    public void GameLevel_Values_Count()
    {
        var catalog = ObjectiveCatalog.Values();
        Assert.That(catalog.Length, Is.GreaterThan(0));
        foreach (var obj in catalog)
        {
            Assert.That(obj, Is.Not.Null);
            Assert.That(obj.name, Is.Not.Null.And.Not.Empty);
            Assert.That(obj.goal, Is.Not.Null.And.Not.Empty);
            Assert.That(obj.pieces, Is.Not.Null.And.Not.Empty);
        }
    }

    [Test]
    public void GameLevel_Bar_Catalog()
    {
        var bar = ObjectiveCatalog.Bar;
        Assert.That(bar.goal.Length, Is.EqualTo(3));
        Assert.That(bar.pieces.Length, Is.EqualTo(2));
    }

    [Test]
    public void GameLevel_Events_Fire()
    {
        var obj = new Objective("test",
            new int[][] { new int[] { 0,0,0,0 }, new int[] { 1,0,0,0 } },
            new int[][][] {
                new int[][] { new int[] { 0,0,0,0 } },
                new int[][] { new int[] { 2,0,0,0 } }
            });
        // The in-place turn of piece 1 flush against piece 0 is legal under the default
        // swept-rotation rules: only the forgiven corner lenses cross the shared face
        // (inscribed-disk semantics, see RotationSweepTests).
        var level = new GameLevel(obj);

        bool translateFired = false;
        bool rotateFired = false;
        bool combineFired = false;
        bool resetFired = false;
        bool changedFired = false;

        level.OnTranslate += (idx, axis) => translateFired = true;
        level.OnRotate += (idx, v, w, pivot) => rotateFired = true;
        level.OnCombine += (idx) => combineFired = true;
        level.OnReset += () => resetFired = true;
        level.OnChanged += () => changedFired = true;

        level.SelectPiece(1);
        level.TranslateSelected(IntegerSignedAxis.MD1); // move piece 1 to (1,0,0,0)
        Assert.That(translateFired, Is.True);
        Assert.That(changedFired, Is.True);
        changedFired = false;

        level.RotateSelected(0, 1);
        Assert.That(rotateFired, Is.True);
        Assert.That(changedFired, Is.True);
        changedFired = false;

        level.CombineSelected();
        Assert.That(combineFired, Is.True);
        Assert.That(changedFired, Is.True);
        changedFired = false;

        level.Reset();
        Assert.That(resetFired, Is.True);
        Assert.That(changedFired, Is.True);
    }
    }
}
