using System;
using System.Collections.Generic;
using System.Linq;
using D4BB.Comb;

namespace D4BB.Game
{
    public enum MoveBlockReason { None, Overlap, OutOfBoundary }

    public class GameLevel
    {
        public List<Compound> compounds = new();
        public int selectedIndex = 0;
        public int[][] goal;
        public GameStatus status = GameStatus.None;
        public Objective Objective { get; private set; }
        public MoveBlockReason LastBlockReason { get; private set; } = MoveBlockReason.None;

        public int[][][] PieceOrigins => compounds.Select(c => c.origins).ToArray();

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
                compounds.Add(new Compound(obj.pieces[i]) { colorSlot = i });
            PropagateStatus();
        }

        public Compound Selected => selectedIndex >= 0 && selectedIndex < compounds.Count
            ? compounds[selectedIndex] : null;

        public void SelectPiece(int index)
        {
            if (index < 0 || index >= compounds.Count) return;
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

            // 1. Apply rotation to origins
            foreach (var o in c.origins)
                IntegerOps.RotateAsCenters(o, pivot, v, w);

            // 2. Boundary check
            if (!IsInsideBoundary(c.origins))
            {
                foreach (var o in c.origins)
                    IntegerOps.RotateAsCenters(o, pivot, w, v);
                LastBlockReason = MoveBlockReason.OutOfBoundary;
                return false;
            }

            // 3. Check for collisions
            if (IsOverlapping())
            {
                // Revert if blocked
                foreach (var o in c.origins)
                    IntegerOps.RotateAsCenters(o, pivot, w, v);
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
            if (c0 == null || compounds.Count == 1) return;
            var bordering = FindAdjacent(c0);
            if (bordering.Count == 0) return;
            c0.Combine(bordering);
            foreach (var c in bordering)
                compounds.Remove(c);
            // Keep selectedIndex pointing to c0 (still in list)
            selectedIndex = compounds.IndexOf(c0);
            PropagateStatus();
            OnCombine?.Invoke(selectedIndex);
            OnChanged?.Invoke();
        }

        public void Reset()
        {
            compounds.Clear();
            for (int i = 0; i < Objective.pieces.Length; i++)
                compounds.Add(new Compound(Objective.pieces[i]) { colorSlot = i });
            selectedIndex = 0;
            PropagateStatus();
            OnReset?.Invoke();
            OnChanged?.Invoke();
        }

        public void CyclePiece()
        {
            if (compounds.Count == 0) return;
            selectedIndex = (selectedIndex + 1) % compounds.Count;
            OnChanged?.Invoke();
        }

        private bool IsOverlapping()
        {
            return IntegerOps.Intersecting(compounds.Select(c => c.origins).ToArray());
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

        private List<Compound> FindAdjacent(Compound c0)
        {
            var result = new List<Compound>();
            foreach (var c in compounds)
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
            // Absolute (default): the goal is reached only when the single remaining
            // compound is congruent with the goal (same cell origins — no translation
            // or rotation). Shape: equality modulo translation/rotation, as before.
            bool absolute = Objective == null || Objective.mode == GoalMode.Absolute;
            if (compounds.Count == 1)
            {
                bool reached = absolute
                    ? IntegerOps.SetEqual(goal, compounds[0].origins)
                    : IntegerOps.MotionEqual(goal, compounds[0].origins);
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
