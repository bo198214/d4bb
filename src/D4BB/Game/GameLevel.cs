using System;
using System.Collections.Generic;
using System.Linq;
using D4BB.Comb;
using D4BB.Transforms;

namespace D4BB.Game
{
    public enum MoveBlockReason { None, Overlap, OutOfBoundary }

    public class GameLevel
    {
        public List<Piece> pieces = new();
        public int selectedIndex = 0;
        public int[][] goal;
        public GameStatus status = GameStatus.None;
        public Objective Objective { get; private set; }
        public MoveBlockReason LastBlockReason { get; private set; } = MoveBlockReason.None;

        public int[][][] PieceOrigins => pieces.Select(c => c.origins).ToArray();

        public event Action OnChanged;
        public event Action<int, IntegerSignedAxis> OnTranslate;
        public event Action<int, int, int, int[]> OnRotate;
        public event Action<int> OnCombine;
        public event Action OnReset;

        public GameLevel(Objective obj)
        {
            Objective = obj;
            goal = IntegerOps.Clone(obj.goal);
            for (int i = 0; i < obj.pieces.Length; i++)
                pieces.Add(new Piece(obj.pieces[i]) { colorSlot = i });
            PropagateStatus();
        }

        public Piece Selected => selectedIndex >= 0 && selectedIndex < pieces.Count
            ? pieces[selectedIndex] : null;

        public void SelectPiece(int index)
        {
            if (index < 0 || index >= pieces.Count) return;
            selectedIndex = index;
        }

        public bool TranslateSelected(IntegerSignedAxis axis)
        {
            var c = Selected;
            if (c == null) return false;
            int idx = selectedIndex;
            c.Translate(axis);
            if (!IsInsideBoundary(c.origins))
            {
                c.Translate(new IntegerSignedAxis(-axis.Human()));
                LastBlockReason = MoveBlockReason.OutOfBoundary;
                return false;
            }
            if (IsOverlapping())
            {
                c.Translate(new IntegerSignedAxis(-axis.Human()));
                LastBlockReason = MoveBlockReason.Overlap;
                return false;
            }
            LastBlockReason = MoveBlockReason.None;
            PropagateStatus();
            OnTranslate?.Invoke(idx, axis);
            OnChanged?.Invoke();
            return true;
        }

        public bool RotateSelected(int v, int w, int[] pivotOrigin = null)
        {
            var c = Selected;
            if (c == null) return false;
            int idx = selectedIndex;

            var pivot = pivotOrigin != null
                ? new IntegerCenter(pivotOrigin)
                : new IntegerCenter(c.origins, asCubes: true);

            // 1. Apply rotation (origins + topology) via the unified Piece move.
            c.Rotate(v, w, pivot);

            // 2. Boundary check (revert with the inverse (w,v) rotation if blocked)
            if (!IsInsideBoundary(c.origins))
            {
                c.Rotate(w, v, pivot);
                LastBlockReason = MoveBlockReason.OutOfBoundary;
                return false;
            }

            // 3. Check for collisions
            if (IsOverlapping())
            {
                c.Rotate(w, v, pivot);
                LastBlockReason = MoveBlockReason.Overlap;
                return false;
            }

            // 4. Success: Commit and notify
            LastBlockReason = MoveBlockReason.None;
            PropagateStatus();
            OnRotate?.Invoke(idx, v, w, pivotOrigin);
            OnChanged?.Invoke();
            return true;
        }

        public void CombineSelected()
        {
            var c0 = Selected;
            if (c0 == null || pieces.Count == 1) return;
            var bordering = FindAdjacent(c0);
            if (bordering.Count == 0) return;
            c0.Combine(bordering);
            foreach (var c in bordering)
                pieces.Remove(c);
            // Keep selectedIndex pointing to c0 (still in list)
            selectedIndex = pieces.IndexOf(c0);
            PropagateStatus();
            OnCombine?.Invoke(selectedIndex);
            OnChanged?.Invoke();
        }

        public void Reset()
        {
            pieces.Clear();
            for (int i = 0; i < Objective.pieces.Length; i++)
                pieces.Add(new Piece(Objective.pieces[i]) { colorSlot = i });
            selectedIndex = 0;
            PropagateStatus();
            OnReset?.Invoke();
            OnChanged?.Invoke();
        }

        public void CyclePiece()
        {
            if (pieces.Count == 0) return;
            selectedIndex = (selectedIndex + 1) % pieces.Count;
            OnChanged?.Invoke();
        }

        private bool IsOverlapping()
        {
            return IntegerOps.Intersecting(pieces.Select(c => c.origins).ToArray());
        }

        private bool IsInsideBoundary(int[][] origins)
        {
            var bmm = Objective?.boundary_min_max;
            if (bmm == null || bmm.Length < 2 || bmm[0] == null || bmm[1] == null) return true;
            int dims = bmm[0].Length;
            foreach (var o in origins)
            {
                for (int a = 0; a < dims && a < o.Length; a++)
                {
                    if (o[a] < bmm[0][a]) return false;
                    if (o[a] + 1 > bmm[1][a]) return false;
                }
            }
            return true;
        }

        private List<Piece> FindAdjacent(Piece c0)
        {
            var result = new List<Piece>();
            foreach (var c in pieces)
                if (c != c0 && IntegerOps.D3adjacent(c.origins, c0.origins))
                    result.Add(c);
            return result;
        }

        private void PropagateStatus()
        {
            if (goal == null)
            {
                status = GameStatus.None;
                return;
            }
            // Shape (default): equality modulo translation/rotation. Absolute: the goal
            // is reached only when the single remaining compound is congruent with the
            // goal (same cell origins — no translation or rotation).
            bool absolute = Objective != null && Objective.mode == GoalMode.Absolute;
            if (pieces.Count == 1)
            {
                bool reached = absolute
                    ? IntegerOps.SetEqual(goal, pieces[0].origins)
                    : IntegerOps.MotionEqual(goal, pieces[0].origins);
                status = reached ? GameStatus.Reached : GameStatus.Missed;
                return;
            }
            var sel = Selected;
            if (sel != null)
            {
                bool contained = absolute
                    ? IntegerOps.SetContained(sel.origins, goal)
                    : IntegerOps.MotionContained(sel.origins, goal);
                if (!contained)
                {
                    status = GameStatus.Missed;
                    return;
                }
            }
            if (status != GameStatus.Pending)
                status = GameStatus.Pending;
        }
    }
}
