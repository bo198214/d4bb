using System;

namespace D4BB.Geometry {
public class Camera4dParallel : ICamera4d
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

    public Camera4dParallel() {
        SetCavalier();
    }
    public Camera4dParallel(Point4d eye) : this() {
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
    // Allocation-free projection for hot per-vertex loops. The caller is responsible
    // for invalidating cached basis copies after any SetCavalier/SetIsometric/eye change.
    public void Proj3dInto(double x0, double x1, double x2, double x3,
                           out float ox, out float oy, out float oz) {
        var e = _eye.x;
        double d0 = x0 - e[0], d1 = x1 - e[1], d2 = x2 - e[2], d3 = x3 - e[3];
        var v0 = v[0].x; var v1 = v[1].x; var v2 = v[2].x;
        ox = (float)((v0[0]*d0 + v0[1]*d1 + v0[2]*d2 + v0[3]*d3) * zoom3d);
        oy = (float)((v1[0]*d0 + v1[1]*d1 + v1[2]*d2 + v1[3]*d3) * zoom3d);
        oz = (float)((v2[0]*d0 + v2[1]*d1 + v2[2]*d2 + v2[3]*d3) * zoom3d);
    }
    public bool IsFacedBy(Point normal) {
        return viewNormal.sc(normal) < 0;
    }
    public bool IsFacedBy(Point origin, Point normal) {
        return viewNormal.sc(normal) < 0;
    }
    public bool IsFacedBy(double n0, double n1, double n2, double n3) {
        var vn = v[3].x;
        return vn[0]*n0 + vn[1]*n1 + vn[2]*n2 + vn[3]*n3 < 0;
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
