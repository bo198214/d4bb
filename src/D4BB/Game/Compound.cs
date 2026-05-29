using System.Collections.Generic;
using D4BB.Comb;

namespace D4BB.Game
{
    public class Compound
    {
        public int[][] origins;
        // Stable identity slot used by the renderer to pick a piece color from a palette that
        // is sized at level load and never shrinks. Survives Combine — see Combine below — so a
        // piece's color does not change when a neighbour merges into it. -1 = unassigned;
        // GameLevel sets it to the original piece index when the level is built / reset.
        public int colorSlot = -1;
        private IntegerCenter center;

        public Compound(int[][] origins)
        {
            this.origins = IntegerOps.Clone(origins);
            RecomputeCenter();
        }

        public void Translate(IntegerSignedAxis v)
        {
            IntegerOps.Translate(origins, v);
            center.Translate(v);
        }

        public void Rotate(int v, int w)
        {
            foreach (var o in origins)
                IntegerOps.RotateAsCenters(o, center, v, w);
        }

        public void Combine(IEnumerable<Compound> others)
        {
            var all = new List<int[]>(origins);
            foreach (var other in others)
            {
                all.AddRange(other.origins);
                if (other.colorSlot >= 0 && (colorSlot < 0 || other.colorSlot < colorSlot))
                    colorSlot = other.colorSlot;
            }
            origins = all.ToArray();
            RecomputeCenter();
        }

        private void RecomputeCenter()
        {
            center = new IntegerCenter(origins, asCubes: true);
        }
    }
}
