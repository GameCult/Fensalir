# Fensalir

<p align="center">
  <img src="Fensalir-Icon.png" alt="Fensalir icon" width="160" />
</p>

Fensalir is a native C# runtime for interactive visual clients. It opens the
window, owns the D3D12 renderer, loads client runtimes behind contracts, keeps
hot reload alive, mixes audio, draws debug UI, and carries the reusable
field/fractal machinery used by Aquarium-style projects.

The repo is not the Epiphany Aquarium client. Epiphany now lives in
`GameCult/Aquarium-Engine` and consumes this repo as its runtime dependency.

## What Runs

Fensalir runs a host executable from `src/Aquarium.Engine`. The host loads a
client assembly that implements `IAquariumRuntime`, then drives this loop:

```text
Win32 input + window
  -> client runtime update
  -> AquariumFrame + AquariumSceneState
  -> D3D12 renderer passes
  -> DirectWrite debug overlay
  -> WASAPI audio output
```

Clients describe render targets, cameras, shaders, SDF proxy objects, UI panels,
audio buffers, and frame state through `Aquarium.Engine.Contracts`. The engine
translates that data into D3D12 resources, pipeline state, command lists,
presentation, hot reload, and diagnostics.

## Projects

- `src/Aquarium.Engine.Contracts`: public client/runtime contracts.
- `src/Aquarium.Engine`: Win32 host, D3D12 renderer, reload transport, debug UI,
  audio host, bundled assets, and renderer-owned shaders.
- `src/Aquarium.Engine.Fractal`: `.aquageo`, fractal/IFS, reservoir, temporal
  field, and lowering machinery.
- `src/Aquarium.Fensalir`: splash/art reconstruction demo using a Fensalir
  scene patch and custom shaders.
- `src/Aquarium.Sample.Minimal`: tiny client proving the runtime boundary.
- `src/Aquarium.Zyphos`: planetary/fractal terrain demo.
- `src/Aquarium.LocalCast`: LocalCast/Mimir-facing bridge experiments.
- `tools/Aquarium.Fractal.Receipt`: CPU/GPU fractal receipt harness.

The assemblies still use `Aquarium.*` names for API continuity. That naming is
legacy surface, not current repo ownership.

## Build

Requirements:

- Windows
- .NET SDK matching `global.json`
- sibling repos currently expected at `E:\Projects\CultMath` and
  `E:\Projects\CultLib`

```powershell
dotnet build Fensalir.sln
```

## Run

Run the Fensalir splash demo:

```powershell
.\scripts\dev-reload.ps1 -ClientProject src\Aquarium.Fensalir\Aquarium.Fensalir.csproj
```

Run headless:

```powershell
.\scripts\dev-reload.ps1 -Headless -RetainSlots 4 -ClientProject src\Aquarium.Fensalir\Aquarium.Fensalir.csproj
```

Watch and reload on source changes:

```powershell
.\scripts\dev-watch.ps1 -ClientProject src\Aquarium.Fensalir\Aquarium.Fensalir.csproj
```

Run another client by changing `-ClientProject`.

## Research And State

- `docs/`: live engine architecture and subsystem notes.
- `research/`: rendering/fractal/D3D12 research that informs implementation.
- `state/`: persistent project memory for future agents.

Start with:

- `docs/engine-client-boundary.md`
- `docs/hlsl-renderer.md`
- `docs/perfect-machine-architecture.md`
- `docs/temporal-spatial-evidence-reservoir.md`
- `research/README.md`
- `state/README.md`

## Boundary

Fensalir owns reusable runtime and renderer authority. Client repos own meaning.
If a feature requires app-specific policy, role names, story state, or client
layout semantics, it belongs outside the engine unless it has been reduced to a
reusable data-only contract.
