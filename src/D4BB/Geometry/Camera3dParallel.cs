using System;

namespace D4BB.Geometry {
public class Camera3dParallel : ICamera3d
{
    public Point3d[] v;
    public Point3d viewNormal { get { return v[2]; } }
    public double zoom2d = 1;

    // Direction the world z-axis projects into the 2D plane as a 2-vector (px, py). A unit
    // z-step lands at (px, py) on screen, so the projected z-axis length is sqrt(px² + py²).
    // Cabinet projection = diagonal (0.5, 0.5) → length √2/2. Mirrors Camera4dParallel.wDir.
    private Point _zDir;
    public Point zDir {
        get => _zDir;
        set => SetCavalier(value);
    }

    public Camera3dParallel() {
        SetCavalier();
    }
    public Camera3dParallel(double length) {
        SetCavalier(length);
    }
    public Camera3dParallel(Point zDir) {
        SetCavalier(zDir);
    }

    public Point Proj2d(Point point3d) {
        Point3d res = new Point3d();
        res.x[0] = v[0].sc(point3d);
        res.x[1] = v[1].sc(point3d);
        res.multiply(zoom2d);
        return res;
    }

    public bool IsFacedBy(Point origin, Point normal) {
        return viewNormal.sc(normal) < 0;
    }

    // Cabinet projection: shear (0.5, 0.5) → projected z-axis length √2/2.
    public void SetCavalier() {
        SetCavalier(0.5);
    }
    // Diagonal shear (length, length) → projected z-axis length = length·√2.
    public void SetCavalier(double length) {
        SetCavalier(new Point(length, length));
    }
    // Generic shear: each unit z-step projects to (zDir.x[0], zDir.x[1]) on screen. The shear
    // is baked into the in-plane basis vectors v[0..1], so Proj2d is a pure dot product (no
    // separate eye-based shift, unlike the old Camera3dOrthographic).
    public void SetCavalier(Point zDir) {
        _zDir = zDir;
        double px = zDir.x[0], py = zDir.x[1];
        double n = 1.0 / Math.Sqrt(px*px + py*py + 1.0);
        v = new Point3d[] {
            new(1, 0, px),
            new(0, 1, py),
            new(-px*n, -py*n, n),
        };
    }
}
}
