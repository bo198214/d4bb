using System;
using System.Collections.Generic;
using D4BB.Geometry;
using Edge = D4BB.Geometry2.Edge;   // dimension-free index pair (D4BB.Geometry has an unrelated Edge)

namespace D4BB.Geometry3d {

    /// Several boundary complexes merged into ONE PolyhedralComplex3d with per-part rigid
    /// poses — the 3D library form of the merge/pose pattern the 4D bake uses
    /// (BakeLAssemble2SequenceEditor.AppendComplex/ApplyPose): the original vertices and
    /// face normals are snapshotted at merge time, and SetPose rewrites a part's current
    /// vertices/normals from those originals (never accumulating), so poses can be applied
    /// every frame without drift. Consumers hand <see cref="complex"/> to
    /// RenderPipeline3d; caches are invalidated on every pose change.
    ///
    /// The merged complex CLONES all geometry; the input parts stay untouched.
    public class PosedComplexes {
        public readonly PolyhedralComplex3d complex;
        readonly (int start, int count)[] vertexRanges;
        readonly (int start, int count)[] faceRanges;
        readonly double[][] originalVerts;     // per merged vertex: snapshot of x[0..2]
        readonly double[][] originalNormals;   // per merged face: snapshot or null

        public int PartCount => vertexRanges.Length;

        PosedComplexes(PolyhedralComplex3d complex,
                       (int start, int count)[] vertexRanges,
                       (int start, int count)[] faceRanges) {
            this.complex = complex;
            this.vertexRanges = vertexRanges;
            this.faceRanges = faceRanges;
            originalVerts = new double[complex.vertices.Count][];
            for (int i = 0; i < complex.vertices.Count; i++)
                originalVerts[i] = (double[])complex.vertices[i].x.Clone();
            originalNormals = new double[complex.faces.Count][];
            for (int i = 0; i < complex.faces.Count; i++)
                originalNormals[i] = complex.faces[i].normal != null
                    ? (double[])complex.faces[i].normal.x.Clone() : null;
        }

        public static PosedComplexes Merge(IList<PolyhedralComplex3d> parts) {
            var merged = new PolyhedralComplex3d();
            var vRanges = new (int, int)[parts.Count];
            var fRanges = new (int, int)[parts.Count];
            for (int p = 0; p < parts.Count; p++) {
                var part = parts[p];
                int vOff = merged.vertices.Count;
                int eOff = merged.edges.Count;
                vRanges[p] = (vOff, part.vertices.Count);
                fRanges[p] = (merged.faces.Count, part.faces.Count);
                foreach (var v in part.vertices) merged.vertices.Add(v.clone());
                foreach (var e in part.edges) merged.edges.Add(new Edge(e.v0 + vOff, e.v1 + vOff));
                foreach (var face in part.faces) {
                    var edgeIds = new int[face.edgeIds.Length];
                    for (int k = 0; k < edgeIds.Length; k++) edgeIds[k] = face.edgeIds[k] + eOff;
                    merged.faces.Add(new Face3d(edgeIds, face.normal?.clone()));
                }
            }
            return new PosedComplexes(merged, vRanges, fRanges);
        }

        public static PosedComplexes Single(PolyhedralComplex3d part) =>
            Merge(new List<PolyhedralComplex3d> { part });

        /// Bakes an affine pre-transform x → scale·x + shift into a part's ORIGINAL
        /// vertices (normals are untouched — a uniform positive scale plus translation
        /// preserves directions) and re-applies the identity pose. Use it once after
        /// Merge to center/scale a part (e.g. a unit cube to a centered box of side s)
        /// before per-frame SetPose calls.
        public void BakeOriginalTransform(int part, double scale, double[] shift) {
            if (scale <= 0) throw new ArgumentException("scale must be positive");
            var (vStart, vCount) = vertexRanges[part];
            for (int k = vStart; k < vStart + vCount; k++) {
                var o = originalVerts[k];
                for (int c = 0; c < 3; c++) o[c] = scale * o[c] + (shift != null ? shift[c] : 0.0);
                Array.Copy(o, complex.vertices[k].x, 3);
            }
            complex.InvalidateCaches();
        }

        /// Rigid pose for one part: x' = R·(x_original − pivot) + pivot + offset, normals
        /// n' = R·n_original. `rot` is a row-major 3×3 matrix (x' = R·x); `pivot` and
        /// `offset` are 3D (either may be null = zero). Recomputed from the originals —
        /// call every frame with the absolute pose, not with deltas.
        public void SetPose(int part, double[] rot, double[] pivot, double[] offset) {
            if (rot == null || rot.Length != 9)
                throw new ArgumentException("rot must be a row-major 3×3 matrix (9 doubles)");
            double p0 = pivot != null ? pivot[0] : 0, p1 = pivot != null ? pivot[1] : 0, p2 = pivot != null ? pivot[2] : 0;
            double t0 = p0 + (offset != null ? offset[0] : 0);
            double t1 = p1 + (offset != null ? offset[1] : 0);
            double t2 = p2 + (offset != null ? offset[2] : 0);

            var (vStart, vCount) = vertexRanges[part];
            for (int k = vStart; k < vStart + vCount; k++) {
                var o = originalVerts[k];
                var x = complex.vertices[k].x;
                double d0 = o[0] - p0, d1 = o[1] - p1, d2 = o[2] - p2;
                x[0] = rot[0] * d0 + rot[1] * d1 + rot[2] * d2 + t0;
                x[1] = rot[3] * d0 + rot[4] * d1 + rot[5] * d2 + t1;
                x[2] = rot[6] * d0 + rot[7] * d1 + rot[8] * d2 + t2;
            }
            var (fStart, fCount) = faceRanges[part];
            for (int k = fStart; k < fStart + fCount; k++) {
                var o = originalNormals[k];
                if (o == null) continue;
                var n = complex.faces[k].normal.x;
                n[0] = rot[0] * o[0] + rot[1] * o[1] + rot[2] * o[2];
                n[1] = rot[3] * o[0] + rot[4] * o[1] + rot[5] * o[2];
                n[2] = rot[6] * o[0] + rot[7] * o[1] + rot[8] * o[2];
            }
            complex.InvalidateCaches();
        }

        public static readonly double[] IdentityRot = { 1, 0, 0, 0, 1, 0, 0, 0, 1 };

        /// Row-major 3×3 rotation by `angle` (radians) about the unit `axis` (Rodrigues).
        public static double[] AxisAngleRot(double[] axis, double angle) {
            double c = Math.Cos(angle), s = Math.Sin(angle), t = 1 - c;
            double ax = axis[0], ay = axis[1], az = axis[2];
            return new[] {
                t*ax*ax + c,    t*ax*ay - s*az, t*ax*az + s*ay,
                t*ax*ay + s*az, t*ay*ay + c,    t*ay*az - s*ax,
                t*ax*az - s*ay, t*ay*az + s*ax, t*az*az + c,
            };
        }
    }
}
