using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using D4BB.Comb;

public class OrientedIntegerCell : IntegerCell {
    // inverted=true means that the outside normal with respect to the parent points opposite to a main direction 
    public readonly bool inverted; 
    public readonly bool parity;

    public OrientedIntegerCell(int[] _origin, HashSet<int> _span, bool inverted, bool parity) :
            base(_origin, _span) {
        this.inverted = inverted;
        this.parity = parity;
    }
    /* normal with respect to span */
    public int[] Normal(HashSet<int> superSpan) {
        Debug.Assert(span.Count+1==superSpan.Count,"0081763009 normal can only computed in d-1 subspace");
        var normalAxis = superSpan.Except(span).First();
        var res = new int[SpaceDim()];
        if (!inverted) {
            res[normalAxis] = 1;
        } else {
            res[normalAxis] = -1;
        }
        return res;
    }
    /* computes normal for facets (i.e. d-1 dimensional cell in d dimensional space) */
    public int[] Normal() {
        return Normal(FullSpan());
    }
    public override bool Parity(int a) {
        var p = base.Parity(a);
        return p==parity==inverted;
    }
    public int[][] ClockwiseFromOutsideVertices2d() {
        Debug.Assert(Dim()==2,"7289482044");
        var res = new int[4][];
        int[][] schema = new int[][] {//clockwise
            new int[] {0,0},
            new int[] {0,1},
            new int[] {1,1},
            new int[] {1,0},
        };

        var spanList = new List<int>(span);
        spanList.Sort();

        // var parity1 = (spanList[0] + (spanList[1]-1))%2 == 0;

        // int[] parent = new int[] { spanList[0], spanList[1], normal};
        // Array.Sort(parent);
        // var parity2 = Array.IndexOf(parent,normal)%2==0;

        // var parity3 = Permutation.Parity(new int[]{
        //     Array.IndexOf(parent,spanList[0]),
        //     Array.IndexOf(parent,spanList[1]),
        //     Array.IndexOf(parent,normal)
        // });
        
        if (!inverted==parity) {
            (spanList[0],spanList[1])=(spanList[1],spanList[0]);
        }
        for (int n=0;n<4;n++) {
            res[n] = IntegerOps.Clone(origin);
            for (int i=0;i<2;i++) {
                res[n][spanList[i]] += schema[n][i];
            }
        }
        return res;
    }
    public OrientedIntegerCell[] ClockwiseFromOutsideEdges2d() {
        var spaceDim = SpaceDim();
        Debug.Assert(Dim()==2,"7390155317");
        var res = new OrientedIntegerCell[4];

        OrientedIntegerCell[] schema = new OrientedIntegerCell[] {//clockwise
            new(new int[] {0,0},new(){1},false,true),
            new(new int[] {0,1},new(){0},false,true),
            new(new int[] {1,0},new(){1},true,true),//inverse
            new(new int[] {0,0},new(){0},true,true),//inverse
        };
        
        var spanList = new List<int>(span);
        spanList.Sort();

        //var parity = (spanList[0] + (spanList[1]-1))%2 == 0;

        if (inverted!=parity) {
            (spanList[0],spanList[1])=(spanList[1],spanList[0]);
        } 
        
        for (int n=0;n<4;n++) {
            int[] o = IntegerOps.Clone(origin);
            HashSet<int> s = new(){spanList[schema[n].span.First()]};
            for (int i=0;i<2;i++) {
                o[spanList[i]] += schema[n].origin[i];
            }
            res[n] = new OrientedIntegerCell(o,s,schema[n].inverted,true);
        }
        return res;
    }
    public IntegerCell EdgeA() {//returns point a of the edge
        Debug.Assert(Dim()==1,"1953953185");
        IntegerCell res = new IntegerCell(IntegerOps.Clone(origin),new HashSet<int>());
        if (!inverted) return res;
        var axis = span.First();
        res.origin[axis] += 1;
        return res;
    }
    public IntegerCell EdgeB() {//returns point b of the edge
        Debug.Assert(Dim()==1,"5586912188");
        IntegerCell res = new IntegerCell(IntegerOps.Clone(origin),new HashSet<int>());
        if (inverted) return res;
        var axis = span.First();
        res.origin[axis] += 1;
        return res;
    }

    // public override bool Equals(object o)
    // {
    //     if (!base.Equals(o)) return false;
    //     var other = (OrientedIntegerCell) o;
    //     return this.inverted == other.inverted;
    // }
    // public override int GetHashCode()
    // {
    //     return base.GetHashCode() + inverted.GetHashCode();
    // }
    public override string ToString()
    {
        int[] spanArray = span.ToArray();
        Array.Sort(spanArray);
        return IntegerOps.ToString(origin) + (inverted?"-":"+") + IntegerOps.ToString(spanArray);
    }

    public int SideOf(IntegerCell facet, HashSet<int> superSpan) {
        //Both facets needs to reside in the same subspace
        int n = superSpan.Count();
        Debug.Assert(Dim()==n-1,"0576592695");

        int normalAxis = superSpan.Except(span).First();
        int sign = inverted ? -1 : 1;
        var dir = IntegerOps.Minus(facet.origin,origin);
        if (dir[normalAxis]>0) {
            return sign;
        } 
        if (dir[normalAxis]<0) {
            return -sign;
        }
        Debug.Assert(dir[normalAxis]==0,"7809940611");
        if (facet.span.Contains(normalAxis)) {
            return sign;
        }
        return 0;
    }
    public int SideOf(IntegerCell facet) {
        return SideOf(facet,FullSpan());
    }
}
