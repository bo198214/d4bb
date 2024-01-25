using System.Collections.Generic;
using System.Diagnostics;
using D4BB.Comb;
using D4BB.Geometry;

namespace D4BB.Transforms
{public abstract class Camera {
	public Point4d[] v;
	public abstract void rotate(double ph, Point a, Point b, Point c);
	public abstract void translate(Point axis,double d);
	
	
	/** k>=0 */
	private Point selectDirec(int k, bool cameraCoordinates) {
		if (cameraCoordinates) {
			return v[k].clone();
		}
		else {
			double[] x = new double[v.Length];
			for (int i=0;i<v.Length;i++) {
				if (i==k) x[i] = 1;
				else x[i] = 0;
			}
			return new Point(x);
		}
	}

	/** a1>0, a2>0 */
	public void rot(double ph, int a1, int a2, Point center, bool cameraCoordinates) {
		Debug.Assert(a1>0 && a2>0,"5878690340");
		//System.out.println("called rotate on " + this);
		rotate(ph,selectDirec(a1-1,cameraCoordinates),selectDirec(a2-1,cameraCoordinates),center);		
	}
	
	/** axis != 0 */
	public void trans(int axis, double d, bool cameraCoordinates) {
		Debug.Assert(axis != 0,"7722709650");
		if (axis>0) translate(selectDirec(axis-1,cameraCoordinates),d);
		if (axis<0) translate(selectDirec(-axis-1,cameraCoordinates),-d);
	}
}
public abstract class Camera4d : Camera {
	
	public static Point4d[] InitialV(Point4d viewDirIn, Point4d eyeInOut) {
		Point4d w = new Point4d(0,0,0,1);
		Point4d[] v = new Point4d[] { new(1,0,0,0), new(0,1,0,0), new(0,0,1,0), new(0,0,0,1) };
		eyeInOut.rotate(w, viewDirIn);
		for (int i=0;i<4;i++) {
			v[i].rotate(w, viewDirIn);
		}
		Debug.Assert(v[3].Equals(viewDirIn.clone().normalize()),"4562015293"); // : v[3] + "!=" + viewDirIn;
		return v;
	}
	public static Point4d[] InitialV(Point4d viewDirIn) {
		Point4d w = new Point4d(0,0,0,1);
		Point4d[] v = new Point4d[] { new(1,0,0,0), new(0,1,0,0), new(0,0,1,0), new(0,0,0,1) };
		for (int i=0;i<4;i++) {
			v[i].rotate(w, viewDirIn);
		}
		Debug.Assert(v[3].Equals(viewDirIn.clone().normalize()),"1231613220"); // : v[3] + "!=" + viewDirIn;
		return v;
	}

	public static Point4d defaultInitialEye() {
		return new Point4d(3,3,3,-2);
	}
	
	public static Point4d[] defaultInitialV() {
		return new Point4d[] {
				new Point4d(1,0,0,0),
				new Point4d(0,1,0,0),
				new Point4d(0,0,1,0),
				new Point4d(0,0,0,1)
		};
	}
	
//	public abstract void rotate(double ph, Point a4d, Point b4d, Point p4d);
	//public abstract void initAxes(double ph1,double ph2,double ph3);
//	public abstract void initAxes(DSignedAxis a);
	/** projects the point p to res via this camera 
	 * and returns whether the point p was in front of the camera */
	public abstract bool nproj3d(Point4d p,Point3d res);
	public abstract Point4d viewingDirection();
	public abstract bool facedBy(HalfSpace d);

	public Point4d eye;
	private double zoom3d = 1;//distance of 3d projection space from eye
	
	protected readonly Point4d initialEye;
	protected readonly Point4d[] initialV;
	private int orientation = +1; //+1 for left-handed  or -1 for right-handed
	
	protected Camera4d(Point4d initialEye) {
		this.v = defaultInitialV();
	}
	/** Specify initial camera v coordinate system with origin at eye. 
	 * v[3] is supposed to be the viewing direction in the left-handed case.
	 * Coordinate system must be orthonormal.
	 * */
	protected Camera4d(Point4d _initialEye,Point4d[] _initialV) {
		initialEye = _initialEye;
		initialV = _initialV;
		v = new Point4d[4];  //v[0],v[1],v[2] projection plane; v[3] viewing direction
		setToDefault(); //no changed() comes outside as long as initialization
	}
	
	/** Camera coordinate system is derived by rotating 
	 * the standard R4 basis and initial eye by (0,0,0,1) -> initialViewDir 
	 */
	protected Camera4d(Point4d _initialEye, Point4d initialViewDir) {
		initialEye = _initialEye;
		initialV = InitialV(initialViewDir,eye);
		Debug.Assert( v[0].Equals(initialViewDir),"6310536006");
		setToDefault();
	}
	
	private void initAxes() {
		for (int i=0;i<4;i++) {
			v[i] = initialV[i].clone();
		}
		eye = initialEye.clone();
	}
	
	// private void initAxes(DSignedAxis axis) {
	// 	initAxes();
	// 	Point w = AOP.unitVector4(3);
	// 	Point x = AOP.unitVector4(0);
	// 	Point a = new Point(axis);
	// 	if (axis.human()==-4) {
	// 		rotateAxes(w,x);
	// 		rotateAxes(x,a);
	// 		eye.rotate(w,x);
	// 		eye.rotate(x,a );
	// 	}
	// 	else {
	// 		rotateAxes(w,a);
	// 		eye.rotate(w,a);
	// 	}
	// }

	public void setZoom(double _zoom) {
		if (zoom3d!=_zoom) {
			zoom3d = _zoom;
		}
	}
	
	//sets cameras viewing direction given by the poloar coordinate arcs
	//aligns other directions with local tetrahedrals of the sphere
	public void initAxes(double ph1,double ph2,double ph3) {
		eye = initialEye.clone();
		v = eye.polarRotate(ph1, ph2, ph3);
	}

//	private void setViewArb0() {
//		eye=new Point(0,0,0,-zoom);
////		setDirec(Math.PI/4,0,-Math.PI/6);
//		setDirec(0,0,0);
//		eye.translate(new Point(3.5,2.7,1,-1));		
//	}
	
	public void rotate(Point a,Point b) {
		Debug.Assert(a.isNormal() && b.isNormal(),"6346406157");
		rotate(a.arc(b),a,b,new Point(0,0,0,0));
	}

	public override void  translate(Point a,double dist) {
		Debug.Assert( a.x.Length == 4,"0082518828");
		Debug.Assert( a.isNormal(),"2482626780");
		eye.addby(a,dist);
	}
	
	private void rotateAxes(Point a,Point b) {
		Debug.Assert( a.isNormal() && b.isNormal(),"3223408082");
		for (int i=0;i<4;i++) {
			v[i].rotate(a,b);
		}
		AOP.orthoNormalize(v); //avoid those tiny drifts
	}

	public override void rotate(double ph,Point a, Point b, Point c) {
		Debug.Assert( a.dim() == 4 ,"5389913849"); //: a.dim(,"");
		Debug.Assert( b.dim() == 4,"9450309344"); // : b.dim(,"");
		Debug.Assert( c.dim() == 4,"0778938652"); // : c.dim(,"");
		for (int i=0;i<4;i++) {
			v[i].rotate(ph, a,b);
		}
		eye.rotate(ph,a,b,c);
		AOP.orthoNormalize(v); //avoid those tiny drifts
	}

	public void setToDefault() {
		setOrientation(1);
		initAxes();
		zoom3d = 1;
	}

	public Point3d getDirec() {
		return viewingDirection().getPolarArcs();
	}
	
	public Point4d getEye() {
		return eye;
	}
	
	public void setEye(double x1, double x2, double x3, double x4) {
		eye = new Point4d(x1,x2,x3,x4);
	}

	// public void setDirec(DSignedAxis a) {
	// 	initAxes(a);		
	// }
	
	private void swapOrientation() {
		orientation *= -1;
		for (int i=0;i<4;i++) {
			v[i].x[3] *= -1;
		}
		eye.x[3] *= -1;
	}
	
	/** +1 for left handed, -1 for right handed */
	public void setOrientation(int _orientation) {
		if (orientation != _orientation) {
			swapOrientation();
		}
	}
	
	public int getOrientation() {
		return orientation;
	}

	
}
}
