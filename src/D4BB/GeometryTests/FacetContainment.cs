using System;
using System.Collections.Generic;
using D4BB.Geometry;
using NUnit.Framework;

namespace D4BB.GeometryTests
{
    public class FacetContainmentTests
    {
        [Test] public void Face2dContainmentInside() {
            Face2d facet = new(new List<Point>{
                new(0,0,0),new(1,0,0),new(1,1,0),new(0,1,0)
            });
            int containment = facet.Containment(new Point(0.5,0.5,0));
            Assert.That(containment,Is.EqualTo(HalfSpace.INSIDE));
        }
        [Test] public void Face2dContainmentBoundary() {
            Face2d facet = new(new List<Point>{
                new(0,0,0),new(1,0,0),new(1,1,0),new(0,1,0)
            });
            {
                int containment = facet.Containment(new Point(0.5,1,0));
                Assert.That(containment,Is.EqualTo(HalfSpace.CONTAINED));
            }
            {
                int containment = facet.Containment(new Point(0,0,0));
                Assert.That(containment,Is.EqualTo(HalfSpace.CONTAINED));
            }
            {
                int containment = facet.Containment(new Point(0,1,0));
                Assert.That(containment,Is.EqualTo(HalfSpace.CONTAINED));
            }
            {
                int containment = facet.Containment(new Point(1,1,0));
                Assert.That(containment,Is.EqualTo(HalfSpace.CONTAINED));
            }
            {
                int containment = facet.Containment(new Point(0,2,0));
                Assert.That(containment,Is.EqualTo(HalfSpace.OUTSIDE));
            }
            {
                int containment = facet.Containment(new Point(0,-1,0));
                Assert.That(containment,Is.EqualTo(HalfSpace.OUTSIDE));
            }
        }
        [Test] public void Face2dContainmentOutside() {
            Face2d facet = new(new List<Point>{
                new(0,0,0),new(1,0,0),new(1,1,0),new(0,1,0)
            });
            {
                int containment = facet.Containment(new Point(0.5,-0.5,0));
                Assert.That(containment,Is.EqualTo(HalfSpace.OUTSIDE));
            }
            {
                bool isCatched = false;
                try {
                    int containment = facet.Containment(new Point(0.5,0.5,0.5));
                } catch (Face2d.NotInPlaneException) {
                    isCatched = true;
                }
                Assert.That(isCatched);
            }
        }
        [Test] public void Face2dContainmentFace2d() {
            Face2d square = new(new List<Point>{
                new(0,0,0),new(1,0,0),new(1,1,0),new(0,1,0)
            });
            Assert.That(square.Contains(square));
            Face2d triangle = new(new List<Point>{
                new(0,0,0),new(1,0,0),new(1,1,0)
            });
            Assert.That(triangle.Contains(triangle));
            
            Assert.That(square.Contains(triangle),Is.True);
            Assert.That(triangle.Contains(square),Is.False);
        }
        [Test] public void TriangulationContainmentTriangle() {
            Face2d triangle = new(new List<Point>{
                new(0,0,0),new(1,0,0),new(1,1,0)
            });
            Assert.That(triangle.Contains(triangle));
            var triangles = triangle.BoundaryTriangulation2d();
            foreach (var t in triangles) {
                Assert.That(triangle.Contains(t),Is.True,$"{triangle} does not contain {t}");
            }
        }
        [Test] public void TriangleContainsTriangle() {
            Face2d triangle1 = new(new List<Point>{
                new(0,0,0),new(1,0,0),new(1,1,0)
            });
            Assert.That(triangle1.Contains(triangle1));
            Face2d triangle2 = new(new List<Point>{
                new(1,0,0),new(1,1,0),new(0,0,0)
            });
            var contains = triangle2.Contains(triangle2);
            Assert.That(contains);

            Assert.That(triangle1.Contains(triangle2));
            Assert.That(triangle2.Contains(triangle1));
        }

    }
}
