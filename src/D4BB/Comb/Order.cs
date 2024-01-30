using System.Collections.Generic;
using System.Diagnostics;

namespace D4BB.Comb {
public class InFrontOfCellComparer : IComparer<OrientedIntegerCell> {
		/** Returns >0 if d1 is in front of (or greater than) d2,
		 * returns 0 if neither is in front of the other
		 * returns <0 if d2 is in front of (or greater than) d1
		 */
		public static int IsInFrontOf(OrientedIntegerCell d1, OrientedIntegerCell d2) {
            //this is non-circular if all cells facing the camera
            int sideOf1 = d2.SideOf(d1);
			int sideOf2 = d1.SideOf(d2);
			if (sideOf1 == 0) {
				Debug.Assert(sideOf2 == 0,"2195277695");
			}
			return sideOf1 - sideOf2;
		}
		public int Compare(OrientedIntegerCell d1, OrientedIntegerCell d2) {
			return IsInFrontOf(d1,d2);
		}
}
public class InFrontOfComponentComparer : IComparer<HashSet<OrientedIntegerCell>> {
		/** Returns >0 if d1 is in front of (or greater than) d2,
		 * returns 0 if neither is in front of the other
		 * returns <0 if d2 is in front of (or greater than) d1
		 */
		 //this works only if c1 and c2 are essentially disjunct
		public static int IsInFrontOf(
				HashSet<OrientedIntegerCell> cells1,
				HashSet<OrientedIntegerCell> cells2, 
				bool debug=false) {
			foreach (var cell1 in cells1) {
				foreach (var cell2 in cells2) {
					var compare = InFrontOfCellComparer.IsInFrontOf(cell1,cell2);
					if (!debug) {
						if (compare!=0) {
							return compare;
						}
					} else {

					}
				}
			}
			return 0;
		}
		public int Compare(HashSet<OrientedIntegerCell> c1, HashSet<OrientedIntegerCell> c2) {
			return IsInFrontOf(c1,c2);
		}
}
}