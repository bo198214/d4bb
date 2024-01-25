using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using D4BB.Comb;
using D4BB.Geometry;
using D4BB.Transforms;
using NUnit.Framework;
namespace D4BB.Transforms {
public class TransformTests {
    // [Test] public void MyVector3Test() {
    //     var a = new Vector3(0,0,0);
    //     var b = new Vector3(0,0,0);
    //     var hs = new HashSet<Vector3>();
    //     hs.Add(a);
    //     hs.Add(b);
    //     Assert.That(hs.Count, Is.EqualTo(1));
    // }
    [Test] public void PolyhedronMesh() {
        IPolyhedron cube = Face2dBC.FromIntegerCell(new int[]{0,0,0});;
        HashSet<Vertex> vertices = new(new RawVertexEquality());
        foreach (var facet in cube.facets) {
            foreach (var edge in facet.facets) {
                foreach (var vertex in edge.facets) {
                    vertices.Add((Vertex)vertex);
                }
            }
        }
        Assert.That(vertices, Has.Count.EqualTo(8*3*2));

        cube = Face2dBC.FromIntegerCell(new int[]{0,0,0});
        var cm = new FacetsGenericMesh(cube.facets,withCenter: false, duplicateVertices: false);
        Assert.That(cm.triangles.Count,Is.EqualTo(6*2*3));
        Assert.That(cm.vertices.Count,Is.EqualTo(8));

        cube = Face2dBC.FromIntegerCell(new int[]{0,0,0});
        cm = new FacetsGenericMesh(cube.facets,withCenter: true, duplicateVertices: false);
        Assert.That(cm.triangles.Count,Is.EqualTo(6*4*3));
        Assert.That(cm.vertices.Count,Is.EqualTo(8+6));

        cube = Face2dBC.FromIntegerCell(new int[]{0,0,0});
        cm = new FacetsGenericMesh(cube.facets,withCenter: false, duplicateVertices: true);
        Assert.That(cm.triangles.Count,Is.EqualTo(6*2*3));
        Assert.That(cm.vertices.Count,Is.EqualTo(8*3));

        cube = Face2dBC.FromIntegerCell(new int[]{0,0,0});
        cm = new FacetsGenericMesh(cube.facets,withCenter: true, duplicateVertices: true);
        Assert.That(cm.triangles.Count,Is.EqualTo(6*4*3));
        Assert.That(cm.vertices.Count,Is.EqualTo(8*3+6));
    }
    [Test] public void UV4facet() {
        //x-y-axis
        var uvcoordinates = FacetsGenericMesh.UV4facetList(new List<Point>() {
            new(0,0,0), new(1,0,0), new(1,1,0), new(0,1,0)},
            new Point(0.5,0.5,1));
        
        Assert.That(uvcoordinates.Count==4);
        Assert.That(uvcoordinates[0][0]==0 && uvcoordinates[0][1]==0 && uvcoordinates[1][0]==1 && uvcoordinates[1][1]==0);
        Assert.That(uvcoordinates[2][0]==1 && uvcoordinates[2][1]==1 && uvcoordinates[3][0]==0 && uvcoordinates[3][1]==1);

        //y-z-axis
        uvcoordinates = FacetsGenericMesh.UV4facetList(new List<Point>() {
            new(0,0,0), new(0,1,0), new(0,1,1), new(0,0,1)},
            new Point(1,0.5,0.5));
        Assert.That(uvcoordinates.Count==4);
        Assert.That(uvcoordinates[0][0]==0 && uvcoordinates[0][1]==0 && uvcoordinates[1][0]==1 && uvcoordinates[1][1]==0);
        Assert.That(uvcoordinates[2][0]==1 && uvcoordinates[2][1]==1 && uvcoordinates[3][0]==0 && uvcoordinates[3][1]==1);

        //translation
        uvcoordinates = FacetsGenericMesh.UV4facetList(new List<Point>() {
            new(0,7,7), new(0,8,7), new(0,8,8), new(0,7,8)},
            new Point(1,4,4));
        Assert.That(uvcoordinates.Count==4);
        Assert.That(uvcoordinates[0][0]==0 && uvcoordinates[0][1]==0 && uvcoordinates[1][0]==1 && uvcoordinates[1][1]==0);
        Assert.That(uvcoordinates[2][0]==1 && uvcoordinates[2][1]==1 && uvcoordinates[3][0]==0 && uvcoordinates[3][1]==1);

        //scaling
        uvcoordinates = FacetsGenericMesh.UV4facetList(new List<Point>() {
            new(0,0,0), new(0,0.5,0), new(0,0.5,0.5), new(0,0,0.5)},
            new Point(1,0.25,0.25));
        Assert.That(uvcoordinates.Count==4);
        Assert.That(uvcoordinates[0][0]==0 && uvcoordinates[0][1]==0 && uvcoordinates[1][0]==1 && uvcoordinates[1][1]==0);
        Assert.That(uvcoordinates[2][0]==1 && uvcoordinates[2][1]==1 && uvcoordinates[3][0]==0 && uvcoordinates[3][1]==1);

        //rotation
        uvcoordinates = FacetsGenericMesh.UV4facetList(new List<Point>() {
            new(0,0.5,0), new(0.5,0,0), new(1,0.5,0), new(0.5,1,0)},
            new Point(0.5,0.5,1));
        Assert.That(uvcoordinates.Count==4);
        Assert.That(uvcoordinates[0][0]==0 && uvcoordinates[0][1]==0 && uvcoordinates[1][0]==1 && uvcoordinates[1][1]==0);
        Assert.That(uvcoordinates[2][0]==1 && uvcoordinates[2][1]==1 && uvcoordinates[3][0]==0 && uvcoordinates[3][1]==1);        
    }
    [Test] public void PolyhedralComplex_mesh() {
        {
            var ic = Face2dBC.FromIntegerCell(new int[] { 0, 0, 0 });
            var pc = new PolyhedralComplex(new HashSet<IPolyhedron>{ic});
            var cm = new FacetsGenericMesh(pc.CalculateVisibleFacets(),duplicateVertices:false,withCenter: false);
            Assert.That(cm.vertices.Count, Is.EqualTo(8));
            Assert.That(cm.triangles.Count, Is.EqualTo(6*2*3));
        }
        {
            var ic = Face2dBC.FromIntegerCell(new int[] { 0, 0, 0 });
            var ic2 = Face2dBC.FromIntegerCell(new int[] { 1, 0, 0 });
            var pc = new PolyhedralComplex(new HashSet<IPolyhedron>{ic,ic2});
            var cm = new FacetsGenericMesh(pc.CalculateVisibleFacets(),duplicateVertices:false,withCenter: false);
            Assert.That(cm.vertices.Count, Is.EqualTo(12));
            Assert.That(cm.triangles.Count, Is.EqualTo(10*2*3));
        }
    }
    [Test] public  void CutOutTest() {
            var pc = new PolyhedralComplex(new HashSet<IPolyhedron>(){
                Face2dBC.FromIntegerCell(new int[]{0,0,0}),
            });
            foreach (var cell in pc.cells) {
                foreach (var facet in cell.facets) {
                    Assert.That(
                        facet.GetType(), Is.EqualTo(typeof(Face2dBC)));
                }
            }
            var polyhedron1 = PolyhedronCreate.Cube3dAt(new Point(-0.5,-0.5,-0.5),1);
            var pc2 = pc.CutOut(polyhedron1);
            var facetsCount = 8+4+4+6+8;
            var edgesCount = 3+8+1+8+4;
            Assert.That(pc2.CalculateVisibleFaces(1).Count,Is.EqualTo(facetsCount));
            Assert.That(pc2.CalculateVisibleFaces(2).Count,Is.EqualTo(edgesCount));
            var cm = new FacetsGenericMesh(pc2.CalculateVisibleFacets(),withCenter: true, withVertexUVs: true);
            Assert.That(true);
    }
    [Test] public void Inset() {
        {
            var face2d1 = new Face2dBC(new OrientedIntegerCell(new int[]{0,0,0},new HashSet<int>{0,1},true,true));
            var face2d2 = new Face2dBC(new OrientedIntegerCell(new int[]{0,0,0},new HashSet<int>{0,1},true,true));
            var cm1 = new FacetsGenericMesh(new HashSet<IPolyhedron>{face2d1},withCenter: false, withVertexUVs: true);
            face2d2.Inset(0.25);
            var cm2 = new FacetsGenericMesh(new HashSet<IPolyhedron>{face2d2},withCenter: false, withVertexUVs: true);
            Assert.That(cm2.vertices, Has.Count.EqualTo(cm1.vertices.Count));
            Assert.That(cm2.triangles, Has.Count.EqualTo(cm1.triangles.Count));
        }
        {
            var pc1 = new PolyhedralComplex(new HashSet<IPolyhedron>(){
                    Face2dBC.FromIntegerCell(new int[]{0,0,0}),
            });
            var pc2 = new PolyhedralComplex(new HashSet<IPolyhedron>(){
                    Face2dBC.FromIntegerCell(new int[]{0,0,0}),
                });
            foreach (var cell in pc2.cells) {
                foreach (var facet in cell.facets) {
                    ((Face2d)facet).Inset(-1);
                }
            }
            var pc2faces = pc2.Faces(2);
            Assert.That(pc2faces,Has.Count.EqualTo(pc1.Faces(2).Count));
            var cm1 = new FacetsGenericMesh(pc1.Faces(2),withCenter: false,withVertexUVs: true);
            var cm2 = new FacetsGenericMesh(pc2.Faces(2),withCenter: false,withVertexUVs: true);
            Assert.That(cm2.vertices,Has.Count.EqualTo(cm1.vertices.Count));
            Assert.That(cm2.triangles,Has.Count.EqualTo(cm1.triangles.Count));
        }
    }

    [Test] public void Scene4dSingleTest() {
        var camera = new Camera4dCentral();
        var scene = new Scene4d(new int[][][] { new int[][] { new int[] {0,0,0,0}}},camera);
        Assert.That(scene.components3d,Has.Count.EqualTo(4));

        camera.eye = new Point4d(0.5,0.5,0.5,-1);
        scene.ReCalculate();
        Assert.That(scene.components3d,Has.Count.EqualTo(1));
    }
    [Test] public void ComponentOcclusion() {
        {
            Scene4d.Component c1 = new(){cells=new HashSet<OrientedIntegerCell>{
                new(new int[]{0,0},new HashSet<int>{0},false,true),
            }};
            Scene4d.Component c2 = new(){cells=new HashSet<OrientedIntegerCell>{
                new(new int[]{0,0},new HashSet<int>{1},false,true)
            }};
            var occ = new InFrontOfComponentComparer();
            Assert.That(occ.Compare(c1,c2),Is.EqualTo(0));
        }
        {
            Scene4d.Component c1 = new(){cells=new HashSet<OrientedIntegerCell>{
                new(new int[]{0,0},new HashSet<int>{0},false,true),
            }};
            Scene4d.Component c2 = new(){cells=new HashSet<OrientedIntegerCell>{
                new(new int[]{0,0},new HashSet<int>{1},false,true),
                new(new int[]{0,-1},new HashSet<int>{1},false,true)
            }};
            List<Scene4d.Component> list = new(){c1,c2};
            list.Sort(new InFrontOfComponentComparer());
            Assert.That(Is.ReferenceEquals(list.First(),c2));
        }
        {
            Scene4d.Component c1 = new(){cells=new HashSet<OrientedIntegerCell>{
                new(new int[]{0,0},new HashSet<int>{0},false,true),
                new(new int[]{-1,0},new HashSet<int>{0},false,true),
            }};
            Scene4d.Component c2 = new(){cells=new HashSet<OrientedIntegerCell>{
                new(new int[]{0,0},new HashSet<int>{1},false,true),
            }};
            List<Scene4d.Component> list = new(){c1,c2};
            list.Sort(new InFrontOfComponentComparer());
            Assert.That(Is.ReferenceEquals(list.First(),c1));
        }
        {
            Scene4d.Component c1 = new(){cells=new HashSet<OrientedIntegerCell>{
                new(new int[]{0,0},new HashSet<int>{0},false,true),
                new(new int[]{-1,0},new HashSet<int>{0},false,true),
            }};
            Scene4d.Component c2 = new(){cells=new HashSet<OrientedIntegerCell>{
                new(new int[]{0,0},new HashSet<int>{1},false,true),
                new(new int[]{0,1},new HashSet<int>{1},false,true),
            }};
            List<Scene4d.Component> list = new(){c1,c2};
            list.Sort(new InFrontOfComponentComparer());
            Assert.That(Is.ReferenceEquals(list.First(),c1));
        }
    }
}
}