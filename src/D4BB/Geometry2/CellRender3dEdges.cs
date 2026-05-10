using System.Collections.Generic;
using System.Linq;
using D4BB.Geometry;

namespace D4BB.Geometry2 {

    /// 3D edge segment with provenance.
    ///
    /// Four categories based on (isOriginal, isCoplanar):
    ///   (true,  false) — non-coplanar original edge, possibly clipped (always shown).
    ///   (true,  true)  — coplanar-embedded original edge (e.g. between cells in the same
    ///     hyperplane); hidden by default, toggleable.
    ///   (false, false) — cut edge introduced by polygon clipping, lying on a non-coplanar
    ///     boundary plane (always shown).
    ///   (false, true)  — cut edge introduced by polygon clipping, lying in the plane of a
    ///     coplanar-embedded face (= flat-region cut, visually spurious); hidden by default,
    ///     debug-toggleable.
    public struct EdgeSegment3d {
        public Point a;
        public Point b;
        /// True iff the segment lies on a 3D-projected complex edge (= clipped original);
        /// false iff it's a brand-new segment introduced by Sutherland-Hodgman polygon clipping.
        public bool isOriginal;
        /// True iff the segment is coplanar-embedded (= an original edge with `IsCoplanarEdge`,
        /// or a cut edge lying in a coplanar-embedded face's plane).
        public bool isCoplanar;
        public EdgeSegment3d(Point a, Point b, bool isOriginal, bool isCoplanar) {
            this.a = a; this.b = b; this.isOriginal = isOriginal; this.isCoplanar = isCoplanar;
        }
    }

    /// Two strategies for producing the edge mesh in the rendering pipeline.
    public enum EdgeClippingMode {
        /// No clipping at all — edges drawn from original (rotated) vertex positions, full length.
        /// Edges that should be occluded by nearer cells punch through visually.
        Unclipped,
        /// Variant 1 — extract every polygon's boundary as edges, after CutOut. The polygons
        /// are already HSR-correct (clipped); their boundary edges naturally trace the visible
        /// surface. Some boundary edges are cuts introduced by clipping (no original-edge ID).
        FromPolygonBoundaries,
        /// Variant 2 — clip each original complex edge directly against the visible cells'
        /// halfspaces (excluding cells that own the edge). Produces 0..N sub-segments per
        /// original edge; all sub-segments are "original" in the classification sense.
        DirectEdgeClipping,
    }

    /// Helpers used by the rendering pipeline to derive edges from already-clipped face
    /// polygons (variant 1) or by clipping original edges directly (variant 2).
    public static class CellRender3dEdges {

        /// Variant 1: extract every polygon's boundary edges from `processedCells`. Each
        /// polygon's boundary is decomposed into segments; segments are classified into the
        /// four (isOriginal, isCoplanar) categories.
        ///
        /// Source faces with `IsCoplanarFace` are skipped (their boundaries are internal-seam
        /// artifacts in the original geometry).
        ///
        /// Classification:
        ///   • original: matches a 3D-projected complex edge (within eps); `isCoplanar` =
        ///     the source edge's `IsCoplanarEdge` flag (4D structural).
        ///   • cut: introduced by clipping. `isCoplanar` = the segment is **shared** between
        ///     two surviving polygons in the final output. Rationale: when a face is split,
        ///     the cut edge sits between two coplanar fragments. If both fragments survive
        ///     CutOut, the edge is an internal seam (hide). If one fragment is discarded,
        ///     the cut edge becomes a real visible boundary (show).
        public static List<EdgeSegment3d> ExtractFromPolygonBoundaries(
                IList<CellRender3d> processedCells,
                PolyhedralComplex4d complex,
                ICamera4d camera,
                double eps = 1e-4) {
            // Pre-project every complex edge to 3D, with coplanar flag.
            var origEdges = new List<(Point a, Point b, bool coplanar)>();
            for (int eId = 0; eId < complex.edges.Count; eId++) {
                var e = complex.edges[eId];
                origEdges.Add((
                    camera.Proj3d(complex.vertices[e.v0]),
                    camera.Proj3d(complex.vertices[e.v1]),
                    complex.IsCoplanarEdge(eId)));
            }

            // Pass 1: extract all polygon-boundary edges, classify originals (with isCoplanar
            // from IsCoplanarEdge); leave cuts with isCoplanar=false provisionally.
            var result = new List<EdgeSegment3d>();
            foreach (var cell in processedCells) {
                for (int p = 0; p < cell.faces.Count; p++) {
                    var poly = cell.faces[p];
                    if (poly.Count < 2) continue;
                    int srcFaceId = cell.faceIds != null && p < cell.faceIds.Count ? cell.faceIds[p] : -1;
                    if (srcFaceId >= 0 && complex.IsCoplanarFace(srcFaceId)) continue;

                    int n = poly.Count;
                    for (int i = 0; i < n; i++) {
                        Point a = poly[i];
                        Point b = poly[(i + 1) % n];
                        if (a.clone().subtract(b).len() < eps) continue;
                        bool isOriginal = false;
                        bool isCoplanar = false;
                        foreach (var (p0, p1, coplanar) in origEdges) {
                            if (PointOnSegment(a, p0, p1, eps) && PointOnSegment(b, p0, p1, eps)) {
                                isOriginal = true;
                                isCoplanar = coplanar;
                                break;
                            }
                        }
                        result.Add(new EdgeSegment3d(a, b, isOriginal, isCoplanar));
                    }
                }
            }

            // Pass 2: cut edges that appear (even partially) on the same line as another
            // surviving polygon's boundary are coplanar-embedded over their overlap. Group
            // cuts by line; within each group, subdivide along sweep-line t parameters and
            // mark sub-intervals with coverage ≥ 2 as coplanar.
            var origs   = result.Where(e =>  e.isOriginal).ToList();
            var cuts    = result.Where(e => !e.isOriginal).ToList();
            var refined = SubdivideCutEdgesByLineCoverage(cuts, eps);
            result.Clear();
            result.AddRange(origs);
            result.AddRange(refined);
            return result;
        }

        /// Sweep-line subdivision of cut edges along their shared lines. For every group of
        /// cut edges that lie on the same 3D line, the union of intervals is partitioned;
        /// sub-intervals with coverage ≥ 2 (= shared between ≥ 2 polygons) are marked
        /// `isCoplanar = true`, others `false`.
        static List<EdgeSegment3d> SubdivideCutEdgesByLineCoverage(List<EdgeSegment3d> cuts, double eps) {
            var result = new List<EdgeSegment3d>(cuts.Count);
            // Group by canonical line key.
            var groups = new Dictionary<LineKey, List<EdgeSegment3d>>();
            foreach (var seg in cuts) {
                if (!TryMakeLineKey(seg.a, seg.b, eps, out var key)) {
                    result.Add(seg);  // degenerate, pass through unchanged
                    continue;
                }
                if (!groups.TryGetValue(key, out var list)) {
                    list = new List<EdgeSegment3d>();
                    groups[key] = list;
                }
                list.Add(seg);
            }
            foreach (var group in groups.Values) {
                if (group.Count == 1) {
                    result.Add(group[0]);
                    continue;
                }
                SweepLineGroup(group, eps, result);
            }
            return result;
        }

        static void SweepLineGroup(List<EdgeSegment3d> edges, double eps, List<EdgeSegment3d> result) {
            // Project each edge to a 1D t-range along a reference line built from the first
            // edge. Use sweep-line over endpoint t's; classify each sub-interval by coverage.
            var refA = edges[0].a;
            var dir = edges[0].b.clone().subtract(refA);
            double dirLen = dir.len();
            if (dirLen < eps) { foreach (var e in edges) result.Add(e); return; }
            dir.multiply(1.0 / dirLen);
            var intervals = new List<(double t0, double t1)>(edges.Count);
            foreach (var e in edges) {
                double ta = dir.sc(e.a.clone().subtract(refA));
                double tb = dir.sc(e.b.clone().subtract(refA));
                if (ta > tb) (ta, tb) = (tb, ta);
                intervals.Add((ta, tb));
            }
            // Sorted unique boundary t-values.
            var tsSet = new SortedSet<double>();
            foreach (var (t0, t1) in intervals) { tsSet.Add(t0); tsSet.Add(t1); }
            var ts = tsSet.ToList();
            for (int k = 0; k + 1 < ts.Count; k++) {
                double tLo = ts[k], tHi = ts[k + 1];
                if (tHi - tLo < eps) continue;
                double tMid = (tLo + tHi) * 0.5;
                int coverage = 0;
                foreach (var (t0, t1) in intervals)
                    if (t0 - eps <= tMid && tMid <= t1 + eps) coverage++;
                if (coverage == 0) continue;
                var a = refA.clone().add(dir.clone().multiply(tLo));
                var b = refA.clone().add(dir.clone().multiply(tHi));
                result.Add(new EdgeSegment3d(a, b, isOriginal: false, isCoplanar: coverage >= 2));
            }
        }

        /// Hashable representation of a 3D line (independent of which two points generated it).
        readonly struct LineKey : System.IEquatable<LineKey> {
            readonly long rx, ry, rz, dx, dy, dz;
            public LineKey(long rx, long ry, long rz, long dx, long dy, long dz) {
                this.rx = rx; this.ry = ry; this.rz = rz; this.dx = dx; this.dy = dy; this.dz = dz;
            }
            public bool Equals(LineKey o) => rx==o.rx && ry==o.ry && rz==o.rz && dx==o.dx && dy==o.dy && dz==o.dz;
            public override bool Equals(object o) => o is LineKey k && Equals(k);
            public override int GetHashCode() => (int)(((rx*31+ry)*31+rz)*31 ^ ((dx*31+dy)*31+dz));
        }

        /// Build a canonical hashable key for the line through `a` and `b`. Two points on the
        /// same infinite line produce the same key. Returns false for degenerate (a==b) input.
        static bool TryMakeLineKey(Point a, Point b, double eps, out LineKey key) {
            key = default;
            var dir = b.clone().subtract(a);
            double dirLen = dir.len();
            if (dirLen < eps) return false;
            dir.multiply(1.0 / dirLen);
            // Normalize direction sign so first non-near-zero component is positive.
            int sign = 0;
            for (int i = 0; i < 3; i++) {
                if (dir.x[i] >  eps) { sign =  1; break; }
                if (dir.x[i] < -eps) { sign = -1; break; }
            }
            if (sign < 0) dir.multiply(-1);
            // Reference point: projection of origin onto the line (= a − dir·a · dir scaled to dir·dir=1).
            double tOrigin = -dir.sc(a);
            var refPt = a.clone().add(dir.clone().multiply(tOrigin));
            long Q(double x) => (long)System.Math.Round(x / eps);
            key = new LineKey(Q(refPt.x[0]), Q(refPt.x[1]), Q(refPt.x[2]),
                              Q(dir.x[0]),    Q(dir.x[1]),    Q(dir.x[2]));
            return true;
        }

        /// Variant 2: each visible edge is clipped against the halfspaces of every visible
        /// cell (except cells that own the edge). Produces 0..N sub-segments per source edge,
        /// all `isOriginal=true`; `isCoplanar` is inherited from the source edge's
        /// `IsCoplanarEdge` flag.
        public static List<EdgeSegment3d> ClipEdgesAgainstCells(
                PolyhedralComplex4d complex,
                IList<CellRender3d> visibleCells,
                ICamera4d camera) {
            var visibleCellIds = new HashSet<int>();
            var halfspacesByCell = new Dictionary<int, HalfSpace[]>();
            foreach (var cell in visibleCells) {
                if (cell.sourceCellId < 0) continue;
                if (halfspacesByCell.ContainsKey(cell.sourceCellId)) continue;
                visibleCellIds.Add(cell.sourceCellId);
                halfspacesByCell[cell.sourceCellId] = cell.DefiningHalfSpaces();
            }

            var result = new List<EdgeSegment3d>();
            // Iterate ALL edges (including coplanar) so the renderer can apply its own toggle.
            for (int eId = 0; eId < complex.edges.Count; eId++) {
                if (!IsEdgeVisible(complex, eId, visibleCellIds)) continue;
                var edge = complex.edges[eId];
                bool coplanar = complex.IsCoplanarEdge(eId);
                Point a = camera.Proj3d(complex.vertices[edge.v0]);
                Point b = camera.Proj3d(complex.vertices[edge.v1]);

                var owners = new HashSet<int>();
                foreach (int fId in complex.FacesPerEdge(eId))
                    foreach (int cId in complex.CellsPerFace(fId)) owners.Add(cId);

                var intervals = new List<(double lo, double hi)> { (0.0, 1.0) };
                foreach (var kv in halfspacesByCell) {
                    if (owners.Contains(kv.Key)) continue;
                    intervals = ClipIntervalsAgainstCell(intervals, a, b, kv.Value);
                    if (intervals.Count == 0) break;
                }
                foreach (var (lo, hi) in intervals) {
                    if (hi - lo < 1e-9) continue;
                    var segA = a.clone().multiply(1 - lo).add(b.clone().multiply(lo));
                    var segB = a.clone().multiply(1 - hi).add(b.clone().multiply(hi));
                    result.Add(new EdgeSegment3d(segA, segB, isOriginal: true, isCoplanar: coplanar));
                }
            }
            return result;
        }

        /// Edge is visible iff it has no incident 3-cell (free-floating) OR at least one
        /// incident cell is in `visibleCellIds`. Same predicate as
        /// PolyhedralComplex4d.VisibleNonCoplanarEdgeIds, but here we ignore the coplanar
        /// flag — the renderer decides whether to display coplanar edges.
        static bool IsEdgeVisible(PolyhedralComplex4d complex, int eId, HashSet<int> visibleCellIds) {
            bool hasIncidentCell = false;
            foreach (int fId in complex.FacesPerEdge(eId)) {
                foreach (int cId in complex.CellsPerFace(fId)) {
                    hasIncidentCell = true;
                    if (visibleCellIds.Contains(cId)) return true;
                }
            }
            return !hasIncidentCell;
        }

        /// For each interval [lo, hi] on the segment a→b, find the sub-range of t inside the
        /// cell defined by `cellHs` (= side ≤ 0 for ALL halfspaces) and return the COMPLEMENT
        /// (= the parts of the interval that survive the clip).
        static List<(double lo, double hi)> ClipIntervalsAgainstCell(
                List<(double lo, double hi)> intervals, Point a, Point b, HalfSpace[] cellHs) {
            var result = new List<(double, double)>();
            // Compute the cell-interior t-range once (it depends only on a, b, cellHs).
            double tInLo = 0.0, tInHi = 1.0;
            bool empty = false;
            foreach (var h in cellHs) {
                double sa = h.normal.sc(a.clone().subtract(h.origin()));
                double diff = h.normal.sc(b.clone().subtract(a));
                if (System.Math.Abs(diff) < AOP.ERR) {
                    if (sa > AOP.ERR) { empty = true; break; }
                    // else: no constraint
                } else {
                    double tCut = -sa / diff;
                    if (diff > 0) tInHi = System.Math.Min(tInHi, tCut);
                    else          tInLo = System.Math.Max(tInLo, tCut);
                }
            }
            if (empty || tInLo >= tInHi) {
                // Segment doesn't enter the cell → keep all intervals unchanged.
                return new List<(double, double)>(intervals);
            }
            foreach (var (lo, hi) in intervals) {
                double subLo = System.Math.Max(lo, tInLo);
                double subHi = System.Math.Min(hi, tInHi);
                if (subLo >= subHi) {
                    result.Add((lo, hi));  // no overlap with cell interior
                } else {
                    if (lo < subLo) result.Add((lo, subLo));
                    if (subHi < hi) result.Add((subHi, hi));
                }
            }
            return result;
        }

        /// True iff `p` lies on the segment between `a` and `b` within `eps`.
        static bool PointOnSegment(Point p, Point a, Point b, double eps) {
            var ab = b.clone().subtract(a);
            var ap = p.clone().subtract(a);
            double abLen2 = ab.sc(ab);
            if (abLen2 < eps * eps) return ap.len() < eps;
            double t = ap.sc(ab) / abLen2;
            double abLen = System.Math.Sqrt(abLen2);
            if (t < -eps / abLen || t > 1 + eps / abLen) return false;
            var proj = a.clone().add(ab.clone().multiply(t));
            return p.clone().subtract(proj).len() < eps;
        }
    }
}
