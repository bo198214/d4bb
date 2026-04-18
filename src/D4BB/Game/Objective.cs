using D4BB.Comb;

namespace D4BB.Game
{
    public class Objective
    {
        public string name;
        public int[][] goal;
        public int[][][] pieces;

        public Objective(string name, int[][] goal, int[][][] pieces)
        {
            this.name = name;
            this.goal = IntegerOps.Clone(goal);
            this.pieces = new int[pieces.Length][][];
            for (int i = 0; i < pieces.Length; i++)
                this.pieces[i] = IntegerOps.Clone(pieces[i]);
        }
    }
}
