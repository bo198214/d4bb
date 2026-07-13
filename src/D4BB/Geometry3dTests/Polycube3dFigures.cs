using System.Collections.Generic;
using System.Linq;
using D4BB.Comb;

namespace D4BB.Geometry3dTests {

    /// Typical polycube figures as integer cube-origin lists — the 3D sibling of
    /// Geometry2Tests.PolycubeFigures. The same origin list feeds both pipelines:
    ///   old:  new Scene3d(new[] { cells }, cam)              (one piece holding all cubes)
    ///   new:  IntegerComplex3dBuilder.Boundary(AsIntegerCells(cells))
    public static class Polycube3dFigures {

        /// name → unit-cube origins. "single"/"bar2"/"block22" are convex sanity cases;
        /// everything else is non-convex. "L3" is exactly the L tricube the
        /// LAssembleCubesBeat docks (its DockOffsets derive from these origins).
        public static readonly (string name, int[][] cells)[] All = {
            ("single",  new[] { new[] { 0, 0, 0 } }),
            ("bar2",    new[] { new[] { 0, 0, 0 }, new[] { 1, 0, 0 } }),
            ("block22", new[] { new[] { 0, 0, 0 }, new[] { 1, 0, 0 },
                                new[] { 0, 1, 0 }, new[] { 1, 1, 0 } }),
            ("L3",      new[] { new[] { 0, -1, 0 }, new[] { -1, -1, 0 }, new[] { 0, 0, 0 } }),
            ("T4",      new[] { new[] { 0, 0, 0 }, new[] { 1, 0, 0 },
                                new[] { 2, 0, 0 }, new[] { 1, 1, 0 } }),
            ("S4",      new[] { new[] { 0, 0, 0 }, new[] { 1, 0, 0 },
                                new[] { 1, 1, 0 }, new[] { 2, 1, 0 } }),
            ("rnd5",    RandomPolycube(42, 5)),
            ("rnd7",    RandomPolycube(7, 7)),
        };

        public static int[][] ByName(string name) => All.First(f => f.name == name).cells;

        public static IEnumerable<IntegerCell> AsIntegerCells(int[][] cells)
            => cells.Select(o => new IntegerCell(o));

        /// Deterministic connected random polycube: grow from the origin by repeatedly
        /// attaching a unit cube at a random free face of a random existing cube.
        public static int[][] RandomPolycube(int seed, int count) {
            var rng = new System.Random(seed);
            var cells = new List<int[]> { new[] { 0, 0, 0 } };
            var occupied = new HashSet<(int, int, int)> { (0, 0, 0) };
            while (cells.Count < count) {
                var basis = cells[rng.Next(cells.Count)];
                int axis = rng.Next(3);
                int dir = rng.Next(2) == 0 ? 1 : -1;
                var next = (int[])basis.Clone();
                next[axis] += dir;
                if (occupied.Add((next[0], next[1], next[2])))
                    cells.Add(next);
            }
            return cells.ToArray();
        }
    }
}
