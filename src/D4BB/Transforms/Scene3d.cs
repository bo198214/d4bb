using System.Collections.Generic;
using System.Linq;
using D4BB.Comb;
using D4BB.Geometry;

namespace D4BB.Transforms
{
public class Scene3d
{
    public ICamera3d camera { get; set; }
    public bool showInvisibleEdges;
    public bool enable3dOcclusion = true;
    public readonly List<Slab> slabs = new();

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

    public Scene3d(int[][][] origins, ICamera3d camera, bool showInvisibleEdges = false)
    {
        this.camera = camera;
        this.showInvisibleEdges = showInvisibleEdges;
        Update(origins);
    }

    public HashSet<Face2d> VisibleFacets(int pieceIndex)
    {
        var res = new HashSet<Face2d>(new Face2dUnOrientedEquality(AOP.binaryPrecision));
        foreach (var slab in slabs)
            if (slab.pieceIndex == pieceIndex)
                foreach (var facet in slab.pbc.VisibleFacets()) res.Add(facet);
        return res;
    }

    public HashSet<IPolyhedron> VisibleEdges(int pieceIndex)
    {
        var res = new HashSet<IPolyhedron>();
        foreach (var slab in slabs)
            if (slab.pieceIndex == pieceIndex)
                foreach (var edge in slab.pbc.VisibleEdges()) res.Add(edge);
        return res;
    }

    public void Update(int[][][] pieceOrigins)
    {
        RebuildSlabs(pieceOrigins, camera, enable3dOcclusion, showInvisibleEdges, slabs);
        ApplyCameraOcclusion();
    }

    public void UpdateCamera()
    {
        ApplyCameraOcclusion();
    }

    private static void RebuildSlabs(int[][][] pieceOrigins, ICamera3d camera, bool enable3dOcclusion, bool showInvisibleEdges, List<Slab> slabsOut)
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
                        pbc = new Polyhedron2dBoundaryComplex(slabCells, camera, showInvisibleEdges),
                    });
            }
        }
    }

    private void ApplyCameraOcclusion()
    {
        // TODO: implement 3D→2D occlusion analogous to Scene4d.ApplyCameraOcclusion
        // Requires CutOut on Polyhedron2dBoundaryComplex (uses 2D half-spaces).
    }
}
}
