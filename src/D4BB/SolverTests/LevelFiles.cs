using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace D4BB.SolverTests
{
    /// <summary>
    /// Locates the game's level JSONs from inside the (Unity-independent) test assembly: walk up
    /// from the assembly location until an ancestor contains the levels folder — the same trick
    /// <c>Geometry2Tests.PolychoraAssets</c> uses, and the reason these tests run both under
    /// <c>dotnet test</c> in <c>Packages/d4bb</c> and in the Unity Editor test runner.
    /// </summary>
    public static class LevelFiles
    {
        static readonly string RelativeLevelsDir =
            Path.Combine("Assets", "_Tesserian", "Game", "levels");

        public static string Directory()
        {
            var assemblyDir = Path.GetDirectoryName(typeof(LevelFiles).Assembly.Location);
            var d = new DirectoryInfo(assemblyDir);
            while (d != null)
            {
                var candidate = Path.Combine(d.FullName, RelativeLevelsDir);
                if (System.IO.Directory.Exists(candidate)) return candidate;
                d = d.Parent;
            }
            return null;
        }

        /// <summary>
        /// Every level file, recursively. Excludes the JSON schema and the <c>test/</c> subtree,
        /// whose files are render/colour fixtures rather than puzzles (a one-cell goal with four
        /// one-cell pieces is unsolvable on purpose).
        /// </summary>
        public static IEnumerable<string> All()
        {
            var dir = Directory();
            if (dir == null) return Enumerable.Empty<string>();
            return System.IO.Directory
                .EnumerateFiles(dir, "*.json", SearchOption.AllDirectories)
                .Where(p => Path.GetFileName(p) != "level.schema.json")
                .Where(p => !RelativeTo(dir, p).Split(Path.DirectorySeparatorChar)
                                              .Contains("test", StringComparer.OrdinalIgnoreCase))
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase);
        }

        public static string RelativeTo(string dir, string path)
            => path.StartsWith(dir, StringComparison.OrdinalIgnoreCase)
                ? path.Substring(dir.Length).TrimStart(Path.DirectorySeparatorChar,
                                                       Path.AltDirectorySeparatorChar)
                : path;

        /// <summary>One NUnit case per level, named by its path relative to the levels folder.</summary>
        public static IEnumerable Cases()
        {
            var dir = Directory();
            if (dir == null)
            {
                yield return new TestCaseData((string)null).SetName("levels folder not found");
                yield break;
            }
            foreach (var path in All())
                yield return new TestCaseData(path).SetName(RelativeTo(dir, path));
        }
    }
}
