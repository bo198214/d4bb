using System.Collections.Generic;
using System.Linq;
using D4BB.Geometry;

namespace D4BB.Geometry2 {

    /// Render-time 3D representation of a single 3-cell, used by the BSP-driven
    /// rendering pipeline. Owns mutable lists of 3D polygons (the cell's 2-faces
    /// projected into 3-space) so that a later call to <see cref="CutOut"/> can
    /// trim away the regions of those polygons that are occluded by nearer cells.
    public class CellRender3d {
        public int sourceCellId;
        /// 2-faces of the cell as cyclically ordered 3D points (Point with x.Length == 3).
        public List<List<Point>> faces;
        /// Parallel to <see cref="faces"/>: source PolyhedralComplex4d.faces index, or -1
        /// for synthetic faces (BSP-split caps). Allows the renderer to filter polygons
        /// by their original face's coplanarity status.
        public List<int> faceIds;

        Point _centroidCache;

        public static CellRender3d FromFragment(CellFragment fragment, ICamera4d camera) {
            var cell = new CellRender3d {
                sourceCellId = fragment.sourceCellId,
                faces = new List<List<Point>>(fragment.faces.Count),
                faceIds = new List<int>(fragment.faces.Count),
            };
            for (int i = 0; i < fragment.faces.Count; i++) {
                var poly4d = fragment.faces[i];
                var poly3d = new List<Point>(poly4d.Count);
                foreach (var v in poly4d) poly3d.Add(camera.Proj3d(v));
                cell.faces.Add(poly3d);
                cell.faceIds.Add(fragment.faceIds != null ? fragment.faceIds[i] : -1);
            }
            return cell;
        }

        /// Average of all vertex occurrences across all faces. Lies inside the convex hull
        /// of the cell's projected polyhedron and is sufficient as a probe point for
        /// halfspace-orientation in <see cref="DefiningHalfSpaces"/>.
        public Point Centroid3d() {
            if (_centroidCache != null) return _centroidCache;
            var sum = new Point(3);
            int count = 0;
            foreach (var face in faces)
                foreach (var v in face) { sum.add(v); count++; }
            if (count > 0) sum.multiply(1.0 / count);
            _centroidCache = sum;
            return _centroidCache;
        }

        /// Returns one HalfSpace per 2-face whose plane contains the face. The HalfSpace's
        /// INSIDE half-space contains the cell centroid (HalfSpace.normal points AWAY from
        /// centroid). The intersection of all halfspaces' INSIDE regions equals the projected
        /// cell's 3D interior.
        public HalfSpace[] DefiningHalfSpaces() {
            var centroid = Centroid3d();
            var result = new List<HalfSpace>(faces.Count);
            foreach (var f in faces) {
                if (f.Count < 3) continue;
                // Find first non-collinear triple (normally f[0],f[1],f[2] suffices).
                Point a = f[0], b = null, c = null;
                for (int i = 1; i < f.Count; i++) {
                    if (f[i].clone().subtract(a).len() > AOP.ERR) { b = f[i]; break; }
                }
                if (b == null) continue;
                Point cross = null;
                for (int i = 1; i < f.Count; i++) {
                    if (ReferenceEquals(f[i], b)) continue;
                    var crossCandidate = AOP.cross(b.clone().subtract(a), f[i].clone().subtract(a));
                    if (crossCandidate.len() > AOP.ERR) { c = f[i]; cross = crossCandidate; break; }
                }
                if (c == null) continue;  // degenerate face
                // HalfSpace constructor requires unit-length normal — pass normalized cross,
                // then flip if centroid sits on the wrong side.
                cross.normalize();
                var hs = new HalfSpace(a, cross);
                if (hs.side(centroid) == HalfSpace.OUTSIDE) hs = hs.flip();
                result.Add(hs);
            }
            return result.ToArray();
        }

        /// Removes the regions of this cell's 2-faces that lie inside the polyhedron
        /// defined by the given halfspaces (= occluded by a nearer cell). Faces that
        /// don't intersect the polyhedron at all are kept unchanged; faces that intersect
        /// partially are clipped; faces entirely inside (= entirely occluded) are dropped.
        ///
        /// Faces lying exactly on one of the cutter's halfspace planes are routed by
        /// orientation: if the face's outward normal is co-oriented with the cutter's
        /// halfspace normal, the face sits on the cutter's far side (occluded); otherwise
        /// it's the visible boundary surface. Mirrors Scene4d's
        /// Polyhedron3dBoundaryComplex.Split routing for boundary-coincident faces.
        public void CutOut(HalfSpace[] halfSpaces) {
            if (halfSpaces == null || halfSpaces.Length == 0) return;
            var cellCentroid = Centroid3d();
            var outerKeep = new List<List<Point>>();
            var outerKeepIds = new List<int>();
            // Pre-pass: faces that don't intersect the cutter polyhedron at all are kept
            // unmodified. Without this, every face is iteratively split against every halfspace,
            // producing spurious fragments along halfspace planes even when the face doesn't
            // actually overlap the cutter (e.g. a face entirely beyond cutter's y=+0.5
            // gets split at cutter's x=±0.5 anyway). Mirrors Polyhedron3dBoundaryComplex.CutOut.
            var current = new List<List<Point>>();
            var currentIds = new List<int>();
            var origIds = faceIds != null ? faceIds : Enumerable.Repeat(-1, faces.Count).ToList();
            for (int i = 0; i < faces.Count; i++) {
                if (FaceIntersectsPolyhedron(faces[i], halfSpaces)) {
                    current.Add(faces[i]);
                    currentIds.Add(origIds[i]);
                } else {
                    outerKeep.Add(faces[i]);
                    outerKeepIds.Add(origIds[i]);
                }
            }
            foreach (var hs in halfSpaces) {
                if (current.Count == 0) break;
                var nextCurrent = new List<List<Point>>(current.Count);
                var nextIds = new List<int>(current.Count);
                for (int idx = 0; idx < current.Count; idx++) {
                    var face = current[idx];
                    int srcId = currentIds[idx];
                    if (FaceLiesEntirelyOnPlane(face, hs)) {
                        // Coincident: route by orientation. Mirrors Polyhedron3dBoundaryComplex.Split.
                        //   counter-oriented (face outward opposes hs.normal) ⇒ this is the
                        //     visible boundary on this halfspace's outside ⇒ permanently kept.
                        //   co-oriented ⇒ this face is "behind" this halfspace from the cutter's
                        //     POV, so it's inside w.r.t. THIS halfspace. It still needs to clear
                        //     the OTHER halfspaces to actually be inside the whole cutter, so we
                        //     route it to nextCurrent for further evaluation. If it ends up
                        //     inside ALL halfspaces, it gets discarded at the very end. If at
                        //     some later halfspace it goes outer, that fragment is preserved.
                        if (!FaceOutwardCoOrientedWith(face, cellCentroid, hs.normal)) {
                            outerKeep.Add(face);
                            outerKeepIds.Add(srcId);
                        } else {
                            nextCurrent.Add(face);
                            nextIds.Add(srcId);
                        }
                        continue;
                    }
                    var inner = new List<Point>();
                    var outer = new List<Point>();
                    SplitPolygon3d(face, hs, inner, outer);
                    if (inner.Count >= 3) { nextCurrent.Add(inner); nextIds.Add(srcId); }
                    if (outer.Count >= 3) { outerKeep.Add(outer); outerKeepIds.Add(srcId); }
                }
                current = nextCurrent;
                currentIds = nextIds;
            }
            // `current` are face fragments inside ALL halfspaces ⇒ entirely occluded ⇒ discard
            faces = outerKeep;
            faceIds = outerKeepIds;
            _centroidCache = null;
        }

        /// True iff the face polygon, clipped against every cutter halfspace in turn,
        /// retains at least 3 vertices — i.e. has a non-trivial intersection with the
        /// cutter's interior region. Used by CutOut as a pre-filter.
        static bool FaceIntersectsPolyhedron(List<Point> face, HalfSpace[] halfSpaces) {
            var pts = face;
            foreach (var hs in halfSpaces) {
                pts = ClipConvexPolygonInside(pts, hs);
                if (pts.Count < 3) return false;
            }
            return true;
        }

        static List<Point> ClipConvexPolygonInside(List<Point> poly, HalfSpace hs) {
            var result = new List<Point>(poly.Count + 1);
            int n = poly.Count;
            for (int i = 0; i < n; i++) {
                var cur = poly[i];
                var nxt = poly[(i + 1) % n];
                int cs = hs.side(cur);
                int ns = hs.side(nxt);
                if (cs <= 0) result.Add(cur);
                if ((cs < 0 && ns > 0) || (cs > 0 && ns < 0))
                    result.Add(hs.cutPoint(cur, nxt));
            }
            return result;
        }

        static bool FaceLiesEntirelyOnPlane(List<Point> face, HalfSpace hs) {
            foreach (var v in face)
                if (hs.side(v) != 0) return false;
            return true;
        }

        /// True iff the face's outward normal (pointing away from cellCentroid) and the
        /// reference normal point in the same half-space. Used to decide which side of a
        /// boundary-coincident face is occluded.
        static bool FaceOutwardCoOrientedWith(List<Point> face, Point cellCentroid, Point referenceNormal) {
            // Compute face normal from first non-collinear triple.
            Point a = face[0];
            Point b = null, c = null;
            for (int i = 1; i < face.Count; i++) {
                if (face[i].clone().subtract(a).len() > AOP.ERR) { b = face[i]; break; }
            }
            if (b == null) return false;
            for (int i = 1; i < face.Count; i++) {
                if (ReferenceEquals(face[i], b)) continue;
                var ab = b.clone().subtract(a);
                var ap = face[i].clone().subtract(a);
                if (AOP.cross(ab, ap).len() > AOP.ERR) { c = face[i]; break; }
            }
            if (c == null) return false;
            var faceNormal = AOP.cross(b.clone().subtract(a), c.clone().subtract(a));
            // Orient AWAY from cellCentroid.
            if (faceNormal.sc(cellCentroid.clone().subtract(a)) > 0)
                faceNormal.multiply(-1);
            return faceNormal.sc(referenceNormal) > 0;
        }

        /// Sutherland-Hodgman polygon split: emits the part of `poly` on the halfspace's
        /// INSIDE (side ≤ 0) into `outInner`, and the part on the OUTSIDE (side ≥ 0) into
        /// `outOuter`. Vertices on the plane (side == 0) appear in both outputs.
        static void SplitPolygon3d(List<Point> poly, HalfSpace hs, List<Point> outInner, List<Point> outOuter) {
            int n = poly.Count;
            for (int i = 0; i < n; i++) {
                var cur = poly[i];
                var nxt = poly[(i + 1) % n];
                int cs = hs.side(cur);
                int ns = hs.side(nxt);
                if (cs <= 0) outInner.Add(cur);
                if (cs >= 0) outOuter.Add(cur);
                if ((cs < 0 && ns > 0) || (cs > 0 && ns < 0)) {
                    var ip = hs.cutPoint(cur, nxt);
                    outInner.Add(ip);
                    outOuter.Add(ip);
                }
            }
        }
    }
}
