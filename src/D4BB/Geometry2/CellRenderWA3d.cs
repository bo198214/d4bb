using System;
using System.Collections.Generic;
using D4BB.Geometry;

namespace D4BB.Geometry2 {

    /// Render-time 3D representation of a single 3-cell for the Weiler-Atherton
    /// occlusion pipeline (RenderPipeline2). The Sutherland-Hodgman counterpart is
    /// CellRender3d: there, CutOut peels one convex fragment off a face per cutter
    /// halfspace, fragmenting partially occluded faces and introducing internal seams.
    /// Here a face stays ONE FaceRegion — a possibly concave polygon with holes — and
    /// CutOut subtracts the cutter's cross-section via WeilerAtherton.Subtract.
    ///
    /// Consumers that fan-triangulate around the polygon centroid (valid for the convex
    /// Sutherland-Hodgman fragments) must NOT be fed these faces unchanged — concave
    /// outlines and holes need a proper triangulation (e.g. ear clipping with hole
    /// bridging).
    public class CellRenderWA3d {
        public int sourceCellId;
        public List<FaceRegion> faces = new();

        Point _centroidCache;

        public static CellRenderWA3d FromFragment(CellFragment fragment, ICamera4d camera) {
            var cell = new CellRenderWA3d { sourceCellId = fragment.sourceCellId };
            for (int i = 0; i < fragment.faces.Count; i++) {
                var poly4d = fragment.faces[i];
                var ring = new List<Point>(poly4d.Count);
                foreach (var v in poly4d) ring.Add(camera.Proj3d(v));
                if (ring.Count < 3) continue;
                cell.faces.Add(MakeFaceRegion(ring, fragment.faceIds != null ? fragment.faceIds[i] : -1));
            }
            return cell;
        }

        /// Wraps a projected 3D ring as a FaceRegion; planeNormal is the normalized
        /// Newell normal (null for degenerate rings — those pass through CutOut unchanged).
        public static FaceRegion MakeFaceRegion(List<Point> ring, int faceId) {
            var nrm = WeilerAtherton.NewellNormal(ring);
            double len = nrm.len();
            return new FaceRegion {
                outer = ring,
                holes = new List<List<Point>>(),
                faceId = faceId,
                planeNormal = len > AOP.ERR ? nrm.multiply(1.0 / len) : null,
            };
        }

        /// Average of all outer-ring vertex occurrences. Lies inside the convex hull of
        /// the (fresh, convex) projected cell — sufficient as the orientation probe for
        /// DefiningHalfSpaces and the boundary-coincidence routing in CutOut.
        public Point Centroid3d() {
            if (_centroidCache != null) return _centroidCache;
            var sum = new Point(3);
            int count = 0;
            foreach (var f in faces)
                foreach (var v in f.outer) { sum.add(v); count++; }
            if (count > 0) sum.multiply(1.0 / count);
            _centroidCache = sum;
            return _centroidCache;
        }

        /// One halfspace per face, INSIDE (normal pointing away) containing the cell
        /// centroid — the same contract as CellRender3d.DefiningHalfSpaces. Only called
        /// on freshly projected cells (before any CutOut), where every face is a convex
        /// ring without holes and planeNormal is the face's plane normal.
        public HalfSpace[] DefiningHalfSpaces() {
            var centroid = Centroid3d();
            var list = new List<HalfSpace>(faces.Count);
            foreach (var f in faces) {
                if (f.planeNormal == null) continue;
                var hs = new HalfSpace(f.outer[0], f.planeNormal.clone());
                if (hs.side(centroid) == HalfSpace.OUTSIDE) hs = hs.flip();
                list.Add(hs);
            }
            return list.ToArray();
        }

        /// Removes the regions of this cell's faces that lie inside the cutter polyhedron
        /// (= occluded by a nearer cell), Weiler-Atherton style: per face ONE subtraction
        /// of the cutter's cross-section polygon, instead of one Sutherland-Hodgman peel
        /// per halfspace.
        ///
        /// Faces lying exactly on a cutter halfspace plane are routed by orientation,
        /// mirroring CellRender3d.CutOut: counter-oriented ⇒ visible boundary (kept);
        /// co-oriented ⇒ occluded side (subtract the cross-section of the REMAINING
        /// halfspaces); centroid on the plane (free-floating face) ⇒ dropped.
        public void CutOut(HalfSpace[] halfSpaces) {
            if (halfSpaces == null || halfSpaces.Length == 0 || faces.Count == 0) return;
            var centroid = Centroid3d();
            var result = new List<FaceRegion>(faces.Count);
            foreach (var face in faces) {
                if (face.planeNormal == null) { result.Add(face); continue; }

                HalfSpace[] effective = halfSpaces;
                bool keepUnchanged = false, dropFace = false;
                foreach (var hs in halfSpaces) {
                    if (!RingOnPlane(face.outer, hs)) continue;
                    if (hs.side(centroid) == 0) {
                        dropFace = true;             // free-floating face coplanar with cutter
                    } else {
                        // Outward face normal = planeNormal oriented away from the centroid.
                        double toCentroid = face.planeNormal.sc(centroid) - face.planeNormal.sc(face.outer[0]);
                        double outwardDot = (toCentroid > 0 ? -1 : 1) * face.planeNormal.sc(hs.normal);
                        if (outwardDot > 0) {
                            effective = Without(halfSpaces, hs);
                            if (effective.Length == 0) dropFace = true;
                        } else {
                            keepUnchanged = true;    // visible boundary surface
                        }
                    }
                    break;
                }
                if (dropFace) continue;
                if (keepUnchanged) { result.Add(face); continue; }

                // Pre-filter: faces whose outer ring doesn't reach the cutter at all are
                // kept unchanged (mirrors CellRender3d.FaceIntersectsPolyhedron).
                if (!RingIntersects(face.outer, effective)) { result.Add(face); continue; }

                BoundsOf(face.outer, out Point center, out double radius);
                var cross = WeilerAtherton.ConvexCrossSection(
                    effective, center, face.planeNormal, 4 * radius + 1);
                if (cross.Count < 3
                        || Math.Abs(WeilerAtherton.SignedArea(cross, face.planeNormal)) < AOP.ERR) {
                    result.Add(face);
                    continue;
                }
                result.AddRange(WeilerAtherton.Subtract(face, effective, cross));
            }
            faces = result;
            _centroidCache = null;
        }

        /// Flattens the FaceRegions into a CellRender3d (outer and hole rings become
        /// separate polygons with the same faceId). Suitable for boundary-based consumers
        /// like CellRender3dEdges — NOT for centroid-fan triangulation (concave outlines
        /// and hole rings would be filled incorrectly).
        public CellRender3d ToCellRender3d() {
            var cell = new CellRender3d {
                sourceCellId = sourceCellId,
                faces = new List<List<Point>>(faces.Count),
                faceIds = new List<int>(faces.Count),
            };
            foreach (var f in faces) {
                cell.faces.Add(f.outer);
                cell.faceIds.Add(f.faceId);
                foreach (var h in f.holes) {
                    cell.faces.Add(h);
                    cell.faceIds.Add(f.faceId);
                }
            }
            return cell;
        }

        // ── helpers ────────────────────────────────────────────────────────────

        static bool RingOnPlane(List<Point> ring, HalfSpace hs) {
            foreach (var v in ring)
                if (hs.side(v) != 0) return false;
            return true;
        }

        static HalfSpace[] Without(HalfSpace[] arr, HalfSpace excluded) {
            var list = new List<HalfSpace>(arr.Length - 1);
            foreach (var h in arr)
                if (!ReferenceEquals(h, excluded)) list.Add(h);
            return list.ToArray();
        }

        /// Sutherland-Hodgman clip of the ring against all halfspaces, used only as a
        /// boolean "does the face reach the cutter" pre-filter (empty result ⇔ disjoint).
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

        static void BoundsOf(List<Point> ring, out Point center, out double radius) {
            center = new Point(3);
            foreach (var v in ring) center.add(v);
            center.multiply(1.0 / ring.Count);
            double r2max = 0;
            foreach (var v in ring) {
                double r2 = 0;
                for (int i = 0; i < 3; i++) { double d = v.x[i] - center.x[i]; r2 += d * d; }
                if (r2 > r2max) r2max = r2;
            }
            radius = Math.Sqrt(r2max);
        }
    }
}
