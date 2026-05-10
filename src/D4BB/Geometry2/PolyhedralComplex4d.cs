using System;
using System.Collections.Generic;
using System.Linq;
using D4BB.Geometry;

namespace D4BB.Geometry2 {

    /// 1-cell of the complex: an edge between two vertex indices.
    public class Edge {
        public readonly int v0;
        public readonly int v1;
        public Edge(int v0, int v1) { this.v0 = v0; this.v1 = v1; }
        public bool Contains(int v) => v == v0 || v == v1;
        public int Other(int v) {
            if (v == v0) return v1;
            if (v == v1) return v0;
            throw new ArgumentException($"vertex {v} not in edge ({v0},{v1})");
        }
    }

    /// 2-cell of the complex: a convex polygon as a cyclically ordered list of edge indices.
    /// Consecutive edges (modulo length) share exactly one vertex.
    public class Face {
        public readonly int[] edgeIds;
        public Face(int[] edgeIds) {
            if (edgeIds == null || edgeIds.Length < 3)
                throw new ArgumentException("Face needs at least 3 edges");
            this.edgeIds = edgeIds;
        }
    }

    /// 3-cell of the complex: a convex 3-polytope as a set of face indices.
    /// Optional 4D outward normal: when non-null, used for backface culling in BSP traversal.
    public class Cell {
        public readonly int[] faceIds;
        public readonly Point normal;  // 4D vector or null
        public Cell(int[] faceIds, Point normal = null) {
            if (faceIds == null || faceIds.Length < 4)
                throw new ArgumentException("Cell needs at least 4 faces");
            if (normal != null && normal.x.Length != 4)
                throw new ArgumentException("Cell normal must be 4D");
            this.faceIds = faceIds;
            this.normal = normal;
        }
    }

    /// Index-based polyhedral complex: a 3-complex embedded in 4-space.
    /// Vertices are 4D Points (x.Length == 4). Free-floating k-cells (not part of any
    /// (k+1)-cell) are allowed at every level.
    public class PolyhedralComplex4d {
        public readonly List<Point> vertices;
        public readonly List<Edge> edges;
        public readonly List<Face> faces;
        public readonly List<Cell> cells;

        // Lazy caches
        Plane2in4[] _facePlanes;
        HalfSpace[] _cellHyperplanes;
        List<int>[] _facesPerEdge;
        List<int>[] _cellsPerFace;
        int[][] _faceVertexIds;

        public PolyhedralComplex4d() {
            vertices = new List<Point>();
            edges = new List<Edge>();
            faces = new List<Face>();
            cells = new List<Cell>();
        }

        public PolyhedralComplex4d(List<Point> vertices, List<Edge> edges, List<Face> faces, List<Cell> cells) {
            this.vertices = vertices ?? throw new ArgumentNullException(nameof(vertices));
            this.edges = edges ?? throw new ArgumentNullException(nameof(edges));
            this.faces = faces ?? throw new ArgumentNullException(nameof(faces));
            this.cells = cells ?? throw new ArgumentNullException(nameof(cells));
            foreach (var p in this.vertices)
                if (p.x.Length != 4)
                    throw new ArgumentException("Every vertex must be 4D");
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

        public HashSet<int> CellVertexIds(int cellId) {
            var seen = new HashSet<int>();
            foreach (var fId in cells[cellId].faceIds)
                foreach (var vId in FaceVertexIds(fId))
                    seen.Add(vId);
            return seen;
        }

        public List<Point> CellVertices(int cellId) {
            return CellVertexIds(cellId).Select(i => vertices[i]).ToList();
        }

        // ── supporting subspaces (lazy) ────────────────────────────────────────

        public Plane2in4 FacePlane(int faceId) {
            if (_facePlanes == null) _facePlanes = new Plane2in4[faces.Count];
            if (_facePlanes[faceId] == null) {
                var pts = FaceVertices(faceId).ToList();
                _facePlanes[faceId] = Plane2in4.FromPoints(pts)
                    ?? throw new InvalidOperationException($"Face {faceId} is degenerate (vertices colinear)");
            }
            return _facePlanes[faceId];
        }

        public HalfSpace CellHyperplane(int cellId) {
            if (_cellHyperplanes == null) _cellHyperplanes = new HalfSpace[cells.Count];
            if (_cellHyperplanes[cellId] == null) _cellHyperplanes[cellId] = ComputeCellHyperplane(cellId);
            return _cellHyperplanes[cellId];
        }

        HalfSpace ComputeCellHyperplane(int cellId) {
            var pts = CellVertices(cellId);
            var spanning = AOP.SpanningPoints(pts);
            if (spanning.Count != 4)
                throw new InvalidOperationException(
                    $"Cell {cellId} vertices span dimension {spanning.Count - 1}, expected 3");
            var v0 = spanning[0];
            var d1 = spanning[1].clone().subtract(v0);
            var d2 = spanning[2].clone().subtract(v0);
            var d3 = spanning[3].clone().subtract(v0);
            var normalArr = AOP.Cross(new[] { d1.x, d2.x, d3.x });
            var normal = new Point(normalArr).normalize();
            var cell = cells[cellId];
            if (cell.normal != null && normal.sc(cell.normal) < 0) normal.multiply(-1);
            return new HalfSpace(v0, normal);
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

        public List<int> CellsPerFace(int faceId) {
            if (_cellsPerFace == null) {
                _cellsPerFace = new List<int>[faces.Count];
                for (int i = 0; i < faces.Count; i++) _cellsPerFace[i] = new List<int>();
                for (int c = 0; c < cells.Count; c++)
                    foreach (var fId in cells[c].faceIds) _cellsPerFace[fId].Add(c);
            }
            return _cellsPerFace[faceId];
        }

        // ── coplanarity tests ──────────────────────────────────────────────────

        /// A 2-face is coplanar-embedded iff it has exactly two parent 3-cells AND those
        /// two cells share the same supporting 3-hyperplane in 4D.
        public bool IsCoplanarFace(int faceId) {
            var owners = CellsPerFace(faceId);
            if (owners.Count != 2) return false;
            var hs0 = CellHyperplane(owners[0]);
            var hs1 = CellHyperplane(owners[1]);
            if (!AOP.eq(Math.Abs(hs0.normal.sc(hs1.normal)), 1)) return false;
            return hs1.side(hs0.origin()) == 0;
        }

        /// An edge is coplanar-embedded iff it has at least 2 incident 2-faces AND all of
        /// them share the same supporting affine 2-plane in 4D.
        public bool IsCoplanarEdge(int edgeId) {
            var owners = FacesPerEdge(edgeId);
            if (owners.Count < 2) return false;
            var plane0 = FacePlane(owners[0]);
            for (int i = 1; i < owners.Count; i++)
                if (!plane0.SameSubspace(FacePlane(owners[i]))) return false;
            return true;
        }

        // ── public id queries ──────────────────────────────────────────────────

        public IEnumerable<int> AllFaceIds() => Enumerable.Range(0, faces.Count);
        public IEnumerable<int> AllEdgeIds() => Enumerable.Range(0, edges.Count);

        public IEnumerable<int> NonCoplanarFaceIds() {
            for (int i = 0; i < faces.Count; i++)
                if (!IsCoplanarFace(i)) yield return i;
        }
        public IEnumerable<int> NonCoplanarEdgeIds() {
            for (int i = 0; i < edges.Count; i++)
                if (!IsCoplanarEdge(i)) yield return i;
        }

        // ── invalidate caches (call after mutating the lists in place) ─────────

        public void InvalidateCaches() {
            _facePlanes = null;
            _cellHyperplanes = null;
            _facesPerEdge = null;
            _cellsPerFace = null;
            _faceVertexIds = null;
        }
    }
}
