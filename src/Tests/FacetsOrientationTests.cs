using NUnit.Framework;

using D4BB.Comb;
using System.Diagnostics;
using D4BB.Geometry;
using D4BB.Transforms;
using System.Collections.Generic;

public class FacetsOrientationTests {
    [Test] public void TakeOut1Of3Test() {
        Assert.That(new IntegerCell(new int[]{0,0,0}).Parity(2),Is.EqualTo(true));
        Assert.That(new IntegerCell(new int[]{0,0,0}).Parity(1),Is.EqualTo(false));
        Assert.That(new IntegerCell(new int[]{0,0,0}).Parity(0),Is.EqualTo(true));
    }
    [Test] public void TakeOut1Of4Test() {
        Assert.That(new IntegerCell(new int[]{0,0,0,0}).Parity(3),Is.EqualTo(true));
        Assert.That(new IntegerCell(new int[]{0,0,0,0}).Parity(2),Is.EqualTo(false));
        Assert.That(new IntegerCell(new int[]{0,0,0,0}).Parity(1),Is.EqualTo(true));
        Assert.That(new IntegerCell(new int[]{0,0,0,0}).Parity(0),Is.EqualTo(false));
    }
    [Test, Combinatorial] public void IntegerCross2dTest([Values(
            new int[]{0, 1},
            new int[]{1, 0}
        )] int[] axes) {

        Assert.That(new HashSet<int>(axes),Has.Count.EqualTo(2));

        var a = axes[0];
        var b = axes[1];

        var av = new int[]{0,0}; av[a]=1;
        var bv = IntegerOps.Cross(av);
        var bvAlt = IntegerOps.Cross(new int[][]{av});
        Assert.That(bv,Is.EqualTo(bvAlt));
        var b2 = bv[b];
        Assert.That(b2==1,Is.EqualTo(b2!=-1));
        Assert.That(Permutation.Parity(new int[]{a,b}),Is.EqualTo(b2==-1));
    }
    [Test, Combinatorial] public void IntegerCross3dTest([Values(
            new int[]{0, 1, 2},
            new int[]{0, 2, 1},
            new int[]{1, 0, 2},
            new int[]{1, 2, 0},
            new int[]{2, 0, 1},
            new int[]{2, 1, 0}
        )] int[] axes) {

        Assert.That(new HashSet<int>(axes),Has.Count.EqualTo(3));

        var a = axes[0];
        var b = axes[1];
        var c = axes[2];

        var av = new int[]{0,0,0}; av[a]=1;
        var bv = new int[]{0,0,0}; bv[b]=1;
        var cv = IntegerOps.Cross(av,bv);
        var cvAlt = IntegerOps.Cross(new int[][]{av,bv});
        Assert.That(cv,Is.EqualTo(cvAlt));
        var c2 = cv[c];
        Assert.That(c2==1,Is.EqualTo(c2!=-1));
        Assert.That(Permutation.Parity(new int[]{a,b,c}),Is.EqualTo(c2==1));
    }
    [Test, Combinatorial] public void IntegerCrossTest4d([Values(
            new int[]{0, 1, 2, 3},
            new int[]{0, 1, 3, 2},
            new int[]{0, 2, 1, 3},
            new int[]{0, 2, 3, 1},
            new int[]{0, 3, 1, 2},
            new int[]{0, 3, 2, 1},
            new int[]{1, 0, 2, 3},
            new int[]{1, 0, 3, 2},
            new int[]{1, 2, 0, 3},
            new int[]{1, 2, 3, 0},
            new int[]{1, 3, 0, 2},
            new int[]{1, 3, 2, 0},
            new int[]{2, 0, 1, 3},
            new int[]{2, 0, 3, 1},
            new int[]{2, 1, 0, 3},
            new int[]{2, 1, 3, 0},
            new int[]{2, 3, 0, 1},
            new int[]{2, 3, 1, 0},
            new int[]{3, 0, 1, 2},
            new int[]{3, 0, 2, 1},
            new int[]{3, 1, 0, 2},
            new int[]{3, 1, 2, 0},
            new int[]{3, 2, 0, 1},
            new int[]{3, 2, 1, 0}
        )] int[] axes) {

        Assert.That(new HashSet<int>(axes),Has.Count.EqualTo(4));
        var a = axes[0];
        var b = axes[1];
        var c = axes[2];
        var d = axes[3];

        var av = new int[]{0,0,0,0}; av[a]=1;
        var bv = new int[]{0,0,0,0}; bv[b]=1;
        var cv = new int[]{0,0,0,0}; cv[c]=1;
        var dv = IntegerOps.Cross(av,bv,cv);
        var dvAlt = IntegerOps.Cross(new int[][]{av,bv,cv});
        Assert.That(dv,Is.EqualTo(dvAlt));
        var d2 = dv[d];
        Assert.That(d2==1,Is.EqualTo(d2!=-1));
        Assert.That(Permutation.Parity(new int[]{a,b,c,d}),Is.EqualTo(d2==-1));
    }

    private static bool CheckClockwiseProj(OrientedIntegerCell cell3d, OrientedIntegerCell cell2d, ICamera4d cam) {
        var center = new Point(3);
        var cellVertices = cell3d.Vertices();
        Assert.That(cellVertices.Length==8);
        Point[] projVertices = new Point[8];
        int i=0;
        foreach (var corner in cellVertices) {
            projVertices[i] = cam.Proj3d(new Point4d(corner));
            i++;
        }
        foreach (var projVertex in projVertices) {
            center.add(projVertex);
        }
        center.multiply(1.0/8);

        var iVertices = cell2d.ClockwiseFromOutsideVertices2d();

        var o = cam.Proj3d(new Point4d(iVertices[0]));
        var p1st = cam.Proj3d(new Point4d(iVertices[1]));
        var p2nd = cam.Proj3d(new Point4d(iVertices[3]));
        var d1st = p1st.subtract(o).normalize();
        var d2nd = p2nd.subtract(o).normalize();
        var normal = AOP.cross(d1st,d2nd).normalize();
        return o.clone().subtract(center).sc(normal)>0;
    }
    private static void CheckClockwise3d(IntegerCell cell3d, OrientedIntegerCell cell2d) {
        var center = new Point(3);
        var cellVertices = cell3d.Vertices();
        Assert.That(cellVertices.Length==8);
        Point[] projVertices = new Point[8];
        int i=0;
        foreach (var corner in cellVertices) {
            projVertices[i] = new Point(corner);
            i++;
        }
        foreach (var projVertex in projVertices) {
            center.add(projVertex);
        }
        center.multiply(1.0/8);

        var iVertices = cell2d.ClockwiseFromOutsideVertices2d();

        var o = new Point(iVertices[0]);
        var p1st = new Point(iVertices[1]);
        var p2nd = new Point(iVertices[3]);
        var d1st = p1st.subtract(o).normalize();
        var d2nd = p2nd.subtract(o).normalize();
        var normal = AOP.cross(d1st,d2nd).normalize();
        Assert.That(o.clone().subtract(center).sc(normal),Is.GreaterThan(0));
    }
    private static void CheckOrientation4d(IntegerCell cell4d,OrientedIntegerCell cell3d) {
        Point center = new Point(cell4d.Center());
        Assert.That(new Point(cell3d.origin).subtract(center).sc(new Point(cell3d.Normal())),Is.GreaterThan(0));
    }
    private static bool CheckOrientation3d(OrientedIntegerCell cell3d, OrientedIntegerCell cell2d) {
        Point center3d = new(cell3d.Center());
        var cell2dNormal = cell2d.Normal(cell3d.span);
        return new Point(cell2d.origin).subtract(center3d).sc(new Point(cell2dNormal))>0;
    }
    private static void CheckOrientation2d(IntegerCell cell3d, OrientedIntegerCell cell2d, OrientedIntegerCell cell1d) {
        Point center2d = new(cell2d.Center());
        var cell1dNormal = new Point(cell1d.Normal(cell2d.span));
        //var cell1dNormal = IntegerOps.Cross3dLeft(IntegerOps.Minus(cell1d.EdgeB().origin,cell1d.EdgeA().origin),cell2d.Normal());
        Assert.That(new Point(cell1d.origin).subtract(center2d).sc(new Point(cell1dNormal)),Is.GreaterThan(0));

        if (cell1d.inverted) Assert.That(cell1d.origin, Is.EqualTo(cell1d.EdgeB().origin));
        else                 Assert.That(cell1d.origin, Is.EqualTo(cell1d.EdgeA().origin));
    }
    [Test] public void SingleCell3d() {
        var cell3d = new IntegerCell(new int[]{0,0,0});
        {
            OrientedIntegerCell cell2d;
            OrientedIntegerCell cell1d;
            cell3d.Facets().TryGetValue(
                new OrientedIntegerCell(new int[]{0,0,0}, new HashSet<int>{0,1},false,true), //cube front
                out cell2d);
            //Assert.That(cell2d.inverted,Is.EqualTo(true));
            //Assert.That(cell2d.Normal(),Is.EqualTo(new int[]{0,0,-1}));

            cell2d.Facets().TryGetValue(
                new OrientedIntegerCell(new int[]{0,0,0},new HashSet<int>{0},false,true),   //lower edge
                out cell1d
            );
            Assert.That(cell1d,Is.Not.Null);
            //Assert.That(cell1d.inverted,Is.True);
            CheckOrientation2d(cell3d,cell2d,cell1d);
            CheckClockwise3d(cell3d,cell2d);
        }
        {
            OrientedIntegerCell cell2d;
            OrientedIntegerCell cell1d;
            cell3d.Facets().TryGetValue(
                new OrientedIntegerCell(new int[]{0,0,0}, new HashSet<int>{0,1},false,true), //cube front
                out cell2d);
            Assert.That(cell2d.inverted,Is.EqualTo(true));
            Assert.That(cell2d.Normal(),Is.EqualTo(new int[]{0,0,-1}));

            cell2d.Facets().TryGetValue(
                new OrientedIntegerCell(new int[]{1,0,0},new HashSet<int>{1},false,true),    //right edge
                out cell1d
            );
            Assert.That(cell1d,Is.Not.Null);
            Assert.That(cell1d.inverted,Is.False);
            CheckOrientation2d(cell3d,cell2d,cell1d);
            CheckClockwise3d(cell3d,cell2d);
        }
        {
            OrientedIntegerCell cell2d;
            OrientedIntegerCell cell1d;
            cell3d.Facets().TryGetValue(
                new OrientedIntegerCell(new int[]{1,0,0}, new HashSet<int>{1,2},true,true), //cube's right side
                out cell2d);
            Assert.That(cell2d.inverted,Is.False);
            Assert.That(cell2d.Normal(),Is.EqualTo(new int[]{1,0,0}));
            CheckClockwise3d(cell3d,cell2d);

            cell2d.Facets().TryGetValue(
                new OrientedIntegerCell(new int[]{1,0,0},new HashSet<int>{1},false,true),   //front edge
                out cell1d
            );
            Assert.That(cell1d,Is.Not.Null);
            Assert.That(cell1d.inverted,Is.True);
            CheckOrientation2d(cell3d,cell2d,cell1d);
        }
        {
            OrientedIntegerCell cell2d;
            cell3d.Facets().TryGetValue(
                new OrientedIntegerCell(new int[]{0,0,0}, new HashSet<int>{1,2},false,true), //cube's left side
                out cell2d);
            Assert.That(cell2d.inverted,Is.True);
            Assert.That(cell2d.Normal(),Is.EqualTo(new int[]{-1,0,0}));
            CheckClockwise3d(cell3d,cell2d);
        }
        {
            foreach (var cell2d in cell3d.Facets()) {
                CheckClockwise3d(cell3d,cell2d);
                foreach (var cell1d in cell2d.Facets()) {
                    CheckOrientation2d(cell3d,cell2d,cell1d);
                }
            }
        }
    }
    [Test] public void SingleCell_0s012() {
        var cell4d = new IntegerCell(new int[]{0,0,0,0});
        OrientedIntegerCell cell3d;
        cell4d.Facets().TryGetValue(
            new OrientedIntegerCell(new int[]{0,0,0,0}, new HashSet<int>{0,1,2},true,true),
            out cell3d);
        Assert.That(cell3d.Normal(),Is.EqualTo(new int[]{0,0,0,-1}));
        CheckOrientation4d(cell4d,cell3d);
    }
    [Test] public void SingleCell_1s123_0s23() {
        var cell4d = new IntegerCell(new int[]{0,0,0,0});
        cell4d.Facets().TryGetValue(
            new OrientedIntegerCell(new int[]{1,0,0,0}, new HashSet<int>{1,2,3},true,true),
            out OrientedIntegerCell cell3d);
        Assert.That(cell3d.inverted,Is.EqualTo(false));
        cell3d.Facets().TryGetValue(
            new OrientedIntegerCell(new int[]{1,0,0,0}, new HashSet<int>{2,3},false,true),
            out OrientedIntegerCell cell2d);
        Assert.That(cell2d.inverted,Is.EqualTo(true));

        Assert.That(CheckOrientation3d(cell3d,cell2d));

        Assert.That(CheckClockwiseProj(cell3d,cell2d,new Camera4dCentral()));
    }

    [Test] public void SingleCell_1s123_0s12() {
        var cell4d = new IntegerCell(new int[]{0,0,0,0});
        cell4d.Facets().TryGetValue(
            new OrientedIntegerCell(new int[]{1,0,0,0}, new HashSet<int>{1,2,3},true,true),
            out OrientedIntegerCell cell3d);
        Assert.That(cell3d.inverted,Is.EqualTo(false));
        cell3d.Facets().TryGetValue(
            new OrientedIntegerCell(new int[]{1,0,0,0}, new HashSet<int>{1,2},false,true),
            out OrientedIntegerCell cell2d);
        Assert.That(cell2d.inverted,Is.EqualTo(true));

        Assert.That(CheckOrientation3d(cell3d,cell2d));

        Camera4dCentral cam = new();
        Assert.That(CheckClockwiseProj(cell3d,cell2d,cam));
    }
    [Test] public void SingleCell_1s012_0s12() {
        var cell4d = new IntegerCell(new int[]{0,0,0,0});
        cell4d.Facets().TryGetValue(
            new OrientedIntegerCell(new int[]{0,0,0,1}, new HashSet<int>{0,1,2},true,true),
            out OrientedIntegerCell cell3d);
        Assert.That(cell3d.inverted,Is.EqualTo(false));
        var facets2d = cell3d.Facets();
        facets2d.TryGetValue(
            new OrientedIntegerCell(new int[]{0,0,0,1}, new HashSet<int>{1,2},true,true),
            out OrientedIntegerCell cell2d);
        Assert.That(cell2d.inverted,Is.EqualTo(true));

        Assert.That(CheckOrientation3d(cell3d,cell2d));

        Camera4dCentral cam = new();
        Assert.That(CheckClockwiseProj(cell3d,cell2d,cam));

    }
    [Test] public void SingleCell_1s023_0s23() {
        var cell4d = new IntegerCell(new int[]{0,0,0,0});
        cell4d.Facets().TryGetValue(
            new OrientedIntegerCell(new int[]{0,1,0,0}, new HashSet<int>{0,2,3},true,true),
            out OrientedIntegerCell cell3d);
        Assert.That(cell3d.inverted,Is.EqualTo(false));
        cell3d.Facets().TryGetValue(
            new OrientedIntegerCell(new int[]{0,1,0,0}, new HashSet<int>{2,3},false,true),
            out OrientedIntegerCell cell2d);
        Assert.That(cell2d.inverted,Is.EqualTo(true));

        Assert.That(CheckOrientation3d(cell3d,cell2d));

        Camera4dCentral cam = new();
        Assert.That(CheckClockwiseProj(cell3d,cell2d,cam));
    }
    [Test] public void SingleCell_2s013_0s03() {
        var cell4d = new IntegerCell(new int[]{1,0,0,0});
        cell4d.Facets().TryGetValue(
            new OrientedIntegerCell(new int[]{1,0,1,0}, new HashSet<int>{0,1,3},true,true),
            out OrientedIntegerCell cell3d);
        cell3d.Facets().TryGetValue(
            new OrientedIntegerCell(new int[]{1,0,1,0}, new HashSet<int>{0,3},false,true),
            out OrientedIntegerCell cell2d);

        Assert.That(CheckOrientation3d(cell3d,cell2d));
        Camera4dCentral cam = new();
        Assert.That(CheckClockwiseProj(cell3d,cell2d,cam));
    }
    [Test] public void All4dFacets() {
        Camera4dCentral cam = new();
        var cell4d = new IntegerCell(new int[]{1,0,0,0});
        foreach (var cell3d in cell4d.Facets()) {
            foreach (var cell2d in cell3d.Facets()) {
                Assert.That(CheckOrientation3d(cell3d,cell2d), cell3d + "->" + cell2d);
                Assert.That(CheckClockwiseProj(cell3d,cell2d,cam), cell3d + "->" + cell2d);
            }
        }
    }

    [Test] public void Complex2CellsTest() {
        Camera4dCentral cam = new();
        var ibc = new IntegerBoundaryComplex(new int[][] { new int[] {0,0,0,0},new int[] {1,0,0,0}});
        foreach (var component in ibc.Components()) 
        foreach (var cell3d in component) {
            foreach (var cell2d in cell3d.Facets()) {
                Assert.That(CheckOrientation3d(cell3d,cell2d), cell3d + "->" + cell2d);
                Assert.That(CheckClockwiseProj(cell3d,cell2d,cam), cell3d + "->" + cell2d);
            }
        }
    }
    [Test] public void ComplexLTest() {
        Camera4dCentral cam = new();
        var ibc = new IntegerBoundaryComplex(new int[][] { 
            new int[] {0,0,0,0},new int[] {1,0,0,0},new int[] {1,1,0,0}});
        foreach (var component in ibc.Components()) 
        foreach (var cell3d in component) {
            foreach (var cell2d in cell3d.Facets()) {
                Assert.That(CheckOrientation3d(cell3d,cell2d), cell3d + "->" + cell2d);
                Assert.That(CheckClockwiseProj(cell3d,cell2d,cam), cell3d + "->" + cell2d);
            }
        }
    }

}