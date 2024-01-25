
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using D4BB.General;

namespace D4BB.Comb
{

public class IntegerCell
{
    public int[] origin;
    public HashSet<int> span;
    //public bool outwards = true; //this is a helper attribute for oriented cells
    /// <summary>
    /// Creates an IntegerCell at _origin and subspace given by _spat (subspace spanned by given axes in _spat)
    /// </summary>
    public IntegerCell(int[] _origin, HashSet<int> _span)
    {
        origin = IntegerOps.Clone(_origin);
        span = new(_span);
    }

    /// <summary>
    /// Creates a CubicalCell at _origin of dimension of the whole space (dimension of _origin)
    /// </summary>
    public IntegerCell(int[] _origin)
    {
        origin = IntegerOps.Clone(_origin);
        int dim = _origin.Length;
        span = new HashSet<int>();
        for (int d = 0; d < dim; d++)
        {
            span.Add(d);
        }
    }

    protected IntegerCell(IntegerCell dloc) : this(dloc.origin, dloc.span)
    {
    }

    public virtual IntegerCell Clone()
    {
        return new IntegerCell(origin, span);
    }

    override public bool Equals(object o)
    {
        if (o==null) { return false; }
        IntegerCell f = (IntegerCell)o;
        return IntegerOps.VecEqual(origin, f.origin) && span.SetEquals(f.span);
    }

    override public int GetHashCode()
    {

        //needs to be equal if the objects are equal
        int spanHash = 0;
        foreach (int a in span)
        {
            spanHash += 1 << a; //uniquely identifies the span
        }

        int originHash = 0; // unique for origin coordinates inside 0..9, max 9999 for 4 dimensions
        for (int i = 0; i < origin.Length; i++)
        {
            originHash += originHash * 10 + origin[i];
        }


        //unique for up to 4 dimensions and origin coordinates inside 0..9
        //could be even increased to use full integer range
        return 10000 * spanHash + originHash;
    }

    override public string ToString()
    {
        int[] spanArray = span.ToArray();
        Array.Sort(spanArray);
        return IntegerOps.ToString(origin) + ":" + IntegerOps.ToString(spanArray);
    }

    /// <summary>
    /// the dimension of the space the cell is contained in
    /// </summary>
    public virtual int SpaceDim()
    {
        return origin.Length;
    }

    /// <summary>
    /// the dimension of the cell
    /// </summary>
    public virtual int Dim()
    {
        return span.Count;
    }

    public virtual void Translate(IntegerSignedAxis v)
    {
        origin[v.axis] += v.PmSign();
    }

    /// <summary>
    /// precondition: v,w positive
    /// </summary>
    public virtual void Rotate(IntegerCenter o, int v, int w)
    {
        Debug.Assert(v != w,"8708844408");

        //		Center c = new Center(origin);
        //		c.rotate(o,v,w);
        //		origin = c.origin();
        IntegerOps.Rotate(origin, o, v, w);
        HashSet<int> res = new();
        foreach (int a in span)
        {
            if (a == v)
            {
                res.Add(w);
            }
            else if (a == w)
            {
                origin[v] -= 1;
                res.Add(v);
            }
            else {
                res.Add(a);
            }
        }
        span = res;
    }

    int[] CoSpace()
    {
        int[] res = new int[SpaceDim() - Dim()];
        int k = 0;
        for (int i = 0; i < SpaceDim(); i++)
        {
            bool contained = false;
            foreach (int a in span)
            {
                if (a == i)
                    contained = true;
            }

            if (!contained)
            {
                res[k] = i;
                k++;
            }
        }

        Debug.Assert(k == SpaceDim() - Dim(),"5480066745");
        return res;
    }

    IntegerSubSpace Space()
    {
        return new IntegerSubSpace(this);
    }

    public virtual bool Parity(int takeOut) {
        int[] spanArray = span.ToArray();
        Array.Sort(spanArray);
        var p = (span.Count-Array.IndexOf(spanArray,takeOut)-1)%2==0;
        return p;
    }

    public HashSet<OrientedIntegerCell> Facets()
    {
        HashSet<OrientedIntegerCell> res = new();
        for (int s = 0; s < 2; s++)
        {
            foreach (int a in span)
            {
                HashSet<int> faceSpan = new HashSet<int>(span);
                faceSpan.Remove(a);
                int[] faceOrigin = IntegerOps.Clone(origin);
                faceOrigin[a] += s;

                OrientedIntegerCell cell = new(faceOrigin, faceSpan, s==0, Parity(a));

                res.Add(cell);
            }
        }

        return res;
    }


    public HashSet<IntegerCell> SubFacets() {
        HashSet<IntegerCell> res = new HashSet<IntegerCell>();
        int dim = Dim();
        for (int s1 = 0; s1 < 2; s1++) {
            for (int s2=0; s2 < 2; s2++) {
                var pool = new HashSet<int>(span);
                foreach (int a1 in span) {
                    pool.Remove(a1);
                    foreach (int a2 in pool) {
                        HashSet<int> subFacetSpan = span.Except(new HashSet<int>(){a1,a2}).ToHashSet();
                        int[] faceOrigin = IntegerOps.Clone(origin);
                        faceOrigin[a1] += s1;
                        faceOrigin[a2] += s2;
                        res.Add(new IntegerCell(faceOrigin, subFacetSpan));
                    }
                }
            }
        }
        return res;
    }
    /* for a given sub facet returns both parents in this cell - the facets that contain the sub facet */
    public Dualton<OrientedIntegerCell> Parents(IntegerCell subFacet) {
        int d = Dim();
        Debug.Assert(subFacet.span.Count==d-2,"4975947727 subFacet dimension incorrect");
        var spanDiff = new List<int>(span.Except(subFacet.span));
        Debug.Assert(spanDiff.Count()==2, "9770996425 spanDiff must have dimension 2");

        foreach (var axis in subFacet.span) {
            Debug.Assert(subFacet.origin[axis]==origin[axis],"6263272379");
        }


        var originA = IntegerOps.Clone(subFacet.origin);
        var originB = IntegerOps.Clone(subFacet.origin);
        
        var spanA = new HashSet<int>(subFacet.span);
        var spanB = new HashSet<int>(subFacet.span);

        
        int axisA = spanDiff[0];
        int axisB = spanDiff[1];
        
        spanA.Add(axisA); //spanA is missing axisB
        spanB.Add(axisB); //spanB is missing axisA (see below Parity)
        originA[axisA] = origin[axisA];
        originB[axisB] = origin[axisB];
        return new Dualton<OrientedIntegerCell>(
            new OrientedIntegerCell(originA,spanA,originA[axisB]==origin[axisB],Parity(axisB)),
            new OrientedIntegerCell(originB,spanB,originB[axisA]==origin[axisA],Parity(axisA)));
    }
    /* in d dimensional span given a d-1-cell, which are the neighboring d-cells in this span */
    public static Dualton<IntegerCell> SpaceParents(IntegerCell facet, HashSet<int> span) {
        var dim = facet.Dim();
        var spaceDim = span.Count;
        Debug.Assert(spaceDim==dim+1,"6450156659");
        var originA = IntegerOps.Clone(facet.origin);
        var originB = IntegerOps.Clone(facet.origin);
        var spanA = new HashSet<int>(span);
        var spanB = new HashSet<int>(span);
        var axis = span.Except(facet.span).First();
        originA[axis] -= 1;
        return new Dualton<IntegerCell>(new(originA,spanA),new(originB,spanB));
    }
    /* for the facet being a facet of this cell, return the other cell that is in the same subspace as this cell */
    public IntegerCell SameSpaceOtherParent(IntegerCell facet) {
        return SpaceParents(facet, span).Other(this);
        
    }
    /* returns the cell that is the intersection of this and other cell, null if empty */
    public IntegerCell Intersection(IntegerCell other) {
        int dim = SpaceDim();
        Debug.Assert(dim==other.SpaceDim(),"9199618670");
        int diff;
        var thisSpan = new HashSet<int>(span);
        var otherSpan = new HashSet<int>(other.span);
        var resOrigin = new int[dim];
        for (int i=0;i<dim;i++) {
            diff = origin[i] - other.origin[i];
            if (diff>1||diff<-1) return null;
            int max = other.origin[i];
            if (origin[i]>max) max=origin[i];
            if (diff!=0) {
                HashSet<int> span;
                if (origin[i]!=max) {
                    span = thisSpan;
                } else {
                    span = otherSpan;
                }
                if (!span.Contains(i)) return null;
                span.Remove(i);
            } 
            resOrigin[i] = max;
        }
        HashSet<int> resSpan = thisSpan.Intersect(otherSpan).ToHashSet();
        return new IntegerCell(resOrigin,resSpan);
    }
    /* returns the facet that connects this and other. Null if they are not adjacent */
    public IntegerCell ConnectingFacet(IntegerCell other) {
        Debug.Assert(SpaceDim()==other.SpaceDim(),"0906689402");
        Debug.Assert(Dim()==other.Dim(),"1336187464");
        var intersection = Intersection(other);
        if (intersection == null || intersection.Dim()+1!=Dim()) return null;
        return intersection;
    }

    public int[][] Vertices() {
        int d = Dim();
        var res = new int[1<<d][];
        var spanArray = span.ToArray();
        //Array.Sort(spanArray);
        for (int mask=0; mask < 1<<d; mask++) {
            int[] p = IntegerOps.Clone(origin);
            for (int i=0;i<d;i++) {
                var aBit = mask>>i;
                if (aBit % 2 == 1) {
                    p[spanArray[i]]+=1;
                }
            }
            res[mask] = p;
        }
        return res;
    }

    public double[] Center() {
        int n = SpaceDim();
        var res = new double[n];
        for (int i=0;i<n;i++) {
            res[i] = origin[i];
        }
        foreach (int a in span) {
            res[a] += 0.5;
        }
        return res;
    }

    public static HashSet<int> FullSpan(int dim) {
        var res = new HashSet<int>();
        for (int i=0;i<dim;i++) {
            res.Add(i);
        }
        return res;
    }
    public HashSet<int> FullSpan() {
        return FullSpan(SpaceDim());
    }
}
}

