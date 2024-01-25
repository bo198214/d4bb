
using System.Collections.Generic;
using NUnit.Framework;

using D4BB.Comb;
using static D4BB.Comb.IntegerCell;
using System;
using System.Linq;
using D4BB.Geometry;
using D4BB.Transforms;
using D4BB.General;
using System.Diagnostics;

public class CombTests
{
    [Test] public void IntegerCell_vertices() {
        var cell = new IntegerCell(new int[]{7,0,0});
        var vertices = cell.Vertices();
        Assert.That(vertices.Length,Is.EqualTo(8));
        var expected = new HashSet<int[]>() {
            new int[]{7,0,0},
            new int[]{7,0,1},
            new int[]{7,1,0},
            new int[]{7,1,1},
            new int[]{8,0,0},
            new int[]{8,0,1},
            new int[]{8,1,0},
            new int[]{8,1,1},
        };
        Assert.That(new HashSet<int[]>(vertices),Is.EquivalentTo(expected));
    }
    [Test] public void IntegerCell_SubFacets() {
        var cell = new IntegerCell(new int[]{0,0,0});
        var exp = new HashSet<IntegerCell>(){
            new(new int[]{0,0,0},new HashSet<int>(){0}),
            new(new int[]{0,0,0},new HashSet<int>(){1}),
            new(new int[]{0,0,0},new HashSet<int>(){2}),
            new(new int[]{0,0,1},new HashSet<int>(){0}),
            new(new int[]{0,0,1},new HashSet<int>(){1}),
            new(new int[]{0,1,0},new HashSet<int>(){0}),
            new(new int[]{0,1,0},new HashSet<int>(){2}),
            new(new int[]{1,0,0},new HashSet<int>(){1}),
            new(new int[]{1,0,0},new HashSet<int>(){2}),
            new(new int[]{1,1,0},new HashSet<int>(){2}),
            new(new int[]{0,1,1},new HashSet<int>(){0}),
            new(new int[]{1,0,1},new HashSet<int>(){1}),
        };
        Assert.That(cell.SubFacets(),Is.EquivalentTo(exp));
    }
    [Test] public void IntegerCell_Parents() {
        {
            var cell = new IntegerCell(new int[]{0,0,0});
            var subFacet = new IntegerCell(new int[]{0,0,0}, new HashSet<int>(){1});
            var expected = new Dualton<OrientedIntegerCell>(
                new OrientedIntegerCell(new int[]{0,0,0}, new HashSet<int>(){0,1},true,true),
                new OrientedIntegerCell(new int[]{0,0,0}, new HashSet<int>(){1,2},true,true)
            );
            var parents = cell.Parents(subFacet);
            Assert.That(parents, Is.EqualTo(expected));
        }
        {
            var cell = new IntegerCell(new int[]{5,0,0});
            var subFacet = new IntegerCell(new int[]{5,0,0}, new HashSet<int>(){1});
            var expected = new Dualton<OrientedIntegerCell>(
                new OrientedIntegerCell(new int[]{5,0,0}, new HashSet<int>(){0,1},true,true),
                new OrientedIntegerCell(new int[]{5,0,0}, new HashSet<int>(){1,2},true,true)
            );
            Assert.That(cell.Parents(subFacet), Is.EqualTo(expected));
        }
        {
            var cell = new IntegerCell(new int[]{0,0,0});
            var subFacet = new IntegerCell(new int[]{1,1,0}, new HashSet<int>(){2});
            var expected = new Dualton<OrientedIntegerCell>(
                new OrientedIntegerCell(new int[]{0,1,0}, new HashSet<int>(){0,2},false,true),
                new OrientedIntegerCell(new int[]{1,0,0}, new HashSet<int>(){1,2},false,true)
            );
            Assert.That(cell.Parents(subFacet), Is.EqualTo(expected));
        }
        {
            var cell = new IntegerCell(new int[]{5,6,7});
            var subFacet = new IntegerCell(new int[]{6,7,7}, new HashSet<int>(){2});
            var expected = new Dualton<OrientedIntegerCell>(
                new OrientedIntegerCell(new int[]{5,7,7}, new HashSet<int>(){0,2},false,true),
                new OrientedIntegerCell(new int[]{6,6,7}, new HashSet<int>(){1,2},false,true)
            );
            Assert.That(cell.Parents(subFacet), Is.EqualTo(expected));
        }
    }
    [Test] public void IntegerCell_SpaceParents() {
        {
            var edge = new IntegerCell(new int[]{0,0,0}, new HashSet<int>(){0});
            var span = new HashSet<int>() {0,2};
            var expected = new Dualton<IntegerCell>(
                new(new int[]{0,0,0}, new HashSet<int>(){0,2}),
                new(new int[]{0,0,-1}, new HashSet<int>(){0,2})
            );
            Assert.That(IntegerCell.SpaceParents(edge,span),Is.EqualTo(expected));
        }
        {
            var edge = new IntegerCell(new int[]{5,5,5}, new HashSet<int>(){0});
            var span = new HashSet<int>() {0,2};
            var expected = new Dualton<IntegerCell>(
                new(new int[]{5,5,5}, new HashSet<int>(){0,2}),
                new(new int[]{5,5,4}, new HashSet<int>(){0,2})
            );
            Assert.That(IntegerCell.SpaceParents(edge,span),Is.EqualTo(expected));
        }

    }
    [Test] public void IntegerCell_intersection() {
        {
            var a = new IntegerCell(new int[]{0,0}, new HashSet<int>(){0,1});
            var b = new IntegerCell(new int[]{1,-1}, new HashSet<int>(){0,1});
            Assert.That(a.Intersection(b),Is.EqualTo(new IntegerCell(new int[]{1,0}, new HashSet<int>(){})));
        }
        {
            var a = new IntegerCell(new int[]{0,0}, new HashSet<int>(){0,1});
            var b = new IntegerCell(new int[]{1,-1}, new HashSet<int>(){0});
            Assert.That(a.Intersection(b),Is.EqualTo(null));
        }
        {
            var a = new IntegerCell(new int[]{0,0}, new HashSet<int>(){0,1});
            var b = new IntegerCell(new int[]{0,0}, new HashSet<int>(){0,1});
            Assert.That(a.Intersection(b),Is.EqualTo(new IntegerCell(new int[]{0,0}, new HashSet<int>(){0,1})));
        }
        {
            var a = new IntegerCell(new int[]{0,0}, new HashSet<int>(){0,1});
            var b = new IntegerCell(new int[]{1,0}, new HashSet<int>(){0,1});
            Assert.That(a.Intersection(b),Is.EqualTo(new IntegerCell(new int[]{1,0}, new HashSet<int>(){1})));
        }
        {
            var a = new IntegerCell(new int[]{0,0}, new HashSet<int>(){0,1});
            var b = new IntegerCell(new int[]{1,0}, new HashSet<int>(){1});
            Assert.That(a.Intersection(b),Is.EqualTo(new IntegerCell(new int[]{1,0}, new HashSet<int>(){1})));
        }
        {
            var a = new IntegerCell(new int[]{0,0}, new HashSet<int>(){0,1});
            var b = new IntegerCell(new int[]{1,-1}, new HashSet<int>(){1});
            Assert.That(a.Intersection(b),Is.EqualTo(new IntegerCell(new int[]{1,0}, new HashSet<int>(){})));
        }
    }
    [Test] public void IntegerComplexesEquality() {
        var ccc1 = new IntegerBoundaryComplex(new List<IntegerCell> {new IntegerCell(new int[] {0,0,0}, new HashSet<int>(){0}), new IntegerCell(new int[] {0,0,1}, new HashSet<int>(){0})});
        var ccc2 = new IntegerBoundaryComplex(new List<IntegerCell> {new IntegerCell(new int[] {0,0,0}, new HashSet<int>(){0}), new IntegerCell(new int[] {0,0,1}, new HashSet<int>(){0})});
        var cccx = new IntegerBoundaryComplex(new IntegerCell(new int[] {0,0,1}, new HashSet<int>(){0}));
        Assert.That(ccc1.Equals(ccc2));
        //Assert.AreEqual(ccc1.connectA.GetHashCode(),ccc2.connectA.GetHashCode());
        //Assert.That(ccc1.cells.GetHashCode() == ccc2.cells.GetHashCode());
        Assert.That(ccc1.GetHashCode() == ccc2.GetHashCode());
        Assert.That(!ccc1.Equals(cccx));
        var cccdict = new Dictionary<IntegerBoundaryComplex,bool>();
        cccdict[ccc1] = true;
        cccdict[ccc2] = true;
        Assert.That(cccdict.Count,Is.EqualTo(1));
        var cccset = new HashSet<IntegerBoundaryComplex>();
        cccset.Add(ccc1);
        cccset.Add(ccc2);
        Assert.That(cccset.Count,Is.EqualTo(1));

        var lc1 = new IntegerBoundaryComplex(new IntegerCell(new int[] {0,0,0},new HashSet<int>() {1}));
        var lc2 = new IntegerBoundaryComplex(new IntegerCell(new int[] {0,0,0},new HashSet<int>() {1}));
        Assert.That(lc1.Equals(lc2));
        cccset = new HashSet<IntegerBoundaryComplex>();
        cccset.Add(lc1);
        cccset.Add(lc2);
        Assert.That(cccset.Count,Is.EqualTo(1));

    }
    [Test] public void IntegerCell_subFacets() {
        IntegerCell cube = new IntegerCell(new int[] { 0, 0, 0 });
        var facets = cube.Facets();
        Assert.That(facets.Count,Is.EqualTo(6));
        var subFacets = cube.SubFacets();
        Assert.That(subFacets.Count,Is.EqualTo(12));
    }
    [Test] public void IntegerCell_square()
    {
        IntegerCell square = new IntegerCell(new int[] { 0, 0 });
        var facets1 = square.Facets();
        Assert.That(facets1.Count, Is.EqualTo(4));
        var cells0 = new HashSet<IntegerCell>();
        foreach (IntegerCell facet1 in facets1)
        {
            var facets0 = facet1.Facets();
            Assert.That(facets0.Count, Is.EqualTo(2));
            foreach (IntegerCell cell0 in facets0)
            {
                cells0.Add(cell0);
            }
        }

        Assert.That(cells0.Count, Is.EqualTo(4));
    }

    [Test] public void IntegerCell_cube()
    {
        IntegerCell cube = new IntegerCell(new int[] { 0, 0, 0 });
        var cells2 = cube.Facets();
        HashSet<IntegerCell> cells1 = new HashSet<IntegerCell>();
        foreach (IntegerCell cell2 in cells2)
        {
            
            cells1.UnionWith(cell2.Facets());
        }

        HashSet<IntegerCell> cells0 = new HashSet<IntegerCell>();
        foreach (IntegerCell cell1 in cells1)
        {
            cells0.UnionWith(cell1.Facets());
        }

        Assert.That(cells2.Count, Is.EqualTo(6));
        Assert.That(cells1.Count, Is.EqualTo(12));
        Assert.That(cells0.Count, Is.EqualTo(8));
    }

    [Test] public void IntegerCell_tesseract()
    {
        IntegerCell tesseract = new IntegerCell(new int[] { 0, 0, 0, 0 });
        var cells3 = tesseract.Facets();
        HashSet<IntegerCell> cells2 = new HashSet<IntegerCell>();
        foreach (IntegerCell cell3 in cells3)
        {
            cells2.UnionWith(cell3.Facets());
        }

        HashSet<IntegerCell> cells1 = new HashSet<IntegerCell>();
        foreach (IntegerCell cell2 in cells2)
        {
            cells1.UnionWith(cell2.Facets());
        }

        HashSet<IntegerCell> cells0 = new HashSet<IntegerCell>();
        foreach (IntegerCell cell1 in cells1)
        {
            cells0.UnionWith(cell1.Facets());
        }

        Assert.That(cells3.Count, Is.EqualTo(8));
        Assert.That(cells2.Count, Is.EqualTo(24));
        Assert.That(cells1.Count, Is.EqualTo(32));
        Assert.That(cells0.Count, Is.EqualTo(16));
    }

    static long Binomi(int n, int k)
    {
        if ((n == k) || (k == 0))
            return 1;
        else
            return Binomi(n - 1, k) + Binomi(n - 1, k - 1);
    }

    void IntegerCellTest(int dim)
    {
        int[] origin = new int[dim];
        for (int d = 0; d < dim; d++)
        {
            origin[d] = 0;
        }

        IntegerCell hyperCube = new IntegerCell(origin);
        int[] counts = new int[dim];
        var cells = hyperCube.Facets();
        for (int d = dim - 1; d >= 0; d--)
        {
            counts[d] = cells.Count;
            if (d <= 0)
            {
                break;
            }

            var cellsDec = new HashSet<OrientedIntegerCell>();
            foreach (IntegerCell cell in cells)
            {
                cellsDec.UnionWith(cell.Facets());
            }

            cells = cellsDec;
        }

        for (int d = 0; d < dim; d++)
        {
            Assert.That(counts[d], Is.EqualTo(Binomi(dim, d) * (int)Math.Pow(2, dim - d)));
        }
    }

    [Test] public void IntegerCell_n()
    {
        IntegerCellTest(4);
        IntegerCellTest(5);
        IntegerCellTest(6);
        IntegerCellTest(7);
        IntegerCellTest(8);
    }

    [Test] public void IntegerCell_clockwise() {
        var ic = new IntegerCell(new int[] { 0,0,0});
        var ibc = new IntegerBoundaryComplex(ic);
        List<int[][]> ibcVertices = new List<int[][]>(); 
        var faces2d = new HashSet<IPolyhedron>();
        foreach (var cell in ibc.cells) {
            ibcVertices.Add(cell.ClockwiseFromOutsideVertices2d());
            faces2d.Add(Face2dBC.FromIntegerCell(cell));
        }
        var expected = new HashSet<Face2d>() {
            new(new List<Point>() {new(0,0,1), new(1,0,1), new(1,1,1), new(0,1,1)}),
            new(new List<Point>() {new(0,0,0), new(0,0,1), new(0,1,1), new(0,1,0)}),
            new(new List<Point>() {new(0,0,0), new(1,0,0), new(1,0,1), new(0,0,1)}),
            new(new List<Point>() {new(0,0,0), new(0,1,0), new(1,1,0), new(1,0,0)}),
            new(new List<Point>() {new(1,0,0), new(1,1,0), new(1,1,1), new(1,0,1)}),
            new(new List<Point>() {new(0,1,0), new(0,1,1), new(1,1,1), new(1,1,0)}),
        };
        Assert.That(faces2d.Count,Is.EqualTo(expected.Count));
        Assert.That(faces2d,Is.EquivalentTo(expected));
        var oFacets2d = new HashSet<OrientedFace2d>();
        foreach (var face2d in faces2d) {
            oFacets2d.Add(new OrientedFace2d((Face2d)face2d));
        }
        var oExpected = new HashSet<OrientedFace2d>();
        foreach (var face2d in expected) {
            var points = face2d.points;
            oExpected.Add(new OrientedFace2d(new Face2d(points,true)));
        }
        Assert.That(oExpected.Except(oFacets2d),Is.Empty);
        Assert.That(oFacets2d,Is.EquivalentTo(oExpected));

    }
    [Test] public void Complex1d_1Edge() {
        IntegerBoundaryComplex compound = new IntegerBoundaryComplex(new IntegerCell(new int[]{0,0}, new HashSet<int>(){0}));
        Assert.That(compound.cells.Count,Is.EqualTo(2));
    }
    [Test] public void Complex2d_1square()
    {
        IntegerBoundaryComplex compound = new IntegerBoundaryComplex(new int[][] { new int[] { 0, 0 } });
        Assert.That(compound.cells.Count, Is.EqualTo(4));
        var ps = compound.PrunedSkeleton();
        Assert.That(ps, Has.Count.EqualTo(4));
    }
    [Test] public void IntegerComplex_connectCell() {
        IntegerBoundaryComplex compound = new(new int[] { 0, 0 });
        Assert.That(compound.cells.Count,Is.EqualTo(4));
        //Assert.That(compound.connections.Count,Is.EqualTo(4));
        var cell = new IntegerCell(new int[] { 0, 1 });
        compound.ConnectCell(cell);
        Assert.That(compound.cells.Count,Is.EqualTo(6));
        //Assert.That(compound.connections.Count,Is.EqualTo(6));
    }

    [Test] public void Complex2d_2adjacentSquares()
    {
        IntegerBoundaryComplex compound = new IntegerBoundaryComplex(new int[][] { new int[] { 0, 0 }, new int[] { 0, 1 } });
        Assert.That(compound.cells.Count, Is.EqualTo(6));

        var components = compound.Components();
        Assert.That(components.Count,Is.EqualTo(4));

        Assert.That(compound.cells.Count, Is.EqualTo(6));
        HashSet<IntegerCell> facets0 = compound.PrunedSkeletonCellsOfDim(0);
        Assert.That(facets0.Count, Is.EqualTo(4));
    }

    [Test] public void Complex2d_2square_join_complexes()
    {
        IntegerBoundaryComplex compound = new IntegerBoundaryComplex(new List<IntegerCell>() { new IntegerCell(new int[] { 0, 0 }), new IntegerCell(new int[] { 0, 1 }) });
        Assert.That(compound.cells.Count, Is.EqualTo(6));
        Assert.That(compound.PrunedSkeletonComponentsOfDim(0).Count, Is.EqualTo(4));
    }

    [Test] public void Complex2d_separateSquares_connectA()
    {
        IntegerBoundaryComplex compound = new IntegerBoundaryComplex(new int[][] { new int[] { 0, 0 }, new int[] { 0, 2 } });
        Assert.That(compound.cells.Count, Is.EqualTo(8));
    }

    [Test] public void Complex2d_L()
    {
        IntegerBoundaryComplex compound = new IntegerBoundaryComplex(new int[][] { new int[] { 0, 0 }, new int[] { 0, 1 }, new int[] { 1, 0 } });
        HashSet<IntegerCell> prunedFacets1 = compound.PrunedSkeletonCellsOfDim(1);
        Assert.That(prunedFacets1.Count, Is.EqualTo(8));
        HashSet<IntegerCell> prunedFacets0 = compound.PrunedSkeletonCellsOfDim(0);
        Assert.That(prunedFacets0.Count, Is.EqualTo(6));
    }
    [Test] public void Complex2d_add2edges()
    {
        IntegerBoundaryComplex compound = new IntegerBoundaryComplex(new int[][] { new int[] { 0, 0 }, new int[] { 0, 1 }, new int[] { 1, 0 } });
        IntegerCell cell = new IntegerCell(new int[] {1,1});
        compound.ConnectCell(cell);
        Assert.That(compound.cells.Count,Is.EqualTo(8));
    }

    [Test] public void Complex2d_semi_closed_ring()
    {
        IntegerBoundaryComplex compound = new IntegerBoundaryComplex(new int[][] { 
            new int[] { 0, 0 }, new int[] { 1, 0 }, new int[] { 2, 0 }, new int[] { 0, 1 }, new int[] { 2, 1 }, new int[] { 0, 2 }, new int[] { 1, 2 } });
        IntegerBoundaryComplex boundary = compound.Skeleton().First();
        //Assert.That(boundary.cells.Count,Is.EqualTo(16));
        Assert.That(compound.PrunedSkeletonCellsOfDim(1).Count, Is.EqualTo(16));
        Assert.That(compound.PrunedSkeletonCellsOfDim(0).Count, Is.EqualTo(9));
    }

    [Test] public void Complex2d_semi_closed_ring_filling()
    {
        IntegerBoundaryComplex compound = new(new int[][] { 
            new int[] { 0, 0 }, new int[] { 1, 0 }, new int[] { 2, 0 }, new int[] { 0, 1 }, new int[] { 2, 1 }, new int[] { 0, 2 }, new int[] { 1, 2 } });
        IntegerCell filling = new IntegerCell(new int[]{1,1});
        compound.ConnectCell(filling);
        Assert.That(compound.CheckConnectors());
    }
    [Test] public void Complex3d_ring_union() {
        var mapping = new Dictionary<int[],int>() {
            {new int[]{0,0,0},0},
            {new int[]{0,0,1},0},
            {new int[]{0,0,2},0},
            {new int[]{0,1,0},0},
            {new int[]{0,1,1},0},
            {new int[]{0,1,2},0},
            {new int[]{0,2,0},0},
            {new int[]{0,2,1},0},
            {new int[]{0,2,2},0},
            {new int[]{1,0,0},0},
            {new int[]{1,0,1},2},
            {new int[]{1,0,2},0},
            {new int[]{1,1,0},0},
            {new int[]{1,1,1},2},
            {new int[]{1,1,2},0},
            {new int[]{1,2,0},0},
            {new int[]{1,2,1},2},
            {new int[]{1,2,2},0},
            {new int[]{2,0,0},0},
            {new int[]{2,0,1},2},
            {new int[]{2,0,2},0},
            {new int[]{2,1,0},0},
            {new int[]{2,1,1},0},
            {new int[]{2,1,2},0},
            {new int[]{2,2,0},0},
            {new int[]{2,2,1},2},
            {new int[]{2,2,2},0},
            {new int[]{3,0,0},0},
            {new int[]{3,0,1},2},
            {new int[]{3,0,2},0},
            {new int[]{3,1,0},0},
            {new int[]{3,1,1},2},
            {new int[]{3,1,2},0},
            {new int[]{3,2,0},0},
            {new int[]{3,2,1},2},
            {new int[]{3,2,2},0},
        };
        var a = new HashSet<int[]>();
        var b = new HashSet<int[]>();
        foreach (var pos_ind in mapping) {
            var pos = pos_ind.Key;
            var ind = pos_ind.Value;
            if (ind==2) {
                b.Add(pos);
            }
            if (ind==0) {
                a.Add(pos);
            }
        }
        var complex = new IntegerBoundaryComplex(a.ToArray());
        var complexB = new IntegerBoundaryComplex(b.ToArray());
        foreach (var cell in b) {
            complex.ConnectCell(new IntegerCell(cell));
        }
        //complex.ConnectComplex(complexB);
        Assert.That(complex.Components().Count,Is.EqualTo(6));
    }
    [Test] public void Complex3d_random() {
        var mapping = new Dictionary<int[],int>() {
            {new int[]{0,0,0},0},
            {new int[]{0,0,1},0},
            {new int[]{0,0,2},0},
            {new int[]{0,1,0},0},
            {new int[]{0,1,1},0},
            {new int[]{0,1,2},0},
            {new int[]{0,2,0},0},
            {new int[]{0,2,1},0},
            {new int[]{0,2,2},0},
            {new int[]{1,0,0},0},
            {new int[]{1,0,1},0},
            {new int[]{1,0,2},0},
            {new int[]{1,1,0},0},
            {new int[]{1,1,1},0},
            {new int[]{1,1,2},0},
            {new int[]{1,2,0},0},
            {new int[]{1,2,1},0},
            {new int[]{1,2,2},0},
            {new int[]{2,0,0},0},
            {new int[]{2,0,1},0},
            {new int[]{2,0,2},0},
            {new int[]{2,1,0},0},
            {new int[]{2,1,1},0},
            {new int[]{2,1,2},0},
            {new int[]{2,2,0},0},
            {new int[]{2,2,1},0},
            {new int[]{2,2,2},0},
            {new int[]{3,0,0},0},
            {new int[]{3,0,1},0},
            {new int[]{3,0,2},0},
            {new int[]{3,1,0},0},
            {new int[]{3,1,1},0},
            {new int[]{3,1,2},0},
            {new int[]{3,2,0},0},
            {new int[]{3,2,1},0},
            {new int[]{3,2,2},0},
        };
        var a = new HashSet<int[]>();
        var b = new HashSet<int[]>();
        Random rnd = new Random();
        foreach (var pos_ind in mapping) {
            var pos = pos_ind.Key;
            int ind  = rnd.Next(0,2);
            if (ind==1) {
                b.Add(pos);
            }
            if (ind==0) {
                a.Add(pos);
            }
        }
        var complex = new IntegerBoundaryComplex(a.ToArray());
        foreach (var cell in b) {
            complex.ConnectCell(new IntegerCell(cell));
        }
        Assert.That(complex.Components().Count,Is.EqualTo(6));
    }

    void Complex_square(int sidelen)
    {
        int[][] origins = new int[sidelen * sidelen][];
        for (int i=0;i<sidelen*sidelen;i++) {
            origins[i] = new int[2];
        }
        for (int i1 = 0; i1 < sidelen; i1++)
        {
            for (int i2 = 0; i2 < sidelen; i2++)
            {
                origins[i1 * sidelen + i2][0] = i1;
                origins[i1 * sidelen + i2][1] = i2;
            }
        }

        IntegerBoundaryComplex compound = new IntegerBoundaryComplex(origins);
        Assert.That(compound.Components().Count,Is.EqualTo(4));
        HashSet<IntegerCell> c0 = compound.PrunedSkeletonCellsOfDim(0);
        Assert.That(c0.Count, Is.EqualTo(4));
    }

    [Test] public void Complex2d_square_2x2()
    {
        Complex_square(2);
    }

    [Test]
    public void Complex2d_square_5x5()
    {
        Complex_square(5);
    }

    void Complex_cube(int sidelen)
    {
        int[][] origins = new int[sidelen * sidelen * sidelen][];
        for (int i=0;i<sidelen * sidelen * sidelen;i++) {
            origins[i] = new int[3];
        }
        for (int i1 = 0; i1 < sidelen; i1++)
        {
            for (int i2 = 0; i2 < sidelen; i2++)
            {
                for (int i3 = 0; i3 < sidelen; i3++)
                {
                    origins[i1 * sidelen * sidelen + i2 * sidelen + i3][0] = i1;
                    origins[i1 * sidelen * sidelen + i2 * sidelen + i3][1] = i2;
                    origins[i1 * sidelen * sidelen + i2 * sidelen + i3][2] = i3;
                }
            }
        }

        IntegerBoundaryComplex compound = new IntegerBoundaryComplex(origins);
        Assert.That(compound.Components().Count,Is.EqualTo(6));
        HashSet<IntegerCell> c0 = compound.PrunedSkeletonCellsOfDim(0);
        Assert.That(c0.Count, Is.EqualTo(8));
    }

    [Test] public void Complex3d_cube_3x3x3()
    {
        Complex_cube(3);
    }

    [Test] public void Complex3d_cube_5x5x5()
    {
        Complex_cube(5);
    }
    IntegerBoundaryComplex Complex_tesseract(int sidelen, bool random=false)
    {
        int[][] origins = new int[sidelen * sidelen * sidelen * sidelen][];
        for (int i=0;i<sidelen * sidelen * sidelen * sidelen;i++) {
            origins[i] = new int[4];
        }
        for (int i1 = 0; i1 < sidelen; i1++)
        {
            for (int i2 = 0; i2 < sidelen; i2++)
            {
                for (int i3 = 0; i3 < sidelen; i3++)
                {
                    for (int i4 = 0; i4 < sidelen; i4++)
                    {
                        origins[i1 * sidelen * sidelen * sidelen + i2 * sidelen * sidelen + i3 * sidelen + i4][0] = i1;
                        origins[i1 * sidelen * sidelen * sidelen + i2 * sidelen * sidelen + i3 * sidelen + i4][1] = i2;
                        origins[i1 * sidelen * sidelen * sidelen + i2 * sidelen * sidelen + i3 * sidelen + i4][2] = i3;
                        origins[i1 * sidelen * sidelen * sidelen + i2 * sidelen * sidelen + i3 * sidelen + i4][3] = i4;
                    }
                }
            }
        }

        // if (random) {
        //     List<IntegerCell> a = new();
        //     List<IntegerCell> b = new();
        //     var rnd = new Random();
        //     for (int i=0;i<origins.Length;i++) 
        //     {
        //         var ind = rnd.Next(0,2);
        //         if (ind==0) {
        //             a.Add(new IntegerCell(origins[i]));
        //         } else {
        //             b.Add(new IntegerCell(origins[i]));
        //         }
        //     }
        //     var complexA = new IntegerBoundaryComplex(a.ToArray());
        //     var complexB = new IntegerBoundaryComplex(b.ToArray());
        //     complexA.ConnectComplex(complexB);
        //     Assert.That(complexA.Components().Count,Is.EqualTo(8));
        //     return complexA;
        // } else {
            IntegerBoundaryComplex compound = new IntegerBoundaryComplex(origins);
            Assert.That(compound.Components().Count,Is.EqualTo(8));
            HashSet<IntegerCell> c0 = compound.PrunedSkeletonCellsOfDim(0);
            Assert.That(c0.Count, Is.EqualTo(16));
            return compound;
        // }
    }

    [Test] public void Complex4d_tesseract_4x4x4x4_random() {
        Complex_tesseract(4,true);
    }
    [Test] public void Complex4d_tesseract_5x5x5x5()
    {
        Complex_tesseract(5);
    }
    [Test] public void Complex3d_facets() {

    }
    [Test] public void Boundary_2dim_non_planar()
    {
        IntegerCell c1 = new IntegerCell(new int[] { 0, 0, 0 }, new HashSet<int>() { 0, 1 });
        IntegerCell c2 = new IntegerCell(new int[] { 1, 0, 0 }, new HashSet<int>() { 0, 1 });
        IntegerCell c3 = new IntegerCell(new int[] { 1, 1, 0 }, new HashSet<int>(){ 0, 2 });
        IntegerBoundaryComplex compound = new IntegerBoundaryComplex(new List<IntegerCell> { c1, c2, c3 });
        Assert.That(compound.cells.Count, Is.EqualTo(8));
    }

    [Test] public void Complex3d_L()
    {
        IntegerBoundaryComplex compound = new IntegerBoundaryComplex(new int[][] { new int[] { 0, 0, 0 }, new int[] { 0, 1, 0 }, new int[] { 1, 0, 0 } });
        HashSet<IntegerCell> prunedFacets2 = compound.PrunedSkeletonCellsOfDim(2);
        Assert.That(prunedFacets2.Count, Is.EqualTo(14));
        HashSet<IntegerCell> prunedFacets1 = compound.PrunedSkeletonCellsOfDim(1);
        Assert.That(prunedFacets1.Count, Is.EqualTo(16 + 6));
    }

    [Test] public void Complex_1square()
    {
        IntegerBoundaryComplex compound = new IntegerBoundaryComplex(new int[][] { new int[] { 0, 0 } });
        HashSet<OrientedIntegerCell> cellsCopy = new();
        foreach (OrientedIntegerCell cell in compound.cells)
        {
            cellsCopy.Add(cell);
        }
        List<HashSet<OrientedIntegerCell>> components = compound.Components(cellsCopy);
        Assert.That(components.Count, Is.EqualTo(4));
    }

    [Test] public void Complex_2_separated_squares()
    {
        IntegerBoundaryComplex compound = new IntegerBoundaryComplex(new int[][] { new int[] { 0, 0 }, new int[] { 2, 0 } });
        HashSet<OrientedIntegerCell> cellsCopy = new ();
        foreach (OrientedIntegerCell cell in compound.cells)
        {
            cellsCopy.Add(cell);
        }

        //compound.MarkSameSubspaceInvisible();
        List<HashSet<OrientedIntegerCell>> components = compound.Components(cellsCopy);
        Assert.That(components.Count, Is.EqualTo(8));
    }

    [Test] public void Complex_2_connected_squares()
    {
        IntegerBoundaryComplex compound = new IntegerBoundaryComplex(new int[][] { new int[] { 0, 0 }, new int[] { 1, 0 } });
        List<HashSet<OrientedIntegerCell>> components = compound.Components(compound.cells);
        Assert.That(components.Count, Is.EqualTo(4));
    }

    [Test] public void Complex_tesseract_5x5x5x5()
    {
        IntegerBoundaryComplex compound = Complex_tesseract(5);
        List<HashSet<OrientedIntegerCell>> components = compound.Components(compound.cells);
        Assert.That(components.Count, Is.EqualTo(8));
    }

    [Test] public void Complex3d_2_connected_cubes()
    {
        IntegerBoundaryComplex compound = new IntegerBoundaryComplex(new int[][] { new int[] { 0, 0, 0 }, new int[] { 1, 0, 0 } });
        Assert.That(compound.cells.Count, Is.EqualTo(10));
        var sidesComponents = compound.Skeleton();
        Assert.That(sidesComponents.Count, Is.EqualTo(6));
        Assert.That(compound.PrunedSkeletonComponentsOfDim(2).Count, Is.EqualTo(1));
        var edgesComplexes = compound.PrunedSkeletonComponentsOfDim(1);
        Assert.That(edgesComplexes.Count, Is.EqualTo(6));
        foreach (IntegerBoundaryComplex edgesComplex in edgesComplexes)
        {
            int n = edgesComplex.cells.Count;
            Assert.True(n == 4 || n == 6);
        }

        Assert.That(compound.PrunedSkeletonComponentsOfDim(2).Count, Is.EqualTo(1));
        Assert.That(compound.PrunedSkeletonCellsOfDim(2).Count, Is.EqualTo(10));
        Assert.That(compound.PrunedSkeletonComponentsOfDim(1).Count, Is.EqualTo(6));
        Assert.That(compound.PrunedSkeletonCellsOfDim(1).Count, Is.EqualTo(16));
        var c0hs = compound.PrunedSkeletonComponentsOfDim(0);
        IntegerBoundaryComplex[] c0 = c0hs.ToArray();
        int equalityCounter = 0;
        for (int i = 0; i < c0.Length; i++)
        {
            IntegerBoundaryComplex a = c0[i];
            for (int j = i + 1; j < c0.Length; j++)
            {
                IntegerBoundaryComplex b = c0[j];
                if (a.Equals(b))
                {
                    equalityCounter += 1;
                }
            }
        }

        Assert.That(equalityCounter, Is.EqualTo(0));
        Assert.That(c0hs.Count, Is.EqualTo(12)); //12 edges with 2 points in each
        Assert.That(compound.PrunedSkeletonCellsOfDim(0).Count, Is.EqualTo(8));
    }

    [Test] public void Skeleton3d_ring()
    {
        IntegerBoundaryComplex ring = new IntegerBoundaryComplex(new int[][] { new int[] { 0, 0, 0 }, new int[] { 1, 0, 0 }, new int[] { 2, 0, 0 }, new int[] { 0, 1, 0 }, new int[] { 2, 1, 0 }, new int[] { 0, 2, 0 }, new int[] { 1, 2, 0 }, new int[] { 2, 2, 0 } });
        Assert.That(ring.PrunedSkeletonCellsOfDim(2).Count, Is.EqualTo(8 * 2 + 4 * 3 + 4));
        Assert.That(ring.PrunedSkeletonCellsOfDim(1).Count, Is.EqualTo(2 * 12 + 2 * 4 + 4 + 4));
    }

    [Test] public void IntegerCell_sideTest() {
        var facet = new OrientedIntegerCell(new int[]{0,0,0},new HashSet<int>{1,2},false,true);
        Assert.That(facet.SideOf(facet),Is.EqualTo(0));
        Assert.That(facet.SideOf(new OrientedIntegerCell(new int[]{1,0,0},new HashSet<int>{1,2},false,true)),Is.EqualTo(1));
        Assert.That(facet.SideOf(new OrientedIntegerCell(new int[]{-1,0,0},new HashSet<int>{1,2},false,true)),Is.EqualTo(-1));
        Assert.That(facet.SideOf(new OrientedIntegerCell(new int[]{0,1,0},new HashSet<int>{1,2},false,true)),Is.EqualTo(0));
        Assert.That(facet.SideOf(new OrientedIntegerCell(new int[]{0,1,0},new HashSet<int>{0,2},false,true)),Is.EqualTo(1));
        Assert.That(facet.SideOf(new OrientedIntegerCell(new int[]{-1,1,0},new HashSet<int>{0,2},false,true)),Is.EqualTo(-1));
    }
}
