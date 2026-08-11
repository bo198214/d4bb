using System;
using System.Collections.Generic;
using D4BB.Comb;

namespace D4BB.Game
{
    /// <summary>
    /// Exact swept-collision test for the 90° lattice rotations of <see cref="GameLevel"/>:
    /// does a piece collide with obstacle cells or leave the boundary DURING the quarter turn,
    /// not just in its end pose? Guards against "quantum rotation" (tunneling through blocked
    /// intermediate poses); enabled per level via <see cref="Objective.quantumRotation"/> = false.
    ///
    /// The test is exact, not sampled. A 90° rotation acts in the (v,w) coordinate plane and
    /// leaves the complementary axes pointwise fixed, so a rotating unit cell can only ever
    /// collide with obstacle cells in the same complementary-axes "fiber" (identical
    /// coordinates on all axes outside {v,w}); what remains is a 2D question: does a unit
    /// square, rotating by 90° around the pivot, overlap a static unit square at some
    /// intermediate angle? The truth value of that predicate changes only at contact events
    /// (a corner of one square crossing an edge line of the other), all of which are
    /// closed-form circle/line intersections; one strict separating-axis test between each
    /// pair of consecutive events decides the whole interval.
    ///
    /// Collision means overlapping INTERIORS. Boundary contact of any extent or duration is
    /// legal, matching the end-pose semantics: a face-to-face neighbor in the fixed plane
    /// slides along the rotating piece for the entire turn without blocking it, and a
    /// touching in-plane neighbor on the trailing side is only grazed in the start instant.
    /// </summary>
    public static class RotationSweep
    {
        // A footprint depends only on the rotating cell's offset to the pivot in the rotation
        // plane (the rotation sense is fixed: IntegerOps.Rotate(v, w) is (x,y) → (−y,x), CCW
        // in the (v,w) frame — the caller encodes the other sense by swapping v and w, which
        // swaps the two plane coordinates and thus mirrors the key and the result). Keys and
        // members are twice-coordinates (IntegerCenter convention) of cell origins relative
        // to the pivot: (2·origin − pivotTwice) on the two plane axes.
        private static readonly Dictionary<(int, int), HashSet<(int, int)>> footprints = new();

        private const double HalfPi = Math.PI / 2;
        private const double TwoPi = 2 * Math.PI;

        /// <summary>
        /// Why the quarter turn of <paramref name="rotatingOrigins"/> (PRE-rotation cube
        /// origins) in the (v,w) plane around <paramref name="pivot"/> is blocked mid-motion,
        /// or None. <paramref name="obstacleOrigins"/> are the cube origins of all OTHER
        /// pieces — the rotating piece moves rigidly and cannot collide with itself.
        /// <paramref name="boundaryMinMax"/> as in <see cref="Objective.boundary_min_max"/>
        /// (null tolerated: unbounded). The endpoints of the sweep contribute nothing new:
        /// the start pose is legal by the game invariant and the caller checks the end pose
        /// separately, so a report from here always means a genuinely intermediate collision.
        /// </summary>
        public static MoveBlockReason Check(int[][] rotatingOrigins, int v, int w,
            IntegerCenter pivot, IEnumerable<int[]> obstacleOrigins, int[][] boundaryMinMax)
        {
            var ptwice = pivot.Twice();
            int pv = ptwice[v], pw = ptwice[w];
            var cellFootprints = new List<(int[] origin, HashSet<(int, int)> fp)>(rotatingOrigins.Length);
            foreach (var o in rotatingOrigins)
                cellFootprints.Add((o, Footprint2d(2 * o[v] - pv, 2 * o[w] - pw)));

            // Boundary first, mirroring the end-pose check order in GameLevel.RotateSelected.
            // Only the two plane axes can newly violate the boundary — the fixed axes keep
            // their (legal) start values.
            bool hasBounds = boundaryMinMax != null && boundaryMinMax.Length >= 2
                && boundaryMinMax[0] != null && boundaryMinMax[1] != null;
            if (hasBounds)
            {
                foreach (var (_, fp) in cellFootprints)
                    foreach (var (tx, ty) in fp)
                    {
                        int bv = (tx + pv) / 2, bw = (ty + pw) / 2; // exact: matching parity
                        if (bv < boundaryMinMax[0][v] || bv + 1 > boundaryMinMax[1][v]
                            || bw < boundaryMinMax[0][w] || bw + 1 > boundaryMinMax[1][w])
                            return MoveBlockReason.OutOfBoundary;
                    }
            }

            foreach (var b in obstacleOrigins)
            {
                foreach (var (o, fp) in cellFootprints)
                {
                    bool sameFiber = true;
                    for (int a = 0; a < o.Length && sameFiber; a++)
                        if (a != v && a != w && b[a] != o[a]) sameFiber = false;
                    if (!sameFiber) continue;
                    if (fp.Contains((2 * b[v] - pv, 2 * b[w] - pw)))
                        return MoveBlockReason.Overlap;
                }
            }
            return MoveBlockReason.None;
        }

        /// <summary>
        /// The 2D footprint of one quarter turn: every lattice cell whose interior the unit
        /// square with origin twice-offset (tox, toy) to the pivot passes through while
        /// rotating CCW by 90° around the pivot — start and end cell included. Cells are
        /// returned as origin twice-offsets to the pivot. The result is cached and shared;
        /// do not mutate.
        /// </summary>
        public static HashSet<(int, int)> Footprint2d(int tox, int toy)
        {
            lock (footprints)
            {
                if (footprints.TryGetValue((tox, toy), out var cached)) return cached;
            }

            double ax = tox * 0.5, ay = toy * 0.5; // square [ax,ax+1]×[ay,ay+1], pivot at 0
            double rmaxSq = 0;
            for (int i = 0; i <= 1; i++)
                for (int j = 0; j <= 1; j++)
                    rmaxSq = Math.Max(rmaxSq, (ax + i) * (ax + i) + (ay + j) * (ay + j));
            double rmax = Math.Sqrt(rmaxSq);

            var result = new HashSet<(int, int)>();
            int mLo = (int)Math.Floor(-rmax - ax) - 1, mHi = (int)Math.Ceiling(rmax - ax) + 1;
            int nLo = (int)Math.Floor(-rmax - ay) - 1, nHi = (int)Math.Ceiling(rmax - ay) + 1;
            for (int m = mLo; m <= mHi; m++)
                for (int n = nLo; n <= nHi; n++)
                {
                    double bx = ax + m, by = ay + n;
                    // Cells whose nearest point is at or beyond the outermost corner's radius
                    // are at most grazed (measure-zero contact) — never interior-overlapped.
                    double dx = AxisDistToZero(bx, bx + 1), dy = AxisDistToZero(by, by + 1);
                    if (dx * dx + dy * dy >= rmaxSq) continue;
                    if (SweptInteriorOverlap(ax, ay, bx, by))
                        result.Add((tox + 2 * m, toy + 2 * n));
                }

            lock (footprints) { footprints[(tox, toy)] = result; }
            return result;
        }

        private static double AxisDistToZero(double lo, double hi)
            => lo > 0 ? lo : (hi < 0 ? -hi : 0);

        // Does the open interior of [ax,ax+1]×[ay,ay+1], rotating CCW around the origin by
        // θ ∈ [0, π/2], meet the open interior of the static [bx,bx+1]×[by,by+1] at some θ?
        private static bool SweptInteriorOverlap(double ax, double ay, double bx, double by)
        {
            var events = new List<double> { 0, HalfPi };
            for (int i = 0; i <= 1; i++)
                for (int j = 0; j <= 1; j++)
                {
                    // Corner of the rotating square crossing a grid line of the static one:
                    // x(θ) = r·cos(φ+θ), y(θ) = r·cos(φ−π/2+θ).
                    double cx = ax + i, cy = ay + j;
                    double r = Math.Sqrt(cx * cx + cy * cy);
                    if (r >= 1e-9)
                    {
                        double phi = Math.Atan2(cy, cx);
                        AddCosEvents(events, r, phi, +1, bx);
                        AddCosEvents(events, r, phi, +1, bx + 1);
                        AddCosEvents(events, r, phi - HalfPi, +1, by);
                        AddCosEvents(events, r, phi - HalfPi, +1, by + 1);
                    }
                    // Corner of the static square crossing an edge line of the rotating one
                    // (in the rotating frame the static corner turns by −θ).
                    double qx = bx + i, qy = by + j;
                    double s = Math.Sqrt(qx * qx + qy * qy);
                    if (s >= 1e-9)
                    {
                        double psi = Math.Atan2(qy, qx);
                        AddCosEvents(events, s, psi, -1, ax);
                        AddCosEvents(events, s, psi, -1, ax + 1);
                        AddCosEvents(events, s, psi - HalfPi, -1, ay);
                        AddCosEvents(events, s, psi - HalfPi, -1, ay + 1);
                    }
                }
            events.Sort();
            for (int k = 0; k + 1 < events.Count; k++)
            {
                if (events[k + 1] - events[k] < 1e-12) continue;
                if (InteriorOverlapAt(ax, ay, bx, by, (events[k] + events[k + 1]) * 0.5))
                    return true;
            }
            return false;
        }

        // Solutions θ ∈ (0, π/2) of r·cos(phi0 + sign·θ) = line.
        private static void AddCosEvents(List<double> events, double r, double phi0, int sign, double line)
        {
            double c = line / r;
            if (c < -1 || c > 1) return;
            double alpha = Math.Acos(c);
            for (int u = 0; u <= 1; u++)
            {
                // phi0 + sign·θ ≡ ±alpha (mod 2π)  →  θ ≡ sign·(±alpha − phi0) (mod 2π)
                double t = sign * ((u == 0 ? alpha : -alpha) - phi0) % TwoPi;
                if (t < 0) t += TwoPi;
                if (t > 0 && t < HalfPi) events.Add(t);
            }
        }

        // Strict separating-axis test: do the open interiors of the CCW-by-θ-rotated square
        // [ax,ax+1]×[ay,ay+1] and the static square [bx,bx+1]×[by,by+1] intersect? Axes are
        // the two squares' edge normals; on the rotating square's own axes its projection is
        // just the original [ax,ax+1] / [ay,ay+1]. Strict inequalities implement the
        // open-interior semantics (touching does not count).
        private static bool InteriorOverlapAt(double ax, double ay, double bx, double by, double theta)
        {
            double c = Math.Cos(theta), s = Math.Sin(theta);
            double minRx = double.MaxValue, maxRx = double.MinValue;
            double minRy = double.MaxValue, maxRy = double.MinValue;
            double minBu = double.MaxValue, maxBu = double.MinValue;
            double minBv = double.MaxValue, maxBv = double.MinValue;
            for (int i = 0; i <= 1; i++)
                for (int j = 0; j <= 1; j++)
                {
                    double x = (ax + i) * c - (ay + j) * s;
                    double y = (ax + i) * s + (ay + j) * c;
                    if (x < minRx) minRx = x;
                    if (x > maxRx) maxRx = x;
                    if (y < minRy) minRy = y;
                    if (y > maxRy) maxRy = y;
                    double bu = (bx + i) * c + (by + j) * s;
                    double bv = -(bx + i) * s + (by + j) * c;
                    if (bu < minBu) minBu = bu;
                    if (bu > maxBu) maxBu = bu;
                    if (bv < minBv) minBv = bv;
                    if (bv > maxBv) maxBv = bv;
                }
            if (!(minRx < bx + 1 && bx < maxRx)) return false;
            if (!(minRy < by + 1 && by < maxRy)) return false;
            if (!(ax < maxBu && minBu < ax + 1)) return false;
            if (!(ay < maxBv && minBv < ay + 1)) return false;
            return true;
        }
    }
}
