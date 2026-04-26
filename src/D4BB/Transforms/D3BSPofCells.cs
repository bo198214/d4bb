using System;
using System.Collections.Generic;

namespace D4BB.Transforms
{
// Analog of D4BSPofCells but for 2-cells (in 3D) paired with Polyhedron2dBoundaryComplex.
public class D3BSPofCells
{
    public int splitAxis;
    public int splitValue;
    public List<CellBoundary2d> planeCells = new();
    public D3BSPofCells left;
    public D3BSPofCells right;

    static int NormalAxis(OrientedIntegerCell c) {
        for (int i = 0; i < c.SpaceDim(); i++)
            if (!c.span.Contains(i)) return i;
        throw new Exception("No normal axis found");
    }

    public static D3BSPofCells Build(IList<CellBoundary2d> cells) {
        if (cells.Count == 0) return null;
        var pivot = cells[0];
        foreach (var candidate in cells) {
            int k = NormalAxis(candidate.cell), v = candidate.cell.origin[k];
            bool unique = true;
            foreach (var c in cells)
                if (c != candidate && NormalAxis(c.cell) == k && c.cell.origin[k] == v) { unique = false; break; }
            if (unique) { pivot = candidate; break; }
        }
        int axis  = NormalAxis(pivot.cell);
        int value = pivot.cell.origin[axis];
        var node  = new D3BSPofCells { splitAxis = axis, splitValue = value };
        var leftList  = new List<CellBoundary2d>();
        var rightList = new List<CellBoundary2d>();
        foreach (var c in cells) {
            if (NormalAxis(c.cell) == axis && c.cell.origin[axis] == value)
                node.planeCells.Add(c);
            else if (c.cell.origin[axis] < value)
                leftList.Add(c);
            else
                rightList.Add(c);
        }
        node.left  = Build(leftList);
        node.right = Build(rightList);
        return node;
    }

    public IEnumerable<List<CellBoundary2d>> TraverseBackToFront(double[] viewNormal) {
        bool rightIsFar = viewNormal[splitAxis] > 0;
        var far  = rightIsFar ? right : left;
        var near = rightIsFar ? left  : right;
        if (far  != null) foreach (var b in far.TraverseBackToFront(viewNormal))  yield return b;
        if (planeCells.Count > 0) yield return planeCells;
        if (near != null) foreach (var b in near.TraverseBackToFront(viewNormal)) yield return b;
    }
}
}
