using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using D4BB.Transforms;

namespace D4BB.Comb {

public class D4BSPofCellsTests {

    static List<CellBoundary> CellsOf(int[] origin) =>
        new IntegerBoundaryComplex(origin).cells
            .Select(c => new CellBoundary(c, null))
            .ToList();

    // Returns true if cell A is (partially) obscured by cell B for the given viewNormal:
    //   1. B is strictly nearer than A along viewNormal (depth(B) < depth(A))
    //   2. Their projections perpendicular to the dominant viewNormal axis overlap.
    static bool Obscures(CellBoundary obscured, CellBoundary occluder, double[] viewNormal) {
        return new InFrontOfViewNormalComparer(viewNormal).Compare(occluder.cell, obscured.cell) > 0;
        // int spaceDim = obscured.cell.SpaceDim();

        // double depthA = 0, depthB = 0;
        // for (int i = 0; i < spaceDim; i++) {
        //     double cA = obscured.cell.origin[i] + (obscured.cell.span.Contains(i) ? 0.5 : 0);
        //     double cB = occluder.cell.origin[i] + (occluder.cell.span.Contains(i) ? 0.5 : 0);
        //     depthA += viewNormal[i] * cA;
        //     depthB += viewNormal[i] * cB;
        // }
        // if (depthB >= depthA - 1e-9) return false; // occluder not strictly nearer

        // // Projection overlap: project perpendicular to the dominant axis
        // int k = 0;
        // for (int i = 1; i < spaceDim; i++)
        //     if (Math.Abs(viewNormal[i]) > Math.Abs(viewNormal[k])) k = i;

        // for (int i = 0; i < spaceDim; i++) {
        //     if (i == k) continue;
        //     double aMin = obscured.cell.origin[i];
        //     double aMax = aMin + (obscured.cell.span.Contains(i) ? 1.0 : 0.0);
        //     double bMin = occluder.cell.origin[i];
        //     double bMax = bMin + (occluder.cell.span.Contains(i) ? 1.0 : 0.0);
        //     if (aMax <= bMin || bMax <= aMin) return false;
        // }
        // return true;
    }

    // General BSP correctness check: for every pair (A, B) where B obscures A,
    // A (farther) must appear before B (nearer) in the back-to-front traversal.
    static void AssertBspOrderCorrect(IList<CellBoundary> cells, double[] viewNormal) {
        var bsp = D4BSPofCells.Build(cells);
        Assert.That(bsp, Is.Not.Null);
        var order = bsp.TraverseBackToFront(viewNormal)
                       .SelectMany(b => b)
                       .ToList();
        Assert.That(order.Count, Is.EqualTo(cells.Count));

        for (int i = 0; i < order.Count; i++)
            for (int j = 0; j < order.Count; j++)
                if (Obscures(order[i], order[j], viewNormal))
                    Assert.That(i, Is.LessThan(j),
                        $"BSP ordering error: cell obscured by later cell. " +
                        $"Obscured index={i}, occluder index={j}");
    }

    [Test] public void TwoCubes_W_PlusW()     => AssertBspOrderCorrect(
        CellsOf(new[]{0,0,0,0}).Concat(CellsOf(new[]{0,0,0,2})).ToList(),
        new double[]{0,0,0,1});

    [Test] public void TwoCubes_W_MinusW()    => AssertBspOrderCorrect(
        CellsOf(new[]{0,0,0,0}).Concat(CellsOf(new[]{0,0,0,2})).ToList(),
        new double[]{0,0,0,-1});

    [Test] public void TwoCubes_X_PlusX()    => AssertBspOrderCorrect(
        CellsOf(new[]{0,0,0,0}).Concat(CellsOf(new[]{2,0,0,0})).ToList(),
        new double[]{1,0,0,0});

    [Test] public void TwoCubes_W_Diagonal() => AssertBspOrderCorrect(
        CellsOf(new[]{0,0,0,0}).Concat(CellsOf(new[]{0,0,0,4})).ToList(),
        new double[]{1.0/Math.Sqrt(2), 0, 0, 1.0/Math.Sqrt(2)});

    [Test] public void FourCubesGrid_PlusX() => AssertBspOrderCorrect(
        CellsOf(new[]{0,0,0,0}).Concat(CellsOf(new[]{2,0,0,0}))
        .Concat(CellsOf(new[]{0,2,0,0})).Concat(CellsOf(new[]{2,2,0,0})).ToList(),
        new double[]{1,0,0,0});

    [Test] public void FourCubesGrid_PlusY() => AssertBspOrderCorrect(
        CellsOf(new[]{0,0,0,0}).Concat(CellsOf(new[]{2,0,0,0}))
        .Concat(CellsOf(new[]{0,2,0,0})).Concat(CellsOf(new[]{2,2,0,0})).ToList(),
        new double[]{0,1,0,0});

    [Test] public void FourCubesGrid_Diagonal() => AssertBspOrderCorrect(
        CellsOf(new[]{0,0,0,0}).Concat(CellsOf(new[]{2,0,0,0}))
        .Concat(CellsOf(new[]{0,2,0,0})).Concat(CellsOf(new[]{2,2,0,0})).ToList(),
        new double[]{1.0/Math.Sqrt(2), 1.0/Math.Sqrt(2), 0, 0});
}

}
