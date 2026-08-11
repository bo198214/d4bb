using System;
using System.Collections.Generic;
using System.Linq;
using D4BB.Comb;
using D4BB.Game;
using D4BB.Transforms;

namespace D4BB.Solver
{
    public sealed class VerificationResult
    {
        /// <summary>Every move was legal AND the level reports <see cref="GameStatus.Reached"/>.</summary>
        public bool Solved;
        /// <summary>Null when solved; otherwise why the replay failed, in one line.</summary>
        public string Failure;
        /// <summary>Index into the move list of the rejected move, or -1 if all moves were legal.</summary>
        public int FailedMoveIndex = -1;
        public int MovesApplied;
        public int PiecesRemaining;
        public GameStatus Status;
    }

    /// <summary>
    /// Replays a move sequence through the REAL rule engine — a live <see cref="GameLevel"/> built
    /// from the level's own <see cref="Objective"/> — and reports whether it wins the level.
    ///
    /// <para>This is the project's definition of "this level is solvable": every constraint the
    /// player faces is enforced by the very code the game runs — piece overlap, the movement
    /// envelope, the swept quarter turn (<see cref="RotationSweep"/>, unless the level sets
    /// <c>quantum_rotation</c>), and the win condition of the level's mode. Nothing about the rules
    /// is re-modelled here, so a verified sequence cannot be a solver artefact: it is a replay of
    /// moves a player could make.</para>
    ///
    /// <para>Consequently <see cref="PathSearch"/>'s output is ALWAYS run through this verifier
    /// before it is reported or written to a solution file — a bug in the search can waste time,
    /// but it cannot produce a false "solved".</para>
    /// </summary>
    public static class SolutionVerifier
    {
        /// <summary>
        /// Replays <paramref name="moves"/> on a fresh level built from <paramref name="obj"/>.
        /// In shape mode a trailing <see cref="GameLevel.CombineSelected"/> is applied
        /// automatically when more than one piece is left (combining is forced bookkeeping, not a
        /// puzzle decision — a solution file need not spell it out; an explicit <c>Nc</c> move is
        /// still allowed for sequences that must combine mid-way).
        /// </summary>
        public static VerificationResult Verify(Objective obj, IReadOnlyList<Move> moves)
        {
            var res = new VerificationResult();
            var level = new GameLevel(obj);

            for (int i = 0; i < moves.Count; i++)
            {
                var mv = moves[i];
                int idx = IndexOfOriginalPiece(level, mv.Piece);
                if (idx < 0)
                {
                    res.FailedMoveIndex = i;
                    res.Failure = $"move {i + 1} '{mv}': piece {mv.Piece + 1} does not exist " +
                                  "(absorbed by an earlier combine, or the level has fewer pieces)";
                    break;
                }
                level.SelectPiece(idx);

                if (mv.Kind == MoveKind.Translate)
                {
                    // IntegerSignedAxis' "human" encoding is 1-based and signed: +1 = +x, -1 = -x, …
                    var axis = new IntegerSignedAxis(mv.Sign * (mv.V + 1));
                    if (!level.TranslateSelected(axis))
                    {
                        res.FailedMoveIndex = i;
                        res.Failure = $"move {i + 1} '{mv}': blocked ({level.LastBlockReason})";
                        break;
                    }
                }
                else if (mv.Kind == MoveKind.Rotate)
                {
                    var pivot = mv.Pivot ?? DefaultPivot(level.pieces[idx]);
                    if (!IsCellOfPiece(level.pieces[idx], pivot))
                    {
                        res.FailedMoveIndex = i;
                        res.Failure = $"move {i + 1} '{mv}': pivot [{string.Join(",", pivot)}] is not " +
                                      "a current cell of that piece";
                        break;
                    }
                    if (!level.RotateSelected(mv.V, mv.W, pivot))
                    {
                        res.FailedMoveIndex = i;
                        res.Failure = $"move {i + 1} '{mv}': blocked ({level.LastBlockReason})";
                        break;
                    }
                }
                else // Combine
                {
                    int before = level.pieces.Count;
                    level.CombineSelected();
                    if (level.pieces.Count == before)
                    {
                        res.FailedMoveIndex = i;
                        res.Failure = $"move {i + 1} '{mv}': nothing to combine — no piece is " +
                                      "face-adjacent to that one";
                        break;
                    }
                }
                res.MovesApplied++;
            }

            if (res.FailedMoveIndex < 0
                && obj.mode == GoalMode.Shape && level.pieces.Count > 1)
            {
                // One CombineSelected already cascades transitively over the whole connected set.
                level.SelectPiece(0);
                level.CombineSelected();
            }

            res.PiecesRemaining = level.pieces.Count;
            res.Status = level.status;
            res.Solved = res.FailedMoveIndex < 0 && level.status == GameStatus.Reached;
            if (!res.Solved && res.Failure == null)
                res.Failure = obj.mode == GoalMode.Shape && level.pieces.Count > 1
                    ? $"all moves legal, but the pieces are not one compound at the end " +
                      $"({level.pieces.Count} pieces remain — they are not all face-adjacent), " +
                      $"status {level.status}"
                    : $"all moves legal, but the level is not solved (status {level.status})";
            return res;
        }

        /// <summary>
        /// The list index of the piece a move's 1-based number refers to, or -1. Identity survives
        /// combines through <see cref="Piece.colorSlot"/> (set to the piece's file index at level
        /// construction, and kept at the SMALLEST slot of a merged set).
        /// </summary>
        static int IndexOfOriginalPiece(GameLevel level, int originalIndex)
            => level.pieces.FindIndex(p => p.colorSlot == originalIndex);

        /// <summary>
        /// The RULES.md default rotation pivot: the lexicographically smallest current cell of the
        /// piece. Deliberately not GameLevel's own null-pivot default (the centroid) — see
        /// <see cref="Move"/>.
        /// </summary>
        public static int[] DefaultPivot(Piece piece)
        {
            int[] best = null;
            foreach (var o in piece.origins)
                if (best == null || Lexicographic(o, best) < 0) best = o;
            return IntegerOps.Clone(best);
        }

        public static int Lexicographic(int[] a, int[] b)
        {
            for (int k = 0; k < a.Length && k < b.Length; k++)
                if (a[k] != b[k]) return a[k] < b[k] ? -1 : +1;
            return 0;
        }

        static bool IsCellOfPiece(Piece piece, int[] cell)
            => piece.origins.Any(o => IntegerOps.VecEqual(o, cell));
    }
}
