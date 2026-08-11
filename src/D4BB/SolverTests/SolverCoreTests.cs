using System;
using System.IO;
using System.Linq;
using D4BB.Game;
using D4BB.Solver;
using NUnit.Framework;

namespace D4BB.SolverTests
{
    /// <summary>
    /// The solver's own guards: the move notation must round-trip and must mean in the engine what
    /// <c>tools/puzzle/RULES.md</c> says it means (solution files are shared between the two tools),
    /// and each verdict tier must be reachable on a level small enough to check by hand.
    /// </summary>
    [TestFixture]
    public class SolverCoreTests
    {
        static int[] C(int x, int y, int z, int w) => new[] { x, y, z, w };

        // ── notation ──────────────────────────────────────────────────────────────────────────

        [TestCase("1t+x")]
        [TestCase("2t-w")]
        [TestCase("3r+zw")]
        [TestCase("1r-xy")]
        [TestCase("4r+yz@0,3,0,0")]
        [TestCase("2c")]
        public void Notation_RoundTrips(string token)
        {
            Assert.That(Move.Parse(token).ToString(), Is.EqualTo(token));
        }

        [Test]
        public void Notation_NegativeSenseIsTheSwappedPlane()
        {
            // r-vw is the inverse of r+vw, i.e. the (w,v) rotation — so r+wz and r-zw are one move
            // and print identically.
            Assert.That(Move.Parse("2r+wz").ToString(), Is.EqualTo("2r-zw"));
            var m = Move.Parse("2r-zw");
            Assert.That((m.V, m.W), Is.EqualTo((3, 2)));
        }

        [TestCase("1x+x")]
        [TestCase("1t+xy")]
        [TestCase("1r+xx")]
        [TestCase("0t+x")]
        [TestCase("1t+x@0,0,0,0")]
        public void Notation_RejectsNonsense(string token)
        {
            Assert.Throws<FormatException>(() => Move.Parse(token));
        }

        [Test]
        public void Notation_CommentsAndSeparators()
        {
            var moves = Move.ParseSequence("1t+x 1t+x   # slide it over\n2r+zw,2c\n");
            Assert.That(moves.Select(m => m.ToString()),
                        Is.EqualTo(new[] { "1t+x", "1t+x", "2r+zw", "2c" }));
        }

        [Test]
        public void Notation_PivotCommasAreNotMoveSeparators()
        {
            // ',' separates moves AND pivot coordinates. Splitting on it tore every generated
            // rotation into four unparsable fragments — a whole sweep of solution files was
            // unreadable on the next run before this was tokenised instead of split.
            var moves = Move.ParseSequence("1r+xy@0,3,0,0 2t-w\n3r-zw@-1,0,2,0,1c");
            Assert.That(moves.Select(m => m.ToString()),
                        Is.EqualTo(new[] { "1r+xy@0,3,0,0", "2t-w", "3r-zw@-1,0,2,0", "1c" }));
        }

        [Test]
        public void SolutionFile_SurvivesTheRoundTripItIsWrittenFor()
        {
            // The scenario that actually broke: sweep 1 searches and writes the file, sweep 2 reads
            // it back and must confirm it. The square's solution contains rotations, so the file
            // carries explicit @pivot coordinates — the part that used to be shredded on re-read.
            var obj = Square();
            var dir = Path.Combine(Path.GetTempPath(), "tesserian-solver-roundtrip");
            Directory.CreateDirectory(dir);
            var levelPath = Path.Combine(dir, "square.json");
            File.WriteAllText(levelPath, obj.ToJson());
            try
            {
                var first = LevelValidator.Check(obj, levelPath, new ValidatorOptions());
                Assert.That(first.Verdict, Is.EqualTo(LevelVerdict.Solved), first.Detail);
                Assert.That(first.SolutionWritten, Is.True, "the search should have written a file");
                var text = File.ReadAllText(SolutionFile.PathFor(levelPath));
                Assert.That(text, Does.Contain("@"), "expected an explicit pivot in the file");

                var second = LevelValidator.Check(obj, levelPath, new ValidatorOptions());
                Assert.That(second.SolutionFromFile, Is.True, "the second pass should read the file");
                Assert.That(second.Verdict, Is.EqualTo(LevelVerdict.Solved), second.Detail);
            }
            finally { Directory.Delete(dir, true); }
        }

        // ── verifier ──────────────────────────────────────────────────────────────────────────

        /// <summary>Two single cells three apart; the goal is the domino they make when adjacent.</summary>
        static Objective SlideTogether()
            => new Objective("slide",
                new[] { C(0, 0, 0, 0), C(1, 0, 0, 0) },
                new[] { new[] { C(0, 0, 0, 0) }, new[] { C(3, 0, 0, 0) } },
                padding: 2);

        [Test]
        public void Verifier_AcceptsAWinningSequence()
        {
            var res = SolutionVerifier.Verify(SlideTogether(), Move.ParseSequence("2t-x 2t-x"));
            Assert.That(res.Solved, Is.True, res.Failure);
            Assert.That(res.PiecesRemaining, Is.EqualTo(1), "shape mode combines at the end");
        }

        [Test]
        public void Verifier_RejectsAnIncompleteSequence()
        {
            var res = SolutionVerifier.Verify(SlideTogether(), Move.ParseSequence("2t-x"));
            Assert.That(res.Solved, Is.False);
            Assert.That(res.FailedMoveIndex, Is.EqualTo(-1), "the move itself was legal");
            Assert.That(res.Failure, Does.Contain("not one compound"));
        }

        [Test]
        public void Verifier_ReportsTheBlockedMove()
        {
            var res = SolutionVerifier.Verify(SlideTogether(), Move.ParseSequence("2t-x 2t-x 2t-x"));
            Assert.That(res.Solved, Is.False);
            Assert.That(res.FailedMoveIndex, Is.EqualTo(2), "the third step walks into piece 1");
            Assert.That(res.Failure, Does.Contain("Overlap"));
        }

        [Test]
        public void Verifier_PieceNumbersSurviveACombine()
        {
            // After "1c" absorbs piece 2, the merged piece is still "1" — and "2" is gone.
            var obj = new Objective("combine",
                new[] { C(0, 0, 0, 0), C(1, 0, 0, 0) },
                new[] { new[] { C(0, 0, 0, 0) }, new[] { C(1, 0, 0, 0) } },
                padding: 2);
            Assert.That(SolutionVerifier.Verify(obj, Move.ParseSequence("1c")).Solved, Is.True);
            var res = SolutionVerifier.Verify(obj, Move.ParseSequence("1c 2t+w"));
            Assert.That(res.Failure, Does.Contain("does not exist"));
        }

        [Test]
        public void Verifier_DefaultPivotIsTheSmallestCell()
        {
            // RULES.md/p.py convention. A domino along x turned in the x-y plane about its own
            // smallest cell keeps that cell and swings the other one to +y.
            var obj = new Objective("pivot",
                new[] { C(0, 0, 0, 0), C(0, 1, 0, 0) },
                new[] { new[] { C(0, 0, 0, 0), C(1, 0, 0, 0) } },
                padding: 3);
            var level = new GameLevel(obj);
            var piece = level.pieces[0];
            Assert.That(SolutionVerifier.DefaultPivot(piece), Is.EqualTo(C(0, 0, 0, 0)));
        }

        // ── the three verdict tiers ───────────────────────────────────────────────────────────

        /// <summary>A 2x2 square from two dominoes, one of which lies the wrong way round —
        /// solving it needs a real rotation, not just sliding.</summary>
        static Objective Square()
            => new Objective("square",
                new[] { C(0, 0, 0, 0), C(1, 0, 0, 0), C(0, 1, 0, 0), C(1, 1, 0, 0) },
                new[]
                {
                    new[] { C(0, 0, 0, 0), C(1, 0, 0, 0) },
                    new[] { C(4, 0, 0, 0), C(4, 1, 0, 0) },
                },
                padding: 2);

        [Test]
        public void Validator_ProvesSolvableByFindingAndReplayingMoves()
        {
            var report = LevelValidator.Check(Square(), null, new ValidatorOptions { WriteSolutions = false });
            Assert.That(report.Verdict, Is.EqualTo(LevelVerdict.Solved), report.Detail);
            Assert.That(report.MoveCount, Is.GreaterThan(0));
        }

        [Test]
        public void Validator_ProvesUnsolvableWhenNothingTiles()
        {
            // A straight tromino cannot fit inside a 2x2 square, whatever the single cell does.
            var obj = new Objective("no-tiling",
                new[] { C(0, 0, 0, 0), C(1, 0, 0, 0), C(0, 1, 0, 0), C(1, 1, 0, 0) },
                new[]
                {
                    new[] { C(5, 0, 0, 0), C(6, 0, 0, 0), C(7, 0, 0, 0) },
                    new[] { C(5, 2, 0, 0) },
                },
                padding: 2);
            var report = LevelValidator.Check(obj, null, new ValidatorOptions { WriteSolutions = false });
            Assert.That(report.Verdict, Is.EqualTo(LevelVerdict.Unsolvable), report.Detail);
        }

        [Test]
        public void Assembly_HandlesDisconnectedPieces()
        {
            // A piece need not be face-connected in this game, and levels are built on that. Here
            // both pieces are diagonal cell pairs that interleave into a 2x2 square — already
            // solved, in fact. The exact cover's region prune ("each component of the uncovered
            // region must be a subset sum of the remaining piece sizes") is FALSE for such pieces:
            // after placing the first, two single-cell components remain and no subset of {2} makes
            // 1. Applying it anyway reported 114 real levels as provably unsolvable.
            var goal = new[] { C(0, 0, 0, 0), C(1, 0, 0, 0), C(0, 1, 0, 0), C(1, 1, 0, 0) };
            var pieces = new[]
            {
                new[] { C(0, 0, 0, 0), C(1, 1, 0, 0) },
                new[] { C(1, 0, 0, 0), C(0, 1, 0, 0) },
            };
            var result = new AssemblySolver(goal, pieces).Solve();
            Assert.That(result.Verdict, Is.EqualTo(AssemblyVerdict.Exists), result.Reason);
        }

        [Test]
        public void Validator_ProvesUnsolvableOnACellCountMismatch()
        {
            var obj = new Objective("count",
                new[] { C(0, 0, 0, 0), C(1, 0, 0, 0), C(2, 0, 0, 0) },
                new[] { new[] { C(0, 0, 0, 0) }, new[] { C(4, 0, 0, 0) } },
                padding: 2);
            var report = LevelValidator.Check(obj, null, new ValidatorOptions { WriteSolutions = false });
            Assert.That(report.Verdict, Is.EqualTo(LevelVerdict.Unsolvable));
            Assert.That(report.Detail, Does.Contain("2 cells but the goal has 3"));
        }

        [Test]
        public void Validator_ProvesUnsolvableOnADisconnectedShapeGoal()
        {
            // Shape mode wins only as ONE compound, and pieces merge only across shared 3-cells.
            var obj = new Objective("split",
                new[] { C(0, 0, 0, 0), C(2, 0, 0, 0) },
                new[] { new[] { C(0, 0, 0, 0) }, new[] { C(4, 0, 0, 0) } },
                padding: 2);
            var report = LevelValidator.Check(obj, null, new ValidatorOptions { WriteSolutions = false });
            Assert.That(report.Verdict, Is.EqualTo(LevelVerdict.Unsolvable));
            Assert.That(report.Detail, Does.Contain("face-connected"));
        }

        [Test]
        public void Validator_AbsoluteModeKeepsTheGoalWhereItIs()
        {
            // Same two cells, but absolute mode: they must end on the goal cells themselves, and a
            // disconnected goal is fine there (no combine is required).
            var obj = new Objective("absolute",
                new[] { C(0, 0, 0, 0), C(2, 0, 0, 0) },
                new[] { new[] { C(0, 0, 0, 0) }, new[] { C(4, 0, 0, 0) } },
                padding: 2)
            { mode = GoalMode.Absolute };
            var report = LevelValidator.Check(obj, null, new ValidatorOptions { WriteSolutions = false });
            Assert.That(report.Verdict, Is.EqualTo(LevelVerdict.Solved), report.Detail);
        }
    }
}
