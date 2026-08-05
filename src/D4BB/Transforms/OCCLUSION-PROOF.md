# Provable correctness of the Scene4d / Scene3d painter occlusion

This note proves that the painter's-algorithm occlusion in `Scene4d` (and, one dimension
down, `Scene3d`) computes **exactly** the visible surface for lattice polycube scenes —
up to a measure-zero set handled by the epsilon regime. It exists because the previous
justification ("scalar far-to-near sort is exact for translates of a convex body") was a
statement about the solid unit **tesseracts**, while the implementation sorted the flat
boundary **3-cells** by their own centers — an unproven (and in principle unsound) proxy.
Two changes close the gap, both motivated by this proof:

1. **Occluder completeness** (2026-08): the occluder set is the *full* boundary-cell list
   (`Piece.boundaryCells`), never a set derived from face-pair bookkeeping. (A wall-center
   cell with all six 2-faces coplanar-interior appears in no `(c3, f2)` pair and was
   silently dropped — the tunnel-level bug.)
2. **Parent-center depth key** (2026-08): the sort/skip key of a boundary cell is the view
   depth of its **parent tesseract's center** (`cell.Center() − ½·cell.Normal()`), not the
   cell's own center.

   *Historical note:* a randomized arithmetic search (5·10⁷ configurations of two
   front-facing cell orientations × integer parent offsets in [−3,3]⁴ × random view
   normals, 10⁻³ margins) found **no** case where the old cell-center key ordered two
   hull-overlapping cells against the true parent order — the flip window (`|v·δ|`
   smaller than the ±½ orientation corrections) and the hull-overlap condition appear to
   be mutually exclusive. So the old key seems to have been accidentally safe in
   practice; it was replaced because "no counterexample found" is not a proof, while the
   parent key *is* provably correct (Lemma 3).

## Setting

- **Solid.** `S = ⋃ᵢ Tᵢ`, where the `Tᵢ` are unit tesseracts with integer origins
  (`Tᵢ = K + aᵢ`, `K = [0,1]⁴`, `aᵢ ∈ ℤ⁴`, pairwise distinct). Pieces move by lattice
  translations and 90° rotations, so a multi-piece scene is still of this form on one
  common lattice, and piece-overlap checks guarantee interior-disjointness.
- **Camera.** Parallel projection `π : ℝ⁴ → ℝ³` with **fibers along the unit view normal
  `v`**: `π(p + t·v) = π(p)`. This holds for `Camera4dParallel` in both bases: the three
  projection rows satisfy `v[i]·v[3] = 0` (for the cavalier basis: `−pₓ·n + pₓ·n = 0`
  etc.), so the null direction of `π` is `v[3] = v`. The cavalier "obliqueness" is a shear
  *within* the 3D screen (`v[0]·v[1] ≠ 0`), i.e. an invertible linear reparametrization of
  screen space — it maps projected hulls to projected hulls and changes no
  inside/outside/overlap relation. **Depth** of a point `p` is `v·p`; smaller = nearer.
- **Rendered geometry.** The boundary 3-cells of `S` (`IntegerBoundaryComplex`), each a
  flat unit cube in ∂S; each front-facing cell (`v·n < 0`, `n` = outward normal, strict)
  contributes its owned 2-faces. The projected hull `hull(c) = π(c)` of a front-facing
  3-cell is a full-dimensional parallelepiped in screen space (`v·n ≠ 0`).
- **Hidden.** A surface point `P ∈ ∂S` is *hidden* iff some `q ∈ int S` has
  `π(q) = π(P)` and `v·q < v·P`.
- **Algorithm.** Far-to-near sweep over all front-facing boundary cells; a cell `c_A` cuts
  the faces of `c_B` (removing the part of `c_B`'s faces strictly inside `hull(c_A)`) iff
  `depth(c_A) < depth(c_B)`, where `depth(c) = v·(parent-tesseract center)`. Equal-depth
  pairs are skipped. The AABB overlap gate only skips pairs whose hulls cannot intersect —
  a conservative accelerator with no semantic content.

Write `A, B` for two distinct tesseracts of `S`, `δ = center(B) − center(A) ∈ ℤ⁴ \ {0}`.
Interior-disjointness of lattice translates is equivalent to `δ ∉ int(K−K) = (−1,1)⁴`.
The **shadow** of a body is the interior of its projection; the **chord** of a body on a
fiber is its (open) intersection interval with that fiber.

## Lemma 1 (separating hyperplane ⇒ uniform chord order)

*Let `A, B` be interior-disjoint convex bodies and `H = {x : n·x = c}` a hyperplane with
`int A ⊆ {n·x < c}`, `int B ⊆ {n·x > c}`. (a) If `n·v = 0`, no fiber meets both
interiors, so the shadows are disjoint. (b) If `n·v ≠ 0`, then on every fiber meeting
both interiors the two chords are disjoint intervals and their depth order is
`sign(n·v)` — the same on every such fiber.*

**Proof.** Along a fiber, `n·x` is affine with slope `n·v`. If `n·v = 0` the fiber stays
on one side of `H` and cannot meet both interiors. If `n·v > 0`, all points with
`n·x < c` come at strictly smaller depth than all points with `n·x > c`; hence the
`A`-chord precedes the `B`-chord; for `n·v < 0` conversely. ∎

## Lemma 2 (equal parent depth ⇒ disjoint shadows)

*If `v·δ = 0`, the shadows of `A` and `B` are disjoint.*

**Proof.** Take `n = δ`. For integer `δ`: `δ·δ = Σδᵢ² ≥ Σ|δᵢ| = h_{K−K}(δ)` (each
`|δᵢ|` is `0` or `≥ 1`), so `{x : δ·x = c}` separates `int A` from `int B` for a suitable
`c` (the support function of the difference body `K−K = [−1,1]⁴` is `h_{K−K}(n) = ‖n‖₁`).
Since `n·v = δ·v = 0`, Lemma 1(a) applies. ∎

This makes the algorithm's equal-depth **skip** exact rather than merely convenient for
*distinct* tesseracts. The same-parent case (`A = B`, equal depth by construction) is
skipped between front cells for the same reason — a convex body's front cells' hulls tile
its shadow with disjoint interiors, so front never occludes front — but front-over-back
is a real occlusion within one tesseract; see "Backfaces" below.

## Lemma 3 (chord order = parent-center order)

*If the shadows of `A` and `B` overlap, then `v·δ ≠ 0`, and on every common fiber the
`A`-chord precedes the `B`-chord iff `v·δ > 0` — i.e. iff `A`'s center is nearer.*

**Proof.** Shadows overlap iff there are `x ∈ int A`, `y ∈ int B` on one fiber, i.e.
`y = x + s·v`; writing `x = k₁ + a`, `y = k₂ + b` with `k₁, k₂ ∈ int K` this is
equivalent to

&nbsp;&nbsp;&nbsp;&nbsp;`δ = (k₁ − k₂) + s·v  =  w + s·v,  w ∈ int(K−K).`

*Sign of `s` is well-defined:* `T = {t : δ − t·v ∈ int(K−K)}` is an open interval
(convexity) not containing `0` (interior-disjointness: `δ ∉ int(K−K)`), so all
decompositions share one sign; `v·δ = 0` would put `T ∌ 0` in contradiction with
Lemma 2's disjoint shadows, so `T` is nonempty only when `v·δ ≠ 0`.

*Order witness:* from `δ = w + s·v` with `w = k₁ − k₂`: the points `x = k₁ + a ∈ int A`
and `y = k₂ + b = x + s·v ∈ int B` (substitute `b = a + δ`) lie on one fiber, the
`B`-point deeper by `s`.
Chords of interior-disjoint bodies on a fiber are disjoint intervals, so a single strict
witness fixes the order on that fiber; by Lemma 1(b) (any separating hyperplane of the
two bodies has `n·v ≠ 0` here, since a common fiber exists) the same order holds on
*every* common fiber. Hence: `A` first iff `s > 0`.

*Integrality pins the sign:* suppose `s < 0` and `v·δ > 0`. Then
`w = δ + |s|·v ∈ (−1,1)⁴` componentwise: for `δᵢ ≥ 1`, `δᵢ + |s|vᵢ < 1` forces `vᵢ < 0`;
for `δᵢ ≤ −1` symmetrically `vᵢ > 0` — in both cases `vᵢδᵢ < 0`, and `δᵢ = 0`
contributes nothing. Summing: `v·δ < 0`, contradiction. The mirrored case is symmetric,
so `sign(s) = sign(v·δ)`. ∎

## Theorem (the algorithm computes exactly the visible surface)

*Up to a measure-zero exception set, a rendered surface point `P` (on a face owned by a
front-facing boundary cell `c_B` with parent `B`) is removed by the sweep iff it is
hidden.*

**Soundness (nothing visible is cut).** If `P` is cut, then `π(P) ∈ int hull(c_A)` for
some front-facing occluder `c_A` (parent `A`) with `depth(c_A) < depth(c_B)`, hence
`A ≠ B` (equal parents ⇒ equal depth ⇒ skipped) and `v·center(A) < v·center(B)`.
`P ∈ c_B ⊆ ∂B` and `c_B` front-facing make `v·P` the entry depth of `B`'s chord on its
fiber. `int hull(c_A) ⊆` shadow of `A`, and `π(P)` is in `B`'s shadow closure —
generically in its interior (the boundary is the measure-zero silhouette). So the shadows
overlap, Lemma 3 puts `A`'s whole chord strictly before `B`'s, and any interior point `q`
of `A`'s chord on `P`'s fiber has `v·q <` (entry of `B`) `= v·P`: `P` is hidden. ∎

**Completeness (everything hidden is cut).** Let `P` be hidden by `q ∈ int S` on its
fiber. Follow the fiber toward the camera side and let `r` be the first point of `S̄` at
depth `≤ v·q`; `r ∈ ∂S`, and generically `r` lies in the relative interior of a single
boundary 3-cell `c` (fibers through the 2-skeleton are measure zero) with parent `C`.
The fiber passes from outside `S` into `C` at `r`, so `c` is front-facing (`v·n < 0`) and
`r` is the entry point of `C`'s chord; `π(P) = π(r) ∈ int hull(c)` (relative interior of
a front-facing cell projects to the open hull). From
`v·r < v·q < v·P = entry_B(π(P))` and chord-disjointness, `C`'s chord precedes `B`'s, so
by Lemma 3 `v·center(C) < v·center(B)`, i.e. `depth(c) < depth(c_B)`. Since the occluder
set contains **every** front-facing boundary cell (`Piece.boundaryCells` — this is where
occluder completeness is load-bearing), the sweep cuts `P`. ∎

**Exception set.** All "generically" qualifiers exclude only measure-zero loci — shadow
boundaries (silhouettes), fibers through the 2-skeleton, edge-on cells (`v·n = 0`, which
`IsFacedBy`'s strict `< 0` drops together with their zero-area projections). These fall
into the same epsilon regime that the cut arithmetic already handles with `AOP.ERR`
snapping; they can misclassify only sets of zero projected area.

## Backfaces (`cullBackFaces = false`)

With culling off the renderer also draws the camera-averted boundary cells (`v·n > 0`) —
all of which are *hidden* surface in the sense above (they are chord **exit** points; the
same fiber enters `S` strictly nearer). Since 2026-08 the sweep removes them exactly, via
one extension: **at equal depth, a front cell cuts the back cells of its own parent
tesseract** (`OccludePieceCells`; `SortFarToNear` orders averted cells before facing ones
within an equal-depth group so the back cell is already enqueued as occludee when its
front sibling arrives). Parent identity is decided in exact integer arithmetic
(`SameParentTesseract`), never by depth comparison.

*Soundness:* a point cut by the new rule lies (strictly, generically) inside a front hull
of its own parent `B` — the fiber enters `B` strictly before `P` (entry < exit on every
fiber of a convex body), so `P` is hidden. Cuts of back-cell points by *other* parents'
front cells are the old Lemma 3 argument (the occluder's whole chord precedes `B`'s chord,
and `v·P` is now the exit rather than the entry — even later). Front cells are never cut
by the new rule (it requires an averted occludee), so the front surface is untouched.

*Completeness:* let `P` be a rendered back-cell point (parent `B`), generic fiber. The
fiber enters `S̄` first at some `r` on a front-facing boundary cell `c` (parent `C`) with
`v·r ≤ entry_B < exit_B = v·P` and `π(P) ∈ int hull(c)`. If `C = B`, `c` is a front cell
of `P`'s own parent — the new same-parent rule cuts `P`. If `C ≠ B`, `C`'s chord starts at
or before `B`'s entry and chords are disjoint, so `C`'s chord precedes `B`'s and Lemma 3
gives `depth(c) < depth(c_B)` — the ordinary sweep cuts `P`. (Note the case split is
exactly why the rule is needed: the nearer-parent argument covers every hidden back-cell
point *except* those whose entering cell belongs to the same tesseract — e.g. all of them,
for a single-tesseract piece.) Equal-depth back-back and front-front pairs of one parent
are shadow-disjoint tilings of the same shadow (projection restricted to the front resp.
back surface of a convex body is injective), so skipping them stays exact. ∎

Hence with occlusion ON the rendered result is the same visible surface **regardless of
`cullBackFaces`** — backfaces are simply removed by cutting instead of never being built
(verified per sweep in `Scene4dBackfaceRemovalTests`). Two practical notes: the mixed
mode does strictly more work for the same picture (it builds, cuts and discards every
back cell), and averted cells enqueued as *occluders* are silent no-ops (their
`DefiningHalfSpaces` windings invert under the orientation-reversing projection, leaving
an empty hull test — harmless, since their front siblings cover the same shadow).

## Corollaries and scope

- **Multi-piece scenes** are covered as long as all pieces sit interior-disjoint on one
  integer lattice — which game moves (lattice steps, 90° rotations, overlap checks)
  preserve by construction. Cross-piece occlusion needs no extra argument.
- **Every camera pose** is covered: the proof constrains only the fiber direction, never
  the rotation state; both cavalier and isometric bases keep fibers along `viewNormal`
  (rows stay `⊥ v[3]` under the rigid `rotate*` updates).
- **`Scene3d`** is the same statement one dimension down (unit cubes in ℝ³, boundary
  2-cells, `K = [0,1]³`): every proof step is dimension-agnostic. Its depth key uses the
  parent cube center accordingly.
- **`cullBackFaces = false` combined with occlusion** is covered by the "Backfaces"
  section above (2026-08; it used to be out of scope, which is why the game once coupled
  the two toggles).
- **Not covered:** non-lattice poses (freely rotated bodies — the Geometry2/Geometry3d
  pairwise pipelines have their own ordering arguments) and perspective cameras
  (`Camera4dCentral`: fibers are not parallel).

## Implementation map

| Proof element | Code |
|---|---|
| Occluder completeness (every front-facing boundary cell) | `Piece.boundaryCells`; `Scene4d.ComputeOccluders`, `Scene4d.BuildPieceCells` role (b) |
| Parent-center depth key | `Scene4d.Depth`, `Scene3d.Depth` (`Center() − ½·Normal()`) |
| Equal-depth skip = Lemma 2 | `Scene4d.OccludePieceCells` (`depth != occ.depth`), `Scene3d.ApplyCameraOcclusion` |
| Same-parent front-over-back ("Backfaces") | `Scene4d.OccludePieceCells` + `SameParentTesseract`; averted-first tiebreak in `Scene4d.SortFarToNear` (Scene4d only — `Scene3d` still culls unconditionally) |
| Cut volume = projected hull | `Scene4d.DefiningHalfSpaces`, `Scene3d.DefiningHalfSpaces2d` |
| Fibers along `viewNormal` | `Camera4dParallel` (`v[i]·v[3] = 0` for `SetCavalier`/`SetIsometric`) |
| Empirical guards | `Scene4dParityTests`, `OcclusionSoundnessTests`, `Scene4dOcclusionSoundnessTests` (both pipelines checked against the pipeline-independent under-cut invariant, with the parent-center key) |
