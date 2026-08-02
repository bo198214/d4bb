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
        /// random polycubes for broader coverage. "box3d" is a solid 3x3x3 xyz-block, but
        /// only ONE cell thick in w — a genuine 3D box merely embedded in 4D — with its
        /// center cell removed. The cavity is fully enclosed in xyz but NOT in w (the slab
        /// has no depth to hide it behind), so at generic 4D viewing angles the cavity's
        /// inner faces are legitimately visible through the see-through gap in the w
        /// direction — the opposite stress case from a fully hidden cavity: CutOut must
        /// show this geometry, not remove it.
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
            ("box3d",    new[] { new[] { 0, 0, 0, 0 }, new[] { 1, 0, 0, 0 }, new[] { 2, 0, 0, 0 },
                                 new[] { 0, 1, 0, 0 }, new[] { 1, 1, 0, 0 }, new[] { 2, 1, 0, 0 },
                                 new[] { 0, 2, 0, 0 }, new[] { 1, 2, 0, 0 }, new[] { 2, 2, 0, 0 },
                                 
                                 new[] { 0, 0, 1, 0 }, new[] { 1, 0, 1, 0 }, new[] { 2, 0, 1, 0 },
                                 new[] { 0, 1, 1, 0 },                       new[] { 2, 1, 1, 0 },
                                 new[] { 0, 2, 1, 0 }, new[] { 1, 2, 1, 0 }, new[] { 2, 2, 1, 0 },
                                 
                                 new[] { 0, 0, 2, 0 }, new[] { 1, 0, 2, 0 }, new[] { 2, 0, 2, 0 },
                                 new[] { 0, 1, 2, 0 }, new[] { 1, 1, 2, 0 }, new[] { 2, 1, 2, 0 },
                                 new[] { 0, 2, 2, 0 }, new[] { 1, 2, 2, 0 }, new[] { 2, 2, 2, 0 } }),
            ("rnd5",     RandomPolycube(42, 5)),
            ("rnd7",     RandomPolycube(7, 7)),
            // The big pieces of the tunnel-1d / tunnel-2d levels (Assets/_Tesserian/Game/levels/):
            // a 3x3x3x3 block with a through-tunnel. tunnel1d: 1x1x1 hole swept along w (open at
            // w=0-/w=2+ only). tunnel2d: the z=1,w=1 slab removed — the cavity spans ALL of x,y,
            // so it is open in the four x/y directions and closed in z and w. Both are genuinely
            // 4D cavities (unlike box3d's w-thin slab): at generic angles parts of the inner tunnel
            // walls are occluded by the surrounding block and parts are visible through the tunnel
            // mouths — added because the shipping game shows suspected occlusion errors on these
            // levels (2026-08).
            ("tunnel1d", Block3333(except: (x, y, z, w) => x == 1 && y == 1 && z == 1)),
            ("tunnel2d", Block3333(except: (x, y, z, w) => z == 1 && w == 1)),
        };

        public static int[][] ByName(string name) => All.First(f => f.name == name).cells;

        /// Large figures (the 70+-cell tunnel blocks) are an order of magnitude more expensive
        /// per case than the classic figures. Test suites use this to thin their angle coverage
        /// for them (coarser sweep steps, exclusion from the dense XY×ZW grid) so normal test
        /// runs stay fast — coverage-per-angle is unchanged, only the sampling density drops.
        public static bool IsLarge(string name) => ByName(name).Length >= 30;

        public static IEnumerable<IntegerCell> AsIntegerCells(int[][] cells)
            => cells.Select(o => new IntegerCell(o));

        /// 3x3x3x3 block minus the cells matching `except` (see the tunnel figures above).
        public static int[][] Block3333(System.Func<int, int, int, int, bool> except) {
            var cells = new List<int[]>();
            for (int w = 0; w < 3; w++)
                for (int z = 0; z < 3; z++)
                    for (int y = 0; y < 3; y++)
                        for (int x = 0; x < 3; x++)
                            if (!except(x, y, z, w))
                                cells.Add(new[] { x, y, z, w });
            return cells.ToArray();
        }

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
