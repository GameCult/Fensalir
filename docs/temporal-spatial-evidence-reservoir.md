# Temporal Spatial Evidence Reservoir

## Objective

Fensalir owns the shared temporal evidence machine for two customers:

- fractal rendering, where Form/Appearance/Transport candidates need bounded
  reuse across pixels, frames, and nested domains;
- Mimir sensor fusion, where camera and microphone features need a
  delayed coherence window before the resolved field is rendered.

The old stable-key accumulator was useful, but it was not ReSTIR. The live
architecture now splits the problem into a small resampled-importance core, a
spatial evidence track layer, typed lowerings, renderer passes, and reservoir
resolve history.

## Engine Identity

Fensalir is not a traditional rendering pipeline. A pass is not correct because
it produces pixels. A pass is correct only when it preserves the field evidence
contract for the surface or volume it claims to represent.

The renderer is allowed to use rasterization, compute, ray marching, splats,
meshlets, direct SDF proxies, or old-fashioned triangles, but those are lowering
strategies. They do not own truth. The live truth is the claim packet: stable
domain identity where available, bounded support, color/travel, metadata,
control/coverage, reservoir-guide evidence, and depth when temporal validation
needs it.

Any implementation that treats Fensalir as "draw some geometry, blend some
color, let post handle the rest" is violating the engine body. That path may
exist only as an explicitly named fallback/debug draw with no authority over
field evidence.

The reservoir is the organ. Payload families are not. A TubeField path may bind
a rolling buffer, evaluate Catmull-Rom tube envelopes, generate proxy geometry,
march a tube SDF, sample a blackbody ramp, and emit candidate weights. Those
are TubeField producer responsibilities. Final visibility, temporal reuse,
spatial reuse, validation, contribution weight, and presentation membership
belong to the shared spatiotemporal field reservoir.

## Research Spine

- NVIDIA ReSTIR DI repeatedly resamples candidate light samples, then applies
  spatial and temporal resampling to share useful samples across nearby pixels
  and frames. The paper reports equal-error speedups of 6-60x, with biased
  variants reaching 35-65x in its tested direct-lighting workloads.
  Source: https://research.nvidia.com/labs/rtr/publication/bitterli2020spatiotemporal/
- ReSTIR GI applies the same screen-space spatiotemporal reuse idea to
  multi-bounce indirect paths and reports 9.3x-166x MSE improvement at one
  sample per pixel in tested scenes.
  Source: https://research.nvidia.com/publication/2021-06_restir-gi-path-resampling-real-time-path-tracing
- GRIS generalizes RIS/ReSTIR to correlated samples, unknown PDFs, varied
  domains, and shift mappings. That is the important part for Aquarium:
  fractal domains and sensor-fusion samples are not all light samples.
  Source: https://research.nvidia.com/labs/rtr/publication/lin2022generalized/
- RTXDI's integration docs make the pass boundary concrete: acquire initial
  samples, store reservoir data, validate temporal reuse through reprojection,
  validate spatial reuse against nearby surfaces, shift candidate paths between
  domains, then shade/denoise from the final reservoir.
  Sources:
  https://github.com/NVIDIA-RTX/RTXDI/blob/main/Doc/RestirGI.md
  https://github.com/NVIDIA-RTX/RTXDI/blob/main/Doc/RestirPT.md
- Area ReSTIR is the missing area-domain reference. It stores the selected
  sample's actual pixel/lens coordinates and reuses history only when the
  previous sample lies in, or can be shifted into, the target support with valid
  target/PDF/visibility/Jacobian accounting. For Fensalir this generalizes to
  TubeField, SDF, mesh, volume, sensor, and fractal-domain support: selected
  domain coordinates are part of reservoir identity, not optional debug data.
  Sources:
  https://graphics.cs.utah.edu/research/projects/area-restir/
  https://github.com/guiqi134/Area-ReSTIR

## Fensalir Divergence

Pure ReSTIR usually starts after primary visibility is known: a ray or G-buffer
surface exists, then the reservoir chooses and reuses lighting or path
candidates for that surface. Fensalir cannot assume that order for field
claims. In many Fensalir scenes, the thing being sampled is the visible field
candidate itself: tube surface, SDF envelope, splat support, volume boundary,
sensor confidence surface, or mesh summary.

That makes the shared field reservoir responsible for the job normally assigned
to TAA. Claim producers must first generate local visibility
candidates with stable identity, support, depth/travel, coverage, material
encoding, target value, and proposal policy. The reservoir then selects and
reuses those field candidates across time and space. Material and transport
evaluation ride on the selected field candidate; they do not repair missing
Form identity after the fact.

There is no separate TAA organ in the target architecture. Any temporal
stability, history validation, antialiasing, or reconstruction happens inside
reservoir update and reservoir resolve. The final presentation pass samples or
filters the resolved reservoir; it does not repair it. If a field surface only
becomes stable because a downstream pixel-history pass smears unstable pixels
until they look acceptable, the field producer or reservoir validation is
wrong.

The next divergence is more important: the reservoir is not fundamentally a
screen-space texel structure. Texel rows are the current GPU execution surface.
The actual reservoir domain is fractal and field-native: semantic claims lower
through ownership trees, conservative summaries, stochastic contribution
caches, and bounded proposals before a per-frame row projection exists. Pixel
resolution is therefore a budget and resolve target, not the source of sampling
truth.

## Pipeline Map

```text
producer observations
-> local visibility candidate generator
-> target evaluator
-> ResampledImportanceReservoir<TSample>
-> temporal reuse pass
-> spatial/domain reuse pass
-> validation + shift mapping
-> TemporalSpatialEvidenceReservoir track layer when stable fields are needed
-> TemporalSpatialEvidenceLowering
-> backend packet stream
-> renderer/fusion passes
-> reservoir resolve/history buffers
```

## Ownership

`ResampledImportanceReservoir<TSample>` owns RIS/ReSTIR-style candidate math:

- one selected representative sample;
- summed importance weight of accepted candidates;
- number of source candidates represented by the reservoir;
- selected sample target value;
- final contribution weight `weightSum / (candidateCount * selectedTarget)`.

The sample type owns selected domain identity:

- domain key and producer kind;
- selected coordinate inside the domain, including subpixel or local support
  coordinates when the producer has area support;
- support/filter footprint and conservative bounds;
- legal shift/reconnection metadata needed by temporal and spatial reuse.

If those values are absent, the reservoir cannot prove area-domain reuse. It
may still be a useful cache, but it is not the Perfect Machine reservoir.

Candidate generators own proposal distributions. A fractal renderer may propose
form probe candidates from projected error, node bounds, blue-noise screen
tiles, or resident children. Mimir may propose visual/audio feature candidates
from sensor confidence and calibration state. The reservoir does not know these
domains; it only receives target and source-PDF values.

For opaque Form fields, local candidate generation owns primary visibility
discovery before reservoir reuse. It may be deterministic when sparse stochastic
sampling would leave holes in the visible surface. This is not a fallback to a
private renderer; it is the producer side of the shared reservoir contract.
TubeField's current concrete example is raster proxy candidate discovery:
compute emits conservative segment envelopes, the fixed-function pipeline
finds covered pixels, and the proxy pixel shader evaluates the tube SDF before
writing candidates into the shared reservoir inlet.

Every evidence sample should state its layer and encoding before lowering:
Form, Appearance, or Transport; then SDF, height, density, extinction,
material, phase, emission, radiance, feature, or confidence as appropriate.
Opaque worlds can promote Form evidence into SDF surfaces. Flames and uncertain
sensor fields may remain density/extinction/confidence volumes. That is not a
failure to find the surface; it is the correct field.

Reservoir resolve may happen at pixel scale, but reservoir contents are not
therefore pixel-sized. A claim carries bounded support in its own domain. A
perfectly flat, infinite, uniform plane is theoretically one enormous surface
claim with uninteresting curvature and material variation. A complex terrain
is the opposite: it should subdivide only where projected footprint, curvature,
brush detail, material change, or temporal uncertainty require more claims.
Pixel-level resolve samples the claims; it does not dictate their native scale.

For heightfield terrain, the coherent shape is a quadtree over the authored
surface domain. 2D surface brushes paint into quadtree tiles to determine the
height/SDF/material payload. The SDF probe or surface-claim producer then emits
surface splats at the density needed to represent tile detail under the current
view: flat tiles can stay broad, while high curvature, sharp brush gradients,
silhouettes, and high projected error subdivide. A reservoir full of pixel-sized
terrain dots is a failed lowering unless the terrain actually contains
pixel-scale variation.

Reuse passes own validity and shift mapping. A sample may be reused only when
the target domain can explain it. For pixels this means depth/normal/material
compatibility, motion vectors, conservative visibility, and disocclusion tests.
For fractal domains it means matching domain ancestry, bounded local-frame
error, resident payload compatibility, conservative surface bounds for SDF
encodings, and conservative support/extinction bounds for density encodings.
For sensor fusion it means time delay, calibration confidence, modality
agreement, and feature reprojection error.

`TemporalSpatialEvidenceReservoir` owns stable resolved spatial tracks after
candidate selection when the output is a field with persistent identity:

- stable key identity;
- accumulation window and presentation delay;
- confidence-weighted smoothing;
- velocity prediction;
- history weight from confidence and sample age;
- expiry;
- max-track eviction by confidence and age.

`TemporalSpatialEvidenceLowering` owns packet conversion. Consumers do not pack
payload vectors by private convention when a lowering helper exists.

Reservoir resolve owns temporal antialiasing and reconstruction over the
resolved field output. It consumes reservoir confidence, sample age, domain id,
motion, and temporal-detail lanes. No separate TAA pass owns producer identity,
stable spatial evidence, or the decision that a field candidate persists across
frames.

## Contributor Paths

The reservoir is a consumer of coherent field evidence, not the single factory
that must manufacture every visible thing. Sensor fusion and adaptive IFS
probing are important producers, but they do not own the surface contract.

Supported contributor families:

- adaptive IFS and fractal probe candidates, lowered through selected evidence
  packets when stochastic refresh is the right cost model;
- live sensor and simulation fusion, where observations become calibrated Form,
  Appearance, or Transport evidence;
- direct SDF envelope passes, including bounded object proxies and
  texture-driven tube or curve fields, when the surface can be evaluated more
  honestly as an analytic or sampled envelope than as a cloud of probes;
- traditional mesh geometry, when triangles are the clearest resident summary
  or authoring surface for the current domain.

Every contributor that wants temporal reuse must emit the shared renderer
packet shape: color/travel, metadata, control, reservoir-guide evidence, and
depth where depth participates in validation. The exact producer can be a
compute pass, a proxy draw, a mesh draw, or a future volume pass. The invariant
is the evidence contract, not the producer's religion.

The shared field candidate inlet keeps a fixed small candidate budget per pixel.
That budget is currently four slots. Producers may compete for those slots, but
the shared resolver owns final priority and presentation reconstruction.
Payload code must not assume a private single winner path.

When a contributor needs specialized candidate generation, the specialization
stops at the candidate boundary. `TubeField` is therefore a claim/candidate
producer for rolling-buffer tube surfaces, not a separate TubeField reservoir
renderer. The same rule applies to meshes, volumes, fractal probes, surface
pages, and future sensor-derived fields.

Traditional meshes therefore do not bypass the machine. They are accepted when
they can provide stable identity, motion or previous-frame mapping where needed,
material/field encoding, conservative bounds, and guide data sufficient for
reservoir validation. A mesh that only paints pixels is a fallback draw. A
mesh that emits field evidence is a first-class contributor.

The `.aquafield` script surface follows the same boundary. A script declares
intent such as "read this rolling texture slice as a spline field over this
axis, with this surface graph and quality budget." It does not need to choose
direct SDF tubes, reservoir splats, or future mesh lowering. `lowering
mode=auto` is the default contract: the renderer may emit direct SDF tube
geometry when that is cheap and legible, decimate columns/control points under
LOD pressure, or route oversized fields through the reservoir path.

## Invariants

- Reservoir math is a pure, testable core before it becomes HLSL.
- A reservoir is not a dictionary of tracks. A track layer may use reservoirs,
  but it does not replace candidate resampling.
- A reservoir is not a texel dictionary either. Pixel rows are row-stage
  projections of selected domain evidence; they do not define the native
  sampling domain.
- Selected sample coordinates and support are part of reservoir identity for
  any area, tube, SDF, volume, sensor, or fractal-domain contributor.
- Reuse is invalid until a pass proves the shift/validation contract for the
  source and target domains.
- Conservative bounds remain the safety authority. Learned or stochastic
  priority may decide what to refresh first; it must not replace field bounds,
  visibility bounds, or calibration bounds.
- CPU, GPU, RAM, and SSD budgets are inputs to candidate generation and
  residency. They are not hidden side effects of renderer convenience code.
- Consumer repos do not grow parallel stable-key temporal caches.
- Producer kind is not authority. SDF proxies, direct tube fields, mesh
  geometry, sensor fusion, and adaptive probes are all legitimate only to the
  extent that they satisfy the same evidence, bounds, and temporal-guide
  contracts.

## Current State

Built:

- `ResampledImportanceCandidate<TSample>` stores a sample, target value, source
  PDF, and represented candidate count.
- `ResampledImportanceReservoir<TSample>` accepts candidates by weight
  proportional to `target / sourcePdf`, merges reservoirs, preserves represented
  candidate count, and exposes the RIS contribution weight.
- `TemporalSpatialEvidenceReservoir` remains the stable track layer used by
  temporal Gaussian fields and GPU sensor fusion.
- `FractalContributionCache` remains the node-summary LOD estimator, and now
  frames scheduled update nodes as weighted reservoir candidates with a
  per-frame reservoir snapshot for debug and tests. It is the candidate source,
  not the full temporal/spatial reuse pass.
- `FractalProbeSample` is the first typed fractal form/detail sample shape for
  the ReSTIR/GRIS path. It carries domain key, node key, local center, bound
  radius, target contribution, source PDF, material delta, and payload handle.
  `FractalProbeReuseValidator` currently proves domain lineage and local-shift
  compatibility; renderer-facing camera, disocclusion, material, and visibility
  checks still need to be added before temporal/spatial reuse is complete.

Not built yet:

- GPU reservoir buffers;
- camera/disocclusion/material validation for fractal form/appearance reservoirs;
- spatial neighbor reuse across screen tiles and cube-sphere neighbor domains;
- GRIS-style domain shift mappings for nested `.aquageo` domains;
- SSD/RAM residency queues driven by reservoir contribution estimates.

## Reservoir Guide Layout

Reservoir guide data is emitted by current-frame scene producers and then
persisted in ping-ponged structured reservoir history rows. It is deliberately
separate from legacy pixel-history control. Pixel age is now a field reservoir
history signal, not a separate TAA authority.

```text
x: reservoir confidence
y: reservoir sample age
z: domain validity
w: invalidation code
```

The first live producers are SDF surfaces, temporal Gaussian splats, and
TubeField candidates, but the schema is not surface-only. The shared reservoir
history update compute pass reads current candidate rows and previous structured
reservoir-history rows, folds confidence and domain validity into validation,
writes the next structured reservoir history, and emits the resolved HDR field
texture. Presentation samples that resolved texture; it does not write reservoir
history. Future ReSTIR/GRIS passes should extend the producer side of this
schema with explicit Form/Appearance/Transport fields rather than packing more
reservoir folklore into scene-control channels.

## Implementation Roadmap

1. Keep the CPU reservoir core pure and exhaustive under unit tests.
2. Add typed reservoir samples for fractal field probes: domain key, local
   frame, bound radius, target contribution, source PDF, layer/encoding, payload
   handle, and material or transport delta.
3. Turn `FractalContributionCache` into a candidate generator that refreshes
   nodes under CPU budget and submits candidates to the reservoir core.
4. Add temporal reuse for fractal probes using camera motion, domain ancestry,
   local-frame error, and conservative bounds.
5. Add spatial reuse across screen tiles and quadtree neighbors.
6. Lower selected reservoirs into GPU field packets. SDF packets are the solid
   surface encoding; density/extinction packets are the transparent/sensor
   volume encoding. Expose debug views for weight sum, selected target,
   candidate count, confidence, age, and invalidation reason.
7. Weave reservoir confidence and temporal detail into structured reservoir
   history rows so reservoir resolve can distinguish stable reused evidence
   from fresh stochastic noise. The current implementation keeps four
   resolver-owned rows per pixel, updates them in a shared compute pass, emits
   a resolved HDR field texture for bloom/presentation, and no longer stores
   previous reservoir validity in pixel-history MRTs.
8. Add Mimir-facing candidate adapters only after the fractal path proves the
   contract: modality features are candidates, not a second reservoir system.
9. Port the pure core to HLSL and add CPU/GPU parity fixtures.
10. Add residency scheduling: hot selected reservoirs keep GPU packets resident,
    warm reservoirs keep RAM payloads, cold reservoirs fall to SSD handles.

## Cut Line

If a new subsystem stores temporal candidates, it must state whether it owns raw
producer retention, resampled candidate selection, stable resolved tracks,
packet lowering, or reservoir resolve history. If it cannot name that authority,
it is not an architecture. It is a decorative leak.
