using System;
using System.Collections.Generic;
using D4BB.Geometry;
using D4BB.Geometry2;   // CellSide, WeilerAtherton (SignedArea)

namespace D4BB.Geometry3d {

    /// A face lifted out of the complex for the pairwise ordering: its 3D world ring,
    /// outward normal and supporting plane — the one-dimension-lower analog of
    /// Geometry2.CellFragment (a flat codim-1 convex polytope with a supporting
    /// (hyper)plane). No fragmentation ever happens in the pairwise pipeline; the name
    /// keeps the correspondence to the 4D original.
    public class FaceFragment {
        public int sourceFaceId;
        public Point normal;                // optional 3D outward normal
        public List<Point> worldRing;       // cyclically ordered 3D vertices
        public HalfSpace supportingPlane;   // plane in 3-space containing this face

        public static FaceFragment FromFace(PolyhedralComplex3d complex, int faceId) {
            var ring = new List<Point>();
            foreach (var v in complex.FaceVertices(faceId)) ring.Add(v);
            return new FaceFragment {
                sourceFaceId = faceId,
                normal = complex.faces[faceId].normal,
                worldRing = ring,
                supportingPlane = complex.FacePlane(faceId),
            };
        }
    }

    /// BSP-free pairwise Weiler-Atherton hidden-surface removal for 3D→2D parallel
    /// projection — the one-dimension-lower analog of RenderPipeline2.ProcessPairwise.
    /// Faces (flat convex 2-polygons in 3-space) take the role 3-cells play in 4D: every
    /// pair of front-facing faces is ordered via their supporting PLANES and the farther
    /// one of each overlapping pair is cut by the nearer one's projected hull. Faces are
    /// never split; partially occluded faces stay single (possibly concave) polygons and
    /// punch-throughs become real hole rings.
    ///
    /// Requires a PARALLEL camera whose viewNormal spans the projection kernel
    /// (Camera3dParallel: v[0]·viewNormal == v[1]·viewNormal == 0) — the mutual-straddle
    /// fallback depth-probes along that fiber.
    ///
    /// Ordering (see NearerOfPair):
    ///   • COINCIDENT supporting planes ⇒ the projections cannot overlap ⇒ skip;
    ///   • one face doesn't straddle the other's plane ⇒ EXACT plane-side test
    ///     (transcribed from the 4D pipeline);
    ///   • mutual straddle — impossible in 4D lattice polycubes and hence a throw there —
    ///     CAN occur here between independently posed rigid bodies (each face poking
    ///     through the other's infinite plane away from the projected overlap). Handled
    ///     by an exact depth probe: the depth difference of two planes along the
    ///     projection fiber is affine over the (convex) projected overlap, so its sign at
    ///     an interior point decides the order for non-interpenetrating faces; a sign
    ///     change across the overlap (= real face crossing) throws (fail fast).
    public static class RenderPipeline3d {

        /// Runs the pipeline and returns the processed faces (no meaningful depth order —
        /// with exact HSR none is needed for opaque rendering), each containing only the
        /// still-visible regions (when applyCutOut). Edge-on faces (projected area below
        /// AOP.ERR) are dropped entirely: they are invisible and occlude nothing.
        public static List<FaceRenderWA2d> ProcessPairwise(PolyhedralComplex3d complex, ICamera3d camera,
                                                           bool applyCutOut, bool backfaceCulling) {
            var zUp = new Point(0, 0, 1);
            var frags = new List<FaceFragment>();
            var faces2d = new List<FaceRenderWA2d>();
            for (int fId = 0; fId < complex.faces.Count; fId++) {
                var face = complex.faces[fId];
                if (backfaceCulling && face.normal != null) {
                    int probeVid = complex.FaceVertexIds(fId)[0];
                    if (!camera.IsFacedBy(complex.vertices[probeVid], face.normal)) continue;
                }
                var frag = FaceFragment.FromFace(complex, fId);
                var ring2d = new List<Point>(frag.worldRing.Count);
                foreach (var v in frag.worldRing) ring2d.Add(camera.Proj2d(v));
                if (ring2d.Count < 3) continue;
                double area = WeilerAtherton.SignedArea(ring2d, zUp);
                if (Math.Abs(area) < AOP.ERR) continue;      // edge-on: invisible, occludes nothing
                if (area < 0) ring2d.Reverse();              // normalize to CCW w.r.t. +z
                frags.Add(frag);
                faces2d.Add(FaceRenderWA2d.FromRing(ring2d, fId));
            }

            if (applyCutOut) {
                int n = faces2d.Count;
                // Snapshot every face's PRE-CUT hull; the pre-cut ring is FaceRenderWA2d.ring
                // (kept immutable through CutOut). A face's shadow on faces behind it is its
                // full projected region even where the face is itself occluded.
                var hulls = new HalfSpace[n][];
                for (int i = 0; i < n; i++) hulls[i] = faces2d[i].DefiningHalfSpaces2d();
                for (int i = 0; i < n; i++) {
                    for (int j = i + 1; j < n; j++) {
                        // Cheap SAT skip: if a halfplane of either projected hull separates
                        // them, the projections don't overlap — no occlusion. (Hull axes
                        // only, so this may miss a separation; that is safe: the resulting
                        // cut is a no-op.)
                        if (SeparatedBy(hulls[i], faces2d[j].ring) || SeparatedBy(hulls[j], faces2d[i].ring)) continue;
                        int nearer = NearerOfPair(frags[i], frags[j], camera,
                                                  hulls[i], faces2d[i].ring, hulls[j], faces2d[j].ring);
                        if (nearer < 0) continue;
                        if (nearer == 0) faces2d[j].CutOut(hulls[i], faces2d[i].ring);
                        else faces2d[i].CutOut(hulls[j], faces2d[j].ring);
                    }
                }
            }
            return faces2d;
        }

        /// Pairwise occlusion order of two flat convex faces (see class doc):
        /// 0 = `a` is nearer, 1 = `b` is nearer, -1 = no order needed (coincident planes,
        /// or projections don't overlap).
        static int NearerOfPair(FaceFragment a, FaceFragment b, ICamera3d camera,
                                HalfSpace[] hullA, List<Point> ringA2d,
                                HalfSpace[] hullB, List<Point> ringB2d) {
            var sideB = Classify(b.worldRing, a.supportingPlane);
            if (sideB == CellSide.COINCIDENT) return -1;
            if (sideB != CellSide.STRADDLE) {
                bool camPositive = camera.IsFacedBy(
                    a.supportingPlane.origin(), a.supportingPlane.normal);
                return (sideB == CellSide.POSITIVE) == camPositive ? 1 : 0;
            }
            var sideA = Classify(a.worldRing, b.supportingPlane);
            if (sideA == CellSide.COINCIDENT) return -1;
            if (sideA != CellSide.STRADDLE) {
                bool camPositive = camera.IsFacedBy(
                    b.supportingPlane.origin(), b.supportingPlane.normal);
                return (sideA == CellSide.POSITIVE) == camPositive ? 0 : 1;
            }
            // Mutual straddle: order by an exact depth probe over the projected overlap.
            return DepthProbeOrder(a, b, camera, hullA, ringB2d);
        }

        /// Vertex-sign classification of a face ring against a plane — the ring-level
        /// transcription of Bsp4d.Classify.
        static CellSide Classify(List<Point> ring, HalfSpace plane) {
            bool anyPos = false, anyNeg = false;
            foreach (var v in ring) {
                int s = plane.side(v);
                if (s > 0) anyPos = true;
                else if (s < 0) anyNeg = true;
                if (anyPos && anyNeg) return CellSide.STRADDLE;
            }
            if (anyPos) return CellSide.POSITIVE;
            if (anyNeg) return CellSide.NEGATIVE;
            return CellSide.COINCIDENT;
        }

        /// Exact order for a mutually straddling pair: intersect B's projected ring with
        /// A's projected hull; empty ⇒ no overlap (-1). Otherwise compare the two
        /// supporting planes' depths along the projection fiber q0 + t·viewNormal through
        /// interior points of the overlap. The camera sits at -viewNormal infinity
        /// (IsFacedBy ⇔ viewNormal·n &lt; 0 ⇔ the far-negative side is the positive side
        /// of the plane), so the SMALLER t is nearer. The depth difference is affine over
        /// the convex overlap; a strict sign change between its centroid and a vertex
        /// means the faces cross inside the overlap — undecidable, throw (fail fast).
        static int DepthProbeOrder(FaceFragment a, FaceFragment b, ICamera3d camera,
                                   HalfSpace[] hullA, List<Point> ringB2d) {
            var overlap = ClipRing(ringB2d, hullA);
            if (overlap.Count < 3) return -1;
            var zUp = new Point(0, 0, 1);
            if (Math.Abs(WeilerAtherton.SignedArea(overlap, zUp)) < AOP.ERR) return -1;

            var centroid = new Point(3);
            foreach (var v in overlap) centroid.add(v);
            centroid.multiply(1.0 / overlap.Count);

            // Probe the centroid AND every overlap vertex: strictly opposite signs mean
            // the depth order flips inside the overlap (= the faces cross there) — throw
            // even when the centroid itself sits on the depth-equal line.
            bool anyPos = false, anyNeg = false;
            double dCentroid = DepthDiff(centroid, a.supportingPlane, b.supportingPlane, camera);
            if (dCentroid > AOP.ERR) anyPos = true;
            else if (dCentroid < -AOP.ERR) anyNeg = true;
            foreach (var v in overlap) {
                double dv = DepthDiff(v, a.supportingPlane, b.supportingPlane, camera);
                if (dv > AOP.ERR) anyPos = true;
                else if (dv < -AOP.ERR) anyNeg = true;
            }
            if (anyPos && anyNeg)
                throw new Exception(
                    "7263918405 pairwise occlusion order undecidable: faces " + a.sourceFaceId +
                    " and " + b.sourceFaceId + " cross each other inside their projected overlap");
            if (!anyPos && !anyNeg) return -1;   // grazing contact everywhere — safe no-op
            // DepthDiff = tA − tB; smaller t = nearer ⇒ negative diff ⇒ a nearer.
            return anyNeg ? 0 : 1;
        }

        /// tA − tB of the fiber q(t) = q0 + t·viewNormal through the projected point
        /// (q0 = the point itself: z = 0 points project to themselves under
        /// Camera3dParallel) against the two supporting planes.
        static double DepthDiff(Point projected, HalfSpace planeA, HalfSpace planeB, ICamera3d camera) {
            var k = camera.viewNormal;
            double denomA = planeA.normal.sc(k);
            double denomB = planeB.normal.sc(k);
            // A plane parallel to the fiber would be edge-on and was dropped before pairing;
            // guard anyway (safe no-op handled by the caller via the ERR window).
            if (Math.Abs(denomA) < AOP.ERR || Math.Abs(denomB) < AOP.ERR) return 0;
            double tA = (planeA.length - planeA.normal.sc(projected)) / denomA;
            double tB = (planeB.length - planeB.normal.sc(projected)) / denomB;
            return tA - tB;
        }

        /// Sutherland-Hodgman clip of `ring` against all halfplanes; returns the clipped
        /// polygon (fewer than 3 points ⇔ empty intersection).
        static List<Point> ClipRing(List<Point> ring, HalfSpace[] hss) {
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
                if (next.Count < 3) return next;
                poly = next;
            }
            return poly;
        }

        /// True iff some halfplane of `hull` has all `otherVerts` on its outside or
        /// boundary — then the two projected regions have disjoint interiors.
        static bool SeparatedBy(HalfSpace[] hull, List<Point> otherVerts) {
            foreach (var hs in hull) {
                bool allOutside = true;
                foreach (var v in otherVerts) {
                    if (hs.side(v) < 0) { allOutside = false; break; }
                }
                if (allOutside) return true;
            }
            return false;
        }
    }
}
