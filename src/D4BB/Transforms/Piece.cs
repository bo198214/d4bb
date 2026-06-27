using System.Collections.Generic;
using System.Runtime.CompilerServices;
using D4BB.Comb;

namespace D4BB.Transforms
{
    // Reference-identity comparer for OrientedIntegerCell — topology mutation and cell building need to
    // touch every distinct *instance* exactly once, regardless of content equality.
    internal sealed class ByRefCellComparer : IEqualityComparer<OrientedIntegerCell>
    {
        internal static readonly ByRefCellComparer I = new();
        public bool Equals(OrientedIntegerCell x, OrientedIntegerCell y) => ReferenceEquals(x, y);
        public int GetHashCode(OrientedIntegerCell c) => RuntimeHelpers.GetHashCode(c);
    }

    // The (3-cell, 2-face) boundary pairs of a piece, computed once via IntegerBoundaryComplex. A
    // Translate/Rotate only mutates the origins/spans of these cells in place, so the face *selection*
    // stays valid and the expensive IBC rebuild is avoided.
    public class PieceTopology
    {
        // Origins are not stored — single source of truth is gameLevel.compounds[i].origins.
        // Topology mutations (Translate/Rotate) act on the OrientedIntegerCell instances *inside* these
        // tuple arrays directly.
        public (OrientedIntegerCell c3, OrientedIntegerCell f2)[] coplanarBoundaryFaces;
        // Interior 2-faces between two coplanar boundary 3-cells of the same piece (the "Grid-Division"
        // faces — the 2D subdivision that lives one dimension deeper than coplanar grid edges). Filtered
        // out of the boundary in Scene4d.ComputePieceTopology with `continue`, but kept here so the cell
        // builder can re-add them as Face2dBCs when showGridDivisions=true.
        // (c3 is the lexicographically smaller of the two coplanar parents — pragmatic ownership choice
        // so the face attaches to a single PBC for CutOut.)
        public (OrientedIntegerCell c3, OrientedIntegerCell f2)[] interiorDivisionFaces;

        // In-place topology mutation. Every OrientedIntegerCell in the tuple arrays gets Translate/Rotate
        // called exactly once: the same c3 reference appears in up to 6 boundary tuples (one per facet)
        // and possibly also in interior tuples, so it is deduped by reference; f2 instances are unique
        // per tuple (Facets() builds fresh OrientedIntegerCells), so no dedup is needed for them.
        public void Translate(IntegerSignedAxis axis)
        {
            var seen = new HashSet<OrientedIntegerCell>(ByRefCellComparer.I);
            foreach (var (c3, f2) in coplanarBoundaryFaces)
            {
                if (seen.Add(c3)) c3.Translate(axis);
                f2.Translate(axis);
            }
            if (interiorDivisionFaces != null)
                foreach (var (c3, f2) in interiorDivisionFaces)
                {
                    if (seen.Add(c3)) c3.Translate(axis);
                    f2.Translate(axis);
                }
        }

        public void Rotate(int v, int w, IntegerCenter center)
        {
            var seen = new HashSet<OrientedIntegerCell>(ByRefCellComparer.I);
            foreach (var (c3, f2) in coplanarBoundaryFaces)
            {
                if (seen.Add(c3)) c3.Rotate(center, v, w);
                f2.Rotate(center, v, w);
            }
            if (interiorDivisionFaces != null)
                foreach (var (c3, f2) in interiorDivisionFaces)
                {
                    if (seen.Add(c3)) c3.Rotate(center, v, w);
                    f2.Rotate(center, v, w);
                }
        }
    }

    // One render piece in a Scene4d: its combinatorial topology plus the per-piece render state derived
    // from it. Replaces the former parallel arrays (pieceTopologies[] / cellsByPiece[] / pieceBounds[])
    // that were coupled only by array index.
    public class Piece
    {
        // Cached boundary topology (the IBC result); mutated in place by Translate/Rotate.
        public readonly PieceTopology topology;
        // Occluded (cut) cells of this piece — rebuilt from topology + camera on every (re)occlusion.
        public List<CellBoundary> cells = new();
        // Projected 3D AABB, union over this piece's occluder cells. The dependency signal for the
        // incremental path: a piece overlapping the moved piece (before or after) may need re-cutting.
        public ScreenBounds bounds = ScreenBounds.Empty();

        public Piece(PieceTopology topology) { this.topology = topology; }
    }
}
