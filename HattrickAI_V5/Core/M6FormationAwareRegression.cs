namespace HattrickAI.V5.Core;

public static class M6FormationAwareRegression
{
    public static async Task<IReadOnlyList<string>> RunAsync(
        IReadOnlyList<PositionAssignmentCandidate> xiCandidates,
        IReadOnlyList<Player> players,
        CancellationToken cancellationToken = default)
    {
        var failures = new List<string>();
        var selected = xiCandidates
            .GroupBy(x => x.Formation, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .SelectMany(g => g.Take(2))
            .ToList();

        var expectedFormations = selected
            .Select(x => x.Formation)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        if (expectedFormations.Count < 2)
        {
            failures.Add("M6 formation isolation requires >= 2 formations");
            return failures;
        }

        var callsByFormation = expectedFormations.ToDictionary(x => x, _ => 0, StringComparer.Ordinal);
        var dominantFormation = expectedFormations[0];

        var m6 = new M6GlobalOptimizationEngine();
        var result = await m6.OptimizeAsync(
            selected,
            players,
            (lineup, _) =>
            {
                callsByFormation[lineup.Formation]++;
                var tacticalScore = string.Equals(lineup.Formation, dominantFormation, StringComparison.Ordinal) ? 1000d : 1d;
                var rating = new RegionalRatingSnapshot(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
                var matchup = new MatchupEvaluation(0, 0, 0, 0, 0, 0, 0, tacticalScore);
                return Task.FromResult(new TacticalCandidate(lineup, rating, matchup, tacticalScore));
            },
            beamWidth: 2,
            maxIterations: 2,
            cancellationToken: cancellationToken);

        foreach (var formation in expectedFormations)
        {
            var minimumExpectedCalls = selected.Count(x => x.Formation == formation);
            Check(callsByFormation[formation] >= minimumExpectedCalls, $"M6 searched formation {formation}", failures);
        }

        Check(result.EvaluatedCandidates == callsByFormation.Values.Sum(), "M6 evaluated count matches evaluator calls", failures);
        Check(result.BestCandidate is not null && string.Equals(result.BestCandidate.Lineup.Formation, dominantFormation, StringComparison.Ordinal),
            "M6 global best remains score-driven after formation-isolated search", failures);

        Console.WriteLine("M6 formation search: " + string.Join(", ", expectedFormations.Select(f => $"{f}={callsByFormation[f]}")));
        Console.WriteLine($"M6 evaluated: {result.EvaluatedCandidates} | retained: {result.RetainedCandidates}");

        RunDatabaseDepthRegression(expectedFormations, players, failures);
        RunM10FormationCompetitionRegression(selected, expectedFormations, failures);
        RunM11FinalComparisonRegression(selected, expectedFormations, failures);

        return failures;
    }

    private static void RunDatabaseDepthRegression(
        IReadOnlyList<string> formations,
        IReadOnlyList<Player> players,
        List<string> failures)
    {
        var db = new CandidateEvaluationDatabase("Regression DB", 30, formations);
        foreach (var formation in formations.Take(2))
        {
            for (var i = 0; i < CandidateEvaluationDatabase.MinimumPerFormation; i++)
            {
                var lineup = BuildSyntheticLineup(formation, players, i);
                var tactical = BuildSyntheticTactical(lineup, i + 1);
                db.Add(new CandidateEvaluationRecord(
                    $"{formation}:{i}",
                    formation,
                    lineup,
                    1,
                    1,
                    tactical.TacticalScore,
                    tactical.Rating,
                    new AdvancedTacticalScenarioResult(),
                    new M8ChanceResult(0.5, 0.5, 0.5, 0.5, 0.5),
                    new MatchPrediction(0.5, 1, 1, 0.5, 0.2, 0.3),
                    i + 1,
                    "REG"));
            }
        }

        var top = db.TopWithFormationDiversity(30, CandidateEvaluationDatabase.MaxPerFormation);
        foreach (var formation in formations.Take(2))
        {
            var count = top.Count(x => x.Formation == formation);
            Check(count >= CandidateEvaluationDatabase.MinimumPerFormation, $"DB depth {formation} >= {CandidateEvaluationDatabase.MinimumPerFormation}", failures);
        }

        Console.WriteLine("DB depth regression: " + string.Join(", ", formations.Take(2).Select(f => $"{f}={top.Count(x => x.Formation == f)}")));
    }

    private static void RunM10FormationCompetitionRegression(
        IReadOnlyList<PositionAssignmentCandidate> selected,
        IReadOnlyList<string> formations,
        List<string> failures)
    {
        var candidates = new List<M10CandidateEvaluation>();
        foreach (var formation in formations.Take(2))
        {
            var seed = selected.First(x => x.Formation == formation).Lineup;
            for (var i = 0; i < M10FinalDecisionEngine.RequiredFormationDepth; i++)
            {
                var tactical = BuildSyntheticTactical(seed, i + 1 + (formation == formations[0] ? 100 : 0));
                candidates.Add(new M10CandidateEvaluation(
                    tactical,
                    new MatchPrediction(0.5, 1, 1, 0.5 + (i * 0.001), 0.2, 0.3),
                    0.8));
            }
        }

        var decision = new M10FinalDecisionEngine().Select(candidates);
        var competition = decision.FormationCompetition ?? [];
        foreach (var formation in formations.Take(2))
        {
            var row = competition.FirstOrDefault(x => x.Formation == formation);
            Check(row is not null, $"M10 competition contains {formation}", failures);
            if (row is not null)
                Check(row.CandidateCount >= M10FinalDecisionEngine.RequiredFormationDepth, $"M10 depth sufficient {formation}", failures);
        }
        Check(competition.Count == formations.Take(2).Count(), "M10 formation count regression", failures);
        Check(competition.All(x => x.Rank > 0), "M10 ranks formations", failures);
        Check(competition.Count < 2 || competition[0].MarginVsNext >= 0, "M10 winner margin is non-negative", failures);
        Check(competition.All(x => x.SearchDepthStatus == M10SearchDepthStatus.Sufficient), "M10 depth status regression", failures);
    }

    private static void RunM11FinalComparisonRegression(
        IReadOnlyList<PositionAssignmentCandidate> selected,
        IReadOnlyList<string> formations,
        List<string> failures)
    {
        var candidates = new List<M11CandidateEvaluation>();
        foreach (var formation in formations.Take(2))
        {
            var lineup = selected.First(x => x.Formation == formation).Lineup;
            var tactical = BuildSyntheticTactical(lineup, formation == formations[0] ? 10 : 9);
            candidates.Add(new M11CandidateEvaluation(
                tactical,
                new MatchPrediction(0.5, 1, 1, formation == formations[0] ? 0.65 : 0.45, 0.2, formation == formations[0] ? 0.15 : 0.35),
                0.8,
                1.0));
        }

        var decision = new M11FinalSelectorEngine().Select(candidates);
        Check(decision.FormationCount == formations.Take(2).Count(), "M11 compares all finalist formations", failures);
        Check(decision.CandidateCount == candidates.Count, "M11 candidate count regression", failures);
        Check(double.IsFinite(decision.Ranking[0].FinalScore), "M11 final score finite", failures);
    }

    private static Lineup BuildSyntheticLineup(string formation, IReadOnlyList<Player> players, int offset)
    {
        var source = players.Take(11).ToList();
        var slots = source.Select((p, i) => new Slot(
            SlotCode(i),
            SlotCode(i),
            "synthetic",
            p.Name,
            p.Id,
            1,
            i,
            0,
            PlayerOrder.Normal)).ToList();
        return new Lineup("Regression", formation, slots);
    }

    private static TacticalCandidate BuildSyntheticTactical(Lineup lineup, double score)
    {
        var rating = new RegionalRatingSnapshot(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        var matchup = new MatchupEvaluation(0, 0, 0, 0, 0, 0, 0, score);
        return new TacticalCandidate(lineup, rating, matchup, score / 10.0);
    }

    private static string SlotCode(int index) => index switch
    {
        0 => "GK", 1 => "DEF-L", 2 => "DEF-CL", 3 => "DEF-C", 4 => "DEF-CR",
        5 => "DEF-R", 6 => "IM-L", 7 => "IM-C", 8 => "IM-R", 9 => "FW-L", 10 => "FW-R", _ => "GK"
    };

    private static void Check(bool ok, string name, List<string> failures)
    {
        if (!ok) failures.Add(name);
    }
}
