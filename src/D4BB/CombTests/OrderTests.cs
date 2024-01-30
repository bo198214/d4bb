using System;
using System.Collections.Generic;
using System.Linq;
using D4BB.Comb;
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
    }
}
