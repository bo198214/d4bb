using System.Collections.Generic;
using D4BB.Comb;
using D4BB.Geometry;

namespace D4BB.Transforms
{
    public class Scene4d
    {
        public ICamera4d camera { get; set; }
        public bool showInvisibleEdges;
        public bool enable4dOcclusion = true;
        public bool stepMode = false;
        public int stepIndex = 0;
        public int maxSteps = 0;
        public readonly List<CellBoundary> cells = new();

        public Scene4d(int[][][] origins, ICamera4d camera, bool showInvisibleEdges = false)
        {
            this.camera = camera;
            this.showInvisibleEdges = showInvisibleEdges;
            Update(origins);
        }

        public HashSet<Face2d> VisibleFacets(int pieceIndex)
        {
            var res = new HashSet<Face2d>(new Face2dUnOrientedEquality(AOP.binaryPrecision));
            foreach (var cb in cells)
                if (cb.pieceIndex == pieceIndex)
                    foreach (var facet in cb.pbc.d2faces) res.Add(facet);
            return res;
        }

        public HashSet<IPolyhedron> VisibleEdges(int pieceIndex)
        {
            var res = new HashSet<IPolyhedron>();
            foreach (var cb in cells)
                if (cb.pieceIndex == pieceIndex)
                    foreach (var edge in cb.pbc.VisibleEdges()) res.Add(edge);
            return res;
        }

        public void Update(int[][][] pieceOrigins)
        {
            RebuildCells(pieceOrigins, camera, enable4dOcclusion, showInvisibleEdges, cells);
            ApplyCameraOcclusion();
        }

        public void UpdateCamera()
        {
            ApplyCameraOcclusion();
        }

        private static void RebuildCells(int[][][] pieceOrigins, ICamera4d camera, bool enable4dOcclusion, bool showInvisibleEdges, List<CellBoundary> cellsOut)
        {
            cellsOut.Clear();
            if (pieceOrigins == null) return;

            for (int pieceIndex = 0; pieceIndex < pieceOrigins.Length; pieceIndex++)
            {
                var ibc = new IntegerBoundaryComplex(pieceOrigins[pieceIndex]);

                // Step 1: For each boundary 3D cell, compute its visible 2D faces.
                // f2 is interior iff its IBC neighbor equals its same-hyperplane neighbor.
                var allFace2dBC = new Dictionary<IntegerCell, Face2dBC>();
                var cell2Faces = new Dictionary<OrientedIntegerCell, List<Face2dBC>>();

                foreach (OrientedIntegerCell c3 in ibc.cells)
                {
                    if (!camera.IsFacedBy(new Point(c3.origin), new Point(c3.Normal())) && enable4dOcclusion)
                        continue;

                    var cellFacesList = new List<Face2dBC>();
                    foreach (var f2 in c3.Facets())
                    {
                        if (ibc.neighborOfVia[c3].TryGetValue(f2, out var ibcNeighbor)
                            && ibcNeighbor.Equals(c3.SameSpaceOtherParent(f2))) continue; // interior

                        if (allFace2dBC.ContainsKey(f2)) continue; // already claimed

                        var pf = new Face2dBC(f2, camera);
                        allFace2dBC[f2] = pf;
                        cellFacesList.Add(pf);
                    }

                    if (cellFacesList.Count > 0)
                        cell2Faces[c3] = cellFacesList;
                }

                // Step 2: Set up edge neighbor links between adjacent visible faces.
                // For edge e of face f1: if the same-hyperplane face f2 is also visible,
                // link them as interior (invisible) neighbors; otherwise the edge is visible.
                foreach (var pf1 in allFace2dBC.Values)
                {
                    var f1 = (OrientedIntegerCell)pf1.integerCell;
                    foreach (var e in f1.Facets())
                    {
                        var pEdge1 = pf1.i2p[e];
                        pEdge1.parent = pf1;
                        var sameSpace2d = f1.SameSpaceOtherParent(e);
                        if (allFace2dBC.TryGetValue(sameSpace2d, out var pf2))
                        {
                            pEdge1.neighbor = pf2.i2p[e];
                            pEdge1.isInvisible = true;
                        }
                        else
                        {
                            pEdge1.isInvisible = false;
                        }
                    }
                }

                // Step 3: Build per-cell PBCs and set the pbc reference on each face.
                foreach (var (c3, faces) in cell2Faces)
                {
                    var cellPbc = new Polyhedron3dBoundaryComplex(faces, showInvisibleEdges);
                    foreach (var pf in faces) pf.pbc = cellPbc;
                    cellsOut.Add(new CellBoundary(c3, cellPbc, pieceIndex));
                }
            }

            // Cross-piece deduplication: shared 2-faces between pieces cancel.
            var claimedCells = new HashSet<IntegerCell>();
            foreach (var cb in cellsOut)
            {
                var toRemove = new List<Face2dBC>();
                foreach (var kvp in cb.pbc.i2p)
                    if (!claimedCells.Add(kvp.Key))
                        toRemove.Add(kvp.Value);
                foreach (var facet in toRemove)
                    cb.pbc.RemoveFace(facet);
            }
        }

        private void ApplyCameraOcclusion()
        {
            if (!enable4dOcclusion) return;

            var viewNormal = camera.viewNormal.x;
            var cmp = new InFrontOfViewNormalComparer(viewNormal, reverse: true);

            cells.Sort((a, b) => cmp.Compare(b.cell, a.cell)); // far-to-near

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
                    var halfSpaces = DefiningHalfSpaces(nearCell.cell, camera);
                    foreach (var farCell in back)
                        if (cmp.Compare(farCell.cell, nearCell.cell) != 0)
                            farCell.pbc.CutOut(halfSpaces);
                    back.Add(nearCell);
                }
                i++;
            }
        }

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
