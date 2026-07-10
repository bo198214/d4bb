using System.Collections.Generic;
using System.Linq;
using D4BB.Comb;

namespace D4BB.Geometry2Tests {

    /// Typical polytesseract figures as integer cell-origin lists, defined in code so the
    /// parity tests need no JSON assets. The same origin list feeds both pipelines:
    ///   old:  new Scene4d(new[] { cells }, cam)            (one piece holding all cells)
    ///   new:  IntegerComplex4dBuilder.Boundary(AsIntegerCells(cells))
    public static class PolycubeFigures {

        /// name → unit-tesseract origins. "single"/"bar2"/"block221" are convex sanity
        /// cases; everything else is non-convex — the configurations the BSP+CutOut path
        /// exists for. "L3" is exactly the L.json piece (PolychoraGenerators.Generate_L_3Tesseracts),
        /// the canonical known-bad case. "Lw3" bends into the w-axis, "rnd*" are seeded
        /// random polycubes for broader coverage.
        public static readonly (string name, int[][] cells)[] All = {
            ("single",   new[] { new[] { 0, 0, 0, 0 } }),
            ("bar2",     new[] { new[] { 0, 0, 0, 0 }, new[] { 1, 0, 0, 0 } }),
            ("block221", new[] { new[] { 0, 0, 0, 0 }, new[] { 1, 0, 0, 0 },
                                 new[] { 0, 1, 0, 0 }, new[] { 1, 1, 0, 0 } }),
            ("L3",       new[] { new[] { 0, -1, 0, 0 }, new[] { -1, -1, 0, 0 }, new[] { 0, 0, 0, 0 } }),
            ("Lw3",      new[] { new[] { 0, 0, 0, 0 }, new[] { 0, 0, 0, 1 }, new[] { 1, 0, 0, 1 } }),
            ("T4",       new[] { new[] { 0, 0, 0, 0 }, new[] { 1, 0, 0, 0 },
                                 new[] { 2, 0, 0, 0 }, new[] { 1, 1, 0, 0 } }),
            ("S4",       new[] { new[] { 0, 0, 0, 0 }, new[] { 1, 0, 0, 0 },
                                 new[] { 1, 1, 0, 0 }, new[] { 2, 1, 0, 0 } }),
            ("rnd5",     RandomPolycube(42, 5)),
            ("rnd7",     RandomPolycube(7, 7)),
        };

        public static int[][] ByName(string name) => All.First(f => f.name == name).cells;

        public static IEnumerable<IntegerCell> AsIntegerCells(int[][] cells)
            => cells.Select(o => new IntegerCell(o));

        /// Deterministic connected random polycube: grow from the origin by repeatedly
        /// attaching a unit tesseract at a random free face of a random existing cell.
        public static int[][] RandomPolycube(int seed, int count) {
            var rng = new System.Random(seed);
            var cells = new List<int[]> { new[] { 0, 0, 0, 0 } };
            var occupied = new HashSet<(int, int, int, int)> { (0, 0, 0, 0) };
            while (cells.Count < count) {
                var basis = cells[rng.Next(cells.Count)];
                int axis = rng.Next(4);
                int dir = rng.Next(2) == 0 ? 1 : -1;
                var next = (int[])basis.Clone();
                next[axis] += dir;
                if (occupied.Add((next[0], next[1], next[2], next[3])))
                    cells.Add(next);
            }
            return cells.ToArray();
        }
    }
}
