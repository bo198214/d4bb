using System.Collections.Generic;
using System.Text;
using D4BB.Comb;

namespace D4BB.Game
{
    /// <summary>
    /// Geometry-free core for the "auto-orient a piece for the clearest view" feature: counts
    /// overlapping front-facing 3-cells in a piece's parallel 3D projection, and searches the discrete
    /// 4-cube rotation group for the orientation with the fewest such overlaps.
    ///
    /// This depends only on <c>D4BB.Comb</c> (no Unity, no <c>D4BB.Geometry</c>), so it can be linked
    /// straight into the offline level generators (tools/region-split, tools/region-split-sym) as well
    /// as used by the game. The game-facing camera wrapper is <see cref="PieceOrientation"/>, which
    /// derives a <see cref="ParallelProjection"/> from a live <c>Camera4dParallel</c> and delegates here.
    ///
    /// Overlap is measured exactly as in FrontCellsNonOverlappingTests: only front-facing boundary
    /// 3-cells are considered (back-facing ones are hidden behind the solid piece), and a pair counts
    /// when their projected interiors intersect (mere face/edge touching does not count).
    /// </summary>
    public static class PieceProjectionOverlap
    {
        /// <summary>
        /// A parallel 4D→3D projection plus the view normal, capturing any <c>Camera4dParallel</c>
        /// state. <see cref="v"/> are the three image-plane basis rows (each length 4): the projection
        /// is <c>(v0·p, v1·p, v2·p)</c>. A boundary cell is front-facing iff
        /// <c>viewNormal·cellNormal &lt; 0</c>. A uniform zoom is irrelevant to overlap, so none is stored.
        /// </summary>
        public readonly struct ParallelProjection
        {
            public readonly double[][] v;          // [3][4] — image-plane basis rows
            public readonly double[] viewNormal;   // [4]

            public ParallelProjection(double[][] v, double[] viewNormal)
            {
                this.v = v;
                this.viewNormal = viewNormal;
            }

            /// <summary>
            /// The game's default camera: cavalier oblique projection with wDir = (1,1,1)/√5, exactly
            /// what <c>new Camera4dParallel()</c> (SetCavalier) produces. A package test
            /// (PieceOrientationTests) locks this against the real camera so it can't silently drift.
            /// </summary>
            public static ParallelProjection DefaultGame
            {
                get
                {
                    double p = 1.0 / System.Math.Sqrt(5.0);          // SetCavalier() default length
                    double n = 1.0 / System.Math.Sqrt(3 * p * p + 1.0);
                    return new ParallelProjection(
                        new[]
                        {
                            new double[] { 1, 0, 0, p },
                            new double[] { 0, 1, 0, p },
                            new double[] { 0, 0, 1, p },
                        },
                        new double[] { -p * n, -p * n, -p * n, n });
                }
            }
        }

        public sealed class Result
        {
            /// <summary>Piece origins after applying <see cref="rotationPath"/>.</summary>
            public int[][] origins;
            /// <summary>
            /// Ordered 90° rotations to reach <see cref="origins"/> from the input, each directly
            /// playable via <c>GameLevel.RotateSelected(v, w)</c>. Empty ⇒ the input is already optimal.
            /// </summary>
            public List<(int v, int w)> rotationPath = new();
            /// <summary>Overlapping front-facing 3-cell pairs at the chosen orientation.</summary>
            public int overlapPairs;
            /// <summary>Overlapping pairs at the input orientation, for reference / no-op detection.</summary>
            public int baselineOverlapPairs;
        }

        // 12 ordered coordinate-plane generators (both turn directions, so the BFS finds the
        // shortest natural move sequence). Their closure is the full 4-cube rotation group (order 192).
        static readonly (int v, int w)[] Generators = {
            (0,1),(1,0),(0,2),(2,0),(0,3),(3,0),
            (1,2),(2,1),(1,3),(3,1),(2,3),(3,2),
        };

        /// <summary>
        /// Searches the discrete 4-cube rotation group for the orientation of <paramref name="origins"/>
        /// whose parallel projection has the fewest overlapping front-facing 3-cell pairs. Pure: does
        /// not mutate the input.
        /// </summary>
        public static Result BestRotation(int[][] origins, ParallelProjection proj)
        {
            var start = IntegerOps.Clone(origins);
            var visited = new HashSet<string> { CanonicalKey(start) };
            var queue = new Queue<(int[][] origins, List<(int, int)> path)>();
            queue.Enqueue((start, new List<(int, int)>()));

            Result best = null;
            int baseline = -1;
            while (queue.Count > 0)
            {
                var (cur, path) = queue.Dequeue();
                int overlaps = CountOverlappingFrontCellPairs(cur, proj);
                if (baseline < 0) baseline = overlaps; // first dequeued node is the input orientation

                if (best == null || overlaps < best.overlapPairs)
                    best = new Result { origins = cur, rotationPath = path, overlapPairs = overlaps };

                // BFS dequeues by non-decreasing path length, so the first zero-overlap orientation
                // is reached by the shortest move sequence — nothing can beat it.
                if (overlaps == 0) break;

                foreach (var (v, w) in Generators)
                {
                    var next = IntegerOps.Clone(cur);
                    var pivot = new IntegerCenter(next, asCubes: true);
                    foreach (var o in next) IntegerOps.RotateAsCenters(o, pivot, v, w);
                    if (visited.Add(CanonicalKey(next)))
                        queue.Enqueue((next, new List<(int, int)>(path) { (v, w) }));
                }
            }

            best.baselineOverlapPairs = baseline;
            return best;
        }

        /// <summary>
        /// Number of pairs of front-facing boundary 3-cells of the piece whose parallel projections
        /// have overlapping interiors. Translation-invariant, so it depends only on the orientation.
        /// </summary>
        public static int CountOverlappingFrontCellPairs(int[][] origins, ParallelProjection proj)
        {
            var cells = BuildFrontFacingProjectedCells(origins, proj);
            int count = 0;
            for (int i = 0; i < cells.Count; i++)
                for (int j = i + 1; j < cells.Count; j++)
                    if (ProjectedCellsOverlap(cells[i], cells[j])) count++;
            return count;
        }

        // ── projected 3-cell geometry ───────────────────────────────────────────

        // A boundary 3-cell (cube facet of a tesseract) projected to 3D: a parallelepiped given by its
        // 8 vertices plus its 3 edge directions and 3 face normals (precomputed for SAT).
        sealed class ProjCell
        {
            public double[][] verts;       // [8][3]
            public double[][] edges;       // [3][3] — the parallelepiped's 3 edge directions
            public double[][] faceNormals; // [3][3] — cross products of edge pairs
        }

        static List<ProjCell> BuildFrontFacingProjectedCells(int[][] origins, ParallelProjection proj)
        {
            var result = new List<ProjCell>();
            if (origins == null || origins.Length == 0) return result;

            // Lattice occupancy: a tesseract facet is on the boundary iff the neighbouring tesseract
            // across it is absent.
            var occupied = new HashSet<string>();
            foreach (var o in origins) occupied.Add(OriginKey(o));

            foreach (var o in origins)
            {
                for (int a = 0; a < 4; a++)
                {
                    for (int s = 0; s <= 1; s++)
                    {
                        var neighbour = IntegerOps.Clone(o);
                        neighbour[a] += s == 0 ? -1 : 1;
                        if (occupied.Contains(OriginKey(neighbour))) continue; // interior facet

                        // Outward normal: +axis a on the high (s=1) face, −axis a on the low (s=0) face.
                        int sign = s == 1 ? 1 : -1;
                        if (!IsFacedBy(proj, a, sign)) continue; // back-facing ⇒ hidden behind the solid

                        result.Add(ProjectFacet(o, a, s, proj));
                    }
                }
            }
            return result;
        }

        // Front-facing test for a boundary facet whose outward normal is `sign`·e_axis:
        // viewNormal·normal < 0.
        static bool IsFacedBy(ParallelProjection proj, int axis, int sign)
            => proj.viewNormal[axis] * sign < 0;

        static ProjCell ProjectFacet(int[] origin, int normalAxis, int side, ParallelProjection proj)
        {
            var faceOrigin = IntegerOps.Clone(origin);
            faceOrigin[normalAxis] += side;
            var spanAxes = new int[3];
            int si = 0;
            for (int k = 0; k < 4; k++) if (k != normalAxis) spanAxes[si++] = k;

            var verts = new double[8][];
            for (int mask = 0; mask < 8; mask++)
            {
                double x0 = faceOrigin[0], x1 = faceOrigin[1], x2 = faceOrigin[2], x3 = faceOrigin[3];
                for (int i = 0; i < 3; i++)
                {
                    if (((mask >> i) & 1) == 0) continue;
                    switch (spanAxes[i]) { case 0: x0++; break; case 1: x1++; break; case 2: x2++; break; default: x3++; break; }
                }
                verts[mask] = Project(proj, x0, x1, x2, x3);
            }

            // Edge directions emanate from vertex 0 along each span axis (mask bit i → vertex 1<<i).
            var e0 = Sub(verts[1], verts[0]);
            var e1 = Sub(verts[2], verts[0]);
            var e2 = Sub(verts[4], verts[0]);
            return new ProjCell
            {
                verts = verts,
                edges = new[] { e0, e1, e2 },
                faceNormals = new[] { Cross(e0, e1), Cross(e0, e2), Cross(e1, e2) },
            };
        }

        static double[] Project(ParallelProjection proj, double x0, double x1, double x2, double x3)
        {
            var v = proj.v;
            return new[]
            {
                v[0][0]*x0 + v[0][1]*x1 + v[0][2]*x2 + v[0][3]*x3,
                v[1][0]*x0 + v[1][1]*x1 + v[1][2]*x2 + v[1][3]*x3,
                v[2][0]*x0 + v[2][1]*x1 + v[2][2]*x2 + v[2][3]*x3,
            };
        }

        // ── separating-axis test for two parallelepipeds ────────────────────────
        // Complete axis set for two convex parallelepipeds: each one's 3 face normals plus the 9
        // pairwise edge cross products. If any axis separates the projected vertex sets, the interiors
        // are disjoint. Touching (gap within eps) counts as separated, not overlap.

        static bool ProjectedCellsOverlap(ProjCell a, ProjCell b)
        {
            foreach (var ax in a.faceNormals) if (SeparatesOn(a, b, ax)) return false;
            foreach (var ax in b.faceNormals) if (SeparatesOn(a, b, ax)) return false;
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    if (SeparatesOn(a, b, Cross(a.edges[i], b.edges[j]))) return false;
            return true;
        }

        static bool SeparatesOn(ProjCell a, ProjCell b, double[] axis)
        {
            double len2 = axis[0] * axis[0] + axis[1] * axis[1] + axis[2] * axis[2];
            if (len2 < 1e-18) return false; // degenerate axis carries no separation information
            double inv = 1.0 / System.Math.Sqrt(len2);

            double aMin = double.MaxValue, aMax = double.MinValue;
            double bMin = double.MaxValue, bMax = double.MinValue;
            foreach (var u in a.verts)
            {
                double d = (u[0] * axis[0] + u[1] * axis[1] + u[2] * axis[2]) * inv;
                if (d < aMin) aMin = d;
                if (d > aMax) aMax = d;
            }
            foreach (var u in b.verts)
            {
                double d = (u[0] * axis[0] + u[1] * axis[1] + u[2] * axis[2]) * inv;
                if (d < bMin) bMin = d;
                if (d > bMax) bMax = d;
            }
            const double eps = 1e-7;
            return aMax <= bMin + eps || bMax <= aMin + eps;
        }

        // ── small helpers ───────────────────────────────────────────────────────

        static double[] Sub(double[] u, double[] v) =>
            new[] { u[0] - v[0], u[1] - v[1], u[2] - v[2] };

        static double[] Cross(double[] u, double[] v) => new[]
        {
            u[1] * v[2] - u[2] * v[1],
            u[2] * v[0] - u[0] * v[2],
            u[0] * v[1] - u[1] * v[0],
        };

        static string OriginKey(int[] o) => $"{o[0]},{o[1]},{o[2]},{o[3]}";

        // Translation-normalized, order-independent fingerprint of a piece orientation, so the BFS
        // visits each of the (up to 192) distinct orientations exactly once.
        static string CanonicalKey(int[][] origins)
        {
            int dim = origins[0].Length;
            var min = new int[dim];
            for (int k = 0; k < dim; k++)
            {
                min[k] = int.MaxValue;
                foreach (var o in origins) if (o[k] < min[k]) min[k] = o[k];
            }
            var rows = new List<string>(origins.Length);
            foreach (var o in origins)
            {
                var sb = new StringBuilder();
                for (int k = 0; k < dim; k++)
                {
                    if (k > 0) sb.Append(',');
                    sb.Append(o[k] - min[k]);
                }
                rows.Add(sb.ToString());
            }
            rows.Sort(System.StringComparer.Ordinal);
            return string.Join(";", rows);
        }
    }
}
