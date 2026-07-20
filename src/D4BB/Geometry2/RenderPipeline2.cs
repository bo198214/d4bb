using System.Collections.Generic;
using D4BB.Geometry;

namespace D4BB.Geometry2 {

    /// Weiler-Atherton variant of RenderPipeline: identical structure (free-face
    /// wrapping, BSP build, BackToFront traversal, 3D projection, CutOut of farther
    /// cells against nearer ones), but the CutOut is CellRenderWA3d.CutOut — one
    /// polygon subtraction per (face, cutter) pair instead of one Sutherland-Hodgman
    /// peel per cutter halfspace. Partially occluded faces therefore stay single
    /// (possibly concave) polygons, and a cutter poking through a face's interior
    /// produces a proper hole ring instead of a fan of convex fragments.
    ///
    /// Two entry points:
    ///   • Process — same BSP back-to-front ordering as RenderPipeline, WA clipping only.
    ///   • ProcessPairwise — NO BSP at all: cells are ordered pairwise via their 4D
    ///     supporting hyperplanes and the farther one of each overlapping pair is cut by
    ///     the nearer one (the original Weiler-Atherton hidden-surface-removal scheme).
    public static class RenderPipeline2 {

        /// Runs the pipeline and returns the processed cells in back-to-front order,
        /// each containing only the still-visible face regions (when applyCutOut).
        public static List<CellRenderWA3d> Process(PolyhedralComplex4d complex, ICamera4d camera,
                                                   bool useBsp, bool applyCutOut, bool backfaceCulling) {
            var processedCells = new List<CellRenderWA3d>();

            // Free-floating 2-faces (no 3-cell parent): wrap each as a single-face cell at
            // the start of processedCells. They never act as cutters, but every 3-cell
            // processed afterwards gets a chance to clip them. The coplanar-with-cutter
            // case is dropped by CutOut's centroid-on-plane check (orientation undefined) —
            // the same contract as RenderPipeline.
            for (int fId = 0; fId < complex.faces.Count; fId++) {
                if (complex.CellsPerFace(fId).Count > 0) continue;
                var poly3d = new List<Point>();
                foreach (var v in complex.FaceVertices(fId)) poly3d.Add(camera.Proj3d(v));
                if (poly3d.Count < 3) continue;
                var cell = new CellRenderWA3d { sourceCellId = -1 };
                cell.faces.Add(CellRenderWA3d.MakeFaceRegion(poly3d, fId));
                processedCells.Add(cell);
            }

            if (useBsp) {
                var bsp = Bsp4d.Build(complex);
                foreach (var fragment in bsp.BackToFront(camera, cullBackfaces: backfaceCulling)) {
                    var cell3d = CellRenderWA3d.FromFragment(fragment, camera);
                    if (applyCutOut) {
                        var halfSpaces = cell3d.DefiningHalfSpaces();
                        foreach (var farCell in processedCells)
                            farCell.CutOut(halfSpaces);
                    }
                    processedCells.Add(cell3d);
                }
            } else {
                // No-BSP debug mode: natural cell order, optional backface culling,
                // no occlusion handling.
                for (int ci = 0; ci < complex.cells.Count; ci++) {
                    var cell = complex.cells[ci];
                    if (backfaceCulling && cell.normal != null) {
                        int probeVid = -1;
                        foreach (int vid in complex.CellVertexIds(ci)) { probeVid = vid; break; }
                        if (probeVid < 0) continue;
                        if (!camera.IsFacedBy(complex.vertices[probeVid], cell.normal)) continue;
                    }
                    var fragment = CellFragment.FromCell(complex, ci);
                    processedCells.Add(CellRenderWA3d.FromFragment(fragment, camera));
                }
            }
            return processedCells;
        }

        /// Convenience for boundary-based consumers (e.g. CellRender3dEdges): runs Process
        /// and flattens each cell's face regions into a CellRender3d, where hole rings
        /// become separate polygons carrying their face's faceId.
        public static List<CellRender3d> ProcessFlattened(PolyhedralComplex4d complex, ICamera4d camera,
                                                          bool useBsp, bool applyCutOut, bool backfaceCulling) {
            var cells = Process(complex, camera, useBsp, applyCutOut, backfaceCulling);
            var result = new List<CellRender3d>(cells.Count);
            foreach (var c in cells) result.Add(c.ToCellRender3d());
            return result;
        }

        /// BSP-FREE Weiler-Atherton pipeline — hidden-surface removal the way the original
        /// Weiler-Atherton algorithm works: no global depth order is built; instead every
        /// PAIR of (front-facing) cells is ordered directly and the farther one is cut by
        /// the nearer one's projected hull. Cells are never split.
        ///
        /// The pairwise order exploits that 3-cells are FLAT in 4-space (each lies in its
        /// supporting 3-hyperplane H):
        ///   • if cell B does not straddle H_A, every sight line through both meets A
        ///     exactly ON H_A, so B is nearer iff B and the camera are on the same side
        ///     of H_A — this is EXACT, not a heuristic;
        ///   • two cells in the same hyperplane can never overlap in projection (the
        ///     projection restricted to a hyperplane is affine-injective) — skip;
        ///   • if A and B mutually straddle each other's hyperplanes, neither cell's OWN
        ///     hyperplane separates the pair — but two disjoint convex cells always have SOME
        ///     separating hyperplane, so a genuine one is computed with GJK
        ///     (<see cref="ConvexSeparation"/>) and orders the pair the same way. Only cells that
        ///     truly overlap in 4D (a real degeneracy, not this straddle case) remain undecidable
        ///     and throw (fail fast); for translucent back-to-front blending still use the BSP variant.
        ///
        /// The returned list carries NO meaningful depth order: with exact HSR none is
        /// needed for opaque rendering. Translucent back-to-front blending still requires
        /// the BSP path.
        public static List<CellRenderWA3d> ProcessPairwise(PolyhedralComplex4d complex, ICamera4d camera,
                                                           bool applyCutOut, bool backfaceCulling) {
            // Free-floating 2-faces — same contract as Process; they never act as cutters,
            // so with pairwise HSR they are simply cut by every 3-cell's hull.
            var freeCells = new List<CellRenderWA3d>();
            for (int fId = 0; fId < complex.faces.Count; fId++) {
                if (complex.CellsPerFace(fId).Count > 0) continue;
                var poly3d = new List<Point>();
                foreach (var v in complex.FaceVertices(fId)) poly3d.Add(camera.Proj3d(v));
                if (poly3d.Count < 3) continue;
                var cell = new CellRenderWA3d { sourceCellId = -1 };
                cell.faces.Add(CellRenderWA3d.MakeFaceRegion(poly3d, fId));
                freeCells.Add(cell);
            }

            var frags = new List<CellFragment>();
            for (int ci = 0; ci < complex.cells.Count; ci++) {
                var cell = complex.cells[ci];
                if (backfaceCulling && cell.normal != null) {
                    int probeVid = -1;
                    foreach (int vid in complex.CellVertexIds(ci)) { probeVid = vid; break; }
                    if (probeVid < 0) continue;
                    if (!camera.IsFacedBy(complex.vertices[probeVid], cell.normal)) continue;
                }
                frags.Add(CellFragment.FromCell(complex, ci));
            }
            var cells3d = new List<CellRenderWA3d>(frags.Count);
            foreach (var f in frags) cells3d.Add(CellRenderWA3d.FromFragment(f, camera));

            if (applyCutOut) {
                int n = cells3d.Count;
                // Snapshot every cell's PRE-CUT hull and vertices: CutOut mutates the faces,
                // but a cell's shadow on cells behind it is its full projected volume even
                // where the cell is itself occluded by something nearer.
                var hulls = new HalfSpace[n][];
                var verts = new List<Point>[n];
                for (int i = 0; i < n; i++) {
                    hulls[i] = cells3d[i].DefiningHalfSpaces();
                    verts[i] = new List<Point>();
                    foreach (var face in cells3d[i].faces)
                        foreach (var v in face.outer) verts[i].Add(v);
                }
                foreach (var free in freeCells)
                    for (int j = 0; j < n; j++) free.CutOut(hulls[j]);
                for (int i = 0; i < n; i++) {
                    for (int j = i + 1; j < n; j++) {
                        // Cheap SAT skip: if a face halfspace of either projected hull
                        // separates them, the projections don't overlap — no occlusion.
                        // (Face axes only, so this may miss a separation; that is safe:
                        // the resulting cut is a no-op.)
                        if (SeparatedBy(hulls[i], verts[j]) || SeparatedBy(hulls[j], verts[i])) continue;
                        int nearer = NearerOfPair(frags[i], frags[j], camera);
                        if (nearer < 0) continue;
                        if (nearer == 0) cells3d[j].CutOut(hulls[i]);
                        else cells3d[i].CutOut(hulls[j]);
                    }
                }
            }
            freeCells.AddRange(cells3d);
            return freeCells;
        }

        /// Pairwise occlusion order of two flat convex cells (see ProcessPairwise doc):
        /// 0 = `a` is nearer, 1 = `b` is nearer, -1 = same hyperplane (cannot overlap).
        static int NearerOfPair(CellFragment a, CellFragment b, ICamera4d camera) {
            var sideB = Bsp4d.Classify(b, a.supportingHyperplane);
            if (sideB == CellSide.COINCIDENT) return -1;
            if (sideB != CellSide.STRADDLE) {
                bool camPositive = camera.IsFacedBy(
                    a.supportingHyperplane.origin(), a.supportingHyperplane.normal);
                return (sideB == CellSide.POSITIVE) == camPositive ? 1 : 0;
            }
            var sideA = Bsp4d.Classify(a, b.supportingHyperplane);
            if (sideA == CellSide.COINCIDENT) return -1;
            if (sideA != CellSide.STRADDLE) {
                bool camPositive = camera.IsFacedBy(
                    b.supportingHyperplane.origin(), b.supportingHyperplane.normal);
                return (sideA == CellSide.POSITIVE) == camPositive ? 0 : 1;
            }
            // Neither cell's OWN supporting hyperplane separates the pair (both mutually straddle) —
            // but two disjoint convex cells always have SOME separating hyperplane. Compute one with
            // GJK instead of giving up; its normal orders the pair exactly like a supporting
            // hyperplane does (one cell entirely on each side, camera-facing side is nearer).
            var vertsA = new List<Point>(a.AllVertices());
            var vertsB = new List<Point>(b.AllVertices());
            if (ConvexSeparation.TrySeparatingHyperplane(vertsA, vertsB, out var sepNormal, out var sepPoint)) {
                var hs = new HalfSpace(sepPoint, sepNormal);
                var sideBsep = Bsp4d.Classify(b, hs);
                if (sideBsep == CellSide.POSITIVE || sideBsep == CellSide.NEGATIVE) {
                    bool camPositive = camera.IsFacedBy(hs.origin(), hs.normal);
                    return (sideBsep == CellSide.POSITIVE) == camPositive ? 1 : 0;
                }
            }
            // No separating hyperplane found ⇒ the cells actually overlap in 4D (a real degeneracy,
            // not the old straddle limitation). Fail fast.
            throw new System.Exception(
                "6021837445 pairwise occlusion order undecidable: cells " + a.sourceCellId +
                " and " + b.sourceCellId + " overlap in 4D (no separating hyperplane found); " +
                "use the BSP pipeline for this geometry");
        }

        /// True iff some halfspace of `hull` has all `otherVerts` on its outside or
        /// boundary — then the two projected volumes have disjoint interiors.
        static bool SeparatedBy(HalfSpace[] hull, List<Point> otherVerts) {
            foreach (var hs in hull) {
                bool allOutside = true;
                foreach (var v in otherVerts) {
                    if (hs.side(v) < 0) { allOutside = false; break; }
                }
                if (allOutside) return true;
            }
            return false;
        }
    }
}
