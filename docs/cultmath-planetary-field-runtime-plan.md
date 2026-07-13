# CultMath Planetary Field Runtime Plan

## Objective

Make CultMath the canonical, renderer-independent planetary field runtime used
by Aetheria's daemon simulation, Unity's three-dimensional planet renderer,
Fensalir's D3D12 renderer, and Electron/Web strategic maps.

The product is a deterministic, scale-aware spherical field that can be queried
on the CPU and lowered into several presentation substrates. Rendering is one
consumer. It is not the owner of planetary geometry.

The intended end state is:

> All deterministic planetary geometry, evidence, addressing, sampling,
> paging, projection, and refinement live in CultMath. Aetheria owns gameplay
> meaning and committed consequences. Renderers lower the same versioned field.

## Current mechanism

CultMath now owns the matching C# and HLSL erosion operator, physical
frequency-band selection, scale-aware field definition and sampling,
tangent/QSC cube-sphere topology, bordered page layouts, CPU residual baking and
composition, projected-error LOD selection, residual residency transitions,
renderer-neutral patch meshes, CPU ray/path/region queries, map projections,
and versioned projected tile baking. Fensalir and Zyphos consume the CultMath
topology, field, LOD, residency, page, patch-direction, and refinement functions
while retaining D3D12 buffers, bindings, dispatch, scene policy, and drawing.

Fensalir still exposes compatibility types for its existing fractal API, and
Zyphos still owns its geological base-field preset, authored brush chart, page
resource structures, shader entry points, and scene/material policy. Unity,
Electron, CultMesh tile publication, WGSL, and Aetheria daemon integration are
not implemented by the restricted pass recorded below.

The completed Fensalir implementation proves the approach, but its ownership
boundary is too renderer-shaped for Aetheria. The reusable algorithms should
move into CultMath. D3D12 resource allocation and command recording should not.

Aetheria's daemon is the authority for committed game state and simulation.
Unity objects, Electron canvases, local GPU pages, and projected map tiles are
derived state. Terrain queries that affect movement, visibility, construction,
resources, physics, or combat must be available to the daemon without booting a
renderer or depending on client cache residency.

## Restricted implementation ledger

This ledger records the pass performed without modifying Aetheria. A phase is
complete only when its stated exit criterion is proved; generic machinery alone
does not impersonate a consumer integration.

| Phase | State | Current evidence |
|---|---|---|
| 0. Contract and parity corpus | In progress | Field identity, query scale, samples, errors, topology/page/projection fixtures, D3D12 QSC readback, and equirectangular/Equal Earth HLSL parity exist; remaining projection HLSL parity remains |
| 1. Topology extraction | In progress | CultMath owns CPU/HLSL QSC mapping and page directions; Fensalir compatibility types and non-planet fractal projection APIs remain |
| 2. Authoritative CPU queries | In progress | Point, batch, double-position, ray, clearance, great-circle, and region queries exist; the forbidden Aetheria daemon smoke remains |
| 3. Page machinery extraction | In progress | CultMath owns CPU baking, residuals, summaries, composition, HLSL page interpolation/residual functions, and residency; renderer resource contracts and GPU entry points remain adapters |
| 4. Residency and patch extraction | In progress | CultMath owns LOD, lifecycle, mesh generation, QSC patch direction, and radial refinement; Unity equivalence remains |
| 5. Unity adapter and viewer | In progress | A separate `CultMath.Unity` assembly converts canonical patches and residency snapshots into Unity meshes/page payloads and compiles against Unity 6; a renderer/viewer remains, and Aetheria was explicitly out of scope |
| 6. Projections and map tiles | In progress | All named CPU projections and versioned surface-tile baking exist; publication and Electron lowering remain |
| 7. Optional WebGPU lowering | Not started | Still correctly optional |
| 8. Delete obsolete owners | In progress | Zyphos private page addressing, LOD, lifecycle, triplanar erosion composition, page interpolation, and patch direction were removed or delegated |

Current verification from this pass includes CultMath managed tests, the full
Fensalir fractal suite, a zero-warning solution build, CPU lifecycle/LOD
integration probes, advanced erosion CPU/GPU parity, QSC CPU/GPU readback,
focused shader compilation, page residual generation, all cube-edge/corner seam
probes, finite summary evidence, and page-backed radial-hit parity.

## Authority map

### Owner

CultMath owns the canonical planetary field function and the algorithms needed
to query or approximate it at a declared physical scale.

Aetheria owns planet definitions, authored parameters, gameplay interpretation,
simulation policy, and every committed consequence of a terrain query.

### Inputs

The field runtime may read:

- a stable planet identifier and field version;
- planet seed, radius, and declared coordinate frame;
- base-field and erosion parameters;
- a unit spherical direction or world-space query location;
- a physical sample footprint or requested wavelength range;
- an explicit accuracy/error budget;
- deterministic authored contributions supplied through a narrow Aetheria
  policy boundary.

### Outputs

A point query returns a `PlanetarySurfaceSample` containing at least:

- radius and radial displacement;
- planet-local and optional world-space surface position;
- tangent gradient, geometric normal, and slope;
- ridge, gully, drainage, and related field evidence;
- water-relative height when a sea datum is supplied;
- the resolved wavelength interval;
- a conservative unresolved-height bound;
- the planet and field versions used to produce the result.

Batch, region, intersection, page, and projection queries compose this same
contract. They do not define separate terrain.

### Derived state

These are caches or projections, not owners:

- cube-sphere pages, borders, child residuals, and summaries;
- quadtree residency and transition weights;
- Unity meshes, buffers, textures, and GameObjects;
- Fensalir D3D12 buffers and pipeline state;
- projected Electron/Web map tiles;
- shaded-relief, biome-color, political, or tactical map layers;
- local collision meshes and navigation grids.

Eviction or regeneration may change latency or available fidelity. It must not
change the canonical answer at the same query scale.

### Forbidden writers

These paths must not decide planetary truth:

- Unity scene lifetime, collision callbacks, or renderer-local noise;
- Electron-only projection or terrain generators;
- cube-face-local seeds or seam repair passes;
- D3D12- or ShaderLab-only erosion equations;
- cache residency, request order, or transition age;
- map palettes, labels, and visualization thresholds;
- renderer-specific gameplay compensators.

### Shared paths

Daemon point and batch queries, CPU and HLSL page baking, Unity and Fensalir
patch rendering, map-tile generation, reload, and LOD transitions identify and
evaluate the same field contract.

### Cut line

Move renderer-independent cube-sphere, page, residual, LOD, sampling, projection,
and patch algorithms out of Zyphos and Fensalir into CultMath. Delete the old
algorithm owners after each consumer delegates to CultMath. Do not preserve two
implementations behind adapters.

Keep only resource allocation, descriptor layout, dispatch, camera/clip-space
conversion, render-pipeline integration, presentation materials, and user
interaction in renderer packages.

## Invariants

1. The same field version, direction, and physical footprint produce the same
   terrain within the declared backend tolerance.
2. CPU simulation never depends on renderer availability or page residency.
3. Simulation queries explicitly declare physical footprint or wavelength
   range. There is no ambiguous full-detail `HeightAt` authority.
4. The daemon alone commits gameplay consequences. Client evaluation is
   prediction or presentation of daemon-published field truth.
5. Cube faces and map projections are coordinate charts, never terrain owners.
6. Parent and child pages share identical low-frequency terrain. Children add
   only representable residual bands.
7. Page and tile eviction affects cost and fidelity availability, not identity.
8. A map pixel and a 3D point resolving to the same spherical direction and
   footprint sample the same field.
9. Projection singularities and polar cutoffs are explicit contracts.
10. Integer hashing and field identity are exact across supported backends.
    Floating-point results carry measured tolerances and error bounds.
11. Erosion evidence does not silently claim hydrology, climate, biome,
    resource, or traversability authority.
12. CultMath core has no Unity, D3D12, Electron, or Aetheria dependency.

## Canonical package shape

```text
CultMath.Planetary
|-- Definition     field definition, version, coordinate frame, query scale
|-- Field          composition, erosion, differentials, conservative errors
|-- Topology       cube faces, tile addresses, direction/UV, seam rules
|-- Query          point, batch, ray, clearance, path, and region queries
|-- Pages          layouts, generation, residuals, summaries, composition
|-- Residency      projected-error selection, fallback, arrival, eviction
|-- Projection     forward/inverse transforms and projected tile sampling
`-- Patch          cube-sphere mesh, radial refinement, surface differentials
```

The HLSL surface mirrors these organs with small includes. Resource declarations
and executable compute/raster entry points remain adapter code so the common
functions work in Unity, Fensalir, and standalone parity probes.

## CPU query contract

The primary API is scale-aware:

```csharp
PlanetarySurfaceSample SampleDirection(
    in PlanetaryFieldDefinition planet,
    float3 unitDirection,
    in PlanetaryQueryScale scale);

PlanetarySurfaceSample SamplePosition(
    in PlanetaryFieldDefinition planet,
    double3 planetLocalPosition,
    in PlanetaryQueryScale scale);
```

`PlanetaryQueryScale` declares a footprint in metres or an explicit wavelength
range, plus an accepted unresolved-height bound. Named Aetheria policies such
as strategic, navigation, vehicle, or character scale belong to Aetheria and
lower to this physical contract before evaluation.

Required CPU compositions are:

- allocation-free point sampling and structure-of-arrays batches;
- bounded ray/surface intersection and segment clearance;
- great-circle and arbitrary path profiles;
- region min/max/slope queries with declared error;
- deterministic page baking identical to the GPU page contract;
- projection-tile sampling.

World-space APIs use double precision for astronomical positions and reduce to
a stable planet-local frame before float field evaluation. Floating-origin
policy must not perturb the field.

## Daemon integration

The Aetheria daemon publishes typed planet definitions and field versions
through CultCache/CultMesh. Simulation systems query CultMath locally in the
daemon process. A result becomes game truth only when an Aetheria simulation
owner uses it in a committed operation or frame.

The daemon may maintain CPU pages, spatial summaries, collision meshes, or
navigation grids. Every cache key includes complete field identity and query
scale. Cache misses evaluate the canonical field or report deferral; they never
substitute a different terrain function.

The initial integration targets Aetheria's current C# daemon host. If authority
moves to Rust, the CultMath semantics and parity corpus specify the Rust port.
C# and HLSL remain first-class rather than becoming Unity artifacts.

## Unity 3D lowering

`CultMath.Unity` is a thin package outside core CultMath. It owns conversion to
Unity types, graphics buffers, shader entry points, page-atlas dispatch,
camera-relative patch submission, render-pipeline integration, and materials.

It consumes CultMath HLSL for page generation, residual composition, sampling,
radial refinement, and differentials. Aetheria supplies the published planet
definition. Unity does not retain an independent planet generator.

## Electron/Web 2D lowering

A map follows one path:

```text
pixel -> inverse projection -> unit sphere direction
      -> scale-aware surface query -> evidence layer
      -> client-owned palette, labels, and interaction
```

CultMath initially owns forward and inverse transforms for:

- equirectangular;
- Web Mercator with an explicit polar cutoff;
- Equal Earth or Mollweide equal-area projection;
- orthographic globe view;
- azimuthal equidistant and equal-area local views;
- cube-face atlas;
- local tangent-plane maps.

Two delivery modes share the same identity:

1. The daemon or attached worker generates versioned projection tiles and
   publishes typed descriptors plus binary assets through CultMesh/CDN. This is
   the baseline for Electron and thin browser clients.
2. A future WebGPU lowering evaluates locally for unusual projections or highly
   interactive inspection.

The first mode provides one auditable CPU implementation without requiring a
JavaScript terrain port. WebGPU should be added only with a WGSL parity harness,
not as a hand-translated browser generator.

A projection-tile key includes planet and field version, projection and
parameters, evidence layer, tile level/coordinates, dimensions and border,
wavelength/footprint policy, and sampling/encoding version. Tiles are derived
assets. Strategic objects, territories, visibility, orders, and simulation
facts remain typed Aetheria state layered over them.

## Field definition and publication

The portable field definition is a typed document, not renderer configuration.
It contains stable parameters, coordinate conventions, component identities,
and a content fingerprint. CultCache stores it; CultMesh publishes it. JSON is
only a schema, debug, or xenos-boundary projection.

- CultMath emits geometry and field evidence.
- Aetheria decides biomes, resources, movement, settlement, and gameplay.
- Unity decides 3D material lowering.
- Electron decides map palettes, contours, labels, and overlays.

## Execution roadmap

### Phase 0: contract and parity corpus

Define field identity, coordinates, query scale, sample, and error types. Record
canonical point, seam, corner, wavelength, and projection fixtures. Separate
exact integer/hash requirements from bounded floating-point parity.

Exit: CPU, HLSL, page, and projection tests name the same field contract without
renderer-owned types.

### Phase 1: topology extraction

Move cube-face, tile-key, direction/UV, border, and seam algorithms to CultMath.
Replace Fensalir copies and test all edges and corners.

Exit: Fensalir contains no private cube-sphere terrain topology.

### Phase 2: authoritative CPU queries

Add scale-aware point/batch queries, double-to-local reduction, differentials,
bounds, ray intersection, clearance, and path profiles. Integrate an Aetheria
daemon simulation smoke.

Exit: the daemon makes a deterministic terrain decision without Unity or
Fensalir assemblies.

### Phase 3: page machinery extraction

Move page layouts, CPU baking, residual bands, summaries, sampling, and matching
HLSL into CultMath. Preserve Fensalir measurements and transition evidence.

Exit: CPU and HLSL pages match direct queries within declared unresolved bounds;
Fensalir is only a resource/dispatch adapter.

### Phase 4: residency and patch extraction

Move projected-error selection, fallback, transition weights, patch topology,
and refinement composition. Keep camera projection and drawing in renderers.

Exit: Unity and Fensalir consume the same page set and patch description for
equivalent camera/error inputs.

### Phase 5: Unity adapter and Aetheria viewer

Build `CultMath.Unity`, bind daemon-published definitions, render from orbit to
ground, and compare Unity readback and seams against the parity corpus.

Exit: Aetheria views the daemon-defined planet without a second generator.

### Phase 6: projections and map tiles

Implement projection transforms, footprint calculation, antialiased tile
sampling, CultMesh/CDN publication, and Electron elevation, slope, shaded
relief, plus one Aetheria strategic overlay.

Exit: Unity and Electron identify matching landmarks at shared directions;
changing projection changes only the chart.

### Phase 7: optional WebGPU lowering

Add WGSL only if interactive local evaluation earns its cost, then prove point,
seam, page, and projection parity. Daemon tiles remain production until then.

### Phase 8: delete obsolete owners

Remove superseded Zyphos/Fensalir algorithms and Aetheria's Unity-only celestial
terrain from authoritative paths.

Exit: renderer code, cache state, or projection cannot change the planet.

## Verification matrix

| Claim | CPU daemon | Fensalir | Unity | Electron tiles | WebGPU later |
|---|---:|---:|---:|---:|---:|
| Field identity/hash | exact | exact | exact | exact metadata | exact |
| Point height/evidence | reference | bounded | bounded | sampled | bounded |
| Six-face edges/corners | required | required | required | projection-specific | required |
| Wavelength selection | reference | bounded | bounded | encoded in key | bounded |
| Page residual sum | reference | required | required | optional source | required |
| Projection round trip | reference | optional | optional | required | required |
| Cache-independent answer | required | required | required | required | required |

Negative checks must prove that residency cannot change a direct query,
projection cannot change the spherical field, Unity settings cannot alter daemon
identity, stale tiles fail version checks, omitted wavelengths remain within
their bound, seams/poles remain single-valued, and reload/eviction cannot commit
gameplay state.

## Initial deliverable

1. Extract cube-sphere topology and scale-aware CPU point/batch queries.
2. Add an Aetheria daemon smoke for height, normal, slope, and error.
3. Add equirectangular and Equal Earth CPU projection sampling.
4. Produce one versioned elevation tile and one slope tile.
5. Match the same fixture directions against Fensalir's HLSL oracle.

This proves the daemon and map architecture before moving the larger page and
patch machine. It produces useful Aetheria output without letting a quick tile
generator become a second planetary authority.
