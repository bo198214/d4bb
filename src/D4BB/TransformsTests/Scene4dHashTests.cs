using System.Collections.Generic;
using System.Linq;
using D4BB.Comb;
using D4BB.Geometry;
using D4BB.Transforms;
using NUnit.Framework;

namespace D4BB.Transforms {
// Tests targeting "sporadically missing 2-faces in a unit tesseract render".
// User report: faces (always inner, always going into the 4th dimension) are sometimes
// not rendered, even without piece motion. Suspicion: HashSet-related non-determinism.
public class Scene4dHashTests {

    // Convenience: build a Scene4d for a single 4-cube at origin, with the same
    // Camera4dParallel cavalier projection that GameScene4d uses by default.
    static Scene4d BuildSingleHypercubeScene() {
        var camera = new Camera4dParallel();
        var origins = new int[][][] { new int[][] { new int[] {0,0,0,0} } };
        return new Scene4d(origins, camera);
    }

    // Stable string keys derived from a Face2d's IntegerCell ‒ used to compare runs.
    static string FaceKey(Face2d f) {
        var ic = ((Face2dWithIntegerCellAttribute)f).integerCell;
        return ic.ToString();
    }

    // Sanity baseline: the cell count is what Scene4dSingleTest already pins
    // for Camera4dCentral; for the cavalier camera we just want to know it.
    [Test] public void Cavalier_SingleHypercube_VisibleFacetsCount() {
        var scene = BuildSingleHypercubeScene();
        var facets = scene.VisibleFacets(0);
        TestContext.Progress.WriteLine($"visible facets: {facets.Count}");
        TestContext.Progress.WriteLine($"cells: {scene.cells.Count}");
        foreach (var f in facets) TestContext.Progress.WriteLine($"  {FaceKey(f)}");
        Assert.That(facets.Count, Is.GreaterThan(0));
    }

    // The actual repro driver: build the scene many times in this process and
    // verify the visible-faces *set* is stable. If the bug is non-determinism
    // from HashSet iteration order, this should fail intermittently — or at
    // least produce different snapshots across processes.
    [Test] public void Cavalier_SingleHypercube_VisibleFacets_AreStableAcrossRebuilds() {
        const int N = 50;
        HashSet<string> reference = null;
        for (int run = 0; run < N; run++) {
            var scene = BuildSingleHypercubeScene();
            var keys = scene.VisibleFacets(0).Select(FaceKey).ToHashSet();
            if (reference == null) reference = keys;
            else {
                var missing = reference.Except(keys).ToList();
                var extra   = keys.Except(reference).ToList();
                if (missing.Count != 0 || extra.Count != 0) {
                    TestContext.Progress.WriteLine($"run {run}: missing={string.Join(",",missing)} extra={string.Join(",",extra)}");
                }
                Assert.That(missing, Is.Empty, $"run {run} missing faces: {string.Join(",",missing)}");
                Assert.That(extra,   Is.Empty, $"run {run} extra faces: {string.Join(",",extra)}");
            }
        }
    }

    // Drill-down: which 2-faces "go into the 4th dimension" (span contains axis 3)?
    // For a single hypercube there are 12 such faces (3 spans × 4 positions). Whether
    // they are visible after backface culling is camera-dependent, but the set must
    // not change between identical Scene4d builds.
    [Test] public void Cavalier_SingleHypercube_4dGoingFaces_AreStableAcrossRebuilds() {
        const int N = 50;
        HashSet<string> reference = null;
        for (int run = 0; run < N; run++) {
            var scene = BuildSingleHypercubeScene();
            var keys = scene.VisibleFacets(0)
                .Where(f => ((Face2dWithIntegerCellAttribute)f).integerCell.span.Contains(3))
                .Select(FaceKey)
                .ToHashSet();
            if (reference == null) {
                reference = keys;
                TestContext.Progress.WriteLine($"4d-going visible faces ({keys.Count}):");
                foreach (var k in keys.OrderBy(s => s)) TestContext.Progress.WriteLine($"  {k}");
            } else {
                Assert.That(keys, Is.EquivalentTo(reference), $"run {run} 4d-going face set differs");
            }
        }
    }

    // The IntegerCell hash function relies on origin coords ∈ 0..9 to be collision-free
    // ("unique for up to 4 dimensions and origin coordinates inside 0..9"). For a unit
    // hypercube all coordinates are 0 or 1, so collisions cannot be the explanation —
    // pin that as a sanity check.
    [Test] public void IntegerCell_Hash_UnitHypercube_NoCollisions() {
        var seen = new Dictionary<int, IntegerCell>();
        // enumerate every 2-face of the unit hypercube
        var hyper = new IntegerCell(new int[]{0,0,0,0});
        foreach (var c3 in hyper.Facets()) {
            foreach (var f2 in c3.Facets()) {
                int h = f2.GetHashCode();
                if (seen.TryGetValue(h, out var other)) {
                    Assert.That(f2.Equals(other),
                        $"hash collision: {f2} vs {other} both hash to {h}");
                }
                seen[h] = f2;
            }
        }
    }

    // Detached subtest: the no-occlusion case isolates back-face culling + topology dedup.
    [Test] public void Cavalier_SingleHypercube_NoOcclusion_VisibleFacetsCount() {
        var camera = new Camera4dParallel();
        var origins = new int[][][] { new int[][] { new int[] {0,0,0,0} } };
        var scene = new Scene4d(origins, camera);
        scene.enable4dOcclusion = false;
        scene.Update(origins);
        var facets = scene.VisibleFacets(0);
        TestContext.Progress.WriteLine($"no-occlusion visible facets: {facets.Count}");
        foreach (var f in facets) TestContext.Progress.WriteLine($"  {FaceKey(f)}");
        // The 4 front-facing 3-cells contribute 4*6 - shared = 18 unique faces.
        Assert.That(facets.Count, Is.EqualTo(18));
        var fourD = facets.Where(f => ((Face2dWithIntegerCellAttribute)f).integerCell.span.Contains(3)).Count();
        Assert.That(fourD, Is.EqualTo(9));
    }

    // With occlusion *and* without, the 4D-going visible-faces set should be identical for
    // a single hypercube. (Cells occlude each other in 4D, but the 4D-going faces of a
    // single piece span all 4 visible 3-cells without any one fully hiding another.)
    [Test] public void Cavalier_SingleHypercube_4dGoingFaces_AreSame_With_And_Without_Occlusion() {
        var camera = new Camera4dParallel();
        var origins = new int[][][] { new int[][] { new int[] {0,0,0,0} } };

        var sceneOcc = new Scene4d(origins, camera);
        var setOcc = sceneOcc.VisibleFacets(0)
            .Where(f => ((Face2dWithIntegerCellAttribute)f).integerCell.span.Contains(3))
            .Select(FaceKey).ToHashSet();

        var sceneNo = new Scene4d(origins, camera);
        sceneNo.enable4dOcclusion = false;
        sceneNo.Update(origins);
        var setNo = sceneNo.VisibleFacets(0)
            .Where(f => ((Face2dWithIntegerCellAttribute)f).integerCell.span.Contains(3))
            .Select(FaceKey).ToHashSet();

        Assert.That(setOcc, Is.EquivalentTo(setNo),
            $"occlusion changed 4d-going visible-set:\n  +{string.Join(",",setNo.Except(setOcc))}\n  -{string.Join(",",setOcc.Except(setNo))}");
    }

    // The IntegerCell-projected boundary should preserve a 1:1 correspondence between
    // the unique 2-faces and the entries in pbc.i2p. If pbc has extra Face2dBC's in d2faces
    // that aren't in i2p, the cross-piece dedup would silently miss them and a face could
    // get rendered twice or "claimed" inconsistently.
    [Test] public void Cavalier_SingleHypercube_pbc_d2faces_Match_i2p() {
        var scene = BuildSingleHypercubeScene();
        foreach (var cb in scene.cells) {
            var d2 = cb.pbc.d2faces.ToList();
            var i2p = cb.pbc.i2p;
            // every d2 entry's integerCell is unique
            var icSeen = new HashSet<IntegerCell>();
            foreach (var f in d2) {
                Assert.That(icSeen.Add(f.integerCell), Is.True,
                    $"duplicate integerCell in d2faces: {f.integerCell}");
            }
            // d2faces ↔ i2p
            Assert.That(d2.Count, Is.EqualTo(i2p.Count),
                $"d2faces({d2.Count}) != i2p({i2p.Count}) for cell {cb.cell}");
            foreach (var f in d2)
                Assert.That(i2p.ContainsKey(f.integerCell), Is.True,
                    $"d2faces face {f.integerCell} missing from i2p");
            foreach (var kvp in i2p)
                Assert.That(d2.Contains(kvp.Value), Is.True,
                    $"i2p value not in d2faces for key {kvp.Key}");
        }
    }

    // ── HYPOTHESIS: floating-point depth comparison ───────────────────────────
    // For a unit hypercube, three of the four front-facing 3-cells are mathematically
    // at the SAME view-depth — so InFrontOfViewNormalComparer should return 0 between
    // them. If floating-point summation makes their depths differ by 1 ULP, they get
    // sorted into a definite far-to-near order, and ApplyCameraOcclusion will cut
    // each of them with the others' halfspaces. The 4D-going face shared between
    // two such "cmp-different" cells lives in only one cell's d2faces (dedup), and
    // the cut will remove it from that cell's d2faces — face vanishes entirely.
    [Test] public void Cavalier_SingleHypercube_SameDepth_3Cells_Comparator_ReturnsZero() {
        var camera = new Camera4dParallel();
        // The 3 same-depth front-facing 3-cells:
        var c3a = new OrientedIntegerCell(new int[]{1,0,0,0}, new HashSet<int>{1,2,3}, false, true);
        var c3b = new OrientedIntegerCell(new int[]{0,1,0,0}, new HashSet<int>{0,2,3}, false, true);
        var c3c = new OrientedIntegerCell(new int[]{0,0,1,0}, new HashSet<int>{0,1,3}, false, true);
        var cmp = new InFrontOfViewNormalComparer(camera.viewNormal.x, reverse: true);
        TestContext.Progress.WriteLine($"viewNormal = {camera.viewNormal}");
        // Print depths to see whether floating-point produces exactly equal values
        double DepthOf(OrientedIntegerCell c) {
            var ctr = c.Center();
            double d = 0;
            for (int i = 0; i < ctr.Length; i++) d += camera.viewNormal.x[i] * ctr[i];
            return d;
        }
        var dA = DepthOf(c3a); var dB = DepthOf(c3b); var dC = DepthOf(c3c);
        TestContext.Progress.WriteLine($"depth(c3a)={dA:G17}");
        TestContext.Progress.WriteLine($"depth(c3b)={dB:G17}");
        TestContext.Progress.WriteLine($"depth(c3c)={dC:G17}");
        TestContext.Progress.WriteLine($"cmp(a,b)={cmp.Compare(c3a,c3b)} cmp(b,c)={cmp.Compare(c3b,c3c)} cmp(a,c)={cmp.Compare(c3a,c3c)}");
        Assert.That(cmp.Compare(c3a, c3b), Is.EqualTo(0), $"cmp(c3a,c3b)≠0: depths {dA:G17} vs {dB:G17}");
        Assert.That(cmp.Compare(c3b, c3c), Is.EqualTo(0), $"cmp(c3b,c3c)≠0: depths {dB:G17} vs {dC:G17}");
        Assert.That(cmp.Compare(c3a, c3c), Is.EqualTo(0), $"cmp(c3a,c3c)≠0: depths {dA:G17} vs {dC:G17}");
    }

    // The actual end-to-end repro: even if the comparator does treat them as different
    // depths, the SHARED 4D-going face must not vanish. This test must always pass —
    // any failure here is the user-reported bug.
    [Test] public void Cavalier_SingleHypercube_All_4dGoing_BoundaryFaces_AreVisible() {
        var scene = BuildSingleHypercubeScene();
        var visibleKeys = scene.VisibleFacets(0).Select(FaceKey).ToHashSet();
        // Enumerate the 9 4D-going faces that should be visible:
        // facets of front-facing c3s (a,b,c) whose span contains axis 3, dedup'd as IntegerCell.
        var expected4d = new HashSet<string> {
            "[1,0,0,0]:[1, 3]",  // span and origin formatting derived from IntegerCell.ToString
        };
        // Easier: just count visible 4D-going faces and ensure the right number.
        var fourD = scene.VisibleFacets(0)
            .Where(f => ((Face2dWithIntegerCellAttribute)f).integerCell.span.Contains(3))
            .Count();
        Assert.That(fourD, Is.EqualTo(9),
            $"expected 9 visible 4D-going faces, got {fourD}. Actual visibles:\n  " +
            string.Join("\n  ", visibleKeys));
    }

    // If the depth comparator returns nonzero for cells at "the same" mathematical depth,
    // simulate that by manually re-sorting the cells list and re-running occlusion. This
    // reproduces the bug deterministically: the mutual cut between same-depth cells
    // removes the shared 4D-going face.
    [Test] public void Cavalier_SingleHypercube_AdversarialCellsOrder_StillRendersAll4dFaces() {
        var camera = new Camera4dParallel();
        var origins = new int[][][] { new int[][] { new int[] {0,0,0,0} } };
        // Try every permutation of the 4 cells before occlusion; if the bug exists, at
        // least some permutations will trigger it.
        // We can't easily inject permutations into Scene4d.Update without modifying it.
        // Instead, run Update many times and rebuild — this exercises the natural
        // (deterministic) ordering. As an additional probe, we also disable occlusion
        // and verify the same set of 4D-going faces is reported.
        var scene = new Scene4d(origins, camera);
        var occOn  = scene.VisibleFacets(0)
            .Where(f => ((Face2dWithIntegerCellAttribute)f).integerCell.span.Contains(3))
            .Select(FaceKey).ToHashSet();
        scene.enable4dOcclusion = false;
        scene.Update(origins);
        var occOff = scene.VisibleFacets(0)
            .Where(f => ((Face2dWithIntegerCellAttribute)f).integerCell.span.Contains(3))
            .Select(FaceKey).ToHashSet();
        Assert.That(occOn, Is.EquivalentTo(occOff),
            "4D-going visibles differ between enable4dOcclusion on/off. " +
            $"Only-on: [{string.Join(",", occOn.Except(occOff))}]  " +
            $"Only-off: [{string.Join(",", occOff.Except(occOn))}]");
    }

    // Cross-check: enumerate all 24 2-faces of the unit hypercube without going through
    // 3-cells, and verify Equals/GetHashCode are mutually consistent across orientations
    // and across HashSet<int> span iteration order (which is observable in GetHashCode
    // via foreach over span — but should be commutative because the bits are summed).
    // ── Multi-cube scenes ────────────────────────────────────────────────────
    // Two 4-cubes adjacent along axis 0 in a single piece. The shared 3-cell between
    // them is interior in the same hyperplane; its face contributions must dedup.
    // The 4D-going faces on the *outside* must remain visible.
    [Test] public void Cavalier_TwoHypercubesAlongAxis0_4dGoingFaceCount() {
        var camera = new Camera4dParallel();
        var origins = new int[][][] {
            new int[][] { new int[] {0,0,0,0}, new int[] {1,0,0,0} }
        };
        var scene = new Scene4d(origins, camera);
        var visible = scene.VisibleFacets(0);
        var fourD = visible.Where(f => ((Face2dWithIntegerCellAttribute)f).integerCell.span.Contains(3)).ToList();
        TestContext.Progress.WriteLine($"two cubes: {visible.Count} visible, {fourD.Count} 4D-going");
        foreach (var f in fourD.OrderBy(f => FaceKey(f)))
            TestContext.Progress.WriteLine($"  4D: {FaceKey(f)}");
        Assert.That(visible.Count, Is.GreaterThan(0));
    }

    // Two 4-cubes stacked along axis 3. The shared 3-cell {0,1,2}-span face between
    // them is the inter-w wall — that 3-cell should disappear from the boundary.
    [Test] public void Cavalier_TwoHypercubesAlongAxis3_4dGoingFaceCount() {
        var camera = new Camera4dParallel();
        var origins = new int[][][] {
            new int[][] { new int[] {0,0,0,0}, new int[] {0,0,0,1} }
        };
        var scene = new Scene4d(origins, camera);
        var visible = scene.VisibleFacets(0);
        var fourD = visible.Where(f => ((Face2dWithIntegerCellAttribute)f).integerCell.span.Contains(3)).ToList();
        TestContext.Progress.WriteLine($"axis3 stack: {visible.Count} visible, {fourD.Count} 4D-going");
        foreach (var f in fourD.OrderBy(f => FaceKey(f)))
            TestContext.Progress.WriteLine($"  4D: {FaceKey(f)}");
        Assert.That(visible.Count, Is.GreaterThan(0));
    }

    // Two pieces sharing a face. The shared face must be rendered once (one piece keeps it
    // via the cross-piece dedup); BOTH pieces should still have all their other 4D-going faces.
    [Test] public void Cavalier_TwoPiecesSharingFace_4dGoingNotLost() {
        var camera = new Camera4dParallel();
        var origins = new int[][][] {
            new int[][] { new int[] {0,0,0,0} },
            new int[][] { new int[] {1,0,0,0} },
        };
        var scene = new Scene4d(origins, camera);
        var visible0 = scene.VisibleFacets(0);
        var visible1 = scene.VisibleFacets(1);
        var fourD0 = visible0.Where(f => ((Face2dWithIntegerCellAttribute)f).integerCell.span.Contains(3)).ToList();
        var fourD1 = visible1.Where(f => ((Face2dWithIntegerCellAttribute)f).integerCell.span.Contains(3)).ToList();
        TestContext.Progress.WriteLine($"piece0: {visible0.Count} visible, {fourD0.Count} 4D-going");
        TestContext.Progress.WriteLine($"piece1: {visible1.Count} visible, {fourD1.Count} 4D-going");

        // Sanity: each piece has its own set of 4D-going faces
        Assert.That(fourD0.Count + fourD1.Count, Is.GreaterThanOrEqualTo(9),
            "Combined 4D-going count below the single-cube count of 9 — a face seems to have vanished entirely.");
    }

    // ── Adversarial input ordering ───────────────────────────────────────────
    // If the bug is hiding behind hashset iteration order in
    // Polyhedron3dBoundaryComplex(List<Face2dBC>), permuting the input list should
    // not change which faces end up visible. Brute force all 6! permutations.
    [Test] public void Polyhedron3dBoundaryComplex_InputOrder_DoesNotAffectFaces() {
        // Build the canonical pbc from the standard cube
        var canonical = new Polyhedron3dBoundaryComplex(new int[]{0,0,0});
        var canonicalKeys = canonical.d2faces.Select(f => f.integerCell.ToString()).OrderBy(s => s).ToList();
        var canonicalI2pKeys = canonical.i2p.Keys.Select(k => k.ToString()).OrderBy(s => s).ToList();
        Assert.That(canonicalKeys.Count, Is.EqualTo(6));
        Assert.That(canonicalI2pKeys.Count, Is.EqualTo(6));
        Assert.That(canonicalKeys, Is.EquivalentTo(canonicalI2pKeys));

        // Now grab those 6 faces, build them as a list, and permute.
        var faces = canonical.d2faces.ToList();
        Assert.That(faces.Count, Is.EqualTo(6));
        // Trivial check: input order doesn't affect the output set
        for (int seed = 0; seed < 30; seed++) {
            var rnd = new System.Random(seed);
            var permuted = faces.OrderBy(_ => rnd.Next()).ToList();
            // Reconstruct: we need a fresh set of Face2dBC because the originals
            // have neighbor links wired up. For this invariant test, we only care
            // that d2faces and i2p are populated 1:1 from the same input.
            var pbc = TestHook_BuildPbc(permuted);
            var pbcKeys = pbc.d2faces.Select(f => f.integerCell.ToString()).OrderBy(s => s).ToList();
            var pbcI2pKeys = pbc.i2p.Keys.Select(k => k.ToString()).OrderBy(s => s).ToList();
            Assert.That(pbcKeys, Is.EquivalentTo(canonicalKeys), $"permutation seed {seed}: d2faces set differs");
            Assert.That(pbcI2pKeys, Is.EquivalentTo(canonicalKeys), $"permutation seed {seed}: i2p keyset differs");
            Assert.That(pbc.d2faces.Count, Is.EqualTo(6), $"permutation seed {seed}: d2faces count");
            Assert.That(pbc.i2p.Count, Is.EqualTo(6), $"permutation seed {seed}: i2p count");
        }
    }

    // The internal Polyhedron3dBoundaryComplex(List<Face2dBC>, bool) ctor is `internal`,
    // so this lives in the same assembly via InternalsVisibleTo... wait, no, the test
    // assembly references Transforms but doesn't have IVT. Use the public 3-cell-set
    // ctor which still exercises the same construction path.
    private static Polyhedron3dBoundaryComplex TestHook_BuildPbc(System.Collections.Generic.List<Face2dBC> permutedFaces) {
        // The internal ctor is reachable from the same namespace if the test assembly
        // is in the same project; here we don't have IVT, so just hammer the same
        // constructor by going through OrientedIntegerCell-set ctor:
        // We can't directly inject a permuted list. Skip this assertion path and
        // settle for asserting that the public ctor is order-stable.
        // (Reflection works too, but is fragile.)
        var ctor = typeof(Polyhedron3dBoundaryComplex).GetConstructor(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public,
            null,
            new System.Type[] { typeof(System.Collections.Generic.List<Face2dBC>), typeof(bool) },
            null);
        Assert.That(ctor, Is.Not.Null, "couldn't find Polyhedron3dBoundaryComplex(List<Face2dBC>, bool)");
        return (Polyhedron3dBoundaryComplex)ctor.Invoke(new object[] { permutedFaces, false });
    }

    // Wraps Camera4dParallel and perturbs viewNormal slightly so that the otherwise-
    // symmetric depths of c3_a/b/c become *unequal* by an ULP-or-so. Used to simulate
    // what Mono's floating-point optimizer might (or might not) do.
    private class PerturbedCamera : ICamera4d {
        private readonly Camera4dParallel inner;
        public Point4d perturbedViewNormal;
        public PerturbedCamera(Camera4dParallel c, Point4d vn) {
            inner = c; perturbedViewNormal = vn;
        }
        public Point4d eye => inner.eye;
        public Point4d viewNormal => perturbedViewNormal;
        public Point3d Proj3d(Point p) => inner.Proj3d(p);
        public bool IsFacedBy(Point origin, Point normal) => inner.IsFacedBy(origin, normal);
        public void rotate(double ph, Point a, Point b, Point c) => inner.rotate(ph, a, b, c);
    }

    // The "smoking gun" test for hypothesis A: ULP-level asymmetry in viewNormal
    // breaks the same-depth equivalence of c3_a/b/c, so InFrontOfViewNormalComparer
    // sorts them and they cut each other. The shared 4D-going face vanishes.
    //
    // If this test passes (i.e., all 9 4D-going faces remain visible despite the
    // perturbation), the depth comparator is robust. If it fails, hypothesis A is
    // confirmed and the fix in NOTES.md (toleranced comparator) is correct.
    [TestCase(1e-12)]
    [TestCase(1e-9)]
    [TestCase(1e-6)]
    [TestCase(1e-3)]
    public void Cavalier_SingleHypercube_PerturbedViewNormal_StillRendersAll4dFaces(double rel) {
        var inner = new Camera4dParallel();
        // Perturb viewNormal[0] by `rel` so depth ties of c3_a/b/c become non-ties
        var vn = new Point4d(
            inner.viewNormal.x[0] * (1 + rel),
            inner.viewNormal.x[1],
            inner.viewNormal.x[2],
            inner.viewNormal.x[3]);
        var camera = new PerturbedCamera(inner, vn);
        var origins = new int[][][] { new int[][] { new int[] {0,0,0,0} } };
        var scene = new Scene4d(origins, camera);
        var visible = scene.VisibleFacets(0);
        var fourD = visible.Where(f => ((Face2dWithIntegerCellAttribute)f).integerCell.span.Contains(3)).ToList();
        TestContext.Progress.WriteLine($"perturbed by {rel:G2}: {visible.Count} visible, {fourD.Count} 4D-going");
        foreach (var f in fourD.OrderBy(f => FaceKey(f)))
            TestContext.Progress.WriteLine($"  4D: {FaceKey(f)}");
        // For a unit hypercube, all 9 4D-going boundary faces should still be visible.
        Assert.That(fourD.Count, Is.EqualTo(9),
            $"only {fourD.Count}/9 4D-going faces visible after viewNormal perturbation by {rel:G2}.");
    }

    // ── THE ACTUAL BUG ───────────────────────────────────────────────────────
    // Scene4d.UpdateCamera() calls ApplyCameraOcclusion() WITHOUT calling
    // RebuildCellsFromTopologies() first. This means the cells' d2faces are
    // *cumulatively* cut every time UpdateCamera is called.
    //
    // PerspectiveControl.Update() (triggered when the A button is held) does a
    // double→float→double roundtrip on wDir, which silently perturbs viewNormal
    // by float-precision (~1e-7). After that, the previously-equal depths of
    // c3_a/b/c become unequal, so InFrontOfViewNormalComparer sorts them and
    // they cut each other. The 4D-going face shared between two same-depth
    // cells exists in only one of them (RebuildCellsFromPieceTopology dedup) —
    // the cut from the "newly-in-front" sibling removes that face from the cell
    // that owned it, and the face vanishes entirely.
    [Test] public void Cavalier_SingleHypercube_RepeatedUpdateCamera_KeepsAll4dFaces() {
        var camera = new Camera4dParallel();
        var origins = new int[][][] { new int[][] { new int[] {0,0,0,0} } };
        var scene = new Scene4d(origins, camera);
        var initial4d = scene.VisibleFacets(0)
            .Where(f => ((Face2dWithIntegerCellAttribute)f).integerCell.span.Contains(3))
            .Select(FaceKey).ToHashSet();
        Assert.That(initial4d.Count, Is.EqualTo(9), "baseline 4D-going count must be 9");

        // Mimic PerspectiveControl.Update: a no-op camera nudge that goes through float
        // (this is what Vector3 in Unity does to wDir each frame the A button is held).
        for (int frame = 0; frame < 10; frame++) {
            var wDir = camera.wDir;
            var asFloat = new UnityEngine_Vector3((float)wDir.x[0], (float)wDir.x[1], (float)wDir.x[2]);
            camera.wDir = new Point3d(asFloat.x, asFloat.y, asFloat.z); // triggers SetCavalier
            scene.UpdateCamera();
            var now4d = scene.VisibleFacets(0)
                .Where(f => ((Face2dWithIntegerCellAttribute)f).integerCell.span.Contains(3))
                .Select(FaceKey).ToHashSet();
            var lost = initial4d.Except(now4d).ToList();
            TestContext.Progress.WriteLine($"frame {frame}: 4d-count={now4d.Count}, lost so far: [{string.Join(",", lost)}]");
            Assert.That(now4d, Is.EquivalentTo(initial4d),
                $"frame {frame}: 4D-going faces vanished after UpdateCamera. Lost: {string.Join(",", lost)}");
        }
    }

    // The minimal repro: UpdateCamera with the camera unchanged at all. If ApplyCameraOcclusion
    // is *cumulative* (cuts d2faces in place rather than rebuilding), repeated calls will
    // erode d2faces even without any camera motion at all.
    [Test] public void Cavalier_SingleHypercube_UpdateCamera_NoCameraChange_IsIdempotent() {
        var camera = new Camera4dParallel();
        var origins = new int[][][] { new int[][] { new int[] {0,0,0,0} } };
        var scene = new Scene4d(origins, camera);
        var initial = scene.VisibleFacets(0).Select(FaceKey).ToHashSet();
        TestContext.Progress.WriteLine($"initial visible count: {initial.Count}");

        for (int frame = 0; frame < 5; frame++) {
            scene.UpdateCamera(); // no camera change at all
            var now = scene.VisibleFacets(0).Select(FaceKey).ToHashSet();
            var lost = initial.Except(now).ToList();
            TestContext.Progress.WriteLine($"frame {frame}: visible={now.Count} lost=[{string.Join(",", lost)}]");
            Assert.That(now, Is.EquivalentTo(initial),
                $"frame {frame}: visible facets changed after no-op UpdateCamera. Lost: {string.Join(",", lost)}");
        }
    }

    // Asymmetric controller noise: every frame nudges wDir by a tiny non-symmetric amount,
    // mimicking real controller noise. This is what triggers the bug in the live game.
    [Test] public void Cavalier_SingleHypercube_AsymmetricCameraNudge_KeepsAll4dFaces() {
        var camera = new Camera4dParallel();
        var origins = new int[][][] { new int[][] { new int[] {0,0,0,0} } };
        var scene = new Scene4d(origins, camera);
        var initial4d = scene.VisibleFacets(0)
            .Where(f => ((Face2dWithIntegerCellAttribute)f).integerCell.span.Contains(3))
            .Select(FaceKey).ToHashSet();
        Assert.That(initial4d.Count, Is.EqualTo(9));

        var rnd = new System.Random(7);
        for (int frame = 0; frame < 30; frame++) {
            // Tiny asymmetric jitter on each axis, much smaller than face geometry
            var w = camera.wDir;
            camera.wDir = new Point3d(
                w.x[0] + (rnd.NextDouble() - 0.5) * 1e-6,
                w.x[1] + (rnd.NextDouble() - 0.5) * 1e-6,
                w.x[2] + (rnd.NextDouble() - 0.5) * 1e-6);
            scene.UpdateCamera();
            var now4d = scene.VisibleFacets(0)
                .Where(f => ((Face2dWithIntegerCellAttribute)f).integerCell.span.Contains(3))
                .Select(FaceKey).ToHashSet();
            var lost = initial4d.Except(now4d).ToList();
            if (lost.Count > 0)
                TestContext.Progress.WriteLine($"frame {frame}: lost [{string.Join(",", lost)}]");
            Assert.That(now4d, Is.EquivalentTo(initial4d),
                $"frame {frame}: 4D-going face vanished after asymmetric jitter. Lost: {string.Join(",", lost)}");
        }
    }

    // Stand-in for UnityEngine.Vector3, which we can't reference here.
    private struct UnityEngine_Vector3 {
        public float x, y, z;
        public UnityEngine_Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
    }

    // Regression: across many asymmetric wDir magnitudes (covering the formerly-fragile
    // Goldilocks zone), all 9 4D-going faces remain visible. With CutOut's interiorOnly
    // semantics (default), shared boundary faces between cells are not lost regardless
    // of the camera-induced depth ordering.
    [TestCase(1e-7)]
    [TestCase(1e-6)]
    [TestCase(1e-5)]
    [TestCase(1e-4)]
    [TestCase(1e-3)]
    [TestCase(1e-2)]
    public void Cavalier_SingleHypercube_AsymmetricWDir_Keeps4dFaces(double eps) {
        var camera = new Camera4dParallel();
        camera.wDir = new Point3d(camera.wDir.x[0] + eps, camera.wDir.x[1], camera.wDir.x[2]);
        var origins = new int[][][] { new int[][] { new int[] {0,0,0,0} } };
        var scene = new Scene4d(origins, camera);
        var fourD = scene.VisibleFacets(0)
            .Where(f => ((Face2dWithIntegerCellAttribute)f).integerCell.span.Contains(3))
            .Count();
        Assert.That(fourD, Is.EqualTo(9), $"eps={eps:G2}: lost shared 4D-going faces");
    }

    // Demonstrates that the bug is NOT an epsilon problem. With a non-symmetric wDir
    // (e.g. user rotated the camera by some non-trivial angle), c3_a/b/c are at
    // genuinely different depths, the comparator correctly orders them, and the back
    // cell's shared 4D-going face is still cut even though both cells are part of the
    // same single hypercube and the face is on the surface of the projected hypercube.
    [TestCase(0.1, 0.0, 0.0)]   // skew x by 10%
    [TestCase(0.5, 0.0, 0.0)]   // bigger skew
    [TestCase(0.0, 0.3, 0.7)]   // y/z asymmetric
    [TestCase(2.0, 1.5, 1.0)]   // arbitrary non-symmetric camera
    public void Cavalier_SingleHypercube_NonSymmetricCamera_ShouldKeepAll4dFaces(double dx, double dy, double dz) {
        var camera = new Camera4dParallel();
        var w = camera.wDir;
        camera.wDir = new Point3d(w.x[0] + dx, w.x[1] + dy, w.x[2] + dz);
        var origins = new int[][][] { new int[][] { new int[] {0,0,0,0} } };
        var scene = new Scene4d(origins, camera);
        var fourD = scene.VisibleFacets(0)
            .Where(f => ((Face2dWithIntegerCellAttribute)f).integerCell.span.Contains(3))
            .Select(FaceKey).ToHashSet();
        TestContext.Progress.WriteLine($"wDir+={dx},{dy},{dz}: {fourD.Count} 4D-going visible");
        foreach (var k in fourD.OrderBy(s => s)) TestContext.Progress.WriteLine($"  {k}");
        // For a non-symmetric camera the depths of c3_a/b/c are genuinely different —
        // no comparator-tolerance fix can paper over this. The shared 4D-going face
        // F between c3_a and c3_b lies *on* the boundary of one of them and gets cut
        // by ClipConvexPolygon's `cs <= 0` (boundary-as-inside) rule.
        Assert.That(fourD.Count, Is.EqualTo(9),
            $"With non-symmetric wDir+={dx},{dy},{dz}, shared 4D-going faces are cut. " +
            "This proves the bug is fundamental to CutOut's boundary semantics, not a float-precision issue.");
    }

    // ── TwoTesseracts: occluding pieces overlap in 3D projection ─────────────────
    // Two single-hypercube pieces at non-overlapping 4D positions whose 3D-cavalier
    // projections overlap. Piece 0 (w=0..1) is closer; piece 1 (w=2..3) is farther.
    // The originally-reported bug: parts of piece 1's faces remained visible inside
    // piece 0's projected volume (occlusion failed). Verified via the union-of-closer-
    // cells halfspace test below.
    [Test] public void TwoTesseracts_NoFaceFragmentInsideUnionOfCloserOtherPieceCells() {
        var camera = new Camera4dParallel();
        var origins = new int[][][] {
            new int[][] { new int[] {0,0,0,0} },
            new int[][] { new int[] {-1,-1,0,2} },
        };
        var scene = new Scene4d(origins, camera);

        double DepthOf(IntegerCell c) {
            var ctr = c.Center();
            double d = 0;
            for (int i = 0; i < camera.viewNormal.x.Length; i++) d += camera.viewNormal.x[i] * ctr[i];
            return d;
        }

        bool VertexInsideOrOnBoundary(Point pt, HalfSpace[] halfSpaces) {
            foreach (var hs in halfSpaces)
                if (hs.side(pt) > 0) return false;
            return true;
        }

        // For every visible face, if every vertex lies inside the union of *other-piece*
        // closer cells, the face fragment is hidden — should have been cut by occlusion.
        var problems = new List<string>();
        for (int p = 0; p < scene.cells.Count; p++) {
            var cellP = scene.cells[p];
            var depthP = DepthOf(cellP.cell);
            var closerHs = new List<HalfSpace[]>();
            for (int q = 0; q < scene.cells.Count; q++) {
                if (q == p) continue;
                // Only other-piece cells count as occluders here. Same-piece sibling
                // cells legitimately share boundaries with this face.
                if (scene.cells[q].pieceIndex == cellP.pieceIndex) continue;
                if (DepthOf(scene.cells[q].cell) >= depthP) continue;
                closerHs.Add(Scene4d.DefiningHalfSpaces((OrientedIntegerCell)scene.cells[q].cell, camera));
            }
            if (closerHs.Count == 0) continue;

            foreach (var face in cellP.pbc.d2faces) {
                bool hidden = true;
                foreach (var pt in face.points) {
                    bool insideAny = false;
                    foreach (var hs in closerHs)
                        if (VertexInsideOrOnBoundary(pt, hs)) { insideAny = true; break; }
                    if (!insideAny) { hidden = false; break; }
                }
                if (hidden) {
                    var pts = string.Join(" ", face.points.Select(pt => $"({pt.x[0]:F2},{pt.x[1]:F2},{pt.x[2]:F2})"));
                    problems.Add($"{face.integerCell} (piece {cellP.pieceIndex}) hidden by closer other-piece cells; pts: {pts}");
                }
            }
        }
        Assert.That(problems, Is.Empty,
            "Some visible face fragment is hidden by a closer occluding piece — should have been cut. " +
            string.Join("; ", problems));
    }

    // ── 3DBox level: 3x3x3-minus-center self-occlusion ─────────────────────────
    // Piece 0 is a 3x3x3 cube of unit hypercubes (all at w=0) with the center cube at
    // (1,1,1,0) removed → 26 hypercubes. The boundary contains 6 inward-facing 3-cells
    // around the hole. The user reports artifacts in the hole region.
    static int[][] Build3DBoxPiece0() {
        var pieces = new List<int[]>();
        for (int x = 0; x < 3; x++)
        for (int y = 0; y < 3; y++)
        for (int z = 0; z < 3; z++) {
            if (x == 1 && y == 1 && z == 1) continue; // hole
            pieces.Add(new int[] {x, y, z, 0});
        }
        return pieces.ToArray();
    }

    // ── 3DBox: self-occlusion of a 3x3x3-minus-center piece ─────────────────────
    // After CutOut, the inner-hole region in 3D should show the 3 inner walls (front-
    // facing; the 3 back walls are correctly back-face-culled). One specific face — the
    // w=1 face of the -z-facing inner-hole cell — is supposed to be fully cut by the
    // surrounding w=0 face cells (they form a frame around it in 3D), leaving only the
    // inner-corner quadrant visible.
    //
    // Symptom (the test enforces): no cell may contain two fragments of the same
    // integerCell. Pre-fix the diagonally-opposite corner quadrant survives as a second
    // fragment of the same integerCell, visible as an artifact in the hole.
    //
    // Root cause (current understanding — see Scene4dHashTests.NOTES.md):
    // RebuildCellsFromPieceTopology used to add a 3-cell c3 to `cells` only if c3 owned at
    // least one 2-face after dedup. The diagonal-corner cell (e.g. wi=0 face of (2,2,1,0))
    // owned no 2-faces — its f2's were claimed by sibling c3's in other hyperplanes — so
    // its halfspaces were missing from ApplyCameraOcclusion and the diagonal quadrant
    // (which lies strictly inside that cell's 3D volume) was not cut.
    [Test] public void Box3D_NoDuplicateFaceFragmentsInSameCell() {
        var camera = new Camera4dParallel();
        var origins = new int[][][] { Build3DBoxPiece0(), new int[][] { new int[] {4,1,1,0} } };
        var scene = new Scene4d(origins, camera);

        var problems = new List<string>();
        foreach (var cb in scene.cells) {
            var dups = cb.pbc.d2faces.GroupBy(f => f.integerCell.ToString()).Where(g => g.Count() > 1);
            foreach (var g in dups) {
                problems.Add($"cell {cb.cell}: {g.Count()} fragments share integerCell {g.Key}");
                foreach (var f in g) {
                    var pts = string.Join(" ", f.points.Select(p => $"({p.x[0]:F3},{p.x[1]:F3},{p.x[2]:F3})"));
                    problems.Add($"    pts: {pts}");
                }
            }
        }
        Assert.That(problems, Is.Empty, string.Join("\n  ", problems));
    }

    [Test] public void IntegerCell_Hash_Equals_AreConsistent_Across_SpanOrders() {
        var rnd = new System.Random(12345);
        // For each 2-face, build it with HashSet<int> spans inserted in different orders
        for (int s = 0; s < 1<<4; s++) {
            // pick spans of size 2
            var bits = new List<int>();
            for (int i=0;i<4;i++) if ((s & (1<<i))!=0) bits.Add(i);
            if (bits.Count != 2) continue;
            for (int o0=0;o0<2;o0++)
            for (int o1=0;o1<2;o1++)
            for (int o2=0;o2<2;o2++)
            for (int o3=0;o3<2;o3++) {
                var origin = new int[]{o0,o1,o2,o3};
                var spanA = new HashSet<int>{bits[0], bits[1]};
                var spanB = new HashSet<int>{bits[1], bits[0]};
                // also try the rehashed-from-scratch variants
                var rebuiltA = new HashSet<int>(spanA);
                var rebuiltB = new HashSet<int>(spanA.OrderBy(x => rnd.Next()));
                var icA = new IntegerCell(origin, spanA);
                var icB = new IntegerCell(origin, spanB);
                var icRebuiltA = new IntegerCell(origin, rebuiltA);
                var icRebuiltB = new IntegerCell(origin, rebuiltB);
                Assert.That(icA.Equals(icB),       Is.True);
                Assert.That(icA.GetHashCode(),     Is.EqualTo(icB.GetHashCode()));
                Assert.That(icA.GetHashCode(),     Is.EqualTo(icRebuiltA.GetHashCode()));
                Assert.That(icA.GetHashCode(),     Is.EqualTo(icRebuiltB.GetHashCode()));
            }
        }
    }
}
}
