using System.Collections.Generic;
using D4BB.Game;

namespace D4BB.Solver
{
    /// <summary>
    /// Trims the detours a greedy search leaves behind (a piece nudged aside and put back, a turn
    /// undone two moves later) before a solution is written to a file — those files are read by
    /// people, and a shorter sequence is a better description of what the level actually asks for.
    ///
    /// <para>Correctness is not argued, it is re-established: a move is dropped only if the
    /// SHORTENED sequence still replays to a win through <see cref="SolutionVerifier"/>. So the
    /// worst a bug here can do is leave a longer sequence in place.</para>
    ///
    /// <para>Not an optimiser — it never reorders or replaces moves, so the result is not a shortest
    /// solution, just one without removable steps.</para>
    /// </summary>
    public static class SolutionPolish
    {
        public static List<Move> Shorten(Objective obj, IReadOnlyList<Move> moves, int maxPasses = 3)
        {
            var current = new List<Move>(moves);
            for (int pass = 0; pass < maxPasses; pass++)
            {
                bool changed = false;
                // Backwards: dropping a late move cannot invalidate the indices of earlier ones.
                for (int i = current.Count - 1; i >= 0; i--)
                {
                    var candidate = new List<Move>(current);
                    candidate.RemoveAt(i);
                    if (!SolutionVerifier.Verify(obj, candidate).Solved) continue;
                    current = candidate;
                    changed = true;
                }
                if (!changed) break;
            }
            return current;
        }
    }
}
