namespace D4BB.Transforms {

public class CellBoundary {
    public readonly OrientedIntegerCell cell;
    public readonly Polyhedron3dBoundaryComplex pbc;
    public readonly int pieceIndex;

    public CellBoundary(OrientedIntegerCell cell, Polyhedron3dBoundaryComplex pbc, int pieceIndex = 0) {
        this.cell = cell;
        this.pbc = pbc;
        this.pieceIndex = pieceIndex;
    }
}

}
