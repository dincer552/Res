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

## V5 test output before the latest fixes

DEF-L 9.25
DEF-C 14.75
DEF-R 9.25
MID 8.75
ATT-L 7.25
ATT-C 9.75
ATT-R 6.75

## Formula audit and fixes

1. **Central-defender overcrowding scope corrected.**
   - 2 central defenders: Playmaking contribution × 0.964
   - 3 central defenders: Playmaking contribution × 0.900
   Defensive contributions are no longer multiplied by the central-defender overcrowding factor.

2. **Inner-midfielder overcrowding scope corrected.**
   - 2 IM: Playmaking contribution × 0.935
   - 3 IM: Playmaking contribution × 0.825
   Passing, scoring, winger and defending contributions are no longer multiplied by the IM overcrowding factor.

3. **Forward overcrowding retained.**
   - 2 forwards: all forward contributions × 0.945
   - 3 forwards: all forward contributions × 0.865

4. **Forward central-attack coefficients are the researched values.**
   Normal forward central attack:
   `Scoring × 0.178 + Passing × 0.066`

5. **Coach modifiers match the researched Hattrick values.**
   - Offensive coach: attack +8%, defence -11%
   - Defensive coach: defence +14%, attack -8%
   - Neutral coach: no coach-specific modifier.

6. **Match-attitude and venue midfield modifiers use the researched values.**
   - Normal: 100%
   - PIC: 83.945%
   - MOTS: 111.49%
   - Home-field midfield: 119.892%

7. **Lead-retreat coefficients use the documented in-match values.**
   Defence increases by about 7.5% and attack decreases by about 9% per goal after the two-goal threshold, capped by the engine.

8. **Confidence is now read automatically from CHPP training XML.**
   `SelfConfidence = 4` is used as the neutral/decent baseline. The current V5 calibration applies a conservative empirical +5% attack multiplier per confidence level above 4 (and -5% below 4). This coefficient is explicitly marked empirical; it is not hard-coded to the S4MSUNFC reference result.

9. **The questionnaire remains exactly three questions.**
   Coach style, team spirit and match importance are the only user inputs. Confidence is live CHPP data and is not a fourth question.

## Next regression test

Run the same S4MSUNFC 3-5-2 XI with the same questionnaire choices:

- Dengeli / neutral coach
- Kaynaşık / composed team spirit
- Normal match importance

Ground truth from Hattrick:

`10.25 / 16.50 / 10.25 / 7.25 / 10.50 / 12.00 / 9.50`

The next live V5 output should be compared sector-by-sector. Do not tune coefficients again until this exact XI is tested after the new build.
