using System.Collections.Generic;
using System.Linq;
using D4BB.Geometry;
using D4BB.Comb;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System;

namespace D4BB.Transforms
{
public class RawVertexEquality : IEqualityComparer<Vertex>
{
    public bool Equals(Vertex a, Vertex b)
    {
        return a == b;
    }

    public int GetHashCode(Vertex obj)
    {
        return RuntimeHelpers.GetHashCode(obj);
    }
}    
    
public class FacetsGenericMesh {
    public List<double[]> vertices;
    public List<ushort> triangles;
    public List<double[]> uvs;
    public List<double[]> normals;
    public List<Face2dWithIntegerCellAttribute> debugPolyTriangles;

    public double[] CenterPoint() {
        double[] center = null;
        foreach (var vertex in vertices) {
            if (center==null) {
                center = vertex;
                continue;
            }
            for (int i=0;i<vertex.Length;i++) center[i]+=vertex[i];
        }
        for (int i=0;i<center.Length;i++) {
            center[i] /= vertices.Count;
        }
        return center;
    }
    public void Center() {
        double[] centerPoint = CenterPoint();
        foreach (var vertex in vertices) {
            for (int i=0;i<vertex.Length;i++) vertex[i] -= centerPoint[i];
        } 
    }

    public static List<double[]> UV4facetList(List<Point> facetBoundary, Point center) {
        var minX = double.MaxValue;
        var minY = double.MaxValue;
        var maxX = double.MinValue;
        var maxY = double.MinValue;
        Point unit1 = facetBoundary[ 1].clone().subtract(facetBoundary[0]);
        Point unit2 = facetBoundary[^1].clone().subtract(facetBoundary[0]);
        AOP.orthoNormalize(unit1,unit2);
        List<double[]> res = new();
        for (int i=0;i<facetBoundary.Count;i++) {
            Point vertex = facetBoundary[i];
            var x = unit1.sc(vertex);
            var y = unit2.sc(vertex);
            if (x<minX) minX=x;
            if (x>maxX) maxX=x;
            if (y<minY) minY=y;
            if (y>maxY) maxY=y;
            res.Add(new double[]{x,y});
        }
        var distX=maxX-minX;
        var distY=maxY-minY;
        for (int i=0;i<facetBoundary.Count;i++) {
            var x = res[i][0];
            var y = res[i][1];
            res[i] = new double[]{(x-minX)/distX,(y-minY)/distY};
        }
        return res;            
    }
    public static double[] UVFromIntegerFace(Point point, IntegerCell integerFace, double[] normal) {
        Debug.Assert(integerFace.Dim()==2,"1387012337");
        var res = new double[2];
        
        var spanList = new int[2];
        var n = new double[3];
        if (integerFace.span.Contains(1)) { //y coordinate should point upwards (be y in uv)
            if (integerFace.span.Contains(0)) {
                spanList[0]=0;
                spanList[1]=1;
                n[2]=-1; //x cross y = -z
            } else if (integerFace.span.Contains(2)) {
                spanList[0]=2;
                spanList[1]=1;
                n[0]=1; // z cross y = x
            }
        } else { //contains x and z, z coordinate should point upward (be y in uv)
            spanList[0]=0;
            spanList[1]=2;
            n[1]=1;  // x cross z = y
        }

        double orientation = n[0]*normal[0]+n[1]*normal[1]+n[2]*normal[2];
        for (int i=0;i<2;i++) {
            res[i] = point.x[spanList[i]]-integerFace.origin[spanList[i]];
            if (spanList[i]!=spanList[1] && orientation < 0) {
                res[i] = 1 - res[i];
            }
        }
        return res;
    }

    public static double[] UV(Point point, IntegerCell integerFace, ICamera4d cam) {
        Debug.Assert(integerFace.Dim()==2,"7762149526");
        var spanList = new int[2];
        if (integerFace.span.Contains(1)) { //y coordinate should point upwards (be y in uv)
            spanList[0]=integerFace.span.Except(new HashSet<int>{1}).First();
            spanList[1]=1;
        } else { //contains x and z, z coordinate should point upward (be y in uv)
            spanList=integerFace.span.ToArray();
            Array.Sort(spanList);
        }

        var oi = new Point4d(integerFace.origin);
        var xi = new Point4d(integerFace.origin);
        xi.x[spanList[0]] += 1;
        var yi = new Point4d(integerFace.origin);
        yi.x[spanList[1]] += 1;

        var o  = cam.Proj3d(oi);
        var xv = cam.Proj3d(xi).subtract(o);
        var yv = cam.Proj3d(yi).subtract(o);
        var pv = point.clone().subtract(o);
        return AOP.Params(xv,yv,pv);
    }

    public FacetsGenericMesh(ICollection<IPolyhedron> faces2d, double inset = 0,
            bool withVertexNormals=true, 
            bool withVertexUVs=true,
            bool duplicateVertices=true,
            bool withCenter=false) {

         Dictionary<Vertex, ushort> vertexNumbers;
         if (duplicateVertices) {
            vertexNumbers = new(new RawVertexEquality());
         } else {
            vertexNumbers = new();
         }

        vertices = new List<double[]>();
        if (faces2d!=null)     triangles = new();
        if (withVertexUVs)     uvs        = new();
        if (withVertexNormals) normals   = new();

        List<Face2dWithIntegerCellAttribute> polyTriangles = new();
        debugPolyTriangles = polyTriangles;

        foreach (var pFacet in faces2d) {
            // var insetPoints = AOP.Inset3d(((Face2d)pFacet).points,0.01);
            // var facet = new Face2dBC(new List<Point>(insetPoints), ((Face2d)pFacet).isInvisible,((Face2dWithIntegerCellAttribute)pFacet).integerCell);

            Face2dWithIntegerCellAttribute facet;
            if (inset>0) {
                facet = new Face2dBC(
                    ((Face2d)pFacet).points,
                    ((Face2d)pFacet).isInvisible,
                    ((Face2dWithIntegerCellAttribute)pFacet).integerCell) { camera = ((Face2dBC)pFacet).camera };
                    facet.Inset(inset);
            } else {
                facet = (Face2dWithIntegerCellAttribute)pFacet;
            }
            Debug.Assert(facet.Dim()==2,"2503750714");

            List<Face2dWithIntegerCellAttribute> pts;
            if (withCenter) {
                var facetCenter = facet.CenterPoint();
                pts = facet.CenterTriangulation2d(facetCenter).Cast<Face2dWithIntegerCellAttribute>().ToList();
            } else {
                pts = facet.BoundaryTriangulation2d().Cast<Face2dWithIntegerCellAttribute>().ToList();
            }
            
            foreach (var pt in pts) {
                polyTriangles.Add(pt);
            }
        }
        foreach (var pt in polyTriangles) {
            var triangleVertices = pt.GetVertices();
            var normal = pt.Normal();
            Debug.Assert(AOP.eq(pt.Normal().len(),1));
            foreach (var v in triangleVertices) {
                Point p = v.getPoint();
                if (!vertexNumbers.ContainsKey(v)) {
                    vertexNumbers[v] = (ushort)vertices.Count;
                    vertices.Add(p.x);
                    
                    normals?.Add(normal.x);
                    if (pt.isInvisible) {
                        uvs?.Add(new double[]{0,0});
                    }
                    else {
                        if (((Face2dBC)pt).camera!=null) {
                            uvs?.Add(UV(p,pt.integerCell,((Face2dBC)pt).camera));
                        } else {
                            uvs?.Add(UVFromIntegerFace(p,pt.integerCell,normal.x));
                        }
                    }
                }
                //if (!pt.isInvisible) {
                    triangles.Add(vertexNumbers[v]);
                //}
            }
        }
    }
    public Dictionary<Face2dWithIntegerCellAttribute,Face2dWithIntegerCellAttribute> ContainedFacetsInComponents() {
        Dictionary<Face2dWithIntegerCellAttribute,Face2dWithIntegerCellAttribute> res = new();
        List<Face2dWithIntegerCellAttribute> pool = new(debugPolyTriangles);
        foreach (var facet1 in debugPolyTriangles) {
            pool.Remove(facet1);
            foreach (var facet2 in pool) {
                try {
                    if (facet1.Containment(facet2.CenterPoint())==HalfSpace.INSIDE) {
                        res[facet2]=facet1;
                    }
                } catch (Face2d.NotInPlaneException) {

                }
            }
        }
        return res;
    }
}}