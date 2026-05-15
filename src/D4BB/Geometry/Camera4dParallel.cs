using System;

namespace D4BB.Geometry {
public class Camera4dParallel : ICamera4d
{
    public Point4d[] v;
    public Point4d viewNormal { get { return v[3]; } }
    // Normal to the image hyperplane {w=0} — i.e. the world w-axis, expressed
    // in the (rotated) camera state. Cached and updated incrementally by the
    // rotation methods so dist(p) = imageNormal·p is a single 4-d dot product
    // in hot loops. Initialised to e3 = (0,0,0,1) by SetCavalier/SetIsometric
    // (zero-rotation state); subsequent rotate*-calls rotate it along with v[].
    public Point4d imageNormal;
    private readonly double zoom3d = 1;
    private Point3d _wDir;
    private bool isIsometric = false;
    public Point3d wDir {
        get => _wDir;
        set { if (!isIsometric) SetCavalier(value);  }
    }

    public Camera4dParallel() {
        SetCavalier();
    }
    // Builds an orthonormal 4D basis whose v[3] points along viewDirIn. Method:
    // rotate the standard basis by the rotation that takes (0,0,0,1) to
    // viewDirIn. The two-arg overload also transports eyeInOut by the same
    // rotation (in-place), so the eye position stays consistent with the new
    // viewing direction.
    public static Point4d[] InitialV(Point4d viewDirIn) {
        var w = new Point4d(0, 0, 0, 1);
        var v = new Point4d[] {
            new(1, 0, 0, 0), new(0, 1, 0, 0), new(0, 0, 1, 0), new(0, 0, 0, 1),
        };
        for (int i = 0; i < 4; i++) v[i].rotate(w, viewDirIn);
        return v;
    }
    public static Point4d[] InitialV(Point4d viewDirIn, Point4d eyeInOut) {
        var w = new Point4d(0, 0, 0, 1);
        eyeInOut.rotate(w, viewDirIn);
        return InitialV(viewDirIn);
    }
    public Point3d Proj3d(Point point4d) {
        Point3d res = new Point3d();
        res.x[0] = v[0].sc(point4d);
        res.x[1] = v[1].sc(point4d);
        res.x[2] = v[2].sc(point4d);
        res.multiply(zoom3d);
        return res;
    }
    // Allocation-free projection for hot per-vertex loops.
    public void Proj3dInto(double x0, double x1, double x2, double x3,
                           out float ox, out float oy, out float oz) {
        var v0 = v[0].x; var v1 = v[1].x; var v2 = v[2].x;
        ox = (float)((v0[0]*x0 + v0[1]*x1 + v0[2]*x2 + v0[3]*x3) * zoom3d);
        oy = (float)((v1[0]*x0 + v1[1]*x1 + v1[2]*x2 + v1[3]*x3) * zoom3d);
        oz = (float)((v2[0]*x0 + v2[1]*x1 + v2[2]*x2 + v2[3]*x3) * zoom3d);
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
    // Signed scalar projection of (x,y,z,w) onto v[3] — i.e. the perpendicular
    // distance from p to the projection plane span(v[0..2]).
    public double viewNormalDist(double x, double y, double z, double w) {
        var vn = v[3].x;
        return vn[0]*x + vn[1]*y + vn[2]*z + vn[3]*w;
    }
    // Signed perpendicular distance of (x,y,z,w) from the (rotated) image
    // hyperplane {w=0} — the 4D analogue of "how far above/below the screen
    // plane is this point". Equivalent to (R·p).w where R is the world
    // rotation accumulated in the camera basis since SetCavalier/SetIsometric.
    // Pure dot product against the cached imageNormal (e3 in the rotated
    // camera state), kept current by the rotate* methods.
    public double dist(double x, double y, double z, double w) {
        var n = imageNormal.x;
        return n[0]*x + n[1]*y + n[2]*z + n[3]*w;
    }
    // Generic camera rotation: rotates v[0..3] in the (a,b)-plane by ph.
    // Mathematically equivalent to rotating every 4D vertex by −ph, but
    // O(1) per call. The center c is unused (parallel projection has no eye
    // anchor); it stays in the signature only to satisfy the ICamera4d
    // contract shared with the perspective camera.
    //
    // Note: unlike Camera4dCentral, Cavalier projection bases are NOT
    // orthonormal (v[0]·v[1] = px*py ≠ 0), so AOP.orthoNormalize would corrupt
    // the projection. The caller is expected to start from a fresh basis (or
    // accept that pure rotations preserve inner products up to FP drift).
    public void rotate(double ph, Point a, Point b, Point c) {
        for (int i = 0; i < 4; i++) v[i].rotate(ph, a, b);
        imageNormal.rotate(ph, a, b);
    }
    // Allocation-free Clifford double-rotation helpers. Rotate v[0..3] in the
    // canonical (e0,e1) or (e2,e3) coordinate plane around the world origin.
    // The two together form a Clifford rotation in S³. imageNormal is rotated
    // along, so dist(p) stays consistent without recompute.
    public void RotateBasisXY(double angle) {
        double co = Math.Cos(angle), si = Math.Sin(angle);
        for (int k = 0; k < 4; k++) {
            var x = v[k].x;
            double x0 = x[0], x1 = x[1];
            x[0] = co * x0 - si * x1;
            x[1] = si * x0 + co * x1;
        }
        var nx = imageNormal.x;
        double n0 = nx[0], n1 = nx[1];
        nx[0] = co * n0 - si * n1;
        nx[1] = si * n0 + co * n1;
    }
    public void RotateBasisZW(double angle) {
        double co = Math.Cos(angle), si = Math.Sin(angle);
        for (int k = 0; k < 4; k++) {
            var x = v[k].x;
            double x2 = x[2], x3 = x[3];
            x[2] = co * x2 - si * x3;
            x[3] = si * x2 + co * x3;
        }
        var nx = imageNormal.x;
        double n2 = nx[2], n3 = nx[3];
        nx[2] = co * n2 - si * n3;
        nx[3] = si * n2 + co * n3;
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
        imageNormal = new Point4d(0, 0, 0, 1);
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
        imageNormal = new Point4d(0, 0, 0, 1);
    }
}
}
