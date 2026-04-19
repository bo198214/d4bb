using NUnit.Framework;
using D4BB.Comb;

namespace D4BB.Game
{
[TestFixture]
public class GameTests
{
    [Test]
    public void Compound_TranslateRoundTrip()
    {
        var origins = new int[][] { new int[] { 0,0,0,0 }, new int[] { 1,0,0,0 } };
        var c = new Compound(origins);
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
        var c = new Compound(origins);
        var before = IntegerOps.Clone(c.origins);
        c.Rotate(0, 1); // rotate XY
        c.Rotate(1, 0); // counter-rotate XY
        Assert.That(IntegerOps.SetEqual(c.origins, before), Is.True);
    }

    [Test]
    public void Compound_Combine()
    {
        var c0 = new Compound(new int[][] { new int[] { 0,0,0,0 } });
        var c1 = new Compound(new int[][] { new int[] { 1,0,0,0 } });
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
        Assert.That(IntegerOps.SetEqual(level.compounds[0].origins,
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
        Assert.That(level.compounds.Count, Is.EqualTo(1));
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
}
}
