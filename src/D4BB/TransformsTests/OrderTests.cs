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
                new Camera4dOrthographic(new Point4d(3,3,3,-2)),
                new Camera4dOrthographic(new Point4d(-1,3,3,-2)),
                new Camera4dOrthographic(new Point4d(3,-1,3,-2)),
                new Camera4dOrthographic(new Point4d(-1,-1,3,-2)),
                new Camera4dOrthographic(new Point4d(3,3,-1,-2)),
                new Camera4dOrthographic(new Point4d(-1,3,-1,-2)),
                new Camera4dOrthographic(new Point4d(3,-1,-1,-2)),
                new Camera4dOrthographic(new Point4d(-1,-1,-1,-2)),
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
                new Camera4dOrthographic(new Point4d(3,3,3,-2)),
                new Camera4dOrthographic(new Point4d(-1,3,3,-2)),
                new Camera4dOrthographic(new Point4d(3,-1,3,-2)),
                new Camera4dOrthographic(new Point4d(-1,-1,3,-2)),
                new Camera4dOrthographic(new Point4d(3,3,-1,-2)),
                new Camera4dOrthographic(new Point4d(-1,3,-1,-2)),
                new Camera4dOrthographic(new Point4d(3,-1,-1,-2)),
                new Camera4dOrthographic(new Point4d(-1,-1,-1,-2)),
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
