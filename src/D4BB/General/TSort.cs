using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace D4BB.General {
public static class TopologicalSort {
    public static void TSort<S,T>(this List<T> l,IComparer<S> c) where T : S {
		Dictionary<T,HashSet<T>> predecessors = new();
		foreach (T t in l) predecessors[t] = new HashSet<T>();
		int n = l.Count;
		for (int i=0;i<n;i++) {
			T t1 = l[i];
			for (int j=i+1;j<n;j++) {
				T t2 = l[j];
				int cmp = c.Compare(t1, t2);
				if (cmp<0) {//t1<t2
					predecessors[t2].Add(t1);
				}
				if (cmp>0) {//t1>t2
					predecessors[t1].Add(t2);
				}
			}
		}
		foreach (T t1 in predecessors.Keys) {
			foreach (T t2 in predecessors[t1]) {
				Debug.Assert(c.Compare(t2, t1) < 0); 
			}
		}
		
		l.Clear();
		while (l.Count<n) {
			bool isCyclic = true;
			foreach (T t in predecessors.Keys) {
				if (predecessors[t].Count == 0) {
					isCyclic = false;
					l.Add(t);
				}
			}
			if (isCyclic) {
				Debug.WriteLine("Cycle detected:");
				T t = predecessors.Keys.First();
				List<T> prev = new List<T>();
				while (!prev.Contains(t)) {
					prev.Add(t);
					t = predecessors[t].First();
				}
				int i=prev.IndexOf(t);
				prev.Add(t);
				for (;i<prev.Count-1;i++) {
					t = prev[i];
					T tn = prev[i+1];
					Debug.WriteLine(t + ", " + t.GetHashCode() + ", this > next: " + (c.Compare(t, tn)>0) + "(" + c.Compare(t,tn) + ")");
					Debug.WriteLine(t + " > " + tn);
				}
				Debug.Assert(false);
			}
				
			foreach (T t in predecessors.Keys) {
				foreach (T s in l) predecessors[t].Remove(s);
			}
			foreach (T s in l) predecessors.Remove(s);
		}
	}	

    }
}