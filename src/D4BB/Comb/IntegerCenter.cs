using System;
using System.Diagnostics;

namespace D4BB.Comb
{
    public class IntegerCenter
    {
        private int[] twice;
        public IntegerCenter(int[] origin)
        {
            Origin(origin);
        }

        /// <summary>
        /// Creates center from given origins
        ///  a must have at least one element
        ///  the given origins can either interpreted
        ///  as origines from cubes
        ///  or as points
        ///  to compute the center of
        /// </summary>
        public IntegerCenter(int[][] a, bool asCubes)
        {
            Debug.Assert(a.Length > 0, "2472911590");
            twice = new int[a[0].Length];
            for (int ix = 0; ix < twice.Length; ix++)
            {
                int sum = 0;
                for (int i = 0; i < a.Length; i++)
                {
                    if (asCubes)
                    {
                        sum += 2 * a[i][ix] + 1;
                    }
                    else
                    {
                        sum += 2 * a[i][ix];
                    }
                }

                twice[ix] = sum / a.Length;
            }

            Round();
        }

        /// <summary>
        /// for a rotation center either each twice[i] is even,
        /// or each twice[i] is odd. Sets to nearest rotation center
        /// </summary>
        private void Round()
        {
            int[] minU = new int[twice.Length];
            int toU = 0; //distance to next uneven point
            int[] minE = new int[twice.Length];
            int toE = 0; //distance to next even point
            for (int i = 0; i < twice.Length; i++)
            {
                int v = twice[i];
                int r = Math.Abs(v % 2);
                minU[i] = v - (1 - r);
                toU += 1 - r;
                minE[i] = v - r;
                toE += r;
            }

            if (toU < toE)
            {
                twice = minU;
                return;
            }

            if (toE < toU)
            {
                twice = minE;
                return;
            }

            twice = minE;
        }

        /// <summary>
        /// returns computed real location
        /// </summary>
        public virtual double[] Loc()
        {
            double[] res = new double[twice.Length];
            for (int i = 0; i < res.Length; i++)
            {
                res[i] = twice[i] * 0.5;
            }

            return res;
        }

        /// <summary>
        /// returns computed origin
        /// </summary>
        public virtual int[] Origin()
        {
            int[] res = new int[twice.Length];
            for (int i = 0; i < res.Length; i++)
            {
                res[i] = (twice[i] - 1) / 2;
            }

            return res;
        }

        /// <summary>
        /// sets origin by value
        /// </summary>
        public virtual void Origin(int[] origin)
        {
            twice = new int[origin.Length];
            for (int i = 0; i < origin.Length; i++)
            {
                twice[i] = origin[i] * 2 + 1;
            }
        }

        /// <summary>
        /// returns computed `twice'
        /// </summary>
        public virtual int[] Twice()
        {
            return IntegerOps.Clone(twice);
        }

        /// <summary>
        /// sets `twice' by value
        /// </summary>
        public virtual void Twice(int[] _twice)
        {
            twice = IntegerOps.Clone(_twice);
            Check();
        }

        public virtual void Translate(IntegerSignedAxis v)
        {
            int[] o = Origin();
            IntegerOps.Translate(o, v);
            Origin(o);
        }

        public virtual void Rotate(IntegerCenter o, int v, int w)
        {
            Debug.Assert(v != w, "3996649019");
            IntegerOps.Rotate(twice, o.twice, v, w);
        }

        private void Check()
        {
            if (twice.Length == 0)
            {
                return;
            }

            for (int i = 1; i < twice.Length; i++)
            {
                Debug.Assert(twice[i] % 2 == twice[0] % 2, "9150539992");
            }
        }
    }
}