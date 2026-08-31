# Rating calibration notes — V5

Reference data supplied by the manager for future calibration:

## Hattrick direct reference (S4MSUNFC, 3-5-2)

DEF-L 10.25
DEF-C 16.50
DEF-R 10.25
MID 7.25
ATT-L 10.50
ATT-C 12.00
ATT-R 9.50

These values must be treated as direct Hattrick reference values when pasted into future conversations. Do not infer the sector mapping from screenshots.

## Current V5 output for the same visible XI

DEF-L 9.75
DEF-C 16.25
DEF-R 9.75
MID 11.75
ATT-L 7.75
ATT-C 9.75
ATT-R 7.00

## Known engine issues under review

1. Central-line overcrowding was being counted only when Side == Center. Hattrick's contribution loss applies to all players occupying the central defender / inner midfielder / forward lines. The count must therefore be by position family, not by Side.
2. The existing experience stage adds a large absolute bonus after applying contribution coefficients. The public contribution table already includes a standard experience component, so this additive stage can inflate ratings. Experience should not be added as a second full contribution layer until calibrated.
3. Loyalty is a +0.05 skill-level increment per loyalty level up to +1.0 at divine loyalty; the implementation must not exceed +1.0.

## New verified findings — 2026-08-31

4. NORMAL FORWARD CENTRAL-ATTACK COEFFICIENTS ARE SWAPPED IN `RegionalRatingEngine.cs`.
   Current code uses:
   - Passing × 0.178
   - Scoring × 0.066

   Hattrick's contribution table specifies for a normal forward:
   - Scoring × 0.178
   - Passing × 0.066

   Therefore the normal-forward central attack calculation must be changed to:
   `k.Scoring * .178 + k.Passing * .066`

   Source: Hattrick Contribution table:
   https://wiki.hattrick.org/wiki/Contribution

5. CENTRAL-LINE OVERCROWDING PENALTIES IN THE CURRENT CODE MATCH THE VERIFIED HATTRICK VALUES AND SHOULD NOT BE REMOVED.
   Verified values:
   - 2 central defenders: -3.6% => 0.964
   - 3 central defenders: -10.0% => 0.900
   - 2 inner midfielders: -6.5% => 0.935
   - 3 inner midfielders: -17.5% => 0.825
   - 2 forwards: -5.5% => 0.945
   - 3 forwards: -13.5% => 0.865

   The Hattrick manual states that the loss affects the skills of all players in the affected central area. Therefore the current family-count approach is correct in principle; previous notes suggesting that the defender/forward penalties should simply be removed were incorrect and must not be followed.

   Sources:
   https://wiki.hattrick.org/wiki/Midfield
   https://wiki.hattrick.org/wiki/Manual

6. THE CURRENT POSITION CONTRIBUTION COEFFICIENTS WERE COMPARED AGAINST THE VERIFIED HATTRICK CONTRIBUTION TABLE.
   The following major coefficient groups in the current V5 engine match the published table: goalkeeper, central defender normal/offensive/towards-wing, wing-back orders, inner-midfielder orders, winger orders, forward-towards-wing, and defensive-forward. The confirmed coefficient defect from this comparison is the normal-forward central-attack swap documented in item 4.

7. MIDFIELD IS STILL THE LARGEST CALIBRATION GAP FOR THE SUPPLIED S4MSUNFC REFERENCE.
   The direct reference is MID 7.25 in the stored validation fixture, while the earlier V5 output was MID 11.75. The current engine also applies venue/attitude/tactic modifiers to midfield. Future calibration must therefore use the exact same match context, player form/stamina/loyalty/experience values, positions and orders before changing coefficients. Do not tune midfield from a different XI or screenshot.

8. DO NOT USE THE PREVIOUS CLAIM THAT FORWARD OVERCROWDING DOES NOT EXIST.
   Verified Hattrick documentation explicitly lists forward overcrowding penalties (-5.5% for two and -13.5% for three). The existing `AttackCentrePenalty` values 0.945 and 0.865 are therefore directionally correct.

## Calibration rule

Compare the exact same XI, same positions/orders, same match context, and use pasted Hattrick values as ground truth. Do not tune coefficients from screenshots with a different XI.

When the next calibration pass is made, fix the normal-forward central-attack coefficient swap first, then rerun the exact S4MSUNFC fixture before making any further coefficient changes.
