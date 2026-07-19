using System;
using D4BB.Geometry;

namespace D4BB.Geometry2 {

    /// Per-frame camera adapter that lets a rotation-invariant structure (the cached
    /// object-space BSP, see RenderPipeline.Process's cachedBsp parameter) be rendered
    /// under a rigid rotation WITHOUT rebuilding it: instead of rotating every vertex
    /// into world space, the object→world matrix M is applied to the inputs of every
    /// camera query. Since M is orthogonal, side classifications and projections are
    /// numerically equivalent to rotating the geometry itself.
    ///
    /// The adapter is a throwaway per frame; interactive camera mutation is not
    /// supported (mutate the inner camera between frames instead).
    public class RotatedCamera : ICamera4d {
        readonly ICamera4d inner;
        readonly double[] m;  // 4×4 object→world, row-major (m[row*4+col])

        public RotatedCamera(ICamera4d inner, double[] objectToWorldRowMajor) {
            if (objectToWorldRowMajor == null || objectToWorldRowMajor.Length != 16)
                throw new ArgumentException("expected a 4x4 row-major matrix (length 16)");
            this.inner = inner;
            this.m = objectToWorldRowMajor;
        }

        Point ToWorld(Point p) {
            var r = new Point(4);
            for (int i = 0; i < 4; i++)
                r.x[i] = m[i*4+0]*p.x[0] + m[i*4+1]*p.x[1] + m[i*4+2]*p.x[2] + m[i*4+3]*p.x[3];
            return r;
        }

        public Point3d Proj3d(Point point4d) => inner.Proj3d(ToWorld(point4d));

        public bool IsFacedBy(Point origin, Point normal) =>
            inner.IsFacedBy(ToWorld(origin), ToWorld(normal));

        /// The inner camera's view normal pulled back into object space (Mᵀ · world,
        /// valid because M is orthogonal).
        public Point4d viewNormal {
            get {
                var w = inner.viewNormal;
                var r = new Point4d();
                for (int i = 0; i < 4; i++)
                    r.x[i] = m[0*4+i]*w.x[0] + m[1*4+i]*w.x[1] + m[2*4+i]*w.x[2] + m[3*4+i]*w.x[3];
                return r;
            }
        }

        public double dist(double x, double y, double z, double w) {
            double a = m[0]*x  + m[1]*y  + m[2]*z  + m[3]*w;
            double b = m[4]*x  + m[5]*y  + m[6]*z  + m[7]*w;
            double c = m[8]*x  + m[9]*y  + m[10]*z + m[11]*w;
            double d = m[12]*x + m[13]*y + m[14]*z + m[15]*w;
            return inner.dist(a, b, c, d);
        }

        public void rotate(double ph, Point a, Point b, Point c) =>
            throw new NotSupportedException("RotatedCamera is a per-frame adapter; rotate the inner camera instead.");
    }
}
