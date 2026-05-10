using System.Collections.Generic;
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

        // Reusable scratch buffers for CutOut. CellRender3d instances live one frame each,
        // ~O(N) per frame for an N-cell polychoron — cheaper to lazy-allocate per instance
        // than to allocate fresh lists at every CutOut/halfspace iteration. Cleared at the
        // start of each CutOut call. Non-readonly because we tuple-swap _current ↔ _nextCurrent.
        List<List<Point>> _outerKeep    = new();
        List<int>         _outerKeepIds = new();
        List<List<Point>> _current      = new();
        List<int>         _currentIds   = new();
        List<List<Point>> _nextCurrent  = new();
        List<int>         _nextIds      = new();
        readonly Point    _crossBuffer  = new(3);

        // Single shared scratch list for ClipConvexPolygonInside (used only inside the
        // FaceIntersectsPolyhedron pre-pass; the polygon itself is read, never mutated).
        static readonly List<Point> _clipScratch = new();

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
            double errSq = AOP.ERR * AOP.ERR;
            // Allocate at upper bound; trim only if degenerate faces shrunk the count.
            var temp = new HalfSpace[faces.Count];
            int ri = 0;
            for (int fi = 0; fi < faces.Count; fi++) {
                var f = faces[fi];
                if (f.Count < 3) continue;
                // Find first non-collinear triple (normally f[0],f[1],f[2] suffices).
                Point a = f[0]; int bIdx = -1, cIdx = -1;
                for (int i = 1; i < f.Count; i++) {
                    if (AOP.LenSquaredDiff(f[i], a) > errSq) { bIdx = i; break; }
                }
                if (bIdx < 0) continue;
                Point b = f[bIdx];
                for (int i = 1; i < f.Count; i++) {
                    if (i == bIdx) continue;
                    AOP.CrossDiff(a, b, f[i], _crossBuffer);
                    if (_crossBuffer.sc(_crossBuffer) > errSq) { cIdx = i; break; }
                }
                if (cIdx < 0) continue;  // degenerate face
                // _crossBuffer holds cross(b-a, c-a). Hand a fresh Point to HalfSpace so the
                // normal isn't aliased to our shared scratch buffer.
                var normal = new Point(_crossBuffer.x[0], _crossBuffer.x[1], _crossBuffer.x[2]);
                normal.normalize();
                var hs = new HalfSpace(a, normal);
                if (hs.side(centroid) == HalfSpace.OUTSIDE) hs = hs.flip();
                temp[ri++] = hs;
            }
            if (ri == temp.Length) return temp;
            var result = new HalfSpace[ri];
            System.Array.Copy(temp, result, ri);
            return result;
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
            // Reuse instance-scoped buffers — see field declarations.
            _outerKeep.Clear(); _outerKeepIds.Clear();
            _current.Clear(); _currentIds.Clear();
            // Pre-pass: faces that don't intersect the cutter polyhedron at all are kept
            // unmodified. Without this, every face is iteratively split against every halfspace,
            // producing spurious fragments along halfspace planes even when the face doesn't
            // actually overlap the cutter. Mirrors Polyhedron3dBoundaryComplex.CutOut.
            for (int i = 0; i < faces.Count; i++) {
                int srcId = faceIds != null ? faceIds[i] : -1;
                if (FaceIntersectsPolyhedron(faces[i], halfSpaces)) {
                    _current.Add(faces[i]);
                    _currentIds.Add(srcId);
                } else {
                    _outerKeep.Add(faces[i]);
                    _outerKeepIds.Add(srcId);
                }
            }
            foreach (var hs in halfSpaces) {
                if (_current.Count == 0) break;
                _nextCurrent.Clear(); _nextIds.Clear();
                for (int idx = 0; idx < _current.Count; idx++) {
                    var face = _current[idx];
                    int srcId = _currentIds[idx];
                    if (FaceLiesEntirelyOnPlane(face, hs)) {
                        // Coincident: route by orientation. Mirrors Polyhedron3dBoundaryComplex.Split.
                        //   counter-oriented (face outward opposes hs.normal) ⇒ visible boundary
                        //     on this halfspace's outside ⇒ permanently kept.
                        //   co-oriented ⇒ "behind" this halfspace from the cutter's POV; route to
                        //     _nextCurrent so later halfspaces still get a chance to peel off outer
                        //     fragments. If it ends up inside ALL halfspaces, discarded at the end.
                        //
                        // Special case — free-floating 2-face (no parent 3-cell): cellCentroid
                        // lies in the face's own plane, so face-outward direction is undefined.
                        // Per design: drop in this ambiguous case.
                        if (hs.side(cellCentroid) == 0) {
                            continue;
                        }
                        if (!FaceOutwardCoOrientedWith(face, cellCentroid, hs.normal)) {
                            _outerKeep.Add(face);
                            _outerKeepIds.Add(srcId);
                        } else {
                            _nextCurrent.Add(face);
                            _nextIds.Add(srcId);
                        }
                        continue;
                    }
                    // inner/outer must be fresh Lists — they end up as polygon owners in
                    // _nextCurrent or _outerKeep, where they live until the next CutOut call.
                    var inner = new List<Point>();
                    var outer = new List<Point>();
                    SplitPolygon3d(face, hs, inner, outer);
                    if (inner.Count >= 3) { _nextCurrent.Add(inner); _nextIds.Add(srcId); }
                    if (outer.Count >= 3) { _outerKeep.Add(outer); _outerKeepIds.Add(srcId); }
                }
                // Swap _current ↔ _nextCurrent without reallocating: copy nextCurrent into
                // current via Clear+AddRange (List internal arrays are reused).
                (_current, _nextCurrent) = (_nextCurrent, _current);
                (_currentIds, _nextIds) = (_nextIds, _currentIds);
            }
            // `_current` are face fragments inside ALL halfspaces ⇒ entirely occluded ⇒ discard.
            // We must hand `faces`/`faceIds` to a fresh List instance because consumers may
            // iterate them across frames; reusing _outerKeep would tie the cell's exposed
            // state to the next CutOut call's scratch space.
            faces = new List<List<Point>>(_outerKeep);
            faceIds = new List<int>(_outerKeepIds);
            _centroidCache = null;
        }

        /// True iff the face polygon, clipped against every cutter halfspace in turn,
        /// retains at least 3 vertices — i.e. has a non-trivial intersection with the
        /// cutter's interior region. Used by CutOut as a pre-filter.
        ///
        /// Ping-pong between two static scratch buffers — face (read-only) feeds the first
        /// clip, then alternate. No fresh List allocations per halfspace.
        static bool FaceIntersectsPolyhedron(List<Point> face, HalfSpace[] halfSpaces) {
            if (halfSpaces.Length == 0) return true;
            var bufA = _clipScratch;
            var bufB = _clipScratchAlt;
            // First clip: face → bufA
            bufA.Clear();
            ClipConvexPolygonInside(face, halfSpaces[0], bufA);
            if (bufA.Count < 3) return false;
            // Subsequent clips alternate bufA ↔ bufB.
            for (int i = 1; i < halfSpaces.Length; i++) {
                var src = (i % 2 == 1) ? bufA : bufB;
                var dst = (i % 2 == 1) ? bufB : bufA;
                dst.Clear();
                ClipConvexPolygonInside(src, halfSpaces[i], dst);
                if (dst.Count < 3) return false;
            }
            return true;
        }

        static readonly List<Point> _clipScratchAlt = new();

        static void ClipConvexPolygonInside(List<Point> poly, HalfSpace hs, List<Point> result) {
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
            double errSq = AOP.ERR * AOP.ERR;
            // Compute face normal from first non-collinear triple.
            Point a = face[0];
            int bIdx = -1, cIdx = -1;
            for (int i = 1; i < face.Count; i++) {
                if (AOP.LenSquaredDiff(face[i], a) > errSq) { bIdx = i; break; }
            }
            if (bIdx < 0) return false;
            Point b = face[bIdx];
            for (int i = 1; i < face.Count; i++) {
                if (i == bIdx) continue;
                AOP.CrossDiff(a, b, face[i], _faceNormalScratch);
                if (_faceNormalScratch.sc(_faceNormalScratch) > errSq) { cIdx = i; break; }
            }
            if (cIdx < 0) return false;
            // _faceNormalScratch now holds cross(b-a, c-a). Orient AWAY from cellCentroid:
            // if scratch · (cellCentroid - a) > 0, the normal points TOWARD centroid → flip.
            // Use the existing in-place sc(a, b) overload that computes scratch · (cellCentroid - a).
            if (_faceNormalScratch.sc(a, cellCentroid) > 0)
                _faceNormalScratch.multiply(-1);
            return _faceNormalScratch.sc(referenceNormal) > 0;
        }

        // Scratch for FaceOutwardCoOrientedWith. Only the static method touches it; keeps
        // the cross product alloc-free across thousands of CutOut iterations per frame.
        static readonly Point _faceNormalScratch = new(3);

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
