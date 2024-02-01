using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using D4BB.Comb;
using D4BB.Geometry;
using static D4BB.General.TopologicalSort;


namespace D4BB.Transforms
{public class Scene4d {
    public ICamera4d camera { get; set; }
    public readonly Piece[] pieces;
    public bool debug;
    public readonly List<List<IntegerBoundaryComplex>> culledSorted3d;
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
            HashSet<Face2d> res = new(new Face2dUnOrientedEquality(AOP.precision)){};
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
        public static Dictionary<Face2d,Face2d> ContainedFacets(HashSet<Face2d> facets) {
            Dictionary<Face2d,Face2d> res = new();
            List<Face2d> pool = new(facets);
            foreach (var facet1 in facets) {
                pool.Remove(facet1);
                foreach (var facet2 in pool) {
                    if (facet1.Contains(facet2)) {
                        res[facet2]=facet1;
                    }
                }
            }
            return res;
        }
    }
    public class Component {
        public Piece piece;
        public HashSet<OrientedIntegerCell> cells;
        public Polyhedron3dBoundaryComplex pbc;
        public List<HalfSpace[]> definingHalfSpaces = new();
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
    public Scene4d(int[][][] pieces, ICamera4d camera, bool debug=false) {
        this.camera = camera;
        this.debug=debug;
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
                        pbc=new Polyhedron3dBoundaryComplex(new IntegerBoundaryComplex(component3dCells),camera,debug),
                    };
                    foreach (var cell3d in component3dCells) {
                        mapping[cell3d] = component3d;
                        component3d.definingHalfSpaces.Add(DefiningHalfSpaces(cell3d,camera));
                    }
                    Debug.Assert(!components3d.Contains(component3d), "4840513520");
                    piece.components3d.Add(component3d);
                    components3d.Add(component3d);
                }
            }
            foreach (var component3d in piece.components3d) {
                foreach (var iCell3d in component3d.cells) {
                    foreach (var iFacet2d in iCell3d.Facets()) {
                        if (!component3d.pbc.i2p.ContainsKey(iFacet2d)) continue; //consider boundary facets
                        var iOther3d = pieceIBC.neighborOfVia[iCell3d][iFacet2d];
                        if (mapping.TryGetValue(iOther3d,out var otherComp3d)) {
                            Debug.Assert(component3d.pbc.i2p.ContainsKey(iFacet2d), "4840513521");
                            Debug.Assert(otherComp3d.pbc.i2p.ContainsKey(iFacet2d), "4840513522");
                            var otherPFace2d = otherComp3d.pbc.i2p[iFacet2d];
                            var thisPFace2d = component3d.pbc.i2p[iFacet2d];
                            otherPFace2d.neighbor = thisPFace2d;
                            thisPFace2d.neighbor = otherPFace2d;
                        }
                    }
                }
            }
            if (debug) foreach (var component3d in piece.components3d) {
                foreach (var pFacet in component3d.pbc.facets) {
                    foreach (var pEdge in pFacet.facets) {
                        Debug.Assert(pEdge.neighbor!=null, "2662198159");
                        Debug.Assert(pEdge.neighbor.neighbor==pEdge, "2662198160");
                    }
                }
            }
        }
        //for those 3d-facets now calculate an order and sort the list accordingly
        //it is dependent on the camera only as far the facing components are concerned
        components3d.TSort(new InFrontOfComponentComparer());

        // //before cutting we make sure to remove doublet facets
        // HashSet<Face2dBC> facets = new();
        // for (int i=0;i<components3d.Count;i++) {
        //     components3d[i].distinguishedFacets.Clear();
        //     foreach (var facet in components3d[i].pbc.facets) {
        //         if (facets.TryGetValue(facet, out var f)) {
        //             components3d[i].distinguishedFacets.Add(f);
        //         } else {
        //             facets.Add(facet);
        //             components3d[i].distinguishedFacets.Add(facet);
        //         }
        //     }
        // }
        // cut out (in 3d) according to list order (if 4d cam moves 3d cut points needs to be recalculated, non-cut-points can just be exchanged)
        for (int i=0;i<components3d.Count;i++) {
            for (int j=i+1;j<components3d.Count;j++) {
                foreach (var halfSpaces in components3d[j].definingHalfSpaces) {
                    components3d[i].pbc.CutOut(halfSpaces);
                    if (debug) foreach (var component3d in components3d) {
                        foreach (var pFacet in component3d.pbc.facets) {
                            foreach (var pEdge in pFacet.facets) {
                                //Debug.Assert(pEdge.neighbor!=null, $"2692198159 {pFacet} {pEdge} null neighbor");
                                if (pEdge.neighbor!=null) Debug.Assert(pEdge.neighbor.neighbor==pEdge,
                                     $"2362198160 {pFacet} {pEdge} neighbor: {pEdge.neighbor}, nn: {pEdge.neighbor.neighbor}");
                            }
                        }
                    }
                    // components3d[i].distinguishedFacets = 
                    //     Polyhedron3dBoundaryComplex.CutOut(components3d[i].distinguishedFacets,halfSpaces);
                    
                }
            }
        }
    }
}
}