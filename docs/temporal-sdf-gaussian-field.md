# Temporal Field Gaussian

## Objective

Fensalir owns the renderer feature for live volumetric point-cloud/splat input:
clients provide stable world-space Gaussian observations, and the engine turns
them into a buffered, reprojectable, D3D12-rendered field. Mimir can feed
sensor-fusion output into Fensalir as a normal scene contract instead of
smuggling renderer policy through app code.

The historical `SDF` name is now backend baggage. This contract is a temporal
Form-field path: opaque producers may resolve to SDF/level-set surfaces, while
flames, smoke, transparent gradients, and sensor fusion may stay as
density/extinction/confidence fields until evidence justifies a surface.

## Current Mechanism

The client-facing contract is `AquariumSceneState.TemporalGaussianField`.
Each `AquariumTemporalSdfGaussian` carries stable identity, current and previous
center, velocity, oriented radii, color/opacity, confidence, history weight,
compact-kernel controls, and field id.

`Aquarium.Engine.Fractal.Temporal.TemporalGaussianAccumulator` consumes
`TemporalGaussianObservation` rows and lowers them through the shared
`TemporalSpatialEvidenceReservoir`. The reservoir owns the live buffering
policy:

- stable keys identify tracks across camera/sensor frames
- presentation delay lets late sensor data converge before rendering
- accumulation window expires old tracks
- max-track budgets evict low-confidence stale tracks before renderer lowering
- smoothed velocity predicts the presented center
- history weight is confidence scaled by age inside the window

The Gaussian field is one backend for this reservoir, not the reservoir's
identity. Fractal form probes, visual point-cloud features, transparent density
samples, and future acoustic constraints can all enter as stable keyed spatial
evidence as long as each producer declares confidence, time, bounds, field
layer, field encoding, and payload lowering rules.
`TemporalSpatialEvidenceLowering` owns the current packet conversions for
`TemporalGaussianObservation`, `AquariumTemporalSdfGaussian`, and
`AquariumGpuFusionSeed`; consumers should use those helpers instead of packing
payload vectors privately.

The D3D12 renderer lowers the field into `D3D12TemporalGaussianPacket`, uploads
the active packet span into a 1,048,576-entry structured buffer, and renders an
instanced proxy-quad pass from
`D3D12TemporalGaussian.hlsl`. The pixel shader evaluates a compact anisotropic
Gaussian kernel in world space, writes HDR scene color/travel, field metadata,
normal, and temporal-control coverage so the existing resolve sees the field as
diegetic scene content.

The production ownership line is `AquariumGpuSensorFrame`: camera and Leap
inputs arrive as calibrated sensor records plus shared GPU texture handles, the
D3D12 backend owns their metadata buffers, and fusion kernels lower those
GPU-resident inputs into the temporal Gaussian buffer consumed by the SDF
Gaussian draw. `AquariumGpuFusionField` remains a generic fallback/debug
contract for already-derived point claims.

## Invariants

- Field accumulation happens in world space before pixel history. Reservoir
  resolve reconstructs presentation; no separate TAA pass owns sensor identity
  or temporal smoothing.
- Stable keys belong to the producer/accumulator boundary; shader packets are
  backend output and do not invent identity.
- The compact support kernel has a finite bound. Renderer cost must scale from
  declared bounds, not from infinite translucent fog.
- Density/opacity observations are not required to become surfaces. A
  transparent medium may remain a participating Form field with Appearance and
  Transport payloads instead of being collapsed into a fake SDF.
- Client code may construct observations or a field for diagnostics, but
  Fensalir owns live sensor texture import, packet layout, root binding, shader
  evaluation, fusion, and temporal-control metadata.
- JSON is not a renderer boundary. CultCache/CultNet producers should lower into
  typed contract rows before Aquarium sees the data.

## Cut Line

This cut deliberately claims million-slot ingestion, not a finished million-splat
renderer architecture. The live D3D12 path can draw up to 1,048,576 temporal
Gaussians through instanced proxy quads, only uploads the active seed/packet
span, and now owns the GPU lowering step. Mimir may still produce calibration
artifacts and reference captures, but it must not own Fensalir renderer policy.
The next scaling cut belongs to the Fensalir renderer: shared texture import,
packed camera planes, Leap packed-map channel extraction, selected-cut
residency, tiled/bin dispatch, GPU accumulation, and clustered visibility.

## GPU Fusion Spine

The active GPU boundary is deliberately narrow:

```text
Mimir calibration/device metadata or FieldEvidence camera Feature claims
-> AquariumGpuSensorFrame or planned GpuSensorFusion packets
-> D3D12 sensor metadata buffers + imported/leased texture SRVs
-> D3D12 GPU sensor fusion compute shader
-> RWStructuredBuffer<TemporalGaussian>
-> instanced SDF Gaussian draw
-> reservoir resolve
```

Ownership:

- `AquariumGpuSensorFrame` is the live renderer contract for GPU-owned fusion
  input when a client publishes the legacy direct sensor frame.
- FieldEvidence camera-sensor `Feature` claims with `SensorObservation`
  proposal policy are the current Mimir sensor-fusion contract. They plan to
  the `GpuSensorFusion` backend, and `D3D12Renderer` derives sensor metadata
  plus SRV bindings from resolved Texture2D resources.
- `AquariumGpuFusionField` remains a temporary fallback/debug contract for
  already-derived point claims.
- Mimir-owned adapters may convert app-specific point claims into compact seeds
  for that fallback path.
- `D3D12GpuSensorFusion.hlsl` owns the first compute lowering pass.
- `D3D12Renderer` owns GPU sensor camera metadata storage, UAV-capable temporal
  Gaussian storage, dispatch, and the transition back to shader-resource state
  for the draw.
- `AquariumAcousticFieldFrame` carries the ultrasonic chirplet timing oracle and
  room/position constraints. Visual evidence may arrive late and be refined, but
  acoustic timing is the clock witness for aligning the delayed broadcast world.
- `AquariumCalibrationEventFrame` carries deliberate calibration actions such
  as claps. A clap gives the system one event that every camera can see and the
  audio timing oracle can timestamp to microsecond-scale uncertainty, making it
  the cheap, brutal alignment hammer for camera clock offsets and pose drift.

Current Aquarium cut: the contract, D3D12 camera metadata buffer, external
texture importer, and sensor SRV table exist. A producer may provide either a
duplicated shared handle or a named shared handle for each camera/Leap plane.
When sensor textures are present, Aquarium can dispatch fusion without fallback
seeds and write RGB-derived Gaussian samples into the temporal buffer. Those
samples are no longer isolated per-camera flecks: the shader computes a compact
per-sample visual descriptor, compares it against a neighboring camera stream,
raises confidence when stochastic samples appear to correspond, and shrinks the
kernel toward the matched surface. Acoustic constraints from the ultrasonic
chirplet loop bias confidence and velocity near measured room/position returns.

The current FieldEvidence cut also consumes Mimir camera texture leases without
restoring the retired direct sensor DTO path. LeapStereoIr and Bayer8/R8-style
textures are sampled directly by the compute shader; YUY2 camera textures are
selected as `GpuSensorFusion` evidence but skipped by the renderer until a
format-aware YUY2 conversion/feature lane exists. Packed video formats must not
pretend to be RGBA.

Next cut: replace the first-pass descriptor comparison with calibrated
epipolar/flow search, Leap packed-map channel extraction, and a persistent GPU
correspondence/refinement buffer that can update camera pose and surface tracks
over the several-second broadcast delay window. Deliberate clap events should
feed that buffer as high-confidence timing correspondences: visual impact frame
per camera against acoustic oracle time, then pose/clock correction under the
same delayed broadcast horizon.

The first shader pass also uses camera-facing proxy planes to evaluate each
kernel. True ray-integrated volume compositing, Gaussian depth sorting, and
per-Gaussian previous-center reprojection remain the next renderer cuts.
