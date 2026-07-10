using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using D4BB.Geometry;
using D4BB.Geometry2;
using D4BB.Transforms;

namespace D4BB.Geometry2Tests {

    /// Cross-pipeline parity: for polytesseract figures, the Geometry2 BSP+CutOut pipeline
    /// must produce the same visible geometry as the battle-tested Scene4d pipeline (the
    /// shipping game's renderer, which handles grid pieces with correct 4D occlusion).
    ///
    /// Ground truth: the cells are translates of one convex body (the unit tesseract), and
    /// for translates of a convex body the depth order along the view normal is consistent
    /// wherever projections overlap — so Scene4d's scalar far-to-near sort is exact for
    /// polycube figures under ANY camera orientation, not just the axis-aligned default.
    ///
    /// Two pose modes cover both ways the 4D pose can vary at runtime:
    ///   CameraRot:  both pipelines view the identity-pose figure through the same rotated
    ///               camera (Scene4d's production zoom/rotate path).
    ///   ComplexRot: Geometry2 rotates the complex's vertices+normals in place — exactly
    ///               what ComplexFrame.ApplyRotation does every frame in RotatingComplex —
    ///               while Scene4d poses the equivalent view through an inversely rotated
    ///               camera basis (v'ₖ = Rᵀvₖ, so v'ₖ·p == vₖ·(R·p) for every vertex p:
    ///               identical projected geometry, exercised through the game's actual
    ///               rotation code path).
    ///
    /// Compared is the *union* of visible face polygons (mutual sampling coverage), not the
    /// polygon lists: fragmentation legitimately differs between the pipelines. Excluded on
    /// the Geometry2 side are synthetic BSP-cap faces (faceId -1) and coplanar seam faces
    /// (IsCoplanarFace) — matching what ComplexMeshes actually renders minus the caps, whose
    /// render policy is a separate known issue; Scene4d correspondingly runs with
    /// showGridDivisions=false. (For polycube figures the cap exclusion is provably moot:
    /// lattice-cube supporting hyperplanes can never STRADDLE another lattice cube, so the
    /// BSP never splits — see ParityHarnessSelfTests.PolycubeBsp_NeverSplits.)
    ///
    /// Camera angles are generic (no view-normal component hits zero, no lattice direction
    /// becomes depth-degenerate), so backface culling and depth ordering are unambiguous on
    /// both sides. Convex figures ("single", "bar2", "block221") double as harness sanity
    /// checks; the non-convex figures (L3 — the L.json piece — Lw3, T4, S4, rnd*) are the
    /// configurations the BSP+CutOut path exists for. With this angle grid, CutOut actually
    /// engages (changes faces) in 13–76% of the cases per non-convex figure — measured, so
    /// the parity is not vacuous.
    public class Scene4dParityTests {

        public enum PoseMode { CameraRot, ComplexRot }

        // Generic angles: for the (symmetric) default cavalier camera, XY-rotation by ~π/4
        // and ZW-rotation by ~2.72 would zero a view-normal component (edge-on cells);
        // these values stay clear of both.
        static readonly double[] Angles = { 0.16, 0.42, 0.67, 0.93, 1.21, 1.48, 1.73, 2.13, 2.55, 3.01 };

        // ── the two pipelines ──────────────────────────────────────────────────

        static List<List<Point>> Scene4dVisiblePolygons(int[][] cells, ICamera4d cam) {
            var scene = new Scene4d(new[] { cells }, cam,
                showIntraCoplanarEdges: false, cullBackFaces: true,
                showGridDivisions: false, enable4dOcclusion: true);
            return scene.pieces[0].visibleFacets.Select(f => f.points).ToList();
        }

        static List<List<Point>> Geometry2VisiblePolygons(PolyhedralComplex4d complex, ICamera4d cam) {
            var processed = RenderPipeline.Process(complex, cam,
                useBsp: true, applyCutOut: true, backfaceCulling: true);
            var polys = new List<List<Point>>();
            foreach (var cell in processed) {
                for (int i = 0; i < cell.faces.Count; i++) {
                    int srcFaceId = cell.faceIds[i];
                    if (srcFaceId < 0) continue;                       // synthetic BSP cap
                    if (complex.IsCoplanarFace(srcFaceId)) continue;   // seam between coplanar cells
                    polys.Add(cell.faces[i]);
                }
            }
            return polys;
        }

        // ── cases ──────────────────────────────────────────────────────────────

        static System.Collections.IEnumerable ParityCases() {
            foreach (PoseMode mode in System.Enum.GetValues(typeof(PoseMode)))
                foreach (var (name, _) in PolycubeFigures.All)
                    foreach (var a1 in Angles)
                        foreach (var a2 in Angles)
                            yield return new TestCaseData(name, a1, a2, mode)
                                .SetName($"{name}_{mode}_a1={a1:F2}_a2={a2:F2}");
        }

        [Test, TestCaseSource(nameof(ParityCases))]
        public void VisibleGeometry_MatchesScene4d(string figureName, double a1, double a2, PoseMode mode) {
            var cells = PolycubeFigures.ByName(figureName);
            var complex = IntegerComplex4dBuilder.Boundary(PolycubeFigures.AsIntegerCells(cells));

            Camera4dParallel scene4dCam, geometry2Cam;
            if (mode == PoseMode.CameraRot) {
                scene4dCam = new Camera4dParallel();
                scene4dCam.RotateBasisXY(a1);
                scene4dCam.RotateBasisZW(a2);
                geometry2Cam = scene4dCam;
            } else {
                // Rotate the complex like the game does; give Scene4d the equivalent view
                // via the inversely rotated camera basis (the XY/ZW plane rotations commute).
                TestGeom.RotateComplex(complex, a1, a2);
                geometry2Cam = new Camera4dParallel();
                scene4dCam = new Camera4dParallel();
                scene4dCam.RotateBasisXY(-a1);
                scene4dCam.RotateBasisZW(-a2);
            }

            var expected = TestGeom.Prepare(Scene4dVisiblePolygons(cells, scene4dCam));   // ground truth
            var actual = TestGeom.Prepare(Geometry2VisiblePolygons(complex, geometry2Cam));

            Assert.That(expected.Count, Is.GreaterThan(0), "Scene4d produced no visible faces — harness problem");
            Assert.That(actual.Count, Is.GreaterThan(0), "Geometry2 produced no visible faces");

            // missing: Scene4d shows it, Geometry2 doesn't (over-cut / lost geometry).
            // extra:   Geometry2 shows it, Scene4d doesn't (occluded geometry not cut away).
            var missing = TestGeom.CoverageMismatches(expected, actual);
            var extra = TestGeom.CoverageMismatches(actual, expected);

            Assert.That(missing.Count + extra.Count, Is.EqualTo(0),
                $"{figureName} {mode} (a1={a1:F2}, a2={a2:F2}): " +
                $"{expected.Count} Scene4d polys vs {actual.Count} Geometry2 polys\n" +
                Describe("MISSING in Geometry2 (visible area lost)", missing) +
                Describe("EXTRA in Geometry2 (occluded area not cut)", extra));
        }

        static string Describe(string label, List<string> mismatches) {
            if (mismatches.Count == 0) return "";
            const int cap = 8;
            var shown = mismatches.Take(cap);
            var more = mismatches.Count > cap ? $"\n  … and {mismatches.Count - cap} more" : "";
            return $"{label} ({mismatches.Count}):\n  " + string.Join("\n  ", shown) + more + "\n";
        }
    }
}
