# Stateless Planetary Erosion Rendering Plan

## Objective

Render planetary-scale, erosion-shaped terrain from one deterministic spherical
field, from orbit to ground, without making cube faces, cache residency, request
order, or the renderer into competing terrain authorities.

The live CultMath kernel is phase-zero scaffolding. It proves that C# and HLSL
can share a point-evaluable planet-space contract and that Zyphos can consume
it. It is not yet a faithful implementation of the advanced erosion filter, a
measured CPU/GPU parity surface, or a production planetary renderer.

## Current mechanism

CultMath now owns the source-grounded erosion filter in matching C# and HLSL,
including deterministic integer cell hashing, physical wavelength selection,
and a measured compute-shader parity surface. Zyphos can evaluate that field
directly and can generate bordered cube-sphere pages whose overlapping samples
and GPU-reduced summaries have dedicated tests.

The renderer integration is in progress. A camera-selected root-face page can
be generated into persistent D3D12 buffers and sampled by the planet shader,
with direct field evaluation retained for missing pages. This is not yet the
production planetary renderer: page summaries are not yet connected to the
live intersection bounds, the resident set is not yet a quadtree, and stable
parent/child residual transitions have not been built.

## Progress ledger

| Phase | State | Evidence |
|---|---|---|
| 1. Faithful filter | Complete | C# reference fixtures and adapted HLSL contract |
| 2. CPU/GPU parity | Complete | Compute readback comparison over 2,052 samples |
| 3. Physical bands | Complete | Wavelength selection, fractional terminal octave, unresolved bound |
| 4. GPU pages | Complete | Spherical borders, sibling seam tests, GPU summary reduction |
| 5. Page-backed intersection | Complete | Persistent page and summary buffers, conservative bounds/steps, bracketed refinement, radial hit parity |
| 6. Quadtree transitions | In progress | Residual atlas and lifecycle probes complete; visible no-pop capture remains |
| 7. Unified differentials/materials | Complete | World-gradient pages, composed radial normals, shared ridge/gully material evidence |
| 8. Profiling/backend decision | Not started | No frame-time or memory-budget claim yet |

“Complete” here means the phase exit criterion has evidence. It does not mean
planetary-scale rendering as a whole is complete.

## Authority map

- **Owner:** CultMath owns the deterministic spherical erosion operator and its
  matching C# and HLSL semantics.
- **Inputs:** unit planet direction, base height, world-distance tangent
  gradient, planet seed, planet radius, requested frequency band, and bounded
  erosion parameters.
- **Outputs:** radial height residual, tangent gradient, ridge mask, gully mask,
  drainage hint, and declared unresolved error.
- **Derived state:** cube-sphere pages, page borders, min/max reductions,
  residency records, render meshes, and debug projections are disposable
  lowerings.
- **Client policy:** Zyphos owns terrain composition, parameter presets,
  geological meaning, biome use, and authored overrides.
- **Forbidden writers:** face-local terrain noise, tile-local seeds, seam repair
  passes, renderer-only erosion logic, and caches that change terrain when
  evicted.
- **Shared path:** direct compute evaluation, CPU page baking, GPU page baking,
  reload, and LOD transitions evaluate the same field contract.
- **Deletion line:** remove direct full-spectrum erosion from the repeated
  sphere-tracing loop once page-backed geometry proves equivalent. Keep direct
  evaluation as a verification oracle. The visible fallback is the nearest
  resident ancestor band; the root fallback is the uneroded base field until a
  root page is available.

## Invariants

1. The same seed and spherical point produce the same terrain regardless of
   cube face, tile level, request order, cache state, or evaluation backend.
2. Parent and child pages share identical low-frequency terrain. A child adds
   only the residual band it can represent.
3. Cube faces are charts and scheduling domains, never terrain owners.
4. Geometry displacement, visible normals, material masks, and CPU queries
   describe the same final field.
5. Cache eviction affects latency and fidelity availability, not world truth.
6. Frequencies above the projected or page Nyquist limit do not enter geometry.
7. Visual erosion does not claim watershed, sediment, lake, or river authority.
8. SDF stepping remains conservative throughout page arrival and LOD blending.

## Target rendering pipeline

```text
planet seed + spherical position + client terrain policy
                         |
                         v
              canonical spherical field
                         |
             GPU compute or CPU oracle
                         |
                         v
       cube-sphere height/gradient/material pages
                         |
        quadtree residency + frequency-band LOD
                         |
                         v
       conservative displaced-sphere intersection
                         |
                bracketed height refine
                         |
                         v
       differential normal + material evaluation
                         |
                         v
              diegetic PBR presentation
```

## Surface-page contract

Each resident page represents one cube-sphere quadtree tile and stores an
interior region plus a measured filtering border. Initial measurements should
start at 128 by 128 interior samples and 4--8 border texels; these values are
experiments, not doctrine.

Required channels:

- radial displacement;
- two-component tangent gradient in a declared tile tangent frame;
- ridge and gully masks;
- material or climate evidence required by the client;
- validity/confidence and active frequency-band metadata.

Required summaries are minimum and maximum displacement, unresolved height
error, maximum slope or curvature, angular texel size, included wavelength
range, field version, and parameter identity.

Borders sample the canonical spherical function beyond face and tile edges.
They do not copy a neighbor's potentially missing page. Seam truth therefore
does not depend on residency.

## Geometry and intersection

The broad phase intersects a sphere expanded by the selected tile's maximum
displacement. The near-surface phase refines the root against the page-backed
radial height field. Tile summaries supply conservative bounds for stepping and
culling.

The current general SDF path may remain the first backend, but near the terrain
root it should use a bracketed method rather than assume an unproven global
Lipschitz bound. A later tessellated cube-sphere or mesh-shader backend may
render opaque terrain more cheaply. Both consume the same pages and cannot
redefine terrain.

## Differential and normal contract

For unit direction `d`, planet radius `R`, radial height `h`, and
world-distance tangent gradient `grad(h)`, the surface is

```text
P(d) = (R + h) d
```

and the normal is derived from the same differential, approximately

```text
N = normalize((R + h) d - grad(h))
```

The exact scaling must be derived and tested for the selected gradient units.
Cube-face UV differences are not the visible-normal authority. Authored
residuals publish compatible derivatives or use the same bounded sampling
contract.

## LOD and frequency bands

The terrain is one spectrum evaluated through different filters, not separate
orbital and ground worlds.

- Express erosion scale as physical wavelength using planet radius.
- Select octaves from projected footprint and page sampling rate.
- Fade bands over a narrow transition interval.
- Store children as shared parent low frequencies plus child-resolvable
  residual bands.
- Blend residual contribution; never blend two independent full terrains.
- Keep parent pages renderable while children generate or stream.

Orbital views consume continental and major-relief bands. Flight adds regional
drainage and ridge bands. Ground adds local geometry and may lower smaller
omitted bands into normal/material detail from the same field.

## Material use

Ridge, gully, slope, height, climate, and authored masks are material evidence.
They may inform exposed rock, soil, moisture, snow, vegetation, and client
overrides. Drainage hints are not guaranteed rivers. Saga or a later hydrology
organ owns conserved flow, watershed topology, lakes, sediment, and climate
coupling.

## Implementation roadmap

### Phase 1: faithful filter and reference fixtures

- Replace the approximation with a source-grounded advanced erosion filter.
- Implement cell pivots, fade handling, stacked fading, partial normalization,
  straight-gully slopes, ridge/crease rounding, and drainage outputs.
- Record provenance and MPL obligations for adapted reference code.
- Create fixed CPU fixtures from the reference implementation.

Exit: CultMath C# matches reference samples and exposes the complete contract.

### Phase 2: measured CPU/GPU parity

- Evaluate fixed spherical samples in a small compute shader.
- Read results back and compare height, gradient, and masks against C#.
- Replace transcendental hashing with integer hashing if strict cross-device
  determinism requires it.
- Document ordinary and singular-region tolerances.

Exit: parity is measured over thousands of samples, not inferred from similar
source text.

### Phase 3: physical scale and band selection

- Add planet radius and wavelength-based parameters.
- Select octaves from requested world-space footprint.
- Prove low-frequency identity when higher bands are absent.
- Add broad, regional, local, and shading-only band presets.

Exit: planet radius does not silently alter geological scale and band limiting
does not rearrange broad terrain.

### Phase 4: GPU surface-page generation

- Generate displacement, tangent gradient, ridge, gully, and material evidence.
- Sample borders through the spherical field across cube adjacencies.
- Reduce displacement bounds and error/slope summaries.
- Key pages by planet, field version, parameters, tile, and frequency band.

Exit: independently generated overlapping pages produce identical samples.

### Phase 5: page-backed terrain intersection

- Add page lookup to the Zyphos intersection path.
- Use min/max summaries for broad-phase culling and conservative stepping.
- Add bracketed near-surface root refinement.
- Preserve direct compute evaluation as the oracle. Use the nearest resident
  ancestor, or the base field before root residency, as the visible fallback.
- Remove repeated full-spectrum erosion from page-backed sphere tracing.

Exit: direct and page-backed hits agree within declared page-filter error.

### Phase 6: stable quadtree transitions

- Select the cut by projected geometric error and residency.
- Generate child residual bands relative to parent low frequencies.
- Fade residuals without double counting.
- Exercise orbital descent, lateral flight, teleport, and reload.

Exit: no visible popping, crawling, or terrain rearrangement.

### Phase 7: differential shading and materials

- Derive normals from composed displacement differentials.
- Incorporate authored residual gradients.
- Feed terrain evidence into client material policy.
- Lower unresolved bands into matching normal/material detail where useful.

Exit: geometry, lighting, materials, and CPU queries agree on one surface.

### Phase 8: performance and backend decision

- Measure per-ray calls, octave cost, page generation, occupancy, residency,
  cache hit rate, and frame cost from orbit to ground.
- Compare page-backed SDF rendering with tessellated or mesh-shader patches.
- Keep one terrain contract regardless of backend.

Exit: the chosen lowering fits named frame and memory budgets.

## Verification matrix

Numerical probes cover repeatability, CPU/GPU parity, scale invariance,
band-limited identity, direct-versus-page error, and finite behavior near weak
gradients and basis switches.

Topology probes cover all twelve cube edges, all eight corners, sibling and
parent/child neighbors, independent request order, eviction, and page-border
overlap at several levels.

Timeline probes cover direct load, orbital descent, cube-edge crossing,
transition midpoint and arrival, missing-page fallback, page arrival, eviction,
regeneration, reload, and field-version change.

Visible debug views expose face/tile/level, residency, field version, active
wavelengths, height bounds, tangent gradient, final normal, ridge/gully masks,
parent residual blend, direct-minus-page error, and cross-edge discontinuity.

## Performance questions

- How many terrain evaluations occur per ray and resolved pixel?
- What is the cost per octave on target GPUs?
- What page size and border minimize generation and filtering waste?
- Can page generation overlap rendering without starving the D3D12 queue?
- What page budget preserves orbital and ground traversal locality?
- Does conservative SDF refinement remain competitive with opaque patches?

No performance claim follows from a headless boot or successful shader compile.
Measure the actual visible path.

## Immediate next cut

Phase 6 replaces the single camera-face page with a projected-error resident
quadtree:

1. Define one page identity containing field version, parameter identity, tile,
   and represented wavelength band.
2. Keep all six root pages resident so the root fallback is stable across
   camera-face changes.
3. Select a quadtree cut from projected geometric error and current residency;
   requests do not become visible until their page and summary are complete.
4. Generate each child as only the newly resolvable residual band over its
   parent's identical low-frequency field.
5. Blend residual weight during arrival and eviction. The parent remains the
   terrain owner until the child reaches full weight, and no path blends two
   independent full-spectrum heights.
6. Probe descent, lateral edge/corner crossing, teleport, reload, page arrival,
   and eviction at transition midpoint as well as settled state.

## Sources

- Rune Skovbo Johansen, *Fast and Gorgeous Erosion Filter*:
  <https://blog.runevision.com/2026/03/fast-and-gorgeous-erosion-filter.html>
- Alexander Goslin, *Terrain Diffusion / InfiniteDiffusion*:
  <https://xandergos.github.io/terrain-diffusion/>
- Existing Fensalir cube-sphere and field architecture:
  `research/rendering/cube-sphere-fractal-brushes.md` and
  `docs/perfect-machine-architecture.md`.
