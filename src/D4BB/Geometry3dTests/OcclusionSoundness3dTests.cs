using System;
using System.Collections.Generic;
using NUnit.Framework;
using D4BB.Geometry;
using D4BB.Geometry3d;

namespace D4BB.Geometry3dTests {

    /// Oracle-INDEPENDENT occlusion invariants under freely rotated poses (where the
    /// lattice-bound Scene3d cannot follow) — the 3D sibling of
    /// Geometry2Tests.OcclusionSoundnessTests, strengthened by a completeness direction:
    ///
    ///   (a) UNDER-CUT: no surviving sample lies strictly inside the pre-cut projected
    ///       hull of a strictly NEARER front face — "nearer" decided per sample by an
    ///       independent fiber-depth comparison (TestGeom3d.FiberDepth), so the check
    ///       shares no ordering logic with the pipeline.
    ///   (b) COMPLETENESS (over-cut guard): every pre-cut front-face sample NOT strictly
    ///       occluded by any other front face must still be covered by the output.
    public class OcclusionSoundness3dTests {

        // Rigid rotations of whole figures through the three coordinate planes.
        [Test] public void Figures_RotationSweeps() {
            (int i, int j)[] planes = { (0, 1), (0, 2), (1, 2) };
            foreach (var (figName, cells) in Polycube3dFigures.All) {
                int step = figName == "L3" || figName == "T4" || figName == "rnd5" ? 10 : 30;
                var boundary = IntegerComplex3dBuilder.Boundary(cells);
                var posed = PosedComplexes.Single(boundary);
                var cam = new Camera3dParallel();   // cabinet
                foreach (var (i, j) in planes)
                    for (int deg = 0; deg < 360; deg += step) {
                        posed.SetPose(0, PlaneRot(i, j, deg * Math.PI / 180.0), null, null);
                        AssertSound(posed.complex, cam, $"{figName} rot({i},{j}) {deg}°");
                    }
            }
        }

        // Three separate cubes with seeded random rigid poses at beat-like separations.
        [Test] public void ThreeCubes_RandomPoses() {
            var cube = IntegerComplex3dBuilder.Boundary(Polycube3dFigures.ByName("single"));
            var posed = PosedComplexes.Merge(new List<PolyhedralComplex3d> { cube, cube, cube });
            for (int p = 0; p < 3; p++)
                posed.BakeOriginalTransform(p, 1.0, new double[] { -0.5, -0.5, -0.5 });
            var offsets = new[] {
                new double[] { 2.4, -1.2, 2.2 },
                new double[] { -2.4, -1.2, 4.4 },
                new double[] { 0.0, 2.0, 6.6 },
            };
            var cam = new Camera3dParallel(new Point(0.35, 0.30));
            var rng = new Random(1337);
            for (int trial = 0; trial < 8; trial++) {
                for (int p = 0; p < 3; p++) {
                    var axis = RandomUnitAxis(rng);
                    double angle = rng.NextDouble() * 2 * Math.PI;
                    posed.SetPose(p, PosedComplexes.AxisAngleRot(axis, angle), null, offsets[p]);
                }
                AssertSound(posed.complex, cam, $"threeCubes trial {trial}");
            }
        }

        // Replay of the LAssembleCubesBeat FromZ choreography poses (tumbling approach in
        // disjoint z-lanes, hover, simultaneous descent into the L slots) — certifies the
        // exact flight the beat renders, including any mutual-straddle fallback activations.
        [Test] public void FromZChoreographyReplay() {
            var cube = IntegerComplex3dBuilder.Boundary(Polycube3dFigures.ByName("single"));
            var posed = PosedComplexes.Merge(new List<PolyhedralComplex3d> { cube, cube, cube });
            for (int p = 0; p < 3; p++)
                posed.BakeOriginalTransform(p, 1.0, new double[] { -0.5, -0.5, -0.5 });
            var cam = new Camera3dParallel(new Point(0.35, 0.30));

            // Mirrors the beat's constants (cubeSize = 1): dock offsets of the L cells
            // (0,-1,0)/(-1,-1,0)/(0,0,0) about the L's bbox center, z-lanes, lateral spread.
            var dock = new[] {
                new double[] { 0.5, -0.5, 0 },
                new double[] { -0.5, -0.5, 0 },
                new double[] { 0.5, 0.5, 0 },
            };
            double[] laneZ = { 2.2, 4.4, 6.6 };
            var lateral = new[] {
                new double[] { 2.4, -1.2 },
                new double[] { -2.4, -1.2 },
                new double[] { 0.0, 2.0 },
            };
            const double approachEnd = 0.60, descentStart = 0.65;
            const float flyInSeconds = 9f, tumbleSpeed = 50f;
            double tumbleTotalRad = tumbleSpeed * (approachEnd * flyInSeconds) * Math.PI / 180.0;
            var rng = new Random(42);
            var axes = new double[3][];
            for (int p = 0; p < 3; p++) axes[p] = RandomUnitAxis(rng);

            for (int step = 0; step <= 20; step++) {
                double u = step / 20.0;
                for (int p = 0; p < 3; p++) {
                    double[] off;
                    double[] rot;
                    if (u < approachEnd) {
                        double a = u / approachEnd;
                        double rem = Math.Pow(1.0 - a, 3.0);   // 1 − easeOutCubic(a)
                        off = new[] {
                            dock[p][0] + rem * lateral[p][0],
                            dock[p][1] + rem * lateral[p][1],
                            dock[p][2] + laneZ[p] };
                        rot = PosedComplexes.AxisAngleRot(axes[p], rem * tumbleTotalRad);
                    } else {
                        double d = u < descentStart ? 0.0
                            : SmoothStep((u - descentStart) / (1.0 - descentStart));
                        off = new[] { dock[p][0], dock[p][1], dock[p][2] + laneZ[p] * (1.0 - d) };
                        rot = PosedComplexes.IdentityRot;
                    }
                    posed.SetPose(p, rot, null, off);
                }
                AssertSound(posed.complex, cam, $"FromZ u={u:F2}");
            }
        }

        // ── the invariant checker ────────────────────────────────────────────────

        static void AssertSound(PolyhedralComplex3d complex, ICamera3d cam, string label) {
            var faces = RenderPipeline3d.ProcessPairwise(complex, cam, applyCutOut: true, backfaceCulling: true);

            // Per output face: its pre-cut hull, supporting plane, and fiber depth basis.
            int n = faces.Count;
            var hulls = new HalfSpace[n][];
            var planes = new HalfSpace[n];
            for (int i = 0; i < n; i++) {
                hulls[i] = faces[i].DefiningHalfSpaces2d();
                planes[i] = complex.FacePlane(faces[i].sourceFaceId);
            }

            var violations = new List<string>();
            for (int i = 0; i < n && violations.Count < 5; i++) {
                foreach (var s in TestGeom3d.InteriorSamples(faces[i].ring)) {
                    double di = TestGeom3d.FiberDepth(s, planes[i], cam);
                    // STRICT occlusion (for under-cut): some single occluder strictly
                    // contains the sample and is strictly nearer.
                    bool strictlyOccluded = false;
                    // POSSIBLE occlusion (for over-cut): a sample can be legitimately cut
                    // while lying exactly on the shared boundary of several occluders
                    // that jointly cover it, or exactly on a depth-equal line — so this
                    // direction is tolerant (+margin on both hull membership and depth).
                    bool possiblyOccluded = false;
                    for (int j = 0; j < n; j++) {
                        if (j == i) continue;
                        double dj = TestGeom3d.FiberDepth(s, planes[j], cam);
                        if (TestGeom3d.StrictlyInsideHull(s, hulls[j], TestGeom3d.StrictMargin)
                                && dj < di - TestGeom3d.StrictMargin) {
                            strictlyOccluded = true;
                            possiblyOccluded = true;
                            break;
                        }
                        if (TestGeom3d.InsideHullTolerant(s, hulls[j], TestGeom3d.StrictMargin)
                                && dj < di + TestGeom3d.StrictMargin)
                            possiblyOccluded = true;
                    }
                    bool surviving = TestGeom3d.InsideRegions(s, faces[i])
                                     && TestGeom3d.DistToContours(s, faces[i]) > TestGeom3d.StrictMargin;
                    bool clearlyCut = !TestGeom3d.InsideRegions(s, faces[i])
                                      && TestGeom3d.DistToContours(s, faces[i]) > TestGeom3d.StrictMargin;
                    if (strictlyOccluded && surviving)
                        violations.Add($"UNDER-CUT face {faces[i].sourceFaceId} at {Fmt(s)}");
                    if (!possiblyOccluded && clearlyCut)
                        violations.Add($"OVER-CUT face {faces[i].sourceFaceId} at {Fmt(s)}");
                }
            }
            Assert.That(violations, Is.Empty, label + "\n" + string.Join("\n", violations));
        }

        static string Fmt(Point p) =>
            string.Format(System.Globalization.CultureInfo.InvariantCulture,
                          "({0:F4}, {1:F4})", p.x[0], p.x[1]);

        static double SmoothStep(double x) {
            x = Math.Max(0.0, Math.Min(1.0, x));
            return x * x * (3.0 - 2.0 * x);
        }

        static double[] PlaneRot(int i, int j, double angle) {
            var m = (double[])PosedComplexes.IdentityRot.Clone();
            double c = Math.Cos(angle), s = Math.Sin(angle);
            m[i * 3 + i] = c; m[i * 3 + j] = -s;
            m[j * 3 + i] = s; m[j * 3 + j] = c;
            return m;
        }

        static double[] RandomUnitAxis(Random rng) {
            while (true) {
                var v = new[] {
                    rng.NextDouble() * 2 - 1, rng.NextDouble() * 2 - 1, rng.NextDouble() * 2 - 1 };
                double m = Math.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]);
                if (m > 0.1 && m <= 1) return new[] { v[0] / m, v[1] / m, v[2] / m };
            }
        }
    }
}
