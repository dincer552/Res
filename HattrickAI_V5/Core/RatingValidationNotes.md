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

## V5 test output before the formula correction

DEF-L 7.75
DEF-C 13.00
DEF-R 7.75
MID 7.00
ATT-L 6.50
ATT-C 7.50
ATT-R 6.00

The large gaps in defence and attack triggered a formula audit against the researched Hattrick contribution table.

## Verified formula fixes applied 2026-08-31

1. **Central-defender overcrowding is now applied only to the central defender's Playmaking contribution.**
   - 2 central defenders: Playmaking contribution × 0.964
   - 3 central defenders: Playmaking contribution × 0.900
   Defensive contributions themselves are not multiplied by the overcrowding factor.

2. **Inner-midfielder overcrowding remains on Playmaking contribution only.**
   - 2 IM: × 0.935
   - 3 IM: × 0.825

3. **Forward central-attack coefficients are corrected.**
   Normal forward central attack is:
   `Scoring × 0.178 + Passing × 0.066`
   The previous implementation had these two coefficients swapped.

4. **Forward overcrowding is retained.**
   The verified table documents central-forward overcrowding:
   - 2 forwards: × 0.945
   - 3 forwards: × 0.865

5. **Coach modifiers match the verified Hattrick values.**
   - Offensive coach: attack +8%, defence -11%
   - Defensive coach: defence +14%, attack -8%
   Neutral has no coach-specific modifier.

6. **Match-attitude midfield modifiers use the verified values.**
   - Normal: 100%
   - PIC: 83.945%
   - MOTS: 111.49%
   Home-field midfield: 119.892%.

7. **Lead-retreat coefficients use the documented in-match values.**
   Each additional goal of lead after two goals increases defence by about 7.5% and reduces attack by about 9%, with the documented upper limit represented by the engine cap.

8. **Experience contribution remains the researched flat bonus.**
   Experience is converted to the published flat skill-equivalent bonus table before the position contribution coefficients are applied. Loyalty remains capped at +1.0 skill-equivalent.

## Important calibration rule

Compare the **exact same XI**, same positions, same individual orders, same home/away context, same Normal/PIC/MOTS choice, and the same player form/stamina/experience/loyalty values.

Do not tune a coefficient from a different XI or from a screenshot whose seven sectors were manually inferred.

The next test should use the same S4MSUNFC 3-5-2 XI and the user's direct Hattrick copy/paste values as ground truth:

`10.25 / 16.50 / 10.25 / 7.25 / 10.50 / 12.00 / 9.50`

Sources:
- https://wiki.hattrick.org/wiki/Contribution
- https://wiki.hattrick.org/wiki/Midfield
- https://wiki.hattrick.org/wiki/Attack_ratings
- https://wiki.hattrick.org/wiki/Confidence
- https://wiki.hattrick.org/wiki/Coach
