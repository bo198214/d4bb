using System;
using D4BB.Geometry;
using NUnit.Framework;

namespace D4BB.Transforms {
public class Camera4dOrthographic : ICamera4d
{
    public Point4d[] v;
    public Point4d viewNormal { get { return v[3]; } }
    private readonly double zoom3d = 1;
    // 3D vector that one unit along the w-axis maps to; (0,0,0) = true orthographic
    private Point4d _eye = new Point4d(0, 0, 0, 0);
    public Point4d eye { get => _eye; set => _eye = value; }
    private Point3d _wDir = (Point3d)new Point3d(1, 1, 1).multiply(1/Math.Sqrt(5));
    bool isIsometric = false;
    public Point3d wDir {
        get => _wDir;
        set { if (!isIsometric) {_wDir = value; BuildV();} }
    }

    public Camera4dOrthographic() {
        SetIsometric();
    }
    public Camera4dOrthographic(Point4d eye) {
        this.eye = eye;
        SetIsometric();
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
    // True orthographic isometric: v[3]=(1,1,1,1)/2, all 4 axes project with equal length sqrt(3)/2.
    // wDir is no longer meaningful after this call.
    public void SetIsometricZ() {
        double s2  = 1.0 / Math.Sqrt(2);
        double s6  = 1.0 / Math.Sqrt(6);
        double s12 = 1.0 / Math.Sqrt(12);
        v = new Point4d[] {
            new( s2, -s2,      0,       0),
            new( s6,  s6,  -2*s6,       0),
            new(s12, s12,   s12,  -3*s12),
            new(0.5, 0.5,   0.5,     0.5)
        };
        isIsometric = true;
    }
    public void SetIsometric() {
        SetIsometricXY();
    }
    // Orthographic isometric: e0->x, e1 in xy-plane.
    // v[3]=(1,1,1,1)/2, all 4 axes project with equal length sqrt(3)/2.
    public void SetIsometricXY() {
        double s3 = 1.0 / Math.Sqrt(3.0);
        double s6 = 1.0 / Math.Sqrt(6.0);
        double s2 = 1.0 / Math.Sqrt(2.0);
        v = new Point4d[] {
            new( 3*s3/2, -s3/2, -s3/2, -s3/2),  // e0 -> along +x
            new(      0,  2*s6,   -s6,    -s6),  // e1 in xy-plane
            new(      0,     0,    s2,    -s2),
            new(    0.5,   0.5,   0.5,    0.5)
        };
        isIsometric = true;
    }
    // Orthographic isometric: all 4 axes at equal angle acos(1/sqrt(3)) ~54.7 deg to their output axis.
    // The 4 axes project onto the 4 body diagonals of a cube (orthogonal +/-0.5 matrix).
    public void SetIsometricUniform() {
        v = new Point4d[] {
            new( 0.5, -0.5, -0.5,  0.5),
            new(-0.5,  0.5, -0.5,  0.5),
            new(-0.5, -0.5,  0.5,  0.5),
            new( 0.5,  0.5,  0.5,  0.5)
        };
        isIsometric = true;
    }
    public void SetIsometricXYZ() {
        double s3 = 1.0 / Math.Sqrt(3.0);
        double s6 = 1.0 / Math.Sqrt(6.0);
        double s2 = 1.0 / Math.Sqrt(2.0);
        v = new Point4d[] {
            new( 3*s3/2, -s3/2, -s3/2, -s3/2),
            new(      0,  2*s6,   -s6,    -s6),
            new(      0,     0,   -s2,     s2),  // z inverted
            new(    0.5,   0.5,   0.5,    0.5)
        };
        isIsometric = true;
    }
    public void SetIsometricUniformZ() {
        v = new Point4d[] {
            new( 0.5, -0.5, -0.5,  0.5),
            new(-0.5,  0.5, -0.5,  0.5),
            new( 0.5,  0.5, -0.5, -0.5),  // z inverted
            new( 0.5,  0.5,  0.5,  0.5)
        };
        isIsometric = true;
    }
    // Oblique parallel (cavalier): w-axis projected along diagonal (1,1,1) with length wLength.
    public void SetCavalier(double length) {
        isIsometric = false;
        wDir = new Point3d(length, length, length);
    }
}
}
