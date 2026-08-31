using System;
using System.Collections.Generic;

namespace HattrickAI.V5.Core;

/// <summary>
/// Motor 2 integration bridge.
/// Keeps the public contract explicit: players are the second argument and
/// the opponent profile is optional and supplied before XI selection.
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
    {
        ArgumentNullException.ThrowIfNull(players);
        return _optimizer.Optimize(teamName, players, formation, opponent);
    }
}
