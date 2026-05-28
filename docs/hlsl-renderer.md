# HLSL Renderer

Fensalir's renderer is a D3D12/HLSL runtime that consumes client-declared render
plans and frame state. The renderer owns GPU resources, command recording,
shader compilation/reload, presentation, debug views, and final overlay text.
Clients own the scene meaning and shader modules they declare.

## Frame Path

The current frame path is deliberately small:

1. `D3D12HeightField.hlsl` renders a scalar height target from client-authored
   `AquariumHeightFieldBrush` rows.
2. A client-selected scene shader renders the main HDR scene target. The default
   engine scene shader is `D3D12Scene.hlsl`; demos may provide their own.
3. Client-declared SDF proxy shaders render bounded object passes through shared
   engine includes: `D3D12SdfCommon.hlsli`, `D3D12SdfProxy.hlsli`, and
   `D3D12SdfMath.hlsli`.
4. Optional fractal/field passes consume GPU-resident splat/reservoir buffers.
5. `D3D12Post.hlsl` resolves temporal diagnostics, builds bloom, applies
   exposure/ACES, and presents.
6. `DirectWriteOverlay` draws crisp final-pixel debug UI after scene rendering.

The C# renderer feeds explicit constants for resolution, time, camera,
presentation controls, previous-frame state, cursor anchors, and debug mode.
Vortice stays at the graphics API membrane; clients talk through contracts.

## Render Plan

Clients build an `AquariumRenderPlan` with `AquariumApp`:

- render targets and formats;
- cameras;
- fullscreen, proxy, and feature passes;
- shader paths;
- SDF libraries and per-object proxy shaders;
- debug target views;
- presentation features such as bloom and DirectWrite overlay.

The live renderer still contains some fixed execution paths, but ownership is
already split: clients declare intent, Fensalir owns D3D12 lowering.

## Height Fields

The height-field target is currently a 128x128 `R16Float` scalar field. Client
brushes may use circular or shaped anisotropic envelopes. The target stores
height in `.r`; extra channels are not free and need a pass contract before they
exist.

The height-field lane is used by multiple clients and demos as a cheap scalar
surface, not as a global engine worldview. Future terrain, water, and projected
field work should move through explicit field/page contracts instead of growing
one magic grid.

## SDF Proxies

SDF objects render as bounded proxy draws. The shared proxy vertex shader builds
a conservative screen rectangle from the uploaded object center and bound
radius. The object-specific pixel shader raymarches only that object and writes
travel-derived depth so surfaces and nearer proxies arbitrate visibility through
normal depth testing.

SDF control flow is split by cost:

- `sdfDistance` participates in marching and normals.
- `sdfSurface` runs once at the refined hit.
- Material output uses base color, metallic, roughness, and emission.

Reusable math belongs in engine includes. Client anatomy, symbols, bodies, and
materials belong in client shaders.

## Fractal And Field Passes

Fensalir has a GPU-resident fractal/field lane:

- `.aquageo` and flame fixtures compile into semantic ownership trees and
  backend packets.
- persistent flame state lives in GPU UAV rows so samples advance over frames;
- Form/Appearance/Transport reservoirs keep stable temporal evidence;
- opaque signed-distance splats and transparent density/extinction splats render
  through separate pipeline states over shared packed buffers;
- guide output follows the reservoir guide schema: confidence, sample age, domain validity,
  invalidation code.

The receipt harness lives in `tools/Aquarium.Fractal.Receipt`.

## Temporal Diagnostics

`D3D12Post.hlsl` keeps color, metadata, and control history. Projection jitter
uses a small Halton sequence. History is accepted only when travel, field,
normal, coverage, and control signals stay coherent.

Debug modes include final color, raw current scene, history, history age,
history weight, coverage/step ratio, field identity, bloom contribution,
exposed luminance, proxy identity, proxy step count, and reservoir guide
views. Startup mode can be set with `--render-debug` or
`AQUARIUM_RENDER_DEBUG_MODE`.

## HDR

Presentation is scene-linear until the final display transform. Bloom is a
low-gain pre-tonemap veil pyramid with firefly-safe downsampling; it is not a
threshold glow sticker over a broken exposure model.
