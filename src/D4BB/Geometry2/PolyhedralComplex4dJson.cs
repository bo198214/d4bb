using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using D4BB.Geometry;

namespace D4BB.Geometry2 {

    /// JSON serialization for PolyhedralComplex4d compatible with the
    /// Assets/tesserian/Resources/polychora/*.json schema.
    ///
    /// Schema:
    ///   {
    ///     "name":        string                          (optional)
    ///     "description": string                          (optional)
    ///     "vertices":    [[x,y,z,w], ...]                4D coords
    ///     "edges":       [[v0,v1], ...]                  vertex indices
    ///     "faces2d":     [[v0,v1,...], ...]              vertex indices in cyclic order around the polygon
    ///     "cells":       [[v0,v1,...], ...]              vertex indices per cell (sorted, redundant — derived from cell_faces)
    ///     "cell_faces":  [[f0,f1,...], ...]              face indices per cell
    ///     "normals":     [[nx,ny,nz,nw] | null, ...]     outward 4D normal per cell, or null (optional whole field)
    ///     "marked_faces":[i, j, ...]                     2-face indices to highlight (optional)
    ///   }
    public static class PolyhedralComplex4dJson {

        public static string ToJson(PolyhedralComplex4d complex, string name = null, string description = null) {
            var sb = new StringBuilder();
            sb.Append("{\n");
            if (!string.IsNullOrEmpty(name))        sb.Append("  \"name\": \"").Append(Escape(name)).Append("\",\n");
            if (!string.IsNullOrEmpty(description)) sb.Append("  \"description\": \"").Append(Escape(description)).Append("\",\n");

            WriteArrayOfPoints(sb, "vertices", complex.vertices);
            sb.Append(",\n");

            sb.Append("  \"edges\": [");
            for (int i = 0; i < complex.edges.Count; i++) {
                if (i > 0) sb.Append(",");
                sb.Append("\n    [").Append(complex.edges[i].v0).Append(", ").Append(complex.edges[i].v1).Append("]");
            }
            sb.Append("\n  ],\n");

            sb.Append("  \"faces2d\": [");
            for (int i = 0; i < complex.faces.Count; i++) {
                if (i > 0) sb.Append(",");
                sb.Append("\n    ");
                WriteIntArray(sb, complex.FaceVertexIds(i));
            }
            sb.Append("\n  ],\n");

            sb.Append("  \"cells\": [");
            for (int i = 0; i < complex.cells.Count; i++) {
                if (i > 0) sb.Append(",");
                sb.Append("\n    ");
                var vIds = complex.CellVertexIds(i).ToList();
                vIds.Sort();
                WriteIntArray(sb, vIds.ToArray());
            }
            sb.Append("\n  ],\n");

            sb.Append("  \"cell_faces\": [");
            for (int i = 0; i < complex.cells.Count; i++) {
                if (i > 0) sb.Append(",");
                sb.Append("\n    ");
                WriteIntArray(sb, complex.cells[i].faceIds);
            }
            sb.Append("\n  ]");

            if (complex.cells.Any(c => c.normal != null)) {
                sb.Append(",\n  \"normals\": [");
                for (int i = 0; i < complex.cells.Count; i++) {
                    if (i > 0) sb.Append(",");
                    sb.Append("\n    ");
                    var n = complex.cells[i].normal;
                    if (n == null) sb.Append("null");
                    else WritePoint(sb, n);
                }
                sb.Append("\n  ]");
            }

            if (complex.markedFaces.Count > 0) {
                sb.Append(",\n  \"marked_faces\": ");
                WriteIntArray(sb, complex.markedFaces.OrderBy(i => i).ToArray());
            }

            sb.Append("\n}\n");
            return sb.ToString();
        }

        public static PolyhedralComplex4d FromJson(string text) {
            var json = (Dictionary<string, object>)MiniJson.Parse(text);

            var rawVerts = (List<object>)json["vertices"];
            var vertices = new List<Point>(rawVerts.Count);
            foreach (var v in rawVerts) {
                var arr = (List<object>)v;
                if (arr.Count != 4) throw new FormatException($"Vertex must have 4 coords, got {arr.Count}");
                vertices.Add(new Point(new[] { ToDouble(arr[0]), ToDouble(arr[1]), ToDouble(arr[2]), ToDouble(arr[3]) }));
            }

            var rawEdges = (List<object>)json["edges"];
            var edges = new List<Edge>(rawEdges.Count);
            var edgeMap = new Dictionary<(int, int), int>();
            foreach (var e in rawEdges) {
                var arr = (List<object>)e;
                int v0 = ToInt(arr[0]);
                int v1 = ToInt(arr[1]);
                edges.Add(new Edge(v0, v1));
                int lo = Math.Min(v0, v1), hi = Math.Max(v0, v1);
                edgeMap[(lo, hi)] = edges.Count - 1;
            }

            var rawFaces = (List<object>)json["faces2d"];
            var faces = new List<Face>(rawFaces.Count);
            foreach (var f in rawFaces) {
                var arr = (List<object>)f;
                int n = arr.Count;
                if (n < 3) throw new FormatException($"Face must have at least 3 vertices, got {n}");
                var verts = new int[n];
                for (int k = 0; k < n; k++) verts[k] = ToInt(arr[k]);
                // Many existing JSON files store face vertices as a SORTED SET rather than as
                // a cyclic ordering around the polygon. For triangles every permutation is a
                // valid cycle (every pair shares an edge), but for ≥ 4-gons the sorted form
                // typically references non-existent diagonals. Try the as-given order first;
                // if that fails, reconstruct the cycle from the edge graph restricted to the
                // vertex set (each polygon vertex has exactly 2 neighbors → unique cycle up
                // to direction, which is fine for our consumers).
                if (!TryBuildFaceEdgeIds(verts, edgeMap, out var faceEdgeIds)) {
                    var reordered = ReconstructPolygonCycle(verts, edgeMap)
                        ?? throw new FormatException(
                            $"Face vertices {string.Join(",", verts)} cannot be ordered as a polygon: " +
                            "neither the given order nor any reconstruction matches edges in 'edges'");
                    if (!TryBuildFaceEdgeIds(reordered, edgeMap, out faceEdgeIds))
                        throw new FormatException(
                            $"Face vertices {string.Join(",", verts)} reordered to {string.Join(",", reordered)} " +
                            "but edge lookup still fails");
                }
                faces.Add(new Face(faceEdgeIds));
            }

            var rawCellFaces = (List<object>)json["cell_faces"];
            List<object> rawNormals = null;
            if (json.TryGetValue("normals", out var nObj) && nObj is List<object> ln) rawNormals = ln;

            var cells = new List<Cell>(rawCellFaces.Count);
            for (int i = 0; i < rawCellFaces.Count; i++) {
                var fList = (List<object>)rawCellFaces[i];
                var fIds = new int[fList.Count];
                for (int k = 0; k < fList.Count; k++) fIds[k] = ToInt(fList[k]);
                Point normal = null;
                if (rawNormals != null && i < rawNormals.Count && rawNormals[i] != null) {
                    var arr = (List<object>)rawNormals[i];
                    if (arr.Count != 4) throw new FormatException($"Normal must have 4 coords, got {arr.Count}");
                    normal = new Point(new[] { ToDouble(arr[0]), ToDouble(arr[1]), ToDouble(arr[2]), ToDouble(arr[3]) });
                }
                cells.Add(new Cell(fIds, normal));
            }

            var complex = new PolyhedralComplex4d(vertices, edges, faces, cells);
            if (json.TryGetValue("marked_faces", out var mfObj) && mfObj is List<object> mf) {
                foreach (var x in mf) complex.markedFaces.Add(ToInt(x));
            }
            return complex;
        }

        // ── face cycle reconstruction (reader) ─────────────────────────────────

        static bool TryBuildFaceEdgeIds(int[] verts, Dictionary<(int,int), int> edgeMap, out int[] faceEdgeIds) {
            int n = verts.Length;
            faceEdgeIds = new int[n];
            for (int k = 0; k < n; k++) {
                int a = verts[k], b = verts[(k + 1) % n];
                int lo = Math.Min(a, b), hi = Math.Max(a, b);
                if (!edgeMap.TryGetValue((lo, hi), out int eId)) { faceEdgeIds = null; return false; }
                faceEdgeIds[k] = eId;
            }
            return true;
        }

        /// Given a polygon's vertex set (in arbitrary order) and the global edge map,
        /// reconstruct a cyclic ordering whose consecutive pairs are all edges. Returns
        /// null if the induced subgraph on `verts` is not a single cycle (every vertex must
        /// have exactly 2 neighbors within the set). The recovered direction is arbitrary
        /// — for our consumers (renderer doesn't depend on outward orientation, face plane
        /// is direction-independent) this is acceptable.
        static int[] ReconstructPolygonCycle(int[] verts, Dictionary<(int,int), int> edgeMap) {
            int n = verts.Length;
            var nbrs = new Dictionary<int, List<int>>(n);
            foreach (var v in verts) nbrs[v] = new List<int>(2);
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++) {
                    int a = verts[i], b = verts[j];
                    int lo = Math.Min(a, b), hi = Math.Max(a, b);
                    if (edgeMap.ContainsKey((lo, hi))) { nbrs[a].Add(b); nbrs[b].Add(a); }
                }
            foreach (var kv in nbrs) if (kv.Value.Count != 2) return null;
            var ordered = new int[n];
            int start = verts[0];
            int prev = -1, cur = start;
            for (int i = 0; i < n; i++) {
                ordered[i] = cur;
                var ns = nbrs[cur];
                int next = ns[0] == prev ? ns[1] : ns[0];
                prev = cur;
                cur = next;
            }
            if (cur != start) return null;
            return ordered;
        }

        // ── writer helpers ─────────────────────────────────────────────────────

        static void WriteArrayOfPoints(StringBuilder sb, string key, List<Point> points) {
            sb.Append("  \"").Append(key).Append("\": [");
            for (int i = 0; i < points.Count; i++) {
                if (i > 0) sb.Append(",");
                sb.Append("\n    ");
                WritePoint(sb, points[i]);
            }
            sb.Append("\n  ]");
        }

        static void WritePoint(StringBuilder sb, Point p) {
            sb.Append("[");
            for (int k = 0; k < p.x.Length; k++) {
                if (k > 0) sb.Append(", ");
                sb.Append(FormatDouble(p.x[k]));
            }
            sb.Append("]");
        }

        static void WriteIntArray(StringBuilder sb, int[] a) {
            sb.Append("[");
            for (int k = 0; k < a.Length; k++) {
                if (k > 0) sb.Append(", ");
                sb.Append(a[k]);
            }
            sb.Append("]");
        }

        static string FormatDouble(double d) {
            // "R" gives the shortest string that round-trips to the exact double.
            return d.ToString("R", CultureInfo.InvariantCulture);
        }

        static string Escape(string s) =>
            s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");

        // ── reader conversion ──────────────────────────────────────────────────

        static int ToInt(object o) {
            switch (o) {
                case int i: return i;
                case long l: return (int)l;
                case double d: return (int)d;
                case string s: return int.Parse(s, CultureInfo.InvariantCulture);
                default: throw new FormatException($"Cannot convert {o?.GetType().Name ?? "null"} to int");
            }
        }

        static double ToDouble(object o) {
            switch (o) {
                case double d: return d;
                case int i: return i;
                case long l: return l;
                case string s: return double.Parse(s, CultureInfo.InvariantCulture);
                default: throw new FormatException($"Cannot convert {o?.GetType().Name ?? "null"} to double");
            }
        }
    }

    /// Minimal recursive-descent JSON parser. Supports objects, arrays, strings, numbers, true/false/null.
    /// Numbers come back as System.Double; objects as Dictionary<string,object>; arrays as List<object>.
    internal static class MiniJson {
        public static object Parse(string text) {
            int p = 0;
            SkipWs(text, ref p);
            return ParseValue(text, ref p);
        }

        static object ParseValue(string s, ref int p) {
            SkipWs(s, ref p);
            if (p >= s.Length) throw new FormatException("Unexpected end of JSON");
            char c = s[p];
            if (c == '{') return ParseObject(s, ref p);
            if (c == '[') return ParseArray(s, ref p);
            if (c == '"') return ParseString(s, ref p);
            if (c == 'n' || c == 't' || c == 'f') return ParseLiteral(s, ref p);
            return ParseNumber(s, ref p);
        }

        static Dictionary<string, object> ParseObject(string s, ref int p) {
            var d = new Dictionary<string, object>();
            p++;  // '{'
            SkipWs(s, ref p);
            while (s[p] != '}') {
                string key = ParseString(s, ref p);
                SkipWs(s, ref p);
                if (s[p] != ':') throw new FormatException($"Expected ':' at position {p}");
                p++;
                d[key] = ParseValue(s, ref p);
                SkipWs(s, ref p);
                if (s[p] == ',') { p++; SkipWs(s, ref p); }
            }
            p++;  // '}'
            return d;
        }

        static List<object> ParseArray(string s, ref int p) {
            var list = new List<object>();
            p++;  // '['
            SkipWs(s, ref p);
            while (s[p] != ']') {
                list.Add(ParseValue(s, ref p));
                SkipWs(s, ref p);
                if (s[p] == ',') { p++; SkipWs(s, ref p); }
            }
            p++;  // ']'
            return list;
        }

        static string ParseString(string s, ref int p) {
            if (s[p] != '"') throw new FormatException($"Expected '\"' at position {p}");
            p++;
            var sb = new StringBuilder();
            while (s[p] != '"') {
                if (s[p] == '\\' && p + 1 < s.Length) {
                    p++;
                    switch (s[p]) {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            sb.Append((char)int.Parse(s.Substring(p + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            p += 4;
                            break;
                        default: sb.Append(s[p]); break;
                    }
                    p++;
                } else {
                    sb.Append(s[p++]);
                }
            }
            p++;  // closing '"'
            return sb.ToString();
        }

        static double ParseNumber(string s, ref int p) {
            int start = p;
            if (s[p] == '-' || s[p] == '+') p++;
            while (p < s.Length && (char.IsDigit(s[p]) || s[p] == '.')) p++;
            if (p < s.Length && (s[p] == 'e' || s[p] == 'E')) {
                p++;
                if (p < s.Length && (s[p] == '-' || s[p] == '+')) p++;
                while (p < s.Length && char.IsDigit(s[p])) p++;
            }
            return double.Parse(s.Substring(start, p - start), CultureInfo.InvariantCulture);
        }

        static object ParseLiteral(string s, ref int p) {
            if (p + 4 <= s.Length && s.Substring(p, 4) == "null") { p += 4; return null; }
            if (p + 4 <= s.Length && s.Substring(p, 4) == "true") { p += 4; return true; }
            if (p + 5 <= s.Length && s.Substring(p, 5) == "false") { p += 5; return false; }
            throw new FormatException($"Unexpected literal at position {p}");
        }

        static void SkipWs(string s, ref int p) {
            while (p < s.Length && (s[p] == ' ' || s[p] == '\t' || s[p] == '\n' || s[p] == '\r')) p++;
        }
    }
}
