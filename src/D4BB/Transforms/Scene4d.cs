using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using D4BB.Comb;
using D4BB.Geometry;


namespace D4BB.Transforms
{public class Scene4d {
    public ICamera4d camera { get; set; }
    public readonly Piece[] pieces;
    public bool showInvisibleEdges;
    public bool enable4dOcclusion = true;
    public readonly List<Component> components3d = new();
    //public FacetsGenericMesh facetsMesh;
    //public EdgesGenericMesh edgesMesh;
    // public ICollection<IPolyhedron> facets = new HashSet<IPolyhedron>();
    // public HashSet<IPolyhedron> edges = new();
     
    public class Piece {
        public int[][] origins;
        public List<Component> components3d = new();
        public Piece(int[][] origins) {
            this.origins = origins;
        }
        public HashSet<Face2d> VisibleFacets() {
            HashSet<Face2d> res = new(new Face2dUnOrientedEquality(AOP.binaryPrecision)){};
            foreach (var component3d in components3d) {
                foreach (var facet in component3d.pbc.VisibleFacets()) {
                    res.Add(facet);
                }
            }
            return res;
        }
        public HashSet<IPolyhedron> VisibleEdges() {
            HashSet<IPolyhedron> res = new();
            foreach (var component3d in components3d) {
                foreach (var edge in component3d.pbc.VisibleEdges()) {
                    res.Add(edge);
                }
            }
            return res;
        }
    }
    public class Component {
        public Piece piece;
        public HashSet<OrientedIntegerCell> cells;
        public Polyhedron3dBoundaryComplex pbc;
        // public List<Face2dBC> distinguishedFacets = new();
        public override bool Equals(object obj)
        {
            var other = (Component) obj;
            return this.cells.SetEquals(other.cells);
        }
        public override int GetHashCode()
        {
            int res = 0;
            foreach (var cell in cells) { res += cell.GetHashCode(); }
            return res;
        }
    }
    public Scene4d(int[][][] pieces, ICamera4d camera, bool showInvisibleEdges=false) {
        this.camera = camera;
        this.showInvisibleEdges=showInvisibleEdges;
        this.pieces = new Piece[pieces.Length];
        for (int i=0;i<pieces.Length;i++) {
            this.pieces[i] = new Piece(pieces[i]);
        }
        ReCalculate();
    }
    //returns 6 HalfSpaces according to the cube defined in 4d space and projected to 3d via cam
    public static HalfSpace[] DefiningHalfSpaces(OrientedIntegerCell cell, ICamera4d cam) {
        HalfSpace[] res = new HalfSpace[6];
        
        var center = new Point(3);
        var vertices = cell.Vertices();
        Debug.Assert(vertices.Length==8,"3249408123");
        foreach (var corner in vertices) {
            center.add(cam.Proj3d(new Point4d(corner)));
        }
        center.multiply(1.0/8);

        int i=0;
        foreach (var facet in cell.Facets()) {
            var iVertices = facet.ClockwiseFromOutsideVertices2d();
            var o    = cam.Proj3d(new Point4d(iVertices[0]));
            var p1st = cam.Proj3d(new Point4d(iVertices[1]));
            var p2nd = cam.Proj3d(new Point4d(iVertices[3]));
            var d1st = p1st.subtract(o).normalize();
            var d2nd = p2nd.subtract(o).normalize();
            var normal = AOP.cross(d1st,d2nd).normalize();
            {
                var sc = o.clone().subtract(center).sc(normal);
                Debug.Assert(sc>0,"1529589158 " + sc + " " + cell + " " + facet);
            }
            HalfSpace hs = new HalfSpace(o,normal);
            res[i] = hs;
            i++;
        }
        return res;
    }
    public Dictionary<Face2dBC,Face2dBC> ContainedFacetsInComponents() {
        Dictionary<Face2dBC,Face2dBC> res = new();
        List<Face2dBC> pool = new();
        foreach (var component3d in components3d) {
            pool.AddRange(component3d.pbc.facets);
        }
        foreach (var component3d in components3d) {
            foreach (var facet1 in component3d.pbc.facets) {
                pool.Remove(facet1);
                foreach (var facet2 in pool) {
                    if (facet1.Contains(facet2)) {
                        res[facet2]=facet1;
                    }
                }
            }
        }
        return res;
    }
    public void UpdatePiece(int index, int[][] newOrigins) {
        pieces[index].origins = newOrigins;
        ReCalculate();
    }
    public void ReCalculate() {
        //Take the pieces and calculate the 3d IntegerBoundaryComplexes
        components3d.Clear();
        foreach (var piece in pieces) {
            var pieceIBC = new IntegerBoundaryComplex(piece.origins); //contains 3d facets for each piece
            //selecting the cells that are in front of the camera and pointing to the 4d-Camera
            piece.components3d.Clear();
            Dictionary<OrientedIntegerCell,Component> mapping = new();
            foreach (var component3dCells in pieceIBC.Components()) {
                Point origin = new(component3dCells.First().origin);
                Point normal = new(component3dCells.First().Normal());
                //Point originDir = origin.clone().subtract(camera.eye);
                bool isInFront  = camera.Proj3d(origin) != null;
                bool isFacing   = camera.IsFacedBy(origin,normal);
                if (isInFront && isFacing) {
                    var component3d = new Component(){
                        piece=piece,
                        cells=component3dCells,
                        pbc=new Polyhedron3dBoundaryComplex(new IntegerBoundaryComplex(component3dCells),camera,showInvisibleEdges),
                    };
                    foreach (var cell3d in component3dCells) {
                        mapping[cell3d] = component3d;
                    }
                    Debug.Assert(!components3d.Contains(component3d), "4840513520");
                    piece.components3d.Add(component3d);
                    components3d.Add(component3d);
                }
            }
            // foreach (var component3d in piece.components3d) {
            //     foreach (var iCell3d in component3d.cells) {
            //         foreach (var iFacet2d in iCell3d.Facets()) {
            //             if (!component3d.pbc.i2p.ContainsKey(iFacet2d)) continue; //consider boundary facets
            //             var iOther3d = pieceIBC.neighborOfVia[iCell3d][iFacet2d];
            //             if (mapping.TryGetValue(iOther3d,out var otherComp3d)) {
            //                 Debug.Assert(component3d.pbc.i2p.ContainsKey(iFacet2d), "4840513521");
            //                 Debug.Assert(otherComp3d.pbc.i2p.ContainsKey(iFacet2d), "4840513522");
            //                 var otherPFace2d = otherComp3d.pbc.i2p[iFacet2d];
            //                 var thisPFace2d = component3d.pbc.i2p[iFacet2d];
            //                 otherPFace2d.neighbor = thisPFace2d;
            //                 thisPFace2d.neighbor = otherPFace2d;
            //             }
            //         }
            //     }
            // }
            if (Debugger.IsAttached) foreach (var component3d in piece.components3d) {
                foreach (var pFacet in component3d.pbc.facets) {
                    foreach (var pEdge in pFacet.facets) {
                        Debug.Assert(pEdge.neighbor!=null, "2662198159");
                        Debug.Assert(pEdge.neighbor.neighbor==pEdge, "2662198160");
                    }
                }
            }
        }
        
        // Each 2D IntegerCell that appears in multiple PBCs represents a shared boundary face.
        // When IsInFrontOf=0 (same 4D depth), neither component cuts the other, so both keep
        // the shared face. Subsequent CutOut operations can then modify the copies differently,
        // producing non-identical duplicates that survive into the final mesh.
        // Fix: assign each 2D cell to exactly one PBC (first component wins); remove from others.
        {
            var claimedCells = new HashSet<IntegerCell>();
            foreach (var component in components3d) {
                var toRemove = new List<Face2dBC>();
                foreach (var kvp in component.pbc.i2p) {
                    if (!claimedCells.Add(kvp.Key))
                        toRemove.Add(kvp.Value);
                }
                foreach (var facet in toRemove) {
                    component.pbc.facets.Remove(facet);
                    foreach (IPolyhedron edge in facet.facets)
                        if (edge.neighbor != null) edge.neighbor.neighbor = null;
                }
            }
        }

        if (enable4dOcclusion) {
            Dictionary<OrientedIntegerCell,HalfSpace[]> halfSpaces = new();
            foreach (var component in components3d)
                foreach (var cell in component.cells)
                    halfSpaces[cell] = DefiningHalfSpaces(cell,camera);

            for (int i=0;i<components3d.Count;i++) {
                for (int j=0;j<components3d.Count;j++) {
                    if (i==j) continue;
                    foreach (var cell_j in components3d[j].cells) {
                        foreach (var cell_i in components3d[i].cells) {
                            if (InFrontOfCellComparer.IsInFrontOf(cell_j, cell_i) > 0) {
                                components3d[i].pbc.CutOut(halfSpaces[cell_j]);
                                break;
                            }
                        }
                    }
                }
            }
        }
    }
}
}