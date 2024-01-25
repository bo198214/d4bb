using System.Collections.Generic;
using System.Linq;

namespace D4BB.General {
    public static class DictionaryHelper {
        public static void AddToValue<S,T>(this IDictionary<S,List<T>> registry, S ic, T element) {
            if (!registry.TryGetValue(ic, out List<T> value)) registry[ic] = new List<T>(){element};
            else                                                        value.Add(element);
        }
        public static bool DictEqual<S,T>(this Dictionary<S,T> dict1, Dictionary<S,T> dict2) {
            if (dict1.Count!=dict2.Count) return false;
            if (dict1.Except(dict2).Any()) return false;
            return true;
        } 
        public static bool DictEqual<S,T,U>(this
            Dictionary<S, Dictionary<T,U>> dict1,
            Dictionary<S, Dictionary<T,U>> dict2) {
            if (dict1.Count != dict2.Count) return false;
            if (dict1.Keys.Except(dict2.Keys).Any()) return false;
            foreach (var key in dict1.Keys) {
                if (!DictEqual(dict1[key],dict2[key])) return false;
            }
            return true;
        }
    }
}