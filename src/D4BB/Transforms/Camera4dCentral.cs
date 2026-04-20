using D4BB.Geometry;

namespace D4BB.Transforms {
public interface ICamera4d {
    public Point4d eye {get;}
    public Point3d Proj3d(Point point4d);
    public bool IsFacedBy(Point origin, Point normal);
    public Point4d viewNormal {get;}
	public void SetPerspective(Point4d point);
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
}
}