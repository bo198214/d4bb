using System.Collections.Generic;
using NUnit.Framework;
using D4BB.Comb;

namespace D4BB.Comb
{
    /// <summary>
    /// Tests for the integer orthogonal-transform helpers in <see cref="IntegerOps"/>.
    ///
    /// Regression guard: <c>MirrorRotations(1)</c> must return the 1x1 matrix with
    /// det -1. When it returned +1, the "negative subdeterminant" branch of
    /// <c>Rotations</c>/<c>MirrorRotations</c> propagated the wrong sign, so for every
    /// dim >= 2 both methods returned a 50/50 mix of det +-1 instead of a proper
    /// rotation group. That broke <c>MotionEqual</c> (which rotates only one side and
    /// therefore relies on the transform set being a group closed under inverses):
    /// it produced false negatives for genuinely congruent compounds, which in turn
    /// can mis-judge the win condition in <c>GameLevel</c>.
    /// </summary>
    public class IntegerOpsRotationTests
    {
        // --- helpers ---

        private static int Det(int[][] m)
        {
            int n = m.Length;
            if (n == 1) return m[0][0];
            int d = 0;
            for (int c = 0; c < n; c++)
            {
                int[][] sub = new int[n - 1][];
                for (int i = 1; i < n; i++)
                {
                    sub[i - 1] = new int[n - 1];
                    int cc = 0;
                    for (int j = 0; j < n; j++)
                    {
                        if (j == c) continue;
                        sub[i - 1][cc++] = m[i][j];
                    }
                }
                d += ((c % 2 == 0) ? 1 : -1) * m[0][c] * Det(sub);
            }
            return d;
        }

        private static int[][] Mul(int[][] a, int[][] b)
        {
            int n = a.Length;
            int[][] c = new int[n][];
            for (int i = 0; i < n; i++)
            {
                c[i] = new int[n];
                for (int j = 0; j < n; j++)
                {
                    int s = 0;
                    for (int k = 0; k < n; k++) s += a[i][k] * b[k][j];
                    c[i][j] = s;
                }
            }
            return c;
        }

        private static string Key(int[][] m)
        {
            string s = "";
            foreach (int[] row in m)
                foreach (int x in row) s += x + ",";
            return s;
        }

        // --- counts ---

        [TestCase(1, 1)]
        [TestCase(2, 4)]
        [TestCase(3, 24)]
        [TestCase(4, 192)] // |proper hyperoctahedral group| = 2^(n-1) * n!
        public void RotationsHaveExpectedCount(int dim, int expected)
        {
            Assert.That(IntegerOps.Rotations(dim).Length, Is.EqualTo(expected));
            Assert.That(IntegerOps.MirrorRotations(dim).Length, Is.EqualTo(expected));
        }

        // --- determinants ---

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void RotationsAreProper(int dim)
        {
            foreach (int[][] m in IntegerOps.Rotations(dim))
                Assert.That(Det(m), Is.EqualTo(1), "a rotation must have det +1");
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void MirrorRotationsAreImproper(int dim)
        {
            foreach (int[][] m in IntegerOps.MirrorRotations(dim))
                Assert.That(Det(m), Is.EqualTo(-1), "a mirror rotation must have det -1");
        }

        // --- group structure (the property MotionEqual depends on) ---

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void RotationsAreDistinct(int dim)
        {
            int[][][] rot = IntegerOps.Rotations(dim);
            HashSet<string> set = new HashSet<string>();
            foreach (int[][] m in rot) set.Add(Key(m));
            Assert.That(set.Count, Is.EqualTo(rot.Length), "Rotations contains duplicates");
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void RotationsAreClosedUnderMultiplication(int dim)
        {
            int[][][] rot = IntegerOps.Rotations(dim);
            HashSet<string> set = new HashSet<string>();
            foreach (int[][] m in rot) set.Add(Key(m));

            foreach (int[][] a in rot)
                foreach (int[][] b in rot)
                    Assert.That(set.Contains(Key(Mul(a, b))), Is.True,
                        "product of two rotations is not itself a rotation (set is not a group)");
        }

        // --- MotionEqual semantics ---

        [Test]
        public void MotionEqualIsReflexive()
        {
            int[][] a =
            {
                new[] { 0, 0, 0, 0 },
                new[] { 1, 0, 0, 0 },
                new[] { 1, 1, 0, 0 },
                new[] { 1, 1, 1, 0 },
            };
            Assert.That(IntegerOps.MotionEqual(a, a), Is.True);
        }

        [Test]
        public void MotionEqualDetectsRotatedCopy()
        {
            // a 2x2 square in the x-y plane and the same square in the z-w plane
            int[][] squareXY =
            {
                new[] { 0, 0, 0, 0 },
                new[] { 1, 0, 0, 0 },
                new[] { 0, 1, 0, 0 },
                new[] { 1, 1, 0, 0 },
            };
            int[][] squareZW =
            {
                new[] { 0, 0, 0, 0 },
                new[] { 0, 0, 1, 0 },
                new[] { 0, 0, 0, 1 },
                new[] { 0, 0, 1, 1 },
            };
            Assert.That(IntegerOps.MotionEqual(squareXY, squareZW), Is.True);
            Assert.That(IntegerOps.MotionEqual(squareZW, squareXY), Is.True); // symmetric
        }

        [Test]
        public void MotionEqualRejectsIncongruentShapes()
        {
            int[][] line =
            {
                new[] { 0, 0, 0, 0 },
                new[] { 1, 0, 0, 0 },
                new[] { 2, 0, 0, 0 },
                new[] { 3, 0, 0, 0 },
            };
            int[][] square =
            {
                new[] { 0, 0, 0, 0 },
                new[] { 1, 0, 0, 0 },
                new[] { 0, 1, 0, 0 },
                new[] { 1, 1, 0, 0 },
            };
            Assert.That(IntegerOps.MotionEqual(line, square), Is.False);
        }

        [Test]
        public void MotionEqualDetectsCongruentTesseractHalves()
        {
            // Concrete regression case: two complementary 8-cell halves of the 2^4
            // tesseract that are proper-rotation congruent. With the MirrorRotations(1)
            // sign bug, MotionEqual returned false for this pair.
            int[][] p =
            {
                new[] { 0, 0, 0, 0 }, new[] { 0, 0, 0, 1 }, new[] { 0, 0, 1, 0 }, new[] { 0, 0, 1, 1 },
                new[] { 0, 1, 0, 0 }, new[] { 0, 1, 0, 1 }, new[] { 1, 0, 0, 1 }, new[] { 1, 0, 1, 1 },
            };
            int[][] q =
            {
                new[] { 0, 0, 0, 0 }, new[] { 0, 0, 0, 1 }, new[] { 0, 0, 1, 0 }, new[] { 0, 0, 1, 1 },
                new[] { 0, 1, 0, 1 }, new[] { 0, 1, 1, 1 }, new[] { 1, 0, 0, 1 }, new[] { 1, 1, 0, 1 },
            };
            Assert.That(IntegerOps.MotionEqual(p, q), Is.True);
            Assert.That(IntegerOps.MotionEqual(q, p), Is.True);
        }

        [Test]
        public void SingleCubeHasFullRotationSymmetry()
        {
            int[][] cube = { new[] { 0, 0, 0, 0 } };
            // every one of the 192 proper rotations fixes a single point
            Assert.That(IntegerOps.RotationSymmetry(cube), Is.EqualTo(192));
        }
    }
}
