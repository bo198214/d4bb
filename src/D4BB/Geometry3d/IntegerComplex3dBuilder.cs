using System.Collections.Generic;
using System.Linq;
using D4BB.Comb;
using D4BB.Geometry;
using Edge = D4BB.Geometry2.Edge;   // dimension-free index pair (D4BB.Geometry has an unrelated Edge)

namespace D4BB.Geometry3d {

    /// Generators that build a PolyhedralComplex3d from axis-aligned integer-grid input —
    /// the one-dimension-lower analog of Geometry2.IntegerComplex4dBuilder.
    public static class IntegerComplex3dBuilder {

        /// Builds the boundary 2-complex of a set of unit cubes at the integer grid.
        /// Inner squares (shared by two adjacent cubes) cancel out; the result contains
        /// exactly the boundary squares (each with its outward normal set), their edges
        /// and vertices, all deduplicated by geometric position.
        public static PolyhedralComplex3d Boundary(IEnumerable<IntegerCell> cubes) {
            var list = cubes.ToList();
            if (list.Count == 0) return new PolyhedralComplex3d();
            foreach (var c in list) {
                if (c.SpaceDim() != 3)
                    throw new System.ArgumentException($"Expected 3D cubes, got SpaceDim={c.SpaceDim()}");
                if (c.Dim() != 3)
                    throw new System.ArgumentException($"Expected full-dimensional cubes (Dim=3), got Dim={c.Dim()}");
            }
            var ibc = new IntegerBoundaryComplex(list);
            return FromBoundaryFaces(ibc.cells);
        }

        /// Convenience overload taking raw integer origins.
        public static PolyhedralComplex3d Boundary(int[][] cubeOrigins) =>
            Boundary(cubeOrigins.Select(o => new IntegerCell(o)));

        /// Builds a PolyhedralComplex3d directly from an arbitrary collection of 2-dimensional
        /// OrientedIntegerCells in 3-space. Each square becomes a Face3d with outward normal
        /// taken from <see cref="OrientedIntegerCell.Normal()"/>. Vertices/edges are deduplicated.
        public static PolyhedralComplex3d FromBoundaryFaces(IEnumerable<OrientedIntegerCell> squares) {
            var c = new PolyhedralComplex3d();
            var vertexMap = new Dictionary<(int, int, int), int>();
            var edgeMap = new Dictionary<(int, int), int>();

            foreach (var square in squares) {
                if (square.SpaceDim() != 3)
                    throw new System.ArgumentException($"Expected squares in 3D, got SpaceDim={square.SpaceDim()}");
                if (square.Dim() != 2)
                    throw new System.ArgumentException($"Expected 2-dimensional squares, got Dim={square.Dim()}");

                int[][] verts = square.ClockwiseFromOutsideVertices2d();   // 4 vertex coords
                int[] vIds = new int[4];
                for (int i = 0; i < 4; i++) vIds[i] = AddVertex(c, vertexMap, verts[i]);
                int[] edgeIds = new int[4];
                for (int i = 0; i < 4; i++) {
                    int v0 = vIds[i];
                    int v1 = vIds[(i + 1) % 4];
                    int lo = System.Math.Min(v0, v1);
                    int hi = System.Math.Max(v0, v1);
                    if (!edgeMap.TryGetValue((lo, hi), out int eId)) {
                        eId = c.edges.Count;
                        c.edges.Add(new Edge(lo, hi));
                        edgeMap[(lo, hi)] = eId;
                    }
                    edgeIds[i] = eId;
                }

                var normalArr = square.Normal();   // int[3], one entry ±1, rest 0
                var normal = new Point(new[] {
                    (double)normalArr[0], (double)normalArr[1], (double)normalArr[2]
                });
                c.faces.Add(new Face3d(edgeIds, normal));
            }
            return c;
        }

        static int AddVertex(
                PolyhedralComplex3d c,
                Dictionary<(int, int, int), int> vertexMap,
                int[] coord) {
            var key = (coord[0], coord[1], coord[2]);
            if (vertexMap.TryGetValue(key, out int idx)) return idx;
            idx = c.vertices.Count;
            c.vertices.Add(new Point(new[] { (double)coord[0], (double)coord[1], (double)coord[2] }));
            vertexMap[key] = idx;
            return idx;
        }
    }
}
