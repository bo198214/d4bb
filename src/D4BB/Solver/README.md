# D4BB.Solver — level solvability QA

Answers one question about a level file: **is it solvable, and can we prove it?**

The question is a reachability problem — an initial state, a set of allowed steps, a final
condition — so this is a planner. It is a *domain-specific* one rather than a generic PDDL/STRIPS
planner, and deliberately so: a piece has 192 proper lattice orientations times a few hundred
positions, with collision preconditions over every cell of the envelope, which grounds out into
millions of actions before search begins; and delete-relaxation heuristics are blind to precisely
what makes these puzzles hard (a cell cannot be occupied twice, and a piece often has to move
*away* before anything can be assembled).

## The rules being checked

The move set and the win condition are **not modelled here**. Everything is decided by replaying
moves through a live `D4BB.Game.GameLevel` — the class the game itself runs — so a level counts as
solvable exactly when a player could solve it:

- one-cell translations along the four axes,
- 90° turns in any coordinate plane about the centre of **any of the piece's own cells** (the game's
  pivot is the cube under the grabbed facet, so every cell is a legal pivot),
- no piece overlap, nothing outside `boundary_min_max`, and — unless the level sets
  `quantum_rotation` — no collision *during* the swept quarter turn (`RotationSweep`),
- `shape` mode: the pieces must end as **one compound** (combine merges only across shared 3-cells)
  congruent to the goal under a proper rotation; `absolute` mode: the union must be exactly the
  goal cells.

## Three tiers of answer

`LevelValidator.Check` runs them cheapest-and-strongest first, and the verdicts are deliberately
asymmetric in what they claim:

| Verdict | Meaning | Strength |
|---|---|---|
| `Solved` | a move sequence was replayed through the engine and won | **proof** |
| `Unsolvable` | structural defect, or the pieces cannot tile the goal at all | **proof** |
| `AssemblyOnly` | the pieces *can* tile the goal, but no path into that tiling was found | unknown |
| `Unknown` | a budget ran out first | unknown |
| `SolutionFileInvalid` | a `.moves` file exists but no longer wins | **defect** |

1. **Structural checks** (`LevelValidator.Structure`). Some settle it outright: pieces can never
   overlap, so if their cells do not add up to the goal's, no arrangement can ever match it;
   and a `shape`-mode goal that is not face-connected can never become the single compound that
   mode requires. Others are only suspicious and become warnings (pieces overlapping at the start,
   pieces starting outside the envelope, duplicate cells).
2. **Solution file** (`SolutionFile`, see below). If `<level>.moves` exists it is replayed — the
   strongest verdict available, for the cost of a few hundred move applications.
3. **Search.** `AssemblySolver` (exact cover: can the pieces tile the goal in *any* orientation?)
   then `PathSearch` (can they be manoeuvred there?). A found sequence is verified, shortened
   (`SolutionPolish`), verified again, and written out as the level's solution file — so the
   expensive search happens once per level, ever.

## Solution files

`<level>.moves`, next to `<level>.json`. Plain text, `#` starts a comment, moves are whitespace- or
comma-separated. The notation is the one in [`tools/puzzle/RULES.md`](../../../../../tools/puzzle/RULES.md)
— `<piece><t|r><+|-><axes>[@x,y,z,w]`, plus `<piece>c` for an explicit combine — so a sequence
written for `p.py confirm` replays here and vice versa.

```
# Tesserian solution for "Two Cuboids" (TwoCuboids.json)
1t+x 2t-w 1r-xw@2,0,0,0 1t-w 2r-xw@3,0,0,1
```

Details that are easy to get wrong:

- **Piece numbers are 1-based file order and survive combines.** They resolve through
  `Piece.colorSlot`, which `Combine` keeps at the *smallest* slot of the merged set: after `1c`
  absorbs piece 3, the merged piece is still `1`, and naming `3` is an error rather than a no-op.
- **`r+vw` sends axis +v → +w**; `r-vw` is the inverse, i.e. the `(w,v)` rotation. `r+wz` and
  `r-zw` are the same move and print identically.
- **The default pivot is the lexicographically smallest current cell of the piece** — the
  RULES.md/p.py convention, *not* `GameLevel`'s own null-pivot default (the piece centroid). The
  verifier always passes an explicit pivot origin, and generated files spell the pivot out.
- **A trailing combine is implicit.** In shape mode the verifier combines whatever is left at the
  end, so a file need not spell out the bookkeeping; an explicit `Nc` is still available for
  sequences that must combine mid-way.
- The extension is not `.json` on purpose: a stray JSON file in the levels folder invites being read
  as a level.

Hand-writing a file is the intended way to cover levels the search cannot crack — the interlocking
ones, where getting the pieces there *is* the puzzle. Once the file exists the sweep verifies it in
milliseconds and never searches that level again.

## What the search can and cannot do

`AssemblySolver` is exhaustive within its budget: its `None` is a real proof of unsolvability (it
enumerates placements over the proper rotation group only — a mirrored tiling is correctly rejected,
the game has no reflection move). Symmetry breaking over congruent pieces and a subset-sum pruning
of the uncovered region keep it fast; the pentomino-sized levels are milliseconds.

`PathSearch` is **incomplete by design**: weighted best-first, budget-capped, aimed at a handful of
candidate target assemblies rather than all of them. Its "found" is a lead that the verifier
confirms; its "not found" says nothing about the level. In shape mode the compound may be built
anywhere, so each tiling is tried in every proper global rotation, shifted as close to where the
pieces already are as the envelope allows, and the nearest few are searched.

## Two traps this hit already

Both were caught only because a result looked implausible — worth knowing before touching this code.

**Pieces are not necessarily face-connected.** The exact cover's region prune ("every connected
component of the uncovered region must be a subset sum of the remaining piece sizes") is false for a
disconnected piece, which can fill parts of two components at once. There are levels built on
exactly that (one is named *"2 congruent pieces, consisting of 4 separate parts"*). Applying the
prune unconditionally cut valid branches; the search then completed empty and reported it as a
*proof* of unsolvability — for **114 perfectly solvable levels**. The prune is now gated on a
per-level connectivity check (`piecesAreConnected`), and the cell-selection prune that replaced most
of its value (a cell no remaining placement can cover) is sound either way. Regression:
`Assembly_HandlesDisconnectedPieces`.

**`,` has two jobs in the notation.** It separates moves *and* the coordinates inside a pivot
(`@3,0,0,0`). Splitting a move list on commas tears every generated rotation into four unparsable
fragments — 80 freshly written, freshly verified solution files were unreadable on the next run.
The list is tokenised now, and a pivot is capped at 3–4 coordinates so a comma-separated list stays
unambiguous. Regressions: `Notation_PivotCommasAreNotMoveSeparators`,
`SolutionFile_SurvivesTheRoundTripItIsWrittenFor`.

The shared moral: the *reasoned* verdicts are the fragile ones. Nothing false ever reached the
"solved" side, because that side never reasons — it replays.

## Running it

```
cd Packages/d4bb
dotnet test --filter FullyQualifiedName~LevelSolvabilityTests   # one case per level, writes .moves
dotnet test --filter FullyQualifiedName~SolverCoreTests         # the solver's own guards
dotnet test --filter Name~Report_AllLevels                      # one table for all levels
```

The per-level cases are the gate, and they fail **only on a proof of a problem**: an unsolvable
level or a solution file that stopped replaying. A level the search merely could not solve is
reported inconclusive, because that is what "we did not find one" honestly means.

## Relation to `tools/puzzle`

`tools/puzzle/p.py` serves a different purpose — measuring how hard levels are for an *agent* to
solve by reasoning — and its `RULES.md` forbids solvers for exactly that reason. This package is
level QA, not a rating aid: an agent doing the difficulty task must not use it, and must not read
the generated `.moves` files.
