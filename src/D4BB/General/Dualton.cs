using System;
using System.Collections.Generic;

namespace D4BB.General
{
public class Dualton<T>  {
    bool aIsSet = false;
    bool bIsSet = false;
    public T a { get; set; }
    public T b { get; set; }
    public Dualton(T a, T b) {
        this.a = a;
        aIsSet = true;
        this.b = b;
        bIsSet = true;
    }
    public Dualton(HashSet<T> elements) {
        foreach (var element in elements) {
            if (!aIsSet) {
                a = element;
                aIsSet = true;
                continue;
            }
            if (!bIsSet) {
                b = element;
                bIsSet = true;
                break;
            }
        }
    }
    public override bool Equals(object obj)
    {
        if (obj==null) { return false; }
        var other = (Dualton<T>) obj;
        bool parallel = this.a.Equals(other.a) && this.b.Equals(other.b);
        bool crossed = this.a.Equals(other.b) && this.b.Equals(other.a);
        return parallel || crossed;
    }
    public override int GetHashCode()
    {
        return a.GetHashCode() + b.GetHashCode();
    }
    public override string ToString()
    {
        return "{" + a.ToString()+","+b.ToString()+"}";
    }
    public bool Contains(T e) {
        return a.Equals(e) || b.Equals(e);
    }
    public bool IsComplete() {
        return aIsSet && bIsSet;
    }
    public bool IsVirgin() {
        return !aIsSet && !bIsSet;
    }
    public void Add(T e) {
        if (!aIsSet) {
            a=e;
            aIsSet=true;
        } else if (!bIsSet) {
            if (!a.Equals(e))
            b=e;
            bIsSet=true;
        } else {
            if (!a.Equals(e)&&!b.Equals(e)) {
                throw new Exception("Already two elements contained");
            }
        }
    }
    public T Other(T one) {
        if (!aIsSet || !bIsSet) throw new Exception("Dualton not yet complete");
        if (a.Equals(one)) return b;
        if (b.Equals(one)) return a;
        throw new Exception("neither a nor b equal to the given element");
    }
}
}