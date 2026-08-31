# Rating calibration notes — V5

Reference data supplied by the manager for future calibration.

## Hattrick direct reference — S4MSUNFC, 3-5-2

DEF-L 10.25
DEF-C 16.50
DEF-R 10.25
MID 7.25
ATT-L 10.50
ATT-C 12.00
ATT-R 9.50

These values are direct Hattrick reference values supplied by copy/paste. Do not infer sector mapping from screenshots.

## Known live psychology at capture

- Team spirit: composed / Turkish UI `kaynaşık` = level 4
- Confidence: `biraz abartı` / slightly exaggerated = level 7
- Coach style: balanced / neutral
- Match approach: normal

The three-question rule remains unchanged. Confidence is not asked from the user because CHPP exposes it automatically.

## Stable V5 regression output

Latest live V5 output for the same S4MSUNFC 3-5-2 XI and the same questionnaire:

DEF-L 9.50
DEF-C 16.00
DEF-R 9.50
MID 7.50
ATT-L 10.25
ATT-C 11.50
ATT-R 9.25

Sector deltas versus Hattrick reference:

DEF-L -0.75
DEF-C -0.50
DEF-R -0.75
MID +0.25
ATT-L -0.25
ATT-C -0.50
ATT-R -0.25

This is the current stable V5 baseline. It was obtained without fixture-specific sector multipliers. Maximum absolute sector error is 0.75.

## Formula audit — 2026-08-31

The live analysis path uses `RegionalRatingEngineFixed`, as wired by `AnalysisService`.

### Position/skill contribution coefficients

The implemented coefficients were rechecked against the researched Hattrick Contribution table:

- Goalkeeper: GK `.165` central / `.183` side; Defending `.079` central / `.082` side
- Central defender: normal DF `.186` central / `.077` side / PM `.035`; offensive `.130` / `.058` / `.047`; towards wing `.133` / `.217` / `.023` plus Passing `.063` side attack
- Wing back: normal `.083` central def / `.268` side def / PM `.023` / WG `.129` side attack / Passing `.054` central attack; defensive, towards-middle and offensive rows use the researched coefficients
- Inner midfielder: normal DF `.070` central / `.028` side / PM `.139` midfield / Passing `.028` side / `.057` central / Scoring `.038` central; the defensive/offensive/towards-wing rows use their researched coefficients
- Winger: normal `.037` central def / `.104` side def / PM `.065` / WG `.219` side attack / Passing `.054` side and `.018` central; order variants use their researched coefficients
- Normal forward: PM `.041` midfield; side attack Scoring `.058` + Passing `.048` + WG `.032`; central attack Scoring `.178` + Passing `.066`

Hattrick's Contribution page states that these published coefficients are for a normal away match, balanced coach and average confidence and already include a standard form/stamina/experience uplift; loyalty is added separately. Source: https://wiki.hattrick.org/wiki/Contribution

### Context formulas

- Home midfield: `119.892%`
- Normal away midfield: `100%`
- PIC midfield: `83.945%`
- MOTS midfield: `111.49%`
- Counter-attack midfield: `93%`
- Offensive coach: attack `+8%`, defence `-11%`
- Defensive coach: defence `+14%`, attack `-8%`
- Lead-retreat: attack `-9%` and defence `+7.5%` per additional goal after the documented threshold/cap

Sources: Hattrick Team Spirit, Attack, Defence and Confidence pages.

### Player state formulas

The engine keeps form and experience relative to the published coefficient baseline rather than stacking a second full standard uplift. Loyalty is applied separately. This is consistent with the Contribution table's baseline note and is left unchanged until a new direct Hattrick dataset disproves it.

### Questionnaire

V5 asks exactly three user inputs:

1. Coach style: `Dengeli / Hücum / Defans`
2. Team spirit: the ten Hattrick levels, with `Kaynaşık` mapped to `Composed`
3. Match approach: `Normal / PIC / MOTS`

No additional user question is to be added. `SelfConfidence` is read automatically from CHPP training data by `AnalysisService`.

### Crowding

The stable V5 implementation uses the following contribution-loss factors:

- 2 CD: PM contribution × `.964`
- 3 CD: PM contribution × `.900`
- 2 IM: PM contribution × `.935`
- 3 IM: PM contribution × `.825`
- 2 FW: forward contributions × `.945`
- 3 FW: forward contributions × `.865`

These factors are kept as the current empirically validated V5 behavior. Do not broaden them or add arbitrary sector multipliers without a new direct Hattrick reference test.

### Side/slot mapping

Historical opponent lineup parsing resolves `Behaviour` 5/6/7 before ordinary `PositionCode` side mapping so extra central forward/inner/defender entries are not mistaken for normal left/right slots. Own lineup side is derived from explicit slot codes `-L`, `-C`, `-R`.

## Freeze decision

As of 2026-08-31 the formula audit does not justify another behavioral coefficient change. The current V5 result is close to the supplied Hattrick reference without fixture-specific tuning.

The next formula change should only be made after a new direct Hattrick reference match demonstrates a reproducible error pattern across another formation, position order, or player profile.

## Regression fixture

Scenario:
- Team: S4MSUNFC
- Formation: 3-5-2
- Questionnaire: Dengeli / Kaynaşık / Normal

Ground truth:

`10.25 / 16.50 / 10.25 / 7.25 / 10.50 / 12.00 / 9.50`

Current V5 baseline:

`9.50 / 16.00 / 9.50 / 7.50 / 10.25 / 11.50 / 9.25`

Do not alter this reference when testing future builds.