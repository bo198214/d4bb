using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace D4BB.Geometry
{
    public class Point {
        public double[] x;
        public Point(int dim) {
            x = new double[dim];
        }
        public Point(double[] _x) : this(_x.Length) {
            for (int i=0;i<_x.Length;i++) {
                x[i] = _x[i];
            }
        }
        public Point(double x, double y, double z, double w) : this(new double[] {x,y,z,w}) {}
        public Point(double x, double y, double z) : this(new double[] {x,y,z}) {}
        public Point(double x, double y) : this(new double[] {x,y}) {}
        
        public Point(int[] _x) : this(_x.Length) {
            for (int i=0;i<_x.Length;i++) {
                x[i] = _x[i];
            }
        }
        public Point(Point p) : this(p.x.Length) {
            for (int i=0;i<x.Length;i++) {
                x[i] = p.x[i];
            }
        }
        public Point clone() {
            return new Point(x);
        }
        public int dim() {
            return x.Length;
        }
        override public bool Equals(Object obj) {
            if (obj==null) { return false; }
            Point p = (Point) obj;
            if (x.Length!=p.x.Length) { return false; }
            return clone().subtract(p).len() < AOP.ERR;
    //		for (int i=0;i<x.length;i++) {
    //			if (Math.abs(x[i]-p.x[i])>Main.opt.ERR) { return false; }
    //		}
    //		return true;
        }

        public override int GetHashCode()
        {
            int res = 0;
            for (int i=0;i<x.Length;i++) {
                res += x[i].GetHashCode();
            }
            return res;
        }
        public override string ToString()
        {
            var res = "<";
            for (int i=0;i<x.Length;i++) {
                res += " " + x[i].ToString();
            }
            res += ">"; 
            return res;
        }
        /** returns the length of this */
        public double len() {
            double res = 0;
            for (int i=0;i<x.Length;i++) {
                res += x[i]*x[i];
            }
            return Math.Sqrt(res);
        }
        /** scalar product of this and b, no change of this */
        public double sc(Point b) {
            double res = 0;
            for (int d=0;d<x.Length;d++) {
                res += x[d]*b.x[d];
            }
            return res;
        }
        
        /** faster sc(b.clone().subtract(a)) */
        public double sc(Point a, Point b) {
            double res = 0;
            for (int d=0;d<x.Length;d++) {
                res += x[d]*(b.x[d]-a.x[d]);
            }
            return res;
        }
        
        /** subtracts b from this */
        public Point subtract(Point b) {
            for (int d=0;d<this.x.Length;d++) {
                x[d] -= b.x[d];
            }
            return this;		
        }
        public Point add(Point d) {
            for (int i=0;i<x.Length;i++) {
                x[i]+=d.x[i];
            }
            return this;
        }
	
        /** multiplies this by f */
        public Point multiply(double f) {
            for (int i=0;i<x.Length;i++) {
                x[i] *= f;
            }
            return this;
        }
        	/** adds d to this */
        /** Faster add(d.clone().multiply(dist))
        */
        public Point addby(Point d,double dist) {
            for (int i=0;i<d.x.Length;i++) {
                x[i]+=d.x[i]*dist;
            }
            return this;
        }
        public Point rotate(double ph,Point a, Point b) {
            Debug.Assert(AOP.isZero(a.sc(b)),"4017877697");
            Debug.Assert(AOP.eq(a.len(), 1));
            Debug.Assert(AOP.eq(b.len(), 1));
            var as_ = a.sc(this);
            var bs = b.sc(this);
            addby(a,-as_);
            addby(b,-bs);
            // newthis + as*a2 + bs*b2 = oldthis
            Point a2 = a.clone();
            Point b2 = b.clone();
            AOP.rotate(ph,a2,b2);
            add(a2.multiply(as_));
            add(b2.multiply(bs));
            return this;
        }
        /** angle from this to p!=0, 0&lt;al&lt;pi */
        public double arc(Point p) {
            return Math.Acos(sc(p)/(len()*p.len()));
        }
	

        /** rotates this by the rotation from vector a to vector b */
        public Point rotate(Point a, Point b) {
            double assertLength = len();
            double ph = a.arc(b); 
            if (AOP.eq(ph, 0)) return this;
            Point a0 = a.clone();
            Point b0 = b.clone();
            AOP.orthoNormalize(a0,b0);
            rotate(a.arc(b),a0,b0);
            Debug.Assert(AOP.eq(assertLength, len()));
            return this;
        }
        
        /** rotates this with center p from direction a to direction b */
        public Point rotate(double ph,Point a, Point b, Point p) {
            subtract(p);
            rotate(ph,a,b);
            add(p);
            return this;
        }

        /** returns the projection of Point p to this Point, this Point remains unmodified */ 
        public Point proj(Point p) {
            return clone().multiply(sc(p)/sc(this));
        }
        /* assume this is a vector of length 1 */
        public Point uproj(Point p) {
            return clone().multiply(sc(p));
        }

        /** returns 1 if the first non-zero coordinate is positive
        *  returns 0 if the this is 0
        *  return -1 otherwise (if the first non-zero coordinate is negative) 
        * */
        public int positivity() {
            for (int i=0;i<x.Length;i++) {
                if (-AOP.ERR < x[i] && x[i]<AOP.ERR) { continue; }
                if (x[i]>0) { return 1; }
                if (x[i]<0) { return -1; }
            }		
            return 0;
        }
        
        /** scales this Point to having length 1 */
        public Point normalize() {
            double l = len();
            if (l >= AOP.ERR) {
                for (int i=0;i<x.Length;i++) {
                    x[i] /= l;
                }
            }
            else {
                for (int i=0;i< x.Length;i++) {
                    x[i]=0;
                }
            }
            Debug.Assert(Math.Abs(len()-1) < AOP.ERR || len() < AOP.ERR,"2062111805");
            return this;
        }
        
        public bool isNormal() {
            return Math.Abs(len()-1) < AOP.ERR;
        }
        
        public String toString() {
            String res = "(";
            for (int i=0;i<x.Length;i++) {
                res += x[i] + ",";
            }
            res += ")";
            return res;
        }
        

        public int compareTo(Point o) {
            for (int i=0;i<x.Length;i++) {
                if (x[i]<o.x[i]) return -1;
                if (x[i]>o.x[i]) return 1;
            }
            return 0;
        }

    }
}