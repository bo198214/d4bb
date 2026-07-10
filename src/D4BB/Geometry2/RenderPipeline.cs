using System.Collections.Generic;
using D4BB.Geometry;

namespace D4BB.Geometry2 {

    /// The Unity-free per-camera rendering pipeline for a PolyhedralComplex4d:
    ///   - wraps free-floating 2-faces (no 3-cell parent) as single-face cells,
    ///   - builds the BSP and traverses it BackToFront (far → near),
    ///   - projects each cell fragment to 3D via the camera,
    ///   - applies CutOut (clips farther cells against the 3D-halfspaces of nearer cells).
    ///
    /// This is the production code path driven per frame by ComplexFrame (which adds
    /// rotation and marked-face handling on top) and exercised headlessly by the
    /// Geometry2Tests (BSP/CutOut invariant and parity tests).
    public static class RenderPipeline {

        /// Runs the pipeline and returns the processed cells in back-to-front order,
        /// each containing only the still-visible polygon fragments (when applyCutOut).
        public static List<CellRender3d> Process(PolyhedralComplex4d complex, ICamera4d camera,
                                                 bool useBsp, bool applyCutOut, bool backfaceCulling) {
            var processedCells = new List<CellRender3d>();

            // Free-floating 2-faces (no 3-cell parent): wrap each as a single-face CellRender3d
            // and add at the start of processedCells. They have no orientation, so they don't act
            // as cutters (no DefiningHalfSpaces would be derived from a single planar face anyway —
            // a flat polygon's "inside" is empty in 3D), but every 3-cell processed afterward in
            // the BSP+CutOut loop gets a chance to clip them. Coplanar-with-cutter case is dropped
            // by CellRender3d.CutOut's centroid-on-plane check (orientation undefined).
            for (int fId = 0; fId < complex.faces.Count; fId++) {
                if (complex.CellsPerFace(fId).Count > 0) continue;  // belongs to a cell, handled below
                var poly3d = new List<Point>();
                foreach (var v in complex.FaceVertices(fId)) poly3d.Add(camera.Proj3d(v));
                if (poly3d.Count < 3) continue;
                processedCells.Add(new CellRender3d {
                    sourceCellId = -1,
                    faces = new List<List<Point>> { poly3d },
                    faceIds = new List<int> { fId },
                });
            }

            if (useBsp) {
                // BSP back-to-front order. Optionally CutOut clips each farther cell against
                // the 3D-halfspaces of every nearer cell (exact HSR). Without CutOut, faces
                // overdraw — fine for translucent materials, debug-friendly to compare orders.
                var bsp = Bsp4d.Build(complex);
                foreach (var fragment in bsp.BackToFront(camera, cullBackfaces: backfaceCulling)) {
                    var cell3d = CellRender3d.FromFragment(fragment, camera);
                    if (applyCutOut) {
                        var halfSpaces = cell3d.DefiningHalfSpaces();
                        foreach (var farCell in processedCells)
                            farCell.CutOut(halfSpaces);
                    }
                    processedCells.Add(cell3d);
                }
            } else {
                // No-BSP debug mode: render every cell in its natural order, optionally
                // backface-culled. No occlusion handling.
                for (int ci = 0; ci < complex.cells.Count; ci++) {
                    var cell = complex.cells[ci];
                    if (backfaceCulling && cell.normal != null) {
                        int probeVid = -1;
                        foreach (int vid in complex.CellVertexIds(ci)) { probeVid = vid; break; }
                        if (probeVid < 0) continue;
                        if (!camera.IsFacedBy(complex.vertices[probeVid], cell.normal)) continue;
                    }
                    var fragment = CellFragment.FromCell(complex, ci);
                    processedCells.Add(CellRender3d.FromFragment(fragment, camera));
                }
            }
            return processedCells;
        }
    }
}
