using System.Collections.Generic;
using D4BB.Geometry;
using D4BB.Comb;

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

public struct EdgeClassInfo {
    public int submesh;      // 0=regular, 1=cut, 2=debug
    public double[] a;       // 3D start position
    public double[] b;       // 3D end position
    public bool isCoplanarInterior;
    public bool neighborNull;
    public bool neighborCoplanarInterior;
    public IntegerCell integerCell;  // owning 2-face (from EdgeBC), null if unknown
}

public class EdgesGenericMesh {
    public List<double[]> vertices = new();
    public List<double[]> vertices4d = new();
    public List<double[]> normals4d = new();
    public List<ushort> triangles0 = new(); //regular
    public List<ushort> triangles1 = new(); //cut
    public List<ushort> triangles2 = new(); //debug
    public List<double[]> uv0s = new();
    public List<double[]> uv1s = new();
    public List<double[]> normals = new();
    public List<EdgeClassInfo> edgeClassifications = new();

    public EdgesGenericMesh(HashSet<IPolyhedron> edgesIn) {
        Dictionary<Vertex, ushort> vertexNumbers = new(new RawVertexEquality());
        foreach (var edge_ in edgesIn) {
            var edge = (Edge)edge_;

            List<ushort> target;
            int submesh;
            // Submesh routing:
            //   0 = regular boundary edges (edgeRegularMaterial)
            //   1 = cut-boundary edges (edgeCutMaterial): one side of a coplanar pair where
            //       the other side was discarded by CutOut, so neighbor was nulled.
            //   2 = intracoplanar-pair edges (edgeInvisibleMaterial): symmetric coplanar
            //       pair where both halves are still present. Reached when a Split's
            //       CrossReference paired them and no CutOut has since removed one side.
            // Grid-intersection edges (mark=MARK_GRID_DIVISION) are a symmetric pair
            // too, but route to 0 (regular) — they reach BoundaryEdges() only when the
            // grid-toggle is on, and the user wants them with the regular material.
            if (edge.mark == IPolyhedron.MARK_GRID_DIVISION) {
                target = triangles0;
                submesh = 0;
            }
            else if (edge.isCoplanarInterior && edge.neighbor==null) {
                target = triangles1;
                submesh = 1;
            } else if (edge.neighbor!=null && edge.neighbor.isCoplanarInterior) {
                target = triangles2;
                submesh = 2;
            }
            else {
                target = triangles0;
                submesh = 0;
            }

            var a = edge.a;
            var b = (Vertex)edge.b.neighbor; //because in the triangles we only use edge.a vertices

            double[] ac0 = a.PointRef().x;
            double[] bc0 = b.PointRef().x;
            edgeClassifications.Add(new EdgeClassInfo {
                submesh = submesh,
                a = new double[]{ac0[0],ac0[1],ac0[2]},
                b = new double[]{bc0[0],bc0[1],bc0[2]},
                isCoplanarInterior = edge.isCoplanarInterior,
                neighborNull = edge.neighbor == null,
                neighborCoplanarInterior = edge.neighbor != null && edge.neighbor.isCoplanarInterior,
                integerCell = (edge as EdgeBC)?.integerCell,
            });

            var i0 = (ushort)vertices.Count;
            for (int i=0;i<4;i++) {
                vertexNumbers[a] = (ushort)vertices.Count;
                double[] ac = a.PointRef().x;
                double[] bc = b.PointRef().x;
                vertices.Add(new double[] {ac[0],ac[1],ac[2]});
                normals.Add( new double[] {bc[0],bc[1],bc[2]});
                vertices4d.Add(a.pos4d);
                normals4d.Add(b.pos4d);
       			uv0s.Add(VolumetricLineVertexData.TexCoords[i]);
       			uv1s.Add(VolumetricLineVertexData.VertexOffsets[i]);
            }
            for (int i=0;i<4;i++) {
                vertexNumbers[b] = (ushort)vertices.Count;
                double[] ac = a.PointRef().x;
                double[] bc = b.PointRef().x;
                vertices.Add(new double[] {bc[0],bc[1],bc[2]});
                normals.Add( new double[] {ac[0],ac[1],ac[2]});
                vertices4d.Add(b.pos4d);
                normals4d.Add(a.pos4d);
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
