using System.Collections.Generic;
using D4BB.Comb;
using D4BB.Geometry;
using D4BB.Transforms;
using NUnit.Framework;

public class BoundaryComplexTests {
    [Test] public void Polyhedron3dBoundaryComplex_SingleCube_Numbers() {
        var cube = new Polyhedron3dBoundaryComplex(new IntegerBoundaryComplex(new int[]{0,0,0}));
        Assert.That(cube.VisibleFacets(),Has.Count.EqualTo(6));
        Assert.That(cube.VisibleEdges(),Has.Count.EqualTo(12));
    }
    [Test] public void Polyhedron3dBoundaryComplex_Links() {
        var cube = new Polyhedron3dBoundaryComplex(new IntegerBoundaryComplex(new int[]{0,0,0}));
        var iFront = new OrientedIntegerCell(new int[]{0,0,0},new HashSet<int>{0,1},true,true);
        var front = new Face2dBC(iFront);
        var left  = new Face2dBC(new OrientedIntegerCell(new int[]{0,0,0},new HashSet<int>{1,2},true,true));
        var bottom= new Face2dBC(new OrientedIntegerCell(new int[]{0,0,0},new HashSet<int>{0,2},true,true));
        var frontIndex = cube.facets.IndexOf(front);
        var bottomIndex = cube.facets.IndexOf(bottom);
        var leftIndex = cube.facets.IndexOf(left);
        Assert.That(frontIndex,Is.GreaterThanOrEqualTo(0));
        Assert.That(bottomIndex,Is.GreaterThanOrEqualTo(0));
        Assert.That(leftIndex,Is.GreaterThanOrEqualTo(0));
        var frontInCube  = cube.facets[frontIndex];
        var bottomInCube = cube.facets[bottomIndex];
        var leftInCube   = cube.facets[leftIndex];
        var frontLeftEdge = frontInCube.i2p[new IntegerCell(new int[]{0,0,0},new HashSet<int>{1})];
        var leftFrontEdge =  leftInCube.i2p[new IntegerCell(new int[]{0,0,0},new HashSet<int>{1})];
        Assert.That(frontLeftEdge.neighbor,Is.SameAs(leftFrontEdge));
        Assert.That(leftFrontEdge.neighbor,Is.SameAs(frontLeftEdge));
        Assert.That(frontLeftEdge.a.getPoint(),Is.EqualTo(new Point(0,0,0)));
        Assert.That(leftFrontEdge.b.getPoint(),Is.EqualTo(new Point(0,0,0)));
        var frontBottomEdge = frontInCube.i2p[new IntegerCell(new int[]{0,0,0},new HashSet<int>{0})];
        var bottomFrontEdge = bottomInCube.i2p[new IntegerCell(new int[]{0,0,0},new HashSet<int>{0})];
        Assert.That(frontBottomEdge.neighbor,Is.SameAs(bottomFrontEdge));
        Assert.That(bottomFrontEdge.neighbor,Is.SameAs(frontBottomEdge));
        Assert.That(frontBottomEdge.b.getPoint(),Is.EqualTo(new Point(0,0,0)));
        Assert.That(bottomFrontEdge.a.getPoint(),Is.EqualTo(new Point(0,0,0)));
    }
    [Test] public void Polyhedron3dBoundaryComplex_TwoCubes_Numbers() {
        var cubes = new Polyhedron3dBoundaryComplex(new IntegerBoundaryComplex(new int[][] {new int[]{0,0,0},new int[]{1,0,0}}));
        Assert.That(cubes.VisibleFacets(),Has.Count.EqualTo(10));
        Assert.That(cubes.VisibleEdges(),Has.Count.EqualTo(16));
    }
    [Test] public void Polyhedron3dBoundaryComplex_TwoCubes_Links() {
        var cubes = new Polyhedron3dBoundaryComplex(new IntegerBoundaryComplex(new int[][] {new int[]{0,0,0},new int[]{1,0,0}}));
        var front1 = new Face2dBC(new OrientedIntegerCell(new int[]{0,0,0},new HashSet<int>{0,1},true,true));
        var front2 = new Face2dBC(new OrientedIntegerCell(new int[]{1,0,0},new HashSet<int>{0,1},true,true));
        var front1Index = cubes.facets.IndexOf(front1);
        var front2Index = cubes.facets.IndexOf(front2);
        Assert.That(front1Index,Is.GreaterThanOrEqualTo(0));
        Assert.That(front2Index,Is.GreaterThanOrEqualTo(0));
        var front1InComplex = cubes.facets[front1Index];
        var front2InComplex = cubes.facets[front2Index];
        var front1RightEdge = front1InComplex.i2p[new IntegerCell(new int[]{1,0,0},new HashSet<int>{1})];
        var front2LeftEdge =  front2InComplex.i2p[new IntegerCell(new int[]{1,0,0},new HashSet<int>{1})];
        Assert.That(front1RightEdge.neighbor,Is.SameAs(front2LeftEdge));
        Assert.That(front2LeftEdge.neighbor,Is.SameAs(front1RightEdge));
        Assert.That(front1RightEdge.b.getPoint(),Is.EqualTo(new Point(1,0,0)));
        Assert.That(front2LeftEdge.a.getPoint(),Is.EqualTo(new Point(1,0,0)));

        var iSandwich = new IntegerCell(new int[]{1,0,0},new HashSet<int>{1,2});
        Assert.That(!cubes.i2p.ContainsKey(iSandwich));
    }
    [Test] public void CutOutTest_Half() {
        var pbc = new Polyhedron3dBoundaryComplex(new IntegerBoundaryComplex(new int[]{0,0,0}));
        var polyhedron1 = PolyhedronCreate.Cube3dAt(new Point(-0.5,0,0),1);
        pbc.CutOut(polyhedron1);
        var facets = pbc.VisibleFacets();
        Assert.That(facets, Has.Count.EqualTo(5));
        Assert.That(pbc.VisibleEdges(), Has.Count.EqualTo(12));
    }
    [Test] public void CutOutTest_L() {
        var pbc = new Polyhedron3dBoundaryComplex(new IntegerBoundaryComplex(new int[]{0,0,0}));
        var polyhedron1 = PolyhedronCreate.Cube3dAt(new Point(-0.5,-0.5,0),1);
        pbc.CutOut(polyhedron1);
        var visibleFacets = pbc.VisibleFacets();
        Assert.That(visibleFacets, Has.Count.EqualTo(8));

        var edgeToFind = new Edge(new Point(0.5,0.5,0),new Point(0.5,1,0),true);
        List<EdgeBC> foundEdges = new();
        foreach (var facet in pbc.facets) {
            foreach (var edge in facet.facets) {
                if (edge.Equals(edgeToFind)) foundEdges.Add((EdgeBC)edge);
            }
        }
        Assert.That(foundEdges,Has.Count.EqualTo(2));
        var edge1 = foundEdges[0];
        var edge2 = foundEdges[1];
        Assert.That(edge1.neighbor,Is.SameAs(edge2));
        Assert.That(edge2.neighbor,Is.SameAs(edge1));

        var edges = pbc.VisibleEdges();
        Assert.That(edges, Has.Count.EqualTo(19));
    }
    [Test] public void CutOutTest() 
    {
        var pbc = new Polyhedron3dBoundaryComplex(new IntegerBoundaryComplex(new int[]{0,0,0}));
        foreach (var facet in pbc.facets) {
                Assert.That(facet.GetType(), Is.EqualTo(typeof(Face2dBC)));
                foreach (var edge in facet.facets) {
                    Assert.That(edge.GetType(), Is.EqualTo(typeof(EdgeBC)));
                }
        }
        var edges = pbc.VisibleEdges();
        Assert.That(edges, Has.Count.EqualTo(12));
        var polyhedron1 = PolyhedronCreate.Cube3dAt(new Point(-0.5,-0.5,-0.5),1);
        pbc.CutOut(polyhedron1);
        foreach (var facet in pbc.facets) {
                Assert.That(facet.GetType(), Is.EqualTo(typeof(Face2dBC)));
                foreach (var edge in facet.facets) {
                    Assert.That(edge.GetType(), Is.EqualTo(typeof(EdgeBC)));
                }
        }
        //Assert.That(pbc.VisibleFacets(), Has.Count.EqualTo(8+1+8+4));
        //Assert.That(pbc.VisibleEdges(), Has.Count.EqualTo(8+4+4+3+8));
        foreach (var facet in pbc.facets) {
            Assert.That(
                facet.GetType(), Is.EqualTo(typeof(Face2dBC)));
        }

        foreach (var facet in new List<Face2d>(){
            new Face2d(new List<Point>(){new(0.5,0,0),new(0.5,0,0.5),new(0.5,0.5,0.5),new(0.5,0.5,0)}, false),
            new Face2d(new List<Point>(){new(0,0.5,0),new(0,0.5,0.5),new(0.5,0.5,0.5),new(0.5,0.5,0)}, false),
            new Face2d(new List<Point>(){new(0.5,0,0),new(0.5,0.5,0),new(0.5,0.5,0.5),new(0.5,0,0.5)}, false),
        }) {
            Assert.That(pbc.facets,Does.Not.Contains(facet));
        }
    }

    [Test] public void Cutout4dSingleFromL() {
        var pbc = new Polyhedron3dBoundaryComplex(new IntegerBoundaryComplex(new int[][]{new int[]{0,0,0,2},new int[]{1,0,0,2}, new int[]{1,1,0,2},new int[]{1,0,0,0}}));
        
    }

}