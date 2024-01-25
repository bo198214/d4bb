using System.Collections.Generic;
using D4BB.Geometry;
using NUnit.Framework;
using System.Linq;
using System;
using D4BB.Transforms;
using System.Diagnostics;
using D4BB.Comb;

public class Geom3dTests
{
    
    [Test] public void CrossTest(
        [Random(-2.0, 2.0, 2)] double a1,
        [Random(-2.0, 2.0, 2)] double a2,
        [Random(-2.0, 2.0, 2)] double a3,
        [Random(-2.0, 2.0, 2)] double b1,
        [Random(-2.0, 2.0, 2)] double b2,
        [Random(-2.0, 2.0, 2)] double b3
    ) {
        double[] a = new double[]{a1,a2,a3};
        double[] b = new double[]{b1,b2,b3};
        var cross3d = AOP.cross(new Point(a),new Point(b)).x;
        var crossGen = AOP.Cross(new double[][]{a,b});
        Assert.That(cross3d,Is.EqualTo(crossGen));
    }
    [Test] public void AOP_Params2d() {
        var xv = new double[] { 1.5, 7.8 };
        var yv = new double[] { 3.5, 1.9 };
        var pv = new double[] { 3, 5};
        var ps = AOP.Params2d(xv,yv,pv);
        Assert.That(new Point(xv).multiply(ps[0]).add(new Point(yv).multiply(ps[1])),Is.EqualTo(new Point(pv)));
    }
    [Test] public void AOP_Params() {
        {
            var xv = new Point(2,0,0);
            var yv = new Point(1,1,0);
            var pv = new Point(4,2,0);
            var ps = AOP.Params(xv,yv,pv);
            Assert.That(AOP.eq(ps[0],1));
            Assert.That(AOP.eq(ps[1],2));
        }{
            var xv = new Point( 1.5, 7.8, 1.7 );
            var yv = new Point( 3.5, 1.9, 5.6 );
            var pv = xv.clone().multiply(2).add(yv.clone().multiply(3));
            var ps = AOP.Params(xv,yv,pv);
            Assert.That(AOP.eq(ps[0],2));
            Assert.That(AOP.eq(ps[1],3));
        }
    }
    [Test] public void HalfSpace() {
        var hs = new HalfSpace(1,new Point(1,0,0));
        Assert.That(hs.side(new Point(2,0,0)), Is.EqualTo(D4BB.Geometry.HalfSpace.OUTSIDE));
        Assert.That(hs.side(new Point(1,0,0)), Is.EqualTo(D4BB.Geometry.HalfSpace.CONTAINED));
        Assert.That(hs.side(new Point(0,0,0)), Is.EqualTo(D4BB.Geometry.HalfSpace.INSIDE));
        var hs2 = new HalfSpace(new Point(1,0,0),new Point(1,0,0));
        Assert.That(hs, Is.EqualTo(hs2));
        hs2 = hs.flip();
        Assert.That(!hs.Equals(hs2));
        hs = new HalfSpace(new Point(1,0,0),new Point(-1,0,0));
        Assert.That(hs, Is.EqualTo(hs2));
        Assert.That(hs.side(new Point(2,0,0)), Is.EqualTo(D4BB.Geometry.HalfSpace.INSIDE));
        Assert.That(hs.side(new Point(1,0,0)), Is.EqualTo(D4BB.Geometry.HalfSpace.CONTAINED));
        Assert.That(hs.side(new Point(0,0,0)), Is.EqualTo(D4BB.Geometry.HalfSpace.OUTSIDE));

        hs = new HalfSpace(new Point(-1,0,0),new Point(1,0,0));
        Assert.That(hs.length, Is.LessThan(0));
    }
    
    [Test] public void CutEdges() {
        var edge = new Edge(new Point(0,0,0), new Point(2,0,0));
        var edge2 = new Edge(new Point(0,0,0), new Point(2,0,0));
        Assert.That(edge, Is.EqualTo(edge2));

        var cutPlane = new HalfSpace(new Point(1,0,0),new Point(1,0,0));
        edge = new Edge(new Point(0,0,0), new Point(2,0,0));
        var res = edge.Split(cutPlane);
        Assert.That(res.inner, Is.EqualTo(new Edge(new Point(0,0,0), new Point(1,0,0))));
        Assert.That(res.innerCut, Is.EqualTo(new Vertex(new Point(1,0,0),false)));
        Assert.That(res.outer, Is.EqualTo(new Edge(new Point(1,0,0), new Point(2,0,0))));

        edge = new Edge(new Point(0,0,0), new Point(1,0,0));
        res = edge.Split(cutPlane);
        Assert.That(res.inner, Is.EqualTo(edge));
        Assert.That(res.innerCut.Equals(new Vertex(new Point(1,0,0),false)));
        Assert.That(res.outer== null);

        edge = new Edge(new Point(-1,0,0), new Point(0,0,0));
        res = edge.Split(cutPlane);
        Assert.That(res.inner.Equals(edge));
        Assert.That(res.innerCut, Is.EqualTo(null));
        Assert.That(res.outer, Is.EqualTo(null));

        edge = new Edge(new Point(1,-1,0), new Point(1,1,0));
        res = edge.Split(cutPlane);
        Assert.That(res.inner, Is.EqualTo(null));
        Assert.That(res.isContained);
        Assert.That(res.outer, Is.EqualTo(null));

        edge = new Edge(new Point(0,-1,0), new Point(0,1,0));
        cutPlane = new HalfSpace(new Point(0,0,0),new Point(1,0,0));
        res = edge.Split(cutPlane);
        Assert.That(res.inner, Is.EqualTo(null));
        Assert.That(res.isContained);
        Assert.That(res.outer, Is.EqualTo(null));
    }
    [Test] public void CutFacets() {
        {
            var cutPlane = new HalfSpace(new Point(0.5,0,0),new Point(1,0,0));
            var facet = new Face2d(new List<Point>(){
                new Point(0,0,0), new Point(1,0,0), new Point(1,1,0), new Point(0,1,0)});
            var res = facet.Split(cutPlane);
            Assert.That(res.inner, Is.EqualTo(new Face2d(new List<Point>(){
                new Point(0,0,0), new Point(0.5,0,0), new Point(0.5,1,0), new Point(0,1,0)})));
            Assert.That(res.innerCut, Is.EqualTo(new Edge(new HashSet<Point>(){
                new Point(0.5,0,0), new Point(0.5,1,0) })));
            Assert.That(res.outer, Is.EqualTo(new Face2d(new List<Point>(){
                new Point(0.5,0,0), new Point(1,0,0), new Point(1,1,0), new Point(0.5,1,0)})));
        }
        //Square, empty cut, one edge on splitPlane
        {
            var facet = new Face2d(new List<Point>(){
                new Point(0,0,0), new Point(1,0,0), new Point(1,1,0), new Point(0,1,0)});
            var cutPlane = new HalfSpace(new Point(0,0,0),new Point(1,0,0));
            var res = facet.Split(cutPlane);
            Assert.That(res.inner, Is.EqualTo(null));
            Assert.That(res.outerCut, Is.EqualTo(new Edge(new Point(0,0,0),new Point(0,1,0))));
            Assert.That(res.outer, Is.EqualTo(facet));
        }
        //triangle, non-empty cut, one vertex on splitPlane
        foreach (var facet in new List<Face2d>() {
            new Face2d(new List<Point>{new(0,0,0),new(0.5,1,0),new(1,0,0)}),
            new Face2d(new List<Point>{new(0.5,1,0),new(1,0,0),new(0,0,0)}),
            new Face2d(new List<Point>{new(1,0,0),new(0,0,0),new(0.5,1,0)}),
            new Face2d(new List<Point>{new(0,0,0),new(1,0,0),new(0.5,1,0)}),
            new Face2d(new List<Point>{new(0.5,1,0),new(0,0,0),new(1,0,0)}),
            new Face2d(new List<Point>{new(1,0,0),new(0.5,1,0),new(0,0,0)}),
        }) {
            var cutPlane = new HalfSpace(new Point(0.5,0,0),new Point(1,0,0));
            var res = facet.Split(cutPlane);
            Assert.That(res.inner, Is.EqualTo(new Face2d(
                new Point(0,0,0),new Point(0.5,1,0),new Point(0.5,0,0), false)));
            Assert.That(res.innerCut, Is.EqualTo(new Edge(
                new Point(0.5,1,0),new Point(0.5,0,0))));
            Assert.That(res.outer, Is.EqualTo(new Face2d(
                new Point(1,0,0),new Point(0.5,1,0),new Point(0.5,0,0), false)));
        }
        //triangle, empty cut, one vertex on splitPlane
        {
            var facet = new Face2d(new Point(0,0,0),new Point(0.5,1,0),new Point(1,0,0), false);
            var cutPlane = new HalfSpace(new Point(0,0,0),new Point(1,0,0));
            var res = facet.Split(cutPlane);
            Assert.That(res.inner, Is.EqualTo(null));
            Assert.That(res.innerCut, Is.EqualTo(null));
            Assert.That(res.outer, Is.EqualTo(facet));
        }
    }

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
    private static void CheckSquarish(IPolyhedron squarish) {
        Assert.That(squarish.FacesList(2), Has.Count.EqualTo(1));
        Assert.That(squarish.FacesList(1), Has.Count.EqualTo(4));
        Assert.That(squarish.Faces(0), Has.Count.EqualTo(4));
        Assert.That(squarish.FacesList(0), Has.Count.EqualTo(8));
    }
    [Test] public void CutSquarish() {
        var face = new Face2d(new List<Point>(){
            new(0.5,0,0),new(0.5,0,1),new(0.5,1,1),new(0.5,1,0)
        });
        CheckSquarish(face);
        var res = face.Split(new HalfSpace(new Point(0,0.5,0),new Point(0,1,0)));
        var facet1 = new Face2d(new List<Point>(){
            new(0.5,0,0),new(0.5,0.5,0),new(0.5,0.5,1),new(0.5,0,1)
        });
        CheckSquarish(res.inner);
        CheckSquarish(res.outer);
        Assert.That(res.inner,Is.EqualTo(facet1));
        Assert.That(((Edge)res.outerCut).a.getPoint(),Is.EqualTo(new Point(0.5,0.5,0)));
        Assert.That(((Edge)res.innerCut).b.getPoint(),Is.EqualTo(new Point(0.5,0.5,0)));
        Assert.That(((Edge)res.outerCut).b.getPoint(),Is.EqualTo(new Point(0.5,0.5,1)));
        Assert.That(((Edge)res.innerCut).a.getPoint(),Is.EqualTo(new Point(0.5,0.5,1)));

        var res2 = face.Split(new HalfSpace(new Point(0,1,0),new Point(0,1,0)));
        Assert.That(res2.inner,Is.EqualTo(face));
        Assert.That(((Edge)res2.innerCut).a.getPoint(), Is.EqualTo(new Point(0.5,1,1)));
        Assert.That(((Edge)res2.innerCut).b.getPoint(), Is.EqualTo(new Point(0.5,1,0)));
        
    }
    private static void CheckCuboid(IPolyhedron cuboid)
    {
        Assert.That(cuboid.FacesList(3), Has.Count.EqualTo(1));
        Assert.That(cuboid.FacesList(2), Has.Count.EqualTo(6));
        Assert.That(cuboid.Faces(1), Has.Count.EqualTo(12));
        Assert.That(cuboid.FacesList(1), Has.Count.EqualTo(24));
        Assert.That(cuboid.Faces(0), Has.Count.EqualTo(8));
        Assert.That(cuboid.FacesList(0), Has.Count.EqualTo(8 * 3));
    }

    [Test] public void CutCuboid() {
        var cuboid = new Polyhedron(new HashSet<IPolyhedron>(){
            new Face2d(new List<Point>{new(0,0,0),new(0,0,1),new(0,1,1),new(0,1,0)}),
            new Face2d(new List<Point>{new(0,0,0),new(0.5,0,0),new(0.5,0,1),new(0,0,1)}),
            new Face2d(new List<Point>{new(0,0,0),new(0,1,0),new(0.5,1,0),new(0.5,0,0)}),
            new Face2d(new List<Point>{new(0,1,0),new(0,1,1),new(0.5,1,1),new(0.5,1,0)}),
            new Face2d(new List<Point>{new(0,0,1),new(0.5,0,1),new(0.5,1,1),new(0,1,1)}),
//            new Face2d(new List<Point>{new(0.5,0,0),new(0.5,0,1),new(0.5,1,1),new(0.5,1,0)}),
            new Face2d(new List<Point>{new(0.5,0,0),new(0.5,1,0),new(0.5,1,1),new(0.5,0,1)}),
        },false);
        CheckCuboid(cuboid);

        var res2 = cuboid.Split(new HalfSpace(new Point(0,0.5,0),new Point(0,1,0)));
        var facet1 = new Face2d(new List<Point>(){
            new(0.5,0,0),new(0.5,0.5,0),new(0.5,0.5,1),new(0.5,0,1)
        });
        CheckCuboid(res2.inner);
        CheckCuboid(res2.outer);

        Assert.That(res2.inner.facets,Is.SupersetOf(new HashSet<IPolyhedron>{res2.innerCut}));
        Assert.That(res2.inner.facets,Is.SupersetOf(new HashSet<IPolyhedron>{facet1}));
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

        square = new Face2dBC(new Face2d(new List<Point>(){
            new Point(0,0,0), new Point(1,0,0), new Point(1,1,0), new Point(0,1,0)
        }).facets,false,null);
        triangles = square.BoundaryTriangulation2d();
        Assert.That(triangles, Has.Count.EqualTo(2));
        foreach (var triangle in triangles) {
            var vs = triangle.points;
            Assert.That(vs.Contains(new Point(0,0,0)));
            Assert.That(vs, Has.Count.EqualTo(3));
        }
    }
    public void CheckVertices2face2vertices(List<Point> points) {
        var facet = new Face2d(points);
        var facetB = new Face2d(facet.facets, false);
        Assert.That(facet, Is.EqualTo(facetB));
    }
    [Test] public void Vertices2d() {
        double a = 1.7;
        double b = 1.7;
        Assert.That(a.GetHashCode(),Is.EqualTo(b.GetHashCode()));
        Assert.That((new double[] {1,2}).GetHashCode() != (new double[] {1,2}).GetHashCode());
        Assert.That(!(new double[] {1,2}).Equals(new double[] {1,2}));

        CheckVertices2face2vertices( new List<Point>(){
            new Point(0,0,0), new Point(1,0,0), new Point(1,1,0), new Point(0,1,0)
        });
        CheckVertices2face2vertices( new List<Point>(){
            new Point(0,0,0), new Point(0,0,1), new Point(0,1,1), new Point(0,1,0)
        });

    }
    [Test] public void Colinear() {
        Assert.That(AOP.Colinear1d(new Point(0,0,0),new Point(1,0,0), new Point(2,0,0)));
        Assert.That(!AOP.Colinear1d(new Point(0,0,0),new Point(1,0,0), new Point(2,0.1,0)));
        Assert.That(AOP.Colinear2d(new Point(0,0,0),new Point(1,0,0), new Point(0,1,0), new Point(1,1,0)));
        Assert.That(!AOP.Colinear2d(new Point(0,0,0),new Point(1,0,0), new Point(0,1,0), new Point(1,1,0.1)));
        //2d colinear in 2d space
        Assert.That(AOP.Colinear2d(new Point(0,0),new Point(1,0), new Point(0,1), new Point(5,7)));
    }
    [Test] public void AOPSpanningPoints() {
        List<Point> points;
        
        points = AOP.SpanningPoints(new List<Point>(){new(0,0,0)});
        Assert.That(points.Contains(new Point(0,0,0)));

        points = AOP.SpanningPoints(new List<Point>(){new(0,0,0), new(0,0,1)});
        Assert.That(points.Contains(new Point(0,0,0)));
        Assert.That(points.Contains(new Point(0,0,1)));

        points = AOP.SpanningPoints(new List<Point>(){new(0,0,0), new(0,0,1), new(0,1,1)});
        Assert.That(points.Contains(new Point(0,0,0)));
        Assert.That(points.Contains(new Point(0,0,1)));
        Assert.That(points.Contains(new Point(0,1,1)));


        points = AOP.SpanningPoints(new List<Point>(){new(0,0,0), new(0,0,1), new(0,1,1), new(1,1,1)});
        Assert.That(points.Contains(new Point(0,0,0)));
        Assert.That(points.Contains(new Point(0,0,1)));
        Assert.That(points.Contains(new Point(0,1,1)));
        Assert.That(points.Contains(new Point(1,1,1)));

        points = AOP.SpanningPoints(new List<Point>(){new(0,0,0), new(0,0,1), new(0,1,1), new(0,1,0)});
        Assert.That(points, Has.Count.EqualTo(3));
        points = AOP.SpanningPoints(new List<Point>(){new(0,0,0), new(0,0,1), new(0,1,1), new(0,1,0), new(0,2,2)});
        Assert.That(points, Has.Count.EqualTo(3));
        points = AOP.SpanningPoints(new List<Point>(){new(0,0,0), new(0,0,1), new(0,1,1), new(0,1,0), new(0,2,2), new(1,2,2)});
        Assert.That(points, Has.Count.EqualTo(4));
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
    [Test] public void PolyhedralComplexTest() {
        {
            var rc1 = Face2dBC.FromIntegerCell(new int[] { 0, 0, 0 });
            var rc2 = Face2dBC.FromIntegerCell(new int[] { 1, 0, 0 });
            var cells = new HashSet<IPolyhedron>{rc1,rc2};
            var pc = new PolyhedralComplex(cells);
            Assert.That(pc.CalculateVisibleFacets(), Has.Count.EqualTo(rc1.facets.Count+rc2.facets.Count-2));
        }
        {
            var rc1 = Face2dBC.FromIntegerCell(new int[] { 0, 0, 0 });
            var rc2 = Face2dBC.FromIntegerCell(new int[] { 1, 0, 0 });
            var rc3 = Face2dBC.FromIntegerCell(new int[] { 0, 1, 0 });
            var cells = new HashSet<IPolyhedron>{rc1,rc2,rc3};
            var pc = new PolyhedralComplex(cells);
            Assert.That(pc.CalculateVisibleFacets(), Has.Count.EqualTo(rc1.facets.Count+rc2.facets.Count+rc3.facets.Count-4));
        }
        {
            var rc1 = Face2dBC.FromIntegerCell(new int[] { 0, 0, 0 });
            var rc2 = Face2dBC.FromIntegerCell(new int[] { 1, 0, 0 });
            var rc3 = Face2dBC.FromIntegerCell(new int[] { 0, 1, 0 });
            var rc4 = Face2dBC.FromIntegerCell(new int[] { 1, 1, 0 });
            var cells = new HashSet<IPolyhedron>{rc1,rc2,rc3,rc4};
            var pc = new PolyhedralComplex(cells);
            Assert.That(pc.CalculateVisibleFacets(), Has.Count.EqualTo(rc1.facets.Count+rc2.facets.Count+rc3.facets.Count+rc4.facets.Count-8));
        }
        {
            var rc1 = Face2dBC.FromIntegerCell(new int[] { 0, 0, 0 },new HashSet<int>() {0,1},true,true);
            var rc2 = Face2dBC.FromIntegerCell(new int[] { 0, 0, 0 },new HashSet<int>() {0,2},true,true);
            var cells = new HashSet<IPolyhedron>{rc1,rc2};
            var pc = new PolyhedralComplex(cells);
            Assert.That(pc.CalculateVisibleFacets(), Has.Count.EqualTo(rc1.facets.Count+rc2.facets.Count-1));
        }
        {
            var rc1 = Face2dBC.FromIntegerCell(new int[] { 0, 0, 0 },new HashSet<int>() {0,1},true,true);
            var rc2 = Face2dBC.FromIntegerCell(new int[] { 1, 0, 0 },new HashSet<int>() {0,1},true,true);
            var cells = new HashSet<IPolyhedron>{rc1,rc2};
            var pc = new PolyhedralComplex(cells);
            Assert.That(pc.CalculateVisibleFacets(), Has.Count.EqualTo(rc1.facets.Count+rc2.facets.Count-2));
        }

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
    [Test] public void CutOutTest() 
    {
        var pc = new PolyhedralComplex(new HashSet<IPolyhedron>(){
            Face2dBC.FromIntegerCell(new int[]{0,0,0}),
        });
        foreach (var cell in pc.cells) {
            foreach (var facet in cell.facets) {
                Assert.That(
                    facet.GetType(), Is.EqualTo(typeof(Face2dBC)));
            }
        }
        var polyhedron1 = PolyhedronCreate.Cube3dAt(new Point(-0.5,-0.5,-0.5),1);
        var pc2 = pc.CutOut(polyhedron1);
        Assert.That(pc2.CalculateVisibleFaces(1), Has.Count.EqualTo(8+4+4+6+8));
        Assert.That(pc2.CalculateVisibleFaces(2), Has.Count.EqualTo(3+8+1+8+4));
        foreach (var cell in pc2.cells) {
            foreach (var facet in cell.facets) {
                Assert.That(
                    facet.GetType(), Is.EqualTo(typeof(Face2dBC)));
            }
        }

        foreach (var facet1 in new List<Face2d>(){
            new Face2d(new List<Point>(){new(0.5,0,0),new(0.5,0,0.5),new(0.5,0.5,0.5),new(0.5,0.5,0)}, false),
            new Face2d(new List<Point>(){new(0,0.5,0),new(0,0.5,0.5),new(0.5,0.5,0.5),new(0.5,0.5,0)}, false),
            new Face2d(new List<Point>(){new(0.5,0,0),new(0.5,0.5,0),new(0.5,0.5,0.5),new(0.5,0,0.5)}, false),
        }) {
            Face2d facetFound = null;
            foreach (var cell in pc2.cells) {
                foreach (var facet in cell.facets) {
                    if (facet.Equals(facet1)) {
                        facetFound = (Face2d) facet;
                        break;
                    }
                }
            }
            Assert.That(facetFound,Is.Not.EqualTo(null));
            Assert.That(facetFound.isInvisible);
        }
    }

     [Test] public void CubeDoubleSplit() {
        var pbc = new Polyhedron3dBoundaryComplex(new IntegerBoundaryComplex(new int[][]{new int[]{0,0,0},}));
        HalfSpace cutLeftRight = new HalfSpace(0.5,new Point(1,0,0));
        HalfSpace cutUpDown =    new HalfSpace(0.5,new Point(0,1,0));
        pbc.CutOut(new HalfSpace[] {cutLeftRight,cutUpDown});
        var visibleFacets = pbc.VisibleFacets();
        Assert.That(visibleFacets, Has.Count.EqualTo(8));
        var cm = new FacetsGenericMesh(pbc.facets.Cast<IPolyhedron>().ToHashSet());
    }
   
    [Test] public void Inset2dTest() {
        {
            var points = new List<Point>(){new(0,0),new(1,0),new(1,1),new(0,1)}; //counter clockwise
            var insetPoints = AOP.Inset2dCounterClockwise(points,0.25);
            Assert.That(insetPoints.Length,Is.EqualTo(4));
            var exp = new List<Point>{new(0.25,0.25),new(0.75,0.25),new(0.75,0.75),new(0.25,0.75)};
            Assert.That(insetPoints,Is.EqualTo(exp));
        }
        {
            var points = new List<Point>(){new(0,0),new(0,1),new(1,1),new(1,0)}; //clockwise
            var insetPoints = AOP.Inset2dCounterClockwise(points,-0.25);
            Assert.That(insetPoints.Length,Is.EqualTo(4));
            var exp = new List<Point>{new(0.25,0.25),new(0.25,0.75),new(0.75,0.75),new(0.75,0.25)};
            Assert.That(insetPoints,Is.EqualTo(exp));
        }
    }
    [Test] public void Inset3dTest() {
        {
            var points = new List<Point>(){new(0,0,0),new(1,0,0),new(1,1,0),new(0,1,0)};
            var insetPoints = AOP.Inset3d(points,0.25);
            var exp = new Point[]{new(0.25,0.25,0),new(0.75,0.25,0),new(0.75,0.75,0),new(0.25,0.75,0)};
            Assert.That(insetPoints,Is.EqualTo(exp));
        }
        {
            var points = new List<Point>(){new(0,0,0),new(0,1,0),new(1,1,0),new(1,0,0)};
            var insetPoints = AOP.Inset3d(points,0.25);
            var exp = new Point[]{new(0.25,0.25,0),new(0.25,0.75,0),new(0.75,0.75,0),new(0.75,0.25,0)};
            Assert.That(insetPoints,Is.EqualTo(exp));
        }
        {
            var points = new List<Point>(){new(0,0,1),new(1,0,1),new(1,1,1),new(0,1,1)};
            var insetPoints = AOP.Inset3d(points,0.25);
            var exp = new Point[]{new(0.25,0.25,1),new(0.75,0.25,1),new(0.75,0.75,1),new(0.25,0.75,1)};
            Assert.That(insetPoints,Is.EqualTo(exp));
        }
        {
            var points = new List<Point>(){new(7,0,1),new(8,0,1),new(8,1,1),new(7,1,1)};
            var insetPoints = AOP.Inset3d(points,0.25);
            var exp = new Point[]{new(7.25,0.25,1),new(7.75,0.25,1),new(7.75,0.75,1),new(7.25,0.75,1)};
            Assert.That(insetPoints,Is.EqualTo(exp));
        }
    }
    [Test] public void Inset3dFace2dTest() {
        var face2d = new Face2d(new List<Point>(){new(0,0,0),new(1,0,0),new(1,1,0),new(0,1,0)});
        face2d.Inset(0.25);
        var exp = new List<Point>{new(0.25,0.25,0),new(0.75,0.25,0),new(0.75,0.75,0),new(0.25,0.75,0)};
        Assert.That(face2d.points,Is.EqualTo(exp));
        var n = face2d.edges.Count;
        for (int i=0;i<n;i++) {
            face2d.edges[i].neighbor = face2d.edges[(i+1)%n];
            face2d.edges[(i+1)%n].neighbor = face2d.edges[i];
        }
    }
    [Test] public void Inset3dFace2dSameSpaceEdgesTest() {
        var face2d = new Face2d(new List<Point>(){new(0,0,0),new(0.5,0,0),new(1,0,0),new(1,1,0),new(0,1,0)});
        face2d.Inset(0.25);
        var exp = new List<Point>{new(0.25,0.25,0),new(0.5,0.25,0),new(0.75,0.25,0),new(0.75,0.75,0),new(0.25,0.75,0)};
        Assert.That(face2d.points,Is.EqualTo(exp));
        var n = face2d.edges.Count;
        for (int i=0;i<n;i++) {
            face2d.edges[i].neighbor = face2d.edges[(i+1)%n];
            face2d.edges[(i+1)%n].neighbor = face2d.edges[i];
        }
    }
    [Test] public void RotationEqualTest() {
        {
            var a = new List<Point> {
                new(0.5,1,0),
                new(1,0,0),
                new(0.5,0,0)
            };
            var b = new List<Point> {
                new(0.5,0,0),
                new(0.5,1,0),
                new(1,0,0),
            };
            Assert.That(Face2d.RotationEqual(a,b));
        }   
        {
            var a = new Face2d(new Point(0,0,0),new Point(1,0,0),new Point(1,1,0),false);
            var b = new Face2d(new Point(0,0,0),new Point(1,1,0),new Point(0,1,0),false);
            Assert.That(a, Is.Not.EqualTo(b));
        }
    }
    [Test] public void RotationEqualWithDuplicateElements() {
        var a = new List<Point> {
            new(0.5,0,0),new(0.5,0,1),new(0.5,0.5,0),new(0.5,0.5,0)
        };
        var b = new List<Point> {
            new(0.5,0.5,0),new(0.5,0,0),new(0.5,0,1),new(0.5,0.5,0)
        };
        Assert.That(Face2d.RotationEqual(b,a));
    }
    [Test] public void Face2dSplitTest() {
        var facesList = new List<Face2d>() {
            new Face2d(new List<Point>(){new(0,0,0),new(0,0,1),new(0,1,1)}),
            new Face2d(new List<Point>(){new(0,0,1),new(0,1,1),new(0,0,0)}),
            new Face2d(new List<Point>(){new(0,1,1),new(0,0,0),new(0,0,1)}),
            new Face2d(new List<Point>(){new(0,1,1),new(0,0,1),new(0,0,0)}),
            new Face2d(new List<Point>(){new(0,0,1),new(0,0,0),new(0,1,1)}),
            new Face2d(new List<Point>(){new(0,0,0),new(0,1,1),new(0,0,1)}),
        };
        foreach (var f in facesList) {
            {
                var cutPlane = new HalfSpace(-0.5,new Point(0,0,1));
                var res = f.Split(cutPlane);
                Assert.That(res.inner,Is.EqualTo(null));
                Assert.That(res.innerCut,Is.EqualTo(null));
                Assert.That(res.outer,Is.EqualTo(f));
            }
            {
                var cutPlane = new HalfSpace(0,new Point(0,0,1));
                var res = f.Split(cutPlane);
                Assert.That(res.inner,Is.EqualTo(null));
                Assert.That(res.innerCut,Is.EqualTo(null));
                Assert.That(res.outer,Is.EqualTo(f));
            }
            {
                var cutPlane = new HalfSpace(1,new Point(0,0,1));
                var res = f.Split(cutPlane);
                Assert.That(res.inner,Is.EqualTo(f));
                Assert.That(res.innerCut,Is.EqualTo(new Edge(
                    new Point(0,0,1),new Point(0,1,1))));
                Assert.That(res.outer,Is.EqualTo(null));
            }
            {
                var cutPlane = new HalfSpace(1,new Point(0,0,1));
                var res = f.Split(cutPlane);
                Assert.That(res.inner,Is.EqualTo(f));
                Assert.That(res.innerCut,Is.EqualTo(new Edge(
                    new Point(0,0,1),new Point(0,1,1))));
                Assert.That(res.outer,Is.EqualTo(null));
            }
            {
                var cutPlane = new HalfSpace(0.5,new Point(0,0,1));
                var res = f.Split(cutPlane);
                Assert.That(res.inner,Is.EqualTo(new Face2d(new List<Point>(){
                    new(0,0,0), new(0,0,0.5), new(0,0.5,0.5)
                })));
                Assert.That(res.innerCut,Is.EqualTo(new Edge(
                    new Point(0,0,0.5),new Point(0,0.5,0.5))));
                Assert.That(res.outer,Is.EqualTo(new Face2d(new List<Point>(){
                    new(0,0,0.5),new(0,0,1),new(0,1,1),new(0,0.5,0.5)
                })));
            }
            {
                var cutPlane = new HalfSpace(0,new Point(1,0,0));
                var res = f.Split(cutPlane);
                Assert.That(res.inner,Is.EqualTo(null));
                Assert.That(res.isContained);
                Assert.That(res.outer,Is.EqualTo(null));
            }
        }
    }
    public IPolyhedron[] connectingEdge(IPolyhedron p1, IPolyhedron p2) {
        foreach (var edge1 in p1.facets) {
            foreach (var edge2 in p2.facets) {
                if (edge1.Equals(edge2)) {
                    return new IPolyhedron[]{edge1,edge2};
                }
            }
        }
        return null;
    }
    [Test] public void Face2dDoubleSplit() {
        var face2d = new Face2d(new List<Point>(){new(0,0,0),new(0,2,0),new(2,2,0),new(2,0,0)});
        var cutLeftRight = new HalfSpace(1,new Point(1,0,0));
        var cutUpDown = new HalfSpace(1,new Point(0,1,0));
        var sr = face2d.Split(cutLeftRight);
        var left = (Face2d)sr.inner;
        var right = (Face2d)sr.outer;
        var rightEdge = sr.outerCut;
        var leftEdge = sr.innerCut;
        Assert.That(leftEdge,Is.Not.SameAs(rightEdge));

        Assert.That(left,Is.Not.Null);
        Assert.That(leftEdge.neighbor,Is.SameAs(rightEdge));
        //Assert.That(leftEdge.innerNeighborTemp==null);

        Assert.That(right,Is.Not.Null);
        Assert.That(rightEdge.neighbor,Is.SameAs(leftEdge));
        //Assert.That(rightEdge.innerNeighborTemp==null);

        
        sr = left.Split(cutUpDown);
        var leftUpper = (Face2d)sr.outer;
        var leftUpperEdge = sr.outerCut;
        var leftLower = (Face2d)sr.inner;
        var leftLowerEdge = sr.innerCut;
        //left edge of right half is precut by the cut of the left half
        //Assert.That(rightEdge.outerNeighborTemp,Is.Not.Null);
        Assert.That(leftUpper.edges[2].neighbor,Is.SameAs(right.edges[4]));
        Assert.That(leftUpper.edges[2]         ,Is.SameAs(right.edges[4].neighbor));
        Assert.That(leftLower.edges[2].neighbor,Is.SameAs(right.edges[3]));
        Assert.That(leftLower.edges[2]         ,Is.SameAs(right.edges[3].neighbor));

        

        sr = right.Split(cutUpDown);
        var rightUpper = (Face2d)sr.outer;
        var rightLower = (Face2d)sr.inner;
        var c = connectingEdge(rightUpper,rightLower);
        Assert.That(c[0].neighbor,Is.SameAs(c[1]));
        Assert.That(c[1].neighbor,Is.SameAs(c[0]));

        c = connectingEdge(leftUpper,rightUpper);
        Assert.That(c,Is.Not.Null);
        Assert.That(c[0].neighbor,Is.SameAs(c[1]));
        Assert.That(c[1].neighbor,Is.SameAs(c[0]));
    }
}
