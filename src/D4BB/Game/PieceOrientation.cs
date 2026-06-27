using D4BB.Geometry;
using Proj = D4BB.Game.PieceProjectionOverlap.ParallelProjection;

namespace D4BB.Game
{
    /// <summary>
    /// Game-facing camera wrapper around <see cref="PieceProjectionOverlap"/>: finds a discrete lattice
    /// rotation of a piece whose parallel 3D projection through a live
    /// <c>Camera4dParallel</c> shows as few mutually overlapping 3-cells as possible — an "auto-orient
    /// for clearest view" helper.
    ///
    /// All the geometry/search logic lives in the Geometry-free core; this type only converts a
    /// <c>Camera4dParallel</c> into a <see cref="PieceProjectionOverlap.ParallelProjection"/> (its image
    /// basis <c>v[0..2]</c> and <c>viewNormal</c>) and forwards. See that core for the method and the
    /// definition of "overlap".
    /// </summary>
    public static class PieceOrientation
    {
        public static PieceProjectionOverlap.Result BestViewRotation(int[][] origins, Camera4dParallel cam)
            => PieceProjectionOverlap.BestRotation(origins, ProjectionOf(cam));

        public static int CountOverlappingFrontCellPairs(int[][] origins, Camera4dParallel cam)
            => PieceProjectionOverlap.CountOverlappingFrontCellPairs(origins, ProjectionOf(cam));

        // Snapshot the camera's parallel projection: the three image-plane basis rows v[0..2] and the
        // view normal v[3]. zoom3d is a uniform scale and irrelevant to overlap, so it is dropped.
        static Proj ProjectionOf(Camera4dParallel cam)
        {
            var v = cam.v;
            var vn = cam.viewNormal;
            return new Proj(
                new[]
                {
                    new[] { v[0].x[0], v[0].x[1], v[0].x[2], v[0].x[3] },
                    new[] { v[1].x[0], v[1].x[1], v[1].x[2], v[1].x[3] },
                    new[] { v[2].x[0], v[2].x[1], v[2].x[2], v[2].x[3] },
                },
                new[] { vn.x[0], vn.x[1], vn.x[2], vn.x[3] });
        }
    }
}
