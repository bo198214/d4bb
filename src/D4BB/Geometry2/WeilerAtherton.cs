using System;
using System.Collections.Generic;
using D4BB.Geometry;

namespace D4BB.Geometry2 {

    /// A planar polygonal region: one outer contour plus zero or more hole contours,
    /// all lying in the same plane. Orientation convention: `outer` is counter-clockwise
    /// and every hole is clockwise when viewed along `planeNormal` (i.e. the region lies
    /// to the LEFT of every contour). This is the face representation of the
    /// Weiler-Atherton rendering pipeline (RenderPipeline2 / CellRenderWA3d) — unlike
    /// the Sutherland-Hodgman peel in CellRender3d.CutOut, a partially occluded face
    /// stays ONE polygon (possibly concave, possibly with holes) instead of being
    /// fragmented into one convex piece per cutter halfspace.
    public class FaceRegion {
        /// Cyclically ordered boundary, CCW w.r.t. planeNormal.
        public List<Point> outer;
        /// Hole contours, each CW w.r.t. planeNormal. Never null (may be empty).
        public List<List<Point>> holes = new();
        /// Source PolyhedralComplex4d.faces index, or -1 for synthetic faces (BSP caps).
        public int faceId;
        /// Unit normal of the supporting plane (reference orientation for the contours).
        /// Null for degenerate faces (collinear after projection) — those are never cut.
        public Point planeNormal;
    }

    /// Weiler-Atherton style polygon subtraction, specialised to the occlusion problem:
    /// the clip region is always CONVEX (the cross-section of a nearer cell's projected
    /// polyhedron with the face's plane). That specialisation removes the classic WA
    /// pain points — instead of general segment/segment intersection bookkeeping, each
    /// subject edge has at most ONE interval inside the clip (computed by interval
    /// clipping against the cutter halfspaces), and the traversal order along the clip
    /// boundary is given by a simple perimeter parametrisation.
    ///
    /// Algorithm per Subtract call (subject = one FaceRegion, clip = convex polygon):
    ///   1. classify subject vertices inside/outside the clip (boundary counts inside),
    ///   2. split every contour into "chains" — maximal runs outside the clip; chain
    ///      endpoints lie on the clip boundary (entry/exit crossings),
    ///   3. stitch result loops: follow a chain, then walk the clip boundary CLOCKWISE
    ///      (= reversed, as Weiler-Atherton prescribes for difference) to the nearest
    ///      chain start, repeat until closed,
    ///   4. no crossings at all ⇒ either disjoint (keep), subject inside clip (drop),
    ///      or clip inside subject (punch a hole = reversed clip contour),
    ///   5. group result loops into FaceRegions by orientation (CCW ⇒ outer, CW ⇒ hole)
    ///      and containment.
    public static class WeilerAtherton {

        class Chain {
            public List<Point> points;   // first point = exit crossing, last = entry crossing
            public double startParam;    // clip-perimeter parameter of points[0]
            public double endParam;      // clip-perimeter parameter of points[^1]
        }

        /// Subtracts the convex cutter from `face`. `halfSpaces` define the cutter volume
        /// (normals pointing OUT of the cutter, as produced by DefiningHalfSpaces);
        /// `crossSection` is the cutter's cross-section polygon in the face's plane,
        /// CCW w.r.t. face.planeNormal (see ConvexCrossSection). Returns the surviving
        /// fragments (empty list = face entirely occluded). The input face is not mutated;
        /// surviving contours may share Point instances with the input.
        public static List<FaceRegion> Subtract(FaceRegion face, HalfSpace[] halfSpaces,
                                                List<Point> crossSection) {
            int K = crossSection.Count;
            var cum = new double[K + 1];
            for (int k = 0; k < K; k++) cum[k + 1] = cum[k] + Dist(crossSection[k], crossSection[(k + 1) % K]);
            double L = cum[K];
            if (L < AOP.ERR) return new List<FaceRegion> { face };  // degenerate cross-section

            var chains = new List<Chain>();
            var resultRings = new List<List<Point>>();

            ProcessRing(face.outer, halfSpaces, crossSection, cum, chains, resultRings, out bool outerDropped);
            foreach (var hole in face.holes)
                ProcessRing(hole, halfSpaces, crossSection, cum, chains, resultRings, out _);

            if (chains.Count == 0) {
                if (outerDropped) return new List<FaceRegion>();
                // No boundary crossings: the clip is either disjoint from the region or
                // strictly inside it. Probe with the clip's interior point: if it lies in
                // the (surviving) region, the cutter punches a hole — the reversed clip
                // contour (CW) becomes a new hole ring.
                var probe = Average(crossSection);
                if (PointInRingsEvenOdd(probe, resultRings, face.planeNormal)) {
                    var newHole = new List<Point>(crossSection);
                    newHole.Reverse();
                    resultRings.Add(newHole);
                }
            } else {
                StitchLoops(chains, crossSection, cum, L, resultRings);
            }
            return GroupRings(resultRings, face);
        }

        /// Splits one contour into chains (maximal runs outside the clip). Contours fully
        /// outside are passed through unchanged into `resultRings`; contours fully inside
        /// the clip are dropped (`dropped` reports this for the caller's outer-ring case).
        static void ProcessRing(List<Point> ring, HalfSpace[] hss, List<Point> clip, double[] cum,
                                List<Chain> chains, List<List<Point>> resultRings, out bool dropped) {
            dropped = false;
            int n = ring.Count;
            // Vertex classification; the clip boundary counts as inside, so a face exactly
            // covering the cross-section is treated as occluded.
            var vin = new bool[n];
            for (int i = 0; i < n; i++) vin[i] = MaxHalfSpaceDist(ring[i], hss) <= AOP.ERR;

            // Crossing events in ring order; pos = edgeIndex + t along the contour. Vertex
            // states are authoritative (guaranteeing entry/exit alternation); the interval
            // clip refines the crossing position within an edge. Because the clip is convex,
            // an edge with both endpoints inside is entirely inside (no events), and an edge
            // has at most one inside-interval.
            var events = new List<(double pos, Point pt, bool isEntry)>();
            for (int i = 0; i < n; i++) {
                var p = ring[i]; var q = ring[(i + 1) % n];
                bool sIn = vin[i], eIn = vin[(i + 1) % n];
                if (sIn && eIn) continue;
                double edgeLen = Dist(p, q);
                if (edgeLen < AOP.ERR) continue;
                bool has = InsideInterval(p, q, hss, out double t0, out double t1);
                double tEps = AOP.ERR / edgeLen;
                if (sIn && !eIn) {
                    double t = has ? t1 : 0;
                    events.Add((i + t, Interp(p, q, t), false));
                } else if (!sIn && eIn) {
                    double t = has ? t0 : 1;
                    events.Add((i + t, Interp(p, q, t), true));
                } else if (has) {
                    // Both endpoints outside but the edge dips through the clip.
                    if (t0 <= tEps || t1 >= 1 - tEps) continue;          // grazing a vertex
                    if ((t1 - t0) * edgeLen <= AOP.ERR) continue;        // sliver dip
                    events.Add((i + t0, Interp(p, q, t0), true));
                    events.Add((i + t1, Interp(p, q, t1), false));
                }
            }

            if (events.Count == 0) {
                if (vin[0]) dropped = true;          // ring entirely inside the cutter
                else resultRings.Add(ring);          // ring untouched by the cutter boundary
                return;
            }

            int m = events.Count;
            for (int k = 0; k < m; k++) {
                if (events[k].isEntry) continue;
                var exitEv = events[k];
                var entryEv = events[(k + 1) % m];
                if (!entryEv.isEntry) throw new Exception("8127465301 non-alternating clip crossing events");
                var pts = new List<Point> { exitEv.pt };
                // ring vertices strictly between the exit and entry positions (circular)
                double span = Mod(entryEv.pos - exitEv.pos, n);
                int j = ((int)Math.Floor(exitEv.pos) + 1) % n;
                double dj = Mod(j - exitEv.pos, n);
                while (dj < span) {
                    AddDedup(pts, ring[j]);
                    j = (j + 1) % n;
                    dj += 1;
                }
                AddDedup(pts, entryEv.pt);
                if (pts.Count < 2) continue;         // degenerate (tangent) crossing
                chains.Add(new Chain {
                    points = pts,
                    startParam = ClosestPerimeterParam(pts[0], clip, cum),
                    endParam = ClosestPerimeterParam(pts[pts.Count - 1], clip, cum),
                });
            }
        }

        /// Weiler-Atherton loop assembly for the DIFFERENCE operation: from a chain's end
        /// (the point where the subject boundary enters the clip) walk the clip boundary
        /// clockwise — i.e. with DECREASING perimeter parameter, against the clip's CCW
        /// orientation — to the nearest chain start, emit the clip vertices passed on the
        /// way, and continue with that chain until the loop closes.
        static void StitchLoops(List<Chain> chains, List<Point> clip, double[] cum, double L,
                                List<List<Point>> resultRings) {
            var used = new bool[chains.Count];
            for (int start = 0; start < chains.Count; start++) {
                if (used[start]) continue;
                var loop = new List<Point>();
                int c = start;
                int guard = 0;
                while (true) {
                    if (guard++ > chains.Count + 1) throw new Exception("5540912873 loop stitching did not close");
                    used[c] = true;
                    foreach (var p in chains[c].points) AddDedup(loop, p);
                    double pe = chains[c].endParam;
                    int best = -1; double bestDelta = double.MaxValue;
                    for (int j = 0; j < chains.Count; j++) {
                        if (used[j] && j != start) continue;
                        double delta = Mod(pe - chains[j].startParam, L);
                        // Entry and exit at the SAME boundary point (tangential touch,
                        // e.g. exactly at a clip corner) may jitter to delta ≈ L instead
                        // of 0 — without this clamp the stitching would walk the full
                        // clip perimeter and glue a spurious clip-shaped loop into the
                        // result ring.
                        if (delta > L - 2 * AOP.ERR) delta = 0;
                        if (delta < bestDelta) { bestDelta = delta; best = j; }
                    }
                    if (best < 0) throw new Exception("2214684970 no chain start found on clip boundary");
                    AppendClipArc(loop, clip, cum, L, pe, bestDelta);
                    if (best == start) break;
                    c = best;
                }
                while (loop.Count > 1 && Dist(loop[0], loop[loop.Count - 1]) < AOP.ERR)
                    loop.RemoveAt(loop.Count - 1);
                if (loop.Count >= 3) resultRings.Add(loop);
            }
        }

        /// Emits the clip vertices passed when walking CW (decreasing parameter) from
        /// `pe` by `span` along the clip perimeter, in walking order.
        static void AppendClipArc(List<Point> loop, List<Point> clip, double[] cum, double L,
                                  double pe, double span) {
            var passed = new List<(double d, Point p)>();
            for (int k = 0; k < clip.Count; k++) {
                double d = Mod(pe - cum[k], L);
                if (d > AOP.ERR && d < span - AOP.ERR) passed.Add((d, clip[k]));
            }
            passed.Sort((a, b) => a.d.CompareTo(b.d));
            foreach (var (_, p) in passed) AddDedup(loop, p);
        }

        /// Classifies result rings by orientation (CCW w.r.t. planeNormal ⇒ outer,
        /// CW ⇒ hole, near-zero area ⇒ dropped) and groups every hole with the outer
        /// ring that contains it.
        static List<FaceRegion> GroupRings(List<List<Point>> rings, FaceRegion face) {
            var outers = new List<List<Point>>();
            var holes = new List<List<Point>>();
            foreach (var r in rings) {
                if (r.Count < 3) continue;
                double a = SignedArea(r, face.planeNormal);
                if (a > AOP.ERR) outers.Add(r);
                else if (a < -AOP.ERR) holes.Add(r);
            }
            if (outers.Count == 0) return new List<FaceRegion>();
            var result = new List<FaceRegion>(outers.Count);
            foreach (var o in outers)
                result.Add(new FaceRegion {
                    outer = o, holes = new List<List<Point>>(),
                    faceId = face.faceId, planeNormal = face.planeNormal,
                });
            foreach (var h in holes) {
                FaceRegion target = null;
                if (result.Count == 1) {
                    target = result[0];
                } else {
                    foreach (var v in h) {
                        foreach (var fr in result) {
                            if (PointInRingEvenOdd(v, fr.outer, face.planeNormal)) { target = fr; break; }
                        }
                        if (target != null) break;
                    }
                    if (target == null) target = LargestOuter(result, face.planeNormal);
                }
                target.holes.Add(h);
            }
            return result;
        }

        /// Cross-section of the cutter volume (intersection of `halfSpaces`) with the plane
        /// through `center` (a point ON the plane) with unit normal `planeNormal`. Returns
        /// a convex polygon CCW w.r.t. planeNormal, or fewer than 3 points if the cutter
        /// does not reach the plane. `extent` bounds the construction quad — it must exceed
        /// the radius of the subject face around `center` (portions of the true cross-section
        /// farther out can never contribute to the difference, because clip arcs used by the
        /// stitching always lie inside the subject region).
        public static List<Point> ConvexCrossSection(HalfSpace[] halfSpaces, Point center,
                                                     Point planeNormal, double extent) {
            // In-plane orthonormal basis (u, v) with cross(u, v) == planeNormal, so the
            // quad below is CCW w.r.t. planeNormal.
            int minAxis = 0; double minAbs = Math.Abs(planeNormal.x[0]);
            for (int i = 1; i < 3; i++) {
                double a = Math.Abs(planeNormal.x[i]);
                if (a < minAbs) { minAbs = a; minAxis = i; }
            }
            var u = new Point(3); u.x[minAxis] = 1;
            u.subtract(planeNormal.clone().multiply(planeNormal.sc(u))).normalize();
            var v = AOP.cross(planeNormal, u);
            var poly = new List<Point> {
                center.clone().addby(u, -extent).addby(v, -extent),
                center.clone().addby(u,  extent).addby(v, -extent),
                center.clone().addby(u,  extent).addby(v,  extent),
                center.clone().addby(u, -extent).addby(v,  extent),
            };
            foreach (var hs in halfSpaces) {
                var next = new List<Point>();
                int m = poly.Count;
                for (int i = 0; i < m; i++) {
                    var cur = poly[i]; var nxt = poly[(i + 1) % m];
                    int cs = hs.side(cur); int ns = hs.side(nxt);
                    if (cs <= 0) next.Add(cur);
                    if ((cs < 0 && ns > 0) || (cs > 0 && ns < 0)) next.Add(hs.cutPoint(cur, nxt));
                }
                poly = next;
                if (poly.Count < 3) return poly;
            }
            return poly;
        }

        // ── small geometry helpers ─────────────────────────────────────────────

        /// Unnormalized Newell normal of a 3D ring; its length is twice the ring's area.
        public static Point NewellNormal(List<Point> ring) {
            var nrm = new Point(3);
            int n = ring.Count;
            for (int i = 0; i < n; i++) {
                var a = ring[i]; var b = ring[(i + 1) % n];
                nrm.x[0] += (a.x[1] - b.x[1]) * (a.x[2] + b.x[2]);
                nrm.x[1] += (a.x[2] - b.x[2]) * (a.x[0] + b.x[0]);
                nrm.x[2] += (a.x[0] - b.x[0]) * (a.x[1] + b.x[1]);
            }
            return nrm;
        }

        /// Signed area of a planar 3D ring w.r.t. the given unit normal
        /// (positive = CCW when viewed along the normal).
        public static double SignedArea(List<Point> ring, Point unitNormal) {
            return NewellNormal(ring).sc(unitNormal) / 2;
        }

        /// Even-odd point-in-region test over a set of contours, evaluated in the 2D
        /// projection that drops the dominant axis of `planeNormal`. With outer+hole
        /// contours this directly answers "inside the region" (odd crossing count).
        public static bool PointInRingsEvenOdd(Point pt, List<List<Point>> rings, Point planeNormal) {
            GetPlaneAxes(planeNormal, out int a0, out int a1);
            int crossings = 0;
            foreach (var ring in rings) crossings += RayCrossings(pt, ring, a0, a1);
            return (crossings & 1) == 1;
        }

        public static bool PointInRingEvenOdd(Point pt, List<Point> ring, Point planeNormal) {
            GetPlaneAxes(planeNormal, out int a0, out int a1);
            return (RayCrossings(pt, ring, a0, a1) & 1) == 1;
        }

        static void GetPlaneAxes(Point planeNormal, out int a0, out int a1) {
            int drop = 0; double max = Math.Abs(planeNormal.x[0]);
            for (int i = 1; i < 3; i++) {
                double a = Math.Abs(planeNormal.x[i]);
                if (a > max) { max = a; drop = i; }
            }
            a0 = (drop + 1) % 3; a1 = (drop + 2) % 3;
        }

        static int RayCrossings(Point pt, List<Point> ring, int a0, int a1) {
            double px = pt.x[a0], py = pt.x[a1];
            int n = ring.Count, crossings = 0;
            for (int i = 0; i < n; i++) {
                var u = ring[i]; var v = ring[(i + 1) % n];
                double uy = u.x[a1] - py, vy = v.x[a1] - py;
                if ((uy > 0) == (vy > 0)) continue;
                double t = uy / (uy - vy);
                double xCross = u.x[a0] + t * (v.x[a0] - u.x[a0]);
                if (xCross > px) crossings++;
            }
            return crossings;
        }

        static FaceRegion LargestOuter(List<FaceRegion> regions, Point planeNormal) {
            FaceRegion best = regions[0];
            double bestArea = SignedArea(best.outer, planeNormal);
            for (int i = 1; i < regions.Count; i++) {
                double a = SignedArea(regions[i].outer, planeNormal);
                if (a > bestArea) { bestArea = a; best = regions[i]; }
            }
            return best;
        }

        /// Largest signed distance of `v` to any cutter halfspace plane; ≤ 0 means the
        /// point is inside the (convex) cutter volume.
        static double MaxHalfSpaceDist(Point v, HalfSpace[] hss) {
            double m = double.MinValue;
            foreach (var hs in hss) {
                double d = hs.normal.sc(v) - hs.length;
                if (d > m) m = d;
            }
            return m;
        }

        /// Parameter interval [t0, t1] of the segment p→q that lies inside the cutter
        /// (all halfspace distances ≤ 0). Returns false if the intersection is empty or
        /// degenerate.
        static bool InsideInterval(Point p, Point q, HalfSpace[] hss, out double t0, out double t1) {
            t0 = 0; t1 = 1;
            foreach (var hs in hss) {
                double dp = hs.normal.sc(p) - hs.length;
                double dq = hs.normal.sc(q) - hs.length;
                double dd = dq - dp;
                if (Math.Abs(dd) < 1e-12) {
                    if ((dp + dq) * 0.5 >= AOP.ERR) return false;   // parallel, outside
                    continue;                                        // parallel, inside
                }
                double tz = -dp / dd;
                if (dd > 0) { if (tz < t1) t1 = tz; }
                else        { if (tz > t0) t0 = tz; }
                if (t0 >= t1) return false;
            }
            return t1 > t0;
        }

        /// Perimeter parameter (arc length from clip vertex 0) of the closest point on
        /// the clip boundary. Chain endpoints lie on the boundary up to tolerance, so the
        /// closest-point projection is a robust way to place them — no bookkeeping of
        /// which halfspace produced the crossing is needed.
        static double ClosestPerimeterParam(Point p, List<Point> clip, double[] cum) {
            int K = clip.Count;
            double bestD2 = double.MaxValue, bestParam = 0;
            for (int k = 0; k < K; k++) {
                var a = clip[k]; var b = clip[(k + 1) % K];
                double segLen = cum[k + 1] - cum[k];
                double t = 0;
                if (segLen >= AOP.ERR) {
                    double num = 0;
                    for (int d = 0; d < 3; d++) num += (p.x[d] - a.x[d]) * (b.x[d] - a.x[d]);
                    t = num / (segLen * segLen);
                    if (t < 0) t = 0; else if (t > 1) t = 1;
                }
                double d2 = 0;
                for (int d = 0; d < 3; d++) {
                    double diff = p.x[d] - (a.x[d] + t * (b.x[d] - a.x[d]));
                    d2 += diff * diff;
                }
                if (d2 < bestD2) { bestD2 = d2; bestParam = cum[k] + t * segLen; }
            }
            return bestParam;
        }

        static Point Interp(Point p, Point q, double t) {
            if (t <= 0) return p;
            if (t >= 1) return q;
            return p.clone().add(q.clone().subtract(p).multiply(t));
        }

        static Point Average(List<Point> pts) {
            var s = new Point(3);
            foreach (var p in pts) s.add(p);
            return s.multiply(1.0 / pts.Count);
        }

        static double Dist(Point a, Point b) {
            double s = 0;
            for (int i = 0; i < 3; i++) { double d = a.x[i] - b.x[i]; s += d * d; }
            return Math.Sqrt(s);
        }

        static void AddDedup(List<Point> pts, Point p) {
            if (pts.Count > 0 && Dist(pts[pts.Count - 1], p) < AOP.ERR) return;
            pts.Add(p);
        }

        static double Mod(double x, double m) {
            double r = x % m;
            return r < 0 ? r + m : r;
        }
    }
}
