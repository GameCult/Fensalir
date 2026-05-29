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

## Previous Failure

The live reservoir history pass is too forgiving. It can carry previous field
candidates into the current frame even when the current pixel has no supporting
scene or field candidate. That creates harsh ghosts on the background and lets
old occluded tubes leak through nearby missed tube samples.

## Rebuilt Cut: 2026-05-29

- The shared GPU reservoir ABI is now `FieldReservoirSample`, not
  `FieldReservoirCandidate`. Its lanes are colorTravel, metadata, control,
  guide, motion, proposal, and stats.
- Proposal stores target, sourcePdf, representedCandidateCount, and
  proposalKind. Stats stores selectedTarget, weightSum, candidateCount, and
  contributionWeight.
- The four rows per pixel have explicit ownership: row 0 current-frame initial
  RIS, row 1 temporal reuse, row 2 spatial/domain reuse, row 3 final resolved
  reservoir.
- TubeField no longer replaces "worst" slots by nearest-travel-minus-coverage
  priority. Proxy rasterization only generates proposals; row 0 owns RIS merge.
- Direct scene/SDF evidence is normalized into the same one-sample proposal
  shape as TubeField before merge.
- RIS update uses `weight = target / sourcePdf`; merge selection uses
  `other.weightSum / (current.weightSum + other.weightSum)`; contribution
  weight is `weightSum / (candidateCount * selectedTarget)`.
- Carried history is validation input only. It cannot become visible fallback by
  itself.
- Temporal reuse validates only previous row-3 final reservoirs by field id,
  travel, normal, domain validity, motion/previous UV, and guide confidence
  before merge.
- Spatial reuse validates neighboring current reservoirs by field id, travel,
  normal, support, and domain before merge.
- TubeField writes previous history UV and expected previous travel from
  rolling-buffer offset motion.
- TubeField metadata includes physical rolling-column identity, so different
  age columns from the same source lane cannot validate as the same surface.
- TubeField samples without explicit motion do not fall back to generic
  camera-only history validation.
- TubeField validation bypasses raw RGB neighborhood clamp and relaxes coverage
  continuity; travel, physical-column identity, normal agreement, and support
  now own reuse.
- Debug modes 13-15 inspect the compute-owned reservoir result rather than old
  current-frame guide textures: rejection state, selectedTarget, weightSum,
  candidateCount, proposal target/sourcePdf, and contribution weight.
- Field evidence lowering now preserves proposal policy through backend packets,
  and validation rejects zero or non-finite target/sourcePdf before backend
  emission.

## Authority Map

- Owner: `D3D12ReservoirHistoryUpdateCS` owns temporal/spatial reuse and row-3
  final reservoir output; `D3D12FieldReservoirResolvePS` owns only current-frame
  normalization and initial RIS merge.
- Inputs: scene/SDF MRT evidence, TubeField row-0 proposals, previous row-3
  final reservoirs, camera reprojection inputs, current metadata/control/guide.
- Outputs: row 0 current, row 1 temporal, row 2 spatial, row 3 final, plus the
  resolved HDR field texture.
- Derived state: proxy raster output and scene MRTs are proposal inputs; carried
  history rows are validation inputs; debug guide lanes are diagnostics.
- Forbidden writers: TubeField proxy rasterization, presentation, prior history
  rows, and priority-sorted visibility buffers must not decide final color.
- Shared path: direct scene/SDF, TubeField, and future producers must emit
  target/sourcePdf/represented proposal state before reservoir update/reuse.
- Deletion line: the old nearest-travel-minus-coverage replacement path is gone
  from TubeField injection and final resolve.

## Action Items

1. Done: cut unsupported carried history from visible fallback.
2. Done: cut top-four visibility candidate rows.
3. Done: add proposal/stats lanes and RIS merge math.
4. Done: make TubeField and scene/SDF share one-sample proposal normalization.
5. Done: assign row 0/1/2/3 to current/temporal/spatial/final reservoir stages.
6. Done: preserve proposal policy through field evidence lowering/backend
   packets and reject invalid proposal weights.
7. Done: expose rebuilt reservoir diagnostics as named UI labels. Modes 13-15
   now mean rejection, stats, and proposal; invalid final reservoirs render as
   unsupported instead of masquerading as accepted history.
8. Partial: tune occlusion/disocclusion thresholds against fresh captures after
   this architecture cut.
9. Next: capture final color and row-3 debug views against the noisy/occlusion
   screenshot class and tune target/support policy from those diagnostics.
