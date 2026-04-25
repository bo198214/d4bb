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
        public readonly List<D3SameSubSpaceBoundaryPart> d3sssbps = new();

        public class D3SameSubSpaceBoundaryPart
        {
            public int pieceIndex;
            public HashSet<OrientedIntegerCell> cells;
            public Polyhedron3dBoundaryComplex pbc;

            public override bool Equals(object obj)
            {
                var other = (D3SameSubSpaceBoundaryPart)obj;
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

        public int PieceCount => d3sssbps.Select(c => c.pieceIndex).DefaultIfEmpty(-1).Max() + 1;

        public HashSet<Face2d> VisibleFacets(int pieceIndex)
{
            HashSet<Face2d> res = new(new Face2dUnOrientedEquality(AOP.binaryPrecision));
            foreach (var comp in d3sssbps)
            {
                if (comp.pieceIndex == pieceIndex)
                {
                    foreach (var facet in comp.pbc.VisibleFacets()) res.Add(facet);
                }
            }
            return res;
        }

        public HashSet<IPolyhedron> VisibleEdges(int pieceIndex)
        {
            HashSet<IPolyhedron> res = new();
            foreach (var comp in d3sssbps)
            {
                if (comp.pieceIndex == pieceIndex)
                {
                    foreach (var edge in comp.pbc.VisibleEdges()) res.Add(edge);
                }
            }
            return res;
        }

        public void Update(int[][][] pieceOrigins)
        {
            d3sssbps.Clear();
            if (pieceOrigins == null) return;

            for (int i = 0; i < pieceOrigins.Length; i++)
            {
                var origins = pieceOrigins[i];
                var d4PieceIBC = new IntegerBoundaryComplex(origins);

                foreach (var d3sssbpRaw in d4PieceIBC.SameSubSpaceBoundaryParts())
                {
                    Point origin = new(d3sssbpRaw.First().origin);
                    Point normal = new(d3sssbpRaw.First().Normal());
                    bool isFacing = camera.IsFacedBy(origin, normal);
                    if (isFacing || !enable4dOcclusion)
                    {
                        var d3sssbp = new D3SameSubSpaceBoundaryPart()
                        {
                            pieceIndex = i,
                            cells = d3sssbpRaw,
                            pbc = new Polyhedron3dBoundaryComplex(new IntegerBoundaryComplex(d3sssbpRaw), camera, showInvisibleEdges),
                        };
                        d3sssbps.Add(d3sssbp);
                    }
                }
            }

            // Occlusion logic
            {
                var claimedCells = new HashSet<IntegerCell>();
                foreach (var d3sssbp in d3sssbps)
                {
                    var toRemove = new List<Face2dBC>();
                    foreach (var kvp in d3sssbp.pbc.i2p)
                    {
                        if (!claimedCells.Add(kvp.Key))
                            toRemove.Add(kvp.Value);
                    }
                    foreach (var facet in toRemove)
                    {
                        d3sssbp.pbc.d2faces.Remove(facet);
                        foreach (IPolyhedron edge in facet.facets)
                            if (edge.neighbor != null) edge.neighbor.neighbor = null;
                    }
                }
            }

            if (enable4dOcclusion)
            {
                Dictionary<OrientedIntegerCell, HalfSpace[]> halfSpaces = new();
                foreach (var d3sssbp in d3sssbps)
                    foreach (var cell in d3sssbp.cells)
                        halfSpaces[cell] = DefiningHalfSpaces(cell, camera);

                for (int i = 0; i < d3sssbps.Count; i++)
                {
                    for (int j = 0; j < d3sssbps.Count; j++)
                    {
                        if (i == j) continue;
                        foreach (var cell_j in d3sssbps[j].cells)
                        {
                            foreach (var cell_i in d3sssbps[i].cells)
                            {
                                if (InFrontOfCellComparer.IsInFrontOf(cell_j, cell_i) > 0)
                                {
                                    d3sssbps[i].pbc.CutOut(halfSpaces[cell_j]);
                                    break;
                                }
                            }
                        }
                    }
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
                var o = cam.Proj3d(new Point4d(iVertices[0]));
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
