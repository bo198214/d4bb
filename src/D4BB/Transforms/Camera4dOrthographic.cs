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
    private Point3d _wDir;
    private bool isIsometric = false;
    public Point3d wDir {
        get => _wDir;
        set { if (!isIsometric) SetCavalier(value);  }
    }

    public Camera4dOrthographic() {
        SetCavalier();
    }
    public Camera4dOrthographic(Point4d eye) : this() {
        this.eye = eye;
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
    // Orthographic isometric: e0->x, e1 in xy-plane.
    // v[3]=(1,1,1,1)/2, all 4 axes project with equal length sqrt(3)/2.
    // True orthographic isometric: v[3]=(1,1,1,1)/2, all 4 axes project with equal length sqrt(3)/2.
    // wDir is no longer meaningful after this call.
    public void SetIsometric() {
        double s3 = 1.0 / Math.Sqrt(3.0);
        double s6 = 1.0 / Math.Sqrt(6.0);
        double s2 = 1.0 / Math.Sqrt(2.0);
        v = new Point4d[] {
            (Point4d)new Point4d( 3*s3, -s3, -s3,   -s3).normalize(),  // e0 -> along +x
            (Point4d)new Point4d(    0,4*s6,-2*s6,-2*s6).normalize(),  // e1 in xy-plane
            (Point4d)new Point4d(    0,   0, 2*s2,-2*s2).normalize(),
            (Point4d)new Point4d(    1,   1,   1,     1).normalize()
        };
        isIsometric = true;
    }
    // Oblique parallel (cavalier): w-axis projected along diagonal (1,1,1) with length wLength.
    public void SetCavalier() {
        SetCavalier(1/Math.Sqrt(5));
    }
    public void SetCavalier(double length) {
        SetCavalier(new Point3d(length, length, length));
    }
    public void SetCavalier(Point3d wDir) {
        isIsometric = false;
        _wDir = wDir;
        double px = _wDir.x[0], py = _wDir.x[1], pz = _wDir.x[2];
        double n = 1.0 / Math.Sqrt(px*px + py*py + pz*pz + 1.0);
        v = new Point4d[] {
            new(1, 0, 0, px),
            new(0, 1, 0, py),
            new(0, 0, 1, pz),
            new(-px*n, -py*n, -pz*n, n)
        };
    }
}
}
