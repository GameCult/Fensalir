# Scratch

## Current Slice

Extend native-domain reservoir replay from single-batch TubeField/SDF proof
paths into measured baseline/native-domain capture.

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

1. Capture the occluded-spectrum-tubes scene in baseline/native-domain modes
   after replay is real, not merely support-overlap filtered.
2. Add exact SDF producer replay only when client SDF distance functions have a
   shared replay include or manifest instead of duplicating shader policy in
   post.
3. Replace the bounded TubeField replay source descriptor table with a replay
   atlas only if real scenes hit the cap.

## Verification

- `dotnet build Fensalir.sln`
- `dotnet test tests/Aquarium.Engine.Tests/Aquarium.Engine.Tests.csproj`
- `dotnet test tests/Aquarium.Engine.Fractal.Tests/Aquarium.Engine.Fractal.Tests.csproj`
- `.\scripts\dev-reload.ps1 -Headless -RetainSlots 4 -ClientProject src\Aquarium.Fensalir\Aquarium.Fensalir.csproj`
