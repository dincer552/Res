using System;
using System.Collections.Generic;

namespace HattrickAI.V5.Core;

/// <summary>
/// Integration bridge for Motor 2.
/// Motor 1 supplies positional suitability; an optional opponent profile is
/// supplied before XI selection so the chosen XI is opponent-aware without
/// creating a circular dependency on our own final rating.
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

    public Lineup BuildBestXI(
        string teamName,
        IReadOnlyList<Player> players,
        string formation,
        OpponentMatchProfile? opponent = null)
        => _optimizer.Optimize(teamName, players, formation, opponent);
}
