# Perfect Machine Architecture

## Objective

Build one real-time spatial evidence machine that can render and resolve:

- fast 2D scalar-field splats on tileable surfaces;
- fast 3D scalar-field splats in object/world volumes;
- SDF/level-set surfaces for opaque solids;
- density/extinction fields for flames, smoke, fog, plasma, and uncertain
  sensor volumes;
- 2D fields projected onto 3D domains such as cube-sphere planets and toroidal
  station decks;
- camera/microphone sensor evidence from Mimir-style capture;
- cached structural probes emitted by IFS `.aquageo` grammars;
- direct SDF proxy, tube, and curve fields that can write reservoir-guide
  evidence without first becoming adaptive probe clouds;
- traditional mesh geometry when triangles are the most coherent resident
  summary for a surface;
- stochastic updates that converge under CPU, GPU, RAM, and SSD budgets.

The product requirement is not "infinite detail." It is a compact authored
description that can put detail where pixels can see it, keep enough evidence
alive to stabilize it, and refuse work that cannot justify its cost.

## Research Spine

This architecture is not invented from the floorboards:

- ReSTIR DI and GI prove reservoir reuse as a real-time sampling strategy for
  expensive candidates across time and screen space.
- GRIS generalizes reservoir resampling beyond direct light candidates through
  target functions, correlated samples, domains, and shift mappings.
- EWA surface/volume splatting and 3D Gaussian splatting prove anisotropic
  projected support as a practical rendering primitive.
- Geometry clipmaps and Nanite-style virtualized geometry prove that visible
  projected error and resident summaries beat global leaf traversal.
- TSDF fusion/KinectFusion prove that streamed sensor samples can accumulate
  into a coherent implicit spatial field.
- Instant-NGP's multiresolution hash encoding is a warning and inspiration:
  stochastic evidence can be fast when the data layout is explicit and GPU
  friendly, but Aquarium should preserve deterministic summaries and bounds as
  safety authority.

Aquarium diverges from pure ReSTIR at the primary-visibility boundary. ReSTIR
usually reuses lighting or path candidates after a visible surface is known.
Aquarium/Fensalir often samples the visible field candidate itself. Local Form
producers therefore generate candidate visibility first, and the reservoir owns
the temporal-antialiasing-shaped decision about which field candidate persists
into presentation. There is no separate TAA organ in the target architecture:
reservoir resolve reconstructs smooth presentation directly from selected field
evidence.

## Prime Invariants

- Authored semantic domains are the source of truth.
- Backend packets are compiled output and may be deleted/rebuilt.
- Every domain, node, claim, probe, payload, page, and reservoir has a stable
  key.
- Every reusable sample must name its target function and source PDF or state
  why it is non-probabilistic structural evidence.
- Every LOD subtree has a conservative parent summary.
- Learned/stochastic priority may rank work; it may not own field safety,
  visibility safety, or calibration safety.
- Missing children render through parent summaries. A frame does not wait for
  SSD.
- Reservoir resolve owns temporal antialiasing and reconstruction; no separate
  TAA pass owns producer identity or temporally reused field presentation.
- Mimir sensor fusion and fractal rendering share the evidence machinery; they
  do not share client policy.

## System Pipeline

```text
Authored intent, resident mesh, direct SDF field, or live sensor input
-> Domain binding
-> Evidence candidate generation
-> Target evaluation
-> Resampled importance reservoir
-> Temporal reuse validation
-> Spatial/domain reuse validation
-> Occupancy graph update
-> Residency/page scheduling
-> Backend packet lowering
-> direct SDF, mesh, volume, and/or field splat passes
-> Reservoir resolve guide buffers
-> Debug/evidence telemetry
```

The machine is allowed to have multiple candidate producers and multiple
renderer contributors. The reservoir contract makes evidence comparable without
pretending every surface must originate as an adaptive probe splat.

## Module Network

```text
Aquarium.Engine.Contracts
  Stable DTOs: domains, claims, nodes, summaries, probes, reservoir guides,
  payload pages, backend packets, debug rows.

Aquarium.Engine.Fractal
  Pure CPU algorithms: domain math, grammar expansion, ownership trees,
  summaries, scoring, reservoirs, occupancy graph updates, residency planning,
  packet planning.

Aquarium.Engine.SensorFusion
  Future shared adapter layer: camera/audio feature candidates, calibration
  confidence, raw retention lowerings, Mimir-facing packet contracts.

Aquarium.Engine.Render
  D3D12 resources, page tables, structured buffers, field splat passes,
  surface/volume resolves, reservoir guide buffers, debug visualization.

Aquarium.Zyphos
  World policy, cube-sphere/tile roots, planet grammar seeds, setting-safe
  visuals.

Aquarium.Epiphany
  Agent/body policy, role grammar selection, semantic bindings.

Mimir
  Sensor capture, raw rolling retention, feature extraction, calibration facts.
  It does not own resolved spatial evidence history.
```

## Data Model

### Domain

```text
DomainKey
ParentKey
Kind: solar/orbital/planet/cubeTile/torus/surface2d/object3d/volume3d/sensorRig
Frame
Projection
Bounds
Periodicity
Owner
```

Domains say where coordinates live and how they map to parent space. A domain is
allowed to be 2D, 3D, or 2D projected onto 3D.

### Claim

```text
ClaimKey
DomainKey
NodeKey
Layer: form/appearance/transport
Kind: height/sdf2d/sdf3d/density/extinction/material/phase/emission/light/feature/confidence
LocalFrame
Envelope
Payload
Tags
CostTier
Seed
```

Claims are authored or inferred statements about a field. They are not renderer
packets.

### Field Layer Split

The old shorthand was `SDF / PBR / Radiosity`. That naming was useful for the
first opaque-object path, but it is not the architecture.

The durable split is:

```text
Form       -> what exists where
Appearance -> how it interacts locally with light, color, and material
Transport  -> how energy moves through or from it
```

Opaque solids lower through level-set form:

```text
phi(x) = signed distance
gradient(phi) = surface normal
surface exists near phi = 0
```

Transparent media lower through density/extinction form:

```text
rho(x) = density / extinction / confidence
gradient(rho) = direction of local density change
volume exists where rho contributes above threshold
```

Sensor fusion may begin as confidence density and sharpen into surface claims
when multi-view/acoustic evidence makes a stable boundary plausible. Fractal
terrain may begin as SDF/height surfaces. Flames stay participating fields:
their form is density/extinction, their appearance is emission/color/phase, and
their transport is emission/scattering reuse.

Implementation note: current packet names still say SDF/PBR/radiosity in places
because the first GPU receipt was built for opaque splats. Treat those as
backend packet names, not the conceptual contract.

### Probe

```text
ProbeKey
DomainKey
LocalPosition
LocalNormalOrGradient
BoundRadius
PayloadHandle
TargetContribution
SourcePdf
Confidence
ObservedFrame
ObservedTime
ProducerKind
FieldLayer
FieldEncoding
```

A structural probe can come from an IFS grammar. A sensor probe can come from a
camera/audio feature. Both become candidates only after they have a target
function and validation rules.

### Reservoir

```text
SelectedSample
SelectedTarget
WeightSum
CandidateCount
ContributionWeight
ValidationMask
SampleAge
DomainKey
```

The reservoir owns resampling math. It does not own stable tracks or packet
lowering. Reservoir resolve history is the presentation side of the same
spatiotemporal organ, not a separate TAA owner.

### Occupancy Graph

```text
NodeKey
DomainKey
Bounds
SummaryPayload
ChildPayload
MeanContribution
Variance
Confidence
SampleAge
ResidencyState
LastVisibleFrame
LastUpdateFrame
```

The occupancy graph is the shared heuristic memory of where useful field
evidence likely lives. It is structural enough for IFS trees and statistical
enough for sensor fusion.

### Page

```text
PageKey
PayloadKind
ByteRangeOrHandle
ResidentTier: gpu/ram/ssd/missing
EstimatedGpuCost
EstimatedRamBytes
EstimatedSsdBytes
LastUseFrame
```

Pages are storage decisions, not semantic identity.

## Algorithms

### 1. Domain Binding

Every candidate is first bound to a domain path. Cube-sphere terrain binds to a
face/tile path. Torus surfaces bind to periodic `u/v` patches. Agent details
bind to object-local sheets, curves, or volumes. Sensor features bind to a
sensor rig frame and then to a calibrated world or object domain.

Validation starts here. If two samples do not share a valid lineage or shift
mapping, they do not reuse each other.

### 2. IFS Grammar Expansion

The `.aquageo` DSL emits semantic claims and ownership nodes:

```text
grammar -> domains -> claims -> ownership tree -> summaries
```

Expansion is deterministic by stable key and seed. Generated leaves do not all
need to be resident. The ownership tree must summarize itself before it earns
LOD rights.

### 3. Sensor Candidate Lowering

Camera and microphone producers retain raw capture ordering, calibration, and
provenance. Aquarium receives resolved candidate observations:

```text
camera/audio feature -> calibrated local/world candidate -> target evaluator
```

Sensor adapters own modality interpretation. Fensalir owns spatial evidence
history after lowering.

### 4. Target Evaluation

Targets are comparable scalar contributions:

- projected SDF/form error;
- density/extinction contribution;
- appearance delta over visible coverage;
- transport/radiance contribution;
- feature confidence times calibration confidence;
- expected pixel influence;
- expected information gain for an uncertain region.

The source PDF is the probability by which the candidate was proposed. If the
candidate is deterministic structural evidence, it uses an explicit proposal
policy rather than a magical free sample.

### 5. Reservoir Update

Each reservoir stores one representative sample plus the weight mass of the
candidates it represents:

```text
candidateWeight = target / sourcePdf
weightSum += candidateWeight
candidateCount += representedCount
select candidate with probability candidateWeight / weightSum
```

Final contribution weight follows the RIS/ReSTIR shape:

```text
weightSum / (candidateCount * selectedTarget)
```

Temporal reuse and spatial/domain reuse are explicit passes, not side effects.

### 6. Reuse Validation

Temporal reuse validates:

- camera motion/reprojection;
- previous/current domain lineage;
- disocclusion;
- field id/material class;
- local-frame error;
- conservative bounds;
- sample age and confidence.

Spatial/domain reuse validates:

- neighbor surface compatibility;
- tile adjacency or periodic wrap;
- cube-face seam mapping;
- torus seam mapping;
- object-local parent frame stability;
- sensor calibration agreement.

GRIS-style shift mappings live here. A shift is an owned object with tests, not
a helper function hiding inside a shader.

### 7. Occupancy Graph Update

The occupancy graph receives selected reservoirs and sparse high-quality probes.
It updates node statistics under budget:

```text
mean, variance, confidence, sampleAge, lastVisible, lastUpdated
```

Unvisited nodes decay. Stale uncertainty grows. Visible, uncertain, stale, and
near-threshold nodes retain nonzero exploration probability.

### 8. Selected Cut And Residency

A hierarchy cut is selected from summaries:

```text
projected error
material delta
coverage
current reservoir confidence
estimated GPU cost
residency state
```

Children stream only when the cut justifies them. Parent summaries remain
renderable. Eviction favors low-score high-cost payloads and protects
near-threshold nodes from thrash.

### 9. Backend Lowering

The same semantic field can lower to:

- 2D SDF tile pages;
- 2D height/material pages;
- 3D SDF splat packets;
- 3D density/extinction splat packets;
- transparent emission/scattering splat packets;
- 2D-projected-to-3D splat packets;
- sensor confidence volumes;
- direct SDF proxy draws;
- texture-backed tube, curve, or ribbon fields;
- resident mesh draws with stable ids, conservative bounds, motion, material
  encoding, and reservoir-guide outputs;
- debug overlays.

The first renderer path should use compact-support anisotropic envelopes. They
borrow Gaussian splatting's covariance discipline without inheriting infinite
support as a default runtime tax. Surface and volume encodings may share the
same envelope math; they do not share resolve rules.

Support size is semantic, not pixel dogma. The reservoir may resolve claims at
pixel granularity, but the claims themselves should be as large as the domain
allows. A flat, infinite, uniform plane can collapse to one huge surface splat
with stable normal/material evidence. A detailed heightfield should not become
one splat per pixel by default; it should be represented by a quadtree of
surface tiles whose subdivision follows projected footprint, curvature, brush
detail, material discontinuity, silhouette risk, and temporal uncertainty.

For Aquarium-style terrain, 2D surface brushes paint tile payloads first:
height, signed-distance support, material, and confidence. The probe/lowering
stage then emits surface splats only where the tile cannot be represented by a
larger claim. Flat broad terrain stays broad. Curved or brush-detailed terrain
subdivides. The SDF probe shader is a detail-adaptive claim emitter, not a
pixel-dot factory.

This is the explicit escape hatch from brush monoculture. IFS probes, sensor
fusion, and stochastic splat reservoirs are not the only way to feed the
spatiotemporal machine. If a mesh, analytic SDF, tube envelope, or volume pass
can write the same evidence lanes and validation guides, it belongs in the same
frame contract.

For buffer-driven spline fields, the DSL should express the rolling texture
slice, axis, modulo window, surface graph, and quality budget. The interpreter
then chooses the current best lowering. Today that means direct SDF tube
geometry for moderate fields and reservoir splats when an automatic budget says
the direct representation has become too expensive. Future mesh lowering fits
the same contract; it is another backend for the same evidence, not another
authoring language.

### 10. Reservoir Resolve Integration

Reservoir resolve consumes guide signals:

- reservoir confidence;
- sample age;
- domain validity;
- temporal detail;
- invalidation reason;
- field id and motion.

There is no separate TAA owner. Reservoir resolve decides how much temporal
history may survive after the field reservoir has selected and validated
presentation candidates. If downstream pixel filtering is required to make an
unstable field look stable, the Form producer or reservoir validation has
failed upstream.

## Resource Tradeoffs

### CPU

CPU performs grammar expansion, coarse scoring, stochastic scheduling, page
requests, eviction decisions, and selected-cut planning. It must not rescore
every leaf or rebuild payload pages mid-frame.

### RAM

RAM keeps summaries, estimator state, hot/warm metadata, selected cuts, and
recent probes. It does not keep every generated child payload.

### SSD

SSD stores payload pages, summary pages, probe history, and optional training
datasets. SSD never blocks a frame; parent summaries render while requests are
pending.

### GPU

GPU evaluates selected packets, splats, SDF proxy passes, density/transparent
volume passes, compute scoring where profitable, page-table sampling, and debug
views. It must not walk the authored grammar tree.

## Render Frame Flow

```text
1. Gather visible domains and active sensor regions.
2. Load resident summaries and previous reservoirs.
3. Generate structural and sensor candidates under CPU budget.
4. Evaluate targets and source PDFs.
5. Update local reservoirs.
6. Validate temporal reuse.
7. Validate spatial/domain reuse.
8. Update occupancy graph statistics.
9. Select hierarchy cut under CPU/GPU/RAM/SSD budgets.
10. Queue missing pages; keep parent summaries active.
11. Lower selected evidence to direct SDF, mesh, volume, or field-splat packets.
12. Render surface and/or transparent field passes into scene and guide targets.
13. Resolve with reservoir guide buffers.
14. Emit debug telemetry and evidence logs.
```

## Test Boundaries

Pure unit seams:

- projection round trips and seam continuity;
- domain lineage and shift mapping;
- DSL expansion determinism;
- ownership bounds;
- summary conservativeness;
- reservoir math;
- stochastic scheduler convergence;
- residency selection under fake stores.

Mock boundaries:

- `IFractalRandom`;
- `IFractalClock`;
- `IContributionProbe`;
- `IFractalPayloadStore`;
- `ISensorCandidateSource`;
- `IDomainShiftMap`;
- `ITaaGuideSink`;
- `IFractalDebugSink`.

GPU parity:

- envelope evaluator;
- projection evaluator;
- packet decoding;
- selected-cut fade;
- guide-buffer packing.

Performance fixtures:

- fixed camera path;
- fixed DSL world;
- fixed sensor trace;
- fixed budget profile for GTX 1070-class hardware;
- output CPU ms, GPU ms, payload count, page requests, selected cut size,
  reservoir confidence, and convergence error.

## Waterfall Implementation Phases

### Phase A: Architecture Freeze

Deliver this document, the public thesis article, updated memory, and a cut
line for the reservoir guide-buffer fork.

### Phase B: Explicit Reservoir Guide Layout

Add a dedicated reservoir guide history target rather than packing previous
reservoir validity into existing control channels. Preserve current-control.w
as the current confidence lane.

Guide schema:

```text
x: reservoir confidence
y: reservoir sample age
z: domain validity
w: invalidation code
```

Current state: SDF and temporal Gaussian passes write a scene reservoir guide
MRT. Resolve reads current and previous guide textures, folds confidence and
domain validity into history validation, and writes a ping-ponged history
reservoir guide target. `history-control.w` remains pixel history age.

### Phase C: Fractal Probe Pipeline

Turn IFS node probes into typed reservoir candidates with renderer-facing camera
motion, disocclusion, material, and visibility validation.

Current state: `FractalStructuralProbeGenerator` lowers an
`AquariumFractalSummary` into `FractalProbeSample` candidates with local center,
bound radius, projected target contribution, source PDF, material delta, and
payload handle. Renderer-facing temporal validation remains open.
`FractalStructuralProbeReservoir` builds a selected-cut reservoir from an
ownership tree, summaries, and projected pixels per world unit; Zyphos now
exposes that reservoir in its fractal render plan/debug dump.
`FractalProbeReuseValidator.ValidateTemporal` now names the renderer-facing
rejection reasons for camera motion, disocclusion, material mismatch, and
visibility before those checks are lowered to GPU reuse passes.

### Phase D: Occupancy Graph

Promote contribution state into the heuristic occupancy graph and add
confidence decay, stale uncertainty growth, and convergence telemetry.

Current state: `FractalOccupancyGraph` now owns per-node contribution state,
last visible frame, last observed score, and update probability. The
contribution cache routes through this graph, and unobserved nodes decay
confidence while stale uncertainty grows. Residency planning now keeps resident
payloads by score-per-cost and emits eviction decisions for low-value expensive
payloads. Convergence telemetry and richer resource-tier residency fields remain
open.

### Phase E: 2D Surface Field Tile Backend

Build cached 2D SDF/height/material/confidence pages for cube-sphere and torus domains.
Use parent summaries while child pages stream.

Current state: `AquariumFractalSurfacePageKey` and
`AquariumFractalSurfacePage` define renderer-agnostic page metadata for height,
2D signed-distance, material, and confidence pages. `FractalSurfacePagePlanner`
turns a selected IFS cut into stable page metadata, and the Zyphos render plan
now exposes height, 2D signed-distance, material, and confidence pages beside
its structural probe reservoir. `FractalSurfacePageResidencyPlanner` adds a
mockable page-store boundary with byte-budgeted resident, missing, requested,
and evicted page decisions. `FractalSurfacePageRasterizer` now lowers resident
pages to CPU height, 2D signed-distance support, material, and confidence
sample payloads using the same shaped brush envelope math as the live brush
compiler. The real page store, atlas, and D3D12 lowering remain open.

### Phase F: 3D Form Field Splat Backend

Build compact-support 3D form splat packets for object/volume domains. Opaque
solids use SDF/level-set packets; transparent media use density/extinction
packets. Keep distance safety conservative and LOD gated for solids, and keep
volume cost bounded by explicit extinction/support budgets.

Current state: `FractalProbeSample` now carries explicit field layer and
encoding metadata. The same reservoir sample type can represent opaque
`Form/SignedDistance` and transparent `Form/Density` or `Form/Extinction`
evidence without creating a second cache. `AquariumFractalSdfSplat3D` and
`FractalSdfSplat3DCompiler` own the opaque SDF lowering path;
`AquariumFractalDensitySplat3D` and `FractalDensitySplat3DCompiler` own the
transparent density/extinction lowering path. Reuse rejects mismatched
encodings before lineage/local-shift reuse can smear density and SDF together.
The `.aquageo` DSL accepts `density` and `extinction` claims beside `height`,
and its `flame` recursive primitive now emits density claims instead of height
claims. Height brush lowering filters to height payloads, leaving transparent
Form claims for field lowering. Zyphos now exposes a first structural SDF splat
from its probe reservoir. D3D12 lowering and object/body recursive form
integration remain open.

### Phase G: 2D-Projected-To-3D Field Backend

Project 2D field pages onto 3D domains: cube-sphere planets, torus stations,
curved sheets, object-local surfaces, and transparent sheets/volumes when the
domain mapping supports them.

Current state: `FractalProjectedSdfSplatCompiler` lowers resident
`SignedDistance2D` page payloads into compact 3D SDF splats through an explicit
`Vector2 -> Vector3` mapping and surface normal. Zyphos caches a first bounded
set of projected surface splats from its resident SDF pages using a flat local
mapping; real cube-sphere and torus mappings remain open.

### Phase H: Mimir Adapter

Add sensor candidate adapters after the fractal reservoir path proves the
contract. Camera/audio features become candidates; Fensalir owns resolved
evidence. Sensor fusion initially emits confidence/density form claims; only
coherent gradients and multi-view/acoustic agreement should promote them into
surface claims.

### Phase I: GPU Reservoirs And Spatial Reuse

Move hot reservoirs to GPU buffers, add spatial reuse across screen tiles and
domain neighbors, and keep CPU/GPU parity fixtures.

Current state: `tools/Aquarium.Fractal.Receipt` is the first GPU-resident
receipt harness. It allocates an 80-byte packed SDF splat UAV plus three
separate 64-byte reservoir UAVs for SDF envelopes, PBR material envelopes, and
radiosity because that was the first opaque-object packet slice. Conceptually
those are the first Form, Appearance, and Transport reservoirs. The receipt
runs independent D3D12 compute passes for splat population, form/SDF reservoir
sampling, appearance/PBR reservoir sampling, and transport/radiosity reservoir
sampling; the three reservoir passes share only the stochastic update budget
vocabulary, not packet anatomy. On the local GTX 1070, the budgeted receipt
kept `2,000,000` splats and `2,000,000` reservoirs per layer resident, updated
`50,000` entries per reservoir layer per frame with two candidates per update,
and measured `4.471 ms/frame`, `223.7 FPS` equivalent over `120` frames. This
proves compute-side GPU residency and budgeted temporal convergence for the
layered cache; final shaded visibility still has to consume the same packed
buffers. Next packet evolution should add density/extinction Form rows and
participating-medium Appearance/Transport rows instead of forcing flames through
opaque SDF/PBR semantics.

### Phase J: Learned Priority Gate

Only after telemetry exists, train or calibrate a predictor for update/residency
priority. Delete it if it does not beat the heuristic on held-out camera/sensor
traces.

## Cut Lines

- No learned predictor before telemetry.
- No recursive 3D form before 2D tile summaries and debug views are boring.
- No consumer-owned stable temporal cache.
- No GPU grammar traversal.
- No SSD frame stalls.
- No reuse without an explicit domain shift and validation contract.
- No debug-free recursive density.

## Sources

- NVIDIA ReSTIR DI:
  https://research.nvidia.com/publication/2020-07_spatiotemporal-reservoir-resampling-real-time-ray-tracing-dynamic-direct
- NVIDIA ReSTIR GI:
  https://research.nvidia.com/publication/2021-06_restir-gi-path-resampling-real-time-path-tracing
- GRIS:
  https://graphics.cs.utah.edu/research/projects/gris/sig22_GRIS.pdf
- RTXDI ReSTIR docs:
  https://github.com/NVIDIA-RTX/RTXDI/blob/main/Doc/RestirGI.md
  https://github.com/NVIDIA-RTX/RTXDI/blob/main/Doc/RestirPT.md
- EWA splatting:
  https://dash.harvard.edu/bitstream/handle/1/4138240/Zwicker_EWA.pdf
- Object-space EWA surface splatting:
  https://publications.ri.cmu.edu/object-space-ewa-surface-splatting-a-hardware-accelerated-approach-to-high-quality-point-rendering/
- 3D Gaussian Splatting:
  https://arxiv.org/abs/2308.04079
- Geometry clipmaps:
  https://developer.nvidia.com/gpugems/gpugems2/part-i-geometric-complexity/chapter-2-terrain-rendering-using-gpu-based-geometry
- Nanite:
  https://advances.realtimerendering.com/s2021/Karis_Nanite_SIGGRAPH_Advances_2021_final.pdf
- KinectFusion:
  https://www.microsoft.com/en-us/research/publication/kinectfusion-real-time-dense-surface-mapping-tracking/
- Curless and Levoy volumetric fusion:
  https://graphics.stanford.edu/papers/volrange/volrange.pdf
- Instant-NGP:
  https://research.nvidia.com/publication/2022-07_instant-neural-graphics-primitives-multiresolution-hash-encoding
