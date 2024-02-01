using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace D4BB.Geometry
{
	// public class CutSpace : HalfSpace {
	// 	public static Factory<CutSpace> cutspaceFactory = new();
	// 	protected CutSpace(Point _normal, double _length) :
	// 	        base(_normal,_length) {}
		
	// 	protected CutSpace(Point orig, Point _normal) :
	// 	        base(orig,_normal) {}
	// 	public override bool FactoryEquals(Object obj) {
	// 		//existingElement.GetType().Equals(newElement.GetType()) && 
	// 		CutSpace ss2 = (CutSpace) obj;
	// 		return AOP.eq(normal.sc(ss2.normal),1) || AOP.eq(normal.sc(ss2.normal),-1);
	// 	}
	// 	public new static CutSpace New(Point _normal, double _length) {
	// 		var res = new CutSpace(_normal, _length);
	// 		return cutspaceFactory.AddIfNotContained(res);
	// 	}
	// 	public new static CutSpace New(Point orig, Point _normal) {
	// 		var res = new CutSpace(orig, _normal);
	// 		return cutspaceFactory.AddIfNotContained(res);
	// 	}
    // }
	public class HalfSpace {
		/* 
		the half space is uniquely identified by the normal and the distance to the (0,0,0).
		*/
		public readonly Point normal;
		public double length; // length from (0,0,0) to plane along the normal, can be negative
		// parent is 3d space 
		public readonly static int INSIDE = -1;
		public readonly static int CONTAINED = 0;
		public readonly static int OUTSIDE = 1;
		protected HalfSpace() {}
		public HalfSpace(double _length, Point _normal) {
			normal = _normal;
			length = _length;
		}
		public HalfSpace(Point orig, Point _normal) {
			Debug.Assert(AOP.eq(_normal.len(),1),"0416830688");
			normal = _normal;
			length = normal.sc(orig);
		}
		/* creates Halfspace in 3D space, that contains points a,b,c so that probe is inside */
		public static HalfSpace From3PointsProbeInside(Point a, Point b, Point c, Point probe) {
			var res = new HalfSpace(a,AOP.cross(b.clone().subtract(a),c.clone().subtract(a)));
			if (res.side(probe)==HalfSpace.INSIDE) {
				return res;
			}
			if (res.side(probe)==HalfSpace.OUTSIDE) {
				return res.flip();
			}
			throw new Exception();
		}

		public override bool Equals(object obj) {
			HalfSpace hs2 = (HalfSpace) obj;
			return AOP.eq(normal.clone().subtract(hs2.normal).len(),0) &&
					AOP.eq(length,hs2.length);
		}
        public override int GetHashCode()
        {
			return normal.GetHashCode() + length.GetHashCode();
        }
        public override string ToString()
        {
            return "("+length+", "+normal+")";
        }

        //	/** s is on the inner side of the plane spanned by a,b,c */
        //	public Space2d init(Point3d a, Point3d b, Point3d c) {
        //		normal = Gop.X((Point3d)b.minus(a).normalize(), (Point3d)c.minus(a).normalize());
        //		length = a.sc(normal);
        //		return this;
        //	}


        // public HalfSpace(List<Edge> edges) {
        // 	Debug.Assert(edges.Count() >= 2,"9618170778");
        // 	int i=0;
        // 	Point[] d= new Point[2];
        // 	Point a = null;
        // 	foreach (Edge edge in edges) {
        // 		Point f1a,f1b;
        //         Polyhedron[] va = edge.facets.ToArray();

        // 		f1a = ((Point) va[0]).clone();
        // 		f1b = ((Point) va[1]).clone();
        // 		if (i>=2) { break; }
        // 		if (i==0) { a = f1a; }
        // 		d[i] = f1b.subtract(f1a).normalize();
        // 		i++;
        // 	}
        // 	//TODO normal = Gop.X(d[0],d[1]); //for spacedim 3d
        // 	normal.multiply(normal.positivity());
        // 	length = a.sc(normal);
        // }

        public HalfSpace flip() {
		return new HalfSpace(length*(-1),normal.clone().multiply(-1));
	}
	public Point origin() {
		return normal.clone().multiply(length);
	}
	public Point proj(Point p) {
		return p.clone().subtract(normal.proj(p.clone().subtract(origin())));
	}
	
	public double dist(Point p) {
		return  p.clone().subtract(proj(p)).len();
	}
	
	/*
	 *  1 outer
	 *  0 on
	 * -1 inner
	 */
	public int side(Point p) {
		double proj = normal.sc(p.clone().subtract(origin())); 
		if ( proj >=  AOP.ERR ) { return  1; }
		if ( proj <= -AOP.ERR ) { return -1; }
		return 0;
	}

	/* the point where this halfspace cuts the line spanned by a and b */
	public Point cutPoint(Point a, Point b) {
		double ad = dist(a);
		double bd = dist(b);
		return proj(a).multiply(bd/(ad+bd)).add(proj(b).multiply(ad/(ad+bd)));
	}

}
public class HalfSpaceEqualityComparer : IEqualityComparer<HalfSpace>
{
	public readonly int binaryPrecision;
	public readonly double err;
	public HalfSpaceEqualityComparer(int binaryPrecision) {
		this.binaryPrecision = binaryPrecision;
		this.err = Math.Pow(10,-binaryPrecision);
	}
    public bool Equals(HalfSpace a, HalfSpace b)
    {
        return AOP.eq(a.normal.clone().subtract(b.normal).len(),0,err) &&
					AOP.eq(a.length,b.length,err);
    }

    public int GetHashCode(HalfSpace obj)
    {
        
		int res = Precision.TruncateBinary(obj.length,binaryPrecision).GetHashCode();
		foreach (double x in obj.normal.x) {
			res +=Precision.TruncateBinary(x,binaryPrecision).GetHashCode();
		}
		return res;
    }
}    
}