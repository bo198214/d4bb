using System.Collections.Generic;
using System.Linq;
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

            for (int i = 0; i < pieceOrigins.Length; i++)
            {
                var d4PieceIBC = new IntegerBoundaryComplex(pieceOrigins[i]);
                foreach (var slabCells in d4PieceIBC.Slabs())
                {
                    Point origin = new(slabCells.First().origin);
                    Point normal = new(slabCells.First().Normal());
                    if (camera.IsFacedBy(origin, normal) || !enable4dOcclusion)
                    {
                        var slabPbc = new Polyhedron3dBoundaryComplex(slabCells, camera, showInvisibleEdges);
                        foreach (var cb in slabPbc.cellBoundaries)
                            cellsOut.Add(new CellBoundary(cb.cell, cb.pbc, i));
                    }
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
            var vertices = cell.Vertices();
            foreach (var corner in vertices)
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
