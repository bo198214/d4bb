# Notizen zum Bug "sporadisch fehlende 4D-gehende Innenflächen"

Begleitende Notizen zu `Scene4dHashTests.cs`. **Bug ist behoben** — siehe unten.

## Symptom

Beim Drücken/Halten des A-Knopfs (siehe [Game.cs:329](Assets/tesserian/scenarios/Game.cs#L329)) sind sporadisch innere Flächen verschwunden, die schräg in die 4. Dimension gehen — also 2-Faces mit Span enthält Achse 3, die zwischen je zwei der drei sichtbaren 3-Cells `c3_a/b/c` eines Einheitstesserakts geteilt sind.

## Wurzelursache

`CutOut` in [`PolyhedronBoundaryComplex.cs`](Packages/d4bb/src/D4BB/Transforms/PolyhedronBoundaryComplex.cs) behandelt 2-Faces, die *exakt auf* einer der schneidenden Halbebenen liegen, als "innen drin" (via `cs <= 0` in `ClipConvexPolygon` und der `out_inner.Add(facet)`-Zweig in `Split` für `isContained`-Faces). Das ist die Konvention für **Boolean-Difference** ("ziehe Volumen B von A ab — die berührende Wand ist Teil des Abzugs").

`ApplyCameraOcclusion` benutzt aber `CutOut` für **Occlusion** ("verdecke alles, was strikt im Inneren des Verdeckers liegt — die Vorderseite des Verdeckers bleibt sichtbar"). Beim 4D-Tesserakt teilen sich angrenzende 3-Cells eine 2-Face F. Wenn der Painter's-Algorithmus die beiden Cells in eine Tiefen-Reihenfolge bringt (was er bei jedem nicht-perfekt-symmetrischen Kamerawinkel tut, also fast immer), schneidet der Vorder-Zell ihre Halbebenen aus dem Hinter-Zell heraus. F liegt auf einer dieser Halbebenen → Boolean-Difference-Semantik klassifiziert sie als "drinnen" → entfernt.

Dazu kommt: F ist in genau *einer* der beiden Cells gespeichert (Dedup in `RebuildCellsFromPieceTopology` gibt sie nur dem zuerst-iterierten Owner). Wenn F im *Hinter-Zell* gelandet ist, wird sie also komplett gelöscht. Im *Vorder-Zell* hätte sie überlebt — Owner-Auswahl ist aber kamera-unabhängig.

Verstärkend: `Scene4d.UpdateCamera()` rief `ApplyCameraOcclusion()` ohne vorheriges `RebuildCellsFromTopologies()` auf, sodass jeder Frame die `pbc.d2faces` *kumulativ* erodierte und die `Face2dBC.points` (Build-Zeitpunkt-Kamera) gegen die Halbebenen (Aktuelle-Kamera) inkonsistent machte.

## Fix (umgesetzt)

### 1. `CutOut` bekommt `interiorOnly`-Parameter (Default `true`)

`Polyhedron3dBoundaryComplex.CutOut` und `Polyhedron3dBoundaryComplex.Split` haben jetzt einen `bool interiorOnly = true`. In Occlusion-Semantik (Default) werden `isContained`-Faces auf die *outer*-Seite gelegt — also bewahrt. Die alten Tests `CutOutTest_Half`, `CutOutTest_L`, `NeighborCut` benutzen jetzt explizit `interiorOnly: false`, um die alte Boolean-Difference-Semantik zu testen.

```csharp
// PolyhedronBoundaryComplex.cs
public void CutOut(HalfSpace[] halfSpaces, bool interiorOnly = true) { ... }
public static void Split(HalfSpace halfSpace, IEnumerable<Face2dBC> facets,
                          List<Face2dBC> out_inner, List<Face2dBC> out_outer,
                          bool interiorOnly = true) {
    foreach (var facet in facets) {
        var split = facet.Split(halfSpace);
        if (split.inner != null) out_inner.Add((Face2dBC)split.inner);
        if (split.outer != null) out_outer.Add((Face2dBC)split.outer);
        if (split.isContained) {
            if (interiorOnly) out_outer.Add(facet);    // Occlusion: bewahren
            else              out_inner.Add(facet);     // Boolean-Diff: entfernen
        }
    }
}
```

### 2. `Scene4d.UpdateCamera` rebuildet zuerst

```csharp
public void UpdateCamera() {
    RebuildCellsFromTopologies();   // ← neu: konsistente Face-Punkte + Halbebenen
    ApplyCameraOcclusion();
    RefreshVisibleCache();
}
```

Macht `UpdateCamera` idempotent, beseitigt das kumulative Schneiden, und sorgt dafür dass `Face2dBC.points` mit der aktuellen Kamera konsistent sind.

## Status der Tests

Vor dem Fix: 7 zusätzliche Failures unter `Scene4dHashTests.cs` (Bug-Repros).
Nach dem Fix: 0 zusätzliche Failures. Die 6 weiterhin fehlschlagenden Tests sind alle vor dem Bug schon kaputt gewesen (`HashSetEdgesEquality`, `PointEquality1dim`, `Face2dContainmentBoundary`, `Face2dContainmentFace2d`, `TriangleContainsTriangle`, `TriangulationContainmentTriangle` — die meisten gehen auf Asymmetrien in `Precision.TruncateBinary` für negative Zahlen zurück, irrelevant für den 4D-Bug).

Wichtigste Regression-Tests in `Scene4dHashTests.cs`:

| Test | Zweck |
|------|-------|
| `Cavalier_SingleHypercube_AsymmetricCameraNudge_KeepsAll4dFaces` | Smoking-Gun: Float-Roundtrip + Controller-Rauschen ≈ 5e-7 → früher erste-Frame-Verlust, jetzt stabil. |
| `Cavalier_SingleHypercube_NonSymmetricCamera_ShouldKeepAll4dFaces(...)` | Echte non-symmetrische wDir-Verschiebungen (10%, 50%, gemischt y/z) → früher Verlust geteilter Faces, jetzt erhalten. |
| `Cavalier_SingleHypercube_AsymmetricWDir_Keeps4dFaces(eps)` | Goldilocks-Zone-Sweep über 6 eps-Werte. |
| `Cavalier_SingleHypercube_RepeatedUpdateCamera_KeepsAll4dFaces` | 10 wiederholte UpdateCamera-Aufrufe mit Float-Roundtrip-Wackeln. |
| `Cavalier_SingleHypercube_UpdateCamera_NoCameraChange_IsIdempotent` | UpdateCamera ohne Kameraänderung ist idempotent. |
| `Cavalier_SingleHypercube_pbc_d2faces_Match_i2p` | `d2faces` und `i2p` haben dieselben Schlüssel (1:1). |
| `Polyhedron3dBoundaryComplex_InputOrder_DoesNotAffectFaces` | Permutationen der Eingabeliste ändern den Output nicht. |

## Beobachtungen am Design (unabhängig vom Fix)

Diese Punkte fielen mir beim Lesen auf — Kandidaten für nachgelagerte Aufräumaktionen:

1. **`UpdateCamera` war eine "Halbe" Pipeline.** Vor dem Fix sahen die vier öffentlichen Mutations-Endpoints so aus:

   | Methode      | ComputeTopologies | RebuildCells | ApplyOcclusion | RefreshCache |
   |--------------|:-:|:-:|:-:|:-:|
   | `Update`     | ✓ | ✓ | ✓ | ✓ |
   | `Translate`  | – (Mutation in-place) | ✓ | ✓ | ✓ |
   | `Rotate`     | – (Mutation in-place) | ✓ | ✓ | ✓ |
   | `UpdateCamera` | – | **fehlte** | ✓ | ✓ |

   Inkonsistenz behoben. Empfehlenswert wäre eine private `RebuildAndOcclude()`-Methode für die letzten drei, damit der "rebuild always before occlude"-Vertrag lokal sichtbar bleibt.

2. **`ApplyCameraOcclusion` mutiert `pbc.d2faces` destruktiv**. Heißt: jeder `Polyhedron3dBoundaryComplex` ist nach Konstruktion in einem zwei-Phasen-Zustand ("vor / nach Occlusion"). Empfehlung: Occlusion in eine separate Read-Only-Sicht schreiben (z. B. `pbc.visibleAfterOcclusion`), oder `ApplyCameraOcclusion` in `RebuildCellsFromTopologies` integrieren, damit nur ein Eingangspunkt existiert.

3. **`CutOut` hat zwei Semantiken hinter einem Namen.** Mit dem `interiorOnly`-Parameter ist es jetzt explizit. Wenn das Boolean-Difference-Use-Case in der Codebasis nirgends sonst verwendet wird (in `Scene4d` jedenfalls nicht — der einzige Call kommt aus `ApplyCameraOcclusion`), könnte man perspektivisch den Default auf "Occlusion" festsetzen und die Boolean-Difference-Methode komplett herausziehen oder umbenennen.

4. **`Face2dBC.points` aus dem Build-Zeitpunkt vs. `DefiningHalfSpaces` aus der aktuellen Kamera.** Wenn die Kamera sich zwischen den Phasen ändert, geraten Punkte und Halbebenen in eine sub-AOP.ERR-Inkonsistenz, die `HalfSpace.side` *gerade* in den 0-Bereich pusht. Mit `interiorOnly=true` ist das jetzt unkritisch, aber als Code-Hygiene-Punkt: `DefiningHalfSpaces` könnte aus den schon gespeicherten Face-Punkten ableiten statt erneut zu projizieren.

5. **`OrientedIntegerCell` hat auskommentierte Equals/GetHashCode** [OrientedIntegerCell.cs:122-131](Packages/d4bb/src/D4BB/Comb/OrientedIntegerCell.cs#L122-L131). Die Equality ignoriert `inverted`, was für die geteilte-Face-Dedup ausgenutzt wird. Stille semantische Kollision; explizit machen via expliziten `IEqualityComparer` oder `class FaceKey : IntegerCell`.

6. **`IntegerCell.GetHashCode` kommentiert "0..9 unique"**, ist tatsächlich Base-11 (kollisionsfrei für 0..10 in 4D). Bei Eingaben außerhalb `(int)((double))*32768`-Range Overflow. Empfehlung: `HashCode.Combine` plus `Debug.Assert` an der Range.

7. **`Polyhedron3dBoundaryComplex` doppelte Source of Truth**: `d2faces` (Set) vs. `i2p` (Dict). `RemoveFace` aktualisiert nur `d2faces`. Heute kein Bug, aber spröde — Invariante als XML-Doc festhalten oder `RemoveFace` synchronisiert beide.

8. **`Scene4d.cells.Sort` ist instabil**. Folgenlos in der jetzigen Architektur (gleich-tiefe Cells cutten sich nicht), aber latentes Risiko. Empfehlung: stabilen Tiebreaker (`cell.GetHashCode()`).

9. **`Point.GetHashCode` (parameterlos)** [Point.cs:59](Packages/d4bb/src/D4BB/Geometry/Point.cs#L59) verstößt gegen den Hash/Equals-Vertrag relativ zu `Equals` (wegen `len() < AOP.ERR`-Toleranz vs. ungerundetem `double.GetHashCode()`). Empfehlung: parameterlose Variante als `[Obsolete]` markieren.

10. **`Precision.TruncateBinary` truncated bei negativen Zahlen Richtung Null** (asymmetrisch zu Null hin) — Ursache der 6 vor-existierenden Failures `HashSetEdgesEquality` und `PointEquality1dim`. Workaround: `Math.Floor` statt `(int)`-Cast.
