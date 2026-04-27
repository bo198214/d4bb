using System;
using D4BB.Geometry;

namespace D4BB.Transforms {
public class Camera4dOrthographic : ICamera4d
{
    public Point4d[] v;
    public Point4d viewNormal { get { return v[3]; } }
    private readonly double zoom3d = 1;
    // 3D vector that one unit along the w-axis maps to; (0,0,0) = true orthographic
    private Point4d _eye = new Point4d(0, 0, 0, 0);
    public Point4d eye { get => _eye; set => _eye = value; }
    private Point3d _wDir = new Point3d(0.25, 0.25, 0.25);
    public Point3d wDir {
        get => _wDir;
        set { _wDir = value; BuildV(); }
    }

    public Camera4dOrthographic() {
        BuildV();
    }
    public Camera4dOrthographic(Point4d eye) {
        this.eye = eye;
        BuildV();
    }
    void BuildV() {
        double px = _wDir.x[0], py = _wDir.x[1], pz = _wDir.x[2];
        double n = 1.0 / Math.Sqrt(px*px + py*py + pz*pz + 1.0);
        v = new Point4d[] {
            new(1, 0, 0, px),
            new(0, 1, 0, py),
            new(0, 0, 1, pz),
            new(-px*n, -py*n, -pz*n, n)
        };
    }
    public Point3d Proj3d(Point point4d) {
        Point3d res = new Point3d();
        Point diff = point4d.clone().subtract(eye);
        res.x[0] = v[0].sc(diff);
        res.x[1] = v[1].sc(diff);
        res.x[2] = v[2].sc(diff);
        res.multiply(zoom3d);
        return res;
    }
    public bool IsFacedBy(Point normal) {
        return viewNormal.sc(normal) < 0;
    }
    public bool IsFacedBy(Point origin, Point normal) {
        return viewNormal.sc(normal) < 0;
    }
}
}
