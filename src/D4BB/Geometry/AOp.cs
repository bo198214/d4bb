using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using D4BB.Comb;

namespace D4BB.Geometry {
public class AOP {

	readonly public static double deg = Math.PI*2/360;

	readonly private static Point4d[] UNITVECTOR4 = new Point4d[] {
		new Point4d(1,0,0,0),
		new Point4d(0,1,0,0),
		new Point4d(0,0,1,0),
		new Point4d(0,0,0,1)
	};
	readonly private static Point3d[] UNITVECTOR3 = new Point3d[] {
		new Point3d(1,0,0),
		new Point3d(0,1,0),
		new Point3d(0,0,1)
	};
	
	/* Computes the angle (given in degree) in the range 0<= .. <360.
	 *  Not fast.
	 */
	public static double angle0360(double ph) {
		if (ph<0) { return angle0360(ph+360); }
		if (ph>=360) { return angle0360(ph-360); }
		return ph;
	}
	/* Computes the angle (given in rad) into the range 0<= .. < 2*pi.
	 * Not fast.
	 */
	public static double angle02pi(double ph) {
		double pi2 = Math.PI*2;
		if (ph<0) { return angle02pi(ph+pi2); }
		if (ph>=pi2) { return angle02pi(ph-pi2); }
		return ph;
	}
	
	public static Point4d unitVector4(int i) {
		Debug.Assert(0<=i && i<4, "3179754808");
		return UNITVECTOR4[i].clone();
	}
	
	public static Point3d unitVector3(int i) {
		Debug.Assert(0<=i && i<3, "5379243249");
		return UNITVECTOR3[i].clone();
	}

//	/**  
//	 * Given 2 orthogonal directions a and b,
//	 * rotates a towards b by the angle ph
//	 * rotates b by the same rotation
//	 *
//	 * @param ph rotation angle.
//	 * @param a and b determine rotation plane and direction
//	 * @return rotated a and b
//	*/
//	public static void rotate(double ph,Point4d a,Point4d b) {
//		double ax1,ax2,ax3,ax4,bx1,bx2,bx3,bx4;
//		ax1=a.x[0]; ax2=a.x[1]; ax3=a.x[2]; ax4=a.x[3];
//		bx1=b.x[0]; bx2=b.x[1]; bx3=b.x[2]; bx4=b.x[3];
//		double co = Math.cos(ph);
//		double si = Math.sin(ph);
//		a.x[0] = co*ax1+si*bx1;
//		a.x[1] = co*ax2+si*bx2;
//		a.x[2] = co*ax3+si*bx3;
//		a.x[3] = co*ax4+si*bx4;
//		b.x[0] = co*bx1-si*ax1;
//		b.x[1] = co*bx2-si*ax2;
//		b.x[2] = co*bx3-si*ax3;
//		b.x[3] = co*bx4-si*ax4;
//	}
//	
	/**  
	 * Given 2 orthogonal directions a and b,
	 * rotates a towards b by the angle ph
	 * rotates b by the same rotation
	 *
	 * @param ph rotation angle.
	 * @param a and b determine rotation plane and direction
	 * @return rotated a and b
	*/
	public static void rotate(double ph,Point a,Point b) {
		Debug.Assert(Math.Abs(a.sc(b)) < AOP.ERR, "4305288632");
		double co = Math.Cos(ph);
		double si = Math.Sin(ph);
		Point a0, b0;
		a0 = a.clone();
		b0 = b.clone();
		for (int i=0;i<a.x.Length;i++) {
 			a.x[i] = co*a0.x[i]+si*b0.x[i];
			b.x[i] = co*b0.x[i]-si*a0.x[i];
		}
	}
	
	public static bool isOrthoNormal(Point[] v) {
		int n = v.Length;
		for (int i=0;i<n;i++) {
			for (int j=i+1;j<n;j++) {
				if (!isZero(v[i].sc(v[j]))) return false;
			}
		}
		for (int i=0;i<n;i++) {
			if (!eq(v[i].len(),1)) return false;
		}
		return true;
	}
	public static void orthogonalize(Point v, Point w) {
		w.subtract(v.proj(w));
	}
	
	public static void orthoNormalize(Point v, Point w) {
		orthogonalize(v, w);
		v.normalize();
		w.normalize();
	}
	
	/* subtracts the projection of v to orthBase from v (modifies v)
	 * this results in a vector that is orthogonal to orthBase (if v is contained in the span of orthBase the result is 0)
	 * all vectors in orthBase needs to be orthogonal to each other (but not necessarily of length 1)
	 */
	public static Point orth(Point v,Point[] orthBase) {
		for (int i=0;i<orthBase.Length;i++) {
			v.subtract(orthBase[i].proj(v));
		}
		return v;
	}
	public static void orthogonalize(Point[] v) {
		for (int n=0;n<v.Length;n++) {
			Point p = v[n].clone();
			for (int i=0;i<n;i++) {
				v[n].subtract(v[i].proj(p));
			}
			for (int i=0;i<n;i++) {
				Debug.Assert(Math.Abs(v[i].sc(v[n])) < AOP.ERR, "0682826603");
			}
		}
	}
	
	public static void orthoNormalize(Point[] v) {
		for (int n=0;n<v.Length;n++) {
			Point p = v[n].clone();
			for (int i=0;i<n;i++) {
				v[n].subtract(v[i].uproj(p));
			}
			v[n].multiply(1.0/v[n].len());
			for (int i=0;i<n;i++) {
				Debug.Assert(Math.Abs(v[i].sc(v[n])) < AOP.ERR, "0512882765");
			}
		}
		Debug.Assert(isOrthoNormal(v), "4224031881");
	}
	public static Point[] Points2Vectors(Point[] points) {
		var res = new Point[points.Length-1];
		for (int i=0;i<res.Length;i++) {
			res[i]=points[i+1].clone().subtract(points[0]);
		}
		return res;
	}
	/* goes through the points and copies over each point that is not colinear with the previous points */
	public static List<Point> SpanningPoints(List<Point> points) {
		var baseVectors = new List<Point>();
		var res = new List<Point>{points[0]};
		if (points.Count==1) {
			return res;
		}
		var v0 = points[1].clone().subtract(points[0]);
		baseVectors.Add(v0.multiply(1.0/v0.len()));
		 res.Add(points[1]);
		Point p0=null;
		foreach (var point in points) {
			var p = point.clone();
			if (p0==null) {
				p0=point;
				continue;
			}
			var v = p.subtract(p0);
			if (v0==null) {
				v0=v.multiply(1.0/v.len());
				continue;
			}
			foreach (var bv in baseVectors) {
				v.subtract(bv.uproj(p));
			}
			var d = v.len();
			if (!AOP.eq(d,0)) {
				baseVectors.Add(v.multiply(1.0/d));
				res.Add(point);

				for (int i=0;i<baseVectors.Count-1;i++) {
					Debug.Assert(Math.Abs(baseVectors[i].sc(v)) < AOP.ERR, "1221326622");
				}
			}
		}
		return res;
	}
	/* calculates the dimension spanned by the given points */
	public static int SpanningDim(List<Point> points) {
		return SpanningPoints(points).Count-1;
	}
	public static bool Colinear(List<Point> points) {
		return AOP.SpanningDim(points)<points.Count-1;
	}
	// calculates the distance of the probe from the line spanned by a and b
	public static bool Colinear1d(Point a, Point b, Point probe) {
		return Colinear(new List<Point>(){a,b,probe});
	}
	// calculates the distance of the probe from the plane defined by a,b and c
	public static bool Colinear2d(Point a, Point b, Point c, Point probe) {
		return Colinear(new List<Point>(){a,b,c,probe});
	}
	// calculates the distance of the probe from the 3d-Space defined by a,b,c and d
	// for 3d points this is always 0
	// makes sense in higher than 3d-spaces
	public static bool Colinear3d(Point a, Point b, Point c, Point d, Point probe) {
		return Colinear(new List<Point>(){a,b,c,d,probe});
	}
	

//	/** @see #rotate(double, Point4d, Point4d) */
//	public static void rotate(double ph,Point3d a,Point3d b) {
//		double ax1,ax2,ax3,bx1,bx2,bx3;
//		ax1=a.x[0]; ax2=a.x[1]; ax3=a.x[2];
//		bx1=b.x[0]; bx2=b.x[1]; bx3=b.x[2];
//		double co = Math.cos(ph);
//		double si = Math.sin(ph);
//		a.x[0] = co*ax1+si*bx1;
//		a.x[1] = co*ax2+si*bx2;
//		a.x[2] = co*ax3+si*bx3;
//		b.x[0] = co*bx1-si*ax1;
//		b.x[1] = co*bx2-si*ax2;
//		b.x[2] = co*bx3-si*ax3;
//	}
		
	public static Point cross(Point a3d,Point b3d) {
		//x cross y = z, y cross z = x, z cross y = x
		return new Point(
				a3d.x[1]*b3d.x[2]-a3d.x[2]*b3d.x[1],
				a3d.x[2]*b3d.x[0]-a3d.x[0]*b3d.x[2],
				a3d.x[0]*b3d.x[1]-a3d.x[1]*b3d.x[0]);
	}
	
	public static double[] Cross(double[][] vs) {
		var n = vs.Length+1;
		Debug.Assert(n==vs[0].Length);
		double[] res = new double[n];
		foreach (var permutation in Permutation.Permutations(n)) {
			var i = permutation[0];
			double mul = Permutation.Int(permutation.Parity());
			for (int k=0;k<n-1;k++) {
				mul *= vs[k][permutation[k+1]];
			}
			res[i] += mul;
		}
		return res;
	}

	static public readonly Point4d D1000 = UNITVECTOR4[0];
	static public readonly Point3d D100 = UNITVECTOR3[0];

	public const int binaryPrecision = 15; 
	public  const double ERR = 0.00001;
	public static bool isZero(double x, double err=ERR) {
		return Math.Abs(x) < err;
	}
	public static bool eq(double x, double y, double err=ERR) {
		return isZero(x-y,err);
	}

	public static bool lt(double x, double y, double err=ERR) {
		return x <= y - err;
	}
	public static bool gt(double x, double y, double err=ERR) {
		return x >= y + err;
	}

	public static bool le(double x, double y, double err=ERR) {
		return x < y + err;
	}
	public static bool ge(double x, double y, double err=ERR) {
		return x > y - err;
	}
	// calculates the distance of the last point from the subspace spanned by the first n-1 points
//	public static boolean inConvex(Point p,Collection vertices) {
//		Iterator i = vertices.iterator();
//		double sum = 0;
//		while (i.hasNext()) {
//			double param = ((Point)i.next()).sc(p);
//			if (param < 0 || param > 1) { return false; }
//			sum += param;
//		}
//		return Math.abs(sum-1)<Opt.ERR;
//	}
//	public boolean inConvex(Point3d p,Point3d[] vertices) {
//		
//	}
	
    public static Point[] Inset2dCounterClockwise(List<Point> points, double delta) {
        var n = points.Count;
        Point[][] od = new Point[n][];

        for (int i=0;i<n;i++) {
            var p = points[i];
            var q = points[(i+1)%n];
            var a = q.clone().subtract(p);
            var pa = p.clone().addby(new Point(-a.x[1],a.x[0]),delta/Math.Sqrt(a.sc(a))); //orthogonally the inset point
            od[i] = new Point[]{pa,a};
        }
        Point[] res = new Point[n];
        for (int i=0;i<n;i++) {
            var pa = od[i][0];
            var a = od[i][1];
            var qb = od[(i+1)%n][0];
            var b = od[(i+1)%n][1];
			var denom = a.x[0]*b.x[1]-a.x[1]*b.x[0];
			if (AOP.eq(denom,0)) { // a and b have same direction
				res[(i+1)%n] = pa.add(a);
			}
			else {
				var nom = (qb.x[0]*b.x[1]-qb.x[1]*b.x[0]) - (pa.x[0]*b.x[1]-pa.x[1]*b.x[0]);
				var fa = nom / denom;
	            res[(i+1)%n] = pa.add(a.multiply(fa));
			}
        }
        return res;
    }
    public static Point[] Inset2dClockwise(List<Point> points, double delta) => Inset2dCounterClockwise(points,delta);
    public static Point[] Inset3d(List<Point> points, double delta) {
        var n = points.Count;
        var o = points[0];
        var nx = points[1].clone().subtract(o);
        var ny = points[^1].clone().subtract(o);
        AOP.orthoNormalize(nx,ny);
        
    	Point[] points2d = new Point[n];
		for (int i=0;i<n;i++) {
			var d = points[i].clone().subtract(o);
			points2d[i] = new Point(d.sc(nx),d.sc(ny));
		}
		var insetPoints = Inset2dCounterClockwise(new List<Point>(points2d),delta);
        return insetPoints.Select(p=>o.clone().add(
			nx.clone().multiply(p.x[0]).add(ny.clone().multiply(p.x[1])
		))).ToArray();
    }

	/* the parameters p such that p[0]*xv + p[1]*yv == pv */
	public static double[] Params2d(double[] xv, double[] yv, double[] pv) {
        var a = xv[0];
        var c = xv[1];
        var b = yv[0];
        var d = yv[1];
        var e = a*d-b*c;


        var x = ( pv[0]*d-pv[1]*b)/e;
        var y = (-pv[0]*c+pv[1]*a)/e;
		return new double[]{x,y};
	}
	/* the parameters p such that p[0]*xv + p[1]*yv == pv */
	public static double[] Params(Point xv, Point yv, Point pv) {
        var x = xv.clone();
        var y = yv.clone();
        AOP.orthoNormalize(x,y);
        var pvx = pv.sc(x);
        var pvy = pv.sc(y);
        var xvx = xv.sc(x);
        var xvy = xv.sc(y);
        Debug.Assert(AOP.eq(xvy,0),"6034210141");
        Debug.Assert(AOP.eq(xvx,xv.len()),"9525793985");
        var yvx = yv.sc(x);
        var yvy = yv.sc(y);
		var b = pvy/yvy;
		var a = (pvx-b*yvx)/xvx;
        return new double[]{a, b};
	}
}

}