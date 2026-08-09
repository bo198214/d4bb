using System.IO;
using NUnit.Framework;
using D4BB.Comb;
using D4BB.Geometry2;

namespace D4BB.Geometry2Tests {

    /// One-off generators that write polychora JSON files into the live Assets folder.
    /// Marked [Explicit] so they don't run during the normal test suite — invoke
    /// explicitly via `dotnet test --filter "FullyQualifiedName~PolychoraGenerators"`.
    [Explicit]
    public class PolychoraGenerators {

        [Test] public void Generate_L_3Tesseracts() {
            var tesseracts = new[] {
                new IntegerCell(new[] {  0, -1, 0, 0 }),
                new IntegerCell(new[] { -1, -1, 0, 0 }),
                new IntegerCell(new[] {  0,  0, 0, 0 }),
            };
            var complex = IntegerComplex4dBuilder.Boundary(tesseracts);
            var json = PolyhedralComplex4dJson.ToJson(complex,
                name: "L",
                description: "L-shaped 3-tesseract piece (cells [0,-1,0,0], [-1,-1,0,0], [0,0,0,0])");

            // Locate the polychora asset directory relative to the test assembly.
            var dir = new DirectoryInfo(Path.GetDirectoryName(typeof(PolychoraGenerators).Assembly.Location));
            string outPath = null;
            while (dir != null && outPath == null) {
                foreach (var rel in new[] {
                        // Where the generated L.json actually lives: the nonconvex Complex-mode
                        // assets, which — unlike the uniform polychora — stay inside Resources.
                        Path.Combine("Assets", "_Tesserian", "RotatingPolychoron", "Resources", "complexes"),
                        // Legacy layouts.
                        Path.Combine("Assets", "_Tesserian", "Common", "Resources", "polychora"),
                        Path.Combine("Assets", "tesserian", "Resources", "polychora") }) {
                    var candidate = Path.Combine(dir.FullName, rel);
                    if (Directory.Exists(candidate)) {
                        outPath = Path.Combine(candidate, "L.json");
                        break;
                    }
                }
                dir = dir.Parent;
            }
            Assert.That(outPath, Is.Not.Null, "polychora directory not found in any ancestor of the test assembly");

            File.WriteAllText(outPath, json);
            TestContext.WriteLine($"wrote {outPath}");
            TestContext.WriteLine($"  vertices: {complex.vertices.Count}");
            TestContext.WriteLine($"  edges:    {complex.edges.Count}");
            TestContext.WriteLine($"  faces:    {complex.faces.Count}");
            TestContext.WriteLine($"  cells:    {complex.cells.Count}");
        }
    }
}
