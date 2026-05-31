using System.Collections.Generic;
using System.Linq;
using D4BB.Comb;
using D4BB.Geometry;

namespace D4BB.Transforms
{
public class Scene3d
{
    public ICamera3d camera { get; set; }
    public bool showIntraCoplanarEdges;
    public bool showGridDivisions = true;
    public bool enable3dOcclusion = true;
    public readonly List<Slab> slabs = new();
    // Last origins passed to Update — lets UpdateCamera rebuild the slabs before re-occluding,
    // so the destructive CutOut always starts from a clean state (see UpdateCamera).
    private int[][][] lastPieceOrigins;

    public class Slab
    {
        public int pieceIndex;
        public HashSet<OrientedIntegerCell> cells;
        public Polyhedron2dBoundaryComplex pbc;

        public override bool Equals(object obj)
        {
            var other = (Slab)obj;
            return this.cells.SetEquals(other.cells);
        }
        public override int GetHashCode()
        {
            int res = 0;
            foreach (var cell in cells) res += cell.GetHashCode();
            return res;
        }
    }

    // BSP for 3D occlusion (analogous to D4BSPofCells, reused directly)

    public Scene3d(int[][][] origins, ICamera3d camera, bool showIntraCoplanarEdges = false, bool showGridDivisions = true)
    {
        this.camera = camera;
        this.showIntraCoplanarEdges = showIntraCoplanarEdges;
        this.showGridDivisions = showGridDivisions;
        Update(origins);
    }

    public HashSet<Face2d> VisibleFacets(int pieceIndex)
    {
        var res = new HashSet<Face2d>(new Face2dUnOrientedEquality(AOP.binaryPrecision));
        foreach (var slab in slabs)
            if (slab.pieceIndex == pieceIndex)
                foreach (var facet in slab.pbc.BoundaryFacets()) res.Add(facet);
        return res;
    }

    public HashSet<IPolyhedron> VisibleEdges(int pieceIndex)
    {
        var res = new HashSet<IPolyhedron>();
        foreach (var slab in slabs)
            if (slab.pieceIndex == pieceIndex)
                foreach (var edge in slab.pbc.BoundaryEdges()) res.Add(edge);
        return res;
    }

    public void Update(int[][][] pieceOrigins)
    {
        lastPieceOrigins = pieceOrigins;
        RebuildSlabs(pieceOrigins, camera, enable3dOcclusion, showIntraCoplanarEdges, showGridDivisions, slabs);
        ApplyCameraOcclusion();
    }

    public void UpdateCamera()
    {
        // Must rebuild before re-occluding: ApplyCameraOcclusion mutates the per-cell PBCs
        // destructively (CutOut), so calling it twice on the same slabs would erode d2faces
        // cumulatively. Cheap — RebuildSlabs just reprojects the cached origins. Mirrors
        // Scene4d.UpdateCamera (see Scene4dHashTests.NOTES.md for the bug this guards against).
        RebuildSlabs(lastPieceOrigins, camera, enable3dOcclusion, showIntraCoplanarEdges, showGridDivisions, slabs);
        ApplyCameraOcclusion();
    }

    private static void RebuildSlabs(int[][][] pieceOrigins, ICamera3d camera, bool enable3dOcclusion, bool showIntraCoplanarEdges, bool showGridDivisions, List<Slab> slabsOut)
    {
        slabsOut.Clear();
        if (pieceOrigins == null) return;

        for (int i = 0; i < pieceOrigins.Length; i++)
        {
            var d3PieceIBC = new IntegerBoundaryComplex(pieceOrigins[i]);
            foreach (var slabCells in d3PieceIBC.Slabs())
            {
                Point origin = new(slabCells.First().origin);
                Point normal = new(slabCells.First().Normal());
                if (camera.IsFacedBy(origin, normal) || !enable3dOcclusion)
                    slabsOut.Add(new Slab {
                        pieceIndex = i,
                        cells = slabCells,
                        pbc = new Polyhedron2dBoundaryComplex(slabCells, camera, showIntraCoplanarEdges, showGridDivisions),
                    });
            }
        }
    }

    // 3D→2D camera occlusion, one dimension down from Scene4d.ApplyCameraOcclusion. The
    // occludable unit is the individual (convex) 2-cell, exposed per slab as a CellBoundary2d
    // with its own PBC — the same per-cell PBCs that VisibleFacets/VisibleEdges already read
    // through BoundaryFacets()/BoundaryEdges(). Painter's algorithm far→near: each nearer cell
    // cuts away the projection of every farther cell via CutOut against the nearer cell's
    // projected silhouette (DefiningHalfSpaces2d). RebuildSlabs already builds fresh PBCs each
    // Update, so the destructive CutOut starts from a clean state every frame.
    private void ApplyCameraOcclusion()
    {
        if (!enable3dOcclusion) return;

        var viewNormal = camera.viewNormal.x;

        // Flatten the front-facing per-cell units across all slabs (back-facing slabs were
        // already dropped in RebuildSlabs when occlusion is on).
        var units = new List<(OrientedIntegerCell cell, Polyhedron2dBoundaryComplex pbc)>();
        foreach (var slab in slabs)
            if (slab.pbc.cellBoundaries != null)
                foreach (var cb in slab.pbc.cellBoundaries)
                    units.Add((cb.cell, cb.pbc));

        // Far → near: larger depth (along viewNormal) is farther from the camera.
        units.Sort((a, b) => Depth(b.cell, viewNormal).CompareTo(Depth(a.cell, viewNormal)));

        var back = new List<(OrientedIntegerCell cell, Polyhedron2dBoundaryComplex pbc)>();
        foreach (var near in units)
        {
            var nearDepth = Depth(near.cell, viewNormal);
            var halfSpaces = DefiningHalfSpaces2d(near.cell, camera);
            foreach (var far in back)
                if (Depth(far.cell, viewNormal) != nearDepth)   // equal-depth cells coexist
                    far.pbc.CutOut(halfSpaces);
            back.Add(near);
        }
    }

    private static double Depth(IntegerCell cell, double[] viewNormal)
    {
        var c = cell.Center();
        double sum = 0;
        for (int i = 0; i < viewNormal.Length; i++) sum += viewNormal[i] * c[i];
        return sum;
    }

    // The four 2D half-spaces bounding the projected silhouette of a 2-cell (a square face),
    // analogous to Scene4d.DefiningHalfSpaces (which returns six for a projected cube). Each
    // edge of the cell projects to a screen-space segment; its in-plane perpendicular gives a
    // half-space whose normal is flipped so the cell's projected center is on the inside. A
    // point inside all four lies inside the projected quad — exactly the occluded region.
    public static HalfSpace[] DefiningHalfSpaces2d(OrientedIntegerCell cell, ICamera3d cam)
    {
        var center = new Point3d();
        var verts = cell.Vertices();
        foreach (var corner in verts) center.add(cam.Proj2d(new Point(corner)));
        center.multiply(1.0 / verts.Length);

        var facets = cell.Facets();         // the cell's four edges (1-cells)
        var res = new HalfSpace[facets.Count];
        int i = 0;
        foreach (var edge in facets)
        {
            var o = cam.Proj2d(new Point(edge.EdgeA().origin));
            var p = cam.Proj2d(new Point(edge.EdgeB().origin));
            var d = p.clone().subtract(o);                       // edge direction (z = 0)
            var normal = new Point3d(d.x[1], -d.x[0], 0).normalize();   // in-plane perpendicular
            var hs = new HalfSpace(o, normal);
            if (hs.side(center) == HalfSpace.OUTSIDE) hs = hs.flip();
            res[i++] = hs;
        }
        return res;
    }
}
}
