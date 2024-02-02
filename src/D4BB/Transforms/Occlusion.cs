using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using D4BB.Comb;
using D4BB.Geometry;
using D4BB.Transforms;
using static D4BB.Transforms.Scene4d;

namespace D4BB.Transforms {
public class InFrontOfComponentComparer : IComparer<Component> {
        /** Returns >0 if d1 is in front of (or greater than) d2,
         * returns 0 if neither is in front of the other
         * returns <0 if d2 is in front of (or greater than) d1
         */
         //this works only if c1 and c2 are essentially disjunct
        public static int IsInFrontOf(Component c1, Component c2) {
            foreach (var cell1 in c1.cells) {
                foreach (var cell2 in c2.cells) {
                    var compare = InFrontOfCellComparer.IsInFrontOf(cell1,cell2);
                    if (compare!=0) {
                        return compare;
                    }
                }
            }
            return 0;
        }
        public int Compare(Component c1, Component c2) {
            return IsInFrontOf(c1,c2);
        }
}
}