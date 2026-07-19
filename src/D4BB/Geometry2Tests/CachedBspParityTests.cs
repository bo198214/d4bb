using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using D4BB.Geometry;
using D4BB.Geometry2;
using NUnit.Framework;

namespace D4BB.Geometry2Tests {

    /// The cached-BSP path (object-space Bsp4d + RotatedCamera adapter, used per frame by
    /// ComplexFrame) must produce the same visible geometry as the classic path that
    /// rotates the vertices and rebuilds the BSP every call.
    [TestFixture]
    public class CachedBspParityTests {

        static readonly double[] Angles = { 0.0, 0.35, 1.1, 2.7 };

        static System.Collections.IEnumerable Cases() {
            foreach (var (name, _) in PolycubeFigures.All)
                foreach (var a1 in Angles)
                    foreach (var a2 in Angles)
                        yield return new TestCaseData(name, a1, a2)
                            .SetName($"{name}_a1={a1:F2}_a2={a2:F2}");
        }

        [Test, TestCaseSource(nameof(Cases))]
        public void CachedBsp_MatchesPerFrameRebuild(string figureName, double a1, double a2) {
            var cells = PolycubeFigures.ByName(figureName);

            // Baseline: rotate the vertices, build the BSP from the rotated complex.
            var rotated = IntegerComplex4dBuilder.Boundary(PolycubeFigures.AsIntegerCells(cells));
            TestGeom.RotateComplex(rotated, a1, a2);
            var cam = new Camera4dParallel();
            var baseline = RenderPipeline.Process(rotated, cam,
                useBsp: true, applyCutOut: true, backfaceCulling: true);

            // Cached: BSP built once in object space; the RotatedCamera adapter carries the
            // pose. The complex passed to Process is the rotated one (as in ComplexFrame,
            // where the in-place rotated vertices serve the free-floating-face block).
            var objectSpace = IntegerComplex4dBuilder.Boundary(PolycubeFigures.AsIntegerCells(cells));
            var cache = Bsp4d.Build(objectSpace);
            var cached = RenderPipeline.Process(rotated, cam,
                useBsp: true, applyCutOut: true, backfaceCulling: true,
                cachedBsp: cache, cachedBspCamera: new RotatedCamera(cam, ObjectToWorld(a1, a2)));

            Assert.That(Canonical(cached), Is.EqualTo(Canonical(baseline)),
                $"{figureName}: cached-BSP visible polygons differ from per-frame rebuild");
        }

        static double[] ObjectToWorld(double a1, double a2) {
            double c1 = System.Math.Cos(a1), s1 = System.Math.Sin(a1);
            double c2 = System.Math.Cos(a2), s2 = System.Math.Sin(a2);
            return new[] {
                c1, -s1,  0,   0,
                s1,  c1,  0,   0,
                0,   0,   c2, -s2,
                0,   0,   s2,  c2,
            };
        }

        // Canonical multiset of visible polygons: coordinates rounded, each cycle rotated
        // to its lexicographically smallest form in both orientations, list sorted.
        static List<string> Canonical(List<CellRender3d> processed) {
            var polys = new List<string>();
            foreach (var cell in processed)
                for (int i = 0; i < cell.faces.Count; i++) {
                    if (cell.faceIds[i] < 0) continue;          // synthetic BSP caps
                    var pts = cell.faces[i]
                        .Select(p => string.Join(",", p.x.Select(v =>
                            (System.Math.Round(v, 6) + 0.0).ToString("F6", CultureInfo.InvariantCulture))))
                        .ToList();
                    if (pts.Count < 3) continue;
                    polys.Add(SmallestCycle(pts));
                }
            polys.Sort();
            return polys;
        }

        static string SmallestCycle(List<string> pts) {
            string best = null;
            foreach (var seq in new[] { pts, Enumerable.Reverse(pts).ToList() })
                for (int s = 0; s < seq.Count; s++) {
                    var rot = string.Join(";", Enumerable.Range(0, seq.Count).Select(k => seq[(s + k) % seq.Count]));
                    if (best == null || string.CompareOrdinal(rot, best) < 0) best = rot;
                }
            return best;
        }
    }
}
