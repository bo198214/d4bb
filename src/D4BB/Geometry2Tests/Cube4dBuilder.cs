using System;
using System.Collections.Generic;
using D4BB.Geometry;
using D4BB.Geometry2;
using Edge = D4BB.Geometry2.Edge;

namespace D4BB.Geometry2Tests {

    /// Test helper: builds an axis-aligned unit cube as a single 3-cell embedded in 4-space.
    /// The cube spans [origin.x..origin.x+1] in axes 0,1,2 and is fixed at origin.x[3] in axis 3.
    /// Vertex bit i (0..7) selects high (1) vs low (0) on axis i.
    public static class Cube4dBuilder {

        public static PolyhedralComplex4d UnitCubeAtW(double w, Point cellNormal = null) {
            return UnitCubeAt(0, 0, 0, w, cellNormal);
        }

        public static PolyhedralComplex4d UnitCubeAt(double ox, double oy, double oz, double ow, Point cellNormal = null) {
            var c = new PolyhedralComplex4d();
            for (int i = 0; i < 8; i++) {
                double x = ox + (i & 1);
                double y = oy + ((i >> 1) & 1);
                double z = oz + ((i >> 2) & 1);
                c.vertices.Add(new Point(x, y, z, ow));
            }
            AddCubeEdgesAndFaces(c, vertexOffset: 0);
            // Single cell with all 6 faces (face indices 0..5 as added by AddCubeEdgesAndFaces)
            int[] faceIds = new int[6];
            for (int i = 0; i < 6; i++) faceIds[i] = i;
            c.cells.Add(new Cell(faceIds, cellNormal));
            return c;
        }

        /// Builds a complex with two unit cubes at different w values, neither sharing geometry.
        public static PolyhedralComplex4d TwoCubesAtDifferentW(double w0, double w1,
                Point normal0 = null, Point normal1 = null) {
            var c = new PolyhedralComplex4d();
            // Cube 0: vertices 0..7
            for (int i = 0; i < 8; i++) {
                c.vertices.Add(new Point(i & 1, (i >> 1) & 1, (i >> 2) & 1, w0));
            }
            int faceOffset0 = c.faces.Count;
            AddCubeEdgesAndFaces(c, vertexOffset: 0);
            int[] faceIds0 = new int[6];
            for (int i = 0; i < 6; i++) faceIds0[i] = faceOffset0 + i;
            c.cells.Add(new Cell(faceIds0, normal0));
            // Cube 1: vertices 8..15
            for (int i = 0; i < 8; i++) {
                c.vertices.Add(new Point(i & 1, (i >> 1) & 1, (i >> 2) & 1, w1));
            }
            int faceOffset1 = c.faces.Count;
            AddCubeEdgesAndFaces(c, vertexOffset: 8);
            int[] faceIds1 = new int[6];
            for (int i = 0; i < 6; i++) faceIds1[i] = faceOffset1 + i;
            c.cells.Add(new Cell(faceIds1, normal1));
            return c;
        }

        /// Adds the 12 edges and 6 faces of a unit cube whose 8 vertices are at indices
        /// vertexOffset..vertexOffset+7 in c.vertices, indexed by the same bit convention.
        static void AddCubeEdgesAndFaces(PolyhedralComplex4d c, int vertexOffset) {
            int edgeOffset = c.edges.Count;
            var edgeMap = new Dictionary<(int a, int b), int>();
            for (int a = 0; a < 8; a++) {
                for (int b = a + 1; b < 8; b++) {
                    int diff = a ^ b;
                    if (diff != 0 && (diff & (diff - 1)) == 0) {
                        edgeMap[(a, b)] = c.edges.Count;
                        c.edges.Add(new Edge(vertexOffset + a, vertexOffset + b));
                    }
                }
            }
            // 6 faces in a fixed order: axis 0 lo, axis 0 hi, axis 1 lo, axis 1 hi, axis 2 lo, axis 2 hi.
            for (int axis = 0; axis < 3; axis++) {
                for (int val = 0; val < 2; val++) {
                    int[] cyclic = CubeFaceCyclicVertexBits(axis, val);
                    var faceEdges = new int[4];
                    for (int k = 0; k < 4; k++) {
                        int a = cyclic[k];
                        int b = cyclic[(k + 1) % 4];
                        int lo = Math.Min(a, b);
                        int hi = Math.Max(a, b);
                        faceEdges[k] = edgeMap[(lo, hi)];
                    }
                    c.faces.Add(new Face(faceEdges));
                }
            }
        }

        static int[] CubeFaceCyclicVertexBits(int axis, int val) {
            // Returns 4 vertex bit-indices forming the face (axis fixed at val), in cyclic order.
            switch (axis) {
                case 0: { int b = val;       return new[] { b, b | 2, b | 6, b | 4 }; }
                case 1: { int b = val << 1;  return new[] { b, b | 1, b | 5, b | 4 }; }
                case 2: { int b = val << 2;  return new[] { b, b | 1, b | 3, b | 2 }; }
                default: throw new ArgumentException("axis must be 0,1,2");
            }
        }
    }
}
