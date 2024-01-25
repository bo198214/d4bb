using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using D4BB.General;

namespace D4BB.Comb
{
public class IntegerBoundaryComplex {
    public readonly Dictionary<OrientedIntegerCell,Dictionary<IntegerCell,OrientedIntegerCell>> neighborOfVia = new(); // dim-1
    public HashSet<OrientedIntegerCell> cells { 
        get {
            return neighborOfVia.Keys.ToHashSet();
        } 
    }
    public IntegerBoundaryComplex(Dictionary<OrientedIntegerCell,Dictionary<IntegerCell,OrientedIntegerCell>> neighborVia_) {
        neighborOfVia = neighborVia_;
    }
    public int Dim() {
        foreach (var cell in neighborOfVia.Keys) {
            return cell.Dim();
        }
        return -1;
    }
    public IntegerBoundaryComplex(int[] _origin) : this(new IntegerCell(_origin)) {}
    public IntegerBoundaryComplex(IntegerCell cube)
    {
        //important, "outward" attribute is set in "Facets()" hence this must run first to 
        //have the outwards information correct (which may differ in Parents())
        foreach (var v in cube.Facets()) neighborOfVia[v] = new();
        foreach (var subFacet in cube.SubFacets()) {
            var parents = cube.Parents(subFacet);
            neighborOfVia[parents.a][subFacet] = parents.b;
            neighborOfVia[parents.b][subFacet] = parents.a;
        }
        Debug.Assert(CheckConnectors(),"4375695141");
    }
    public bool CheckConnectors() {
        foreach (var a_f_b in neighborOfVia) {
            var a = a_f_b.Key;
            foreach (var facet_neighbor in a_f_b.Value) {
                var b = facet_neighbor.Value;
                var f = facet_neighbor.Key;
                if (!neighborOfVia[b][f].Equals(a)) return false;
                if (!a_f_b.Key.Intersection(b).Equals(f)) return false;
            }
        }
        return true;
    }
    public static List<IntegerCell> Int2Cell(int[][] locations) {
        List<IntegerCell> res = new();
        for (int i=0;i<locations.Length;i++) {
            res.Add(new IntegerCell(locations[i]));
        }
        return res;
    }
    public static List<IntegerCell> Int2Cell(List<int[]> locations) {
        List<IntegerCell> res = new();
        for (int i=0;i<locations.Count;i++) {
            res.Add(new IntegerCell(locations[i]));
        }
        return res;
    }
    public IntegerBoundaryComplex(List<IntegerCell> cells) : this(cells[0])
    {
        for (int i=1;i<cells.Count;i++) {
            ConnectCell(cells[i]);
        }
    }
    public IntegerBoundaryComplex(List<OrientedIntegerCell> cells) : this(cells[0])
    {
        for (int i=1;i<cells.Count;i++) {
            ConnectCell(cells[i]);
        }
    }
    public IntegerBoundaryComplex(HashSet<IntegerCell> cells) : this(new List<IntegerCell>(cells)) {}
    public IntegerBoundaryComplex(HashSet<OrientedIntegerCell> cells) : this(new List<OrientedIntegerCell>(cells)) {}
    public IntegerBoundaryComplex(List<int[]> cells) : this(Int2Cell(cells)) {}
    public IntegerBoundaryComplex(int[][] cells) : this(Int2Cell(cells)) {}
    public void ConnectCell(IntegerCell otherCell) {
        var other = new IntegerBoundaryComplex(otherCell);
        var commonCells = new HashSet<OrientedIntegerCell>(this.neighborOfVia.Keys.Intersect(other.neighborOfVia.Keys));
        foreach (var commonCell in commonCells) {
            if (!this.neighborOfVia.ContainsKey(commonCell)) throw new Exception();
            if (!other.neighborOfVia.ContainsKey(commonCell)) throw new Exception();
            foreach (var facet in commonCell.Facets()) {
                var thisNeighbor = this.neighborOfVia[commonCell][facet];
                //Debug.Assert(this.neighborOfVia[thisNeighbor][facet].Equals(commonCell),"0915952808");
                var otherNeighbor = other.neighborOfVia[commonCell][facet];
                //Debug.Assert(other.neighborOfVia[otherNeighbor][facet].Equals(commonCell),"7001410087");
                
                if (this.neighborOfVia.ContainsKey(thisNeighbor))    this.neighborOfVia[thisNeighbor][facet]=otherNeighbor;
                else                                                other.neighborOfVia[thisNeighbor][facet]=otherNeighbor;
                if (other.neighborOfVia.ContainsKey(otherNeighbor)) other.neighborOfVia[otherNeighbor][facet]=thisNeighbor;
                else                                                 this.neighborOfVia[otherNeighbor][facet]=thisNeighbor;
            }
            neighborOfVia.Remove(commonCell);
            other.neighborOfVia.Remove(commonCell);
        }
        //there is no UnionWith for dictionaries
        foreach (var other_a_f_b in other.neighborOfVia) {
            var a = other_a_f_b.Key;
            var f_b = other_a_f_b.Value;
            neighborOfVia[a]=f_b;
        }
        Debug.Assert(CheckConnectors(),"5838116310");
    }
    public HashSet<OrientedIntegerCell> ConnectedCells(
        OrientedIntegerCell initialFacet,
        HashSet<OrientedIntegerCell> outAllFacets)
    {
        HashSet<OrientedIntegerCell> res = new ();
        outAllFacets.Remove(initialFacet);
        res.Add(initialFacet);
        foreach (var f_b in neighborOfVia[initialFacet])
        {
            var side = f_b.Key;
            var otherCubeByComplex = f_b.Value;
            var otherCubeBySpace = initialFacet.SameSpaceOtherParent(side);
            if (otherCubeByComplex.Equals(otherCubeBySpace) && outAllFacets.Contains(otherCubeBySpace)) {
                res.UnionWith(ConnectedCells(otherCubeByComplex, outAllFacets));
            }
        }
        return res;
    }
    public List<HashSet<OrientedIntegerCell>> Components(HashSet<OrientedIntegerCell> outAllCubes)
    {
        List<HashSet<OrientedIntegerCell>> res = new();
        while (outAllCubes.Count() > 0)
        {
            OrientedIntegerCell initialCube = outAllCubes.First();
            HashSet<OrientedIntegerCell> connectedCells = ConnectedCells(initialCube, outAllCubes);
            res.Add(connectedCells); 
        }
        return res;
    }
    public List<HashSet<OrientedIntegerCell>> Components()
    {
        return Components(new HashSet<OrientedIntegerCell>(neighborOfVia.Keys));
    }
    public HashSet<IntegerBoundaryComplex> Skeleton()            
    {
        List<HashSet<OrientedIntegerCell>> components = Components();
        HashSet<IntegerBoundaryComplex> skeletons = new();
        foreach (HashSet<OrientedIntegerCell> component in components)
        {
            var skeleton = new IntegerBoundaryComplex(component);
            skeletons.Add(skeleton);
        }
        return skeletons;
    }

    /// <summary>
    /// dimension of the containing space
    /// </summary>
    public int SpaceDim()
    {
        foreach (IntegerCell cell in cells)
        {
            return cell.SpaceDim();
        }

        throw new Exception();
    }

    public HashSet<IntegerBoundaryComplex> PrunedSkeletonComponentsOfDim(int d)
    {
        if (d > Dim())
        {
            return new HashSet<IntegerBoundaryComplex>();
        }

        if (d == Dim())
        {
            return new(){this};
        }

        if (d == Dim() - 1)
        {
            return Skeleton();
        }
        else
        {

            //d < dim()-1
            HashSet<IntegerBoundaryComplex> res = new();
            var d1Components = PrunedSkeletonComponentsOfDim(d + 1);
            foreach (IntegerBoundaryComplex d1Component in d1Components)
            {
                var d0Components = d1Component.Skeleton();
                res.UnionWith(d0Components);
            }

            return res;
        }
    }
    public HashSet<IntegerCell> PrunedSkeletonCellsOfDim(int d)
    {
        HashSet<IntegerCell> res = new();
        var components = PrunedSkeletonComponentsOfDim(d);
        foreach (var component in components)
        {
            res.UnionWith(component.cells);
        }

        return res;
    }

    public HashSet<IntegerCell> PrunedSkeleton()
    {
        return Skel2cells(Skeleton());
    }
    public static HashSet<IntegerCell> Skel2cells(HashSet<IntegerBoundaryComplex> skel)
    {
        var res = new HashSet<IntegerCell>();
        foreach (var component in skel)
        {
            res.UnionWith(component.cells);
        }

        return res;
    }
    public override bool Equals(object obj)
    {
        if (obj==null) return false;
        var other = (IntegerBoundaryComplex) obj;
        return this.neighborOfVia.DictEqual(other.neighborOfVia);
    }
    public override int GetHashCode()
    {
        int sum = 0;
        foreach (var a_f_b in neighborOfVia) {
            sum += a_f_b.Key.GetHashCode();
            foreach (var f_b in a_f_b.Value) {
                sum += f_b.Key.GetHashCode() + f_b.Value.GetHashCode();
            }
        }
        return sum;
    }

    }
}