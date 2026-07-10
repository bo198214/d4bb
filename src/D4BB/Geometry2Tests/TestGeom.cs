using System.Collections.Generic;
using System.Globalization;
using D4BB.Geometry;
using D4BB.Geometry2;

namespace D4BB.Geometry2Tests {

    /// Pure-geometry helpers for the parity/invariant tests. All polygons are convex,
    /// cyclically ordered 3D point lists — the representation both rendering pipelines
    /// emit (Face2d.points on the Scene4d side, CellRender3d.faces on the Geometry2 side).
    ///
    /// Epsilon regime: both pipelines quantize plane-side decisions with AOP.ERR (1e-5),
    /// so cut vertices may legitimately differ between pipelines by that order. The
    /// coverage tolerances sit one decade above (1e-4) to absorb this, while remaining
    /// far below the unit-cell scale of any real occlusion bug.
    public static class TestGeom {
        /// Max distance of a sample from a polygon's supporting plane to count as coplanar.
        public const double PlaneEps = 1e-4;
        /// Max outside-distance of a sample from a polygon's edge to still count as inside.
        public const double EdgeEps = 1e-4;
        /// Polygons below this area are cutting debris, invisible at render scale.
        public const double MinArea = 1e-6;

        /// A polygon prepared for coverage checks: unit plane normal + centroid anchor.
        public readonly struct Poly {
            public readonly List<Point> points;
            public readonly Point normal;   // unit; orientation arbitrary
            public readonly Point centroid;
            public Poly(List<Point> points, Point normal, Point centroid) {
                this.points = points; this.normal = normal; this.centroid = centroid;
            }
        }

        /// Wraps the raw polygons, dropping degenerate ones (fewer than 3 vertices,
        /// no usable plane normal, or area below MinArea).
        public static List<Poly> Prepare(IEnumerable<List<Point>> polygons) {
            var result = new List<Poly>();
            foreach (var poly in polygons) {
                if (poly == null || poly.Count < 3) continue;
                var newell = NewellVector(poly);
                double len = System.Math.Sqrt(newell.sc(newell));
                if (len / 2 < MinArea) continue;   // |newell|/2 == polygon area
                newell.multiply(1.0 / len);
                result.Add(new Poly(poly, newell, Centroid(poly)));
            }
            return result;
        }

        public static Point Centroid(List<Point> poly) {
            var c = new Point(3);
            foreach (var v in poly) c.add(v);
            c.multiply(1.0 / poly.Count);
            return c;
        }

        /// Newell's method: a vector normal to the polygon's plane whose length is twice
        /// the polygon area. Robust for slightly noisy, nearly-degenerate fragments.
        public static Point NewellVector(List<Point> poly) {
            double nx = 0, ny = 0, nz = 0;
            for (int i = 0; i < poly.Count; i++) {
                var a = poly[i].x;
                var b = poly[(i + 1) % poly.Count].x;
                nx += (a[1] - b[1]) * (a[2] + b[2]);
                ny += (a[2] - b[2]) * (a[0] + b[0]);
                nz += (a[0] - b[0]) * (a[1] + b[1]);
            }
            return new Point(nx, ny, nz);
        }

        public static double Area(List<Point> poly) {
            var n = NewellVector(poly);
            return System.Math.Sqrt(n.sc(n)) / 2;
        }

        /// Strictly-interior sample points of a convex polygon: the centroid plus the
        /// midpoints between the centroid and each vertex / each edge midpoint.
        public static List<Point> InteriorSamples(Poly p) {
            var samples = new List<Point>(1 + 2 * p.points.Count) { p.centroid };
            var c = p.centroid.x;
            for (int i = 0; i < p.points.Count; i++) {
                var v = p.points[i].x;
                var m = p.points[(i + 1) % p.points.Count].x;
                samples.Add(new Point((c[0] + v[0]) / 2, (c[1] + v[1]) / 2, (c[2] + v[2]) / 2));
                samples.Add(new Point((c[0] + (v[0] + m[0]) / 2) / 2,
                                      (c[1] + (v[1] + m[1]) / 2) / 2,
                                      (c[2] + (v[2] + m[2]) / 2) / 2));
            }
            return samples;
        }

        static double PlaneDist(Point sample, Poly p) {
            var s = sample.x; var c = p.centroid.x; var n = p.normal.x;
            return (s[0] - c[0]) * n[0] + (s[1] - c[1]) * n[1] + (s[2] - c[2]) * n[2];
        }

        /// True iff `sample` (assumed near the polygon's plane) lies inside the convex
        /// polygon, within `tol` of its edges (inclusive — boundary/seam points count as
        /// inside). Winding-agnostic: inside means all signed edge distances agree in sign
        /// up to the tolerance.
        public static bool PointInConvexPolygon(Point sample, Poly p, double tol) {
            var s = sample.x; var n = p.normal.x;
            bool pos = false, neg = false;
            int count = p.points.Count;
            for (int i = 0; i < count; i++) {
                var a = p.points[i].x;
                var b = p.points[(i + 1) % count].x;
                double ex = b[0] - a[0], ey = b[1] - a[1], ez = b[2] - a[2];
                double len = System.Math.Sqrt(ex * ex + ey * ey + ez * ez);
                if (len < 1e-12) continue;
                double px = s[0] - a[0], py = s[1] - a[1], pz = s[2] - a[2];
                // (edge × toSample) · normal = signed in-plane distance from the edge line, × |edge|
                double cx = ey * pz - ez * py, cy = ez * px - ex * pz, cz = ex * py - ey * px;
                double d = (cx * n[0] + cy * n[1] + cz * n[2]) / len;
                if (d > tol) pos = true;
                else if (d < -tol) neg = true;
                if (pos && neg) return false;
            }
            return true;
        }

        /// True iff the sample lies on some polygon of `cover` (coplanar within PlaneEps,
        /// inside within EdgeEps).
        public static bool IsCovered(Point sample, List<Poly> cover) {
            foreach (var p in cover) {
                if (System.Math.Abs(PlaneDist(sample, p)) > PlaneEps) continue;
                if (PointInConvexPolygon(sample, p, EdgeEps)) return true;
            }
            return false;
        }

        /// Mutual-coverage check, direction `from` ⊆ `to`: every interior sample of every
        /// `from`-polygon must lie on some coplanar `to`-polygon. Returns one message per
        /// polygon with uncovered samples (not per sample, to keep failure output readable).
        public static List<string> CoverageMismatches(List<Poly> from, List<Poly> to) {
            var mismatches = new List<string>();
            for (int i = 0; i < from.Count; i++) {
                var p = from[i];
                int uncovered = 0;
                Point firstMiss = null;
                foreach (var sample in InteriorSamples(p)) {
                    if (IsCovered(sample, to)) continue;
                    uncovered++;
                    firstMiss ??= sample;
                }
                if (uncovered > 0)
                    mismatches.Add(string.Format(CultureInfo.InvariantCulture,
                        "poly {0} (area {1:G3}, centroid {2}): {3} uncovered sample(s), e.g. {4}",
                        i, Area(p.points), Fmt(p.centroid), uncovered, Fmt(firstMiss)));
            }
            return mismatches;
        }

        public static string Fmt(Point p) => string.Format(CultureInfo.InvariantCulture,
            "({0:F4}, {1:F4}, {2:F4})", p.x[0], p.x[1], p.x[2]);

        /// Apply the same per-frame XY/ZW double-rotation that ComplexFrame.ApplyRotation
        /// uses (RotatingComplex's runtime scenario). Mutates `complex` vertices and cell
        /// normals in place; calls InvalidateCaches.
        public static void RotateComplex(PolyhedralComplex4d complex, double angle1, double angle2) {
            double c1 = System.Math.Cos(angle1), s1 = System.Math.Sin(angle1);
            double c2 = System.Math.Cos(angle2), s2 = System.Math.Sin(angle2);
            void Rot(Point p) {
                double x = p.x[0], y = p.x[1], z = p.x[2], w = p.x[3];
                p.x[0] = c1 * x - s1 * y;
                p.x[1] = s1 * x + c1 * y;
                p.x[2] = c2 * z - s2 * w;
                p.x[3] = s2 * z + c2 * w;
            }
            foreach (var v in complex.vertices) Rot(v);
            foreach (var cell in complex.cells) if (cell.normal != null) Rot(cell.normal);
            complex.InvalidateCaches();
        }

        /// Rotate the complex's vertices and cell normals in the coordinate plane spanned
        /// by axes i and j (0..3) by `angle`, about the origin. Covers the rotation
        /// families the XY/ZW double-rotation cannot reach (e.g. XZ — the L-Sequence
        /// plane). Mutates in place; calls InvalidateCaches.
        public static void RotateComplexInPlane(PolyhedralComplex4d complex, int i, int j, double angle) {
            double c = System.Math.Cos(angle), s = System.Math.Sin(angle);
            void Rot(Point p) {
                double a = p.x[i], b = p.x[j];
                p.x[i] = c * a - s * b;
                p.x[j] = s * a + c * b;
            }
            foreach (var v in complex.vertices) Rot(v);
            foreach (var cell in complex.cells) if (cell.normal != null) Rot(cell.normal);
            complex.InvalidateCaches();
        }
    }
}
