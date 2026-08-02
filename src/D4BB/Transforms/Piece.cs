using System.Collections.Generic;
using System.Runtime.CompilerServices;
using D4BB.Comb;
using D4BB.Geometry;

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

    // A single movable piece. The unified per-piece object (the former game-state `Compound` and the
    // render-side topology/cells were merged here): it carries the authoritative game state (`origins`,
    // `colorSlot`) AND the render state (boundary topology, occluded cells, visible facets/edges). A
    // single Translate/Rotate keeps all of them consistent, so there is no cross-layer lockstep.
    //
    // The render fields are only populated when a Scene4d processes the piece (builds its topology then
    // occludes); a piece used purely as game state (e.g. a GameLevel piece not yet bound to a Scene4d,
    // or the 3D path which renders via Scene3d) leaves `coplanarBoundaryFaces` null, and Translate/Rotate
    // then mutate only the game state.
    public class Piece
    {
        // ── game state (authoritative) ──
        // The piece's elementary-cell origins (one int[] per unit tesseract). The single source of truth
        // for the piece's position; PieceOrigins, win-detection and overlap checks all read this.
        public int[][] origins;
        // Stable identity slot used to pick a piece color from a palette sized at level load. Survives
        // Combine so a piece's color does not change when a neighbour merges into it. -1 = unassigned.
        public int colorSlot = -1;
        // Lattice center (in half-integer "twice" coords); the default rotation pivot.
        private IntegerCenter center;

        // ── topology (the IBC boundary, computed once by Scene4d; mutated in place by Translate/Rotate) ──
        // ALL boundary 3-cells of the piece (the full IntegerBoundaryComplex cell set). This — not the
        // pair list below — is the source of the occluder role: a 3-cell whose six 2-faces are ALL
        // coplanar-interior (the center cell of a flat >=3x3-cell wall, e.g. in the tunnel levels'
        // 3x3x3x3 block) contributes no (c3,f2) pair at all, yet its projected volume still occludes
        // whatever lies behind it. Deriving occluders from the pair list silently dropped such cells,
        // so nothing behind a wall-center cell was ever cut (the 2026-08 tunnel-level bug — the same
        // role conflation as the earlier Box3D ownerless-occluder bug, one level earlier in the chain).
        // Shares instances with the pair lists' c3 entries. Null until a Scene4d builds it.
        public OrientedIntegerCell[] boundaryCells;
        // The (3-cell, 2-face) boundary pairs of the piece, computed once via IntegerBoundaryComplex.
        // A Translate/Rotate only mutates the origins/spans of these cells in place, so the face
        // *selection* stays valid and the expensive IBC rebuild is avoided. Null until a Scene4d builds it.
        public (OrientedIntegerCell c3, OrientedIntegerCell f2)[] coplanarBoundaryFaces;
        // Interior 2-faces between two coplanar boundary 3-cells of the same piece (the "Grid-Division"
        // faces — the 2D subdivision that lives one dimension deeper than coplanar grid edges). Filtered
        // out of the boundary in Scene4d.ComputePiece with `continue`, but kept here so the cell builder
        // can re-add them as Face2dBCs when showGridDivisions=true. (c3 is the lexicographically smaller
        // of the two coplanar parents — pragmatic ownership choice so the face attaches to a single PBC.)
        public (OrientedIntegerCell c3, OrientedIntegerCell f2)[] interiorDivisionFaces;

        // ── render state, derived from the topology + camera ──
        // Occluded (cut) cells of this piece — rebuilt from topology + camera on every (re)occlusion.
        public List<CellBoundary> cells = new();
        // Projected 3D AABB, union over this piece's occluder cells. The dependency signal for the
        // incremental path: a piece overlapping the moved piece (before or after) may need re-cutting.
        public ScreenBounds bounds = ScreenBounds.Empty();
        // The piece's visible 2-faces / boundary edges, derived from `cells` after occlusion. This is
        // what the renderer reads to build the Unity mesh. (Re)filled by Scene4d.RefreshVisibleCache.
        public HashSet<Face2d> visibleFacets = new();
        public HashSet<IPolyhedron> visibleEdges = new();
        // The renderable geometry (triangulated facet mesh + volumetric edge mesh), built from
        // visibleFacets/visibleEdges by Scene4d.RefreshVisibleCacheForPiece. A *new* instance is
        // assigned on every rebuild, so reference identity doubles as a per-piece dirty signal: a
        // piece untouched by an incremental move keeps the same object, and the Unity-side renderer
        // (Scene4dView) can skip re-uploading it. These are Unity-free (D4BB.Transforms) — the Unity
        // Mesh upload + per-vertex decoration stay on the Assets side. Null until a Scene4d processes
        // the piece (a piece used purely as game state never gets meshes).
        public FacetsGenericMesh facetsMesh;
        public EdgesGenericMesh edgesMesh;

        public Piece(int[][] origins, int colorSlot = -1)
        {
            this.origins = IntegerOps.Clone(origins);
            this.colorSlot = colorSlot;
            RecomputeCenter();
        }

        private void RecomputeCenter() => center = new IntegerCenter(origins, asCubes: true);

        // Move the piece by one lattice step: origins + center, and the topology cells in place (if a
        // Scene4d has built them). The same c3 reference appears in up to 6 boundary tuples (one per
        // facet) and possibly also in interior tuples, so it is deduped by reference; f2 instances are
        // unique per tuple (Facets() builds fresh OrientedIntegerCells), so no dedup is needed for them.
        public void Translate(IntegerSignedAxis axis)
        {
            IntegerOps.Translate(origins, axis);
            center.Translate(axis);
            if (coplanarBoundaryFaces == null) return;
            var seen = new HashSet<OrientedIntegerCell>(ByRefCellComparer.I);
            // boundaryCells first: it holds every c3 instance including wall-center cells that
            // appear in NO pair (see the field note) — those must move with the piece too.
            foreach (var c3 in boundaryCells)
                if (seen.Add(c3)) c3.Translate(axis);
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

        // 90° rotation in the (v,w) plane around `pivot`: origins + topology cells (if built); the center
        // is recomputed because an arbitrary pivot (not the centroid) moves it.
        public void Rotate(int v, int w, IntegerCenter pivot)
        {
            foreach (var o in origins)
                IntegerOps.RotateAsCenters(o, pivot, v, w);
            RecomputeCenter();
            if (coplanarBoundaryFaces == null) return;
            var seen = new HashSet<OrientedIntegerCell>(ByRefCellComparer.I);
            foreach (var c3 in boundaryCells)
                if (seen.Add(c3)) c3.Rotate(pivot, v, w);
            foreach (var (c3, f2) in coplanarBoundaryFaces)
            {
                if (seen.Add(c3)) c3.Rotate(pivot, v, w);
                f2.Rotate(pivot, v, w);
            }
            if (interiorDivisionFaces != null)
                foreach (var (c3, f2) in interiorDivisionFaces)
                {
                    if (seen.Add(c3)) c3.Rotate(pivot, v, w);
                    f2.Rotate(pivot, v, w);
                }
        }

        // Absorb other pieces' cells into this one (used by the combine move). Only the game state is
        // merged here; the caller rebuilds the topology/render of the enlarged piece afterwards. The
        // surviving colorSlot is the smallest among the merged set so the merged piece keeps a stable
        // color identity.
        public void Combine(IEnumerable<Piece> others)
        {
            var all = new List<int[]>(origins);
            foreach (var other in others)
            {
                all.AddRange(other.origins);
                if (other.colorSlot >= 0 && (colorSlot < 0 || other.colorSlot < colorSlot))
                    colorSlot = other.colorSlot;
            }
            origins = all.ToArray();
            RecomputeCenter();
        }
    }
}
