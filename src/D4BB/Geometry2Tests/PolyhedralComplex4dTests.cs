using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using D4BB.Geometry;
using D4BB.Geometry2;
using Edge = D4BB.Geometry2.Edge;

namespace D4BB.Geometry2Tests {
    public class PolyhedralComplex4dTests {

        [Test] public void SingleCube_Counts() {
            var c = Cube4dBuilder.UnitCubeAtW(0);
            Assert.That(c.vertices.Count, Is.EqualTo(8));
            Assert.That(c.edges.Count, Is.EqualTo(12));
            Assert.That(c.faces.Count, Is.EqualTo(6));
            Assert.That(c.cells.Count, Is.EqualTo(1));
        }

        [Test] public void SingleCube_AllFacesAreNonCoplanar() {
            // A single cube has 6 boundary faces (each face has only one parent cell)
            // → all are non-coplanar by definition.
            var c = Cube4dBuilder.UnitCubeAtW(0);
            Assert.That(c.NonCoplanarFaceIds().Count(), Is.EqualTo(6));
        }

        [Test] public void SingleCube_AllEdgesAreNonCoplanar() {
            // Each edge of the cube has 2 incident faces; those faces are perpendicular
            // (in a 2-plane that differs by one axis) → edge is non-coplanar.
            var c = Cube4dBuilder.UnitCubeAtW(0);
            Assert.That(c.NonCoplanarEdgeIds().Count(), Is.EqualTo(12));
        }

        [Test] public void SingleCube_HyperplaneNormalIsAlongW() {
            var c = Cube4dBuilder.UnitCubeAtW(2.5);
            var hs = c.CellHyperplane(0);
            Assert.That(System.Math.Abs(hs.normal.x[3]), Is.EqualTo(1).Within(AOP.ERR));
            for (int i = 0; i < 3; i++)
                Assert.That(System.Math.Abs(hs.normal.x[i]), Is.LessThan(AOP.ERR));
            Assert.That(System.Math.Abs(hs.length), Is.EqualTo(2.5).Within(AOP.ERR));
        }

        [Test] public void SingleCube_NormalRespectsCellNormalSign() {
            var n = new Point(0, 0, 0, 1);
            var c = Cube4dBuilder.UnitCubeAtW(0, cellNormal: n);
            var hs = c.CellHyperplane(0);
            Assert.That(hs.normal.x[3], Is.EqualTo(1).Within(AOP.ERR));
        }

        [Test] public void TwoCoplanarCubes_SharedFaceIsCoplanar() {
            // Two cubes both in w=0 hyperplane, side by side at x ∈ [0,1] and x ∈ [1,2].
            var c = TwoCubesSharingFaceInSameHyperplane();
            // The shared face is the one whose vertices all have x=1 (computed below).
            int sharedFaceId = -1;
            for (int f = 0; f < c.faces.Count; f++) {
                if (c.CellsPerFace(f).Count == 2) { sharedFaceId = f; break; }
            }
            Assert.That(sharedFaceId, Is.GreaterThanOrEqualTo(0));
            Assert.That(c.IsCoplanarFace(sharedFaceId), Is.True);
            // The other 10 faces are boundary (one parent only) → non-coplanar.
            int nonCoplanar = 0;
            for (int i = 0; i < c.faces.Count; i++)
                if (!c.IsCoplanarFace(i)) nonCoplanar++;
            Assert.That(nonCoplanar, Is.EqualTo(10));
        }

        [Test] public void TwoCubesInDifferentHyperplanes_SharedFaceIsNotCoplanar() {
            // Cube A in w=0 hyperplane (x,y,z ∈ [0,1]).
            // Cube B in x=1 hyperplane (y,z,w ∈ [0,1]).
            // They share the 2-face at x=1, w=0, y,z ∈ [0,1].
            var c = TwoCubesSharingFaceInDifferentHyperplanes();
            int sharedFaceId = -1;
            for (int f = 0; f < c.faces.Count; f++) {
                if (c.CellsPerFace(f).Count == 2) { sharedFaceId = f; break; }
            }
            Assert.That(sharedFaceId, Is.GreaterThanOrEqualTo(0));
            Assert.That(c.IsCoplanarFace(sharedFaceId), Is.False);
        }

        [Test] public void FreeFloatingFace_IsNonCoplanar() {
            var c = new PolyhedralComplex4d();
            c.vertices.Add(new Point(0, 0, 0, 0));
            c.vertices.Add(new Point(1, 0, 0, 0));
            c.vertices.Add(new Point(1, 1, 0, 0));
            c.vertices.Add(new Point(0, 1, 0, 0));
            c.edges.Add(new Edge(0, 1));
            c.edges.Add(new Edge(1, 2));
            c.edges.Add(new Edge(2, 3));
            c.edges.Add(new Edge(3, 0));
            c.faces.Add(new Face(new[] { 0, 1, 2, 3 }));
            Assert.That(c.IsCoplanarFace(0), Is.False);
            Assert.That(c.NonCoplanarFaceIds().Count(), Is.EqualTo(1));
        }

        [Test] public void TwoCoplanarFacesShareAnEdge_EdgeIsCoplanar() {
            // Two unit squares in the (x,y)-plane (z=w=0) sharing the edge x=1, y∈[0,1].
            var c = new PolyhedralComplex4d();
            c.vertices.Add(new Point(0, 0, 0, 0));  // 0
            c.vertices.Add(new Point(1, 0, 0, 0));  // 1
            c.vertices.Add(new Point(1, 1, 0, 0));  // 2
            c.vertices.Add(new Point(0, 1, 0, 0));  // 3
            c.vertices.Add(new Point(2, 0, 0, 0));  // 4
            c.vertices.Add(new Point(2, 1, 0, 0));  // 5
            c.edges.Add(new Edge(0, 1));  // 0
            c.edges.Add(new Edge(1, 2));  // 1 shared
            c.edges.Add(new Edge(2, 3));  // 2
            c.edges.Add(new Edge(3, 0));  // 3
            c.edges.Add(new Edge(1, 4));  // 4
            c.edges.Add(new Edge(4, 5));  // 5
            c.edges.Add(new Edge(5, 2));  // 6
            c.faces.Add(new Face(new[] { 0, 1, 2, 3 }));
            c.faces.Add(new Face(new[] { 4, 5, 6, 1 }));
            Assert.That(c.IsCoplanarEdge(1), Is.True);
            Assert.That(c.IsCoplanarEdge(0), Is.False);  // boundary edge of one face only
        }

        [Test] public void TwoNonCoplanarFacesShareAnEdge_EdgeIsNotCoplanar() {
            // One square in (x,y)-plane, one in (x,z)-plane, sharing the x-axis edge.
            var c = new PolyhedralComplex4d();
            c.vertices.Add(new Point(0, 0, 0, 0));  // 0
            c.vertices.Add(new Point(1, 0, 0, 0));  // 1
            c.vertices.Add(new Point(1, 1, 0, 0));  // 2
            c.vertices.Add(new Point(0, 1, 0, 0));  // 3
            c.vertices.Add(new Point(1, 0, 1, 0));  // 4
            c.vertices.Add(new Point(0, 0, 1, 0));  // 5
            c.edges.Add(new Edge(0, 1));  // 0 shared
            c.edges.Add(new Edge(1, 2));  // 1
            c.edges.Add(new Edge(2, 3));  // 2
            c.edges.Add(new Edge(3, 0));  // 3
            c.edges.Add(new Edge(1, 4));  // 4
            c.edges.Add(new Edge(4, 5));  // 5
            c.edges.Add(new Edge(5, 0));  // 6
            c.faces.Add(new Face(new[] { 0, 1, 2, 3 }));
            c.faces.Add(new Face(new[] { 0, 4, 5, 6 }));
            Assert.That(c.IsCoplanarEdge(0), Is.False);
        }

        // ── helpers ───────────────────────────────────────────────────────────

        /// Builds two cubes side-by-side at x∈[0,1] and x∈[1,2], both at w=0,
        /// sharing the x=1 face. 12 vertices, 20 edges, 11 faces, 2 cells.
        static PolyhedralComplex4d TwoCubesSharingFaceInSameHyperplane() {
            var c = new PolyhedralComplex4d();
            // Vertex grid: x ∈ {0,1,2}, y,z ∈ {0,1}, w=0
            int VID(int x, int y, int z) => x * 4 + y * 2 + z;
            for (int x = 0; x <= 2; x++)
                for (int y = 0; y < 2; y++)
                    for (int z = 0; z < 2; z++)
                        c.vertices.Add(new Point(x, y, z, 0));
            var edgeMap = new Dictionary<(int, int), int>();
            void AddEdge(int a, int b) {
                int lo = System.Math.Min(a, b), hi = System.Math.Max(a, b);
                if (edgeMap.ContainsKey((lo, hi))) return;
                edgeMap[(lo, hi)] = c.edges.Count;
                c.edges.Add(new Edge(lo, hi));
            }
            int EID(int a, int b) {
                int lo = System.Math.Min(a, b), hi = System.Math.Max(a, b);
                return edgeMap[(lo, hi)];
            }
            for (int x = 0; x <= 2; x++)
                for (int y = 0; y < 2; y++)
                    for (int z = 0; z < 2; z++) {
                        if (x < 2) AddEdge(VID(x, y, z), VID(x + 1, y, z));
                        if (y < 1) AddEdge(VID(x, y, z), VID(x, y + 1, z));
                        if (z < 1) AddEdge(VID(x, y, z), VID(x, y, z + 1));
                    }
            int AddFace(int v0, int v1, int v2, int v3) {
                int fIdx = c.faces.Count;
                c.faces.Add(new Face(new[] { EID(v0, v1), EID(v1, v2), EID(v2, v3), EID(v3, v0) }));
                return fIdx;
            }
            int fA_x0     = AddFace(VID(0,0,0), VID(0,1,0), VID(0,1,1), VID(0,0,1));
            int fShared   = AddFace(VID(1,0,0), VID(1,1,0), VID(1,1,1), VID(1,0,1));
            int fA_y0     = AddFace(VID(0,0,0), VID(1,0,0), VID(1,0,1), VID(0,0,1));
            int fA_y1     = AddFace(VID(0,1,0), VID(1,1,0), VID(1,1,1), VID(0,1,1));
            int fA_z0     = AddFace(VID(0,0,0), VID(1,0,0), VID(1,1,0), VID(0,1,0));
            int fA_z1     = AddFace(VID(0,0,1), VID(1,0,1), VID(1,1,1), VID(0,1,1));
            int fB_x2     = AddFace(VID(2,0,0), VID(2,1,0), VID(2,1,1), VID(2,0,1));
            int fB_y0     = AddFace(VID(1,0,0), VID(2,0,0), VID(2,0,1), VID(1,0,1));
            int fB_y1     = AddFace(VID(1,1,0), VID(2,1,0), VID(2,1,1), VID(1,1,1));
            int fB_z0     = AddFace(VID(1,0,0), VID(2,0,0), VID(2,1,0), VID(1,1,0));
            int fB_z1     = AddFace(VID(1,0,1), VID(2,0,1), VID(2,1,1), VID(1,1,1));
            c.cells.Add(new Cell(new[] { fA_x0, fShared, fA_y0, fA_y1, fA_z0, fA_z1 }));
            c.cells.Add(new Cell(new[] { fB_x2, fShared, fB_y0, fB_y1, fB_z0, fB_z1 }));
            return c;
        }

        /// Builds Cube A (w=0) and Cube B (x=1), sharing the 2-face at x=1,w=0,y,z∈[0,1].
        static PolyhedralComplex4d TwoCubesSharingFaceInDifferentHyperplanes() {
            var c = new PolyhedralComplex4d();
            // Cube A: bits = (x,y,z), w=0  → 8 vertices, indices 0..7
            for (int i = 0; i < 8; i++)
                c.vertices.Add(new Point(i & 1, (i >> 1) & 1, (i >> 2) & 1, 0));
            // Cube B: x=1 always; bits = (y,z,w). Reuse A's x=1 vertices for w=0.
            // New vertices for w=1: indices 8..11 = (y,z) ∈ {0,1}²
            for (int by = 0; by < 2; by++)
                for (int bz = 0; bz < 2; bz++)
                    c.vertices.Add(new Point(1, by, bz, 1));
            int AVID(int x, int y, int z) => x | (y << 1) | (z << 2);
            int BVID(int y, int z, int w) {
                if (w == 0) return AVID(1, y, z);
                return 8 + y + (z << 1);
            }

            var edgeMap = new Dictionary<(int, int), int>();
            void AddEdge(int a, int b) {
                int lo = System.Math.Min(a, b), hi = System.Math.Max(a, b);
                if (edgeMap.ContainsKey((lo, hi))) return;
                edgeMap[(lo, hi)] = c.edges.Count;
                c.edges.Add(new Edge(lo, hi));
            }
            int EID(int a, int b) {
                int lo = System.Math.Min(a, b), hi = System.Math.Max(a, b);
                return edgeMap[(lo, hi)];
            }
            // A edges
            for (int i = 0; i < 8; i++)
                for (int axis = 0; axis < 3; axis++) {
                    int j = i ^ (1 << axis);
                    if (j > i) AddEdge(i, j);
                }
            // B edges
            for (int by = 0; by < 2; by++)
                for (int bz = 0; bz < 2; bz++)
                    for (int bw = 0; bw < 2; bw++) {
                        int v = BVID(by, bz, bw);
                        if (by == 0) AddEdge(v, BVID(1, bz, bw));
                        if (bz == 0) AddEdge(v, BVID(by, 1, bw));
                        if (bw == 0) AddEdge(v, BVID(by, bz, 1));
                    }
            int AddFace(int v0, int v1, int v2, int v3) {
                int fIdx = c.faces.Count;
                c.faces.Add(new Face(new[] { EID(v0, v1), EID(v1, v2), EID(v2, v3), EID(v3, v0) }));
                return fIdx;
            }
            // Cube A's 6 faces
            int fA_x0 = AddFace(AVID(0,0,0), AVID(0,1,0), AVID(0,1,1), AVID(0,0,1));
            int fShared = AddFace(AVID(1,0,0), AVID(1,1,0), AVID(1,1,1), AVID(1,0,1));  // also B's w=0 face
            int fA_y0 = AddFace(AVID(0,0,0), AVID(1,0,0), AVID(1,0,1), AVID(0,0,1));
            int fA_y1 = AddFace(AVID(0,1,0), AVID(1,1,0), AVID(1,1,1), AVID(0,1,1));
            int fA_z0 = AddFace(AVID(0,0,0), AVID(1,0,0), AVID(1,1,0), AVID(0,1,0));
            int fA_z1 = AddFace(AVID(0,0,1), AVID(1,0,1), AVID(1,1,1), AVID(0,1,1));
            // Cube B's 5 other faces (not the shared w=0)
            int fB_w1 = AddFace(BVID(0,0,1), BVID(1,0,1), BVID(1,1,1), BVID(0,1,1));
            int fB_y0 = AddFace(BVID(0,0,0), BVID(0,0,1), BVID(0,1,1), BVID(0,1,0));
            int fB_y1 = AddFace(BVID(1,0,0), BVID(1,0,1), BVID(1,1,1), BVID(1,1,0));
            int fB_z0 = AddFace(BVID(0,0,0), BVID(1,0,0), BVID(1,0,1), BVID(0,0,1));
            int fB_z1 = AddFace(BVID(0,1,0), BVID(1,1,0), BVID(1,1,1), BVID(0,1,1));
            c.cells.Add(new Cell(new[] { fA_x0, fShared, fA_y0, fA_y1, fA_z0, fA_z1 }));
            c.cells.Add(new Cell(new[] { fShared, fB_w1, fB_y0, fB_y1, fB_z0, fB_z1 }));
            return c;
        }
    }
}
