using System;
using System.Diagnostics;

namespace D4BB.Comb
{
    public class IntegerOps
    {
        public static int Length(int[] a)
        {
            int res = 0;
            for (int i = 0; i < a.Length; i++)
            {
                res += Math.Abs(a[i]);
            }

            return res;
        }

        public static int Dist(int[] a, int[] b)
        {
            int res = 0;
            Debug.Assert(a.Length == b.Length,"1168567507");
            for (int i = 0; i < a.Length; i++)
            {
                res += Math.Abs(b[i] - a[i]);
            }

            Debug.Assert(res == Length(Minus(b, a)),"7558251654");
            return res;
        }

        private static void CopyAdjunctL(int l0, int dim, int[][] adjunct, int[][] matrix)
        {
            for (int l = 0; l < dim; l++)
            {
                matrix[l][0] = 0;
            }

            for (int k = 1; k < dim; k++)
            {
                for (int l = 0; l < l0; l++)
                {
                    matrix[l][k] = adjunct[l][k - 1];
                }

                for (int l = l0 + 1; l < dim; l++)
                {
                    matrix[l][k] = adjunct[l - 1][k - 1];
                }
            }
        }

        /// <summary>
        /// computes all matrices M with det M = -1
        /// the indices are [number][line][column]
        /// </summary>
        public static int[][][] MirrorRotations(int dim)
        {
            int[][][] nr; //negative rotations, result
            if (dim == 1)
            {
                
                nr = new int[1][][];
                nr[0] = new int[1][];
                nr[0][0] = new int[1];
                nr[0][0][0] = 1;
            }
            else
            {
                int[][][] snr = MirrorRotations(dim - 1); // sub negative rotations
                int[][][] spr = Rotations(dim - 1); // sub positive rotations
                int N = 2 * dim * snr.Length; //number of negative/positive rotations 

                nr = new int[N][][];
                for (int i1=0;i1<N;i1++) {
                    nr[i1] = new int[dim][];
                    for (int i2=0;i2<dim;i2++) {
                        nr[i1][i2] = new int[dim];
                    }
                }
                int n = 0;

                //positive subdeterminants
                for (int i = 0; i < dim; i++)
                {
                    for (int ispr = 0; ispr < spr.Length; ispr++)
                    {
                        CopyAdjunctL(i, dim, spr[ispr], nr[n]);
                        nr[n][i][0] = (i % 2) * 2 - 1; // -1 at [0][0], alternating

                        //for the determinant being -1, the subdeterminant must be 1 
                        n++;
                    }
                }


                //negative subdeterminants
                for (int i = 0; i < dim; i++)
                {
                    for (int isnr = 0; isnr < snr.Length; isnr++)
                    {
                        CopyAdjunctL(i, dim, snr[isnr], nr[n]);
                        nr[n][i][0] = 1 - (i % 2) * 2; // 1 at [0][0], alternating

                        //for the determinant being -1, the subdeterminant must be -1
                        n++;
                    }
                }
            }

            return nr;
        }

        /// <summary>
        /// computes all matrices M with det M = 1
        /// the indices are [number][line][column]
        /// </summary>
        public static int[][][] Rotations(int dim)
        {
            int[][][] pr; //positive rotations, result
            if (dim == 1)
            {
                pr = new int[1][][];
                pr[0] = new int[1][];
                pr[0][0] = new int[1];
                pr[0][0][0] = 1;
            }
            else
            {
                int[][][] snr = MirrorRotations(dim - 1); // sub negative rotations
                int[][][] spr = Rotations(dim - 1); // sub positive rotations
                int N = 2 * dim * snr.Length; //number of negative/positive rotations 
                pr = new int[N][][];
                for (int i1=0;i1<N;i1++) {
                    pr[i1] = new int[dim][];
                    for (int i2=0;i2<dim;i2++) {
                        pr[i1][i2] = new int[dim];
                    }
                }
                
                int n = 0;

                //positive subdeterminants
                for (int i = 0; i < dim; i++)
                {
                    for (int ispr = 0; ispr < spr.Length; ispr++)
                    {
                        CopyAdjunctL(i, dim, spr[ispr], pr[n]);
                        pr[n][i][0] = 1 - (i % 2) * 2; // 1 at [0][0], alternating

                        //for the determinant being 1, the subdeterminant must be 1
                        n++;
                    }
                }


                //negative subdeterminants
                for (int i = 0; i < dim; i++)
                {
                    for (int isnr = 0; isnr < snr.Length; isnr++)
                    {
                        CopyAdjunctL(i, dim, snr[isnr], pr[n]);
                        pr[n][i][0] = (i % 2) * 2 - 1; //-1 at [0][0], alternating

                        //for the determinant being 1, the subdeterminant must be -1
                        n++;
                    }
                }
            }

            return pr;
        }

        /// <summary>
        /// computes the vector of smalles coordinates of compound a
        /// for dimension information a must not be empty
        /// </summary>
        public static int[] ExtentMin(int[][] a)
        {
            int dim = a[0].Length;
            int[] v = new int[dim];
            for (int ix = 0; ix < dim; ix++)
            {
                v[ix] = Int32.MaxValue;
            }

            for (int i = 0; i < a.Length; i++)
            {
                int[] p = a[i];
                for (int ix = 0; ix < dim; ix++)
                {
                    if (p[ix] < v[ix])
                    {
                        v[ix] = p[ix];
                    }
                }
            }

            return v;
        }

        /// <summary>
        /// computes the vector of largest coordinates of compound a
        /// for dimension information a must not be empty
        /// </summary>
        public static int[] ExtentMax(int[][] a)
        {
            int dim = a[0].Length;
            int[] v = new int[dim];
            for (int ix = 0; ix < dim; ix++)
            {
                v[ix] = Int32.MinValue;
            }

            for (int i = 0; i < a.Length; i++)
            {
                int[] p = a[i];
                for (int ix = 0; ix < dim; ix++)
                {
                    if (p[ix] > v[ix])
                    {
                        v[ix] = p[ix];
                    }
                }
            }

            return v;
        }

        public static bool VecEqual(int[] a, int[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// verifies if compound a as a set of coordinates is contained in b.
        /// </summary>
        /// <returns>true if a is contained in b</returns>
        public static bool SetContained(int[][] a, int[][] b)
        {
            if (a.Length > b.Length)
            {
                return false;
            } //heuristic optimization

            for (int i = 0; i < a.Length; i++)
            {
                bool contained = false;
                for (int j = 0; j < b.Length; j++)
                {
                    if (VecEqual(a[i], b[j]))
                    {
                        contained = true;
                        break;
                    }
                }

                if (!contained)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// verifies if compound a takes the same space points as compound b
        /// </summary>
        /// <returns>true if equal</returns>
        public static bool SetEqual(int[][] a, int[][] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }

            return SetContained(a, b); //this suffice because there are no two equal cubes in a compound
        }

        public static bool SetContained(int[] a, int[] b)
        {
            if (a.Length > b.Length)
            {
                return false;
            } //heuristic optimization

            for (int i = 0; i < a.Length; i++)
            {
                bool contained = false;
                for (int j = 0; j < b.Length; j++)
                {
                    if (a[i] == b[j])
                    {
                        contained = true;
                        break;
                    }
                }

                if (!contained)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// precondition: a must have no 2 equal elements and b too
        /// </summary>
        public static bool SetEqual(int[] a, int[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }

            return SetContained(a, b);
        }

        public static int[] Minus(int[] a, int[] b)
        {
            int[] r = new int[a.Length];
            for (int i = 0; i < a.Length; i++)
            {
                r[i] = a[i] - b[i];
            }

            return r;
        }

        public static int[] Minus(int[] a)
        {

            //		int[] r = new int[a.Length];
            //		for (int i=0;i<a.Length;i++) {
            //			r[i] = -a[i];
            //		}
            int[] r = Clone(a);
            Negate(r);
            return r;
        }

        public static void Negate(int[] a)
        {
            for (int i = 0; i < a.Length; i++)
            {
                a[i] = -a[i];
            }
        }

        public static int[] Plus(int[] a, int[] b)
        {

            //		int[] r = new int[a.Length];
            //		for (int i=0;i<a.Length;i++) {
            //			r[i] = a[i]+b[i];
            //		}
            int[] r = Clone(a);
            Translate(r, b);
            return r;
        }

        public static void Translate(int[] a, int[] d)
        {
            for (int i = 0; i < a.Length; i++)
            {
                a[i] += d[i];
            }
        }

        public static int[][] Trans(int[][] a, int[] d)
        {

            //		int[][] r = new int[a.Length][];
            //		for (int i=0;i<a.Length;i++) {
            //			r[i] = plus(a[i],d);
            //		}
            int[][] r = Clone(a);
            for (int i = 0; i < a.Length; i++)
            {
                Translate(r[i], d);
            }

            return r;
        }

        public static void Translate(int[][] a, int[] d)
        {
            for (int i = 0; i < a.Length; i++)
            {
                Translate(a[i], d);
            }
        }

        public static int[] Map(int[] a, int[][] matrix)
        {
            int[] r = new int[a.Length];
            for (int ix = 0; ix < matrix.Length; ix++)
            {
                r[ix] = 0;
                for (int iv = 0; iv < matrix.Length; iv++)
                {
                    r[ix] += a[iv] * matrix[iv][ix];
                }
            }

            return r;
        }

        public static int[][] Map(int[][] a, int[][] matrix)
        {
            int[][] r = new int[a.Length][];
            for (int i = 0; i < a.Length; i++)
            {
                r[i] = Map(a[i], matrix);
            }

            return r;
        }

        /// <summary>
        /// verifies whether compound a can be shifted to be b
        /// </summary>
        /// <returns>true if equal</returns>
        public static bool TransEqual(int[][] a, int[][] b)
        {
            int[][] b2 = Trans(b, Minus(ExtentMin(a), ExtentMin(b)));
            return SetEqual(a, b2);
        }

        /// <summary>
        /// verifies whether compound a can be shifted to be contained in b
        /// </summary>
        /// <returns>true if a is contained in b</returns>
        public static bool TransContained(int[][] a, int[][] b)
        {
            int[] diff = Minus(Minus(ExtentMax(b), ExtentMin(b)), Minus(ExtentMax(a), ExtentMin(a)));
            int prod = 1;
            for (int i = 0; i < diff.Length; i++)
            {
                prod *= diff[i] + 1;
            }

            int[][] a2 = Trans(a, Minus(ExtentMin(b), ExtentMin(a)));
            for (int i = 0; i < prod; i++)
            {
                int n = i;
                int[] d = new int[diff.Length];
                for (int j = 0; j < diff.Length; j++)
                {
                    int bas = diff[j] + 1;
                    d[j] = n % bas;
                    n /= bas;
                }

                int[][] a3 = Trans(a2, d);
                if (SetContained(a3, b))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// verifies whether a can be rotated and shifted to be b
        /// dimension is retrieved from first element of a, so a must not be empty
        /// </summary>
        /// <returns>true if equal</returns>
        public static bool MotionEqual(int[][] a, int[][] b)
        {

            //try all rotations of b and shiftCompare
            int[][][] rot = IntegerOps.Rotations(a[0].Length);
            for (int i = 0; i < rot.Length; i++)
            {
                if (TransEqual(a, Map(b, rot[i])))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// verifies whether a can be rotated and shifted
        /// to be contained or equal to be b
        /// </summary>
        /// <returns>true if a is contained in b</returns>
        public static bool MotionContained(int[][] a, int[][] b)
        {
            int[][][] rot = IntegerOps.Rotations(a[0].Length);
            for (int i = 0; i < rot.Length; i++)
            {
                if (TransContained(a, Map(b, rot[i])))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// returns the number of different rotations of <b>a</b> that
        /// are transEqual to the original <b>a</b>. Minimum is 1 because
        /// of the identity rotation.
        /// </summary>
        /// <param name="a">the compound</param>
        /// <returns>number of rotations</returns>
        public static int RotationSymmetry(int[][] a)
        {
            int[][][] rot = IntegerOps.Rotations(a[0].Length);
            int symmetry = 0;
            for (int i = 0; i < rot.Length; i++)
            {
                if (TransEqual(a, Map(a, rot[i])))
                {
                    symmetry++;
                }
            }

            return symmetry;
        }

        //	/**
        //	 * 
        //	 * verifies whether there is a motion to embed a into b
        //	 */
        //	public static boolean motionContained(int[][] a, int[][] b) {
        //		//TODO
        //		return true;
        //	}
        /// <summary>
        /// returns the n-th 4d unit vector
        /// i.e. 1 -> (1,0,0,0), 2-> (0,1,0,0)
        /// for negative n returns the corresponding negated unit vector
        /// </summary>
        public static int[] UnitVector(int n)
        {
            int[] r = new[]
            {
                0,
                0,
                0,
                0
            };
            if (n > 0)
            {
                r[n - 1] = 1;
                return r;
            }

            if (n < 0)
            {
                r[-n - 1] = -1;
                return r;
            }

            return r;
        }

        /// <summary>
        /// returns the n-th d-dimensional unit vector
        /// </summary>
        /// <remarks>@seeunitVector</remarks>
        public static int[] UnitVector(int n, int d)
        {
            int[] r = new int[d];
            for (int i = 0; i < d; i++)
            {
                r[i] = 0;
            }

            if (n > 0)
            {
                r[n - 1] = 1;
                return r;
            }

            if (n < 0)
            {
                r[n - 1] = -1;
                return r;
            }

            return r;
        }

        /// <summary>
        /// Compound created by going from o first into direction d[0]
        /// then d[1], d[2], etc. Each d[i] can be the index of a unit vector
        /// i.e. 1,2,3 or 4. Or it can be one of -1,-2,-3,-4 denoting the
        /// inverse respective unit vector.
        /// </summary>
        public static int[][] CompoundTail(int[] o, int[] d)
        {
            int[][] r = new int[1 + d.Length][];
            r[0] = o;
            for (int i = 0; i < d.Length; i++)
            {
                r[i + 1] = Plus(r[i], UnitVector(d[i]));
            }

            return r;
        }

        public static int[][] Create4dTail(IntegerSignedAxis[] d)
        {
            int[][] res = new int[d.Length + 1][];
            res[0] = new int[]
            {
                0,
                0,
                0,
                0
            };
            for (int i = 0; i < d.Length; i++)
            {
                res[i + 1] = Plus(res[i], d[i].UnitVector(4));
            }

            return res;
        }

        public static int[][] Create4dStar()
        {
            return new int[][]
            {
                new int[]
                {
                    0,
                    0,
                    0,
                    0
                },
                new int[]
                {
                    1,
                    0,
                    0,
                    0
                },
                new int[]
                {
                    0,
                    1,
                    0,
                    0
                },
                new int[]
                {
                    0,
                    0,
                    1,
                    0
                },
                new int[]
                {
                    0,
                    0,
                    0,
                    1
                },
                new int[]
                {
                    -1,
                    0,
                    0,
                    0
                },
                new int[]
                {
                    0,
                    -1,
                    0,
                    0
                },
                new int[]
                {
                    0,
                    0,
                    -1,
                    0
                },
                new int[]
                {
                    0,
                    0,
                    0,
                    -1
                }
            };
        }

        public static int[][] Create4dCube(int d)
        {
            int[][] res = new int[d * d * d * d][];
            for (int i0 = 0; i0 < d; i0++)
            {
                for (int i1 = 0; i1 < d; i1++)
                {
                    for (int i2 = 0; i2 < d; i2++)
                    {
                        for (int i3 = 0; i3 < d; i3++)
                        {
                            res[d * d * d * i0 + d * d * i1 + d * i2 + i3] = new int[]
                            {
                                i0,
                                i1,
                                i2,
                                i3
                            };
                        }
                    }
                }
            }

            return res;
        }

        public static void Rotate(int[] a, int v, int w)
        {
            Debug.Assert(v != w,"7571997825");

            //		int sign = v.pmSign() * w.pmSign();
            //		int h = a[w.axis];
            //		a[w.axis]=sign*a[v.axis];
            //		a[v.axis]=-sign*h;
            int h = a[w];
            a[w] = a[v];
            a[v] = -h;
        }

        public static void Rotate(int[][] a, int v, int w)
        {
            Debug.Assert(v != w, "2912366080");
            for (int i = 0; i < a.Length; i++)
            {
                Rotate(a[i], v, w);
            }
        }

        public static int[] Rot(int[] a, int v, int w)
        {
            Debug.Assert(v != w, "0571277225");
            int[] r = Clone(a);

            //		int sign, sign1=1, sign2=1;
            //		if (v<0) { sign1=-1; }
            //		if (w<0) { sign2=-1; }
            //		sign = sign1 * sign2;
            //		r[w-1]=sign*a[v-1];
            //		r[v-1]=-sign*a[w-1];
            Rotate(r, v, w);
            return r;
        }

        //	public static int[] rot(int[] a,int[] o,int v, int w) {
        //		return plus(rot(minus(a,o),v,w),o);
        //	}
        public static void Rotate(int[] a, int[] o, int v, int w)
        {
            Debug.Assert(v != w, "9834306068");
            Translate(a, Minus(o));
            Rotate(a, v, w);
            Translate(a, o);
        }

        public static void Rotate(int[] a, IntegerCenter o, int v, int w)
        {
            Debug.Assert(v != w, "7635171047");
            for (int i = 0; i < a.Length; i++)
            {
                a[i] *= 2;
            }

            Rotate(a, o.Twice(), v, w);
            for (int i = 0; i < a.Length; i++)
            {
                a[i] /= 2;
            }
        }

        public static void RotateAsCenters(int[] a, IntegerCenter o, int v, int w)
        {
            Debug.Assert(v != w, "2500632269");
            for (int i = 0; i < a.Length; i++)
            {
                a[i] = a[i] * 2 + 1;
            }

            Rotate(a, o.Twice(), v, w);
            for (int i = 0; i < a.Length; i++)
            {
                a[i] = (a[i] - 1) / 2;
            }
        }

        public static void Rotate(int[][] a, IntegerCenter o, int v, int w)
        {
            Debug.Assert(v != w, "6038321148");
            for (int i = 0; i < a.Length; i++)
            {
                Rotate(a[i], o, v, w);
            }
        }

        public static int[][] Rot(int[][] a, int v, int w)
        {
            Debug.Assert(v != w, "2744393190");
            int[][] b = Clone(a);
            Rotate(b, v, w);
            return b;
        }

        public static int[][] Rot(int[][] a, IntegerCenter o, int v, int w)
        {
            Debug.Assert(v != w, "4492124332");
            int[][] b = Clone(a);
            Rotate(b, o, v, w);
            return b;
        }

        //	public static void rotate(int[][] a,SignedAxis v, SignedAxis w) {
        //		rotate(a,a[0],v,w);
        //	}
        public static int[] Trans(int[] a, int v)
        {
            return Plus(a, UnitVector(v));
        }

        public static void Translate(int[] a, IntegerSignedAxis v)
        {
            a[v.axis] += v.PmSign();
        }

        public static void Translate(int[][] a, IntegerSignedAxis v)
        {
            for (int i = 0; i < a.Length; i++)
            {
                Translate(a[i], v);
            }
        }

        public static bool Intersecting(int[][][] compounds)
        {
            for (int i1 = 0; i1 < compounds.Length; i1++)
            {
                int[][] c1 = compounds[i1];
                for (int i2 = i1 + 1; i2 < compounds.Length; i2++)
                {
                    int[][] c2 = compounds[i2];
                    for (int j1 = 0; j1 < c1.Length; j1++)
                    {
                        int[] t1 = c1[j1];
                        for (int j2 = 0; j2 < c2.Length; j2++)
                        {
                            int[] t2 = c2[j2];
                            if (VecEqual(t1, t2))
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        public static int[] Clone(int[] a)
        {
            int[] r = new int[a.Length];
            for (int i = 0; i < a.Length; i++)
            {
                r[i] = a[i];
            }

            return r;
        }

        public static int[][] Clone(int[][] a)
        {
            int[][] r = new int[a.Length][];
            for (int i = 0; i < a.Length; i++)
            {
                r[i] = Clone(a[i]);
            }

            return r;
        }

        public static bool D3adjacent(int[] a, int[] b)
        {
            int diff = 0;
            for (int ix = 0; ix < a.Length; ix++)
            {
                diff += Math.Abs(a[ix] - b[ix]);
            }

            return diff == 1;
        }

        public static bool D3adjacent(int[][] a, int[][] b)
        {
            for (int i = 0; i < a.Length; i++)
            {
                for (int j = 0; j < b.Length; j++)
                {
                    if (D3adjacent(a[i], b[j]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static string ToString(int[] v)
        {
            string res = "[";
            for (int i = 0; i < v.Length; i++)
            {
                res += v[i];
                if (i < v.Length - 1)
                {
                    res += ",";
                }
            }

            res += "]";
            return res;
        }
        public static int[] Cross(int[][] vs) {
            var n = vs.Length+1;
            Debug.Assert(n==vs[0].Length);
            int[] res = new int[n];
            foreach (var permutation in Permutation.Permutations(n)) {
                var i = permutation[0];
                var mul = 1;
                for (int k=0;k<n-1;k++) {
                    mul *= vs[k][permutation[k+1]];
                }
                res[i] += Permutation.Int(permutation.Parity())*mul;
            }
            return res;
        }
        public static int[] Cross(int[] av) {
            //cross(x) = y
            return new int[] {av[1],-av[0]};
        }
        public static int[] Cross(int[] av, int[] bv) {
		    //cross(x,y) = z
            return new int[] {
                av[1]*bv[2]-av[2]*bv[1],
                av[2]*bv[0]-av[0]*bv[2],
                av[0]*bv[1]-av[1]*bv[0]
            };
        }
      	public static int[] Cross(int[] a,int[] b,int[] c) {
            //cross(x,y,z)=-w
            return new int[] {
                    a[1]*b[2]*c[3] + a[3]*b[1]*c[2] + a[2]*b[3]*c[1] -
                    a[3]*b[2]*c[1] - a[2]*b[1]*c[3] - a[1]*b[3]*c[2] ,
                    a[3]*b[2]*c[0] + a[2]*b[0]*c[3] + a[0]*b[3]*c[2] -
                    a[0]*b[2]*c[3] - a[3]*b[0]*c[2] - a[2]*b[3]*c[0] ,
                    a[0]*b[1]*c[3] + a[3]*b[0]*c[1] + a[1]*b[3]*c[0] -
                    a[3]*b[1]*c[0] - a[1]*b[0]*c[3] - a[0]*b[3]*c[1] ,
                    a[2]*b[1]*c[0] + a[1]*b[0]*c[2] + a[0]*b[2]*c[1] -
                    a[0]*b[1]*c[2] - a[2]*b[0]*c[1] - a[1]*b[2]*c[0]  
            };
        }


        // public static void Main(String[] args)
        // {

        //     //		int[][][] m = rotations(3);
        //     //		for (int i=0;i<m.Length;i++) {
        //     //			for (int ix=0;ix<m[i].Length;ix++) {
        //     //				for (int iv=0;iv<m[i].Length;iv++) {
        //     //					System.out.print(m[i][iv][ix]+"\t");
        //     //				}
        //     //				System.out.println();
        //     //			}
        //     //			System.out.println();
        //     //		}
        //     int[][] c1 = new[]
        //     {
        //         new int[]
        //         {
        //             0,
        //             0,
        //             0,
        //             0
        //         }
        //     };
        //     Console.WriteLine(RotationSymmetry(c1));
        //     Console.WriteLine(RotationSymmetry(Create4dTail(new DSignedAxis[] { new DSignedAxis(1), new DSignedAxis(2), new DSignedAxis(3), new DSignedAxis(4) }))); //		System.out.println(rotationSymmetry(Objectives.LEVEL0.goal));
        //     //		System.out.println(rotationSymmetry(Objectives.LEVEL1.goal));
        //     //		System.out.println(rotationSymmetry(Objectives.LEVEL2.goal));
        //     //		System.out.println(rotationSymmetry(Objectives.LEVEL4.goal));
        // }
    }
}