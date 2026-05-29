# Fractal Domain Reservoir

## Objective

Fensalir's Perfect Machine reservoir is not a screen-space texel cache. It is a
dynamic, hierarchical, domain-native evidence reservoir that can be projected
into pixels, history rows, field packets, denoiser inputs, residency decisions,
and debug views.

The renderer may resolve through a per-pixel UAV row layout, but that row is an
execution surface. It is not the sampling foundation.

## Reorientation

TAA proves that a renderer can accumulate more than one pixel worth of
information over time by sampling different subpixel locations each frame.
ReSTIR generalizes that idea for path and light samples by storing a selected
representative sample plus the probability mass it represents. TSR generalizes
the reconstruction side by deciding when history is trustworthy, when it must
be rejected, and how rejected regions should be reconstructed without exposing
raw noise.

Area ReSTIR exposes the missing invariant for Fensalir: when samples live over a
pixel footprint, lens aperture, filter footprint, tube support, SDF envelope,
quadtree tile, sensor cone, or nested fractal domain, the reservoir must store
the selected sample's actual domain coordinates and support. Reuse is legal
only when a source sample can be shifted or re-evaluated in the target domain
with valid support, target/source PDF, visibility, and MIS/Jacobian accounting
where required.

Fensalir goes one step higher: its selected samples are not merely subpixel
rays. They are field and radiance claims lowered from semantic domains into a
dynamic cache, then into GPU-visible reservoirs under frame budget.

```text
DSL / producer intent
-> semantic domain claims
-> ownership tree and conservative summaries
-> stochastic contribution cache / residency scheduler
-> bounded candidate proposals
-> fractal domain reservoir
-> temporal domain validation and shift
-> spatial/domain validation and shift
-> TSR-style rejection and reconstruction
-> denoiser-integrated resolved field/radiance output
```

## Invariant

A reservoir sample must answer these questions before it can persist:

```text
Which domain owns the sample?
Where inside that domain was it sampled?
What support/filter footprint does it represent?
What target says it matters?
What source PDF or proposal policy produced it?
What candidate count or subtree mass does it represent?
What shift mappings can legally reuse it?
What conservative bounds keep Form, Appearance, or Transport honest?
What denoiser/reconstruction features describe its confidence and variance?
```

If a sample only answers "which pixel wrote me," it is not a Perfect Machine
reservoir sample. It is a screen-space cache entry.

## Architecture Consequences

- Screen resolution is a consumer budget, not the native sampling domain.
- Pixel rows are a row-stage projection of selected domain evidence, not a
  private top-k visibility list and not the owner of field identity.
- TubeField, SDF, mesh, volume, fractal probe, and sensor contributors must all
  publish bounded domain proposals before reuse.
- A broad proxy raster footprint may discover candidates, but it does not define
  selected support. The selected domain sample must carry its own subpixel and
  producer-domain coordinates.
- Temporal reuse must reason over overlapping source and target domains, not
  nearest previous texel ownership.
- Spatial reuse must shift or re-evaluate a neighbor's selected sample in the
  target domain before merge.
- The occupancy/contribution tree is not separate from the reservoir. It is the
  hierarchy that decides which domain nodes deserve probes, residency, child
  expansion, or parent-summary fallback under the current compute budget.
- The denoiser is not downstream cosmetics. It consumes reservoir confidence,
  selected support, contribution weight, variance/age, rejection reason, and
  reconstruction features, and the sampling scheduler should respond to those
  signals when choosing future probes.

## Compute-Budget Contract

Every stage must accept a budget without changing the truth model:

```text
high budget:
  more node probes, more local candidates, richer shifts, deeper tree cut,
  better denoiser features

low budget:
  fewer probes, parent summaries, older but validated reservoirs, coarser
  domains, stronger reconstruction
```

The frame target changes sample count, probe depth, candidate count, and
reconstruction aggressiveness. It must not change who owns visibility,
identity, support, or contribution mass.

## Immediate Cut Line

The live seven-lane GPU reservoir row can remain as an execution ABI only if it
gains a first-class domain/sample-coordinate contract, either by adding a lane
or by repacking an existing one deliberately. Without selected domain position
and support, the row-stage rebuild is still a pixel reservoir wearing field
language.

Do not tune occlusion thresholds as the next answer to noisy TubeField leakage.
First make the selected sample's domain coordinates, support, and legal shift
mapping visible in the reservoir state and debug views.

## Reference Anchors

- Area ReSTIR: https://graphics.cs.utah.edu/research/projects/area-restir/
- Area ReSTIR code: https://github.com/guiqi134/Area-ReSTIR
- ReSTIR DI: https://research.nvidia.com/labs/rtr/publication/bitterli2020spatiotemporal/
- GRIS: https://research.nvidia.com/labs/rtr/publication/lin2022generalized/
- Unreal TSR docs: https://dev.epicgames.com/documentation/unreal-engine/temporal-super-resolution-in-unreal-engine
- Perfect Machine article: E:\Projects\gamecult-site\GameCult\Blog\perfect-machine-fractal-reservoir-architecture.md
