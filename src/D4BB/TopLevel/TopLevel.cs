using D4BB.Game;
using D4BB.Geometry;
using D4BB.Transforms;

namespace D4BB.TopLevel
{
    public static class TopLevel
    {
        public static double[][] fieldBoundary3d(int[][] boundary4dMinMax, ICamera4d camera)
        {
            double[] min = new double[3];
            double[] max = new double[3];
            for (int k=0; k<3; k++) {
                min[k] = double.MaxValue;
                max[k] = double.MinValue;
            }
            for ( int vertexBinary = 0; vertexBinary < 16; vertexBinary++)
            {
                var vertex = new Point(4);
                for (int k = 0; k < 4; k++)
                    vertex.x[k] = boundary4dMinMax[0][k] + ((vertexBinary >> k) & 1) * (boundary4dMinMax[1][k] - boundary4dMinMax[0][k]);
                var vertex3d = camera.Proj3d(vertex);
                for (int k=0; k<3; k++) {
                    if (vertex3d.x[k] < min[k]) min[k] = vertex3d.x[k];
                    if (vertex3d.x[k] > max[k]) max[k] = vertex3d.x[k];
                }
            }
            return new double[][] { min, max };
        }

    }
}