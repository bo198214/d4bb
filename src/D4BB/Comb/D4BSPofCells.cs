using System.Collections.Generic;

namespace D4BB.Comb {
public class D4BSPofCells {
    public int splitAxis;
    public int splitValue;
    public List<OrientedIntegerCell> planeCells = new(); // cells on this hyperplane
    public D4BSPofCells left;   // cells with origin[splitAxis] < splitValue
    public D4BSPofCells right;  // cells with origin[splitAxis] >= splitValue

    static int NormalAxis(OrientedIntegerCell c) {
        for (int i = 0; i < c.SpaceDim(); i++)
            if (!c.span.Contains(i)) return i;
        throw new System.Exception("No normal axis found");
    }

    public static D4BSPofCells Build(IList<OrientedIntegerCell> cells) {
        if (cells.Count == 0) return null;
        var pivot = cells[0];
        int axis = NormalAxis(pivot);
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

    // Visible cells always have camera on the positive side (IsFacedBy guarantees this),
    // so right (higher coordinate) is always the near side.
    public IEnumerable<OrientedIntegerCell> TraverseFrontToBack() {
        if (right != null) foreach (var c in right.TraverseFrontToBack()) yield return c;
        foreach (var c in planeCells) yield return c;
        if (left  != null) foreach (var c in left.TraverseFrontToBack())  yield return c;
    }
}
}
