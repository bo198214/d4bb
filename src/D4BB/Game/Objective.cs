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
    /// <item><b>Absolute</b>: the pieces must occupy exactly the goal's cell
    /// origins — no translation or rotation allowed. The pieces need not be
    /// combined into one compound; the arrangement alone decides.</item>
    /// </list>
    /// </summary>
    public enum GoalMode { Absolute, Shape }

    public class Objective
    {
        public string name;
        // Optional metadata (null when the level JSON has none). The description is player-facing:
        // the game shows it as a briefing page before the level starts (Unity rich-text, like the
        // tutorial pages). Both round-trip through ToJson/FromJson so a re-exported level keeps them.
        public string description;
        public string author;
        // The level's score weight in the game's progression (points earned by solving it; the
        // currency that unlocks chapters and Polychoron Watch exhibits). Default 1 = "unrated
        // easy" — level files without the field are worth one point, so the field is optional
        // metadata like description/author and generators only emit it for harder levels.
        public int points = 1;
        // The level's default 3D display scale: the 4D→3D projection zoom (Camera4dParallel.zoom3d)
        // the level starts at (and "Reset Zoom" returns to). Default 1 = one grid unit per world
        // unit, so the field is optional metadata like points — level files without it stay free of
        // it. Purely a display hint; the geometry itself is untouched.
        public double scale = 1;
        // ---- Viewer placement ----------------------------------------------------------------
        // The level's facade distance (JSON "dist", meters): how far the viewer-facing front of
        // the CONTENT — the nearest projected corner of the pieces+goal bounding box — sits in
        // front of the player's eyes. null = DefaultDist. This is the ONE authored placement
        // quantity; the game pins the facade at Dist (Game.ResetView), and the scalar-padding
        // envelope derives its near z margin from it (see BoundaryMinMax): the room between the
        // MinFrontDistance comfort line and the facade is filled with workbench cells rather
        // than left empty. In meters on purpose — padding is in cells × scale, the wrong unit
        // for a distance, and Dist stays put when the player zooms (zoom scales about the facade).
        public double? dist;
        public double Dist => dist ?? DefaultDist;
        // Facade distance of levels without "dist" — where most authored levels stand.
        public const double DefaultDist = 3.5;
        // Closest the play volume's front (and thus any movable piece) may come to the eyes:
        // the physical eye-comfort floor (focus + convergence), deliberately zoom-independent —
        // a miniature and a full-size field get the same floor. ONE constant, two enforcement
        // points: at load, the scalar-padding derivation never lets the envelope front cross it
        // and every parsed level is checked against it (an explicit envelope can violate it —
        // fail fast); at runtime, the game's collision guard (Tesserian SceneGrabController)
        // rejects every pose change — grab, zoom, distance slider — that would bring a piece
        // closer to the eyes than its clearance (Game's Inspector field minPiecesClearance, seeded
        // from this constant; the guard's body is an Inspector experiment: plane, cylinder, ball,
        // the whole envelope, or off). 0.3 m
        // since 2026-09-20 (was 1 m; lowering it changed no catalog envelope — the fill rounds to
        // the same whole cells for every authored dist/padding — and the authored placement of
        // every catalog level still starts at ≥ 1 m; anything closer is the player's own doing).
        public const double MinFrontDistance = 0.3;
        // The canonical cavalier projection's w→z shear (azimuth 45°, elevation 45°, length 1):
        // one w cell of near padding moves the facade this many cells further back. The envelope
        // must be deterministic for the solver, so the derivation uses this design-time value,
        // not the live (player-adjustable) projection length.
        public const double CavalierShearZ = 0.5;
        // Where the play volume's front lands when the facade is at Dist: the near z/w padding
        // (whatever form it came from) projected back toward the viewer. >= MinFrontDistance for
        // every valid level; the game reads Dist and this only through the placement pivot.
        public double EnvelopeFrontDistance {
            get {
                var lo = PaddingsLowerUpper()[0];
                double z = lo.Length > 2 ? lo[2] : 0, w = lo.Length > 3 ? lo[3] : 0;
                return Dist - scale * (z + CavalierShearZ * w);
            }
        }
        // Whether "quantum rotation" is allowed: with true, a 90° rotation is legal whenever
        // its END pose is free, even if the swept quarter turn would pass through other
        // pieces or leave the boundary (tunneling — the pre-2026-08 behavior). Default false:
        // the swept motion is checked too, with each cell's INSCRIBED DISK as the moving
        // body — face-touching neighbors and flush walls tolerate the turn (only the corner
        // lenses cross them), while genuine pass-throughs block (see RotationSweep for the
        // exact semantics). Level JSON: "quantum_rotation": true — only emitted when set,
        // like "mode".
        public bool quantumRotation = false;
        // The level is unsolvable by design (e.g. a goal that is the mirror image of a chiral
        // piece). The solver tests skip it, and it does not count towards completing its chapter.
        // Level JSON: "unsolvable": true. Read-only like "boundary_min_max": ToJson never writes
        // it, because ToJson feeds the player-visible Custom-Level page, and the level should not
        // give away that it cannot be solved.
        public bool unsolvable = false;
        public int[][] goal;
        public int[][][] pieces;
        public int[][] boundary_min_max;
        public GoalMode mode = GoalMode.Shape;

        // Scalar-padding envelope at scale 1 and DefaultDist (the envelope depends on both via the
        // near z derivation — see BoundaryMinMax). FromJson computes the envelope with the level's
        // own scale/dist; this overload serves code-constructed levels and tests.
        public Objective(string name, int[][] goal, int[][][] pieces, int padding = 1) : this(name, goal, pieces, BoundaryMinMax(pieces, goal, padding, 1, DefaultDist)) {}
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
        // paddings_lower_upper). The envelope is written as paddings_lower_upper — the form the
        // hand-written level files use — rather than the absolute boundary_min_max or the scalar
        // padding, so an exported level stays readable and keeps following its pieces if they are
        // later edited. The round-trip is still exact regardless of how this Objective was
        // constructed: PaddingsLowerUpper() is the exact inverse of BoundaryMinMax(paddings).
        // InlineIntArrayConverter keeps each coordinate tuple on one line ([0, 0, 0, 0]) while the
        // surrounding structure stays indented — matches the hand-written level files.
        public string ToJson() {
            var data = new ObjectiveData {
                Name = name,
                // Absent and empty collapse to "not emitted" (NullValueHandling.Ignore below), so
                // metadata-free level files stay free of the fields on round-trip.
                Description = string.IsNullOrEmpty(description) ? null : description,
                Author = string.IsNullOrEmpty(author) ? null : author,
                // Only emit "points" when it deviates from the default 1, keeping unrated level
                // files free of the field on round-trip (same policy as "mode").
                Points = points == 1 ? (int?)null : points,
                // Same only-when-non-default policy as "points".
                Scale = scale == 1 ? (double?)null : scale,
                // null = DefaultDist; only an explicit facade distance is emitted.
                Dist = dist,
                Goal = goal,
                Pieces = pieces,
                PaddingsLowerUpper = PaddingsLowerUpper(),
                // Only emit "mode" when it deviates from the Shape default, keeping
                // shape-mode level files free of a redundant field on round-trip.
                Mode = mode == GoalMode.Shape ? null : "absolute",
                // Same only-when-non-default policy: absent means false (swept rotations).
                QuantumRotation = quantumRotation ? (bool?)true : null,
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
            // scale and dist first: the scalar-padding envelope is derived from both.
            // A non-positive scale would render the level invisible or mirrored — loud, per fail fast.
            if (data.Scale.HasValue && data.Scale.Value <= 0)
                throw new ArgumentException(
                    $"Level '{data.Name}': \"scale\" must be > 0 (got {data.Scale.Value}).");
            double scale = data.Scale ?? 1;
            if (data.Dist.HasValue && data.Dist.Value <= 0)
                throw new ArgumentException(
                    $"Level '{data.Name}': \"dist\" must be > 0 (got {data.Dist.Value}).");
            Objective obj;
            if (data.BoundaryMinMax != null)
                obj = new Objective(data.Name, data.Goal, data.Pieces, data.BoundaryMinMax);
            else if (data.PaddingsLowerUpper != null)
                obj = new Objective(data.Name, data.Goal, data.Pieces,
                                    BoundaryMinMax(data.Pieces, data.Goal, data.PaddingsLowerUpper));
            else
                obj = new Objective(data.Name, data.Goal, data.Pieces,
                                    BoundaryMinMax(data.Pieces, data.Goal, data.Padding ?? 1, scale, data.Dist ?? DefaultDist));
            obj.scale = scale;
            obj.dist = data.Dist;
            // The play volume's front must stay behind the comfort line. The scalar form guarantees
            // it by construction; an explicit envelope with a large near margin and a small dist
            // does not — loud, per fail fast.
            if (obj.EnvelopeFrontDistance < MinFrontDistance - 1e-9)
                throw new ArgumentException(
                    $"Level '{data.Name}': dist {obj.Dist} m puts the play volume's front at " +
                    $"{obj.EnvelopeFrontDistance:0.##} m, closer than the {MinFrontDistance} m minimum — " +
                    "raise \"dist\" or reduce the near z/w padding.");
            obj.mode = ParseMode(data.Mode);
            obj.description = data.Description;
            obj.author = data.Author;
            // A zero/negative weight would silently corrupt the point-based progression
            // (unlock thresholds are sums of these) — loud, per fail fast.
            if (data.Points.HasValue && data.Points.Value < 1)
                throw new ArgumentException(
                    $"Level '{data.Name}': \"points\" must be >= 1 (got {data.Points.Value}).");
            obj.points = data.Points ?? 1;
            obj.quantumRotation = data.QuantumRotation ?? false;
            obj.unsolvable = data.Unsolvable ?? false;
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
            [JsonProperty("description")]
            public string Description { get; set; }
            [JsonProperty("author")]
            public string Author { get; set; }
            [JsonProperty("points")]
            public int? Points { get; set; }
            [JsonProperty("scale")]
            public double? Scale { get; set; }
            [JsonProperty("dist")]
            public double? Dist { get; set; }
            // Declaration order is emission order: paddings_lower_upper (the only envelope form
            // ToJson writes) sits with the short header fields, right before the bulky "goal" /
            // "pieces" arrays, so the envelope is visible at the top of a dumped level file.
            [JsonProperty("paddings_lower_upper")]
            public int[][] PaddingsLowerUpper { get; set; }
            [JsonProperty("goal")]
            public int[][] Goal { get; set; }
            [JsonProperty("pieces")]
            public int[][][] Pieces { get; set; }
            [JsonProperty("boundary_min_max")]
            public int[][] BoundaryMinMax { get; set; }
            [JsonProperty("mode")]
            public string Mode { get; set; }
            [JsonProperty("quantum_rotation")]
            public bool? QuantumRotation { get; set; }
            [JsonProperty("unsolvable")]
            public bool? Unsolvable { get; set; }
            [JsonProperty("padding")]
            public int? Padding { get; set; }
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
        // Inverse of BoundaryMinMax(pieces, goal, paddingsLowerUpper): the per-axis, per-side
        // distance between this Objective's envelope and the tight bounding box of pieces+goal.
        // Exact by construction — BoundingBox() already carries the +1 half-open offset on the
        // upper side, the same one BoundaryMinMax adds — so feeding the result back through
        // BoundaryMinMax reproduces boundary_min_max bit for bit. Values may be negative if the
        // envelope cuts into the bounding box (an explicitly authored boundary_min_max can do
        // that); the round-trip stays exact, since the padding form has no sign restriction.
        public int[][] PaddingsLowerUpper()
        {
            var bb = BoundingBox();
            int dim = bb[0].Length;
            var res = new int[2][] { new int[dim], new int[dim] };
            for (int k = 0; k < dim; k++) {
                res[0][k] = bb[0][k] - boundary_min_max[0][k];
                res[1][k] = boundary_min_max[1][k] - bb[1][k];
            }
            return res;
        }
        // The near z margin of a scalar-padding envelope, derived from the facade distance: the
        // room between MinFrontDistance and the facade, in whole cells (after the near w margin's
        // share of the depth), clamped to [min(padding,1), padding]. So dist raises the near z
        // margin above the one workbench cell but never past the level's own padding — beyond
        // that the remainder is pure distance (EnvelopeFrontDistance > MinFrontDistance). A
        // fraction of a cell always goes into distance, never into a partial cell.
        public static int NearZPadding(int padding, double scale, double dist, int nearW)
        {
            int min = Math.Min(padding, 1);
            int fill = (int)Math.Floor((dist - MinFrontDistance) / scale - CavalierShearZ * nearW + 1e-9);
            return Math.Max(min, Math.Min(padding, fill));
        }
        public static int[][] BoundaryMinMax(int[][][] pieces, int[][] goal, int padding, double scale, double dist)
        {
            int dim = pieces.Length > 0 ? pieces[0][0].Length : goal[0].Length;
            int nearW = dim > 3 ? Math.Min(padding, 1) : 0;
            int nearZ = NearZPadding(padding, scale, dist, nearW);
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
                // Every axis gets the same total slack of 2·padding cells, but the depth
                // axes distribute it asymmetrically. Cavalier world_z = z + pz·w (pz > 0):
                // the viewer-facing surface of the play volume sits at (min z, min w), so
                // near-side padding on the depth axes is room BETWEEN the player and the
                // content. Axes 0/1 (the projection plane) take the full padding on both
                // sides. w gets a one-cell near margin (padding 0 stays 0: a w-slice is a
                // puzzle property) — never more, since every w layer changes the depth
                // palette. z gets its near margin from the facade distance (NearZPadding):
                // as many workbench cells as fit between MinFrontDistance and dist, capped
                // at `padding`. Whatever the near side does not take goes to the far side,
                // keeping the sum at 2·padding. For per-axis, per-side control use
                // paddings_lower_upper (literal; dist then only places the facade).
                int near = k < 2 ? padding : k == 2 ? nearZ : nearW;
                res[0][k] -= near;
                res[1][k] += 1 + 2 * padding - near;
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
