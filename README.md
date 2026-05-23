# Fensalir

<p align="center">
  <img src="Fensalir-Icon.png" alt="Fensalir icon" width="160" />
</p>

Fensalir is the native C# runtime for Aquarium-style clients: Win32 hosting,
D3D12 rendering, input, hot reload, audio output, debug UI, render contracts,
fractal/field machinery, and engine demo clients.

Epiphany Aquarium is no longer inside this repo. It lives next door at
`E:\Projects\Aquarium-Engine` and consumes Fensalir as a dependency.

## Repository Shape

- `src/Aquarium.Engine.Contracts`: client-facing runtime, render, UI, audio,
  input, and frame contracts.
- `src/Aquarium.Engine`: Win32 host, D3D12 backend, reload transport, debug UI,
  audio host, bundled engine assets, and renderer-owned shaders.
- `src/Aquarium.Engine.Fractal`: reusable fractal, field, reservoir, and DSL
  machinery.
- `src/Aquarium.Fensalir`: Fensalir splash/art reconstruction demo client.
- `src/Aquarium.Sample.Minimal`: minimal client boundary proof.
- `src/Aquarium.Zyphos`: planetary-scale engine demo client.
- `src/Aquarium.LocalCast`: LocalCast/Mimir-facing runtime bridge experiments.
- `tests`: engine, fractal, LocalCast, and demo coverage.
- `tools`: engine/fractal receipt tools.

The assemblies still use `Aquarium.*` names for API continuity. Rename the
public namespace deliberately later if the package boundary earns that churn.

## Build

```powershell
dotnet build Fensalir.sln
```

## Run A Client

```powershell
.\scripts\dev-reload.ps1 -ClientProject src\Aquarium.Fensalir\Aquarium.Fensalir.csproj
```

Other demo clients can be launched by changing `-ClientProject`.

## Boundary

Fensalir owns reusable host and renderer authority. Client repos own their
semantics. Do not add Epiphany, Mimir, or other client policy nouns here unless
they are demo/client projects inside this repo. Shared capability belongs in
contracts or reusable engine modules; client interpretation belongs outside.
