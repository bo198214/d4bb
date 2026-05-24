using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using D4BB.Comb;
using D4BB.Geometry;

namespace D4BB.Transforms {

// Stamping tests for the *BC pipeline: every polyhedron originating from an IntegerCell
// is tagged with mark=MARK_GRID_DIVISION so the showGridDivisions toggle can
// distinguish them from BSP-cut geometry (mark=MARK_NONE).
public class MarkStampingTests
{
    const int GI = IPolyhedron.MARK_GRID_DIVISION;
    const int NONE = IPolyhedron.MARK_NONE;

    [Test] public void Face2dBC_FromIntegerCell_StampsGridIntersection() {
        var cube = Face2dBC.FromIntegerCell(new int[] { 0, 0, 0 });
        Assert.That(cube.mark, Is.EqualTo(GI), "3-cell from IntegerCell is grid-intersection.");
        foreach (var face in cube.facets) {
            Assert.That(face.mark, Is.EqualTo(GI), "2-face from IntegerCell is grid-intersection.");
            foreach (var edge in face.facets) {
                Assert.That(edge.mark, Is.EqualTo(GI), "Edge from IntegerCell is grid-intersection.");
                foreach (var vertex in edge.facets) {
                    Assert.That(vertex.mark, Is.EqualTo(GI), "Vertex from IntegerCell is grid-intersection.");
                }
            }
        }
    }

    [Test] public void Face2dBC_Cut_HalvesKeepMark_NewCutFaceIsNone() {
        var cube = Face2dBC.FromIntegerCell(new int[] { 0, 0, 0 });
        var cutPlane = new HalfSpace(new Point(0.5, 0, 0), new Point(1, 0, 0));
        var sr = cube.Split(cutPlane);

        Assert.That(sr.inner.mark, Is.EqualTo(GI), "Inner half of grid cell is still grid.");
        Assert.That(sr.outer.mark, Is.EqualTo(GI), "Outer half of grid cell is still grid.");
        Assert.That(sr.innerCut.mark, Is.EqualTo(NONE), "New cut face at split plane is not grid.");
        Assert.That(sr.outerCut.mark, Is.EqualTo(NONE), "New cut face (outer) at split plane is not grid.");
    }
}
}
