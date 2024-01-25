using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace D4BB.Comb {
public static class Permutation {
    public static short Int(bool s) {
        if (s) return 1;
        return -1;
    }
    //returns the permutation that turns a into b
    public static int[] Map<T>(T[] a, T[] b) {
        int n = a.Length;
        Debug.Assert(n==b.Length);
        var res = new int[n];
        for (int i=0;i<n;i++) {
            res[i] = Array.IndexOf(b,a[i]);
        }
        return res;
    }
    //Like Permutation, but figures out the remaining last element without looking it up
    public static int[] PermutationLazy<T>(T[] a, T[] b) {
        int n = a.Length;
        Debug.Assert(n==b.Length);
        var res = new int[n];
        HashSet<int> unUsed = new();
        for (int i=0;i<n;i++) {
            unUsed.Add(i);
        }
        int unUsedIndex = -1;
        for (int i=0;i<n;i++) {
            var ind = Array.IndexOf(b,a[i]);
            if (ind >= 0)  {
                res[i] = ind;
                unUsed.Remove(ind);
            } else {
                unUsedIndex = i;
            }
        }
        Debug.Assert(unUsed.Count==1);
        res[unUsedIndex]=unUsed.First();

        return res;
    }
    public static bool Parity<T>(T[] a, T[] b) {
        return Parity(Map(a,b));
    }
    public static bool ParityOne(int[] b) {
        var n = b.Length;
        int[] a = new int[]{};
        for (int i=0;i<n;i++) a[i]=i;
        return Parity(PermutationLazy(a,b));
    }
    public static bool Parity(this int[] p) {
        var n = p.Length;

        var ps = (int[]) p.Clone();
        Array.Sort(ps);
        for (int i=0;i<n;i++) {
            Debug.Assert(ps[i]==i);
        }

        return p.SwapCount()%2==0;
    }
    public static int SwapCount(this int[] p) {
        // if (p.Length<=64) {
            return p.SwapCountSmall();
        // } else {
        //     return p.swapCountLong();
        // }
    }

    // private static int swapCountLong(this int[] data) {
    //     int n=data.Length;
    //     int swaps=0;
    //     BitSet seen=new BitSet(n);
    //     for (int i=0; i<n; i++) {
    //         if (seen.get(i)) continue;
    //         seen.set(i);
    //         for(int j=data[i]; !seen.get(j); j=data[j]) {
    //             seen.set(j);
    //             swaps++;
    //         }       
    //     }
    //     return swaps;
    // }

    private static int SwapCountSmall(this int[] data) {
        int n=data.Length;
        int swaps=0;
        long seen=0;
        for (int i=0; i<n; i++) {
            long mask=1L<<i;
            if ((seen&mask)!=0) continue;
            seen|=mask;
            for(int j=data[i]; (seen&(1L<<j))==0; j=data[j]) {
                seen|=1L<<j;
                swaps++;
            }       
        }
        return swaps;
    }

    public static List<T[]> Permutations<T>(List<T> elements, int width) {
        if (width==0) { return new List<T[]>{new T[]{}}; }

        List<T[]> res = new();
        foreach (var i in elements) {
            var u = new List<T>(elements);
            u.Remove(i);
            foreach (var permutation in Permutations(u,width-1)) {
                var element = new T[width];
                element[0] = i;
                for (int j=1;j<width;j++) {
                    element[j]=permutation[j-1];
                }
                res.Add(element);
            }
        }
        return res;
    }
    public static List<int[]> Permutations(int n) {
        List<int> unUsed = new();
        for (int i=0;i<n;i++) {
            unUsed.Add(i);
        }
        return Permutations(unUsed,n);
    }

}
}