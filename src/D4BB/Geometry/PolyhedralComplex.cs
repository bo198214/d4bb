using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using D4BB.Comb;
using D4BB.General;

namespace D4BB.Geometry
{
    public interface IPolyhedronWithIntegerCellAttribute : IPolyhedron {
        public IntegerCell integerCell { get; }
    }
    public interface IPolyhedralComplexCell : IPolyhedron, IPolyhedronWithIntegerCellAttribute {
        public static IPolyhedralComplexCell New(IntegerCell ic, bool isInvisible, Dictionary<IntegerCell,List<IPolyhedralComplexCell>> registry) {
            if (ic.Dim()==0) { return new VertexCC(ic,isInvisible,registry);}
            if (ic.Dim()==1) { return new EdgeCC(ic,isInvisible,registry);}
            if (ic.Dim()==2) { return new Face2dCC((OrientedIntegerCell)ic,isInvisible,registry);}
            return new PolyhedronCC(ic,isInvisible,registry);
        }
    }
    public class PolyhedronCC : Polyhedron, IPolyhedralComplexCell {
        public IntegerCell integerCell { get; set; }
        public PolyhedronCC(IntegerCell ic, bool isInvisible, Dictionary<IntegerCell,List<IPolyhedralComplexCell>> registry) :
                    base(ic.Facets().Select(
                        iF => IPolyhedralComplexCell.New(iF,isInvisible,registry)
                    ).Cast<IPolyhedron>().ToHashSet(),isInvisible)         
        {
            Debug.Assert(Dim()>0,"2679684628");
            integerCell = ic;
            registry.AddToValue(ic,this);
        }
    }
    public class VertexCC : Vertex, IPolyhedralComplexCell
    {
        public IntegerCell integerCell { get; set; }
        public VertexCC(IntegerCell ic, bool isInvisible, Dictionary<IntegerCell,List<IPolyhedralComplexCell>> registry) :
                base(new Point(ic.origin), isInvisible)
        {
            Debug.Assert(ic.Dim()==0,"6028659827");
            integerCell = ic;
            registry.AddToValue(ic,this);
        }
    }
    public class EdgeCC : Edge, IPolyhedralComplexCell
    {
        public IntegerCell integerCell { get; set; }
        public EdgeCC(IntegerCell ic, bool isInvisible, Dictionary<IntegerCell,List<IPolyhedralComplexCell>> registry) :
                base(ic.Facets().Select(ic => new VertexCC(ic,isInvisible,registry)).Cast<IPolyhedron>().ToHashSet(), isInvisible)
        {
            Debug.Assert(ic.Dim()==1,"6750306897");
            integerCell = ic;
            registry.AddToValue(ic,this);
        }
        public EdgeCC(OrientedIntegerCell oic, bool isInvisible, Dictionary<IntegerCell,List<IPolyhedralComplexCell>> registry) :
                base(new VertexCC(oic.EdgeA(),isInvisible,registry), new VertexCC(oic.EdgeB(),isInvisible,registry), isInvisible)
        {
            Debug.Assert(oic.Dim()==1,"3872280198");
            integerCell = oic;
            registry.AddToValue(oic,this);
        }
    }
    public class Face2dCC : Face2dWithIntegerCellAttribute, IPolyhedralComplexCell
    {
        public Face2dCC(OrientedIntegerCell ic, bool isInvisible, Dictionary<IntegerCell,List<IPolyhedralComplexCell>> registry) :
                base(ic.ClockwiseFromOutsideEdges2d().Select(e => new EdgeCC(e,isInvisible,registry)).Cast<Edge>().ToList(), isInvisible, ic)
        {
            Debug.Assert(ic.Dim()==2,"6464186877");
            integerCell = ic;
            registry.AddToValue(ic,this);
        }
    }
    public class PolyhedralComplex
    {
        protected Dictionary<IntegerCell,List<IPolyhedralComplexCell>> registry = new();
        public readonly HashSet<IPolyhedron> cells = new();
        public PolyhedralComplex(HashSet<IPolyhedron> _cells) {
            cells = _cells;
        }
        public PolyhedralComplex(int[][] locations) {
            HashSet<IPolyhedron> polyhedrons = new();
            foreach (var location in locations) {
                var cell = IPolyhedralComplexCell.New(new IntegerCell(location),true,registry); //by default all cells are invisible
                cells.Add(cell);
            }
            var ibc = new IntegerBoundaryComplex(locations);
            foreach (var ic in ibc.PrunedSkeletonCellsOfDim(2)) { //TODO performance 
                foreach (var face in registry[ic]) {
                    face.isInvisible = false;
                }
            }
            foreach (var ic in ibc.PrunedSkeletonCellsOfDim(1)) { //TODO performance 
                foreach (var face in registry[ic]) {
                    face.isInvisible = false;
                }
            }
            foreach (var ic in ibc.PrunedSkeletonCellsOfDim(0)) { //TODO performance 
                foreach (var face in registry[ic]) {
                    face.isInvisible = false;
                }
            }
        }
        public int Dim() {
            foreach (var cell in cells) {
                return cell.Dim();
            }
            throw new Exception();
        }

        public PolyhedralComplex CutOut(IPolyhedron polyhedron) {
            Debug.Assert(polyhedron.Dim()==polyhedron.SpaceDim(),"2380654181");
            HashSet<IPolyhedron> cellsToCut = new();
            foreach (var cell in cells) {
                foreach (var halfSpace in polyhedron.HalfSpaces().Values) {
                    var side = cell.Side(halfSpace);
                    if ( side == SplitResult.GENUINE_OUTSIDE || side == SplitResult.TOUCHING_OUTSIDE) {
                        //this cell is outside of polyhedron, no cutting necessary
                        //even if there might be some side halfspaces that would cut this cell
                    } else {
                        cellsToCut.Add(cell);
                    }
                }
            }
            HashSet<IPolyhedron> innerCells1 = new(cellsToCut);
            HashSet<IPolyhedron> outerCells1 = new();
            HashSet<IPolyhedron> innerCells2 = new();
            HashSet<IPolyhedron> outerCells2 = new();
            foreach (var halfSpace in polyhedron.HalfSpaces().Values) {
                foreach (var cell in innerCells1) {
                    var split = cell.Split(halfSpace);
                    if (split.inner!=null)
                        innerCells2.Add(split.inner);
                    if (split.outer!=null)
                        outerCells2.Add(split.outer);
                }
                foreach (var cell in outerCells1) {
                    var split = cell.Split(halfSpace);
                    if (split.inner!=null)
                        outerCells2.Add(split.inner);
                    if (split.outer!=null)
                        outerCells2.Add(split.outer);
                }
                innerCells1=innerCells2;
                innerCells2 = new();
                outerCells1=outerCells2;
                outerCells2 = new();
            }
            return new PolyhedralComplex(outerCells1);
        }
        public override bool Equals(object obj)
        {
            PolyhedralComplex other = (PolyhedralComplex)obj;
            return cells.SetEquals(other.cells);
        }
        public override int GetHashCode()
        {
            var res = 0;
            foreach (var cell in cells) {
                res += cell.GetHashCode();
            }
            return res;
        }

        public HashSet<IPolyhedron> Faces(int dim) {
            if (dim>Dim()) {
                return new HashSet<IPolyhedron>();
            }
            if (dim==Dim()) {
                return cells;
            }
            HashSet<IPolyhedron> res = new();
            foreach (var cell in cells) {
                foreach (var facet in cell.facets) {
                    res.Add(facet);
                }
            }
            return res;
        }
        public HashSet<IPolyhedron> VisibleFacets() {
            return VisibleFaces(Dim()-1);
        }
        public HashSet<IPolyhedron> VisibleFaces(int dim) {
            if (dim>Dim()) {
                return new HashSet<IPolyhedron>();
            }
            if (dim==Dim()) {
                return cells;
            }
            HashSet<IPolyhedron> res = new();
            foreach (var cell in VisibleFaces(dim+1)) {
                foreach (var facet in cell.facets) {
                    if (!facet.isInvisible) res.Add(facet);
                }
            }
            return res;
        }
        public HashSet<IPolyhedron> CalculateVisibleFacets() {
            return CalculateVisibleFaces(Dim()-1);
        }
        public HashSet<IPolyhedron> CalculateVisibleFaces(int dim) {
            if (dim > Dim()) {
                return new HashSet<IPolyhedron>();
            }
            if (dim == Dim()) {
                return cells;
            }
            //each facet is contained in exactly two cells (inner facet)
            //or in exactly one cell (boundary facet)
            //facet is invisible if it is contained in two cells and both cells are in the same subspace
            var connectA = new Dictionary<IPolyhedron,IPolyhedron>();
            var connectB = new Dictionary<IPolyhedron,IPolyhedron>();
            foreach (var cell in CalculateVisibleFaces(dim+1)) {
                foreach (var facet in cell.facets) {
                    if (connectA.ContainsKey(facet)) {
                        connectB[facet] = cell;
                    } else {
                        connectA[facet] = cell;
                    }
                }
            }
            HashSet<IPolyhedron> res = new();
            foreach (var facet in connectA.Keys) {
                var cellA = connectA[facet];
                if (connectB.ContainsKey(facet) ) {
                    //cell connectB[facet] is bordering to cell connectA[facet] via facet
                    var cellB = connectB[facet];
                    if (!cellA.SameSubspace(cellB)) {
                        res.Add(facet);
                    }
                } else {
                    res.Add(facet);
                }
            }
            return res;
        }
    }
}