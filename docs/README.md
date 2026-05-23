# Fensalir Docs

Start here if you are trying to understand the engine rather than a specific
historical branch scar.

## Core Runtime

- `engine-client-boundary.md`: what Fensalir owns versus what client repos own.
- `hlsl-renderer.md`: current D3D12/HLSL frame path.
- `vortice-spine.md`: native host and renderer spine.
- `cult-runtime-surface.md`: cache-path and typed-state boundary.
- `input.md`: input and camera contracts.

## Fields, Reservoirs, And Fractals

- `perfect-machine-architecture.md`: end-to-end map for authored/sensor field
  input, domain binding, evidence candidates, reservoir reuse, residency, and
  renderer lowering.
- `temporal-spatial-evidence-reservoir.md`: stable-key temporal evidence cache
  for fractal rendering and Mimir/LocalCast-style sensor fusion.
- `temporal-sdf-gaussian-field.md`: temporal Form-field evidence lane.
- `tsr-inspired-taa-spec.md`: temporal resolve design and guide-buffer intent.
- `stochastic-transparent-surface-pipeline.md`: density/extinction and
  transparent field rendering notes.

## Migration And Demo Context

- `d3d12-best-practices-audit.md` and `d3d12-migration.md`: D3D12 migration and
  audit notes.
- `zyphos-eusocial-sync.md`: Zyphos demo worldbuilding boundary.

Fractal DSL and rendering research starts in
`../research/rendering/fractal-brush-architecture-plan.md`. Persistent agent
state lives under `../state/`.
