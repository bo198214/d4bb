using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using D4BB.Geometry;
using D4BB.Geometry2;

namespace D4BB.Geometry2Tests {

    /// Tests for the Weiler-Atherton occlusion variant (CellRenderWA3d / RenderPipeline2).
    ///
    /// Two kinds of assertions:
    ///   • structural — WA keeps a partially occluded face as ONE polygon (fewer, concave
    ///     fragments; holes as hole rings) where the Sutherland-Hodgman peel fragments it,
    ///   • area parity — on the same geometry, WA and CellRender3d.CutOut must leave
    ///     exactly the same visible area (the two algorithms compute the same set
    ///     difference), which pins correctness without depending on fragment counts.
    public class WeilerAthertonCutOutTests {

        // ── helpers ────────────────────────────────────────────────────────────

        /// Axis-aligned cube as a CellRenderWA3d — same geometry/winding as the UnitCube
        /// helper in CellRender3dCutOutParityTests.
        static CellRenderWA3d UnitCubeWA(Point origin, double side) {
            var cell = new CellRenderWA3d { sourceCellId = -1 };
            foreach (var f in UnitCubeFaces(origin, side))
                cell.faces.Add(CellRenderWA3d.MakeFaceRegion(f, -1));
            return cell;
        }

        static CellRender3d UnitCubeSH(Point origin, double side) {
            var faces = UnitCubeFaces(origin, side);
            return new CellRender3d {
                sourceCellId = -1,
                faces = faces,
                faceIds = Enumerable.Repeat(-1, faces.Count).ToList(),
            };
        }

        static List<List<Point>> UnitCubeFaces(Point origin, double side) {
            double x0 = origin.x[0], y0 = origin.x[1], z0 = origin.x[2];
            double x1 = x0 + side, y1 = y0 + side, z1 = z0 + side;
            Point P(double x, double y, double z) => new Point(x, y, z);
            return new List<List<Point>> {
                new List<Point> { P(x0, y0, z0), P(x0, y0, z1), P(x0, y1, z1), P(x0, y1, z0) },  // x=lo
                new List<Point> { P(x1, y0, z0), P(x1, y1, z0), P(x1, y1, z1), P(x1, y0, z1) },  // x=hi
                new List<Point> { P(x0, y0, z0), P(x1, y0, z0), P(x1, y0, z1), P(x0, y0, z1) },  // y=lo
                new List<Point> { P(x0, y1, z0), P(x0, y1, z1), P(x1, y1, z1), P(x1, y1, z0) },  // y=hi
                new List<Point> { P(x0, y0, z0), P(x0, y1, z0), P(x1, y1, z0), P(x1, y0, z0) },  // z=lo
                new List<Point> { P(x0, y0, z1), P(x1, y0, z1), P(x1, y1, z1), P(x0, y1, z1) },  // z=hi
            };
        }

        /// Visible area of a WA cell: signed ring areas w.r.t. each face's planeNormal
        /// (outer positive, holes negative).
        static double AreaWA(CellRenderWA3d cell) {
            double a = 0;
            foreach (var f in cell.faces) {
                a += WeilerAtherton.SignedArea(f.outer, f.planeNormal);
                foreach (var h in f.holes) a += WeilerAtherton.SignedArea(h, f.planeNormal);
            }
            return a;
        }

        /// Visible area of a Sutherland-Hodgman cell: sum of unsigned polygon areas
        /// (fragments are convex and disjoint, so unsigned is correct).
        static double AreaSH(CellRender3d cell) {
            double a = 0;
            foreach (var f in cell.faces)
                a += WeilerAtherton.NewellNormal(f).len() / 2;
            return a;
        }

        static void PrintFaces(CellRenderWA3d cell) {
            for (int i = 0; i < cell.faces.Count; i++) {
                var f = cell.faces[i];
                string V(List<Point> ring) =>
                    string.Join(" ", ring.Select(v => $"({v.x[0]:F2},{v.x[1]:F2},{v.x[2]:F2})"));
                TestContext.WriteLine($"  face {i} outer({f.outer.Count}v): {V(f.outer)}");
                foreach (var h in f.holes)
                    TestContext.WriteLine($"        hole({h.Count}v): {V(h)}");
            }
        }

        // ── structural tests ───────────────────────────────────────────────────

        /// Cutter overlaps the source on the x ∈ [0, 0.5] slab. Same expectation as the
        /// Sutherland-Hodgman parity test: 5 surviving faces (x=0 face occluded, x=1 face
        /// intact, 4 lateral faces clipped to single quads). Visible area 6 − 1 − 4·0.5 = 3.
        [Test] public void Half_FaceCountAndArea() {
            var src = UnitCubeWA(new Point(0, 0, 0), 1.0);
            var cutter = UnitCubeWA(new Point(-0.5, 0, 0), 1.0);
            src.CutOut(cutter.DefiningHalfSpaces());
            PrintFaces(src);
            Assert.That(src.faces.Count, Is.EqualTo(5));
            Assert.That(src.faces.All(f => f.outer.Count == 4 && f.holes.Count == 0), Is.True);
            Assert.That(AreaWA(src), Is.EqualTo(3.0).Within(1e-6));
        }

        /// Cutter offset in two axes ("L" case). Sutherland-Hodgman fragments the two
        /// z-faces into 2 quads each (8 polygons total); Weiler-Atherton must keep each
        /// z-face as ONE concave L-shaped hexagon: 6 faces total.
        [Test] public void L_KeepsConcaveHexagons() {
            var src = UnitCubeWA(new Point(0, 0, 0), 1.0);
            var cutter = UnitCubeWA(new Point(-0.5, -0.5, 0), 1.0);
            src.CutOut(cutter.DefiningHalfSpaces());
            PrintFaces(src);
            Assert.That(src.faces.Count, Is.EqualTo(6));
            int hexagons = src.faces.Count(f => f.outer.Count == 6);
            Assert.That(hexagons, Is.EqualTo(2), "z=0 and z=1 faces should stay single L-hexagons");
            Assert.That(src.faces.All(f => f.holes.Count == 0), Is.True);
            Assert.That(AreaWA(src), Is.EqualTo(4.5).Within(1e-6));
        }

        /// A thin column poking through the middle of the z=0 face must punch a HOLE:
        /// one face region with a 4-vertex outer ring and a 4-vertex hole ring — the
        /// Sutherland-Hodgman peel would fragment the face into 4 quads instead.
        [Test] public void ColumnThroughFace_PunchesHole() {
            var src = UnitCubeWA(new Point(0, 0, 0), 1.0);
            var cutter = UnitCubeWA(new Point(0.4, 0.4, -0.1), 0.2);
            src.CutOut(cutter.DefiningHalfSpaces());
            PrintFaces(src);
            Assert.That(src.faces.Count, Is.EqualTo(6));
            var holed = src.faces.Where(f => f.holes.Count > 0).ToList();
            Assert.That(holed.Count, Is.EqualTo(1), "exactly the z=0 face should carry a hole");
            Assert.That(holed[0].outer.Count, Is.EqualTo(4));
            Assert.That(holed[0].holes[0].Count, Is.EqualTo(4));
            Assert.That(AreaWA(src), Is.EqualTo(6.0 - 0.2 * 0.2).Within(1e-6));
        }

        /// Disjoint cutter: every face survives unchanged (same list instances).
        [Test] public void Disjoint_NoOp() {
            var src = UnitCubeWA(new Point(0, 0, 0), 1.0);
            var before = src.faces.Select(f => f.outer).ToList();
            var cutter = UnitCubeWA(new Point(5, 5, 5), 1.0);
            src.CutOut(cutter.DefiningHalfSpaces());
            Assert.That(src.faces.Count, Is.EqualTo(6));
            for (int i = 0; i < 6; i++)
                Assert.That(ReferenceEquals(src.faces[i].outer, before[i]), Is.True);
        }

        /// Cutter fully covering the source: everything occluded.
        [Test] public void FullyCovered_AllFacesDropped() {
            var src = UnitCubeWA(new Point(0, 0, 0), 1.0);
            var cutter = UnitCubeWA(new Point(-0.5, -0.5, -0.5), 2.0);
            src.CutOut(cutter.DefiningHalfSpaces());
            Assert.That(src.faces.Count, Is.EqualTo(0));
        }

        /// Cutter touching the x=1 face exactly (boundary-coincident case): the x=1 face
        /// is counter-oriented to the cutter's x-lo halfspace ⇒ visible boundary, kept;
        /// the lateral faces meet the cutter only in a measure-zero line ⇒ kept. Nothing
        /// may be removed.
        [Test] public void TouchingCutter_KeepsEverything() {
            var src = UnitCubeWA(new Point(0, 0, 0), 1.0);
            var cutter = UnitCubeWA(new Point(1, 0, 0), 1.0);
            src.CutOut(cutter.DefiningHalfSpaces());
            PrintFaces(src);
            Assert.That(src.faces.Count, Is.EqualTo(6));
            Assert.That(AreaWA(src), Is.EqualTo(6.0).Within(1e-6));
        }

        /// Corner cut: the far x=1 face must survive intact as the full unit square
        /// (mirrors CutOutTest_Corner_Parity).
        [Test] public void Corner_FarFaceIntact() {
            var src = UnitCubeWA(new Point(0, 0, 0), 1.0);
            var cutter = UnitCubeWA(new Point(-0.5, -0.5, -0.5), 1.0);
            src.CutOut(cutter.DefiningHalfSpaces());
            PrintFaces(src);
            bool xEqOneIntact = src.faces.Any(f =>
                f.outer.Count == 4 && f.holes.Count == 0 &&
                f.outer.All(v => Math.Abs(v.x[0] - 1.0) < AOP.ERR));
            Assert.That(xEqOneIntact, Is.True, "x=1 face should survive intact");
        }

        // ── area parity with the Sutherland-Hodgman CutOut ─────────────────────

        static void AssertAreaParity(Point cutterOrigin, double cutterSide) {
            var srcWA = UnitCubeWA(new Point(0, 0, 0), 1.0);
            var srcSH = UnitCubeSH(new Point(0, 0, 0), 1.0);
            var hs = UnitCubeSH(cutterOrigin, cutterSide).DefiningHalfSpaces();
            srcWA.CutOut(hs);
            srcSH.CutOut(hs);
            Assert.That(AreaWA(srcWA), Is.EqualTo(AreaSH(srcSH)).Within(1e-4),
                $"visible area diverges for cutter at {cutterOrigin}");
        }

        [Test] public void AreaParity_Half()   => AssertAreaParity(new Point(-0.5,  0,    0),    1.0);
        [Test] public void AreaParity_L()      => AssertAreaParity(new Point(-0.5, -0.5,  0),    1.0);
        [Test] public void AreaParity_Corner() => AssertAreaParity(new Point(-0.5, -0.5, -0.5),  1.0);
        [Test] public void AreaParity_Column() => AssertAreaParity(new Point(0.4,   0.4, -0.1),  0.2);
        [Test] public void AreaParity_Oblique() {
            var srcWA = UnitCubeWA(new Point(0, 0, 0), 1.0);
            var srcSH = UnitCubeSH(new Point(0, 0, 0), 1.0);
            var hs1 = new HalfSpace(new Point(0.6, 0.5, 0), new Point(1, 1, 0).normalize());
            var hs2 = new HalfSpace(new Point(0.5, 0.5, 0), new Point(-1, 1, 0).normalize());
            srcWA.CutOut(new[] { hs1, hs2 });
            srcSH.CutOut(new[] { hs1, hs2 });
            Assert.That(AreaWA(srcWA), Is.EqualTo(AreaSH(srcSH)).Within(1e-4));
        }

        /// Helly case: every halfspace individually intersects the cube but the common
        /// intersection is empty inside the cube — nothing may be removed.
        [Test] public void HellyNoCut_KeepsAllFaces() {
            var src = UnitCubeWA(new Point(0, 0, 0), 1.0);
            var hs1 = new HalfSpace(new Point(0.6, 0, 0), new Point(-1, 0, 0));
            var hs2 = new HalfSpace(new Point(0, 0, 0.6), new Point(0, 0, -1));
            var hs3 = new HalfSpace(new Point(0.2, 0, 0.2), new Point(1, 0, 1).normalize());
            src.CutOut(new[] { hs1, hs2, hs3 });
            Assert.That(src.faces.Count, Is.EqualTo(6));
            Assert.That(AreaWA(src), Is.EqualTo(6.0).Within(1e-6));
        }

        /// Repeated cuts: two disjoint columns through the same face must yield two holes
        /// in one face region (cut resilience of the hole representation).
        [Test] public void TwoColumns_TwoHolesInOneFace() {
            var src = UnitCubeWA(new Point(0, 0, 0), 1.0);
            src.CutOut(UnitCubeWA(new Point(0.15, 0.15, -0.1), 0.2).DefiningHalfSpaces());
            src.CutOut(UnitCubeWA(new Point(0.65, 0.65, -0.1), 0.2).DefiningHalfSpaces());
            PrintFaces(src);
            var holed = src.faces.Where(f => f.holes.Count > 0).ToList();
            Assert.That(holed.Count, Is.EqualTo(1));
            Assert.That(holed[0].holes.Count, Is.EqualTo(2));
            Assert.That(AreaWA(src), Is.EqualTo(6.0 - 2 * 0.04).Within(1e-6));
        }

        /// A second cutter that swallows an existing hole and reaches the face boundary:
        /// the hole must disappear into the larger cut (ring-inside-clip dropping).
        [Test] public void CutSwallowsExistingHole() {
            var src = UnitCubeWA(new Point(0, 0, 0), 1.0);
            src.CutOut(UnitCubeWA(new Point(0.4, 0.4, -0.1), 0.2).DefiningHalfSpaces());
            // second cutter covers x ∈ [0.3, 1.5], y ∈ [0.3, 1.5] around z=0 — swallows
            // the hole and cuts a notch reaching the face's x=1/y=1 boundary
            src.CutOut(UnitCubeWA(new Point(0.3, 0.3, -0.1), 1.2).DefiningHalfSpaces());
            PrintFaces(src);
            var zLoFaces = src.faces.Where(f =>
                f.outer.All(v => Math.Abs(v.x[2]) < AOP.ERR)).ToList();
            Assert.That(zLoFaces.Count, Is.EqualTo(1), "z=0 face should stay one region");
            Assert.That(zLoFaces[0].holes.Count, Is.EqualTo(0), "hole must be swallowed by the bigger cut");
            // z=0 visible: 1 − 0.7·0.7 (notch) = 0.51 ; but the second cutter also clips
            // the lateral x=1/y=1 faces (z ∈ [0, 0.1] strip is NOT reached: cutter z-range
            // is [-0.1, 1.1]... it spans the full side faces region x/y ∈ [0.3,1.5]):
            // just assert against the Sutherland-Hodgman result instead of hand-computing.
            var srcSH = UnitCubeSH(new Point(0, 0, 0), 1.0);
            srcSH.CutOut(UnitCubeSH(new Point(0.4, 0.4, -0.1), 0.2).DefiningHalfSpaces());
            srcSH.CutOut(UnitCubeSH(new Point(0.3, 0.3, -0.1), 1.2).DefiningHalfSpaces());
            Assert.That(AreaWA(src), Is.EqualTo(AreaSH(srcSH)).Within(1e-4));
        }

        // ── full pipeline parity ───────────────────────────────────────────────

        /// RenderPipeline (Sutherland-Hodgman) and RenderPipeline2 (Weiler-Atherton) must
        /// leave the same visible area per source cell on the same complex and camera.
        [Test] public void Pipeline_TwoCubes_AreaParity() {
            var camera = new Camera4dParallel();
            var complexA = Cube4dBuilder.TwoCubesAtDifferentW(0, 1);
            var sh = RenderPipeline.Process(complexA, camera,
                useBsp: true, applyCutOut: true, backfaceCulling: false);
            var complexB = Cube4dBuilder.TwoCubesAtDifferentW(0, 1);
            var wa = RenderPipeline2.Process(complexB, camera,
                useBsp: true, applyCutOut: true, backfaceCulling: false);

            var shAreas = new Dictionary<int, double>();
            foreach (var c in sh)
                shAreas[c.sourceCellId] = shAreas.GetValueOrDefault(c.sourceCellId) + AreaSH(c);
            var waAreas = new Dictionary<int, double>();
            foreach (var c in wa)
                waAreas[c.sourceCellId] = waAreas.GetValueOrDefault(c.sourceCellId) + AreaWA(c);

            Assert.That(waAreas.Keys, Is.EquivalentTo(shAreas.Keys));
            foreach (var kv in shAreas) {
                TestContext.WriteLine($"cell {kv.Key}: SH area {kv.Value:F6}, WA area {waAreas[kv.Key]:F6}");
                Assert.That(waAreas[kv.Key], Is.EqualTo(kv.Value).Within(1e-3),
                    $"visible area diverges for source cell {kv.Key}");
            }
        }

        /// Same parity on the 4-simplex (pen.json), whose BSP splits cells into fragments —
        /// exercises the pipeline on non-cube geometry with synthetic cap faces.
        [Test] public void Pipeline_Pen_AreaParity() {
            var camera = new Camera4dParallel();
            var sh = RenderPipeline.Process(PolychoraAssets.Load("pen.json"), camera,
                useBsp: true, applyCutOut: true, backfaceCulling: false);
            var wa = RenderPipeline2.Process(PolychoraAssets.Load("pen.json"), camera,
                useBsp: true, applyCutOut: true, backfaceCulling: false);
            double shTotal = sh.Sum(AreaSH);
            double waTotal = wa.Sum(AreaWA);
            TestContext.WriteLine($"pen.json: SH area {shTotal:F6}, WA area {waTotal:F6}");
            Assert.That(waTotal, Is.EqualTo(shTotal).Within(1e-3));
        }
    }
}
