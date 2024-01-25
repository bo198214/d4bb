using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using D4BB.Comb;

namespace D4BB.Geometry
{
    public struct SplitResult {
        /* following combinations are possible:
        isContained false:
            if innerCut is defined, then also inner has to be defined, same with outer
            if all pointers are non-null the polyhedron's split into two same-dimensional polyhedrons
            if only innerCut/outerCut is null, the polyhedron is completely to one side of the cut plane
            if innerCut & inner is non-null then the polyhedron is on the inner side of the cut plane, and the plane intersects with a facet
            same for outer
        isContained:
            the polyhedron is completely contained in the cut plane.
        */
        public static readonly int GENUINE_INSIDE = -2; //no touching facet, but maybe touching lower dimensional faces
        public static readonly int TOUCHING_INSIDE = -1; //touching facet
        public static readonly int GENUINE_CUT = 0;
        public static readonly int TOUCHING_OUTSIDE = 1; //touching facet
        public static readonly int GENUINE_OUTSIDE = 2; //no touching facet, but maybe touching lower dimensional faces
        public static readonly int CONTAINED = 10;
        public IPolyhedron inner;
        public IPolyhedron innerCut;
        public IPolyhedron outerCut;
        public IPolyhedron outer;
        public bool isContained;
        public readonly SplitResult CrossReference(IPolyhedron orig, HalfSpace hs) {
            if (inner!=null && outer!=null) {
                Debug.Assert(innerCut!=null && outerCut!=null,"5333029568");
                innerCut.isInvisible = true; //because they are in the same subspace
                outerCut.isInvisible = true;

                inner.parent = orig.parent;
                outer.parent = orig.parent;
                innerCut.neighbor = outerCut;
                innerCut.parent = inner;
                outerCut.neighbor = innerCut;
                outerCut.parent = outer;

                // if (polyhedron.innerNeighborTemp!=null) { //neighbor was already split
                //     Debug.Assert(polyhedron.outerNeighborTemp!=null,"0865182622");
                //     inner.neighbor = polyhedron.innerNeighborTemp;
                //     polyhedron.innerNeighborTemp.neighbor = inner;
                //     outer.neighbor = polyhedron.outerNeighborTemp;
                //     polyhedron.outerNeighborTemp.neighbor = outer;
                //     polyhedron.innerNeighborTemp = null;
                //     polyhedron.outerNeighborTemp = null;
                // }
                // else if (polyhedron.neighbor!=null) { //neighbor exists, but was not yet split
                //     polyhedron.neighbor.neighbor = null;
                //     polyhedron.neighbor.innerNeighborTemp = inner;
                //     polyhedron.neighbor.outerNeighborTemp = outer;
                //     inner.neighbor = polyhedron.neighbor;
                //     outer.neighbor = polyhedron.neighbor;
                // }
                if (orig.neighbor!=null) {
                    //cut also the neighbor polyhedron
                    Debug.Assert(orig.neighbor.neighbor==orig);
                    orig.neighbor.neighbor = null;
                    SplitResult sr = orig.neighbor.Split(hs).CrossReference(orig.neighbor,hs);
                    orig.neighbor.parent.Replace(orig.neighbor,sr.inner,sr.outer);
                    inner.neighbor = sr.inner;
                    sr.inner.neighbor = inner;
                    outer.neighbor = sr.outer;
                    sr.outer.neighbor = outer;
                }
            }
            return this;
        }
    }
    public interface IPolyhedron {
        public HashSet<IPolyhedron> facets {get;}
        public IPolyhedron parent {get;set;}
        public IPolyhedron neighbor {get;set;}
        public int Dim();
        public SplitResult Split(HalfSpace cutPlane);
        public IPolyhedron Recreate(HashSet<IPolyhedron> _facets);
        public void Replace(IPolyhedron ab, IPolyhedron a, IPolyhedron b);
        public bool isInvisible { get; set; }
        /* duplicates the polyhedron with opposite orientation if applicable */
        public IPolyhedron OpposingClone();

        /* dimension of the enclosing space */
        public virtual int SpaceDim() {
            foreach (var facet in facets) {
                return facet.Dim()+1;
            }
            throw new Exception();
        }
        public HashSet<IPolyhedron> Faces(int dim) {
            var res = new HashSet<IPolyhedron>();
            if (Dim() < dim) { return res; }
            if (Dim() == dim) { res.Add(this); return res; }
            foreach (var face in Faces(dim+1)) {
                foreach (var ff in face.facets) {
                    res.Add(ff);
                }
            }
            return res;
        }
        public List<IPolyhedron> FacesList(int dim) {
            var res = new List<IPolyhedron>();
            if (Dim() < dim) { return res; }
            if (Dim() == dim) { res.Add(this); return res; }
            foreach (var face in Faces(dim+1)) {
                foreach (var ff in face.facets) {
                    res.Add(ff);
                }
            }
            return res;
        }
        public HashSet<IPolyhedron> SubFacets() {
            var res = new HashSet<IPolyhedron>();
            foreach (var facet in facets) {
                foreach (var subFacet in facet.facets) {
                    res.Add(subFacet);
                }
            }
            return res;
        }
        public Point[] SpanningPoints() {
            if (Dim()==0) {
                return new Point[]{((Vertex)this).getPoint()};
            }
            if (Dim()==1) {
                var result = new Point[2]; //wasting computation time just to convert type
                var i=0;
                foreach (var poly in facets) {
                    result[i]=((Vertex)poly).getPoint();
                    i++;
                }
                return result;
            }; 

            //two neighboring facets and their connecting subFacet
            IPolyhedron facet1 = null;
            IPolyhedron facet2 = null;
            IPolyhedron subFacet = null;
            foreach (var facet in facets) {
                if (facet1==null) {
                    facet1 = facet;
                    foreach (var sf in facet1.facets) {
                        subFacet = sf;
                        break;
                    }
                    continue;
                }
                if (facet.facets.Contains(subFacet)) {
                    facet2 = facet;
                    break;
                }
            }
            if (subFacet==null) throw new Exception("missing subfacets");
            int n = Dim() + 1;
            Point[] res = new Point[n];
            var subFacetPoints = subFacet.SpanningPoints();
            Debug.Assert(n==subFacetPoints.Length+2,"8658209077");
            for (int i=0;i<n-2;i++) {
                res[i] = subFacetPoints[i];
            }
            res[n-2] = facet1.VertexNotIn(subFacet);
            res[n-1] = facet2.VertexNotIn(subFacet);
            return res;

        }
        /* returns d+1 Vertices for a d dimensional face */
        /* returns one vertex from polyhedron that is not in facet */
        protected  Point VertexNotIn(IPolyhedron facet) {
            if (facet.Dim()==0) {
                foreach (var poly in facets) {
                    Vertex vertex = (Vertex) poly;
                    if (!facet.Equals(vertex)) {
                        return ((Vertex) vertex).getPoint();
                    }
                }
            }
            IPolyhedron subSubFacet = null;
            IPolyhedron subFacet1 = null;
            foreach (var ssf in facet.facets) {
                subSubFacet = ssf;
                break;
            }
            foreach (var sf in facets) {
                if (sf.facets.Contains(subSubFacet) && !sf.Equals(facet)) {
                    subFacet1 = sf;
                    break;
                }
            }
            return subFacet1.VertexNotIn(subSubFacet);
        }
        public bool SameSubspace(IPolyhedron other) {
            var points = new List<Point>(this.SpanningPoints());
            Debug.Assert(points.Count==this.Dim()+1,"2205828049");
            foreach (var b in other.SpanningPoints()) {
                points.Add(b);
            }
            return AOP.SpanningDim(points) == this.Dim();            
        }
        public Point CenterPoint() {
            HashSet<IPolyhedron> vertices = Faces(0);
            Point res = null;
            foreach (var poly in vertices) {
                var vertex = (Vertex)poly;
                if (res==null) {
                    var dim = vertex.getPoint().x.Length;
                    res = new Point(dim);
                }
                res.add(vertex.getPoint());
            }
            res.multiply(1.0/vertices.Count);
            return res;
        }
        // public Point CenterVertex() {
        //     return new Point(CenterPoint());
        // }
        public Dictionary<IPolyhedron,HalfSpace> HalfSpaces() {
            Debug.Assert(Dim()==SpaceDim(),"3882041893");
            Dictionary<IPolyhedron,HalfSpace> res = new();
            Point centerPoint = CenterPoint();
            foreach (var facet in facets) {
                var spanningPoints = facet.SpanningPoints();
                var point0 = spanningPoints[0];
                var spanningVectors = AOP.Points2Vectors(spanningPoints);
                var vectors = new Point[spanningVectors.Length+1];
                for (int i=0;i<spanningVectors.Length;i++) {
                    vectors[i]=spanningVectors[i];
                }
                var centerVector = centerPoint.clone().subtract(point0);
                vectors[spanningVectors.Length]=centerVector;
                AOP.orthoNormalize(vectors);
                var normal = vectors[vectors.Length-1].multiply(-1);
                var halfSpace = new HalfSpace(point0, normal);
                res[facet] = halfSpace;
            }
            return res;
        }
        public Dictionary<IPolyhedron,Point> Normals() {
            Dictionary<IPolyhedron,Point> res = new();
            foreach (var entry in HalfSpaces()) {
                res[entry.Key] = entry.Value.normal;
            }
            return res;
        }
        /* returns one of the SplitResult constants */
        public virtual int Side(HalfSpace cutPlane) {
            bool ii = false;
            bool i = false;
            bool oo = false;
            bool o = false;
            bool c = false;

            foreach (var facet in facets) {
                int side = facet.Side(cutPlane);
                if      (side==SplitResult.GENUINE_CUT)      return SplitResult.GENUINE_CUT;
                else if (side==SplitResult.GENUINE_INSIDE)   ii = true;
                else if (side==SplitResult.TOUCHING_INSIDE)  i  = true;
                else if (side==SplitResult.GENUINE_OUTSIDE)  oo = true;
                else if (side==SplitResult.TOUCHING_OUTSIDE) o  = true;
                else if (side==SplitResult.CONTAINED)        c  = true;
                if ((ii||i)&&(oo||o)) return SplitResult.GENUINE_CUT;
                if (c && (ii||i))     return SplitResult.TOUCHING_INSIDE;
                if (c && (oo||o))     return SplitResult.TOUCHING_OUTSIDE;
            }
            if (c) return SplitResult.CONTAINED;
            if (ii||i) return SplitResult.GENUINE_INSIDE;
            if (oo||o)  return SplitResult.GENUINE_OUTSIDE;
            throw new Exception($"Impossible case dim={Dim()}, {facets.Count} facets, i{i},ii{ii},oo{oo},o{o},c{c}");
        }
        public void SetNeighbors() {
            var pool = new HashSet<IPolyhedron>(facets);
            foreach (var facet in facets) {
                facet.parent = this;
                pool.Remove(facet);
                facet.SetNeighbors();
                foreach (var subFacet in facet.facets) {
                    foreach (var facet2 in pool) {
                        foreach (var subFacet2 in facet2.facets) {
                            if (subFacet.Equals(subFacet2)) {
                                subFacet.neighbor = subFacet2;
                                subFacet2.neighbor = subFacet;
                            }
                        }
                    }
                }
            }
        }

    }
    public class Polyhedron : IPolyhedron {
        /*
        For a polyhedron each subfacets is contained in exactly two facets.
        */
        public virtual HashSet<IPolyhedron> facets {get; protected set; }
        /* connecting two parent-faces of different polyhedrons, like in the case of a split */
        public bool isInvisible {get; set;}
        public IPolyhedron parent {get; set;}
        public IPolyhedron neighbor {get;set;}

        protected Polyhedron(bool isInvisible) {
            this.isInvisible = isInvisible;
        }
        public Polyhedron(HashSet<IPolyhedron> _facets, bool isInvisible) {
            this.isInvisible = isInvisible;
            facets = _facets;
            ((IPolyhedron)this).SetNeighbors();
        }
        public Polyhedron(IntegerCell ic, bool isInvisible) :
                this(ic.Facets().Select(facet => new Polyhedron(facet,isInvisible)).Cast<IPolyhedron>().ToHashSet(),isInvisible) {
        }
        public virtual IPolyhedron Recreate(HashSet<IPolyhedron> _facets)
        {
            return new Polyhedron(_facets,isInvisible) { parent = parent };
        }
        public virtual IPolyhedron OpposingClone() {
            //we have no orientation, so opposingClone is just a copy
            var res = new Polyhedron(facets,isInvisible) { parent = parent };
            res.neighbor = this;
            neighbor = res;
            return res;
        }
        /* dimension of the polyhedron (not the dimension of the enclosing space) */
        public int Dim() {
            foreach (var facet in facets) {
                return facet.Dim() + 1;
            }
            return 0;
        }
        /* checks without performing  full split whether this polyhedron is completely on one side of the cut Plane
           returns which side that would be
           
        */
        public virtual SplitResult Split(HalfSpace cutPlane) {
            HashSet<IPolyhedron> inners = new();
            HashSet<IPolyhedron> outers = new();
            HashSet<IPolyhedron> innerCuts = new();
            HashSet<IPolyhedron> outerCuts = new();
            IPolyhedron containedFacet = null;
            IPolyhedron someFacet = null; //for deriving cut type
            foreach (var facet in facets) {
                if (someFacet==null) someFacet=facet;
                SplitResult cutResult = facet.Split(cutPlane);
                if (cutResult.inner!=null ) {
                    inners.Add(cutResult.inner);
                    if (containedFacet!=null) break;
                }
                if (cutResult.outer!=null) {
                    outers.Add(cutResult.outer);
                    if (containedFacet!=null) break;
                }
                if (cutResult.isContained) {
                    //cutResult[1] is a facet and is contained in the cutPlane
                    //this implies that cut takes place on the facet of the polyhedron, hence it is not split
                    containedFacet = facet;
                } else if (cutResult.innerCut!=null || cutResult.outerCut != null) {
                    //cutResult.innerCut is a subFacet
                    if (cutResult.innerCut!=null) {
                        //a cut could come from two inner facets, meaning nothing was really cut
                        //so we remove such cuts
                        if (innerCuts.Contains(cutResult.innerCut)) innerCuts.Remove(cutResult.innerCut);
                        else                                        innerCuts.Add(cutResult.innerCut);
                    }
                    if (cutResult.outerCut!=null) {
                        if (outerCuts.Contains(cutResult.outerCut)) outerCuts.Remove(cutResult.outerCut);
                        else                                        outerCuts.Add(cutResult.outerCut);
                    }
                } else {
                    //here are the cases that cutResult[0]==null && cutResult[1]!=null && cutResult[2]!=null
                    //                     or cutResult[0]!=null && cutResult[1]!=null && cutResult[2]==null
                    //in both cases nothing needs to be done
                }
            }
            SplitResult res = new();
            if (inners.Count==0 && outers.Count==0) {
                res.isContained = true; //take this as abbreviation, for in the next steps the polyhedron would be recreated
                return res;
            }
            if (containedFacet!=null) {
                if (inners.Count>0) {
                    res.inner = this;
                    res.innerCut = containedFacet;
                }
                else if (outers.Count>0) {
                    res.outer = this;
                    res.outerCut = containedFacet;
                }
                else throw new Exception();
                return res;             
            }
            if (innerCuts.Count>0) {
                Debug.Assert(inners.Count>0,"6419165487");
                Debug.Assert(innerCuts.SetEquals(outerCuts),"2396973648");
                HashSet<IPolyhedron> innerCutsOpposing = new();
                HashSet<IPolyhedron> outerCutsOpposing = new();
                foreach (var innerCut in innerCuts) {
                    var i1 = innerCut;
                    var o1 = innerCut.neighbor;
                    var i2 = i1.OpposingClone();
                    var o2 = o1.OpposingClone();
                    innerCutsOpposing.Add(i2);
                    outerCutsOpposing.Add(o2);
                }
                res.innerCut = someFacet.Recreate(innerCutsOpposing);
                res.outerCut = someFacet.Recreate(outerCutsOpposing);
                inners.Add(res.innerCut);
                outers.Add(res.outerCut);
            }
            if (inners.Count>0) { 
                res.inner = Recreate(inners);
            }
            if (outers.Count>0) { 
                res.outer = Recreate(outers);
            }
            res.CrossReference(this,cutPlane);
            return res;
        }
        /* returns the neighbor of facet1 wrt subFacet, facet needs to be dim-1 dimensional, subFacet dim-2 */
        public IPolyhedron Neighbor(IPolyhedron facet1, IPolyhedron subFacet) {
            foreach (var facet in facets) {
                if (!facet.Equals(subFacet)) {
                    if (facet.facets.Contains(subFacet)) {
                        return facet;
                    }
                }
            }
            throw new Exception();
        }
        public override bool Equals(object obj)
        {
            if (!typeof(Polyhedron).IsAssignableFrom(obj.GetType())) { return false; };
            Polyhedron p2 = (Polyhedron) obj;
            if (Dim()!=p2.Dim()) {
                return false; //this should not occur
            }
            return facets.SetEquals(p2.facets);
        }
        public override int GetHashCode()
        {   
            var res = 0;
            foreach (var facet in facets) {
                res += facet.GetHashCode();
            }
            return res;
        }
        public override string ToString()
        {
            var res = "[";
            foreach (var facet in facets) {
                res += " ";
                res += facet.ToString();
            }
            res += "]";
            return res;
        }
        public void Move3d(Point d) {
            foreach (var facet in facets) {
                foreach (var edge in facet.facets) {
                    foreach (var vertex_ in edge.facets) {
                        var vertex = (Vertex)vertex_;
                        vertex.PointRef().add(d);
                    }
                }
            }
        }

        public void Replace(IPolyhedron ab, IPolyhedron inner, IPolyhedron outer)
        {
            facets.Remove(ab);
            facets.Add(inner);
            facets.Add(outer);
        }
    }
}

