# Hattrick AI V5 Engine Map

This document links each engine concept to the real implementation files.

| Engine | File | Class | Main Functions | Input | Output | Status |
|---|---|---|---|---|---|---|
| M3 | TBD | TBD | TBD | TBD | TBD | Pending |
| M4 | TBD | TBD | TBD | TBD | TBD | Pending |
| M5 | TBD | TBD | TBD | TBD | TBD | Pending |
| M6-A | `HattrickAI_V5/Core/M6GlobalOptimizationEngine.cs` | `M6GlobalOptimizationEngine` | optimization/evaluation path | lineup candidates and engine context | M6 optimization result | Partially mapped |
| M7 | `HattrickAI_V5/Core/RegionalRatingScenarioEngine.cs` | `RegionalRatingScenarioEngine` | `CalculateLineup` | lineup, players, `MatchState` including `TeamTactic` | rating scenario | Verified |
| M7.2 | `HattrickAI_V5/Core/AdvancedTacticalScenarioEngine.cs` | `AdvancedTacticalScenarioEngine` | `CalculateLineup`, `Map`, `CalculateTacticSkill` | lineup, players, `MatchState`, opponent average main skill | advanced tactical scenario | Verified |
| M8 | `HattrickAI_V5/Core/M8ChanceAllocationEngine.cs` | `M8ChanceAllocationEngine` | tactic conversion / sector allocation / chance calculation | advanced tactical inputs and opponent rating | M8 chance result | Verified |
| M9 | `HattrickAI_V5/Core/M9MatchPredictionEngine.cs` | `M9MatchPredictionEngine` | prediction calculation | M8/chance and matchup inputs | M9 prediction | Partially mapped |
| DB1 | TBD | TBD | TBD | TBD | TBD | Pending |
| M10 | `HattrickAI_V5/Core/M10FinalDecisionEngine.cs` | `M10FinalDecisionEngine` | `Select`, `SelectApproach` | candidate evaluations and predictions | final decision / match plan / team attitude | Verified |
| M6-B | `HattrickAI_V5/Core/M6GlobalOptimizationEngine.cs` | `M6GlobalOptimizationEngine` | refinement path | formation/budget candidates | refined optimization result | Partially mapped |
| DB2 | TBD | TBD | TBD | TBD | TBD | Pending |
| M11 | `HattrickAI_V5/Core/M11FinalSelectorEngine.cs` | `M11FinalSelectorEngine` | final selection | finalist pool | final selector result | Partially mapped |

---

# Verified Tactical Data Flow

```text
AnalysisService
    |
    | RatingContext(..., TeamTactic.Normal)
    v
MotorPipelineService
    |
    | context.RatingContext.Tactic
    v
MatchState
    |
    +--> M7 RegionalRatingScenarioEngine
    |
    +--> M7.2 AdvancedTacticalScenarioEngine
    |       |
    |       +--> Map(TeamTactic -> AdvancedTactic)
    |       +--> CalculateTacticSkill(...)
    |       +--> tactical level / distribution effects
    |
    +--> M8 M8ChanceAllocationEngine
            |
            +--> tactic conversion rate
            +--> sector chance distribution
```

## Critical distinction

M7.2 and M8 **calculate the consequences of a supplied tactic**. They do not currently select the best team tactic.

The web production path supplies `TeamTactic.Normal` in `AnalysisService.cs`.

M10 can select `TeamAttitude` through `SelectApproach`, but `TeamAttitude` is not `TeamTactic`.

Therefore there is currently no verified motor-to-UI path that produces a calculated strategy such as `AttackMiddle` or `AttackWings` as a final tactical decision.

---

# Known Gaps / Risks

- `M3`, `M4`, `M5`, DB1 and DB2 mappings still need direct source verification before being filled.
- M6-A/M6-B are represented by the same optimization engine class in the current source map; the exact stage-specific method boundaries need further source inspection before documenting more detail.
- M9 and M11 require further direct source inspection for complete input/output and calculation documentation.
- Do not label `AdvancedTactic.Normal` as an engine-selected tactic. In the current web path it originates from `TeamTactic.Normal` supplied by `AnalysisService`.
- Do not infer a "best tactic" from M8 sector distributions or M10 formation ranking. Those are downstream calculations/selection mechanisms, not a tactical selector.

Rules:

- Only real repository findings are added.
- No guessed mappings.
- Update after every engine analysis.
