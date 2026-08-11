using System;
using System.Collections.Generic;
using System.Diagnostics;
using D4BB.Comb;
using D4BB.Game;

namespace D4BB.Solver
{
    public sealed class PathSearchOptions
    {
        /// <summary>Wall-clock budget for the whole search (all target attempts together).</summary>
        public TimeSpan Budget = TimeSpan.FromSeconds(5);
        /// <summary>Hard node cap, mostly a memory guard (every node keeps its own cell arrays).</summary>
        public int MaxNodes = 400000;
        /// <summary>How many candidate target assemblies to try before giving up.</summary>
        public int MaxTargets = 4;
        /// <summary>
        /// Weight on the heuristic. 1 = A* (shortest solutions, slowest); higher = greedier, finds
        /// longer solutions faster. Solutions are proofs of solvability, not par scores, so the
        /// default leans greedy.
        /// </summary>
        public int HeuristicWeight = 4;
    }

    public sealed class PathResult
    {
        public bool Found;
        public List<Move> Moves;
        public long Nodes;
        public TimeSpan Elapsed;
        public string Note;
    }

    /// <summary>
    /// Stage 2: can the pieces actually BE MANOEUVRED into an assembly the
    /// <see cref="AssemblySolver"/> found? A weighted best-first search over game states, whose
    /// successors are exactly the moves a player has — one-cell translations and 90° turns about
    /// any of the piece's own cells — under the real constraints (overlap, movement envelope, and
    /// the swept quarter turn via <see cref="RotationSweep"/> unless the level allows quantum
    /// rotation).
    ///
    /// <para><b>Its "found" is a lead, not a verdict.</b> The move sequence it returns is handed to
    /// <see cref="SolutionVerifier"/>, which replays it through a real <see cref="GameLevel"/>;
    /// only that replay decides. A "not found" means nothing at all about solvability — the search
    /// is deliberately incomplete (greedy, budget-capped, and it aims at a handful of candidate
    /// target assemblies rather than all of them). Levels it cannot crack are exactly what
    /// hand-written solution files are for.</para>
    ///
    /// <para>Targets: in shape mode the compound may be built anywhere, so each assembly is tried in
    /// every proper global rotation, translated to sit as close to where the pieces already are as
    /// the envelope permits, and the closest few are searched. In absolute mode only the assembly
    /// as it lies is a target — that mode's win condition is exact.</para>
    /// </summary>
    public static class PathSearch
    {
        public static PathResult Search(Objective obj, Assembly assembly, PathSearchOptions options = null)
        {
            var opt = options ?? new PathSearchOptions();
            var watch = Stopwatch.StartNew();
            var result = new PathResult { Moves = null };

            int dim = obj.pieces[0][0].Length;
            var start = new long[obj.pieces.Length][];
            for (int p = 0; p < obj.pieces.Length; p++) start[p] = Encode(obj.pieces[p], dim);

            var targets = CandidateTargets(obj, assembly, start, dim, opt.MaxTargets);
            if (targets.Count == 0)
            {
                result.Note = "no candidate target assembly fits inside the movement envelope";
                result.Elapsed = watch.Elapsed;
                return result;
            }

            int attempted = 0;
            for (int t = 0; t < targets.Count; t++)
            {
                var target = targets[t];
                var remaining = opt.Budget - watch.Elapsed;
                if (remaining <= TimeSpan.Zero) break;
                // Share what is left evenly over the targets still to try, so a hopeless first
                // target cannot eat the whole budget — and so an early success hands its leftover
                // to the next one.
                var slice = TimeSpan.FromTicks(remaining.Ticks / (targets.Count - t));
                attempted++;
                var attempt = new Attempt(obj, dim, start, target, opt, slice);
                var moves = attempt.Run();
                result.Nodes += attempt.Nodes;
                if (moves != null)
                {
                    result.Found = true;
                    result.Moves = moves;
                    break;
                }
            }
            result.Elapsed = watch.Elapsed;
            if (!result.Found)
                result.Note = $"no path found within {opt.Budget.TotalSeconds:0.#}s " +
                              $"({result.Nodes} nodes, {attempted} of {targets.Count} target " +
                              "assemblies searched)";
            return result;
        }

        // ── the search itself ─────────────────────────────────────────────────────────────────

        sealed class Attempt
        {
            readonly Objective obj;
            readonly int dim;
            readonly long[][] start;
            readonly long[][] target;
            readonly PathSearchOptions opt;
            readonly TimeSpan budget;
            readonly int[][] bmm;
            readonly bool sweep;
            readonly long[] targetSum;      // per piece: summed coordinates of the target cells
            readonly HashSet<long>[] targetSet;

            readonly List<long[][]> states = new List<long[][]>();
            readonly List<int> parents = new List<int>();
            readonly List<Move> arrivals = new List<Move>();
            readonly List<int> gs = new List<int>();
            readonly HashSet<ulong> seen = new HashSet<ulong>();
            readonly Heap open = new Heap();

            public long Nodes;

            public Attempt(Objective obj, int dim, long[][] start, long[][] target,
                           PathSearchOptions opt, TimeSpan budget)
            {
                this.obj = obj; this.dim = dim; this.start = start; this.target = target;
                this.opt = opt; this.budget = budget;
                bmm = obj.boundary_min_max;
                sweep = !obj.quantumRotation;
                targetSet = new HashSet<long>[target.Length];
                targetSum = new long[target.Length * dim];
                var buf = new int[dim];
                for (int p = 0; p < target.Length; p++)
                {
                    targetSet[p] = new HashSet<long>(target[p]);
                    foreach (var code in target[p])
                    {
                        Decode(code, buf, dim);
                        for (int k = 0; k < dim; k++) targetSum[p * dim + k] += buf[k];
                    }
                }
            }

            public List<Move> Run()
            {
                var watch = Stopwatch.StartNew();
                Push(start, -1, default(Move), 0);
                var buf = new int[dim];
                var occupied = new HashSet<long>();

                while (open.Count > 0)
                {
                    if ((++Nodes & 0xFF) == 0 && watch.Elapsed > budget) return null;
                    if (states.Count > opt.MaxNodes) return null;

                    int cur = open.Pop();
                    var state = states[cur];
                    if (IsTarget(state)) return Reconstruct(cur);

                    occupied.Clear();
                    foreach (var piece in state)
                        foreach (var code in piece) occupied.Add(code);

                    for (int p = 0; p < state.Length; p++)
                        Expand(cur, state, p, occupied, buf);
                }
                return null;
            }

            void Expand(int parent, long[][] state, int p, HashSet<long> occupied, int[] buf)
            {
                var own = new HashSet<long>(state[p]);
                int g = gs[parent] + 1;

                // translations
                for (int axis = 0; axis < dim; axis++)
                    for (int sign = -1; sign <= 1; sign += 2)
                    {
                        var moved = new long[state[p].Length];
                        bool ok = true;
                        for (int i = 0; i < moved.Length && ok; i++)
                        {
                            Decode(state[p][i], buf, dim);
                            buf[axis] += sign;
                            if (!InsideBoundary(buf)) { ok = false; break; }
                            long code = Encode(buf, dim);
                            if (occupied.Contains(code) && !own.Contains(code)) { ok = false; break; }
                            moved[i] = code;
                        }
                        if (ok) PushChild(parent, state, p, moved, Move.Translate(p, axis, sign), g);
                    }

                // rotations: every plane, both senses, every cell of the piece as pivot — the same
                // freedom the player has (the pivot is the cube under the grabbed facet).
                var pivot = new int[dim];
                int[][] pre = null;
                List<int[]> obstacles = null;
                if (sweep)
                {
                    pre = DecodeAll(state[p], dim);
                    obstacles = new List<int[]>();
                    for (int q = 0; q < state.Length; q++)
                        if (q != p) obstacles.AddRange(DecodeAll(state[q], dim));
                }
                for (int i = 0; i < state[p].Length; i++)
                {
                    Decode(state[p][i], pivot, dim);
                    for (int v = 0; v < dim; v++)
                        for (int w = 0; w < dim; w++)
                        {
                            if (v == w) continue;
                            var turned = new long[state[p].Length];
                            bool ok = true;
                            for (int j = 0; j < turned.Length && ok; j++)
                            {
                                Decode(state[p][j], buf, dim);
                                RotateAboutCell(buf, pivot, v, w, dim);
                                if (!InsideBoundary(buf)) { ok = false; break; }
                                long code = Encode(buf, dim);
                                if (occupied.Contains(code) && !own.Contains(code)) { ok = false; break; }
                                turned[j] = code;
                            }
                            if (!ok) continue;
                            if (sweep && RotationSweep.Check(pre, v, w, new IntegerCenter(pivot),
                                                             obstacles, bmm) != MoveBlockReason.None)
                                continue;
                            PushChild(parent, state, p, turned,
                                      Move.Rotate(p, v, w, (int[])pivot.Clone()), g);
                        }
                }
            }

            void PushChild(int parent, long[][] state, int p, long[] replacement, Move move, int g)
            {
                var next = new long[state.Length][];
                for (int q = 0; q < state.Length; q++) next[q] = q == p ? replacement : state[q];
                Array.Sort(next[p]);
                if (!seen.Add(Key(next))) return;
                Push(next, parent, move, g);
            }

            void Push(long[][] state, int parent, Move move, int g)
            {
                if (parent < 0)
                {
                    var sorted = new long[state.Length][];
                    for (int p = 0; p < state.Length; p++)
                    {
                        sorted[p] = (long[])state[p].Clone();
                        Array.Sort(sorted[p]);
                    }
                    state = sorted;
                    seen.Add(Key(state));
                }
                int index = states.Count;
                states.Add(state); parents.Add(parent); arrivals.Add(move); gs.Add(g);
                open.Push(index, g + opt.HeuristicWeight * Heuristic(state));
            }

            /// <summary>
            /// Distance to the target assembly: per piece, how far its centroid still is from the
            /// slot's centroid plus how many of its cells are outside the slot. Zero exactly when
            /// every piece sits on its slot, so h == 0 IS the goal test — and since a slot is a
            /// winning arrangement by construction, no separate win check is needed inside the loop.
            /// Not admissible (a turn about a far pivot moves and reorients at once); that is
            /// deliberate — the search hunts for A solution, and the verifier decides.
            /// </summary>
            int Heuristic(long[][] state)
            {
                int h = 0;
                var buf = new int[dim];
                for (int p = 0; p < state.Length; p++)
                {
                    var sums = new long[dim];
                    int outside = 0;
                    foreach (var code in state[p])
                    {
                        Decode(code, buf, dim);
                        for (int k = 0; k < dim; k++) sums[k] += buf[k];
                        if (!targetSet[p].Contains(code)) outside++;
                    }
                    if (outside == 0) continue;
                    long drift = 0;
                    for (int k = 0; k < dim; k++) drift += Math.Abs(sums[k] - targetSum[p * dim + k]);
                    h += (int)(2 * drift / state[p].Length) + outside;
                }
                return h;
            }

            bool IsTarget(long[][] state)
            {
                for (int p = 0; p < state.Length; p++)
                {
                    if (state[p].Length != target[p].Length) return false;
                    foreach (var code in state[p]) if (!targetSet[p].Contains(code)) return false;
                }
                return true;
            }

            bool InsideBoundary(int[] cell)
            {
                if (bmm == null) return true;
                for (int k = 0; k < dim && k < bmm[0].Length; k++)
                {
                    if (cell[k] < bmm[0][k]) return false;
                    if (cell[k] + 1 > bmm[1][k]) return false;
                }
                return true;
            }

            List<Move> Reconstruct(int index)
            {
                var moves = new List<Move>();
                while (parents[index] >= 0)
                {
                    moves.Add(arrivals[index]);
                    index = parents[index];
                }
                moves.Reverse();
                return moves;
            }

            static ulong Key(long[][] state)
            {
                ulong h = 14695981039346656037UL;
                foreach (var piece in state)
                {
                    foreach (var code in piece)
                    {
                        h ^= (ulong)code;
                        h *= 1099511628211UL;
                    }
                    h ^= 0x9E3779B97F4A7C15UL;   // piece separator: keeps identity per piece
                    h *= 1099511628211UL;
                }
                return h;
            }
        }

        // ── candidate targets ─────────────────────────────────────────────────────────────────

        static List<long[][]> CandidateTargets(Objective obj, Assembly assembly, long[][] start,
                                               int dim, int maxTargets)
        {
            var result = new List<long[][]>();
            if (obj.mode == GoalMode.Absolute)
            {
                var direct = EncodeAssembly(assembly.cells, dim);
                if (Fits(direct, obj, dim)) result.Add(direct);
                return result;
            }

            // Shape mode: the compound may be built anywhere, so score every proper global rotation
            // of the assembly, each shifted as close to the pieces' current position as the envelope
            // allows, and keep the closest few.
            var scored = new List<(long score, long[][] target)>();
            var startCentroid = Centroid(start, dim);
            foreach (var rot in IntegerOps.Rotations(dim))
            {
                var rotated = new int[assembly.cells.Length][][];
                for (int p = 0; p < assembly.cells.Length; p++)
                    rotated[p] = IntegerOps.Map(assembly.cells[p], rot);
                var shifted = ShiftInto(rotated, obj, dim, startCentroid);
                if (shifted == null) continue;
                var target = EncodeAssembly(shifted, dim);
                long score = 0;
                for (int p = 0; p < target.Length; p++)
                {
                    var a = Centroid(new[] { start[p] }, dim);
                    var b = Centroid(new[] { target[p] }, dim);
                    for (int k = 0; k < dim; k++) score += Math.Abs(a[k] - b[k]);
                }
                scored.Add((score, target));
            }
            scored.Sort((x, y) => x.score.CompareTo(y.score));
            var keys = new HashSet<string>();
            foreach (var entry in scored)
            {
                if (result.Count >= maxTargets) break;
                var key = TargetKey(entry.target);
                if (keys.Add(key)) result.Add(entry.target);
            }
            return result;
        }

        /// <summary>
        /// Translates the whole assembly so its centroid lands as close to <paramref name="towards"/>
        /// as possible while staying inside the movement envelope; null if it does not fit at all.
        /// The axes are independent (both the envelope and the Manhattan objective separate).
        /// </summary>
        static int[][][] ShiftInto(int[][][] cells, Objective obj, int dim, long[] towards)
        {
            var bmm = obj.boundary_min_max;
            var all = new List<int[]>();
            foreach (var piece in cells) all.AddRange(piece);
            var lo = IntegerOps.ExtentMin(all.ToArray());
            var hi = IntegerOps.ExtentMax(all.ToArray());
            var centroid = Centroid(cells, dim);

            var delta = new int[dim];
            for (int k = 0; k < dim; k++)
            {
                int want = (int)Math.Round((towards[k] - centroid[k]) / 1000.0);
                int minDelta = bmm == null ? want : bmm[0][k] - lo[k];
                int maxDelta = bmm == null ? want : bmm[1][k] - 1 - hi[k];
                if (minDelta > maxDelta) return null;
                delta[k] = Math.Max(minDelta, Math.Min(maxDelta, want));
            }

            var res = new int[cells.Length][][];
            for (int p = 0; p < cells.Length; p++)
            {
                res[p] = new int[cells[p].Length][];
                for (int i = 0; i < cells[p].Length; i++)
                {
                    res[p][i] = new int[dim];
                    for (int k = 0; k < dim; k++) res[p][i][k] = cells[p][i][k] + delta[k];
                }
            }
            return res;
        }

        static bool Fits(long[][] target, Objective obj, int dim)
        {
            var bmm = obj.boundary_min_max;
            if (bmm == null) return true;
            var buf = new int[dim];
            foreach (var piece in target)
                foreach (var code in piece)
                {
                    Decode(code, buf, dim);
                    for (int k = 0; k < dim && k < bmm[0].Length; k++)
                        if (buf[k] < bmm[0][k] || buf[k] + 1 > bmm[1][k]) return false;
                }
            return true;
        }

        /// <summary>Centroid in thousandths of a cell, so it stays integral.</summary>
        static long[] Centroid(long[][] state, int dim)
        {
            var sums = new long[dim];
            long n = 0;
            var buf = new int[dim];
            foreach (var piece in state)
                foreach (var code in piece)
                {
                    Decode(code, buf, dim);
                    for (int k = 0; k < dim; k++) sums[k] += buf[k];
                    n++;
                }
            for (int k = 0; k < dim; k++) sums[k] = sums[k] * 1000 / Math.Max(1, n);
            return sums;
        }

        static long[] Centroid(int[][][] cells, int dim)
        {
            var sums = new long[dim];
            long n = 0;
            foreach (var piece in cells)
                foreach (var cell in piece)
                {
                    for (int k = 0; k < dim; k++) sums[k] += cell[k];
                    n++;
                }
            for (int k = 0; k < dim; k++) sums[k] = sums[k] * 1000 / Math.Max(1, n);
            return sums;
        }

        static string TargetKey(long[][] target)
        {
            var parts = new List<string>();
            foreach (var piece in target)
            {
                var copy = (long[])piece.Clone();
                Array.Sort(copy);
                parts.Add(string.Join(",", copy));
            }
            return string.Join("|", parts);
        }

        // ── cell coding ───────────────────────────────────────────────────────────────────────
        //
        // A cell origin becomes one long: dim 15-bit fields, biased so negative coordinates stay
        // positive. Level coordinates live in single digits, so the range is never in question — the
        // point is that a state is an array of longs (cheap to hash, compare and store by the
        // hundred thousand) instead of a jagged int[][].

        const int Bias = 4096;
        const int Bits = 15;

        static long Encode(int[] cell, int dim)
        {
            long code = 0;
            for (int k = 0; k < dim; k++) code = (code << Bits) | (uint)(cell[k] + Bias);
            return code;
        }
        static long[] Encode(int[][] cells, int dim)
        {
            var res = new long[cells.Length];
            for (int i = 0; i < cells.Length; i++) res[i] = Encode(cells[i], dim);
            Array.Sort(res);
            return res;
        }
        static long[][] EncodeAssembly(int[][][] cells, int dim)
        {
            var res = new long[cells.Length][];
            for (int p = 0; p < cells.Length; p++) res[p] = Encode(cells[p], dim);
            return res;
        }
        static void Decode(long code, int[] cell, int dim)
        {
            for (int k = dim - 1; k >= 0; k--)
            {
                cell[k] = (int)(code & ((1 << Bits) - 1)) - Bias;
                code >>= Bits;
            }
        }
        static int[][] DecodeAll(long[] codes, int dim)
        {
            var res = new int[codes.Length][];
            for (int i = 0; i < codes.Length; i++)
            {
                res[i] = new int[dim];
                Decode(codes[i], res[i], dim);
            }
            return res;
        }

        /// <summary>
        /// A 90° turn of one cell origin about the CENTRE of the pivot cell — the game's move, in
        /// the twice-coordinates of <see cref="IntegerOps.RotateAsCenters"/> (and the truncating
        /// division that goes with it), so the search cannot drift from the engine.
        /// </summary>
        static void RotateAboutCell(int[] cell, int[] pivot, int v, int w, int dim)
        {
            int cv = 2 * cell[v] + 1 - (2 * pivot[v] + 1);
            int cw = 2 * cell[w] + 1 - (2 * pivot[w] + 1);
            int nv = -cw, nw = cv;
            cell[v] = (nv + 2 * pivot[v] + 1 - 1) / 2;
            cell[w] = (nw + 2 * pivot[w] + 1 - 1) / 2;
        }

        // ── a tiny binary heap (no PriorityQueue: this assembly also compiles under Unity) ─────

        sealed class Heap
        {
            readonly List<int> items = new List<int>();
            readonly List<int> keys = new List<int>();
            public int Count => items.Count;

            public void Push(int item, int key)
            {
                items.Add(item); keys.Add(key);
                int i = items.Count - 1;
                while (i > 0)
                {
                    int parent = (i - 1) / 2;
                    if (keys[parent] <= keys[i]) break;
                    Swap(parent, i); i = parent;
                }
            }

            public int Pop()
            {
                int top = items[0];
                int last = items.Count - 1;
                items[0] = items[last]; keys[0] = keys[last];
                items.RemoveAt(last); keys.RemoveAt(last);
                int i = 0;
                while (true)
                {
                    int l = 2 * i + 1, r = l + 1, best = i;
                    if (l < items.Count && keys[l] < keys[best]) best = l;
                    if (r < items.Count && keys[r] < keys[best]) best = r;
                    if (best == i) break;
                    Swap(best, i); i = best;
                }
                return top;
            }

            void Swap(int a, int b)
            {
                (items[a], items[b]) = (items[b], items[a]);
                (keys[a], keys[b]) = (keys[b], keys[a]);
            }
        }
    }
}
