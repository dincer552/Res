namespace HattrickAI.V5.Core;

/// <summary>
/// M6 bounded global behaviour optimizer.
/// Starts from the supplied XI candidates and performs deterministic beam search
/// over legal individual orders. M6-A normal baseline'dan geniş arama yapar;
/// M6-B ise DB1 seed'lerinin mevcut davranışlarını koruyarak ikinci refinement
/// turunu çalıştırır.
/// </summary>
public sealed class M6GlobalOptimizationEngine
{
    private readonly BehaviourCandidateEngine _candidateEngine;
    private const int CandidateDatabaseCapacity = 100;

    public M6GlobalOptimizationEngine(BehaviourCandidateEngine? candidateEngine = null)
        => _candidateEngine = candidateEngine ?? new BehaviourCandidateEngine();

    public async Task<M6OptimizationResult> OptimizeAsync(
        IReadOnlyList<PositionAssignmentCandidate> xiCandidates,
        IReadOnlyList<Player> players,
        Func<Lineup, CancellationToken, Task<TacticalCandidate>> evaluator,
        int beamWidth = 12,
        int maxIterations = 8,
        CancellationToken cancellationToken = default,
        Action<int, int, int, int>? progress = null,
        bool preserveInputOrders = false)
    {
        ArgumentNullException.ThrowIfNull(xiCandidates);
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(evaluator);
        if (beamWidth < 1) throw new ArgumentOutOfRangeException(nameof(beamWidth));
        if (maxIterations < 1) throw new ArgumentOutOfRangeException(nameof(maxIterations));

        TacticalCandidate? globalBest = null;
        var database = new List<TacticalCandidate>(CandidateDatabaseCapacity);
        var seenDatabase = new HashSet<string>(StringComparer.Ordinal);
        var evaluated = 0;
        var retained = 0;
        var iterations = 0;
        var converged = false;

        foreach (var xi in xiCandidates
            .OrderByDescending(x => x.SuitabilityScore)
            .ThenByDescending(x => x.StructuralScore)
            .ThenBy(x => x.CandidateId, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var matrix = _candidateEngine.Build(xi.Lineup, players);
            var baseline = preserveInputOrders
                ? ToExistingLineup(xi.Lineup, matrix)
                : ToNormalLineup(xi.Lineup, matrix);
            var beam = new List<Lineup> { baseline };
            var seen = new HashSet<string>(StringComparer.Ordinal) { Signature(baseline) };
            TacticalCandidate? localBest = await EvaluateAndStore(baseline);
            evaluated++;

            for (var iteration = 1; iteration <= maxIterations; iteration++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                iterations = Math.Max(iterations, iteration);
                progress?.Invoke(iteration, maxIterations, evaluated, retained);

                var frontier = new List<Lineup>();
                foreach (var lineup in beam)
                {
                    foreach (var slot in lineup.Slots.Where(s => s.PlayerId > 0))
                    {
                        var allowed = matrix.Slots.FirstOrDefault(s => s.PositionCode == slot.Code && s.PlayerId == slot.PlayerId)?.AllowedOrders
                            ?? [PlayerOrder.Normal];
                        foreach (var order in allowed)
                        {
                            if (order == slot.Order) continue;
                            var next = lineup with
                            {
                                Slots = lineup.Slots
                                    .Select(s => s.PlayerId == slot.PlayerId && s.Code == slot.Code ? s with { Order = order } : s)
                                    .ToList()
                            };
                            var key = Signature(next);
                            if (seen.Add(key)) frontier.Add(next);
                        }
                    }
                }

                if (frontier.Count == 0)
                {
                    converged = true;
                    break;
                }

                var scored = new List<TacticalCandidate>(frontier.Count);
                foreach (var candidate in frontier)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    scored.Add(await EvaluateAndStore(candidate));
                    evaluated++;
                }

                var ranked = scored
                    .OrderByDescending(x => x.TacticalScore)
                    .ThenBy(x => Signature(x.Lineup), StringComparer.Ordinal)
                    .Take(beamWidth)
                    .ToList();

                retained += ranked.Count;
                if (ranked.Count == 0) { converged = true; break; }

                var previousScore = localBest!.TacticalScore;
                localBest = Better(localBest, ranked[0]);
                beam = ranked.Select(x => x.Lineup).ToList();

                if (localBest.TacticalScore <= previousScore + 1e-9)
                {
                    converged = true;
                    break;
                }
            }

            globalBest = globalBest is null ? localBest : Better(globalBest, localBest!);
        }

        var topCandidates = database
            .OrderByDescending(x => x.TacticalScore)
            .ThenBy(x => x.Lineup.Formation, StringComparer.Ordinal)
            .ThenBy(x => Signature(x.Lineup), StringComparer.Ordinal)
            .Take(CandidateDatabaseCapacity)
            .ToList();

        return new M6OptimizationResult(globalBest, topCandidates, iterations, evaluated, retained, converged);

        async Task<TacticalCandidate> EvaluateAndStore(Lineup lineup)
        {
            var result = await evaluator(lineup, cancellationToken);
            ArgumentNullException.ThrowIfNull(result);
            var key = Signature(result.Lineup);
            if (seenDatabase.Add(key))
            {
                database.Add(result);
                database.Sort((a, b) => b.TacticalScore.CompareTo(a.TacticalScore));
                if (database.Count > CandidateDatabaseCapacity) database.RemoveAt(database.Count - 1);
            }
            return result;
        }
    }

    private static Lineup ToNormalLineup(Lineup lineup, BehaviourCandidateSet matrix)
    {
        var valid = matrix.Slots.Select(x => (x.PositionCode, x.PlayerId)).ToHashSet();
        return lineup with
        {
            Slots = lineup.Slots
                .Select(s => valid.Contains((s.Code, s.PlayerId)) ? s with { Order = PlayerOrder.Normal } : s)
                .ToList()
        };
    }

    private static Lineup ToExistingLineup(Lineup lineup, BehaviourCandidateSet matrix)
    {
        var valid = matrix.Slots.Select(x => (x.PositionCode, x.PlayerId)).ToHashSet();
        return lineup with
        {
            Slots = lineup.Slots
                .Where(s => s.PlayerId <= 0 || valid.Contains((s.Code, s.PlayerId)))
                .ToList()
        };
    }

    private static TacticalCandidate Better(TacticalCandidate a, TacticalCandidate b)
        => b.TacticalScore > a.TacticalScore + 1e-9 ||
           Math.Abs(b.TacticalScore - a.TacticalScore) <= 1e-9 && string.CompareOrdinal(Signature(b.Lineup), Signature(a.Lineup)) < 0
            ? b : a;

    private static string Signature(Lineup lineup)
        => string.Join(";", lineup.Slots
            .OrderBy(s => s.Code, StringComparer.Ordinal)
            .ThenBy(s => s.PlayerId)
            .Select(s => $"{s.Code}:{s.PlayerId}:{(int)s.Order}"));
}

public sealed record M6OptimizationResult(
    TacticalCandidate? BestCandidate,
    IReadOnlyList<TacticalCandidate> TopCandidates,
    int Iterations,
    int EvaluatedCandidates,
    int RetainedCandidates,
    bool Converged);
