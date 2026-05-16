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
                for (int k = 0; k < 4; k++) {
                    vertex.x[k] = boundary4dMinMax[0][k] + ((vertexBinary >> k) & 1) * (boundary4dMinMax[1][k] - boundary4dMinMax[0][k]);
                }
                var vertex3d = camera.Proj3d(vertex);
                for (int k=0; k<3; k++) {
                    if (vertex3d.x[k] < min[k]) min[k] = vertex3d.x[k];
                    if (vertex3d.x[k] > max[k]) max[k] = vertex3d.x[k];
                }
            }
            return new double[][] { min, max };
        }
        public static double[] cavaliersFrontCenter(Objective objective, ICamera4d camera, double z_offset = 0, double y_offset = 0)
        {
            // X/Y: piece-bbox center (intentionally raw grid coords — cavalier shear
            // in x/y is small and the piece-bbox center gives the framing the levels
            // were authored against).
            var min_max = objective.BoundingBox();
            var x = (min_max[0][0] + min_max[1][0])/2.0;
            var y = (min_max[0][1] + min_max[1][1])/2.0;
            // Z: project the full 4D movement envelope through the camera and take
            // the smallest world-z. Accounts for cavalier w-shear so z_offset=0
            // really places the front of the play volume on world Z=0.
            var bmm4d = objective.boundary_min_max ?? min_max;
            var box3d = fieldBoundary3d(bmm4d, camera);
            var z = box3d[0][2];
            return new double[] { x, y-y_offset, z-z_offset};
        }
    }
}
