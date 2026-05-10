using System.Collections.Generic;
using D4BB.Geometry;

namespace D4BB.Geometry2 {
    /// Affine 2-plane embedded in 4-space, given by an origin and two orthonormal spanning vectors.
    public class Plane2in4 {
        public readonly Point origin;
        public readonly Point u;
        public readonly Point v;

        public Plane2in4(Point origin, Point u, Point v) {
            this.origin = origin;
            this.u = u;
            this.v = v;
        }

        /// Builds a Plane2in4 from any three points spanning the plane. Returns null if colinear.
        public static Plane2in4 From3Points(Point a, Point b, Point c) {
            var u0 = b.clone().subtract(a);
            var v0 = c.clone().subtract(a);
            if (u0.len() < AOP.ERR) return null;
            u0.normalize();
            AOP.orthogonalize(u0, v0);
            if (v0.len() < AOP.ERR) return null;
            v0.normalize();
            return new Plane2in4(a.clone(), u0, v0);
        }

        /// Builds a Plane2in4 from a list of points. Picks the first three affinely independent ones.
        public static Plane2in4 FromPoints(IList<Point> points) {
            if (points.Count < 3) return null;
            var a = points[0];
            Point b = null;
            for (int i = 1; i < points.Count; i++) {
                if (points[i].clone().subtract(a).len() > AOP.ERR) { b = points[i]; break; }
            }
            if (b == null) return null;
            var ab = b.clone().subtract(a);
            for (int i = 1; i < points.Count; i++) {
                var p = points[i];
                if (ReferenceEquals(p, b)) continue;
                var ap = p.clone().subtract(a);
                var proj = ab.clone().multiply(ab.sc(ap) / ab.sc(ab));
                ap.subtract(proj);
                if (ap.len() > AOP.ERR) return From3Points(a, b, p);
            }
            return null;
        }

        public bool Contains(Point p) {
            var d = p.clone().subtract(origin);
            d.subtract(u.clone().multiply(u.sc(d)));
            d.subtract(v.clone().multiply(v.sc(d)));
            return d.len() < AOP.ERR;
        }

        /// True iff this and other span the same affine 2-plane in 4-space.
        public bool SameSubspace(Plane2in4 other) {
            if (other == null) return false;
            if (!other.Contains(origin)) return false;
            if (!other.Contains(origin.clone().add(u))) return false;
            if (!other.Contains(origin.clone().add(v))) return false;
            return true;
        }
    }
}
