namespace D4BB.Geometry {
public interface ICamera4d {
    public Point4d eye {get;}
    public Point3d Proj3d(Point point4d);
    public bool IsFacedBy(Point origin, Point normal);
    public Point4d viewNormal {get;}
    // Rotates the camera basis v[0..3] in the plane spanned by orthonormal
    // a, b by angle ph, and rotates eye around center c in the same plane.
    // Equivalent to rotating the world by -ph around c — useful as an O(1)
    // alternative to rotating every vertex per frame.
    void rotate(double ph, Point a, Point b, Point c);
}
public class Camera4dCentral : ICamera4d {
    public Point4d eye {get; set; }
    public Point4d[] v;
	private double zoom3d = 1;//distance of 3d projection space from eye
	private double clipDist = 0.3;
	public Point4d viewNormal { get { return v[3]; } }
	protected readonly Point4d initialEye = new Point4d(3,3,3,-2);
	protected readonly Point4d[] initialV = new Point4d[] {new(1,0,0,0),new(0,1,0,0),new(0,0,1,0),new(0,0,0,1)};

	public Camera4dCentral(Point4d initialEye, Point4d[] initialV) {
        eye = initialEye;
        v = initialV;
    }
	public Camera4dCentral() {
        eye = initialEye;
        v = initialV;
	}
	public Camera4dCentral(Point4d eye) {
        this.eye = eye;
        v = initialV;
	}
	public void SetPerspective(Point4d eye) {
		this.eye = eye;
	}
	// Builds an orthonormal 4D basis whose v[3] points along viewDirIn. Method:
	// rotate the standard basis by the rotation that takes (0,0,0,1) to
	// viewDirIn. The two-arg overload also transports eyeInOut by the same
	// rotation (in-place), so the eye position stays consistent with the new
	// viewing direction.
	public static Point4d[] InitialV(Point4d viewDirIn) {
		var w = new Point4d(0, 0, 0, 1);
		var v = new Point4d[] {
			new(1, 0, 0, 0), new(0, 1, 0, 0), new(0, 0, 1, 0), new(0, 0, 0, 1),
		};
		for (int i = 0; i < 4; i++) v[i].rotate(w, viewDirIn);
		return v;
	}
	public static Point4d[] InitialV(Point4d viewDirIn, Point4d eyeInOut) {
		var w = new Point4d(0, 0, 0, 1);
		eyeInOut.rotate(w, viewDirIn);
		return InitialV(viewDirIn);
	}

	public bool facedBy(HalfSpace oc) {
		Point o = oc.origin().clone().subtract(eye);
		//			if (o.sc(viewingDirection()) < Param.ERR) return false;
		return oc.normal.sc(o,eye) >= AOP.ERR;			
	}

	/** Returns null if p4 is behind the camera */
	public Point3d Proj3d(Point point4d) {
        var w = v[3].sc(point4d);
		var ew = -eye.x[3]; //this is typically positive
		if (w+ew<clipDist) return null;

        Point3d res = new Point3d();
		var x0 = v[0].sc(point4d);
		var x1 = v[1].sc(point4d);
		var x2 = v[2].sc(point4d);
		res.x[0] = x0 + (eye.x[0]-x0)/(w+ew)*w;
		res.x[1] = x1 + (eye.x[1]-x1)/(w+ew)*w;
		res.x[2] = x2 + (eye.x[2]-x2)/(w+ew)*w;
        res.multiply(zoom3d);
        return res;
	}
	public bool IsFacedBy(Point origin,Point normal) {
		return normal.sc(origin,eye) > 0;
	}
	public void rotate(double ph, Point a, Point b, Point c) {
		for (int i = 0; i < 4; i++) v[i].rotate(ph, a, b);
		eye.rotate(ph, a, b, c);
		AOP.orthoNormalize(v);
	}
}
}