namespace D4BB.Transforms {

public class CellBoundary {
    public readonly OrientedIntegerCell cell;
    public readonly Polyhedron3dBoundaryComplex pbc;

    public CellBoundary(OrientedIntegerCell cell, Polyhedron3dBoundaryComplex pbc) {
        this.cell = cell;
        this.pbc = pbc;
    }
}

}
