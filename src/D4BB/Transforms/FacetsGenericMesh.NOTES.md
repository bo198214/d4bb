# FacetsGenericMesh — Hinweise für Per-Face-Overlay-Code

Diese Notiz richtet sich an Code, der **per-Vertex-Attribute aus Face-Metadaten** ableiten will (z. B. Overlay-UVs für Tiefenbalken/Wand-Streifen, Per-Face-Tönung, 4D-Tiefen-Hints). Die wichtigen, nicht-offensichtlichen Eigenschaften:

## Pro-Mesh-Vertex 4D-Information

- `vertices4d` (`List<double[]>`) — parallel zu `vertices`, gleicher Index. Jedes Element ist `double[4]` mit der **4D-Welt-Koordinate** des Vertex (Piece-Origin schon mit eingerechnet). Praktisch z. B. zum Ableiten face-lokaler UV-Koordinaten aus 4D-Position minus Face-Origin.

## Pro-Triangle Integer-Cell

- `debugPolyTriangles` (`List<Face2dWithIntegerCellAttribute>`) — ein Eintrag pro Triangle, in derselben Reihenfolge wie `triangles` sie referenziert (`triangles[3t..3t+2]` sind die drei Vertex-Indices des `t`-ten Triangles).
- `debugPolyTriangles[t].integerCell` gibt die `IntegerCell` der **2-Face**, aus der dieses Triangle stammt — mit `origin` (`int[]`, 4D-Welt-Koord der „Min-Ecke") und `span` (`HashSet<int>` mit den zwei Achsen-Indices, die die Face aufspannt; Komplement = die zwei fixen Achsen).

→ Test auf eine bestimmte Orientierung: `iFace.span.Contains(axisA) && iFace.span.Contains(axisB)`.

## Vertex-Dedup ist face-lokal, nicht mesh-global

Im Konstruktor (`vertexNumbers = new(new RawVertexEquality())`) wird **Referenz-Identität** als Schlüssel benutzt — nicht räumliche Koordinate. Dedup passiert nur, weil eine Fan-Triangulation **die gleichen Vertex-Objekte** für Centroid + Boundary-Vertices über mehrere Triangles derselben Face wiederverwendet.

**Konsequenz:** Zwei Faces, die im 4D-Raum einen Vertex teilen (z. B. zwei (x,y)-Faces eines Tesseracts an der gleichen Ecke), werden zwei **separate Mesh-Vertices** erzeugen, weil die Vertex-Objekte verschiedene Referenzen sind. → Ein Mesh-Vertex gehört zu **genau einer Face** → per-Vertex-Face-Attribut (Overlay-Kind, Face-Origin, …) ist wohldefiniert ohne Konflikt.

Das ist genau das, was man für „pro Face ein Overlay" haben will, aber es heißt auch: das Mesh ist **größer als ein optimal dedupliziertes** — kein Drama bei Tesserakt-Cells, aber gut zu wissen.

## Bei `withCenter = true`

Centroid-Triangulation statt Fan. Spielt für Face-Overlays keine Rolle (alle Triangles derselben Face behalten den gleichen `integerCell`), aber Vertex-Layout ist anders strukturiert.

## Bei `inset > 0`

`Face2dBC.Inset(inset)` modifiziert die Punkte — `vertices` werden eingerückt, aber `vertices4d` (`pos4d`) bleibt die **originale 4D-Position**. Das ist wichtig für Overlay-UV-Berechnung: man sollte 4D-zu-Face-lokal aus `vertices4d` ableiten, nicht aus den (möglicherweise inset-verzerrten) 3D-`vertices`.

## Was hier **nicht** verfügbar ist

- Keine Per-Face-Orientierungs-Lookup-Tabelle. Wer wissen will „welcher Mesh-Vertex gehört zu welcher Face mit welcher Orientierung", muss über `triangles[3t..]` + `debugPolyTriangles[t]` iterieren (siehe `Game.FaceOverlayUVs.NOTES.md` im Tesserian-Repo).
- `uvs` ist nur die statische Per-Face-UV aus `UVFromIntegerFace`/`UV`-Helpers (für die alte `_BaseMap`-Pipeline). Die Overlay-Metadaten nutzen einen anderen UV-Slot (UV3), nicht UV0.
