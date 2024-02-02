using System.Collections.Generic;
using System.Linq;
using D4BB.Geometry;
using D4BB.Comb;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System;

namespace D4BB.Transforms
{
public static class VolumetricLineVertexData
{
    public static readonly double[][] TexCoords = {
        new double[] {1.0f, 1.0f},
        new double[] {1.0f, 0.0f},
        new double[] {0.5f, 1.0f},
        new double[] {0.5f, 0.0f},
        new double[] {0.5f, 0.0f},
        new double[] {0.5f, 1.0f},
        new double[] {0.0f, 0.0f},
        new double[] {0.0f, 1.0f},
    };


    public static readonly double[][] VertexOffsets = {
            new double[] {1.0f,   1.0f},
            new double[] {1.0f,  -1.0f},
            new double[] {0.0f,   1.0f},
            new double[] {0.0f,  -1.0f},
            new double[] {0.0f,   1.0f},
            new double[] {0.0f,  -1.0f},
            new double[] {1.0f,   1.0f},
            new double[] {1.0f,  -1.0f},
    };

    public static readonly ushort[] Indices =
    {
        2, 1, 0,
        3, 1, 2,
        4, 3, 2,
        5, 4, 2,
        4, 5, 6,
        6, 5, 7
    };
}

public class EdgesGenericMesh {
    public List<double[]> vertices = new();
    public List<ushort> triangles0 = new(); //regular
    public List<ushort> triangles1 = new(); //cut
    public List<ushort> triangles2 = new(); //debug
    public List<double[]> uv0s = new();
    public List<double[]> uv1s = new();
    public List<double[]> normals = new();

    public EdgesGenericMesh(HashSet<IPolyhedron> edgesIn) {
        Dictionary<Vertex, ushort> vertexNumbers = new(new RawVertexEquality());
        foreach (var edge_ in edgesIn) {
            var edge = (Edge)edge_;

            List<ushort> target;
            if (edge.isInvisible && edge.neighbor==null) {
                target = triangles1;
            } else if (edge.neighbor!=null && edge.neighbor.isInvisible) {
                target = triangles2;
            }
            else {
                target = triangles0;
            }

            var a = edge.a;
            var b = (Vertex)edge.b.neighbor; //because in the triangles we only use edge.a vertices

            var i0 = (ushort)vertices.Count;
            for (int i=0;i<4;i++) {
                vertexNumbers[a] = (ushort)vertices.Count;
                double[] ac = a.PointRef().x;
                double[] bc = b.PointRef().x;
                vertices.Add(new double[] {ac[0],ac[1],ac[2]});
                normals.Add( new double[] {bc[0],bc[1],bc[2]});
       			uv0s.Add(VolumetricLineVertexData.TexCoords[i]);
       			uv1s.Add(VolumetricLineVertexData.VertexOffsets[i]);
            }
            for (int i=0;i<4;i++) {
                vertexNumbers[b] = (ushort)vertices.Count;
                double[] ac = a.PointRef().x;
                double[] bc = b.PointRef().x;
                vertices.Add(new double[] {bc[0],bc[1],bc[2]});
                normals.Add( new double[] {ac[0],ac[1],ac[2]});
       			uv0s.Add(VolumetricLineVertexData.TexCoords[4+i]);
       			uv1s.Add(VolumetricLineVertexData.VertexOffsets[4+i]);
            }

            for (int i=0;i<VolumetricLineVertexData.Indices.Length;i++) {
                var j = VolumetricLineVertexData.Indices[i];
       			target.Add((ushort)(j+i0));
            }
        }
    }
}}
