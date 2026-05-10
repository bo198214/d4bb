using System;
using System.Collections.Generic;
using D4BB.Geometry;

namespace D4BB.Geometry2 {

    /// Splits a convex 3-cell fragment in 4-space along a 3-hyperplane.
    /// The two output fragments lie on each side of the splitter and share a new
    /// "cap" 2-face that lies in the splitter's hyperplane (intersected with the
    /// fragment's own supporting hyperplane).
    public static class CellSplit {

        public static (CellFragment pos, CellFragment neg) Split(CellFragment f, HalfSpace splitter) {
            var posFaces = new List<List<Point>>();
            var negFaces = new List<List<Point>>();
            var posFaceIds = new List<int>();
            var negFaceIds = new List<int>();
            var capPointsRaw = new List<Point>();

            for (int i = 0; i < f.faces.Count; i++) {
                var face = f.faces[i];
                int srcId = f.faceIds != null ? f.faceIds[i] : -1;
                var posPoly = new List<Point>();
                var negPoly = new List<Point>();
                SplitPolygon(face, splitter, posPoly, negPoly, capPointsRaw);
                if (posPoly.Count >= 3) { posFaces.Add(posPoly); posFaceIds.Add(srcId); }
                if (negPoly.Count >= 3) { negFaces.Add(negPoly); negFaceIds.Add(srcId); }
            }

            var cap = OrderCapPolygon(capPointsRaw);
            if (cap != null && cap.Count >= 3) {
                posFaces.Add(cap); posFaceIds.Add(-1);  // synthetic cap face
                negFaces.Add(new List<Point>(cap)); negFaceIds.Add(-1);
            }

            CellFragment posFrag = posFaces.Count >= 4 ? new CellFragment {
                sourceCellId = f.sourceCellId,
                normal = f.normal,
                faces = posFaces,
                faceIds = posFaceIds,
                supportingHyperplane = f.supportingHyperplane
            } : null;
            CellFragment negFrag = negFaces.Count >= 4 ? new CellFragment {
                sourceCellId = f.sourceCellId,
                normal = f.normal,
                faces = negFaces,
                faceIds = negFaceIds,
                supportingHyperplane = f.supportingHyperplane
            } : null;
            return (posFrag, negFrag);
        }

        /// Sutherland-Hodgman clip of one convex polygon against a hyperplane,
        /// emitting both half-polygons and accumulating cap-polygon vertex candidates.
        static void SplitPolygon(
                List<Point> poly,
                HalfSpace splitter,
                List<Point> outPos,
                List<Point> outNeg,
                List<Point> outCapPoints) {
            int n = poly.Count;
            for (int i = 0; i < n; i++) {
                var cur = poly[i];
                var nxt = poly[(i + 1) % n];
                int cs = splitter.side(cur);
                int ns = splitter.side(nxt);
                if (cs >= 0) outPos.Add(cur);
                if (cs <= 0) outNeg.Add(cur);
                if (cs == 0) outCapPoints.Add(cur);  // vertex on the splitter is part of the cap
                if ((cs > 0 && ns < 0) || (cs < 0 && ns > 0)) {
                    var ip = splitter.cutPoint(cur, nxt);
                    outPos.Add(ip);
                    outNeg.Add(ip);
                    outCapPoints.Add(ip);
                }
            }
        }

        /// Deduplicates raw cap points (since edges/vertices are shared between adjacent faces),
        /// then orders them angularly around their centroid in the cap's 2-plane.
        /// Returns null if fewer than 3 distinct points are available.
        static List<Point> OrderCapPolygon(List<Point> raw) {
            var pts = Dedupe(raw);
            if (pts.Count < 3) return null;
            var plane = Plane2in4.FromPoints(pts);
            if (plane == null) return null;

            var centroid = new Point(4);
            foreach (var p in pts) centroid.add(p);
            centroid.multiply(1.0 / pts.Count);

            var angles = new double[pts.Count];
            for (int i = 0; i < pts.Count; i++) {
                var d = pts[i].clone().subtract(centroid);
                angles[i] = Math.Atan2(plane.v.sc(d), plane.u.sc(d));
            }
            var indices = new int[pts.Count];
            for (int i = 0; i < pts.Count; i++) indices[i] = i;
            Array.Sort(indices, (a, b) => angles[a].CompareTo(angles[b]));

            var ordered = new List<Point>(pts.Count);
            foreach (var i in indices) ordered.Add(pts[i]);
            return ordered;
        }

        static List<Point> Dedupe(List<Point> points) {
            var result = new List<Point>();
            foreach (var p in points) {
                bool dup = false;
                foreach (var q in result)
                    if (p.clone().subtract(q).len() < AOP.ERR) { dup = true; break; }
                if (!dup) result.Add(p);
            }
            return result;
        }
    }
}
