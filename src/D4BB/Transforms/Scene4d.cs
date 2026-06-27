using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using D4BB.Comb;
using D4BB.Geometry;

namespace D4BB.Transforms
{
    public class Scene4d
    {
        // Reference-identity comparer for OrientedIntegerCell — Translate/Rotate need to
        // mutate every distinct *instance* exactly once, regardless of content equality.
        private sealed class ByRefCellComparer : IEqualityComparer<OrientedIntegerCell> {
            internal static readonly ByRefCellComparer I = new();
            public bool Equals(OrientedIntegerCell x, OrientedIntegerCell y) => ReferenceEquals(x, y);
            public int GetHashCode(OrientedIntegerCell c) => RuntimeHelpers.GetHashCode(c);
        }

        public ICamera4d camera { get; set; }
        public bool showIntraCoplanarEdges;
        public bool showGridDivisions = true;
        public bool enable4dOcclusion = true;
        public bool cullBackFaces = true;
        public bool stepMode = false;
        public int stepIndex = 0;
        public int maxSteps = 0;
        public readonly List<CellBoundary> cells = new();
        public HashSet<Face2d>[] visibleFacets { get; private set; } = System.Array.Empty<HashSet<Face2d>>();
        public HashSet<IPolyhedron>[] visibleEdges { get; private set; } = System.Array.Empty<HashSet<IPolyhedron>>();

        // Caches the (3-cell, 2-face) boundary pairs for a piece, computed once via
        // IntegerBoundaryComplex. Translate/Rotate only mutate the origins/spans in-place,
        // so the face selection stays valid and the expensive IBC rebuild is avoided.
        public class PieceTopology {
            // Origins are not stored — single source of truth is gameLevel.compounds[i].origins.
            // Topology mutations (Translate/Rotate) act on the OrientedIntegerCell instances
            // *inside* these tuple arrays directly.
            public (OrientedIntegerCell c3, OrientedIntegerCell f2)[] coplanarBoundaryFaces;
            // Interior 2-faces between two coplanar boundary 3-cells of the same piece
            // (the "Grid-Division" faces — the 2D subdivision that lives one dimension
            // deeper than coplanar grid edges). Filtered out in ComputePieceTopology with
            // `continue`, but kept here so RebuildCellsFromPieceTopology can re-add them as
            // Face2dBCs when showGridDivisions=true.
            // (c3 is the lexicographically smaller of the two coplanar parents — pragmatic
            // ownership choice so the face attaches to a single PBC for CutOut.)
            public (OrientedIntegerCell c3, OrientedIntegerCell f2)[] interiorDivisionFaces;
        }
        public PieceTopology[] pieceTopologies { get; private set; } = System.Array.Empty<PieceTopology>();

        private int numPieces = 0;

        public Scene4d(int[][][] origins, ICamera4d camera, bool showIntraCoplanarEdges = false, bool cullBackFaces = true, bool showGridDivisions = true, bool enable4dOcclusion = true)
        {
            this.camera = camera;
            this.showIntraCoplanarEdges = showIntraCoplanarEdges;
            this.showGridDivisions = showGridDivisions;
            this.cullBackFaces = cullBackFaces;
            this.enable4dOcclusion = enable4dOcclusion;
            Update(origins);
        }

        public HashSet<Face2d> VisibleFacets(int pieceIndex) => visibleFacets[pieceIndex];
        public HashSet<IPolyhedron> VisibleEdges(int pieceIndex) => visibleEdges[pieceIndex];

        // ── public API ────────────────────────────────────────────────────────

        public void Update(int[][][] pieceOrigins)
        {
            numPieces = pieceOrigins?.Length ?? 0;
            pieceTopologies = ComputeAllTopologies(pieceOrigins, showGridDivisions);
            RebuildCellsFromTopologies();
            ApplyCameraOcclusion();
            RefreshVisibleCache();
        }

        public void UpdateCamera()
        {
            // Must rebuild before re-occluding: ApplyCameraOcclusion mutates pbc.d2faces
            // destructively, so calling it twice on the same cells erodes d2faces. The
            // rebuild is cheap because pieceTopologies is cached (no IBC recomputation).
            // Also keeps Face2dBC.points in sync with the current camera so HalfSpace.side
            // checks against DefiningHalfSpaces use a consistent projection.
            RebuildCellsFromTopologies();
            ApplyCameraOcclusion();
            RefreshVisibleCache();
        }

        public void Translate(int pieceIndex, IntegerSignedAxis axis)
        {
            TranslateTopology(pieceIndex, axis);
            UpdateCamera();
        }

        public void Rotate(int pieceIndex, int v, int w, IntegerCenter center)
        {
            RotateTopology(pieceIndex, v, w, center);
            UpdateCamera();
        }

        // Topology-only mutation, no cell rebuild / occlusion / cache refresh. Lets a caller
        // mirror several committed moves into pieceTopologies and then run a single UpdateCamera
        // (RebuildCells + Occlusion + RefreshVisibleCache, all without the expensive IBC) — used
        // by the drag path so multi-step drags occlude once, not per micro-step.
        //
        // Mutates in place — every OrientedIntegerCell in the tuple arrays gets Translate/Rotate
        // called exactly once. The same c3 reference appears in up to 6 boundary tuples (one per
        // facet) and possibly also in interior tuples; only the first occurrence is mutated. f2
        // instances are unique per tuple (Facets() builds fresh OrientedIntegerCells), no dedup.
        public void TranslateTopology(int pieceIndex, IntegerSignedAxis axis)
        {
            var topo = pieceTopologies[pieceIndex];
            var seen = new HashSet<OrientedIntegerCell>(ByRefCellComparer.I);
            foreach (var (c3, f2) in topo.coplanarBoundaryFaces)
            {
                if (seen.Add(c3)) c3.Translate(axis);
                f2.Translate(axis);
            }
            if (topo.interiorDivisionFaces != null)
                foreach (var (c3, f2) in topo.interiorDivisionFaces)
                {
                    if (seen.Add(c3)) c3.Translate(axis);
                    f2.Translate(axis);
                }
        }

        public void RotateTopology(int pieceIndex, int v, int w, IntegerCenter center)
        {
            var topo = pieceTopologies[pieceIndex];
            var seen = new HashSet<OrientedIntegerCell>(ByRefCellComparer.I);
            foreach (var (c3, f2) in topo.coplanarBoundaryFaces)
            {
                if (seen.Add(c3)) c3.Rotate(center, v, w);
                f2.Rotate(center, v, w);
            }
            if (topo.interiorDivisionFaces != null)
                foreach (var (c3, f2) in topo.interiorDivisionFaces)
                {
                    if (seen.Add(c3)) c3.Rotate(center, v, w);
                    f2.Rotate(center, v, w);
                }
        }

        // ── topology computation (runs IntegerBoundaryComplex once per piece) ─

        private static PieceTopology[] ComputeAllTopologies(int[][][] pieceOrigins, bool showGridDivisions)
        {
            if (pieceOrigins == null) return System.Array.Empty<PieceTopology>();
            var result = new PieceTopology[pieceOrigins.Length];
            for (int i = 0; i < pieceOrigins.Length; i++)
                result[i] = ComputePieceTopology(pieceOrigins[i], showGridDivisions);
            return result;
        }

        // A 2-face f2 is interior (coplanar with the same 3-cell on both sides) when the IBC
        // neighbor of c3 via f2 equals the same-space sibling of c3 — i.e. both cells sharing
        // f2 lie in the same hyperplane. Such faces are excluded from the boundary.
        // Note: the same f2 may appear with multiple c3's (from different hyperplanes). Dedup
        // happens later in RebuildCellsFromPieceTopology, after backface culling, so that a backface-
        // culled c3 cannot block f2 from being claimed by a front-facing c3'.
        private static PieceTopology ComputePieceTopology(int[][] origins, bool showGridDivisions)
        {
            var ibc = new IntegerBoundaryComplex(origins);
            var coplanarBoundaryFaces = new List<(OrientedIntegerCell c3, OrientedIntegerCell f2)>();
            // Only allocate interior-collection plumbing when the toggle is on; saves work
            // and allocations on the common path. The toggle is propagated from Scene4d via
            // ComputeAllTopologies — a Game.cs toggle flip triggers scene4d.Update, which
            // re-runs ComputeAllTopologies with the new flag, so the cache stays in sync.
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

            return new PieceTopology {
                coplanarBoundaryFaces = coplanarBoundaryFaces.ToArray(),
                interiorDivisionFaces = interiorDivisionFaces?.ToArray()
            };
        }

        // ── fast rebuild from precomputed topology (skips IntegerBoundaryComplex) ─

        private void RebuildCellsFromTopologies()
        {
            cells.Clear();
            for (int i = 0; i < pieceTopologies.Length; i++)
                RebuildCellsFromPieceTopology(pieceTopologies[i], i);

            // // Cross-piece deduplication: shared 2-faces between pieces cancel.
            // var claimedCells = new HashSet<IntegerCell>();
            // foreach (var cb in cells)
            // {
            //     var toRemove = new List<Face2dBC>();
            //     foreach (var kvp in cb.pbc.i2p)
            //         if (!claimedCells.Add(kvp.Key))
            //             toRemove.Add(kvp.Value);
            //     foreach (var facet in toRemove)
            //         cb.pbc.RemoveFace(facet);
            // }
        }

        // Each front-facing 3-cell c3 plays TWO independent roles in the scene:
        //   (a) Owner of visible 2-faces: each f2 is rendered exactly once, owned by some c3.
        //   (b) Occluder: c3's halfspaces cut other cells' faces in ApplyCameraOcclusion.
        //
        // These roles must NOT be conflated. A c3 may legitimately own zero f2's — when all
        // its f2's are 2-corners shared with other-hyperplane c3's that won the dedup race —
        // and still be required as an occluder. Coupling the roles (only adding c3 to `cells`
        // if it owns at least one f2) caused Box3D_NoDuplicateFaceFragmentsInSameCell: the
        // diagonal-corner cell of the 3DBox hole had no owned f2's, so its halfspaces were
        // missing from occlusion, and the diagonal-corner quadrant of the inner-hole wall
        // survived the cut.
        //
        // Fresh Face2dBC objects are created on every rebuild because CutOut (called in
        // ApplyCameraOcclusion) destructively modifies them by severing neighbor links.
        // Reusing objects from a previous frame would leave stale topology.
        private void RebuildCellsFromPieceTopology(PieceTopology topo, int pieceIndex)
            => cells.AddRange(BuildCellsFromPieceTopology(topo, pieceIndex, cullBackFaces));

        // Pure builder — does not touch this.cells. Caller decides what to do with the result
        // (RebuildCellsFromPieceTopology appends to this.cells; ComputePieceBoundaryEdges runs
        // BoundaryEdges() locally without participating in occlusion).
        private List<CellBoundary> BuildCellsFromPieceTopology(PieceTopology topo, int pieceIndex, bool cullBackFacesArg)
        {
            // Role (b): every front-facing c3 must contribute halfspaces, regardless of f2 ownership.
            var occluderCells = new HashSet<OrientedIntegerCell>();
            // Role (a): each f2 → one Face2dBC, owned by exactly one c3.
            var faceOf = new Dictionary<IntegerCell, Face2dBC>();
            var ownedFacesOf = new Dictionary<OrientedIntegerCell, List<Face2dBC>>();

            // Iterate in a deterministic order so f2-dedup ownership is reproducible across
            // runs (HashSet iteration in coplanarBoundaryFaces' source is not stable).
            // Tiebreaker: lexicographic by (c3.origin, c3.span, f2.origin, f2.span).
            var orderedPairs = new List<(OrientedIntegerCell c3, OrientedIntegerCell f2)>(topo.coplanarBoundaryFaces);
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
            if (showGridDivisions && topo.interiorDivisionFaces != null)
            {
                foreach (var (owner_c3, f2) in topo.interiorDivisionFaces)
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
                "BuildCellsFromPieceTopology: every front-facing c3 must contribute an occluder.");
            return result;
        }

        // Edges of a single piece without participating in scene-wide occlusion or mutating
        // this.cells. Used for the drag-ghost snapshot: pass cullBackFaces=false to get the
        // full hull of the piece (front + back facing cells).
        public HashSet<IPolyhedron> ComputePieceBoundaryEdges(int pieceIndex, bool cullBackFaces)
        {
            var topo = pieceTopologies[pieceIndex];
            var cellsLocal = BuildCellsFromPieceTopology(topo, pieceIndex, cullBackFaces);
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

        private void ApplyCameraOcclusion()
        {
            if (!enable4dOcclusion) return;

            var viewNormal = camera.viewNormal.x;
            SortCellsFarToNear(viewNormal);

            var back = new List<CellBoundary>();
            maxSteps = cells.Count;
            int i = 0;
            foreach (var nearCell in cells)
            {
                if (stepMode && i >= stepIndex)
                {
                    nearCell.pbc.d2faces.Clear();
                }
                else
                {
                    var nearDepth = Depth(nearCell.cell, viewNormal);
                    var halfSpaces = DefiningHalfSpaces(nearCell.cell, camera);
                    foreach (var farCell in back)
                        if (Depth(farCell.cell, viewNormal) != nearDepth)
                            farCell.pbc.CutOut(halfSpaces);
                    back.Add(nearCell);
                }
                i++;
            }
        }

        // Painter's-algorithm depth ordering: cells with larger depth (deeper into the
        // viewNormal direction, i.e. farther from the camera) come first; smaller-depth
        // (nearer) cells come last so each near cell can cut all already-processed far cells
        // in `back`. Equal-depth cells coexist (skipped from cutting each other below).
        private void SortCellsFarToNear(double[] viewNormal)
        {
            cells.Sort((a, b) => Depth(b.cell, viewNormal).CompareTo(Depth(a.cell, viewNormal)));
        }
        private static double Depth(IntegerCell cell, double[] viewNormal)
        {
            var c = cell.Center();
            double sum = 0;
            for (int i = 0; i < viewNormal.Length; i++) sum += viewNormal[i] * c[i];
            return sum;
        }

        // ── visible cache ─────────────────────────────────────────────────────

        private void RefreshVisibleCache()
        {
            visibleFacets = new HashSet<Face2d>[numPieces];
            visibleEdges = new HashSet<IPolyhedron>[numPieces];
            for (int i = 0; i < numPieces; i++)
            {
                visibleFacets[i] = new HashSet<Face2d>(new Face2dUnOrientedEquality(AOP.binaryPrecision));
                visibleEdges[i] = new HashSet<IPolyhedron>();
            }
            foreach (var cb in cells)
            {
                foreach (var facet in cb.pbc.d2faces) visibleFacets[cb.pieceIndex].Add(facet);
                foreach (var edge in cb.pbc.BoundaryEdges()) visibleEdges[cb.pieceIndex].Add(edge);
            }
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
