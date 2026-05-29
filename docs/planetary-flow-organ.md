# Planetary Flow Organ

## Objective

Add a reusable planetary flow organ to Fensalir that predicts stochastic
advection parameter sets over optimized cube-sphere domains. The organ should
serve whole-planet ocean-surface and atmospheric flow to renderers and
simulations at arbitrary LOD: fine near the camera, coarse far away, and always
bounded by explicit uncertainty.

The product is not "one current vector per point." The product is a streamable
transport field:

```text
cube-sphere tile + time/context + static planet geometry + dynamic state
-> stochastic advection parameters
-> renderer/simulation samples, transition kernels, and uncertainty telemetry
```

## Authority Map

Owner: `Aquarium.Engine.Fractal` should own the domain math, tile identity,
LOD/residency contract, training data preparation shape, and inference packet
schema. Runtime inference may later move to a dedicated ML package, but the
engine owns the field contract that clients consume.

Inputs: cube-sphere tile address, projection metadata, static relief/bathymetry,
land/sea mask, explicit Coriolis/rotation features, calendar/phase features,
arbitrary external energy/mass/momentum forcing source records in planet/global
and local tangent bases, optional recent atmosphere/ocean state, and dataset
provenance/version.

Outputs: bounded stochastic advection parameter tiles for atmosphere and ocean
surface. The first output schema should support mean velocity, covariance or
diffusivity, optional transition-kernel logits over neighboring cells, and
calibration/confidence.

Derived state: visualization arrows, particle paths, current speed heatmaps,
plastic trajectories, and gameplay/weather effects are derived views. They do
not own flow truth.

Forbidden writers: renderer shaders, demo clients, PlasticAdrift import code,
weather dataset loaders, and particle systems must not invent private flow
fields. They may request or display flow packets; they may not become alternate
advection authorities.

Shared paths: direct camera streaming, background planetary simulation, offline
training export, replay from persisted time, and imported dataset validation
all use the same cube-sphere tile key, same normalization metadata, and same
flow packet schema.

Deletion line: do not add a lat-long-only flow path as a convenient prototype
that later needs adapters. Lat-long sources are ingestion formats; cube-sphere
tiles are the engine domain.

## Current Mechanism

Fensalir already has the right substrate:

- `CubeTileKey` names a six-face quadtree tile.
- cube-sphere projections exist for normalized and tangent mappings.
- surface pages, residency planning, and selected cuts already express
  streamable planetary surface data.
- the field architecture already distinguishes Form, Appearance, and Transport
  evidence. Planetary flow belongs in Transport.

The missing organ is a data and inference path that fills cube-sphere Transport
pages with stochastic advection parameters instead of authored brush evidence.

## Invariants

- Cube-sphere tile identity is the runtime domain. Lat-long is only source data
  layout or debug view.
- Static relief alone is not a sufficient predictor. Bathymetry/topography can
  shape flow, but dynamic atmosphere/ocean state, time conditioning, and
  explicit Coriolis and external forcing are real inputs.
- A stochastic output must expose uncertainty. A mean vector without dispersion
  is a visualization convenience, not the organ.
- Conservative LOD summaries remain renderable. Missing high-resolution child
  tiles fall back to parent flow summaries.
- Learned priority may choose what to refine first; it may not replace packet
  validity, tile bounds, provenance, or calibration checks.
- PlasticAdrift-style transition matrices are validation and auxiliary targets,
  not full ocean-dynamics truth.

## Output Parameterization

Start with a compact packet that can represent both velocity-like and
transition-matrix targets:

```text
FlowTilePacket
  Domain: CubeTileKey, projection id, cell spacing, time basis
  Rotation: local Coriolis parameters, rotation-axis basis, beta terms
  Forcing: arbitrary directional/point/area/volume source records
  Layer: oceanSurface | atmosphereNearSurface | atmospherePressureLevel
  MeanVelocity: tangent-space u/v per sample
  Diffusivity: symmetric 2x2 tangent covariance or log-Cholesky parameters
  NeighborKernel: optional logits over fixed cube-tile neighbor stencil
  DivergenceCurl: optional diagnostic channels
  Confidence: calibration, dataset mask, and extrapolation flags
  Provenance: source dataset, model version, normalization version
```

The first implementation should train mean velocity and diffusivity. Add
neighbor-kernel logits once ingestion can derive transition targets from
drifter/plastic or rollouts. A full dense transition matrix is useful offline,
but it is too large as the primary render packet.

## Dataset Spine

Static planet geometry:

- GEBCO_2025 Grid: global land/ocean terrain at 15 arc-second resolution, with
  source-type identifier grid and attribution request.
- NOAA ETOPO 2022: 15 arc-second global relief, useful as a CC0-licensed
  baseline.

Atmosphere targets:

- ERA5 through the Copernicus Climate Data Store: hourly global reanalysis from
  1940 onward, 0.25 degree public data, useful for wind and pressure fields.

Ocean targets:

- Copernicus Marine GLORYS12V1 global ocean physics reanalysis: eddy-resolving
  1/12 degree, daily/monthly currents, sea level, temperature, salinity, and
  related fields from 1993 onward.
- OSCAR surface currents: satellite-derived upper-ocean currents at coarse
  global resolution, useful for surface-current validation and lightweight
  baselines.
- HYCOM/NCODA global products: high-resolution operational/reanalysis ocean
  fields, useful but operationally rougher because availability and exact data
  gaps matter.

Plastic/drifter transport:

- PlasticAdrift/Adrift style transition matrices provide probability
  distributions over future surface positions from observed drifter-derived
  transport. Use them to supervise or validate stochastic surface transport,
  especially transition kernels and long-horizon debris behavior.

See `../research/planetary-flow-organ-literature.md` for source-backed notes.

## Model Hypotheses

See `planetary-flow-nn-architecture.md` for the concrete parameter budgets,
layer stacks, losses, and training curriculum. The short version: use an
8-15M-parameter local baseline, a 40-60M-parameter hierarchical cube-sphere
graph model as the first serious runtime target, and a 150-300M-parameter
global teacher only for offline distillation if the smaller model cannot carry
global coherence.

### Baseline

Use deterministic interpolation and simple stochastic residuals first:

```text
source reanalysis field
-> reproject to cube-sphere tile pyramid
-> train predictor for mean velocity
-> fit residual covariance/diffusivity per biome/season/tile scale
```

This proves ingestion, normalization, tiling, loss functions, and packet
streaming before the neural architecture gets to wear a crown.

### Strong Candidate

Use a graph/operator hybrid:

- graph mesh over cube-sphere tiles for domain adjacency, seams, and variable
  LOD;
- local multiscale tile encoder for relief/bathymetry and recent dynamic
  fields;
- message passing across neighboring and parent/child tiles;
- operator-style global context for long-range circulation;
- probabilistic head emitting mean velocity, log-Cholesky diffusivity, and
  optional neighbor-kernel logits.

This follows the useful parts of GraphCast and MeshGraphNets while preserving
Fensalir's cube-sphere domain instead of importing an icosahedral mesh as a
second planetary truth.

### Alternative

Train an SFNO/FourCastNet-like global model for atmosphere/ocean state
forecasting, then distill or sample it into cube-sphere flow packets. This is
attractive for global coherence but less naturally aligned with arbitrary
camera-driven LOD.

### Probabilistic Extension

Use diffusion, flow matching, or latent-variable ensembles only after the
packet schema and deterministic/stochastic residual baseline are measurable.
GenCast proves diffusion-style ensemble weather can work; it also proves that
sampling cost is real. Fensalir should not pay that cost per visible tile unless
the result beats calibrated covariance and ensemble heads.

## Training Plan

1. Ingest static relief into a cube-sphere pyramid.
   Preserve source resolution, vertical datum notes, land/sea mask, slope,
   curvature, roughness, and coastline distance.

2. Ingest ERA5 and GLORYS/OSCAR/HYCOM targets.
   Normalize to common time slices, tangent basis, units, masks, and
   cube-sphere tile pyramid levels. Store source provenance per tile/time.

3. Build the baseline target set.
   Atmosphere: near-surface winds first, then pressure-level winds. Ocean:
   surface `uo/vo` or equivalent currents first. Plastic: transition
   distributions as auxiliary/validation targets.

4. Train a single-tile local model.
   Inputs are multi-channel multiscale crops around a target cell: relief,
   slope, land/sea, explicit Coriolis/rotation features, phase features,
   arbitrary forcing source summaries, and recent dynamic fields. Output mean
   velocity plus diagonal covariance.

5. Add cross-tile and parent/child context.
   Replace pure crops with cube-sphere graph adjacency and LOD-aware message
   passing. Verify seams explicitly.

6. Add stochastic transport heads.
   Upgrade covariance to full tangent 2x2 log-Cholesky. Add neighbor-kernel
   logits over a small stencil where transition data supports it.

7. Validate at the layer users see.
   Check one-step vector RMSE, calibrated uncertainty, multi-step trajectory
   spread, PlasticAdrift-style transition behavior, visible tile LOD stability,
   seam continuity, and parent/child summary consistency.

8. Distill for runtime.
   Export a tile-streaming inference package or precomputed coefficient pages
   with model/version metadata. Runtime packets must be small enough for camera
   streaming and cache eviction.

## Implementation Roadmap

### Phase A: Schema And Data Map

- Add `FlowTilePacket` and source provenance notes in docs before code.
- Define tangent-space basis conventions for each cube face.
- Decide the first tile pyramid levels used for training and runtime.
- Create ingestion manifests for GEBCO/ETOPO, ERA5, GLORYS, OSCAR, HYCOM, and
  PlasticAdrift.

### Phase B: Offline Reprojection

- Build offline reprojection from lat-long/source grids to cube-sphere tiles.
- Emit min/mean/max/std summaries for parent LODs.
- Validate seam continuity and area weighting.

### Phase C: Baseline Flow Pages

- Store mean velocity and simple covariance pages as engine-readable Transport
  pages.
- Add debug visualization: vectors, speed, uncertainty ellipse, mask, source
  version, parent/child mismatch.

### Phase D: Training Harness

- Train local multiscale baseline on reanalysis targets.
- Add held-out years/regions and no-cheating temporal splits.
- Compare against source interpolation and seasonal climatology.

### Phase E: Graph/LOD Model

- Add cube-sphere adjacency graph and parent/child message passing.
- Train seam-aware, LOD-aware stochastic packets.
- Verify arbitrary camera streaming by requesting mixed-resolution cuts and
  comparing against full-resolution source fields.

### Phase F: Runtime Inference Or Distillation

- Choose between precomputed flow pages, local tile inference, or hybrid
  coarse-global plus local-refinement inference.
- Lower results into shared field resource pages and debug telemetry.

## Cut Lines

- No demo-owned flow field.
- No lat-long runtime authority.
- No bathymetry-only predictor except as an explicit baseline.
- No stochastic claims without calibration plots.
- No neural architecture before the baseline proves ingestion and packet
  semantics.
- No per-pixel weather model in the renderer. The renderer samples packets.
