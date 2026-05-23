# Fensalir Instructions

## Purpose

Fensalir is the native C# runtime and renderer dependency for Aquarium-style
clients. It owns the window, D3D12 renderer, input loop, live-reload boundary,
audio host, debug chrome, render contracts, and reusable field/fractal machinery.

Client-specific meaning belongs to client repos. Epiphany Aquarium lives at
`E:\Projects\Aquarium-Engine`.

## Operating Doctrine

- The renderer is the source of truth for visible world pixels.
- Keep the frame legible: explicit passes, explicit ownership, explicit state.
- Do not leave lies in the walls. When building engine systems meant to last,
  refuse hidden compensators, mystery state, and prototype-shaped production
  code.
- Every millisecond counts. Avoid JSON churn, redundant ray work, unbounded
  full-resolution filters, and abstractions that hide performance costs.
- Before inventing renderer, simulation, storage, or post-processing machinery,
  check standard literature, production talks, engine docs, or proven local
  patterns.
- Continuous visual/physical forms should start from continuous field math or a
  coherent parameterization. Piecewise construction needs a measured reason.
- Lighting is diegetic. Ambient convenience light is guilty until proven useful.
- Overlay UI and diegetic UI are different beasts. Overlay text can use
  DirectWrite; world text needs renderer-owned billboards when it belongs in the
  scene.
- Fensalir owns reusable contracts and mechanisms. Client repos own policy,
  narrative semantics, role names, and app-specific state.

## Persistent State

- `state/map.yaml` is the canonical project map.
- `state/memory.json` is durable engine doctrine and taste.
- `state/evidence.jsonl` stores distilled lessons that should change future
  behavior.
- `state/scratch.md` is disposable working context for the active slice.

Update state when engine ownership or durable renderer doctrine changes.

## Verification

```powershell
dotnet build Fensalir.sln
.\scripts\dev-reload.ps1 -Headless -RetainSlots 4 -ClientProject src\Aquarium.Fensalir\Aquarium.Fensalir.csproj
```

For normal iteration:

```powershell
.\scripts\dev-watch.ps1 -ClientProject src\Aquarium.Fensalir\Aquarium.Fensalir.csproj
```

## Style

Prefer small, intentional commits. Push completed work once the real remote
exists. Keep docs about the live system; put old branch scars only in evidence
when they alter future decisions.
