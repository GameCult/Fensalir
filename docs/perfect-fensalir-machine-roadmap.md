# Perfect Fensalir Machine Roadmap

## Objective

Build the real Fensalir field machine: a renderer where pixels consume selected
domain evidence instead of owning temporal truth.

The target is not a prettier TubeField, a smarter TAA pass, or a reservoir name
painted onto a visibility buffer. The target is a vertically integrated
pipeline:

```text
producer intent / DSL / resource / sensor input
-> semantic field and radiance claims
-> domain binding and conservative summaries
-> bounded proposals with target and proposal measure
-> budgeted native-domain reservoir work grid
-> temporal and spatial domain shifts under sampling/update budgets
-> TSR-grade rejection and reconstruction
-> denoiser-integrated full-present HDR field output
-> contribution/residency feedback
```

Every phase below exists to pay one proof obligation. If a phase cannot name the
invariant it proves, it does not ship.

## Current Mechanism

Fensalir already has the first shared GPU field-reservoir spine:

- TubeField and scene/SDF contributors normalize into a shared
  `FieldReservoirSample` row shape.
- Proposal lanes carry target, source PDF, represented count, and proposal kind.
- Stats lanes carry selected target, weight sum, candidate count, and
  contribution weight.
- The nine-lane `FieldReservoirSample` row now carries color/travel, metadata,
  control, guide, motion, selected domain/sample coordinate, support/shift
  metadata, proposal, and stats.
- The four rows per pixel mean current RIS, temporal reuse, spatial/domain
  reuse, and final resolved reservoir.
- The old TubeField-local ReSTIR/tile-bin path and nearest-travel-minus-coverage
  final authority have been cut.
- Debug modes now inspect the compute-owned reservoir layer rather than stale
  guide textures.
- The reservoir work grid is now explicitly budgeted below presentation
  resolution by default. Scene evidence targets, reservoir candidate/history
  rows, reservoir resolve, bloom, and match-window graph targets follow the
  internal grid; the full-resolution buffer is the final presentation surface
  where reconstruction, denoising, bloom, tone mapping, and debug display spend
  their visible-output budget.

The surviving flaw has moved one layer downstream: the live ABI now stores
selected sample UV, packed producer coordinate, support footprint, domain kind,
and shift kind, and temporal/spatial validation uses support overlap. TubeField
history now re-evaluates shifted selected logical column/curve coordinates
through a bounded producer-keyed replay manifest plus raw source descriptor
table, so the history shader can choose the correct replay source per selected
TubeField sample. Manifest overflow remains invalid rather than guessed. SDF history now carries previous object hits through
object motion and validates the replayed hit against the current camera ray,
travel, and conservative object support before temporal reuse may merge it.
Exact client SDF distance re-evaluation is still producer-owned work; the
native post pass does not pretend to own every client distance function.

## Invariants

- Pixels are consumers, not owners.
- Backend packets are lowerings, not truth.
- Candidate generators own proposal distributions and finite proposal measures.
- Target evaluators own importance units.
- Reservoirs own RIS/merge math and represented mass.
- Reuse passes own shift mappings, support overlap, and producer re-evaluation.
- TSR-style resolve owns rejection, reconstruction, and history trust over the
  resolved field output.
- Native-domain reservoirs are not native-resolution buffers. Domain cache
  updates, proposal injection, temporal/spatial reuse, bloom sources, and graph
  intermediate targets run under an explicit work-grid budget; only final
  presentation is allowed to be full resolution.
- The denoiser consumes reservoir diagnostics and can feed sampling pressure
  back into future probe selection.
- Conservative summaries own safety. Stochastic or learned priority may rank
  work; it may not replace bounds, visibility, support, or calibration truth.

## Authority Map

- Owner: the native-domain field reservoir owns temporal/spatial reuse and
  final resolved field membership.
- Inputs: current producer proposals, previous final reservoirs, domain
  mappings, producer support footprints, current/previous motion, scene/SDF
  evidence, guide confidence, reconstruction feedback.
- Outputs: budgeted current/temporal/spatial/final reservoir stages, budgeted
  resolved HDR field texture, full-resolution presented frame,
  rejection/debug layers, contribution/residency feedback signals.
- Derived state: work-grid pixel rows, MRTs, proxy raster output, debug
  palettes, upscaled presentation, and presentation history are projections or
  diagnostics.
- Forbidden writers: native-resolution intermediate caches, proxy
  rasterization, presentation, old TubeField-local paths, stale history rows,
  nearest-depth priority, and generic postprocess history cannot decide final
  field visibility.
- Shared paths: TubeField, SDF, compact splats, meshes, volumes, fractal probes,
  and future sensor fields must enter through bounded proposals with target,
  proposal measure, selected domain coordinates, and support metadata.
- Deletion line: any compatibility path that can make an old texel-owned sample
  visible after native-domain validation rejects it must be deleted or demoted
  to debug-only.

## Phase 0: Freeze The Proof Target

Purpose: stop the broad architecture from pretending to be an evaluated paper.

Deliverables:

- Keep `docs/papers/fensalir-pipeline.tex` as the architecture synthesis.
- Treat `docs/papers/fensalir-native-domain-reservoirs.tex` as the evaluated
  core paper target.
- Keep the first falsifiable claim narrow:

```text
Native-domain reservoir state for mixed TubeField/SDF/splat reuse reduces
temporal leakage and ghosting under motion, occlusion, and resolution changes
relative to texel-owned reuse at comparable sample budgets.
```

Exit gate:

- The implementation backlog, debug views, and captures all map to that claim.
- No new broad subsystem is allowed to jump ahead of the native-domain proof.

## Phase 1: Add Native-Domain Reservoir State

Purpose: make it structurally possible for the reservoir to know what sample it
selected.

Current ABI:

```text
colorTravel
metadata
control
guide
motion
proposal
stats
```

Required ABI addition or repack:

```text
domainSample:
  selectedPixelOrSampleUv.xy
  selectedProducerCoord.xy_or_packed
  supportFootprint.xy_or_radiusShape
  domainKind / shiftKind / supportShape where needed
```

Minimum per-producer state:

- TubeField: subpixel position, source row/column, curve parameter or segment
  coordinate, tube-core radius/support, feather, rolling-column motion.
- SDF: object/world hit coordinate, local object coordinate where available,
  normal/gradient, conservative SDF support/bound, object motion.
- Compact splat: local sample coordinate, orientation/frame, anisotropic
  support, projected footprint, domain lineage.

Implementation cuts:

1. Add the lane/packing helpers in HLSL and C# mirror structs.
2. Preserve row 0/1/2/3 semantics.
3. Make invalid or missing domain-sample state fail validation for producers
   that claim area support.
4. Add CPU/HLSL packing parity tests.
5. Add debug modes for selected sample coordinate and support footprint.

Exit gate:

- TubeField and SDF proposals both write selected domain/sample data into row 0.
- Row 3 debug can display selected coordinate, support shape, and domain kind.
- Old rows without domain support cannot silently validate as native-domain
  samples.

## Phase 2: Define Target And Proposal Measures

Purpose: stop heterogeneous producer targets from laundering incompatible units
through RIS.

Shared target form:

```text
target =
  projected support
  * expected field/radiance relevance
  * visibility/domain validity
  * producer confidence
```

Producer policies:

- TubeField target: projected core coverage times guide confidence times
  luminance/emission relevance times visibility.
- SDF target: projected covered area times depth/normal confidence times
  material/radiance relevance times visibility.
- Splat target: projected anisotropic footprint times confidence times
  Form/Appearance contribution bound.

Proposal measure rules:

- Finite deterministic proposal sets must state their finite probability, not
  hide behind a magical `sourcePdf = 1`.
- Singleton deterministic proposals may use `sourcePdf = 1` only when the
  represented count and target are normalized to that singleton.
- Stochastic producers must publish the actual sampling PDF or a conservative
  explicit policy.
- Invalid zero, negative, NaN, or infinite target/PDF is rejected before GPU
  proposal emission.

Exit gate:

- Tests assert proposal policy survives field evidence planning and backend
  lowering for TubeField, SDF, and one compact splat proof input.
- Debug views expose target, source PDF/proposal probability, represented
  count, selected target, weight sum, and contribution weight at the owner row.

## Phase 3: Native Temporal Reuse

Purpose: replace texel history trust with support-aware domain replay.

Budget law:

- Temporal reuse operates over the internal reservoir work grid, not the final
  present grid.
- Work-grid scale is clamped below native presentation scale by engine
  settings; present resolution may increase, but cache/update work may not
  silently become native resolution.
- Any future adaptive planner may spend more samples on high-pressure regions,
  but it must do so by explicit budget allocation, not by allocating another
  full-resolution native reservoir buffer.

Algorithm:

1. Reproject or map the current target domain into the previous frame.
2. Load previous row-3 reservoir candidates.
3. Transform the previous selected domain coordinate into the current target
   domain.
4. Test support overlap.
5. Re-evaluate the producer at the shifted/replayed coordinate.
6. Validate visibility, travel, normal/gradient, field id, domain kind, layer,
   encoding, and confidence.
7. Merge only after the shifted sample has a valid target/proposal story.

Initial shift modes:

- TubeField random replay: reuse selected curve/source coordinate under rolling
  motion, then re-evaluate the tube support at the current pixel/sample.
- SDF primary hit replay: map previous world/object hit forward and validate
  visibility/travel/normal in the current domain. The live proof validates the
  object-motion hit against current ray/support; exact per-producer distance
  re-evaluation waits for producer replay manifests or shared replay includes.
- Splat local-frame replay: transform selected local coordinate through the
  domain lineage and validate support/footprint.

Exit gate:

- Rejected history cannot become visible fallback.
- Debug distinguishes support miss, invalid shift, visibility miss,
  disocclusion, field mismatch, travel mismatch, normal mismatch, and missing
  explicit motion.
- Captures show the rejection reason at the same layer where the visible bug is
  observed.

## Phase 4: Native Spatial/Domain Reuse

Purpose: make neighbor reuse a domain operation rather than a screen-neighbor
guess.

Neighbor classes:

- screen-tile neighbors for local antialiasing;
- TubeField curve/source neighbors;
- SDF object-local neighbors;
- compact splat/domain-lineage neighbors;
- later cube-face/quadtree seam neighbors.

Rules:

- A neighbor reservoir must be shifted or re-evaluated in the target domain
  before merge.
- If the shift changes measure, carry a Jacobian or explicit finite-set/MIS
  correction.
- If no correction is defined, the shift is invalid.
- Spatial reuse should reduce speckle without broadening support beyond the
  selected producer-domain footprint.

Exit gate:

- Spatial debug shows accepted neighbor count, rejected reason, selected shift
  kind, and contribution delta.
- Ablation `no support overlap` visibly leaks or ghosts in at least one stress
  scene, proving the test can catch the old failure.

## Phase 5: TSR-Grade Resolve And Denoiser Feedback

Purpose: connect reservoir truth to reconstruction instead of smearing after the
fact.

Resolve inputs:

- selected sample coordinate and support;
- contribution weight, selected target, weight sum, candidate count;
- confidence, variance/uncertainty, sample age;
- rejection reason and shift kind;
- field id, layer, encoding, normal/gradient, travel, motion;
- disocclusion/occlusion classification.

Behavior:

- Current evidence dominates when support or visibility is invalid.
- Rejected regions get spatial reconstruction/AA appropriate to the rejection
  class.
- Stable selected evidence accumulates subpixel detail.
- Denoiser stress feeds back into future sampling priority for affected domain
  nodes/producers.

Exit gate:

- Debug views can explain final history weight, reconstruction class, support
  miss, occlusion/disocclusion, spatial AA, and current-frame dominance.
- Final output no longer depends on a generic postprocess history pass to hide
  producer mistakes.

## Phase 6: Controlled Evaluation Harness

Purpose: generate the proof the focused paper demands.

Scenes:

1. Occluded spectrum tubes: dense rolling TubeField columns behind/through an
   SDF occluder.
2. Mixed SDF and splat surface: analytic SDF object with compact splat detail
   and camera/subpixel motion.
3. Resolution sweep: same world-space support at multiple output resolutions.
4. Disocclusion stress: fast camera/object motion exposing curve/SDF
   intersections.
5. Near-equal-depth conflict: overlapping field claims at similar travel.

Baselines:

- raw current-frame rendering;
- TSR/TAA-style texel history with depth/normal/field-id validation;
- texel-owned field reservoir without selected native coordinate;
- producer-specific TubeField reuse path if resurrected as a measurement-only
  baseline;
- native-domain reservoir with support and shift metadata.

Ablations:

- no selected domain coordinate;
- selected coordinate but no support overlap;
- support overlap but no producer re-evaluation;
- no measure/Jacobian/MIS correction;
- no confidence/variance feedback;
- no structural occlusion gate before RIS.

Metrics:

- temporal leakage pixels in occluded/background regions;
- ghosting duration after disocclusion;
- support-miss rejection precision/recall against producer re-evaluation;
- resolved image error against high-sample/high-budget reference;
- stable-region stochastic variance;
- GPU time, memory per row, bandwidth, and candidate counts.

Exit gate:

- The focused paper replaces prototype debug screenshots with controlled
  comparison figures, ablation tables, and timing/memory data.

## Phase 7: Multi-Producer Contract Expansion

Purpose: prove the reservoir is shared machinery, not TubeField exceptionalism.

Add contributors in this order:

1. TubeField + direct SDF: current proof pair.
2. Compact Form/Appearance splats: first fractal/splat proof.
3. Mesh summary contributor: triangles as field evidence, not bypass.
4. Density/extinction volume: transparent Form, separate resolve semantics.
5. SurfacePage/VolumeTexture resources: resource-backed claims.

Exit gate:

- Each contributor emits the same proposal/domain-sample/stats contract.
- No contributor owns final temporal visibility outside the shared reservoir.
- Debug can compare target, source proposal measure, support, and rejection
  reasons across producer families.

## Phase 8: Fractal Contribution Tree Integration

Purpose: connect the reservoir to the Perfect Machine hierarchy.

Build:

- node summaries as conservative safety authority;
- stochastic contribution estimator with mean, variance, confidence, sample
  age, update probability;
- selected-cut planner under CPU/GPU/RAM/SSD budgets;
- native-domain reservoir proposals from selected fractal nodes;
- feedback from denoiser/reconstruction stress into node update priority.

Do not build:

- learned predictor before telemetry;
- GPU grammar traversal;
- recursive detail without debug views;
- parent/child fade that violates signed-distance safety.

Exit gate:

- A selected fractal node can emit a native-domain proposal, survive temporal
  validation, lower to a backend packet, and feed contribution/residency state.

## Phase 9: Backend And Residency Maturity

Purpose: make the machine usable across real scenes without hidden CPU rescue.

Backends:

- 2D height/SDF/material/confidence pages;
- 3D compact SDF splats;
- density/extinction/emission splats;
- 2D-projected-to-3D fields;
- generated meshes;
- resource-backed mesh/surface/volume packages.

Residency:

- GPU resources stay GPU-resident.
- Missing children render through parent summaries.
- SSD never blocks a frame.
- Resource declarations remain validity/version/fence contracts, not payload
  authority.

Exit gate:

- The selected cut can stream, evict, and render parent fallbacks while keeping
  reservoir identity stable across residency changes.

## Phase 10: Sensor And Planetary Flow Organs

Purpose: bring Mimir and planetary flow into the same evidence machinery only
after native-domain rendering is real.

Mimir path:

- raw capture stays in Mimir;
- calibrated features become Form/Appearance/Transport claims;
- confidence/density fields are first-class fields;
- stable surfaces emerge only after multi-view/acoustic evidence earns them.

Planetary flow path:

- cube-sphere tiles remain runtime authority;
- ERA5/GLORYS/OSCAR/PlasticAdrift-style data trains or validates stochastic
  advection packets;
- flow emits velocity/covariance/transition-kernel claims;
- visible flow fields reuse the same reservoir/reconstruction diagnostics.

Exit gate:

- Sensor or flow data enters as bounded domain evidence, not a second temporal
  cache or client-owned renderer.

## Phase 11: Learned Priority Gate

Purpose: let learning rank work only after the deterministic machine is
inspectable.

Allowed:

- predict expected contribution;
- predict uncertainty;
- rank update priority;
- rank residency priority;
- distill expensive probes into cheaper scheduling features.

Forbidden:

- learned SDF safety;
- learned visibility authority without conservative bounds;
- learned calibration truth;
- learned replacement for target/proposal accounting.

Exit gate:

- Learned priority beats EMA/variance/bandit scheduling on held-out traces with
  better quality/cost and no safety regression. Otherwise delete it.

## Immediate Next Work Packet

Objective: pay the first proof obligation for the focused native-domain paper.

Work packet:

1. Done: add/repack the `FieldReservoirSample` ABI with selected domain/sample
   coordinate and support footprint.
2. Done: write TubeField row-0 selected subpixel/source/curve support.
3. Done: write SDF row-0 selected hit/domain support.
4. Done: add debug modes for selected coordinate, support footprint/domain,
   and shift kind.
5. Done: implement temporal/spatial support-overlap validation before further
   threshold tuning.
6. Partial: implement producer re-evaluation after the support-overlap gate for
   TubeField rolling-column replay. The path now uses a bounded replay manifest
   and source descriptor table instead of the old single-batch binding; batches
   beyond the replay-source cap remain invalid until the source table grows or
   a replay atlas replaces it.
7. Done: implement SDF object-hit replay validation. Previous SDF hits are
   reconstructed from stored selected UV/travel, carried through object center
   motion, and checked against the current camera ray/travel/support before
   reuse can merge them.
8. Done: add an explicit field-reservoir mode switch and capture paired
   native-domain versus texel-baseline outputs. The first comparison set lives
   under `artifacts/fensalir-captures/reservoir-compare-20260529-231904-*`.
   Native versus baseline changed 10.8208% of final-frame pixels, 0.0165% of
   rejection-debug pixels, and 0.2976% of shift-debug pixels. That proves the
   switch is live; it is not yet a quality metric.
9. Done: make the reservoir work grid explicitly budgeted below final
   presentation resolution. `GraphicsSettings.FieldReservoirScale` clamps the
   internal scene evidence, reservoir candidate/history, resolve, bloom, and
   match-window graph targets to 0.25-0.75 of present size, with 0.5 as the
   default. Headless capture accepts `--field-reservoir-scale`; debug UI reports
   the live work-grid size beside present size.
10. Partial: add temporal sequence probes. `scripts/capture-reservoir-mode-sequence.ps1`
    builds one disposable runtime slot and captures native/baseline sequences
    across ready-frame counts, debug modes, and work-grid scale.
    `scripts/measure-reservoir-sequence.ps1` reports per-frame native/baseline
    disagreement, per-mode frame-to-frame temporal deltas, and masked rows when
    rejection-debug captures exist. Smoke runs:
    `reservoir-sequence-20260529-234414-*` at 640x360, final color only, and
    `reservoir-sequence-20260529-235031-*` at 320x180 with final plus rejection
    debug. The latter proved resumable capture and rejection-mask filtering; it
    is still a probe, not a high-budget-reference ghosting score.
11. Current: add temporal leakage/ghosting metrics over motion/disocclusion
    sequences, then use those metrics to decide where the sampler spends its
    bounded update budget. The temporal owner now exposes debug mode 19,
    `Reservoir Disocclusion`, derived from temporal rejection codes for previous
    UV loss, previous-closer history, and support loss. The sequence measurer
    prefers disocclusion masks over broad rejection masks when they exist.
    Smoke run `reservoir-sequence-20260530-000151-*` captured native final plus
    disocclusion at 320x180; the disocclusion mask covered 3.1098% of frame
    pixels and produced a masked temporal-native delta row.
12. Partial: add same-time higher-work-grid reference scoring. The sequence
    capture script accepts `-ReferenceFieldReservoirScale`; reference captures
    use native-domain mode at that larger scale and are labeled `reference` in
    the manifest. The measurer reports `reference-error-*` rows against matching
    ready-frame counts and uses disocclusion masks when present. Smoke run
    `reservoir-sequence-20260530-001046-*` compared native scale 0.5 against
    reference scale 0.75 at 320x180, ready frame 2, final plus disocclusion.
    This is a higher-budget work-grid reference, not a native-resolution escape
    hatch.
13. Partial: add offline budget-pressure ranking from masked reference error.
    `scripts/measure-reservoir-budget-pressure.ps1` bins candidate-vs-reference
    final-color error by fixed tiles, applies the disocclusion/rejection mask
    union when present, and ranks tiles by mean reference error multiplied by
    mask coverage. Smoke `reservoir-budget-pressure-20260530-001046.*` measured
    1306 of 42864 pixels and found the top 5 of 50 tiles carried 99.3305% of
    measured reference-error delta. This is allocator evidence only; it does
    not own runtime visibility or scheduling.

Required verification:

- `dotnet build Fensalir.sln`
- `dotnet test tests/Aquarium.Engine.Tests/Aquarium.Engine.Tests.csproj`
- `dotnet test tests/Aquarium.Engine.Fractal.Tests/Aquarium.Engine.Fractal.Tests.csproj`
- Mimir synthetic spectrum upload smoke when touching TubeField integration.
- Headless captures for final color plus reservoir debug modes.

Cut line:

- If adding the lane becomes awkward because the current seven-lane ABI is too
  cramped, repack or extend the ABI. Do not route around it with a side cache
  that can become a second owner.

## Paper Sync Rule

Every completed phase updates:

- `docs/papers/fensalir-native-domain-reservoirs.tex` when it changes the
  focused claim, evidence, baselines, figures, or metrics;
- `docs/papers/fensalir-pipeline.tex` when it changes the broad architecture;
- `state/evidence.jsonl` when the lesson changes future belief;
- `state/map.yaml` when ownership or next actions change.

Architecture memory that does not survive into these surfaces is not memory. It
is just a dramatic local variable.
