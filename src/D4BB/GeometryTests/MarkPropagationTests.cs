using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace D4BB.Geometry
{
// Tests for IPolyhedron.mark propagation through Recreate/OpposingClone and Split.
//
// Invariants verified here:
//   - All Recreate(...) overloads copy mark from parent.
//   - OpposingClone() copies mark from `this` (semantically the same polyhedron,
//     opposite orientation).
//   - Cut halves inherit mark from the polyhedron being split.
//   - Elements freshly created at the cut plane (cut vertex, cut edge, cut face)
//     get mark=MARK_NONE — they are new geometry, not the original polyhedron.
public class MarkPropagationTests
{
    const int GI = IPolyhedron.MARK_GRID_INTERSECTION;
    const int NONE = IPolyhedron.MARK_NONE;

    // ── Edge.Split ────────────────────────────────────────────────────────

    [Test] public void EdgeSplit_HalvesInheritMark_CutVertexIsNone() {
        var a = new Vertex(new Point(0, 0, 0)) { mark = GI };
        var b = new Vertex(new Point(2, 0, 0)) { mark = GI };
        a.pos4d = new double[] { 0, 0, 0, 0 };
        b.pos4d = new double[] { 2, 0, 0, 0 };
        var edge = new Edge(a, b) { mark = GI };
        var cutPlane = new HalfSpace(new Point(1, 0, 0), new Point(1, 0, 0));

        var sr = edge.Split(cutPlane);

        Assert.That(sr.inner.mark, Is.EqualTo(GI), "Inner half inherits parent mark.");
        Assert.That(sr.outer.mark, Is.EqualTo(GI), "Outer half inherits parent mark.");
        Assert.That(sr.innerCut.mark, Is.EqualTo(NONE), "Cut vertex is fresh geometry — mark=NONE.");
        Assert.That(sr.outerCut.mark, Is.EqualTo(NONE), "Cut vertex (outer side) — mark=NONE.");
    }

    [Test] public void EdgeRecreate_PropagatesMark() {
        var a = new Vertex(new Point(0, 0, 0));
        var b = new Vertex(new Point(1, 0, 0));
        var edge = new Edge(a, b) { mark = GI };
        var copy = (Edge)edge.Recreate(a, b);
        Assert.That(copy.mark, Is.EqualTo(GI));
    }

    // ── Face2d.Split ──────────────────────────────────────────────────────

    [Test] public void Face2dSplit_HalvesInheritMark_NewCutEdgeIsNone() {
        // Unit square in the XY plane, marked as grid intersection.
        var pts = new List<Point> {
            new Point(0, 0, 0), new Point(2, 0, 0),
            new Point(2, 2, 0), new Point(0, 2, 0),
        };
        var face = new Face2d(pts) { mark = GI };
        foreach (var e in face.edges) {
            e.mark = GI;
            e.a.mark = GI; e.b.mark = GI;
            e.a.pos4d = new double[] { e.a.PointRef().x[0], e.a.PointRef().x[1], 0, 0 };
            e.b.pos4d = new double[] { e.b.PointRef().x[0], e.b.PointRef().x[1], 0, 0 };
        }
        var cutPlane = new HalfSpace(new Point(1, 0, 0), new Point(1, 0, 0));

        var sr = face.Split(cutPlane);

        Assert.That(sr.inner.mark, Is.EqualTo(GI), "Inner half inherits face mark.");
        Assert.That(sr.outer.mark, Is.EqualTo(GI), "Outer half inherits face mark.");
        Assert.That(sr.innerCut.mark, Is.EqualTo(NONE), "Fresh cut edge — mark=NONE.");
        Assert.That(sr.outerCut.mark, Is.EqualTo(NONE), "Fresh cut edge (outer side) — mark=NONE.");
    }

    [Test] public void Face2dOpposingClone_PreservesMark() {
        var pts = new List<Point> {
            new Point(0, 0, 0), new Point(1, 0, 0), new Point(1, 1, 0), new Point(0, 1, 0),
        };
        var face = new Face2d(pts) { mark = GI };
        var clone = (Face2d)face.OpposingClone();
        Assert.That(clone.mark, Is.EqualTo(GI));
    }

    // ── Polyhedron.Split ──────────────────────────────────────────────────

    [Test] public void PolyhedronSplit_HalvesInheritMark_NewCutFaceIsNone() {
        // Build a unit cube from 6 Face2d facets.
        var cube = BuildUnitCube(mark: GI);
        var cutPlane = new HalfSpace(new Point(0.5, 0, 0), new Point(1, 0, 0));

        var sr = cube.Split(cutPlane);

        Assert.That(sr.inner.mark, Is.EqualTo(GI), "Inner half inherits cube mark.");
        Assert.That(sr.outer.mark, Is.EqualTo(GI), "Outer half inherits cube mark.");
        Assert.That(sr.innerCut.mark, Is.EqualTo(NONE), "Fresh cut face on cut plane — mark=NONE.");
        Assert.That(sr.outerCut.mark, Is.EqualTo(NONE), "Fresh cut face (outer side) — mark=NONE.");
    }

    static Polyhedron BuildUnitCube(int mark) {
        // 8 vertices of unit cube
        var verts = new Vertex[8];
        for (int i = 0; i < 8; i++) {
            verts[i] = new Vertex(new Point(i & 1, (i >> 1) & 1, (i >> 2) & 1));
            verts[i].pos4d = new double[] { verts[i].PointRef().x[0], verts[i].PointRef().x[1], verts[i].PointRef().x[2], 0 };
            verts[i].mark = mark;
        }
        // 6 faces: each is a quad in CCW order looking from outside.
        Face2d Quad(int a, int b, int c, int d) {
            var pts = new List<Point> { verts[a].PointRef(), verts[b].PointRef(), verts[c].PointRef(), verts[d].PointRef() };
            var f = new Face2d(pts) { mark = mark };
            foreach (var e in f.edges) { e.mark = mark; e.a.mark = mark; e.b.mark = mark; }
            return f;
        }
        var facets = new HashSet<IPolyhedron> {
            Quad(0, 2, 6, 4),   // x=0
            Quad(1, 5, 7, 3),   // x=1
            Quad(0, 4, 5, 1),   // y=0
            Quad(2, 3, 7, 6),   // y=1
            Quad(0, 1, 3, 2),   // z=0
            Quad(4, 6, 7, 5),   // z=1
        };
        return new Polyhedron(facets, false) { mark = mark };
    }
}
}
