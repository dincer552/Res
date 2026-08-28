# HattrickAI V5 — Regional Rating Engine Roadmap

## Target

HattrickAI must calculate the same seven regional team ratings that Hattrick displays:

- Right Defence
- Central Defence
- Left Defence
- Midfield
- Right Attack
- Central Attack
- Left Attack

The engine must depend on both the selected lineup and every player's relevant skills. Player choice and individual orders must change the regional ratings in the correct direction.

## Important limitation

Hattrick does not publish the complete match-engine formula. The public contribution table is community research, based on the 2017-era match engine. Therefore the engine is being built as a transparent, testable implementation of the researched model and will be calibrated against real Hattrick match reports before it is used for lineup optimization.

## Phase 1 — Contribution core

Implemented in `Core/RegionalRatingEngine.cs`.

- Seven sectors are represented independently.
- Position + individual order determine which coefficients are used.
- Keeper, central defender, wing back, inner midfielder, winger and forward contributions are isolated.
- Side-specific contributions are kept distinct.
- Central-line overcrowding penalties are represented.
- A quarter-step display conversion is isolated in one function.

## Phase 2 — Player state model

Add the complete player state required by the researched model:

- loyalty
- current form
- experience
- stamina effects where applicable
- specialty / special-role effects where applicable

The current engine already has form and loyalty in its effective-skill pipeline; experience is deliberately kept separate so it can be calibrated correctly by sector instead of being multiplied blindly into skills.

## Phase 3 — Match context

Add and calibrate:

- home / away / derby midfield effect
- PIC / MOTS
- coach mentality
- tactic effects (normal, counter-attack, long shots, etc.)
- confidence
- team-spirit / psychology inputs where they affect ratings

Context must be a separate layer so the same player contribution can be compared under different match conditions.

## Phase 4 — Historical match validation

For each real match we know the Hattrick report ratings for:

- collect player skills at match time
- collect exact lineup and individual orders
- collect home/away status
- collect tactic, coach mentality and relevant psychology inputs
- calculate seven raw sectors
- convert them to displayed quarter-step ratings
- compare with Hattrick's reported values

Store every mismatch as a calibration case instead of changing coefficients ad hoc.

## Phase 5 — Calibration

Use a growing set of real matches to minimize error by sector.

Priority order:

1. Central Defence
2. Left / Right Defence
3. Midfield
4. Central Attack
5. Left / Right Attack

Do not optimize against one screenshot only. A coefficient change is accepted only when it improves the validation set without breaking previously matched cases.

## Phase 6 — Lineup optimizer

Only after the rating engine is stable, change the recommendation engine.

For every candidate lineup:

1. assign players to positions
2. assign allowed individual orders
3. calculate all seven sectors
4. compare attack sectors against the opponent's opposite defence sectors
5. evaluate midfield / possession expectation
6. score the whole tactical plan

The optimizer must evaluate the lineup as a system. It must not select players using a single per-player RP number.

## Phase 7 — UI integration

Expose seven regional ratings behind the analysis result. Initially keep the UI unchanged and use the values for debugging. Once validated, display them in a compact Hattrick-style panel.

## Phase 8 — Match prediction

After sector ratings are reliable, build the chance model:

- midfield determines chance ownership
- central attack vs central defence
- right attack vs left defence
- left attack vs right defence
- later: set pieces, special events and tactic-specific chance changes

## Rule for V5 development

Only `HattrickAI_V5` is active. Do not import code from V1/V3/V4 unless explicitly requested. `YEDEK` is archival only.
