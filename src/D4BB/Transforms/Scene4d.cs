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
        public readonly List<Component> components3d = new();

        public class Component
        {
            public int pieceIndex;
            public HashSet<OrientedIntegerCell> cells;
            public Polyhedron3dBoundaryComplex pbc;

            public override bool Equals(object obj)
            {
                var other = (Component)obj;
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

        public int PieceCount => components3d.Select(c => c.pieceIndex).DefaultIfEmpty(-1).Max() + 1;

        public HashSet<Face2d> VisibleFacets(int pieceIndex)
{
            HashSet<Face2d> res = new(new Face2dUnOrientedEquality(AOP.binaryPrecision));
            foreach (var comp in components3d)
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
            foreach (var comp in components3d)
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
            components3d.Clear();
            if (pieceOrigins == null) return;

            for (int i = 0; i < pieceOrigins.Length; i++)
            {
                var origins = pieceOrigins[i];
                var pieceIBC = new IntegerBoundaryComplex(origins);

                foreach (var component3dCells in pieceIBC.Components())
                {
                    Point origin = new(component3dCells.First().origin);
                    Point normal = new(component3dCells.First().Normal());
                    bool isFacing = camera.IsFacedBy(origin, normal);
                    if (isFacing || !enable4dOcclusion)
                    {
                        var component3d = new Component()
                        {
                            pieceIndex = i,
                            cells = component3dCells,
                            pbc = new Polyhedron3dBoundaryComplex(new IntegerBoundaryComplex(component3dCells), camera, showInvisibleEdges),
                        };
                        components3d.Add(component3d);
                    }
                }
            }

            // Occlusion logic
            {
                var claimedCells = new HashSet<IntegerCell>();
                foreach (var component in components3d)
                {
                    var toRemove = new List<Face2dBC>();
                    foreach (var kvp in component.pbc.i2p)
                    {
                        if (!claimedCells.Add(kvp.Key))
                            toRemove.Add(kvp.Value);
                    }
                    foreach (var facet in toRemove)
                    {
                        component.pbc.facets.Remove(facet);
                        foreach (IPolyhedron edge in facet.facets)
                            if (edge.neighbor != null) edge.neighbor.neighbor = null;
                    }
                }
            }

            if (enable4dOcclusion)
            {
                Dictionary<OrientedIntegerCell, HalfSpace[]> halfSpaces = new();
                foreach (var component in components3d)
                    foreach (var cell in component.cells)
                        halfSpaces[cell] = DefiningHalfSpaces(cell, camera);

                for (int i = 0; i < components3d.Count; i++)
                {
                    for (int j = 0; j < components3d.Count; j++)
                    {
                        if (i == j) continue;
                        foreach (var cell_j in components3d[j].cells)
                        {
                            foreach (var cell_i in components3d[i].cells)
                            {
                                if (InFrontOfCellComparer.IsInFrontOf(cell_j, cell_i) > 0)
                                {
                                    components3d[i].pbc.CutOut(halfSpaces[cell_j]);
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
