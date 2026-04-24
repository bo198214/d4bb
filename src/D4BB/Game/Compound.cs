using System.Collections.Generic;
using D4BB.Comb;

namespace D4BB.Game
{
    public class Compound
    {
        public int[][] origins;
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
                all.AddRange(other.origins);
            origins = all.ToArray();
            RecomputeCenter();
        }

        private void RecomputeCenter()
        {
            center = new IntegerCenter(origins, asCubes: true);
        }
    }
}
