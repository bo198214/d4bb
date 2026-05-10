using System.Collections.Generic;
using System.Linq;
using D4BB.Geometry;

namespace D4BB.Geometry2 {

    public enum CellSide {
        COINCIDENT,   // all vertices lie on the splitter
        POSITIVE,     // all vertices are on the splitter's positive side (or on it)
        NEGATIVE,     // all vertices are on the splitter's negative side (or on it)
        STRADDLE      // vertices on both sides
    }

    /// Self-contained convex 3-polytope used by the BSP. Owns its own face/vertex buffers
    /// so splitting can fragment freely without touching the source PolyhedralComplex4d.
    public class CellFragment {
        public int sourceCellId;
        public Point normal;                 // optional 4D outward normal, copied from source Cell
        public List<List<Point>> faces;      // each face: cyclically ordered 4D points
        public List<int> faceIds;            // parallel to `faces`; the source PolyhedralComplex4d.faces
                                             // index, or -1 for synthetic faces (BSP-split caps).
        public HalfSpace supportingHyperplane;  // 3-hyperplane in 4D containing this fragment

        public Point AnyVertex() => faces[0][0];

        public IEnumerable<Point> AllVertices() {
            foreach (var face in faces)
                foreach (var v in face) yield return v;
        }

        public static CellFragment FromCell(PolyhedralComplex4d complex, int cellId) {
            var cell = complex.cells[cellId];
            var fragment = new CellFragment {
                sourceCellId = cellId,
                normal = cell.normal,
                faces = new List<List<Point>>(),
                faceIds = new List<int>(cell.faceIds.Length),
                supportingHyperplane = complex.CellHyperplane(cellId)
            };
            foreach (var fId in cell.faceIds) {
                var verts = complex.FaceVertices(fId).Select(v => v.clone()).ToList();
                fragment.faces.Add(verts);
                fragment.faceIds.Add(fId);
            }
            return fragment;
        }
    }

    public class BspNode {
        public HalfSpace splitter;
        public List<CellFragment> coincident;  // fragments lying entirely in the splitter's hyperplane
        public BspNode positive;               // subtree on splitter.normal-positive side
        public BspNode negative;
    }

    /// Classical BSP over the 3-cells of a PolyhedralComplex4d, supporting cell fragmentation.
    /// Yields cell fragments back-to-front for a given 4D camera, with optional backface culling.
    public class Bsp4d {
        public BspNode root;

        public static Bsp4d Build(PolyhedralComplex4d complex) {
            var fragments = new List<CellFragment>();
            for (int i = 0; i < complex.cells.Count; i++)
                fragments.Add(CellFragment.FromCell(complex, i));
            var bsp = new Bsp4d { root = BuildNode(fragments) };
            return bsp;
        }

        static BspNode BuildNode(List<CellFragment> fragments) {
            if (fragments.Count == 0) return null;
            var splitter = ChooseSplitter(fragments).supportingHyperplane;
            var coincident = new List<CellFragment>();
            var pos = new List<CellFragment>();
            var neg = new List<CellFragment>();
            foreach (var f in fragments) {
                switch (Classify(f, splitter)) {
                    case CellSide.COINCIDENT:
                        coincident.Add(f);
                        break;
                    case CellSide.POSITIVE:
                        pos.Add(f);
                        break;
                    case CellSide.NEGATIVE:
                        neg.Add(f);
                        break;
                    case CellSide.STRADDLE: {
                        var (fp, fn) = CellSplit.Split(f, splitter);
                        if (fp != null) pos.Add(fp);
                        if (fn != null) neg.Add(fn);
                        break;
                    }
                }
            }
            return new BspNode {
                splitter = splitter,
                coincident = coincident,
                positive = BuildNode(pos),
                negative = BuildNode(neg)
            };
        }

        /// Picks the candidate hyperplane that splits the fewest other fragments.
        /// Inspects up to 8 candidates to keep build time tractable.
        static CellFragment ChooseSplitter(List<CellFragment> fragments) {
            int sample = System.Math.Min(fragments.Count, 8);
            CellFragment best = fragments[0];
            int bestStraddle = int.MaxValue;
            for (int i = 0; i < sample; i++) {
                var candidate = fragments[i];
                var hs = candidate.supportingHyperplane;
                int straddle = 0;
                foreach (var f in fragments) {
                    if (ReferenceEquals(f, candidate)) continue;
                    if (Classify(f, hs) == CellSide.STRADDLE) straddle++;
                    if (straddle >= bestStraddle) break;
                }
                if (straddle < bestStraddle) { bestStraddle = straddle; best = candidate; }
                if (bestStraddle == 0) break;
            }
            return best;
        }

        public static CellSide Classify(CellFragment f, HalfSpace splitter) {
            bool anyPos = false, anyNeg = false;
            foreach (var face in f.faces) {
                foreach (var v in face) {
                    int s = splitter.side(v);
                    if (s > 0) anyPos = true;
                    else if (s < 0) anyNeg = true;
                    if (anyPos && anyNeg) return CellSide.STRADDLE;
                }
            }
            if (anyPos) return CellSide.POSITIVE;
            if (anyNeg) return CellSide.NEGATIVE;
            return CellSide.COINCIDENT;
        }

        // ── traversal ─────────────────────────────────────────────────────────

        public IEnumerable<CellFragment> BackToFront(ICamera4d camera, bool cullBackfaces = true) {
            return Traverse(root, camera, cullBackfaces);
        }

        static IEnumerable<CellFragment> Traverse(BspNode node, ICamera4d camera, bool cullBackfaces) {
            if (node == null) yield break;
            // ICamera4d.IsFacedBy(origin, normal) is true iff the camera looks at the
            // surface from its positive-normal side, i.e. the camera sits in the splitter's
            // positive half-space. In that case "positive" is NEAR.
            bool cameraOnPositiveSide = camera.IsFacedBy(node.splitter.origin(), node.splitter.normal);
            var far = cameraOnPositiveSide ? node.negative : node.positive;
            var near = cameraOnPositiveSide ? node.positive : node.negative;
            foreach (var f in Traverse(far, camera, cullBackfaces)) yield return f;
            foreach (var f in node.coincident)
                if (!cullBackfaces || PassesBackfaceCulling(f, camera)) yield return f;
            foreach (var f in Traverse(near, camera, cullBackfaces)) yield return f;
        }

        static bool PassesBackfaceCulling(CellFragment f, ICamera4d camera) {
            if (f.normal == null) return true;
            return camera.IsFacedBy(f.AnyVertex(), f.normal);
        }
    }
}
