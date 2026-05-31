using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using D4BB.Comb;
using D4BB.Geometry;

namespace D4BB.Transforms
{

public class Face2dIC : Face2dWithIntegerCellAttribute
{
    public Dictionary<IntegerCell, Edge> i2p = new();
    public ICamera3d camera;
    public Polyhedron2dBoundaryComplex pbc;

    protected Face2dIC(List<Edge> edges, bool isConnecting, IntegerCell integerCell)
        : base(edges, isConnecting, integerCell) {}

    protected Face2dIC(HashSet<IPolyhedron> facets, bool isConnecting, IntegerCell integerCell)
        : base(facets, isConnecting, integerCell) {}

    protected Face2dIC(List<Point> points, bool isConnecting, IntegerCell integerCell)
        : base(points, isConnecting, integerCell) {}

    public Face2dIC(OrientedIntegerCell ic, ICamera3d cam)
        : base(
            ic.ClockwiseFromOutsideEdges2d()
               .Select(e => EdgeFromCell(e, cam))
               .ToList(),
            false, ic)
    {
        Debug.Assert(ic.Dim() == 2, "Face2dIC requires a 2-cell");
        camera = cam;
        mark = IPolyhedron.MARK_GRID_DIVISION;
        var iEdges = ic.ClockwiseFromOutsideEdges2d();
        for (int k = 0; k < iEdges.Length; k++)
            i2p[iEdges[k]] = edges[k];
    }

    private static Edge EdgeFromCell(OrientedIntegerCell e1, ICamera3d cam) {
        var aOrig = e1.EdgeA().origin.Select(v => (double)v).ToArray();
        var bOrig = e1.EdgeB().origin.Select(v => (double)v).ToArray();
        var pa = cam?.Proj2d(new Point(aOrig)) ?? new Point(aOrig);
        var pb = cam?.Proj2d(new Point(bOrig)) ?? new Point(bOrig);
        var va = new Vertex(pa) { mark = IPolyhedron.MARK_GRID_DIVISION };
        var vb = new Vertex(pb) { mark = IPolyhedron.MARK_GRID_DIVISION };
        va.pos4d = aOrig; // original 3D position — reused for animation
        vb.pos4d = bOrig;
        return new Edge(va, vb) { mark = IPolyhedron.MARK_GRID_DIVISION };
    }

    public override IPolyhedron Recreate(HashSet<IPolyhedron> facets) =>
        new Face2dIC(facets, isCoplanarInterior, integerCell) { parent = parent, neighbor = neighbor, camera = camera, pbc = pbc, mark = mark };

    public override Face2d Recreate(List<Edge> edges) =>
        new Face2dIC(edges, isCoplanarInterior, integerCell) { parent = parent, neighbor = neighbor, camera = camera, pbc = pbc, mark = mark };

    public override Face2d Recreate(List<Point> points) =>
        new Face2dIC(points, isCoplanarInterior, integerCell) { parent = parent, neighbor = neighbor, camera = camera, pbc = pbc, mark = mark };
}

public class CellBoundary2d
{
    public readonly OrientedIntegerCell cell;
    public readonly Polyhedron2dBoundaryComplex pbc;

    public CellBoundary2d(OrientedIntegerCell cell, Polyhedron2dBoundaryComplex pbc) {
        this.cell = cell;
        this.pbc = pbc;
    }
}

public class Polyhedron2dBoundaryComplex
{
    public List<Face2dIC> d2faces = new();
    public Dictionary<IntegerCell, Face2dIC> i2p = new();
    public List<CellBoundary2d> cellBoundaries;
    bool showIntraCoplanarEdges;
    bool showGridDivisions;

    internal Polyhedron2dBoundaryComplex(List<Face2dIC> prebuiltFaces, bool showIntraCoplanarEdges, bool showGridDivisions) {
        this.showIntraCoplanarEdges = showIntraCoplanarEdges;
        this.showGridDivisions = showGridDivisions;
        d2faces = prebuiltFaces;
        foreach (var face in d2faces)
            i2p[face.integerCell] = face;
    }

    public Polyhedron2dBoundaryComplex(HashSet<OrientedIntegerCell> cells2, ICamera3d cam = null, bool showIntraCoplanarEdges = false, bool showGridDivisions = true) {
        this.showIntraCoplanarEdges = showIntraCoplanarEdges;
        this.showGridDivisions = showGridDivisions;

        foreach (var ic2 in cells2) {
            var face = new Face2dIC(ic2, cam) { pbc = this };
            i2p[ic2] = face;
            d2faces.Add(face);
        }

        // Build map: 1-cell → which 2-cells contain it
        var edge2cells = new Dictionary<IntegerCell, List<OrientedIntegerCell>>();
        foreach (var ic2 in cells2) {
            foreach (var ic1 in ic2.Facets()) {
                if (!edge2cells.TryGetValue(ic1, out var list)) {
                    list = new List<OrientedIntegerCell>();
                    edge2cells[ic1] = list;
                }
                list.Add(ic2);
            }
        }

        // Link shared edges and mark inner ones as invisible
        foreach (var ic2 in cells2) {
            var face = i2p[ic2];
            foreach (var ic1 in ic2.Facets()) {
                var siblings = edge2cells[ic1];
                if (siblings.Count == 2) {
                    var other = siblings.First(f => !f.Equals(ic2));
                    var myEdge    = face.i2p[ic1];
                    var theirEdge = i2p[other].i2p[ic1];
                    myEdge.neighbor    = theirEdge;
                    myEdge.isCoplanarInterior = true;
                }
                // outer boundary edges keep neighbor=null, isCoplanarInterior=false
            }
        }

        // Prepare cellBoundaries for future BSP-based occlusion
        cellBoundaries = new List<CellBoundary2d>();
        foreach (var ic2 in cells2)
            cellBoundaries.Add(new CellBoundary2d(ic2,
                new Polyhedron2dBoundaryComplex(new List<Face2dIC> { i2p[ic2] }, showIntraCoplanarEdges, showGridDivisions)));
    }

    public List<Face2dIC> BoundaryFacets() {
        if (cellBoundaries != null) {
            var result = new List<Face2dIC>();
            foreach (var cb in cellBoundaries) result.AddRange(cb.pbc.d2faces);
            return result;
        }
        return d2faces;
    }

    public HashSet<Edge> BoundaryEdges() {
        var res = new HashSet<Edge>();
        var faces = cellBoundaries != null
            ? cellBoundaries.SelectMany(cb => cb.pbc.d2faces)
            : (IEnumerable<Face2dIC>)d2faces;
        foreach (var face in faces)
            foreach (var edge in face.edges) {
                bool isInterior = edge.isCoplanarInterior && edge.neighbor != null;
                bool show = !isInterior
                    || (edge.mark == IPolyhedron.MARK_GRID_DIVISION
                        ? showGridDivisions
                        : showIntraCoplanarEdges);
                if (show) res.Add(edge);
            }
        return res;
    }

    // ── camera occlusion (CutOut) ─────────────────────────────────────────────
    // Ported from Polyhedron3dBoundaryComplex.CutOut, one dimension down: here the faces are
    // 2D quads (projected onto the camera's z=0 plane) and the half-spaces are 2D lines (their
    // normals lie in that plane). The clip/split routines are point-dimension-agnostic — they
    // only call HalfSpace.side / cutPoint and Face2d.Split — so the body mirrors the 3D one with
    // Face2dBC → Face2dIC. Faces here carry no face-level neighbor (only their edges do), so
    // Face2d.Split's CrossReference never recurses into a neighbor face and no Split override is
    // needed (unlike Face2dBC, whose override matters only for the BSP path).
    //
    // Phase 1: keep faces whose projection doesn't overlap the cut region (noSplit).
    // Phase 2: clip the rest against each half-space, collecting the surviving outer fragments;
    //          the leftover innerFacets1 are the parts being occluded away.
    // Finally sever the neighbor links of the removed faces so the now-exposed edges of adjacent
    // faces (in other cells' PBCs) become visible cut-boundary edges (mark=NONE → edgeCutMaterial).
    public void CutOut(HalfSpace[] halfSpaces) {
        var noSplit = new List<Face2dIC>();
        var innerFacets1 = new List<Face2dIC>();
        foreach (var facet in d2faces) {
            if (FaceIntersectsPolyhedron(facet, halfSpaces)) innerFacets1.Add(facet);
            else                                             noSplit.Add(facet);
        }
        var outerFacets = new List<Face2dIC>();
        foreach (var halfSpace in halfSpaces) {
            var innerFacets2 = new List<Face2dIC>();
            Split(halfSpace, innerFacets1, innerFacets2, outerFacets);
            innerFacets1 = innerFacets2;
        }
        foreach (var facet in innerFacets1)
            foreach (var edge in facet.facets)
                if (edge.neighbor != null) edge.neighbor.neighbor = null;
        outerFacets.AddRange(noSplit);
        d2faces = outerFacets;
    }

    // Routing for faces lying exactly on a cutter's line: by orientation (see the 3D twin).
    // Cannot occur for a non-degenerate 2D quad vs a 2D cut line, so this is defensive only.
    static void Split(HalfSpace halfSpace, IEnumerable<Face2dIC> facets, List<Face2dIC> out_inner, List<Face2dIC> out_outer) {
        foreach (var facet in facets) {
            var split = facet.Split(halfSpace);
            if (split.inner != null) out_inner.Add((Face2dIC)split.inner);
            if (split.outer != null) out_outer.Add((Face2dIC)split.outer);
            if (split.isContained) {
                if (AOP.gt(facet.Normal().sc(halfSpace.normal), 0)) out_inner.Add(facet);
                else                                                out_outer.Add(facet);
            }
        }
    }

    static bool FaceIntersectsPolyhedron(Face2dIC facet, HalfSpace[] halfSpaces) {
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
}

}
