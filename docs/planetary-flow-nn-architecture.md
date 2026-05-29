# Planetary Flow Neural Architecture

This is the concrete model spec for Fensalir's planetary flow organ. It is not
the final law of physics. It is the first architecture sharp enough to build,
profile, falsify, and replace without pretending a vague "graph/operator
hybrid" was a plan.

## Recommendation

Use three model sizes:

| Model | Role | Parameter Target | Runtime Use |
| --- | --- | ---: | --- |
| FlowNet-S | ingestion and contract baseline | 8-15M | offline bake, debug, ablation |
| FlowNet-M | first real runtime/distillation model | 40-60M | primary tile-streaming model |
| FlowNet-T | global teacher / ensemble producer | 150-300M | offline distillation, not hot path |

FlowNet-M is the first serious target. It is small enough to distill, cache, and
eventually run per requested tile, but large enough to carry local terrain,
dynamic state, cross-face seams, and LOD context. Starting with a 500M-parameter
weather god would be a fine way to produce a dissertation-shaped blockage.

## Input Tensor

Each training sample is centered on a cube-sphere tile cell and carries a
multiscale neighborhood pyramid. The first cut should use three spatial scales:

```text
P0: 64x64 samples at target LOD
P1: 32x32 samples at parent/2x footprint
P2: 16x16 samples at parent/4x footprint
```

Static channels:

- elevation / bathymetry;
- land/sea/ice mask;
- slope x/y in local tangent frame;
- curvature or roughness;
- coastline distance;
- source quality / GEBCO TID or equivalent;
- latitude, sin(latitude), cos(latitude), Coriolis term;
- projection metric channels: area scale and tangent basis distortion.

Dynamic channels:

- recent target-layer velocity fields in local tangent coordinates;
- ERA5 `u10`, `v10`, pressure, temperature, humidity for atmosphere;
- ocean `uo`, `vo`, sea-surface height, SST, salinity or mixed-layer proxies
  where available;
- masks for missing source data;
- temporal deltas over the input history.

Conditioning tokens:

- day-of-year Fourier features;
- hour/diurnal Fourier features when using hourly atmosphere data;
- Coriolis parameters in the local frame, including `f`, beta/gradient terms
  when available, and rotation-axis direction;
- external forcing source tokens for arbitrary energy, mass, and momentum
  inputs;
- per-source direction in planet-centered coordinates and local tangent
  coordinates;
- per-source local elevation/azimuth at the sample point when directional;
- per-source intensity, spectral/thermal class, radius/angular size, distance
  or attenuation, and time profile;
- source relation features such as phase angles between dominant directional
  sources;
- lead time;
- layer id: ocean surface, atmosphere near-surface, or pressure level;
- dataset/model/provenance id embedding;
- requested output LOD.

Do not make elevation the protagonist. It is one channel group. The dynamic
state and time phase are not optional decoration; they are the difference
between a flow model and a coastline horoscope.

External forcing is an evaluator input, not a derived afterthought. Earth sun
and moon terms are just two source records in a general forcing set. The same
interface must support multiple suns, artificial heat sources, nuclear or
volcanic eruptions, injected momentum, gas-giant irradiation, extreme pressure
regimes, and non-Earth rotation. A forcing source can be directional,
point-like, area-like, volumetric, transient, periodic, or persistent.

Coriolis is also explicit. Latitude-derived `f` is fine for Earth data bakes,
but the model must receive rotation-derived Coriolis features directly so a
client can evaluate fast rotators, tilted axes, retrograde planets, gas giants,
or fictional worlds without smuggling the answer through Earth latitude.

## FlowNet-S: Local Baseline

Purpose: prove ingestion, tangent-frame targets, stochastic heads, calibration,
and page export.

Approximate size: 8-15M parameters.

Layer stack:

```text
per-scale input projection:
  3x3 conv, width 48/64/96 for P0/P1/P2

encoder:
  stage 0: 2 residual depthwise-separable conv blocks, width 64
  stage 1: downsample + 2 residual blocks, width 96
  stage 2: downsample + 3 residual blocks, width 128
  stage 3: downsample + 3 residual blocks, width 192

conditioning:
  FiLM or adaptive layer norm from forcing/Coriolis/time/layer/LOD embeddings

decoder:
  FPN/U-Net upsample to 64x64
  skip connections from encoder stages
  2 residual refinement blocks, width 96

heads:
  mean velocity: 2 channels
  covariance: 3 channels, Cholesky parameterized
  confidence/log variance calibration: 1 channel
  optional mask/validity: 1 channel
```

This model has no global graph. It should fail gracefully on long-range
circulation and seams. That failure is useful because it measures what the graph
must earn.

## FlowNet-M: Hierarchical Cube-Sphere Model

Purpose: first production-grade architecture for arbitrary LOD planetary flow.

Approximate size: 40-60M parameters.

High-level shape:

```text
multiscale tile pyramid
  -> shared local CNN/ConvNeXt encoder
  -> tile tokens
  -> cube-sphere graph transformer processor
  -> FPN decoder
  -> stochastic advection heads
```

### Local Encoder

Use a shared-weight encoder over each requested tile and its immediate neighbor
ring. For each scale:

```text
stem:
  3x3 conv to width 96

stage 0:
  2 ConvNeXt-style blocks, width 96

stage 1:
  stride-2 downsample
  3 ConvNeXt-style blocks, width 128

stage 2:
  stride-2 downsample
  4 ConvNeXt-style blocks, width 192

stage 3:
  stride-2 downsample
  4 ConvNeXt-style blocks, width 256

tile token:
  attention or mean pool to one 256-wide token per tile/scale
```

Use cross-face aware padding in the data loader before asking the model to
learn cube seams by suffering.

### Graph Processor

Build a graph over cube-sphere tile tokens:

- same-level 4 or 8 neighbor edges;
- cross-face seam edges;
- parent-to-child and child-to-parent edges;
- optional long edges to coarse global anchors.

Edge features:

- relative tangent offset;
- geodesic distance;
- area-scale ratio;
- face-boundary flag;
- LOD delta;
- mask/land-sea transition flag.

Layer stack:

```text
12 graph transformer/message-passing blocks
hidden width: 256
attention heads: 8
MLP expansion: 4x
edge-conditioned bias on attention logits
residual + RMSNorm or LayerNorm
drop path: 0.0-0.1
```

Expected parameter share: roughly 20-30M.

The graph processor is the organ that makes arbitrary LOD coherent. It lets a
near-camera child tile know about parent context, adjacent seams, and coarse
basin/planetary signals without forcing the renderer to evaluate the whole
planet at full resolution.

### Forcing Encoder

Use a permutation-invariant source encoder before FiLM/adaptive normalization:

```text
per-source MLP:
  input: type, direction, local basis, intensity, spectrum/thermal class,
         attenuation, time profile, source radius, momentum/mass injection
  width: 128
  layers: 3

source attention:
  4 cross-attention blocks
  query: tile token
  keys/values: forcing source tokens
  heads: 4
  width: 128

pooled forcing embedding:
  concatenate mean/max/attention-pooled source state
  project to graph width 256
```

This handles zero sources, one Earth-like sun, binary suns, nearby eruptions,
orbital mirrors, artificial heat columns, or gas-giant energy bands without
changing the packet schema. Source count is data, not architecture.

### Decoder

Decode selected tile tokens back into requested flow pages:

```text
token-to-feature broadcast at 8x8 or 16x16
FPN merge with local encoder features
upsample blocks to 64x64
2 refinement blocks, width 128
```

The decoder emits the same parameter family at every LOD. Parent pages are not
a separate model. They are lower-resolution outputs of the same flow grammar.

### Heads

Required heads:

```text
mean_velocity:
  2 channels, tangent u/v

diffusivity_cholesky:
  3 channels
  L00 = softplus(a) + epsilon
  L10 = b
  L11 = softplus(c) + epsilon
  Sigma = L * transpose(L)

confidence:
  1 channel, calibrated log uncertainty or quality score
```

Optional heads:

```text
neighbor_kernel:
  8, 16, or 32 logits over a fixed cube-sphere neighbor stencil

divergence_curl:
  2 diagnostic channels, used for loss/debug before runtime authority

refinement_priority:
  1 channel, used only for scheduling; it must not replace validity checks
```

For ocean and atmosphere, prefer shared trunk plus separate small heads. Shared
geometry and state features are useful, but the heads should not force a single
diffusion scale onto fluids with different behavior.

## FlowNet-T: Teacher Model

Purpose: offline global coherence and ensemble uncertainty.

Approximate size: 150-300M parameters.

Candidates:

- spherical Fourier neural operator over global coarse fields;
- graph neural operator over a full coarse cube-sphere mesh;
- diffusion or flow-matching ensemble model for uncertainty.

Do not ship this as the hot runtime path first. Use it to generate improved
coarse priors, ensemble moments, and distillation targets for FlowNet-M.

## Losses

Use area-weighted masked losses. A cube-sphere exists so the poles stop lying;
do not let the loss function smuggle the lie back in.

Primary vector loss:

```text
L_velocity = Huber(predicted_mean - target_velocity)
```

Gaussian negative log likelihood:

```text
L_nll = 0.5 * r^T Sigma^-1 r + 0.5 * logdet(Sigma)
```

LOD consistency:

```text
L_lod = distance(parent_prediction, aggregate(child_predictions))
```

Seam consistency:

```text
L_seam = tangent-frame distance between adjacent cross-face predictions
```

Transition loss when PlasticAdrift or drifter kernels are available:

```text
L_transition = KL(target_transition || predicted_transition)
```

Calibration:

```text
L_calibration = expected calibration error over trajectory residual quantiles
```

Suggested first weights:

```text
velocity: 1.0
nll: 0.25
lod: 0.1
seam: 0.1
transition: 0.2 when available
calibration: 0.05
```

Tune these by validation behavior, not aesthetics.

## Training Curriculum

### Stage 1: Contract Baseline

- dataset: tiny ERA5 subset plus synthetic analytic flows;
- model: FlowNet-S;
- output: mean velocity and diagonal covariance;
- goal: prove cube-sphere bakes, tangent targets, masks, and page export.

### Stage 2: Surface Atmosphere

- dataset: ERA5 near-surface wind and scalar context;
- model: FlowNet-S, then FlowNet-M without transition head;
- split: hold out full years and geographic regions;
- baseline to beat: persistence, seasonal climatology, and source-grid
  interpolation.

### Stage 3: Ocean Surface

- dataset: GLORYS or OSCAR surface current fields;
- model: FlowNet-M shared trunk with ocean head;
- add: sea-surface height, SST, salinity or mixed-layer proxy where available;
- validation: particle rollouts and coastline/gyre residual maps.

### Stage 4: Stochastic Transport

- dataset: PlasticAdrift transition outputs and/or transition matrix where
  license/access are verified;
- add neighbor-kernel head;
- train KL loss against observed transport distributions at selected lead
  times;
- validate multi-step spread, not only one-step vector error.

### Stage 5: Arbitrary LOD

- train mixed-resolution batches;
- randomly drop child tiles and require parent fallback;
- request irregular selected cuts during validation;
- measure particle continuity across LOD boundaries and cube-face seams.

### Stage 6: Distillation

- optional FlowNet-T generates global priors or ensemble moments;
- FlowNet-M learns compact runtime packets;
- export versioned checkpoints, normalization stats, and calibration tables.

## Batch Shape

Initial batch:

```text
batch size: 8-32 tile centers, depending on channels/GPU memory
history: 2-4 previous timesteps
lead times: 1, 3, 6, 24 hours for atmosphere; daily/monthly for ocean variants
tile crop: 64x64 + 32x32 + 16x16 pyramid
neighbor graph: center tile + 8 same-level neighbors + parent + selected siblings
precision: bf16/fp16 mixed precision
optimizer: AdamW
schedule: cosine decay with warmup
```

First-pass hyperparameters:

```text
learning rate: 2e-4 for FlowNet-S, 1e-4 for FlowNet-M
weight decay: 0.01
warmup: 2,000-10,000 steps
training length: prove signal at 50k steps before scaling
gradient clipping: 1.0
EMA weights: yes for evaluation/export
```

## Runtime Export

Runtime should not depend on a Python process in the first production cut.
Export one of:

- baked flow pages for known worlds/times;
- ONNX model plus normalization stats for tile inference;
- hybrid: coarse baked global pages plus local refinement model.

The export bundle must include:

- model architecture id;
- checkpoint hash;
- source dataset manifest hashes;
- normalization statistics;
- cube-sphere projection id and tile levels;
- calibration table;
- output channel layout;
- license/provenance ledger.

## What To Build First

1. FlowNet-S with synthetic and ERA5 wind targets.
2. Cube-sphere page exporter and debug visualizer.
3. FlowNet-M graph processor over selected cuts.
4. Ocean head with OSCAR/GLORYS.
5. PlasticAdrift transition head.
6. Teacher/distillation only after runtime packets have proven useful.

The model architecture is allowed to grow only when a measured failure explains
what capacity is missing. Otherwise it is just parameter inflation with a
conference badge.
