# Hattrick AI V5 Project Memory

## Purpose

This file is the permanent project memory for important architectural decisions, implementation notes, and investigation results.

The goal is to prevent loss of context during future development.

---

# Architecture Memory

## Main Pipeline

M3
↓
M4
↓
M5
↓
M6-A
↓
M7
↓
M7.2
↓
M8
↓
M9
↓
DB1
↓
M10
↓
M6-B
↓
DB2
↓
M11
↓
FinalPlan
↓
Frontend

---

# Development Rules

Before changing any engine:

1. Locate the real calculation source in code.
2. Identify input and output models.
3. Document the change in CHANGE_HISTORY.md.
4. Update ENGINE_MAP.md if motor relationships change.
5. Run related acceptance tests.

Never document a calculation that is not confirmed from code, config, tests, or official reference documents.

---

# Verified Investigation Results

## Tactical Display / Tactical Selection

Goal:

Show the real calculated match tactic in the Recommended XI card, if the production pipeline actually calculates one.

### Verified production behavior — 2026-09-05

The current web analysis path does **not** contain a tactical-selector stage that chooses between tactics such as AttackMiddle or AttackWings.

`HattrickAI_V5/Core/AnalysisService.cs` creates the rating context with:

`new RatingContext(locationEnum, questionnaire.MatchImportance, TeamTactic.Normal)`

Therefore the production analysis pipeline currently enters the motor pipeline with `TeamTactic.Normal`.

`HattrickAI_V5/Core/MotorPipelineService.cs` passes the tactic from `context.RatingContext.Tactic` into `MatchState`. The tactic is then consumed by M7/M7.2/M8 calculations.

`HattrickAI_V5/Core/AdvancedTacticalScenarioEngine.cs` maps the supplied team tactic to `AdvancedTactic`, calculates tactic skill and tactical effects, and returns the resulting scenario. It does **not** choose the team tactic.

`HattrickAI_V5/Core/M8ChanceAllocationEngine.cs` also consumes the selected/supplied tactic to calculate tactic conversion and chance distribution. It does **not** select the tactic.

`HattrickAI_V5/Core/M10FinalDecisionEngine.cs` selects the final formation/plan and match attitude. Its `SelectApproach` logic is about `TeamAttitude` (Normal / PlayItCool / MatchOfTheSeason), not `TeamTactic`.

### Important conclusion

It would be incorrect for the frontend to display `ORTADAN ATAK`, `KANATTAN ATAK`, etc. as a calculated tactic based on the current production analysis path.

For the current web path, the truthful state is:

- Tactical selector: **not implemented / not present in production analysis path**
- Supplied team tactic: **TeamTactic.Normal**
- UI semantic value when no tactical decision exists: **TAKTİK YOK**

Do not hardcode a tactical strategy merely to make the UI look complete.

### Related implementation files

- `HattrickAI_V5/Core/AnalysisService.cs` — creates `RatingContext` with `TeamTactic.Normal`.
- `HattrickAI_V5/Core/MotorPipelineService.cs` — carries the tactic into `MatchState` and downstream motor calculations.
- `HattrickAI_V5/Core/AdvancedTacticalScenarioEngine.cs` — calculates effects of the supplied tactic; does not select it.
- `HattrickAI_V5/Core/M8ChanceAllocationEngine.cs` — calculates chance/tactic conversion effects; does not select the tactic.
- `HattrickAI_V5/Core/M10FinalDecisionEngine.cs` — selects final plan/attitude, not team tactic.

### Documentation rule

Any future UI or manual text claiming that V5 "calculates the best tactic" must first be backed by an actual selector/calculation path in code. Until such a path exists, document the tactic as supplied input (`Normal`) rather than as an engine decision.

---

# Known Acceptance / Documentation Risks

- Acceptance/regression behavior has changed during recent C10-C18 fixes; do not describe C10-C18 as universally green unless the corresponding run has actually been verified.
- C13 previously compared exposed DB2 count against production SecondPass count incorrectly. The production pipeline exposes a formation-diversified DB2 subset, so exposed count and production DB2 count are not required to be equal.
- C17 contains pipeline/telemetry continuity checks, but a successful `Finish` event must be verified before documenting the acceptance as fully passing.
- C18 deterministic rerun regression exists and is invoked after C17; its current pass status must be verified from an actual run before being documented as green.

---

# Current Documentation Project

The technical manual is being built incrementally from repository code, tests, configuration and reference documents.

The manual must distinguish clearly between:

1. formulas/reference material from source documents,
2. actual V5 implementation,
3. test/acceptance behavior,
4. unresolved or missing implementation.

No guessed calculation, coefficient, selector or UI behavior may be presented as an implemented feature.
