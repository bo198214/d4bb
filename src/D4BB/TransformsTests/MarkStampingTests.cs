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

    // Compound of two 4D tesseracts touching at the (1,_,_,_)-hyperplane: their shared
    // internal 3-cell has 6 boundary 2-faces. These 6 are the Grid-Division interior
    // faces — invisible by default, visible when showGridDivisions=true.
    [Test] public void Scene4d_TwoTesseractsCompound_GridDivisionFaces_Lazy() {
        var origins = new int[][][] {
            new int[][] { new int[] {0,0,0,0}, new int[] {1,0,0,0} }   // one piece, two tesseracts
        };
        var camera = new Camera4dParallel();

        // showGridDivisions=false → no interior faces created (lazy)
        var sceneOff = new Scene4d(origins, camera, showIntraCoplanarEdges: false, cullBackFaces: false, showGridDivisions: false);
        sceneOff.enable4dOcclusion = false;
        sceneOff.Update(origins);
        int facesOff = sceneOff.VisibleFacets(0).Count;

        // showGridDivisions=true → interior division faces added
        var sceneOn = new Scene4d(origins, camera, showIntraCoplanarEdges: false, cullBackFaces: false, showGridDivisions: true);
        sceneOn.enable4dOcclusion = false;
        sceneOn.Update(origins);
        int facesOn = sceneOn.VisibleFacets(0).Count;

        Assert.That(facesOn, Is.GreaterThan(facesOff),
            "showGridDivisions=true must expose at least one extra Face2dBC compared to =false.");
        Assert.That(facesOn - facesOff, Is.EqualTo(6),
            "Two touching tesseracts share an internal hexahedral 3-cell with 6 boundary 2-faces.");

        // Every extra face has mark=GRID_DIVISION and is isCoplanarInterior=true
        var offSet = new HashSet<Face2d>(sceneOff.VisibleFacets(0), new Face2dUnOrientedEquality(AOP.binaryPrecision));
        foreach (var f in sceneOn.VisibleFacets(0))
        {
            if (offSet.Contains(f)) continue;
            Assert.That(f.mark, Is.EqualTo(GI), "Extra face must be marked as Grid-Division.");
            Assert.That(f.isCoplanarInterior, Is.True, "Extra face must be marked isCoplanarInterior.");
        }
    }
}
}
