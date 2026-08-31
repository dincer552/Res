using System;
using System.Collections.Generic;

namespace HattrickAI.V5.Core;

/// <summary>
/// Integration bridge for Motor 2.
/// Keeps XI selection separate from rating calculation and from future
/// behaviour optimization. The bridge is intentionally thin so the
/// RegionalRatingEngine remains the single rating authority.
/// </summary>
public sealed class XIOptimizationService
{
    private readonly XIOptimizer _optimizer;

    public XIOptimizationService()
        : this(new PositionSuitabilityEngine())
    {
    }

    public XIOptimizationService(PositionSuitabilityEngine suitability)
    {
        _optimizer = new XIOptimizer(suitability ?? throw new ArgumentNullException(nameof(suitability)));
    }

    public Lineup BuildBestXI(string teamName, IReadOnlyList<Player> players, string formation)
        => _optimizer.Optimize(teamName, players, formation);
}
