using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using D4BB.Game;

namespace D4BB.Solver
{
    /// <summary>
    /// A level's optional solution file: <c>&lt;level&gt;.moves</c>, right next to
    /// <c>&lt;level&gt;.json</c>. Plain text, '#' starts a comment, moves are whitespace- or
    /// comma-separated in the notation of <c>tools/puzzle/RULES.md</c> (see <see cref="Move"/>).
    ///
    /// <para>The file is a checked-in PROOF, not a hint for players: when one exists, the level
    /// validator replays it instead of searching, which is both far faster and far stronger than
    /// anything the search can conclude. Levels the search cannot crack — the interlocking ones,
    /// where getting the pieces there is the puzzle — get their file written by hand (or by an
    /// agent solving the level); everything else gets its file written by the solver itself.</para>
    ///
    /// <para>Deliberately not <c>.json</c>: a stray JSON file in the levels folder invites being
    /// read as a level.</para>
    /// </summary>
    public static class SolutionFile
    {
        public const string Extension = ".moves";

        public static string PathFor(string levelPath)
            => Path.ChangeExtension(levelPath, Extension);

        public static bool Exists(string levelPath)
            => File.Exists(PathFor(levelPath));

        /// <summary>Parses the solution file belonging to a level; throws <see cref="FormatException"/>.</summary>
        public static List<Move> Read(string levelPath)
            => Move.ParseSequence(File.ReadAllText(PathFor(levelPath)));

        /// <summary>
        /// Writes (overwrites) the solution file. <paramref name="provenance"/> names who found the
        /// sequence — it is the one thing a reader cannot reconstruct from the moves themselves.
        /// No timestamp: a regenerated file should be diff-free when the moves are unchanged.
        /// </summary>
        public static void Write(string levelPath, Objective obj, IReadOnlyList<Move> moves,
                                 string provenance)
        {
            var sb = new StringBuilder();
            sb.Append("# Tesserian solution for \"").Append(obj.name).Append("\" (")
              .Append(Path.GetFileName(levelPath)).AppendLine(")");
            sb.Append("# ").AppendLine(provenance);
            sb.AppendLine("# Verified by replay through D4BB.Game.GameLevel: status Reached.");
            sb.AppendLine("# Notation: tools/puzzle/RULES.md — <piece><t|r><+|-><axes>[@pivot];");
            sb.AppendLine("# piece numbers are 1-based file order and survive combines.");
            sb.Append("# ").Append(moves.Count).Append(" moves, ")
              .Append(obj.mode == GoalMode.Absolute ? "absolute" : "shape").AppendLine(" mode.");

            const int perLine = 8;
            for (int i = 0; i < moves.Count; i++)
            {
                sb.Append(moves[i].ToString());
                bool last = i == moves.Count - 1;
                sb.Append(last || (i + 1) % perLine == 0 ? Environment.NewLine : " ");
            }
            File.WriteAllText(PathFor(levelPath), sb.ToString());
        }
    }
}
