namespace HattrickAI.V5.Core;

/// <summary>
/// Motor 5-9 arasındaki aday değerlendirme döngüsünün altyapısı.
/// Her turda aynı aday için pozisyon → davranış → rating → eşleşme → skor
/// zinciri çalıştırılır. Daha iyi aday bulunursa yeni tur başlatılır.
///
/// Bu sınıf henüz canlı AnalysisService'e bağlanmaz; amaç döngü sözleşmesini
/// sabitlemek ve motorlar tamamlandıkça tek tek bağlanabilmektir.
/// </summary>
public sealed class AnalysisOptimizationLoop
{
    public const int DefaultMaxIterations = 50;

    public async Task<OptimizationLoopResult> RunAsync(
        FormationCandidateSet formations,
        Func<FormationCandidate, CancellationToken, Task<IReadOnlyList<TacticalCandidate>>> evaluate,
        int maxIterations = DefaultMaxIterations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(formations);
        ArgumentNullException.ThrowIfNull(evaluate);
        if (maxIterations < 1) throw new ArgumentOutOfRangeException(nameof(maxIterations));

        TacticalCandidate? best = null;
        var iterations = 0;
        var evaluatedFormations = new HashSet<string>(StringComparer.Ordinal);

        while (iterations < maxIterations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            iterations++;

            var improved = false;
            foreach (var formation in formations.Candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var key = formation.Formation + "|" + string.Join(",", formation.SlotCodes);
                if (!evaluatedFormations.Add(key)) continue;

                var candidates = await evaluate(formation, cancellationToken);
                foreach (var candidate in candidates)
                {
                    if (best is null || candidate.TacticalScore > best.TacticalScore)
                    {
                        best = candidate;
                        improved = true;
                    }
                }
            }

            // Tüm adaylar aynı kaldıysa veya iyileşme yoksa döngü durur.
            // Motor 9 gelecekte yeni bir aday seti ürettiğinde bu metot tekrar
            // çağrılabilir; böylece sonsuz döngü riski oluşmaz.
            if (!improved) break;
        }

        return new OptimizationLoopResult(best, iterations, evaluatedFormations.Count);
    }
}

public sealed record OptimizationLoopResult(
    TacticalCandidate? BestCandidate,
    int Iterations,
    int EvaluatedCandidates);
