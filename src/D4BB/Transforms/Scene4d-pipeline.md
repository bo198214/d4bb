# Scene4d render pipeline & incremental update

How the 4D→3D render data is computed, and — the point of the recent refactor — **what is recomputed
when a piece moves, and what is reused**. Covers the *old/stable* pipeline (`D4BB.Transforms`, used by
the main game). `Scene3d` (the legacy 3D path) is analogous but separate.

## Data model

One unified per-piece object, [`Piece`](Piece.cs), carries **game state + render state** together:

| group | fields | who writes |
|---|---|---|
| game state | `origins` (the unit-cell list), `colorSlot`, `center` | the owner (`GameLevel`) via `Piece.Translate/Rotate/Combine` |
| topology (cache) | `coplanarBoundaryFaces`, `interiorDivisionFaces` | `Scene4d.FillTopology` (IBC); mutated in place by `Piece.Translate/Rotate` |
| render | `cells`, `bounds`, `visibleFacets`, `visibleEdges`, `facetsMesh`, `edgesMesh` | `Scene4d` (occlusion + visible cache + mesh) |

**Binding (shared objects).** `GameLevel.pieces` and `Scene4d.pieces` are the **same `Piece` instances**
— the play `Scene4d` is constructed with `new Scene4d(gameLevel.pieces, …)` (the `List<Piece>` ctor). So a
move applied by the owner (`gameLevel.TranslateSelected` → `piece.Translate`) *is* the mutation the scene
renders; there is **no separate scene-side mirror** (the former double-update is gone). A *standalone*
`Scene4d(int[][][] origins, …)` (tests, the static goal scene) owns its own pieces instead.

## The pipeline, in stages

```
origins ──(A)── topology ──(B)── projected cells ──(C)── occluded cells ── visible cache ──(D)── *GenericMesh ──(E)── Unity mesh
          IBC            camera             CutOut                                       (Scene4d)        (Scene4dView)
```

- **(A) Topology** — `Scene4d.FillTopology(piece)`: runs `IntegerBoundaryComplex` on `piece.origins`,
  selecting the visible boundary `(3-cell, 2-face)` pairs (+ interior grid-division faces). **Expensive.**
  Result is *cached on the piece*.
- **(B) Projection** — `BuildPieceCells(piece)`: projects each cached 2-face through the camera to a
  `Face2dBC` (3D), back-face-culls, dedups shared faces, wires edge-neighbor links → per-cell
  `Polyhedron3dBoundaryComplex`.
- **(C) Occlusion** — `OccludePieceCells`: cuts each cell against every strictly-nearer, screen-AABB-
  overlapping occluder (`CutOut`). Occluder half-spaces depend only on the integer cell + camera, never on
  whether the occluder itself was cut. (See `ScreenBounds`, `ComputeOccluders`.)
- **visible cache** — `RefreshVisibleCacheForPiece`: collects `piece.visibleFacets` / `visibleEdges` from
  the cut cells, then **(D)** builds the per-piece `piece.facetsMesh` / `edgesMesh` (`FacetsGenericMesh` /
  `EdgesGenericMesh`, both Unity-free, in `D4BB.Transforms`) — triangulation + volumetric-edge geometry. A
  *new* mesh object is assigned every rebuild, so for an incremental move only the **affected** pieces get
  new objects; an untouched piece keeps the same object (reference identity = the renderer's dirty signal).
- **(E) Unity mesh** — `Scene4dView` (Assets) reads `piece.facetsMesh/edgesMesh`, uploads them into a Unity
  `Mesh` (`SetInMesh`), and runs per-piece decoration (colors / symbol UVs, in `Game`). Only the changed
  pieces are uploaded: the move methods return the affected set (`RefreshAffectedMeshes`); full rebuilds
  upload all (`RefreshAllMeshes`).
- **Decoration-only changes** (vertex data, not geometry) skip (D)+(E)'s upload entirely:
  `Scene4dView.RedecorateAllMeshes` re-runs just the per-piece decorator on the *existing* meshes. Used by
  `Game.ApplyPendingDisplayChanges` for **color mode**, **face shader**, **spectrum/hueStart** (only
  `mesh.colors` / symbol UV3 change). **Day/night** is even lighter — a pure material-reference swap, no
  re-decoration (colors are day/night-independent). The selection ring goes through `RebakeSymbolUVsAllPieces`
  (symbol UV3 only). Changes that *do* alter geometry still rebuild + re-upload: **grid divisions** /
  **cut edges** via a full `Scene4d.Update` + `RefreshAllMeshes`; the **occlusion / backface** toggles
  need only `UpdateCamera()` + `RefreshAllMeshes` (the flags affect the rebuild from cached topology,
  not the topology itself).

## The four recompute granularities (cheapest → most expensive)

1. **In-place topology shift** — `piece.Translate(axis)` / `piece.Rotate(v,w,pivot)`.
   Mutates `origins` + `center` + **shifts the cached topology cells' coordinates in place** (no IBC).
   This is *only the (A)-cache update*; it does **not** re-project or re-occlude.

2. **`Scene4d.UpdateCamera()`** — re-runs **(B)+(C)+visible cache for *all* pieces** from the cached
   topology. No IBC. This is the workhorse after a move or a camera change.

3. **Incremental re-occlusion** — `ReoccludeAfterPieceChange`: re-projects **only the moved piece** and
   re-occludes **only the pieces whose projected AABB overlaps it** before/after the move; all other pieces
   keep their cut cells. Two public entry points: `Scene4d.Translate(i,axis)`/`Rotate(i,…)` (which also do
   the #1 topology mutation), and **`Scene4d.ReoccludePiece(i)`** (re-occlusion only, for when the owner
   already moved the shared piece — the drag path). Proven byte-identical to a full `UpdateCamera` by
   `Scene4dIncrementalTests` (incl. multi-step snaps). **This is what the game drag now uses.**

4. **`Scene4d.Update(origins)`** — the full rebuild: **(A)+(B)+(C)+cache for all pieces**, IBC included.
   For a bound scene the `origins` argument is ignored and the scene resyncs from the shared pieces
   (`UpdateBound`), so existing `scene4d.Update(gameLevel.PieceOrigins)` call sites keep working.

## When each fires

| trigger | path | recompute |
|---|---|---|
| Level load / level change | `new Scene4d(gameLevel.pieces,…)` / `Update` | #4 full (IBC) |
| Toggle: grid-divisions / cut-edges | `Game` → `scene4d.Update(PieceOrigins)` | #4 full (IBC) |
| Toggle: occlusion / backface culling (GameMenu) | `scene4d.UpdateCamera()` | #2 (no IBC — the flags only affect the rebuild from cached topology) |
| Camera zoom / scene rotate | `PerspectiveControl` → `scene4d.UpdateCamera()` | #2 (no IBC) |
| **Drag** snap (per batch of steps) | `gameLevel.TranslateSelected` ×N (#1 on shared piece) → `RefreshSnapFacetMesh` → `scene4d.ReoccludePiece(i)` → `RefreshAffectedMeshes(affected)` | #1 ×N + #3 (no IBC); only moved + overlapping pieces' meshes uploaded |
| Drag end (commit) | `Scene4dView.RefreshAllMeshes()` only (restores the dragged piece's real edge mesh) | none — the incremental #3 path is byte-identical to a full rebuild (`Scene4dIncrementalTests`), so no scene4d work at drag end (2026-08-03; was #4, the entire release freeze) |
| Non-drag move (keyboard/button) animation | `OnTranslate/OnRotate` (see below) | #1 ×2 + #2, then #4 at animation end |
| Combine / Reset | `gameLevel` mutates `pieces` list → `scene4d.Update` | #4 full (IBC) |

**Drag (the main VR interaction).** Each snap: `gameLevel.TranslateSelected(axis)` shifts the shared
piece's cached topology in place (#1, no IBC) — possibly several steps — then a single `ReoccludePiece(i)`
(#3) re-projects only the dragged piece and re-occludes only the pieces it overlaps (camera is unchanged
during a snap, so the others stay valid). Fast: IBC is skipped on every snap *and* only the affected
pieces are re-occluded. Drag end does **no** scene4d work at all (the incremental path is byte-identical
to a full rebuild); only the Unity meshes are refreshed.

**Non-drag animated move.** `gameLevel.TranslateSelected/RotateSelected` already moved the *shared* piece
to NEW. Because the animation interpolates from the OLD geometry, `OnTranslate/OnRotate` **temporarily
revert** the shared piece to OLD (`piece.Translate(inverse)` / `piece.Rotate(w,v,pivot)`), snapshot the
from-state via `UpdateCamera`, then **re-apply** NEW (net-zero) and let the per-frame animation drive the
mesh OLD→NEW; `CompleteAnimation` does a final #4 rebuild. (Drag detaches these handlers, so this only
affects the keyboard/button fallback path.)

## Why moves are cheap

The expensive step is **(A) IBC**. The cache means a translate/rotate **never re-runs IBC**: the boundary
selection is topology-invariant under lattice translation/90°-rotation, so the cached `(c3,f2)` cells are
just *coordinate-shifted in place* (#1). Everything downstream (B/C) is a re-projection of those cached
cells, cheap relative to IBC. On top of that, the drag re-occludes only the *overlapping* pieces (#3,
`ReoccludePiece`) instead of all of them (#2) — so a snap touches just the dragged piece and whatever it
overlaps.

## `cullBackFaces` × `enable4dOcclusion`: all four combinations are in contract (2026-08)

Since the **same-parent front-over-back rule** (2026-08) every flag combination is well-defined and
artifact-free; the game layer's former "occlusion ⇒ culling" invariant in `Game.SetOcclusion4d` /
`Game.SetBackfaceCulling` is gone and the two GameMenu toggles are independent again. The rule: at
equal parent depth, `OccludePieceCells` lets a front cell cut the back cells of its **own parent
tesseract** (`SameParentTesseract`, exact integer identity; `SortFarToNear` orders averted cells first
within an equal-depth group so the back cell is already enqueued when its front sibling arrives).
Distinct-parent equal-depth pairs are still skipped (provably shadow-disjoint), and same-parent
front-front/back-back pairs never overlap (each surface tiles the parent's shadow injectively).
Correctness: `OCCLUSION-PROOF.md` "Backfaces"; pinned by `Scene4dBackfaceRemovalTests`.

Consequence: **occlusion ON + culling OFF renders the same picture as culling ON** — every backface is
hidden surface and is now fully cut away (by its own parent's front cells, or by strictly nearer
tesseracts as before) instead of never being built. The mode just does strictly more work for the same
result (it builds, cuts and discards every back cell), so culling stays the sensible default; to
actually *see* backfaces, turn occlusion OFF.

Historical notes, kept because the geometry facts still hold:

- Before the depth key moved to the **parent tesseract center**, a cell's own backface was *deeper*
  than its front cells and the mixed mode looked accidentally clean. The parent-center key gave front
  and back cells of one tesseract equal depth; the equal-depth skip then kept backfaces alive, and
  sibling tesseracts' front-cell hulls carved grid-patterned holes into them — the 2026-08-03 artifact
  analysis that led first to the toggle coupling, now to the same-parent rule.
- **Averted occluder cells never cut** (silent no-op, still true). `DefiningHalfSpaces` derives its
  halfspace normals from `ClockwiseFromOutsideVertices2d` windings; the 4D→3D projection restricted to
  a cell's hyperplane is orientation-*reversing* exactly for camera-averted cells, so their six
  halfspace normals all point inward and the hull-intersection test is empty. Harmless: a solid's
  front cells alone cover its full silhouette (and now also cut its own backfaces), so nothing that
  should be cut escapes. The 2026-08-03 under-/over-cut census found zero violations, and the
  incremental `ReoccludePiece` path stays byte-identical to a full rebuild in this mode too.
- **Exactly-coplanar faces are routed by an orientation test calibrated to front-wound faces**
  (`Split()`'s `isContained` branch: co-oriented with the cutter ⇒ removed). Faces owned by averted
  cells (ownership pass 2 in `BuildPieceCells`) are deliberately back-wound, so on exactly-coplanar
  contact planes their keep/remove decision inverts relative to a front-wound twin. Less load-bearing
  now that back-owned faces are cut away entirely, and it never manifested in the census or the
  backface-removal sweeps — but it remains unproven in general (epsilon regime).
