# Scratch

## Current Slice

Turn the native-domain reservoir from a native-resolution cache into a
budgeted spatiotemporal sampler. The work grid now defaults to half present
resolution and is clamped below native presentation scale; scene evidence,
reservoir candidate/history, resolve, bloom, and match-window graph targets use
that grid. The final present buffer is the only full-resolution surface.

The first comparison set is
`artifacts/fensalir-captures/reservoir-compare-20260529-231904-*`: native versus
texel-baseline changed 10.8208% of final-frame pixels, 0.0165% of
rejection-debug pixels, and 0.2976% of shift-debug pixels. The repeatable
measurement script is `scripts/measure-reservoir-mode-comparison.ps1`.
First half-scale live capture:
`artifacts/fensalir-captures/budgeted-reservoir-20260529-234039-final.png`
at 1264x681 present output, nonblank by sampled pixel probe.

Temporal sequence harness smoke:
`scripts/capture-reservoir-mode-sequence.ps1` and
`scripts/measure-reservoir-sequence.ps1` produced
`artifacts/fensalir-captures/reservoir-sequence-20260529-234414-*` at 640x360,
final color only, ready frames 2 and 4. Native/baseline mode-delta changed
82.9025% of sampled pixels at f2 and 20.0206% at f4; temporal-native changed
78.5581%, temporal-baseline changed 48.9741%. These are probes, not a final
ghosting score.

## Hot Lesson

Area ReSTIR confirms that reuse over an area domain is invalid unless the
reservoir stores the selected sample's actual domain coordinates and support.
TSR confirms that temporal reconstruction must reject, reconstruct, and expose
history trust at the layer that owns visible output. Fensalir needs both, but
one level higher: selected samples are field and radiance claims lowered from
semantic domains and a dynamic contribution tree into bounded GPU reservoirs.

The current nine-lane row ABI now carries first-class selected
domain/sample-coordinate authority. A row keyed only by pixel, travel, field id,
guide, proposal, and stats is no longer accepted as valid reservoir evidence.
That authority now lives on a budgeted work grid, not on the final present grid:
native-domain state describes selected field samples, while final resolution is
reconstructed from bounded evidence instead of allocating native-res
reservoirs.
TubeField row samples store selected UV, logical column, curve coordinate,
support footprint, domain kind, and shift kind; field id carries physical
rolling-column identity. The old single-batch replay binding has been replaced
by a bounded producer-keyed manifest plus raw source descriptor table, so the
shader picks the replay source per selected TubeField sample instead of
accepting/rejecting the whole frame by batch count.
SDF history reconstructs the previous selected world hit from stored UV/travel,
carries it through object center motion, and validates current ray/travel/support
before temporal reuse can merge it. TubeField batches beyond the manifest source
cap remain intentionally non-authoritative.

## Next Bounded Move

Follow `docs/perfect-fensalir-machine-roadmap.md` Phase 1:

1. Turn the temporal sequence probe into a real leakage/ghosting score using
   disocclusion/rejection masks or a high-budget reference.
2. Add exact SDF producer replay only when client SDF distance functions have a
   shared replay include or manifest instead of duplicating shader policy in
   post.
3. Add adaptive update/sample budget allocation only after the leakage harness
   can tell whether extra samples buy visible stability.
4. Replace the bounded TubeField replay source descriptor table with a replay
   atlas only if real scenes hit the cap.

## Verification

- `dotnet build Fensalir.sln`
- `dotnet test tests/Aquarium.Engine.Tests/Aquarium.Engine.Tests.csproj`
- `dotnet test tests/Aquarium.Engine.Fractal.Tests/Aquarium.Engine.Fractal.Tests.csproj`
- `.\scripts\dev-reload.ps1 -Headless -RetainSlots 4 -ClientProject src\Aquarium.Fensalir\Aquarium.Fensalir.csproj`
