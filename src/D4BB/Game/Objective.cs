using D4BB.Comb;
using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace D4BB.Game
{
    /// <summary>
    /// How a level's goal is matched against the player's assembled compound.
    /// <list type="bullet">
    /// <item><b>Shape</b> (default): the compound must match the goal only up to
    /// translation/rotation (the classic "build this shape anywhere" rule).</item>
    /// <item><b>Absolute</b>: the single remaining compound must be
    /// <i>congruent</i> with the goal — exactly the same cell origins, no
    /// translation or rotation allowed.</item>
    /// </list>
    /// </summary>
    public enum GoalMode { Absolute, Shape }

    public class Objective
    {
        public string name;
        public int[][] goal;
        public int[][][] pieces;
        public int[][] boundary_min_max;
        public GoalMode mode = GoalMode.Shape;

        public Objective(string name, int[][] goal, int[][][] pieces, int padding = 1) : this(name, goal, pieces, BoundaryMinMax(pieces, goal, padding)) {}
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
        // Serialize back to the same JSON shape FromJson reads (name / goal / pieces /
        // boundary_min_max). boundary_min_max is written explicitly rather than padding, so the
        // round-trip is exact regardless of how this Objective was originally constructed.
        // InlineIntArrayConverter keeps each coordinate tuple on one line ([0, 0, 0, 0]) while the
        // surrounding structure stays indented — matches the hand-written level files.
        public string ToJson() {
            var data = new ObjectiveData {
                Name = name,
                Goal = goal,
                Pieces = pieces,
                BoundaryMinMax = boundary_min_max,
                // Only emit "mode" when it deviates from the Shape default, keeping
                // shape-mode level files free of a redundant field on round-trip.
                Mode = mode == GoalMode.Shape ? null : "absolute",
            };
            return JsonConvert.SerializeObject(data, new JsonSerializerSettings {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore,
                Converters = { new InlineIntArrayConverter() },
            });
        }

        // Writes an int[] as a single inline JSON array ([0, 0, 0, 0]) regardless of the
        // serializer's indentation. WriteRawValue keeps the parent array's indentation intact, so
        // only the innermost coordinate tuples collapse to one line. Serialize-only.
        private class InlineIntArrayConverter : JsonConverter<int[]> {
            public override bool CanRead => false;
            public override int[] ReadJson(JsonReader reader, Type objectType, int[] existingValue,
                                           bool hasExistingValue, JsonSerializer serializer)
                => throw new NotSupportedException();
            public override void WriteJson(JsonWriter writer, int[] value, JsonSerializer serializer) {
                if (value == null) { writer.WriteNull(); return; }
                writer.WriteRawValue("[" + string.Join(", ", value) + "]");
            }
        }
        public static Objective FromJson(string json) {
            var data = JsonConvert.DeserializeObject<ObjectiveData>(json);
            Objective obj;
            if (data.BoundaryMinMax != null)
                obj = new Objective(data.Name, data.Goal, data.Pieces, data.BoundaryMinMax);
            else if (data.PaddingsLowerUpper != null)
                obj = new Objective(data.Name, data.Goal, data.Pieces,
                                    BoundaryMinMax(data.Pieces, data.Goal, data.PaddingsLowerUpper));
            else if (data.Padding.HasValue)
                obj = new Objective(data.Name, data.Goal, data.Pieces, data.Padding.Value);
            else
                obj = new Objective(data.Name, data.Goal, data.Pieces);
            obj.mode = ParseMode(data.Mode);
            return obj;
        }

        // Absent / unknown "mode" → Shape (the default). Only "absolute"
        // (case-insensitive) selects exact-congruence matching.
        private static GoalMode ParseMode(string mode) {
            return string.Equals(mode, "absolute", StringComparison.OrdinalIgnoreCase)
                ? GoalMode.Absolute
                : GoalMode.Shape;
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
            [JsonProperty("mode")]
            public string Mode { get; set; }
            [JsonProperty("padding")]
            public int? Padding { get; set; }
            [JsonProperty("paddings_lower_upper")]
            public int[][] PaddingsLowerUpper { get; set; }
        }

        // Spatial dimension of the puzzle, inferred from the piece coordinates (4 for the
        // main 4D game, 3 for Game3d). Every cell origin carries one coord per axis.
        static int Dim(int[][][] pieces) => pieces[0][0].Length;

        public int[][] BoundingBox()
        {
            int dim = pieces.Length > 0 ? pieces[0][0].Length : goal[0].Length;
            int[][] res = new int[2][];
            res[0] = new int[dim];
            res[1] = new int[dim];
            for (int k = 0; k < dim; k++) {
                res[0][k] = int.MaxValue;
                res[1][k] = int.MinValue;
                for (int i = 0; i <= pieces.Length; i++) {
                    var o = i==pieces.Length ? goal : pieces[i];
                    for (int j = 0; j < o.Length; j++) {
                        if (o[j][k] < res[0][k]) { res[0][k] = o[j][k]; }
                        if (o[j][k] > res[1][k]) { res[1][k] = o[j][k]; }
                    }
                }
                res[1][k] += 1;
            }
            return res;
        }
        public static int[][] BoundaryMinMax(int[][][] pieces, int[][] goal, int padding)
        {
            int dim = pieces.Length > 0 ? pieces[0][0].Length : goal[0].Length;
            int[][] res = new int[2][];
            res[0] = new int[dim];
            res[1] = new int[dim];
            for (int k = 0; k < dim; k++) {
                res[0][k] = int.MaxValue;
                res[1][k] = int.MinValue;
                for (int i = 0; i <= pieces.Length; i++) {
                    var o = i==pieces.Length ? goal : pieces[i];
                    for (int j = 0; j < o.Length; j++) {
                        if (o[j][k] < res[0][k]) { res[0][k] = o[j][k]; }
                        if (o[j][k] > res[1][k]) { res[1][k] = o[j][k]; }
                    }
                }
                // Cavalier world_z = z + pz·w (pz > 0): the viewer-facing surface of
                // the play volume sits at (min z, min w). Front-padding on the depth
                // axes (2 and up) would let pieces drift into the viewer; we only pad
                // the far side. Axes 0/1 (the projection plane) are padded both sides.
                if (k < 2) res[0][k] -= padding;
                res[1][k] += 1+padding;
            }
            return res;
        }
        public static int[][] BoundaryMinMax(int[][][] pieces, int[][] goal, int[][] paddingsLowerUpper)
        {
            int dim = pieces.Length > 0 ? pieces[0][0].Length : goal[0].Length;
            if (paddingsLowerUpper == null
                || paddingsLowerUpper.Length != 2
                || paddingsLowerUpper[0] == null || paddingsLowerUpper[0].Length != dim
                || paddingsLowerUpper[1] == null || paddingsLowerUpper[1].Length != dim)
                throw new ArgumentException(
                    $"paddings_lower_upper must be an int[2][{dim}]: [lower-padding[{dim}], upper-padding[{dim}]].",
                    nameof(paddingsLowerUpper));
            int[][] res = new int[2][];
            res[0] = new int[dim];
            res[1] = new int[dim];
            for (int k = 0; k < dim; k++) {
                res[0][k] = int.MaxValue;
                res[1][k] = int.MinValue;
                for (int i = 0; i <= pieces.Length; i++) {
                    var o = i==pieces.Length ? goal : pieces[i];
                    for (int j = 0; j < o.Length; j++) {
                        if (o[j][k] < res[0][k]) { res[0][k] = o[j][k]; }
                        if (o[j][k] > res[1][k]) { res[1][k] = o[j][k]; }
                    }
                }
                res[0][k] -= paddingsLowerUpper[0][k];
                res[1][k] += 1 + paddingsLowerUpper[1][k];
            }
            return res;
        }
    }
}
