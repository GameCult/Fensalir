# Scratch

## Current Slice

Implement the first Perfect Fensalir Machine roadmap packet: add native-domain
sample coordinate/support authority to the shared field reservoir.

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

Follow `docs/perfect-fensalir-machine-roadmap.md` Phase 1 and the immediate
work packet:

1. Add or repack `FieldReservoirSample` with selected domain/sample coordinate
   and support footprint.
2. Write TubeField row-0 selected subpixel/source/curve support.
3. Write SDF row-0 selected hit/domain support.
4. Add debug modes for selected coordinate, support overlap, and shift kind.
5. Implement temporal support-overlap validation before further threshold
   tuning.

## Verification

- `dotnet build Fensalir.sln`
- `.\scripts\dev-reload.ps1 -Headless -RetainSlots 4 -ClientProject src\Aquarium.Fensalir\Aquarium.Fensalir.csproj`
