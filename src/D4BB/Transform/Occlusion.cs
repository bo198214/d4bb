using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using D4BB.Comb;
using D4BB.Geometry;
using D4BB.Transforms;
using static D4BB.Transforms.Scene4d;

public class InFrontOfCellComparer : IComparer<OrientedIntegerCell> {
		/** Returns >0 if d1 is in front of (or greater than) d2,
		 * returns 0 if neither is in front of the other
		 * returns <0 if d2 is in front of (or greater than) d1
		 */
		public static int StaticCompare(OrientedIntegerCell d1, OrientedIntegerCell d2) {
            //this is non-circular if all cells facing the camera
            int sideOf1 = d2.SideOf(d1);
			int sideOf2 = d1.SideOf(d2);
			if (sideOf1 == 0) {
				Debug.Assert(sideOf2 == 0,"2195277695");
			}
			return sideOf1 - sideOf2;
		}
		public int Compare(OrientedIntegerCell d1, OrientedIntegerCell d2) {
			return StaticCompare(d1,d2);
		}
}
public class InFrontOfComponentComparer : IComparer<Component> {
		/** Returns >0 if d1 is in front of (or greater than) d2,
		 * returns 0 if neither is in front of the other
		 * returns <0 if d2 is in front of (or greater than) d1
		 */
		 //this works only if c1 and c2 are essentially disjunct
		public static int StaticCompare(Component c1, Component c2) {
			foreach (var cell1 in c1.cells) {
				foreach (var cell2 in c2.cells) {
					var compare = InFrontOfCellComparer.StaticCompare(cell1,cell2);
					if (compare!=0) {
						return compare;
					}
				}
			}
			return 0;
		}
		public int Compare(Component c1, Component c2) {
			return StaticCompare(c1,c2);
		}
}
