using D4BB.Geometry;
using NUnit.Framework;

namespace D4BB.Transforms
{
// End-to-end pos4d preservation through Polyhedron3dBoundaryComplex.CutOut — the path
// Game.OnFaceMeshUpdated reads when 4D occlusion clips one piece against another.
// Unit-level tests for OpposingClone/Edge.Split/Face2d.Split live in
// GeometryTests/Pos4dPreservationTests.cs (different assembly).
public class Pos4dPreservationPbcTests
{
    [Test] public void CutOut_Half_AllVerticesRetainPos4d() {
        // Mirrors PolyhedronBoundaryComplexTests.CutOutTest_Half but additionally
        // verifies pos4d preservation on every surviving boundary vertex.
        var pbc = new Polyhedron3dBoundaryComplex(new int[] { 0, 0, 0 });
        var cutter = PolyhedronCreate.Cube3dAt(new Point(-0.5, 0, 0), 1);
        pbc.CutOut(cutter);
        AssertAllBoundaryVerticesHavePos4dMatching3d(pbc);
    }

    [Test] public void CutOut_L_AllVerticesRetainPos4d() {
        // Mirrors CutOutTest_L (more cut edges than the half-cube cut).
        var pbc = new Polyhedron3dBoundaryComplex(new int[] { 0, 0, 0 });
        var cutter = PolyhedronCreate.Cube3dAt(new Point(-0.5, -0.5, 0), 1);
        pbc.CutOut(cutter);
        AssertAllBoundaryVerticesHavePos4dMatching3d(pbc);
    }

    [Test] public void CutOut_TwoCubes_AllVerticesRetainPos4d() {
        // Two adjacent cubes form a 2×1×1 bar; cut diagonally to produce a cut that
        // crosses the inter-cube boundary edge.
        var pbc = new Polyhedron3dBoundaryComplex(new int[][] {
            new int[] { 0, 0, 0 }, new int[] { 1, 0, 0 }
        });
        var cutter = PolyhedronCreate.Cube3dAt(new Point(0.5, 0.5, 0), 1);
        pbc.CutOut(cutter);
        AssertAllBoundaryVerticesHavePos4dMatching3d(pbc);
    }

    // EdgeBC(ic, cam=null) initializes pos4d directly from the integer cell origin,
    // so for null-camera tests pos4d must equal the 3D point coordinates component-wise.
    // Edge.Split's lerp preserves this identity for cut-introduced vertices, and
    // OpposingClone (after the fix) preserves it for the OpposingClone'd cut-edge
    // endpoints.  A violation means a vertex was created somewhere that doesn't go
    // through the pos4d-preserving path.
    static void AssertAllBoundaryVerticesHavePos4dMatching3d(Polyhedron3dBoundaryComplex pbc) {
        int total = 0, nullCount = 0, mismatchCount = 0;
        double worstErr = 0;
        foreach (var face in pbc.BoundaryFacets()) {
            foreach (var edge in face.edges) {
                foreach (var v in new[] { edge.a, edge.b }) {
                    total++;
                    if (v.pos4d == null) { nullCount++; continue; }
                    var p = v.getPoint();
                    int n = System.Math.Min(p.x.Length, v.pos4d.Length);
                    for (int i = 0; i < n; i++) {
                        double err = System.Math.Abs(p.x[i] - v.pos4d[i]);
                        if (err > worstErr) worstErr = err;
                        if (err > 1e-6) { mismatchCount++; break; }
                    }
                }
            }
        }
        Assert.That(nullCount, Is.Zero,
            $"After CutOut, every boundary vertex must have pos4d set ({nullCount}/{total} null).");
        Assert.That(mismatchCount, Is.Zero,
            $"After CutOut, pos4d must match the 3D coords for null-camera EdgeBC ({mismatchCount}/{total} mismatched, worst={worstErr:G3}).");
    }
}
}
