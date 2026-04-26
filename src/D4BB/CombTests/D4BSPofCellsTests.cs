using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace D4BB.Comb {

public class D4BSPofCellsTests {

    static List<OrientedIntegerCell> CellsOf(int[] origin) =>
        new IntegerBoundaryComplex(origin).cells.ToList();

    // In back-to-front traversal, all cells of 'far' must appear before all cells of 'near'.
    static void AssertFarBeforeNear(
        List<OrientedIntegerCell> near,
        List<OrientedIntegerCell> far,
        double[] viewNormal)
    {
        var allCells = near.Concat(far).ToList();
        var bsp = D4BSPofCells.Build(allCells);
        Assert.That(bsp, Is.Not.Null);

        var order = bsp.TraverseBackToFront(viewNormal)
                       .SelectMany(b => b)
                       .ToList();
        Assert.That(order.Count, Is.EqualTo(allCells.Count));

        int lastFar = -1, firstNear = order.Count;
        for (int i = 0; i < order.Count; i++) {
            if (far.Contains(order[i]))  lastFar   = i;
            if (near.Contains(order[i]) && i < firstNear) firstNear = i;
        }
        Assert.That(lastFar, Is.LessThan(firstNear),
            "All FAR cells must appear before all NEAR cells in back-to-front traversal.");
    }

    [Test] public void TwoCubesAlongW_ViewNormalPlusW() {
        var near = CellsOf(new int[]{0,0,0,0}); // cube at w=0, nearer
        var far  = CellsOf(new int[]{0,0,0,2}); // cube at w=2, farther
        AssertFarBeforeNear(near, far, new double[]{0,0,0,1});
    }

    [Test] public void TwoCubesAlongW_ViewNormalMinusW() {
        var near = CellsOf(new int[]{0,0,0,2}); // now w=2 is nearer
        var far  = CellsOf(new int[]{0,0,0,0}); // w=0 is farther
        AssertFarBeforeNear(near, far, new double[]{0,0,0,-1});
    }

    [Test] public void TwoCubesAlongX_ViewNormalPlusX() {
        var near = CellsOf(new int[]{0,0,0,0}); // cube at x=0
        var far  = CellsOf(new int[]{2,0,0,0}); // cube at x=2
        AssertFarBeforeNear(near, far, new double[]{1,0,0,0});
    }

    [Test] public void TwoCubesAlongW_DiagonalViewNormal() {
        // Cube at w=0 (near) and w=4 (far). Diagonal viewNormal = (1,0,0,1)/√2.
        // Depths don't overlap: near=[0,1.41], far=[2.83,4.24].
        var near = CellsOf(new int[]{0,0,0,0});
        var far  = CellsOf(new int[]{0,0,0,4});
        double r = 1.0 / System.Math.Sqrt(2);
        AssertFarBeforeNear(near, far, new double[]{r, 0, 0, r});
    }
}

}
