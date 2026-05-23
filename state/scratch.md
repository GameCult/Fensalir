# Scratch

## Current Slice

Fensalir has been split into its own adjacent repo.

- This repo owns the native runtime, renderer, contracts, fractal machinery,
  host scripts, and engine demo clients.
- `E:\Projects\Aquarium-Engine` now owns only the Epiphany Aquarium client.
- There is no remote configured yet; set it to the future `GameCult/Fensalir`
  remote before pushing.

## Verification

- `dotnet build Fensalir.sln`
- `.\scripts\dev-reload.ps1 -Headless -RetainSlots 4 -ClientProject src\Aquarium.Fensalir\Aquarium.Fensalir.csproj`
