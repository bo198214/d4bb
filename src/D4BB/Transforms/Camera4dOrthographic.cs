using D4BB.Geometry;

namespace D4BB.Transforms {
public class Camera4dOrthographic : ICamera4d
{
    public Point4d eye {get; set; } //absolute length of this vector doesn't matter
    public Point4d[] v;
    public Point4d viewNormal { get { return v[3]; } }
	private double clipDist = 0.3;
  	private double zoom3d = 1;
	protected readonly Point4d initialEye = (Point4d)new Point4d(1,1,1,-1).normalize();
	protected readonly Point4d[] initialV = new Point4d[] {new(1,0,0,0),new(0,1,0,0),new(0,0,1,0),new(0,0,0,1)};
    public Camera4dOrthographic() {
        eye = initialEye;
        v = initialV;
   		SetPerspective(initialEye);
    }
    public Point3d Proj3d(Point point4d)
    {
        var w = v[3].sc(point4d);
		var ew = -eye.x[3]; //this is typically positive
		if (w+ew<clipDist) return null;

        Point3d res = new Point3d();
		res.x[0] = v[0].sc(point4d)+ eye.x[0]/ew*w;
		res.x[1] = v[1].sc(point4d)+ eye.x[1]/ew*w;
		res.x[2] = v[2].sc(point4d)+ eye.x[2]/ew*w;
        res.multiply(zoom3d);
        return res;
    }
	public bool IsFacedBy(Point origin, Point normal) {
		return normal.sc(eye) > 0;
	}
    public void SetPerspective(Point4d point) {
		eye = point;
	}

}
}