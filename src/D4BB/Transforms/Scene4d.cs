using System.Collections.Generic;
using System.Diagnostics;
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
            public int[][] origins;  // current tesseract origins (owned copy)
            public (OrientedIntegerCell c3, OrientedIntegerCell f2)[] coplanarBoundaryFaces;
        }
        public PieceTopology[] pieceTopologies { get; private set; } = System.Array.Empty<PieceTopology>();

        private int numPieces = 0;

        public Scene4d(int[][][] origins, ICamera4d camera, bool showIntraCoplanarEdges = false, bool cullBackFaces = true, bool showGridDivisions = true)
        {
            this.camera = camera;
            this.showIntraCoplanarEdges = showIntraCoplanarEdges;
            this.showGridDivisions = showGridDivisions;
            this.cullBackFaces = cullBackFaces;
            Update(origins);
        }

        public HashSet<Face2d> VisibleFacets(int pieceIndex) => visibleFacets[pieceIndex];
        public HashSet<IPolyhedron> VisibleEdges(int pieceIndex) => visibleEdges[pieceIndex];

        // ── public API ────────────────────────────────────────────────────────

        public void Update(int[][][] pieceOrigins)
        {
            numPieces = pieceOrigins?.Length ?? 0;
            pieceTopologies = ComputeAllTopologies(pieceOrigins);
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
            var topo = pieceTopologies[pieceIndex];
            IntegerOps.Translate(topo.origins, axis);
            foreach (var (c3, f2) in topo.coplanarBoundaryFaces)
            {
                c3.Translate(axis);
                f2.Translate(axis);
            }
            RebuildCellsFromTopologies();
            ApplyCameraOcclusion();
            RefreshVisibleCache();
        }

        public void Rotate(int pieceIndex, int v, int w, IntegerCenter center)
        {
            var topo = pieceTopologies[pieceIndex];
            IntegerOps.Rotate(topo.origins, center, v, w);
            foreach (var (c3, f2) in topo.coplanarBoundaryFaces)
            {
                c3.Rotate(center, v, w);
                f2.Rotate(center, v, w);
            }
            RebuildCellsFromTopologies();
            ApplyCameraOcclusion();
            RefreshVisibleCache();
        }

        // ── topology computation (runs IntegerBoundaryComplex once per piece) ─

        private static PieceTopology[] ComputeAllTopologies(int[][][] pieceOrigins)
        {
            if (pieceOrigins == null) return System.Array.Empty<PieceTopology>();
            var result = new PieceTopology[pieceOrigins.Length];
            for (int i = 0; i < pieceOrigins.Length; i++)
                result[i] = ComputePieceTopology(pieceOrigins[i]);
            return result;
        }

        // A 2-face f2 is interior (coplanar with the same 3-cell on both sides) when the IBC
        // neighbor of c3 via f2 equals the same-space sibling of c3 — i.e. both cells sharing
        // f2 lie in the same hyperplane. Such faces are excluded from the boundary.
        // Note: the same f2 may appear with multiple c3's (from different hyperplanes). Dedup
        // happens later in RebuildCellsFromPieceTopology, after backface culling, so that a backface-
        // culled c3 cannot block f2 from being claimed by a front-facing c3'.
        private static PieceTopology ComputePieceTopology(int[][] origins)
        {
            var ibc = new IntegerBoundaryComplex(origins);
            var coplanarBoundaryFaces = new List<(OrientedIntegerCell c3, OrientedIntegerCell f2)>();

            foreach (OrientedIntegerCell c3 in ibc.cells)
            {
                foreach (var f2 in c3.Facets())
                {
                    if (ibc.neighborOfVia[c3].TryGetValue(f2, out var ibcNeighbor)
                        && ibcNeighbor.Equals(c3.SameSpaceOtherParent(f2))) continue; // interior
                    coplanarBoundaryFaces.Add((c3, f2));
                }
            }

            return new PieceTopology {
                origins = DeepCloneOrigins(origins),
                coplanarBoundaryFaces = coplanarBoundaryFaces.ToArray()
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

            foreach (var (c3, f2) in orderedPairs)
            {
                if (cullBackFaces && !camera.IsFacedBy(new Point(c3.origin), new Point(c3.Normal())))
                    continue;
                occluderCells.Add(c3);
                if (faceOf.ContainsKey(f2)) continue; // already claimed by an earlier c3 in dedup order

                var pf = new Face2dBC(f2, camera);
                faceOf[f2] = pf;
                if (!ownedFacesOf.ContainsKey(c3)) ownedFacesOf[c3] = new();
                ownedFacesOf[c3].Add(pf);
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

            int addedHere = 0;
            foreach (var c3 in occluderCells)
            {
                var faces = ownedFacesOf.TryGetValue(c3, out var fs) ? fs : new List<Face2dBC>();
                var cellPbc = new Polyhedron3dBoundaryComplex(faces, showIntraCoplanarEdges, showGridDivisions);
                foreach (var pf in faces) pf.pbc = cellPbc;
                cells.Add(new CellBoundary(c3, cellPbc, pieceIndex));
                addedHere++;
            }
            // Invariant: every front-facing c3 contributes an occluder, even ownerless ones.
            Debug.Assert(addedHere == occluderCells.Count,
                "RebuildCellsFromPieceTopology: every front-facing c3 must contribute an occluder.");
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

        private static int[][] DeepCloneOrigins(int[][] origins)
        {
            var clone = new int[origins.Length][];
            for (int i = 0; i < origins.Length; i++)
                clone[i] = (int[])origins[i].Clone();
            return clone;
        }
    }
}
