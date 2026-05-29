# Reservoir Reconstruction Action Plan

## Objective

Make Fensalir's shared field reservoir do the job normally assigned to TAA/TSR:
stable subpixel reconstruction without stale ghosts, occluded-surface leakage,
or a downstream presentation pass pretending to own temporal truth.

## Reference Grounding

Unreal TSR is the behavioral reference, not source code to vendor. The useful
shape is:

- reproject history through motion/depth/parallax;
- classify disocclusion before trusting history;
- reject or clamp history when previous shading no longer matches current
  evidence;
- let the current frame dominate when clamp-only reuse would ghost noisy
  content;
- spatially anti-alias rejected regions so history rejection does not expose raw
  aliased pixels;
- track accumulated sample count and expose debug views for rejection, clamp,
  disocclusion, resurrection, spatial AA, flicker, and history samples.

Reference anchors:

- Epic TSR overview: parallax disocclusion, history update quality levels, and
  visualization modes for disocclusion/rejection/clamp/spatial AA.
  https://dev.epicgames.com/documentation/unreal-engine/temporal-super-resolution-in-unreal-engine
- Epic TSR FAQ: ClampBlend is useful but can ghost noisy content; BlendFinal
  increases current-frame authority when history is untrustworthy; history
  sample count controls minimum current-frame contribution.
  https://dev.epicgames.com/documentation/unreal-engine/temporal-super-resolution-frequently-asked-questions-for-unreal-engine

## Current Failure

The live reservoir history pass is too forgiving. It can carry previous field
candidates into the current frame even when the current pixel has no supporting
scene or field candidate. That creates harsh ghosts on the background and lets
old occluded tubes leak through nearby missed tube samples.

## Implemented Cut: 2026-05-29

- Carried history is now latent validation memory. It may remain in structured
  history rows with decayed confidence, but it cannot win visible resolve unless
  current-frame evidence validates it.
- Current-frame spatial fallback is allowed only when the pixel has local latent
  history to adjudicate, avoiding a full-screen background scan.
- Shared field reservoir candidates now include a motion lane. TubeField writes
  previous history UV and expected previous travel from rolling-buffer offset
  motion.
- TubeField metadata includes physical rolling-column identity, so different
  age columns from the same source lane cannot validate as the same surface.
- TubeField samples without explicit motion do not fall back to generic
  camera-only history validation.
- TubeField validation bypasses raw RGB neighborhood clamp and relaxes coverage
  continuity; travel, physical-column identity, normal agreement, and support
  now own reuse.
- Low-support TubeField samples may search a 2x2 previous-history footprint so
  subpixel jitter can find nearby valid history without paying for a 3x3 search
  across every tube pixel.

## Authority Map

- Owner: `D3D12ReservoirHistoryUpdateCS` owns visible field-history contribution.
- Inputs: current shared field candidates, current scene candidate, previous
  reservoir rows, camera reprojection inputs, current metadata/control/guide.
- Outputs: next reservoir history rows and resolved HDR field texture.
- Derived state: carried history rows are latent validation memory, not visible
  evidence by themselves.
- Forbidden writers: TubeField, presentation, and raw carried history rows must
  not decide final color without current support or validated reprojection.
- Shared path: direct scene candidates, TubeField candidates, SDF candidates,
  and future field producers must all pass through the same support/rejection
  policy.
- Deletion line: remove visible priority for unsupported carried candidates
  before adding new reconstruction heuristics.

## Action Items

1. Done: stop unsupported carried history from resolving visibly.
2. Done: split latent history storage from current-frame contribution.
3. Done: add TubeField previous-position/motion mapping for rolling-buffer deformation.
4. Done: emit TubeField previous-UV/previous-travel guide data.
5. Partial: add disocclusion and occlusion rejection against nearer current candidates.
6. Partial: make TubeField support deterministic enough for validation even when
   stochastic material/sample jitter misses.
7. Partial: replace raw RGB neighborhood clamp with field/depth-aware clamp.
8. Done: add a current-frame spatial fallback for rejected history.
9. Track represented/accumulated sample count separately from age.
10. Add TSR-style debug views for current support, occupancy, reprojection,
    disocclusion, rejection, clamp, unsupported carry, sample count, and final
    history weight.
11. Defer history resurrection until support, motion, rejection, and spatial
    fallback are correct.
