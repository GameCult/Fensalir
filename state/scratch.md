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

Masked sequence smoke:
`artifacts/fensalir-captures/reservoir-sequence-20260529-235031-*` at 320x180,
ready frames 2 and 3, final plus rejection debug. The sequence capture is now
resumable: existing PNGs are skipped and still written into the manifest.
Rejection-mask rows covered about 41% of frame pixels. Masked native/baseline
mode-delta changed 87.4393% at f2 and 94.7517% at f3; masked temporal-native
changed 83.2208%, masked temporal-baseline changed 83.4813%. This proves the
mask path is live but still needs a high-budget reference or explicit
disocclusion mask before it can claim ghosting quality.

Disocclusion owner signal:
Debug mode 19, `Reservoir Disocclusion`, is derived from temporal rejection
codes in `D3D12Post.hlsl`: previous UV outside history, previous history closer
than expected travel, and support-overlap loss. The sequence measurer prefers
`disocclusion` masks over broad `rejection` masks. Native-only smoke
`artifacts/fensalir-captures/reservoir-sequence-20260530-000151-*` at 320x180,
ready frames 2 and 3, final plus disocclusion debug, produced a mask covering
3.1098% of the frame and a masked temporal-native delta of 46.8867%.

Higher-work-grid reference:
`scripts/capture-reservoir-mode-sequence.ps1` accepts
`-ReferenceFieldReservoirScale`, captures native-domain reference frames at that
larger scale, and labels them `reference` in the manifest.
`scripts/measure-reservoir-sequence.ps1` reports `reference-error-*` rows
against matching ready-frame counts, using disocclusion masks when present.
Smoke `artifacts/fensalir-captures/reservoir-sequence-20260530-001046-*`
compared native scale 0.5 against reference scale 0.75 at 320x180, ready frame
2, final plus disocclusion. Whole-frame reference-error-native changed 53.9240%;
masked reference-error-native covered 3.0468% of frame pixels and changed
42.0368%.

Budget-pressure probe:
`scripts/measure-reservoir-budget-pressure.ps1` reads a sequence manifest with a
same-time `reference` capture and ranks fixed tiles by masked candidate versus
reference final-color error. It uses the candidate/reference disocclusion masks
when available, falling back to rejection masks. Smoke
`artifacts/fensalir-captures/reservoir-budget-pressure-20260530-001046.*`
measured 1306 of 42864 pixels and found the top 5 of 50 tiles carried 99.3305%
of measured reference-error delta. This is offline allocator evidence, not a
runtime scheduler.

Runtime spatial reuse budget:
`GraphicsSettings.FieldReservoirSpatialReuseBudget` now flows through renderer
UI, runtime options, CLI/env overrides, headless capture scripts, frame
constants, and `D3D12ReservoirHistoryUpdateCS`. Current and temporal reservoir
rows are still written for every work-grid pixel; the budget only decides
whether that pixel spends the 3x3 spatial neighbor sampling pass this frame.
Default is 0.5. Smoke `artifacts/fensalir-captures/reservoir-sequence-20260530-003006-*`
used native scale 0.5 / spatial budget 0.5 against reference scale 0.75 /
spatial budget 1.0. Masked reference-error-native changed 63.7195% over 3.8261%
of frame pixels, and `reservoir-budget-pressure-20260530-003006.*` found the
top 5 of 50 tiles carried 90.8321% of measured reference-error delta.

Spatial budget debug:
Debug mode 20, `Reservoir Spatial Budget`, visualizes the sampler branch that
spends 3x3 spatial reuse. Green pixels spend spatial reuse; blue pixels skip it
for the current frame. Smoke
`artifacts/fensalir-captures/reservoir-sequence-20260530-003746-*` captured the
mode at scale 0.5 / spatial budget 0.5. A bitmap probe counted 32078 green,
6818 blue, and 3968 other/filtering pixels in the 304x141 present output.

Window resize fix:
The Fensalir window resize path now explicitly clears and flushes the D3D11-on-12
overlay context after disposing DirectWrite overlays and wrapped backbuffer
resources, before `IDXGISwapChain::ResizeBuffers`. Resize also disposes
program-output shared textures before recreating size-dependent output surfaces.
Probe `scripts/dev-reload.ps1` visible slot `20260530-215113-639e6612` survived
seven programmatic resizes and logged rebuilt D3D12 targets at each size with no
stderr.

Reservoir loss curve:
`scripts/measure-reservoir-loss-curve.ps1` now reports RGB reconstruction loss
against same-time higher-work-grid native references. Metrics include MAE, MSE,
RMSE, PSNR, changed pixels, max channel delta, and optional masked loss when
disocclusion/rejection masks are present. Smoke
`artifacts/fensalir-captures/reservoir-sequence-20260530-221043-*` compared
native scale 0.5 / spatial budget 0.5 against reference scale 0.75 / spatial
budget 1.0 at ready frames 1, 2, 4, and 8. Whole-frame PSNR was 21.668, 20.981,
20.911, and 21.690 dB respectively; RMSE was 21.043, 22.777, 22.962, and 20.992.
This is a higher-budget online reference, not offline ground truth.

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
Swapchain resize correctness depends on releasing both D3D12 backbuffer owners
and D3D11-on-12 overlay references before `ResizeBuffers`. Disposing wrapped
resources is not enough; the D3D11 immediate context must be cleared and flushed
so it stops holding hidden references.
Reservoir quality must be golfed against loss-over-time, not single-frame smoke
deltas. The current curve is noisy and not monotonically improving, which means
the candidate/reference comparison is exposing temporal instability rather than
a settled convergence story.

## Next Bounded Move

Follow `docs/perfect-fensalir-machine-roadmap.md` Phase 1:

1. Convert the fixed spatial-reuse budget into a pressure-guided adaptive
   update contract only after the renderer owns a per-tile pressure input path.
   Debug mode 20 is the visible layer to validate the active budget phase. Do
   not add native-resolution reservoir intermediates.
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
