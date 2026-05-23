using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using D4BB.Comb;

namespace D4BB.Geometry
{
    public interface IPolyhedronWithIntegerCellAttribute : IPolyhedron {
        public IntegerCell integerCell { get; }
    }
    public class Face2d : IPolyhedron { // a more efficient implementation than the default Polyhedron
        public List<Edge> edges {get; protected set;}
        public bool isCoplanarInterior { get; set; }
        public IPolyhedron parent {get; set; }
        public IPolyhedron neighbor {get;set;}
        public int mark {get;set;}
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

        public Face2d(List<Point> points, bool isConnecting=false) { //clockwise or anticlockwise
            this.isCoplanarInterior = isConnecting;
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
        public Face2d(Point a, Point b, Point c, bool isConnecting) : this(new List<Point>{a,b,c},isConnecting){}
        public Face2d(Face2d face2d) : this(face2d.edges,face2d.isCoplanarInterior) {}
        public Face2d(List<Edge> edges, bool isConnecting=false) {
            this.isCoplanarInterior = isConnecting;
            this.edges = edges;
            SetNeighbors();
        }
        public Face2d(HashSet<IPolyhedron> edges, bool isConnecting) : this(SortedEdges(edges),isConnecting) {}
        public virtual IPolyhedron Recreate(HashSet<IPolyhedron> facets) {
            return new Face2d(facets, isCoplanarInterior) { parent = parent, neighbor=neighbor, mark=mark };
        }
        public virtual Face2d Recreate(List<Edge> edges) {
            return new Face2d(edges, isCoplanarInterior) { parent = parent, neighbor=neighbor, mark=mark };
        }
        public virtual Face2d Recreate(List<Point> points) {
            return new Face2d(points, isCoplanarInterior) { parent = parent, neighbor=neighbor, mark=mark };
        }
        public IPolyhedron OpposingClone() {
            List<Edge> newEdges = new();
            foreach (var edge in edges) {
                newEdges.Add((Edge)edge.OpposingClone());
            }
            newEdges.Reverse();
            var res = new Face2d(newEdges,isCoplanarInterior) { parent = parent, mark = mark };
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
        public static bool RotationEqual(List<Point> points1, List<Point> points2, int binaryPrecision=AOP.binaryPrecision) {
            if (points1.Count!=points2.Count) return false;
            if (points1.Count==0) return true;
            for (int i=0;i<points2.Count;i++) {
                if (points1[0].Equals(points2[i],binaryPrecision)) { 
                    var off=i; 
                    var isEqual = true;
                    for (int j=0;j<points2.Count;j++) {
                        if (!points1[j].Equals(points2[(j+off)%points2.Count],binaryPrecision)) {
                            isEqual=false;
                            break;
                        }
                    }
                    if (isEqual) return true;
                    }
            }
            return false;
        }
        public static bool RotationEqual(List<Edge> edges1, List<Edge> edges2, int binaryPrecision=AOP.binaryPrecision) { 
            return RotationEqual(
                edges1.Select(edge => edge.a.getPoint()).ToList(),
                edges2.Select(edge => edge.a.getPoint()).ToList(),
                binaryPrecision
            );}
        public static bool Face2dOrientedEquals(Face2d a,Face2d b, int binaryPrecision=AOP.binaryPrecision) {
            return RotationEqual(a.edges,b.edges, binaryPrecision);
        }
        public static bool Face2dUnOrientedEquals(Face2d a, Face2d b,int binaryPrecision=AOP.binaryPrecision) {
            if (RotationEqual(a.edges,b.edges,binaryPrecision)) return true;
            var aReversedEdges = new List<Edge>(a.edges);
            aReversedEdges.Reverse();
            return RotationEqual(aReversedEdges,b.edges,binaryPrecision);
        }
        public override bool Equals(object obj) {
            if (obj==null) return false;
            var other = (Face2d) obj;
            return Face2dUnOrientedEquals(this,other);
        }
        public bool Equals(object obj, int precision) {
            if (obj==null) return false;
            var other = (Face2d) obj;
            return Face2dUnOrientedEquals(this,other,precision);
        }
        public override int GetHashCode()
        {
            return GetHashCode(AOP.binaryPrecision);
        }
        public int GetHashCode(int precision)
        {
            int res = 0;
            foreach (var edge in edges) {
                res += edge.a.PointRef().GetHashCode(precision);
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
            cross.normalize();
            return cross;
        }
        public HalfSpace HalfSpaceOf() {
            return new HalfSpace(edges[0].a.getPoint(),Normal());
        }
        /* assumes polyhedron to be 2d */
        public HashSet<Face2d> CenterTriangulation2d(Point centerPoint) {
            HashSet<Face2d> res = new();
            var centerVertex = new Vertex(centerPoint,isCoplanarInterior:true);
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
            if (iB>iE && iB==2 && iE==n-2) { //undivided triangle
                Debug.Assert(n==3);
                return new List<Face2d> {Recreate(new List<Edge>{
                    (Edge)edges[0].Recreate(edges[0].a,edges[1].a),
                    (Edge)edges[0].Recreate(edges[1].a,edges[2].a),
                    (Edge)edges[0].Recreate(edges[2].a,edges[0].a)
                })};
            }
            if (iB>iE && iB > 2 && iE < n-2) {
                //beginning and end is subdivided
                var res = new List<Face2d>();
                for (int i=1;i+1<iB;i++) {
                    res.Add(Recreate(new List<Edge>{
                        (Edge)edges[0].Recreate(edges[i].a,edges[i+1].a),
                        (Edge)edges[0].Recreate(edges[i+1].a,edges[iB].a),
                        (Edge)edges[0].Recreate(edges[iB].a,edges[i].a)}));
                }
                for (int i=iE+1;i<n;i++) {
                    res.Add(Recreate(new List<Edge>{
                        (Edge)edges[0].Recreate(edges[1].a,edges[i].a),
                        (Edge)edges[0].Recreate(edges[i].a,edges[(i+1)%n].a),
                        (Edge)edges[0].Recreate(edges[(i+1)%n].a,edges[1].a)}));
                }
                return res;
            }
            {
                var res = new List<Face2d>();
                if (iB<=iE||iE==n-2) for (int i=0;i+1<iB;i++) {
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
                if (iB<=iE||iB==2) for (int i=iE+1;i<n;i++) {
                    res.Add(Recreate(new List<Edge>{
                        (Edge)edges[0].Recreate(edges[iE].a,edges[i].a),
                        (Edge)edges[0].Recreate(edges[i].a,edges[(i+1)%n].a),
                        (Edge)edges[0].Recreate(edges[(i+1)%n].a,edges[iE].a)}));
                }
                return res;
            }
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
                Debug.Assert(inner.b.Equals(edge.b), "2662490147");
                edge2 = (Edge)inner;
                edge1 = (Edge)outer;
            } else if (inner.a.Equals(edge.a)) {
                Debug.Assert(outer.b.Equals(edge.b), "2662490148");
                edge2 = (Edge)outer;
                edge1 = (Edge)inner;
            } else {
                throw new Exception();
            }
            Debug.Assert(edge1.a.Equals(edge.a), "2662490149");
            Debug.Assert(edge2.b.Equals(edge.b), "2662490150");
            
            var index = edges.IndexOf(edge);
            Debug.Assert(index>=0, $"2662490151 {this} {edge}");

            edges[index]= edge2;
            edges.Insert(index,edge1);
        }
        // Sutherland-Hodgman based polygon split. The polygon is assumed convex.
        // Each polygon vertex is classified as INSIDE / OUTSIDE / CONTAINED relative to cutPlane.
        // - All CONTAINED (and not all colinear): isContained.
        // - No OUTSIDE vertex: polygon entirely on inner side. innerCut may be a CONT-CONT edge.
        // - No INSIDE vertex: polygon entirely on outer side.
        // - Otherwise: walk edges once, routing each edge (or its split halves) into
        //   innerEdges / outerEdges, and recording the up to 2 cut-vertex pairs. The
        //   inner/outer arc start/end are recovered from the edge lists by point-set
        //   difference, then the cut edges are added with the orientation that closes
        //   the cycle CCW. SortedEdges (via Recreate(HashSet)) takes care of ordering.
        public SplitResult Split(HalfSpace cutPlane) {
            int n = edges.Count;
            int[] sides = new int[n];
            Vertex[] verts = new Vertex[n];
            for (int i = 0; i < n; i++) {
                verts[i] = edges[i].a;
                sides[i] = cutPlane.side(verts[i].getPoint());
            }

            int nIn = 0, nOut = 0, nCon = 0;
            for (int i = 0; i < n; i++) {
                if (sides[i] == HalfSpace.INSIDE) nIn++;
                else if (sides[i] == HalfSpace.OUTSIDE) nOut++;
                else nCon++;
            }

            if (nCon == n) {
                bool allColinear = true;
                for (int i = 0; i < n; i++) {
                    if (!AOP.Colinear1d(verts[i].PointRef(), verts[(i+1)%n].PointRef(), verts[(i+2)%n].PointRef())) {
                        allColinear = false; break;
                    }
                }
                if (!allColinear) return new SplitResult { isContained = true };
            }

            if (nOut == 0) {
                Edge tangentEdge = null;
                for (int i = 0; i < n; i++) {
                    if (sides[i] == HalfSpace.CONTAINED && sides[(i+1)%n] == HalfSpace.CONTAINED) {
                        tangentEdge = edges[i]; break;
                    }
                }
                return new SplitResult { inner = this, innerCut = tangentEdge, outer = null };
            }
            if (nIn == 0) {
                Edge tangentEdge = null;
                for (int i = 0; i < n; i++) {
                    if (sides[i] == HalfSpace.CONTAINED && sides[(i+1)%n] == HalfSpace.CONTAINED) {
                        tangentEdge = edges[i]; break;
                    }
                }
                return new SplitResult { outer = this, outerCut = tangentEdge, inner = null };
            }

            var innerEdges = new List<Edge>();
            var outerEdges = new List<Edge>();
            var innerCutVerts = new List<Vertex>();
            var outerCutVerts = new List<Vertex>();

            for (int i = 0; i < n; i++) {
                var edge = edges[i];
                int sa = sides[i];
                int sb = sides[(i+1) % n];

                if (sa == HalfSpace.INSIDE && sb == HalfSpace.INSIDE) {
                    innerEdges.Add(edge);
                }
                else if (sa == HalfSpace.OUTSIDE && sb == HalfSpace.OUTSIDE) {
                    outerEdges.Add(edge);
                }
                else if ((sa == HalfSpace.INSIDE && sb == HalfSpace.OUTSIDE)
                      || (sa == HalfSpace.OUTSIDE && sb == HalfSpace.INSIDE)) {
                    var sr = edge.Split(cutPlane);
                    innerEdges.Add((Edge)sr.inner);
                    outerEdges.Add((Edge)sr.outer);
                    innerCutVerts.Add((Vertex)sr.innerCut);
                    outerCutVerts.Add((Vertex)sr.outerCut);
                }
                else if (sa == HalfSpace.CONTAINED && sb == HalfSpace.INSIDE) {
                    innerEdges.Add(edge);
                    int iPrev = (i - 1 + n) % n;
                    if (sides[iPrev] == HalfSpace.OUTSIDE) {
                        // transition through CONTAINED vertex
                        innerCutVerts.Add(edge.a);
                        outerCutVerts.Add(edges[iPrev].b);
                    }
                }
                else if (sa == HalfSpace.CONTAINED && sb == HalfSpace.OUTSIDE) {
                    outerEdges.Add(edge);
                    int iPrev = (i - 1 + n) % n;
                    if (sides[iPrev] == HalfSpace.INSIDE) {
                        outerCutVerts.Add(edge.a);
                        innerCutVerts.Add(edges[iPrev].b);
                    }
                }
                else if (sa == HalfSpace.INSIDE && sb == HalfSpace.CONTAINED) {
                    innerEdges.Add(edge);
                    // transition recorded by next iteration's CONTAINED-OUTSIDE / CONTAINED-INSIDE
                }
                else if (sa == HalfSpace.OUTSIDE && sb == HalfSpace.CONTAINED) {
                    outerEdges.Add(edge);
                }
                // sa==CONTAINED && sb==CONTAINED: cannot occur in a genuine cut of a convex polygon — skip defensively
            }

            Debug.Assert(innerCutVerts.Count == 2 && outerCutVerts.Count == 2,
                $"SH split expected 2 cut-vertex pairs but got inner={innerCutVerts.Count} outer={outerCutVerts.Count}");

            Vertex innerArcStart = FindArcStart(innerEdges);
            Vertex innerArcEnd = FindArcEnd(innerEdges);

            Vertex innerCutFromV, innerCutToV, outerCutFromV, outerCutToV;
            if (innerCutVerts[0].PointRef().Equals(innerArcEnd.PointRef())) {
                innerCutFromV = (Vertex)innerCutVerts[0].OpposingClone();
                innerCutToV = (Vertex)innerCutVerts[1].OpposingClone();
                outerCutFromV = (Vertex)outerCutVerts[1].OpposingClone();
                outerCutToV = (Vertex)outerCutVerts[0].OpposingClone();
            } else {
                innerCutFromV = (Vertex)innerCutVerts[1].OpposingClone();
                innerCutToV = (Vertex)innerCutVerts[0].OpposingClone();
                outerCutFromV = (Vertex)outerCutVerts[0].OpposingClone();
                outerCutToV = (Vertex)outerCutVerts[1].OpposingClone();
            }

            Edge proto = edges[0];
            Edge innerCutEdge = (Edge)proto.Recreate(innerCutFromV, innerCutToV);
            Edge outerCutEdge = (Edge)proto.Recreate(outerCutFromV, outerCutToV);
            // Recreate would have inherited proto.mark, but the cut edge is fresh geometry
            // at the cut plane — reset to MARK_NONE so grid-intersection toggles don't
            // accidentally hide BSP-cut edges.
            innerCutEdge.mark = IPolyhedron.MARK_NONE;
            outerCutEdge.mark = IPolyhedron.MARK_NONE;

            // Build chained lists in CCW cycle order. SortedEdges (used by Recreate(HashSet))
            // relies on Vertex.Equals which is type-strict — it fails when a face mixes
            // original VertexBC vertices with plain Vertex cut-points produced by Edge.Split,
            // so we chain by point-equality directly here.
            var innerOrdered = ChainByPoint(innerEdges, innerCutEdge);
            var outerOrdered = ChainByPoint(outerEdges, outerCutEdge);

            IPolyhedron resInner = Recreate(innerOrdered);
            IPolyhedron resOuter = Recreate(outerOrdered);

            return new SplitResult {
                inner = resInner,
                outer = resOuter,
                innerCut = innerCutEdge,
                outerCut = outerCutEdge
            }.CrossReference(this, cutPlane);
        }
        static Vertex FindArcStart(List<Edge> arc) {
            // vertex that appears as .a but not as .b (by point-equality) — open start of arc
            foreach (var e in arc) {
                bool isStart = true;
                foreach (var e2 in arc) {
                    if (ReferenceEquals(e, e2)) continue;
                    if (e2.b.PointRef().Equals(e.a.PointRef())) { isStart = false; break; }
                }
                if (isStart) return e.a;
            }
            return null;
        }
        static Vertex FindArcEnd(List<Edge> arc) {
            foreach (var e in arc) {
                bool isEnd = true;
                foreach (var e2 in arc) {
                    if (ReferenceEquals(e, e2)) continue;
                    if (e2.a.PointRef().Equals(e.b.PointRef())) { isEnd = false; break; }
                }
                if (isEnd) return e.b;
            }
            return null;
        }
        static List<Edge> ChainByPoint(List<Edge> arcEdges, Edge cutEdge) {
            // Combine arc edges and cut edge, then chain them into a directed cycle by
            // matching .b -> .a using Point equality (type-agnostic).
            var pool = new List<Edge>(arcEdges) { cutEdge };
            var result = new List<Edge>(pool.Count);
            var current = pool[0];
            pool.RemoveAt(0);
            result.Add(current);
            while (pool.Count > 0) {
                int found = -1;
                for (int i = 0; i < pool.Count; i++) {
                    if (current.b.PointRef().Equals(pool[i].a.PointRef())) { found = i; break; }
                }
                Debug.Assert(found >= 0, "SH split: arc cannot be chained into a cycle");
                current = pool[found];
                pool.RemoveAt(found);
                result.Add(current);
            }
            Debug.Assert(result[result.Count - 1].b.PointRef().Equals(result[0].a.PointRef()),
                "SH split: chained arc is not closed");
            return result;
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
            bool onBoundary = false;
            foreach (var edge in edges) {
                a = edge.a.PointRef();
                var ab = edge.b.getPoint().subtract(a);
                var hs = new HalfSpace(edge.a.getPoint(),AOP.cross(ab,n).normalize());
                var side = hs.side(p);
                if (side==HalfSpace.OUTSIDE) return side;
                if (side==HalfSpace.CONTAINED) {
                    var t = ab.sc(p.clone().subtract(a)) / ab.sc(ab);
                    if (t<0 || t>1) return HalfSpace.OUTSIDE;
                    onBoundary = true;
                }
            }
            return onBoundary ? HalfSpace.CONTAINED : HalfSpace.INSIDE;
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
        /// <summary>
        /// Calculates minimum and maximum distances of the points in this facet
        /// </summary>
        /// <param name="facet"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        public void Dist(out double min, out double max) {
            max = 0;
            min = Double.MaxValue;
            foreach (var e1 in edges) {
                foreach (var e2 in edges) {
                    if (e1==e2) continue;
                    var p1 = e1.a.PointRef();
                    var p2 = e2.a.PointRef();
                    double d = p1.clone().subtract(p2).len();
                    if (d>max) {
                        max = d; 
                    }
                    if (d<min) {
                        min = d;
                    }
                }
            }

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
        public Face2dWithIntegerCellAttribute(List<Point> points, bool isConnecting, IntegerCell ic) : base(points, isConnecting)
        {
            integerCell = ic;
        }
        public Face2dWithIntegerCellAttribute(List<Edge> edges, bool isConnecting, IntegerCell ic) : base(edges, isConnecting)
        {
            integerCell = ic;
        }
        public Face2dWithIntegerCellAttribute(HashSet<IPolyhedron> facets, bool isConnecting, IntegerCell ic) : base(facets, isConnecting)
        {
            integerCell = ic;
        }
        public override Face2d Recreate(List<Edge> edges) {
            return new Face2dWithIntegerCellAttribute(edges,isCoplanarInterior,integerCell) { parent=parent, neighbor=neighbor, mark=mark };
        }
        public override Face2d Recreate(List<Point> points)
        {
            return new Face2dWithIntegerCellAttribute(points, isCoplanarInterior,integerCell) { parent=parent, neighbor=neighbor, mark=mark };
        }
        public override IPolyhedron Recreate(HashSet<IPolyhedron> facets) {

            return new Face2dWithIntegerCellAttribute(facets, isCoplanarInterior, integerCell) { parent=parent, neighbor=neighbor, mark=mark };
        }
    }
    public class Face2dOrientedEquality : IEqualityComparer<Face2d>
    {
        public readonly int precision;
        public Face2dOrientedEquality(int binaryPrecision) {
            this.precision = binaryPrecision;
        }
        public bool Equals(Face2d x, Face2d y)
        {
            return Face2d.Face2dOrientedEquals(x,y,precision);
        }

        public int GetHashCode(Face2d obj)
        {
            return obj.GetHashCode(precision);
        }
    }
    public class Face2dUnOrientedEquality : IEqualityComparer<Face2d>
    {
        public readonly int precision;
        public Face2dUnOrientedEquality(int binaryPrecision) {
            this.precision = binaryPrecision;
        }
        public bool Equals(Face2d x, Face2d y)
        {
            return Face2d.Face2dUnOrientedEquals(x,y,precision);
        }

        public int GetHashCode(Face2d obj)
        {
            return obj.GetHashCode(precision);
        }
    }
}