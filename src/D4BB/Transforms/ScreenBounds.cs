namespace D4BB.Transforms
{
    // Axis-aligned bounding box of a cell's (or a piece's) projected 3D extent. Used purely as a
    // cheap conservative overlap gate for occlusion: two cells whose projected AABBs don't overlap
    // cannot occlude each other, so the (expensive) CutOut can be skipped. It is also the
    // piece-level dependency signal for incremental Translate/Rotate — a piece whose AABB overlaps
    // the moved piece's AABB (before or after the move) may need re-occluding.
    //
    // Correctness note: this is only ever allowed to be *too generous* (report an overlap that is
    // not real). A false overlap just costs a redundant CutOut, which FaceIntersectsPolyhedron turns
    // into a no-op, or a redundant piece rebuild. A *missed* overlap, on the other hand, would drop a
    // real cut (or skip a piece that needed rebuilding) — a visible bug. Hence Overlaps() uses a
    // positive epsilon and the box is the exact AABB of the projected vertices (a true superset of
    // every face of the cell).
    public struct ScreenBounds
    {
        // Default overlap tolerance. Generous on purpose (see class note): projected integer-cell
        // coordinates have magnitude ~O(10) in the cavalier projection, so 1e-6 never misses a real
        // touch yet stays far below any genuine cell separation.
        public const double EPS = 1e-6;

        public bool valid;
        public double minX, minY, minZ;
        public double maxX, maxY, maxZ;

        public static ScreenBounds Empty() => new ScreenBounds { valid = false };

        // Expand to include a projected 3D point (point.x is double[3]).
        public void Encapsulate(double[] p)
        {
            if (!valid)
            {
                valid = true;
                minX = maxX = p[0];
                minY = maxY = p[1];
                minZ = maxZ = p[2];
                return;
            }
            if (p[0] < minX) minX = p[0]; else if (p[0] > maxX) maxX = p[0];
            if (p[1] < minY) minY = p[1]; else if (p[1] > maxY) maxY = p[1];
            if (p[2] < minZ) minZ = p[2]; else if (p[2] > maxZ) maxZ = p[2];
        }

        // Expand to include another box (union). An invalid box contributes nothing.
        public void Encapsulate(ScreenBounds o)
        {
            if (!o.valid) return;
            if (!valid) { this = o; return; }
            if (o.minX < minX) minX = o.minX;
            if (o.minY < minY) minY = o.minY;
            if (o.minZ < minZ) minZ = o.minZ;
            if (o.maxX > maxX) maxX = o.maxX;
            if (o.maxY > maxY) maxY = o.maxY;
            if (o.maxZ > maxZ) maxZ = o.maxZ;
        }

        public bool Overlaps(ScreenBounds o, double eps = EPS)
        {
            if (!valid || !o.valid) return false;
            return minX <= o.maxX + eps && maxX >= o.minX - eps
                && minY <= o.maxY + eps && maxY >= o.minY - eps
                && minZ <= o.maxZ + eps && maxZ >= o.minZ - eps;
        }
    }
}
