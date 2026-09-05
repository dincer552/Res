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

## 2026-09-05 — Tactical Display Investigation

### Goal

Display the real calculated tactic in the Recommended XI card.

### Verified finding

The current web production analysis path does not contain a team-tactic selector.

`HattrickAI_V5/Core/AnalysisService.cs` creates the `RatingContext` with `TeamTactic.Normal`.

`HattrickAI_V5/Core/MotorPipelineService.cs` carries that tactic into `MatchState` and downstream motor calculations.

`HattrickAI_V5/Core/AdvancedTacticalScenarioEngine.cs` consumes the supplied tactic, maps it to `AdvancedTactic`, calculates tactic skill and tactical effects, and returns a scenario. It does not choose the tactic.

`HattrickAI_V5/Core/M8ChanceAllocationEngine.cs` consumes the tactic to calculate conversion and chance-distribution effects. It does not choose the tactic.

`HattrickAI_V5/Core/M10FinalDecisionEngine.cs` selects a final formation/plan and can select `TeamAttitude`; this is separate from `TeamTactic`.

### UI rule resulting from the investigation

Do not display `ORTADAN ATAK`, `KANATTAN ATAK`, `KONTRA ATAK`, etc. as a calculated engine decision unless an actual selector is added to the production pipeline.

For the current web path, the truthful UI semantic is `TAKTİK YOK`; the underlying supplied value is `TeamTactic.Normal`.

### Important unresolved item

A future implementation may add a real tactical selector, but no such selector is documented as existing until it is found in the repository and verified through the production analysis path and tests.

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
