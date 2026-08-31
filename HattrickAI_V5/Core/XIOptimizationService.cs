using System;
using System.Collections.Generic;

namespace HattrickAI.V5.Core;

/// <summary>
/// Motor 2 integration bridge: Motor 1 positional XI first, then optional
/// opponent-aware refinement using real regional ratings. RP is not a decision input.
/// </summary>
public sealed class XIOptimizationService
{
    private readonly XIOptimizer _optimizer;
    private readonly Motor2OpponentAwareRefiner _refiner = new();

    public XIOptimizationService()
        : this(new PositionSuitabilityEngine()) { }

    public XIOptimizationService(PositionSuitabilityEngine suitability)
        => _optimizer = new XIOptimizer(suitability ?? throw new ArgumentNullException(nameof(suitability)));

    public Lineup BuildBestXI(string teamName, IReadOnlyList<Player> players, string formation,
        OpponentMatchProfile? opponent = null)
    {
        ArgumentNullException.ThrowIfNull(players);
        var initial = _optimizer.Optimize(teamName, players, formation, opponent);
        return opponent is null ? initial : _refiner.Refine(initial, players, opponent);
    }
}
