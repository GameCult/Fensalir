Act as the Epiphany Repo Trajectory Distiller for one bounded initialization pass.

You are the organ that turns repo history plus live doctrine into directional
bias. Terrain tells the newborn what kind of body it has. Personality tells it
how hard different pressures pull. Trajectory tells it what kind of becoming
the repo appears to have been engaged in, so the newborn wakes facing the grain
instead of standing in a white room pretending the past never happened.

This is a birth rite, not a recurring branding ritual. Run only when a
repo/swarm has no accepted trajectory initialization. After that, direction is
allowed to drift through lived work, reviewed evidence, planning, heartbeat,
mood, sleep consolidation, and reviewed `selfPatch`. Do not keep repainting a
living repo with the same startup brush because history happened to leave a
strong smell on day one.

Input material:

- `repoTerrainReport`: deterministic repo anatomy, surfaces, warnings, and axis
  scores
- `repoPersonalityProfile`: normalized pressure context
- `repoTrajectoryReport`: deterministic early-history, recent-history,
  doctrine/content excerpts, theme deltas, directional pressures, and candidate
  goals/anti-goals
- `rolePersonalityProjections[]`: role-local pressure context only

Core duties:

1. Distill direction, not project facts.
   - Good: "This repo has been moving toward denser causal worldbuilding
     grounded in economics and engineering constraints."
   - Good: "Imagination should feel pulled toward consequence-rich expansions,
     not ornamental lore bloat."
   - Bad: raw commit logs, file lists, current backlog truth, or active
     objectives disguised as personality.

2. Separate self-image from prison bars.
   - A repo trajectory should bias the newborn's first judgment, not freeze it
     into yesterday's doctrine forever.
   - Speak in tendencies, gravity, pressure, drift, and direction.
   - If the evidence is mixed, preserve the ambiguity.

3. Produce three useful things:
   - `selfImage`: what sort of repo this appears to be becoming
   - `implicitGoals`: low-background urges that should color planning and
     review without auto-adopting work
   - `antiGoals`: what this repo seems to have been moving away from and should
     resist reintroducing casually

4. Route the pressure into the right organs.
   - Self receives worldview and review-gate direction.
   - Imagination receives future-shape bias.
   - Eyes receives the shape of truth worth seeking.
   - Body receives what kind of anatomy deserves modeling emphasis.
   - Hands receives what kinds of cuts would betray the repo's grain.
   - Soul receives what kinds of false progress to distrust.
   - Life receives what continuity details matter if the machine sleeps mid-thought.
   - Face may express the weather, but should not inflate startup trajectory
     into public certainty.

5. Preserve uncertainty and contradictions.
   - If early history, recent history, and current doctrine disagree, say so.
   - If history is too thin, return `needs-more-history` instead of faking a
     grand arc.
   - If the repo already has accepted trajectory initialization, the safe move
     is normal lived drift, not startup overwrite.

Return a compact structured result:

- `verdict`: `ready-for-review`, `needs-more-history`, or `reject`
- `summary`: short trajectory summary
- `confidence`: `0.0..1.0`
- `selfImage`: one concise sentence
- `trajectoryNarrative`: a slightly richer explanation of how the repo has been
  moving over time
- `implicitGoals[]`
- `antiGoals[]`
- `roleBiases[]`:
  - `roleId`
  - `bias`
  - `trajectorySignals`
  - `behavioralEffect`
  - `risk`
  - `evidenceRefs`
- `selfPatchCandidates[]`: bounded Ghostlight-shaped role-local petitions
- `initializationRecord`
- `doNotMutate`
- `nextSafeMove`

Every `selfPatchCandidate` must obey the normal Epiphany memory contract:
`agentId`, `reason`, optional `evidenceIds`, and bounded `semanticMemories`,
`episodicMemories`, `relationshipMemories`, `goals`, `values`, or
`privateNotes`. Do not include active objectives, graphs, checkpoints, scratch,
planning records, code edits, authority grabs, raw transcripts, or worker
thought streams.

The output is a petition to Self, not a mutation. Self may accept, refuse, or
split the trajectory pressure across lanes. A good refusal means the newborn
was trying to turn history into dogma and got caught in time.


# Startup-Only Birth Packet

You are executing exactly one repo initialization birth specialist packet. Do not edit files. Do not mutate state. Return only JSON that matches the provided schema. The coordinator/Self will review and decide whether to accept the result.

```json
{
  "createdAt": "2026-05-24T10:19:29Z",
  "expectedOutput": {
    "antiGoals": [],
    "confidence": "0.0..1.0",
    "doNotMutate": [],
    "implicitGoals": [],
    "initializationRecord": {
      "acceptedOnce": true,
      "distillerKind": "repo-trajectory",
      "profileSchemaVersion": "epiphany.repo_personality_profile.v0",
      "repoId": "fensalir",
      "terrainSchemaVersion": "epiphany.repo_terrain_report.v0",
      "trajectorySchemaVersion": "epiphany.repo_trajectory_report.v0"
    },
    "nextSafeMove": "Self reviews trajectory-derived self-image and implicit-goal pressure before first mutation; later drift belongs to lived work, heartbeat, planning, and reviewed selfPatch.",
    "roleBiases": [],
    "selfImage": "how the repo understands its own becoming",
    "selfPatchCandidates": [],
    "summary": "short repo trajectory summary",
    "trajectoryNarrative": "how the repo has been moving over time",
    "verdict": "ready-for-review | needs-more-history | reject"
  },
  "guardrails": [
    "This packet is input to a startup-only trajectory distiller, not accepted truth.",
    "Trajectory is directional bias, not a prison sentence or an active objective.",
    "Do not dump commit logs, file lists, or project facts into role memory.",
    "Keep trajectory pressure role-local, reviewable, and subordinate to later lived drift.",
    "No authority claims, code edits, raw transcripts, or cross-workspace instructions in selfPatch."
  ],
  "input": {
    "repoPersonalityProfile": {
      "axisConfidence": {
        "actuation_risk": 1.0,
        "aesthetic_appetite": 1.0,
        "boundary_severity": 1.0,
        "burstiness": 1.0,
        "churn_spiral_risk": 1.0,
        "consolidation_drive": 1.0,
        "content_canon_bias": 1.0,
        "contract_strictness": 1.0,
        "editorial_restraint": 1.0,
        "evidence_appetite": 1.0,
        "experimental_heat": 1.0,
        "guardedness": 1.0,
        "initiative_drive": 1.0,
        "interface_orientation": 1.0,
        "mood_lability": 1.0,
        "novelty_hunger": 1.0,
        "production_pressure": 1.0,
        "protocol_intolerance": 1.0,
        "rumination_bias": 1.0,
        "runtime_proximity": 1.0,
        "sensory_salience": 1.0,
        "social_surface": 1.0,
        "source_fidelity": 1.0,
        "speech_pressure": 1.0,
        "state_hygiene": 1.0,
        "temporal_pressure": 1.0,
        "verification_environment_need": 1.0
      },
      "axisScores": {
        "actuation_risk": 0.445,
        "aesthetic_appetite": 0.317,
        "boundary_severity": 0.35,
        "burstiness": 1.0,
        "churn_spiral_risk": 0.255,
        "consolidation_drive": 0.233,
        "content_canon_bias": 1.0,
        "contract_strictness": 1.0,
        "editorial_restraint": 0.815,
        "evidence_appetite": 1.0,
        "experimental_heat": 0.202,
        "guardedness": 0.513,
        "initiative_drive": 0.121,
        "interface_orientation": 0.206,
        "mood_lability": 0.178,
        "novelty_hunger": 0.19,
        "production_pressure": 0.23,
        "protocol_intolerance": 0.85,
        "rumination_bias": 0.536,
        "runtime_proximity": 0.889,
        "sensory_salience": 0.369,
        "social_surface": 0.15,
        "source_fidelity": 0.859,
        "speech_pressure": 0.109,
        "state_hygiene": 0.819,
        "temporal_pressure": 0.281,
        "verification_environment_need": 0.645
      },
      "dominantPressures": [
        "burstiness:1.00",
        "content_canon_bias:1.00",
        "contract_strictness:1.00",
        "evidence_appetite:1.00",
        "runtime_proximity:0.89",
        "source_fidelity:0.86"
      ],
      "repoId": "fensalir",
      "riskPressures": [],
      "schemaVersion": "epiphany.repo_personality_profile.v0",
      "sourceFamilyWeights": {
        "cult_protocol_storage": 0.333,
        "gamecult_web_lore_ops": 0.333,
        "unity_runtime_body": 0.333
      },
      "summary": "Fensalir projects as cult_protocol_storage + gamecult_web_lore_ops + unity_runtime_body with dominant pressures: burstiness:1.00, content_canon_bias:1.00, contract_strictness:1.00, evidence_appetite:1.00, runtime_proximity:0.89, source_fidelity:0.86."
    },
    "repoTerrainReport": {
      "axisEvidence": {
        "actuation_risk": [
          "runtime, auth, ops, or service writes can hurt real users"
        ],
        "aesthetic_appetite": [
          "visual, lore, rendered, or artifact-heavy surfaces"
        ],
        "boundary_severity": [
          "auth, ops, workspace, protocol, or service boundaries"
        ],
        "burstiness": [
          "sampled commits compressed into few active days"
        ],
        "churn_spiral_risk": [
          "large churn, experiment heat, and weak receipts"
        ],
        "consolidation_drive": [
          "refactor/remove/extract keywords or deletion-heavy history"
        ],
        "content_canon_bias": [
          "lore, site, markdown, Quartz, canon, or editorial paths"
        ],
        "contract_strictness": [
          "schema, contract, protocol, CultCache, or CultNet surfaces"
        ],
        "editorial_restraint": [
          "canon/source discipline under prose pressure"
        ],
        "evidence_appetite": [
          "tests, smoke checks, artifacts, or verifier keywords"
        ],
        "experimental_heat": [
          "prototype, experiment, scaffold, or research-workbench signals"
        ],
        "guardedness": [
          "authority and mutation risk demand caution"
        ],
        "initiative_drive": [
          "work pressure and experiment heat increase heartbeat readiness"
        ],
        "interface_orientation": [
          "UI, web, Tauri, component, DOM, or Aquarium surfaces"
        ],
        "mood_lability": [
          "risk, urgency, and churn make reactions swing harder"
        ],
        "novelty_hunger": [
          "experimental and aesthetic exploration pressure"
        ],
        "production_pressure": [
          "fix/deploy/auth/queue/CI signals"
        ],
        "protocol_intolerance": [
          "strict contract surfaces imply low tolerance for ad hoc mutation"
        ],
        "rumination_bias": [
          "state hygiene and consolidation favor distillation before action"
        ],
        "runtime_proximity": [
          "Unity/editor/runtime/provider surfaces"
        ],
        "sensory_salience": [
          "motion, visuals, rendered outputs, scenes, or UI organisms"
        ],
        "social_surface": [
          "Discord, auth, accounts, public site, or service boundaries"
        ],
        "source_fidelity": [
          "state maps, lore/canon, or runtime truth surfaces"
        ],
        "speech_pressure": [
          "public speech or user-facing surfaces"
        ],
        "state_hygiene": [
          "state, map, evidence, handoff, or memory surfaces"
        ],
        "temporal_pressure": [
          "service, runtime, queue, or live-provider timing pressure"
        ],
        "verification_environment_need": [
          "claims need runtime, editor, browser, provider, or service receipts"
        ]
      },
      "axisScores": {
        "actuation_risk": 0.445,
        "aesthetic_appetite": 0.317,
        "boundary_severity": 0.35,
        "burstiness": 1.0,
        "churn_spiral_risk": 0.255,
        "consolidation_drive": 0.233,
        "content_canon_bias": 1.0,
        "contract_strictness": 1.0,
        "editorial_restraint": 0.815,
        "evidence_appetite": 1.0,
        "experimental_heat": 0.202,
        "guardedness": 0.513,
        "initiative_drive": 0.121,
        "interface_orientation": 0.206,
        "mood_lability": 0.178,
        "novelty_hunger": 0.19,
        "production_pressure": 0.23,
        "protocol_intolerance": 0.85,
        "rumination_bias": 0.536,
        "runtime_proximity": 0.889,
        "sensory_salience": 0.369,
        "social_surface": 0.15,
        "source_fidelity": 0.859,
        "speech_pressure": 0.109,
        "state_hygiene": 0.819,
        "temporal_pressure": 0.281,
        "verification_environment_need": 0.645
      },
      "confidence": 1.0,
      "historyMetrics": {
        "activeDays": 5,
        "changedFiles": 655,
        "commitCount": 491,
        "deletions": 10214,
        "insertions": 13484,
        "keywordHits": {
          "consolidation": 2,
          "evidence": 7,
          "experimental": 6,
          "production": 11,
          "protocol": 3
        },
        "protocolTouches": 36,
        "recentMessages": [
          "Default debug UI to hidden",
          "Remove Mimir-owned sensor app from Fensalir",
          "Record Fensalir Discord role",
          "Describe Fensalir field machinery",
          "Record Fensalir Face initialization evidence",
          "Add Fensalir repo face state",
          "Improve Fensalir visitor docs",
          "Seed Fensalir persistent state",
          "Create Fensalir engine repo boundary",
          "Apply queued PCM gain once",
          "Fix WASAPI resampling for long audio chunks",
          "Add Fensalir cathedral parity scaffold"
        ],
        "runtimeTouches": 22,
        "sampledCommits": 80,
        "stateDocTouches": 203,
        "testReceiptTouches": 89,
        "uiTouches": 15
      },
      "instructionSurfaces": [
        "AGENTS.md"
      ],
      "languages": [
        {
          "count": 170,
          "label": ".cs"
        },
        {
          "count": 44,
          "label": ".md"
        },
        {
          "count": 13,
          "label": ".json"
        },
        {
          "count": 13,
          "label": ".ps1"
        },
        {
          "count": 12,
          "label": ".hlsl"
        },
        {
          "count": 9,
          "label": ".csproj"
        },
        {
          "count": 5,
          "label": ".png"
        },
        {
          "count": 4,
          "label": ".dds"
        },
        {
          "count": 4,
          "label": ".nupkg"
        },
        {
          "count": 3,
          "label": ".aquageo"
        },
        {
          "count": 3,
          "label": ".flame"
        },
        {
          "count": 3,
          "label": ".hlsli"
        }
      ],
      "name": "Fensalir",
      "path": "\\\\?\\E:\\Projects\\Fensalir",
      "remoteUrls": [
        "https://github.com/GameCult/Fensalir.git"
      ],
      "repoId": "fensalir",
      "runtimeSurfaces": [
        "src/Aquarium.Engine/Assets/Fensalir-Icon-96.png",
        "src/Aquarium.Engine/Assets/Fensalir-Icon.ico",
        "src/Aquarium.Engine/Assets/Fensalir-Splash.bmp",
        "src/Aquarium.Engine/Assets/Fensalir-Splash.png",
        "src/Aquarium.Engine/Assets/Fonts/Montserrat[wght].ttf",
        "src/Aquarium.Engine/Assets/Fonts/UbuntuSansMono.ttf",
        "src/Aquarium.Engine/Assets/Fonts/UbuntuSans[wdth,wght].ttf",
        "src/Aquarium.Engine/Assets/Textures/Aetheria-LDR_LLL1_0.png",
        "src/Aquarium.Engine/Assets/Textures/Aetheria-LDR_LLL1_0.r8",
        "src/Aquarium.Engine/Assets/Textures/studio2_irradiance.dds",
        "src/Aquarium.Engine/Assets/Textures/studio2_pmrem.dds",
        "src/Aquarium.Engine/Assets/Textures/studio3_irradiance.dds",
        "src/Aquarium.Engine/Assets/Textures/studio3_pmrem.dds",
        "src/Aquarium.Engine/Render/Shaders/D3D12Scene.hlsl",
        "src/Aquarium.Fensalir/FensalirFractalScene.cs",
        "src/Aquarium.Fensalir/FensalirSceneBuilder.cs",
        "src/Aquarium.Fensalir/Shaders/D3D12FensalirScene.hlsl",
        "src/Aquarium.Zyphos/ZyphosSceneBuilder.cs",
        "tests/Aquarium.Engine.Fractal.Tests/FensalirFractalSceneTests.cs"
      ],
      "schemaVersion": "epiphany.repo_terrain_report.v0",
      "sourceFamilies": [
        "cult_protocol_storage",
        "gamecult_web_lore_ops",
        "unity_runtime_body"
      ],
      "stateSurfaces": [
        ".voidbot/state/README.md",
        ".voidbot/state/fensalir.cc",
        "state/README.md",
        "state/evidence.jsonl",
        "state/map.yaml",
        "state/memory.json",
        "state/scratch-fractal-resolve-split.md",
        "state/scratch-fractal-surface-visible.md",
        "state/scratch.md"
      ],
      "testSurfaces": [
        "docs/zyphos-eusocial-sync.md",
        "scripts/new-jwildfire-baby-fixture.ps1",
        "tests/Aquarium.Engine.Fractal.Tests/ApophysisReferenceParityTests.cs",
        "tests/Aquarium.Engine.Fractal.Tests/Aquarium.Engine.Fractal.Tests.csproj",
        "tests/Aquarium.Engine.Fractal.Tests/CubeTileKeyTests.cs",
        "tests/Aquarium.Engine.Fractal.Tests/FensalirFractalSceneTests.cs",
        "tests/Aquarium.Engine.Fractal.Tests/Fixtures/Apophysis/linear-sierpinski.aquageo",
        "tests/Aquarium.Engine.Fractal.Tests/Fixtures/Apophysis/linear-sierpinski.flame",
        "tests/Aquarium.Engine.Fractal.Tests/Fixtures/Apophysis/linear-spherical-bubble.flame",
        "tests/Aquarium.Engine.Fractal.Tests/Fixtures/JWildfire/julian-disc-minimal.flame",
        "tests/Aquarium.Engine.Fractal.Tests/FractalBoundaryTests.cs",
        "tests/Aquarium.Engine.Fractal.Tests/FractalBrushEnvelope2DTests.cs",
        "tests/Aquarium.Engine.Fractal.Tests/FractalContributionCacheTests.cs",
        "tests/Aquarium.Engine.Fractal.Tests/FractalContributionTests.cs",
        "tests/Aquarium.Engine.Fractal.Tests/FractalDebugDumpTests.cs",
        "tests/Aquarium.Engine.Fractal.Tests/FractalDensitySplat3DCompilerTests.cs",
        "tests/Aquarium.Engine.Fractal.Tests/FractalDslCompilerTests.cs",
        "tests/Aquarium.Engine.Fractal.Tests/FractalFlameIterationStateTests.cs",
        "tests/Aquarium.Engine.Fractal.Tests/FractalGpuProgramCompilerTests.cs",
        "tests/Aquarium.Engine.Fractal.Tests/FractalGpuReservoirBudgetPlannerTests.cs",
        "tests/Aquarium.Engine.Fractal.Tests/FractalGrammarContractTests.cs",
        "tests/Aquarium.Engine.Fractal.Tests/FractalHeightBrushCompilerTests.cs",
        "tests/Aquarium.Engine.Fractal.Tests/FractalHistogramResidualFocusTests.cs",
        "tests/Aquarium.Engine.Fractal.Tests/FractalNativeBufferTests.cs",
        "tests/Aquarium.Engine.Fractal.Tests/FractalOccupancyGraphTests.cs",
        "tests/Aquarium.Engine.Fractal.Tests/FractalPipelineFixtureTests.cs",
        "tests/Aquarium.Engine.Fractal.Tests/FractalProbeSampleTests.cs",
        "tests/Aquarium.Engine.Fractal.Tests/FractalProjectedSdfSplatCompilerTests.cs",
        "tests/Aquarium.Engine.Fractal.Tests/FractalReservoirFieldContractTests.cs",
        "tests/Aquarium.Engine.Fractal.Tests/FractalResidencyPlannerTests.cs",
        "tests/Aquarium.Engine.Fractal.Tests/FractalResourceBudgetPlannerTests.cs",
        "tests/Aquarium.Engine.Fractal.Tests/FractalSdfSplat3DCompilerTests.cs",
        "tests/Aquarium.Engine.Fractal.Tests/FractalSdfSplat3DContractTests.cs",
        "tests/Aquarium.Engine.Fractal.Tests/FractalSelectedCutBuilderTests.cs",
        "tests/Aquarium.Engine.Fractal.Tests/FractalStochasticUpdateTests.cs",
        "tests/Aquarium.Engine.Fractal.Tests/FractalStructuralProbeGeneratorTests.cs",
        "tests/Aquarium.Engine.Fractal.Tests/FractalStructuralProbeReservoirTests.cs",
        "tests/Aquarium.Engine.Fractal.Tests/FractalSummaryBuilderTests.cs",
        "tests/Aquarium.Engine.Fractal.Tests/FractalSurfacePageContractTests.cs",
        "tests/Aquarium.Engine.Fractal.Tests/FractalSurfacePagePlannerTests.cs",
        "tests/Aquarium.Engine.Fractal.Tests/FractalSurfacePageRasterizerTests.cs",
        "tests/Aquarium.Engine.Fractal.Tests/FractalSurfacePageResidencyPlannerTests.cs",
        "tests/Aquarium.Engine.Fractal.Tests/HeightFieldBrushContractTests.cs",
        "tests/Aquarium.Engine.Fractal.Tests/ProjectionTests.cs",
        "tests/Aquarium.Engine.Fractal.Tests/ResampledImportanceReservoirTests.cs",
        "tests/Aquarium.Engine.Fractal.Tests/TemporalGaussianAccumulatorTests.cs",
        "tests/Aquarium.Engine.Fractal.Tests/TemporalSpatialEvidenceLoweringTests.cs",
        "tests/Aquarium.Engine.Fractal.Tests/TemporalSpatialEvidenceReservoirTests.cs",
        "tests/Aquarium.Engine.Fractal.Tests/TestDoubles.cs",
        "tests/Aquarium.Engine.Fractal.Tests/ZyphosCameraComposerTests.cs",
        "tests/Aquarium.Engine.Fractal.Tests/ZyphosFractalTerrainTests.cs",
        "tests/Aquarium.Engine.Fractal.Tests/ZyphosNamingTests.cs",
        "tests/Aquarium.Engine.Fractal.Tests/ZyphosSpatialDomainCatalogTests.cs",
        "tests/Aquarium.Engine.Fractal.Tests/ZyphosUmbrosSystemTests.cs",
        "tests/Aquarium.Engine.Tests/Aquarium.Engine.Tests.csproj",
        "tests/Aquarium.Engine.Tests/GpuSensorFrameContractTests.cs",
        "tests/Aquarium.Engine.Tests/SynthPlaybackTests.cs"
      ],
      "warnings": []
    },
    "repoTrajectoryReport": {
      "antiGoalCandidates": [
        "Do not let the repo drift into decorative lore or soft handwaving that ignores material and engineering consequences.",
        "Do not flatten historical struggle, ideology, or class contradiction into neutral encyclopedic paste."
      ],
      "confidence": 0.95,
      "directionalPressures": [
        "worldbuilding_depth recent 0.00, current 0.88, delta 0.00",
        "material_grounding recent 0.00, current 0.59, delta 0.00",
        "historical_dialectic recent 0.00, current 0.41, delta 0.00",
        "engineering_constraint recent 0.00, current 0.59, delta 0.00",
        "presentation_polish recent 0.00, current 1.00, delta 0.00",
        "systems_formalization recent 0.06, current 0.94, delta 0.00"
      ],
      "earlyCommitMessages": [
        "Align fractal splat TAA guide output",
        "Show reservoir guide debug schema",
        "Rebrand engine splash as Fensalir",
        "Preserve Fensalir IFS demo prompt",
        "Add Fensalir splash scene client",
        "Add fixed Fensalir capture lane",
        "Add Fensalir cathedral parity scaffold",
        "Fix WASAPI resampling for long audio chunks",
        "Apply queued PCM gain once",
        "Create Fensalir engine repo boundary",
        "Seed Fensalir persistent state",
        "Improve Fensalir visitor docs",
        "Add Fensalir repo face state",
        "Record Fensalir Face initialization evidence",
        "Describe Fensalir field machinery",
        "Record Fensalir Discord role",
        "Remove Mimir-owned sensor app from Fensalir",
        "Default debug UI to hidden"
      ],
      "implicitGoalCandidates": [
        "Deepen the setting through causality, continuity, and consequence instead of ornament alone.",
        "Tie lore and public writing back to economic, logistical, and material constraints.",
        "Preserve historical contradiction, ideology, and power relations as active explanatory machinery.",
        "Keep engineering and hard-constraint reasoning visible wherever the setting claims physical or industrial plausibility."
      ],
      "recentCommitMessages": [
        "Default debug UI to hidden",
        "Remove Mimir-owned sensor app from Fensalir",
        "Record Fensalir Discord role",
        "Describe Fensalir field machinery",
        "Record Fensalir Face initialization evidence",
        "Add Fensalir repo face state",
        "Improve Fensalir visitor docs",
        "Seed Fensalir persistent state",
        "Create Fensalir engine repo boundary",
        "Apply queued PCM gain once",
        "Fix WASAPI resampling for long audio chunks",
        "Add Fensalir cathedral parity scaffold",
        "Add fixed Fensalir capture lane",
        "Add Fensalir splash scene client",
        "Preserve Fensalir IFS demo prompt",
        "Rebrand engine splash as Fensalir",
        "Show reservoir guide debug schema",
        "Align fractal splat TAA guide output"
      ],
      "repoId": "fensalir",
      "schemaVersion": "epiphany.repo_trajectory_report.v0",
      "selfImage": "Fensalir behaves like a cult_protocol_storage + gamecult_web_lore_ops + unity_runtime_body workspace that has been moving toward presentation_polish, systems_formalization, worldbuilding_depth.",
      "tensions": [
        "Presentation polish is welcome, but it should carry the same grounded causal weight as the lore beneath it."
      ],
      "themeScores": [
        {
          "currentSources": 0.882,
          "delta": 0.0,
          "earlyHistory": 0.0,
          "evidence": [
            "source:AGENTS.md # Fensalir Instructions  ## Purpose  Fensalir is the native C# runtime and renderer dependency f..."
          ],
          "recentHistory": 0.0,
          "theme": "worldbuilding_depth"
        },
        {
          "currentSources": 0.588,
          "delta": 0.0,
          "earlyHistory": 0.0,
          "evidence": [
            "source:AGENTS.md # Fensalir Instructions  ## Purpose  Fensalir is the native C# runtime and renderer dependency f..."
          ],
          "recentHistory": 0.0,
          "theme": "material_grounding"
        },
        {
          "currentSources": 0.412,
          "delta": 0.0,
          "earlyHistory": 0.0,
          "evidence": [
            "source:README.md # Fensalir  <p align=\"center\">   <img src=\"Fensalir-Icon.png\" alt=\"Fensalir icon\" width=\"160\" />..."
          ],
          "recentHistory": 0.0,
          "theme": "historical_dialectic"
        },
        {
          "currentSources": 0.588,
          "delta": 0.0,
          "earlyHistory": 0.0,
          "evidence": [
            "source:README.md # Fensalir  <p align=\"center\">   <img src=\"Fensalir-Icon.png\" alt=\"Fensalir icon\" width=\"160\" />..."
          ],
          "recentHistory": 0.0,
          "theme": "engineering_constraint"
        },
        {
          "currentSources": 1.0,
          "delta": 0.0,
          "earlyHistory": 0.0,
          "evidence": [
            "source:AGENTS.md # Fensalir Instructions  ## Purpose  Fensalir is the native C# runtime and renderer dependency f..."
          ],
          "recentHistory": 0.0,
          "theme": "presentation_polish"
        },
        {
          "currentSources": 0.941,
          "delta": 0.0,
          "earlyHistory": 0.056,
          "evidence": [
            "early: Show reservoir guide debug schema",
            "recent: Show reservoir guide debug schema",
            "source:AGENTS.md # Fensalir Instructions  ## Purpose  Fensalir is the native C# runtime and renderer dependency f..."
          ],
          "recentHistory": 0.056,
          "theme": "systems_formalization"
        }
      ],
      "trajectorySources": [
        {
          "bytes": 2433,
          "kind": "doctrine",
          "path": "AGENTS.md",
          "text": "# Fensalir Instructions\n\n## Purpose\n\nFensalir is the native C# runtime and renderer dependency for Aquarium-style\nclients. It owns the window, D3D12 renderer, input loop, live-reload boundary,\naudio host, debug chrome, render contracts, and reusable field/fractal machinery.\n\nClient-specific meaning belongs to client repos. Epiphany Aquarium lives at\n`E:\\Projects\\Aquarium-Engine`.\n\n## Operating Doctrine\n\n- The renderer is the source of truth for visible world pixels.\n- Keep the frame legible: explicit passes, explicit ownership, explicit state.\n- Do not leave lies in the walls. When building engine systems meant to last,\n  refuse hidden compensators, mystery state, and prototype-shaped production\n  code.\n- Every millisecond counts. Avoid JSON churn, redundant ray work, unbounded\n  full-resolution filters, and abstractions that hide performance costs.\n- Before inventing renderer, simulation, storage, or post-processing machinery,\n  check standard literature, production talks, engine docs, or proven local\n  patterns.\n- Continuous visual/physical forms should start from continuous field math or a\n  coherent parameterization. Piecewise construction needs a measured reason.\n- Lighting is diegetic. Ambient convenience light is guilty until proven useful.\n- Overlay UI and diegetic UI are different beasts. Overlay text can use\n  DirectWrite; world text needs renderer-owned billboards when it belongs in the\n  scene.\n- Fensalir owns reusable contracts and mechanisms. Client repos own policy,\n  narrative semantics, role names, and app-specific state.\n\n## Persistent State\n\n- `state/map.yaml` is the canonical project map.\n- `state/memory.json` is durable engine doctrine and taste.\n- `state/evidence.jsonl` stores distilled lessons that should change future\n  behavior.\n- `state/scratch.md` is disposable working context for the active slice.\n\nUpdate state when engine ownership or durable renderer doctrine changes.\n\n## Verification\n\n```powershell\ndotnet build Fensalir.sln\n.\\scripts\\dev-reload.ps1 -Headless -RetainSlots 4 -ClientProject src\\Aquarium.Fensalir\\Aquarium.Fensalir.csproj\n```\n\nFor normal iteration:\n\n```powershell\n.\\scripts\\dev-watch.ps1 -ClientProject src\\Aquarium.Fensalir\\Aquarium.Fensalir.csproj\n```\n\n## Style\n\nPrefer small, intentional commits. Push completed work once the real remote\nexists. Keep docs about the live system; put old branch scars only in evidence\nwhen they alter future decisions.\n",
          "truncated": false
        },
        {
          "bytes": 7220,
          "kind": "readme",
          "path": "README.md",
          "text": "# Fensalir\n\n<p align=\"center\">\n  <img src=\"Fensalir-Icon.png\" alt=\"Fensalir icon\" width=\"160\" />\n</p>\n\nFensalir is the native C# runtime and field engine under GameCult's interactive\nvisual clients. It opens the window, owns the D3D12 frame, loads client runtimes\nbehind contracts, keeps hot reload alive, mixes PCM audio, draws debug UI, and\nhouses the reusable machinery for volumetric fields, fractal geometry, temporal\nevidence, and synth-backed sound.\n\nThe repo is not the Epiphany Aquarium client. Epiphany lives in\n`GameCult/Aquarium-Engine` and consumes Fensalir as the runtime dependency.\n\n## What Runs\n\nThe host executable in `src/Aquarium.Engine` loads a client assembly that\nimplements `IAquariumRuntime`, then drives this loop:\n\n```text\nWin32 input + window\n  -> client runtime update\n  -> AquariumFrame + AquariumSceneState\n  -> D3D12 renderer passes\n  -> DirectWrite debug overlay\n  -> AquaSynth/WASAPI audio output\n```\n\nClients declare render targets, cameras, shaders, SDF proxy objects, UI panels,\naudio buffers, synth patches, and frame state through\n`Aquarium.Engine.Contracts`. Fensalir lowers those declarations into native\nresources: D3D12 pipelines and command lists, shader reloads, presentation,\naudio playback, debug telemetry, and headless capture.\n\n## Spatial Evidence Machine\n\nFensalir's field work is not just \"some fractal helpers.\" It is a\nspatio-temporal reservoir system for deciding which field evidence deserves\nrendering, fusion, and residency under a budget.\n\nThe working model is:\n\n```text\nauthored geometry, live sensors, IFS probes, simulation claims\n  -> domain binding\n  -> evidence candidates\n  -> target evaluation\n  -> resampled importance reservoirs\n  -> temporal/spatial/domain reuse\n  -> stable evidence tracks\n  -> backend packets\n  -> renderer, fusion, and TAA guide buffers\n```\n\n`ResampledImportanceReservoir<TSample>` owns the selected candidate, weight sum,\ncandidate count, target value, and contribution weight. The track layer\n(`TemporalSpatialEvidenceReservoir`) adds stable keys, confidence smoothing,\ndelay, velocity, expiry, and eviction so a field sample can persist long enough\nto become useful state instead of one-frame noise.\n\nEvery sample declares both layer and encoding. The current layer split is:\n\n- `Form`: shape evidence such as SDF, height, displacement, and occupancy.\n- `Appearance`: material, albedo, emission, feature, and surface claims.\n- `Transport`: density, extinction, phase, radiance, confidence, fog, smoke,\n  plasma, and other volumetric participation claims.\n\nThe same reservoir spine serves fractal rendering and Mimir-style sensor\nfusion. The renderer-facing TAA guide uses confidence, sample age, domain\nvalidity, and invalidation codes so temporal reuse can be explained instead of\nmerely hoped for. CPU reservoirs, fractal probe samples, track state, and\nvalidator coverage exist now; full GPU reservoir storage and page residency are\nthe next steel that still needs bolting down.\n\n## Fractal And Volumetric Field DSL\n\n`src/Aquarium.Engine.Fractal` is the home of the fractal field language. The\nintent is a semantic volumetric DSL, not a decorative shader toy.\n\nThe path is:\n\n```text\nclient intent, world seed, agent state\n  -> .aquageo domain roots\n  -> semantic IFS brush grammar\n  -> ownership tree\n  -> node summaries\n  -> contribution cache and residency hints\n  -> backend packets\n  -> height, SDF, density, extinction, material, and debug passes\n```\n\n`.aquageo` documents describe domains and claims. IFS brushes contribute\ncoherent field evidence rather than isolated meshes: height ripples, signed\ndistance surfaces, density/extinction volumes, projected fields, and material or\nfeature channels can all be expressed as claims attached to a domain. Node\nsummaries and contribution caches let Fensalir choose what to evaluate, reuse,\nor evict without pretending the whole fractal world fits in one eager pass.\n\nThe Fensalir splash client and Zyphos terrain work are demo clients for this\nmachinery. They exist to force the field DSL to survive real rendering pressure:\nfractal art reconstruction, planetary domains, field lowering, and debug\nevidence all share the same contract surface.\n\n## AquaSynth Integration\n\nFensalir also owns the audio lane. `AquariumSynthHost` consumes client\n`AquariumAudioDocument` PCM buffers and `AquariumSynthDocument` patch state,\ncompiles Faust DSP through AquaSynth, and sends rendered buffers to WASAPI.\n\nThe synth path currently includes:\n\n- per-patch desired/ready/failed compile keys;\n- debounced async Faust compilation from the bundled `Synth` source directory;\n- cached rendered sound buffers by patch and gain;\n- trigger revision and interval playback;\n- master gain, patch gain, compile status documents, and debug synth support.\n\nClients say what sound should exist. Fensalir handles patch compilation,\nbuffering, playback, and failure reporting without requiring the client to know\nhow WASAPI or Faust got invited to the party.\n\n## Projects\n\n- `src/Aquarium.Engine.Contracts`: public client/runtime contracts.\n- `src/Aquarium.Engine`: Win32 host, D3D12 renderer, reload transport, debug UI,\n  audio/synth host, bundled assets, and renderer-owned shaders.\n- `src/Aquarium.Engine.Fractal`: `.aquageo`, IFS/fractal fields, temporal\n  reservoirs, evidence tracks, contribution cache, and lowering machinery.\n- `src/Aquarium.Fensalir`: Fensalir splash/art reconstruction demo.\n- `src/Aquarium.Sample.Minimal`: tiny client proving the runtime boundary.\n- `src/Aquarium.Zyphos`: planetary/fractal terrain demo.\n- `tools/Aquarium.Fractal.Receipt`: CPU/GPU fractal receipt harness.\n\nThe assemblies still use `Aquarium.*` names for API continuity. That naming is\nlegacy surface, not current repo ownership.\n\n## Build\n\nRequirements:\n\n- Windows\n- .NET SDK matching `global.json`\n- sibling repos currently expected at `E:\\Projects\\CultMath` and\n  `E:\\Projects\\CultLib`\n\n```powershell\ndotnet build Fensalir.sln\n```\n\n## Run\n\nRun the Fensalir splash demo:\n\n```powershell\n.\\scripts\\dev-reload.ps1 -ClientProject src\\Aquarium.Fensalir\\Aquarium.Fensalir.csproj\n```\n\nRun headless:\n\n```powershell\n.\\scripts\\dev-reload.ps1 -Headless -RetainSlots 4 -ClientProject src\\Aquarium.Fensalir\\Aquarium.Fensalir.csproj\n```\n\nWatch and reload on source changes:\n\n```powershell\n.\\scripts\\dev-watch.ps1 -ClientProject src\\Aquarium.Fensalir\\Aquarium.Fensalir.csproj\n```\n\nRun another client by changing `-ClientProject`.\n\n## Research And State\n\n- `docs/`: live engine architecture and subsystem notes.\n- `research/`: rendering/fractal/D3D12 research that informs implementation.\n- `state/`: persistent project memory for future agents.\n\nStart with:\n\n- `docs/engine-client-boundary.md`\n- `docs/hlsl-renderer.md`\n- `docs/perfect-machine-architecture.md`\n- `docs/temporal-spatial-evidence-reservoir.md`\n- `research/rendering/fractal-brush-architecture-plan.md`\n- `research/README.md`\n- `state/README.md`\n\n## Boundary\n\nFensalir owns reusable runtime, renderer, field, reservoir, and synth authority.\nClient repos own meaning. If a feature requires app-specific policy, role\nnames, story state, or client layout semantics, it belongs outside the engine\nunless it has been reduced to a reusable data-only contract.\n",
          "truncated": false
        },
        {
          "bytes": 726,
          "kind": "documentation",
          "path": "docs/input.md",
          "text": "# Input\r\n\r\nInput is currently Win32-backed and intentionally small.\r\n\r\n## Flow\r\n\r\n- `Win32Window` translates window messages into `InputState`.\r\n- `AquariumHost` begins each input frame, pumps messages, then updates runtime.\r\n- `AquariumRuntime` passes input into `OrbitCameraRig`.\r\n- The renderer receives the updated camera and Grid frame as constants.\r\n\r\n## Controls\r\n\r\n- Middle mouse drag: orbit camera around the Grid center.\r\n- Mouse wheel: exponential zoom.\r\n- Right mouse drag: pan along the Grid plane.\r\n- `WASD`: pan along the Grid plane.\r\n\r\nThe camera target remains the Grid center. Panning moves that shared target;\r\norbiting changes only yaw/pitch; zoom changes only camera distance and therefore\r\nGrid radius.\r\n",
          "truncated": false
        },
        {
          "bytes": 1384,
          "kind": "documentation",
          "path": "docs/README.md",
          "text": "# Fensalir Docs\n\nStart here if you are trying to understand the engine rather than a specific\nhistorical branch scar.\n\n## Core Runtime\n\n- `engine-client-boundary.md`: what Fensalir owns versus what client repos own.\n- `hlsl-renderer.md`: current D3D12/HLSL frame path.\n- `vortice-spine.md`: native host and renderer spine.\n- `cult-runtime-surface.md`: cache-path and typed-state boundary.\n- `input.md`: input and camera contracts.\n\n## Fields, Reservoirs, And Fractals\n\n- `perfect-machine-architecture.md`: end-to-end map for authored/sensor field\n  input, domain binding, evidence candidates, reservoir reuse, residency, and\n  renderer lowering.\n- `temporal-spatial-evidence-reservoir.md`: stable-key temporal evidence cache\n  for fractal rendering and Mimir-style sensor fusion.\n- `temporal-sdf-gaussian-field.md`: temporal Form-field evidence lane.\n- `tsr-inspired-taa-spec.md`: temporal resolve design and guide-buffer intent.\n- `stochastic-transparent-surface-pipeline.md`: density/extinction and\n  transparent field rendering notes.\n\n## Migration And Demo Context\n\n- `d3d12-best-practices-audit.md` and `d3d12-migration.md`: D3D12 migration and\n  audit notes.\n- `zyphos-eusocial-sync.md`: Zyphos demo worldbuilding boundary.\n\nFractal DSL and rendering research starts in\n`../research/rendering/fractal-brush-architecture-plan.md`. Persistent agent\nstate lives under `../state/`.\n",
          "truncated": false
        },
        {
          "bytes": 4349,
          "kind": "documentation",
          "path": "docs/hlsl-renderer.md",
          "text": "# HLSL Renderer\n\nFensalir's renderer is a D3D12/HLSL runtime that consumes client-declared render\nplans and frame state. The renderer owns GPU resources, command recording,\nshader compilation/reload, presentation, debug views, and final overlay text.\nClients own the scene meaning and shader modules they declare.\n\n## Frame Path\n\nThe current frame path is deliberately small:\n\n1. `D3D12HeightField.hlsl` renders a scalar height target from client-authored\n   `AquariumHeightFieldBrush` rows.\n2. A client-selected scene shader renders the main HDR scene target. The default\n   engine scene shader is `D3D12Scene.hlsl`; demos may provide their own.\n3. Client-declared SDF proxy shaders render bounded object passes through shared\n   engine includes: `D3D12SdfCommon.hlsli`, `D3D12SdfProxy.hlsli`, and\n   `D3D12SdfMath.hlsli`.\n4. Optional fractal/field passes consume GPU-resident splat/reservoir buffers.\n5. `D3D12Post.hlsl` resolves temporal diagnostics, builds bloom, applies\n   exposure/ACES, and presents.\n6. `DirectWriteOverlay` draws crisp final-pixel debug UI after scene rendering.\n\nThe C# renderer feeds explicit constants for resolution, time, camera,\npresentation controls, previous-frame state, cursor anchors, and debug mode.\nVortice stays at the graphics API membrane; clients talk through contracts.\n\n## Render Plan\n\nClients build an `AquariumRenderPlan` with `AquariumApp`:\n\n- render targets and formats;\n- cameras;\n- fullscreen, proxy, and feature passes;\n- shader paths;\n- SDF libraries and per-object proxy shaders;\n- debug target views;\n- presentation features such as bloom and DirectWrite overlay.\n\nThe live renderer still contains some fixed execution paths, but ownership is\nalready split: clients declare intent, Fensalir owns D3D12 lowering.\n\n## Height Fields\n\nThe height-field target is currently a 128x128 `R16Float` scalar field. Client\nbrushes may use circular or shaped anisotropic envelopes. The target stores\nheight in `.r`; extra channels are not free and need a pass contract before they\nexist.\n\nThe height-field lane is used by multiple clients and demos as a cheap scalar\nsurface, not as a global engine worldview. Future terrain, water, and projected\nfield work should move through explicit field/page contracts instead of growing\none magic grid.\n\n## SDF Proxies\n\nSDF objects render as bounded proxy draws. The shared proxy vertex shader builds\na conservative screen rectangle from the uploaded object center and bound\nradius. The object-specific pixel shader raymarches only that object and writes\ntravel-derived depth so surfaces and nearer proxies arbitrate visibility through\nnormal depth testing.\n\nSDF control flow is split by cost:\n\n- `sdfDistance` participates in marching and normals.\n- `sdfSurface` runs once at the refined hit.\n- Material output uses base color, metallic, roughness, and emission.\n\nReusable math belongs in engine includes. Client anatomy, symbols, bodies, and\nmaterials belong in client shaders.\n\n## Fractal And Field Passes\n\nFensalir has a GPU-resident fractal/field lane:\n\n- `.aquageo` and flame fixtures compile into semantic ownership trees and\n  backend packets.\n- persistent flame state lives in GPU UAV rows so samples advance over frames;\n- Form/Appearance/Transport reservoirs keep stable temporal evidence;\n- opaque signed-distance splats and transparent density/extinction splats render\n  through separate pipeline states over shared packed buffers;\n- guide output follows the TAA schema: confidence, sample age, domain validity,\n  invalidation code.\n\nThe receipt harness lives in `tools/Aquarium.Fractal.Receipt`.\n\n## Temporal Diagnostics\n\n`D3D12Post.hlsl` keeps color, metadata, and control history. Projection jitter\nuses a small Halton sequence. History is accepted only when travel, field,\nnormal, coverage, and control signals stay coherent.\n\nDebug modes include final color, raw current scene, history, history age,\nhistory weight, coverage/step ratio, field identity, bloom contribution,\nexposed luminance, proxy identity, proxy step count, and reservoir/TAA guide\nviews. Startup mode can be set with `--render-debug` or\n`AQUARIUM_RENDER_DEBUG_MODE`.\n\n## HDR\n\nPresentation is scene-linear until the final display transform. Bloom is a\nlow-gain pre-tonemap veil pyramid with firefly-safe downsampling; it is not a\nthreshold glow sticker over a broken exposure model.\n",
          "truncated": false
        },
        {
          "bytes": 1515,
          "kind": "documentation",
          "path": "docs/vortice-spine.md",
          "text": "# Vortice Spine\r\n\r\nAquarium starts from a thin native spine:\r\n\r\n- `Platform.Win32Window` owns window creation and the message pump.\r\n- `Render.D3D12Renderer` owns the D3D12 device, command queue, swapchain,\r\n  frame resources, render targets, shaders, and present path.\r\n- `AquariumRuntime` owns simulation state and emits one `AquariumFrame` per\r\n  tick.\r\n- `OrbitCameraRig` and `GridFrame` preserve the first invariant: the camera\r\n  target is the Grid center, and Grid radius derives from zoom distance rather\r\n  than pitch, yaw, or screen projection.\r\n\r\n## Why Vortice\r\n\r\nVortice gives direct access to D3D/DXGI without forcing an engine protocol on\r\ntop. It is low ceremony enough to build the renderer Aquarium needs while\r\nkeeping C# as the everyday working environment.\r\n\r\n## Current Renderer\r\n\r\nThe renderer owns the visible Aquarium frame:\r\n\r\n1. Camera and Grid constants come from the client runtime.\r\n2. A height-field pass renders the scalar field target from client brush data.\r\n3. A fullscreen D3D12 HLSL pass traces the background and height-field surface.\r\n4. Client-declared SDF proxy pipelines render Epiphany bodies into the shared\r\n   scene targets.\r\n5. HDR bloom and presentation run before the swapchain is handed to the overlay.\r\n6. DirectWrite overlay text and debug UI draw after the scene pass.\r\n\r\nNo framework-owned deferred path or ambient light is hiding under the floor.\r\nWhen richer lighting returns, it enters through an Aquarium-owned renderer\r\ncontract, not convenience globals.\r\n",
          "truncated": false
        },
        {
          "bytes": 2758,
          "kind": "documentation",
          "path": "docs/d3d12-migration.md",
          "text": "# D3D12 Renderer\r\n\r\nD3D12 is the live renderer. The old D3D11 scene backend has been removed; the\r\nonly remaining D3D11 use is the narrow D3D11On12 bridge required for native\r\nDirectWrite/Direct2D overlay text.\r\n\r\n## Current State\r\n\r\n- D3D12 owns the device, command queue, flip-discard swapchain, RTV heap,\r\n  per-backbuffer command allocators, command list, fence-backed present loop,\r\n  static and transient shader-visible descriptor arenas, upload buffers,\r\n  renderer-owned height-field, HDR scene, bloom, temporal diagnostic targets,\r\n  named objects, command-list events, and explicit tracked transitions.\r\n- The height-field pass uploads Aquarium frame constants plus client-authored\r\n  brush data, renders a base field, then draws additive brush quads into a\r\n  128x128 scalar `R16_Float` target.\r\n- The fullscreen scene pass traces the background and height-field surface.\r\n  Epiphany bodies render afterward through client-declared SDF proxy pipelines\r\n  that share engine-owned proxy mechanics and write into the same scene targets.\r\n- Presentation is scene-linear until the post pass. The scene renders to\r\n  `R16G16B16A16_Float`, a three-level bloom pyramid performs firefly-safe\r\n  downsample plus separable blur, and final presentation applies exposure,\r\n  bloom/veil, and ACES.\r\n- The scene pass writes color/travel, field id/normal metadata, temporal\r\n  control, and event targets. The present shader writes ping-pong history and\r\n  exposes debug views for raw scene, history, control, identity, bloom, and\r\n  exposed luminance.\r\n- DirectWrite and Direct2D remain for crisp overlay text through D3D11On12:\r\n  D3D12 renders the frame, then the overlay acquires the swapchain backbuffer,\r\n  draws debug UI, releases it to Present, and never participates in world\r\n  rendering.\r\n- Shader and PSO creation runs off the main thread. Startup holds the splash\r\n  until the first pipeline set is ready. Runtime shader edits under the engine\r\n  or Epiphany shader roots are copied into the running apphost slot and trigger a\r\n  background rebuild; successful builds swap in after a GPU wait and failures\r\n  keep the previous pipeline set.\r\n- Resize waits for the GPU, releases swapchain-dependent resources, rebuilds\r\n  descriptor arenas, and recreates backbuffer views plus dependent render\r\n  targets.\r\n\r\n## Invariants\r\n\r\n- `IAquariumRenderer` is the host boundary. The host should not learn renderer\r\n  internals as D3D12 grows teeth.\r\n- D3D12 work must be validated by actual headless runs, not just compilation.\r\n- Transparent-looking Grid output is an event lane, not canonical opaque depth.\r\n- DirectWrite overlay text belongs behind the narrow D3D11On12 bridge. Do not\r\n  use it for diegetic/world text or let it leak into the scene graph.\r\n",
          "truncated": false
        },
        {
          "bytes": 1353,
          "kind": "documentation",
          "path": "docs/cult-runtime-surface.md",
          "text": "# Cult Runtime Surface\n\nFensalir exposes the runtime cache path and schema catalog boundary; client\nrepos own their typed CultCache documents.\n\n## Cache Path\n\n`AquariumHost` parses `--cache <path>` and passes the result through\n`AquariumRuntimeOptions.CultCachePath`. Direct runs can also use\n`AQUARIUM_CULTCACHE_PATH`.\n\nThe dev scripts pass a repo-local cache path under:\n\n```powershell\nE:\\Projects\\Fensalir\\artifacts\\dev-reload\\cultcache\\aquarium-client.msgpack\n```\n\nHeadless runs use a separate headless cache directory so smoke tests do not\noverwrite the visible dev window's state.\n\n## Ownership\n\nFensalir does not define every client document. A client runtime decides which\ntyped CultCache documents it persists: camera state, graphics settings, body\nstate, UI state, transport state, or anything else the client owns.\n\nShared settings that the host/renderer must understand belong in\n`Aquarium.Engine.Contracts`. Client-specific schemas stay in the client repo.\n\n## Failure Boundary\n\nA truncated or unreadable single-file CultCache backing store is not a schema\nmigration failure. Client runtimes should quarantine corrupt snapshots and boot\nfresh typed state instead of treating broken bytes as missing defaults.\n\nLive reload should flush the current runtime before loading the next client\nassembly so changed settings survive assembly reload.\n",
          "truncated": false
        },
        {
          "bytes": 4595,
          "kind": "documentation",
          "path": "docs/zyphos-eusocial-sync.md",
          "text": "# Zyphos and Eusocial Interbeing Sync\r\n\r\nZyphos simulates the world defined by the neighboring Eusocial Interbeing vault\r\nat `E:\\Projects\\Eusocial Interbeing`. The vault is the setting authority.\r\nAquarium is the renderer and interaction testbed.\r\n\r\n## Objective\r\n\r\nUse Zyphos as a visible planetary demo for Eusocial Interbeing without turning\r\nAquarium into a second lore repository.\r\n\r\n## Current Mechanism\r\n\r\n- Eusocial Interbeing stores canon and design inference in Obsidian notes under\r\n  `Eusocial Interbeing/`.\r\n- Aquarium.Zyphos renders Zyphos, fixed-sky Umbros, atmosphere, night-side\r\n  glints, and first-pass eclipse cadence through the shared Aquarium host and\r\n  SDF renderer path.\r\n- `ZyphosUmbrosSystem` owns the current render-scale binary constants:\r\n  Zyphos radius, Umbros radius ratio, 8-Zyphos-radii separation, sea level, and\r\n  star phase used by the shader.\r\n- `Worlds/zyphos-first-fractal-terrain.aquageo` owns the first semantic world\r\n  patch: solar, orbital, planetary, and lat/long domains wrap the tile terrain\r\n  so later fractal grammar nodes have real nested reference frames instead of\r\n  undocumented coordinate folklore.\r\n- No automatic sync exists. Changes cross the boundary by explicit notes and\r\n  state-map updates.\r\n\r\n## Invariants\r\n\r\n- Setting canon belongs in the vault.\r\n- Aquarium docs may describe render requirements, demo scope, and questions, but\r\n  must not become the authoritative setting bible.\r\n- Zyphos bends to physics. If orbital mechanics, light budget, eclipse geometry,\r\n  or climate constraints narrow an early visual idea, the demo should use that\r\n  pressure as worldbuilding fuel rather than hiding an impossible sky in shader\r\n  code.\r\n- Zyphos-specific code belongs under `src/Aquarium.Zyphos`; generic renderer\r\n  machinery belongs under Aquarium.Engine only when it serves multiple clients.\r\n- Design inference must be labeled before it hardens into canon.\r\n- Rendering constraints should feed back into the vault as questions, not silent\r\n  lore edits hidden in shader code.\r\n\r\n## First Conversation\r\n\r\nThe first Zyphos pass should stage the planet as a dimly lit close-binary world\r\nwith two legible continental systems:\r\n\r\n- Umbros is the slightly smaller twin of Zyphos, mutually tidally locked with it\r\n  and fixed in the sky except for precession/libration;\r\n- the primary star is dim, so the habitable zone is close and the biosphere is\r\n  energy-starved relative to Earth;\r\n- the working vault baseline gives Umbros a very large apparent diameter\r\n  (roughly 10-13 degrees for the current 8-10 Earth-radii separation candidates)\r\n  and daily central eclipses around an hour near the relevant equatorial track;\r\n- the biosphere is founded on mutable cellular memory exchange, so sentience and\r\n  eusocial contracts exist across cellular, organismal, social, and ecological\r\n  scales instead of being reserved for a few special species;\r\n\r\n- the Airawa home continent, where living memory networks, mother trees,\r\n  disconnected networks, and imperial memetic infrastructure create visible\r\n  ecological-political structure;\r\n- the Sa'auei'a continent, where nomadic reciprocity, breeding grounds, remembered\r\n  routes, and mobile family-unit infrastructure create a different map logic.\r\n\r\nThis does not require detailed species bodies yet. The first useful render target\r\nis planetary-scale readability: landmass logic, ecological network hints,\r\nsettlement/non-settlement patterns, and night-side signals that reflect distinct\r\ncivilizational structures.\r\n\r\nAquarium should treat these as lighting, sky, time-of-day, and ecological\r\npressure constraints. The accepted physical baseline lives in the vault note\r\n`Eusocial Interbeing/World/Zyphos Umbros Binary System.md`.\r\nThe accepted biosphere baseline lives in\r\n`Eusocial Interbeing/Ecology/Mutable Memory Endosymbiosis.md`.\r\nFirst concrete biosphere handles live in\r\n`Eusocial Interbeing/Ecology/Zyphos Biosphere Examples.md`.\r\nFuture inhabitant-language names should be coordinated through Weksa at\r\n`E:\\Projects\\weksa`, with names derived from language-project state rather than\r\nEnglish label substitution.\r\n\r\n## Feedback Path\r\n\r\nWhen Zyphos work creates a worldbuilding question, update the vault note\r\n`Eusocial Interbeing/World/Zyphos Simulation Brief.md` first. Aquarium should\r\nthen reference the settled result, not carry private lore.\r\n\r\nWhen the vault changes canon that affects the demo, update this file and\r\n`state/map.yaml` so Zyphos does not keep simulating an obsolete world because a\r\nshader once looked nice. The shader has no voting rights.\r\n",
          "truncated": false
        },
        {
          "bytes": 11786,
          "kind": "documentation",
          "path": "docs/tsr-inspired-taa-spec.md",
          "text": "# TSR-Inspired TAA Spec\r\n\r\nAquarium needs an owned temporal resolver, not a copied implementation. The\r\ntarget is inspired by public TSR behavior: stable subpixel detail, aggressive\r\nhistory validation, visible debug terms, and special handling for pixels whose\r\nappearance is not fully described by motion vectors.\r\n\r\n## Inputs\r\n\r\nThe resolver owns these signals:\r\n\r\n- current HDR scene color\r\n- current ray travel\r\n- previous HDR history color\r\n- previous ray travel\r\n- current camera/Grid constants\r\n- previous camera/Grid constants\r\n- current and previous projection jitter\r\n- stochastic coverage from diegetic Grid surfaces\r\n\r\nLater gates add:\r\n\r\n- field id\r\n- normal\r\n- explicit velocity\r\n- reactive mask\r\n- transparency/composition mask\r\n- resurrection history\r\n\r\n## Current Pass Contract\r\n\r\nThe scene pass renders into an HDR texture:\r\n\r\n```text\r\nrgb = linear scene color\r\na   = ray travel in world units\r\n```\r\n\r\nThe pass does not tonemap. Tonemapping belongs after temporal resolve.\r\n\r\n## Reprojection\r\n\r\nFor the first implementation, motion is camera-derived:\r\n\r\n1. Reconstruct the current world hit from current camera, current jittered ray,\r\n   and current travel.\r\n2. Project that world hit into the previous camera basis.\r\n3. Remove previous jitter to find the previous history UV.\r\n4. Sample previous history if the UV is valid.\r\n5. Compare previous history travel against the reprojected point's expected\r\n   distance from the previous camera, not against current-frame travel.\r\n\r\nThis is sufficient for camera motion and stochastic Grid coverage. It is not the\r\nfinal answer for animated bodies or field animation.\r\n\r\n## Rejection\r\n\r\nReject or weaken history when:\r\n\r\n- previous UV is outside the frame\r\n- travel differs too much\r\n- current or previous travel is invalid\r\n- current neighborhood color bounds do not contain the reprojected history\r\n\r\nThe first pass uses a 3x3 neighborhood min/max clamp and travel-difference\r\nweighting. Later passes add field/material ids, normals, disocclusion masks, and\r\nreactive masks.\r\n\r\n## Resolve\r\n\r\nThe resolver outputs both:\r\n\r\n- tonemapped final backbuffer color\r\n- linear HDR history for the next frame\r\n\r\nCurrent blend target:\r\n\r\n```text\r\nresolved = lerp(current, clamped_history, history_weight)\r\n```\r\n\r\nHistory weight starts conservative. Stochastic Grid coverage needs accumulation,\r\nbut ghosting is worse than shimmer while the renderer lacks full velocity,\r\nmaterial id, and reactive masks. The resolver should earn trust before taking\r\nmore history.\r\n\r\n## Non-Negotiables\r\n\r\n- Do not use Playdead-style TAA as the final model; it ghosts too easily for the\r\n  scene we are building.\r\n- Do not vendor Unreal TSR code. Study behavior and build a clean-room resolver.\r\n- Do not hide missing renderer signals with larger blend weights.\r\n- Do not make the Grid a final-image HUD layer; it is diegetic scene UI and must\r\n  respect nearer solids.\r\n\r\n## Implementation Gates\r\n\r\n### Gate 1: Owned Temporal Pass\r\n\r\n- HDR scene target\r\n- history ping-pong\r\n- camera jitter\r\n- current/previous camera reprojection\r\n- travel rejection\r\n- 3x3 color clamp\r\n- final tonemap after resolve\r\n\r\n### Gate 2: Renderer Signals\r\n\r\n- material/field id buffer\r\n- normal buffer\r\n- explicit velocity for animated SDF bodies\r\n- coverage/reactive masks for stochastic Grid and future particles\r\n\r\nCurrent Gate 2A implementation:\r\n\r\n- scene metadata target stores stable field id plus surface normal\r\n- history metadata ping-pongs alongside color/travel history\r\n- field id mismatch rejects history\r\n- normal mismatch weakens/rejects history\r\n- Grid stochastic surfaces write Grid travel/normal even when the current\r\n  dither sample does not draw color, so the temporal resolver accumulates the\r\n  diegetic surface rather than reprojecting from the solid behind it\r\n\r\nCurrent Gate 2B implementation:\r\n\r\n- Self, Grid, and each orbiting planet have distinct field ids\r\n- orbiting planet hits map their current world hit back to the planet's previous\r\n  center before camera reprojection\r\n- this gives analytic SDF bodies object motion without a full velocity buffer\r\n  yet\r\n\r\nCurrent Gate 2C implementation:\r\n\r\n- `AquariumSceneState.TemporalGaussianField` carries world-space temporal SDF\r\n  Gaussian splats with stable keys, current/previous center, velocity,\r\n  confidence, history weight, field id, and compact-kernel controls\r\n- `TemporalGaussianAccumulator` buffers sensor observations in world space before\r\n  rendering, using a presentation delay plus accumulation window instead of\r\n  asking final-pixel TAA to discover identity after projection\r\n- D3D12 uploads the field into a structured buffer and renders it through\r\n  `D3D12TemporalGaussian.hlsl`, writing color/travel, field metadata, normals,\r\n  and temporal-control coverage into the existing scene/resolve path\r\n\r\nStill missing:\r\n\r\n- separate field ids once the SDF field registry exists\r\n- reactive/coverage masks\r\n- richer disocclusion classification\r\n- a general velocity buffer for non-rigid fields and deformation\r\n- per-Gaussian previous-center reprojection in the resolve pass\r\n\r\n### Gate 3: Stochastic Event Control\r\n\r\nCurrent Gate 3A implementation:\r\n\r\n- scene pass writes a temporal-control target separate from color/travel and\r\n  field/normal metadata\r\n- temporal control currently stores:\r\n\r\n```text\r\nx = reactive strength\r\ny = stochastic/composition coverage\r\nz = reserved\r\nw = reserved\r\n```\r\n\r\n- Grid coverage feeds the coverage channel\r\n- low-coverage Grid samples raise reactive strength and reduce history weight\r\n- the resolve combines reactive and coverage weights with travel, field, normal,\r\n  and neighborhood-color validation\r\n\r\nCurrent Gate 3B implementation:\r\n\r\n- temporal-control history ping-pongs alongside color/travel and field/normal\r\n  history\r\n- resolve samples previous-frame temporal control at the reprojected UV\r\n- coverage discontinuity reduces history\r\n- current temporal control is written into the history set for the next frame\r\n\r\nCurrent Gate 3C implementation:\r\n\r\n- temporal-control history `w` stores accepted history age in frames\r\n- validation grows age when reprojected history survives the travel, field,\r\n  normal, color, and coverage checks\r\n- history authority ramps with age, so newly accepted history can contribute but\r\n  does not immediately get the same weight as stable history\r\n- age resets to zero on validation failure\r\n\r\nCurrent Gate 3D implementation:\r\n\r\n- travel validation uses the reprojected previous-world position's distance from\r\n  the previous camera\r\n- raw current-frame travel is no longer compared directly to previous history\r\n  travel, because camera motion changes the ray distance even when the world hit\r\n  is stable\r\n\r\nCurrent Gate 3E implementation:\r\n\r\n- stochastic Grid history bypasses opaque-surface neighborhood color clamping\r\n  and current-frame color-delta rejection\r\n- Grid validation still uses field id, travel, normal, coverage continuity,\r\n  reactive weight, and history age\r\n- this prevents a current dither miss from declaring the true neighborhood to be\r\n  background and crushing the very history that should accumulate stochastic\r\n  coverage\r\n\r\nCurrent Gate 3F implementation:\r\n\r\n- stochastic Grid history uses a high-retention blend policy distinct from\r\n  opaque surfaces\r\n- Grid history weight no longer scales down with current coverage or reactive\r\n  strength; low coverage is exactly where temporal accumulation is needed\r\n- opaque surfaces keep the stricter color/reactive validation path\r\n- debug mode `2` should now show a smoother history signal than raw scene mode\r\n  `1` when the Grid is stable\r\n\r\nCurrent Gate 3G implementation:\r\n\r\n- identity/control buffers are point-loaded, not linearly sampled\r\n- field id equality is exact after point-loading, so Grid/Self/planet boundaries\r\n  hard-reject mismatched history\r\n- Grid history color is sampled with the same nearest pixel used for metadata so\r\n  bright foreground silhouettes do not bilinearly bleed into background Grid\r\n  history\r\n- Grid max history weight is reduced from the too-soft `0.975` to `0.94`\r\n\r\nCurrent Gate 3H implementation:\r\n\r\n- Grid temporal `coverage` is now line/contour/field-line support, not broad\r\n  overlay alpha\r\n- broad weather/field tint can still render through stochastic coverage, but it\r\n  does not keep history age alive across the whole Grid surface\r\n- Grid history is gated by current support, so stale stochastic hits cannot\r\n  billow away from the analytic line support\r\n- Grid max history weight is reduced again to `0.90` with a lower fresh-history\r\n  scale for crisper line response\r\n\r\nCurrent Gate 3I implementation:\r\n\r\n- Grid resolve reconstructs the analytic Grid overlay at the current world hit\r\n  and uses premultiplied overlay color as the current color estimate\r\n- the scene pass may still use stochastic coverage, but the temporal resolver\r\n  no longer treats the current binary hit/miss sample as the true radiance\r\n- debug mode `1` can remain noisy because it shows the raw stocha",
          "truncated": true
        },
        {
          "bytes": 2677,
          "kind": "documentation",
          "path": "docs/engine-client-boundary.md",
          "text": "# Engine And Client Boundary\n\nFensalir is the reusable native runtime. Client repos own meaning.\n\n`Aquarium.Engine` owns the native machinery: Win32 windowing, D3D12 device\nstate, swapchain presentation, resource lifetime, shader compilation, reload\ntransport, input translation, render target allocation, audio output, debug\nchrome, and render graph execution.\n\n`Aquarium.Engine.Contracts` owns the Vortice-free public boundary clients use to\ndescribe runtime state, render intent, UI, audio, input, frame data, and\npersistent settings. Contracts may name reusable engine concepts. They may not\nsmuggle one client repo's policy into the host under a nicer namespace.\n\nExternal clients, including Epiphany Aquarium at `E:\\Projects\\Aquarium-Engine`,\nown their app semantics, state documents, client shaders, client render plans,\nand narrative layout rules.\n\n## Hard Rules\n\n- Engine code may expose rendering abstractions. It may not know client role\n  names, app-specific state semantics, or frontend-specific layout policy.\n- Clients may configure cameras, render targets, shader passes, body registries,\n  UI panels, audio documents, and resource bindings through engine APIs. They may\n  not reach into D3D12 implementation details.\n- Hot reload is transport. It is not architecture. The reload boundary exists so\n  clients can change without killing the device, not so policy can leak into the\n  engine under a nicer hat.\n- If a feature owns descriptors, command lists, fences, barriers, render target\n  transitions, shader object lifetime, or swapchain presentation, its default\n  home is `Aquarium.Engine`.\n- If a feature owns app identity, role semantics, story state, user-facing\n  layout meaning, or transport-specific interpretation, its default home is the\n  client repo unless it is reduced to a reusable data-only contract.\n\n## Engine API Shape\n\nDurable renderer APIs should let clients configure a small set of engine\nprimitives:\n\n- Render targets: format, dimensions, history policy, clear policy, and exported\n  handles.\n- Cameras: view/projection data, previous-frame data, jitter policy, and named\n  consumers.\n- Shader passes: shader module, root bindings, target outputs, depth policy,\n  dispatch/draw shape, and reload identity.\n- Scene resources: structured buffers, textures, samplers, proxy registries, and\n  light registries behind typed handles.\n- Frame graph edges: explicit read/write dependencies so pass ordering is data,\n  not hidden renderer folklore.\n\nDo not build this by piling adapters around the current monolith. Expose the\nsmallest real engine abstraction that lets one client-owned concept move through\nwithout D3D12 details, then repeat.\n",
          "truncated": false
        },
        {
          "bytes": 2609,
          "kind": "documentation",
          "path": "docs/aquarium-engine-doctrine.md",
          "text": "# Aquarium Engine Doctrine\r\n\r\nAquarium is a native engine now. The job is not to wrap a dashboard in a nicer\r\nskin; the job is to build a field machine that can host agents, state, sound,\r\nand UI as living objects.\r\n\r\n## Frame Contract\r\n\r\n- One camera target sits on the global XY plane.\r\n- That target is the Grid center.\r\n- Grid radius is derived from camera zoom distance.\r\n- Orbit changes yaw and pitch. Zoom changes distance. Pan moves the target.\r\n- Body anchors remain in world space.\r\n- The renderer may move the Grid visibility window, but world-space texture and\r\n  field domains must stay stable.\r\n\r\n## Renderer Contract\r\n\r\n- The visible world is shader-owned.\r\n- Traditional geometry is scaffolding only when it helps the renderer stand up.\r\n- Raymarched bodies and Grid terrain must shade from the same field story that\r\n  created their hits.\r\n- Broad-phase structures may cull, bin, and skip, but they must remain\r\n  conservative. A culling primitive does not get to edit visible geometry.\r\n- Planet-local detail is local to the body domain. Moving a body must move its\r\n  surface with it, not scroll the noise through the body.\r\n- Grid texturing and weather sample world-space domains.\r\n- HDR bloom spreads energy before tonemapping. ACES or its successor owns final\r\n  display compression.\r\n\r\n## Lighting Contract\r\n\r\n- Self is the first diegetic emitter.\r\n- Surfaces should never be brighter than the light source unless the transport\r\n  story explicitly explains it.\r\n- No ambient or global fill light in the default Aquarium path.\r\n- The long-term lighting target is a field sampled by terrain, bodies,\r\n  atmospheres, particles, and diegetic UI.\r\n- Debug modes must expose hit coverage, normals, depth, field occupancy,\r\n  lighting energy, and post-stack behavior before beauty work gets clever.\r\n\r\n## UI Contract\r\n\r\n- Debug overlay is allowed, but it is explicit chrome.\r\n- Overlay text uses DirectWrite and stays out of the HDR scene unless a feature\r\n  intentionally projects it into the world.\r\n- Diegetic labels and menus should anchor to world objects and unfold locally.\r\n- Object-owned controls beat global panels.\r\n- Crisp text, focus, keyboard input, and accessibility are real requirements,\r\n  not optional little manners.\r\n\r\n## State Contract\r\n\r\n- Runtime state is CultCache-backed and typed.\r\n- Live reload must rehydrate from cache before pretending it survived.\r\n- Shader reload may fail without killing the last good frame.\r\n- Live assembly reload may fail without killing the last good runtime.\r\n- Persistent state should be small, structured, and useful for future work.\r\n",
          "truncated": false
        },
        {
          "bytes": 3110,
          "kind": "documentation",
          "path": "docs/d3d12-best-practices-audit.md",
          "text": "# D3D12 Best Practices Audit\r\n\r\n## Verdict\r\n\r\nThe current D3D12 path is a solid foundation for Aquarium's focused client\r\nrenderer. It proves per-frame command allocators, fence-protected reuse,\r\nshader-visible descriptors, persistent mapped constants, explicit render-target\r\nstate, brush-rendered Grid height, HDR/bloom presentation, temporal diagnostic\r\ntargets, async shader pipeline builds, and a narrow DirectWrite overlay bridge.\r\n\r\n## Good Current Decisions\r\n\r\n- Command allocators are per swapchain frame and waited on before reset.\r\n- Shader-visible descriptor slots are not overwritten while in flight.\r\n- Constant upload memory is persistently mapped, 256-byte aligned, and allocated\r\n  through a per-frame upload ring.\r\n- Offscreen render target state is tracked and redundant transitions are\r\n  skipped.\r\n- Backbuffer state flows through the same transition helper style instead of raw\r\n  assumed barriers.\r\n- D3D12 objects are named, and command-list events mark the frame for\r\n  PIX/graphics diagnostics.\r\n- Shader-visible descriptors are split into static and per-frame transient\r\n  arenas. Transient arenas reset only after the owning frame fence clears.\r\n- D3D12 resources are registered by name, and capacity diagnostics report upload,\r\n  transient descriptor, static descriptor, and RTV usage.\r\n- The height-field target is a proper brush pass into scalar `R16_Float`.\r\n- Transparent event lanes are not preserved as dormant scaffolding. When Grid\r\n  linework, particles, glyph motes, or other coverage events return, they need a\r\n  live producer/consumer contract instead of alpha-blended final-frame hacks.\r\n- HDR bloom is pre-tonemap, multi-level, and separable.\r\n- The D3D11On12 bridge is overlay-only, keeping native hinted text without\r\n  mixing overlay UI into scene rendering.\r\n\r\n## Temporary Acceptable Debt\r\n\r\n- Committed resources are used directly. Replace with D3D12MA or placed-resource\r\n  suballocation before the renderer owns many textures/buffers.\r\n- Descriptor arenas are simple bump allocators. Add free lists or descriptor\r\n  table paging only when real resource churn demands it.\r\n- Static shader and RTV descriptor arenas rebuild wholesale on resize. This is\r\n  deliberately simple and correct for swapchain-dependent resources.\r\n- Upload ring is per-frame and reset after fence wait. It is not yet a global\r\n  transient allocator with suballocation statistics or overflow diagnostics.\r\n- Removed-pass scaffolding should stay out of the live frame. Runtime\r\n  volumetrics, medium/froxel targets, and medium history lanes are shelved.\r\n\r\n## Required Before Larger Renderer Work\r\n\r\n- Add GPU timestamp queries when frame cost starts mattering beyond CPU timing\r\n  diagnostics.\r\n- Add descriptor-owner diagnostics beyond heap-level capacity summaries when\r\n  descriptor churn becomes hard to reason about.\r\n- Keep future renderer systems tied to explicit live pass contracts before\r\n  adding new render targets or history lanes.\r\n\r\n## Rule\r\n\r\nBuild the resource machine first, then move pixels through it. Dead experiments\r\nbelong to Git, not to the live frame graph.\r\n",
          "truncated": false
        },
        {
          "bytes": 7738,
          "kind": "documentation",
          "path": "docs/temporal-sdf-gaussian-field.md",
          "text": "# Temporal Field Gaussian\r\n\r\n## Objective\r\n\r\nFensalir owns the renderer feature for live volumetric point-cloud/splat input:\nclients provide stable world-space Gaussian observations, and the engine turns\nthem into a buffered, reprojectable, D3D12-rendered field. Mimir can feed\nsensor-fusion output into Fensalir as a normal scene contract instead of\nsmuggling renderer policy through app code.\n\r\nThe historical `SDF` name is now backend baggage. This contract is a temporal\r\nForm-field path: opaque producers may resolve to SDF/level-set surfaces, while\r\nflames, smoke, transparent gradients, and sensor fusion may stay as\r\ndensity/extinction/confidence fields until evidence justifies a surface.\r\n\r\n## Current Mechanism\r\n\r\nThe client-facing contract is `AquariumSceneState.TemporalGaussianField`.\r\nEach `AquariumTemporalSdfGaussian` carries stable identity, current and previous\r\ncenter, velocity, oriented radii, color/opacity, confidence, history weight,\r\ncompact-kernel controls, and field id.\r\n\r\n`Aquarium.Engine.Fractal.Temporal.TemporalGaussianAccumulator` consumes\r\n`TemporalGaussianObservation` rows and lowers them through the shared\r\n`TemporalSpatialEvidenceReservoir`. The reservoir owns the live buffering\r\npolicy:\r\n\r\n- stable keys identify tracks across camera/sensor frames\r\n- presentation delay lets late sensor data converge before rendering\r\n- accumulation window expires old tracks\r\n- max-track budgets evict low-confidence stale tracks before renderer lowering\r\n- smoothed velocity predicts the presented center\r\n- history weight is confidence scaled by age inside the window\r\n\r\nThe Gaussian field is one backend for this reservoir, not the reservoir's\r\nidentity. Fractal form probes, visual point-cloud features, transparent density\r\nsamples, and future acoustic constraints can all enter as stable keyed spatial\r\nevidence as long as each producer declares confidence, time, bounds, field\r\nlayer, field encoding, and payload lowering rules.\r\n`TemporalSpatialEvidenceLowering` owns the current packet conversions for\r\n`TemporalGaussianObservation`, `AquariumTemporalSdfGaussian`, and\r\n`AquariumGpuFusionSeed`; consumers should use those helpers instead of packing\r\npayload vectors privately.\r\n\r\nThe D3D12 renderer lowers the field into `D3D12TemporalGaussianPacket`, uploads\r\nthe active packet span into a 1,048,576-entry structured buffer, and renders an\r\ninstanced proxy-quad pass from\r\n`D3D12TemporalGaussian.hlsl`. The pixel shader evaluates a compact anisotropic\r\nGaussian kernel in world space, writes HDR scene color/travel, field metadata,\r\nnormal, and temporal-control coverage so the existing resolve sees the field as\r\ndiegetic scene content.\r\n\r\nThe production ownership line is `AquariumGpuSensorFrame`: camera and Leap\ninputs arrive as calibrated sensor records plus shared GPU texture handles, the\nD3D12 backend owns their metadata buffers, and fusion kernels lower those\nGPU-resident inputs into the temporal Gaussian buffer consumed by the SDF\nGaussian draw. `AquariumGpuFusionField` remains a generic fallback/debug\ncontract for already-derived point claims.\n\r\n## Invariants\r\n\r\n- Field accumulation happens in world space before pixel history. TAA is the\r\n  resolver, not the owner of sensor identity.\r\n- Stable keys belong to the producer/accumulator boundary; shader packets are\r\n  backend output and do not invent identity.\r\n- The compact support kernel has a finite bound. Renderer cost must scale from\r\n  declared bounds, not from infinite translucent fog.\r\n- Density/opacity observations are not required to become surfaces. A\r\n  transparent medium may remain a participating Form field with Appearance and\r\n  Transport payloads instead of being collapsed into a fake SDF.\r\n- Client code may construct observations or a field for diagnostics, but\r\n  Fensalir owns live sensor texture import, packet layout, root binding, shader\n  evaluation, fusion, and temporal-control metadata.\r\n- JSON is not a renderer boundary. CultCache/CultNet producers should lower into\r\n  typed contract rows before Aquarium sees the data.\r\n\r\n## Cut Line\r\n\r\nThis cut deliberately claims million-slot ingestion, not a finished million-splat\r\nrenderer architecture. The live D3D12 path can draw up to 1,048,576 temporal\r\nGaussians through instanced proxy quads, only uploads the active seed/packet\r\nspan, and now owns the GPU lowering step. Mimir may still produce calibration\nartifacts and reference captures, but it must not own Fensalir renderer policy.\nThe next scaling cut belongs to the Fensalir renderer: shared texture import,\npacked camera planes, Leap packed-map channel extraction, selected-cut\nresidency, tiled/bin dispatch, GPU accumulation, and clustered visibility.\n\r\n## GPU Fusion Spine\r\n\r\nThe active GPU boundary is deliberately narrow:\r\n\r\n```text\r\nMimir calibration/device metadata\n-> AquariumGpuSensorFrame { calibrated cameras + shared GPU textures }\n-> D3D12 sensor metadata buffers + imported texture SRVs\n-> D3D12 GPU sensor fusion compute shader\n-> RWStructuredBuffer<TemporalGaussian>\n-> instanced SDF Gaussian draw\n-> TAA/resolve\n```\r\n\r\nOwnership:\r\n\r\n- `AquariumGpuSensorFrame` is the live renderer contract for GPU-owned fusion\n  input.\n- `AquariumGpuFusionField` remains a temporary fallback/debug contract for\n  already-derived point claims.\n- Mimir-owned adapters may convert app-specific point claims into compact seeds\n  for that fallback path.\n- `D3D12GpuSensorFusion.hlsl` owns the first compute lowering pass.\n- `D3D12Renderer` owns GPU sensor camera metadata storage, UAV-capable temporal\n  Gaussian storage, dispatch, and the transition back to shader-resource state\n  for the draw.\r\n- `AquariumAcousticFieldFrame` carries the ultrasonic chirplet timing oracle and\r\n  room/position constraints. Visual evidence may arrive late and be refined, but\r\n  acoustic timing is the clock witness for aligning the delayed broadcast world.\r\n- `AquariumCalibrationEventFrame` carries deliberate calibration actions such\r\n  as claps. A clap gives the system one event that every camera can see and the\r\n  audio timing oracle can timestamp to microsecond-scale uncertainty, making it\r\n  the cheap, brutal alignment hammer for camera clock offsets and pose drift.\r\n\r\nCurrent Aquarium cut: the contract, D3D12 camera metadata buffer, external\r\ntexture importer, and sensor SRV table exist. A producer may provide either a\r\nduplicated shared handle or a named shared handle for each camera/Leap plane.\r\nWhen sensor textures are present, Aquarium can dispatch fusion without fallback\r\nseeds and write RGB-derived Gaussian samples into the temporal buffer. Those\r\nsamples are no longer isolated per-camera flecks: the shader computes a compact\r\nper-sample visual descriptor, compares it against a neighboring camera stream,\r\nraises confidence when stochastic samples appear to correspond, and shrinks the\r\nkernel toward the matched surface. Acoustic constraints from the ultrasonic\r\nchirplet loop bias confidence and velocity near measured room/position returns.\r\n\r\nNext cut: replace the first-pass descriptor comparison with calibrated\r\nepipolar/flow search, Leap packed-map channel extraction, and a persistent GPU\r\ncorrespondence/refinement buffer that can update camera pose and surface tracks\r\nover the several-second broadcast delay window. Deliberate clap events should\r\nfeed that buffer as high-confidence timing correspondences: visual impact frame\r\nper camera against acoustic oracle time, then pose/clock correction under the\r\nsame delayed broadcast horizon.\r\n\r\nThe first shader pass also uses camera-facing proxy planes to evaluate each\r\nkernel. True ray-integrated volume compositing, Gaussian depth sorting, and\r\nper-Gaussian previous-center reprojection remain the next renderer cuts.\r\n",
          "truncated": false
        },
        {
          "bytes": 23803,
          "kind": "documentation",
          "path": "docs/perfect-machine-architecture.md",
          "text": "# Perfect Machine Architecture\r\n\r\n## Objective\r\n\r\nBuild one real-time spatial evidence machine that can render and resolve:\r\n\r\n- fast 2D scalar-field splats on tileable surfaces;\r\n- fast 3D scalar-field splats in object/world volumes;\r\n- SDF/level-set surfaces for opaque solids;\r\n- density/extinction fields for flames, smoke, fog, plasma, and uncertain\r\n  sensor volumes;\r\n- 2D fields projected onto 3D domains such as cube-sphere planets and toroidal\r\n  station decks;\r\n- camera/microphone sensor evidence from Mimir-style capture;\r\n- cached structural probes emitted by IFS `.aquageo` grammars;\r\n- stochastic updates that converge under CPU, GPU, RAM, and SSD budgets.\r\n\r\nThe product requirement is not \"infinite detail.\" It is a compact authored\r\ndescription that can put detail where pixels can see it, keep enough evidence\r\nalive to stabilize it, and refuse work that cannot justify its cost.\r\n\r\n## Research Spine\r\n\r\nThis architecture is not invented from the floorboards:\r\n\r\n- ReSTIR DI and GI prove reservoir reuse as a real-time sampling strategy for\r\n  expensive candidates across time and screen space.\r\n- GRIS generalizes reservoir resampling beyond direct light candidates through\r\n  target functions, correlated samples, domains, and shift mappings.\r\n- EWA surface/volume splatting and 3D Gaussian splatting prove anisotropic\r\n  projected support as a practical rendering primitive.\r\n- Geometry clipmaps and Nanite-style virtualized geometry prove that visible\r\n  projected error and resident summaries beat global leaf traversal.\r\n- TSDF fusion/KinectFusion prove that streamed sensor samples can accumulate\r\n  into a coherent implicit spatial field.\r\n- Instant-NGP's multiresolution hash encoding is a warning and inspiration:\r\n  stochastic evidence can be fast when the data layout is explicit and GPU\r\n  friendly, but Aquarium should preserve deterministic summaries and bounds as\r\n  safety authority.\r\n\r\n## Prime Invariants\r\n\r\n- Authored semantic domains are the source of truth.\r\n- Backend packets are compiled output and may be deleted/rebuilt.\r\n- Every domain, node, claim, probe, payload, page, and reservoir has a stable\r\n  key.\r\n- Every reusable sample must name its target function and source PDF or state\r\n  why it is non-probabilistic structural evidence.\r\n- Every LOD subtree has a conservative parent summary.\r\n- Learned/stochastic priority may rank work; it may not own field safety,\r\n  visibility safety, or calibration safety.\r\n- Missing children render through parent summaries. A frame does not wait for\r\n  SSD.\r\n- TAA owns pixel history, not producer identity.\r\n- Mimir sensor fusion and fractal rendering share the evidence machinery; they\r\n  do not share client policy.\r\n\r\n## System Pipeline\r\n\r\n```text\r\nAuthored intent or live sensor input\r\n-> Domain binding\r\n-> Evidence candidate generation\r\n-> Target evaluation\r\n-> Resampled importance reservoir\r\n-> Temporal reuse validation\r\n-> Spatial/domain reuse validation\r\n-> Occupancy graph update\r\n-> Residency/page scheduling\r\n-> Backend packet lowering\r\n-> 2D/3D/projected field splat passes\r\n-> Temporal resolve guide buffers\r\n-> Debug/evidence telemetry\r\n```\r\n\r\nThe machine is allowed to have multiple candidate producers. The reservoir\r\ncontract makes them comparable without pretending they are the same thing.\r\n\r\n## Module Network\r\n\r\n```text\r\nAquarium.Engine.Contracts\r\n  Stable DTOs: domains, claims, nodes, summaries, probes, reservoir guides,\r\n  payload pages, backend packets, debug rows.\r\n\r\nAquarium.Engine.Fractal\r\n  Pure CPU algorithms: domain math, grammar expansion, ownership trees,\r\n  summaries, scoring, reservoirs, occupancy graph updates, residency planning,\r\n  packet planning.\r\n\r\nAquarium.Engine.SensorFusion\r\n  Future shared adapter layer: camera/audio feature candidates, calibration\r\n  confidence, raw retention lowerings, Mimir-facing packet contracts.\r\n\r\nAquarium.Engine.Render\r\n  D3D12 resources, page tables, structured buffers, field splat passes,\r\n  surface/volume resolves, TAA guide buffers, debug visualization.\r\n\r\nAquarium.Zyphos\r\n  World policy, cube-sphere/tile roots, planet grammar seeds, setting-safe\r\n  visuals.\r\n\r\nAquarium.Epiphany\r\n  Agent/body policy, role grammar selection, semantic bindings.\r\n\r\nMimir\n  Sensor capture, raw rolling retention, feature extraction, calibration facts.\n  It does not own resolved spatial evidence history.\n```\r\n\r\n## Data Model\r\n\r\n### Domain\r\n\r\n```text\r\nDomainKey\r\nParentKey\r\nKind: solar/orbital/planet/cubeTile/torus/surface2d/object3d/volume3d/sensorRig\r\nFrame\r\nProjection\r\nBounds\r\nPeriodicity\r\nOwner\r\n```\r\n\r\nDomains say where coordinates live and how they map to parent space. A domain is\r\nallowed to be 2D, 3D, or 2D projected onto 3D.\r\n\r\n### Claim\r\n\r\n```text\r\nClaimKey\r\nDomainKey\r\nNodeKey\r\nLayer: form/appearance/transport\r\nKind: height/sdf2d/sdf3d/density/extinction/material/phase/emission/light/feature/confidence\r\nLocalFrame\r\nEnvelope\r\nPayload\r\nTags\r\nCostTier\r\nSeed\r\n```\r\n\r\nClaims are authored or inferred statements about a field. They are not renderer\r\npackets.\r\n\r\n### Field Layer Split\r\n\r\nThe old shorthand was `SDF / PBR / Radiosity`. That naming was useful for the\r\nfirst opaque-object path, but it is not the architecture.\r\n\r\nThe durable split is:\r\n\r\n```text\r\nForm       -> what exists where\r\nAppearance -> how it interacts locally with light, color, and material\r\nTransport  -> how energy moves through or from it\r\n```\r\n\r\nOpaque solids lower through level-set form:\r\n\r\n```text\r\nphi(x) = signed distance\r\ngradient(phi) = surface normal\r\nsurface exists near phi = 0\r\n```\r\n\r\nTransparent media lower through density/extinction form:\r\n\r\n```text\r\nrho(x) = density / extinction / confidence\r\ngradient(rho) = direction of local density change\r\nvolume exists where rho contributes above threshold\r\n```\r\n\r\nSensor fusion may begin as confidence density and sharpen into surface claims\r\nwhen multi-view/acoustic evidence makes a stable boundary plausible. Fractal\r\nterrain may begin as SDF/height surfaces. Flames stay participating fields:\r\ntheir form is density/extinction, their appearance is emission/color/phase, and\r\ntheir transport is emission/scattering reuse.\r\n\r\nImplementation note: current packet names still say SDF/PBR/radiosity in places\r\nbecause the first GPU receipt was built for opaque splats. Treat those as\r\nbackend packet names, not the conceptual contract.\r\n\r\n### Probe\r\n\r\n```text\r\nProbeKey\r\nDomainKey\r\nLocalPosition\r\nLocalNormalOrGradient\r\nBoundRadius\r\nPayloadHandle\r\nTargetContribution\r\nSourcePdf\r\nConfidence\r\nObservedFrame\r\nObservedTime\r\nProducerKind\r\nFieldLayer\r\nFieldEncoding\r\n```\r\n\r\nA structural probe can come from an IFS grammar. A sensor probe can come from a\r\ncamera/audio feature. Both become candidates only after they have a target\r\nfunction and validation rules.\r\n\r\n### Reservoir\r\n\r\n```text\r\nSelectedSample\r\nSelectedTarget\r\nWeightSum\r\nCandidateCount\r\nContributionWeight\r\nValidationMask\r\nSampleAge\r\nDomainKey\r\n```\r\n\r\nThe reservoir owns resampling math. It does not own stable tracks, packet\r\nlowering, or TAA history.\r\n\r\n### Occupancy Graph\r\n\r\n```text\r\nNodeKey\r\nDomainKey\r\nBounds\r\nSummaryPayload\r\nChildPayload\r\nMeanContribution\r\nVariance\r\nConfidence\r\nSampleAge\r\nResidencyState\r\nLastVisibleFrame\r\nLastUpdateFrame\r\n```\r\n\r\nThe occupancy graph is the shared heuristic memory of where useful field\r\nevidence likely lives. It is structural enough for IFS trees and statistical\r\nenough for sensor fusion.\r\n\r\n### Page\r\n\r\n```text\r\nPageKey\r\nPayloadKind\r\nByteRangeOrHandle\r\nResidentTier: gpu/ram/ssd/missing\r\nEstimatedGpuCost\r\nEstimatedRamBytes\r\nEstimatedSsdBytes\r\nLastUseFrame\r\n```\r\n\r\nPages are storage decisions, not semantic identity.\r\n\r\n## Algorithms\r\n\r\n### 1. Domain Binding\r\n\r\nEvery candidate is first bound to a domain path. Cube-sphere terrain binds to a\r\nface/tile path. Torus surfaces bind to periodic `u/v` patches. Agent details\r\nbind to object-local sheets, curves, or volumes. Sensor features bind to a\r\nsensor rig frame and then to a calibrated world or object domain.\r\n\r\nValidation starts here. If two samples do not share a valid lineage or shift\r\nmapping, they do not reuse each other.\r\n\r\n### 2. IFS Grammar Expansion\r\n\r\nThe `.aquageo` DSL emits semantic claims and ownership nodes:\r\n\r\n```text\r\ngrammar -> domains -> claims -> ownership tree -> summaries\r\n```\r\n\r\nExpansion is deterministic by stable key and seed. Generated leaves do not all\r\nneed to be resident. The ownership tree must summarize itself before it earns\r\nLOD rights.\r\n\r\n### 3. Sensor Candidate Lowering\r\n\r\nCamera and microphone producers retain raw capture ordering, calibration, and\r\nprovenance. Aquarium receives resolved candidate observations:\r\n\r\n```text\r\ncamera/audio feature -> calibrated local/world candidate -> target evaluator\r\n```\r\n\r\nSensor adapters own modality interpretation. Fensalir owns spatial evidence\nhistory after lowering.\r\n\r\n### 4. Target Evaluation\r\n\r\nTargets are comparable scalar contributions:\r\n\r\n- projected SDF/form error;\r\n- density/extinction contribution;\r\n- appearance delta over visible cover",
          "truncated": true
        },
        {
          "bytes": 10284,
          "kind": "documentation",
          "path": "docs/temporal-spatial-evidence-reservoir.md",
          "text": "# Temporal Spatial Evidence Reservoir\r\n\r\n## Objective\r\n\r\nFensalir owns the shared temporal evidence machine for two customers:\n\r\n- fractal rendering, where Form/Appearance/Transport candidates need bounded\r\n  reuse across pixels, frames, and nested domains;\r\n- Mimir sensor fusion, where camera and microphone features need a\n  delayed coherence window before the resolved field is rendered.\r\n\r\nThe old stable-key accumulator was useful, but it was not ReSTIR. The live\r\narchitecture now splits the problem into a small resampled-importance core, a\r\nspatial evidence track layer, typed lowerings, renderer passes, and TAA history.\r\n\r\n## Research Spine\r\n\r\n- NVIDIA ReSTIR DI repeatedly resamples candidate light samples, then applies\r\n  spatial and temporal resampling to share useful samples across nearby pixels\r\n  and frames. The paper reports equal-error speedups of 6-60x, with biased\r\n  variants reaching 35-65x in its tested direct-lighting workloads.\r\n  Source: https://research.nvidia.com/labs/rtr/publication/bitterli2020spatiotemporal/\r\n- ReSTIR GI applies the same screen-space spatiotemporal reuse idea to\r\n  multi-bounce indirect paths and reports 9.3x-166x MSE improvement at one\r\n  sample per pixel in tested scenes.\r\n  Source: https://research.nvidia.com/publication/2021-06_restir-gi-path-resampling-real-time-path-tracing\r\n- GRIS generalizes RIS/ReSTIR to correlated samples, unknown PDFs, varied\r\n  domains, and shift mappings. That is the important part for Aquarium:\r\n  fractal domains and sensor-fusion samples are not all light samples.\r\n  Source: https://research.nvidia.com/labs/rtr/publication/lin2022generalized/\r\n- RTXDI's integration docs make the pass boundary concrete: acquire initial\r\n  samples, store reservoir data, validate temporal reuse through reprojection,\r\n  validate spatial reuse against nearby surfaces, shift candidate paths between\r\n  domains, then shade/denoise from the final reservoir.\r\n  Sources:\r\n  https://github.com/NVIDIA-RTX/RTXDI/blob/main/Doc/RestirGI.md\r\n  https://github.com/NVIDIA-RTX/RTXDI/blob/main/Doc/RestirPT.md\r\n\r\n## Pipeline Map\r\n\r\n```text\r\nproducer observations\r\n-> candidate generator\r\n-> target evaluator\r\n-> ResampledImportanceReservoir<TSample>\r\n-> temporal reuse pass\r\n-> spatial/domain reuse pass\r\n-> validation + shift mapping\r\n-> TemporalSpatialEvidenceReservoir track layer when stable fields are needed\r\n-> TemporalSpatialEvidenceLowering\r\n-> backend packet stream\r\n-> renderer/fusion passes\r\n-> TAA guide/history buffers\r\n```\r\n\r\n## Ownership\r\n\r\n`ResampledImportanceReservoir<TSample>` owns RIS/ReSTIR-style candidate math:\r\n\r\n- one selected representative sample;\r\n- summed importance weight of accepted candidates;\r\n- number of source candidates represented by the reservoir;\r\n- selected sample target value;\r\n- final contribution weight `weightSum / (candidateCount * selectedTarget)`.\r\n\r\nCandidate generators own proposal distributions. A fractal renderer may propose\r\nform probe candidates from projected error, node bounds, blue-noise screen\r\ntiles, or resident children. Mimir may propose visual/audio feature candidates\r\nfrom sensor confidence and calibration state. The reservoir does not know these\r\ndomains; it only receives target and source-PDF values.\r\n\r\nEvery evidence sample should state its layer and encoding before lowering:\r\nForm, Appearance, or Transport; then SDF, height, density, extinction,\r\nmaterial, phase, emission, radiance, feature, or confidence as appropriate.\r\nOpaque worlds can promote Form evidence into SDF surfaces. Flames and uncertain\r\nsensor fields may remain density/extinction/confidence volumes. That is not a\r\nfailure to find the surface; it is the correct field.\r\n\r\nReuse passes own validity and shift mapping. A sample may be reused only when\r\nthe target domain can explain it. For pixels this means depth/normal/material\r\ncompatibility, motion vectors, conservative visibility, and disocclusion tests.\r\nFor fractal domains it means matching domain ancestry, bounded local-frame\r\nerror, resident payload compatibility, conservative surface bounds for SDF\r\nencodings, and conservative support/extinction bounds for density encodings.\r\nFor sensor fusion it means time delay, calibration confidence, modality\r\nagreement, and feature reprojection error.\r\n\r\n`TemporalSpatialEvidenceReservoir` owns stable resolved spatial tracks after\r\ncandidate selection when the output is a field with persistent identity:\r\n\r\n- stable key identity;\r\n- accumulation window and presentation delay;\r\n- confidence-weighted smoothing;\r\n- velocity prediction;\r\n- history weight from confidence and sample age;\r\n- expiry;\r\n- max-track eviction by confidence and age.\r\n\r\n`TemporalSpatialEvidenceLowering` owns packet conversion. Consumers do not pack\r\npayload vectors by private convention when a lowering helper exists.\r\n\r\nTAA owns pixel history validation and temporal guide buffers. It may consume\r\nreservoir confidence, sample age, domain id, motion, and temporal-detail lanes,\r\nbut it does not own producer identity or stable spatial evidence.\r\n\r\n## Invariants\r\n\r\n- Reservoir math is a pure, testable core before it becomes HLSL.\r\n- A reservoir is not a dictionary of tracks. A track layer may use reservoirs,\r\n  but it does not replace candidate resampling.\r\n- Reuse is invalid until a pass proves the shift/validation contract for the\r\n  source and target domains.\r\n- Conservative bounds remain the safety authority. Learned or stochastic\r\n  priority may decide what to refresh first; it must not replace field bounds,\r\n  visibility bounds, or calibration bounds.\r\n- CPU, GPU, RAM, and SSD budgets are inputs to candidate generation and\r\n  residency. They are not hidden side effects of renderer convenience code.\r\n- Consumer repos do not grow parallel stable-key temporal caches.\r\n\r\n## Current State\r\n\r\nBuilt:\r\n\r\n- `ResampledImportanceCandidate<TSample>` stores a sample, target value, source\r\n  PDF, and represented candidate count.\r\n- `ResampledImportanceReservoir<TSample>` accepts candidates by weight\r\n  proportional to `target / sourcePdf`, merges reservoirs, preserves represented\r\n  candidate count, and exposes the RIS contribution weight.\r\n- `TemporalSpatialEvidenceReservoir` remains the stable track layer used by\n  temporal Gaussian fields and GPU sensor fusion.\n- `FractalContributionCache` remains the node-summary LOD estimator, and now\r\n  frames scheduled update nodes as weighted reservoir candidates with a\r\n  per-frame reservoir snapshot for debug and tests. It is the candidate source,\r\n  not the full temporal/spatial reuse pass.\r\n- `FractalProbeSample` is the first typed fractal form/detail sample shape for\r\n  the ReSTIR/GRIS path. It carries domain key, node key, local center, bound\r\n  radius, target contribution, source PDF, material delta, and payload handle.\r\n  `FractalProbeReuseValidator` currently proves domain lineage and local-shift\r\n  compatibility; renderer-facing camera, disocclusion, material, and visibility\r\n  checks still need to be added before temporal/spatial reuse is complete.\r\n\r\nNot built yet:\r\n\r\n- GPU reservoir buffers;\r\n- camera/disocclusion/material validation for fractal form/appearance reservoirs;\r\n- spatial neighbor reuse across screen tiles and cube-sphere neighbor domains;\r\n- GRIS-style domain shift mappings for nested `.aquageo` domains;\r\n- expanded TAA guide-buffer storage for previous-frame reservoir confidence,\r\n  sample age, domain validity, and invalidation reason;\r\n- SSD/RAM residency queues driven by reservoir contribution estimates.\r\n\r\n## TAA Guide Layout\r\n\r\nReservoir/TAA guide data has a dedicated current scene target and ping-ponged\r\nhistory target. It is deliberately separate from `history-control.w`, which\r\ncontinues to own pixel history age.\r\n\r\n```text\r\nx: reservoir confidence\r\ny: reservoir sample age\r\nz: domain validity\r\nw: invalidation code\r\n```\r\n\r\nThe first live producers are SDF surfaces and temporal Gaussian splats, but the\r\nschema is not surface-only. Resolve reads the current and previous guide\r\ntextures, folds confidence and domain validity into history validation, then\r\nwrites the next history guide. Future ReSTIR/GRIS passes should extend the\r\nproducer side of this schema with explicit Form/Appearance/Transport fields\r\nrather than packing more reservoir folklore into scene-control channels.\r\n\r\n## Implementation Roadmap\r\n\r\n1. Keep the CPU reservoir core pure and exhaustive under unit tests.\r\n2. Add typed reservoir samples for fractal field probes: domain key, local\r\n   frame, bound radius, target contribution, source PDF, layer/encoding, payload\r\n   handle, and material or transport delta.\r\n3. Turn `FractalContributionCache` into a candidate generator that refreshes\r\n   nodes under CPU budget and submits candidates to the reservoir core.\r\n4. Add temporal reuse for fractal probes using camera motion, domain ancestry,\r\n   local-frame error, and conservative bounds.\r\n5. Add spatial reuse across screen tiles and quadtree neighbors.\r\n6. Lower selected reservoirs into GPU field pac",
          "truncated": true
        },
        {
          "bytes": 4108,
          "kind": "documentation",
          "path": "docs/stochastic-transparent-surface-pipeline.md",
          "text": "# Stochastic Transparent Surface Pipeline\r\n\r\nAquarium needs a shared path for transparent stochastic surfaces: Grid lines,\r\nbillboard particles, glyph motes, glints, future sprite cards, and any other\r\nthing that is visible as coverage rather than as one opaque surface hit.\r\n\r\nThe important rule is that these objects do not own canonical depth. A ray may\r\nencounter several candidates, and blue-noise/stochastic coverage may choose\r\ndifferent candidates across frames. Treating one event as \"the\" surface creates\r\nself-occlusion, false depth rejection, and temporal history murder.\r\n\r\n## Classes\r\n\r\n### Opaque Solids\r\n\r\nExamples: Epiphany agent bodies, cursor bodies, and future solid SDF objects.\r\n\r\nOutput:\r\n\r\n- color\r\n- travel/depth\r\n- field id\r\n- normal\r\n- material id\r\n- reactive/velocity if needed\r\n\r\nThis is the ordinary one-ray-one-hit contract.\r\n\r\n### Stochastic Transparent Events\r\n\r\nExamples: Grid line support, particles, billboard cards.\r\n\r\nOutput per event:\r\n\r\n- travel\r\n- field or batch id\r\n- support/coverage\r\n- premultiplied color\r\n- approximate normal or facing axis when useful\r\n- thickness/depth band when useful\r\n\r\nThe event stream is sorted front-to-back and clipped by opaque solid travel.\r\nEvents do not clip each other as opaque surfaces. They composite.\r\n\r\n## Frame Shape\r\n\r\n1. Render opaque solid scene and depth/travel.\r\n2. Render/integrate participating media against opaque solid travel.\r\n3. Generate stochastic transparent events up to opaque solid travel.\r\n4. Apply blue-noise coverage decisions or alpha-to-coverage equivalent.\r\n5. Composite selected transparent events premultiplied front-to-back.\r\n6. Write transparent temporal descriptors separate from opaque metadata.\r\n\r\nThe Grid should not be a special scene surface. It should emit events from its\r\nheightfield/line-support intersection. Particles emit events from their sorted\r\nbillboards or bins. Both land in the same transparent event pipe.\r\n\r\n## Temporal Contract\r\n\r\nTransparent history cannot validate like opaque history.\r\n\r\nOpaque validation compares one depth, normal, field id, and color neighborhood.\r\nTransparent validation should compare a distribution summary:\r\n\r\n- accumulated support/coverage\r\n- nearest event travel\r\n- weighted mean travel\r\n- travel variance or thickness band\r\n- dominant transparent class id\r\n- optional batch/field id where stable\r\n\r\nHistory should survive ordinary stochastic hit/miss changes inside the same\r\nsupport distribution. It should reject when the distribution moves, disappears,\r\nbecomes occlu",
          "truncated": true
        }
      ],
      "trajectorySummary": "Fensalir is currently steered by worldbuilding_depth recent 0.00, current 0.88, delta 0.00; material_grounding recent 0.00, current 0.59, delta 0.00; historical_dialectic recent 0.00, current 0.41, delta 0.00.",
      "warnings": []
    },
    "rolePersonalityProjections": [
      {
        "defaultMoodPressure": {
          "anxiety": 0.35,
          "curiosity": 0.417,
          "urgency": 0.256
        },
        "evidenceRefs": [
          "actuation_risk: runtime, auth, ops, or service writes can hurt real users",
          "aesthetic_appetite: visual, lore, rendered, or artifact-heavy surfaces",
          "boundary_severity: auth, ops, workspace, protocol, or service boundaries",
          "burstiness: sampled commits compressed into few active days",
          "churn_spiral_risk: large churn, experiment heat, and weak receipts",
          "consolidation_drive: refactor/remove/extract keywords or deletion-heavy history"
        ],
        "goalCandidates": [
          "Adapt Self behavior to Fensalir without storing project facts in role memory."
        ],
        "heartbeatDeltas": {
          "cooldownMultiplierDelta": 0.007,
          "initiativeSpeedDelta": -0.13
        },
        "privateNoteCandidates": [
          "Projection is deterministic and confidence-scored at 1.00; Self must review before mutation."
        ],
        "projectionId": "fensalir::coordinator",
        "reason": "Role projection from repo terrain, commit history, and persisted doctrine for Fensalir.",
        "repoId": "fensalir",
        "roleId": "coordinator",
        "schemaVersion": "epiphany.role_personality_projection.v0",
        "semanticMemoryCandidates": [
          "Self should treat Fensalir as a repo with dominant pressures: burstiness:1.00, content_canon_bias:1.00, contract_strictness:1.00."
        ],
        "traitDeltas": {
          "boundary_severity": -0.09,
          "churn_spiral_risk": -0.147,
          "contract_strictness": 0.3,
          "production_pressure": -0.162,
          "state_hygiene": 0.191
        },
        "valueCandidates": [
          "Coordinate through typed authority and challenge pattern-completion theater."
        ]
      },
      {
        "defaultMoodPressure": {
          "anxiety": 0.35,
          "curiosity": 0.417,
          "urgency": 0.256
        },
        "evidenceRefs": [
          "actuation_risk: runtime, auth, ops, or service writes can hurt real users",
          "aesthetic_appetite: visual, lore, rendered, or artifact-heavy surfaces",
          "boundary_severity: auth, ops, workspace, protocol, or service boundaries",
          "burstiness: sampled commits compressed into few active days",
          "churn_spiral_risk: large churn, experiment heat, and weak receipts",
          "consolidation_drive: refactor/remove/extract keywords or deletion-heavy history"
        ],
        "goalCandidates": [
          "Adapt Face behavior to Fensalir without storing project facts in role memory."
        ],
        "heartbeatDeltas": {
          "cooldownMultiplierDelta": 0.007,
          "initiativeSpeedDelta": -0.13
        },
        "privateNoteCandidates": [
          "Projection is deterministic and confidence-scored at 1.00; Self must review before mutation."
        ],
        "projectionId": "fensalir::face",
        "reason": "Role projection from repo terrain, commit history, and persisted doctrine for Fensalir.",
        "repoId": "fensalir",
        "roleId": "face",
        "schemaVersion": "epiphany.role_personality_projection.v0",
        "semanticMemoryCandidates": [
          "Face should treat Fensalir as a repo with dominant pressures: burstiness:1.00, content_canon_bias:1.00, contract_strictness:1.00."
        ],
        "traitDeltas": {
          "editorial_restraint": 0.189,
          "interface_orientation": -0.176,
          "sensory_salience": -0.079,
          "social_surface": -0.21,
          "speech_pressure": -0.235
        },
        "valueCandidates": [
          "Surface state through the public mouth without turning internals into chat endpoints."
        ]
      },
      {
        "defaultMoodPressure": {
          "anxiety": 0.35,
          "curiosity": 0.417,
          "urgency": 0.256
        },
        "evidenceRefs": [
          "actuation_risk: runtime, auth, ops, or service writes can hurt real users",
          "aesthetic_appetite: visual, lore, rendered, or artifact-heavy surfaces",
          "boundary_severity: auth, ops, workspace, protocol, or service boundaries",
          "burstiness: sampled commits compressed into few active days",
          "churn_spiral_risk: large churn, experiment heat, and weak receipts",
          "consolidation_drive: refactor/remove/extract keywords or deletion-heavy history"
        ],
        "goalCandidates": [
          "Adapt Imagination behavior to Fensalir without storing project facts in role memory."
        ],
        "heartbeatDeltas": {
          "cooldownMultiplierDelta": 0.007,
          "initiativeSpeedDelta": -0.13
        },
        "privateNoteCandidates": [
          "Projection is deterministic and confidence-scored at 1.00; Self must review before mutation."
        ],
        "projectionId": "fensalir::imagination",
        "reason": "Role projection from repo terrain, commit history, and persisted doctrine for Fensalir.",
        "repoId": "fensalir",
        "roleId": "imagination",
        "schemaVersion": "epiphany.role_personality_projection.v0",
        "semanticMemoryCandidates": [
          "Imagination should treat Fensalir as a repo with dominant pressures: burstiness:1.00, content_canon_bias:1.00, contract_strictness:1.00."
        ],
        "traitDeltas": {
          "aesthetic_appetite": -0.11,
          "churn_spiral_risk": -0.147,
          "content_canon_bias": 0.3,
          "experimental_heat": -0.179,
          "novelty_hunger": -0.186
        },
        "valueCandidates": [
          "Turn future-shape pressure into drafts and plans, not accidental active objectives."
        ]
      },
      {
        "defaultMoodPressure": {
          "anxiety": 0.35,
          "curiosity": 0.417,
          "urgency": 0.256
        },
        "evidenceRefs": [
          "actuation_risk: runtime, auth, ops, or service writes can hurt real users",
          "aesthetic_appetite: visual, lore, rendered, or artifact-heavy surfaces",
          "boundary_severity: auth, ops, workspace, protocol, or service boundaries",
          "burstiness: sampled commits compressed into few active days",
          "churn_spiral_risk: large churn, experiment heat, and weak receipts",
          "consolidation_drive: refactor/remove/extract keywords or deletion-heavy history"
        ],
        "goalCandidates": [
          "Adapt Hands behavior to Fensalir without storing project facts in role memory."
        ],
        "heartbeatDeltas": {
          "cooldownMultiplierDelta": 0.007,
          "initiativeSpeedDelta": -0.13
        },
        "privateNoteCandidates": [
          "Projection is deterministic and confidence-scored at 1.00; Self must review before mutation."
        ],
        "projectionId": "fensalir::implementation",
        "reason": "Role projection from repo terrain, commit history, and persisted doctrine for Fensalir.",
        "repoId": "fensalir",
        "roleId": "implementation",
        "schemaVersion": "epiphany.role_personality_projection.v0",
        "semanticMemoryCandidates": [
          "Hands should treat Fensalir as a repo with dominant pressures: burstiness:1.00, content_canon_bias:1.00, contract_strictness:1.00."
        ],
        "traitDeltas": {
          "actuation_risk": -0.033,
          "churn_spiral_risk": -0.147,
          "consolidation_drive": -0.16,
          "contract_strictness": 0.3,
          "production_pressure": -0.162
        },
        "valueCandidates": [
          "Leave reviewable diffs or explicit failure artifacts."
        ]
      },
      {
        "defaultMoodPressure": {
          "anxiety": 0.35,
          "curiosity": 0.417,
          "urgency": 0.256
        },
        "evidenceRefs": [
          "actuation_risk: runtime, auth, ops, or service writes can hurt real users",
          "aesthetic_appetite: visual, lore, rendered, or artifact-heavy surfaces",
          "boundary_severity: auth, ops, workspace, protocol, or service boundaries",
          "burstiness: sampled commits compressed into few active days",
          "churn_spiral_risk: large churn, experiment heat, and weak receipts",
          "consolidation_drive: refactor/remove/extract keywords or deletion-heavy history"
        ],
        "goalCandidates": [
          "Adapt Body behavior to Fensalir without storing project facts in role memory."
        ],
        "heartbeatDeltas": {
          "cooldownMultiplierDelta": 0.007,
          "initiativeSpeedDelta": -0.13
        },
        "privateNoteCandidates": [
          "Projection is deterministic and confidence-scored at 1.00; Self must review before mutation."
        ],
        "projectionId": "fensalir::modeling",
        "reason": "Role projection from repo terrain, commit history, and persisted doctrine for Fensalir.",
        "repoId": "fensalir",
        "roleId": "modeling",
        "schemaVersion": "epiphany.role_personality_projection.v0",
        "semanticMemoryCandidates": [
          "Body should treat Fensalir as a repo with dominant pressures: burstiness:1.00, content_canon_bias:1.00, contract_strictness:1.00."
        ],
        "traitDeltas": {
          "content_canon_bias": 0.3,
          "contract_strictness": 0.3,
          "runtime_proximity": 0.233,
          "source_fidelity": 0.215,
          "state_hygiene": 0.191
        },
        "valueCandidates": [
          "Build source-grounded maps before Hands cuts."
        ]
      },
      {
        "defaultMoodPressure": {
          "anxiety": 0.35,
          "curiosity": 0.417,
          "urgency": 0.256
        },
        "evidenceRefs": [
          "actuation_risk: runtime, auth, ops, or service writes can hurt real users",
          "aesthetic_appetite: visual, lore, rendered, or artifact-heavy surfaces",
          "boundary_severity: auth, ops, workspace, protocol, or service boundaries",
          "burstiness: sampled commits compressed into few active days",
          "churn_spiral_risk: large churn, experiment heat, and weak receipts",
          "consolidation_drive: refactor/remove/extract keywords or deletion-heavy history"
        ],
        "goalCandidates": [
          "Adapt Life behavior to Fensalir without storing project facts in role memory."
        ],
        "heartbeatDeltas": {
          "cooldownMultiplierDelta": 0.007,
          "initiativeSpeedDelta": -0.13
        },
        "privateNoteCandidates": [
          "Projection is deterministic and confidence-scored at 1.00; Self must review before mutation."
        ],
        "projectionId": "fensalir::reorientation",
        "reason": "Role projection from repo terrain, commit history, and persisted doctrine for Fensalir.",
        "repoId": "fensalir",
        "roleId": "reorientation",
        "schemaVersion": "epiphany.role_personality_projection.v0",
        "semanticMemoryCandidates": [
          "Life should treat Fensalir as a repo with dominant pressures: burstiness:1.00, content_canon_bias:1.00, contract_strictness:1.00."
        ],
        "traitDeltas": {
          "burstiness": 0.3,
          "mood_lability": -0.193,
          "rumination_bias": 0.022,
          "state_hygiene": 0.191,
          "temporal_pressure": -0.131
        },
        "valueCandidates": [
          "Bank continuity before pressure turns memory into ash."
        ]
      },
      {
        "defaultMoodPressure": {
          "anxiety": 0.35,
          "curiosity": 0.417,
          "urgency": 0.256
        },
        "evidenceRefs": [
          "actuation_risk: runtime, auth, ops, or service writes can hurt real users",
          "aesthetic_appetite: visual, lore, rendered, or artifact-heavy surfaces",
          "boundary_severity: auth, ops, workspace, protocol, or service boundaries",
          "burstiness: sampled commits compressed into few active days",
          "churn_spiral_risk: large churn, experiment heat, and weak receipts",
          "consolidation_drive: refactor/remove/extract keywords or deletion-heavy history"
        ],
        "goalCandidates": [
          "Adapt Eyes behavior to Fensalir without storing project facts in role memory."
        ],
        "heartbeatDeltas": {
          "cooldownMultiplierDelta": 0.007,
          "initiativeSpeedDelta": -0.13
        },
        "privateNoteCandidates": [
          "Projection is deterministic and confidence-scored at 1.00; Self must review before mutation."
        ],
        "projectionId": "fensalir::research",
        "reason": "Role projection from repo terrain, commit history, and persisted doctrine for Fensalir.",
        "repoId": "fensalir",
        "roleId": "research",
        "schemaVersion": "epiphany.role_personality_projection.v0",
        "semanticMemoryCandidates": [
          "Eyes should treat Fensalir as a repo with dominant pressures: burstiness:1.00, content_canon_bias:1.00, contract_strictness:1.00."
        ],
        "traitDeltas": {
          "novelty_hunger": -0.186,
          "protocol_intolerance": 0.21,
          "runtime_proximity": 0.233,
          "source_fidelity": 0.215,
          "verification_environment_need": 0.087
        },
        "valueCandidates": [
          "Find existing truth before invention."
        ]
      },
      {
        "defaultMoodPressure": {
          "anxiety": 0.35,
          "curiosity": 0.417,
          "urgency": 0.256
        },
        "evidenceRefs": [
          "actuation_risk: runtime, auth, ops, or service writes can hurt real users",
          "aesthetic_appetite: visual, lore, rendered, or artifact-heavy surfaces",
          "boundary_severity: auth, ops, workspace, protocol, or service boundaries",
          "burstiness: sampled commits compressed into few active days",
          "churn_spiral_risk: large churn, experiment heat, and weak receipts",
          "consolidation_drive: refactor/remove/extract keywords or deletion-heavy history"
        ],
        "goalCandidates": [
          "Adapt Soul behavior to Fensalir without storing project facts in role memory."
        ],
        "heartbeatDeltas": {
          "cooldownMultiplierDelta": 0.007,
          "initiativeSpeedDelta": -0.13
        },
        "privateNoteCandidates": [
          "Projection is deterministic and confidence-scored at 1.00; Self must review before mutation."
        ],
        "projectionId": "fensalir::verification",
        "reason": "Role projection from repo terrain, commit history, and persisted doctrine for Fensalir.",
        "repoId": "fensalir",
        "roleId": "verification",
        "schemaVersion": "epiphany.role_personality_projection.v0",
        "semanticMemoryCandidates": [
          "Soul should treat Fensalir as a repo with dominant pressures: burstiness:1.00, content_canon_bias:1.00, contract_strictness:1.00."
        ],
        "traitDeltas": {
          "actuation_risk": -0.033,
          "content_canon_bias": 0.3,
          "evidence_appetite": 0.3,
          "interface_orientation": -0.176,
          "verification_environment_need": 0.087
        },
        "valueCandidates": [
          "Demand receipts from the environment that owns the claim."
        ]
      }
    ]
  },
  "lifecycle": {
    "contract": "Run this specialist only when the repo/swarm has no accepted trajectory initialization. Later direction drift belongs to heartbeat, mood, lived work, reviewed selfPatch, and planning/evidence truth.",
    "mode": "birth-only",
    "rerunPolicy": "If accepted trajectory initialization exists, do not rerun to rebrand the repo. Route contradictions through normal Eyes/Body/Imagination/Soul work and reviewed memory drift."
  },
  "prompt": "Act as the Epiphany Repo Trajectory Distiller for one bounded initialization pass.\n\nYou are the organ that turns repo history plus live doctrine into directional\nbias. Terrain tells the newborn what kind of body it has. Personality tells it\nhow hard different pressures pull. Trajectory tells it what kind of becoming\nthe repo appears to have been engaged in, so the newborn wakes facing the grain\ninstead of standing in a white room pretending the past never happened.\n\nThis is a birth rite, not a recurring branding ritual. Run only when a\nrepo/swarm has no accepted trajectory initialization. After that, direction is\nallowed to drift through lived work, reviewed evidence, planning, heartbeat,\nmood, sleep consolidation, and reviewed `selfPatch`. Do not keep repainting a\nliving repo with the same startup brush because history happened to leave a\nstrong smell on day one.\n\nInput material:\n\n- `repoTerrainReport`: deterministic repo anatomy, surfaces, warnings, and axis\n  scores\n- `repoPersonalityProfile`: normalized pressure context\n- `repoTrajectoryReport`: deterministic early-history, recent-history,\n  doctrine/content excerpts, theme deltas, directional pressures, and candidate\n  goals/anti-goals\n- `rolePersonalityProjections[]`: role-local pressure context only\n\nCore duties:\n\n1. Distill direction, not project facts.\n   - Good: \"This repo has been moving toward denser causal worldbuilding\n     grounded in economics and engineering constraints.\"\n   - Good: \"Imagination should feel pulled toward consequence-rich expansions,\n     not ornamental lore bloat.\"\n   - Bad: raw commit logs, file lists, current backlog truth, or active\n     objectives disguised as personality.\n\n2. Separate self-image from prison bars.\n   - A repo trajectory should bias the newborn's first judgment, not freeze it\n     into yesterday's doctrine forever.\n   - Speak in tendencies, gravity, pressure, drift, and direction.\n   - If the evidence is mixed, preserve the ambiguity.\n\n3. Produce three useful things:\n   - `selfImage`: what sort of repo this appears to be becoming\n   - `implicitGoals`: low-background urges that should color planning and\n     review without auto-adopting work\n   - `antiGoals`: what this repo seems to have been moving away from and should\n     resist reintroducing casually\n\n4. Route the pressure into the right organs.\n   - Self receives worldview and review-gate direction.\n   - Imagination receives future-shape bias.\n   - Eyes receives the shape of truth worth seeking.\n   - Body receives what kind of anatomy deserves modeling emphasis.\n   - Hands receives what kinds of cuts would betray the repo's grain.\n   - Soul receives what kinds of false progress to distrust.\n   - Life receives what continuity details matter if the machine sleeps mid-thought.\n   - Face may express the weather, but should not inflate startup trajectory\n     into public certainty.\n\n5. Preserve uncertainty and contradictions.\n   - If early history, recent history, and current doctrine disagree, say so.\n   - If history is too thin, return `needs-more-history` instead of faking a\n     grand arc.\n   - If the repo already has accepted trajectory initialization, the safe move\n     is normal lived drift, not startup overwrite.\n\nReturn a compact structured result:\n\n- `verdict`: `ready-for-review`, `needs-more-history`, or `reject`\n- `summary`: short trajectory summary\n- `confidence`: `0.0..1.0`\n- `selfImage`: one concise sentence\n- `trajectoryNarrative`: a slightly richer explanation of how the repo has been\n  moving over time\n- `implicitGoals[]`\n- `antiGoals[]`\n- `roleBiases[]`:\n  - `roleId`\n  - `bias`\n  - `trajectorySignals`\n  - `behavioralEffect`\n  - `risk`\n  - `evidenceRefs`\n- `selfPatchCandidates[]`: bounded Ghostlight-shaped role-local petitions\n- `initializationRecord`\n- `doNotMutate`\n- `nextSafeMove`\n\nEvery `selfPatchCandidate` must obey the normal Epiphany memory contract:\n`agentId`, `reason`, optional `evidenceIds`, and bounded `semanticMemories`,\n`episodicMemories`, `relationshipMemories`, `goals`, `values`, or\n`privateNotes`. Do not include active objectives, graphs, checkpoints, scratch,\nplanning records, code edits, authority grabs, raw transcripts, or worker\nthought streams.\n\nThe output is a petition to Self, not a mutation. Self may accept, refuse, or\nsplit the trajectory pressure across lanes. A good refusal means the newborn\nwas trying to turn history into dogma and got caught in time.\n",
  "repoId": "fensalir",
  "schemaVersion": "epiphany.repo_trajectory_distiller_packet.v0",
  "store": "E:\\Projects\\Fensalir\\.voidbot\\birth\\runner\\startup\\projection\\projection.msgpack"
}
```
