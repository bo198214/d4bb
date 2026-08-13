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
    /// A 90° rotation acts in the (v,w) coordinate plane and leaves the complementary axes
    /// pointwise fixed, so a rotating unit cell can only ever collide with obstacle cells in
    /// the same complementary-axes "fiber" (identical coordinates on all axes outside {v,w});
    /// what remains is a 2D question about the rotating unit square.
    ///
    /// The moving body is deliberately NOT the full square but its INSCRIBED DISK (radius ½
    /// around the cell center). A rotating unit square reaches √2/2 from its center, so its
    /// corners unavoidably sweep √2/2 − 1/2 ≈ 0.207 deep into every face-touching in-plane
    /// neighbor and across a flush play-field wall — with the full square, a piece touching
    /// anything in the rotation plane could never turn, not even in place. The inscribed disk
    /// forgives exactly those corner lenses and nothing more: wherever the disk stays out of
    /// a cell, the square's penetration is provably ≤ √2/2 − 1/2, while a genuine pass-through
    /// brings the disk in and still blocks. Equivalently: during (only) the turn, pieces
    /// behave as if their in-plane cross-section had fully rounded corners; the end pose is
    /// the exact square and is checked separately by the caller.
    ///
    /// The test is exact, not sampled: the minimal distance from the center's arc to a cell
    /// is attained at one of finitely many closed-form candidate angles — the arc endpoints,
    /// the crossings of the cell's four grid lines (the boundaries of its nearest-feature
    /// regions), the center path's axis extrema, and the radial alignments with the cell's
    /// corners. Contact semantics stay open, matching the end-pose checks: a disk grazing a
    /// cell or sliding flush along a wall (distance exactly ½) is legal — so a face-to-face
    /// neighbor in the fixed plane slides along the rotating piece for the entire turn
    /// without blocking it, and any face-touching in-plane neighbor tolerates the turn.
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
        /// The 2D footprint of one quarter turn: every lattice cell whose interior the
        /// INSCRIBED DISK (radius ½ around the center) of the unit square with origin
        /// twice-offset (tox, toy) to the pivot passes through while rotating CCW by 90°
        /// around the pivot — start and end cell included. Cells the square meets only with
        /// its corner lenses outside the disk are deliberately absent (see the class doc).
        /// Cells are returned as origin twice-offsets to the pivot. The result is cached and
        /// shared; do not mutate.
        /// </summary>
        public static HashSet<(int, int)> Footprint2d(int tox, int toy)
        {
            lock (footprints)
            {
                if (footprints.TryGetValue((tox, toy), out var cached)) return cached;
            }

            double ax = tox * 0.5, ay = toy * 0.5; // square [ax,ax+1]×[ay,ay+1], pivot at 0
            double cx = ax + 0.5, cy = ay + 0.5;   // its center — the disk's carrier
            double rc = Math.Sqrt(cx * cx + cy * cy);
            double phi = Math.Atan2(cy, cx);
            double reach = rc + 0.5;

            var result = new HashSet<(int, int)>();
            int mLo = (int)Math.Floor(-reach - ax) - 1, mHi = (int)Math.Ceiling(reach - ax) + 1;
            int nLo = (int)Math.Floor(-reach - ay) - 1, nHi = (int)Math.Ceiling(reach - ay) + 1;
            for (int m = mLo; m <= mHi; m++)
                for (int n = nLo; n <= nHi; n++)
                {
                    double bx = ax + m, by = ay + n;
                    // Cells whose nearest point is at or beyond the disk's outermost reach
                    // are at most grazed (measure-zero contact) — never interior-overlapped.
                    double dx = AxisDistToZero(bx, bx + 1), dy = AxisDistToZero(by, by + 1);
                    if (dx * dx + dy * dy >= reach * reach) continue;
                    if (SweptDiskMeetsCell(rc, phi, bx, by))
                        result.Add((tox + 2 * m, toy + 2 * n));
                }

            lock (footprints) { footprints[(tox, toy)] = result; }
            return result;
        }

        // Distance of the interval [lo, hi] to 0 — equivalently, of a point to an interval.
        private static double AxisDistToZero(double lo, double hi)
            => lo > 0 ? lo : (hi < 0 ? -hi : 0);

        // Does the open disk of radius ½ around c(θ) = rc·(cos(phi+θ), sin(phi+θ)), θ ∈ [0, π/2],
        // meet the open interior of [bx,bx+1]×[by,by+1] at some θ? Equivalently: does the
        // center's arc come strictly closer than ½ to the cell? The distance function
        // θ ↦ dist(c(θ), cell) is piecewise smooth; its pieces live in the cell's
        // nearest-feature regions (delimited by the four extended grid lines) and are, per
        // piece, a distance to a fixed corner point or edge line. Its global minimum over the
        // closed interval is therefore attained at one of: the interval endpoints, a grid-line
        // crossing, an axis extremum of the arc, or a radial alignment with a corner — all
        // closed-form; evaluating the distance at those candidates decides exactly.
        private static bool SweptDiskMeetsCell(double rc, double phi, double bx, double by)
        {
            var thetas = new List<double> { 0, HalfPi };
            // Crossings of the cell's four (extended) grid lines:
            // x(θ) = rc·cos(φ+θ), y(θ) = rc·cos(φ−π/2+θ).
            AddCosEvents(thetas, rc, phi, bx);
            AddCosEvents(thetas, rc, phi, bx + 1);
            AddCosEvents(thetas, rc, phi - HalfPi, by);
            AddCosEvents(thetas, rc, phi - HalfPi, by + 1);
            // Axis extrema of the arc (minimum candidates inside an edge-line region).
            for (int k = 0; k < 4; k++)
            {
                double t = (k * HalfPi - phi) % TwoPi;
                if (t < 0) t += TwoPi;
                if (t > 0 && t < HalfPi) thetas.Add(t);
            }
            // Radial alignments with the cell's corners (minimum candidates inside a
            // corner region: there dist(c(θ), P) is extremal where c(θ) is radially in
            // line with P).
            for (int i = 0; i <= 1; i++)
                for (int j = 0; j <= 1; j++)
                {
                    double px = bx + i, py = by + j;
                    if (px * px + py * py < 1e-18) continue;
                    double t = (Math.Atan2(py, px) - phi) % TwoPi;
                    if (t < 0) t += TwoPi;
                    if (t > 0 && t < HalfPi) thetas.Add(t);
                }
            foreach (var t in thetas)
            {
                double x = rc * Math.Cos(phi + t), y = rc * Math.Sin(phi + t);
                double dx = AxisDistToZero(bx - x, bx + 1 - x);
                double dy = AxisDistToZero(by - y, by + 1 - y);
                // Strictly closer than ½ blocks; exact tangency (flush wall/neighbor
                // contact, distance exactly ½) is legal open-contact — the epsilon only
                // absorbs float rounding of the exactly-representable tangency cases.
                if (dx * dx + dy * dy < 0.25 - 1e-9) return true;
            }
            return false;
        }

        // Solutions θ ∈ (0, π/2) of r·cos(phi0 + θ) = line.
        private static void AddCosEvents(List<double> events, double r, double phi0, double line)
        {
            double c = line / r;
            if (c < -1 || c > 1) return;
            double alpha = Math.Acos(c);
            for (int u = 0; u <= 1; u++)
            {
                // phi0 + θ ≡ ±alpha (mod 2π)  →  θ ≡ ±alpha − phi0 (mod 2π)
                double t = ((u == 0 ? alpha : -alpha) - phi0) % TwoPi;
                if (t < 0) t += TwoPi;
                if (t > 0 && t < HalfPi) events.Add(t);
            }
        }
    }
}
