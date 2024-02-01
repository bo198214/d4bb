using System.Collections.Generic;
using NUnit.Framework;
using System.Linq;
using System;
using D4BB.Comb;
using Microsoft.VisualBasic;

namespace D4BB.Geometry {
public class PrecisionTests
{
    [Test] public void DoubleTruncation() {
        {
            Precision.MantissaExponent(4,out var mantissa, out var exponent, out var negative);
            Assert.That(exponent,Is.EqualTo(2));
            Assert.That(mantissa,Is.EqualTo(1));
        }
        {
            Precision.MantissaExponent(8,out var mantissa, out var exponent, out var negative);
            Assert.That(exponent,Is.EqualTo(3));
            Assert.That(mantissa,Is.EqualTo(1));
        }
        {
            Precision.MantissaExponent(-8,out var mantissa, out var exponent, out var negative);
            Assert.That(exponent,Is.EqualTo(3));
            Assert.That(mantissa,Is.EqualTo(1));
            Assert.That(negative,Is.True);
        }
        {
            Precision.MantissaExponent(-1.0/8,out var mantissa, out var exponent, out var negative);
            Assert.That(exponent,Is.EqualTo(-3));
            Assert.That(mantissa,Is.EqualTo(1));
            Assert.That(negative,Is.True);
        }
        {
            Precision.MantissaExponent(8+4,out var mantissa, out var exponent, out var negative);
            Assert.That(exponent,Is.EqualTo(2));
            Assert.That(mantissa,Is.EqualTo(2+1));
        }
        {
            Precision.MantissaExponent(8+4+2,out var mantissa, out var exponent, out var negative);
            Assert.That(exponent,Is.EqualTo(1));
            Assert.That(mantissa,Is.EqualTo(4+2+1));
        }
        {
            Precision.MantissaExponent((8+4+2+1)/16.0,out var mantissa, out var exponent, out var negative);
            Assert.That(exponent,Is.EqualTo(-4));
            Assert.That(mantissa,Is.EqualTo(8+4+2+1));
        }
    }
    [Test] public void RoundBinaryTest() {
        Assert.That(Precision.RoundBinary((8+4+2+1)/16.0,4),Is.EqualTo((8+4+2+1)/16.0));
        Assert.That(Precision.RoundBinary((8+4+2+1)/16.0,3),Is.EqualTo(1));
        Assert.That(Precision.RoundBinary((16+8+4+2+1)/32.0,4),Is.EqualTo(1));
    }
    [Test] public void TruncateBinaryTest() {
        Assert.That(Precision.TruncateBinary((8+4+2+1)/16.0,4),Is.EqualTo((8+4+2+1)/16.0));
        Assert.That(Precision.TruncateBinary(8+4+2+1,-1),Is.EqualTo(8+4+2));
        Assert.That(Precision.TruncateBinary((8+4+2+1)/16.0,3),Is.EqualTo((8+4+2)/16.0));
        Assert.That(Precision.TruncateBinary((8+4+2+1)/16.0,2),Is.EqualTo((8+4)/16.0));
        Assert.That(Precision.TruncateBinary(1/64.0,5),Is.EqualTo(0));
        Assert.That(Precision.TruncateBinary(1/64.0,6),Is.EqualTo(1/64.0));
    }
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
        {
            var square1 = new Face2d(new List<Point>{new(-1,-1,1),new(-1,0,1),new(0,0,1),new(0,-1,1)});
            var square2 = new Face2d(new List<Point>{new(-1,-1.001,1),new(0,-1,1),new(0,0,1),new(-1,0,1)});
            HashSet<Face2d> face2ds = new(new Face2dUnOrientedEquality(2));
            face2ds.Add(square1);
            face2ds.Add(square2);
            Assert.That(face2ds,Has.Count.EqualTo(1));
        }
        {
            var square1 = new Face2d(new List<Point>{new(-1,-1,1),new(-1,0,1),new(0,0,1),new(0,-1,1)});
            var square2 = new Face2d(new List<Point>{new(-1,-1.001,1),new(0,-1,1),new(0,0,1),new(-1,0,1)});
            HashSet<Face2d> face2ds = new(new Face2dUnOrientedEquality(3));
            face2ds.Add(square1);
            face2ds.Add(square2);
            Assert.That(face2ds,Has.Count.EqualTo(2));
        }
    }
}
}