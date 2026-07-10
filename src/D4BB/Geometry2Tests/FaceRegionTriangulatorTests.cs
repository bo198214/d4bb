using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using D4BB.Geometry;
using D4BB.Geometry2;

namespace D4BB.Geometry2Tests {

    /// The triangulation must cover exactly the region: summed (unsigned) triangle area
    /// == outer area minus hole areas, and every triangle winds CCW w.r.t. planeNormal.
    public class FaceRegionTriangulatorTests {

        static FaceRegion Region(List<Point> outer, params List<Point>[] holes) {
            var f = CellRenderWA3d.MakeFaceRegion(outer, -1);
            foreach (var h in holes) f.holes.Add(h);
            return f;
        }

        static List<Point> Ring(params (double x, double y)[] pts) {
            var ring = new List<Point>(pts.Length);
            foreach (var (x, y) in pts) ring.Add(new Point(x, y, 0));
            return ring;
        }

        static double TriangleAreaSum(FaceRegion face, List<Point> verts, List<int> tris,
                                      bool checkWinding = true) {
            double sum = 0;
            for (int t = 0; t < tris.Count; t += 3) {
                var a = verts[tris[t]]; var b = verts[tris[t + 1]]; var c = verts[tris[t + 2]];
                var cross = AOP.cross(b.clone().subtract(a), c.clone().subtract(a));
                double signed = face.planeNormal != null ? cross.sc(face.planeNormal) / 2 : cross.len() / 2;
                if (checkWinding)
                    Assert.That(signed, Is.GreaterThan(-1e-9), "triangle winds against planeNormal");
                sum += System.Math.Abs(signed);
            }
            return sum;
        }

        static void AssertCovers(FaceRegion face, double expectedArea) {
            var verts = new List<Point>();
            var tris = new List<int>();
            FaceRegionTriangulator.Triangulate(face, verts, tris);
            Assert.That(TriangleAreaSum(face, verts, tris), Is.EqualTo(expectedArea).Within(1e-9));
        }

        [Test] public void ConvexQuad() =>
            AssertCovers(Region(Ring((0, 0), (2, 0), (2, 1), (0, 1))), 2.0);

        [Test] public void ConcaveL() =>
            AssertCovers(Region(Ring((0, 0), (2, 0), (2, 1), (1, 1), (1, 2), (0, 2))), 3.0);

        [Test] public void SquareWithHole() {
            var outer = Ring((0, 0), (4, 0), (4, 4), (0, 4));
            var hole = Ring((1, 1), (1, 2), (2, 2), (2, 1));   // CW (viewed along +z)
            AssertCovers(Region(outer, hole), 16.0 - 1.0);
        }

        [Test] public void SquareWithTwoHoles() {
            var outer = Ring((0, 0), (6, 0), (6, 6), (0, 6));
            var hole1 = Ring((1, 1), (1, 2), (2, 2), (2, 1));
            var hole2 = Ring((4, 3), (4, 5), (5, 5), (5, 3));
            AssertCovers(Region(outer, hole1, hole2), 36.0 - 1.0 - 2.0);
        }

        [Test] public void ConcaveOuterWithHole() {
            // L-shaped outer with a hole in its wide arm.
            var outer = Ring((0, 0), (6, 0), (6, 2), (2, 2), (2, 6), (0, 6));
            var hole = Ring((3, 0.5), (3, 1.5), (5, 1.5), (5, 0.5));
            AssertCovers(Region(outer, hole), 20.0 - 2.0);
        }

        /// End-to-end: triangulate everything the WA CutOut produces on the hole-punch
        /// cube scenario and compare with the region areas.
        [Test] public void CutOutOutput_TriangulatesFully() {
            var cell = new CellRenderWA3d { sourceCellId = -1 };
            double side = 1.0; var origin = new Point(0, 0, 0);
            // reuse the cube helper shape from WeilerAthertonCutOutTests inline:
            double x0 = origin.x[0], y0 = origin.x[1], z0 = origin.x[2];
            double x1 = x0 + side, y1 = y0 + side, z1 = z0 + side;
            Point P(double x, double y, double z) => new Point(x, y, z);
            var faces = new List<List<Point>> {
                new List<Point> { P(x0, y0, z0), P(x0, y0, z1), P(x0, y1, z1), P(x0, y1, z0) },
                new List<Point> { P(x1, y0, z0), P(x1, y1, z0), P(x1, y1, z1), P(x1, y0, z1) },
                new List<Point> { P(x0, y0, z0), P(x1, y0, z0), P(x1, y0, z1), P(x0, y0, z1) },
                new List<Point> { P(x0, y1, z0), P(x0, y1, z1), P(x1, y1, z1), P(x1, y1, z0) },
                new List<Point> { P(x0, y0, z0), P(x0, y1, z0), P(x1, y1, z0), P(x1, y0, z0) },
                new List<Point> { P(x0, y0, z1), P(x1, y0, z1), P(x1, y1, z1), P(x0, y1, z1) },
            };
            foreach (var f in faces) cell.faces.Add(CellRenderWA3d.MakeFaceRegion(f, -1));
            // Two hole punches + an L-cut for concave outlines.
            var cutter1 = new CellRenderWA3d { sourceCellId = -1 };
            foreach (var f in CubeFaces(0.15, 0.15, -0.1, 0.2)) cutter1.faces.Add(CellRenderWA3d.MakeFaceRegion(f, -1));
            var cutter2 = new CellRenderWA3d { sourceCellId = -1 };
            foreach (var f in CubeFaces(0.65, 0.65, -0.1, 0.2)) cutter2.faces.Add(CellRenderWA3d.MakeFaceRegion(f, -1));
            var cutter3 = new CellRenderWA3d { sourceCellId = -1 };
            foreach (var f in CubeFaces(-0.5, -0.5, 0.5, 1.0)) cutter3.faces.Add(CellRenderWA3d.MakeFaceRegion(f, -1));
            cell.CutOut(cutter1.DefiningHalfSpaces());
            cell.CutOut(cutter2.DefiningHalfSpaces());
            cell.CutOut(cutter3.DefiningHalfSpaces());

            double regionArea = 0, triangleArea = 0;
            var verts = new List<Point>();
            var tris = new List<int>();
            foreach (var face in cell.faces) {
                regionArea += WeilerAtherton.SignedArea(face.outer, face.planeNormal);
                foreach (var h in face.holes) regionArea += WeilerAtherton.SignedArea(h, face.planeNormal);
                verts.Clear(); tris.Clear();
                FaceRegionTriangulator.Triangulate(face, verts, tris);
                triangleArea += TriangleAreaSum(face, verts, tris);
            }
            Assert.That(triangleArea, Is.EqualTo(regionArea).Within(1e-6));
        }

        static List<List<Point>> CubeFaces(double ox, double oy, double oz, double side) {
            double x0 = ox, y0 = oy, z0 = oz, x1 = ox + side, y1 = oy + side, z1 = oz + side;
            Point P(double x, double y, double z) => new Point(x, y, z);
            return new List<List<Point>> {
                new List<Point> { P(x0, y0, z0), P(x0, y0, z1), P(x0, y1, z1), P(x0, y1, z0) },
                new List<Point> { P(x1, y0, z0), P(x1, y1, z0), P(x1, y1, z1), P(x1, y0, z1) },
                new List<Point> { P(x0, y0, z0), P(x1, y0, z0), P(x1, y0, z1), P(x0, y0, z1) },
                new List<Point> { P(x0, y1, z0), P(x0, y1, z1), P(x1, y1, z1), P(x1, y1, z0) },
                new List<Point> { P(x0, y0, z0), P(x0, y1, z0), P(x1, y1, z0), P(x1, y0, z0) },
                new List<Point> { P(x0, y0, z1), P(x1, y0, z1), P(x1, y1, z1), P(x0, y1, z1) },
            };
        }
    }
}
