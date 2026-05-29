# Saga

**Planetary flow middleware for worlds that refuse to sit still.**

Saga is Fensalir's stochastic planetary flow organ: a cube-sphere native system
for streaming atmosphere, ocean, gas, debris, heat, and other transport fields
across whole planets at arbitrary level of detail.

It is built for studios that want living planets without hand-authoring a fake
weather loop, baking one heroic texture atlas, or stapling a particle system to
a skybox and calling it climate. Saga turns terrain, bathymetry, atmospheric
state, ocean reanalysis, arbitrary forcing sources, and learned stochastic
transport into compact flow packets the engine can stream, inspect, simulate,
and render.

No poles. No flat-Earth texture tricks. No private renderer arrows pretending
to be physics. Just a planet-scale flow field with provenance, uncertainty, and
enough teeth to survive a camera dive from orbit to coastline.

## The Pitch

Modern games and simulation tools can render planets. They rarely make them
move like planets.

Saga is the missing middleware layer between static world geometry and dynamic
planetary behavior:

```text
planet domain + relief + state + forcing + learned transport
-> stochastic advection packets
-> streaming flow pages, particles, weather, debris, clouds, currents, debug
```

Use it for:

- ocean surface currents and debris drift;
- atmospheric wind fields and gas-giant bands;
- volcanic, nuclear, magical, artificial, or stellar energy injection;
- multi-sun lighting and thermal forcing;
- tides, rotation, Coriolis-dominated flow, and exotic planetary regimes;
- gameplay-visible particles, clouds, smoke, spores, ash, foam, or pollutants;
- offline worldbuilding, cinematic previs, and realtime renderer guidance.

Saga is not a weather forecast API wearing a costume. It is a reusable engine
organ for representing and streaming stochastic flow over Fensalir's planetary
domains.

## What Makes It Different

### Cube-Sphere Native

Saga speaks Fensalir's optimized cube-sphere address space directly. Lat/lon
grids are source formats, not runtime truth. Every packet belongs to a tile,
LOD, projection, local tangent frame, and provenance chain.

### Stochastic By Design

Real flow is not one obedient arrow. Saga packets carry mean transport,
diffusivity/covariance, confidence, and optional transition kernels. Renderers
can draw clean vectors; simulations can keep the uncertainty instead of
throwing it into the sea and acting surprised later.

### Arbitrary Forcing

The evaluator is not Earth-locked. Saga conditions on explicit rotation and
Coriolis features plus variable-cardinality energy, mass, and momentum forcing
sources:

- one sun, binary suns, or no sun worth trusting;
- moons and tidal drivers;
- eruptions, impacts, reactors, orbital mirrors, industrial heat, spellstorms;
- gas-giant internal heat and extreme atmosphere bands;
- transient shock events and persistent planetary gradients.

Source count is data, not architecture.

### Built For Streaming

Saga is designed for camera-relative planet rendering:

- coarse flow far from the camera;
- refined flow where the player, shot, simulation, or uncertainty demands it;
- parent/child LOD consistency;
- cross-face seam continuity;
- explicit stale-page and model-version checks.

The renderer samples packets. It does not invent climate in a pixel shader
because the schedule got nervous.

## Architecture

Saga lives inside Fensalir because Fensalir already owns the machinery it needs:

- `CubeTileKey` and cube-sphere projection math;
- field evidence layers, especially `Transport`;
- selected cuts and residency planning;
- GPU resource pages and debug telemetry;
- renderer/simulation lowering boundaries.

Saga's live shape:

```text
source manifests
  -> cube-sphere dataset bakes
  -> training shards
  -> FlowNet-S / FlowNet-M / optional FlowNet-T
  -> Saga stochastic advection packets
  -> Fensalir Transport field claims
  -> GPU pages, particle advection, weather visuals, debug probes
```

## Model Line

Saga uses a staged neural architecture plan:

- `FlowNet-S`: 8-15M parameters, local baseline for ingestion, calibration, and
  export.
- `FlowNet-M`: 40-60M parameters, first serious runtime/distillation model with
  local tile encoders, a cube-sphere graph processor, and stochastic heads.
- `FlowNet-T`: 150-300M parameters, optional global teacher for offline
  distillation and ensemble behavior.

The model emits stochastic advection parameters, not just velocity:

- tangent mean velocity;
- positive-semidefinite 2x2 covariance/diffusivity;
- calibrated confidence;
- optional neighbor transition logits;
- provenance and model identity.

## Data Spine

Saga's first research targets:

- ETOPO / GEBCO for relief and bathymetry;
- ERA5 for atmosphere;
- GLORYS / OSCAR / HYCOM candidates for ocean currents;
- PlasticAdrift and drifter transition matrices for stochastic debris
  transport validation;
- synthetic analytic worlds for contract and seam tests before the ocean gets
  expensive.

Every source needs a manifest. No manifest, no training shard. The altar wants
receipts.

## First Vertical Slice

1. Define the Saga packet contracts.
2. Bake a synthetic cube-sphere flow world.
3. Export Transport field pages.
4. Render/debug vectors, covariance ellipses, confidence, and selected LOD.
5. Train FlowNet-S on synthetic + tiny ERA5 wind.
6. Add ocean surface current targets.
7. Add PlasticAdrift transition validation.
8. Promote FlowNet-M when the baseline has failed in useful, measured ways.

## Boundary

Saga owns reusable planetary flow mechanics. Clients own meaning.

Fensalir clients may ask for currents, winds, debris drift, cloud motion, ash
transport, or gas-giant bands. They may not create a private flow authority that
bypasses Saga packets and Fensalir Transport claims.

That is the deal: the planet can be wild, but the authority has to be clean.
