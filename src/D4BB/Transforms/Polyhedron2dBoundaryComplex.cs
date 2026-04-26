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

    protected Face2dIC(List<Edge> edges, bool isInvisible, IntegerCell integerCell)
        : base(edges, isInvisible, integerCell) {}

    protected Face2dIC(HashSet<IPolyhedron> facets, bool isInvisible, IntegerCell integerCell)
        : base(facets, isInvisible, integerCell) {}

    protected Face2dIC(List<Point> points, bool isInvisible, IntegerCell integerCell)
        : base(points, isInvisible, integerCell) {}

    public Face2dIC(OrientedIntegerCell ic, ICamera3d cam)
        : base(
            ic.ClockwiseFromOutsideEdges2d()
               .Select(e => EdgeFromCell(e, cam))
               .ToList(),
            false, ic)
    {
        Debug.Assert(ic.Dim() == 2, "Face2dIC requires a 2-cell");
        camera = cam;
        var iEdges = ic.ClockwiseFromOutsideEdges2d();
        for (int k = 0; k < iEdges.Length; k++)
            i2p[iEdges[k]] = edges[k];
    }

    private static Edge EdgeFromCell(OrientedIntegerCell e1, ICamera3d cam) {
        var aOrig = e1.EdgeA().origin.Select(v => (double)v).ToArray();
        var bOrig = e1.EdgeB().origin.Select(v => (double)v).ToArray();
        var pa = cam?.Proj2d(new Point(aOrig)) ?? new Point(aOrig);
        var pb = cam?.Proj2d(new Point(bOrig)) ?? new Point(bOrig);
        var va = new Vertex(pa);
        var vb = new Vertex(pb);
        va.pos4d = aOrig; // original 3D position — reused for animation
        vb.pos4d = bOrig;
        return new Edge(va, vb);
    }

    public override IPolyhedron Recreate(HashSet<IPolyhedron> facets) =>
        new Face2dIC(facets, isInvisible, integerCell) { parent = parent, neighbor = neighbor, camera = camera, pbc = pbc };

    public override Face2d Recreate(List<Edge> edges) =>
        new Face2dIC(edges, isInvisible, integerCell) { parent = parent, neighbor = neighbor, camera = camera, pbc = pbc };

    public override Face2d Recreate(List<Point> points) =>
        new Face2dIC(points, isInvisible, integerCell) { parent = parent, neighbor = neighbor, camera = camera, pbc = pbc };
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
    bool showInvisibleEdges;

    internal Polyhedron2dBoundaryComplex(List<Face2dIC> prebuiltFaces, bool showInvisibleEdges) {
        this.showInvisibleEdges = showInvisibleEdges;
        d2faces = prebuiltFaces;
        foreach (var face in d2faces)
            i2p[face.integerCell] = face;
    }

    public Polyhedron2dBoundaryComplex(HashSet<OrientedIntegerCell> cells2, ICamera3d cam = null, bool showInvisibleEdges = false) {
        this.showInvisibleEdges = showInvisibleEdges;

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
                    myEdge.isInvisible = true;
                }
                // outer boundary edges keep neighbor=null, isInvisible=false
            }
        }

        // Prepare cellBoundaries for future BSP-based occlusion
        cellBoundaries = new List<CellBoundary2d>();
        foreach (var ic2 in cells2)
            cellBoundaries.Add(new CellBoundary2d(ic2,
                new Polyhedron2dBoundaryComplex(new List<Face2dIC> { i2p[ic2] }, showInvisibleEdges)));
    }

    public List<Face2dIC> VisibleFacets() {
        if (cellBoundaries != null) {
            var result = new List<Face2dIC>();
            foreach (var cb in cellBoundaries) result.AddRange(cb.pbc.d2faces);
            return result;
        }
        return d2faces;
    }

    public HashSet<Edge> VisibleEdges() {
        var res = new HashSet<Edge>();
        var faces = cellBoundaries != null
            ? cellBoundaries.SelectMany(cb => cb.pbc.d2faces)
            : (IEnumerable<Face2dIC>)d2faces;
        foreach (var face in faces)
            foreach (var edge in face.edges)
                if (showInvisibleEdges || !edge.isInvisible || edge.neighbor == null)
                    res.Add(edge);
        return res;
    }
}

}
