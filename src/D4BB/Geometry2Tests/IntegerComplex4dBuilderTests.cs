using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using D4BB.Comb;
using D4BB.Geometry;
using D4BB.Geometry2;

namespace D4BB.Geometry2Tests {
    public class IntegerComplex4dBuilderTests {

        [Test] public void SingleTesseract_BoundaryCounts() {
            // A 4-cube has 16 vertices, 32 edges, 24 squares (2-faces) and 8 cubes (3-cells) on its boundary.
            var t = new IntegerCell(new[] { 0, 0, 0, 0 });
            var c = IntegerComplex4dBuilder.Boundary(new[] { t });
            Assert.That(c.cells.Count, Is.EqualTo(8));
            Assert.That(c.faces.Count, Is.EqualTo(24));
            Assert.That(c.edges.Count, Is.EqualTo(32));
            Assert.That(c.vertices.Count, Is.EqualTo(16));
        }

        [Test] public void SingleTesseract_AllCellsHaveOutwardNormalAlongOneAxis() {
            var t = new IntegerCell(new[] { 0, 0, 0, 0 });
            var c = IntegerComplex4dBuilder.Boundary(new[] { t });
            // Each boundary cube of a unit tesseract sits on a single axis-aligned 3-hyperplane;
            // its outward normal must be ±e_i for some i.
            foreach (var cell in c.cells) {
                Assert.That(cell.normal, Is.Not.Null);
                int nonZero = 0;
                for (int i = 0; i < 4; i++) {
                    double v = cell.normal.x[i];
                    if (System.Math.Abs(v) > AOP.ERR) {
                        nonZero++;
                        Assert.That(System.Math.Abs(v), Is.EqualTo(1).Within(AOP.ERR));
                    }
                }
                Assert.That(nonZero, Is.EqualTo(1));
            }
        }

        [Test] public void SingleTesseract_AllFacesAreNonCoplanar() {
            // Internal 2-faces of a tesseract's boundary are shared by two cubes that lie in
            // DIFFERENT axis-aligned 3-hyperplanes → they are not coplanar.
            var t = new IntegerCell(new[] { 0, 0, 0, 0 });
            var c = IntegerComplex4dBuilder.Boundary(new[] { t });
            Assert.That(c.NonCoplanarFaceIds().Count(), Is.EqualTo(24));
        }

        [Test] public void TwoAdjacentTesseracts_SharedCubeCancels() {
            // Two tesseracts at (0,0,0,0) and (1,0,0,0) share one 3-cube interface.
            // That cube belongs to both tesseracts' boundary and cancels → 8+8-2 = 14 cubes.
            var t0 = new IntegerCell(new[] { 0, 0, 0, 0 });
            var t1 = new IntegerCell(new[] { 1, 0, 0, 0 });
            var c = IntegerComplex4dBuilder.Boundary(new[] { t0, t1 });
            Assert.That(c.cells.Count, Is.EqualTo(14));
            // In a closed 3-manifold every 2-face is shared by exactly 2 cells.
            // Total face-occurrences = 14*6 = 84 → 42 unique faces.
            Assert.That(c.faces.Count, Is.EqualTo(42));
        }

        [Test] public void SingleTesseract_BspBackToFrontYieldsFrontFacingCubesOnly() {
            // Camera looking from outside along the +w axis at a tesseract at the origin.
            // Backface culling should drop the "+w" cube (away from camera) but keep the others.
            var t = new IntegerCell(new[] { 0, 0, 0, 0 });
            var c = IntegerComplex4dBuilder.Boundary(new[] { t });
            var bsp = Bsp4d.Build(c);
            // Eye at (0.5, 0.5, 0.5, -5) — inside the xyz-square footprint, far in -w.
            var cam = new Camera4dCentral(new Point4d(0.5, 0.5, 0.5, -5));
            var visible = bsp.BackToFront(cam).ToList();
            // Visible cubes: those whose outward normal has positive component toward the camera.
            // Camera is on -w side; the cube at w=0 (normal -w) faces the camera, the w=1 cube (normal +w) does not.
            // Other 6 cubes have normals along ±x, ±y, ±z; all are at the boundary "slab" w∈[0,1].
            // For each ±x,±y,±z cube, IsFacedBy depends on (eye - origin)·normal sign.
            // Cubes with normal +x: origin x=1, eye x=0.5 → (0.5-1, ..., ...) · (1,0,0,0) = -0.5 < 0 → not faced.
            // Cubes with normal -x: origin x=0, eye x=0.5 → (0.5-0, ..., ...) · (-1,0,0,0) = -0.5 < 0 → not faced.
            // Hmm — so only the -w cube is faced (eye at -5 vs origin w=0: (0-(-5)?)...
            // Wait: the -w-cube has normal (0,0,0,-1). Its origin (any vertex) has w=0.
            // (eye - origin) · normal = ((0.5,0.5,0.5,-5)-(*,*,*,0)) · (0,0,0,-1) = -(-5) = 5 > 0 → faced. Good.
            // For ±x,±y,±z cubes: all four vertices of the camera-facing face for these cubes have w∈{0,1}.
            // The cube's representative vertex passed to IsFacedBy could be at w=0 or w=1.
            // For Camera4dCentral.IsFacedBy: returns normal.sc(origin, eye) > 0 = normal · (eye - origin) > 0.
            // For +x cube (vertices at x=1, w∈{0,1}): pick origin=(1,*,*,0) → eye-origin=(-0.5,*,*,-5).
            //   dot with (1,0,0,0) = -0.5 → not faced (correctly, since it points away in x).
            // So only the -w cube is yielded.
            Assert.That(visible.Count, Is.EqualTo(1));
            Assert.That(visible[0].normal.x[3], Is.EqualTo(-1).Within(AOP.ERR));
        }

        [Test] public void SingleTesseract_NoBackfaceCulling_AllCellsYielded() {
            var t = new IntegerCell(new[] { 0, 0, 0, 0 });
            var c = IntegerComplex4dBuilder.Boundary(new[] { t });
            // Strip out the normals to disable culling
            var cellsWithoutNormals = new List<Cell>();
            foreach (var cell in c.cells) cellsWithoutNormals.Add(new Cell(cell.faceIds, normal: null));
            var c2 = new PolyhedralComplex4d(c.vertices, c.edges, c.faces, cellsWithoutNormals);
            var bsp = Bsp4d.Build(c2);
            var cam = new Camera4dCentral(new Point4d(0.5, 0.5, 0.5, -5));
            var visible = bsp.BackToFront(cam).ToList();
            Assert.That(visible.Count, Is.EqualTo(8));
        }
    }
}
