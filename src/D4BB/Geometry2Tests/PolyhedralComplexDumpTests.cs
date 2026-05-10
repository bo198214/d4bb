using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NUnit.Framework;
using D4BB.Geometry;
using D4BB.Geometry2;

namespace D4BB.Geometry2Tests {

    /// Verifies the dump format produced by RotatingComplex.DumpState (Unity side) round-
    /// trips through PolyhedralComplexDumpJson.FromJson. Builds a synthetic dump string
    /// matching the runtime format, parses it, asserts every piece is recovered.
    public class PolyhedralComplexDumpTests {

        static string BuildSyntheticDump() {
            var c = Cube4dBuilder.UnitCubeAtW(0, cellNormal: new Point(0, 0, 0, 1));
            string complexJson = PolyhedralComplex4dJson.ToJson(c, name: "synthetic");
            var inv = CultureInfo.InvariantCulture;
            string F(double d) => d.ToString("R", inv);
            // Mirror RotatingComplex.DumpState: splice extras before the final '}'.
            var extras = new StringBuilder();
            extras.Append(",\n  \"camera\": { \"type\": \"Camera4dParallel\"")
                  .Append(", \"eye\": [").Append(F(0)).Append(", ").Append(F(0)).Append(", ").Append(F(0)).Append(", ").Append(F(-5)).Append("]")
                  .Append(", \"wDir\": [").Append(F(0.4)).Append(", ").Append(F(0.5)).Append(", ").Append(F(0.6)).Append("] }");
            extras.Append(",\n  \"angles\": [").Append(F(1.234)).Append(", ").Append(F(2.345)).Append("]");
            extras.Append(",\n  \"flags\": { \"useBsp\": true, \"applyCutOut\": false, \"backfaceCulling\": true, \"edgesVisible\": false }\n");
            int closeAt = complexJson.LastIndexOf('}');
            return complexJson.Substring(0, closeAt).TrimEnd() + extras + "}\n";
        }

        [Test] public void Dump_RoundTripsAllFields() {
            var dump = PolyhedralComplexDumpJson.FromJson(BuildSyntheticDump());

            Assert.That(dump.complex, Is.Not.Null);
            Assert.That(dump.complex.cells.Count, Is.EqualTo(1));
            Assert.That(dump.complex.vertices.Count, Is.EqualTo(8));

            Assert.That(dump.camera, Is.Not.Null);
            Assert.That(dump.camera.eye.x[0], Is.EqualTo(0).Within(AOP.ERR));
            Assert.That(dump.camera.eye.x[3], Is.EqualTo(-5).Within(AOP.ERR));
            Assert.That(dump.camera.wDir.x[0], Is.EqualTo(0.4).Within(AOP.ERR));
            Assert.That(dump.camera.wDir.x[1], Is.EqualTo(0.5).Within(AOP.ERR));
            Assert.That(dump.camera.wDir.x[2], Is.EqualTo(0.6).Within(AOP.ERR));

            Assert.That(dump.angle1, Is.EqualTo(1.234).Within(AOP.ERR));
            Assert.That(dump.angle2, Is.EqualTo(2.345).Within(AOP.ERR));

            Assert.That(dump.useBsp,          Is.True);
            Assert.That(dump.applyCutOut,     Is.False);
            Assert.That(dump.backfaceCulling, Is.True);
            Assert.That(dump.edgesVisible,    Is.False);
        }

        [Test] public void Dump_CameraSetCavalierMatchesWDir() {
            // The camera's wDir setter routes through SetCavalier when not in isometric mode.
            // After parsing, camera.wDir should equal the dumped wDir, and the projection
            // matrix should reflect it.
            var dump = PolyhedralComplexDumpJson.FromJson(BuildSyntheticDump());
            // SetCavalier(wDir) builds v[0]=(1,0,0,wx), v[1]=(0,1,0,wy), v[2]=(0,0,1,wz).
            Assert.That(dump.camera.v[0].x[3], Is.EqualTo(0.4).Within(AOP.ERR));
            Assert.That(dump.camera.v[1].x[3], Is.EqualTo(0.5).Within(AOP.ERR));
            Assert.That(dump.camera.v[2].x[3], Is.EqualTo(0.6).Within(AOP.ERR));
        }
    }
}
