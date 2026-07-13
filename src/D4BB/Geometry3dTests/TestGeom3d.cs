using System;
using System.Collections.Generic;
using D4BB.Geometry;
using D4BB.Geometry2;   // WeilerAtherton (SignedArea, PointInRingsEvenOdd)
using D4BB.Geometry3d;
using Edge = D4BB.Geometry2.Edge;

namespace D4BB.Geometry3dTests {

    /// Pure-geometry helpers for the Geometry3d parity/invariant tests — the 3D→2D
    /// sibling of Geometry2Tests.TestGeom. All drawing-plane geometry lives at z = 0
    /// (3-component Points, as emitted by Camera3dParallel.Proj2d and the pipeline).
    ///
    /// Epsilon regime (mirrors TestGeom): the pipelines quantize plane-side decisions
    /// with AOP.ERR (1e-5); the coverage tolerances sit one decade above (1e-4);
    /// strictness margins for "strictly inside / strictly nearer" sit at 1e-3.
    public static class TestGeom3d {
        public const double EdgeEps = 1e-4;
        public const double StrictMargin = 1e-3;

        static readonly Point ZUp = new Point(0, 0, 1);

        public static double Area(List<Point> ring) =>
            Math.Abs(WeilerAtherton.SignedArea(ring, ZUp));

        public static Point Centroid(List<Point> ring) {
            var c = new Point(3);
            foreach (var v in ring) c.add(v);
            return c.multiply(1.0 / ring.Count);
        }

        /// Strictly-interior sample points of a convex ring: the centroid plus the
        /// midpoints between the centroid and each vertex / each edge midpoint
        /// (TestGeom.InteriorSamples, on rings instead of Poly wrappers).
        public static List<Point> InteriorSamples(List<Point> ring) {
            var c = Centroid(ring);
            var samples = new List<Point>(1 + 2 * ring.Count) { c };
            int n = ring.Count;
            for (int i = 0; i < n; i++) {
                var v = ring[i].x;
                var m = ring[(i + 1) % n].x;
                samples.Add(new Point((c.x[0] + v[0]) / 2, (c.x[1] + v[1]) / 2, (c.x[2] + v[2]) / 2));
                samples.Add(new Point((c.x[0] + (v[0] + m[0]) / 2) / 2,
                                      (c.x[1] + (v[1] + m[1]) / 2) / 2,
                                      (c.x[2] + (v[2] + m[2]) / 2) / 2));
            }
            return samples;
        }

        public static double TotalVisibleArea(IList<FaceRenderWA2d> faces) {
            double area = 0;
            foreach (var f in faces) area += f.VisibleArea();
            return area;
        }

        /// Even-odd membership of `s` in the union of a face's surviving regions
        /// (outer + hole contours together answer "inside the region").
        public static bool InsideRegions(Point s, FaceRenderWA2d face) {
            var contours = new List<List<Point>>();
            foreach (var r in face.regions) {
                contours.Add(r.outer);
                contours.AddRange(r.holes);
            }
            if (contours.Count == 0) return false;
            return WeilerAtherton.PointInRingsEvenOdd(s, contours, ZUp);
        }

        /// Minimal distance of `s` to any contour segment of the face's surviving regions.
        public static double DistToContours(Point s, FaceRenderWA2d face) {
            double best = double.MaxValue;
            foreach (var r in face.regions) {
                best = Math.Min(best, DistToRing(s, r.outer));
                foreach (var h in r.holes) best = Math.Min(best, DistToRing(s, h));
            }
            return best;
        }

        public static double DistToRing(Point s, List<Point> ring) {
            double best = double.MaxValue;
            int n = ring.Count;
            for (int i = 0; i < n; i++)
                best = Math.Min(best, DistToSegment(s, ring[i], ring[(i + 1) % n]));
            return best;
        }

        public static double DistToSegment(Point s, Point a, Point b) {
            var ab = b.clone().subtract(a);
            var asv = s.clone().subtract(a);
            double len2 = ab.sc(ab);
            double t = len2 < 1e-18 ? 0 : Math.Max(0, Math.Min(1, asv.sc(ab) / len2));
            var proj = a.clone().add(ab.multiply(t));
            return s.clone().subtract(proj).len();
        }

        /// Tolerant membership in the pipeline output: inside some face's surviving
        /// regions (even-odd), or within `tol` of some contour (boundary/seam points
        /// count as covered, like TestGeom.IsCovered).
        public static bool CoveredByFaces(Point s, IList<FaceRenderWA2d> faces, double tol) {
            foreach (var f in faces) {
                if (InsideRegions(s, f)) return true;
                if (DistToContours(s, f) <= tol) return true;
            }
            return false;
        }

        /// True iff `s` is strictly inside the convex hull (all halfplane distances
        /// below -margin). An empty hull contains nothing.
        public static bool StrictlyInsideHull(Point s, HalfSpace[] hull, double margin) {
            if (hull == null || hull.Length == 0) return false;
            foreach (var hs in hull)
                if (hs.normal.sc(s) - hs.length > -margin) return false;
            return true;
        }

        /// True iff `s` is inside the convex hull up to +tol (boundary and near-boundary
        /// count as inside) — the tolerant counterpart for union-coverage arguments: a
        /// point jointly occluded by several hulls can sit exactly on their shared
        /// boundary without being strictly inside any single one.
        public static bool InsideHullTolerant(Point s, HalfSpace[] hull, double tol) {
            if (hull == null || hull.Length == 0) return false;
            foreach (var hs in hull)
                if (hs.normal.sc(s) - hs.length > tol) return false;
            return true;
        }

        /// Depth parameter t of the projection fiber q(t) = s + t·viewNormal against the
        /// plane — the camera sits at -viewNormal infinity, so SMALLER t is NEARER.
        /// Independent re-derivation (not shared with the pipeline) so the soundness
        /// tests cannot inherit a pipeline mistake.
        public static double FiberDepth(Point s, HalfSpace plane, ICamera3d cam) {
            double denom = plane.normal.sc(cam.viewNormal);
            return (plane.length - plane.normal.sc(s)) / denom;
        }

        /// Sutherland-Hodgman clip of a convex ring against a convex hull; fewer than 3
        /// points ⇔ empty intersection.
        public static List<Point> ClipConvex(List<Point> ring, HalfSpace[] hull) {
            var poly = ring;
            foreach (var hs in hull) {
                var next = new List<Point>();
                int m = poly.Count;
                for (int i = 0; i < m; i++) {
                    var cur = poly[i]; var nxt = poly[(i + 1) % m];
                    int cs = hs.side(cur); int ns = hs.side(nxt);
                    if (cs <= 0) next.Add(cur);
                    if ((cs < 0 && ns > 0) || (cs > 0 && ns < 0)) next.Add(hs.cutPoint(cur, nxt));
                }
                if (next.Count < 3) return next;
                poly = next;
            }
            return poly;
        }

        /// Point-in-convex-ring with tolerance (inclusive boundary), winding-agnostic —
        /// the 2D form of TestGeom.PointInConvexPolygon.
        public static bool PointInConvexRing(Point s, List<Point> ring, double tol) {
            bool pos = false, neg = false;
            int n = ring.Count;
            for (int i = 0; i < n; i++) {
                var a = ring[i].x;
                var b = ring[(i + 1) % n].x;
                double ex = b[0] - a[0], ey = b[1] - a[1];
                double len = Math.Sqrt(ex * ex + ey * ey);
                if (len < 1e-12) continue;
                double d = (ex * (s.x[1] - a[1]) - ey * (s.x[0] - a[0])) / len;
                if (d > tol) pos = true;
                else if (d < -tol) neg = true;
                if (pos && neg) return false;
            }
            return true;
        }

        /// Strictly-interior grid samples of a face's surviving regions: bounding-box
        /// grid filtered by even-odd membership and a minimum distance to the face's own
        /// contours (handles concave regions and holes, which InteriorSamples cannot).
        public static List<Point> RegionGridSamples(FaceRenderWA2d face, double margin, int steps = 8) {
            var samples = new List<Point>();
            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;
            foreach (var r in face.regions)
                foreach (var v in r.outer) {
                    minX = Math.Min(minX, v.x[0]); maxX = Math.Max(maxX, v.x[0]);
                    minY = Math.Min(minY, v.x[1]); maxY = Math.Max(maxY, v.x[1]);
                }
            if (minX > maxX) return samples;
            for (int i = 0; i <= steps; i++)
                for (int j = 0; j <= steps; j++) {
                    var s = new Point(minX + (maxX - minX) * i / steps,
                                      minY + (maxY - minY) * j / steps, 0);
                    if (!InsideRegions(s, face)) continue;
                    if (DistToContours(s, face) <= margin) continue;
                    samples.Add(s);
                }
            return samples;
        }

        /// True iff the two segments are collinear and their overlap along the shared
        /// line is longer than `minOverlap` — the EdgeClassOverlayTests invariant probe.
        public static bool CollinearOverlap(Point a1, Point b1, Point a2, Point b2,
                                            double eps, double minOverlap) {
            var dir = b1.clone().subtract(a1);
            double len = dir.len();
            if (len < eps) return false;
            dir.multiply(1.0 / len);
            // Both endpoints of the second segment must lie on the first segment's line.
            if (DistToLine(a2, a1, dir) > eps || DistToLine(b2, a1, dir) > eps) return false;
            double t2a = dir.sc(a2.clone().subtract(a1));
            double t2b = dir.sc(b2.clone().subtract(a1));
            if (t2a > t2b) (t2a, t2b) = (t2b, t2a);
            double lo = Math.Max(0, t2a), hi = Math.Min(len, t2b);
            return hi - lo > minOverlap;
        }

        static double DistToLine(Point p, Point origin, Point unitDir) {
            var d = p.clone().subtract(origin);
            double t = d.sc(unitDir);
            return d.subtract(unitDir.clone().multiply(t)).len();
        }

        /// A complex holding one free rectangle (4 corners, no normal) — for manually
        /// constructed pairwise-ordering scenarios; run with backfaceCulling = false.
        public static PolyhedralComplex3d RectComplex(params double[][] corners) {
            var c = new PolyhedralComplex3d();
            AddRect(c, corners);
            return c;
        }

        /// Appends one free rectangle (n corners, no normal) to the complex.
        public static void AddRect(PolyhedralComplex3d c, double[][] corners) {
            int v0 = c.vertices.Count;
            foreach (var p in corners) c.vertices.Add(new Point((double[])p.Clone()));
            int n = corners.Length;
            var edgeIds = new int[n];
            for (int i = 0; i < n; i++) {
                edgeIds[i] = c.edges.Count;
                c.edges.Add(new Edge(v0 + i, v0 + (i + 1) % n));
            }
            c.faces.Add(new Face3d(edgeIds));
            c.InvalidateCaches();
        }

        /// Finds the complex edge whose endpoints match `a` and `b` (either order).
        public static int FindEdge(PolyhedralComplex3d c, double[] a, double[] b, double eps = 1e-9) {
            for (int eId = 0; eId < c.edges.Count; eId++) {
                var e = c.edges[eId];
                var p0 = c.vertices[e.v0].x;
                var p1 = c.vertices[e.v1].x;
                if ((Close(p0, a, eps) && Close(p1, b, eps)) ||
                    (Close(p0, b, eps) && Close(p1, a, eps))) return eId;
            }
            return -1;
        }

        static bool Close(double[] p, double[] q, double eps) {
            for (int i = 0; i < 3; i++)
                if (Math.Abs(p[i] - q[i]) > eps) return false;
            return true;
        }

        /// Reorients integer cube origins by an axis permutation + per-axis negation:
        /// new axis a takes old axis perm[a]; a negated axis maps the occupied interval
        /// [o, o+1] to [-o-1, -o], i.e. origin -o-1. Keeps input integer, so the result
        /// feeds both Scene3d and the builder.
        public static int[][] Reorient(int[][] cells, int[] perm, bool[] neg) {
            var result = new int[cells.Length][];
            for (int c = 0; c < cells.Length; c++) {
                var o = new int[3];
                for (int a = 0; a < 3; a++) {
                    int v = cells[c][perm[a]];
                    o[a] = neg[a] ? -v - 1 : v;
                }
                result[c] = o;
            }
            return result;
        }
    }
}
