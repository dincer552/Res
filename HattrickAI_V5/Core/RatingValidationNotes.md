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

Future calibration rule: compare the exact same XI, same positions/orders, same match context, and use pasted Hattrick values as ground truth. Do not tune coefficients from screenshots with a different XI.
