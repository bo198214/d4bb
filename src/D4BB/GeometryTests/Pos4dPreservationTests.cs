using System.Collections.Generic;
using NUnit.Framework;

namespace D4BB.Geometry
{
// Tests for Vertex.pos4d preservation through cut/clone operations.
//
// Why: DepthTrueColoredGlass.shader reads pos4d (via UV3) to derive per-vertex spectral
// color from the 4D w-coordinate.  Game.OnFaceMeshUpdated writes vertices4d[k] = v.pos4d
// for every mesh vertex.  When the rendering pipeline cuts polygons (4D occlusion in
// Game, hidden-surface removal in scenes), newly introduced Vertex instances must carry
// pos4d that linearly interpolates the source edge's pos4d — otherwise cut-introduced
// vertices fall back to the 0-depth color and produce visible discontinuities at cut edges.
//
// End-to-end PBC test lives in TransformsTests/Pos4dPreservationTests.cs (depends on
// Transforms assembly, which Geometry/GeometryTests must not reference).
public class Pos4dPreservationTests
{
    // ── OpposingClone (the bug we hit: lost pos4d on Face2d.Split's cut-edge endpoints) ─

    [Test] public void OpposingClone_CopiesPos4d() {
        var v = new Vertex(new Point(1, 2, 3));
        v.pos4d = new double[] { 1, 2, 3, 7 };
        var clone = (Vertex)v.OpposingClone();
        AssertPos4dEquals(clone.pos4d, new double[] { 1, 2, 3, 7 });
        Assert.That(clone.getPoint(), Is.EqualTo(v.getPoint()),
            "OpposingClone preserves the 3D position (sanity).");
    }

    [Test] public void OpposingClone_NullPos4d_StaysNull() {
        var v = new Vertex(new Point(1, 2, 3));  // pos4d not set → null
        var clone = (Vertex)v.OpposingClone();
        Assert.That(clone.pos4d, Is.Null,
            "Null pos4d must not crash OpposingClone (pure-3D callers stay unaffected).");
    }

    // ── Edge.Split — interpolation along the cut segment ────────────────────

    [Test] public void EdgeSplit_InsideOutside_InterpolatesPos4dAtMidpoint() {
        // Edge from x=0 (pos4d w=0) to x=2 (pos4d w=8); cut at x=1 → t=0.5 → w=4.
        var a = new Vertex(new Point(0, 0, 0)); a.pos4d = new double[] { 0, 0, 0, 0 };
        var b = new Vertex(new Point(2, 0, 0)); b.pos4d = new double[] { 2, 0, 0, 8 };
        var edge = new Edge(a, b);
        var cutPlane = new HalfSpace(new Point(1, 0, 0), new Point(1, 0, 0));
        var sr = edge.Split(cutPlane);

        AssertPos4dEquals(((Vertex)sr.innerCut).pos4d, new double[] { 1, 0, 0, 4 });
        AssertPos4dEquals(((Vertex)sr.outerCut).pos4d, new double[] { 1, 0, 0, 4 });
    }

    [Test] public void EdgeSplit_OutsideInside_InterpolatesPos4dAtMidpoint() {
        // a is OUTSIDE, b is INSIDE — exercises the second branch of Edge.Split.
        var a = new Vertex(new Point(2, 0, 0)); a.pos4d = new double[] { 2, 0, 0, 8 };
        var b = new Vertex(new Point(0, 0, 0)); b.pos4d = new double[] { 0, 0, 0, 0 };
        var edge = new Edge(a, b);
        var cutPlane = new HalfSpace(new Point(1, 0, 0), new Point(1, 0, 0));
        var sr = edge.Split(cutPlane);

        AssertPos4dEquals(((Vertex)sr.innerCut).pos4d, new double[] { 1, 0, 0, 4 });
        AssertPos4dEquals(((Vertex)sr.outerCut).pos4d, new double[] { 1, 0, 0, 4 });
    }

    [Test] public void EdgeSplit_AsymmetricCut_InterpolatesProportionally() {
        // Cut at x=1.5 on edge from x=0 (w=0) to x=2 (w=8) → t=0.75 → w=6.
        var a = new Vertex(new Point(0, 0, 0)); a.pos4d = new double[] { 0, 0, 0, 0 };
        var b = new Vertex(new Point(2, 0, 0)); b.pos4d = new double[] { 2, 0, 0, 8 };
        var edge = new Edge(a, b);
        var cutPlane = new HalfSpace(new Point(1.5, 0, 0), new Point(1, 0, 0));
        var sr = edge.Split(cutPlane);

        AssertPos4dEquals(((Vertex)sr.innerCut).pos4d, new double[] { 1.5, 0, 0, 6 });
    }

    [Test] public void EdgeSplit_NullPos4dInputs_ProducesNullCutPos4d() {
        var a = new Vertex(new Point(0, 0, 0));  // pos4d = null
        var b = new Vertex(new Point(2, 0, 0));  // pos4d = null
        var edge = new Edge(a, b);
        var cutPlane = new HalfSpace(new Point(1, 0, 0), new Point(1, 0, 0));
        var sr = edge.Split(cutPlane);

        Assert.That(((Vertex)sr.innerCut).pos4d, Is.Null);
        Assert.That(((Vertex)sr.outerCut).pos4d, Is.Null);
    }

    // ── Face2d.Split — covers the OpposingClone path on cut vertices ────────

    [Test] public void Face2dSplit_AllOutputVerticesHavePos4d() {
        // Unit square in xy-plane with pos4d encoding w = 10*x + y.  Cut along x=0.5 —
        // both top and bottom edges are split.  The cut-edge endpoints are routed
        // through OpposingClone (Face2d.Split lines 494-502), which is where pos4d
        // used to be lost (the bug that produced erratic colors on cut faces).
        Vertex MakeV(double x, double y) {
            var v = new Vertex(new Point(x, y, 0));
            v.pos4d = new double[] { x, y, 0, 10 * x + y };
            return v;
        }
        var v00 = MakeV(0, 0);
        var v10 = MakeV(1, 0);
        var v11 = MakeV(1, 1);
        var v01 = MakeV(0, 1);
        var face = new Face2d(new List<Edge> {
            new Edge(v00, v10),
            new Edge(v10, v11),
            new Edge(v11, v01),
            new Edge(v01, v00),
        });
        var cutPlane = new HalfSpace(new Point(0.5, 0, 0), new Point(1, 0, 0));
        var sr = face.Split(cutPlane);

        AssertAllVerticesHavePos4d((Face2d)sr.inner, "inner");
        AssertAllVerticesHavePos4d((Face2d)sr.outer, "outer");
        AssertEdgeVerticesHavePos4d(sr.innerCut, "innerCut");
        AssertEdgeVerticesHavePos4d(sr.outerCut, "outerCut");

        // Two distinct cut points at x=0.5: bottom (y=0) has w = 0.5*0 + 0.5*10 = 5,
        // top (y=1) has w = 0.5*11 + 0.5*1 = 6.  Each appears as several Vertex instances
        // (original cut vert + OpposingClone + endpoint of two adjacent edges) — dedupe.
        var distinctRoundedW = new SortedSet<int>();
        foreach (var v in CollectVertices((Face2d)sr.inner)) {
            if (System.Math.Abs(v.getPoint().x[0] - 0.5) > 1e-9) continue;
            Assert.That(v.pos4d, Is.Not.Null,
                $"Cut vertex at {v.getPoint()} has null pos4d (this was the original bug).");
            distinctRoundedW.Add((int)System.Math.Round(v.pos4d[3]));
        }
        Assert.That(distinctRoundedW, Is.EquivalentTo(new[] { 5, 6 }),
            "Cut vertices on x=0.5 must have lerped w-values from the original edge endpoints.");
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    static void AssertPos4dEquals(double[] actual, double[] expected, double tol = 1e-9) {
        Assert.That(actual, Is.Not.Null, "pos4d is null");
        Assert.That(actual.Length, Is.EqualTo(expected.Length));
        for (int i = 0; i < expected.Length; i++)
            Assert.That(actual[i], Is.EqualTo(expected[i]).Within(tol),
                $"pos4d[{i}] expected {expected[i]} but was {actual[i]}");
    }

    static IEnumerable<Vertex> CollectVertices(Face2d face) {
        foreach (var edge in face.edges) {
            yield return edge.a;
            yield return edge.b;
        }
    }

    static void AssertAllVerticesHavePos4d(Face2d face, string label) {
        Assert.That(face, Is.Not.Null, $"{label} face must exist");
        foreach (var v in CollectVertices(face)) {
            Assert.That(v.pos4d, Is.Not.Null, $"{label}: vertex at {v.getPoint()} has null pos4d");
            Assert.That(v.pos4d.Length, Is.EqualTo(4));
        }
    }

    static void AssertEdgeVerticesHavePos4d(IPolyhedron edgeOrNull, string label) {
        if (edgeOrNull == null) return;
        var e = (Edge)edgeOrNull;
        Assert.That(e.a.pos4d, Is.Not.Null, $"{label}.a has null pos4d");
        Assert.That(e.b.pos4d, Is.Not.Null, $"{label}.b has null pos4d");
    }
}
}
