# Planetary Flow Implementation Plan

This is the execution plan for Fensalir's planetary flow organ. It is ordered to
prove ownership and data shape before expensive model work.

## Invariants

- Fensalir owns the reusable stochastic advection contract and runtime
  streaming machinery.
- Source datasets are offline evidence with provenance, not runtime authority.
- The runtime representation is cube-sphere native.
- The model emits stochastic advection parameters, not only deterministic flow
  vectors.
- Client repos can consume, visualize, or condition flow; they do not bypass
  the engine field evidence path.
- LOD inheritance must preserve physical continuity across parent and child
  tiles.

## Phase 0: Scope And Provenance

Deliverables:

- source manifest format for dataset id, URL, license, citation, access date,
  variable list, spatial grid, temporal cadence, and derived artifacts;
- initial source rows for ETOPO 2022, GEBCO, ERA5, OSCAR, PlasticAdrift, and
  candidate CMEMS/HYCOM products;
- decision on local storage shape for raw downloads and cube-sphere bakes.

Verification:

- a provenance report can be generated without model code;
- no training shard can be created without a source manifest row.

## Phase 1: Cube-Sphere Bake Pipeline

Deliverables:

- offline tool that samples lat/lon or NetCDF/Zarr inputs into `CubeTileKey`
  pages;
- baked static channels: elevation, bathymetry, land/sea mask, slope,
  curvature, coastline distance, source quality;
- baked dynamic channel fixture from a small ERA5/OSCAR subset;
- metric metadata per tile: area scale, local tangent frame, latitude/Coriolis,
  projection id, and missing-data mask.

Verification:

- round-trip sample checks against source grids at known points;
- area-weighted global summary checks;
- cross-face continuity checks;
- debug image or CSV dump for one tile pyramid.

## Phase 2: Contract Skeleton

Deliverables:

- `PlanetaryFlowDomain`, `PlanetaryFlowRequest`, `PlanetaryFlowSample`, and
  `PlanetaryFlowClaim` contracts;
- encoding enum or equivalent for `StochasticAdvection`;
- field evidence validation for Transport-layer flow claims;
- test fixtures with synthetic flow pages.

Verification:

- contract round-trip tests;
- validation rejects missing model/source versions and invalid covariance;
- synthetic claims can pass through the field evidence planner without D3D12
  flow rendering.

## Phase 3: Synthetic Runtime Slice

Deliverables:

- analytic synthetic flow model over cube-sphere tiles;
- baked/evaluated flow pages at multiple LODs;
- debug visualization for mean velocity, covariance ellipse, confidence, and
  inherited versus evaluated LOD.

Verification:

- particles or probe samples advect continuously across tile and face edges;
- parent-to-child values are consistent under aggregation;
- camera-driven selected cuts request the same flow path as direct debug
  sampling.

## Phase 4: Baseline Learning Pipeline

Deliverables:

- Python training workspace or external tool boundary with sharded data loader;
- baseline local tile-pyramid model;
- outputs: mean tangent velocity and covariance;
- losses: vector regression, negative log likelihood, smoothness, and
  parent-child scale consistency;
- held-out region/time evaluation.

Training plan:

- start with ERA5 10m wind over a small temporal window;
- add OSCAR surface currents as the first ocean target;
- train on cube-sphere pages, not raw lat/lon;
- normalize per variable and preserve masks;
- keep model identity, source versions, and normalization stats with every
  checkpoint.

Verification:

- beat a persistence/climatology baseline;
- visualize residuals by latitude, coastline distance, and LOD;
- check conservation-ish behavior through particle rollouts, not only per-cell
  vector error.

## Phase 5: PlasticAdrift Auxiliary Target

Deliverables:

- importer for PlasticAdrift probability outputs and/or transition matrix where
  license/access are verified;
- cube-sphere mapping from debris probability cells to transport targets;
- auxiliary loss from predicted stochastic parameters to observed transition
  distributions over a lead time;
- validation cases for known garbage-patch accumulation behavior.

Verification:

- probability mass remains normalized after cube-sphere remap within declared
  omission thresholds;
- rollouts from predicted parameters reproduce broad PlasticAdrift transition
  behavior better than deterministic current-only advection.

## Phase 6: Hierarchical Model And Streaming

Deliverables:

- hierarchical graph or neural-operator model over cube-sphere tiles;
- parent/child and same-level neighbor edges;
- streaming evaluation for requested selected cuts;
- cache/page residency keyed by model and conditioning versions;
- uncertainty-driven refinement.

Architecture hypotheses to test:

- local patch encoder plus graph head for first production model;
- hierarchical graph neural operator for long-term arbitrary LOD;
- sparse transition logits only where PlasticAdrift-style debris transport
  needs them;
- covariance output via Cholesky factors to guarantee positive semidefinite
  diffusion.

Verification:

- stable cross-LOD particle rollouts;
- bounded frame-time budget under camera motion;
- debug report ties visible flow to model version, source versions, and selected
  LOD.

## Phase 7: Renderer And Simulation Integration

Deliverables:

- D3D12 flow page resources;
- compute shader helpers for stochastic particle advection;
- debug overlays and capture fixtures;
- API for clients to request flow at world points or publish visual consumers.

Verification:

- headless capture shows nonblank flow diagnostics;
- direct sampling, debug visualization, and particle advection agree on the
  sampled layer;
- stale pages cannot silently render after model/source version changes.

## Recommended First Cut

Build the synthetic runtime slice before downloading the ocean. Then train the
small ERA5 wind model. Then add OSCAR. Then PlasticAdrift. The ocean will try to
turn this into a grand expedition immediately; make the contract prove it can
hold a cup of water first.
