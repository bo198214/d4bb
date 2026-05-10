using System.IO;
using System.Linq;
using NUnit.Framework;
using D4BB.Comb;
using D4BB.Geometry;
using D4BB.Geometry2;

namespace D4BB.Geometry2Tests {
    public class PolyhedralComplex4dJsonTests {

        [Test] public void SingleCube_RoundTrips() {
            var c = Cube4dBuilder.UnitCubeAtW(0, cellNormal: new Point(0, 0, 0, 1));
            var json = PolyhedralComplex4dJson.ToJson(c, name: "cube", description: "a single 3-cube at w=0");
            var c2 = PolyhedralComplex4dJson.FromJson(json);

            Assert.That(c2.vertices.Count, Is.EqualTo(c.vertices.Count));
            Assert.That(c2.edges.Count, Is.EqualTo(c.edges.Count));
            Assert.That(c2.faces.Count, Is.EqualTo(c.faces.Count));
            Assert.That(c2.cells.Count, Is.EqualTo(c.cells.Count));

            // Vertices match by position
            for (int i = 0; i < c.vertices.Count; i++)
                for (int k = 0; k < 4; k++)
                    Assert.That(c2.vertices[i].x[k], Is.EqualTo(c.vertices[i].x[k]).Within(AOP.ERR));

            // Cell normals preserved
            Assert.That(c2.cells[0].normal, Is.Not.Null);
            for (int k = 0; k < 4; k++)
                Assert.That(c2.cells[0].normal.x[k], Is.EqualTo(c.cells[0].normal.x[k]).Within(AOP.ERR));
        }

        [Test] public void Tesseract_RoundTrips_AndPreservesIncidence() {
            var c = IntegerComplex4dBuilder.Boundary(new[] { new IntegerCell(new[] { 0, 0, 0, 0 }) });
            var json = PolyhedralComplex4dJson.ToJson(c, name: "tesseract");
            var c2 = PolyhedralComplex4dJson.FromJson(json);

            Assert.That(c2.vertices.Count, Is.EqualTo(16));
            Assert.That(c2.edges.Count, Is.EqualTo(32));
            Assert.That(c2.faces.Count, Is.EqualTo(24));
            Assert.That(c2.cells.Count, Is.EqualTo(8));
            // Each face still has exactly one parent cell? No — for a closed manifold each face has 2.
            // But for a single tesseract's boundary, every face is shared by 2 cubes.
            for (int f = 0; f < c2.faces.Count; f++)
                Assert.That(c2.CellsPerFace(f).Count, Is.EqualTo(2),
                    $"face {f} should have 2 parent cells in the tesseract boundary");
            // All cells have outward normals
            foreach (var cell in c2.cells) {
                Assert.That(cell.normal, Is.Not.Null);
                int nonZero = 0;
                for (int k = 0; k < 4; k++) if (System.Math.Abs(cell.normal.x[k]) > AOP.ERR) nonZero++;
                Assert.That(nonZero, Is.EqualTo(1));
            }
        }

        [Test] public void NoNormals_RoundTripsWithoutNormalsField() {
            var c = Cube4dBuilder.UnitCubeAtW(0);  // no normal set
            var json = PolyhedralComplex4dJson.ToJson(c);
            Assert.That(json.Contains("normals"), Is.False);
            var c2 = PolyhedralComplex4dJson.FromJson(json);
            Assert.That(c2.cells[0].normal, Is.Null);
        }

        [Test] public void Tesseract_RoundTrip_BspAgreesOnVisibleCells() {
            var c = IntegerComplex4dBuilder.Boundary(new[] { new IntegerCell(new[] { 0, 0, 0, 0 }) });
            var json = PolyhedralComplex4dJson.ToJson(c);
            var c2 = PolyhedralComplex4dJson.FromJson(json);

            var bsp1 = Bsp4d.Build(c);
            var bsp2 = Bsp4d.Build(c2);
            var cam = new Camera4dCentral(new Point4d(0.5, 0.5, 0.5, -5));
            var v1 = bsp1.BackToFront(cam).Select(f => f.sourceCellId).ToList();
            var v2 = bsp2.BackToFront(cam).Select(f => f.sourceCellId).ToList();
            Assert.That(v2, Is.EquivalentTo(v1));
        }

        [Test, TestCase("tes.json"), TestCase("hap.json"), TestCase("rico.json"),
                       TestCase("tip.json"), TestCase("tappy.json"), TestCase("dappat.json")]
        public void NonTriangleFaceFile_LoadsViaCycleReconstruction(string fileName) {
            // These files store face vertices as sorted sets, not cyclic orderings.
            // The parser must reconstruct cyclic order from the edge graph; otherwise
            // squares like [1,3,5,12] would map to non-existent edges (3,5)/(12,1).
            var assemblyDir = Path.GetDirectoryName(typeof(PolyhedralComplex4dJsonTests).Assembly.Location);
            var d = new DirectoryInfo(assemblyDir);
            string path = null;
            while (d != null) {
                var p = Path.Combine(d.FullName, "Assets", "tesserian", "Resources", "polychora", fileName);
                if (File.Exists(p)) { path = p; break; }
                d = d.Parent;
            }
            if (path == null) { Assert.Ignore($"{fileName} not found"); return; }

            var c = PolyhedralComplex4dJson.FromJson(File.ReadAllText(path));
            Assert.That(c.cells.Count, Is.GreaterThan(0));
            // Each face's edgeIds must form a closed cycle of valid edges (already enforced
            // during parse — this just asserts shape sanity).
            for (int f = 0; f < c.faces.Count; f++) {
                var face = c.faces[f];
                Assert.That(face.edgeIds.Length, Is.GreaterThanOrEqualTo(3),
                    $"{fileName}: face {f} has only {face.edgeIds.Length} edges");
                // FaceVertexIds traverses the cycle and asserts adjacent edges share a vertex.
                var vIds = c.FaceVertexIds(f);
                Assert.That(vIds.Length, Is.EqualTo(face.edgeIds.Length));
            }
        }

        [Test] public void ExistingPenJson_LoadsAndExercisesBsp() {
            // Locate Assets/tesserian/Resources/polychora/pen.json relative to the repo root.
            // Walk up from the test assembly to find a directory containing "Assets/tesserian".
            var assemblyDir = Path.GetDirectoryName(typeof(PolyhedralComplex4dJsonTests).Assembly.Location);
            var d = new DirectoryInfo(assemblyDir);
            string penPath = null;
            while (d != null) {
                var candidate = Path.Combine(d.FullName, "Assets", "tesserian", "Resources", "polychora", "pen.json");
                if (File.Exists(candidate)) { penPath = candidate; break; }
                d = d.Parent;
            }
            if (penPath == null) {
                Assert.Ignore("pen.json not found in containing repo; skipping live-file test");
                return;
            }
            var c = PolyhedralComplex4dJson.FromJson(File.ReadAllText(penPath));
            // Pentachoron: 5 vertices, 10 edges, 10 triangular faces, 5 tetrahedral cells, 5 normals.
            Assert.That(c.vertices.Count, Is.EqualTo(5));
            Assert.That(c.edges.Count, Is.EqualTo(10));
            Assert.That(c.faces.Count, Is.EqualTo(10));
            Assert.That(c.cells.Count, Is.EqualTo(5));
            foreach (var cell in c.cells) Assert.That(cell.normal, Is.Not.Null);
            // Each triangular face must have exactly 3 edges
            foreach (var face in c.faces) Assert.That(face.edgeIds.Length, Is.EqualTo(3));
            // Each tetrahedral cell must have exactly 4 faces
            foreach (var cell in c.cells) Assert.That(cell.faceIds.Length, Is.EqualTo(4));

            // BSP build + back-to-front yields some cells (camera somewhere outside)
            var bsp = Bsp4d.Build(c);
            var cam = new Camera4dCentral(new Point4d(0, 0, 0, -5));
            var visible = bsp.BackToFront(cam).ToList();
            Assert.That(visible.Count, Is.GreaterThan(0));
            Assert.That(visible.Count, Is.LessThanOrEqualTo(5));
        }

        [Test] public void MarkedFaces_RoundTrip() {
            var c = Cube4dBuilder.UnitCubeAtW(0, cellNormal: new Point(0, 0, 0, 1));
            c.markedFaces.Add(2);
            c.markedFaces.Add(5);
            var json = PolyhedralComplex4dJson.ToJson(c, name: "cube");
            Assert.That(json, Does.Contain("\"marked_faces\""));
            var c2 = PolyhedralComplex4dJson.FromJson(json);
            Assert.That(c2.markedFaces, Is.EquivalentTo(new[] { 2, 5 }));
        }

        [Test] public void MarkedFaces_AbsentField_DefaultsEmpty() {
            // Round-tripping a complex with no marks must not introduce the field.
            var c = Cube4dBuilder.UnitCubeAtW(0, cellNormal: new Point(0, 0, 0, 1));
            var json = PolyhedralComplex4dJson.ToJson(c, name: "cube");
            Assert.That(json, Does.Not.Contain("marked_faces"));
            var c2 = PolyhedralComplex4dJson.FromJson(json);
            Assert.That(c2.markedFaces, Is.Empty);
        }
    }
}
