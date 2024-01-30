using D4BB.Geometry;

namespace D4BB.Transforms {
public class Camera3dOrthographic : ICamera3d
{
    public Point3d eye {get; set; } //absolute length of this vector doesn't matter
    public Point3d[] v;
    public Point3d viewNormal { get { return v[2]; } }
	private double clipDist = 0.3;
  	private double zoom3d = 1;
	protected readonly Point3d initialEye = (Point3d)new Point3d(1,1,-1).normalize();
	protected readonly Point3d[] initialV = new Point3d[] {new(1,0,0),new(0,1,0),new(0,0,1)};
    public Camera3dOrthographic() {
        eye = initialEye;
        v = initialV;
    }
    public Camera3dOrthographic(Point3d eye) {
        this.eye = eye;
        v = initialV;
    }
    public Point Proj2d(Point point3d)
    {
        var w = v[2].sc(point3d);
		var ew = -eye.x[2]; //this is typically positive
		if (w+ew<clipDist) return null;

        Point3d res = new Point3d();
		res.x[0] = v[0].sc(point3d)+ eye.x[0]/ew*w;
		res.x[1] = v[1].sc(point3d)+ eye.x[1]/ew*w;
        res.multiply(zoom3d);
        return res;
    }
	public bool IsFacedBy(Point origin, Point normal) {
		return normal.sc(eye) > 0;
	}
    public void SetPerspective(Point3d point) {
		eye = point;
	}

}
}