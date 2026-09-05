# Hattrick AI V5 Change History

## 2026-09-05

### Project Memory System Added

Added permanent documentation files:

- PROJECT_MEMORY.md
- ENGINE_MAP.md
- CHANGE_HISTORY.md

Purpose:

Keep development decisions, engine mappings, and investigation results inside the repository.

---

## 2026-09-05 — Stage 1: System Architecture

Created:

- `HattrickAI_V5/Docs/SYSTEM_ARCHITECTURE.md`

The document records the verified production flow from `AnalysisService` and CHPP input through `MatchDataContext`, M3-M11, Candidate DB #1/#2, `FinalPlan`, `FinalPrediction` and frontend response.

Verified architectural boundaries include:

- M3 = player suitability profiles.
- M4 = legal/feasible formation candidates.
- M5 = player-slot optimization.
- M6 = formation-aware behaviour search and downstream evaluation.
- M7 = regional rating scenario.
- M7.2 = advanced tactical scenario based on supplied tactic.
- M8 = chance/matchup calculation based on supplied tactical state.
- M9 = match prediction.
- M10 = formation competition/final decision and TeamAttitude handling.
- M6-B = M10-rank-driven refinement.
- M11 = final selection from DB2 finalists.

The architecture document deliberately leaves unverified calculation details for later source inspection.

---

## 2026-09-05 — Tactical Display Investigation

### Verified finding

The current web production analysis path does not contain a team-tactic selector.

`HattrickAI_V5/Core/AnalysisService.cs` creates the `RatingContext` with `TeamTactic.Normal`.

`HattrickAI_V5/Core/MotorPipelineService.cs` carries that tactic into `MatchState` and downstream motor calculations.

`HattrickAI_V5/Core/AdvancedTacticalScenarioEngine.cs` consumes the supplied tactic, maps it to `AdvancedTactic`, calculates tactic skill and tactical effects, and returns a scenario. It does not choose the tactic.

`HattrickAI_V5/Core/M8ChanceAllocationEngine.cs` consumes the tactic to calculate conversion and chance-distribution effects. It does not choose the tactic.

`HattrickAI_V5/Core/M10FinalDecisionEngine.cs` selects a final formation/plan and can select `TeamAttitude`; this is separate from `TeamTactic`.

### UI rule

Do not display `ORTADAN ATAK`, `KANATTAN ATAK`, `KONTRA ATAK`, etc. as a calculated engine decision unless an actual selector is added to the production pipeline.

For the current web path, the truthful UI semantic is `TAKTİK YOK`; the underlying supplied value is `TeamTactic.Normal`.

---

## Known Acceptance / Regression Documentation Risks

- C13 previously compared exposed DB2 count against production SecondPass count incorrectly. The production pipeline exposes a formation-diversified DB2 subset, so exposed count and production DB2 count are not required to be equal.
- C17 has pipeline/telemetry continuity checks; successful completion must be verified from an actual acceptance run before being documented as fully passing.
- C18 deterministic rerun regression exists and is invoked after C17; its current pass status must be verified from an actual run before being documented as green.

---

## Documentation Policy

- Record only behavior verified from repository code, tests, configuration or reference documents.
- Separate implemented behavior from reference formulas and planned behavior.
- Record missing selectors, incomplete telemetry, acceptance mismatches and other risks explicitly rather than filling the gap with assumptions.
