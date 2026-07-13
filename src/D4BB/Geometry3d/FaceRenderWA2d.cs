using System;
using System.Collections.Generic;
using D4BB.Geometry;
using D4BB.Geometry2;   // FaceRegion, WeilerAtherton

namespace D4BB.Geometry3d {

    /// Render-time 2D representation of a single projected face for the pairwise
    /// Weiler-Atherton occlusion pipeline (RenderPipeline3d) — the one-dimension-lower
    /// analog of Geometry2.CellRenderWA3d. All geometry lives in the z = 0 plane of
    /// 3-space (the drawing plane; Camera3dParallel.Proj2d emits Point3d with x[2] == 0),
    /// so Geometry2's FaceRegion / WeilerAtherton machinery is reused verbatim with the
    /// constant plane normal (0,0,1).
    ///
    /// Simplifications vs. the 4D original: the cutter's cross-section with the face's
    /// plane IS the cutter's projected ring itself (no ConvexCrossSection), and the
    /// face-on-cutter-boundary orientation routing (RingOnPlane) has no analog — a
    /// non-degenerate z = 0 face cannot lie in a cutter halfplane's vertical plane, and
    /// coincident supporting planes are already skipped at the pairwise-ordering level.
    public class FaceRenderWA2d {
        public int sourceFaceId;
        /// The pre-cut projected ring, CCW w.r.t. +z. Kept immutable through CutOut —
        /// it doubles as the snapshot of this face's full shadow (the cutter geometry).
        public List<Point> ring;
        /// The still-visible regions (starts as one region covering `ring`).
        public List<FaceRegion> regions = new();

        static readonly Point ZUp = new Point(0, 0, 1);

        /// Wraps a projected, winding-normalized (CCW w.r.t. +z) ring.
        public static FaceRenderWA2d FromRing(List<Point> ring, int faceId) {
            var face = new FaceRenderWA2d { sourceFaceId = faceId, ring = ring };
            face.regions.Add(new FaceRegion {
                outer = ring,
                holes = new List<List<Point>>(),
                faceId = faceId,
                planeNormal = ZUp.clone(),
            });
            return face;
        }

        /// The 2D halfplanes bounding the projected convex ring (embedded as z-invariant
        /// halfspaces): one per ring edge, in-plane perpendicular, flipped so the ring's
        /// centroid is inside — the construction of Scene3d.DefiningHalfSpaces2d, computed
        /// on the PRE-CUT ring only.
        public HalfSpace[] DefiningHalfSpaces2d() {
            var centroid = new Point(3);
            foreach (var v in ring) centroid.add(v);
            centroid.multiply(1.0 / ring.Count);

            var list = new List<HalfSpace>(ring.Count);
            int n = ring.Count;
            for (int i = 0; i < n; i++) {
                var o = ring[i];
                var p = ring[(i + 1) % n];
                var d = p.clone().subtract(o);
                var normal = new Point(d.x[1], -d.x[0], 0);
                double len = normal.len();
                if (len < AOP.ERR) continue;   // degenerate ring edge
                normal.multiply(1.0 / len);
                var hs = new HalfSpace(o, normal);
                if (hs.side(centroid) == HalfSpace.OUTSIDE) hs = hs.flip();
                list.Add(hs);
            }
            return list.ToArray();
        }

        /// Removes the regions of this face that lie inside the cutter's projected hull
        /// (= occluded by a nearer face), Weiler-Atherton style: ONE subtraction of the
        /// cutter's ring per region. `cutterHull` are the cutter's defining halfplanes,
        /// `cutterRing` its pre-cut projected ring (CCW w.r.t. +z — the required
        /// cross-section orientation, since every planeNormal here is +z).
        public void CutOut(HalfSpace[] cutterHull, List<Point> cutterRing) {
            if (cutterHull == null || cutterHull.Length == 0 || regions.Count == 0) return;
            var result = new List<FaceRegion>(regions.Count);
            foreach (var region in regions) {
                if (region.planeNormal == null) { result.Add(region); continue; }
                // Pre-filter: regions whose outer ring doesn't reach the cutter are kept
                // unchanged (mirrors CellRenderWA3d.CutOut).
                if (!RingIntersects(region.outer, cutterHull)) { result.Add(region); continue; }
                result.AddRange(WeilerAtherton.Subtract(region, cutterHull, cutterRing));
            }
            regions = result;
        }

        /// Flattens the regions into plain polygons (outer and hole rings become separate
        /// polygons carrying the same faceId) — the input format of FaceRender2dEdges.
        public void ToPolygons(List<List<Point>> polys, List<int> faceIds) {
            foreach (var r in regions) {
                polys.Add(r.outer);
                faceIds.Add(r.faceId);
                foreach (var h in r.holes) {
                    polys.Add(h);
                    faceIds.Add(r.faceId);
                }
            }
        }

        /// Total still-visible area of this face: outer rings count positive (CCW), hole
        /// rings negative (CW) — so a plain signed sum over all contours is the area.
        public double VisibleArea() {
            double area = 0;
            foreach (var r in regions) {
                area += WeilerAtherton.SignedArea(r.outer, r.planeNormal ?? ZUp);
                foreach (var h in r.holes)
                    area += WeilerAtherton.SignedArea(h, r.planeNormal ?? ZUp);
            }
            return area;
        }

        /// Sutherland-Hodgman clip of the ring against all halfplanes, used only as a
        /// boolean "does the region reach the cutter" pre-filter (empty result ⇔ disjoint).
        /// Copied from CellRenderWA3d (private there).
        static bool RingIntersects(List<Point> ring, HalfSpace[] hss) {
            var poly = ring;
            foreach (var hs in hss) {
                var next = new List<Point>();
                int m = poly.Count;
                for (int i = 0; i < m; i++) {
                    var cur = poly[i]; var nxt = poly[(i + 1) % m];
                    int cs = hs.side(cur); int ns = hs.side(nxt);
                    if (cs <= 0) next.Add(cur);
                    if ((cs < 0 && ns > 0) || (cs > 0 && ns < 0)) next.Add(hs.cutPoint(cur, nxt));
                }
                if (next.Count < 3) return false;
                poly = next;
            }
            return true;
        }
    }
}
