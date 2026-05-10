using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using D4BB.Geometry;
using D4BB.Geometry2;

namespace D4BB.Geometry2Tests {
    public class CellRender3dTests {

        [Test] public void FromFragment_UnitCube_HasSixFacesProjectedToOriginalCoords() {
            // Camera4dParallel cavalier maps (x,y,z,0) → (x,y,z) (identity when w=0).
            var c = Cube4dBuilder.UnitCubeAtW(0);
            var fragment = CellFragment.FromCell(c, 0);
            var camera = new Camera4dParallel();
            var rc = CellRender3d.FromFragment(fragment, camera);
            Assert.That(rc.faces.Count, Is.EqualTo(6));
            // every projected vertex coord matches (x,y,z) with z taking 0/1 etc.
            foreach (var face in rc.faces)
                foreach (var v in face) {
                    Assert.That(v.x.Length, Is.EqualTo(3));
                    foreach (var coord in v.x)
                        Assert.That(coord, Is.EqualTo(0).Within(AOP.ERR).Or.EqualTo(1).Within(AOP.ERR));
                }
        }

        [Test] public void DefiningHalfSpaces_UnitCube_AllInwardOriented() {
            var c = Cube4dBuilder.UnitCubeAtW(0);
            var fragment = CellFragment.FromCell(c, 0);
            var rc = CellRender3d.FromFragment(fragment, new Camera4dParallel());
            var hsList = rc.DefiningHalfSpaces();
            Assert.That(hsList.Length, Is.EqualTo(6));
            // Centroid (0.5,0.5,0.5) must be INSIDE every halfspace.
            var centroid = new Point(0.5, 0.5, 0.5);
            foreach (var hs in hsList)
                Assert.That(hs.side(centroid), Is.EqualTo(HalfSpace.INSIDE),
                    $"centroid {centroid} should be INSIDE {hs} but was side={hs.side(centroid)}");
            // Each normal is axis-aligned ±e_i (unit length, only one non-zero component).
            foreach (var hs in hsList) {
                int nonZero = 0;
                for (int i = 0; i < 3; i++)
                    if (System.Math.Abs(hs.normal.x[i]) > AOP.ERR) {
                        nonZero++;
                        Assert.That(System.Math.Abs(hs.normal.x[i]), Is.EqualTo(1).Within(AOP.ERR));
                    }
                Assert.That(nonZero, Is.EqualTo(1));
            }
        }

        [Test] public void CutOut_FaceFullyInside_GetsRemoved() {
            // Cell whose halfspaces define the unit cube (use a real one for setup).
            var c = Cube4dBuilder.UnitCubeAtW(0);
            var occluder = CellRender3d.FromFragment(CellFragment.FromCell(c, 0), new Camera4dParallel());
            var hs = occluder.DefiningHalfSpaces();
            // A small triangle entirely inside the unit cube (z=0.5 plane, inside).
            var inside = new CellRender3d {
                sourceCellId = -1,
                faces = new List<List<Point>> {
                    new List<Point> {
                        new Point(0.2, 0.2, 0.5),
                        new Point(0.8, 0.2, 0.5),
                        new Point(0.5, 0.8, 0.5),
                    }
                }
            };
            inside.CutOut(hs);
            Assert.That(inside.faces.Count, Is.EqualTo(0), "fully-occluded face should be removed");
        }

        [Test] public void CutOut_FaceFullyOutside_Survives() {
            var c = Cube4dBuilder.UnitCubeAtW(0);
            var occluder = CellRender3d.FromFragment(CellFragment.FromCell(c, 0), new Camera4dParallel());
            var hs = occluder.DefiningHalfSpaces();
            // Triangle far outside the unit cube
            var outside = new CellRender3d {
                sourceCellId = -1,
                faces = new List<List<Point>> {
                    new List<Point> {
                        new Point(2, 0, 0),
                        new Point(3, 0, 0),
                        new Point(2.5, 1, 0),
                    }
                }
            };
            outside.CutOut(hs);
            Assert.That(outside.faces.Count, Is.EqualTo(1), "fully-outside face should survive unchanged");
            Assert.That(outside.faces[0].Count, Is.EqualTo(3));
        }

        [Test] public void CutOut_FaceStraddling_GetsClipped() {
            var c = Cube4dBuilder.UnitCubeAtW(0);
            var occluder = CellRender3d.FromFragment(CellFragment.FromCell(c, 0), new Camera4dParallel());
            var hs = occluder.DefiningHalfSpaces();
            // Triangle whose vertices straddle the cube along x: one vertex inside, two outside.
            var straddle = new CellRender3d {
                sourceCellId = -1,
                faces = new List<List<Point>> {
                    new List<Point> {
                        new Point(0.5, 0.5, 0.5),  // inside the cube
                        new Point(2.0, 0.5, 0.5),  // outside in +x
                        new Point(2.0, 1.5, 0.5),  // outside in +x and +y
                    }
                }
            };
            straddle.CutOut(hs);
            // Should have a non-empty remaining piece; the inside vertex (0.5,0.5,0.5) should be gone.
            Assert.That(straddle.faces.Count, Is.GreaterThanOrEqualTo(1));
            foreach (var face in straddle.faces) {
                foreach (var v in face) {
                    // No surviving vertex should be strictly inside all halfspaces (strictly inside the cube).
                    bool strictlyInside = true;
                    foreach (var h in hs)
                        if (h.side(v) > -AOP.ERR) { strictlyInside = false; break; }
                    Assert.That(strictlyInside, Is.False, $"vertex {v} should not be strictly inside the cube");
                }
            }
        }

        [Test] public void CutOut_NonOverlappingCells_NoOp() {
            var camera = new Camera4dParallel();
            var c = Cube4dBuilder.UnitCubeAtW(0);
            var cube0 = CellRender3d.FromFragment(CellFragment.FromCell(c, 0), camera);
            // A second cube far away in +x
            var c2 = Cube4dBuilder.UnitCubeAt(5, 0, 0, 0);
            var cube1 = CellRender3d.FromFragment(CellFragment.FromCell(c2, 0), camera);
            int beforeFaces = cube1.faces.Count;
            int beforeVerts = cube1.faces.Sum(f => f.Count);
            cube1.CutOut(cube0.DefiningHalfSpaces());
            Assert.That(cube1.faces.Count, Is.EqualTo(beforeFaces));
            Assert.That(cube1.faces.Sum(f => f.Count), Is.EqualTo(beforeVerts));
        }
    }
}
