using System.Collections.Generic;
using NUnit.Framework;
using System.Linq;
using System;
using D4BB.Comb;

namespace D4BB.Geometry {
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
}