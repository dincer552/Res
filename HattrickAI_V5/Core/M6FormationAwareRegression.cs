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
            Check(
                callsByFormation[formation] >= minimumExpectedCalls,
                $"M6 searched formation {formation}",
                failures);
        }

        Check(
            result.EvaluatedCandidates == callsByFormation.Values.Sum(),
            "M6 evaluated count matches evaluator calls",
            failures);

        Check(
            result.BestCandidate is not null &&
            string.Equals(result.BestCandidate.Lineup.Formation, dominantFormation, StringComparison.Ordinal),
            "M6 global best remains score-driven after formation-isolated search",
            failures);

        Console.WriteLine("M6 formation search: " + string.Join(", ", expectedFormations.Select(f => $"{f}={callsByFormation[f]}")));
        Console.WriteLine($"M6 evaluated: {result.EvaluatedCandidates} | retained: {result.RetainedCandidates}");

        return failures;
    }

    private static void Check(bool ok, string name, List<string> failures)
    {
        if (!ok) failures.Add(name);
    }
}
