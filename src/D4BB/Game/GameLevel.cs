using System;
using System.Collections.Generic;
using System.Linq;
using D4BB.Comb;

namespace D4BB.Game
{
    public class GameLevel
    {
        public List<Compound> compounds = new();
        public int selectedIndex = 0;
        public int[][] goal;
        public GameStatus status = GameStatus.None;
        public Objective Objective { get; private set; }

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
            foreach (var piece in obj.pieces)
                compounds.Add(new Compound(piece));
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
            if (IsOverlapping())
            {
                c.Translate(new IntegerSignedAxis(-axis.Human()));
                return false;
            }
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

            // 2. Check for collisions
            if (IsOverlapping())
            {
                // Revert if blocked
                foreach (var o in c.origins) 
                    IntegerOps.RotateAsCenters(o, pivot, w, v);
                return false;
            }

            // 3. Success: Commit and notify
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
            foreach (var piece in Objective.pieces)
                compounds.Add(new Compound(piece));
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
            if (compounds.Count == 1)
            {
                status = IntegerOps.MotionEqual(goal, compounds[0].origins)
                    ? GameStatus.Reached
                    : GameStatus.Missed;
                return;
            }
            var sel = Selected;
            if (sel != null && !IntegerOps.MotionContained(sel.origins, goal))
            {
                status = GameStatus.Missed;
                return;
            }
            if (status != GameStatus.Pending)
                status = GameStatus.Pending;
        }
    }
}
