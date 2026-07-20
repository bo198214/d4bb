using System.Collections.Generic;
using D4BB.Geometry;

namespace D4BB.Geometry2 {

    /// Computes a separating hyperplane between two convex polytopes (given by their vertex
    /// sets) using the <b>GJK</b> algorithm — the standard method for the distance between two
    /// convex bodies. Dimension-generic (works for the 4D cells of a PolyhedralComplex4d).
    ///
    /// GJK searches the Minkowski difference D = A ⊖ B = { a − b } for the point closest to the
    /// origin. Two convex sets are disjoint iff 0 ∉ D; then the closest point v of D to the origin
    /// is a witness: D lies entirely in the half-space { x : v·x ≥ v·v }, so for every a∈A, b∈B
    ///   v·a − v·b = v·(a−b) ≥ v·v > 0,
    /// i.e. the unit normal n = v/|v| strictly separates A (larger n·x) from B (smaller n·x). This
    /// is exactly the separating (hyper)plane RenderPipeline2 needs when neither cell's own
    /// supporting hyperplane resolves the pair (both mutually straddle), instead of giving up.
    public static class ConvexSeparation {

        const double Eps = 1e-9;
        const int MaxIter = 40;

        /// True and fills `normal` (unit) + `pointOnPlane` (midway between the hulls) iff conv(A)
        /// and conv(B) are disjoint; the plane { x : normal·x = normal·pointOnPlane } then has all
        /// of A strictly on the normal-positive side and all of B strictly on the negative side.
        /// False if the hulls intersect or merely touch (no strict separation within tolerance).
        /// Diagnostic counter: how many times the pairwise pipeline had to fall back to a computed
        /// separating hyperplane (i.e. neither cell's own supporting hyperplane resolved the pair).
        /// Tests assert this is actually exercised; also a cheap gauge of how often WA needs it.
        public static long FallbackInvocations { get; private set; }

        public static bool TrySeparatingHyperplane(
                IReadOnlyList<Point> A, IReadOnlyList<Point> B,
                out Point normal, out Point pointOnPlane) {
            FallbackInvocations++;
            normal = null; pointOnPlane = null;
            if (A == null || B == null || A.Count == 0 || B.Count == 0) return false;
            int dim = A[0].x.Length;

            var a = ToArrays(A);
            var b = ToArrays(B);

            // GJK distance sub-algorithm: maintain a simplex of Minkowski-difference points and
            // walk its closest-point-to-origin toward the true minimum.
            var simplex = new List<double[]>(dim + 1);
            var v = Support(a, b, InitialDir(dim));   // any point of D
            simplex.Add(v);

            for (int iter = 0; iter < MaxIter; iter++) {
                // Farthest support toward the origin from the current closest point v.
                var w = Support(a, b, Negate(v));
                // Converged: the support in direction -v does not get closer to the origin than v.
                double vv = Dot(v, v);
                if (vv - Dot(v, w) <= 1e-12 * (vv + 1e-30)) break;
                simplex.Add(w);
                v = ClosestPointReduce(simplex);
                if (Dot(v, v) < Eps * Eps) return false;   // origin inside D ⇒ hulls intersect
            }

            double dist = System.Math.Sqrt(Dot(v, v));
            if (dist < Eps) return false;                  // touching / not strictly separable

            var n = new double[dim];
            for (int k = 0; k < dim; k++) n[k] = v[k] / dist;

            // Plane offset midway between the two hulls' extents along n.
            double minA = double.MaxValue, maxB = double.MinValue;
            foreach (var p in a) { double d = Dot(n, p); if (d < minA) minA = d; }
            foreach (var p in b) { double d = Dot(n, p); if (d > maxB) maxB = d; }
            if (minA <= maxB) return false;                // no strict gap (numeric guard)
            double mid = 0.5 * (minA + maxB);

            normal = new Point(n);
            var pt = new double[dim];
            for (int k = 0; k < dim; k++) pt[k] = n[k] * mid;
            pointOnPlane = new Point(pt);
            return true;
        }

        // ── GJK support & closest-point helpers ─────────────────────────────────

        /// Support point of D = A⊖B in direction `dir`: (argmax_{p∈A} p·dir) − (argmax_{q∈B} −q·dir).
        static double[] Support(double[][] a, double[][] b, double[] dir) {
            var pa = a[ArgMaxDot(a, dir)];
            var pb = b[ArgMinDot(b, dir)];   // argmax q·(−dir) = argmin q·dir
            int dim = dir.Length;
            var r = new double[dim];
            for (int k = 0; k < dim; k++) r[k] = pa[k] - pb[k];
            return r;
        }

        static int ArgMaxDot(double[][] pts, double[] dir) {
            int best = 0; double bestD = double.MinValue;
            for (int i = 0; i < pts.Length; i++) { double d = Dot(pts[i], dir); if (d > bestD) { bestD = d; best = i; } }
            return best;
        }
        static int ArgMinDot(double[][] pts, double[] dir) {
            int best = 0; double bestD = double.MaxValue;
            for (int i = 0; i < pts.Length; i++) { double d = Dot(pts[i], dir); if (d < bestD) { bestD = d; best = i; } }
            return best;
        }

        /// Closest point of the convex hull of `simplex` to the origin; also reduces `simplex`
        /// in place to the minimal sub-simplex (the face) whose affine hull carries that point
        /// (Johnson's sub-distance, done by brute-force over subsets — the simplex has ≤ dim+1
        /// points so ≤ 2^(dim+1) subsets, trivially cheap in 4D).
        static double[] ClosestPointReduce(List<double[]> simplex) {
            int m = simplex.Count;
            double bestNorm2 = double.MaxValue;
            double[] bestPoint = null;
            int bestSubset = 0;
            for (int mask = 1; mask < (1 << m); mask++) {
                var subset = new List<double[]>();
                for (int i = 0; i < m; i++) if ((mask & (1 << i)) != 0) subset.Add(simplex[i]);
                if (!ClosestOnAffineHull(subset, out var pt, out bool interior)) continue;
                if (!interior) continue;
                double n2 = Dot(pt, pt);
                if (n2 < bestNorm2) { bestNorm2 = n2; bestPoint = pt; bestSubset = mask; }
            }
            if (bestPoint == null) {                // numeric fallback: keep the nearest vertex
                int vi = ArgMinNorm(simplex);
                bestPoint = simplex[vi]; bestSubset = 1 << vi;
            }
            var reduced = new List<double[]>();
            for (int i = 0; i < m; i++) if ((bestSubset & (1 << i)) != 0) reduced.Add(simplex[i]);
            simplex.Clear(); simplex.AddRange(reduced);
            return bestPoint;
        }

        /// Closest point of the AFFINE hull of `pts` to the origin, with a flag whether it lies
        /// inside the simplex (all barycentric coordinates ≥ −tol). Returns false for a degenerate
        /// (rank-deficient) subset.
        static bool ClosestOnAffineHull(List<double[]> pts, out double[] point, out bool interior) {
            point = null; interior = false;
            int k = pts.Count, dim = pts[0].Length;
            if (k == 1) { point = (double[])pts[0].Clone(); interior = true; return true; }

            // x = P0 + Σ μ_i e_i,  e_i = P_i − P0.  Minimize |x|²  ⇒  G μ = −c,  G_ij = e_i·e_j, c_i = e_i·P0.
            var p0 = pts[0];
            int r = k - 1;
            var e = new double[r][];
            for (int i = 0; i < r; i++) { e[i] = new double[dim]; for (int d = 0; d < dim; d++) e[i][d] = pts[i + 1][d] - p0[d]; }
            var G = new double[r][];
            var c = new double[r];
            for (int i = 0; i < r; i++) {
                G[i] = new double[r];
                for (int j = 0; j < r; j++) G[i][j] = Dot(e[i], e[j]);
                c[i] = -Dot(e[i], p0);
            }
            if (!SolveLinear(G, c, out var mu)) return false;   // degenerate subset

            var x = (double[])p0.Clone();
            double sumMu = 0;
            for (int i = 0; i < r; i++) { for (int d = 0; d < dim; d++) x[d] += mu[i] * e[i][d]; sumMu += mu[i]; }
            double lambda0 = 1.0 - sumMu;
            const double tol = 1e-10;
            interior = lambda0 >= -tol;
            for (int i = 0; i < r && interior; i++) if (mu[i] < -tol) interior = false;
            point = x;
            return true;
        }

        /// Gaussian elimination with partial pivoting for a small dense system A·x = rhs.
        static bool SolveLinear(double[][] A, double[] rhs, out double[] x) {
            int n = rhs.Length;
            x = new double[n];
            var M = new double[n][];
            for (int i = 0; i < n; i++) { M[i] = new double[n + 1]; for (int j = 0; j < n; j++) M[i][j] = A[i][j]; M[i][n] = rhs[i]; }
            for (int col = 0; col < n; col++) {
                int piv = col; double best = System.Math.Abs(M[col][col]);
                for (int rr = col + 1; rr < n; rr++) { double val = System.Math.Abs(M[rr][col]); if (val > best) { best = val; piv = rr; } }
                if (best < 1e-14) return false;             // singular ⇒ degenerate simplex
                (M[col], M[piv]) = (M[piv], M[col]);
                for (int rr = 0; rr < n; rr++) {
                    if (rr == col) continue;
                    double f = M[rr][col] / M[col][col];
                    for (int j = col; j <= n; j++) M[rr][j] -= f * M[col][j];
                }
            }
            for (int i = 0; i < n; i++) x[i] = M[i][n] / M[i][i];
            return true;
        }

        // ── small vector utilities on double[] ──────────────────────────────────

        static double[][] ToArrays(IReadOnlyList<Point> pts) {
            var r = new double[pts.Count][];
            for (int i = 0; i < pts.Count; i++) r[i] = (double[])pts[i].x.Clone();
            return r;
        }
        static double Dot(double[] a, double[] b) { double s = 0; for (int k = 0; k < a.Length; k++) s += a[k] * b[k]; return s; }
        static double[] Negate(double[] a) { var r = new double[a.Length]; for (int k = 0; k < a.Length; k++) r[k] = -a[k]; return r; }
        static double[] InitialDir(int dim) { var d = new double[dim]; d[0] = 1; return d; }
        static int ArgMinNorm(List<double[]> pts) {
            int best = 0; double bestN = double.MaxValue;
            for (int i = 0; i < pts.Count; i++) { double n = Dot(pts[i], pts[i]); if (n < bestN) { bestN = n; best = i; } }
            return best;
        }
    }
}
