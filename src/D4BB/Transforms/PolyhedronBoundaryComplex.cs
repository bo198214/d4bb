using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using D4BB.Comb;
using D4BB.Geometry;
namespace D4BB.Transforms
{

public class VertexBC : Vertex
{
    public VertexBC(IntegerCell ic) : base(new Point(ic.origin))
    {
        Debug.Assert(ic.Dim()==0,"2852144565");
        mark = IPolyhedron.MARK_GRID_DIVISION;
    }
}
public class EdgeBC : Edge
{
    // The 2-face this edge bounds (set in Polyhedron3dBoundaryComplex.BoundaryEdges from the
    // owning Face2dBC). Lets consumers attribute an edge to its elementary tesseract exactly
    // like faces, instead of re-deriving it geometrically at render time.
    public IntegerCell integerCell;
    protected EdgeBC(Vertex a, Vertex b, bool isCoplanarInterior = false) : base(a, b, isCoplanarInterior)    {}
    public EdgeBC(OrientedIntegerCell ic, ICamera4d cam=null) : base(
            cam == null ? new Point(ic.EdgeA().origin) : cam.Proj3d(new Point4d(ic.EdgeA().origin)),
            cam == null ? new Point(ic.EdgeB().origin) : cam.Proj3d(new Point4d(ic.EdgeB().origin))
        ) {
        Debug.Assert(ic.Dim()==1,"1163775061");
        a.pos4d = ic.EdgeA().origin.Select(v => (double)v).ToArray();
        b.pos4d = ic.EdgeB().origin.Select(v => (double)v).ToArray();
        // Mark this edge and its endpoints as originating from the IntegerComplex grid,
        // so the showGridDivisions filter can route them through the grid-specific
        // toggle rather than the BSP-cut toggle (showIntraCoplanarEdges).
        mark = IPolyhedron.MARK_GRID_DIVISION;
        a.mark = IPolyhedron.MARK_GRID_DIVISION;
        b.mark = IPolyhedron.MARK_GRID_DIVISION;
    }
    public override IPolyhedron Recreate(Vertex a, Vertex b) {
        return new EdgeBC(a,b,isCoplanarInterior) { parent = parent, neighbor=neighbor, mark=mark, integerCell=integerCell };
    }
}
public class Face2dBC : Face2dWithIntegerCellAttribute {
    /* this class contains some helper attributes for bringing the data on the screen */
    public Dictionary<IntegerCell,EdgeBC> i2p = new();
    public ICamera4d camera; //clockwise from outside original IntegerCell corners
    public Polyhedron3dBoundaryComplex pbc;
    public Face2dBC(HashSet<IPolyhedron> facets, bool connecting_, IntegerCell _integerCell) : base(facets,connecting_,_integerCell) {}
    public Face2dBC(List<Edge> edges, bool connecting_, IntegerCell _integerCell) : base(edges,connecting_,_integerCell) {}
    public Face2dBC(List<Point> points, bool isConnecting, IntegerCell integerCell) : base(points,isConnecting,integerCell) { this.integerCell=integerCell;}
    public Face2dBC(Point a, Point b, Point c, bool connecting_, IntegerCell _integerCell) : 
            base(new List<Point>(){a,b,c}, connecting_,_integerCell) {}
    public Face2dBC(OrientedIntegerCell ic, ICamera4d cam=null) :
            base(ic.ClockwiseFromOutsideEdges2d().Select(e => new EdgeBC(e, cam)).Cast<Edge>().ToList(), false, ic) {
        Debug.Assert(ic.Dim()==2,"7065983586");
        camera = cam;
        mark = IPolyhedron.MARK_GRID_DIVISION;
        int i=0;
        foreach (var iEdge in ic.ClockwiseFromOutsideEdges2d()) {
            i2p[iEdge] = (EdgeBC)edges[i];
            i++;
        }
        // var spanList = ic.span.ToList();
        // var io = new Point4d(IntegerOps.Clone(ic.origin));
        // var iu = new Point4d(IntegerOps.Clone(ic.origin));
        // iu.x[spanList[0]]+=1;
        // var iv = new Point4d(IntegerOps.Clone(ic.origin));
        // iv.x[spanList[1]]+=1;
        // var o = cam.Proj3d(io);
        // var u = cam.Proj3d(iu).subtract(o);
        // var v = cam.Proj3d(iv).subtract(o);
        // foreach (var edge in edges) {
        //     foreach (var c in new Point[]{edge.a.getPoint(),edge.b.getPoint()}) {
        //         var pv = c.subtract(o);
        //         var ps = AOP.Params(u.x,v.x,pv.x);
        //         Debug.Assert(AOP.eq(ps[0],0) || AOP.eq(ps[0],1), c.toString() + " -> " + ps[0].ToString() + " " + ps[1].ToString());
        //         Debug.Assert(AOP.eq(ps[1],0) || AOP.eq(ps[1],1), c.toString() + " -> " + ps[0].ToString() + " " + ps[1].ToString());
        //     }
        // }
    }
    public override IPolyhedron Recreate(HashSet<IPolyhedron> _facets)
    {
        var res = new Face2dBC(_facets, isCoplanarInterior, integerCell)  { parent = parent, neighbor=neighbor, camera = camera, pbc=pbc, mark=mark };
        return res;
    }
    public override Face2d Recreate(List<Point> points)
    {
        var res =  new Face2dBC(points,isCoplanarInterior, integerCell) { parent = parent, neighbor=neighbor, camera = camera, pbc=pbc, mark=mark };
        return res;
    }
    public override Face2d Recreate(List<Edge> edges)
    {
        var res =  new Face2dBC(edges,isCoplanarInterior, integerCell) { parent = parent, neighbor=neighbor, camera = camera, pbc=pbc, mark=mark };
        return res;
    }
    public static IPolyhedron FromIntegerCell(int[] origin) {
        IntegerCell ic = new IntegerCell(origin);
        return FromIntegerCell(ic);
    }
    public static IPolyhedron FromIntegerCell(int[] origin, HashSet<int> span=null) {
        IntegerCell ic = new IntegerCell(origin,span);
        return FromIntegerCell(ic);
    }
    public static IPolyhedron FromIntegerCell(int[] origin, HashSet<int> span, bool inverted, bool parity) {
        IntegerCell ic = new OrientedIntegerCell(origin,span,inverted,parity);
        return FromIntegerCell(ic);
    }
    public static IPolyhedron FromIntegerCell(int[] origin, bool inverted, bool parity) {
        var ic = new OrientedIntegerCell(origin,IntegerCell.FullSpan(origin.Length),inverted, parity);
        return FromIntegerCell(ic);
    }
    public static IPolyhedron FromIntegerCell(IntegerCell ic) {
        // if (ic.Dim()==0) return Vertex.NewVertex(new Point(ic.origin), false);
        // if (ic.Dim()==1) return Edge.NewEdge(resFacets, false);
        if (ic.Dim()==2) {
            int[][] vertices = ((OrientedIntegerCell)ic).ClockwiseFromOutsideVertices2d();
            List<Point> points = new List<Point>();
            foreach (var vi in vertices) {
                points.Add(new Point(vi));
            }
            var face = new Face2dBC(points,false, ic) { mark = IPolyhedron.MARK_GRID_DIVISION };
            // The points-based Face2dBC ctor builds plain Edges/Vertices via Points2Edges,
            // which don't go through EdgeBC.ctor — stamp the inner edges and vertices here
            // so the whole face tree is consistently marked as grid-intersection.
            foreach (var e in face.edges) {
                e.mark = IPolyhedron.MARK_GRID_DIVISION;
                e.a.mark = IPolyhedron.MARK_GRID_DIVISION;
                e.b.mark = IPolyhedron.MARK_GRID_DIVISION;
            }
            return face;
        }
        HashSet<IPolyhedron> resFacets = new() ;
        var ibc = new IntegerBoundaryComplex(ic);
        var facets = new HashSet<Polyhedron>();
        foreach (var cell in ibc.cells) {
            resFacets.Add(FromIntegerCell(cell));
        }
        var res =  new Polyhedron(resFacets, false) { mark = IPolyhedron.MARK_GRID_DIVISION }; //we need the extra information only for 2d facet
        // if (center==null) center = res.CenterPoint();
        // if (ic.Dim()==3) {
        //     foreach (var facet in resFacets) {
        //         ((Mesh3dFacet)facet).MakeCounterClockwise(center);
        //         ((Mesh3dFacet)facet).cubeCenter = center;
        //     }
        // }
        return res;
    }
    public new SplitResult Split(HalfSpace cutPlane) {
        var sr = ((IPolyhedron)this).Split(cutPlane);
        if (sr.inner==null || sr.outer==null || neighbor==null) return sr;

        var pbc = ((Face2dBC)neighbor).pbc;
        pbc.Replace((Face2dBC)neighbor,(Face2dBC)sr.neighborSplitInner,(Face2dBC)sr.neighborSplitOuter);
        return sr;
    }
}

public class Polyhedron3dBoundaryComplex {
    sealed class ByRef : IEqualityComparer<Face2dBC> {
        internal static readonly ByRef I = new();
        public bool Equals(Face2dBC x, Face2dBC y) => ReferenceEquals(x, y);
        public int GetHashCode(Face2dBC f) => RuntimeHelpers.GetHashCode(f);
    }
    public HashSet<Face2dBC> d2faces = new(ByRef.I);
    public Dictionary<IntegerCell,Face2dBC> i2p = new(); // maps 2d integer cells to their corresponding Face2dBC, for quick access when building the complex. Does not consider cut faces.
    // public List<EdgeBC> visibleEdges = new();
    // public List<VertexBC> visibleVertices = new();
    bool showIntraCoplanarEdges;
    bool showGridDivisions;

    public List<CellBoundary> cellBoundaries;
    internal Polyhedron3dBoundaryComplex(List<Face2dBC> prebuiltFaces, bool showIntraCoplanarEdges, bool showGridDivisions = true) {
        this.showIntraCoplanarEdges = showIntraCoplanarEdges;
        this.showGridDivisions = showGridDivisions;
        d2faces = new HashSet<Face2dBC>(prebuiltFaces, ByRef.I);
        foreach (var face in d2faces)
            i2p[face.integerCell] = face;
    }
    public Polyhedron3dBoundaryComplex(HashSet<OrientedIntegerCell> cells3, ICamera4d cam=null, bool showIntraCoplanarEdges=false, bool showGridDivisions=true)
            : this(new IntegerBoundaryComplex(cells3), cam, showIntraCoplanarEdges, showGridDivisions) {
        cellBoundaries = new List<CellBoundary>();
        foreach (var c3 in cells3) {
            var cellFaces = new List<Face2dBC>(); //collecting the projected 2d faces from this PBC that belong to c3
            foreach (var c2 in c3.Facets())
                if (i2p.TryGetValue(c2, out var face))
                    cellFaces.Add(face);
            cellBoundaries.Add(new CellBoundary(c3,
                new Polyhedron3dBoundaryComplex(cellFaces, showIntraCoplanarEdges, showGridDivisions)));
        }
    }
    public Polyhedron3dBoundaryComplex(int[] origin, ICamera4d cam=null, bool showIntraCoplanarEdges=false, bool showGridDivisions=true)
            : this(new IntegerBoundaryComplex(origin), cam, showIntraCoplanarEdges, showGridDivisions) {}
    public Polyhedron3dBoundaryComplex(int[][] origins, ICamera4d cam=null, bool showIntraCoplanarEdges=false, bool showGridDivisions=true)
            : this(new IntegerBoundaryComplex(origins), cam, showIntraCoplanarEdges, showGridDivisions) {}
    public Polyhedron3dBoundaryComplex(IntegerBoundaryComplex i3bc, ICamera4d cam=null, bool showIntraCoplanarEdges=false, bool showGridDivisions=true) {
        this.showIntraCoplanarEdges = showIntraCoplanarEdges;
        this.showGridDivisions = showGridDivisions;
        foreach (var i2c in i3bc.cells) {
            var pc = new Face2dBC(i2c, cam) { pbc = this};
            i2p[i2c] = pc;
            d2faces.Add(pc);
        }
        var visibleIEdges = i3bc.PrunedSkeletonCellsOfDim(1);
        foreach (var ic1 in i3bc.cells) {
            var pc = i2p[ic1];
            foreach (var iEdge in ic1.Facets()) {
                var ic2 = i3bc.neighborOfVia[ic1][iEdge];
                var pEdge1 = i2p[ic1].i2p[iEdge];
                var pEdge2 = i2p[ic2].i2p[iEdge];
                Debug.Assert(pEdge2!=null, "5395413579");
                pEdge1.neighbor = pEdge2;
                pEdge1.parent = pc;
                var visible = visibleIEdges.Contains(iEdge);
                pEdge1.isCoplanarInterior = !visible;
                //pEdge2.isCoplanarInterior = !visible;
            }
        }
    }
    public int Dim() {
        foreach (var facet in d2faces) {
            return facet.Dim();
        }
        throw new Exception();
    }
    // Routing for boundary-coincident faces (split.isContained): compare the face's outward
    // normal with the cutting halfspace's outward normal.
    //   Same direction (co-oriented) ⇒ face is on the cutter's visible-front surface and is
    //     drawn by the cutter itself; occluded here ⇒ inner.
    //   Opposite direction (counter-oriented) ⇒ face's visible side faces away from the
    //     cutter (cutter sits behind it from the camera POV) ⇒ outer (preserved).
    public static void Split(HalfSpace halfSpace, IEnumerable<Face2dBC> facets, List<Face2dBC> out_inner, List<Face2dBC> out_outer) {
        foreach (var facet in facets) {
            var split = facet.Split(halfSpace);
            if (split.inner!=null)
                out_inner.Add((Face2dBC)split.inner);
            if (split.outer!=null)
                out_outer.Add((Face2dBC)split.outer);
            if (split.isContained) {
                if (AOP.gt(facet.Normal().sc(halfSpace.normal), 0))
                    out_inner.Add(facet);   // co-oriented with cutter ⇒ occluded
                else
                    out_outer.Add(facet);   // counter-oriented ⇒ this is the visible boundary
            }
        }
    }
    static bool FaceIntersectsPolyhedron(Face2dBC facet, HalfSpace[] halfSpaces) {
        List<Point> pts = facet.points;
        foreach (var hs in halfSpaces) {
            pts = ClipConvexPolygon(pts, hs);
            if (pts.Count < 3) return false;
        }
        return true;
    }
    static List<Point> ClipConvexPolygon(List<Point> polygon, HalfSpace hs) {
        var result = new List<Point>(polygon.Count + 1);
        int n = polygon.Count;
        for (int i = 0; i < n; i++) {
            Point cur = polygon[i];
            Point nxt = polygon[(i + 1) % n];
            int cs = hs.side(cur);
            int ns = hs.side(nxt);
            if (cs <= 0) result.Add(cur);
            if ((cs < 0 && ns > 0) || (cs > 0 && ns < 0))
                result.Add(hs.cutPoint(cur, nxt));
        }
        return result;
    }
    // Phase 1: skip faces whose bounding box doesn't intersect the cut region (noSplit).
    // Phase 2: iteratively split the remaining candidates against each half-space, collecting
    //          the outer fragments. The surviving innerFacets1 are the faces being removed.
    // Finally, sever the neighbor links of removed faces so adjacent faces know they are now
    // on the boundary.
    // Faces lying exactly on a cutter's halfspace plane are routed by orientation — see Split().
    public void CutOut(HalfSpace[] halfSpaces) {
        List<Face2dBC> noSplit = new();

        List<Face2dBC> innerFacets1 = new();
        foreach (var facet in d2faces) {
            if (FaceIntersectsPolyhedron(facet, halfSpaces))
                innerFacets1.Add(facet);
            else
                noSplit.Add(facet);
        }
        List<Face2dBC> outerFacets = new();
        List<Face2dBC> innerFacets2 = new();
        foreach (var halfSpace in halfSpaces) {
            Split(halfSpace,innerFacets1,innerFacets2,outerFacets);
            innerFacets1=innerFacets2;
            innerFacets2 = new();
        }
        foreach (var facet in innerFacets1) {
            foreach (var edge in facet.facets) {
                if (edge.neighbor!=null) edge.neighbor.neighbor = null;
            }
        }
        outerFacets.AddRange(noSplit);
        d2faces = new HashSet<Face2dBC>(outerFacets, ByRef.I);
    }
    public void CutOut(IPolyhedron polyhedron) {
        Debug.Assert(polyhedron.Dim()==polyhedron.SpaceDim(),"6715569833");
        CutOut(polyhedron.HalfSpaces().Values.ToArray());
    }
    // Severing neighbor links is required so the edges on the now-exposed boundary
    // are no longer considered interior (isCoplanarInterior edges with neighbor==null
    // are rendered as boundary edges).
    public void RemoveFace(Face2dBC facet) {
        if (!d2faces.Remove(facet)) return;
        foreach (IPolyhedron edge in facet.facets)
            if (edge.neighbor != null) edge.neighbor.neighbor = null;
    }
    public ICollection<Face2dBC> BoundaryFacets() {
        if (cellBoundaries != null) {
            var result = new List<Face2dBC>();
            foreach (var cb in cellBoundaries) result.AddRange(cb.pbc.d2faces);
            return result;
        }
        return d2faces;
    }
    public HashSet<EdgeBC> BoundaryEdges() {
        HashSet<EdgeBC> res = new();
        var faces = cellBoundaries != null
            ? cellBoundaries.SelectMany(cb => cb.pbc.d2faces)
            : (IEnumerable<Face2dBC>)d2faces;
        foreach (var facet in faces) {
            foreach (var edge in facet.facets) {
                // isCoplanarInterior=true edges come from two distinct sources:
                //   1. The IntegerComplex (grid-intersection lines between coplanar
                //      neighbor cells of the same compound piece) — mark=GRID_INTERSECTION.
                //   2. BSP-cut faces created in SplitResult.CrossReference — mark=NONE.
                // The `mark` field routes each isCoplanarInterior edge to its own toggle.
                bool isInterior = edge.isCoplanarInterior && edge.neighbor != null;
                bool show = !isInterior
                    || (edge.mark == IPolyhedron.MARK_GRID_DIVISION
                        ? showGridDivisions
                        : showIntraCoplanarEdges);
                if (show) {
                    var ebc = (EdgeBC)edge;
                    // Attribute the edge to its owning 2-face so consumers can resolve the
                    // elementary tesseract (and thus depth) the same way faces do. Shared edges
                    // get whichever face is visited last — fine, since OwningMinDepth on either
                    // 2-face resolves to the same tesseract for non-grid edges.
                    ebc.integerCell = facet.integerCell;
                    res.Add(ebc);
                }
            }
        }
        return res;
    }
    public void Replace(Face2dBC ab, Face2dBC a, Face2dBC b) {
        if (!d2faces.Remove(ab)) throw new Exception($"Replacing non-existing value {ab}");
        d2faces.Add(a);
        d2faces.Add(b);
    }
}
}