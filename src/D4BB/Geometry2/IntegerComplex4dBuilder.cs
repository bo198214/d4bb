using System.Collections.Generic;
using System.Linq;
using D4BB.Comb;
using D4BB.Geometry;

namespace D4BB.Geometry2 {

    /// Generators that build a PolyhedralComplex4d from axis-aligned integer-grid input.
    public static class IntegerComplex4dBuilder {

        /// Builds the boundary 3-complex of a set of unit tesseracts at the integer grid.
        /// Inner cubes (shared by two adjacent tesseracts) cancel out; the result contains
        /// exactly the boundary 3-cubes (each with its outward normal set), their bounding
        /// 2-faces, edges and vertices, all deduplicated by geometric position.
        public static PolyhedralComplex4d Boundary(IEnumerable<IntegerCell> tesseracts) {
            var list = tesseracts.ToList();
            if (list.Count == 0) return new PolyhedralComplex4d();
            foreach (var t in list) {
                if (t.SpaceDim() != 4)
                    throw new System.ArgumentException($"Expected 4D tesseracts, got SpaceDim={t.SpaceDim()}");
                if (t.Dim() != 4)
                    throw new System.ArgumentException($"Expected full-dimensional tesseracts (Dim=4), got Dim={t.Dim()}");
            }
            var ibc = new IntegerBoundaryComplex(list);
            return FromBoundaryCubes(ibc.cells);
        }

        /// Builds a PolyhedralComplex4d directly from an arbitrary collection of 3-dimensional
        /// OrientedIntegerCells in 4-space. Each cube becomes a Cell with outward normal taken
        /// from <see cref="OrientedIntegerCell.Normal()"/>. Vertices/edges/faces are deduplicated.
        public static PolyhedralComplex4d FromBoundaryCubes(IEnumerable<OrientedIntegerCell> cubes) {
            var c = new PolyhedralComplex4d();
            var vertexMap = new Dictionary<(int, int, int, int), int>();
            var edgeMap = new Dictionary<(int, int), int>();
            var faceMap = new Dictionary<IntegerCell, int>();

            foreach (var cube in cubes) {
                if (cube.SpaceDim() != 4)
                    throw new System.ArgumentException($"Expected cubes in 4D, got SpaceDim={cube.SpaceDim()}");
                if (cube.Dim() != 3)
                    throw new System.ArgumentException($"Expected 3-dimensional cubes, got Dim={cube.Dim()}");

                var cubeFaceIds = new int[6];
                int faceSlot = 0;
                foreach (OrientedIntegerCell face2 in cube.Facets()) {
                    if (!faceMap.TryGetValue(face2, out int faceId)) {
                        faceId = AddFace(c, face2, vertexMap, edgeMap);
                        faceMap[face2] = faceId;
                    }
                    cubeFaceIds[faceSlot++] = faceId;
                }

                var normalArr = cube.Normal();   // int[4], one entry ±1, rest 0
                var normal = new Point(new[] {
                    (double)normalArr[0], (double)normalArr[1], (double)normalArr[2], (double)normalArr[3]
                });
                c.cells.Add(new Cell(cubeFaceIds, normal));
            }
            return c;
        }

        static int AddFace(
                PolyhedralComplex4d c,
                OrientedIntegerCell face2,
                Dictionary<(int, int, int, int), int> vertexMap,
                Dictionary<(int, int), int> edgeMap) {
            int[][] verts = face2.ClockwiseFromOutsideVertices2d();   // 4 vertex coords
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
            int faceId = c.faces.Count;
            c.faces.Add(new Face(edgeIds));
            return faceId;
        }

        static int AddVertex(
                PolyhedralComplex4d c,
                Dictionary<(int, int, int, int), int> vertexMap,
                int[] coord) {
            var key = (coord[0], coord[1], coord[2], coord[3]);
            if (vertexMap.TryGetValue(key, out int idx)) return idx;
            idx = c.vertices.Count;
            c.vertices.Add(new Point(new[] { (double)coord[0], (double)coord[1], (double)coord[2], (double)coord[3] }));
            vertexMap[key] = idx;
            return idx;
        }
    }
}
