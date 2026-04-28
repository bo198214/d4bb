using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using D4BB.Comb;
using D4BB.General;
using D4BB.Geometry;
using D4BB.Transforms;
using NUnit.Framework;

namespace D4BB.CombTests
{
    public class OrderTests
    {
        [Test] public void InFrontOfComponentComparerTest2dOrth()
        {
            var edge1 = new OrientedIntegerCell(new int[] { 0, 0}, new HashSet<int>{0}, false, true);
            Assert.That(edge1.NormalAxis(), Is.EqualTo(1));
            Assert.That(edge1.Normal(), Is.EqualTo(new int[]{0,1}));
            var edge2 = new OrientedIntegerCell(new int[] { 0, 0}, new HashSet<int>{1}, false, true);
            Assert.That(edge2.NormalAxis(), Is.EqualTo(0));
            Assert.That(edge2.Normal(), Is.EqualTo(new int[]{1,0}));
            Assert.That(InFrontOfCellComparer.IsInFrontOf(edge1, edge2), Is.EqualTo(0));
        }
        [Test] public void InFrontOfComponentComparerTest2dParallelOpposite()
        {
            var edge1 = new OrientedIntegerCell(new int[] { 0, 0}, new HashSet<int>{1}, false, true);
            Assert.That(edge1.NormalAxis(), Is.EqualTo(0));
            Assert.That(edge1.Normal(), Is.EqualTo(new int[]{1,0}));
            var edge2 = new OrientedIntegerCell(new int[] { 1, 0}, new HashSet<int>{1}, true, true);
            Assert.That(edge2.NormalAxis(), Is.EqualTo(0));
            Assert.That(edge2.Normal(), Is.EqualTo(new int[]{-1,0}));
            Assert.That(InFrontOfCellComparer.IsInFrontOf(edge1, edge2), Is.EqualTo(0));
        }
        [Test] public void InFrontOfComponentComparerTest2dParallelAligned()
        {
            var edge1 = new OrientedIntegerCell(new int[] { 0, 0}, new HashSet<int>{1}, false, true);
            Assert.That(edge1.NormalAxis(), Is.EqualTo(0));
            Assert.That(edge1.Normal(), Is.EqualTo(new int[]{1,0}));
            var edge2 = new OrientedIntegerCell(new int[] { 1, 0}, new HashSet<int>{1}, false, true);
            Assert.That(edge2.NormalAxis(), Is.EqualTo(0));
            Assert.That(edge2.Normal(), Is.EqualTo(new int[]{1,0}));
            Assert.That(InFrontOfCellComparer.IsInFrontOf(edge2, edge1), Is.GreaterThan(0));
            
            Assert.That(new InFrontOfViewNormalComparer(new double[]{1,0}).Compare(edge1, edge2), Is.GreaterThan(0));
            Assert.That(new InFrontOfViewNormalComparer(new double[]{-1,0}).Compare(edge2, edge1), Is.GreaterThan(0));
        }
        // --- InFrontOfViewNormalComparer tests ---

        // Helper: IntegerCell from a 4D hypercube face (normalAxis inferred from span).
        static OrientedIntegerCell Face4d(int[] origin, int normalAxis, bool inverted = false) {
            var span = new HashSet<int>{0,1,2,3};
            span.Remove(normalAxis);
            return new OrientedIntegerCell(origin, span, inverted, false);
        }

        [Test] public void ViewNormalComparer_SameAxis_NearerFirst() {
            // Two cells with same normalAxis=3. viewNormal=(0,0,0,1): x3=0 is nearer than x3=2.
            var near = Face4d(new[]{0,0,0,0}, normalAxis: 3);
            var far  = Face4d(new[]{0,0,0,2}, normalAxis: 3);
            var cmp = new InFrontOfViewNormalComparer(new double[]{0,0,0,1});
            Assert.That(cmp.Compare(near, far), Is.GreaterThan(0), "near (x3=0) should be in front of far (x3=2)");
            Assert.That(cmp.Compare(far, near), Is.LessThan(0));
        }

        [Test] public void ViewNormalComparer_SameAxis_ReverseDirection() {
            // viewNormal=(0,0,0,-1): x3=2 is now nearer (camera looks in -x3).
            var a = Face4d(new[]{0,0,0,0}, normalAxis: 3);
            var b = Face4d(new[]{0,0,0,2}, normalAxis: 3);
            var cmp = new InFrontOfViewNormalComparer(new double[]{0,0,0,-1});
            Assert.That(cmp.Compare(b, a), Is.GreaterThan(0), "x3=2 is nearer for viewNormal=-w");
        }

        [Test] public void ViewNormalComparer_CrossAxis_CorrectDepth() {
            // normalAxis=1 cell from cube [0,0]: center[0]=0.5, depth=0.5.
            // normalAxis=1 cell from cube [2,0]: center[0]=2.5, depth=2.5.
            // viewNormal=(1,0): first is nearer.
            var span1 = new HashSet<int>{0};
            var near = new IntegerCell(new[]{0,0}, span1); // normalAxis=1
            var far  = new IntegerCell(new[]{2,0}, span1);
            var cmp = new InFrontOfViewNormalComparer(new double[]{1,0});
            Assert.That(cmp.Compare(near, far), Is.GreaterThan(0),
                "cell at x0≈0.5 should be in front of cell at x0≈2.5");
        }

        [Test] public void ViewNormalComparer_SameDepth_ReturnsZero() {
            // Two normalAxis=1 cells from the same cube: both have center[0]=0.5 → same depth.
            var span = new HashSet<int>{0};
            var a = new IntegerCell(new[]{0,0}, span);
            var b = new IntegerCell(new[]{0,1}, span);
            var cmp = new InFrontOfViewNormalComparer(new double[]{1,0});
            Assert.That(cmp.Compare(a, b), Is.EqualTo(0),
                "cells with same depth along viewNormal should return 0");
        }

        [Test] public void ViewNormalComparer_PerpendicularAxis_SameDepth_Zero() {
            // normalAxis=1 cells where viewNormal[1]=0: their x1 position is irrelevant,
            // depth = viewNormal·center depends only on x0. Both at center[0]=0.5 → depth equal.
            var span = new HashSet<int>{0};
            var a = new IntegerCell(new[]{0,0}, span); // center[0]=0.5
            var b = new IntegerCell(new[]{0,2}, span); // center[0]=0.5 (x0 not origin[1])
            var cmp = new InFrontOfViewNormalComparer(new double[]{1,0});
            Assert.That(cmp.Compare(a, b), Is.EqualTo(0),
                "cells perpendicular to viewNormal with same depth must return 0 (not occlude each other)");
        }

        [Test] public void InFrontOfComponentComparerTest4d() {
            var behind = new IntegerCell(new int[]{0,0,0,2});
            var inFront = new IntegerCell(new int[]{0,0,0,0});
            behind.Facets().TryGetValue(
                new OrientedIntegerCell(new int[]{0,0,0,2},new HashSet<int>{0,1,2}, true,true),
                out var behindXYZ);
            Assert.That(behindXYZ,Is.Not.Null);
            inFront.Facets().TryGetValue(
                new OrientedIntegerCell(new int[]{0,0,0,1},new HashSet<int>{0,1,2}, true,true),
                out var inFrontBack
            );
            Assert.That(inFrontBack,Is.Not.Null);
            foreach (var frontFacet in inFront.Facets()) {
                var cmp = InFrontOfCellComparer.IsInFrontOf(frontFacet,behindXYZ);
                if (frontFacet.Equals(inFrontBack)) {
                    Assert.That(cmp,Is.EqualTo(0));
                    continue;
                }
                Assert.That(cmp,Is.EqualTo(2),$"{behindXYZ} cmp {frontFacet}");
            }
        }
        [Test] public void InFrontOfComponentComparerTest4dEquality() {
            var inFront = new IntegerCell(new int[]{0,0,0,0});
            foreach (var frontFacet in inFront.Facets()) {
                var cmp = InFrontOfCellComparer.IsInFrontOf(frontFacet,frontFacet);
                Assert.That(cmp,Is.EqualTo(0),$"{frontFacet} cmp {frontFacet} != 0");
            }
        }
        [Test] public void InFrontOfComponentComparerTest4dTransitivity() {
            var behind = new IntegerCell(new int[]{0,0,0,2});
            var inFront = new IntegerCell(new int[]{0,0,0,0});
            
            foreach (ICamera4d camera in new ICamera4d[]{
                new Camera4dCentral(new Point4d(3,3,3,-2)),
                new Camera4dCentral(new Point4d(-1,3,3,-2)),
                new Camera4dCentral(new Point4d(3,-1,3,-2)),
                new Camera4dCentral(new Point4d(-1,-1,3,-2)),
                new Camera4dCentral(new Point4d(3,3,-1,-2)),
                new Camera4dCentral(new Point4d(-1,3,-1,-2)),
                new Camera4dCentral(new Point4d(3,-1,-1,-2)),
                new Camera4dCentral(new Point4d(-1,-1,-1,-2)),
                new Camera4dParallel(new Point4d(3,3,3,-2)),
                new Camera4dParallel(new Point4d(-1,3,3,-2)),
                new Camera4dParallel(new Point4d(3,-1,3,-2)),
                new Camera4dParallel(new Point4d(-1,-1,3,-2)),
                new Camera4dParallel(new Point4d(3,3,-1,-2)),
                new Camera4dParallel(new Point4d(-1,3,-1,-2)),
                new Camera4dParallel(new Point4d(3,-1,-1,-2)),
                new Camera4dParallel(new Point4d(-1,-1,-1,-2)),
            }) {
                List<OrientedIntegerCell> facets = new();
                foreach (var facet in behind.Facets().Union(inFront.Facets())) {
                    Point origin = new(facet.origin);
                    Point normal = new(facet.Normal());
                    bool isFacing   = camera.IsFacedBy(origin,normal);
                    if (isFacing) facets.Add(facet);
                }
                foreach (var facet1 in facets) {
                    foreach (var facet2 in facets) {
                        foreach (var facet3 in facets) {
                            var cmp1 = InFrontOfCellComparer.IsInFrontOf(facet1,facet2);
                            var cmp2 = InFrontOfCellComparer.IsInFrontOf(facet2,facet3);
                            var cmp3 = InFrontOfCellComparer.IsInFrontOf(facet1,facet3);
                            if (cmp1 < 0 && cmp2 < 0) {
                                Assert.That(cmp3, Is.LessThan(0), $"{camera.eye}: {facet1} {cmp1} {facet2} {cmp2} {facet3}");
                            }
                        }
                    }
                }
            }
        }
        [Test] public void InFrontOfComponentComparerTest3dTransitivity() {
            var behind = new IntegerCell(new int[]{0,0,2});
            var inFront = new IntegerCell(new int[]{0,0,0});
            
            foreach (ICamera3d camera in new ICamera3d[]{
                new Camera3dCentral(new Point3d(3,3,-2)),
                new Camera3dCentral(new Point3d(-1,3,-2)),
                new Camera3dCentral(new Point3d(3,-1,-2)),
                new Camera3dCentral(new Point3d(-1,-1,-2)),
                new Camera3dOrthographic(new Point3d(3,3,-2)),
                new Camera3dOrthographic(new Point3d(-1,3,-2)),
                new Camera3dOrthographic(new Point3d(3,-1,-2)),
                new Camera3dOrthographic(new Point3d(-1,-1,-2)),
            }) {
                List<OrientedIntegerCell> facets = new();
                foreach (var facet in behind.Facets().Union(inFront.Facets())) {
                    Point origin = new(facet.origin);
                    Point normal = new(facet.Normal());
                    bool isFacing   = camera.IsFacedBy(origin,normal);
                    if (isFacing) facets.Add(facet);
                }
                foreach (var facet1 in facets) {
                    foreach (var facet2 in facets) {
                        foreach (var facet3 in facets) {
                            var cmp1 = InFrontOfCellComparer.IsInFrontOf(facet1,facet2);
                            var cmp2 = InFrontOfCellComparer.IsInFrontOf(facet2,facet3);
                            var cmp3 = InFrontOfCellComparer.IsInFrontOf(facet1,facet3);
                            if (cmp1 < 0 && cmp2 < 0) {
                                Assert.That(cmp3, Is.LessThan(0), $"{camera.eye}: {facet1} {cmp1} {facet2} {cmp2} {facet3}");
                            }
                        }
                    }
                }
            }
        }
        [Test] public void InFrontOfComponentComparerTest4dTSort() {
            var behind = new IntegerCell(new int[]{0,0,0,2});
            var inFront = new IntegerCell(new int[]{0,0,0,0});
            
            foreach (ICamera4d camera in new ICamera4d[]{
                new Camera4dCentral(new Point4d(3,3,3,-2)),
                new Camera4dCentral(new Point4d(-1,3,3,-2)),
                new Camera4dCentral(new Point4d(3,-1,3,-2)),
                new Camera4dCentral(new Point4d(-1,-1,3,-2)),
                new Camera4dCentral(new Point4d(3,3,-1,-2)),
                new Camera4dCentral(new Point4d(-1,3,-1,-2)),
                new Camera4dCentral(new Point4d(3,-1,-1,-2)),
                new Camera4dCentral(new Point4d(-1,-1,-1,-2)),
                new Camera4dParallel(new Point4d(3,3,3,-2)),
                new Camera4dParallel(new Point4d(-1,3,3,-2)),
                new Camera4dParallel(new Point4d(3,-1,3,-2)),
                new Camera4dParallel(new Point4d(-1,-1,3,-2)),
                new Camera4dParallel(new Point4d(3,3,-1,-2)),
                new Camera4dParallel(new Point4d(-1,3,-1,-2)),
                new Camera4dParallel(new Point4d(3,-1,-1,-2)),
                new Camera4dParallel(new Point4d(-1,-1,-1,-2)),
            }) {
                List<OrientedIntegerCell> facets = new();
                foreach (var facet in behind.Facets().Union(inFront.Facets())) {
                    Point origin = new(facet.origin);
                    Point normal = new(facet.Normal());
                    bool isFacing   = camera.IsFacedBy(origin,normal);
                    if (isFacing) facets.Add(facet);
                }
                int n = facets.Count;
                facets.TSort(new InFrontOfCellComparer());
                Assert.That(facets,Has.Count.EqualTo(n));
                for (int i=0;i<facets.Count;i++) {
                    for (int j=i+1;j<facets.Count;j++) {
                        Assert.That(
                            InFrontOfCellComparer.IsInFrontOf(facets[i],facets[j]),
                            Is.LessThanOrEqualTo(0),
                            $"{facets[i]} !<= {facets[j]}");
                    }
                }
            }
        }

    }
}
