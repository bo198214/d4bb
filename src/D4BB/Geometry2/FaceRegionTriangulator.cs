using System;
using System.Collections.Generic;
using D4BB.Geometry;

namespace D4BB.Geometry2 {

    /// Triangulates a FaceRegion (planar: one CCW outer ring + CW hole rings, see
    /// FaceRegion) for mesh building. The Weiler-Atherton pipeline produces concave
    /// outlines and hole rings, so the centroid-fan used for the convex
    /// Sutherland-Hodgman fragments does not apply. Method: holes are merged into the
    /// outer ring with keyhole bridges (Eberly's max-x visibility construction), then
    /// the resulting simple polygon is ear-clipped. Bridges exist only inside the
    /// triangulation — the FaceRegion is not modified.
    public static class FaceRegionTriangulator {

        /// Appends the region's triangles to `outVerts` / `outTris` (index triples into
        /// outVerts). Triangles wind CCW w.r.t. face.planeNormal. A degenerate face
        /// (null planeNormal) falls back to a simple fan.
        public static void Triangulate(FaceRegion face, List<Point> outVerts, List<int> outTris) {
            var outer = face.outer;
            if (outer == null || outer.Count < 3) return;
            if (face.planeNormal == null || face.holes == null || face.holes.Count == 0) {
                if (face.planeNormal == null) { Fan(outer, outVerts, outTris); return; }
                if (IsConvex(outer, face.planeNormal)) { Fan(outer, outVerts, outTris); return; }
            }

            // 2D projection: drop the dominant axis of planeNormal; swap the remaining
            // two if needed so the outer ring is CCW in 2D.
            int drop = 0; double max = Math.Abs(face.planeNormal.x[0]);
            for (int i = 1; i < 3; i++) {
                double a = Math.Abs(face.planeNormal.x[i]);
                if (a > max) { max = a; drop = i; }
            }
            int a0 = (drop + 1) % 3, a1 = (drop + 2) % 3;
            if (face.planeNormal.x[drop] < 0) (a0, a1) = (a1, a0);

            // Working polygon: parallel lists of 2D coords and out-vertex indices.
            var xs = new List<double>();
            var ys = new List<double>();
            var ids = new List<int>();
            void Append(Point p) {
                xs.Add(p.x[a0]); ys.Add(p.x[a1]);
                ids.Add(outVerts.Count);
                outVerts.Add(p);
            }
            foreach (var p in outer) Append(p);

            if (face.holes != null && face.holes.Count > 0) {
                // Merge holes by descending max-x so every keyhole bridge shoots into
                // geometry that is already part of the working polygon.
                var holes = new List<List<Point>>(face.holes);
                holes.Sort((h1, h2) => MaxX(h2, a0).CompareTo(MaxX(h1, a0)));
                foreach (var hole in holes)
                    MergeHole(hole, a0, a1, xs, ys, ids, outVerts, Append);
            }

            EarClip(xs, ys, ids, outTris);
        }

        // ── keyhole bridging ───────────────────────────────────────────────────

        static double MaxX(List<Point> ring, int a0) {
            double m = double.MinValue;
            foreach (var p in ring) if (p.x[a0] > m) m = p.x[a0];
            return m;
        }

        /// Splices `hole` (CW) into the working polygon via a zero-width bridge from the
        /// hole's max-x vertex M to a visible polygon vertex P (Eberly): shoot a +x ray
        /// from M, take the nearest edge intersection I, prefer the max-x endpoint of
        /// that edge as P, and if any reflex polygon vertex lies inside triangle (M,I,P),
        /// use the one with the smallest angle to the +x direction instead.
        static void MergeHole(List<Point> hole, int a0, int a1,
                              List<double> xs, List<double> ys, List<int> ids,
                              List<Point> outVerts, Action<Point> append) {
            int nh = hole.Count;
            if (nh < 3) return;
            int mIdx = 0;
            for (int i = 1; i < nh; i++) if (hole[i].x[a0] > hole[mIdx].x[a0]) mIdx = i;
            double mx = hole[mIdx].x[a0], my = hole[mIdx].x[a1];

            // Nearest +x ray intersection with polygon edges.
            int n = xs.Count;
            int bestEdge = -1; double bestX = double.MaxValue;
            for (int i = 0; i < n; i++) {
                int j = (i + 1) % n;
                double yi = ys[i] - my, yj = ys[j] - my;
                if ((yi > 0) == (yj > 0)) continue;
                double t = yi / (yi - yj);
                double x = xs[i] + t * (xs[j] - xs[i]);
                if (x >= mx - 1e-12 && x < bestX) { bestX = x; bestEdge = i; }
            }
            int candidate;
            if (bestEdge < 0) {
                // No intersection (should not happen for a hole inside the outer ring) —
                // bridge to the overall max-x polygon vertex as a last resort.
                candidate = 0;
                for (int i = 1; i < n; i++) if (xs[i] > xs[candidate]) candidate = i;
            } else {
                int e0 = bestEdge, e1 = (bestEdge + 1) % n;
                candidate = xs[e0] > xs[e1] ? e0 : e1;
                // Reflex vertices inside triangle (M, I, P) would be crossed by the
                // bridge — pick the one closest in angle to the +x direction.
                double ix = bestX, iy = my;
                double bestCos = double.MinValue;
                int blocked = -1;
                for (int v = 0; v < n; v++) {
                    if (v == e0 || v == e1) continue;
                    if (!InTriangle(xs[v], ys[v], mx, my, ix, iy, xs[candidate], ys[candidate])) continue;
                    double dx = xs[v] - mx, dy = ys[v] - my;
                    double len = Math.Sqrt(dx * dx + dy * dy);
                    if (len < 1e-12) continue;
                    double cos = dx / len;
                    if (cos > bestCos) { bestCos = cos; blocked = v; }
                }
                if (blocked >= 0) candidate = blocked;
            }

            // Splice: ... P, [M, hole vertices CW, M], P, ... (P and M duplicated).
            var nxs = new List<double>(n + nh + 2);
            var nys = new List<double>(n + nh + 2);
            var nids = new List<int>(n + nh + 2);
            for (int i = 0; i <= candidate; i++) { nxs.Add(xs[i]); nys.Add(ys[i]); nids.Add(ids[i]); }
            for (int k = 0; k <= nh; k++) {
                var p = hole[(mIdx + k) % nh];
                nxs.Add(p.x[a0]); nys.Add(p.x[a1]);
                nids.Add(outVerts.Count); outVerts.Add(p);
            }
            nxs.Add(xs[candidate]); nys.Add(ys[candidate]); nids.Add(ids[candidate]);
            for (int i = candidate + 1; i < n; i++) { nxs.Add(xs[i]); nys.Add(ys[i]); nids.Add(ids[i]); }
            xs.Clear(); xs.AddRange(nxs);
            ys.Clear(); ys.AddRange(nys);
            ids.Clear(); ids.AddRange(nids);
        }

        // ── ear clipping ───────────────────────────────────────────────────────

        static void EarClip(List<double> xs, List<double> ys, List<int> ids, List<int> outTris) {
            int n = xs.Count;
            var next = new int[n];
            var prev = new int[n];
            for (int i = 0; i < n; i++) { next[i] = (i + 1) % n; prev[i] = (i + n - 1) % n; }
            int remaining = n;
            int cur = 0;
            int sinceLastEar = 0;
            bool relaxed = false;
            const double eps = 1e-12;

            while (remaining > 3) {
                int p = prev[cur], q = next[cur];
                double cross = Cross(xs[p], ys[p], xs[cur], ys[cur], xs[q], ys[q]);
                bool convex = relaxed ? cross > -eps : cross > eps;
                bool ear = convex && NoVertexInside(xs, ys, next, q, p, cur);
                if (ear) {
                    if (cross > eps) {   // skip zero-area (collinear) ears entirely
                        outTris.Add(ids[p]); outTris.Add(ids[cur]); outTris.Add(ids[q]);
                    }
                    next[p] = q; prev[q] = p;
                    remaining--;
                    cur = q;
                    sinceLastEar = 0;
                    relaxed = false;
                } else {
                    cur = q;
                    if (++sinceLastEar > remaining) {
                        if (!relaxed) { relaxed = true; sinceLastEar = 0; }
                        else break;   // no ear even relaxed — numerically degenerate rest, stop
                    }
                }
            }
            if (remaining == 3) {
                int p = prev[cur], q = next[cur];
                double cross = Cross(xs[p], ys[p], xs[cur], ys[cur], xs[q], ys[q]);
                if (cross > eps) {
                    outTris.Add(ids[p]); outTris.Add(ids[cur]); outTris.Add(ids[q]);
                }
            }
        }

        /// True iff no remaining vertex lies inside (or on the diagonal edges of) the
        /// candidate ear triangle (p, cur, q). Vertices coincident with a corner (the
        /// keyhole-bridge duplicates) don't block.
        static bool NoVertexInside(List<double> xs, List<double> ys, int[] next,
                                   int start, int p, int cur) {
            int q = start;
            for (int v = next[q]; v != p; v = next[v]) {
                if (Coincident(xs, ys, v, p) || Coincident(xs, ys, v, cur) || Coincident(xs, ys, v, q))
                    continue;
                if (InTriangle(xs[v], ys[v], xs[p], ys[p], xs[cur], ys[cur], xs[q], ys[q]))
                    return false;
            }
            return true;
        }

        static bool Coincident(List<double> xs, List<double> ys, int a, int b) {
            return Math.Abs(xs[a] - xs[b]) < AOP.ERR && Math.Abs(ys[a] - ys[b]) < AOP.ERR;
        }

        static double Cross(double ax, double ay, double bx, double by, double cx, double cy) {
            return (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
        }

        /// Inside-or-on test with a small tolerance pulling the boundary inward, so
        /// points exactly on a triangle edge count as blocking (a diagonal through
        /// them would split the polygon incorrectly).
        static bool InTriangle(double px, double py,
                               double ax, double ay, double bx, double by, double cx, double cy) {
            const double eps = 1e-12;
            double d1 = Cross(ax, ay, bx, by, px, py);
            double d2 = Cross(bx, by, cx, cy, px, py);
            double d3 = Cross(cx, cy, ax, ay, px, py);
            bool hasNeg = d1 < -eps || d2 < -eps || d3 < -eps;
            bool hasPos = d1 > eps || d2 > eps || d3 > eps;
            return !(hasNeg && hasPos);
        }

        static bool IsConvex(List<Point> ring, Point planeNormal) {
            int n = ring.Count;
            var cross = new Point(3);
            for (int i = 0; i < n; i++) {
                AOP.CrossDiff(ring[i], ring[(i + 1) % n], ring[(i + 2) % n], cross);
                if (cross.sc(planeNormal) < -AOP.ERR) return false;
            }
            return true;
        }

        static void Fan(List<Point> ring, List<Point> outVerts, List<int> outTris) {
            int baseIdx = outVerts.Count;
            foreach (var p in ring) outVerts.Add(p);
            for (int k = 1; k + 1 < ring.Count; k++) {
                outTris.Add(baseIdx);
                outTris.Add(baseIdx + k);
                outTris.Add(baseIdx + k + 1);
            }
        }
    }
}
