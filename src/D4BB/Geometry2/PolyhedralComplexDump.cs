using System;
using System.Collections.Generic;
using System.Globalization;
using D4BB.Geometry;

namespace D4BB.Geometry2 {

    /// Snapshot of RotatingComplex's per-frame state, captured on un-pause. Lets a test
    /// reproduce the exact constellation that was on screen — no need to re-derive the
    /// rotation, since the polychoron's vertices and cell normals are already rotated.
    ///
    /// File schema: standard PolyhedralComplex4dJson (vertices/edges/faces2d/cells/...) plus
    /// three additional top-level keys:
    ///   "camera": { "type": "Camera4dParallel", "eye": [x,y,z,w], "wDir": [x,y,z] }
    ///   "angles": [angle1, angle2]    — XY/ZW rotation angles for documentation only
    ///   "flags":  { "useBsp": bool, "applyCutOut": bool, "backfaceCulling": bool,
    ///               "edgesVisible": bool,
    ///               "showCoplanarOriginalEdges": bool, "debugShowCoplanarCutEdges": bool }
    public class PolyhedralComplexDump {
        public PolyhedralComplex4d complex;
        public Camera4dParallel camera;
        public double angle1;
        public double angle2;
        public bool useBsp;
        public bool applyCutOut;
        public bool backfaceCulling;
        public bool edgesVisible;
        public bool showCoplanarOriginalEdges;
        public bool debugShowCoplanarCutEdges;
    }

    public static class PolyhedralComplexDumpJson {

        public static PolyhedralComplexDump FromJson(string text) {
            var dump = new PolyhedralComplexDump();
            dump.complex = PolyhedralComplex4dJson.FromJson(text);

            var json = (Dictionary<string, object>)MiniJson.Parse(text);

            if (json.TryGetValue("camera", out var camObj) && camObj is Dictionary<string, object> cam) {
                var wDir = ReadPoint3(cam, "wDir");
                dump.camera = new Camera4dParallel();
                if (wDir != null) dump.camera.SetCavalier(wDir);
                // Legacy dumps may carry an "eye" field; it's meaningless for
                // Camera4dParallel (parallel projection has no eye) and is ignored.
            }

            if (json.TryGetValue("angles", out var anglesObj) && anglesObj is List<object> angles && angles.Count >= 2) {
                dump.angle1 = ToDouble(angles[0]);
                dump.angle2 = ToDouble(angles[1]);
            }

            if (json.TryGetValue("flags", out var flagsObj) && flagsObj is Dictionary<string, object> flags) {
                dump.useBsp                     = ReadBool(flags, "useBsp");
                dump.applyCutOut                = ReadBool(flags, "applyCutOut");
                dump.backfaceCulling            = ReadBool(flags, "backfaceCulling", defaultValue: true);
                dump.edgesVisible               = ReadBool(flags, "edgesVisible",    defaultValue: true);
                dump.showCoplanarOriginalEdges  = ReadBool(flags, "showCoplanarOriginalEdges");
                dump.debugShowCoplanarCutEdges  = ReadBool(flags, "debugShowCoplanarCutEdges");
            }

            return dump;
        }

        static Point4d ReadPoint4(Dictionary<string, object> obj, string key) {
            if (!obj.TryGetValue(key, out var v) || !(v is List<object> arr) || arr.Count != 4)
                throw new FormatException($"Camera dump missing 4D '{key}' field");
            return new Point4d(ToDouble(arr[0]), ToDouble(arr[1]), ToDouble(arr[2]), ToDouble(arr[3]));
        }

        static Point3d ReadPoint3(Dictionary<string, object> obj, string key) {
            if (!obj.TryGetValue(key, out var v) || !(v is List<object> arr) || arr.Count != 3) return null;
            return new Point3d(ToDouble(arr[0]), ToDouble(arr[1]), ToDouble(arr[2]));
        }

        static bool ReadBool(Dictionary<string, object> obj, string key, bool defaultValue = false) {
            if (!obj.TryGetValue(key, out var v)) return defaultValue;
            if (v is bool b) return b;
            return defaultValue;
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
}
