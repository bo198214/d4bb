using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using D4BB.Geometry;
using D4BB.Geometry2;

namespace D4BB.Geometry2Tests {
    public class Bsp4dTests {

        [Test] public void SingleCube_BspYieldsThatCube() {
            var c = Cube4dBuilder.UnitCubeAtW(0);
            var bsp = Bsp4d.Build(c);
            var cam = new Camera4dParallel(new Point4d(0, 0, 0, -5));
            var result = bsp.BackToFront(cam).ToList();
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].sourceCellId, Is.EqualTo(0));
        }

        [Test] public void SingleCube_BackfaceCulling_VisibleFromPositiveW() {
            // Cube at w=0, normal pointing +w. Camera looking from +w side → visible.
            var n = new Point(0, 0, 0, 1);
            var c = Cube4dBuilder.UnitCubeAtW(0, cellNormal: n);
            var bsp = Bsp4d.Build(c);
            var camPos = new Camera4dCentral(new Point4d(0.5, 0.5, 0.5, +5));
            var camNeg = new Camera4dCentral(new Point4d(0.5, 0.5, 0.5, -5));
            Assert.That(bsp.BackToFront(camPos).Count(), Is.EqualTo(1));
            Assert.That(bsp.BackToFront(camNeg).Count(), Is.EqualTo(0));
        }

        [Test] public void SingleCube_NoNormal_AlwaysVisible() {
            var c = Cube4dBuilder.UnitCubeAtW(0);  // no cell normal
            var bsp = Bsp4d.Build(c);
            var camPos = new Camera4dCentral(new Point4d(0.5, 0.5, 0.5, +5));
            var camNeg = new Camera4dCentral(new Point4d(0.5, 0.5, 0.5, -5));
            Assert.That(bsp.BackToFront(camPos).Count(), Is.EqualTo(1));
            Assert.That(bsp.BackToFront(camNeg).Count(), Is.EqualTo(1));
        }

        [Test] public void TwoCubesDifferentW_OrderingIsBackToFront() {
            // Cube A at w=0, Cube B at w=1. No shared geometry, no normals.
            var c = Cube4dBuilder.TwoCubesAtDifferentW(0, 1);
            var bsp = Bsp4d.Build(c);

            // Camera at w=-5 looking toward +w: A (w=0) is closer, B (w=1) is farther.
            var camFromNegW = new Camera4dCentral(new Point4d(0.5, 0.5, 0.5, -5));
            var orderNeg = bsp.BackToFront(camFromNegW).Select(f => f.sourceCellId).ToList();
            Assert.That(orderNeg.Count, Is.EqualTo(2));
            Assert.That(orderNeg[0], Is.EqualTo(1), "B (w=1) should come first (back) when camera is at w=-5");
            Assert.That(orderNeg[1], Is.EqualTo(0));

            // Camera at w=+5 looking toward -w: B (w=1) is closer, A (w=0) is farther.
            var camFromPosW = new Camera4dCentral(new Point4d(0.5, 0.5, 0.5, +5));
            var orderPos = bsp.BackToFront(camFromPosW).Select(f => f.sourceCellId).ToList();
            Assert.That(orderPos.Count, Is.EqualTo(2));
            Assert.That(orderPos[0], Is.EqualTo(0), "A (w=0) should come first (back) when camera is at w=+5");
            Assert.That(orderPos[1], Is.EqualTo(1));
        }

        [Test] public void TwoCubesDifferentW_BackfaceCullingDropsOne() {
            // Cube A at w=0 with normal +w. Cube B at w=1 with normal -w.
            // Camera at w=-5: A's normal points +w (away from camera) → cull. B's normal points -w (toward camera) → visible.
            var c = Cube4dBuilder.TwoCubesAtDifferentW(0, 1,
                normal0: new Point(0, 0, 0, 1),
                normal1: new Point(0, 0, 0, -1));
            var bsp = Bsp4d.Build(c);
            var camFromNegW = new Camera4dCentral(new Point4d(0.5, 0.5, 0.5, -5));
            var visibleIds = bsp.BackToFront(camFromNegW).Select(f => f.sourceCellId).ToList();
            Assert.That(visibleIds, Is.EquivalentTo(new[] { 1 }));
        }

        [Test] public void Classify_SplitterIsOwnHyperplane_IsCoincident() {
            var c = Cube4dBuilder.UnitCubeAtW(0);
            var fragment = CellFragment.FromCell(c, 0);
            var splitter = c.CellHyperplane(0);
            var side = Bsp4d.Classify(fragment, splitter);
            Assert.That(side, Is.EqualTo(CellSide.COINCIDENT));
        }

        [Test] public void Classify_StraddlingSplitter_IsStraddle() {
            // Cube at w=0, splitter at x=0.5 in 4D.
            var c = Cube4dBuilder.UnitCubeAtW(0);
            var fragment = CellFragment.FromCell(c, 0);
            var splitterAtXHalf = new HalfSpace(new Point(0.5, 0, 0, 0), new Point(1, 0, 0, 0));
            var side = Bsp4d.Classify(fragment, splitterAtXHalf);
            Assert.That(side, Is.EqualTo(CellSide.STRADDLE));
        }

        [Test] public void CellSplit_HalfCubeAtX_ProducesTwoBoxes() {
            var c = Cube4dBuilder.UnitCubeAtW(0);
            var fragment = CellFragment.FromCell(c, 0);
            var splitter = new HalfSpace(new Point(0.5, 0, 0, 0), new Point(1, 0, 0, 0));
            var (pos, neg) = CellSplit.Split(fragment, splitter);
            Assert.That(pos, Is.Not.Null);
            Assert.That(neg, Is.Not.Null);
            // Each half-cube should still have 6 faces (4 split-quads + 1 original side + 1 cap).
            Assert.That(pos.faces.Count, Is.EqualTo(6));
            Assert.That(neg.faces.Count, Is.EqualTo(6));
            // pos vertices all have x ≥ 0.5, neg vertices all have x ≤ 0.5.
            foreach (var v in pos.AllVertices()) Assert.That(v.x[0], Is.GreaterThanOrEqualTo(0.5 - AOP.ERR));
            foreach (var v in neg.AllVertices()) Assert.That(v.x[0], Is.LessThanOrEqualTo(0.5 + AOP.ERR));
            // The cap is the rectangle at x=0.5: 4 distinct cap vertices.
            var capCandidates = pos.AllVertices()
                .Where(v => System.Math.Abs(v.x[0] - 0.5) < AOP.ERR)
                .ToList();
            // De-dupe by position
            var uniqueCap = new List<Point>();
            foreach (var p in capCandidates) {
                bool dup = false;
                foreach (var q in uniqueCap) if (p.clone().subtract(q).len() < AOP.ERR) { dup = true; break; }
                if (!dup) uniqueCap.Add(p);
            }
            Assert.That(uniqueCap.Count, Is.EqualTo(4), "cap polygon should have 4 distinct vertices");
        }

        [Test] public void Bsp_StraddlingForcedSplit_OrdersBothFragments() {
            // Build a complex of two cells where every BSP splitter forces at least one split.
            // We provoke this by having only one "real" cell A and then forcing the BSP to use A's
            // hyperplane as splitter (since it is the only cell). To exercise STRADDLE we directly
            // call Split — already covered by CellSplit_HalfCubeAtX_ProducesTwoBoxes.
            // Here we additionally check that after a manual split, BSP traversal of two fragments
            // (positive + negative half of the same source cell) still sorts back-to-front by depth
            // along the splitter normal.
            var c = Cube4dBuilder.UnitCubeAtW(0);
            var fragment = CellFragment.FromCell(c, 0);
            var splitter = new HalfSpace(new Point(0.5, 0, 0, 0), new Point(1, 0, 0, 0));
            var (pos, neg) = CellSplit.Split(fragment, splitter);
            // Build a hand-rolled BSP with these fragments at the root
            var bsp = new Bsp4d {
                root = new BspNode {
                    splitter = splitter,
                    coincident = new List<CellFragment>(),
                    positive = new BspNode { splitter = pos.supportingHyperplane, coincident = new List<CellFragment> { pos }, positive = null, negative = null },
                    negative = new BspNode { splitter = neg.supportingHyperplane, coincident = new List<CellFragment> { neg }, positive = null, negative = null }
                }
            };
            // Camera at x=-5: negative side of splitter is near, positive is far → expect [pos, neg]
            var cam = new Camera4dCentral(new Point4d(-5, 0.5, 0.5, 0));
            var order = bsp.BackToFront(cam).ToList();
            Assert.That(order.Count, Is.EqualTo(2));
            // pos has x ∈ [0.5, 1], farther from camera at x=-5 than neg ∈ [0, 0.5]
            Assert.That(System.Math.Abs(order[0].AnyVertex().x[0] - 0.5), Is.GreaterThan(0).Or.EqualTo(0));
            // Sanity: first yielded is the one with higher x (farther from x=-5)
            double maxXFirst = order[0].AllVertices().Max(v => v.x[0]);
            double maxXSecond = order[1].AllVertices().Max(v => v.x[0]);
            Assert.That(maxXFirst, Is.GreaterThan(maxXSecond));
        }
    }
}
