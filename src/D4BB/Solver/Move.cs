using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace D4BB.Solver
{
    public enum MoveKind { Translate, Rotate, Combine }

    /// <summary>
    /// One player move, in the notation of <c>tools/puzzle/RULES.md</c> (the same grammar
    /// <c>p.py confirm</c> reads, so a sequence produced by either tool is understood by both):
    /// <c>&lt;piece&gt;&lt;op&gt;&lt;sign&gt;&lt;axes&gt;[@x,y,z,w]</c>, e.g. <c>1t+x</c>,
    /// <c>2r-zw</c>, <c>3r+yz@0,3,0,0</c>, plus <c>1c</c> for a combine.
    ///
    /// <para><b>Piece numbers are 1-based ORIGINAL file order</b> and stay valid across combines:
    /// they are resolved through <see cref="D4BB.Transforms.Piece.colorSlot"/>, which
    /// <see cref="D4BB.Transforms.Piece.Combine"/> keeps at the smallest slot of the merged set.
    /// So after "1c" absorbed piece 3, the merged piece is still addressed as "1"; addressing "3"
    /// is then an error, not a silent no-op.</para>
    ///
    /// <para><b>Rotation sense:</b> <c>+vw</c> sends axis +v → +w (<see cref="D4BB.Comb.IntegerOps.Rotate(int[],int,int)"/>
    /// with that argument order); <c>-vw</c> is its inverse, i.e. the (w,v) rotation. The sign is
    /// folded into the stored axis ORDER, so applying a move is always
    /// <c>RotateSelected(V, W, Pivot)</c>; <see cref="ToString"/> re-derives the sign from it and
    /// therefore normalises <c>r+wz</c> to the equivalent <c>r-zw</c>.</para>
    ///
    /// <para><b>Default pivot</b> (no <c>@</c>): the lexicographically smallest CURRENT cell of the
    /// piece — the RULES.md/p.py convention, deliberately NOT <see cref="D4BB.Game.GameLevel"/>'s own
    /// null-pivot default (the piece centroid), so that a sequence authored against p.py replays
    /// identically here. <see cref="SolutionVerifier"/> always passes an explicit pivot origin.</para>
    /// </summary>
    public readonly struct Move
    {
        public readonly MoveKind Kind;
        /// <summary>0-based original piece index (the notation's 1-based number minus one).</summary>
        public readonly int Piece;
        /// <summary>Translate: the axis. Rotate: the first plane axis (v).</summary>
        public readonly int V;
        /// <summary>Rotate: the second plane axis (w). Unused for translate/combine.</summary>
        public readonly int W;
        /// <summary>Translate: +1 / -1. Unused for rotate (the sense lives in the V/W order).</summary>
        public readonly int Sign;
        /// <summary>Rotate: explicit pivot cell origin, or null for the default (smallest cell).</summary>
        public readonly int[] Pivot;

        Move(MoveKind kind, int piece, int v, int w, int sign, int[] pivot)
        {
            Kind = kind; Piece = piece; V = v; W = w; Sign = sign; Pivot = pivot;
        }

        public static Move Translate(int piece, int axis, int sign)
            => new Move(MoveKind.Translate, piece, axis, -1, sign, null);
        public static Move Rotate(int piece, int v, int w, int[] pivot)
            => new Move(MoveKind.Rotate, piece, v, w, +1, pivot);
        public static Move Combine(int piece)
            => new Move(MoveKind.Combine, piece, -1, -1, +1, null);

        public const string AxisNames = "xyzw";

        // A pivot is 3 or 4 comma-separated coordinates. The upper bound is what keeps a
        // comma-separated move LIST unambiguous: without it, "1r+xy@0,0,0,0,2t+x" would swallow the
        // next move's piece number into the pivot.
        const string Body =
            @"(?<n>\d+)(?:(?<op>[rt])(?<sign>[+-])(?<axes>[xyzw]{1,2})(?:@(?<pivot>-?\d+(?:,-?\d+){2,3}))?|(?<combine>c))";

        static readonly Regex Syntax = new Regex("^" + Body + "$", RegexOptions.Compiled);

        // Anchored at the scan position: separators, then exactly one move.
        static readonly Regex Token = new Regex(@"\G[\s,;]*(?<move>" + Body + ")", RegexOptions.Compiled);

        /// <summary>Parses one token; throws <see cref="FormatException"/> with the offending text.</summary>
        public static Move Parse(string token)
        {
            var m = Syntax.Match(token);
            if (!m.Success)
                throw new FormatException($"bad move syntax: '{token}' " +
                    "(expected <piece><t|r><+|-><axes>[@x,y,z,w] or <piece>c)");
            int piece = int.Parse(m.Groups["n"].Value) - 1;
            if (piece < 0)
                throw new FormatException($"'{token}': piece numbers are 1-based");
            if (m.Groups["combine"].Success) return Combine(piece);

            string axes = m.Groups["axes"].Value;
            bool plus = m.Groups["sign"].Value == "+";
            if (m.Groups["op"].Value == "t")
            {
                if (axes.Length != 1)
                    throw new FormatException($"'{token}': a translate names exactly one axis");
                if (m.Groups["pivot"].Success)
                    throw new FormatException($"'{token}': a pivot is meaningless for a translate");
                return Translate(piece, AxisNames.IndexOf(axes[0]), plus ? +1 : -1);
            }
            if (axes.Length != 2 || axes[0] == axes[1])
                throw new FormatException($"'{token}': a rotate names exactly two distinct axes (its plane)");
            int v = AxisNames.IndexOf(axes[0]), w = AxisNames.IndexOf(axes[1]);
            int[] pivot = null;
            if (m.Groups["pivot"].Success)
            {
                var parts = m.Groups["pivot"].Value.Split(',');
                pivot = new int[parts.Length];
                for (int i = 0; i < parts.Length; i++) pivot[i] = int.Parse(parts[i]);
            }
            // '-' = the inverse rotation = the same plane with the axes swapped.
            return plus ? Rotate(piece, v, w, pivot) : Rotate(piece, w, v, pivot);
        }

        /// <summary>
        /// Parses a whitespace- and/or comma-separated move list; '#' starts a comment.
        ///
        /// <para>Tokenised rather than split, because the two roles of ',' collide: it separates
        /// moves AND the coordinates inside a pivot. Splitting on it tore every <c>@x,y,z,w</c>
        /// into four unparsable fragments — which is how a whole sweep's worth of generated
        /// solution files became unreadable on the next run.</para>
        /// </summary>
        public static List<Move> ParseSequence(string text)
        {
            var moves = new List<Move>();
            foreach (var rawLine in text.Split('\n'))
            {
                var line = rawLine;
                int hash = line.IndexOf('#');
                if (hash >= 0) line = line.Substring(0, hash);

                int pos = 0;
                while (pos < line.Length)
                {
                    var m = Token.Match(line, pos);
                    if (!m.Success)
                    {
                        var rest = line.Substring(pos).Trim();
                        if (rest.Length == 0) break;   // trailing separators / whitespace
                        throw new FormatException($"bad move syntax: '{rest}' " +
                            "(expected <piece><t|r><+|-><axes>[@x,y,z,w] or <piece>c)");
                    }
                    moves.Add(Parse(m.Groups["move"].Value));
                    pos = m.Index + m.Length;
                }
            }
            return moves;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(Piece + 1);
            if (Kind == MoveKind.Combine) { sb.Append('c'); return sb.ToString(); }
            if (Kind == MoveKind.Translate)
            {
                sb.Append('t').Append(Sign > 0 ? '+' : '-').Append(AxisNames[V]);
                return sb.ToString();
            }
            // Rotate: the sense is the axis order — emit the plane with axes ascending and let the
            // sign carry the direction, so the text form is canonical (r+wz prints as r-zw).
            bool ascending = V < W;
            sb.Append('r').Append(ascending ? '+' : '-')
              .Append(AxisNames[ascending ? V : W]).Append(AxisNames[ascending ? W : V]);
            if (Pivot != null)
            {
                sb.Append('@');
                for (int i = 0; i < Pivot.Length; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(Pivot[i]);
                }
            }
            return sb.ToString();
        }
    }
}
