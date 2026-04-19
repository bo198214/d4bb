using System.Collections.Generic;
using System.Diagnostics;

namespace D4BB.Comb {
public class InFrontOfCellComparer : IComparer<OrientedIntegerCell> {
		/** Returns >0 if d1 is in front of (or greater than) d2,
		 * returns 0 if neither is in front of the other
		 * returns <0 if d2 is in front of (or greater than) d1
         * is anti symmetric (never a < b and b < a)
		 */
		public static int IsInFrontOf(OrientedIntegerCell d1, OrientedIntegerCell d2) {
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
}