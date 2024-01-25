using System.Collections.Generic;

namespace D4BB.Geometry
{
	public class Factory<T> {
		protected HashSet<T> existingElements = new();
		public bool IsUsed() {
			return false;
		}
		public T AddIfNotContained(T newElement) {
			if (IsUsed()) {
				foreach (var existingElement in existingElements) {
					if (existingElement!=null) {
						if (newElement.Equals(existingElement)) {
							return existingElement;
						}
					}
				}
				existingElements.Add(newElement);
			}
			return newElement;
		}
	}
	public class PolyhedronFactory {
		protected Dictionary<int,Factory<Polyhedron>> existingElementsByDim = new();
		public bool IsUsed() {
			return existingElementsByDim[0].IsUsed();
		}
		public PolyhedronFactory() {
			existingElementsByDim[0] = new Factory<Polyhedron>();
		}
		public Polyhedron AddIfNotContained(Polyhedron newElement) {
			var d = newElement.Dim();
			if (!existingElementsByDim.ContainsKey(d)) { existingElementsByDim[d] = new Factory<Polyhedron>(); }
			var factory = existingElementsByDim[d];
			return factory.AddIfNotContained(newElement);
		}
	}
}