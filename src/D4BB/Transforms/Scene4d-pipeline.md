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

## `cullBackFaces` × `enable4dOcclusion`: the mixed mode is out of contract

Only `cullBackFaces=true` is covered by the correctness suites (`Scene4dParityTests`,
`Scene4dOcclusionSoundnessTests`, `Scene4dMultiPieceSoundnessTests`, `Scene4dIncrementalTests` pin it;
`MarkStampingTests` runs culling-off but asserts only mark stamping), and the scalar-depth-sort proof
(`OCCLUSION-PROOF.md`) is a statement about *front* surfaces of solids. **Occlusion ON + culling OFF**
is not a supported view: the game layer forbids it outright — `Game.SetOcclusion4d` /
`Game.SetBackfaceCulling` are the single enforcement point of the invariant "occlusion ⇒ culling"
(occlusion ON drags culling ON, culling OFF drags occlusion OFF), and both the GameMenu toggles and the
dev controller keys go through them. The mixed state remains constructible on a raw `Scene4d`
(tests, e.g. `MarkStampingTests`, do). Findings of the 2026-08-03 analysis of that mode:

Historical note: before the depth key moved from the 3-cell center to the **parent tesseract center**
(the change that made the scalar sort provably exact), a cell's own backface was *deeper* than its
front cells, so a tesseract cut its own backface and the mixed mode looked clean (backfaces simply
vanished). The parent-center key gives front and back cells of one tesseract equal depth, the
equal-depth skip then keeps the backface — which is what surfaced the artifacts below.

- **Averted occluder cells never cut** (silent no-op). `DefiningHalfSpaces` derives its halfspace
  normals from `ClockwiseFromOutsideVertices2d` windings; the 4D→3D projection restricted to a cell's
  hyperplane is orientation-*reversing* exactly for camera-averted cells, so their six halfspace normals
  all point inward and the hull-intersection test is empty. Verified empirically: a front-facing cell's
  own projected center passes `StrictlyInside` of its own hull 4/4, an averted cell's 0/4. This
  contradicts the role-(b) comment in `BuildPieceCells` ("with culling off, every boundary cell
  [occludes]") — but it is geometrically **masked for complete pieces**: a solid's front cells alone
  cover its full silhouette, so nothing that should be cut escapes. An under-/over-cut census
  (occlusion-off oracle vs. nearer-front-hull model; box3d / tunnel1d / L3 with camera sweeps,
  multi-piece stacked/touching/overlap configs) found **zero** violations, and the incremental
  `ReoccludePiece` path stays byte-identical to a full rebuild in this mode too.
- **The visible weirdness is mostly the mode's semantics, not miscuts.** The equal-parent-depth skip in
  `OccludePieceCells` (`depth != occ.depth`) means a tesseract's own backface is *never* cut by its own
  front cells — the skip is the correctness guard derived from the depth-order theorem (which orders
  *distinct* solids and says nothing about a solid vs. itself; equal-depth distinct solids are provably
  shadow-disjoint), and with culling off it is what makes backfaces visible at all. But sibling
  tesseracts of the same piece (and other pieces) have *different* parent depths, so their front-cell
  hulls — unit-cell-sized, grid-aligned, shear-offset parallelepipeds — **do** carve their occlusion
  holes into a visible backface. On a flat multi-tesseract back wall this yields regular grid-patterned
  jagged cuts that *look like* the piece's interior grid cells were doing the cutting. They are not:
  interior shared 3-cells are cancelled in `IntegerBoundaryComplex.ConnectCell` (both oriented copies
  are removed — `OrientedIntegerCell` hashes orientation-insensitively, so the two copies match) and
  never render nor occlude; the cutters are the per-tesseract *parceling* of the outer boundary.
  Verified empirically: a single-tesseract piece loses **zero** area in this mode (all cells share one
  parent depth → every pair skips), a two-tesseract piece loses area on exactly the far tesseract's
  backface cells.
- **Exactly-coplanar faces are routed by an orientation test calibrated to front-wound faces**
  (`Split()`'s `isContained` branch: co-oriented with the cutter ⇒ removed). Faces owned by averted
  cells (ownership pass 2 in `BuildPieceCells`) are deliberately back-wound — noted there as "harmless
  to a single-sided raycast", which considered picking, not this routing — so on exactly-coplanar
  contact planes their keep/remove decision inverts relative to a front-wound twin. Did not manifest in
  the census configs, but is unproven in general.
