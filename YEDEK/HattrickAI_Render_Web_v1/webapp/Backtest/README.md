# Walk-forward backtest

The backtest uses only completed matches strictly before each target match as the historical opponent input and rejects look-ahead data. It reports result-direction accuracy, Brier score, expected-goal error, selected formation and tactic.

Important limitation: CHPP `matchdetails` gives historical team ratings, but it does not provide a complete historical player-roster snapshot. The current implementation therefore uses the current own roster for the HO Engine selection while keeping opponent history and cutoff data historical. This is explicitly not a fully historical player backtest and must not be presented as one.

Next step for a fully faithful backtest is to persist each team's player/skill snapshot over time (or reconstruct it from dated CHPP player data) and feed that snapshot to `RecommendationEngine`.
