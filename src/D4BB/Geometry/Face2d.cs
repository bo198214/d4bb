using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using D4BB.Comb;

namespace D4BB.Geometry
{
    public class Face2d : IPolyhedron { // a more efficient implementation than the default Polyhedron
        public List<Edge> edges {get; protected set;}
        public bool isInvisible { get; set; }
        public IPolyhedron parent {get; set; }
        public IPolyhedron neighbor {get;set;}
        public IPolyhedron innerNeighborTemp {get;set;}
        public IPolyhedron outerNeighborTemp {get;set;}
        public HashSet<IPolyhedron> facets {
            get {
                return new HashSet<IPolyhedron>(edges);
            }
            protected set {
                edges = SortedEdges(value);
            }
        }
        public List<Point> points {
            get {
                List<Point> res = new();
                foreach (var edge in edges) res.Add(edge.a.getPoint());
                return res;
            }
        }

        public Face2d(List<Point> points, bool isInvisible=false) { //clockwise or anticlockwise
            this.isInvisible = isInvisible;
            edges = Points2Edges(points);
            SetNeighbors();
        }
        public void SetNeighbors() {
            var n = edges.Count;
            for (int i=0;i<n;i++) {
                edges[i].parent = this;
                var i2 = (i+1)%n;
                edges[i].b.neighbor = edges[i2].a;
                edges[i2].a.neighbor = edges[i].b;
            }
        }
        public Face2d(Point a, Point b, Point c, bool isInvisible) : this(new List<Point>{a,b,c},isInvisible){}
        public Face2d(Face2d face2d) : this(face2d.edges,face2d.isInvisible) {}
        public Face2d(List<Edge> edges, bool isInvisible=false) {
            this.isInvisible = isInvisible;
            this.edges = edges;
            SetNeighbors();
        }
        public Face2d(HashSet<IPolyhedron> edges, bool isInvisible) : this(SortedEdges(edges),isInvisible) {}
        public virtual IPolyhedron Recreate(HashSet<IPolyhedron> facets) { 
            return new Face2d(facets, isInvisible) { parent = parent };
        }
        public virtual Face2d Recreate(List<Edge> edges) {
            return new Face2d(edges, isInvisible) { parent = parent };
        }
        public virtual Face2d Recreate(List<Point> points) {
            return new Face2d(points, isInvisible) { parent = parent };
        }
        public IPolyhedron OpposingClone() {
            List<Edge> newEdges = new();
            foreach (var edge in edges) {
                newEdges.Add((Edge)edge.OpposingClone());
            }
            newEdges.Reverse();
            var res = new Face2d(newEdges,isInvisible) { parent = parent };
            res.neighbor = this;
            this.neighbor = res;
            return res;
        }
        public override string ToString() {
            String res = "2d[";
            for (int i=0;i<edges.Count;i++) {
                res += edges[i].a + " ";
            }
            res += "]";
            return res;
        }
        public int Dim() { return 2; }
        public Point CenterPoint() {
            Point res = edges[0].a.getPoint();
            for (int i=1;i<edges.Count;i++) {
                res.add(edges[i].a.getPoint());
            }
            res.multiply(1.0/edges.Count);
            return res;
        }
        public static bool RotationEqual(List<Point> points1, List<Point> points2) {
            if (points1.Count!=points2.Count) return false;
            if (points1.Count==0) return true;
            for (int i=0;i<points2.Count;i++) {
                if (points1[0].Equals(points2[i])) { 
                    var off=i; 
                    var isEqual = true;
                    for (int j=0;j<points2.Count;j++) {
                        if (!points1[j].Equals(points2[(j+off)%points2.Count])) {
                            isEqual=false;
                            break;
                        }
                    }
                    if (isEqual) return true;
                    }
            }
            return false;
        }
        public static bool RotationEqual(List<Edge> edges1, List<Edge> edges2) { 
            return RotationEqual(
                edges1.Select(edge => edge.a.getPoint()).ToList(),
                edges2.Select(edge => edge.a.getPoint()).ToList()
            );}
        public bool OrientedEquals(Face2d other) {
            return RotationEqual(this.edges,other.edges);
        }
        public override bool Equals(object obj) {
            if (obj==null) return false;
            var otherEdges = ((Face2d) obj).edges;
            var otherEdgesReversed = new List<Edge>(otherEdges);
            otherEdgesReversed.Reverse();
            return RotationEqual(this.edges,otherEdges) || RotationEqual(this.edges,otherEdgesReversed);
        }
        public override int GetHashCode()
        {
            int res = 0;
            foreach (var edge in edges) {
                res += edge.a.GetHashCode();
            }
            return res;
        }
        public static List<Edge> Points2Edges(List<Point> points) {
            List<Edge> edges = new();
            Point a = null;
            Point b = null;
            Point first = null;
            foreach (var point in points) {
                if (a==null) {
                    a = point;
                    first = a;
                    continue;
                } 
                b = point;
                edges.Add(new Edge(a,b,false));
                a = b;
            }
            edges.Add(new Edge(b,first,false));

            return edges;
        }
        public static bool IsDirectedCircle(HashSet<IPolyhedron> edges) {
            HashSet<Edge> pool = new();
            foreach (var e in edges) {
                pool.Add((Edge)e);
            }
            var edge1 = pool.First();
            pool.Remove(edge1);
            var edge = pool.First();
            while (true) {
                if (pool.Count==0) {
                    if (edge.b.Equals(edge1.a)) return true;
                    return false;
                }
                var found = false;
                foreach (var e in pool) {
                    if (edge.b.Equals(e.a)) {
                        edge = e;
                        pool.Remove(e);
                        found = true;
                        break;
                    }
                }
                if (!found) return false;
            }
        }
        /* For a 2d facet in 3d space returns the ordered vertices (either clockwise or counterclockwise) */
        public static List<Edge> SortedEdges(HashSet<IPolyhedron> edges, bool edgesAreDirected=true) {
            HashSet<Edge> pool = new();
            foreach (var e in edges) {
                pool.Add((Edge)e);
            }
            List<Edge> res = new();
            var edge1 = (Edge)edges.First();
            pool.Remove(edge1);
            var edge = edge1;
            while (true) {
                res.Add(edge);
                if (edge.b.Equals(edge1.a)) {
                    break;
                }
                bool found = false;
                foreach (var e in pool) {
                    if (e.Contains(edge.b)) {
                        found = true;
                        if (e.b.Equals(edge.b)) {
                            if (edgesAreDirected) Debug.Assert(false,"edges are not circularly directed");
                            else e.Swap();
                        }
                        edge = e;
                        pool.Remove(e);
                        break;
                    }
                }
                if (!found && pool.Count==0) throw new Exception("edges not closed");
                if (!found && pool.Count>0)  throw new Exception("edges are disconnected");
            }
            return res;
        }
        public List<Vertex> GetVertices() {
            List<Vertex> res = new();
            foreach (var edge in edges) res.Add(edge.a);
            return res;
        }
        /*  for 3d vertices 
            Makes the order of the facet vertices counter clockwise wrt the insidePoint
            Which makes it clockwise in the outside direction
        */
        public Point Normal() {
            Point a=null;
            Point b=null;
            Point c=null;
            var n = edges.Count;
            for (int i=0;i<n;i++) {
                a = edges[(n+i-1)%n].a.getPoint();
                b = edges[(n+i-1)%n].b.getPoint();
                Debug.Assert(b.Equals(edges[0].a.getPoint()), "5466236667");
                c = edges[i].b.getPoint();
                if (!AOP.Colinear1d(a,b,c)) break;
            }
            Point cross = AOP.cross(c.clone().subtract(b),a.clone().subtract(b));
            // if (cross.len()<0.000001) {
            //     Debug.Assert(false);
            // }
            return cross.normalize();
        }
        public HalfSpace HalfSpaceOf() {
            return new HalfSpace(edges[0].a.getPoint(),Normal());
        }
        /* assumes polyhedron to be 2d */
        public HashSet<Face2d> CenterTriangulation2d(Point centerPoint) {
            HashSet<Face2d> res = new();
            var centerVertex = new Vertex(centerPoint,isInvisible:true);
            var n = edges.Count;
            Vertex[] oneSidedVertices = new Vertex[n];
            for (int i=0;i<n;i++) {
                oneSidedVertices[i] = edges[i].a;
            }
            for (int i=0;i<n;i++) {
                var triangle = Recreate(new List<Edge>{
                    (Edge)edges[0].Recreate(centerVertex,oneSidedVertices[i]),
                    (Edge)edges[0].Recreate(oneSidedVertices[i],oneSidedVertices[(i+1)%n]),
                    (Edge)edges[0].Recreate(oneSidedVertices[(i+1)%n],centerVertex)
                });
                res.Add(triangle);
            }
            return res;
        }
        /* assumes polyhedron to be 2d */
        public List<Face2d> BoundaryTriangulation2d() {
            var res = new List<Face2d>();
            var n = edges.Count;
            var o = edges[0].a;
            int iB = -1;
            for (int i=0;i<n;i++) {
                var a = edges[i+1].a.getPoint();
                iB = i+2;
                var b = edges[iB].a.getPoint();
                if (!AOP.Colinear1d(o.getPoint(),a,b)) break;
            }
            Debug.Assert(iB!=-1);
            int iE = -1;
            for (int i=n;i>0;i--) {
                var a = edges[i-1].a.getPoint();
                iE = i-2;
                var b = edges[iE].a.getPoint();
                if (!AOP.Colinear1d(o.getPoint(),a,b)) break;
            }
            Debug.Assert(iE!=-1);
            for (int i=0;i+1<iB;i++) {
                res.Add(Recreate(new List<Edge>{
                    (Edge)edges[0].Recreate(edges[i].a,edges[i+1].a),
                    (Edge)edges[0].Recreate(edges[i+1].a,edges[iB].a),
                    (Edge)edges[0].Recreate(edges[iB].a,edges[i].a)}));
            }
            for (int i=iB;i<=iE-1;i++) {
                res.Add(Recreate(new List<Edge>{
                    (Edge)edges[0].Recreate(o,edges[i].a),
                    (Edge)edges[0].Recreate(edges[i].a,edges[i+1].a), //only using .a (reduces vertex count)
                    (Edge)edges[0].Recreate(edges[i+1].a,o)}));
            }
            for (int i=iE+1;i<n;i++) {
                res.Add(Recreate(new List<Edge>{
                    (Edge)edges[0].Recreate(edges[iE].a,edges[i].a),
                    (Edge)edges[0].Recreate(edges[i].a,edges[(i+1)%n].a),
                    (Edge)edges[0].Recreate(edges[(i+1)%n].a,edges[iE].a)}));
            }
            return res;
        }
        public void MakeCounterClockwise(Point insidePoint) {
            Edge edge1 = edges[^1];
            Edge edge2 = edges[0];
            Point middle = edge1.b.getPoint();
            Debug.Assert(middle.Equals(edge2.a.getPoint()), "2662490146");
            Point clockwiseNormal = AOP.cross(edge2.b.getPoint().subtract(middle),edge1.a.getPoint().subtract(middle));
            Point insideVector = insidePoint.clone().subtract(middle);
            if (clockwiseNormal.sc(insideVector)>0) { //clockwise normal points inward
                edges.Reverse();
            }
        }
        public void Replace(IPolyhedron edge_, IPolyhedron inner_, IPolyhedron outer_) {
            var edge = (Edge)edge_;
            var inner = (Edge)inner_;
            var outer = (Edge)outer_;

            Edge edge1;
            Edge edge2;
            if (outer.a.Equals(edge.a)) {
                Debug.Assert(inner.b.Equals(edge.b));
                edge2 = (Edge)inner;
                edge1 = (Edge)outer;
            } else if (inner.a.Equals(edge.a)) {
                Debug.Assert(outer.b.Equals(edge.b));
                edge2 = (Edge)outer;
                edge1 = (Edge)inner;
            } else {
                throw new Exception();
            }
            Debug.Assert(edge1.a.Equals(edge.a));
            Debug.Assert(edge2.b.Equals(edge.b));
            
            var index = edges.IndexOf(edge);
            Debug.Assert(index>=0);

            edges.Insert(index,edge2);
            edges.Insert(index,edge1);
            edges.RemoveAt(index+2);
        }
        public SplitResult Split(HalfSpace cutPlane) {
            List<Edge> inner = new();
            List<Edge> outer = new();
            List<Vertex> innerCut = new();
            List<Vertex> outerCut = new();
            Edge innerCutEdge = null;
            Edge outerCutEdge = null;
            Edge edge1 = edges[^1];
            Vertex a = edge1.a;
            int sideA = cutPlane.side(a.getPoint());
            Vertex b = edge1.b;
            int sideB = cutPlane.side(b.getPoint());
            
            for (int i=0;i<edges.Count;i++) {
                var edge2 = edges[i];
                Vertex c = edge2.b;
                int sideC = cutPlane.side(c.getPoint());

                if (sideB == HalfSpace.INSIDE && sideC == HalfSpace.OUTSIDE) {
                    // Point cutBC = cutPlane.cutPoint(b.getPoint(),c.getPoint());
                    // Edge cut1 = (Edge)edge2.Recreate(b,(Vertex)c.Recreate(cutBC));
                    // Edge cut2 = (Edge)edge2.Recreate((Vertex)b.Recreate(cutBC),c);
                    var cr = edge2.Split(cutPlane);
                    var cut1 = (Edge)cr.inner;
                    var cut2 = (Edge)cr.outer;
                    outerCut.Add((Vertex)cr.outerCut);
                    innerCut.Add((Vertex)cr.innerCut);
                    inner.Add(cut1);
                    if (innerCut.Count==2) {
                        Debug.Assert(outerCut.Count==2,"5601857415");
                        innerCutEdge = (Edge)edge2.Recreate((Vertex)innerCut[1].OpposingClone(),(Vertex)innerCut[0].OpposingClone());
                        inner.Add(innerCutEdge); //inner cycle closed
                        Debug.Assert(innerCut[0].getPoint().Equals(inner.First().a.getPoint()),"8976978753");

                        //outer cycle to continue
                        outerCutEdge = (Edge)edge2.Recreate((Vertex)outerCut[0].OpposingClone(),(Vertex)outerCut[1].OpposingClone());
                        outer.Add(outerCutEdge);
                    }
                    sideB = HalfSpace.CONTAINED;
                    edge2 = cut2;
                } 
                else if (sideB == HalfSpace.OUTSIDE && sideC == HalfSpace.INSIDE) {
                    var cr = edge2.Split(cutPlane);
                    //Point cutBC = cutPlane.cutPoint(b.getPoint(),c.getPoint());
                    //Edge cut1 = (Edge)edge2.Recreate(b,(Vertex)c.Recreate(cutBC));
                    //Edge cut2 = (Edge)edge2.Recreate((Vertex)b.Recreate(cutBC),c);
                    var cut1 = (Edge)cr.outer;
                    var cut2 = (Edge)cr.inner;
                    innerCut.Add((Vertex)cr.innerCut);
                    outerCut.Add((Vertex)cr.outerCut);
                    outer.Add(cut1);
                    if (innerCut.Count==2) {
                        Debug.Assert(outerCut.Count==2,"2312108382");
                        outerCutEdge = (Edge)edge2.Recreate((Vertex)outerCut[1].OpposingClone(),(Vertex)outerCut[0].OpposingClone());
                        outer.Add(outerCutEdge); //outer cycle closed
                        Debug.Assert(outerCut[0].getPoint().Equals(outer.First().a.getPoint()),"1002646478");
                        outerCut[0].neighbor = outer.First().a;
                        outer.First().a.neighbor = outerCut[0];
                        
                        //inner cycle to continue
                        innerCutEdge = (Edge)edge2.Recreate((Vertex)innerCut[0].OpposingClone(),(Vertex)innerCut[1].OpposingClone());
                        inner.Add(innerCutEdge);
                    }
                    sideB = HalfSpace.CONTAINED;
                    edge2 = cut2;
                }
                else if (sideA == HalfSpace.INSIDE && sideB==HalfSpace.CONTAINED && sideC == HalfSpace.OUTSIDE) {
                    outerCut.Add(edge2.a);
                    innerCut.Add(edge1.b);
                    inner.Add(edge1);
                    if (innerCut.Count==2) {
                        Debug.Assert(outerCut.Count==2,"9521366133");
                        innerCutEdge = (Edge)edge2.Recreate((Vertex)innerCut[1].OpposingClone(),(Vertex)innerCut[0].OpposingClone());
                        inner.Add(innerCutEdge); //inner cycle closed
                        Debug.Assert(innerCut[0].getPoint().Equals(inner.First().a.getPoint()),"2307260588");

                        //outer cycle to continue
                        outerCutEdge = (Edge)edge2.Recreate((Vertex)outerCut[0].OpposingClone(),(Vertex)outerCut[1].OpposingClone());
                        outer.Add(outerCutEdge);
                    }
                }
                else if (sideA == HalfSpace.OUTSIDE && sideB==HalfSpace.CONTAINED && sideC == HalfSpace.INSIDE) {
                    innerCut.Add(edge2.a);
                    outerCut.Add(edge1.b);
                    outer.Add(edge1);
                    if (innerCut.Count==2) {
                        outerCutEdge = (Edge)edge2.Recreate((Vertex)outerCut[1].OpposingClone(),(Vertex)outerCut[0].OpposingClone());
                        outer.Add(outerCutEdge); //outer cycle closed
                        Debug.Assert(outerCut[0].getPoint().Equals(outer.First().a.getPoint()),"8097379053");

                        //inner cycle to continue
                        innerCutEdge = (Edge)edge2.Recreate((Vertex)innerCut[0].OpposingClone(),(Vertex)innerCut[1].OpposingClone());
                        inner.Add(innerCutEdge);
                    }
                }
                else if (sideB == HalfSpace.OUTSIDE && sideC == HalfSpace.OUTSIDE) {/* edge1 already added */}
                else if (sideB == HalfSpace.INSIDE  && sideC == HalfSpace.INSIDE ) {/* edge1 already added */}
                else if (sideA==HalfSpace.INSIDE && sideB==HalfSpace.CONTAINED && sideC==HalfSpace.CONTAINED) {
                    var ic = edge2;
                    return new SplitResult() {inner=this,innerCut=ic,outer=null};
                } else if (sideC==HalfSpace.OUTSIDE && sideB==HalfSpace.CONTAINED && sideC==HalfSpace.CONTAINED) {
                    var oc = edge2;
                    return new SplitResult {inner=null,outerCut=oc,outer=this};
                } else if (sideA==HalfSpace.CONTAINED && sideB==HalfSpace.CONTAINED && sideC==HalfSpace.INSIDE) {
                    var ic = edge1;
                    return new SplitResult {inner=this,innerCut=ic,outer=null};
                } else if (sideA==HalfSpace.CONTAINED && sideB==HalfSpace.CONTAINED && sideC==HalfSpace.OUTSIDE) {
                    var oc = edge1;
                    return new SplitResult {inner=null,outerCut=oc,outer=this};
                }
                else if (sideA == HalfSpace.OUTSIDE  && sideB==HalfSpace.CONTAINED && sideC==HalfSpace.OUTSIDE) {
                    outer.Add(edge1);
                }
                else if (sideA == HalfSpace.INSIDE  && sideB==HalfSpace.CONTAINED && sideC==HalfSpace.INSIDE) {
                    inner.Add(edge1);
                }
                else if (sideA == HalfSpace.CONTAINED && sideB==HalfSpace.CONTAINED && sideC == HalfSpace.CONTAINED) {
                  	if (! AOP.Colinear1d(a.PointRef(), b.PointRef(), c.PointRef())) return new SplitResult {isContained=true};
                }
                else if (sideC == HalfSpace.CONTAINED) {/* will be handled in the next step */}
                else {
                    throw new Exception();
                }
                
                if (sideC==HalfSpace.INSIDE) {
                    inner.Add(edge2);
                } else if (sideC == HalfSpace.OUTSIDE) {
                    outer.Add(edge2);
                } else if (sideC == HalfSpace.CONTAINED) {
                    //wait for the next edge
                } else {
                    throw new Exception();
                }

                sideA = sideB;
                b = c;
                sideB = sideC;
                edge1 = edge2;
            }
            IPolyhedron resInner = null;
            IPolyhedron resOuter = null;
            if (inner.Count>0) {
                resInner = Recreate(inner);
            }
            if (outer.Count>0) {
                resOuter = Recreate(outer);
            }
            Debug.Assert(innerCut.Count==2 && outerCut.Count==2 || innerCut.Count==0 && outerCut.Count==0,"3805925512");
            if (innerCut.Count==2) {
                Debug.Assert(innerCutEdge!=null,"5505643563");
                Debug.Assert(outerCutEdge!=null,"7220313878");
            }
            var res = new SplitResult {inner=resInner,innerCut=innerCutEdge,outerCut=outerCutEdge, outer=resOuter}.CrossReference(this,cutPlane);
            return res;
        }
        public void Inset(double delta) {
            var n = edges.Count;
            var insetPoints = AOP.Inset3d(points,delta);
            for (int i=0;i<n;i++) {
                Debug.Assert(edges[i].b.PointRef().Equals(edges[(i+1)%n].a.PointRef()),
                "Neighboring edge's vertices are not equal");
                // Debug.Assert(edges[i].b.PointRef()==edges[(i+1)%n].a.PointRef(),"Edges vertices do not refer to the same points");
            }
            for (int i=0;i<n;i++) {
                for (int d=0;d<insetPoints[0].x.Length;d++) {
                    edges[i].a.PointRef().x[d] = insetPoints[i].x[d];
                    edges[i].b.PointRef().x[d] = insetPoints[(i+1)%n].x[d];
                }
                // edges[i].a.PointRef().x = insetPoints[i].x;
                // edges[i].b.PointRef().x = insetPoints[(i+1)%n].x;
            }
        }

        public class NotInPlaneException : Exception {}
        /// <summary>
        /// 
        /// </summary>
        /// <param name="p">the point to check</param>
        /// <returns>1 if p is outside, 0 if on the boundary, -1 if inside (see HalfSpace)</returns>
        public int Containment(Point p) {
            Debug.Assert(p.dim()==3);
            Point n = Normal();
            var a = edges[0].a.PointRef();
            if (!AOP.eq(p.clone().subtract(a).sc(n),0)) throw new NotInPlaneException();
            foreach (var edge in edges) {
                a = edge.a.PointRef();
                var ab = edge.b.getPoint().subtract(a).normalize();
                var hs = new HalfSpace(edge.a.getPoint(),AOP.cross(ab,n).normalize());
                var side = hs.side(p);
                if (side==HalfSpace.OUTSIDE) return side;
                if (side==HalfSpace.CONTAINED) {
                    var t = ab.sc(p.clone().subtract(a));
                    if (0<=t && t<=1) return 0;
                }
            }
            return -1;
        }
        public bool Contains(Face2d facet) {
            foreach (var edge in facet.edges) {
                var p = edge.a.getPoint();
                try {
                    if (Containment(p)==HalfSpace.OUTSIDE) {
                        return false;
                    }
                } catch (NotInPlaneException) {
                    return false;
                }
            }
            return true;         
        }
    }
    public class OrientedFace2d : Face2d {
        public OrientedFace2d(Face2d face2d) : base(face2d) {}


        public override bool Equals(object obj) {
            if (obj==null) return false;
            var otherVertices = ((OrientedFace2d) obj).edges;
            return RotationEqual(this.edges,otherVertices);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    public class Face2dWithIntegerCellAttribute : Face2d, IPolyhedronWithIntegerCellAttribute
    {
        public IntegerCell integerCell { get; set; }
        public Face2dWithIntegerCellAttribute(List<Point> points, bool isInvisible, IntegerCell ic) : base(points, isInvisible)
        {
            integerCell = ic;
        }
        public Face2dWithIntegerCellAttribute(List<Edge> edges, bool isInvisible, IntegerCell ic) : base(edges, isInvisible)
        {
            integerCell = ic;
        }
        public Face2dWithIntegerCellAttribute(HashSet<IPolyhedron> facets, bool isInvisible, IntegerCell ic) : base(facets, isInvisible)
        {
            integerCell = ic;
        }
        public override Face2d Recreate(List<Edge> edges) {
            return new Face2dWithIntegerCellAttribute(edges,isInvisible,integerCell);
        }
        public override Face2d Recreate(List<Point> points)
        {
            return new Face2dWithIntegerCellAttribute(points, isInvisible,integerCell);
        }
        public override IPolyhedron Recreate(HashSet<IPolyhedron> facets) { 

            return new Face2dWithIntegerCellAttribute(facets, isInvisible, integerCell);
        }
    }
}