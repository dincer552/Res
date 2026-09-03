using System.Text.Json;

namespace HattrickAI.V5.Core;

public static class OfflineRegressionRunner
{
    public static async Task<int> RunAsync(string path)
    {
        if (!File.Exists(path)) return Fail($"offline JSON bulunamadı: {path}");
        try
        {
            await using var stream = File.OpenRead(path);
            using var doc = await JsonDocument.ParseAsync(stream);
            var root = doc.RootElement;
            var normalized = root.GetProperty("normalized");
            var analysis = root.GetProperty("v5Analysis");
            var players = normalized.GetProperty("ownPlayers").EnumerateArray().Select(ReadPlayer).ToList();
            var lineup = ReadLineup(analysis.GetProperty("ownLineup"));
            var opponent = ReadRating(analysis.GetProperty("opponentRating"));

            var failures = new List<string>();
            Check(lineup.Slots.Count == 11, "M7 XI 11 oyuncu", failures);
            Check(players.Count >= 11, "own player pool >= 11", failures);
            Check(IsFiniteRating(opponent), "opponent 7 rating finite", failures);

            var m3 = new PlayerAnalysisEngine();
            var m3Result = m3.Analyze(players);
            Check(m3Result.Players.Count == players.Count, "M3 input/output player count", failures);
            Check(m3Result.Players.Select(p => p.PlayerId).Distinct().Count() == m3Result.Players.Count, "M3 duplicate player profile", failures);
            Check(m3Result.Players.All(p => p.IsEligible == (p.InjuryLevel != 999)), "M3 eligibility consistency", failures);

            var m4 = new FormationCandidateEngine();
            var m4First = m4.Generate(m3Result);
            var m4Second = m4.Generate(m3Result);
            Check(m4First.Candidates.Count > 0, "M4 has legal formation candidates", failures);
            Check(m4First.Candidates.All(c => c.SlotCodes.Count == 11), "M4 all candidates have 11 slots", failures);
            Check(m4First.Candidates.All(c => c.SlotCodes.Distinct(StringComparer.Ordinal).Count() == 11), "M4 duplicate slot code", failures);
            Check(m4First.Candidates.Select(c => c.Formation).Distinct(StringComparer.Ordinal).Count() == m4First.Candidates.Count, "M4 duplicate formation candidate", failures);
            Check(m4First.Candidates.All(c => double.IsFinite(c.StructuralScore) && c.StructuralScore > 0), "M4 finite structural scores", failures);
            Check(m4First.Candidates.Select(c => c.Formation).SequenceEqual(m4Second.Candidates.Select(c => c.Formation), StringComparer.Ordinal), "M4 deterministic candidate order", failures);
            Check(m4First.Candidates.Select(c => c.StructuralScore).SequenceEqual(m4Second.Candidates.Select(c => c.StructuralScore)), "M4 deterministic structural score", failures);
            Console.WriteLine($"M3: {m3Result.Players.Count} profiles | M4: {m4First.Candidates.Count} legal formation candidates | invalid: 0");
            Console.WriteLine("M4 formations: " + string.Join(", ", m4First.Candidates.Select(c => c.Formation)));

            var opponentName = analysis.TryGetProperty("opponentName", out var opponentNameElement) ? opponentNameElement.GetString() ?? "Opponent" : "Opponent";
            var opponentFormation = analysis.TryGetProperty("opponentFormation", out var opponentFormationElement) ? opponentFormationElement.GetString() ?? "" : "";
            var teamName = analysis.TryGetProperty("teamName", out var teamNameElement) ? teamNameElement.GetString() ?? lineup.TeamName : lineup.TeamName;

            var opponentProfile = new OpponentMatchProfile(opponentName, opponentFormation, opponent, new OpponentThreatEngine().Analyze(opponent));
            var context = new MatchDataContext(players, 0, teamName, opponentProfile, RatingContext.Default, MatchQuestionnaire.Default);
            var m5 = new PositionOptimizationEngine();
            Console.WriteLine("M5: generating XI candidates...");
            var m5Candidates = m5.GenerateCandidates(context, m3Result, m4First, 100);
            Console.WriteLine($"M5: generated {m5Candidates.Count} candidates");
            var m5CandidatesAgain = m5.GenerateCandidates(context, m3Result, m4First, 100);

            Check(m5Candidates.Count > 0, "M5 produces XI candidates", failures);
            Check(m5Candidates.All(c => c.Lineup.Slots.Count == 11), "M5 every XI has 11 slots", failures);
            Check(m5Candidates.All(c => c.Lineup.Slots.Select(s => s.PlayerId).Distinct().Count() == 11), "M5 duplicate player inside XI", failures);
            Check(m5Candidates.All(c => c.Lineup.Slots.Select(s => s.PlayerId).All(id => m3Result.Find(id)?.IsEligible == true)), "M5 all players eligible", failures);
            Check(m5Candidates.All(c => c.Lineup.Formation == c.Formation), "M5 formation handoff preserved", failures);
            Check(m5Candidates.All(c => c.Lineup.Slots.All(s => m3Result.Find(s.PlayerId)?.Positions.Any(p => p.PositionCode == s.Code && p.Score > 0) == true)), "M5 M3 suitability preserved", failures);
            Check(m5Candidates.All(c => double.IsFinite(c.SuitabilityScore) && c.SuitabilityScore > 0), "M5 finite suitability", failures);
            Check(m5Candidates.All(c => double.IsFinite(c.StructuralScore) && c.StructuralScore > 0), "M5 M4 structural score preserved", failures);
            Check(m5Candidates.All(c => m4First.Candidates.Any(f => f.Formation == c.Formation && Math.Abs(f.StructuralScore - c.StructuralScore) < 1e-12)), "M5 M4 structural score continuity", failures);
            Check(m5Candidates.Select(c => c.CandidateId).Distinct(StringComparer.Ordinal).Count() == m5Candidates.Count, "M5 duplicate candidate id", failures);
            Check(m5Candidates.All(c => c.FormationId == c.Formation && c.LineupId == c.CandidateId), "M5 candidate identity continuity", failures);
            Check(m5Candidates.Select(c => c.CandidateId).SequenceEqual(m5CandidatesAgain.Select(c => c.CandidateId), StringComparer.Ordinal), "M5 deterministic candidate order", failures);
            Check(m5Candidates.Select(c => c.SuitabilityScore).SequenceEqual(m5CandidatesAgain.Select(c => c.SuitabilityScore)), "M5 deterministic suitability", failures);
            Check(m5Candidates.Select(c => c.StructuralScore).SequenceEqual(m5CandidatesAgain.Select(c => c.StructuralScore)), "M5 deterministic structural handoff", failures);
            Console.WriteLine($"M5: {m5Candidates.Count} XI candidates | invalid: 0 | duplicate: 0");
            Console.WriteLine("M5 top XI: " + (m5Candidates.Count > 0 ? m5Candidates[0].CandidateId : "none"));

            var m6RegressionFailures = await M6FormationAwareRegression.RunAsync(m5Candidates, players);
            failures.AddRange(m6RegressionFailures);
            if (m6RegressionFailures.Count > 0)
            {
                foreach (var failure in m6RegressionFailures) Console.WriteLine("FAIL: " + failure);
                return 1;
            }
            Console.WriteLine("PASS: M6 formation-aware search isolation");

            if (failures.Count > 0) { foreach (var f in failures) Console.WriteLine("FAIL: " + f); return 1; }

            var m7 = new RegionalRatingScenarioEngine();
            var m72 = new AdvancedTacticalScenarioEngine();
            var m8 = new M8ChanceModel();
            var state = new MatchState("offline-m7", analysis.GetProperty("ownFormation").GetString() ?? "3-5-2", "offline-xi", "offline", MatchLocation.Away, TeamAttitude.Normal, TeamTactic.Normal, 4.5, CoachStyle.Neutral);
            var m7Result = m7.CalculateLineup(lineup, players, state);
            Check(IsFiniteRating(m7Result.Rating), "M7 rating finite", failures);
            Check(Math.Abs(RegionalRatingScenarioEngine.TeamSpiritMultiplier(4.5) - 1.0) < 0.01, "M7.1 composed spirit baseline", failures);
            Check(m7Result.Modifiers.CoachStyle == CoachStyle.Neutral, "M7 questionnaire coach baseline", failures);

            var offensive = m7.CalculateLineup(lineup, players, state with { CandidateId = "offline-m7-offensive", CoachStyle = CoachStyle.Offensive });
            var defensive = m7.CalculateLineup(lineup, players, state with { CandidateId = "offline-m7-defensive", CoachStyle = CoachStyle.Defensive });
            Check(offensive.Rating.CentralAttack > m7Result.Rating.CentralAttack, "Coach Offensive increases central attack", failures);
            Check(offensive.Rating.CentralDefence < m7Result.Rating.CentralDefence, "Coach Offensive decreases central defence", failures);
            Check(defensive.Rating.CentralAttack < m7Result.Rating.CentralAttack, "Coach Defensive decreases central attack", failures);
            Check(defensive.Rating.CentralDefence > m7Result.Rating.CentralDefence, "Coach Defensive increases central defence", failures);
            Check(Math.Abs(offensive.Rating.Midfield - m7Result.Rating.Midfield) < 1e-12, "Coach style does not alter midfield", failures);
            Check(Math.Abs(defensive.Rating.Midfield - m7Result.Rating.Midfield) < 1e-12, "Coach style does not alter midfield (defensive)", failures);

            M8ChanceResult? normalChance = null;
            foreach (var tactic in Enum.GetValues<TeamTactic>())
            {
                var tacticState = state with { CandidateId = "offline-" + tactic, TeamTactic = tactic };
                var t = m72.CalculateLineup(lineup, players, tacticState, OpponentAverage(opponent));
                var handoff = AdvancedTacticalScenarioEngine.BuildM8Input(m7.CalculateLineup(lineup, players, tacticState), t);
                var chance = m8.Calculate(handoff, opponent);
                if (tactic == TeamTactic.Normal) normalChance = chance;
                Check(t.CandidateId == handoff.CandidateId, $"M7.2→M8 candidate {tactic}", failures);
                Check(t.Level.Value is >= 0 and <= 10, $"M7.2 level bounds {tactic}", failures);
                Check(Math.Abs(t.ChanceDistribution.LeftShare + t.ChanceDistribution.CentreShare + t.ChanceDistribution.RightShare + t.ChanceDistribution.SetPieceShare - 1.0) < 1e-9, $"M7.2 distribution sum {tactic}", failures);
                Check(chance.MidfieldShare is >= 0 and <= 1 && chance.StructuralChanceIndex is >= 0 and <= 1, $"M8 bounds {tactic}", failures);
                if (tactic == TeamTactic.Pressing)
                    Check(chance.OpponentRegularChanceExpected < chance.OwnRegularChanceExpected || chance.PressingSuppression > 0, "M8 pressing suppression active", failures);
            }

            if (normalChance is not null)
            {
                var actualMatchup = BuildOfflineMatchup(m7Result.Rating, opponent, normalChance);
                var actualTactical = new TacticalCandidate(lineup, m7Result.Rating, actualMatchup, 0.0);
                var m9 = new M9MatchPredictionEngine();
                var actualPrediction = m9.Predict(actualTactical, normalChance, opponent, MatchLocation.Away, players).Prediction;

                // Regression must test model mathematics, not assume that this particular
                // historical fixture is favorable to the user's team. The real fixture can
                // legitimately predict an opponent edge.
                var clearlyFavorableOpponent = new RegionalRatingSnapshot(
                    5, 5, 5, 5,
                    8, 8, 8,
                    5, 5, 5, 5, 8, 8, 8);
                var favorableChance = m8.Calculate(
                    AdvancedTacticalScenarioEngine.BuildM8Input(
                        m7.CalculateLineup(lineup, players, state),
                        m72.CalculateLineup(lineup, players, state, 5)),
                    clearlyFavorableOpponent);
                var favorableMatchup = BuildOfflineMatchup(m7Result.Rating, clearlyFavorableOpponent, favorableChance);
                var favorableTactical = new TacticalCandidate(lineup, m7Result.Rating, favorableMatchup, 0.0);
                var favorablePrediction = m9.Predict(favorableTactical, favorableChance, clearlyFavorableOpponent, MatchLocation.Away, players).Prediction;

                Check(double.IsFinite(actualPrediction.ExpectedHomeGoals) && double.IsFinite(actualPrediction.ExpectedAwayGoals), "M9 historical fixture finite xG", failures);
                Check(Math.Abs(actualPrediction.WinProbability + actualPrediction.DrawProbability + actualPrediction.LossProbability - 1.0) < 1e-9, "M9 historical fixture WDL sums to 1", failures);
                Check(favorablePrediction.ExpectedHomeGoals > favorablePrediction.ExpectedAwayGoals, "M9 clearly favorable case favors own xG", failures);
                Check(favorablePrediction.WinProbability > favorablePrediction.LossProbability, "M9 clearly favorable case favors own WDL", failures);
                Console.WriteLine($"M9 regression: actual fixture xG {actualPrediction.ExpectedHomeGoals:0.###}-{actualPrediction.ExpectedAwayGoals:0.###} | W/D/L {actualPrediction.WinProbability:P1}/{actualPrediction.DrawProbability:P1}/{actualPrediction.LossProbability:P1}");
                Console.WriteLine($"M9 favorable regression: xG {favorablePrediction.ExpectedHomeGoals:0.###}-{favorablePrediction.ExpectedAwayGoals:0.###} | W/D/L {favorablePrediction.WinProbability:P1}/{favorablePrediction.DrawProbability:P1}/{favorablePrediction.LossProbability:P1}");
            }
            else
            {
                failures.Add("M9 normal M8 chance unavailable");
            }

            if (failures.Count > 0) { foreach (var f in failures) Console.WriteLine("FAIL: " + f); return 1; }
            Console.WriteLine("PASS: M3 → M4 → M5 → M7 → M7.1 → M7.2 → M8 → M9 offline regression");
            Console.WriteLine("PASS: Questionnaire CoachStyle → M7 rating wiring");
            Console.WriteLine($"XI: {lineup.Formation} | Opponent: {opponentName}");
            Console.WriteLine($"M7 midfield: {m7Result.Rating.Midfield:0.###} | Opponent midfield: {opponent.Midfield:0.###}");
            Console.WriteLine("Tactics tested: Normal, CounterAttack, LongShots, AttackMiddle, AttackWings, Creative, Pressing");
            return 0;
        }
        catch (Exception ex) { Console.WriteLine("STACK: " + ex); return Fail("offline regression exception: " + ex.Message); }
    }

    private static MatchupEvaluation BuildOfflineMatchup(RegionalRatingSnapshot own, RegionalRatingSnapshot opponent, M8ChanceResult chance)
    {
        static double signed(double share) => (Math.Clamp(share, 0, 1) * 2.0) - 1.0;
        var midfield = signed(chance.MidfieldShare);
        var left = signed(chance.LeftAttackVsRightDefence);
        var centre = signed(chance.CentreAttackVsCentreDefence);
        var right = signed(chance.RightAttackVsLeftDefence);
        var leftDef = signed(Share(own.LeftDefence, opponent.RightAttack));
        var centreDef = signed(Share(own.CentralDefence, opponent.CentralAttack));
        var rightDef = signed(Share(own.RightDefence, opponent.LeftAttack));
        var overall = (midfield + left + centre + right + leftDef + centreDef + rightDef) / 7.0;
        return new MatchupEvaluation(midfield, left, centre, right, leftDef, centreDef, rightDef, overall);
    }

    private static double Share(double own, double opponent)
    {
        var total = Math.Max(0, own) + Math.Max(0, opponent);
        return total <= 0 ? 0.5 : Math.Clamp(Math.Max(0, own) / total, 0, 1);
    }

    private static Player ReadPlayer(JsonElement e) => new(
        e.GetProperty("id").GetInt32(),
        e.GetProperty("name").GetString() ?? "",
        e.GetProperty("keeper").GetInt32(),
        e.GetProperty("defending").GetInt32(),
        e.GetProperty("playmaking").GetInt32(),
        e.GetProperty("passing").GetInt32(),
        e.GetProperty("winger").GetInt32(),
        e.GetProperty("scoring").GetInt32(),
        e.GetProperty("stamina").GetInt32(),
        e.GetProperty("form").GetInt32(),
        e.GetProperty("experience").GetInt32(),
        e.TryGetProperty("loyalty", out var l) ? l.GetInt32() : 0,
        e.TryGetProperty("injuryLevel", out var injury) ? injury.GetInt32() : 0,
        e.TryGetProperty("specialty", out var specialty) && specialty.ValueKind == JsonValueKind.Number ? (PlayerSpecialty)specialty.GetInt32() : PlayerSpecialty.None);

    private static Lineup ReadLineup(JsonElement e)
    {
        var slots = e.GetProperty("slots").EnumerateArray().Select(s => new Slot(s.GetProperty("code").GetString() ?? "", s.GetProperty("label").GetString() ?? "", s.GetProperty("description").GetString() ?? "", s.TryGetProperty("playerName", out var n) ? n.GetString() : null, s.GetProperty("playerId").GetInt32(), s.GetProperty("rating").GetDouble(), s.GetProperty("x").GetDouble(), s.GetProperty("y").GetDouble())).ToList();
        return new Lineup(e.GetProperty("teamName").GetString() ?? "", e.GetProperty("formation").GetString() ?? "", slots);
    }
    private static RegionalRatingSnapshot ReadRating(JsonElement e) => new(e.GetProperty("rawLeftDefence").GetDouble(), e.GetProperty("rawCentralDefence").GetDouble(), e.GetProperty("rawRightDefence").GetDouble(), e.GetProperty("rawMidfield").GetDouble(), e.GetProperty("rawLeftAttack").GetDouble(), e.GetProperty("rawCentralAttack").GetDouble(), e.GetProperty("rawRightAttack").GetDouble(), e.GetProperty("leftDefence").GetDouble(), e.GetProperty("centralDefence").GetDouble(), e.GetProperty("rightDefence").GetDouble(), e.GetProperty("midfield").GetDouble(), e.GetProperty("leftAttack").GetDouble(), e.GetProperty("centralAttack").GetDouble(), e.GetProperty("rightAttack").GetDouble());
    private static double OpponentAverage(RegionalRatingSnapshot r) => (r.LeftDefence + r.CentralDefence + r.RightDefence + r.LeftAttack + r.CentralAttack + r.RightAttack + r.Midfield) / 7.0;
    private static bool IsFiniteRating(RegionalRatingSnapshot r) => new[]{r.LeftDefence,r.CentralDefence,r.RightDefence,r.Midfield,r.LeftAttack,r.CentralAttack,r.RightAttack}.All(double.IsFinite);
    private static void Check(bool ok,string name,List<string> failures){if(!ok)failures.Add(name);}
    private static int Fail(string message){Console.WriteLine("FAIL: "+message);return 1;}
}
