using System;
using System.Collections.Generic;

namespace D4BB.Comb {
public class D4BSPofCells {
    public int splitAxis;
    public int splitValue;
    public List<OrientedIntegerCell> planeCells = new();
    public D4BSPofCells left;   // origin[splitAxis] < splitValue
    public D4BSPofCells right;  // origin[splitAxis] >= splitValue (outside planeCells)

    static int NormalAxis(OrientedIntegerCell c) {
        for (int i = 0; i < c.SpaceDim(); i++)
            if (!c.span.Contains(i)) return i;
        throw new Exception("No normal axis found");
    }

    public static D4BSPofCells Build(IList<OrientedIntegerCell> cells) {
        if (cells.Count == 0) return null;
        // Prefer a pivot whose hyperplane contains exactly one cell (unique split),
        // so planeCells never mixes cells from different depth layers.
        var pivot = cells[0];
        foreach (var candidate in cells) {
            int k = NormalAxis(candidate), v = candidate.origin[k];
            bool unique = true;
            foreach (var c in cells)
                if (c != candidate && NormalAxis(c) == k && c.origin[k] == v) { unique = false; break; }
            if (unique) { pivot = candidate; break; }
        }
        int axis  = NormalAxis(pivot);
        int value = pivot.origin[axis];
        var node = new D4BSPofCells { splitAxis = axis, splitValue = value };
        var leftList  = new List<OrientedIntegerCell>();
        var rightList = new List<OrientedIntegerCell>();
        foreach (var c in cells) {
            if (NormalAxis(c) == axis && c.origin[axis] == value)
                node.planeCells.Add(c);
            else if (c.origin[axis] < value)
                leftList.Add(c);
            else
                rightList.Add(c);
        }
        node.left  = Build(leftList);
        node.right = Build(rightList);
        return node;
    }

    // Back-to-front: what is further along viewNormal comes first.
    // viewNormal[splitAxis] > 0 → right (larger coordinate) is further → right first.
    // viewNormal[splitAxis] < 0 → left (smaller coordinate) is further → left first.
    public IEnumerable<List<OrientedIntegerCell>> TraverseBackToFront(double[] viewNormal) {
        bool rightIsFar = viewNormal[splitAxis] > 0;
        var far  = rightIsFar ? right : left;
        var near = rightIsFar ? left  : right;
        if (far  != null) foreach (var b in far.TraverseBackToFront(viewNormal))  yield return b;
        if (planeCells.Count > 0) yield return planeCells;
        if (near != null) foreach (var b in near.TraverseBackToFront(viewNormal)) yield return b;
    }
}
}
