using System.Collections.Generic;
using NUnit.Framework;
using System.Linq;
using System;
using D4BB.Comb;

namespace D4BB.Geometry {
public class PrecisionTests
{
    [Test] public void PointEquality1dim() {
        {
            Point a = new(new double[]{2.15});
            Assert.That(a.Equals(a,0));
            Assert.That(a.Equals(a,5));
            Assert.That(a.Equals(a,10));
        }
        {
            Point a = new(new double[]{2.14});
            Point b = new(new double[]{2.16});
            Assert.That(a.Equals(b,0));
            Assert.That(!a.Equals(b,1));
        }
        {
            Point a = new(new double[]{2.15});
            Point b = new(new double[]{2.16});
            Assert.That(a.Equals(b,0));
            Assert.That(a.Equals(b,1));
        }
        {
            Point a = new(new double[]{2.15});
        }
    }
    [Test] public void HashSetEdgesEquality() {
        Face2d triangle1 = new(new List<Point>{new(0,0,0),new(1,0,0),new(0,1,0)});
        Face2d triangle2 = new(new List<Point>{new(1,0,0),new(0,0,0),new(0,1,0)});
        Assert.That(triangle1.Equals(triangle2));
        Assert.That(triangle1.Equals(triangle2,10));
        Assert.That(!Face2d.Face2dOrientedEquals(triangle1,triangle2));
        {
            HashSet<Face2d> face2ds = new(new Face2dUnOrientedEquality(10));
            face2ds.Add(triangle1);
            face2ds.Add(triangle2);
            Assert.That(face2ds,Has.Count.EqualTo(1));
        }
        {
            HashSet<Face2d> face2ds = new(new Face2dOrientedEquality(10));
            face2ds.Add(triangle1);
            face2ds.Add(triangle2);
            Assert.That(face2ds,Has.Count.EqualTo(2));
        }
        {
            var square1 = new Face2d(new List<Point>{new(-1,-1,1),new(-1,0,1),new(0,0,1),new(0,-1,1)});
            var square2 = new Face2d(new List<Point>{new(-1,-1,1),new(0,-1,1),new(0,0,1),new(-1,0,1)});
            HashSet<Face2d> face2ds = new(new Face2dUnOrientedEquality(10));
            face2ds.Add(square1);
            face2ds.Add(square2);
            Assert.That(face2ds,Has.Count.EqualTo(1));
        }
        {
            var square1 = new Face2d(new List<Point>{new(-1,-1,1),new(-1,0,1),new(0,0,1),new(0,-1,1)});
            var square2 = new Face2d(new List<Point>{new(-1,-1,1),new(0,-1,1),new(0,0,1),new(-1,0,1)});
            HashSet<Face2d> face2ds = new(new Face2dOrientedEquality(10));
            face2ds.Add(square1);
            face2ds.Add(square2);
            Assert.That(face2ds,Has.Count.EqualTo(2));
        }
    }
}
}