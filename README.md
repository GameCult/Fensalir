# Fensalir

<p align="center">
  <img src="Fensalir-Icon.png" alt="Fensalir icon" width="160" />
</p>

Fensalir is the native C# runtime and field engine under GameCult's interactive
visual clients. It opens the window, owns the D3D12 frame, loads client runtimes
behind contracts, keeps hot reload alive, mixes PCM audio, draws debug UI, and
houses the reusable machinery for volumetric fields, fractal geometry, temporal
evidence, and synth-backed sound.

The repo is not the Epiphany Aquarium client. Epiphany lives in
`GameCult/Aquarium-Engine` and consumes Fensalir as the runtime dependency.

## What Runs

The host executable in `src/Aquarium.Engine` loads a client assembly that
implements `IAquariumRuntime`, then drives this loop:

```text
Win32 input + window
  -> client runtime update
  -> AquariumFrame + AquariumSceneState
  -> D3D12 renderer passes
  -> DirectWrite debug overlay
  -> AquaSynth/WASAPI audio output
```

Clients declare render targets, cameras, shaders, SDF proxy objects, UI panels,
audio buffers, synth patches, and frame state through
`Aquarium.Engine.Contracts`. Fensalir lowers those declarations into native
resources: D3D12 pipelines and command lists, shader reloads, presentation,
audio playback, debug telemetry, and headless capture.

## Spatial Evidence Machine

Fensalir's field work is not just "some fractal helpers." It is a
spatio-temporal reservoir system for deciding which field evidence deserves
rendering, fusion, and residency under a budget.

The working model is:

```text
authored geometry, live sensors, IFS probes, simulation claims, direct meshes
  -> domain binding
  -> evidence candidates
  -> target evaluation
  -> resampled importance reservoirs
  -> temporal/spatial/domain reuse
  -> stable evidence tracks
  -> backend packets
  -> renderer, fusion, and TAA guide buffers
```

`ResampledImportanceReservoir<TSample>` owns the selected candidate, weight sum,
candidate count, target value, and contribution weight. The track layer
(`TemporalSpatialEvidenceReservoir`) adds stable keys, confidence smoothing,
delay, velocity, expiry, and eviction so a field sample can persist long enough
to become useful state instead of one-frame noise.

Every sample declares both layer and encoding. The current layer split is:

- `Form`: shape evidence such as SDF, height, displacement, and occupancy.
- `Appearance`: material, albedo, emission, feature, and surface claims.
- `Transport`: density, extinction, phase, radiance, confidence, fog, smoke,
  plasma, and other volumetric participation claims.

The same reservoir spine serves fractal rendering and Mimir-style sensor
fusion. The renderer-facing TAA guide uses confidence, sample age, domain
validity, and invalidation codes so temporal reuse can be explained instead of
merely hoped for. CPU reservoirs, fractal probe samples, track state, and
validator coverage exist now; full GPU reservoir storage and page residency are
the next steel that still needs bolting down.

The reservoir spine is not a vow of poverty against geometry. Adaptive IFS
probing and sensor fusion are producer families, not the only lawful entrance
into the frame. Direct SDF proxy passes, texture-driven tube fields, volumetric
envelopes, and traditional mesh geometry may all contribute when they emit the
shared scene packet: color/travel, metadata, control, depth where applicable,
and reservoir-guide evidence for temporal validation. Meshes are not heresy;
unaccountable geometry is.

## Fractal And Volumetric Field DSL

`src/Aquarium.Engine.Fractal` is the home of the fractal field language. The
intent is a semantic volumetric DSL, not a decorative shader toy.

The path is:

```text
client intent, world seed, agent state
  -> .aquageo domain roots
  -> semantic IFS brush grammar
  -> ownership tree
  -> node summaries
  -> contribution cache and residency hints
  -> backend packets
  -> height, SDF, density, extinction, material, and debug passes
```

`.aquageo` documents describe domains and claims. IFS brushes contribute
coherent field evidence rather than isolated meshes: height ripples, signed
distance surfaces, density/extinction volumes, projected fields, and material or
feature channels can all be expressed as claims attached to a domain. Node
summaries and contribution caches let Fensalir choose what to evaluate, reuse,
or evict without pretending the whole fractal world fits in one eager pass.

The Fensalir splash client and Zyphos terrain work are demo clients for this
machinery. They exist to force the field DSL to survive real rendering pressure:
fractal art reconstruction, planetary domains, field lowering, and debug
evidence all share the same contract surface.

## AquaSynth Integration

Fensalir also owns the audio lane. `AquariumSynthHost` consumes client
`AquariumAudioDocument` PCM buffers and `AquariumSynthDocument` patch state,
compiles Faust DSP through AquaSynth, and sends rendered buffers to WASAPI.

The synth path currently includes:

- per-patch desired/ready/failed compile keys;
- debounced async Faust compilation from the bundled `Synth` source directory;
- cached rendered sound buffers by patch and gain;
- trigger revision and interval playback;
- master gain, patch gain, compile status documents, and debug synth support.

Clients say what sound should exist. Fensalir handles patch compilation,
buffering, playback, and failure reporting without requiring the client to know
how WASAPI or Faust got invited to the party.

## Projects

- `src/Aquarium.Engine.Contracts`: public client/runtime contracts.
- `src/Aquarium.Engine`: Win32 host, D3D12 renderer, reload transport, debug UI,
  audio/synth host, bundled assets, and renderer-owned shaders.
- `src/Aquarium.Engine.Fractal`: `.aquageo`, IFS/fractal fields, temporal
  reservoirs, evidence tracks, contribution cache, and lowering machinery.
- `src/Saga`: stochastic planetary flow middleware for cube-sphere Transport
  fields, arbitrary forcing, Coriolis-aware advection, and learned atmosphere /
  ocean / debris flow packets.
- `src/Aquarium.Fensalir`: Fensalir splash/art reconstruction demo.
- `src/Aquarium.Sample.Minimal`: tiny client proving the runtime boundary.
- `src/Aquarium.Zyphos`: planetary/fractal terrain demo.
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
- `research/rendering/fractal-brush-architecture-plan.md`
- `research/README.md`
- `state/README.md`

## Boundary

Fensalir owns reusable runtime, renderer, field, reservoir, and synth authority.
Client repos own meaning. If a feature requires app-specific policy, role
names, story state, or client layout semantics, it belongs outside the engine
unless it has been reduced to a reusable data-only contract.
