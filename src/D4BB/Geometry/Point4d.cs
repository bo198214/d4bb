using System;
using System.Diagnostics;
using D4BB.Geometry;

namespace D4BB.Geometry 
{public class Point4d : Point {
	public Point4d() : base(4) {}
	
	public new Point4d clone() {
		Point4d res = new Point4d();
		for (int i=0;i<4;i++) {
			res.x[i] = x[i];
		}
		return res;
	}

	public Point4d(double _x1,double _x2,double _x3,double _x4) : this() {
		x[0]=_x1;x[1]=_x2;x[2]=_x3;x[3]=_x4;
	}
	
	public Point4d(int[] _x) : this() {
		Debug.Assert(x.Length == 4,"6106144986");
		for (int i=0;i<_x.Length;i++) {
			x[i] = _x[i];
		}
	}

	// public Point4d(DSignedAxis a) : base(4) {
	// 	for (int i=0;i<4;i++) x[i] = 0;
	// 	x[a.axis()]=a.pmSign();
	// }

	/** 
	 * TODO: this is not generic
	 * returns ph1,ph2,ph3 0<ph1<2pi, 0<ph2,ph3<pi */
	public Point3d getPolarArcs() {
		double ph3=Math.PI/2,ph2=Math.PI/2,ph1=Math.PI/2;
		ph3 -= AOP.D1000.arc(this);
		if ( sc(this) > AOP.ERR ) {
			ph2 -= AOP.D100.arc(this);
		}
		if (Math.Abs(x[2]) > AOP.ERR || Math.Abs(x[3]) > AOP.ERR ) {
			//atan2 angle from (0,1) clockwise, 
			ph1 = Math.Atan2(x[2],x[3]);
		}
		return new Point3d(ph1,ph2,ph3);
	}
	
	
	public Point4d[] polarRotate(double ph1, double ph2, double ph3) {
		Point4d[] v = new Point4d[4];
		for (int i=0;i<4;i++) {
			v[i] = AOP.unitVector4(i);
		}
		rotate(ph1,v[3],v[2]);
		AOP.rotate(ph1,v[3],v[2]); 
		rotate(ph2,v[3],v[1]);
		AOP.rotate(ph2,v[3],v[1]);
		rotate(ph3,v[3],v[0]);
		AOP.rotate(ph3,v[3],v[0]);
		return v;
	}
	
}}
