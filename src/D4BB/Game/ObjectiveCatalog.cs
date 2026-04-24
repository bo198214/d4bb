using System.ComponentModel;
using System.Reflection;
using D4BB.Comb;

namespace D4BB.Game
{
    public static class ObjectiveCatalog
    {
        public static readonly Objective Bar = new Objective("Bar",
            new int[][] {
                new int[] { 2,0,0,0 },
                new int[] { 1,0,0,0 },
                new int[] { 0,0,0,0 }
            },
            new int[][][] {
                new int[][] {
                    new int[] { -1, 0, 0, 0 },
                    new int[] { -1, 1, 0, 0 }
                },
                new int[][] { new int[] { 1,0,0,0 } }
            },
            new int[][] { new int[] { -5, -5, -5, -5 }, new int[] { 5, 5, 5, 5 } }
        );

        public static readonly Objective Two_Cuboids = new Objective("Two Cuboids",
            IntegerOps.Create4dCube(2),
            new int[][][] {
                new int[][] {
                    new int[] { 0,0,0,0 },
                    new int[] { 1,0,0,0 },
                    new int[] { 0,1,0,0 },
                    new int[] { 1,1,0,0 },
                    new int[] { 0,0,1,0 },
                    new int[] { 1,0,1,0 },
                    new int[] { 0,1,1,0 },
                    new int[] { 1,1,1,0 },
                },
                IntegerOps.Trans(new int[][] {
                    new int[] { 0,0,0,1 },
                    new int[] { 1,0,0,1 },
                    new int[] { 0,1,0,1 },
                    new int[] { 1,1,0,1 },
                    new int[] { 0,0,1,1 },
                    new int[] { 1,0,1,1 },
                    new int[] { 0,1,1,1 },
                    new int[] { 1,1,1,1 },
                }, new int[] { 3,0,0,-1 }),
            },
            new int[][] { new int[] { -5, -5, -5, -5 }, new int[] { 5, 5, 5, 5 } }
        );

        public static readonly Objective Worm = new Objective("Worm",
            new int[][] {
                new int[] { 0,0,0,0 },
                new int[] { 1,0,0,0 },
                new int[] { 1,1,0,0 },
                new int[] { 1,1,1,0 },
                new int[] { 1,1,1,-1 },
            },
            new int[][][] {
                new int[][] { new int[] { 1,1,0,0 } },
                new int[][] { new int[] { 0,0,0,0 } },
                new int[][] { new int[] { 2,0,0,0 } },
                new int[][] { new int[] { 0,2,0,0 } },
                new int[][] { new int[] { 2,2,0,0 } },
            },
            new int[][] { new int[] { -5, -5, -5, -5 }, new int[] { 5, 5, 5, 5 } }
        );

        public static readonly Objective Star = new Objective("Star",
            IntegerOps.Create4dStar(),
            new int[][][] {
                new int[][] { new int[] { 0,0,0,0 } },
                new int[][] { new int[] { 2,0,0,0 } },
                new int[][] { new int[] { -2,0,0,0 } },
                new int[][] { new int[] { 0,2,0,0 } },
                new int[][] { new int[] { 0,-2,0,0 } },
                new int[][] { new int[] { 0,0,2,0 } },
                new int[][] { new int[] { 0,0,-2,0 } },
                new int[][] { new int[] { 0,0,0,2 } },
                new int[][] { new int[] { 0,0,0,-2 } },
            },
            new int[][] { new int[] { -5, -5, -5, -5 }, new int[] { 5, 5, 5, 5 } }
        );

        public static readonly Objective Twirled_Pieces = new Objective("Twirled Pieces",
            IntegerOps.Create4dCube(2),
            new int[][][] {
                IntegerOps.Trans(IntegerOps.Rot(new int[][] {
                    new int[] { 0,0,0,0 },
                    new int[] { 0,0,1,0 },
                    new int[] { 1,0,1,0 },
                    new int[] { 1,1,1,0 },
                    new int[] { 1,1,1,1 },
                    new int[] { 1,0,1,1 },
                    new int[] { 0,0,1,1 },
                    new int[] { 0,0,0,1 },
                }, 0, 2), new int[] { 1,0,0,0 }),
                IntegerOps.Trans(new int[][] {
                    new int[] { 1,0,0,0 },
                    new int[] { 1,1,0,0 },
                    new int[] { 0,1,0,0 },
                    new int[] { 0,1,1,0 },
                    new int[] { 0,1,1,1 },
                    new int[] { 0,1,0,1 },
                    new int[] { 1,1,0,1 },
                    new int[] { 1,0,0,1 },
                }, new int[] { 3,0,0,0 })
            },
            new int[][] { new int[] { -5, -5, -5, -5 }, new int[] { 5, 5, 5, 5 } }
        );

        public static readonly Objective D3Box = new Objective("3D Box",
            new int[][] {
                new int[] { 0,0,0,0 }, new int[] { 1,0,0,0 }, new int[] { 2,0,0,0 },
                new int[] { 0,1,0,0 }, new int[] { 1,1,0,0 }, new int[] { 2,1,0,0 },
                new int[] { 0,2,0,0 }, new int[] { 1,2,0,0 }, new int[] { 2,2,0,0 },
                new int[] { 0,0,1,0 }, new int[] { 1,0,1,0 }, new int[] { 2,0,1,0 },
                new int[] { 0,1,1,0 }, new int[] { 1,1,1,0 }, new int[] { 2,1,1,0 },
                new int[] { 0,2,1,0 }, new int[] { 1,2,1,0 }, new int[] { 2,2,1,0 },
                new int[] { 0,0,2,0 }, new int[] { 1,0,2,0 }, new int[] { 2,0,2,0 },
                new int[] { 0,1,2,0 }, new int[] { 1,1,2,0 }, new int[] { 2,1,2,0 },
                new int[] { 0,2,2,0 }, new int[] { 1,2,2,0 }, new int[] { 2,2,2,0 },
            },
            new int[][][] {
                new int[][] {
                    new int[] { 0,0,0,0 }, new int[] { 1,0,0,0 }, new int[] { 2,0,0,0 },
                    new int[] { 0,1,0,0 }, new int[] { 1,1,0,0 }, new int[] { 2,1,0,0 },
                    new int[] { 0,2,0,0 }, new int[] { 1,2,0,0 }, new int[] { 2,2,0,0 },
                    new int[] { 0,0,1,0 }, new int[] { 1,0,1,0 }, new int[] { 2,0,1,0 },
                    new int[] { 0,1,1,0 },
                    new int[] { 2,1,1,0 }, new int[] { 0,2,1,0 }, new int[] { 1,2,1,0 }, new int[] { 2,2,1,0 },
                    new int[] { 0,0,2,0 }, new int[] { 1,0,2,0 }, new int[] { 2,0,2,0 },
                    new int[] { 0,1,2,0 }, new int[] { 1,1,2,0 }, new int[] { 2,1,2,0 },
                    new int[] { 0,2,2,0 }, new int[] { 1,2,2,0 }, new int[] { 2,2,2,0 },
                },
                new int[][] {
                    new int[] { 4,1,1,0 },
                }
            },
            new int[][] { new int[] { -5, -5, -5, -5 }, new int[] { 5, 5, 5, 5 } }
        );

        public static readonly Objective Mirrored_Worms = new Objective("Mirrored Worms",
            new int[][] {
                new int[] { 0,0,0,0 }, new int[] { 1,0,0,0 },
                new int[] { 1,1,0,0 }, new int[] { 1,1,1,0 }, new int[] { 1,1,1,-1 },
                new int[] { 2,0,0,0 }, new int[] { 3,0,0,0 },
                new int[] { 3,1,0,0 }, new int[] { 3,1,1,0 }, new int[] { 3,1,1,-1 },
            },
            new int[][][] {
                new int[][] {
                    new int[] { 0,0,0,0 },  new int[] { -1,0,0,0 },
                    new int[] { -1,-1,0,0 }, new int[] { -1,-1,-1,0 }, new int[] { -1,-1,-1,1 },
                },
                new int[][] {
                    new int[] { 2,0,0,0 }, new int[] { 3,0,0,0 },
                    new int[] { 3,1,0,0 }, new int[] { 3,1,1,0 }, new int[] { 3,1,1,-1 },
                },
            },
            new int[][] { new int[] { -5, -5, -5, -5 }, new int[] { 5, 5, 5, 5 } }
        );

        public static readonly Objective Plug = new Objective("Plug",
            IntegerOps.Create4dCube(2),
            new int[][][] {
                IntegerOps.Trans(IntegerOps.Rot(IntegerOps.Rot(new int[][] {
                    new int[] { 0,0,0,0 }, new int[] { 1,0,0,0 },
                    new int[] { 0,1,0,0 }, new int[] { 1,1,0,0 },
                    new int[] { 0,0,1,0 }, new int[] { 1,0,1,0 },
                    new int[] { 0,1,1,0 }, new int[] { 1,1,1,0 },
                    new int[] { 0,0,0,1 },
                }, 0, 3), 0, 3), new int[] { 1,0,0,1 }),
                IntegerOps.Trans(new int[][] {
                    new int[] { 1,0,0,1 }, new int[] { 0,1,0,1 }, new int[] { 1,1,0,1 },
                    new int[] { 0,0,1,1 }, new int[] { 1,0,1,1 }, new int[] { 0,1,1,1 },
                    new int[] { 1,1,1,1 },
                }, new int[] { 3,0,0,0 }),
            },
            new int[][] { new int[] { -5, -5, -5, -5 }, new int[] { 5, 5, 5, 5 } }
        );

        public static readonly Objective TwoRings = new Objective("Two Rings",
            new int[][] {
                new int[] { 0,0,0,0 }, new int[] { 1,0,0,0 }, new int[] { 2,0,0,0 },
                new int[] { 2,1,0,0 }, new int[] { 2,2,0,0 }, new int[] { 1,2,0,0 },
                new int[] { 0,2,0,0 }, new int[] { 0,1,0,0 },
                new int[] { 0,0,0,1 }, new int[] { 1,0,0,1 }, new int[] { 2,0,0,1 },
                new int[] { 2,1,0,1 }, new int[] { 2,2,0,1 }, new int[] { 1,2,0,1 },
                new int[] { 0,2,0,1 }, new int[] { 0,1,0,1 }
            },
            new int[][][] {
                new int[][] {
                    new int[] { 0,0,0,0 }, new int[] { 1,0,0,0 }, new int[] { 2,0,0,0 },
                    new int[] { 2,1,0,0 }, new int[] { 2,2,0,0 }, new int[] { 1,2,0,0 },
                    new int[] { 0,2,0,0 }, new int[] { 0,1,0,0 }
                },
                new int[][] {
                    new int[] { 1,1,-1,0 }, new int[] { 1,1,0,0 }, new int[] { 1,1,1,0 },
                    new int[] { 2,1,1,0 }, new int[] { 3,1,1,0 }, new int[] { 3,1,0,0 },
                    new int[] { 3,1,-1,0 }, new int[] { 2,1,-1,0 },
                }
            },
            new int[][] { new int[] { -5, -5, -5, -5 }, new int[] { 5, 5, 5, 5 } }
        );

        public static readonly Objective Clamp = new Objective("4D Clamp",
            new int[][] {
                new int[] { 0,0,0,0 }, new int[] { 1,0,0,0 }, new int[] { 2,0,0,0 },
                new int[] { 0,1,0,0 }, new int[] { 1,1,0,0 }, new int[] { 2,1,0,0 },
                new int[] { 0,2,0,0 }, new int[] { 1,2,0,0 }, new int[] { 2,2,0,0 },
                new int[] { 0,0,1,0 }, new int[] { 1,0,1,0 }, new int[] { 2,0,1,0 },
                new int[] { 0,1,1,0 }, new int[] { 1,1,1,0 }, new int[] { 2,1,1,0 },
                new int[] { 0,2,1,0 }, new int[] { 1,2,1,0 }, new int[] { 2,2,1,0 },
                new int[] { 0,0,2,0 }, new int[] { 1,0,2,0 }, new int[] { 2,0,2,0 },
                new int[] { 0,1,2,0 }, new int[] { 1,1,2,0 }, new int[] { 2,1,2,0 },
                new int[] { 0,2,2,0 }, new int[] { 1,2,2,0 }, new int[] { 2,2,2,0 },
            },
            new int[][][] {
                new int[][] {
                    new int[] { 0,0,0,0 }, new int[] { 1,0,0,0 }, new int[] { 2,0,0,0 },
                    new int[] { 0,1,0,0 }, new int[] { 1,1,0,0 }, new int[] { 2,1,0,0 },
                    new int[] { 0,2,0,0 }, new int[] { 1,2,0,0 }, new int[] { 2,2,0,0 },
                    new int[] { 0,0,1,0 },
                    new int[] { 2,0,1,0 }, new int[] { 0,1,1,0 }, new int[] { 1,1,1,0 }, new int[] { 2,1,1,0 },
                    new int[] { 0,2,1,0 },
                    new int[] { 2,2,1,0 },
                    new int[] { 0,0,2,0 }, new int[] { 1,0,2,0 }, new int[] { 2,0,2,0 },
                    new int[] { 0,1,2,0 }, new int[] { 1,1,2,0 }, new int[] { 2,1,2,0 },
                    new int[] { 0,2,2,0 }, new int[] { 1,2,2,0 }, new int[] { 2,2,2,0 },
                },
                new int[][] {
                    new int[] { 4,0,1,0 },
                    new int[] { 4,2,1,0 },
                }
            },
            new int[][] { new int[] { -5, -5, -5, -5 }, new int[] { 5, 5, 5, 5 } }
        );

        public static readonly Objective TriClamp = new Objective("Triclamp",
            new int[][] {
                new int[] { 0,0,0,0 }, new int[] { 1,0,0,0 }, new int[] { 2,0,0,0 },
                new int[] { 0,1,0,0 }, new int[] { 1,1,0,0 }, new int[] { 2,1,0,0 },
                new int[] { 0,2,0,0 }, new int[] { 1,2,0,0 }, new int[] { 2,2,0,0 },
                new int[] { 0,0,1,0 }, new int[] { 1,0,1,0 }, new int[] { 2,0,1,0 },
                new int[] { 0,1,1,0 }, new int[] { 1,1,1,0 }, new int[] { 2,1,1,0 },
                new int[] { 0,2,1,0 }, new int[] { 1,2,1,0 }, new int[] { 2,2,1,0 },
                new int[] { 0,0,2,0 }, new int[] { 1,0,2,0 }, new int[] { 2,0,2,0 },
                new int[] { 0,1,2,0 }, new int[] { 1,1,2,0 }, new int[] { 2,1,2,0 },
                new int[] { 0,2,2,0 }, new int[] { 1,2,2,0 }, new int[] { 2,2,2,0 },
            },
            new int[][][] {
                new int[][] {
                    new int[] { 0,0,0,0 }, new int[] { 1,0,0,0 }, new int[] { 2,0,0,0 },
                    new int[] { 0,1,0,0 },
                    new int[] { 2,1,0,0 }, new int[] { 0,2,0,0 }, new int[] { 1,2,0,0 }, new int[] { 2,2,0,0 },
                    new int[] { 0,0,1,0 },
                    new int[] { 2,0,1,0 }, new int[] { 1,1,1,0 },
                    new int[] { 0,2,1,0 },
                    new int[] { 2,2,1,0 },
                    new int[] { 0,0,2,0 }, new int[] { 1,0,2,0 }, new int[] { 2,0,2,0 },
                    new int[] { 0,1,2,0 },
                    new int[] { 2,1,2,0 }, new int[] { 0,2,2,0 }, new int[] { 1,2,2,0 }, new int[] { 2,2,2,0 },
                },
                new int[][] {
                    new int[] { 5,1,0,0 }, new int[] { 5,0,1,0 }, new int[] { 5,2,1,0 },
                    new int[] { 6,1,1,0 }, new int[] { 4,1,1,0 }, new int[] { 5,1,2,0 },
                }
            },
            new int[][] { new int[] { -5, -5, -5, -5 }, new int[] { 5, 5, 5, 5 } }
        );

        public static readonly Objective Exam = new Objective("4 Tesseract Pieces",
            IntegerOps.Create4dCube(2),
            new int[][][] {
                new int[][] {
                    new int[] { 0,1,0,0 }, new int[] { 0,0,0,0 },
                    new int[] { 1,0,0,0 }, new int[] { 1,0,1,0 },
                },
                IntegerOps.Trans(IntegerOps.Rot(new int[][] {
                    new int[] { 1,0,0,1 }, new int[] { 1,0,1,1 },
                    new int[] { 0,0,1,1 }, new int[] { 0,0,1,0 },
                }, 1, 3), new int[] { 3,1,-1,0 }),
                IntegerOps.Trans(IntegerOps.Rot(IntegerOps.Rot(new int[][] {
                    new int[] { 1,1,0,0 }, new int[] { 1,1,0,1 },
                    new int[] { 0,1,0,1 }, new int[] { 0,0,0,1 },
                }, 2, 1), 2, 1), new int[] { 0,4,0,-1 }),
                IntegerOps.Trans(IntegerOps.Rot(new int[][] {
                    new int[] { 0,1,1,0 }, new int[] { 0,1,1,1 },
                    new int[] { 1,1,1,1 }, new int[] { 1,1,1,0 },
                }, 3, 1), new int[] { 3,3,-1,1 }),
            },
            new int[][] { new int[] { -5, -5, -5, -5 }, new int[] { 5, 5, 5, 5 } }
        );

        public static Objective[] Values()
        {
            var fields = typeof(ObjectiveCatalog).GetFields(BindingFlags.Public | BindingFlags.Static);
            var result = new Objective[fields.Length];
            for (int i = 0; i < fields.Length; i++)
                result[i] = (Objective)fields[i].GetValue(null);
            return result;
        }
    }
}
