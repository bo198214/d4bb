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

    /// Helpers used by the rendering pipeline to derive edges from already-clipped face
    /// polygons. The only edge-extraction strategy: pull boundaries out of the BSP+CutOut
    /// processed polygons. Correctness piggy-backs on the face pipeline — the polygons are
    /// already HSR-correct, so their boundaries naturally trace the visible surface.
    public static class CellRender3dEdges {

        /// Extract every polygon's boundary edges from `processedCells`. Each polygon's
        /// boundary is decomposed into segments; segments are classified into the four
        /// (isOriginal, isCoplanar) categories.
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
            var origEdges = ProjectComplexEdges(complex, camera);
            var index = BuildEdgeIndex(complex, origEdges, eps);
            ScanPolygonBoundaries(processedCells, complex, origEdges, index, eps,
                                  out var origByEdge, out var cuts);

            // Pass 2: deduplicate originals via per-edge t-interval union.
            var result = new List<EdgeSegment3d>();
            foreach (var kv in origByEdge) {
                int eId = kv.Key;
                var (p0, p1, coplanar) = origEdges[eId];
                MergeOriginalSegments(p0, p1, kv.Value, coplanar, eps, result);
            }
            // Pass 3: subdivide cut edges by line coverage.
            result.AddRange(SubdivideCutEdgesByLineCoverage(cuts, eps));
            return result;
        }

        /// Hash index built per-frame so polygon-boundary lookups can hit O(1) instead of
        /// scanning all complex edges. `vertexHash` maps quantized 3D-projected vertex
        /// positions to the source complex-vertex id; `edgeMap` maps unordered (vId0, vId1)
        /// pairs to the complex-edge id.
        ///
        /// Only ungclipped polygon boundaries (= both endpoints are projected complex
        /// vertices and connected by a complex edge) hit the fast path. Geometry that has
        /// been split by CutOut still falls through to the linear PointOnSegment scan.
        struct EdgeIndex {
            public Dictionary<(long, long, long), int> vertexHash;
            public Dictionary<(int, int), int> edgeMap;
        }

        static EdgeIndex BuildEdgeIndex(PolyhedralComplex4d complex,
                List<(Point a, Point b, bool coplanar)> origEdges, double eps) {
            var vertexHash = new Dictionary<(long, long, long), int>(complex.vertices.Count);
            // Re-project once per vertex; origEdges contains projected endpoints but not the
            // vertex-id mapping, so we walk vertices directly. The ProjectComplexEdges call
            // and this loop both go through camera.Proj3d, so the resulting Point values are
            // bit-identical for the same source vertex (deterministic float pipeline).
            for (int eId = 0; eId < complex.edges.Count; eId++) {
                var e = complex.edges[eId];
                vertexHash[Quantize(origEdges[eId].a, eps)] = e.v0;
                vertexHash[Quantize(origEdges[eId].b, eps)] = e.v1;
            }
            var edgeMap = new Dictionary<(int, int), int>(complex.edges.Count);
            for (int eId = 0; eId < complex.edges.Count; eId++) {
                var e = complex.edges[eId];
                int lo = System.Math.Min(e.v0, e.v1), hi = System.Math.Max(e.v0, e.v1);
                edgeMap[(lo, hi)] = eId;
            }
            return new EdgeIndex { vertexHash = vertexHash, edgeMap = edgeMap };
        }

        static (long, long, long) Quantize(Point p, double eps) =>
            ((long)System.Math.Round(p.x[0] / eps),
             (long)System.Math.Round(p.x[1] / eps),
             (long)System.Math.Round(p.x[2] / eps));

        /// Pre-project every complex edge to 3D, with its `IsCoplanarEdge` flag.
        static List<(Point a, Point b, bool coplanar)> ProjectComplexEdges(
                PolyhedralComplex4d complex, ICamera4d camera) {
            var result = new List<(Point a, Point b, bool coplanar)>(complex.edges.Count);
            for (int eId = 0; eId < complex.edges.Count; eId++) {
                var e = complex.edges[eId];
                result.Add((
                    camera.Proj3d(complex.vertices[e.v0]),
                    camera.Proj3d(complex.vertices[e.v1]),
                    complex.IsCoplanarEdge(eId)));
            }
            return result;
        }

        /// Walk all polygon boundaries; bucket each segment as either an original-edge
        /// contribution (keyed by complex-edge id) or as a cut edge.
        /// Coplanar source faces are skipped at the polygon level.
        ///
        /// Lookup strategy: try the O(1) hash-based fast path first (both polygon-edge
        /// endpoints are projected complex vertices and the pair is connected by a complex
        /// edge). Fall back to the linear PointOnSegment scan only when the polygon edge is
        /// a sub-segment introduced by CutOut (= at least one endpoint isn't a projected
        /// complex vertex).
        static void ScanPolygonBoundaries(
                IList<CellRender3d> processedCells,
                PolyhedralComplex4d complex,
                List<(Point a, Point b, bool coplanar)> origEdges,
                EdgeIndex index,
                double eps,
                out Dictionary<int, List<(Point a, Point b)>> origByEdge,
                out List<EdgeSegment3d> cuts) {
            origByEdge = new Dictionary<int, List<(Point, Point)>>();
            cuts = new List<EdgeSegment3d>();
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
                        int matchedEdgeId = LookupEdgeId(a, b, index, origEdges, eps);
                        if (matchedEdgeId >= 0) {
                            if (!origByEdge.TryGetValue(matchedEdgeId, out var list)) {
                                list = new List<(Point, Point)>();
                                origByEdge[matchedEdgeId] = list;
                            }
                            list.Add((a, b));
                        } else {
                            cuts.Add(new EdgeSegment3d(a, b, isOriginal: false, isCoplanar: false));
                        }
                    }
                }
            }
        }

        /// Returns the complex-edge id whose 3D-projected segment carries both `a` and `b`,
        /// or -1 if no such edge exists (= cut). Fast path uses the precomputed vertex/edge
        /// hash; linear PointOnSegment fallback only when at least one endpoint isn't a
        /// projected complex vertex (= sub-segment introduced by clipping).
        static int LookupEdgeId(Point a, Point b, EdgeIndex index,
                List<(Point a, Point b, bool coplanar)> origEdges, double eps) {
            bool aIsVertex = index.vertexHash.TryGetValue(Quantize(a, eps), out int vIdA);
            bool bIsVertex = index.vertexHash.TryGetValue(Quantize(b, eps), out int vIdB);
            if (aIsVertex && bIsVertex) {
                int lo = System.Math.Min(vIdA, vIdB), hi = System.Math.Max(vIdA, vIdB);
                return index.edgeMap.TryGetValue((lo, hi), out int eId) ? eId : -1;
            }
            // Sub-segment from CutOut: at least one endpoint is interior to a complex edge.
            // Fall back to a linear scan, but restrict candidates to edges incident on the
            // one matching vertex (if any) to keep the worst case small.
            for (int eId = 0; eId < origEdges.Count; eId++) {
                var (p0, p1, _) = origEdges[eId];
                if (PointOnSegment(a, p0, p1, eps) && PointOnSegment(b, p0, p1, eps))
                    return eId;
            }
            return -1;
        }

        /// Compute the union of t-intervals along the line through p0..p1 from the polygon
        /// contributions, output merged segments inheriting `coplanar`.
        static void MergeOriginalSegments(Point p0, Point p1,
                List<(Point a, Point b)> segments, bool coplanar, double eps,
                List<EdgeSegment3d> output) {
            var dir = p1.clone().subtract(p0);
            double len = dir.len();
            if (len < eps) return;
            dir.multiply(1.0 / len);
            var intervals = new List<(double lo, double hi)>(segments.Count);
            foreach (var (a, b) in segments) {
                double ta = dir.sc(a.clone().subtract(p0));
                double tb = dir.sc(b.clone().subtract(p0));
                if (ta > tb) (ta, tb) = (tb, ta);
                intervals.Add((ta, tb));
            }
            intervals.Sort((x, y) => x.lo.CompareTo(y.lo));
            // Merge overlapping/touching intervals.
            var merged = new List<(double lo, double hi)>();
            foreach (var iv in intervals) {
                if (merged.Count > 0 && iv.lo <= merged[merged.Count - 1].hi + eps) {
                    var last = merged[merged.Count - 1];
                    merged[merged.Count - 1] = (last.lo, System.Math.Max(last.hi, iv.hi));
                } else {
                    merged.Add(iv);
                }
            }
            foreach (var (lo, hi) in merged) {
                if (hi - lo < eps) continue;
                var a = p0.clone().add(dir.clone().multiply(lo));
                var b = p0.clone().add(dir.clone().multiply(hi));
                output.Add(new EdgeSegment3d(a, b, isOriginal: true, isCoplanar: coplanar));
            }
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
