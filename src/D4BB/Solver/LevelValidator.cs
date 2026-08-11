using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using D4BB.Comb;
using D4BB.Game;

namespace D4BB.Solver
{
    public enum LevelVerdict
    {
        /// <summary>A move sequence was replayed through the real engine and won the level.</summary>
        Solved,
        /// <summary>Proven unsolvable: a structural defect, or the pieces cannot tile the goal.</summary>
        Unsolvable,
        /// <summary>The pieces CAN tile the goal, but no move sequence into that tiling was found.</summary>
        AssemblyOnly,
        /// <summary>Budgets ran out before anything could be concluded.</summary>
        Unknown,
        /// <summary>A solution file exists but its moves do not win the level.</summary>
        SolutionFileInvalid,
    }

    public sealed class ValidatorOptions
    {
        /// <summary>Replay <c>&lt;level&gt;.moves</c> instead of searching, when it exists.</summary>
        public bool UseSolutionFiles = true;
        /// <summary>Write <c>&lt;level&gt;.moves</c> whenever the search solves a level itself.</summary>
        public bool WriteSolutions = true;
        /// <summary>Drop the search's removable detours before reporting/writing a solution.</summary>
        public bool ShortenSolutions = true;
        public TimeSpan AssemblyBudget = TimeSpan.FromSeconds(10);
        /// <summary>How many distinct tilings to hand to the path search before giving up.</summary>
        public int MaxAssemblies = 3;
        public PathSearchOptions Path = new PathSearchOptions();
    }

    public sealed class LevelReport
    {
        public string Name;
        public string LevelPath;
        public LevelVerdict Verdict;
        /// <summary>One line: what was proven, or why not.</summary>
        public string Detail;
        /// <summary>Structural oddities that do not by themselves decide solvability.</summary>
        public List<string> Warnings = new List<string>();
        public bool SolutionFromFile;
        public bool SolutionWritten;
        public int MoveCount;
        public long Nodes;
        public TimeSpan Elapsed;

        public override string ToString()
        {
            var src = SolutionFromFile ? "file" : SolutionWritten ? "search→file" : "search";
            return $"{Verdict,-19} {Name,-28} {Detail} " +
                   $"[{src}, {MoveCount} moves, {Nodes} nodes, {Elapsed.TotalSeconds:0.00}s]";
        }
    }

    /// <summary>
    /// The level QA entry point: is this level solvable, and can we prove it?
    ///
    /// <para>Three tiers of answer, cheapest and strongest first:</para>
    /// <list type="number">
    /// <item><b>Structural checks</b> — cell counts, goal connectivity, initial legality. Some of
    /// these settle the question outright (pieces whose cells do not add up to the goal can never
    /// become it).</item>
    /// <item><b>Solution file</b> — if <c>&lt;level&gt;.moves</c> exists it is replayed through a
    /// real <see cref="GameLevel"/>. A pass is the strongest possible verdict and costs
    /// milliseconds; a fail is a defect worth shouting about (the level or the file changed).</item>
    /// <item><b>Search</b> — <see cref="AssemblySolver"/> for "can the pieces tile the goal at all"
    /// (a NO here is a proof of unsolvability), then <see cref="PathSearch"/> for "can they be
    /// manoeuvred there". A found sequence is verified and then written out as the level's solution
    /// file, so the expensive search happens once per level, ever.</item>
    /// </list>
    ///
    /// <para>The verdicts are deliberately asymmetric in strength, and the enum keeps them apart:
    /// <see cref="LevelVerdict.Solved"/> and <see cref="LevelVerdict.Unsolvable"/> are proofs,
    /// <see cref="LevelVerdict.AssemblyOnly"/> and <see cref="LevelVerdict.Unknown"/> are "we do not
    /// know" in two flavours. Nothing here ever reports a level unsolvable because a search ran out
    /// of time.</para>
    /// </summary>
    public static class LevelValidator
    {
        public static LevelReport Check(Objective obj, string levelPath = null, ValidatorOptions options = null)
        {
            var opt = options ?? new ValidatorOptions();
            var watch = Stopwatch.StartNew();
            var report = new LevelReport { Name = obj.name, LevelPath = levelPath };

            var fatal = Structure(obj, report);
            if (fatal != null)
            {
                report.Verdict = LevelVerdict.Unsolvable;
                report.Detail = fatal;
                report.Elapsed = watch.Elapsed;
                return report;
            }

            if (opt.UseSolutionFiles && levelPath != null && SolutionFile.Exists(levelPath))
            {
                report.SolutionFromFile = true;
                List<Move> moves;
                try { moves = SolutionFile.Read(levelPath); }
                catch (FormatException ex)
                {
                    report.Verdict = LevelVerdict.SolutionFileInvalid;
                    report.Detail = $"solution file unreadable: {ex.Message}";
                    report.Elapsed = watch.Elapsed;
                    return report;
                }
                report.MoveCount = moves.Count;
                var verification = SolutionVerifier.Verify(obj, moves);
                report.Verdict = verification.Solved ? LevelVerdict.Solved : LevelVerdict.SolutionFileInvalid;
                report.Detail = verification.Solved
                    ? $"solution file replays to a win ({moves.Count} moves)"
                    : $"solution file rejected — {verification.Failure}";
                report.Elapsed = watch.Elapsed;
                return report;
            }

            var assemblySolver = new AssemblySolver(obj.goal, obj.pieces);
            var assemblies = assemblySolver.Solve(opt.MaxAssemblies, opt.AssemblyBudget);
            report.Nodes += assemblies.Nodes;
            if (assemblies.Verdict == AssemblyVerdict.None)
            {
                report.Verdict = LevelVerdict.Unsolvable;
                report.Detail = assemblies.Reason;
                report.Elapsed = watch.Elapsed;
                return report;
            }
            if (assemblies.Verdict == AssemblyVerdict.Unknown)
            {
                report.Verdict = LevelVerdict.Unknown;
                report.Detail = assemblies.Reason;
                report.Elapsed = watch.Elapsed;
                return report;
            }

            foreach (var assembly in assemblies.Assemblies)
            {
                var path = PathSearch.Search(obj, assembly, opt.Path);
                report.Nodes += path.Nodes;
                if (!path.Found) { report.Detail = path.Note; continue; }

                // The search is a lead generator; the engine decides. A sequence that does not
                // replay is a solver bug, never a solution — say so loudly and keep looking.
                var verification = SolutionVerifier.Verify(obj, path.Moves);
                if (!verification.Solved)
                {
                    report.Warnings.Add("INTERNAL: the search produced a sequence the engine rejects — " +
                                        verification.Failure);
                    continue;
                }
                var solution = opt.ShortenSolutions
                    ? SolutionPolish.Shorten(obj, path.Moves)
                    : path.Moves;
                report.Verdict = LevelVerdict.Solved;
                report.MoveCount = solution.Count;
                report.Detail = $"search found and the engine confirmed a {solution.Count}-move solution";
                if (opt.WriteSolutions && levelPath != null)
                {
                    SolutionFile.Write(levelPath, obj, solution,
                        $"Found by D4BB.Solver.PathSearch ({report.Nodes} nodes).");
                    report.SolutionWritten = true;
                }
                report.Elapsed = watch.Elapsed;
                return report;
            }

            report.Verdict = LevelVerdict.AssemblyOnly;
            report.Detail = "the pieces tile the goal, but no move sequence into that tiling was " +
                            "found — " + (report.Detail ?? "search exhausted");
            report.Elapsed = watch.Elapsed;
            return report;
        }

        /// <summary>
        /// Structural checks. Returns the reason when the level is provably unsolvable before any
        /// search, else null (with anything merely suspicious added to <c>report.Warnings</c>).
        /// </summary>
        static string Structure(Objective obj, LevelReport report)
        {
            if (obj.goal == null || obj.goal.Length == 0) return "the level has no goal cells";
            if (obj.pieces == null || obj.pieces.Length == 0) return "the level has no pieces";

            int dim = obj.goal[0].Length;
            foreach (var piece in obj.pieces)
                foreach (var cell in piece)
                    if (cell.Length != dim)
                        return $"mixed dimensions: a piece cell has {cell.Length} coordinates, " +
                               $"the goal has {dim}";

            int pieceCells = 0;
            for (int p = 0; p < obj.pieces.Length; p++)
            {
                var distinct = new HashSet<string>(obj.pieces[p].Select(c => string.Join(",", c)));
                if (distinct.Count != obj.pieces[p].Length)
                    report.Warnings.Add($"piece {p + 1} lists the same cell more than once " +
                                        $"({obj.pieces[p].Length} entries, {distinct.Count} distinct)");
                pieceCells += distinct.Count;
            }
            int goalCells = new HashSet<string>(obj.goal.Select(c => string.Join(",", c))).Count;
            if (goalCells != obj.goal.Length)
                report.Warnings.Add($"the goal lists the same cell more than once " +
                                    $"({obj.goal.Length} entries, {goalCells} distinct)");

            // Pieces can never overlap, so the assembled compound has exactly as many cells as the
            // pieces together — a mismatch is unwinnable in both modes, whatever the moves.
            if (pieceCells != goalCells)
                return $"the pieces have {pieceCells} cells but the goal has {goalCells} — " +
                       "no arrangement can match it";

            if (IntegerOps.Intersecting(obj.pieces))
                report.Warnings.Add("pieces overlap in the START position — the engine rejects every " +
                                    "move that does not resolve the overlap");

            var bmm = obj.boundary_min_max;
            if (bmm != null)
                for (int p = 0; p < obj.pieces.Length; p++)
                    foreach (var cell in obj.pieces[p])
                        for (int k = 0; k < dim && k < bmm[0].Length; k++)
                            if (cell[k] < bmm[0][k] || cell[k] + 1 > bmm[1][k])
                            {
                                report.Warnings.Add($"piece {p + 1} starts outside the movement " +
                                                    "envelope — it can only move back in, never out");
                                k = dim; p = obj.pieces.Length; // one warning is enough
                                break;
                            }

            // Shape mode wins only once the pieces are ONE compound, and pieces merge only across
            // shared 3-cells — so a goal that falls apart into face-disconnected lumps can never be
            // reported as reached, however perfectly the pieces are arranged.
            if (obj.mode == GoalMode.Shape && obj.pieces.Length > 1 && !IsFaceConnected(obj.goal))
                return "the goal is not face-connected, so the pieces can never combine into the " +
                       "single compound shape mode requires";

            return null;
        }

        static bool IsFaceConnected(int[][] cells)
        {
            var seen = new bool[cells.Length];
            var stack = new Stack<int>();
            stack.Push(0); seen[0] = true;
            int reached = 1;
            while (stack.Count > 0)
            {
                int i = stack.Pop();
                for (int j = 0; j < cells.Length; j++)
                    if (!seen[j] && IntegerOps.D3adjacent(cells[i], cells[j]))
                    {
                        seen[j] = true; reached++; stack.Push(j);
                    }
            }
            return reached == cells.Length;
        }
    }
}
