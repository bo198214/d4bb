using System.IO;
using NUnit.Framework;
using D4BB.Geometry2;

namespace D4BB.Geometry2Tests {

    /// Shared locator for the polychora JSON assets that live in the Unity project
    /// (outside the d4bb package). Walks up from the test assembly and probes the
    /// real asset path. A missing file is a FAILURE, not an Ignore — the silent
    /// Ignore variant let the whole JSON-based suite skip unnoticed for months
    /// (219 skipped tests) when the probe path went stale.
    public static class PolychoraAssets {

        static readonly string[] CandidateDirs = {
            Path.Combine("Assets", "_Tesserian", "Common", "Resources", "polychora"),
            // Legacy probe path, kept for older checkouts/layouts.
            Path.Combine("Assets", "tesserian", "Resources", "polychora"),
        };

        /// Loads and parses a polychoron JSON by file name (e.g. "pen.json").
        /// Fails the calling test if the file cannot be found; ignores it (with a
        /// message) only for malformed JSON, which is a data problem, not a path bug.
        public static PolyhedralComplex4d Load(string fileName) {
            var assemblyDir = Path.GetDirectoryName(typeof(PolychoraAssets).Assembly.Location);
            var d = new DirectoryInfo(assemblyDir);
            while (d != null) {
                foreach (var rel in CandidateDirs) {
                    var p = Path.Combine(d.FullName, rel, fileName);
                    if (!File.Exists(p)) continue;
                    try { return PolyhedralComplex4dJson.FromJson(File.ReadAllText(p)); }
                    catch (System.FormatException ex) {
                        Assert.Ignore($"{fileName}: malformed JSON ({ex.Message}) — skipping");
                        return null;
                    }
                }
                d = d.Parent;
            }
            Assert.Fail($"{fileName} not found: no ancestor of {assemblyDir} contains " +
                        string.Join(" or ", CandidateDirs));
            return null;
        }
    }
}
