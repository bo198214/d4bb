using System;
using D4BB.Geometry;

namespace D4BB.Transforms {
public class Camera4dOrthographic : ICamera4d
{
    public Point4d eye {get; set; }
    public Point4d[] v;
    public Point4d viewNormal { get { return v[3]; } }
    private readonly double zoom3d = 1;
    // oblique shear: w-axis appears as diagonal (s,s,s) in 3D
    // v[0..2] bake in the shear; v[3] is the true view normal, orthogonal to all three
    private static readonly double s = 0.25;
    private static readonly double n = 1.0 / Math.Sqrt(1.0 + 3.0 * s * s);
    protected readonly Point4d initialEye = new(0.5, 0.5, 0.5, -2);
    protected readonly Point4d[] initialV = new Point4d[] {
        new(1, 0, 0, s),
        new(0, 1, 0, s),
        new(0, 0, 1, s),
        new(-s*n, -s*n, -s*n, n)
    };
    public Camera4dOrthographic() {
        eye = initialEye;
        v = initialV;
    }
    public Camera4dOrthographic(Point4d eye) {
        this.eye = eye;
        v = initialV;
    }
    public Point3d Proj3d(Point point4d) {
        Point3d res = new Point3d();
        res.x[0] = v[0].sc(point4d);
        res.x[1] = v[1].sc(point4d);
        res.x[2] = v[2].sc(point4d);
        res.multiply(zoom3d);
        return res;
    }
    public bool IsFacedBy(Point origin, Point normal) {
        return viewNormal.sc(normal) < 0;
    }
    public void SetPerspective(Point4d point) {
        eye = point;
    }
}
}
