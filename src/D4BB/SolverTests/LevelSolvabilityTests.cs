using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using D4BB.Game;
using D4BB.Solver;
using NUnit.Framework;

namespace D4BB.SolverTests
{
    /// <summary>
    /// The level QA sweep: every shipped level must be solvable, and the proof is a move sequence
    /// the real engine accepts.
    ///
    /// <para>The first run is the expensive one — it searches. Whatever it solves it writes out as
    /// <c>&lt;level&gt;.moves</c>, so every later run just replays those files (milliseconds per
    /// level) and the search only ever revisits levels that still have no solution file. That is
    /// also the escape hatch for the interlocking levels the search cannot crack: hand-write their
    /// solution file once and the sweep covers them from then on.</para>
    ///
    /// <para>Failure semantics are deliberately narrow: only a PROOF of a problem fails the test —
    /// a structural defect, no possible tiling of the goal, or a solution file that no longer
    /// replays. A level the search merely could not solve is reported Inconclusive, because that is
    /// what "we did not find one" honestly means.</para>
    /// </summary>
    [TestFixture]
    public class LevelSolvabilityTests
    {
        /// <summary>
        /// Per-level budget for the sweep. Small on purpose: with solution files in place almost
        /// every level is a replay, and a level that needs more than this is better served by a
        /// checked-in solution file than by a slower suite (see the runtime budget in
        /// <c>Packages/d4bb</c>'s test conventions).
        /// </summary>
        static ValidatorOptions Options() => new ValidatorOptions
        {
            AssemblyBudget = TimeSpan.FromSeconds(5),
            MaxAssemblies = 3,
            Path = new PathSearchOptions
            {
                Budget = TimeSpan.FromSeconds(4),
                MaxNodes = 150000,
                MaxTargets = 4,
            },
        };

        [TestCaseSource(typeof(LevelFiles), nameof(LevelFiles.Cases))]
        public void Level_IsSolvable(string levelPath)
        {
            if (levelPath == null) Assert.Ignore("levels folder not found next to the package");
            var obj = Objective.FromJsonFile(levelPath);
            var report = LevelValidator.Check(obj, levelPath, Options());

            foreach (var warning in report.Warnings) TestContext.WriteLine("warning: " + warning);
            TestContext.WriteLine(report.ToString());

            switch (report.Verdict)
            {
                case LevelVerdict.Solved:
                    if (report.SolutionWritten)
                        TestContext.WriteLine("wrote " + SolutionFile.PathFor(levelPath));
                    Assert.Pass(report.Detail);
                    break;
                case LevelVerdict.Unsolvable:
                    Assert.Fail($"'{obj.name}' is UNSOLVABLE: {report.Detail}");
                    break;
                case LevelVerdict.SolutionFileInvalid:
                    Assert.Fail($"'{obj.name}': {report.Detail}");
                    break;
                default:
                    Assert.Inconclusive($"'{obj.name}': {report.Detail}");
                    break;
            }
        }

        /// <summary>
        /// Fills the gaps: searches ONLY the levels that still have no solution file, with a budget
        /// far beyond what the sweep can afford. This is the tool for "the sweep left some levels
        /// inconclusive" — run it once, and whatever it cracks turns into a checked-in
        /// <c>.moves</c> file that every later sweep verifies in milliseconds. What survives even
        /// this is a candidate for a hand-written solution file.
        /// </summary>
        [Test, Explicit("Deep run: minutes per unsolved level. Writes solution files.")]
        public void Solve_MissingSolutionFiles()
        {
            var dir = LevelFiles.Directory();
            Assert.That(dir, Is.Not.Null, "levels folder not found");
            var options = new ValidatorOptions
            {
                AssemblyBudget = TimeSpan.FromSeconds(30),
                MaxAssemblies = 8,
                Path = new PathSearchOptions
                {
                    Budget = TimeSpan.FromSeconds(120),
                    MaxNodes = 1500000,
                    MaxTargets = 8,
                    HeuristicWeight = 8,
                },
            };

            int solved = 0, open = 0;
            foreach (var path in LevelFiles.All())
            {
                if (SolutionFile.Exists(path)) continue;
                var obj = Objective.FromJsonFile(path);
                var report = LevelValidator.Check(obj, path, options);
                TestContext.WriteLine($"{LevelFiles.RelativeTo(dir, path),-44} {report}");
                if (report.Verdict == LevelVerdict.Solved) solved++; else open++;
            }
            TestContext.WriteLine($"\nnewly solved: {solved}, still open: {open}");
        }

        /// <summary>
        /// The whole sweep in one test, printing a table and a summary — handy to run on its own
        /// (<c>dotnet test --filter Report_AllLevels</c>) when the per-level cases are too noisy.
        /// Never fails; the per-level cases are the gate.
        /// </summary>
        [Test, Explicit("Reporting run; the per-level cases are the actual gate.")]
        public void Report_AllLevels()
        {
            var dir = LevelFiles.Directory();
            Assert.That(dir, Is.Not.Null, "levels folder not found");
            var options = Options();
            var counts = new Dictionary<LevelVerdict, int>();
            int written = 0;

            foreach (var path in LevelFiles.All())
            {
                var obj = Objective.FromJsonFile(path);
                var report = LevelValidator.Check(obj, path, options);
                counts.TryGetValue(report.Verdict, out int n);
                counts[report.Verdict] = n + 1;
                if (report.SolutionWritten) written++;
                TestContext.WriteLine($"{LevelFiles.RelativeTo(dir, path),-44} {report}");
                foreach (var warning in report.Warnings)
                    TestContext.WriteLine($"{"",-44} warning: {warning}");
            }

            TestContext.WriteLine("");
            foreach (var entry in counts.OrderBy(e => e.Key.ToString()))
                TestContext.WriteLine($"{entry.Key,-20} {entry.Value}");
            TestContext.WriteLine($"solution files written: {written}");
        }
    }
}
