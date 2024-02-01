using System.Collections.Generic;
using NUnit.Framework;
using System.Linq;
using System;
using D4BB.Comb;
using D4BB.Geometry;

namespace D4BB.Transforms {
public class Face2dBCTests
{
    [Test] public void PolyhedronFromRectangularCell() {
        var c1 = Face2dBC.FromIntegerCell(new int[]{0,0,0});
        Assert.That(c1.Faces(4), Is.Empty);
        Assert.That(c1.Faces(3), Has.Count.EqualTo(1));
        Assert.That(c1.facets, Has.Count.EqualTo(6));
        Assert.That(c1.Faces(2), Has.Count.EqualTo(6));
        Assert.That(c1.SubFacets(), Has.Count.EqualTo(12));
        Assert.That(c1.Faces(1), Has.Count.EqualTo(12));
        Assert.That(c1.Faces(0), Has.Count.EqualTo(8));

        var vertices = c1.Faces(0);
        Assert.That(vertices.Contains(new Vertex(new Point(0,0,0),false)));
        Assert.That(vertices.Contains(new Vertex(new Point(1,1,1),false)));
    }
    [Test] public void Cut3d() {
        var c1 = Face2dBC.FromIntegerCell(new int[]{0,0,0});
        var cutPlane = new HalfSpace(new Point(0.5,0,0),new Point(1,0,0));
        var res = c1.Split(cutPlane);
        Assert.That(res.inner.Dim(), Is.EqualTo(3));
        Assert.That(res.innerCut.Dim(), Is.EqualTo(2));
        Assert.That(res.outer.Dim(), Is.EqualTo(3));
        Assert.That(res.inner.Faces(0), Has.Count.EqualTo(8));
        Assert.That(res.innerCut.Faces(0), Has.Count.EqualTo(4));
        Assert.That(res.outer.Faces(0), Has.Count.EqualTo(8));
        Assert.That(res.innerCut.isInvisible);
        var ocp = res.outer.CenterPoint();
        var icp = res.inner.CenterPoint();
        Assert.That(((Face2d)res.outerCut).HalfSpaceOf().side(ocp), Is.EqualTo(D4BB.Geometry.HalfSpace.INSIDE));
        Assert.That(((Face2d)res.innerCut).HalfSpaceOf().side(icp), Is.EqualTo(D4BB.Geometry.HalfSpace.INSIDE));
        foreach (var facet in res.outer.facets) {
            Assert.That(((Face2d)facet).HalfSpaceOf().side(ocp), Is.EqualTo(D4BB.Geometry.HalfSpace.INSIDE));
        }
        foreach (var facet in res.inner.facets) {
            Assert.That(((Face2d)facet).HalfSpaceOf().side(icp), Is.EqualTo(D4BB.Geometry.HalfSpace.INSIDE));
        }
        var expCut = new Face2d(new List<Point>(){
            new(0.5,0,0),new(0.5,0,1),new(0.5,1,1),new(0.5,1,0)
        });
        Assert.That(res.innerCut,Is.EqualTo(expCut));
        Assert.That(new HashSet<IPolyhedron>{res.innerCut}, Is.SubsetOf(res.inner.facets));
        IPolyhedron facetFound;
        res.inner.facets.TryGetValue(res.innerCut,out facetFound);
        Assert.That(facetFound.isInvisible);

        var facet1 = new Face2d(new List<Point>(){
            new(0.5,0,0),new(0.5,0.5,0),new(0.5,0.5,1),new(0.5,0,1)
        });
//        var facetSplit = facetFound.Split(new HalfSpace(new Point(0,0.5,0),new Point(0,1,0)));
//        Assert.That(facetSplit.inner,Is.EqualTo(facet1));

        var res2 = res.inner.Split(new HalfSpace(new Point(0,0.5,0),new Point(0,1,0)));
        Assert.That(res2.inner.facets,Is.SupersetOf(new HashSet<IPolyhedron>{res2.innerCut}));
        Assert.That(res2.inner.facets,Is.SupersetOf(new HashSet<IPolyhedron>{facet1}));
        IPolyhedron polyFacet1;
        res2.inner.facets.TryGetValue(facet1,out polyFacet1);
        Assert.That(polyFacet1.isInvisible);

        //facet contained in cut
        c1 = Face2dBC.FromIntegerCell(new int[]{0,0,0});
        cutPlane = new HalfSpace(new Point(0,0,0),new Point(1,0,0));
        res = c1.Split(cutPlane);
        Assert.That(res.inner, Is.EqualTo(null));
        Assert.That(res.outerCut.Dim(), Is.EqualTo(2));
        Assert.That(res.outer.Dim(), Is.EqualTo(3));
        Assert.That(res.outer, Is.EqualTo(c1));
        Assert.That(!res.outerCut.isInvisible);

        //vertex (0,0,0) contained in cutPlane, otherwise empty cut
        c1 = Face2dBC.FromIntegerCell(new int[]{0,0,0});
        cutPlane = new HalfSpace(new Point(0,0,0),new Point(1,1,1).multiply(1/Math.Sqrt(3)));
        res = c1.Split(cutPlane);
        Assert.That(res.inner, Is.EqualTo(null));
        Assert.That(res.innerCut, Is.EqualTo(null));
        Assert.That(res.outer.Dim(), Is.EqualTo(3));
        Assert.That(res.outer.facets.SetEquals(c1.facets));
        Assert.That(res.outer.facets.Except(c1.facets),Is.Empty);

        //no vertices contained in cutPlane, non-empty cut
        c1 = Face2dBC.FromIntegerCell(new int[]{0,0,0});
        cutPlane = new HalfSpace(new Point(1,1,1).multiply(1.0/2),new Point(1,1,1).multiply(1.0/Math.Sqrt(3)));
        res = c1.Split(cutPlane);
        Assert.That(res.inner.Faces(0), Has.Count.EqualTo(10));
        Assert.That(res.innerCut.Faces(0), Has.Count.EqualTo(6));
        Assert.That(res.outer.Faces(0), Has.Count.EqualTo(10));
        Assert.That(res.innerCut.isInvisible);

        //3 vertices contained in cutPlane, non-empty cut
        c1 = Face2dBC.FromIntegerCell(new int[]{0,0,0});
        cutPlane = new HalfSpace(1.0/Math.Sqrt(3),new Point(1,1,1).multiply(1.0/Math.Sqrt(3)));
        res = c1.Split(cutPlane);
        Assert.That(res.inner.Faces(0), Has.Count.EqualTo(4));
        Assert.That(res.innerCut.Faces(0), Has.Count.EqualTo(3));
        Assert.That(res.outer.Faces(0), Has.Count.EqualTo(7));
        Assert.That(res.innerCut.isInvisible);

        //edge contained in cutPlane, otherwise empty cut
        c1 = Face2dBC.FromIntegerCell(new int[]{0,0,0});
        cutPlane = new HalfSpace(new Point(0,0,0),new Point(1,1,0).multiply(1/Math.Sqrt(2)));
        res = c1.Split(cutPlane);
        Assert.That(res.inner, Is.EqualTo(null));
        Assert.That(res.innerCut, Is.EqualTo(null));
        Assert.That(res.outer.Dim(), Is.EqualTo(3));
        Assert.That(res.outer, Is.EqualTo(c1));
    }
    [Test] public void Triangulation2d() {
        {
            var square = new Face2dBC(new List<Point>(){
                new Point(0,0,0), new Point(1,0,0), new Point(1,1,0), new Point(0,1,0)
            },false, null);
            var centerPoint = square.CenterPoint();
            var triangles = square.CenterTriangulation2d(centerPoint);
            Assert.That(triangles, Has.Count.EqualTo(4));
            var center = new Point(0.5,0.5,0);
            foreach (var triangle in triangles) {
                var vs = triangle.points;
                Assert.That(vs.Contains(center));
                Assert.That(vs, Has.Count.EqualTo(3));
            }
        }
        {
            var square = new Face2dBC(new Face2d(new List<Point>(){
                new Point(0,0,0), new Point(1,0,0), new Point(1,1,0), new Point(0,1,0)
            }).facets,false,null);
            var triangles = square.BoundaryTriangulation2d();
            Assert.That(triangles, Has.Count.EqualTo(2));
            foreach (var triangle in triangles) {
                var vs = triangle.points;
                Assert.That(vs.Contains(new Point(0,0,0)));
                Assert.That(vs, Has.Count.EqualTo(3));
            }
        }
    }
    [Test] public void ColinearBoundaryTriangulation() {
        List<Point> points = new() {
            new(1,0,0),//0
            new(2,0,0),//1
            new(3,0,0),//2
            new(4,0,0),//3
            new(4,1,0),//4
            new(0,1,0),//5
            new(0,0,0),//6
        };
        var rectangle = new Face2d(points);
        var triangles = rectangle.BoundaryTriangulation2d();
        Assert.That(triangles[0],Is.EqualTo(new Face2d(new List<Point>{points[0],points[1],points[4]})));
        Assert.That(triangles[1],Is.EqualTo(new Face2d(new List<Point>{points[1],points[2],points[4]})));
        Assert.That(triangles[2],Is.EqualTo(new Face2d(new List<Point>{points[2],points[3],points[4]})));
        Assert.That(triangles[3],Is.EqualTo(new Face2d(new List<Point>{points[0],points[4],points[5]})));
        Assert.That(triangles[4],Is.EqualTo(new Face2d(new List<Point>{points[0],points[5],points[6]})));
        Assert.That(triangles.Count==5);
    }
    [Test] public void ColinearBoundaryTriangulation2() {
        List<Point> points = new() {
            new(1,0,0),//0
            new(2,0,0),//1
            new(3,0,0),//2
            new(4,0,0),//3
            new(4,1,0),//4
            new(0,1,0),//5
            new(0,0,0),//6
            new(0.5,0,0),//7
        };
        var rectangle = new Face2d(points);
        var triangles = rectangle.BoundaryTriangulation2d();
        Assert.That(triangles[0],Is.EqualTo(new Face2d(new List<Point>{points[0],points[1],points[4]})));
        Assert.That(triangles[1],Is.EqualTo(new Face2d(new List<Point>{points[1],points[2],points[4]})));
        Assert.That(triangles[2],Is.EqualTo(new Face2d(new List<Point>{points[2],points[3],points[4]})));
        Assert.That(triangles[3],Is.EqualTo(new Face2d(new List<Point>{points[0],points[4],points[5]})));
        Assert.That(triangles[4],Is.EqualTo(new Face2d(new List<Point>{points[7],points[5],points[6]})));
        Assert.That(triangles[5],Is.EqualTo(new Face2d(new List<Point>{points[0],points[5],points[7]})));
        Assert.That(triangles.Count==6);
    }
    [Test] public void TriangleBoundaryTriangulation() {
        var triangle = new Face2d(new List<Point>{
            new(0,0,0),new(1,0,0),new(1,1,0)
        });
        var triangles = triangle.BoundaryTriangulation2d();
        Assert.That(triangles,Has.Count.EqualTo(1));
        Assert.That(triangles[0],Is.EqualTo(triangle));
    }
    [Test] public void TriangleDivision10BoundaryTriangulation() {
        var triangle = new Face2d(new List<Point>{
            new(0,0,0),new(0.5,0,0),new(1,0,0),new(1,1,0)
        });
        var triangles = triangle.BoundaryTriangulation2d();
        Assert.That(triangles,Has.Count.EqualTo(2));
        //Assert.That(triangles[0],Is.EqualTo(triangle));
    }
    [Test] public void TriangleDivision01BoundaryTriangulation() {
        var triangle = new Face2d(new List<Point>{
            new(0,0,0),new(1,0,0),new(0,1,0),new(0,0.5,0)
        });
        var triangles = triangle.BoundaryTriangulation2d();
        Assert.That(triangles,Has.Count.EqualTo(2));
        //Assert.That(triangles[0],Is.EqualTo(triangle));
    }
    [Test] public void TriangleDivision11BoundaryTriangulation() {
        var points = new List<Point>{
            new(0,0,0),new(0.5,0,0),new(1,0,0),new(0,1,0),new(0,0.5,0)
        };
        var triangle = new Face2d(points);
        var triangles = triangle.BoundaryTriangulation2d();
        Assert.That(triangles,Has.Count.EqualTo(3));
        Assert.That(triangles[0],Is.EqualTo(new Face2d(new List<Point>{points[1],points[2],points[3]})));
        Assert.That(triangles[1],Is.EqualTo(new Face2d(new List<Point>{points[1],points[3],points[4]})));
        Assert.That(triangles[2],Is.EqualTo(new Face2d(new List<Point>{points[1],points[4],points[0]})));
    }
    [Test] public void TriangleDivisionBase11BoundaryTriangulation() {
        var points = new List<Point>{
            new(0,0,0),new(1,0,0),new(2,0,0),new(0,1,0),new(-2,0,0),new(-1,0,0)
        };
        var triangle = new Face2d(points);
        var triangles = triangle.BoundaryTriangulation2d();
        Assert.That(triangles,Has.Count.EqualTo(4));
        Assert.That(triangles[0],Is.EqualTo(new Face2d(new List<Point>{points[0],points[1],points[3]})));
        Assert.That(triangles[1],Is.EqualTo(new Face2d(new List<Point>{points[1],points[2],points[3]})));
        Assert.That(triangles[2],Is.EqualTo(new Face2d(new List<Point>{points[3],points[4],points[5]})));
        Assert.That(triangles[3],Is.EqualTo(new Face2d(new List<Point>{points[3],points[5],points[0]})));
    }
    [Test] public void PolyhedronSpanningPoints() {
        
        HashSet<Point> svs;
        {
        var polyhedron = new Edge(new Point(0,0,0),new Point(0,0,1));
        svs = new HashSet<Point>(((IPolyhedron)polyhedron).SpanningPoints());
        Assert.That(svs.Contains(new Point(0,0,0)));
        Assert.That(svs.Contains(new Point(0,0,1)));
        }
        {
        var polyhedron = new Face2d(new Point(0,0,0),new Point(0,0,1),new Point(0,1,1),false);
        svs = new HashSet<Point>(((IPolyhedron)polyhedron).SpanningPoints());
        Assert.That(svs, Has.Count.EqualTo(3));
        Assert.That(AOP.SpanningDim(new List<Point>(svs)),Is.EqualTo(2));
        }
        {
        var polyhedron = new Face2d(new List<Point> {
            new Point(0,0,0),new Point(0,0,1),new Point(0,1,1), new Point(0,1,0)},false);
        svs = new HashSet<Point>(((IPolyhedron)polyhedron).SpanningPoints());
        Assert.That(svs, Has.Count.EqualTo(3));
        Assert.That(AOP.SpanningDim(new List<Point>(svs)),Is.EqualTo(2));
        }
        {
        var polyhedron = Face2dBC.FromIntegerCell(new int[]{0,0,0});
        svs = new HashSet<Point>(polyhedron.SpanningPoints());
        Assert.That(svs, Has.Count.EqualTo(4));
        Assert.That(AOP.SpanningDim(new List<Point>(svs)),Is.EqualTo(3));
        }
    }
    [Test] public void Normals() {
        IPolyhedron polyhedron;
        Dictionary<IPolyhedron,Point> normals;
        HashSet<Point> expected;
        polyhedron = Face2dBC.FromIntegerCell(new int[]{0,0,0});
        normals = polyhedron.Normals();
        expected = new HashSet<Point>(){
            new(1,0,0), new(0,1,0),new(0,0,1),new(-1,0,0), new(0,-1,0), new(0,0,-1) };
        Assert.That(normals.Values.ToHashSet().SetEquals(expected));
        Assert.That(normals[new Face2d(new List<Point>() {
            new Point(0,0,0),new Point(0,0,1),new Point(0,1,1),new Point(0,1,0)},false)],
            Is.EqualTo(new Point(-1,0,0)));
        Assert.That(normals[new Face2d(new List<Point>() {
            new Point(1,0,0),new Point(1,0,1),new Point(1,1,1),new Point(1,1,0)})],
            Is.EqualTo(new Point(1,0,0)));

        polyhedron = Face2dBC.FromIntegerCell(new int[]{1,0,0});
        normals = polyhedron.Normals();
        expected = new HashSet<Point>(){
            new(1,0,0), new(0,1,0),new(0,0,1),new(-1,0,0), new(0,-1,0), new(0,0,-1) };
        Assert.That(normals.Values.ToHashSet().SetEquals(expected));

        polyhedron = PolyhedronCreate.Cube3dAt(new Point(0.5,0.5,0.5),2);
        normals = polyhedron.Normals();
        expected = new HashSet<Point>(){
            new(1,0,0), new(0,1,0),new(0,0,1),new(-1,0,0), new(0,-1,0), new(0,0,-1) };
        Assert.That(normals.Values.ToHashSet().SetEquals(expected));
    }
    [Test] public void SideTest() {
        var edge = new Edge(new Point(0,0,0),new Point(1,0,0));
        Assert.That(((IPolyhedron)edge).Side(new HalfSpace(0,new Point(1,0,0))),Is.EqualTo(SplitResult.TOUCHING_OUTSIDE));
        
        var facet = new Face2d(new List<Point>{
            new(0,0,0),new(1,0,0),new(1,0,1),new(0,0,1)
        });
        var side = ((IPolyhedron)facet).Side(new HalfSpace(0,new Point(1,0,0)));
        Assert.That(side,Is.EqualTo(SplitResult.TOUCHING_OUTSIDE));

        var cube = Face2dBC.FromIntegerCell(new int[]{0,0,0});
        Assert.That(cube.Side(new HalfSpace(-1,new Point(1,0,0))),Is.EqualTo(SplitResult.GENUINE_OUTSIDE));
        Assert.That(cube.Side(new HalfSpace(0,new Point(1,0,0))),Is.EqualTo(SplitResult.TOUCHING_OUTSIDE));
        Assert.That(cube.Side(new HalfSpace(0.5,new Point(1,0,0))),Is.EqualTo(SplitResult.GENUINE_CUT));
        Assert.That(cube.Side(new HalfSpace(1,new Point(1,0,0))),Is.EqualTo(SplitResult.TOUCHING_INSIDE));
        Assert.That(cube.Side(new HalfSpace(2,new Point(1,0,0))),Is.EqualTo(SplitResult.GENUINE_INSIDE));
    }
     [Test] public void CubeDoubleSplit() {
        var pbc = new Polyhedron3dBoundaryComplex(new IntegerBoundaryComplex(new int[][]{new int[]{0,0,0},}));
        HalfSpace cutLeftRight = new HalfSpace(0.5,new Point(1,0,0));
        HalfSpace cutUpDown =    new HalfSpace(0.5,new Point(0,1,0));
        pbc.CutOut(new HalfSpace[] {cutLeftRight,cutUpDown});
        var visibleFacets = pbc.VisibleFacets();
        Assert.That(visibleFacets, Has.Count.EqualTo(8));
        var cm = new FacetsGenericMesh(pbc.facets.Cast<Face2d>().ToHashSet());
    }
    [Test] public void DiagonalCubeSplit() {
        IntegerBoundaryComplex ibc = new(new int[]{0,0,0});
        var pbc = new Polyhedron3dBoundaryComplex(ibc);
        HalfSpace diagonal = new HalfSpace(Math.Sqrt(2.0)/2,new Point(1,1,0).normalize());
        pbc.CutOut(new HalfSpace[]{diagonal});
        Assert.That(pbc.facets,Has.Count.EqualTo(4));
    }
    [Test] public void PreSplittedCut3d() {
        var nonSplittedFront = new Face2d(new List<Point>{new(0,0,0),new(0,1,0),new(1,1,0),new(1,0,0)});
        var nonSplittedBack = new Face2d(new List<Point>{new(0,0,1),new(1,0,1),new(1,1,1),new(0,1,1)});
        //var nonSplittedTop = new Face2d(new List<Point>{new(0,1,0),new(0,1,1),new(1,1,1),new(1,1,0)});
        var preSplittedTop = new Face2d(new List<Point>{new(0,1,0),new(0,1,1),new(0.5,1,1),new(1,1,1),new(1,1,0),new(0.5,1,0)});
        IntegerBoundaryComplex ibc = new(new int[]{0,0,0});
        var pbc = new Polyhedron3dBoundaryComplex(ibc);
        var indexFront = -1;
        for (int i=0;i<pbc.facets.Count;i++) if (pbc.facets[i].Equals(nonSplittedFront)) {indexFront=i;break;}
        Assert.That(indexFront,Is.Not.EqualTo(-1)); //==2
        var frontFace = pbc.facets[indexFront];
        var indexBack = -1;
        for (int i=0;i<pbc.facets.Count;i++) if (pbc.facets[i].Equals(nonSplittedBack)) {indexBack=i;break;}
        Assert.That(indexBack,Is.Not.EqualTo(-1));  //==5
        var backFace = pbc.facets[indexBack];
        //indexTop == 4

        HalfSpace halfSpace = new HalfSpace(0.5,new Point(new double[]{1,0,0}).normalize());
        
        SplitResult srFront = frontFace.Split(halfSpace);
        Assert.That(srFront.inner,Is.Not.Null);
        Assert.That(srFront.outer,Is.Not.Null);
        pbc.Replace(frontFace,(Face2dBC)srFront.inner,(Face2dBC)srFront.outer);
        
        SplitResult srBack = backFace.Split(halfSpace);
        Assert.That(srBack.inner,Is.Not.Null);
        Assert.That(srBack.outer,Is.Not.Null);
        pbc.Replace(backFace,(Face2dBC)srBack.inner,(Face2dBC)srBack.outer);
        Assert.That(pbc.facets,Has.Count.EqualTo(8));

        var indexTop = -1;
        for (int i=0;i<pbc.facets.Count;i++) if (pbc.facets[i].Equals(preSplittedTop)) {indexTop=i;break;}
        Assert.That(indexTop,Is.Not.EqualTo(-1));
        var topFace = pbc.facets[indexTop];
        SplitResult srTop = topFace.Split(halfSpace);
        Assert.That(srTop.inner,Is.Not.Null);
        Assert.That(srTop.outer,Is.Not.Null);
        pbc.Replace(topFace,(Face2dBC)srTop.inner,(Face2dBC)srTop.outer);
        Assert.That(pbc.facets,Has.Count.EqualTo(9));

        pbc.CutOut(new HalfSpace[]{halfSpace});
        Assert.That(pbc.facets,Has.Count.EqualTo(5));
    }
    [Test] public void DiagSplittedCube() {
        IntegerBoundaryComplex ibc = new(new int[]{0,0,0});

        HalfSpace hs1 = new HalfSpace(new Point(0.6,0.5,0),new Point(1,1,0).normalize());
        HalfSpace hs2 = new HalfSpace(new Point(0.5,0.5,0),new Point(-1,1,0).normalize());

        {
            var pbc = new Polyhedron3dBoundaryComplex(ibc);
            pbc.CutOut(new HalfSpace[]{hs1});
            Assert.That(pbc.facets,Has.Count.EqualTo(4));
        }
        {
            var pbc = new Polyhedron3dBoundaryComplex(ibc);
            pbc.CutOut(new HalfSpace[]{hs2});
            Assert.That(pbc.facets,Has.Count.EqualTo(4));
        }
        {
            var pbc = new Polyhedron3dBoundaryComplex(ibc);
            List<Face2dBC> i = new();
            List<Face2dBC> o = new();
            Polyhedron3dBoundaryComplex.Split(hs1,pbc.facets,i,o);
            Assert.That(i,Has.Count.EqualTo(6));
            Assert.That(o,Has.Count.EqualTo(4));
            List<Face2dBC> ii = new();
            List<Face2dBC> io = new();
            Polyhedron3dBoundaryComplex.Split(hs2,i,ii,io);
            Assert.That(ii,Has.Count.EqualTo(4));
            Assert.That(io,Has.Count.EqualTo(4));
        }
        {
            var pbc = new Polyhedron3dBoundaryComplex(ibc);
            pbc.CutOut(new HalfSpace[]{hs1,hs2});
            Assert.That(pbc.facets,Has.Count.EqualTo(7));
        }
    }
    [Test] public void CutOutGroove() {
        IntegerBoundaryComplex ibc = new(new int[]{0,0,0});
        {
            var pbc = new Polyhedron3dBoundaryComplex(ibc);
            HalfSpace hs1 = new HalfSpace(new Point(0.5,0.5,0),new Point(1,1,0).normalize());
            HalfSpace hs2 = new HalfSpace(new Point(0.5,0.5,0),new Point(-1,1,0).normalize());
            pbc.CutOut(new HalfSpace[]{hs1,hs2});
            Assert.That(pbc.facets,Has.Count.EqualTo(7));
        }
        {
            var pbc = new Polyhedron3dBoundaryComplex(ibc);
            HalfSpace hs1 = new HalfSpace(new Point(0.6,0.5,0),new Point(1,1,0).normalize());
            HalfSpace hs2 = new HalfSpace(new Point(0.5,0.5,0),new Point(-1,1,0).normalize());
            pbc.CutOut(new HalfSpace[]{hs1,hs2});
            Assert.That(pbc.facets,Has.Count.EqualTo(7));
        }
        {
            var pbc = new Polyhedron3dBoundaryComplex(ibc);
            HalfSpace hs1 = new HalfSpace(new Point(0.4,0.5,0),new Point(1,1,0).normalize());
            HalfSpace hs2 = new HalfSpace(new Point(0.5,0.5,0),new Point(-1,1,0).normalize());
            pbc.CutOut(new HalfSpace[]{hs1,hs2});
            Assert.That(pbc.facets,Has.Count.EqualTo(8));
        }
    }
}
}
