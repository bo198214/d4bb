using D4BB.Comb;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace D4BB.Game
{
    public class Objective
    {
        public string name;
        public int[][] goal;
        public int[][][] pieces;
        public int[][] boundary_min_max;

        public Objective(string name, int[][] goal, int[][][] pieces, int padding = 1) : this(name, goal, pieces, BoundaryMinMax(pieces, padding)) {}
        public Objective(string name, int[][] goal, int[][][] pieces, int[][] boundary_min_max)
        {
            this.name = name;
            this.goal = IntegerOps.Clone(goal);
            this.pieces = new int[pieces.Length][][];
            for (int i = 0; i < pieces.Length; i++)
                this.pieces[i] = IntegerOps.Clone(pieces[i]);
            this.boundary_min_max = boundary_min_max;
        }
        public static Objective FromJsonFile(string filePath) {
            return FromJson(File.ReadAllText(filePath));
        }
        public static Objective FromJson(string json) {
            var data = JsonConvert.DeserializeObject<ObjectiveData>(json);
            if (data.BoundaryMinMax != null)
                return new Objective(data.Name, data.Goal, data.Pieces, data.BoundaryMinMax);
            if (data.Padding.HasValue)
                return new Objective(data.Name, data.Goal, data.Pieces, data.Padding.Value);
            return new Objective(data.Name, data.Goal, data.Pieces);
        }

        private class ObjectiveData {
            [JsonProperty("name")]
            public string Name { get; set; }
            [JsonProperty("goal")]
            public int[][] Goal { get; set; }
            [JsonProperty("pieces")]
            public int[][][] Pieces { get; set; }
            [JsonProperty("boundary_min_max")]
            public int[][] BoundaryMinMax { get; set; }
            [JsonProperty("padding")]
            public int? Padding { get; set; }
        }

        public int[][] BoundingBox()
        {
            int[][] res = new int[2][];
            res[0] = new int[4];
            res[1] = new int[4];
            for (int k = 0; k < 4; k++) {
                res[0][k] = int.MaxValue;
                res[1][k] = int.MinValue;
                for (int i = 0; i < pieces.Length; i++)
                    for (int j = 0; j < pieces[i].Length; j++) {
                        if (pieces[i][j][k] < res[0][k]) { res[0][k] = pieces[i][j][k]; }
                        if (pieces[i][j][k] > res[1][k]) { res[1][k] = pieces[i][j][k]; }
                    }
                res[1][k] += 1;
            }
            return res;
        }
        public static int[][] BoundaryMinMax(int[][][] pieces, int padding)
        {
            int[][] res = new int[2][];
            res[0] = new int[4];
            res[1] = new int[4];
            for (int k = 0; k < 4; k++) {
                res[0][k] = int.MaxValue;
                res[1][k] = int.MinValue;
                for (int i = 0; i < pieces.Length; i++)
                    for (int j = 0; j < pieces[i].Length; j++) {
                        if (pieces[i][j][k] < res[0][k]) { res[0][k] = pieces[i][j][k]; }
                        if (pieces[i][j][k] > res[1][k]) { res[1][k] = pieces[i][j][k]; }
                    }
                res[0][k] -= padding;
                res[1][k] += 1+padding;
            }
            return res;
        }
    }
}
