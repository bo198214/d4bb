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

        public event Action OnChanged;

        public GameLevel(Objective obj)
        {
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
            c.Translate(axis);
            if (IsOverlapping())
            {
                c.Translate(new IntegerSignedAxis(-axis.Human()));
                return false;
            }
            PropagateStatus();
            OnChanged?.Invoke();
            return true;
        }

        public bool RotateSelected(int v, int w)
        {
            var c = Selected;
            if (c == null) return false;
            c.Rotate(v, w);
            if (IsOverlapping())
            {
                c.Rotate(w, v); // counter-rotation
                return false;
            }
            PropagateStatus();
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
