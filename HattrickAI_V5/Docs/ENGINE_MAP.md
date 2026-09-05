# Hattrick AI V5 Engine Map

This document links each engine concept to the real implementation files.

| Engine | File | Class | Main Functions | Input | Output | Status |
|---|---|---|---|---|---|---|
| M3 | `HattrickAI_V5/Core/PlayerAnalysisEngine.cs` | `PlayerAnalysisEngine` | `Analyze`, `AnalyzePlayer`, `Score` | `Player[]` | `PlayerAnalysisResult` / position profiles | Verified |
| M4 | `HattrickAI_V5/Core/FormationCandidateEngine.cs` | `FormationCandidateEngine` | `Generate` | M3 player profiles / `MatchDataContext` | `FormationCandidateSet` | Verified |
| M5 | `HattrickAI_V5/Core/PositionOptimizationEngine.cs` | `PositionOptimizationEngine` | `GenerateCandidates` | context, M3 profiles, formation candidates | `PositionAssignmentCandidate[]` | Verified |
| M6-A | `HattrickAI_V5/Core/M6GlobalOptimizationEngine.cs` | `M6GlobalOptimizationEngine` | optimization path | XI candidates, players, evaluator, budget | `M6OptimizationResult` | Verified |
| M7 | `HattrickAI_V5/Core/RegionalRatingScenarioEngine.cs` | `RegionalRatingScenarioEngine` | `CalculateLineup` | lineup, players, `MatchState` | rating scenario | Verified |
| M7.2 | `HattrickAI_V5/Core/AdvancedTacticalScenarioEngine.cs` | `AdvancedTacticalScenarioEngine` | `CalculateLineup`, `Map`, `CalculateTacticSkill` | lineup, players, `MatchState`, opponent average main skill | advanced tactical scenario | Verified |
| M8 | `HattrickAI_V5/Core/M8ChanceAllocationEngine.cs` | `M8ChanceAllocationEngine` | tactic conversion / sector allocation / chance calculation | tactical inputs and opponent rating | M8 chance result | Verified |
| M9 | `HattrickAI_V5/Core/M9MatchPredictionEngine.cs` | `M9MatchPredictionEngine` | prediction calculation | M8/chance and matchup inputs | M9 prediction | Verified |
| DB1 | `HattrickAI_V5/Core/CandidateEvaluationDatabase.cs` | `CandidateEvaluationDatabase` | `Add`, `Trim`, `TopWithFormationDiversity` | evaluated candidates | bounded/diversified candidate pool | Verified |
| M10 | `HattrickAI_V5/Core/M10FinalDecisionEngine.cs` | `M10FinalDecisionEngine` | `Select`, `SelectApproach` | candidate evaluations/predictions | final decision / plan / team attitude | Verified |
| M6-B | `HattrickAI_V5/Core/M6GlobalOptimizationEngine.cs` | `M6GlobalOptimizationEngine` | refinement path | formation/rank-driven candidates and budgets | refined optimization result | Verified |
| DB2 | `HattrickAI_V5/Core/CandidateEvaluationDatabase.cs` | `CandidateEvaluationDatabase` | `Add`, `Trim`, `TopWithFormationDiversity` | second-pass evaluated candidates | finalist pool | Verified |
| M11 | `HattrickAI_V5/Core/M11FinalSelectorEngine.cs` | `M11FinalSelectorEngine` | final selection | DB2 finalist pool | final selector result | Verified |

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

# Stage 5 Concrete Fixture Link

Real fixture:

`TestJSON/HattrickAI_V5_CHPP_FullOffline_2026-09-01.json`

Documented example:

`HattrickAI_V5/Docs/REAL_MATCH_ANALYSIS.md`

The fixture demonstrates the concrete data path from normalized CHPP player/opponent data through M3/M4/M5 and the stored M7 regional rating/opponent-threat outputs.

Verified example:

```text
Future match: 769648177
Zeytinburnu Sahil Spor vs S4MSUNFC

Own formation: 3-5-2
Own midfield: 6.25
Own total attack: 31.00
Own total defence: 35.50

Opponent formation: 2-5-3
Opponent midfield: 7.00
Opponent total attack: 31.50
Opponent total defence: 22.00
```

The fixture does not store complete standalone M8/M9/M10/M6-B/DB2/M11 final result objects, so the Stage 5 document does not invent them.

---

# Known Gaps / Risks

- Tactical selector is not present in the current production web analysis path.
- M6-A and M6-B use the same optimization engine class; stage-specific behavior is distinguished by pipeline inputs/budgets.
- Do not label `AdvancedTactic.Normal` as an engine-selected tactic when it originates from `TeamTactic.Normal`.
- Do not infer a "best tactic" from M8 sector distributions or M10 formation ranking.
- Acceptance status must always be tied to an actual run; C17/C18 must not be called green without verified output.

Rules:

- Only real repository findings are added.
- No guessed mappings.
- Update after every engine analysis.
