using System;
using System.Collections.Generic;
using System.Linq;
using D4BB.Geometry;
using D4BB.Geometry2;   // WeilerAtherton.NewellNormal
using Edge = D4BB.Geometry2.Edge;   // dimension-free index pair (D4BB.Geometry has an unrelated Edge)

namespace D4BB.Geometry3d {

    /// 2-cell of the complex: a convex polygon (cyclically ordered edge indices) embedded
    /// in 3-space, with an optional 3D outward normal. The normal plays the role
    /// Cell.normal plays one dimension up (Geometry2): backface culling and orienting the
    /// supporting plane. Consecutive edges (modulo length) share exactly one vertex.
    public class Face3d {
        public readonly int[] edgeIds;
        public readonly Point normal;  // 3D vector or null
        public Face3d(int[] edgeIds, Point normal = null) {
            if (edgeIds == null || edgeIds.Length < 3)
                throw new ArgumentException("Face3d needs at least 3 edges");
            if (normal != null && normal.x.Length != 3)
                throw new ArgumentException("Face3d normal must be 3D");
            this.edgeIds = edgeIds;
            this.normal = normal;
        }
    }

    /// Index-based polyhedral complex: a 2-complex embedded in 3-space — the
    /// one-dimension-lower analog of Geometry2.PolyhedralComplex4d, whose flat top-rank
    /// cells (there: 3-cells in 4-space; here: 2-faces in 3-space) are what the pairwise
    /// occlusion pipeline orders by their supporting (hyper)planes. There is deliberately
    /// no volume rank: RenderPipeline3d consumes faces, and the boundary builder already
    /// cancels faces shared by two adjacent cubes.
    /// Vertices are 3D Points (x.Length == 3). Edge is reused from Geometry2 (a plain
    /// index pair, dimension-free).
    public class PolyhedralComplex3d {
        public readonly List<Point> vertices;
        public readonly List<Edge> edges;
        public readonly List<Face3d> faces;

        // Lazy caches
        HalfSpace[] _facePlanes;
        List<int>[] _facesPerEdge;
        int[][] _faceVertexIds;

        public PolyhedralComplex3d() {
            vertices = new List<Point>();
            edges = new List<Edge>();
            faces = new List<Face3d>();
        }

        public PolyhedralComplex3d(List<Point> vertices, List<Edge> edges, List<Face3d> faces) {
            this.vertices = vertices ?? throw new ArgumentNullException(nameof(vertices));
            this.edges = edges ?? throw new ArgumentNullException(nameof(edges));
            this.faces = faces ?? throw new ArgumentNullException(nameof(faces));
            foreach (var p in this.vertices)
                if (p.x.Length != 3)
                    throw new ArgumentException("Every vertex must be 3D");
        }

        // ── vertex enumeration ────────────────────────────────────────────────

        /// Vertex indices around faces[faceId], in the cyclic order induced by edgeIds.
        /// result[i] is the vertex shared by edges[edgeIds[i]] and edges[edgeIds[(i+1) % n]].
        public int[] FaceVertexIds(int faceId) {
            if (_faceVertexIds == null) _faceVertexIds = new int[faces.Count][];
            if (_faceVertexIds[faceId] != null) return _faceVertexIds[faceId];
            var eIds = faces[faceId].edgeIds;
            int n = eIds.Length;
            var result = new int[n];
            for (int i = 0; i < n; i++) {
                var e = edges[eIds[i]];
                var eNext = edges[eIds[(i + 1) % n]];
                if (e.v0 == eNext.v0 || e.v0 == eNext.v1) result[i] = e.v0;
                else if (e.v1 == eNext.v0 || e.v1 == eNext.v1) result[i] = e.v1;
                else throw new InvalidOperationException(
                    $"Face {faceId}: edges {eIds[i]} and {eIds[(i+1)%n]} share no vertex");
            }
            _faceVertexIds[faceId] = result;
            return result;
        }

        public IEnumerable<Point> FaceVertices(int faceId) {
            foreach (var vId in FaceVertexIds(faceId)) yield return vertices[vId];
        }

        // ── supporting planes (lazy) ───────────────────────────────────────────

        /// Supporting plane of the (flat) face in 3-space — the analog of
        /// Geometry2.PolyhedralComplex4d.CellHyperplane. The plane normal is oriented to
        /// agree with the face's stored outward normal (when present).
        public HalfSpace FacePlane(int faceId) {
            if (_facePlanes == null) _facePlanes = new HalfSpace[faces.Count];
            if (_facePlanes[faceId] == null) _facePlanes[faceId] = ComputeFacePlane(faceId);
            return _facePlanes[faceId];
        }

        HalfSpace ComputeFacePlane(int faceId) {
            var pts = FaceVertices(faceId).ToList();
            var normal = WeilerAtherton.NewellNormal(pts);
            double len = normal.len();
            if (len < AOP.ERR)
                throw new InvalidOperationException($"Face {faceId} is degenerate (zero area)");
            normal.multiply(1.0 / len);
            var face = faces[faceId];
            if (face.normal != null && normal.sc(face.normal) < 0) normal.multiply(-1);
            return new HalfSpace(pts[0], normal);
        }

        // ── incidence (lazy) ───────────────────────────────────────────────────

        public List<int> FacesPerEdge(int edgeId) {
            if (_facesPerEdge == null) {
                _facesPerEdge = new List<int>[edges.Count];
                for (int i = 0; i < edges.Count; i++) _facesPerEdge[i] = new List<int>();
                for (int f = 0; f < faces.Count; f++)
                    foreach (var eId in faces[f].edgeIds) _facesPerEdge[eId].Add(f);
            }
            return _facesPerEdge[edgeId];
        }

        // ── coplanarity test ───────────────────────────────────────────────────

        /// An edge is coplanar-embedded iff it has at least two incident faces and they
        /// all share the same supporting plane in 3-space — a seam inside a flat surface
        /// (e.g. between the coplanar boundary squares of a docked polycube), not a ridge.
        /// With fewer than 2 incident faces the edge is treated as visible (silhouette /
        /// free-floating). One-dimension-down transcription of
        /// Geometry2.PolyhedralComplex4d.IsCoplanarEdge; the IsCoplanarFace pre-filter
        /// stage has no analog here because a boundary complex contains no coincident
        /// faces (shared cube faces cancel in the builder).
        public bool IsCoplanarEdge(int edgeId) {
            var owners = FacesPerEdge(edgeId);
            if (owners.Count < 2) return false;
            var plane0 = FacePlane(owners[0]);
            for (int i = 1; i < owners.Count; i++) {
                var plane = FacePlane(owners[i]);
                if (!AOP.eq(Math.Abs(plane0.normal.sc(plane.normal)), 1)) return false;
                if (plane.side(plane0.origin()) != 0) return false;
            }
            return true;
        }

        // ── public id queries ──────────────────────────────────────────────────

        public IEnumerable<int> AllFaceIds() => Enumerable.Range(0, faces.Count);
        public IEnumerable<int> AllEdgeIds() => Enumerable.Range(0, edges.Count);

        public IEnumerable<int> NonCoplanarEdgeIds() {
            for (int i = 0; i < edges.Count; i++)
                if (!IsCoplanarEdge(i)) yield return i;
        }

        // ── invalidate caches (call after mutating the lists in place) ─────────

        public void InvalidateCaches() {
            _facePlanes = null;
            _facesPerEdge = null;
            _faceVertexIds = null;
        }
    }
}
