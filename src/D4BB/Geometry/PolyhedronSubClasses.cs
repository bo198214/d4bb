using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using D4BB.Comb;
using D4BB.General;

namespace D4BB.Geometry
{
    public class RawVertex : IPolyhedron {
        public bool isInvisible {get; set;}
        public IPolyhedron parent {get;set;}
        public IPolyhedron neighbor {get;set;}
        protected readonly Point point;

        public HashSet<IPolyhedron> facets => new();

        public RawVertex(Point point, bool isInvisible) {
            this.point = point;
            this.isInvisible = isInvisible;
        }

        public IPolyhedron OpposingClone() { 
            var res = (Vertex)Recreate(point);
            res.parent = parent;
            res.neighbor = this;
            this.neighbor = res;
            return res;
        }
        public int Dim()
        {
            return 0;
        }
        public int SpaceDim() {
            return point.x.Length;
        }

        public SplitResult Split(HalfSpace cutPlane)
        {
            return new SplitResult();
        }

        public IPolyhedron Recreate(HashSet<IPolyhedron> _facets)
        {
            return new Vertex(point, isInvisible) { parent = parent, neighbor=neighbor };
        }
        //  public Vertex(IntegerCell ic, bool isInvisible) : this(new Point(ic.origin),isInvisible) {}
        public virtual IPolyhedron Recreate(Point point) {
            return new Vertex(point,isInvisible) { parent = parent, neighbor=neighbor };
        }

        public int Side(HalfSpace halfSpace) {
            var side = halfSpace.side(point);
            if (side==HalfSpace.INSIDE) return SplitResult.GENUINE_INSIDE;
            if (side==HalfSpace.OUTSIDE)return SplitResult.GENUINE_OUTSIDE;
            if (side==HalfSpace.CONTAINED) return SplitResult.CONTAINED;
            throw new Exception("side can only have 3 possible values");
        }
         public Point PointRef() {
            return point;
        }
        public Point getPoint() {
            return point.clone();
        }
        public override string ToString()
        {
            return "v" + point.ToString();
        }

        public void Replace(IPolyhedron ab, IPolyhedron a, IPolyhedron b)
        {
            throw new NotImplementedException();
        }
    }
    public class Vertex : RawVertex {
        public Vertex(Point point, bool isInvisible=false) : base(point,isInvisible) {
        }
        public Vertex(double x, double y, double z, bool isInvisible=false) : this(new Point(x,y,z),isInvisible) {}
        public override bool Equals(object obj)
        {
            if (!obj.GetType().Equals(GetType())) { return false; } 
            var other = (Vertex) obj;
            return this.point.Equals(other.point);
        }
        public override int GetHashCode()
        {
            return point.GetHashCode();
        }
   }
    public class Edge : Dualton<Vertex>, IPolyhedron   {
        //1dim polyhedron
        //facets are the vertices
        //Each edge in a 3d polyhedron belongs to 2 Facets
        public bool isInvisible {get; set;}
        public IPolyhedron parent {get;set;}
        public IPolyhedron neighbor {get;set;}
        public HashSet<IPolyhedron> facets {
            get { return new() {a,b}; }
        }

        public Edge(Vertex a, Vertex b, bool isInvisible=false) : base(a,b) {   this.isInvisible= isInvisible; }
        public Edge(Point a, Point b, bool isInvisible=false) : base(new Vertex(a, isInvisible), new Vertex(b,isInvisible)) {   this.isInvisible= isInvisible; }
        public Edge(HashSet<Point> points, bool isInvisible=false) : 
                base(points.Select(p => new Vertex(p,isInvisible)).ToHashSet()) {}
        public Edge(HashSet<Vertex> vertices, bool isInvisible) : base(vertices) {this.isInvisible=isInvisible;}
        public Edge(HashSet<IPolyhedron> vertices, bool isInvisible) : 
                base(vertices.Cast<Vertex>().ToHashSet()) {this.isInvisible=isInvisible;}
        public IPolyhedron Recreate(HashSet<IPolyhedron> vertices) { return new Edge(vertices,isInvisible) { neighbor=neighbor, parent = parent }; }
        public virtual IPolyhedron Recreate(Vertex a, Vertex b) { return new Edge(a, b, isInvisible) { neighbor=neighbor, parent = parent }; }
        public IPolyhedron OpposingClone() { 
            var res = (Edge) Recreate((Vertex)b.OpposingClone(),(Vertex)a.OpposingClone());
            res.neighbor = this;
            this.neighbor = res;
            return res;
        }
        public override string ToString() { return "e[" + a.ToString() + " " + b.ToString() + "]";  }
        public void Swap() {
            (b, a) = (a, b);
        }
        public void Replace(IPolyhedron ab, IPolyhedron inner, IPolyhedron outer)
        {
            throw new NotImplementedException();
        }
        public SplitResult Split(HalfSpace cutPlane) {
            int aSide = cutPlane.side(a.getPoint());
            int bSide = cutPlane.side(b.getPoint());
            if (aSide == HalfSpace.INSIDE && bSide == HalfSpace.INSIDE) {
                return new SplitResult{inner=Recreate(a,b)};
            }
            if (aSide == HalfSpace.OUTSIDE && bSide == HalfSpace.OUTSIDE) {
                return new SplitResult{outer=Recreate(a,b)};
            }
            if (aSide == HalfSpace.INSIDE && bSide == HalfSpace.CONTAINED) {
                return new SplitResult{inner=Recreate(a,b),innerCut=b};
            }
            if (aSide == HalfSpace.OUTSIDE && bSide == HalfSpace.CONTAINED) {
                return new SplitResult{outerCut=b,outer=Recreate(a,b)};
            }
            if (aSide == HalfSpace.CONTAINED && bSide == HalfSpace.INSIDE) {
                return new SplitResult {inner=Recreate(a,b),innerCut=a};
            }
            if (aSide == HalfSpace.CONTAINED && bSide == HalfSpace.OUTSIDE) {
                return new SplitResult {outerCut=a,outer=Recreate(a,b)};
            }
            if (aSide == HalfSpace.INSIDE && bSide == HalfSpace.OUTSIDE) {
                Point c = cutPlane.cutPoint(a.getPoint(),b.getPoint());
                Vertex ci = (Vertex)a.Recreate(c);
                Vertex co = (Vertex)b.Recreate(c);
                return new SplitResult {inner=Recreate(a,ci),innerCut=ci,outerCut=co,outer=Recreate(co,b)}.CrossReference(this,cutPlane);
            }
            if (aSide == HalfSpace.OUTSIDE && bSide == HalfSpace.INSIDE) {
                var c = cutPlane.cutPoint(a.getPoint(),b.getPoint());
                var ci = new Vertex(c,isInvisible);
                var co = new Vertex(c,isInvisible);
                return new SplitResult {inner=Recreate(ci,b),innerCut=ci,outerCut=co,outer=Recreate(a,co)}.CrossReference(this,cutPlane);
            }
            if (aSide == HalfSpace.CONTAINED && bSide == HalfSpace.CONTAINED) {
                return new SplitResult {isContained=true};
            }
            throw new Exception();
        }

        int IPolyhedron.Dim()
        {
            return 1;
        }

    }
    
    public static class PolyhedronCreate {

        public static Polyhedron Cube3dAt(Point origin, float sideLength) {
            var l = sideLength;
            var points = new Point[] { new(0,0,0), new(0,0,1), new(0,1,0), new(0,1,1), new(1,0,0), new(1,0,1), new(1,1,0), new(1,1,1) };
            for (int i=0;i<points.Length;i++) {
                points[i].multiply(sideLength).add(origin);
            }
            var facetIndices = new int[][] {
                new int[] {0,1,3,2}, new int[] {4,5,7,6}, new int[] {1,3,7,5}, new int[] {0,2,6,4}, new int[] {0,1,5,4}, new int[] {2,3,7,6}};
            var pFacets = new HashSet<IPolyhedron>();
            for (int i=0;i<facetIndices.Length;i++) {
                var facet = facetIndices[i];
                var pFacet = new List<Point>();
                for (int j=0;j<facet.Length;j++) {
                    pFacet.Add(points[facet[j]]);
                }
                pFacets.Add(new Face2d(pFacet, false));
            }
            return new Polyhedron(pFacets, false);
        }
    }
}