using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;

using static D4BB.Comb.Permutation;
public class PermutationTests {
    [Test] public void Bool2IntTest() {
        bool a;
        bool b;
        a = false; b = false;
        Assert.That(Int(a==b),Is.EqualTo(Int(a)*Int(b)));
        a = false; b = true;
        Assert.That(Int(a==b),Is.EqualTo(Int(a)*Int(b)));
        a = true; b = false;
        Assert.That(Int(a==b),Is.EqualTo(Int(a)*Int(b)));
        a = true; b = true;
        Assert.That(Int(a==b),Is.EqualTo(Int(a)*Int(b)));
    }
    [Test] public void ParityTest() {
        Assert.That(new int[]{0,1,2}.Parity(), Is.EqualTo(true));
        Assert.That(new int[]{2,0,1}.Parity(), Is.EqualTo(true));
        Assert.That(new int[]{1,2,0}.Parity(), Is.EqualTo(true));
        Assert.That(new int[]{1,0,2}.Parity(), Is.EqualTo(false));
        Assert.That(new int[]{2,1,0}.Parity(), Is.EqualTo(false));
        Assert.That(new int[]{0,2,1}.Parity(), Is.EqualTo(false));
    }

    public bool TakeOutTwo(List<int> list, int a, int b) {
        List<int> res = new(list);
        res.Remove(a);
        res.Add(a);
        res.Remove(b);
        res.Add(b);
        return res.ToArray().Parity();
    }
    [Test] public void TakeOutTwoOf4Test() {
        //The orientation of two equal subFacets is opposite
        List<int> l4 = new() {0,1,2,3};
        {
            var a = 0;
            var b = 1;
            Assert.That(TakeOutTwo(l4,a,b),Is.Not.EqualTo(TakeOutTwo(l4,b,a)));
        }
        {
            var a = 0;
            var b = 2;
            Assert.That(TakeOutTwo(l4,a,b),Is.Not.EqualTo(TakeOutTwo(l4,b,a)));
        }
        {
            var a = 0;
            var b = 3;
            Assert.That(TakeOutTwo(l4,a,b),Is.Not.EqualTo(TakeOutTwo(l4,b,a)));
        }
        {
            var a = 1;
            var b = 2;
            Assert.That(TakeOutTwo(l4,a,b),Is.Not.EqualTo(TakeOutTwo(l4,b,a)));
        }
        {
            var a = 1;
            var b = 3;
            Assert.That(TakeOutTwo(l4,a,b),Is.Not.EqualTo(TakeOutTwo(l4,b,a)));
        }
        {
            var a = 2;
            var b = 3;
            Assert.That(TakeOutTwo(l4,a,b),Is.Not.EqualTo(TakeOutTwo(l4,b,a)));
        }
    }
    [Test] public void TakeOutTwoOf3Test() {
        //The orientation of two equal subFacets is opposite
        List<int> l3 = new() {0,1,2};
        {
            var a = 0;
            var b = 1;
            Assert.That(TakeOutTwo(l3,a,b),Is.Not.EqualTo(TakeOutTwo(l3,b,a)));
        }
        {
            var a = 0;
            var b = 2;
            Assert.That(TakeOutTwo(l3,a,b),Is.Not.EqualTo(TakeOutTwo(l3,b,a)));
        }
        {
            var a = 1;
            var b = 2;
            Assert.That(TakeOutTwo(l3,a,b),Is.Not.EqualTo(TakeOutTwo(l3,b,a)));
        }
    }
    [Test] public void PermutationLazyTest() {
        var p = PermutationLazy(new int[]{0,1,2}, new int[]{1,2,3});
        Assert.That(p,Is.EqualTo(new int[]{2,0,1}));
    }
    [Test] public void PermutationsNTest() {
        Assert.That(Permutations(2),Is.EqualTo(new List<int[]>{new int[] {0,1}, new int[]{1,0}}));
        Assert.That(Permutations(3),Is.EqualTo(new List<int[]>{
            new int[] {0,1,2},
            new int[] {0,2,1},
            new int[] {1,0,2},
            new int[] {1,2,0},
            new int[] {2,0,1},
            new int[] {2,1,0},
        }));
    }
    [Test] public void PermutationsElementTest() {
        Assert.That(Permutations<int>(new List<int>{1,2,3},3),Is.EqualTo(new List<int[]>{
            new int[] {1,2,3},
            new int[] {1,3,2},
            new int[] {2,1,3},
            new int[] {2,3,1},
            new int[] {3,1,2},
            new int[] {3,2,1},
        }));
    }
}