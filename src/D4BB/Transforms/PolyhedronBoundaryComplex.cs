using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using D4BB.Comb;
using D4BB.Geometry;
namespace D4BB.Transforms
{

public class VertexBC : Vertex
{
    public VertexBC(IntegerCell ic) : base(new Point(ic.origin))
    {
        Debug.Assert(ic.Dim()==0,"2852144565");
    }
}
public class EdgeBC : Edge
{
    protected EdgeBC(Vertex a, Vertex b, bool isInvisible = false) : base(a, b, isInvisible)    {}
    public EdgeBC(OrientedIntegerCell ic, ICamera4d cam=null) : base(
            cam == null ? new Point(ic.EdgeA().origin) : cam.Proj3d(new Point4d(ic.EdgeA().origin)),
            cam == null ? new Point(ic.EdgeB().origin) : cam.Proj3d(new Point4d(ic.EdgeB().origin))
        ) {
        Debug.Assert(ic.Dim()==1,"1163775061");
    }
    public override IPolyhedron Recreate(Vertex a, Vertex b) {
        return new EdgeBC(a,b,isInvisible) { parent = parent, neighbor=neighbor };
    }
}
public class Face2dBC : Face2dWithIntegerCellAttribute {
    /* this class contains some helper attributes for bringing the data on the screen */
    public Dictionary<IntegerCell,EdgeBC> i2p = new();
    public ICamera4d camera; //clockwise from outside original IntegerCell corners
    public Polyhedron3dBoundaryComplex pbc;
    public Face2dBC(HashSet<IPolyhedron> facets, bool connecting_, IntegerCell _integerCell) : base(facets,connecting_,_integerCell) {}
    public Face2dBC(List<Edge> edges, bool connecting_, IntegerCell _integerCell) : base(edges,connecting_,_integerCell) {}
    public Face2dBC(List<Point> points, bool invisible, IntegerCell integerCell) : base(points,invisible,integerCell) { this.integerCell=integerCell;}
    public Face2dBC(Point a, Point b, Point c, bool connecting_, IntegerCell _integerCell) : 
            base(new List<Point>(){a,b,c}, connecting_,_integerCell) {}
    public Face2dBC(OrientedIntegerCell ic, ICamera4d cam=null) :
            base(ic.ClockwiseFromOutsideEdges2d().Select(e => new EdgeBC(e, cam)).Cast<Edge>().ToList(), false, ic) {
        Debug.Assert(ic.Dim()==2,"7065983586");
        camera = cam;
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
        var res = new Face2dBC(_facets, isInvisible, integerCell)  { parent = parent, neighbor=neighbor, camera = camera, pbc=pbc };
        return res;
    }
    public override Face2d Recreate(List<Point> points)
    {
        var res =  new Face2dBC(points,isInvisible, integerCell) { parent = parent, neighbor=neighbor, camera = camera, pbc=pbc  };
        return res;
    }
    public override Face2d Recreate(List<Edge> edges)
    {
        var res =  new Face2dBC(edges,isInvisible, integerCell) { parent = parent, neighbor=neighbor, camera = camera, pbc=pbc  };
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
            return new Face2dBC(points,false, ic);
        }
        HashSet<IPolyhedron> resFacets = new() ;
        var ibc = new IntegerBoundaryComplex(ic);
        var facets = new HashSet<Polyhedron>();
        foreach (var cell in ibc.cells) {
            resFacets.Add(FromIntegerCell(cell));
        }
        var res =  new Polyhedron(resFacets, false); //we need the extra information only for 2d facet
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
    public List<Face2dBC> facets = new();
    public Dictionary<IntegerCell,Face2dBC> i2p = new();
    // public List<EdgeBC> visibleEdges = new();
    // public List<VertexBC> visibleVertices = new();
    bool showInvisibleEdges;

    public Polyhedron3dBoundaryComplex(IntegerBoundaryComplex ibc, ICamera4d cam=null,bool showInvisibleEdges=false) {
        this.showInvisibleEdges = showInvisibleEdges;
        foreach (var ic in ibc.cells) {
            var pc = new Face2dBC(ic, cam) { pbc = this};
            i2p[ic] = pc;
            facets.Add(pc);
        }
        var visibleIEdges = ibc.PrunedSkeletonCellsOfDim(1);
        foreach (var ic1 in ibc.cells) {
            var pc = i2p[ic1];
            foreach (var iEdge in ic1.Facets()) {
                var ic2 = ibc.neighborOfVia[ic1][iEdge];
                var pEdge1 = i2p[ic1].i2p[iEdge];
                var pEdge2 = i2p[ic2].i2p[iEdge];
                Debug.Assert(pEdge2!=null, "5395413579");
                pEdge1.neighbor = pEdge2;
                pEdge1.parent = pc;
                var visible = visibleIEdges.Contains(iEdge);
                pEdge1.isInvisible = !visible;
                //pEdge2.isInvisible = !visible;
            }
        }
    }
    public int Dim() {
        foreach (var facet in facets) {
            return facet.Dim();
        }
        throw new Exception();
    }
    public static void Split(HalfSpace halfSpace,List<Face2dBC> facets, List<Face2dBC> out_inner, List<Face2dBC> out_outer) {
        foreach (var facet in facets) {
            var split = facet.Split(halfSpace);
            if (split.inner!=null)
                out_inner.Add((Face2dBC)split.inner);
            if (split.outer!=null)
                out_outer.Add((Face2dBC)split.outer);
            if (split.isContained) {
//                if (AOP.gt(facet.Normal().sc(halfSpace.normal),0)) {
                    out_inner.Add(facet);
//                } else {
//                    out_outer.Add(facet);
//                }
            }
        }
    }
    public void CutOut(HalfSpace[] halfSpaces) {
        List<Face2dBC> noSplit = new();

        List<Face2dBC> innerFacets1 = new();
        foreach (var facet in facets) {
            bool outSideOneHalfSpace = false;
            foreach (var halfSpace in halfSpaces) {
                var side = ((IPolyhedron)facet).Side(halfSpace);
                if (side==SplitResult.GENUINE_OUTSIDE || side==SplitResult.TOUCHING_OUTSIDE) {
                    outSideOneHalfSpace = true;
                    break;
                }
            }
            if (!outSideOneHalfSpace) innerFacets1.Add(facet);
            else noSplit.Add(facet);
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
        facets = outerFacets;
    }
    public void CutOut(IPolyhedron polyhedron) {
        Debug.Assert(polyhedron.Dim()==polyhedron.SpaceDim(),"6715569833");
        CutOut(polyhedron.HalfSpaces().Values.ToArray());
    }
    public List<Face2dBC> VisibleFacets() {
        return facets;
    }
    public HashSet<EdgeBC> VisibleEdges() {
        HashSet<EdgeBC> res = new();
        foreach (var facet in facets) {
            foreach (var edge in facet.facets) {
                if (showInvisibleEdges || !edge.isInvisible || edge.neighbor==null) {
                    res.Add((EdgeBC)edge);
                }
            }
        }
        return res;
    }
    public void Replace(Face2dBC ab,Face2dBC a,Face2dBC b) {
        int index = facets.IndexOf(ab);
        if (index==-1) throw new Exception($"Replacing non-existing value {ab}");
        facets[index]=b;
        facets.Insert(index,a);
    }
}
}