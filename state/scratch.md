# Scratch

## Current Slice

Reorient the reservoir rebuild around the fractal domain reservoir, not
screen-space texel rows.

## Hot Lesson

Area ReSTIR confirms that reuse over an area domain is invalid unless the
reservoir stores the selected sample's actual domain coordinates and support.
TSR confirms that temporal reconstruction must reject, reconstruct, and expose
history trust at the layer that owns visible output. Fensalir needs both, but
one level higher: selected samples are field and radiance claims lowered from
semantic domains and a dynamic contribution tree into bounded GPU reservoirs.

The current seven-lane row ABI can remain only as an execution projection if it
gains first-class selected domain/sample-coordinate authority. A row keyed only
by pixel, travel, field id, guide, proposal, and stats is still too texel-like.

## Next Bounded Move

Before tuning occlusion thresholds, add or repack reservoir state so TubeField
and SDF proposals carry selected subpixel/domain coordinate plus support. Then
make temporal/spatial debug views show support overlap, shift legality, and
selected domain sample position.

## Verification

- `dotnet build Fensalir.sln`
- `.\scripts\dev-reload.ps1 -Headless -RetainSlots 4 -ClientProject src\Aquarium.Fensalir\Aquarium.Fensalir.csproj`
