using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using D4BB.Comb;
using D4BB.Geometry;

namespace D4BB.Transforms
{
    public class Scene4d
    {
        public ICamera4d camera { get; set; }
        public bool showInvisibleEdges;
        public bool enable4dOcclusion = true;
        public readonly List<Slab> slabs = new();

        public class Slab
        {
            public int pieceIndex;
            public HashSet<OrientedIntegerCell> cells;
            public Polyhedron3dBoundaryComplex pbc;

            public override bool Equals(object obj)
            {
                var other = (Slab)obj;
                return this.cells.SetEquals(other.cells);
            }

            public override int GetHashCode()
            {
                int res = 0;
                foreach (var cell in cells) { res += cell.GetHashCode(); }
                return res;
            }
        }

        public Scene4d(int[][][] origins, ICamera4d camera, bool showInvisibleEdges = false)
        {
            this.camera = camera;
            this.showInvisibleEdges = showInvisibleEdges;
            Update(origins);
        }

        public int PieceCount => slabs.Select(s => s.pieceIndex).DefaultIfEmpty(-1).Max() + 1;

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
            slabs.Clear();
            if (pieceOrigins == null) return;

            for (int i = 0; i < pieceOrigins.Length; i++)
            {
                var d4PieceIBC = new IntegerBoundaryComplex(pieceOrigins[i]);
                foreach (var slabCells in d4PieceIBC.Slabs())
                {
                    Point origin = new(slabCells.First().origin);
                    Point normal = new(slabCells.First().Normal());
                    if (camera.IsFacedBy(origin, normal) || !enable4dOcclusion)
                        slabs.Add(new Slab {
                            pieceIndex = i,
                            cells = slabCells,
                            pbc = new Polyhedron3dBoundaryComplex(slabCells, camera, showInvisibleEdges),
                        });
                }
            }

            // 3D occlusion: shared 2-faces between slabs cancel.
            // Remove from main pbc.d2faces AND from each CellBoundary's mini-pbc.
            {
                var claimedCells = new HashSet<IntegerCell>();
                foreach (var slab in slabs)
                {
                    var toRemove = new List<Face2dBC>();
                    foreach (var kvp in slab.pbc.i2p)
                        if (!claimedCells.Add(kvp.Key))
                            toRemove.Add(kvp.Value);
                    foreach (var facet in toRemove)
                    {
                        slab.pbc.d2faces.Remove(facet);
                        foreach (IPolyhedron edge in facet.facets)
                            if (edge.neighbor != null) edge.neighbor.neighbor = null;
                        if (slab.pbc.cellBoundaries != null)
                            foreach (var cb in slab.pbc.cellBoundaries)
                                cb.pbc.d2faces.Remove(facet);
                    }
                }
            }

            if (enable4dOcclusion)
            {
                var halfSpaces = new Dictionary<OrientedIntegerCell, HalfSpace[]>();
                foreach (var slab in slabs)
                    if (slab.pbc.cellBoundaries != null)
                        foreach (var cb in slab.pbc.cellBoundaries)
                            halfSpaces[cb.cell] = DefiningHalfSpaces(cb.cell, camera);

                for (int i = 0; i < slabs.Count; i++)
                    for (int j = 0; j < slabs.Count; j++)
                    {
                        if (slabs[i].pieceIndex == slabs[j].pieceIndex) continue;
                        if (slabs[i].pbc.cellBoundaries == null) continue;
                        if (slabs[j].pbc.cellBoundaries == null) continue;
                        foreach (var cbJ in slabs[j].pbc.cellBoundaries)
                            foreach (var cbI in slabs[i].pbc.cellBoundaries)
                                if (InFrontOfCellComparer.IsInFrontOf(cbJ.cell, cbI.cell) > 0)
                                    cbI.pbc.CutOut(halfSpaces[cbJ.cell]);
                    }
            }
        }

        public static HalfSpace[] DefiningHalfSpaces(OrientedIntegerCell cell, ICamera4d cam)
        {
            HalfSpace[] res = new HalfSpace[6];
            var center = new Point(3);
            var vertices = cell.Vertices();
            foreach (var corner in vertices)
                center.add(cam.Proj3d(new Point4d(corner)));
            center.multiply(1.0 / 8);

            int i = 0;
            foreach (var facet in cell.Facets())
            {
                var iVertices = facet.ClockwiseFromOutsideVertices2d();
                var o    = cam.Proj3d(new Point4d(iVertices[0]));
                var p1st = cam.Proj3d(new Point4d(iVertices[1]));
                var p2nd = cam.Proj3d(new Point4d(iVertices[3]));
                var d1st = p1st.subtract(o).normalize();
                var d2nd = p2nd.subtract(o).normalize();
                var normal = AOP.cross(d1st, d2nd).normalize();
                res[i++] = new HalfSpace(o, normal);
            }
            return res;
        }
    }
}
