# Rendering Teardown / Rebuild Protocol

## Objective

Rebuild Fensalir's rendering architecture as one coherent field-evidence
machine, not a collection of renderer-era passes that happen to land in the
same swapchain.

The real outcome is simple to state and brutal to enforce: authored worlds,
procedural fields, direct SDFs, meshes, Mimir sensor observations, spectrum
tubes, heightfields, volumes, and fractal probes must all become bounded field
claims with stable identity, support, provenance, guide data, and validation
rules before they are allowed to influence temporally reused presentation.

The renderer may still use rasterization, compute, ray marching, mesh draws,
proxy draws, splats, or SDF envelopes. Those are backend lowerings. They are
not truth.

## Current Mechanism

The live renderer still carries several separate authority paths:

```text
client scene state
-> D3D12Renderer monolith
-> heightfield target / scene shader / SDF proxy / spline tubes / temporal Gaussian / fractal splats
-> color, metadata, control, reservoir-guide, depth targets
-> temporal resolve and post
```

This is better than a traditional immediate renderer, but it is still split in
the wrong place. The backend path chosen for a field can accidentally decide
identity, support size, coverage, temporal guide output, and even whether the
surface enters the evidence machine at all.

That is how Fensalir ends up treating a spectrum spline as either a point-splat
cloud, a direct debug strip, or a tube SDF depending on which path got touched
last. That is the machine confessing split authority. No incense required.

## Rebuild Authority Map

Owner:

- `FieldEvidenceMachine` owns the decision that something is visible,
  reusable, resident, or temporally valid.
- The spatiotemporal field reservoir is the shared rendering organ for
  reusable claims. Tube fields, direct SDF envelopes, meshes, heightfields,
  volumes, splats, and sensor-fusion fields are claim producers or lowerings;
  none of them own final composition as a private renderer.

Inputs:

- authored domains and claims from `.aquageo`, `.aquafield`, client scene
  contracts, resident mesh summaries, SDF/tube/volume declarations, and Mimir
  sensor observations;
- previous-frame reservoirs, history guide targets, page residency state,
  camera/view state, budgets, and calibration confidence;
- source proposal PDFs or explicit structural-evidence policies.

Outputs:

- canonical field-claim streams grouped by Form, Appearance, and Transport;
- selected candidate reservoirs and stable track updates where persistent
  identity exists;
- temporally reused field presentation decisions for claims whose visible
  surface or volume candidate is itself the sampled value;
- residency/page requests and selected cuts;
- backend packet streams for direct SDF, mesh, splat, height/tile, volume,
  tube, debug, and future paths;
- scene color/travel, metadata, control, reservoir-guide, depth, and telemetry.

Derived state:

- D3D12 pipeline states, vertex formats, splat buffers, meshlets, texture
  atlases, SDF proxy constants, direct spline frames, heightfield textures, and
  postprocess targets are compiled lowerings or diagnostics.
- The old `SDF/PBR/Radiosity` reservoir naming is backend vocabulary. The
  architectural layers are `Form`, `Appearance`, and `Transport`.
- Legacy TAA history is demoted to reservoir resolve history. It is not
  producer identity, stable spatial evidence, or a separate owner of temporally
  reused field presentation.

Forbidden writers:

- alpha blending, quad UVs, screen-space strips, billboard radii, or debug draw
  geometry deciding visible truth;
- client repos submitting backend packets as if they were semantic claims;
- payload-specific paths such as TubeField naming themselves as the reservoir
  renderer instead of feeding candidate claims into the shared field reservoir;
- Mimir owning a render graph or a second stable evidence cache;
- any downstream pixel-history/TAA pass repairing missing identity after the
  fact;
- backend-specific payload conventions when a typed lowering contract exists.

Shared paths:

- heightfield terrain, direct SDF proxies, texture-backed tube fields,
  spline-spectrum trails, fractal structural probes, temporal Gaussian fields,
  density/extinction volumes, mesh geometry, sensor fusion, and debug overlays
  all use the same claim -> candidate -> validation -> lowering contract when
  they want temporal reuse.
- Payload-specific code may own intersection, envelope evaluation, geometry
  expansion, material sampling, and proposal PDFs for its own claim family. It
  may not own final field visibility or bypass the reservoir when the output is
  meant to participate in temporally reused presentation.

Deletion line:

- A pass that cannot state its field layer, stable identity, support bounds,
  validation guides, and ownership is demoted to explicit fallback/debug draw.
- Old backend paths may remain only if they delegate to the field-evidence
  owner or are quarantined behind names that make their lack of authority
  impossible to miss.

## Intended Architecture

```text
Authored intent / direct field / mesh / Mimir observation
-> Domain binding
-> Field claim normalization
-> Local visibility candidate generation
-> Target evaluation
-> Resampled-importance reservoir
-> Temporal reuse validation
-> Spatial/domain reuse validation
-> Stable track update, when the field has persistent identity
-> Occupancy graph and residency/page scheduling
-> Backend lowering
-> Evidence-writing render/compute passes
-> Reservoir resolve/reconstruction
-> Presentation and debug telemetry
```

### 1. Domain And Claim Layer

Create a stable contract layer that every contributor uses before touching
D3D12 resources:

```text
FieldDomain {
  domainKey
  parentKey
  kind
  frame
  projection
  bounds
  periodicity
}

FieldClaim {
  claimKey
  domainKey
  producerKey
  layer: Form | Appearance | Transport
  encoding: Height | Sdf2d | Sdf3d | Density | Extinction | Material |
            Phase | Emission | Radiance | Feature | Confidence | Tube | Mesh
  localFrame
  supportEnvelope
  payloadHandle
  sourcePdfPolicy
  provenance
  observedTime
  confidence
}
```

This layer is the first cut. Until this exists, every new rendering path is
tempted to smuggle authority inside its vertex layout.

### 2. Candidate And Target Layer

Candidate generators propose work. They do not own final visibility.

For ReSTIR-style lighting, primary surface visibility is usually already known.
Fensalir's Form fields often do not have that luxury: the candidate being
sampled is the visible field surface or volume itself. In that case, the
producer must generate local visibility candidates first, with stable identity,
support bounds, depth/travel, coverage, material encoding, target value, and
source/proposal policy. The shared reservoir then owns reuse and final
presentation membership.

This is the sanctioned divergence from pure ReSTIR and the reason there is no
separate TAA owner. Reservoir update and reservoir resolve own temporal
stability, antialiasing, and reconstruction. A downstream pixel-history pass
may not decide which field candidate existed.

Targets are scalar contribution estimates: projected form error, curvature,
material delta, extinction contribution, radiance contribution, sensor
information gain, visible coverage, or calibration confidence. Probabilistic
sources provide source PDF. Deterministic structural sources provide an explicit
proposal policy so they do not become free magic samples.

### 3. Reservoir And Track Layer

`ResampledImportanceReservoir<T>` remains pure: selected sample, target,
weight sum, candidate count, contribution weight.

The stable track layer remains separate. It owns persistent identity,
prediction, smoothing, expiry, and confidence once candidate evidence becomes a
field with history. Reservoir math is not a dictionary. A track is not RIS.
Keep the organs separate so the body can breathe.

### 4. Support, Residency, And Summaries

Claim support belongs to the represented domain, not to the pixel grid.

- A uniform plane can be one huge surface claim.
- A smooth terrain tile can remain broad.
- A brush-detailed terrain patch subdivides through quadtree pages.
- A spline/tube field uses curve/tube support, not point plates.
- A volume uses density/extinction support and transport budgets.

The occupancy graph and page planner own resident cuts. Backend passes consume
selected cuts; they do not walk authoring trees or invent page policy mid-draw.

### 5. Backend Lowering Layer

Lowerings are replaceable:

- direct analytic SDF proxy;
- texture-backed tube or ribbon field;
- resident mesh draw;
- compact-support surface splat;
- density/extinction volume splat;
- 2D height/SDF/material/confidence page;
- debug overlay.

Every reusable lowering writes the same scene evidence lanes:

```text
color/travel
metadata: field id, claim id, layer, encoding
control: coverage, normal/material hints, temporal detail
reservoirGuide: confidence, sample age, domain validity, invalidation code
depth when depth participates in validation
```

If a lowering cannot provide these, it is a fallback draw. It can help humans
debug. It cannot claim to be the Perfect Machine.

### 6. Reservoir Resolve And Presentation

Reservoir resolve consumes guide data and reconstructs presentation from the
selected field evidence. It does not recover truth from broken producers.

Post, bloom, tonemapping, debug overlays, and output publication operate after
the evidence lanes are coherent. Glow is allowed to make strong evidence look
alive; it must not hide missing support, wrong travel, or unstable identity.

## Mimir Boundary

Mimir owns physical truth before lowering:

- direct capture, device timestamps, ASIO blocks, network observations;
- five-second rolling retention;
- clock fits, calibration receipts, codebooks, response surfaces;
- policy for which observations enter the field.

Fensalir owns field truth after lowering:

- GPU feature extraction and fusion contracts;
- candidate selection and reuse;
- stable field tracks;
- render residency and output surfaces;
- debug views over evidence, not secondhand pixels.

Mimir must not submit "draw this dashboard" as a production command. It submits
typed buffers, observations, constraints, and surface intent. Fensalir chooses
the lowering.

## Migration Plan

### Phase 0: Quarantine Existing Paths

Name every current render path by authority:

- production evidence contributor;
- backend lowering under the shared contract;
- fallback/debug draw;
- obsolete path to delete.

The direct spline-spectrum surface remains a deterministic debug lowering until
the field contract can express rolling texture slices as tube fields. The old
point-splat spectrum path remains dead.

### Phase 1: Extract Field Contracts

Add engine contracts for `FieldDomain`, `FieldClaim`, `FieldCandidate`,
`FieldSupport`, `FieldGuide`, and `FieldBackendPacket`.

Update docs and code comments so `SDF/PBR/Radiosity` names are treated as
legacy packet names. New architecture talks in `Form`, `Appearance`, and
`Transport`.

Acceptance:

- a test can build a domain, claim, candidate, reservoir sample, and backend
  packet without touching D3D12;
- each packet names layer, encoding, support, claim id, and guide schema.

### Phase 2: Rebuild Heightfield As A Surface-Page Domain

Replace the fixed-height target as terrain authority with quadtree surface
pages:

- 2D brushes paint height/SDF/material/confidence tile payloads;
- parent summaries remain renderable while children stream;
- subdivision follows projected footprint, curvature, brush/material detail,
  silhouette risk, and temporal uncertainty;
- flat pages emit broad claims.

Acceptance:

- a flat test terrain resolves from broad claims, not pixel dots;
- adding a sharp brush stroke causes local subdivision only;
- page residency telemetry explains every requested or evicted tile.

### Phase 3: Unify Surface Contributors

Route direct SDF proxies, mesh draws, tube fields, and surface splats through
one evidence-writing packet contract.

Acceptance:

- the same debug view can show support bounds, claim id, field layer, guide
  confidence, and invalidation reason for a mesh, tube, SDF proxy, and splat;
- alpha blending cannot decide visibility for an opaque Form surface;
- travel/depth identity survives camera motion without TAA patching over it.

### Phase 4: Add True Rolling Buffer Field DSL

Let `.aquafield` express:

```text
field spectrumTrail {
  source texture: mimir.audioSpectrumHistory
  slice axis: frequency -> x
  sample axis: amplitude -> y
  history axis: windowAge -> z
  modulo: sequenceId
  row identity: sourceId
  surface: tube radius by confidence, emission by amplitude
  quality: auto
}
```

The interpreter chooses direct tube SDF, mesh ribbon, compact support splats, or
future lowerings based on budget. The script states intent; the engine owns the
representation.

Acceptance:

- the Mimir spectrum dashboard can be recreated from DSL intent;
- row, band, and history identity remain stable across frames;
- lowering can switch under budget without changing claim identity.

### Phase 5: Mimir Sensor Observation Bridge

Expose generic sensor candidate contracts:

- `SensorFrameObservation` for cameras and depth hints;
- `AudioFieldObservation` for timing, response, phase, source candidates;
- `CalibrationConstraint` for known emitted/observed events.

Fensalir lowers observations into feature candidates and field claims. Mimir
continues to own rolling buffers and calibration state.

Acceptance:

- Mimir cannot bypass the bridge with render packets;
- one synthetic camera trace produces stable feature claims;
- one synthetic audio localization trace produces acoustic confidence claims.

### Phase 6: GPU Reservoir And Reuse Parity

Port the pure reservoir and validation seams into GPU passes only after the CPU
contract proves itself.

Acceptance:

- CPU/GPU fixtures agree for reservoir update, packet decoding, guide packing,
  support bounds, and domain shift validation;
- debug telemetry reports candidate count, selected target, weight sum,
  contribution weight, age, confidence, and invalidation reason.

### Phase 7: Presentation And Production Output

Keep bloom, tonemapping, program output, and debug UI downstream of coherent
evidence lanes. Add production output receipts for Mimir and clients only after
field identity survives motion, LOD changes, and sensor delay windows.

Acceptance:

- final output can be inspected by claim/domain id;
- stale or invalid evidence is visibly rejected rather than smeared;
- program output is a view of the field machine, not a pile of raw feeds.

## Negative Verification

The rebuild is not complete until these checks pass:

- old spline strips cannot decide visible tube coverage;
- point splats cannot become the default representation of smooth spectrum
  tubes;
- heightfields cannot require pixel-sized claims for flat regions;
- meshes cannot bypass guide output and still call themselves production;
- Mimir cannot create a second visual evidence cache;
- TAA cannot hide missing producer identity by eventual convergence;
- every backend pass can explain which field layer and support it represents.

## Research Spine

This protocol rests on the existing research and maps:

- `docs/perfect-machine-architecture.md`
- `docs/temporal-spatial-evidence-reservoir.md`
- `docs/tsr-inspired-taa-spec.md`
- `docs/hlsl-renderer.md`
- `research/rendering/fractal-brush-architecture-plan.md`
- Mimir `research/perfect-machine-study-2026-05-23/fensalir-integration-map.md`
- Mimir `research/visual-spatial-map/brushstroke-fusion-rendering.md`

External anchors remain the same: ReSTIR DI/GI, GRIS, EWA splatting,
Gaussian-splat support discipline, geometry clipmaps, Nanite-style selected
cuts, TSDF fusion, and GPU-friendly multiresolution encodings.
