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

# Current Investigation

## Tactical Display

Goal:

Show the calculated match tactic in the Recommended XI card.

Expected behavior:

- If the engine produces a tactical decision, display it.
- If no tactical decision exists, display: TAKTİK YOK.

Source investigation:

Possible sources:
- AdvancedTacticalScenarioEngine
- BehaviourEngine
- Tactical scenario outputs
- FinalPlan output

Status:
Investigation in progress.
