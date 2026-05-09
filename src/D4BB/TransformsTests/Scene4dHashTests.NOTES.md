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

### 1. `CutOut` bekommt `BoundaryFaceMode`-Parameter (Default `Dynamic`)

`Polyhedron3dBoundaryComplex.CutOut` und `Polyhedron3dBoundaryComplex.Split` haben einen `BoundaryFaceMode boundaryMode = BoundaryFaceMode.Dynamic` Parameter. Drei Werte:

- **`Dynamic`** (Default): Bei `isContained`-Faces wird anhand der Normalenrichtung entschieden:
  - F.normal · H.normal > 0 (gleichgerichtet): F ist co-orientiert mit der sichtbaren Vorderseite des Cutters → Face wird gecuttet (vom Cutter selbst gezeichnet, hier verdeckt).
  - F.normal · H.normal < 0 (entgegengesetzt): F's sichtbare Seite zeigt vom Cutter weg → Face bleibt erhalten (Vorderseite einer Cell, die zufällig auf der Cut-Ebene liegt).
- **`PreserveAll`**: Boundary-Faces immer bewahren. Stricter Occlusion-Variant.
- **`CutAll`**: Boundary-Faces immer entfernen. Legacy-Boolean-Difference-Semantik.

Die alten Tests `CutOutTest_Half`, `CutOutTest_L`, `NeighborCut` benutzen `BoundaryFaceMode.CutAll`, um die alte Subtraktions-Semantik weiter zu testen.

```csharp
// PolyhedronBoundaryComplex.cs
public enum BoundaryFaceMode { Dynamic, PreserveAll, CutAll }

public void CutOut(HalfSpace[] halfSpaces, BoundaryFaceMode boundaryMode = BoundaryFaceMode.Dynamic) { ... }

public static void Split(HalfSpace halfSpace, IEnumerable<Face2dBC> facets,
                          List<Face2dBC> out_inner, List<Face2dBC> out_outer,
                          BoundaryFaceMode boundaryMode = BoundaryFaceMode.Dynamic) {
    foreach (var facet in facets) {
        var split = facet.Split(halfSpace);
        if (split.inner != null) out_inner.Add((Face2dBC)split.inner);
        if (split.outer != null) out_outer.Add((Face2dBC)split.outer);
        if (split.isContained) {
            switch (boundaryMode) {
                case BoundaryFaceMode.PreserveAll: out_outer.Add(facet); break;
                case BoundaryFaceMode.CutAll:      out_inner.Add(facet); break;
                case BoundaryFaceMode.Dynamic:
                default:
                    if (AOP.gt(facet.Normal().sc(halfSpace.normal), 0))
                        out_inner.Add(facet);   // co-oriented → occluded
                    else
                        out_outer.Add(facet);   // counter-oriented → visible boundary
                    break;
            }
        }
    }
}
```

### 1a. Warum *Dynamic* statt einer statischen "Occlusion-Variante"

Eine erste Iteration des Fixes hatte `interiorOnly=true` als Default — boundary-koinzidente Faces wurden *immer* bewahrt. Das fixierte den ursprünglichen Unit-Tesserakt-Bug, riss aber einen neuen auf: bei wirklich überlappenden Pieces (`TwoTesseracts.json`) blieben Fragmente von Piece 1 sichtbar, deren sichtbare Seite eigentlich von Piece 0's Surface verdeckt wird.

Der Unterschied lässt sich nicht am Cutter-Geometrie allein erkennen — er hängt davon ab, **wohin die Face zeigt**:

- Geteilte Face zwischen `c3_a` (Cutter, vorne) und `c3_b` (Cell, hinten) im selben Piece: F ist in c3_b's d2faces, F.normal zeigt aus c3_b heraus = in c3_a hinein = **gegen** H.normal → preserve. ✓
- Face von Piece 1 koinzidiert mit Piece 0's Surface: F.normal zeigt entweder aus Piece 1 heraus = vom Cutter weg = **gegen** H.normal → preserve, ODER ins Piece 1 hinein = mit H.normal **gleich** → cut.

Der dynamische Vergleich entscheidet pro Fragment richtig.

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
| `TwoTesseracts_NoFaceFragmentInsideUnionOfCloserOtherPieceCells` | Gegenrichtungs-Test: bei wirklich überlappenden Pieces (TwoTesseracts.json) dürfen keine Fragmente einer hinteren Piece innerhalb der Union der vorderen Piece-Cells bleiben. Ohne Dynamic-Mode (statisches PreserveAll): 2 Fragmente fälschlich erhalten. Mit Dynamic-Mode: 0. |

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

3. **`CutOut` hat drei Semantiken hinter einem Namen.** Mit `BoundaryFaceMode` sind sie explizit (`Dynamic` / `PreserveAll` / `CutAll`). Wenn der Boolean-Difference-Use-Case in der Codebasis nirgends sonst verwendet wird (in `Scene4d` jedenfalls nicht — der einzige Call kommt aus `ApplyCameraOcclusion`), könnte man `CutAll` perspektivisch herausziehen und in einen separaten Boolean-Difference-Methodenname trennen.

4. **`Face2dBC.points` aus dem Build-Zeitpunkt vs. `DefiningHalfSpaces` aus der aktuellen Kamera.** Wenn die Kamera sich zwischen den Phasen ändert, geraten Punkte und Halbebenen in eine sub-AOP.ERR-Inkonsistenz, die `HalfSpace.side` *gerade* in den 0-Bereich pusht. Mit `Dynamic` Mode ist das jetzt unkritisch, aber als Code-Hygiene-Punkt: `DefiningHalfSpaces` könnte aus den schon gespeicherten Face-Punkten ableiten statt erneut zu projizieren.

5. **`OrientedIntegerCell` hat auskommentierte Equals/GetHashCode** [OrientedIntegerCell.cs:122-131](Packages/d4bb/src/D4BB/Comb/OrientedIntegerCell.cs#L122-L131). Die Equality ignoriert `inverted`, was für die geteilte-Face-Dedup ausgenutzt wird. Stille semantische Kollision; explizit machen via expliziten `IEqualityComparer` oder `class FaceKey : IntegerCell`.

6. **`IntegerCell.GetHashCode` kommentiert "0..9 unique"**, ist tatsächlich Base-11 (kollisionsfrei für 0..10 in 4D). Bei Eingaben außerhalb `(int)((double))*32768`-Range Overflow. Empfehlung: `HashCode.Combine` plus `Debug.Assert` an der Range.

7. **`Polyhedron3dBoundaryComplex` doppelte Source of Truth**: `d2faces` (Set) vs. `i2p` (Dict). `RemoveFace` aktualisiert nur `d2faces`. Heute kein Bug, aber spröde — Invariante als XML-Doc festhalten oder `RemoveFace` synchronisiert beide.

8. **`Scene4d.cells.Sort` ist instabil**. Folgenlos in der jetzigen Architektur (gleich-tiefe Cells cutten sich nicht), aber latentes Risiko. Empfehlung: stabilen Tiebreaker (`cell.GetHashCode()`).

9. **`Point.GetHashCode` (parameterlos)** [Point.cs:59](Packages/d4bb/src/D4BB/Geometry/Point.cs#L59) verstößt gegen den Hash/Equals-Vertrag relativ zu `Equals` (wegen `len() < AOP.ERR`-Toleranz vs. ungerundetem `double.GetHashCode()`). Empfehlung: parameterlose Variante als `[Obsolete]` markieren.

10. **`Precision.TruncateBinary` truncated bei negativen Zahlen Richtung Null** (asymmetrisch zu Null hin) — Ursache der 6 vor-existierenden Failures `HashSetEdgesEquality` und `PointEquality1dim`. Workaround: `Math.Floor` statt `(int)`-Cast.
