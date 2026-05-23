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
        public bool isCoplanarInterior {get; set;}
        public IPolyhedron parent {get;set;}
        public IPolyhedron neighbor {get;set;}
        public int mark {get;set;}
        protected readonly Point point;

        public HashSet<IPolyhedron> facets => new();

        public RawVertex(Point point, bool isCoplanarInterior) {
            this.point = point;
            this.isCoplanarInterior = isCoplanarInterior;
        }

        public IPolyhedron OpposingClone() {
            var res = (Vertex)Recreate(point);
            // OpposingClone keeps the same 3D geometric position, so the 4D source point
            // (pos4d) is identical too — copy it through. Without this, Face2d.Split's
            // OpposingClone of cut vertices strips pos4d and downstream shaders that read
            // it (e.g. DepthTrueColoredGlass via UV3) see null at cut-introduced vertices.
            if (this is Vertex v) res.pos4d = v.pos4d;
            res.parent = parent;
            res.mark = mark;
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
            return new Vertex(point, isCoplanarInterior) { parent = parent, neighbor=neighbor, mark=mark };
        }
        //  public Vertex(IntegerCell ic, bool isCoplanarInterior) : this(new Point(ic.origin),isCoplanarInterior) {}
        public virtual IPolyhedron Recreate(Point point) {
            return new Vertex(point,isCoplanarInterior) { parent = parent, neighbor=neighbor, mark=mark };
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
        public double[] pos4d;
        public Vertex(Point point, bool isCoplanarInterior=false) : base(point,isCoplanarInterior) {
        }
        public Vertex(double x, double y, double z, bool isCoplanarInterior=false) : this(new Point(x,y,z),isCoplanarInterior) {}
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
        public bool isCoplanarInterior {get; set;}
        public IPolyhedron parent {get;set;}
        public IPolyhedron neighbor {get;set;}
        public int mark {get;set;}
        public HashSet<IPolyhedron> facets {
            get { return new() {a,b}; }
        }

        public Edge(Vertex a, Vertex b, bool isCoplanarInterior=false) : base(a,b) {   this.isCoplanarInterior= isCoplanarInterior; }
        public Edge(Point a, Point b, bool isCoplanarInterior=false) : base(new Vertex(a, isCoplanarInterior), new Vertex(b,isCoplanarInterior)) {   this.isCoplanarInterior= isCoplanarInterior; }
        public Edge(HashSet<Point> points, bool isCoplanarInterior=false) :
                base(points.Select(p => new Vertex(p,isCoplanarInterior)).ToHashSet()) {}
        public Edge(HashSet<Vertex> vertices, bool isCoplanarInterior) : base(vertices) {this.isCoplanarInterior=isCoplanarInterior;}
        public Edge(HashSet<IPolyhedron> vertices, bool isCoplanarInterior) :
                base(vertices.Cast<Vertex>().ToHashSet()) {this.isCoplanarInterior=isCoplanarInterior;}
        public IPolyhedron Recreate(HashSet<IPolyhedron> vertices) { return new Edge(vertices,isCoplanarInterior) { neighbor=neighbor, parent = parent, mark=mark }; }
        public virtual IPolyhedron Recreate(Vertex a, Vertex b) { return new Edge(a, b, isCoplanarInterior) { neighbor=neighbor, parent = parent, mark=mark }; }
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
                // Cut vertices are fresh geometry at the cut plane, not the same point as
                // a or b — Recreate would have inherited their mark, override to MARK_NONE.
                ci.mark = IPolyhedron.MARK_NONE;
                co.mark = IPolyhedron.MARK_NONE;
                // Cuts happen in 3D after the (affine) camera projection, so the parametric
                // position t of c on [a,b] is the same in 3D and 4D — we can lift it back
                // to interpolate pos4d. Without this, downstream shaders that read pos4d
                // (e.g. DepthTrueColoredGlass via UV3) see null at cut-introduced vertices.
                double[] pos4dCut1 = LerpPos4d(a.pos4d, b.pos4d, LerpParam(a.getPoint(), b.getPoint(), c));
                ci.pos4d = pos4dCut1;
                co.pos4d = pos4dCut1;
                return new SplitResult {inner=Recreate(a,ci),innerCut=ci,outerCut=co,outer=Recreate(co,b)}.CrossReference(this,cutPlane);
            }
            if (aSide == HalfSpace.OUTSIDE && bSide == HalfSpace.INSIDE) {
                var c = cutPlane.cutPoint(a.getPoint(),b.getPoint());
                var ci = new Vertex(c,isCoplanarInterior);
                var co = new Vertex(c,isCoplanarInterior);
                // `new Vertex` already gives mark=0 by default — same effect as above branch.
                double[] pos4dCut2 = LerpPos4d(a.pos4d, b.pos4d, LerpParam(a.getPoint(), b.getPoint(), c));
                ci.pos4d = pos4dCut2;
                co.pos4d = pos4dCut2;
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

        // Parametric position of c on the segment [a, b]; uses the longest component of
        // (b - a) for numerical stability (avoids division by ~0 on near-degenerate axes
        // without paying for a square root).
        static double LerpParam(Point a, Point b, Point c) {
            int n = a.x.Length;
            int axis = 0;
            double bestAbs = 0;
            for (int i = 0; i < n; i++) {
                double d = Math.Abs(b.x[i] - a.x[i]);
                if (d > bestAbs) { bestAbs = d; axis = i; }
            }
            if (bestAbs == 0) return 0;
            return (c.x[axis] - a.x[axis]) / (b.x[axis] - a.x[axis]);
        }

        // Component-wise lerp; returns null if either input is null so callers without
        // a 4D context (pure-3D tests) see the original behavior.
        static double[] LerpPos4d(double[] a, double[] b, double t) {
            if (a == null || b == null || a.Length != b.Length) return null;
            var r = new double[a.Length];
            double u = 1.0 - t;
            for (int i = 0; i < a.Length; i++) r[i] = a[i] * u + b[i] * t;
            return r;
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