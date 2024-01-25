using System.Diagnostics;

namespace D4BB.Comb
{
    public class IntegerSignedAxis
    {
        public static readonly IntegerSignedAxis PD1 = new IntegerSignedAxis(1);
        public static readonly IntegerSignedAxis PD2 = new IntegerSignedAxis(2);
        public static readonly IntegerSignedAxis PD3 = new IntegerSignedAxis(3);
        public static readonly IntegerSignedAxis PD4 = new IntegerSignedAxis(4);
        public static readonly IntegerSignedAxis MD1 = new IntegerSignedAxis(-1);
        public static readonly IntegerSignedAxis MD2 = new IntegerSignedAxis(-2);
        public static readonly IntegerSignedAxis MD3 = new IntegerSignedAxis(-3);
        public static readonly IntegerSignedAxis MD4 = new IntegerSignedAxis(-4);
        bool pos;
        internal int axis; // -1 means diffuse direction
        /// <summary>
        /// 1,2,3,4,... are the positive axes
        /// -1,-2,-3,-4,... are the negative axes
        /// 0 is not allowed as value
        /// </summary>
        public IntegerSignedAxis(int i)
        {
            if (i == 0)
            {
                pos = true;
                axis = -1;
            }

            if (i > 0)
            {
                pos = true;
                axis = i - 1;
            }

            if (i < 0)
            {
                pos = false;
                axis = -i - 1;
            }
        }

        /// <summary>
        /// zoSign==1 indicates a positive axis, zoSign==0 negative
        /// _axis is one of 0,1,2,3,4,...
        /// </summary>
        public IntegerSignedAxis(int zoSign, int _axis)
        {
            Debug.Assert(zoSign == 0 || zoSign == 1, "7454560982");
            Debug.Assert(_axis >= -1, "2023201661");
            if (zoSign == 0)
            {
                pos = false;
            }

            if (zoSign == 1)
            {
                pos = true;
            }

            axis = _axis;
        }

        /// <summary>
        /// _pos indicates whether the axis is positive
        /// _axis is one of 0,1,2,3,4, ...
        /// </summary>
        /// <param name="_axis"></param>
        public IntegerSignedAxis(bool _pos, int _axis)
        {
            pos = _pos;
            axis = _axis;
        }

        /// <summary>
        /// Makes a negative axis from a positive one and vice versa.
        /// </summary>
        public virtual void Invert()
        {
            pos = !pos;
        }

        /// <summary>
        /// Returns the axis as a number 0,1,2,...
        /// </summary>
        public virtual int Axis()
        {
            return axis;
        }

        /// <summary>
        /// returns 1 for a positive axis and -1 for a negative axis
        /// </summary>
        public virtual int PmSign()
        {
            if (pos)
            {
                return +1;
            }

            return -1;
        }

        /* returns 1 for a positive axis and 0 for a negative axis */
        public virtual int ZoSign()
        {
            if (pos)
            {
                return 1;
            }

            return 0;
        }

        /// <summary>
        /// returns the axis as values 1,2,3,... or -1,-2,-3, ...
        /// </summary>
        public virtual int Human()
        {
            if (pos)
            {
                return axis + 1;
            }

            return -(axis + 1);
        }

        public static bool Equal(IntegerSignedAxis v, IntegerSignedAxis w)
        {
            return (v.pos == w.pos) && (v.axis == w.axis);
        }

        override public bool Equals(object a)
        {
            if (a==null) { return false; }
            return Equal(this, (IntegerSignedAxis)a);
        }
        public override int GetHashCode()
        {
            return pos.GetHashCode() + axis.GetHashCode();
        }
        /// <summary>
        /// returns the unit vector of this axis in dim dimensions
        /// </summary>
        public virtual int[] UnitVector(int dim)
        {
            int[] res = new int[dim];
            for (int i = 0; i < dim; i++)
            {
                res[i] = 0;
            }

            if (pos)
            {
                res[axis] = 1;
            }
            else
            {
                res[axis] = -1;
            }

            return res;
        }

        /// <summary>
        /// returns this axis as Point
        /// </summary>
        public virtual double[] Direc(int dim)
        {
            double[] x = new double[dim];
            for (int i = 0; i < dim; i++)
            {
                x[i] = 0;
            }

            if (pos)
            {
                x[axis] = 1;
            }
            else
            {
                x[axis] = -1;
            }

            return x;
        }
    }
}