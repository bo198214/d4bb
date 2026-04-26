using System.Collections.Generic;

namespace D4BB.Comb {
public class D4BSPofSlabs {
    public int splitAxis;
    public int splitValue;
    public List<HashSet<OrientedIntegerCell>> slabs = new(); // slabs on this hyperplane
    public D4BSPofSlabs left;   // slabs with origin[splitAxis] < splitValue
    public D4BSPofSlabs right;  // slabs with origin[splitAxis] >= splitValue

    static int NormalAxis(OrientedIntegerCell c) {
        for (int i = 0; i < c.SpaceDim(); i++)
            if (!c.span.Contains(i)) return i;
        throw new System.Exception("No normal axis found");
    }

    public static D4BSPofSlabs Build(IList<HashSet<OrientedIntegerCell>> sets) {
        if (sets.Count == 0) return null;
        var pivot = sets[0].GetEnumerator();
        pivot.MoveNext();
        int axis = NormalAxis(pivot.Current);
        int value = pivot.Current.origin[axis];
        var node = new D4BSPofSlabs { splitAxis = axis, splitValue = value };
        var leftSets  = new List<HashSet<OrientedIntegerCell>>();
        var rightSets = new List<HashSet<OrientedIntegerCell>>();
        foreach (var set in sets) {
            var onPlane = new HashSet<OrientedIntegerCell>();
            var left    = new HashSet<OrientedIntegerCell>();
            var right   = new HashSet<OrientedIntegerCell>();
            foreach (var c in set) {
                if (NormalAxis(c) == axis && c.origin[axis] == value)
                    onPlane.Add(c);
                else if (c.origin[axis] < value)
                    left.Add(c);
                else
                    right.Add(c);
            }
            if (onPlane.Count > 0) node.slabs.Add(onPlane);
            if (left.Count    > 0) leftSets.Add(left);
            if (right.Count   > 0) rightSets.Add(right);
        }
        node.left  = Build(leftSets);
        node.right = Build(rightSets);
        return node;
    }

    public IEnumerable<HashSet<OrientedIntegerCell>> TraverseFrontToBack(double[] camPos) {
        if (camPos[splitAxis] >= splitValue) {
            if (right != null) foreach (var s in right.TraverseFrontToBack(camPos)) yield return s;
            foreach (var s in slabs) yield return s;
            if (left  != null) foreach (var s in left.TraverseFrontToBack(camPos))  yield return s;
        } else {
            if (left  != null) foreach (var s in left.TraverseFrontToBack(camPos))  yield return s;
            foreach (var s in slabs) yield return s;
            if (right != null) foreach (var s in right.TraverseFrontToBack(camPos)) yield return s;
        }
    }
}
}
