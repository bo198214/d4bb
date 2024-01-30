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
        public HashSet<IPolyhedron> VisibleFacets() {
            HashSet<IPolyhedron> res = new();
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
        public List<HalfSpace[]> definingHalfSpaces = new();
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
    public void ReCalculate() {
        //Take the pieces and calculate the 3d IntegerBoundaryComplexes
        components3d.Clear();
        foreach (var piece in pieces) {
            var pieceIBC = new IntegerBoundaryComplex(piece.origins); //contains 3d facets for each piece
            //selecting the cells that are in front of the camera and pointing to the 4d-Camera
            piece.components3d.Clear();
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
                    foreach (var cell in component3dCells) {
                        component3d.definingHalfSpaces.Add(DefiningHalfSpaces(cell,camera));
                    }
                    Debug.Assert(!components3d.Contains(component3d),"4840513520");
                    piece.components3d.Add(component3d);
                    components3d.Add(component3d);
                }
            }
        }
        //for those 3d-facets now calculate an order and sort the list accordingly
        //it is dependent on the camera only as far the facing components are concerned
        components3d.TSort(new InFrontOfComponentComparer());

        // cut out (in 3d) according to list order (if 4d cam moves 3d cut points needs to be recalculated, non-cut-points can just be exchanged)
        for (int i=0;i<components3d.Count;i++) {
            for (int j=i+1;j<components3d.Count;j++) {
                foreach (var halfSpaces in components3d[j].definingHalfSpaces) {
                    components3d[i].pbc.CutOut(halfSpaces);
                }
            }
        }
    }
}
}