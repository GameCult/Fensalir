# Fensalir planet work: validation and reorientation beside Asura

Status: Imagination pass 1, 2026-09-30. Nothing committed and no working tree
switched. The build probes ran on Yggdrasil through `ygg-verify.sh`, using
composite snapshots in a scratch repo. That repo lives in the scratchpad, not
in any project.

Adopting instruction (operator, 2026-09-30): "Fensalir planet work is old,
needs validation and reorientation beside the Asura campaign".

## Pinned heads (every `file:line` below is against these)

| Repo | Ref | SHA | Note |
| --- | --- | --- | --- |
| Fensalir | `origin/main` = local `main` | `e053486` | 3 commits past the merge base, all housekeeping |
| Fensalir | `origin/codex/geometry-ownership-migration` (the donor) | `42778c1` | 125 ahead of merge base `57b6040` (2026-05-30) |
| Fensalir | `origin/codex/leap-packed-depth` | `492d123` | the donor minus its last commit |
| CultLib | `origin/main` | `93fe1b9` | includes the bounded-least-squares merge; local `main` is `069ecc3` |
| CultLib | `origin/codex/geometry-ownership-migration` | `6a65369` | the donor's other half: 31 ahead of `a58daa5` (2026-07-12), 832 behind |
| CultLib | tags `gamecult-geometry{,-rs,-unity}-v0.1.0` | `b65619f` | pushed to origin, on `origin/codex/geometry-cultmesh-unity-release`, not in main |
| CultMath (standalone, retired) | `main` | `1c007a5` | "Point CultMath history at its CultLib home"; planetary deleted at `6a16260` |
| Asura | `main` | `63f9216` | snoise fix status recorded; the fix is on CultLib `hands/cultmath-snoise-grad` `0a5f4bf` |

## Verdict

**Retire the planet work. Do not land it.** The donor has no surviving code
owner: CultMath deleted the planetary runtime in July, and CultLib never merged
it. Asura has since claimed the planet field with a design that contradicts the
donor's central promises. Its erosion port is the one Asura already judged "not
a source". Its only consumer is a Fensalir demo client that has not changed
since 2026-05-30.

One idea from the geometry half is still live and time-sensitive: deleting the
duplicate vector types from `GameCult.Geometry`. That is separate work, but it
has a closing window. Once Asura Cut 3 ships `GameCult.Geometry.dll` into Unity,
the duplicate `CultVec3` becomes public Unity API (Q-F4).

Most of the branch is not planet work at all. 66 Bokusho commits, 10 Eve
commits, and the Leap/stereo and reservoir work ride on the same ref. They need
their own adoption decision before any ref is deleted (Q-F3).

---

## 1. Validation report

### 1.1 What the donor actually is

Grouping `git log 57b6040..42778c1` by subject:

| Slice | Commits | Dates | Size |
| --- | --- | --- | --- |
| Leap stereo depth, point cloud, reservoir history, camera fusion, loopback audio | ~20 | 05-30 to 05-31 | inside the 61-file, +8832/−279 pre-planet diff |
| Eve native surface client (Direct2D, CultUI previews) | ~17 | 05-31 to 06-02 | same |
| Bokusho brush simulation on D3D12 | 66 | 06-12 to 06-16 | `BokushoBrushSimulation.cs` +1076, `D3D12BokushoBrush.hlsl` +699, tests +1143 |
| **Planet slice** | **23** | **07-12 to 07-22** | **61 files, +2865/−359 (`5854630^..42778c1`)** |

Inside the planet slice, only the last three commits are the "geometry ownership
migration":
- `2531df5` moves Fensalir onto CultMath's July planetary runtime.
- `492d123` deletes Fensalir's `CubeFace`, `CubeFacePosition` and `CubeTileKey`.
- `42778c1` renames every consumer from `cultmath_planetary_*` to
  `gamecult_geometry_planetary_*`.

Its partner commits live in CultLib (`13e8c56..6a65369`) and CultMath (`6a16260`).

### 1.2 Merge probes (`git merge-tree --write-tree`)

- **Fensalir, donor into `origin/main`: 7 conflicts.**
  - Content conflicts in `Fensalir.sln`, `README.md`, `docs/README.md` and
    `src/Aquarium.Engine/Aquarium.Engine.csproj`.
    - The csproj conflict matters. Main's `dc35ecc` moved CultMath to
      `$(CultLibRoot)\packages\cultmath\...` (`Aquarium.Engine.csproj:4,26` at
      `e053486`). The donor still points at the standalone repo
      (`..\..\..\CultMath\src\CultMath\CultMath.csproj`, `Aquarium.Engine.csproj:25`
      and `Aquarium.Engine.Contracts.csproj:10` at `42778c1`).
  - Modify/delete conflicts in `src/Saga/{README.md,Saga.csproj,SagaOrgan.cs}`.
    Main deleted Saga as dead in `d755d9c`; the donor still migrates it.
- **CultLib, July branch into `origin/main`: 23 conflicts.**
  - 13 are source and docs, including a modify/delete on
    `CultDocumentMessagePackGenerator.cs`, which main deleted. The others are in
    `CultManagedDocument.cs`, the serialization code, `GameCult.Geometry.csproj`
    and `README.md`, `GeometryDocumentTests.cs` and `build-unity-package.ps1`.
  - 10 are prebuilt Unity DLLs and pdbs.
  - The branch is 832 commits behind. That is not a merge; it would be a
    re-implementation.

### 1.3 Build probes (Yggdrasil, `mcr.microsoft.com/dotnet/sdk:10.0`, Linux)

The probes use three composite snapshots in the scratch repo. Each lays the
three repos out as siblings, the way the csproj relative paths expect.

| Probe | Layout | Result |
| --- | --- | --- |
| **mainline** | Fensalir `e053486` + CultLib `93fe1b9` | `Aquarium.Engine.Fractal` builds. Fractal tests: **167/167 pass** |
| **then** (the July world) | Fensalir `42778c1` + CultLib `6a65369` + CultMath `b43cf49` | Restore fails first with NU1101: `CultMath 0.1.0` comes only from a git-ignored local feed (`CultLib/Directory.Build.props:18-19` at `6a65369`: `..\CultMath\.tools\local-feed`). After `dotnet pack` of CultMath `b43cf49` and passing the feed explicitly, the lib builds and the Fractal tests give **169/170**. The failure is `ProjectionTests.CheckedInProjectionReportMatchesSampler`, which passes on mainline. The donor regenerated `research/rendering/fractal-projection-report.md` against the July projection code on Windows; this was not chased. |
| **now** (donor against today's owners) | Fensalir `42778c1` + CultLib `93fe1b9` + CultMath `1c007a5` | **Fails** with 10 unique errors (`FRACTAL_LIB_EXIT=1`). Nine are CS0246 for `PlanetaryCubeFace`, `PlanetaryTileAddress` and `PlanetaryFaceCoordinate` (`FractalDslCompiler.cs:505`, `FractalStableKeyBuilder.cs:8`, `ICubeSphereProjection.cs:11`, `NormalizeCubeSphereProjection.cs:11`, `ProjectionDistortionResult.cs:8`, `ProjectionDistortionSampler.cs:14,61,68`, `TangentCubeSphereProjection.cs:11`). The tenth is CS1704: two assemblies named `CultMath` are loaded, one from the standalone repo through the donor's `Contracts` reference and one from CultLib `packages/cultmath` through `GameCult.Geometry`. The donor straddles the pre-monorepo and monorepo layouts. |

Static proof that "now" cannot build, independent of the probe: every
`Planetary*` symbol the donor consumes is absent from CultLib main.
- The donor uses these (`git grep` at `42778c1`): `PlanetaryCubeFace` (47 uses),
  `PlanetaryTileAddress` (24), `PlanetarySurfacePages`,
  `PlanetaryProjectionKind`, `PlanetaryFaceCoordinate`, `PlanetaryPageSampling`,
  `PlanetaryPageLayout`, `PlanetaryResidualResidency`, `PlanetaryLodSelector`,
  `PlanetaryGpuPageBuilder`, `AdvancedErosionParameters` and the
  `GameCultGeometry*` HLSL structs.
- Counts on CultLib `93fe1b9`: `PlanetaryCubeFace` 0, `PlanetaryTileAddress` 0,
  `PlanetaryTopology` 0.
- `src/GameCult.Geometry/Shaders/` does not exist on main, so
  `#include "GameCult.Geometry/GameCult.Geometry.hlsl"`
  (`D3D12ZyphosTerrain.hlsli:4` at `42778c1`) has nothing to resolve.

**Not run, because it needs Windows and D3D12:**
- `tests/Aquarium.Engine.Tests`: `D3D12PlanetarySurfacePageTests`,
  `D3D12PlanetaryTopologyParityTests` and `D3D12AdvancedErosionParityTests`,
  which are GPU readback parity tests;
- `Aquarium.Engine` itself (Vortice D3D12);
- the Zyphos client.

Per the workstation budget, none of this ran on Starfire. Given the verdict,
it does not need to run at all: the GPU parity would prove a runtime nobody
will keep.

### 1.4 What is still right, and what has rotted

Still right:
- The donor's direction of dependency. Algorithms sit in a library and D3D12
  resources stay in Fensalir. That matches the doctrine and Asura's own shape
  (CultLib algorithms, consumer-owned rendering).
- The July migration plan's authority sentence: "CultMath owns how numeric
  primitives behave; GameCult.Geometry owns what those primitives mean in
  space" (CultLib `docs/cultmath-gamecult-geometry-migration-plan.md:56-57` at
  `6a65369`). CultLib main adopted the same split at `63d6094` (2026-08-26),
  and `HlslMirrorTests.cs:7-15` on main enforces the CultMath side.
- The idea of deleting `CultVec2`/`CultVec3` (CultLib `317b7d5`). Main still
  carries both (see Q-F4).

Rotted:
1. **Ownership of the planet field.** The donor's objective
   (`docs/cultmath-planetary-field-runtime-plan.md:3-15` at `42778c1`) makes a
   shared library the canonical planet field for "Aetheria's daemon
   simulation, Unity's three-dimensional planet renderer, Fensalir's D3D12
   renderer, and Electron/Web strategic maps". Asura now owns Aetheria's
   planets and rules the opposite:
   - The field is HLSL only, and the CPU never evaluates it (Asura
     `docs/target.md:42-47`, invariant 2).
   - There is no landing and no surface streaming (the scope boundary).
   - The donor's authoritative CPU queries, residency LOD and QSC page
     streaming are therefore out of scope, or forbidden outright.
2. **The erosion port.** Asura's 4b-iii already rejected the July
   `spherical_erosion` because it "lerps raw phases across cells instead of
   blending phasors, which is not Phacelle" (`docs/cut-map.md:1132-1135`,
   `:1736` at `63f9216`). Phacelle is going into CultMath fresh (2a-ii-b).
3. **Derivatives.** Zyphos's base field is ad hoc sine plates with a
   central-difference gradient (`D3D12ZyphosTerrain.hlsli:113-129` at
   `42778c1`). That is the pattern Asura's invariant 8 exists to prevent.
4. **The code owner is gone.** CultMath deleted the planetary runtime
   (`6a16260`, reachable from CultLib main through the subtree). The receiving
   CultLib branch never merged. On mainline the runtime is orphaned: it exists
   only on unmerged branches and the three pushed `v0.1.0` tags.
5. **The build contract.** It needs an untracked local NuGet feed (see 1.3)
   and paths from before the monorepo.
6. **The CultLib geometry half diverged from main's ruling.**
   - The branch absorbs CSG into `GameCult.Geometry` (`6b44f96`). Main ruled
     that `GameCult.Geometry.Csg` owns CSG production behaviour (`63d6094`,
     `src/GameCult.Geometry/README.md`), and that repo shipped `distance(p)`
     at `d84acb6`.
   - The branch also adds a geometry worker, `CultGeometryWorkerDocuments`,
     `CultGeometryWorkerProvider` and "v2 contracts". None of these exist on
     main, and main's documents never moved to v2.

---

## 2. Ownership table

| Piece (donor) | Where it sits in the donor | Owner today | Disposition |
| --- | --- | --- | --- |
| Planet field definition, base field and erosion as one function | CultLib branch `src/GameCult.Geometry/Planetary/PlanetaryField.cs`, `Shaders/Planetary.hlsl` | **Asura** (field in HLSL, per body archetype) | Retire. Duplicates Asura's field |
| Advanced erosion filter (Johansen port) and `SphericalErosion` | branch `Planetary/AdvancedErosionFilter.cs`, `SphericalErosion.cs`, `Shaders/AdvancedErosionFilter.hlsl` | **Asura** 4b-iii (`AsuraErosion.hlsl`, fresh port with provenance) | Retire. Asura already ruled it "not a source" |
| Phacelle and noise primitives | embedded in the erosion port | **CultMath** (2a-ii-b `phacelle`, `CultPhasor`) | Retire the embedded copy |
| Cube-sphere/QSC topology, tile addresses, page layout, residency, LOD selection, patch meshes, radial refinement | branch `Planetary/PlanetaryTopology.cs`, `PlanetaryPages.cs`, `PlanetaryResidency.cs`, `PlanetaryPatch.cs`, `PlanetaryRadialRefinement.cs` | **No live consumer.** Asura meshes with surface nets and tiles per quad; streaming is out of scope | Retire. Revive only through a new campaign with a named consumer (Q-F1) |
| CPU ray, region and great-circle queries for an "Aetheria daemon" | branch `Planetary/PlanetaryQueries.cs` | **Forbidden** by Asura invariant 2. The dock-site query is Asura's | Retire |
| Map projections and `CMPT` tile codec (Electron maps) | branch `PlanetaryProjection.cs`, `PlanetaryMapTileEncoding.cs`, TS decoder | No consumer exists or is planned | Retire |
| `org.gamecult.geometry` Unity package and planetary viewer sample | branch `unity/org.gamecult.geometry/**`, tags `b65619f` | Superseded: Asura Cut 3 ships Geometry inside `org.gamecult.cultlib` | Retire the branch. Keep the pushed tags as immutable history (Q-F3) |
| Frequency-band selection by sample footprint | branch `Planetary/ErosionFrequencyBands.cs` | **Asura** (band limits per lowering; target invariant 8, Q11) | Retire. Same concept, and Asura implements it fresh |
| D3D12 page buffers, root parameters 29-32, dispatch, patch raster, debug modes | Fensalir `D3D12Renderer.cs:99-102,223` and about 157 planetary lines; `AquariumRenderPlan.cs:526-565` (render ABI) at `42778c1` | **Fensalir** would own it if Fensalir rendered planets. Its only producer is the retired runtime | Retire with the runtime |
| DXC include resolver for `Packages/org.gamecult.cultmath/shaders/` and `GameCult.Geometry/` | Fensalir `D3D12ShaderCompiler.cs:68-102` at `42778c1` | Fensalir. On main no shader includes CultMath HLSL at all | Retire. Its search paths are also stale (the standalone `CultMath/shaders`, pre-monorepo). Rebuild against `packages/cultmath/shaders` when a Fensalir shader first includes CultMath |
| Zyphos terrain shaders, page and patch entry points, `ZyphosPlanetarySurfacePages` | Fensalir `src/Aquarium.Zyphos/**` at `42778c1` | Fensalir's demo client. Main's Zyphos keeps its pre-July analytic sphere | Retire |
| Fensalir's `CubeFace`, `CubeFacePosition`, `CubeTileKey` and the projection code in `Aquarium.Engine.Fractal` | **Main keeps them.** The donor deleted them in favour of Geometry | **Fensalir** (fractal DSL, projection reports; 167 tests green on main) | Keep on main; nothing to do. The donor's deletion is void once the runtime is retired |
| Planetary flow organ (ocean and atmosphere advection), documentation only | Fensalir main: `docs/planetary-flow-{organ,nn-architecture,implementation-plan}.md` (289/462/185 lines); `docs/README.md:39-44`; `state/map.yaml:114-120` and the flow `next_actions` entries after `:171` | Fensalir, as plans. No code exists; last touched 2026-05-29. Not in Asura's scope (no atmospheres or oceans) | Q-F2 |
| Vector-type unification (delete `CultVec2`/`CultVec3`/`CultRect` in favour of CultMath `float2`/`float3`/`rect`) | CultLib branch `317b7d5`, `364c9cd` | **CultMath** owns the vector vocabulary (main `63d6094`). Main still has the duplicates: `CultGeometryPrimitives.cs:11,62,128` and the `origin` parameter of `CultGeometryIsoSurface.cs:42` and `CultGeometrySurfaceNets.cs:47` | Q-F4: a fresh cut on main, not the branch code |
| CSG absorption, geometry worker, v2 schemas, Rust `gamecult-geometry-rs` | CultLib branch | CSG: `GameCult.Geometry.Csg` (main's ruling). The rest have no owner and no consumer | Retire with the branch |

### Duplication against Asura (one side is cut, not kept in parallel)

| Concept | Donor | Asura | Side cut |
| --- | --- | --- | --- |
| Planet shape function | `PlanetaryFieldDefinition` plus base-field provider (CPU and HLSL) | the field `f(p) = |p| − (1 + h(u))`, HLSL only | donor |
| Erosion | July port with raw-phase lerp | 4b-iii fresh port, exact Phacelle, provenance file | donor |
| Level of detail | QSC pages, residency, projected-error LOD | surface-net grid resolution plus per-body tile N | donor. Asura's scope rules streaming out |
| Band limiting | `ErosionFrequencyBands` | per-lowering band limit (Q11) | donor |
| Unity packaging | `org.gamecult.geometry` | `org.gamecult.cultlib` (Cut 3, default A) | donor |

---

## 3. Cuts

Every cut below is deletion or documentation, with no new code. Numbers are
prefixed `P` so they cannot be confused with Asura's cut numbers.

### P0. Operator rulings (Self; blocks everything below)

Q-F1 and Q-F3 must be ruled before P1. Q-F2 gates P3. Q-F4 gates P4.

### P1. Archive the refs, then delete them (Fensalir, CultLib, standalone CultMath)

- **Precondition:** Q-F3 ruled. The non-planet work on the Fensalir donor
  (Bokusho, Eve surface, Leap) must be either archived with it or adopted
  separately first.
- **Deletes first:**
  - Fensalir remote branches: `codex/geometry-ownership-migration` (`42778c1`)
    and `codex/leap-packed-depth` (`492d123`, a strict ancestor).
  - CultLib remote branches: `codex/geometry-ownership-migration` (`6a65369`)
    and `codex/geometry-cultmesh-unity-release` (`b65619f`).
  - The standalone CultMath remote `codex/geometry-ownership-migration`
    (`b43cf49`), already an ancestor of CultMath `main`, so deleting it loses
    nothing.
- **Then:**
  - An annotated tag at each unique tip, so every commit stays reachable:
    - Fensalir `archive/geometry-ownership-migration` → `42778c1`;
    - CultLib `archive/geometry-ownership-migration` → `6a65369`;
    - CultLib `archive/geometry-cultmesh-unity-release` → `b65619f`, if a
      branch ref is wanted beyond the release tags that already pin it.
  - The tag message names this map and the ruling.
  - The published `gamecult-geometry{,-rs,-unity}-v0.1.0` tags stay: they are
    released, immutable artifacts.
- **Authority map:**
  - Owner of planet shape: Asura. Owner of numeric primitives: CultMath.
  - Derived: the archive tags are history only, and nothing resolves a build
    from them.
  - Forbidden writers: any branch that could be merged back to revive
    `Planetary*` in CultLib, or `cultmath_planetary_*` in CultMath.
  - Shared paths: none. No mainline build references these refs; the mainline
    probe proves Fensalir main builds without them.
- **Verification:**
  - `git merge-base --is-ancestor <old tip> archive/<name>` for each deleted
    ref, run before deleting.
  - `git ls-remote --heads origin | grep -c geometry-ownership` gives 0 in all
    three repos.
  - Mainline probe re-run (Fensalir `e053486` + CultLib main): 167/167.
  - Negative: `git grep -c "Planetary" origin/main -- src packages` in CultLib
    stays at its current count (only `HlslMirrorTests.cs:12,14`).
- **Subtraction:** 5 remote branch refs, net of the 2–3 archive tags. The
  unmerged surface removed from view is Fensalir +11,650/−591 (116 files) and
  CultLib +14,933/−317 (156 files). No mainline lines change.
- **Hands budget:** about 20k tokens. It is ref surgery and needs the
  operator's credentials for the pushes.

### P2. Asura documents: replace "do not delete it" with the ruling (Asura, docs only)

- **Deletes first, at Asura `63f9216`:**
  - `docs/target.md:110-113`: the "Not consumers" bullet naming the branches.
  - `docs/cut-map.md:814-820`, Cut 3's "Not a source", including
    "do not delete it; see findings".
- **Then:**
  - One sentence in each place: the July planetary line was retired on
    2026-09-30 and archived at the tags from P1; Asura is the sole owner of
    planet shape.
  - `cut-map.md:1132-1135` and `:1736` keep their technical reason (raw-phase
    lerp, not Phacelle). They point at the archive tag instead of the branch
    names.
  - `target.md:252` (the substrate fact about the `unity-v0.1.0` tag) stays
    true and is unchanged.
- **Authority map:** `target.md` owns the ends, so the retirement ruling is
  recorded under its dated operator rulings. The cut map only points at it.
- **Verification:**
  - `rg -n "codex/geometry-" docs/` in Asura gives 0 lines.
  - `rg -n "do not delete it" docs/` gives 0.
- **Subtraction:** about −8 +4 lines.
- **Timing:** Self can do this in the same pass that records the rulings. It
  does not touch 2a-ii.

### P3. Fensalir main: record the disposition; flow docs per Q-F2 (Fensalir `main`)

- **If Q-F2 = retire. Deletes first, at `e053486`:**
  - `docs/planetary-flow-organ.md`, `docs/planetary-flow-nn-architecture.md`
    and `docs/planetary-flow-implementation-plan.md` (936 lines);
  - `docs/README.md:39-44`;
  - `state/map.yaml:114-120`, and the flow entries in `next_actions` (the
    bullets beginning "Build the planetary flow organ" and "For planetary flow
    modeling", about 10 lines after `:171`).
- **If Q-F2 = park:** add one "Status: parked 2026-09-30, no consumer" line at
  the top of each of the three docs instead.
- **Then, either way:**
  - One line in `state/map.yaml`: Fensalir renders no planet terrain beyond
    the Zyphos demo's analytic sphere. Planet shape for GameCult games is
    Asura's. If Fensalir ever renders an Asura body, it lowers Asura's field
    HLSL and owns only D3D12 resources.
  - Fix `README.md:141` from "planetary/fractal terrain demo" to "fractal
    terrain demo", only if the operator agrees the demo carries no planet
    ambition. Otherwise leave it.
- **Authority map:** Fensalir owns its renderer and demos. It claims no planet
  field.
- **Verification:**
  - `git grep -n -i "planetary" origin/main -- docs/README.md state/map.yaml`
    matches the intended remainder.
  - The mainline Fractal probe still gives 167/167; docs cannot break it.
- **Subtraction:** retire gives about −950 lines and +2. Park gives +3.
- **Hands budget:** about 15k tokens.

### P4. CultLib: collapse `CultVec2`, `CultVec3` and `CultRect` into CultMath, before Asura Cut 3 (only if Q-F4 = A)

- **Why now:**
  - Asura Cut 3 (`docs/cut-map.md:801`) ships `GameCult.Geometry.dll` in
    `org.gamecult.cultlib` 1.1.0.
  - Both extractors take `CultVec3 origin`, so the duplicate type becomes
    Unity public API in that release. Deleting it afterwards is a semver
    major.
  - `CultGeometrySurfaceNets.cs:83` already converts `CultVec3` to `float3`
    internally.
- **Deletes first, at CultLib `93fe1b9`:**
  - In `src/GameCult.Geometry/CultGeometryPrimitives.cs`:
    - `CultVec2` (`:11-60`) and `CultVec3` (`:62-126`);
    - `CultRect` (`:128-197`), which duplicates CultMath `rect`
      (`packages/cultmath/src/CultMath/rect.cs:7`, added 2026-08-07).
  - `CultCircle` and `CultSphere` stay; they are spatial meaning, and that is
    Geometry's. They are rebuilt on `float2`/`float3`.
- **Then:**
  - `CultGeometryIsoSurface.Extract(..., float3 origin = default, ...)`
    (`:42`) and `CultGeometrySurfaceNets.Extract(..., float3 origin = default,
    ...)` (`:47`); delete the conversion at `:83`.
  - Update the tests: `GeometryDocumentTests.cs` (16 uses), `IsoSurfaceTests.cs`
    and `SurfaceNetsTests.cs`.
  - Update `src/GameCult.Geometry/README.md`.
- **Explicitly out of scope:** CultMesh's native-slice value-type names
  `"CultVec2"` and `"CultVec3"` (`packages/cultmesh-ts/test/cultmesh.test.ts:1717,2440,2453`,
  and `src/GameCult.Mesh/docs/public-api.md`). Those are wire vocabulary with
  their own owner. Record them as a finding for the CultMesh owner; do not
  rename them here.
- **Authority map:**
  - Owner: CultMath owns vector and rectangle values. Geometry owns what they
    mean in space.
  - Forbidden writers: any type in `GameCult.Geometry` that carries x/y/z
    fields.
- **Verification (Yggdrasil):**
  - `dotnet test tests/GameCult.Geometry.Tests`, with the per-file counts. The
    Surface Nets byte-identical fixtures from Asura Cut 2 (31 fixtures) must
    stay byte-identical.
  - Negative: `git grep -n "CultVec\|CultRect" -- src/GameCult.Geometry tests/GameCult.Geometry.Tests`
    gives 0.
  - Stryker `--since:93fe1b9` on the touched files.
- **Subtraction:** about −190 lines of duplicate value types, plus test
  churn, net negative.
- **Sequencing:** after Q13 (it blocks Cut 3 anyway) and before Cut 3's
  release tag. It can run beside 2a-ii-b.
- **Hands budget:** about 60k tokens.

---

## 4. Sequencing against Asura

```text
Asura:   2a-ii-a snoise fix (Hands done 0a5f4bf, Soul running) -> 2a-ii-b Phacelle -> 2a-ii-c cultmath 0.3.0 (with bounded LS, already on main 93fe1b9)
                                                                                   \
         Q13 (Geometry CultReference strings) ------------------------------------> Cut 3 cultlib-unity 1.1.0 ---> 4a ...
                                                   P4 (Q-F4 = A) ------------------/   (must land before this tag)

This map: P0 rulings -> P2 (Asura docs, any time) ; P1 (refs, after Q-F3) ; P3 (Fensalir docs, after Q-F2)
```

- None of P1–P3 touches CultMath or CultLib source, so none can collide with
  the snoise fix, Phacelle, or the 0.3.0 release that carries bounded least
  squares.
- P4 is the only cut on Asura's critical path. It must land before Cut 3's
  tag, and it touches `GameCult.Geometry`, which Cut 3 packages. Run it in its
  own worktree from `main`, never in the snoise worktree.
- The Asura campaign gains nothing from the donor. 4b-iii stays a fresh port.

---

## 5. Operator questions (one fork each)

### Q-F1. What is a planet in Fensalir *for*?

Asura answers this for Aetheria: orbital tiny planets, no landing. The donor
answered it for "every renderer, including an Aetheria daemon and strategic
maps", and that answer has been overtaken. What remains is Fensalir's own
Zyphos demo: the domain catalog runs from planet to continent, forest, tree,
leaf and pebble field (`ZyphosSpatialDomainCatalog.cs` on main). That is
landing-scale LOD, which Asura rules out.

- **A. No planet terrain in Fensalir for now.** Retire the donor (P1–P3). If a
  GameCult game later wants planets in Fensalir, it lowers Asura's field HLSL.
- **B. Zyphos needs landing-scale planets.** That is a new campaign with its
  own target. It would decide whether streaming LOD extends Asura's field
  (one field, two lowerings) or is a separate organ. It must not revive the
  July runtime as a second planet-field owner.
- **C. Keep the donor alive as a Fensalir-private runtime.** This recreates a
  second planet-field authority beside Asura, over an orphaned library.

**Recommended: A.** Nothing on main consumes the donor. The Zyphos client has
not moved since 2026-05-30. B stays open as a future campaign and loses nothing
by waiting.

### Q-F2. The planetary *flow* documents in Fensalir: what are they for now?

There are three documents (936 lines) for a cube-sphere ocean and atmosphere
advection organ with neural-network budgets (FlowNet-S/M/T). No code exists,
they were last touched 2026-05-29, and their only client is the Zyphos demo.
Asura is not affected. Separately, Asura's target queues "the flow field as the
single owner of motion in Aetheria" as a later campaign. That is a different
flow.

- **A. Retire** (P3 deletes them). If Zyphos oceans return, their campaign
  starts from a target, not from these plans.
- **B. Park them** with a status line.

**Recommended: B, weakly.** They carry real research (datasets, model sizing),
nothing depends on them, and parking costs 3 lines. Choose A if you want the
docs tree to describe only live machinery.

### Q-F3. The non-planet work that rides on the same Fensalir branch

The donor ref also holds 66 Bokusho commits (a D3D12 port beside the TS/WebGPU
`bokusho` repo), 10 Eve native-surface commits, and the Leap/stereo depth,
reservoir and loopback-audio work. None is on Fensalir main.

- **A. Archive the whole ref** under one tag in P1, and route each of those
  lines to its own adoption decision later.
- **B. Adopt them before touching the ref.**

**Recommended: A.** An archive tag keeps every commit reachable, so B is still
possible afterwards. This map does not judge those lines.

### Q-F4. Delete Geometry's duplicate vectors before Asura Cut 3 ships them to Unity?

What is `CultVec3` for? Nothing that `float3` does not already do, and the
surface-nets code converts it straight to `float3` (`CultGeometrySurfaceNets.cs:83`).
The July branch deleted it, and main never did.

- **A. P4 before Cut 3:** no semver cost, and about −190 lines.
- **B. Ship Cut 3 as is:** a later deletion is a major bump for
  `org.gamecult.cultlib`.

**Recommended: A.** It is the one piece of the geometry half that is right, and
it gets more expensive after Cut 3's release.

## 6. Findings not assigned to a cut

- **Unreproducible build (CultLib `6a65369`).** The July CultLib branch
  restores `CultMath 0.1.0` from a git-ignored local feed. The "Fensalir and
  Aetheria green" receipts in its progress document
  (`docs/cultmath-gamecult-geometry-migration-progress.md`, stage 6) could not
  have been reproduced from committed sources alone. The branch is being
  retired, so this is recorded as a scar and needs no fix.
- **A failing test in the donor's July world.**
  `ProjectionTests.CheckedInProjectionReportMatchesSampler` fails on Linux in
  the July composite (169/170), and the same test passes on mainline. The most
  likely cause is a platform difference in the regenerated report. It was not
  chased, because the code is being retired.
- **CultMath's `HlslMirrorTests.cs:7-15`** is a negative spelling test. It
  guards against planetary kernels returning to CultMath and is cited by Asura
  as the ruling's enforcement. Keep it; after P1 its only threat is gone, but it
  is cheap and names the boundary.
- **Stopgap note.** Composite multi-repo snapshots are how a Fensalir build
  can be probed on Yggdrasil at all: `ygg-verify.sh` takes one repo, and
  Fensalir needs sibling checkouts. The Idunn verify campaign should take a
  multi-repo revision set rather than rely on this trick.
