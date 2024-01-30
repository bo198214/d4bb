using D4BB.Geometry;

namespace D4BB.Transforms {
public interface ICamera3d {
    public Point3d eye {get;}
    public Point Proj2d(Point point3d);
    public bool IsFacedBy(Point origin, Point normal);
    public Point3d viewNormal {get;}
	public void SetPerspective(Point3d point);
}
public class Camera3dCentral : ICamera3d {
    public Point3d eye {get; set; }
    public Point3d[] v;
	private double zoom3d = 1;//distance of 3d projection space from eye
	private double clipDist = 0.3;
	public Point3d viewNormal { get { return v[2]; } }
	protected readonly Point3d initialEye = new Point3d(3,3,-2);
	protected readonly Point3d[] initialV = new Point3d[] {new(1,0,0),new(0,1,0),new(0,0,1)};

	public Camera3dCentral(Point3d initialEye, Point3d[] initialV) {
        eye = initialEye;
        v = initialV;
    }
	public Camera3dCentral() {
        eye = initialEye;
        v = initialV;
	}
	public Camera3dCentral(Point3d eye) {
        this.eye = eye;
        v = initialV;
	}
	public void SetPerspective(Point3d eye) {
		this.eye = eye;
	}

	public bool facedBy(HalfSpace oc) {
		Point o = oc.origin().clone().subtract(eye);
		//			if (o.sc(viewingDirection()) < Param.ERR) return false;
		return oc.normal.sc(o,eye) >= AOP.ERR;			
	}

	/** Returns null if p4 is behind the camera */
	public Point Proj2d(Point point3d) {
        var w = v[2].sc(point3d);
		var ew = -eye.x[2]; //this is typically positive
		if (w+ew<clipDist) return null;

        Point res = new Point(2);
		var x0 = v[0].sc(point3d);
		var x1 = v[1].sc(point3d);
		res.x[0] = x0 + (eye.x[0]-x0)/(w+ew)*w;
		res.x[1] = x1 + (eye.x[1]-x1)/(w+ew)*w;
        res.multiply(zoom3d);
        return res;
	}
	public bool IsFacedBy(Point origin,Point normal) {
		return normal.sc(origin,eye) > 0;
	}
}
}