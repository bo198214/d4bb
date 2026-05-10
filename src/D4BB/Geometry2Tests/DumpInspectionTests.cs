using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using D4BB.Geometry;
using D4BB.Geometry2;

namespace D4BB.Geometry2Tests {
    /// One-shot inspection of dumps/dump_L_20260510_130515.json to localize the user-reported
    /// "looks wrong" constellation in the 20-cell L. Runs the same checks RotatingComplex's
    /// useBsp=true + applyCutOut=true path performs every frame.
    public class DumpInspectionTests {

        static string FindDump(string fileName) {
            var assemblyDir = Path.GetDirectoryName(typeof(DumpInspectionTests).Assembly.Location);
            var d = new DirectoryInfo(assemblyDir);
            while (d != null) {
                var p = Path.Combine(d.FullName, "dumps", fileName);
                if (File.Exists(p)) return p;
                d = d.Parent;
            }
            return null;
        }

        [Test] public void Dump_L_20260510_130515_Inspect() {
            var path = FindDump("dump_L_20260510_130515.json");
            if (path == null) { Assert.Ignore("dump file not found"); return; }
            var dump = PolyhedralComplexDumpJson.FromJson(File.ReadAllText(path));
            var c = dump.complex;
            var cam = dump.camera;

            // 1. Front-cell count
            int front = 0;
            for (int i = 0; i < c.cells.Count; i++) {
                var cell = c.cells[i];
                if (cell.normal == null) { front++; continue; }
                var probe = c.vertices[c.CellVertexIds(i).First()];
                if (cam.IsFacedBy(probe, cell.normal)) front++;
            }
            TestContext.WriteLine($"front-facing cells: {front}/{c.cells.Count}");

            // 2. Build BSP, walk back-to-front, count fragments produced
            var bsp = Bsp4d.Build(c);
            int totalFragments = 0;
            void Walk(BspNode n) {
                if (n == null) return;
                totalFragments += n.coincident.Count;
                Walk(n.positive); Walk(n.negative);
            }
            Walk(bsp.root);
            TestContext.WriteLine($"BSP total fragments (= cells + cell-splits): {totalFragments}, source cells: {c.cells.Count}");

            // 3. Replay the BSP back-to-front + CutOut pipeline (mirrors ComplexFrame.Compute)
            var processed = new List<CellRender3d>();
            int totalCutOutDrops = 0;
            int totalCutOutShrinks = 0;
            foreach (var fragment in bsp.BackToFront(cam, cullBackfaces: dump.backfaceCulling)) {
                var cell3d = CellRender3d.FromFragment(fragment, cam);
                int facesBefore = cell3d.faces.Count;
                int vertsBefore = cell3d.faces.Sum(f => f.Count);
                if (dump.applyCutOut) {
                    var halfSpaces = cell3d.DefiningHalfSpaces();
                    foreach (var farCell in processed) {
                        int fb = farCell.faces.Count;
                        int vb = farCell.faces.Sum(f => f.Count);
                        farCell.CutOut(halfSpaces);
                        if (farCell.faces.Count == 0 && fb > 0) totalCutOutDrops++;
                        if (farCell.faces.Sum(f => f.Count) < vb) totalCutOutShrinks++;
                    }
                }
                processed.Add(cell3d);
            }
            int finallyEmpty = processed.Count(c2 => c2.faces.Count == 0);
            TestContext.WriteLine($"processed cells: {processed.Count}, finally empty: {finallyEmpty}, cut drops: {totalCutOutDrops}, cut shrinks: {totalCutOutShrinks}");

            // 4. After all CutOuts, no two surviving cells should have overlapping interiors
            var overlaps = new List<(int, int)>();
            for (int i = 0; i < processed.Count; i++) {
                if (processed[i].faces.Count == 0) continue;
                for (int j = i + 1; j < processed.Count; j++) {
                    if (processed[j].faces.Count == 0) continue;
                    if (ProjectionsOverlap(processed[i], processed[j]))
                        overlaps.Add((processed[i].sourceCellId, processed[j].sourceCellId));
                }
            }
            TestContext.WriteLine($"overlapping pairs after CutOut: {overlaps.Count}");
            foreach (var p in overlaps.Take(10))
                TestContext.WriteLine($"  ({p.Item1}, {p.Item2})");

            // 5. Same check on raw front-cells (no BSP/CutOut), as a baseline.
            // For NON-convex L this is expected to be > 0; the BSP+CutOut path is required.
            var fronts = new List<CellRender3d>();
            var frontIds = new List<int>();
            for (int i = 0; i < c.cells.Count; i++) {
                var cell = c.cells[i];
                if (cell.normal != null) {
                    var probe = c.vertices[c.CellVertexIds(i).First()];
                    if (!cam.IsFacedBy(probe, cell.normal)) continue;
                }
                fronts.Add(CellRender3d.FromFragment(CellFragment.FromCell(c, i), cam));
                frontIds.Add(i);
            }
            TestContext.WriteLine($"front-facing cell ids: [{string.Join(",", frontIds)}]");
            for (int i = 0; i < fronts.Count; i++)
                for (int j = i + 1; j < fronts.Count; j++)
                    if (ProjectionsOverlap(fronts[i], fronts[j]))
                        TestContext.WriteLine($"raw overlap: cell {frontIds[i]} ↔ cell {frontIds[j]}");

            // 6. Per-cell breakdown: for each front-facing cell, count its source 2-faces and
            // how many are filtered by IsCoplanarFace (= internal seams to a sibling cell in
            // the same 3-hyperplane). For the L-block these are flat regions of the surface;
            // the renderer's BuildFacesMesh skips them.
            TestContext.WriteLine("front-cell face filter breakdown (id : total / coplanar-skipped):");
            foreach (int cId in frontIds) {
                var cell = c.cells[cId];
                int total = cell.faceIds.Length;
                int skipped = 0;
                var skippedDetails = new List<int>();
                foreach (int fId in cell.faceIds) {
                    if (c.IsCoplanarFace(fId)) { skipped++; skippedDetails.Add(fId); }
                }
                TestContext.WriteLine($"  cell {cId}: {total} faces, {skipped} skipped (coplanar) — face ids: [{string.Join(",", skippedDetails)}]");
            }

            // 7. Identify each coplanar-skipped face's two parent cells and their hyperplane
            //    normals. If both parents are front-facing, this skipped face IS visible-region
            //    (interior to a flat front surface — skipping is correct for flat continuity).
            //    If exactly one is front-facing, the skip MAY hide a real boundary (the other
            //    parent is back-facing; the face is on the silhouette in 3D).
            TestContext.WriteLine("coplanar faces with mixed front/back parents (potential silhouette skips):");
            var frontSet = new HashSet<int>(frontIds);
            int suspicious = 0;
            for (int fId = 0; fId < c.faces.Count; fId++) {
                if (!c.IsCoplanarFace(fId)) continue;
                var owners = c.CellsPerFace(fId);
                int frontCount = owners.Count(o => frontSet.Contains(o));
                if (frontCount == 1) {
                    suspicious++;
                    TestContext.WriteLine($"  face {fId}: owners=[{string.Join(",", owners)}] — front: {owners.First(o => frontSet.Contains(o))}, back: {owners.First(o => !frontSet.Contains(o))}");
                }
            }
            TestContext.WriteLine($"total suspicious skipped faces (front+back parents): {suspicious}");

            // 8. After BSP+CutOut: per source cell, which source 2-faces survive (= will be
            //    rendered, modulo IsCoplanarFace). Compare with the original face set to spot
            //    faces fully clipped by CutOut. The renderer additionally filters
            //    IsCoplanarFace, but here we want to see what CutOut leaves behind.
            TestContext.WriteLine("post-CutOut surviving source-face-ids per source cell (after BSP+CutOut):");
            var byCell = new Dictionary<int, HashSet<int>>();
            foreach (var processed_cell in processed) {
                if (!byCell.ContainsKey(processed_cell.sourceCellId))
                    byCell[processed_cell.sourceCellId] = new HashSet<int>();
                for (int p = 0; p < processed_cell.faces.Count; p++) {
                    int srcFaceId = processed_cell.faceIds != null && p < processed_cell.faceIds.Count
                        ? processed_cell.faceIds[p] : -1;
                    if (processed_cell.faces[p].Count >= 3)
                        byCell[processed_cell.sourceCellId].Add(srcFaceId);
                }
            }
            foreach (var kv in byCell.OrderBy(k => k.Key)) {
                int cellId = kv.Key;
                var surviving = kv.Value;
                var orig = new HashSet<int>(c.cells[cellId].faceIds);
                var lost = orig.Except(surviving).Except(new[] { -1 }).Where(f => !c.IsCoplanarFace(f)).ToList();
                if (lost.Count > 0)
                    TestContext.WriteLine($"  cell {cellId}: lost faces (post-CutOut, non-coplanar): [{string.Join(",", lost)}]");
                else
                    TestContext.WriteLine($"  cell {cellId}: all non-coplanar faces survive");
            }

            // 9. For cells involved in raw overlaps (11, 14, 15): show their 4D vertex coords
            //    and 3D-projected centroids, to make it easy to map to the user's visual.
            // 9b. Same pipeline with applyCutOut=false: how many faces survive?
            //     (For non-convex polychora rendered as glass, paint-order suffices and
            //     applyCutOut would otherwise discard faces hidden behind nearer cells.)
            var processedNoCut = new List<CellRender3d>();
            foreach (var fragment in bsp.BackToFront(cam, cullBackfaces: dump.backfaceCulling)) {
                processedNoCut.Add(CellRender3d.FromFragment(fragment, cam));
            }
            TestContext.WriteLine("--- comparison: applyCutOut=false ---");
            var byCellNoCut = new Dictionary<int, HashSet<int>>();
            foreach (var p in processedNoCut) {
                if (!byCellNoCut.ContainsKey(p.sourceCellId))
                    byCellNoCut[p.sourceCellId] = new HashSet<int>();
                for (int q = 0; q < p.faces.Count; q++) {
                    int srcFaceId = p.faceIds != null && q < p.faceIds.Count ? p.faceIds[q] : -1;
                    if (p.faces[q].Count >= 3) byCellNoCut[p.sourceCellId].Add(srcFaceId);
                }
            }
            foreach (var kv in byCell.OrderBy(k => k.Key)) {
                int cellId = kv.Key;
                var cutSet = kv.Value;
                var noCutSet = byCellNoCut[cellId];
                var nowGone = noCutSet.Except(cutSet).Where(f => f >= 0 && !c.IsCoplanarFace(f)).ToList();
                if (nowGone.Count > 0)
                    TestContext.WriteLine($"  cell {cellId}: applyCutOut=true loses faces [{string.Join(",", nowGone)}] vs applyCutOut=false");
            }

            // Isolated trace: cell 11 alone vs cell 14's halfspaces. If face 24 survives
            // here but not in the full BSP run, the issue is in interaction between cells.
            // If it dies here too, the bug is in CellRender3d.CutOut for this specific pair.
            TestContext.WriteLine("--- isolated cell 11 vs cell 14 ---");
            var iso11 = CellRender3d.FromFragment(CellFragment.FromCell(c, 11), cam);
            var iso14 = CellRender3d.FromFragment(CellFragment.FromCell(c, 14), cam);
            var face24Idx = -1;
            for (int p = 0; p < iso11.faces.Count; p++)
                if (iso11.faceIds[p] == 24) face24Idx = p;
            TestContext.WriteLine($"  face 24 vertices in cell 11 (3D): " +
                string.Join(" ", iso11.faces[face24Idx].Select(v => $"({v.x[0]:F3},{v.x[1]:F3},{v.x[2]:F3})")));
            var c14hs = iso14.DefiningHalfSpaces();
            for (int hi = 0; hi < c14hs.Length; hi++) {
                var sides = iso11.faces[face24Idx].Select(v => c14hs[hi].side(v)).ToList();
                TestContext.WriteLine($"  vs c14 halfspace {hi}: vertex sides=[{string.Join(",", sides)}]");
            }
            int face24Polygons = iso11.faces.Where((_, i) => iso11.faceIds[i] == 24).Count();
            iso11.CutOut(c14hs);
            int face24PolygonsAfter = iso11.faces.Where((_, i) => iso11.faceIds[i] == 24).Count();
            TestContext.WriteLine($"  face 24 polygons before isolated cut: {face24Polygons}, after: {face24PolygonsAfter}");

            // Full-sequence trace: cell 11 is processed somewhere in BSP order. After that,
            // every subsequently processed cell's halfspaces clip cell 11. Track face 24
            // through this sequence to find where it dies.
            TestContext.WriteLine("--- full-sequence trace: face 24 of cell 11 through CutOut chain ---");
            CellRender3d cellRebuilt = null;
            int seqStep = 0;
            foreach (var fragment in bsp.BackToFront(cam, cullBackfaces: dump.backfaceCulling)) {
                if (fragment.sourceCellId == 11) {
                    cellRebuilt = CellRender3d.FromFragment(fragment, cam);
                    int polys = cellRebuilt.faces.Where((_, i) => cellRebuilt.faceIds[i] == 24).Count();
                    TestContext.WriteLine($"  step {seqStep}: cell 11 enters processed list with face 24 polys={polys}");
                    seqStep++;
                    continue;
                }
                if (cellRebuilt == null) { seqStep++; continue; }
                var cutter = CellRender3d.FromFragment(fragment, cam);
                var cutterHs = cutter.DefiningHalfSpaces();
                int polysBefore = cellRebuilt.faces.Where((_, i) => cellRebuilt.faceIds[i] == 24).Count();
                int totalVerticesBefore = cellRebuilt.faces
                    .Where((_, i) => cellRebuilt.faceIds[i] == 24)
                    .Sum(f => f.Count);
                cellRebuilt.CutOut(cutterHs);
                int polysAfter = cellRebuilt.faces.Where((_, i) => cellRebuilt.faceIds[i] == 24).Count();
                int totalVerticesAfter = cellRebuilt.faces
                    .Where((_, i) => cellRebuilt.faceIds[i] == 24)
                    .Sum(f => f.Count);
                // Always log face 24's polygons at each step, plus cell centroid
                var face24Polys = cellRebuilt.faces.Select((f, idx) => (f, idx)).Where(t => cellRebuilt.faceIds[t.idx] == 24).ToList();
                var ctr = cellRebuilt.Centroid3d();
                TestContext.WriteLine($"  step {seqStep} (cutter={cutter.sourceCellId}): face 24 polys={face24Polys.Count}, cell11 centroid=({ctr.x[0]:F3},{ctr.x[1]:F3},{ctr.x[2]:F3})");
                foreach (var (f, idx) in face24Polys) {
                    TestContext.WriteLine($"    poly: {string.Join(" ", f.Select(v => $"({v.x[0]:F3},{v.x[1]:F3},{v.x[2]:F3})"))}");
                }
                seqStep++;
            }

            TestContext.WriteLine("--- targeted trace: cell 11 vs in-plane clipping ---");
            var processedTrace = new List<CellRender3d>();
            int targetCellId = 11;
            foreach (var fragment in bsp.BackToFront(cam, cullBackfaces: dump.backfaceCulling)) {
                var cell3d = CellRender3d.FromFragment(fragment, cam);
                var halfSpaces = cell3d.DefiningHalfSpaces();
                if (dump.applyCutOut) {
                    foreach (var farCell in processedTrace) {
                        if (farCell.sourceCellId != targetCellId) continue;
                        // Log any face of cell 11 that lies entirely on one of cell3d's halfspace planes
                        for (int p = 0; p < farCell.faces.Count; p++) {
                            var face = farCell.faces[p];
                            int srcId = farCell.faceIds != null && p < farCell.faceIds.Count ? farCell.faceIds[p] : -1;
                            for (int hi = 0; hi < halfSpaces.Length; hi++) {
                                var hs = halfSpaces[hi];
                                bool onPlane = face.All(v => hs.side(v) == 0);
                                if (!onPlane) continue;
                                // Compute face outward normal (away from cell 11's centroid)
                                var c11 = farCell.Centroid3d();
                                Point a = face[0], b = null;
                                for (int i = 1; i < face.Count; i++)
                                    if (face[i].clone().subtract(a).len() > AOP.ERR) { b = face[i]; break; }
                                Point cIn = null;
                                for (int i = 1; i < face.Count; i++) {
                                    if (ReferenceEquals(face[i], b)) continue;
                                    var ab = b.clone().subtract(a);
                                    var ap = face[i].clone().subtract(a);
                                    if (AOP.cross(ab, ap).len() > AOP.ERR) { cIn = face[i]; break; }
                                }
                                if (cIn == null) continue;
                                var fNormal = AOP.cross(b.clone().subtract(a), cIn.clone().subtract(a));
                                if (fNormal.sc(c11.clone().subtract(a)) > 0) fNormal.multiply(-1);
                                double dot = fNormal.sc(hs.normal);
                                string verdict = dot > 0 ? "co-oriented → DROP face" : "counter-oriented → KEEP face";
                                TestContext.WriteLine($"  cutter cell {cell3d.sourceCellId}, halfspace {hi}: face {srcId} of cell 11 on plane → outward·hsNormal={dot:F3} → {verdict}");
                            }
                        }
                    }
                    foreach (var farCell in processedTrace) farCell.CutOut(halfSpaces);
                }
                processedTrace.Add(cell3d);
            }

            TestContext.WriteLine("vertex info for overlap-involved cells:");
            foreach (int cId in new[] { 2, 3, 11, 14, 15 }) {
                var vIds = c.CellVertexIds(cId).ToList();
                var c3d = CellRender3d.FromFragment(CellFragment.FromCell(c, cId), cam).Centroid3d();
                var n = c.cells[cId].normal;
                TestContext.WriteLine($"  cell {cId}: 3D-centroid=({c3d.x[0]:F3},{c3d.x[1]:F3},{c3d.x[2]:F3}), 4D-normal=({n.x[0]:F3},{n.x[1]:F3},{n.x[2]:F3},{n.x[3]:F3})");
            }

            // 10. For each lost face, inspect its 4D centroid + which cells it touches in
            //     the original complex, plus whether it's actually "behind" a front cell or
            //     genuinely a concavity wall that should NOT be clipped.
            TestContext.WriteLine("--- detailed inspection of lost faces ---");
            int[] lostFaces = { 7, 13, 8, 24 };
            foreach (int fId in lostFaces) {
                var vIds = c.FaceVertexIds(fId);
                var pts4d = vIds.Select(v => c.vertices[v]).ToList();
                var pts3d = pts4d.Select(p => cam.Proj3d(p)).ToList();
                var c4d = new Point(4);
                foreach (var p in pts4d) c4d.add(p);
                c4d.multiply(1.0 / pts4d.Count);
                var c3df = new Point(3);
                foreach (var p in pts3d) c3df.add(p);
                c3df.multiply(1.0 / pts3d.Count);
                var owners = c.CellsPerFace(fId);
                TestContext.WriteLine($"  face {fId}: parents={string.Join(",", owners)} " +
                    $"(front={string.Join(",", owners.Where(o => frontSet.Contains(o)))}, " +
                    $"back={string.Join(",", owners.Where(o => !frontSet.Contains(o)))})");
                TestContext.WriteLine($"           4D-centroid=({c4d.x[0]:F3},{c4d.x[1]:F3},{c4d.x[2]:F3},{c4d.x[3]:F3})");
                TestContext.WriteLine($"           3D-centroid=({c3df.x[0]:F3},{c3df.x[1]:F3},{c3df.x[2]:F3})");

                // Find which front cells contain this 3D-centroid in their convex projection.
                // If contained by some OTHER front cell whose 4D-position is between camera
                // and this face → CutOut correctly removed (face is occluded).
                // If contained by some OTHER front cell whose 4D-position is FARTHER → bug
                // (face is in front, should not have been removed).
                for (int i = 0; i < fronts.Count; i++) {
                    if (frontIds[i] == owners[0] || (owners.Count > 1 && frontIds[i] == owners[1])) continue;
                    var hs = fronts[i].DefiningHalfSpaces();
                    // Check all face vertices, not just centroid: face is clipped wholly by
                    // this cell iff every vertex is inside (or on boundary of) every halfspace.
                    bool inside = pts3d.All(p => hs.All(h => h.side(p) <= 0));
                    if (!inside) continue;
                    // Compare camera-distance properly: face must be in front of cell's
                    // near-most extent along the view ray. viewDot range = [min, max] of
                    // viewNormal·v over the cell's vertices; max = closest to camera.
                    var cellVerts = c.CellVertexIds(frontIds[i]).Select(v => c.vertices[v]).ToList();
                    double cellMaxViewDot = cellVerts.Max(v => cam.viewNormal.sc(v));
                    double cellMinViewDot = cellVerts.Min(v => cam.viewNormal.sc(v));
                    double dFaceViewVal = cam.viewNormal.sc(c4d);
                    string verdict;
                    if (dFaceViewVal > cellMaxViewDot) verdict = "FACE IN FRONT OF cell (CutOut bug — face wrongly removed)";
                    else if (dFaceViewVal < cellMinViewDot) verdict = "face fully behind cell (occlusion correct)";
                    else verdict = "face within cell's depth range (genuine 4D overlap, BSP must split)";
                    TestContext.WriteLine($"           covered by front-cell {frontIds[i]}: " +
                        $"face viewDot={dFaceViewVal:F3}, cell viewDot range=[{cellMinViewDot:F3}, {cellMaxViewDot:F3}] → {verdict}");
                }
            }
        }

        static bool ProjectionsOverlap(CellRender3d a, CellRender3d b) {
            var verticesA = a.faces.SelectMany(f => f).ToList();
            var verticesB = b.faces.SelectMany(f => f).ToList();
            foreach (var h in a.DefiningHalfSpaces())
                if (verticesB.All(v => h.side(v) >= 0)) return false;
            foreach (var h in b.DefiningHalfSpaces())
                if (verticesA.All(v => h.side(v) >= 0)) return false;
            return true;
        }
    }
}
