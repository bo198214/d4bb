namespace D4BB.Game
{
    public enum GameStatus { None, Pending, Reached, Missed }

    // Maps to (v,w) axis-index pairs for IntegerOps.Rotate:
    // XY→(0,1), XZ→(0,2), XW→(0,3), YZ→(1,2), YW→(1,3), ZW→(2,3)
    public enum RotationPlane { XY, XZ, XW, YZ, YW, ZW }

    public static class RotationPlaneExt
    {
        public static (int v, int w) Axes(this RotationPlane plane) => plane switch
        {
            RotationPlane.XY => (0, 1),
            RotationPlane.XZ => (0, 2),
            RotationPlane.XW => (0, 3),
            RotationPlane.YZ => (1, 2),
            RotationPlane.YW => (1, 3),
            RotationPlane.ZW => (2, 3),
            _ => throw new System.ArgumentOutOfRangeException()
        };
    }
}
