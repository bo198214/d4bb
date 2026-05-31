using System.Linq;
using NUnit.Framework;
using D4BB.Comb;
using D4BB.Geometry;

namespace D4BB.Transforms {

// Tests for the 3D→2D camera occlusion (CutOut) ported from the 4D path. A nearer 2-cell
// cuts the projection of farther 2-cells; fully covered far faces vanish, partially covered
// ones gain a MARK_NONE cut edge (routed to edgeCutMaterial). Mirrors the style of
// MarkStampingTests / the Scene4d occlusion tests.
public class Scene3dOcclusionTests
{
    const int GI = IPolyhedron.MARK_GRID_DIVISION;
    const int NONE = IPolyhedron.MARK_NONE;

    // Two single-cube pieces stacked along the camera depth axis, viewed dead-on (zDir=0 so
    // the cabinet shear vanishes): the near cube's front face projects exactly onto the far
    // cube's, so with occlusion on the far piece is fully hidden; off, it stays visible.
    [Test] public void Scene3d_NearCubeFullyOccludesFar_FrontView() {
        var origins = new int[][][] {
            new int[][] { new int[] { 0, 0, 0 } },   // piece 0 — near
            new int[][] { new int[] { 0, 0, 1 } },   // piece 1 — far (larger z = deeper)
        };
        var camera = new Camera3dParallel(new Point(0, 0)); // orthographic front view

        var off = new Scene3d(origins, camera); off.enable3dOcclusion = false; off.Update(origins);
        Assert.That(off.VisibleFacets(1).Count, Is.GreaterThan(0),
            "Occlusion off: the far piece keeps its faces.");

        var on = new Scene3d(origins, camera); on.enable3dOcclusion = true; on.Update(origins);
        Assert.That(on.VisibleFacets(1).Count, Is.EqualTo(0),
            "Occlusion on: the near cube fully covers the far cube's front face.");
        Assert.That(on.VisibleFacets(0).Count, Is.GreaterThan(0),
            "The near piece is never cut by anything behind it.");
    }

    // Partial overlap: a cube two z-steps back (0,0,2) projects partly outside the near cube's
    // cabinet silhouette (whose diagonal +x/+y edges cut across the far front face). The
    // exposed part survives, so the clip must introduce a fresh MARK_NONE silhouette edge —
    // the signal that real geometric clipping happened, not just whole-face removal. (A cube one
    // step back at (0,0,1) would sit exactly in the shadow tunnel and be removed wholesale.)
    [Test] public void Scene3d_PartialOcclusion_ProducesCutEdge() {
        var origins = new int[][][] {
            new int[][] { new int[] { 0, 0, 0 } },   // near
            new int[][] { new int[] { 0, 0, 2 } },   // far (straddles the near silhouette boundary)
        };
        var camera = new Camera3dParallel(); // cabinet (0.5,0.5)

        var off = new Scene3d(origins, camera); off.enable3dOcclusion = false; off.Update(origins);
        Assert.That(off.VisibleEdges(1).All(e => e.mark == GI), Is.True,
            "Occlusion off: every far-piece edge originates from the grid (mark=GRID_DIVISION).");

        var on = new Scene3d(origins, camera); on.enable3dOcclusion = true; on.Update(origins);
        Assert.That(on.VisibleEdges(1).Any(e => e.mark == NONE), Is.True,
            "Occlusion on: a partial cut exposes at least one fresh MARK_NONE cut edge.");
    }

    // DefiningHalfSpaces2d sanity: the four half-spaces bound the cell's projected quad, so the
    // projected center is INSIDE all of them and a far-away screen point is OUTSIDE at least one.
    [Test] public void DefiningHalfSpaces2d_BoundsProjectedQuad() {
        var camera = new Camera3dParallel();
        // The ctor already builds the slabs; a single cube needs no occlusion toggle.
        var scene = new Scene3d(new int[][][] { new int[][] { new int[] { 0, 0, 0 } } }, camera);

        var cell = scene.slabs.SelectMany(s => s.cells).First();
        var hs = Scene3d.DefiningHalfSpaces2d(cell, camera);
        Assert.That(hs.Length, Is.EqualTo(4), "A 2-cell has four bounding edges.");

        var verts = cell.Vertices();
        var center = new Point3d();
        foreach (var c in verts) center.add(camera.Proj2d(new Point(c)));
        center.multiply(1.0 / verts.Length);

        foreach (var h in hs)
            Assert.That(h.side(center), Is.EqualTo(HalfSpace.INSIDE),
                "The projected cell center lies inside every bounding half-space.");

        var farPoint = camera.Proj2d(new Point(new int[] { 100, 100, 0 }));
        Assert.That(hs.Any(h => h.side(farPoint) == HalfSpace.OUTSIDE), Is.True,
            "A point far outside the quad is outside at least one half-space.");
    }
}
}
