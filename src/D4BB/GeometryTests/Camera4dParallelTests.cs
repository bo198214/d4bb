using System;
using NUnit.Framework;

namespace D4BB.Geometry {

// Verifies the dist(x,y,z,w) method on Camera4dParallel, which returns the
// signed perpendicular distance of a 4D point to the (rotated) {w=0} image
// hyperplane. The result is dot-product with the cached imageNormal — kept
// up-to-date by the rotate* methods.
public class Camera4dParallelTests
{
    const double EPS = 1e-12;

    [Test] public void Dist_ZeroRotation_EqualsW()
    {
        var cam = new Camera4dParallel();
        Assert.That(cam.dist(0, 0, 0, 1),  Is.EqualTo( 1).Within(EPS));
        Assert.That(cam.dist(0, 0, 0, -1), Is.EqualTo(-1).Within(EPS));
        Assert.That(cam.dist(1, 2, 3, 0),  Is.EqualTo( 0).Within(EPS));
        Assert.That(cam.dist(1, 2, 3, 4),  Is.EqualTo( 4).Within(EPS));
    }

    [Test] public void ImageNormal_StartsAsE3()
    {
        var cam = new Camera4dParallel();
        var n = cam.imageNormal.x;
        Assert.That(n[0], Is.EqualTo(0).Within(EPS));
        Assert.That(n[1], Is.EqualTo(0).Within(EPS));
        Assert.That(n[2], Is.EqualTo(0).Within(EPS));
        Assert.That(n[3], Is.EqualTo(1).Within(EPS));
    }

    [Test] public void Dist_RotateBasisXY_DoesNotAffectWAxis()
    {
        var cam = new Camera4dParallel();
        cam.RotateBasisXY(Math.PI / 4);
        // imageNormal had zero xy-components, so an xy-rotation leaves it
        // unchanged. dist still measures pure w.
        Assert.That(cam.dist(0, 0, 0, 1), Is.EqualTo(1).Within(EPS));
        Assert.That(cam.dist(0, 0, 1, 0), Is.EqualTo(0).Within(EPS));
        Assert.That(cam.dist(7, -3, 2, 5), Is.EqualTo(5).Within(EPS));
    }

    [Test] public void Dist_RotateBasisZW_RotatesWAxis()
    {
        var cam = new Camera4dParallel();
        double a = Math.PI / 6;
        cam.RotateBasisZW(a);
        // imageNormal: (0,0,0,1) → ZW(a) → (0, 0, -sin(a), cos(a)).
        double s = Math.Sin(a), c = Math.Cos(a);
        Assert.That(cam.dist(0, 0, 0, 1), Is.EqualTo( c).Within(EPS));
        Assert.That(cam.dist(0, 0, 1, 0), Is.EqualTo(-s).Within(EPS));
        Assert.That(cam.dist(1, 1, 0, 0), Is.EqualTo( 0).Within(EPS));
    }

    [Test] public void Dist_RotateBasisZW_HalfTurn_FlipsW()
    {
        var cam = new Camera4dParallel();
        cam.RotateBasisZW(Math.PI);
        // imageNormal → (0, 0, 0, -1)
        Assert.That(cam.dist(0, 0, 0, 1), Is.EqualTo(-1).Within(EPS));
        Assert.That(cam.dist(0, 0, 1, 0), Is.EqualTo( 0).Within(EPS));
    }

    [Test] public void Dist_RotateBasisZW_QuarterTurn_MapsWToMinusZ()
    {
        var cam = new Camera4dParallel();
        cam.RotateBasisZW(Math.PI / 2);
        // imageNormal → (0, 0, -1, 0)
        Assert.That(cam.dist(0, 0, 0, 1), Is.EqualTo( 0).Within(EPS));
        Assert.That(cam.dist(0, 0, 1, 0), Is.EqualTo(-1).Within(EPS));
        Assert.That(cam.dist(0, 0, -1, 0), Is.EqualTo(1).Within(EPS));
    }

    [Test] public void Dist_FullRotationZW_ReturnsToIdentity()
    {
        var cam = new Camera4dParallel();
        cam.RotateBasisZW(2 * Math.PI);
        Assert.That(cam.dist(0, 0, 0, 1), Is.EqualTo(1).Within(1e-10));
        Assert.That(cam.dist(0, 0, 1, 0), Is.EqualTo(0).Within(1e-10));
    }

    [Test] public void Dist_CliffordRotation_OrderIndependent()
    {
        // XY and ZW Clifford rotations act on disjoint coordinate pairs, so
        // applying them in either order yields the same imageNormal (and
        // therefore the same dist for any point).
        var camA = new Camera4dParallel();
        camA.RotateBasisXY(0.3);
        camA.RotateBasisZW(0.5);

        var camB = new Camera4dParallel();
        camB.RotateBasisZW(0.5);
        camB.RotateBasisXY(0.3);

        var p = new[] { 1.2, -3.4, 0.7, 2.5 };
        Assert.That(camA.dist(p[0], p[1], p[2], p[3]),
            Is.EqualTo(camB.dist(p[0], p[1], p[2], p[3])).Within(EPS));
    }

    [Test] public void Dist_SetCavalierResetsImageNormal()
    {
        var cam = new Camera4dParallel();
        cam.RotateBasisZW(Math.PI / 3);
        cam.RotateBasisXY(Math.PI / 5);
        // Reset.
        cam.SetCavalier();
        Assert.That(cam.dist(0, 0, 0, 1), Is.EqualTo(1).Within(EPS));
        Assert.That(cam.dist(0, 0, 1, 0), Is.EqualTo(0).Within(EPS));
        Assert.That(cam.dist(1, 1, 1, 1), Is.EqualTo(1).Within(EPS));
    }

    [Test] public void Dist_SetIsometricResetsImageNormal()
    {
        var cam = new Camera4dParallel();
        cam.RotateBasisZW(0.7);
        cam.SetIsometric();
        Assert.That(cam.dist(0, 0, 0, 1), Is.EqualTo(1).Within(EPS));
        Assert.That(cam.dist(0, 0, 1, 0), Is.EqualTo(0).Within(EPS));
    }

    [Test] public void Dist_LinearityInPoint()
    {
        // dist is linear in its argument (it's a dot product with a fixed
        // 4D vector). Verify after an arbitrary rotation.
        var cam = new Camera4dParallel();
        cam.RotateBasisXY(0.4);
        cam.RotateBasisZW(0.9);
        double da = cam.dist(1, 2, 3, 4);
        double db = cam.dist(2, 1, -1, 0);
        double dsum = cam.dist(3, 3, 2, 4);
        Assert.That(dsum, Is.EqualTo(da + db).Within(EPS));
        double dscaled = cam.dist(2.5, 5, 7.5, 10);
        Assert.That(dscaled, Is.EqualTo(2.5 * da).Within(EPS));
    }

    [Test] public void Dist_RotateMethod_TracksImageNormal()
    {
        // Generic rotate(ph, a, b, c) must also keep imageNormal in sync.
        // Use the standard (e2, e3) plane to compare against RotateBasisZW.
        var camA = new Camera4dParallel();
        camA.RotateBasisZW(0.42);

        var camB = new Camera4dParallel();
        var e2 = new Point4d(0, 0, 1, 0);
        var e3 = new Point4d(0, 0, 0, 1);
        var origin = new Point4d(0, 0, 0, 0);
        camB.rotate(0.42, e2, e3, origin);

        var p = new[] { 0.3, -0.6, 1.1, -0.2 };
        Assert.That(camA.dist(p[0], p[1], p[2], p[3]),
            Is.EqualTo(camB.dist(p[0], p[1], p[2], p[3])).Within(1e-10));
    }

    [Test] public void ViewNormalDist_EqualsViewNormalDotP()
    {
        var cam = new Camera4dParallel();
        cam.RotateBasisXY(0.3);
        cam.RotateBasisZW(0.5);
        var vn = cam.viewNormal.x;
        double expected = vn[0]*1.1 + vn[1]*2.2 + vn[2]*-0.7 + vn[3]*0.9;
        Assert.That(cam.viewNormalDist(1.1, 2.2, -0.7, 0.9),
            Is.EqualTo(expected).Within(EPS));
    }

    [Test] public void Dist_AgainstHandRotation_ZWOnly()
    {
        // Cross-check dist against the closed-form (R·p).w = s2·z + c2·w
        // (which we know is the correct rotated w-component for a pure ZW
        // rotation by angle a applied to the vertex side).
        var cam = new Camera4dParallel();
        double a = 0.85;
        // dist uses the camera-rotation paradigm; to match a "vertex-side"
        // rotation by +a, the camera rotates by −a.
        cam.RotateBasisZW(-a);
        double s = Math.Sin(a), c = Math.Cos(a);
        for (int trial = 0; trial < 5; trial++)
        {
            double[] p = { trial * 0.3, -trial * 0.2, Math.Cos(trial), Math.Sin(trial) };
            double expected = s * p[2] + c * p[3];
            Assert.That(cam.dist(p[0], p[1], p[2], p[3]),
                Is.EqualTo(expected).Within(1e-10),
                $"trial {trial}, p={p[0]},{p[1]},{p[2]},{p[3]}");
        }
    }
}

}
