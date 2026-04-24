using D4BB.Comb;

namespace D4BB.Game
{
    public class Objective
    {
        public string name;
        public int[][] goal;
        public int[][][] pieces;
        public int[][] boundary_min_max;

        public Objective(string name, int[][] goal, int[][][] pieces, int[][] boundary_min_max = null)
        {
            this.name = name;
            this.goal = IntegerOps.Clone(goal);
            this.pieces = new int[pieces.Length][][];
            for (int i = 0; i < pieces.Length; i++)
                this.pieces[i] = IntegerOps.Clone(pieces[i]);
            if (boundary_min_max == null) {
                this.boundary_min_max = new int[2][];
                this.boundary_min_max[0] = new int[4];
                this.boundary_min_max[1] = new int[4];
                for (int k = 0; k < 4; k++) {
                    int min = int.MaxValue, max = int.MinValue;
                    for (int i = 0; i < pieces.Length; i++)
                        for (int j = 0; j < pieces[i].Length; j++) {
                            if (pieces[i][j][k] < min) this.boundary_min_max[0][k] = pieces[i][j][k];
                            if (pieces[i][j][k] > max) this.boundary_min_max[0][k] = pieces[i][j][k];
                        }
                }
            } else {
                this.boundary_min_max = boundary_min_max;
            }
        }
    }
}
