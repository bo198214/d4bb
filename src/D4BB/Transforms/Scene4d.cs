using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using D4BB.Comb;
using D4BB.Geometry;

namespace D4BB.Transforms
{
    public class Scene4d
    {
        public ICamera4d camera { get; set; }
        public bool showIntraCoplanarEdges;
        public bool showGridDivisions = true;
        public bool enable4dOcclusion = true;
        public bool cullBackFaces = true;
        // The scene's pieces — each bundles its topology, its occluded (cut) cells, and its projected
        // AABB. The single per-piece source of truth (replaced the old parallel pieceTopologies[] /
        // cellsByPiece[] / pieceBounds[] arrays). The array index is the level's piece index, matching
        // GameLevel.pieces. Consumers that need a flat view over every cell use AllCells (a computed
        // view, not stored state).
        public Piece[] pieces { get; private set; } = System.Array.Empty<Piece>();
        public IEnumerable<CellBoundary> AllCells => pieces.SelectMany(p => p.cells);

        // When non-null, this Scene4d is *bound* to an externally-owned piece list (the game's
        // GameLevel.pieces): it shares those Piece objects rather than creating its own, so a move
        // applied to a piece by the owner (piece.Translate/Rotate) is the *same* mutation the scene
        // renders — no cross-layer lockstep. Update() then only (re)computes each shared piece's
        // topology + re-occludes. Null ⇒ standalone (owns its pieces, built from int[][][] origins —
        // used by tests and the static goal scene).
        private List<Piece> boundPieces;

        // Standalone scene: owns its pieces, built from the given origins.
        public Scene4d(int[][][] origins, ICamera4d camera, bool showIntraCoplanarEdges = false, bool cullBackFaces = true, bool showGridDivisions = true, bool enable4dOcclusion = true)
        {
            this.camera = camera;
            this.showIntraCoplanarEdges = showIntraCoplanarEdges;
            this.showGridDivisions = showGridDivisions;
            this.cullBackFaces = cullBackFaces;
            this.enable4dOcclusion = enable4dOcclusion;
            Update(origins);
        }

        // Bound scene: shares the caller's piece objects (e.g. GameLevel.pieces). Topology + render
        // state are (re)built into those shared pieces.
        public Scene4d(List<Piece> pieces, ICamera4d camera, bool showIntraCoplanarEdges = false, bool cullBackFaces = true, bool showGridDivisions = true, bool enable4dOcclusion = true)
        {
            this.camera = camera;
            this.showIntraCoplanarEdges = showIntraCoplanarEdges;
            this.showGridDivisions = showGridDivisions;
            this.cullBackFaces = cullBackFaces;
            this.enable4dOcclusion = enable4dOcclusion;
            boundPieces = pieces;
            UpdateBound();
        }

        // ── public API ────────────────────────────────────────────────────────

        // Full rebuild. For a bound scene the origins argument is redundant (the shared pieces already
        // carry the up-to-date origins), so it is ignored and the scene resyncs from boundPieces — this
        // lets every existing `scene4d.Update(gameLevel.PieceOrigins)` call site keep working unchanged
        // after binding. A standalone scene (re)creates its own pieces from the origins.
        public void Update(int[][][] pieceOrigins)
        {
            if (boundPieces != null) { UpdateBound(); return; }
            int n = pieceOrigins?.Length ?? 0;
            pieces = new Piece[n];
            for (int i = 0; i < n; i++)
                pieces[i] = ComputePiece(pieceOrigins[i]);
            RebuildAllPieces();
            RefreshVisibleCache();
        }

        // Resync the working array from the shared list (count may have changed, e.g. after a combine)
        // and (re)fill each shared piece's topology, then occlude.
        private void UpdateBound()
        {
            pieces = boundPieces.ToArray();
            foreach (var p in pieces) FillTopology(p);
            RebuildAllPieces();
            RefreshVisibleCache();
        }

        public void UpdateCamera()
        {
            // Rebuild every piece's cells from cached topology and re-occlude. Cheap relative to a full
            // Update because each piece's topology is cached (no IBC recomputation). A fresh build is required
            // because CutOut destructively mutates faces — reusing last frame's cut cells would erode
            // d2faces. Keeps Face2dBC.points in sync with the current camera too.
            RebuildAllPieces();
            RefreshVisibleCache();
        }

        // Incremental piece move — a scene-level operation (it re-occludes every piece whose projected
        // AABB overlaps the moved piece before or after the move; see ReoccludeAfterPieceChange), so it
        // lives on Scene4d, not on Piece. Only the moved piece is reprojected. These combine the in-place
        // topology mutation with the incremental re-occlusion; the batched drag path instead mutates the
        // shared piece itself (via gameLevel) and then calls ReoccludePiece (re-occlusion only).
        // Returns the indices of the pieces whose render data (cells / visible cache / mesh) was
        // rebuilt — the moved piece plus every piece whose projected AABB overlapped it before or
        // after the move. The renderer uploads exactly these; all other pieces keep their meshes.
        public IReadOnlyList<int> Translate(int pieceIndex, IntegerSignedAxis axis)
        {
            var prev = pieces[pieceIndex].bounds;
            pieces[pieceIndex].Translate(axis);
            return ReoccludeAfterPieceChange(pieceIndex, prev);
        }

        public IReadOnlyList<int> Rotate(int pieceIndex, int v, int w, IntegerCenter center)
        {
            var prev = pieces[pieceIndex].bounds;
            pieces[pieceIndex].Rotate(v, w, center);
            return ReoccludeAfterPieceChange(pieceIndex, prev);
        }

        // Incremental re-occlusion when the caller has *already* moved the (shared) piece in place — the
        // drag path, where gameLevel.TranslateSelected/RotateSelected mutated the shared piece's topology
        // (possibly several steps) before this single refresh. piece.Translate/Rotate never touch
        // `bounds`, so pieces[pieceIndex].bounds still holds the pre-move projected AABB — the correct
        // `prev` for the overlap dependency check. Only the moved piece + the pieces whose AABB overlaps
        // it (before or after) are reprojected/re-occluded; the rest keep their cut cells. Byte-identical
        // to a full UpdateCamera for the same end state (see Scene4dIncrementalTests), just cheaper.
        public IReadOnlyList<int> ReoccludePiece(int pieceIndex)
            => ReoccludeAfterPieceChange(pieceIndex, pieces[pieceIndex].bounds);

        // ── topology computation (runs IntegerBoundaryComplex once per piece) ─

        // A 2-face f2 is interior (coplanar with the same 3-cell on both sides) when the IBC
        // neighbor of c3 via f2 equals the same-space sibling of c3 — i.e. both cells sharing
        // f2 lie in the same hyperplane. Such faces are excluded from the boundary.
        // Note: the same f2 may appear with multiple c3's (from different hyperplanes). Dedup
        // happens later in BuildPieceCells, after backface culling, so that a backface-
        // culled c3 cannot block f2 from being claimed by a front-facing c3'.
        private Piece ComputePiece(int[][] origins)
        {
            var piece = new Piece(origins);
            FillTopology(piece);
            return piece;
        }

        // (Re)compute the boundary topology of `piece` from its current origins (the expensive IBC step),
        // writing coplanarBoundaryFaces / interiorDivisionFaces in place. Called for every piece on a
        // full Update — for a standalone scene's freshly-created pieces, and for a bound scene's shared
        // pieces (which is how a shared piece gains its topology so a later piece.Translate/Rotate keeps
        // origins and topology consistent in one move).
        private void FillTopology(Piece piece)
        {
            var ibc = new IntegerBoundaryComplex(piece.origins);
            var coplanarBoundaryFaces = new List<(OrientedIntegerCell c3, OrientedIntegerCell f2)>();
            // Only allocate interior-collection plumbing when the toggle is on; saves work
            // and allocations on the common path. The toggle is propagated from Scene4d via Update,
            // which re-runs FillTopology with the new flag, so the cache stays in sync.
            var interiorDivisionFaces = showGridDivisions
                ? new List<(OrientedIntegerCell c3, OrientedIntegerCell f2)>() : null;
            // Track interior f2s already collected (from the other coplanar parent's iteration)
            // so we don't add them twice. Pick the lexicographically smaller c3 as the owner.
            var interiorClaimed = showGridDivisions ? new HashSet<IntegerCell>() : null;

            foreach (OrientedIntegerCell c3 in ibc.cells)
            {
                foreach (var f2 in c3.Facets())
                {
                    if (ibc.neighborOfVia[c3].TryGetValue(f2, out var ibcNeighbor)
                        && ibcNeighbor.Equals(c3.SameSpaceOtherParent(f2)))
                    {
                        if (showGridDivisions && interiorClaimed.Add(f2)) {
                            var owner = CompareCells(c3, ibcNeighbor) <= 0 ? c3 : ibcNeighbor;
                            interiorDivisionFaces.Add((owner, f2));
                        }
                        continue;
                    }
                    coplanarBoundaryFaces.Add((c3, f2));
                }
            }

            piece.coplanarBoundaryFaces = coplanarBoundaryFaces.ToArray();
            piece.interiorDivisionFaces = interiorDivisionFaces?.ToArray();
        }

        // ── fast rebuild from precomputed topology (skips IntegerBoundaryComplex) ─

        // Rebuild every piece's cut cells from cached topology + current camera, with occlusion.
        // Fills pieces[*].cells and pieces[*].bounds. Shared by Update and UpdateCamera.
        private void RebuildAllPieces()
        {
            var occluders = ComputeOccluders();   // (re)fills each piece's bounds
            SortFarToNear(occluders);
            for (int p = 0; p < pieces.Length; p++)
                pieces[p].cells = BuildOccludedPieceCells(p, occluders);
        }

        // Incremental re-occlusion after piece `p` moved. Reprojects only `p` (the moved piece) and
        // re-occludes only the pieces whose projected AABB overlaps `p` before or after the move.
        // The before-overlap term restores a piece that `p` used to occlude but no longer does; the
        // after-overlap term cuts a piece `p` now occludes. Pieces touching neither keep their cut
        // cells untouched. Equivalent to a full RebuildAllPieces because a cell's cut result depends
        // only on the strictly-nearer cells overlapping it (cut order is irrelevant — see
        // OccludePieceCells) and a move of `p` only changes overlap relationships that involve `p`.
        private IReadOnlyList<int> ReoccludeAfterPieceChange(int p, ScreenBounds prevBoundsOfP)
        {
            var occluders = ComputeOccluders();   // refreshes every piece's bounds
            SortFarToNear(occluders);
            var newBoundsOfP = pieces[p].bounds;

            var affected = new List<int> { p };
            for (int q = 0; q < pieces.Length; q++)
            {
                if (q == p) continue;
                if (pieces[q].bounds.Overlaps(prevBoundsOfP) || pieces[q].bounds.Overlaps(newBoundsOfP))
                    affected.Add(q);
            }
            foreach (var q in affected)
                pieces[q].cells = BuildOccludedPieceCells(q, occluders);
            RefreshVisibleCache(affected);
            return affected;
        }

        // Build piece `p`'s cells fresh from topology, then (if enabled) cut them against the scene's
        // occluders. The fresh build is mandatory: CutOut destructively edits faces, so a piece must
        // start from a clean projection every time it is re-occluded.
        private List<CellBoundary> BuildOccludedPieceCells(int p, List<Occluder> occludersFarToNear)
        {
            var pieceCells = BuildPieceCells(pieces[p], p, cullBackFaces);
            if (enable4dOcclusion) OccludePieceCells(p, pieceCells, occludersFarToNear);
            return pieceCells;
        }

        // Painter's-algorithm occlusion restricted to occludee == piece `p`. Equivalent to the global
        // far→near sweep (near cell cuts every already-seen farther cell), but only piece `p`'s cells
        // are enqueued as occludees; all cells still act as occluders (their half-spaces depend only on
        // the integer cell + camera, never on whether they were themselves cut). This restriction is
        // exact because CutOut's neighbor-Replace side effects stay within a single piece (there are no
        // cross-piece neighbor links), so cutting `p` is independent of how other pieces get cut.
        private void OccludePieceCells(int p, List<CellBoundary> pieceCells, List<Occluder> occludersFarToNear)
        {
            var cellByRef = new Dictionary<OrientedIntegerCell, CellBoundary>(ByRefCellComparer.I);
            foreach (var cb in pieceCells) cellByRef[cb.cell] = cb;

            // p-cells seen so far in the far→near sweep, i.e. candidates farther than the current
            // occluder. Insertion order is far→near, matching the global loop's `back` list.
            var seen = new List<(CellBoundary cb, double depth, ScreenBounds bounds)>();
            foreach (var occ in occludersFarToNear)
            {
                HalfSpace[] hs = null;
                foreach (var (cb, depth, bounds) in seen)
                    if (depth != occ.depth && bounds.Overlaps(occ.bounds))
                    {
                        hs ??= occ.HalfSpaces(camera);
                        cb.pbc.CutOut(hs);
                    }
                // Enqueue this occluder as an occludee only after it has cut the farther ones, so it
                // never cuts itself. Only p's own cells become occludees here.
                if (occ.pieceIndex == p && cellByRef.TryGetValue(occ.cell, out var pcb))
                    seen.Add((pcb, occ.depth, occ.bounds));
            }
        }

        // Pure per-piece builder — returns piece `pieceIndex`'s un-occluded ("clean") cells with
        // neighbor links wired up. Does not touch pieces[*].cells; the caller decides what to do with the
        // result (BuildOccludedPieceCells cuts it and stores it; ComputePieceBoundaryEdges runs
        // BoundaryEdges() locally without participating in occlusion). A fresh build is produced on
        // every call because CutOut later destructively modifies these faces by severing neighbor
        // links — reusing objects from a previous frame would leave stale topology.
        //
        // Each front-facing 3-cell c3 plays TWO independent roles in the scene:
        //   (a) Owner of visible 2-faces: each f2 is rendered exactly once, owned by some c3.
        //   (b) Occluder: c3's halfspaces cut other cells' faces during occlusion (OccludePieceCells).
        //
        // These roles must NOT be conflated. A c3 may legitimately own zero f2's — when all
        // its f2's are 2-corners shared with other-hyperplane c3's that won the dedup race —
        // and still be required as an occluder. Coupling the roles (only adding c3 if it owns at
        // least one f2) caused Box3D_NoDuplicateFaceFragmentsInSameCell: the diagonal-corner cell of
        // the 3DBox hole had no owned f2's, so its halfspaces were missing from occlusion, and the
        // diagonal-corner quadrant of the inner-hole wall survived the cut. (ComputeOccluders mirrors
        // this occluder set exactly, including the interior-division owner cells below.)
        private List<CellBoundary> BuildPieceCells(Piece piece, int pieceIndex, bool cullBackFacesArg)
        {
            // Role (b): every front-facing c3 must contribute halfspaces, regardless of f2 ownership.
            var occluderCells = new HashSet<OrientedIntegerCell>();
            // Role (a): each f2 → one Face2dBC, owned by exactly one c3.
            var faceOf = new Dictionary<IntegerCell, Face2dBC>();
            var ownedFacesOf = new Dictionary<OrientedIntegerCell, List<Face2dBC>>();

            // Iterate in a deterministic order so f2-dedup ownership is reproducible across
            // runs (HashSet iteration in coplanarBoundaryFaces' source is not stable).
            // Tiebreaker: lexicographic by (c3.origin, c3.span, f2.origin, f2.span).
            var orderedPairs = new List<(OrientedIntegerCell c3, OrientedIntegerCell f2)>(piece.coplanarBoundaryFaces);
            orderedPairs.Sort(ComparePairsForDedup);

            // Memoize the camera-facing test per 3-cell: the same c3 reference recurs in up to
            // 6 pairs (one per facet) and the two ownership passes below revisit them, so this
            // avoids recomputing IsFacedBy (and its two Point allocations) per pair.
            var facingOf = new Dictionary<OrientedIntegerCell, bool>(ByRefCellComparer.I);
            bool Facing(OrientedIntegerCell c3)
            {
                if (!facingOf.TryGetValue(c3, out var f))
                {
                    f = camera.IsFacedBy(new Point(c3.origin), new Point(c3.Normal()));
                    facingOf[c3] = f;
                }
                return f;
            }

            // f2-dedup ownership decides each face's winding (it follows the owner cell's
            // outward orientation). A surface 2-face is shared by a camera-facing and a
            // camera-averted 3-cell; the front owner produces the front-wound copy a
            // single-sided MeshCollider raycast can hit. With cullBackFaces=on the averted
            // cell is culled so the front cell always owns it; with cullBackFaces=off both
            // survive and a pure geometry sort could hand ownership to the back cell, leaving
            // the surface face back-wound (ray passes through it). So claim faces for FACING
            // owners first (pass 1), then let averted owners pick up the rest (pass 2) — those
            // are back/back shared faces on the far side, harmless to a single-sided raycast.
            foreach (var (c3, f2) in orderedPairs)
            {
                bool facing = Facing(c3);
                if (cullBackFacesArg && !facing)
                    continue;
                occluderCells.Add(c3);
                if (!facing) continue; // defer ownership to pass 2
                if (faceOf.ContainsKey(f2)) continue; // already claimed by an earlier facing c3

                var pf = new Face2dBC(f2, camera);
                faceOf[f2] = pf;
                if (!ownedFacesOf.ContainsKey(c3)) ownedFacesOf[c3] = new();
                ownedFacesOf[c3].Add(pf);
            }
            if (!cullBackFacesArg)
            {
                foreach (var (c3, f2) in orderedPairs)
                {
                    if (Facing(c3)) continue;
                    if (faceOf.ContainsKey(f2)) continue;

                    var pf = new Face2dBC(f2, camera);
                    faceOf[f2] = pf;
                    if (!ownedFacesOf.ContainsKey(c3)) ownedFacesOf[c3] = new();
                    ownedFacesOf[c3].Add(pf);
                }
            }

            // Set up edge neighbor links between adjacent visible faces.
            foreach (var pf1 in faceOf.Values)
            {
                var f1 = (OrientedIntegerCell)pf1.integerCell;
                foreach (var e in f1.Facets())
                {
                    var pEdge1 = pf1.i2p[e];
                    pEdge1.parent = pf1;
                    var sameSpace2d = f1.SameSpaceOtherParent(e);
                    if (faceOf.TryGetValue(sameSpace2d, out var pf2))
                    {
                        pEdge1.neighbor = pf2.i2p[e];
                        pEdge1.isCoplanarInterior = true;
                    }
                    else
                    {
                        pEdge1.isCoplanarInterior = false;
                    }
                }
            }

            // Lazy creation of interior Grid-Division 2-faces: only when the toggle is on.
            // They attach to the lexicographically-smaller (owner) c3's PBC so each interior
            // face lives in exactly one PBC for CutOut bookkeeping. The two coplanar parent
            // 3-cells share the same hyperplane, so any occluder that cuts one also cuts the
            // other — the choice of owner doesn't affect visual correctness.
            // The interior Face2dBCs are marked isCoplanarInterior=true (semantically: they
            // sit between two coplanar parents) and inherit mark=GRID_DIVISION from the
            // Face2dBC(OrientedIntegerCell,…) ctor stamp.
            if (showGridDivisions && piece.interiorDivisionFaces != null)
            {
                foreach (var (owner_c3, f2) in piece.interiorDivisionFaces)
                {
                    if (cullBackFacesArg && !camera.IsFacedBy(new Point(owner_c3.origin), new Point(owner_c3.Normal())))
                        continue; // owner backface-culled → drop the division face with it
                    occluderCells.Add(owner_c3); // ensure owner has a PBC
                    var pf = new Face2dBC(f2, camera) { isCoplanarInterior = true };
                    if (!ownedFacesOf.ContainsKey(owner_c3)) ownedFacesOf[owner_c3] = new();
                    ownedFacesOf[owner_c3].Add(pf);
                }
            }

            var result = new List<CellBoundary>(occluderCells.Count);
            foreach (var c3 in occluderCells)
            {
                var faces = ownedFacesOf.TryGetValue(c3, out var fs) ? fs : new List<Face2dBC>();
                var cellPbc = new Polyhedron3dBoundaryComplex(faces, showIntraCoplanarEdges, showGridDivisions);
                foreach (var pf in faces) pf.pbc = cellPbc;
                result.Add(new CellBoundary(c3, cellPbc, pieceIndex));
            }
            // Invariant: every front-facing c3 contributes an occluder, even ownerless ones.
            Debug.Assert(result.Count == occluderCells.Count,
                "BuildPieceCells: every front-facing c3 must contribute an occluder.");
            return result;
        }

        // Edges of a single piece without participating in scene-wide occlusion or mutating
        // pieces[*].cells. Used for the drag-ghost snapshot: pass cullBackFaces=false to get the
        // full hull of the piece (front + back facing cells).
        public HashSet<IPolyhedron> ComputePieceBoundaryEdges(int pieceIndex, bool cullBackFaces)
        {
            var cellsLocal = BuildPieceCells(pieces[pieceIndex], pieceIndex, cullBackFaces);
            var res = new HashSet<IPolyhedron>();
            foreach (var cb in cellsLocal)
                foreach (var e in cb.pbc.BoundaryEdges()) res.Add(e);
            return res;
        }

        // Lexicographic tiebreaker over the (c3, f2) iteration so f2-dedup is deterministic.
        private static int ComparePairsForDedup(
            (OrientedIntegerCell c3, OrientedIntegerCell f2) a,
            (OrientedIntegerCell c3, OrientedIntegerCell f2) b)
        {
            int c = CompareCells(a.c3, b.c3);
            if (c != 0) return c;
            return CompareCells(a.f2, b.f2);
        }
        private static int CompareCells(OrientedIntegerCell x, OrientedIntegerCell y)
        {
            for (int i = 0; i < x.origin.Length; i++)
            {
                int c = x.origin[i].CompareTo(y.origin[i]);
                if (c != 0) return c;
            }
            int xs = x.span.Count, ys = y.span.Count;
            if (xs != ys) return xs.CompareTo(ys);
            var xa = new int[xs]; x.span.CopyTo(xa); System.Array.Sort(xa);
            var ya = new int[ys]; y.span.CopyTo(ya); System.Array.Sort(ya);
            for (int i = 0; i < xs; i++)
            {
                int c = xa[i].CompareTo(ya[i]);
                if (c != 0) return c;
            }
            return x.inverted.CompareTo(y.inverted);
        }

        // ── camera occlusion ──────────────────────────────────────────────────

        // Cut-independent occluder descriptor: an occluder's half-spaces depend only on its integer
        // cell + camera (DefiningHalfSpaces), never on whether the occluder's own faces were cut. So
        // an occluder can be rebuilt from topology alone — the key that lets occlusion be recomputed
        // for a subset of pieces. depth + bounds drive the painter's-algorithm ordering and the cheap
        // overlap reject. HalfSpaces is computed lazily (only when this occluder actually cuts
        // something) and cached for reuse across the per-piece occlusion passes of one rebuild.
        private sealed class Occluder
        {
            public readonly OrientedIntegerCell cell;
            public readonly int pieceIndex;
            public readonly double depth;
            public readonly ScreenBounds bounds;
            private HalfSpace[] halfSpaces;
            public Occluder(OrientedIntegerCell cell, int pieceIndex, double depth, ScreenBounds bounds)
            { this.cell = cell; this.pieceIndex = pieceIndex; this.depth = depth; this.bounds = bounds; }
            public HalfSpace[] HalfSpaces(ICamera4d cam) => halfSpaces ??= DefiningHalfSpaces(cell, cam);
        }

        // Build the occluder set for the whole scene and (re)fill each piece's bounds. This must mirror
        // the occluder cells that BuildPieceCells adds — the front-facing distinct c3 from
        // the coplanar boundary faces, plus the interior-division owner cells when grid divisions are on
        // (those can own zero boundary 2-faces yet are still required as occluders; see the Box3D note
        // on BuildPieceCells). Cheap: only a facing test + an 8-vertex projection per c3,
        // no face construction.
        private List<Occluder> ComputeOccluders()
        {
            var viewNormal = camera.viewNormal.x;
            var result = new List<Occluder>();
            for (int p = 0; p < pieces.Length; p++)
            {
                var piece = pieces[p];
                piece.bounds = ScreenBounds.Empty();
                var seen = new HashSet<OrientedIntegerCell>(ByRefCellComparer.I);
                void Add(OrientedIntegerCell c3)
                {
                    if (!seen.Add(c3)) return;
                    if (cullBackFaces && !camera.IsFacedBy(new Point(c3.origin), new Point(c3.Normal()))) return;
                    var b = ProjectedBounds(c3, camera);
                    result.Add(new Occluder(c3, p, Depth(c3, viewNormal), b));
                    piece.bounds.Encapsulate(b);
                }
                foreach (var (c3, _) in piece.coplanarBoundaryFaces) Add(c3);
                if (showGridDivisions && piece.interiorDivisionFaces != null)
                    foreach (var (owner, _) in piece.interiorDivisionFaces) Add(owner);
            }
            return result;
        }

        // Painter's-algorithm depth ordering: larger depth (deeper into the viewNormal direction,
        // i.e. farther from the camera) first, so each occludee is cut by everything strictly nearer.
        private static void SortFarToNear(List<Occluder> occluders)
        {
            occluders.Sort((a, b) => b.depth.CompareTo(a.depth));
        }
        private static double Depth(IntegerCell cell, double[] viewNormal)
        {
            var c = cell.Center();
            double sum = 0;
            for (int i = 0; i < viewNormal.Length; i++) sum += viewNormal[i] * c[i];
            return sum;
        }

        // Projected 3D AABB of a 3-cell (its 8 vertices through the camera). Cut-independent. Used as
        // the conservative occlusion-overlap gate and the piece-level dependency signal.
        public static ScreenBounds ProjectedBounds(OrientedIntegerCell cell, ICamera4d cam)
        {
            var b = ScreenBounds.Empty();
            foreach (var v in cell.Vertices())
                b.Encapsulate(cam.Proj3d(new Point4d(v)).x);
            return b;
        }

        // ── visible cache ─────────────────────────────────────────────────────

        private void RefreshVisibleCache()
        {
            for (int i = 0; i < pieces.Length; i++) RefreshVisibleCacheForPiece(i);
        }
        // Affected-only refresh for the incremental path: pieces not in `pieceIndices` keep their cached sets.
        private void RefreshVisibleCache(IEnumerable<int> pieceIndices)
        {
            foreach (var i in pieceIndices) RefreshVisibleCacheForPiece(i);
        }
        private void RefreshVisibleCacheForPiece(int i)
        {
            var facets = new HashSet<Face2d>(new Face2dUnOrientedEquality(AOP.binaryPrecision));
            var edges = new HashSet<IPolyhedron>();
            foreach (var cb in pieces[i].cells)
            {
                foreach (var facet in cb.pbc.d2faces) facets.Add(facet);
                foreach (var edge in cb.pbc.BoundaryEdges()) edges.Add(edge);
            }
            pieces[i].visibleFacets = facets;
            pieces[i].visibleEdges = edges;
            // Build the renderable geometry here too, so a piece carries its own up-to-date mesh and an
            // unchanged piece keeps the same object (reference-identity dirty signal). A fresh instance
            // is assigned every rebuild. Unity-free; the Unity upload + decoration happen in Scene4dView.
            pieces[i].facetsMesh = new FacetsGenericMesh(facets);
            pieces[i].edgesMesh = new EdgesGenericMesh(edges);
        }

        // ── helpers ───────────────────────────────────────────────────────────

        public static HalfSpace[] DefiningHalfSpaces(OrientedIntegerCell cell, ICamera4d cam)
        {
            HalfSpace[] res = new HalfSpace[6];
            var center = new Point(3);
            foreach (var corner in cell.Vertices())
                center.add(cam.Proj3d(new Point4d(corner)));
            center.multiply(1.0 / 8);

            int i = 0;
            foreach (var facet in cell.Facets())
            {
                var iVertices = facet.ClockwiseFromOutsideVertices2d();
                var o    = cam.Proj3d(new Point4d(iVertices[0]));
                var p1st = cam.Proj3d(new Point4d(iVertices[1]));
                var p2nd = cam.Proj3d(new Point4d(iVertices[3]));
                var d1st = p1st.subtract(o).normalize();
                var d2nd = p2nd.subtract(o).normalize();
                var normal = AOP.cross(d1st, d2nd).normalize();
                res[i++] = new HalfSpace(o, normal);
            }
            return res;
        }

    }
}
