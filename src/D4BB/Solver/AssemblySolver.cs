using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using D4BB.Comb;

namespace D4BB.Solver
{
    public enum AssemblyVerdict
    {
        /// <summary>At least one tiling of the goal by the pieces was found.</summary>
        Exists,
        /// <summary>The search ran to completion and found none — the level is definitively unsolvable.</summary>
        None,
        /// <summary>The search budget ran out first. Says nothing either way.</summary>
        Unknown,
    }

    /// <summary>One tiling: <c>cells[i]</c> are the goal cells piece <c>i</c> occupies.</summary>
    public sealed class Assembly
    {
        public int[][][] cells;
    }

    public sealed class AssemblyResult
    {
        public AssemblyVerdict Verdict;
        public string Reason;
        public List<Assembly> Assemblies = new List<Assembly>();
        public long Nodes;
        public TimeSpan Elapsed;
    }

    /// <summary>
    /// Stage 1 of the solvability check: can the pieces TILE the goal at all — ignoring how they
    /// would get there? An exact-cover search over placements (each piece in each of its proper
    /// lattice orientations, at each translation that fits inside the goal).
    ///
    /// <para>Two verdicts of very different strength come out of this, and the difference is the
    /// whole point of running it: <see cref="AssemblyVerdict.None"/> is a PROOF of unsolvability
    /// (no arrangement of the pieces is the goal, so no sequence of moves can win), while
    /// <see cref="AssemblyVerdict.Exists"/> is only a necessary condition — the pieces still have to
    /// be manoeuvrable into that arrangement, which is <see cref="PathSearch"/>'s job.</para>
    ///
    /// <para>Orientations come from <see cref="IntegerOps.Rotations"/> — PROPER rotations only, the
    /// same group <see cref="IntegerOps.MotionEqual"/> uses for the win check. A mirrored tiling is
    /// correctly NOT accepted: the game has no reflection move.</para>
    ///
    /// <para>Shape mode vs absolute mode need no distinction here. Tiling the goal where it lies is
    /// equivalent to tiling any congruent copy of it (apply the global motion to every piece), so
    /// covering the goal cells in place answers both.</para>
    /// </summary>
    public sealed class AssemblySolver
    {
        readonly int dim;
        readonly int[][] goal;
        readonly int[][][] pieces;

        // Goal cells, indexed 0..nCells-1, plus a dense bounding-box lookup for O(1) "is this
        // coordinate a goal cell, and which index".
        readonly int nCells, nWords;
        readonly int[] lo, ext;
        readonly int[] flatToCell;
        readonly int[][] neighbours;    // goal cell -> face-adjacent goal cells

        // Placements per piece.
        readonly List<ulong[]>[] masks;
        readonly List<int[][]>[] placementCells;
        readonly List<int>[][] byCell;  // [piece][goal cell] -> indices into masks[piece]
        readonly string[] shapeKey;     // congruence class per piece (for symmetry breaking)
        readonly int[] pieceSize;
        readonly bool piecesAreConnected;   // gate for RegionsFeasible — see its remarks

        // Search state.
        ulong[] covered;
        bool[] used;
        int[] chosen;
        int uncoveredCount;
        List<Assembly> found;
        int maxAssemblies;
        long nodes;
        bool stop, timedOut;
        Stopwatch watch;
        TimeSpan budget;

        public AssemblySolver(int[][] goal, int[][][] pieces)
        {
            this.goal = goal;
            this.pieces = pieces;
            dim = goal[0].Length;
            nCells = goal.Length;
            nWords = (nCells + 63) / 64;

            lo = IntegerOps.ExtentMin(goal);
            var hi = IntegerOps.ExtentMax(goal);
            ext = new int[dim];
            int flatSize = 1;
            for (int k = 0; k < dim; k++) { ext[k] = hi[k] - lo[k] + 1; flatSize *= ext[k]; }
            flatToCell = new int[flatSize];
            for (int i = 0; i < flatSize; i++) flatToCell[i] = -1;
            for (int i = 0; i < nCells; i++) flatToCell[Flat(goal[i])] = i;

            neighbours = BuildNeighbours();

            masks = new List<ulong[]>[pieces.Length];
            placementCells = new List<int[][]>[pieces.Length];
            byCell = new List<int>[pieces.Length][];
            shapeKey = new string[pieces.Length];
            pieceSize = new int[pieces.Length];
            var rotations = IntegerOps.Rotations(dim);
            piecesAreConnected = true;
            for (int p = 0; p < pieces.Length; p++)
            {
                pieceSize[p] = pieces[p].Length;
                if (!IsFaceConnected(pieces[p])) piecesAreConnected = false;
                EnumeratePlacements(p, rotations);
            }
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

        /// <summary>Number of distinct legal placements of piece <paramref name="p"/> inside the goal.</summary>
        public int PlacementCount(int p) => masks[p].Count;

        public AssemblyResult Solve(int maxAssemblies = 1, TimeSpan? budget = null)
        {
            var result = new AssemblyResult();
            this.maxAssemblies = maxAssemblies;
            this.budget = budget ?? TimeSpan.FromSeconds(10);
            watch = Stopwatch.StartNew();

            for (int p = 0; p < pieces.Length; p++)
                if (masks[p].Count == 0)
                {
                    result.Verdict = AssemblyVerdict.None;
                    result.Reason = $"piece {p + 1} does not fit inside the goal in any orientation";
                    result.Elapsed = watch.Elapsed;
                    return result;
                }

            covered = new ulong[nWords];
            used = new bool[pieces.Length];
            chosen = new int[pieces.Length];
            uncoveredCount = nCells;
            found = new List<Assembly>();
            nodes = 0; stop = false; timedOut = false;

            Search();

            result.Nodes = nodes;
            result.Elapsed = watch.Elapsed;
            result.Assemblies = found;
            result.Verdict = found.Count > 0 ? AssemblyVerdict.Exists
                           : timedOut ? AssemblyVerdict.Unknown
                           : AssemblyVerdict.None;
            if (result.Verdict == AssemblyVerdict.None)
                result.Reason = "the pieces cannot tile the goal in any orientation " +
                                $"(exhaustive: {nodes} nodes)";
            else if (result.Verdict == AssemblyVerdict.Unknown)
                result.Reason = $"exact-cover search hit its {this.budget.TotalSeconds:0.#}s budget " +
                                $"after {nodes} nodes";
            return result;
        }

        // ── search ────────────────────────────────────────────────────────────────────────────

        void Search()
        {
            if ((++nodes & 0x3FF) == 0 && watch.Elapsed > budget) { timedOut = true; stop = true; return; }

            if (uncoveredCount == 0)
            {
                // A full cover uses every piece exactly when the sizes add up — which the caller
                // has checked, but a standalone user may not have, and a half-used "assembly" would
                // silently carry stale placements.
                for (int p = 0; p < pieces.Length; p++) if (!used[p]) return;
                var a = new Assembly { cells = new int[pieces.Length][][] };
                for (int p = 0; p < pieces.Length; p++) a.cells[p] = placementCells[p][chosen[p]];
                found.Add(a);
                if (found.Count >= maxAssemblies) stop = true;
                return;
            }
            if (piecesAreConnected && !RegionsFeasible()) return;

            // Fill the lowest uncovered cell. Every tiling must cover it, so branching only over
            // placements that do cover it is complete AND kills all permutation duplicates.
            int c = LowestUncovered();
            for (int p = 0; p < pieces.Length; p++)
            {
                if (used[p] || IsRedundantDuplicate(p)) continue;
                var list = byCell[p][c];
                for (int j = 0; j < list.Count; j++)
                {
                    var mask = masks[p][list[j]];
                    if (Intersects(mask)) continue;
                    Apply(mask); used[p] = true; chosen[p] = list[j];
                    Search();
                    used[p] = false; Remove(mask);
                    if (stop) return;
                }
            }
        }

        /// <summary>
        /// Congruent pieces are interchangeable for tiling purposes, so among a set of unused
        /// congruent pieces only the first index is ever tried — otherwise a level with k identical
        /// pieces multiplies its search by k!.
        /// </summary>
        bool IsRedundantDuplicate(int p)
        {
            for (int q = 0; q < p; q++)
                if (!used[q] && shapeKey[q] == shapeKey[p]) return true;
            return false;
        }

        /// <summary>
        /// Prune: every face-connected component of the still-uncovered region must be fillable by
        /// SOME subset of the unused pieces, so its size must be a subset sum of their sizes. Cheap
        /// (one flood fill + a subset-sum bitset) and it cuts the classic polycube dead ends —
        /// a 3-cell pocket next to only 4-cell pieces, say — before they are explored.
        ///
        /// <para><b>Only sound while every piece is face-connected</b> — hence
        /// <see cref="piecesAreConnected"/>. A piece in this game need NOT be connected (there are
        /// levels built on exactly that, e.g. "2 congruent pieces, consisting of 4 separate
        /// parts"), and a disconnected piece can fill parts of two components at once, so a
        /// component's size is then no longer a sum of whole piece sizes. Applying it regardless
        /// declared 114 perfectly solvable levels "unsolvable" — with a straight face, since a
        /// completed exact cover reports its emptiness as a proof.</para>
        /// </summary>
        bool RegionsFeasible()
        {
            ulong[] sums = new ulong[nWords + 1];
            SetBit(sums, 0);
            for (int p = 0; p < pieces.Length; p++)
            {
                if (used[p]) continue;
                ShiftOrInto(sums, pieceSize[p]);
            }

            var seen = new bool[nCells];
            var stack = new Stack<int>();
            for (int start = 0; start < nCells; start++)
            {
                if (seen[start] || GetBit(covered, start)) continue;
                int size = 0;
                stack.Push(start); seen[start] = true;
                while (stack.Count > 0)
                {
                    int c = stack.Pop();
                    size++;
                    foreach (int n in neighbours[c])
                        if (!seen[n] && !GetBit(covered, n)) { seen[n] = true; stack.Push(n); }
                }
                if (!GetBit(sums, size)) return false;
            }
            return true;
        }

        // ── placements ────────────────────────────────────────────────────────────────────────

        void EnumeratePlacements(int p, int[][][] rotations)
        {
            masks[p] = new List<ulong[]>();
            placementCells[p] = new List<int[][]>();
            byCell[p] = new List<int>[nCells];
            for (int c = 0; c < nCells; c++) byCell[p][c] = new List<int>();

            var shapes = new HashSet<string>();
            string canonical = null;
            var cellBuf = new int[dim];

            foreach (var rot in rotations)
            {
                // Rotating cell ORIGINS as points offsets the resulting min corners by one constant
                // vector shared by all cells of the piece (the same convention IntegerOps.MotionEqual
                // uses); normalising the min corner to zero absorbs it, and the translation loop
                // below re-introduces every position anyway.
                var rotated = Normalise(IntegerOps.Map(pieces[p], rot));
                string key = ShapeKey(rotated);
                if (canonical == null || string.CompareOrdinal(key, canonical) < 0) canonical = key;
                if (!shapes.Add(key)) continue;   // orientation symmetry: same shape as an earlier rotation

                var pext = new int[dim];
                for (int k = 0; k < dim; k++)
                {
                    int m = 0;
                    foreach (var cell in rotated) if (cell[k] > m) m = cell[k];
                    pext[k] = m + 1;
                }

                // All translations whose bounding box fits inside the goal's bounding box.
                var t = new int[dim];
                for (int k = 0; k < dim; k++) t[k] = lo[k];
                while (true)
                {
                    var mask = new ulong[nWords];
                    var cells = new int[rotated.Length][];
                    bool fits = true;
                    for (int i = 0; i < rotated.Length && fits; i++)
                    {
                        for (int k = 0; k < dim; k++) cellBuf[k] = rotated[i][k] + t[k];
                        int flat = Flat(cellBuf);
                        int c = flat < 0 ? -1 : flatToCell[flat];
                        if (c < 0) { fits = false; break; }
                        SetBit(mask, c);
                        cells[i] = (int[])cellBuf.Clone();
                    }
                    if (fits)
                    {
                        int index = masks[p].Count;
                        masks[p].Add(mask);
                        placementCells[p].Add(cells);
                        for (int c = 0; c < nCells; c++)
                            if (GetBit(mask, c)) byCell[p][c].Add(index);
                    }

                    // odometer over the translation box
                    int axis = 0;
                    while (axis < dim)
                    {
                        t[axis]++;
                        if (t[axis] <= lo[axis] + ext[axis] - pext[axis]) break;
                        t[axis] = lo[axis];
                        axis++;
                    }
                    if (axis == dim) break;
                }
            }
            shapeKey[p] = canonical;
        }

        static int[][] Normalise(int[][] cells)
        {
            var mn = IntegerOps.ExtentMin(cells);
            var res = new int[cells.Length][];
            for (int i = 0; i < cells.Length; i++)
            {
                res[i] = new int[cells[i].Length];
                for (int k = 0; k < cells[i].Length; k++) res[i][k] = cells[i][k] - mn[k];
            }
            return res;
        }

        static string ShapeKey(int[][] cells)
        {
            var keys = new List<string>(cells.Length);
            foreach (var c in cells) keys.Add(string.Join(",", c));
            keys.Sort(StringComparer.Ordinal);
            return string.Join(";", keys);
        }

        int[][] BuildNeighbours()
        {
            var res = new int[nCells][];
            var buf = new List<int>();
            var probe = new int[dim];
            for (int i = 0; i < nCells; i++)
            {
                buf.Clear();
                for (int k = 0; k < dim; k++)
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Array.Copy(goal[i], probe, dim);
                        probe[k] += s;
                        int flat = Flat(probe);
                        int c = flat < 0 ? -1 : flatToCell[flat];
                        if (c >= 0) buf.Add(c);
                    }
                res[i] = buf.ToArray();
            }
            return res;
        }

        int Flat(int[] cell)
        {
            int f = 0;
            for (int k = 0; k < dim; k++)
            {
                int c = cell[k] - lo[k];
                if (c < 0 || c >= ext[k]) return -1;
                f = f * ext[k] + c;
            }
            return f;
        }

        // ── bitset helpers ────────────────────────────────────────────────────────────────────

        static void SetBit(ulong[] w, int i) => w[i >> 6] |= 1UL << (i & 63);
        static bool GetBit(ulong[] w, int i) => i >> 6 < w.Length && (w[i >> 6] & (1UL << (i & 63))) != 0;

        bool Intersects(ulong[] mask)
        {
            for (int i = 0; i < nWords; i++) if ((covered[i] & mask[i]) != 0) return true;
            return false;
        }
        void Apply(ulong[] mask)
        {
            for (int i = 0; i < nWords; i++) covered[i] |= mask[i];
            uncoveredCount -= PopCount(mask);
        }
        void Remove(ulong[] mask)
        {
            for (int i = 0; i < nWords; i++) covered[i] &= ~mask[i];
            uncoveredCount += PopCount(mask);
        }
        static int PopCount(ulong[] w)
        {
            int n = 0;
            foreach (var x in w)
            {
                ulong v = x;
                while (v != 0) { v &= v - 1; n++; }
            }
            return n;
        }
        int LowestUncovered()
        {
            for (int i = 0; i < nCells; i++) if (!GetBit(covered, i)) return i;
            return -1;
        }
        /// <summary>sums |= sums &lt;&lt; shift — the subset-sum DP step.</summary>
        static void ShiftOrInto(ulong[] sums, int shift)
        {
            int wordShift = shift >> 6, bitShift = shift & 63;
            for (int i = sums.Length - 1; i >= 0; i--)
            {
                ulong v = 0;
                int src = i - wordShift;
                if (src >= 0)
                {
                    v = sums[src] << bitShift;
                    if (bitShift != 0 && src > 0) v |= sums[src - 1] >> (64 - bitShift);
                }
                sums[i] |= v;
            }
        }
    }
}
