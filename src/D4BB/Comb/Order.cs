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

public class InFrontOfViewNormalComparer : IComparer<IntegerCell> {
        public double[] viewNormal;
        public InFrontOfViewNormalComparer(double[] viewNormal) {
            this.viewNormal = viewNormal;
        }
        public int Compare(IntegerCell d1, IntegerCell d2) {
            var inverted1 = viewNormal[d1.NormalAxis()] >= 0;
            var inverted2 = viewNormal[d2.NormalAxis()] >= 0;
            var od1 = new OrientedIntegerCell(d1.origin, d1.span, inverted1, false);
            var od2 = new OrientedIntegerCell(d2.origin, d2.span, inverted2, false);
            return InFrontOfCellComparer.IsInFrontOf(od1, od2);
        }
    }
}