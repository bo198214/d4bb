using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using D4BB.Geometry;
using D4BB.Geometry3d;
using D4BB.Transforms;

namespace D4BB.Geometry3dTests {

    /// Visible-face parity of RenderPipeline3d against the shipping Scene3d occluder —
    /// the 3D sibling of Geometry2Tests.Scene4dParityTests. Scene3d is lattice-aligned
    /// only, so parity covers LATTICE poses (figures × cameras × 90° reorientations);
    /// freely rotated poses are covered by the oracle-independent
    /// OcclusionSoundness3dTests instead.
    ///
    /// Comparison: mutual coverage of the visible-region union by interior sampling
    /// (tolerant of the differing fragmentation — Scene3d emits Sutherland-Hodgman
    /// convex fragments, the pipeline single concave regions with holes).
    public class Scene3dParityTests {

        static readonly (string name, Point zDir)[] Cameras = {
            ("cabinet",      new Point(0.5, 0.5)),
            ("orthographic", new Point(0.0, 0.0)),
            ("generic",      new Point(0.35, 0.30)),
        };

        static readonly (string name, int[] perm, bool[] neg)[] Reorientations = {
            ("id",      new[] { 0, 1, 2 }, new[] { false, false, false }),
            ("swapXY",  new[] { 1, 0, 2 }, new[] { false, false, false }),
            ("swapXZ",  new[] { 2, 1, 0 }, new[] { false, false, false }),
            ("negX",    new[] { 0, 1, 2 }, new[] { true, false, false }),
            ("negZ",    new[] { 0, 1, 2 }, new[] { false, false, true }),
            ("cycNegY", new[] { 1, 2, 0 }, new[] { false, true, false }),
        };

        [Test] public void AllFigures_AllCameras_IdentityOrientation() {
            foreach (var (figName, cells) in Polycube3dFigures.All)
                foreach (var (camName, zDir) in Cameras)
                    AssertParity(cells, new Camera3dParallel(zDir.clone()), $"{figName}/{camName}");
        }

        [Test] public void AllFigures_AllReorientations_CabinetCamera() {
            foreach (var (figName, cells) in Polycube3dFigures.All)
                foreach (var (orName, perm, neg) in Reorientations) {
                    var reoriented = TestGeom3d.Reorient(cells, perm, neg);
                    AssertParity(reoriented, new Camera3dParallel(), $"{figName}/{orName}");
                }
        }

        static void AssertParity(int[][] cells, Camera3dParallel cam, string label) {
            // Oracle: Scene3d painter's CutOut (exact for translates of a unit cube).
            var scene = new Scene3d(new[] { cells }, cam,
                                    showIntraCoplanarEdges: false, showGridDivisions: false);
            var oracleFacets = scene.VisibleFacets(0).Select(f => f.points).ToList();

            // New pipeline.
            var complex = IntegerComplex3dBuilder.Boundary(cells);
            var faces = RenderPipeline3d.ProcessPairwise(complex, cam, applyCutOut: true, backfaceCulling: true);

            // Oracle ⊆ pipeline: every oracle facet's interior samples are covered.
            var misses = new List<string>();
            for (int i = 0; i < oracleFacets.Count; i++) {
                var ring = oracleFacets[i];
                if (ring.Count < 3 || TestGeom3d.Area(ring) < 1e-6) continue;
                foreach (var s in TestGeom3d.InteriorSamples(ring))
                    if (!TestGeom3d.CoveredByFaces(s, faces, TestGeom3d.EdgeEps)) {
                        misses.Add($"oracle facet {i}: sample {TestGeom3dFmt(s)} uncovered");
                        break;
                    }
            }
            Assert.That(misses, Is.Empty, $"{label}: oracle → pipeline coverage\n" + string.Join("\n", misses));

            // Pipeline ⊆ oracle: strictly-interior grid samples of every surviving region
            // must lie on some oracle facet.
            misses.Clear();
            foreach (var f in faces)
                foreach (var s in TestGeom3d.RegionGridSamples(f, TestGeom3d.StrictMargin)) {
                    bool covered = oracleFacets.Any(ring =>
                        ring.Count >= 3 && TestGeom3d.PointInConvexRing(s, ring, TestGeom3d.EdgeEps));
                    if (!covered) {
                        misses.Add($"pipeline face {f.sourceFaceId}: sample {TestGeom3dFmt(s)} uncovered");
                        break;
                    }
                }
            Assert.That(misses, Is.Empty, $"{label}: pipeline → oracle coverage\n" + string.Join("\n", misses));
        }

        static string TestGeom3dFmt(Point p) =>
            string.Format(System.Globalization.CultureInfo.InvariantCulture,
                          "({0:F4}, {1:F4})", p.x[0], p.x[1]);
    }
}
